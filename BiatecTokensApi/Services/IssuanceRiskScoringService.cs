using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Deterministic compliance risk scoring service for token issuance workflows.
    /// </summary>
    /// <remarks>
    /// Computes a normalized aggregate risk score (0–100) from three independent components:
    /// <list type="bullet">
    ///   <item><description>KYC completeness and verification status (0–40 points)</description></item>
    ///   <item><description>Sanctions screening outcome and confidence (0–30 points)</description></item>
    ///   <item><description>Jurisdiction risk level (0–30 points)</description></item>
    /// </list>
    ///
    /// Risk band thresholds (deterministic, configuration-independent defaults):
    /// <list type="bullet">
    ///   <item><description>0–39  → Low  → decision: allow</description></item>
    ///   <item><description>40–69 → Medium → decision: review</description></item>
    ///   <item><description>70–100 → High → decision: deny</description></item>
    /// </list>
    /// </remarks>
    public class IssuanceRiskScoringService : IIssuanceRiskScoringService
    {
        // Threshold boundaries (deterministic defaults)
        public const int LowRiskMaxScore = 39;
        public const int MediumRiskMaxScore = 69;

        // KYC component scoring constants (max 40 points)
        public const int KycStatusVerifiedPenalty = 0;
        public const int KycStatusInProgressPenalty = 15;
        public const int KycStatusPendingPenalty = 25;
        public const int KycStatusFailedPenalty = 40;
        public const int KycStatusUnknownPenalty = 40;
        public const int KycCompletenessHighThreshold = 90;   // ≥90% completeness → no extra penalty
        public const int KycCompletenessMediumThreshold = 50; // 50-89% → +10 penalty
        public const int KycCompletenessLowPenalty = 10;      // 50-89% completeness penalty
        public const int KycCompletenessVeryLowPenalty = 20;  // <50% completeness penalty

        // Sanctions component scoring constants (max 30 points)
        public const int SanctionsNoScreenPenalty = 20;      // not screened at all → moderate penalty
        public const int SanctionsCleanPenalty = 0;           // screened, no hit → no penalty
        public const int SanctionsLowConfidencePenalty = 10; // possible hit, low confidence (<0.3)
        public const int SanctionsMediumConfidencePenalty = 20; // possible hit, medium confidence (0.3–0.7)
        public const int SanctionsHighConfidencePenalty = 30;   // confirmed or high confidence (>0.7)
        public const double SanctionsLowConfidenceThreshold = 0.3;
        public const double SanctionsHighConfidenceThreshold = 0.7;

        // Jurisdiction component scoring constants (max 30 points)
        public const int JurisdictionLowPenalty = 0;
        public const int JurisdictionMediumPenalty = 10;
        public const int JurisdictionHighPenalty = 20;
        public const int JurisdictionProhibitedPenalty = 30;

        private readonly ILogger<IssuanceRiskScoringService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IssuanceRiskScoringService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for audit tracing</param>
        public IssuanceRiskScoringService(ILogger<IssuanceRiskScoringService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<IssuanceRiskEvaluationResponse> EvaluateAsync(IssuanceRiskEvaluationRequest request)
        {
            var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? Guid.NewGuid().ToString()
                : request.CorrelationId;

            try
            {
                // Validate required fields
                var validationError = ValidateRequest(request, correlationId);
                if (validationError != null)
                {
                    return Task.FromResult(validationError);
                }

                _logger.LogInformation(
                    "Issuance risk evaluation started: OrganizationId={OrganizationId}, IssuerId={IssuerId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    LoggingHelper.SanitizeLogInput(request.IssuerId),
                    LoggingHelper.SanitizeLogInput(correlationId)
                );

                // Score each component independently
                var (kycScore, kycEvidence) = ScoreKyc(request.KycEvidence);
                var (sanctionsScore, sanctionsEvidence) = ScoreSanctions(request.SanctionsEvidence);
                var (jurisdictionScore, jurisdictionEvidence) = ScoreJurisdiction(request.JurisdictionEvidence);

                var componentScores = new RiskComponentScores
                {
                    KycScore = kycScore,
                    SanctionsScore = sanctionsScore,
                    JurisdictionScore = jurisdictionScore
                };

                var aggregateScore = componentScores.Total;
                var riskBand = ClassifyRiskBand(aggregateScore);
                var decision = MapDecision(riskBand);

                // Collect reason codes ordered by contribution (highest penalty first)
                var reasonCodes = BuildReasonCodes(kycEvidence, sanctionsEvidence, jurisdictionEvidence,
                    kycScore, sanctionsScore, jurisdictionScore);

                var primaryReason = BuildPrimaryReason(decision, reasonCodes, aggregateScore);
                var reviewerRequirements = BuildReviewerRequirements(decision, kycEvidence, sanctionsEvidence, jurisdictionEvidence);

                _logger.LogInformation(
                    "Issuance risk evaluation completed: Decision={Decision}, AggregateScore={Score}, RiskBand={Band}, CorrelationId={CorrelationId}",
                    decision,
                    aggregateScore,
                    riskBand,
                    LoggingHelper.SanitizeLogInput(correlationId)
                );

                var response = new IssuanceRiskEvaluationResponse
                {
                    Success = true,
                    CorrelationId = correlationId,
                    EvaluatedAt = DateTime.UtcNow,
                    Decision = decision,
                    AggregateRiskScore = aggregateScore,
                    RiskBand = riskBand,
                    ReasonCodes = reasonCodes,
                    PrimaryReason = primaryReason,
                    PolicyVersion = "1.0.0",
                    KycEvidence = kycEvidence,
                    SanctionsEvidence = sanctionsEvidence,
                    JurisdictionEvidence = jurisdictionEvidence,
                    ComponentScores = componentScores,
                    ReviewerRequirements = reviewerRequirements
                };

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Issuance risk evaluation failed unexpectedly: CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(correlationId)
                );

                return Task.FromResult(new IssuanceRiskEvaluationResponse
                {
                    Success = false,
                    CorrelationId = correlationId,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred during compliance risk evaluation.",
                    EvaluatedAt = DateTime.UtcNow
                });
            }
        }

        // ── Validation ──────────────────────────────────────────────────────────────

        private static IssuanceRiskEvaluationResponse? ValidateRequest(
            IssuanceRiskEvaluationRequest request, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(request.OrganizationId))
            {
                return ValidationError(correlationId, ErrorCodes.MISSING_REQUIRED_FIELD,
                    "OrganizationId is required.", "MISSING_ORGANIZATION_ID");
            }

            if (string.IsNullOrWhiteSpace(request.IssuerId))
            {
                return ValidationError(correlationId, ErrorCodes.MISSING_REQUIRED_FIELD,
                    "IssuerId is required.", "MISSING_ISSUER_ID");
            }

            if (string.IsNullOrWhiteSpace(request.JurisdictionEvidence?.JurisdictionCode))
            {
                return ValidationError(correlationId, ErrorCodes.MISSING_REQUIRED_FIELD,
                    "JurisdictionEvidence.JurisdictionCode is required.", "MISSING_JURISDICTION_CODE");
            }

            var completeness = request.KycEvidence?.CompletenessPercent ?? 0;
            if (completeness < 0 || completeness > 100)
            {
                return ValidationError(correlationId, ErrorCodes.INVALID_REQUEST,
                    "KycEvidence.CompletenessPercent must be between 0 and 100.", "INVALID_KYC_COMPLETENESS");
            }

            var hitConfidence = request.SanctionsEvidence?.HitConfidence ?? 0;
            if (hitConfidence < 0.0 || hitConfidence > 1.0)
            {
                return ValidationError(correlationId, ErrorCodes.INVALID_REQUEST,
                    "SanctionsEvidence.HitConfidence must be between 0.0 and 1.0.", "INVALID_SANCTIONS_CONFIDENCE");
            }

            return null;
        }

        private static IssuanceRiskEvaluationResponse ValidationError(
            string correlationId, string errorCode, string message, string reasonCode)
        {
            return new IssuanceRiskEvaluationResponse
            {
                Success = false,
                CorrelationId = correlationId,
                ErrorCode = errorCode,
                ErrorMessage = message,
                ReasonCodes = new List<string> { reasonCode },
                EvaluatedAt = DateTime.UtcNow
            };
        }

        // ── KYC Scoring ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Scores the KYC component. Returns a penalty score 0–40 and a populated evidence block.
        /// </summary>
        public static (int score, KycEvidenceBlock evidence) ScoreKyc(KycEvidenceInput input)
        {
            var evidence = new KycEvidenceBlock
            {
                Status = input.Status,
                CompletenessPercent = Math.Clamp(input.CompletenessPercent, 0, 100),
                Provider = input.Provider,
                VerificationDate = input.VerificationDate
            };

            var issueCodes = new List<string>();

            // Status penalty (0–40 points)
            int statusPenalty = input.Status switch
            {
                IssuanceKycStatus.Verified => KycStatusVerifiedPenalty,
                IssuanceKycStatus.InProgress => KycStatusInProgressPenalty,
                IssuanceKycStatus.Pending => KycStatusPendingPenalty,
                IssuanceKycStatus.Failed => KycStatusFailedPenalty,
                _ => KycStatusUnknownPenalty
            };

            if (input.Status == IssuanceKycStatus.Failed)
                issueCodes.Add("KYC_VERIFICATION_FAILED");
            else if (input.Status == IssuanceKycStatus.Unknown)
                issueCodes.Add("KYC_STATUS_UNKNOWN");
            else if (input.Status == IssuanceKycStatus.Pending)
                issueCodes.Add("KYC_VERIFICATION_PENDING");
            else if (input.Status == IssuanceKycStatus.InProgress)
                issueCodes.Add("KYC_VERIFICATION_IN_PROGRESS");

            // Completeness penalty (additional 0–20 points on top of status)
            int completenessPenalty = 0;
            if (input.Status == IssuanceKycStatus.Verified || input.Status == IssuanceKycStatus.InProgress)
            {
                var completeness = Math.Clamp(input.CompletenessPercent, 0, 100);
                if (completeness < KycCompletenessMediumThreshold)
                {
                    completenessPenalty = KycCompletenessVeryLowPenalty;
                    issueCodes.Add("KYC_COMPLETENESS_VERY_LOW");
                }
                else if (completeness < KycCompletenessHighThreshold)
                {
                    completenessPenalty = KycCompletenessLowPenalty;
                    issueCodes.Add("KYC_COMPLETENESS_INCOMPLETE");
                }
            }

            // Cap total KYC contribution at 40
            var totalKycPenalty = Math.Min(40, statusPenalty + completenessPenalty);

            evidence.RiskPenalty = totalKycPenalty;
            evidence.IssueCodes = issueCodes;

            return (totalKycPenalty, evidence);
        }

        // ── Sanctions Scoring ────────────────────────────────────────────────────────

        /// <summary>
        /// Scores the sanctions component. Returns a penalty score 0–30 and a populated evidence block.
        /// </summary>
        public static (int score, SanctionsEvidenceBlock evidence) ScoreSanctions(SanctionsEvidenceInput input)
        {
            var evidence = new SanctionsEvidenceBlock
            {
                Screened = input.Screened,
                HitDetected = input.HitDetected,
                HitConfidence = Math.Clamp(input.HitConfidence, 0.0, 1.0),
                ScreeningProvider = input.ScreeningProvider,
                ScreeningDate = input.ScreeningDate
            };

            var issueCodes = new List<string>();
            int penalty;

            if (!input.Screened)
            {
                // Not screened at all – moderate penalty to flag missing step
                penalty = SanctionsNoScreenPenalty;
                issueCodes.Add("SANCTIONS_NOT_SCREENED");
            }
            else if (!input.HitDetected)
            {
                // Screened and clean
                penalty = SanctionsCleanPenalty;
            }
            else
            {
                // Hit detected; score by confidence level
                var confidence = Math.Clamp(input.HitConfidence, 0.0, 1.0);
                if (confidence > SanctionsHighConfidenceThreshold)
                {
                    penalty = SanctionsHighConfidencePenalty;
                    issueCodes.Add("SANCTIONS_HIT_CONFIRMED");
                }
                else if (confidence >= SanctionsLowConfidenceThreshold)
                {
                    penalty = SanctionsMediumConfidencePenalty;
                    issueCodes.Add("SANCTIONS_HIT_MEDIUM_CONFIDENCE");
                }
                else
                {
                    penalty = SanctionsLowConfidencePenalty;
                    issueCodes.Add("SANCTIONS_HIT_LOW_CONFIDENCE");
                }
            }

            evidence.RiskPenalty = penalty;
            evidence.IssueCodes = issueCodes;

            return (penalty, evidence);
        }

        // ── Jurisdiction Scoring ─────────────────────────────────────────────────────

        /// <summary>
        /// Scores the jurisdiction component. Returns a penalty score 0–30 and a populated evidence block.
        /// </summary>
        public static (int score, JurisdictionEvidenceBlock evidence) ScoreJurisdiction(JurisdictionEvidenceInput input)
        {
            var evidence = new JurisdictionEvidenceBlock
            {
                JurisdictionCode = input.JurisdictionCode,
                RiskLevel = input.RiskLevel,
                MicaCompliant = input.MicaCompliant
            };

            var issueCodes = new List<string>();

            int penalty = input.RiskLevel switch
            {
                JurisdictionRiskLevel.Low => JurisdictionLowPenalty,
                JurisdictionRiskLevel.Medium => JurisdictionMediumPenalty,
                JurisdictionRiskLevel.High => JurisdictionHighPenalty,
                JurisdictionRiskLevel.Prohibited => JurisdictionProhibitedPenalty,
                _ => JurisdictionMediumPenalty
            };

            if (input.RiskLevel == JurisdictionRiskLevel.Prohibited)
                issueCodes.Add("JURISDICTION_PROHIBITED");
            else if (input.RiskLevel == JurisdictionRiskLevel.High)
                issueCodes.Add("JURISDICTION_HIGH_RISK");
            else if (!input.MicaCompliant && input.RiskLevel != JurisdictionRiskLevel.Low)
                issueCodes.Add("JURISDICTION_NOT_MICA_COMPLIANT");

            evidence.RiskPenalty = penalty;
            evidence.IssueCodes = issueCodes;

            return (penalty, evidence);
        }

        // ── Classification ───────────────────────────────────────────────────────────

        /// <summary>
        /// Maps an aggregate score to a risk band (deterministic thresholds).
        /// </summary>
        public static IssuanceRiskBand ClassifyRiskBand(int aggregateScore)
        {
            if (aggregateScore <= LowRiskMaxScore) return IssuanceRiskBand.Low;
            if (aggregateScore <= MediumRiskMaxScore) return IssuanceRiskBand.Medium;
            return IssuanceRiskBand.High;
        }

        /// <summary>
        /// Maps a risk band to a normalized decision string.
        /// </summary>
        public static string MapDecision(IssuanceRiskBand band) => band switch
        {
            IssuanceRiskBand.Low => "allow",
            IssuanceRiskBand.Medium => "review",
            IssuanceRiskBand.High => "deny",
            _ => "review"
        };

        // ── Reason Building ──────────────────────────────────────────────────────────

        private static List<string> BuildReasonCodes(
            KycEvidenceBlock kyc,
            SanctionsEvidenceBlock sanctions,
            JurisdictionEvidenceBlock jurisdiction,
            int kycScore, int sanctionsScore, int jurisdictionScore)
        {
            // Order by contribution descending (highest-risk component first)
            var ordered = new[]
            {
                (score: kycScore, codes: kyc.IssueCodes),
                (score: sanctionsScore, codes: sanctions.IssueCodes),
                (score: jurisdictionScore, codes: jurisdiction.IssueCodes)
            }.OrderByDescending(x => x.score);

            var result = new List<string>();
            foreach (var item in ordered)
                result.AddRange(item.codes);

            return result;
        }

        private static string BuildPrimaryReason(string decision, List<string> reasonCodes, int aggregateScore)
        {
            if (decision == "allow")
                return "All compliance checks passed. Issuance is permitted.";

            if (!reasonCodes.Any())
                return $"Aggregate risk score {aggregateScore} requires {decision}.";

            var topCode = reasonCodes.First();
            return topCode switch
            {
                "KYC_VERIFICATION_FAILED" => "KYC verification has failed. Issuance cannot proceed until KYC is resolved.",
                "KYC_STATUS_UNKNOWN" => "KYC verification status is unknown. Please complete KYC before proceeding.",
                "KYC_VERIFICATION_PENDING" => "KYC verification is pending. Issuance is on hold pending KYC completion.",
                "KYC_VERIFICATION_IN_PROGRESS" => "KYC verification is in progress. Issuance may proceed after review.",
                "KYC_COMPLETENESS_VERY_LOW" => "KYC data completeness is below 50%. Additional information is required.",
                "KYC_COMPLETENESS_INCOMPLETE" => "KYC data is incomplete (50–89%). Please provide additional KYC details.",
                "SANCTIONS_HIT_CONFIRMED" => "A confirmed sanctions hit has been detected. Issuance is denied.",
                "SANCTIONS_HIT_MEDIUM_CONFIDENCE" => "A possible sanctions hit requires manual review before issuance.",
                "SANCTIONS_HIT_LOW_CONFIDENCE" => "A low-confidence sanctions match was flagged. Human review is recommended.",
                "SANCTIONS_NOT_SCREENED" => "Sanctions screening has not been performed. Screening is required before issuance.",
                "JURISDICTION_PROHIBITED" => "The issuer's jurisdiction is prohibited. Issuance cannot proceed.",
                "JURISDICTION_HIGH_RISK" => "The issuer's jurisdiction is high-risk. Manual review is required.",
                "JURISDICTION_NOT_MICA_COMPLIANT" => "The jurisdiction is not MICA-compliant. Additional review may be required.",
                _ => $"Compliance evaluation resulted in '{decision}' with aggregate risk score {aggregateScore}."
            };
        }

        private static List<string>? BuildReviewerRequirements(
            string decision,
            KycEvidenceBlock kyc,
            SanctionsEvidenceBlock sanctions,
            JurisdictionEvidenceBlock jurisdiction)
        {
            if (decision != "review") return null;

            var requirements = new List<string>();

            if (kyc.IssueCodes.Any())
                requirements.Add("KYC compliance officer review required");

            if (sanctions.IssueCodes.Any())
                requirements.Add("AML/sanctions analyst review required");

            if (jurisdiction.IssueCodes.Any())
                requirements.Add("Jurisdictional compliance review required");

            return requirements.Count > 0 ? requirements : null;
        }
    }
}

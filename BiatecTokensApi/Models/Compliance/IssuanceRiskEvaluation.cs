using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Risk band categories for issuance compliance decisions
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IssuanceRiskBand
    {
        /// <summary>
        /// Low risk (aggregate score 0-39): issuance allowed
        /// </summary>
        Low,

        /// <summary>
        /// Medium risk (aggregate score 40-69): issuance requires human review
        /// </summary>
        Medium,

        /// <summary>
        /// High risk (aggregate score 70-100): issuance denied
        /// </summary>
        High
    }

    /// <summary>
    /// KYC verification status for issuance evaluation
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IssuanceKycStatus
    {
        /// <summary>
        /// KYC verification is fully completed and verified
        /// </summary>
        Verified,

        /// <summary>
        /// KYC verification is in progress
        /// </summary>
        InProgress,

        /// <summary>
        /// KYC verification is pending submission
        /// </summary>
        Pending,

        /// <summary>
        /// KYC verification has failed
        /// </summary>
        Failed,

        /// <summary>
        /// KYC status is unknown or not provided
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Jurisdiction risk level for issuance evaluation
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum JurisdictionRiskLevel
    {
        /// <summary>
        /// Low-risk jurisdiction (e.g., EU MICA-compliant countries)
        /// </summary>
        Low,

        /// <summary>
        /// Medium-risk jurisdiction
        /// </summary>
        Medium,

        /// <summary>
        /// High-risk jurisdiction (FATF monitored)
        /// </summary>
        High,

        /// <summary>
        /// Prohibited jurisdiction - issuance not permitted
        /// </summary>
        Prohibited
    }

    /// <summary>
    /// Request to evaluate compliance risk for a token issuance workflow
    /// </summary>
    public class IssuanceRiskEvaluationRequest
    {
        /// <summary>
        /// Unique correlation identifier for audit tracing. Auto-generated if not provided.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Identifier of the organization requesting token issuance
        /// </summary>
        public string OrganizationId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the issuer (authenticated actor address or user ID)
        /// </summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>
        /// KYC evidence for this issuance evaluation
        /// </summary>
        public KycEvidenceInput KycEvidence { get; set; } = new();

        /// <summary>
        /// Sanctions screening evidence for this issuance evaluation
        /// </summary>
        public SanctionsEvidenceInput SanctionsEvidence { get; set; } = new();

        /// <summary>
        /// Jurisdiction information for this issuance evaluation
        /// </summary>
        public JurisdictionEvidenceInput JurisdictionEvidence { get; set; } = new();

        /// <summary>
        /// Token issuance details for context (optional, not scored)
        /// </summary>
        public IssuanceContextInput? IssuanceContext { get; set; }

        /// <summary>
        /// Optional policy exceptions or overrides to apply
        /// </summary>
        public List<string>? PolicyExceptions { get; set; }
    }

    /// <summary>
    /// KYC evidence inputs for risk evaluation
    /// </summary>
    public class KycEvidenceInput
    {
        /// <summary>
        /// Current KYC verification status
        /// </summary>
        public IssuanceKycStatus Status { get; set; } = IssuanceKycStatus.Unknown;

        /// <summary>
        /// KYC completeness percentage (0-100). Represents how complete the KYC data is.
        /// </summary>
        public int CompletenessPercent { get; set; }

        /// <summary>
        /// Name of the KYC provider or verification service
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Date when KYC verification was completed (UTC)
        /// </summary>
        public DateTime? VerificationDate { get; set; }

        /// <summary>
        /// Reference identifier for the KYC report
        /// </summary>
        public string? ReportReferenceId { get; set; }
    }

    /// <summary>
    /// Sanctions screening evidence inputs for risk evaluation
    /// </summary>
    public class SanctionsEvidenceInput
    {
        /// <summary>
        /// Whether sanctions screening has been performed
        /// </summary>
        public bool Screened { get; set; }

        /// <summary>
        /// Whether any sanctions hit was detected
        /// </summary>
        public bool HitDetected { get; set; }

        /// <summary>
        /// Confidence level of the sanctions hit (0.0-1.0). 0 = no hit or no screen, 1.0 = confirmed hit.
        /// </summary>
        public double HitConfidence { get; set; }

        /// <summary>
        /// Name of the sanctions screening provider
        /// </summary>
        public string? ScreeningProvider { get; set; }

        /// <summary>
        /// Date when screening was performed (UTC)
        /// </summary>
        public DateTime? ScreeningDate { get; set; }

        /// <summary>
        /// Reference ID of the screening report
        /// </summary>
        public string? ReportReferenceId { get; set; }
    }

    /// <summary>
    /// Jurisdiction evidence inputs for risk evaluation
    /// </summary>
    public class JurisdictionEvidenceInput
    {
        /// <summary>
        /// ISO 3166-1 alpha-2 jurisdiction code (e.g., "DE", "US", "SG")
        /// </summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>
        /// Assessed risk level for the jurisdiction
        /// </summary>
        public JurisdictionRiskLevel RiskLevel { get; set; } = JurisdictionRiskLevel.Medium;

        /// <summary>
        /// Whether the jurisdiction is MICA-compliant
        /// </summary>
        public bool MicaCompliant { get; set; }

        /// <summary>
        /// Applicable regulatory frameworks (e.g., ["MICA", "FATF"])
        /// </summary>
        public List<string> RegulatoryFrameworks { get; set; } = new();
    }

    /// <summary>
    /// Token issuance context (informational, not scored)
    /// </summary>
    public class IssuanceContextInput
    {
        /// <summary>
        /// Type of token being issued (e.g., "ARC3", "ASA", "ERC20")
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Requested issuance amount
        /// </summary>
        public decimal? IssuanceAmount { get; set; }

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Network for issuance (e.g., "mainnet-v1.0", "voimain-v1.0")
        /// </summary>
        public string? Network { get; set; }
    }

    /// <summary>
    /// Response from issuance compliance risk evaluation
    /// </summary>
    public class IssuanceRiskEvaluationResponse
    {
        /// <summary>
        /// Whether the evaluation completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if evaluation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Machine-readable error code if evaluation failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Correlation ID for audit tracing (matches request CorrelationId or auto-generated)
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when this evaluation was produced (UTC)
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Normalized compliance decision: "allow", "review", or "deny"
        /// </summary>
        public string Decision { get; set; } = string.Empty;

        /// <summary>
        /// Aggregate risk score (0-100). Lower is better.
        /// 0-39: Low risk (allow), 40-69: Medium risk (review), 70-100: High risk (deny)
        /// </summary>
        public int AggregateRiskScore { get; set; }

        /// <summary>
        /// Risk band category derived from aggregate score
        /// </summary>
        public IssuanceRiskBand RiskBand { get; set; }

        /// <summary>
        /// Machine-readable reason codes explaining the decision.
        /// Ordered by priority (highest impact first).
        /// </summary>
        public List<string> ReasonCodes { get; set; } = new();

        /// <summary>
        /// Human-readable primary reason for the decision
        /// </summary>
        public string PrimaryReason { get; set; } = string.Empty;

        /// <summary>
        /// Policy version used for this evaluation
        /// </summary>
        public string PolicyVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Structured KYC evidence block
        /// </summary>
        public KycEvidenceBlock KycEvidence { get; set; } = new();

        /// <summary>
        /// Structured sanctions evidence block
        /// </summary>
        public SanctionsEvidenceBlock SanctionsEvidence { get; set; } = new();

        /// <summary>
        /// Structured jurisdiction evidence block
        /// </summary>
        public JurisdictionEvidenceBlock JurisdictionEvidence { get; set; } = new();

        /// <summary>
        /// Individual risk component scores for transparency
        /// </summary>
        public RiskComponentScores ComponentScores { get; set; } = new();

        /// <summary>
        /// Optional reviewer requirements if decision is "review"
        /// </summary>
        public List<string>? ReviewerRequirements { get; set; }
    }

    /// <summary>
    /// Structured KYC evidence block in the evaluation response
    /// </summary>
    public class KycEvidenceBlock
    {
        /// <summary>
        /// Evaluated KYC status
        /// </summary>
        public IssuanceKycStatus Status { get; set; }

        /// <summary>
        /// KYC completeness percentage used in scoring
        /// </summary>
        public int CompletenessPercent { get; set; }

        /// <summary>
        /// KYC provider name (if provided)
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// KYC verification date (if provided)
        /// </summary>
        public DateTime? VerificationDate { get; set; }

        /// <summary>
        /// Risk penalty contributed by KYC (0-40 points)
        /// </summary>
        public int RiskPenalty { get; set; }

        /// <summary>
        /// Machine-readable codes for KYC issues
        /// </summary>
        public List<string> IssueCodes { get; set; } = new();
    }

    /// <summary>
    /// Structured sanctions evidence block in the evaluation response
    /// </summary>
    public class SanctionsEvidenceBlock
    {
        /// <summary>
        /// Whether sanctions screening was performed
        /// </summary>
        public bool Screened { get; set; }

        /// <summary>
        /// Whether a sanctions hit was detected
        /// </summary>
        public bool HitDetected { get; set; }

        /// <summary>
        /// Confidence level of the sanctions hit (0.0-1.0)
        /// </summary>
        public double HitConfidence { get; set; }

        /// <summary>
        /// Screening provider name (if provided)
        /// </summary>
        public string? ScreeningProvider { get; set; }

        /// <summary>
        /// Date of screening (if provided)
        /// </summary>
        public DateTime? ScreeningDate { get; set; }

        /// <summary>
        /// Risk penalty contributed by sanctions (0-30 points)
        /// </summary>
        public int RiskPenalty { get; set; }

        /// <summary>
        /// Machine-readable codes for sanctions issues
        /// </summary>
        public List<string> IssueCodes { get; set; } = new();
    }

    /// <summary>
    /// Structured jurisdiction evidence block in the evaluation response
    /// </summary>
    public class JurisdictionEvidenceBlock
    {
        /// <summary>
        /// Jurisdiction code evaluated
        /// </summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction risk level
        /// </summary>
        public JurisdictionRiskLevel RiskLevel { get; set; }

        /// <summary>
        /// Whether jurisdiction is MICA-compliant
        /// </summary>
        public bool MicaCompliant { get; set; }

        /// <summary>
        /// Risk penalty contributed by jurisdiction (0-30 points)
        /// </summary>
        public int RiskPenalty { get; set; }

        /// <summary>
        /// Machine-readable codes for jurisdiction issues
        /// </summary>
        public List<string> IssueCodes { get; set; } = new();
    }

    /// <summary>
    /// Individual risk component score breakdown for transparency and auditability
    /// </summary>
    public class RiskComponentScores
    {
        /// <summary>
        /// KYC component risk score (0-40)
        /// </summary>
        public int KycScore { get; set; }

        /// <summary>
        /// Sanctions component risk score (0-30)
        /// </summary>
        public int SanctionsScore { get; set; }

        /// <summary>
        /// Jurisdiction component risk score (0-30)
        /// </summary>
        public int JurisdictionScore { get; set; }

        /// <summary>
        /// Total aggregate score (KycScore + SanctionsScore + JurisdictionScore)
        /// </summary>
        public int Total => KycScore + SanctionsScore + JurisdictionScore;
    }
}

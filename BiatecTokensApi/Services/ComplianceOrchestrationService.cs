using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Orchestrates KYC and AML compliance checks, maintains an in-memory auditable decision trail,
    /// and enforces idempotency for repeated requests with the same key.
    /// </summary>
    public class ComplianceOrchestrationService : IComplianceOrchestrationService
    {
        private readonly IKycProvider _kycProvider;
        private readonly IAmlProvider _amlProvider;
        private readonly ILogger<ComplianceOrchestrationService> _logger;

        // Primary store: decisionId → decision
        private readonly ConcurrentDictionary<string, NormalizedComplianceDecision> _decisions = new();
        // Idempotency store: idempotencyKey → decisionId
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();
        // Subject index: subjectId → list of decisionIds (using ConcurrentBag for lock-free add)
        private readonly ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentBag<string>> _subjectIndex = new();

        public ComplianceOrchestrationService(
            IKycProvider kycProvider,
            IAmlProvider amlProvider,
            ILogger<ComplianceOrchestrationService> logger)
        {
            _kycProvider = kycProvider;
            _amlProvider = amlProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ComplianceCheckResponse> InitiateCheckAsync(
            InitiateComplianceCheckRequest request,
            string actorId,
            string correlationId)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return ErrorResponse(ErrorCodes.MISSING_REQUIRED_FIELD, "SubjectId is required.", correlationId);
            }
            if (string.IsNullOrWhiteSpace(request.ContextId))
            {
                return ErrorResponse(ErrorCodes.MISSING_REQUIRED_FIELD, "ContextId is required.", correlationId);
            }

            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"{request.SubjectId}:{request.ContextId}:{request.CheckType}"
                : request.IdempotencyKey;

            // Idempotency: return cached result if key already used
            if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId) &&
                _decisions.TryGetValue(existingId, out var existing))
            {
                _logger.LogInformation(
                    "Idempotent replay for key={Key}, DecisionId={DecisionId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(idempotencyKey),
                    LoggingHelper.SanitizeLogInput(existingId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return ToResponse(existing, isReplay: true);
            }

            var decisionId = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var metadata = request.SubjectMetadata ?? new Dictionary<string, string>();

            var decision = new NormalizedComplianceDecision
            {
                DecisionId = decisionId,
                SubjectId = request.SubjectId,
                ContextId = request.ContextId,
                CheckType = request.CheckType,
                SubjectType = request.SubjectType,
                State = ComplianceDecisionState.Pending,
                CorrelationId = correlationId,
                InitiatedAt = now
            };

            AppendAuditEvent(decision, "CheckInitiated", ComplianceDecisionState.Pending, correlationId, null,
                $"SubjectType={request.SubjectType}");

            _logger.LogInformation(
                "Initiating compliance check. DecisionId={DecisionId}, SubjectId={SubjectId}, CheckType={CheckType}, SubjectType={SubjectType}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.CheckType,
                request.SubjectType,
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                switch (request.CheckType)
                {
                    case ComplianceCheckType.Kyc:
                        await RunKycCheckAsync(decision, request.SubjectId, metadata, correlationId);
                        break;

                    case ComplianceCheckType.Aml:
                        await RunAmlCheckAsync(decision, request.SubjectId, metadata, correlationId);
                        break;

                    case ComplianceCheckType.Combined:
                    default:
                        await RunCombinedCheckAsync(decision, request.SubjectId, metadata, correlationId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during compliance check. DecisionId={DecisionId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(decisionId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                decision.State = ComplianceDecisionState.Error;
                decision.ProviderErrorCode = ComplianceProviderErrorCode.InternalError;
                decision.ReasonCode = "INTERNAL_ERROR";
                AppendAuditEvent(decision, "CheckError", ComplianceDecisionState.Error, correlationId, null, ex.Message);
            }

            // Set CompletedAt for terminal states
            if (IsTerminal(decision.State))
            {
                decision.CompletedAt = DateTimeOffset.UtcNow;

                // Set evidence expiry when a validity window is requested and the check was approved
                if (request.EvidenceValidityHours is > 0 && decision.State == ComplianceDecisionState.Approved)
                {
                    decision.EvidenceExpiresAt = decision.CompletedAt.Value.AddHours(request.EvidenceValidityHours.Value);
                }
            }

            // Persist
            _decisions[decisionId] = decision;
            _idempotencyIndex[idempotencyKey] = decisionId;
            _subjectIndex.GetOrAdd(request.SubjectId, _ => new System.Collections.Concurrent.ConcurrentBag<string>())
                         .Add(decisionId);

            return ToResponse(decision, isReplay: false);
        }

        /// <inheritdoc/>
        public Task<ComplianceCheckResponse> GetCheckStatusAsync(string decisionId)
        {
            if (_decisions.TryGetValue(decisionId, out var decision))
            {
                // Check freshness: if the evidence has passed its expiry window, transition to Expired
                if (decision.EvidenceExpiresAt.HasValue
                    && DateTimeOffset.UtcNow > decision.EvidenceExpiresAt.Value
                    && decision.State == ComplianceDecisionState.Approved)
                {
                    decision.State = ComplianceDecisionState.Expired;
                    decision.ReasonCode = "EVIDENCE_EXPIRED";
                    AppendAuditEvent(decision, "EvidenceExpired", ComplianceDecisionState.Expired,
                        decision.CorrelationId, null,
                        $"Evidence validity window expired at {decision.EvidenceExpiresAt.Value:O}");

                    _logger.LogInformation(
                        "Compliance decision evidence expired. DecisionId={DecisionId}, ExpiredAt={ExpiredAt}",
                        LoggingHelper.SanitizeLogInput(decisionId),
                        decision.EvidenceExpiresAt.Value);
                }

                return Task.FromResult(ToResponse(decision, isReplay: false));
            }

            return Task.FromResult(new ComplianceCheckResponse
            {
                Success = false,
                ErrorMessage = $"Decision '{decisionId}' not found.",
                ErrorCode = ErrorCodes.COMPLIANCE_CHECK_NOT_FOUND
            });
        }

        /// <inheritdoc/>
        public Task<ComplianceDecisionHistoryResponse> GetDecisionHistoryAsync(string subjectId)
        {
            if (!_subjectIndex.TryGetValue(subjectId, out var ids))
            {
                return Task.FromResult(new ComplianceDecisionHistoryResponse
                {
                    Success = true,
                    SubjectId = subjectId,
                    Decisions = new List<ComplianceCheckResponse>(),
                    TotalCount = 0
                });
            }

            List<ComplianceCheckResponse> responses = ids
                .Where(id => _decisions.ContainsKey(id))
                .Select(id =>
                {
                    _decisions.TryGetValue(id, out var d);
                    return d;
                })
                .Where(d => d != null)
                .Select(d => ToResponse(d!, isReplay: false))
                .OrderByDescending(r => r.InitiatedAt)
                .ToList();

            return Task.FromResult(new ComplianceDecisionHistoryResponse
            {
                Success = true,
                SubjectId = subjectId,
                Decisions = responses,
                TotalCount = responses.Count
            });
        }

        // ---------------------------------------------------------------------------
        // Private helpers
        // ---------------------------------------------------------------------------

        private const int AuditMessageContentPreviewLength = 80;

        /// <inheritdoc/>
        public Task<AppendReviewerNoteResponse> AppendReviewerNoteAsync(
            string decisionId,
            AppendReviewerNoteRequest request,
            string actorId,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
            {
                return Task.FromResult(new AppendReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "DecisionId is required.",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                return Task.FromResult(new AppendReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Note content is required.",
                    CorrelationId = correlationId
                });
            }

            if (!_decisions.TryGetValue(decisionId, out var decision))
            {
                return Task.FromResult(new AppendReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.COMPLIANCE_CHECK_NOT_FOUND,
                    ErrorMessage = $"Decision '{decisionId}' not found.",
                    CorrelationId = correlationId
                });
            }

            var note = new ComplianceReviewerNote
            {
                NoteId = Guid.NewGuid().ToString("N"),
                DecisionId = decisionId,
                ActorId = actorId,
                Content = request.Content,
                EvidenceReferences = request.EvidenceReferences ?? new Dictionary<string, string>(),
                AppendedAt = DateTimeOffset.UtcNow,
                CorrelationId = correlationId
            };

            decision.ReviewerNotes.Add(note);

            var sanitizedActor = LoggingHelper.SanitizeLogInput(actorId);
            var sanitizedPreview = LoggingHelper.SanitizeLogInput(
                request.Content[..Math.Min(AuditMessageContentPreviewLength, request.Content.Length)]);
            AppendAuditEvent(decision, "ReviewerNoteAppended", decision.State, correlationId, null,
                $"Note appended by {sanitizedActor}: {sanitizedPreview}");

            _logger.LogInformation(
                "Reviewer note appended. DecisionId={DecisionId}, NoteId={NoteId}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(note.NoteId),
                sanitizedActor,
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new AppendReviewerNoteResponse
            {
                Success = true,
                Note = note,
                CorrelationId = correlationId
            });
        }

        private async Task RunKycCheckAsync(
            NormalizedComplianceDecision decision,
            string subjectId,
            Dictionary<string, string> metadata,
            string correlationId)
        {
            var kycRequest = BuildKycRequest(subjectId, metadata);
            var (providerRefId, kycStatus, errorMessage) =
                await _kycProvider.StartVerificationAsync(subjectId, kycRequest, correlationId);

            decision.KycProviderReferenceId = providerRefId;
            decision.State = MapKycStatus(kycStatus);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                decision.ReasonCode = "KYC_PROVIDER_ERROR";
                decision.ProviderErrorCode = ComplianceProviderErrorCode.InternalError;
            }
            else if (decision.State == ComplianceDecisionState.Rejected)
            {
                decision.ReasonCode = "KYC_REJECTED";
            }
            else if (decision.State == ComplianceDecisionState.NeedsReview)
            {
                decision.ReasonCode = "KYC_NEEDS_REVIEW";
            }
            else if (decision.State == ComplianceDecisionState.Error)
            {
                decision.ReasonCode = "KYC_ERROR";
                decision.ProviderErrorCode = ComplianceProviderErrorCode.InternalError;
            }

            AppendAuditEvent(decision, "KycCompleted", decision.State, correlationId, providerRefId,
                errorMessage ?? kycStatus.ToString());
        }

        private async Task RunAmlCheckAsync(
            NormalizedComplianceDecision decision,
            string subjectId,
            Dictionary<string, string> metadata,
            string correlationId)
        {
            var (providerRefId, state, reasonCode, errorMessage) =
                await _amlProvider.ScreenSubjectAsync(subjectId, metadata, correlationId);

            decision.AmlProviderReferenceId = providerRefId;
            decision.State = state;
            decision.ReasonCode = reasonCode;

            if (state == ComplianceDecisionState.Error)
            {
                decision.ProviderErrorCode = MapAmlErrorCode(reasonCode);
            }

            // Populate watchlist categories from well-known reason codes
            decision.MatchedWatchlistCategories = DeriveWatchlistCategories(reasonCode);

            AppendAuditEvent(decision, "AmlCompleted", state, correlationId, providerRefId,
                errorMessage ?? reasonCode);
        }

        private async Task RunCombinedCheckAsync(
            NormalizedComplianceDecision decision,
            string subjectId,
            Dictionary<string, string> metadata,
            string correlationId)
        {
            // Step 1: KYC
            var kycRequest = BuildKycRequest(subjectId, metadata);
            var (kycRefId, kycStatus, kycError) =
                await _kycProvider.StartVerificationAsync(subjectId, kycRequest, correlationId);

            decision.KycProviderReferenceId = kycRefId;
            var kycState = MapKycStatus(kycStatus);

            AppendAuditEvent(decision, "KycCompleted", kycState, correlationId, kycRefId,
                kycError ?? kycStatus.ToString());

            // If KYC is a hard failure, skip AML (fail-closed)
            if (kycState == ComplianceDecisionState.Rejected
                || kycState == ComplianceDecisionState.Error
                || kycState == ComplianceDecisionState.ProviderUnavailable
                || kycState == ComplianceDecisionState.InsufficientData
                || kycState == ComplianceDecisionState.Expired)
            {
                decision.State = kycState;
                decision.ReasonCode = kycState switch
                {
                    ComplianceDecisionState.Error => "KYC_PROVIDER_ERROR",
                    ComplianceDecisionState.ProviderUnavailable => "KYC_PROVIDER_UNAVAILABLE",
                    ComplianceDecisionState.InsufficientData => "KYC_INSUFFICIENT_DATA",
                    ComplianceDecisionState.Expired => "KYC_EXPIRED",
                    _ => "KYC_REJECTED"
                };
                if (kycState == ComplianceDecisionState.Error)
                    decision.ProviderErrorCode = ComplianceProviderErrorCode.InternalError;
                AppendAuditEvent(decision, "AmlSkipped", kycState, correlationId, null, "AML skipped due to KYC failure");
                return;
            }

            // Step 2: AML
            var (amlRefId, amlState, amlReasonCode, amlError) =
                await _amlProvider.ScreenSubjectAsync(subjectId, metadata, correlationId);

            decision.AmlProviderReferenceId = amlRefId;
            decision.MatchedWatchlistCategories = DeriveWatchlistCategories(amlReasonCode);

            AppendAuditEvent(decision, "AmlCompleted", amlState, correlationId, amlRefId,
                amlError ?? amlReasonCode);

            // Combine results: rejection / unavailability take priority
            if (amlState == ComplianceDecisionState.Rejected || kycState == ComplianceDecisionState.Rejected)
            {
                decision.State = ComplianceDecisionState.Rejected;
                decision.ReasonCode = amlState == ComplianceDecisionState.Rejected ? amlReasonCode : "KYC_REJECTED";
            }
            else if (amlState == ComplianceDecisionState.ProviderUnavailable)
            {
                decision.State = ComplianceDecisionState.ProviderUnavailable;
                decision.ReasonCode = amlReasonCode;
            }
            else if (amlState == ComplianceDecisionState.InsufficientData)
            {
                decision.State = ComplianceDecisionState.InsufficientData;
                decision.ReasonCode = amlReasonCode;
            }
            else if (amlState == ComplianceDecisionState.Error)
            {
                decision.State = ComplianceDecisionState.Error;
                decision.ProviderErrorCode = MapAmlErrorCode(amlReasonCode);
                decision.ReasonCode = amlReasonCode;
            }
            else if (amlState == ComplianceDecisionState.NeedsReview || kycState == ComplianceDecisionState.NeedsReview)
            {
                decision.State = ComplianceDecisionState.NeedsReview;
                decision.ReasonCode = amlState == ComplianceDecisionState.NeedsReview ? amlReasonCode : "KYC_NEEDS_REVIEW";
            }
            else
            {
                // Both approved (or one pending/approved)
                decision.State = ComplianceDecisionState.Approved;
            }
        }

        private static StartKycVerificationRequest BuildKycRequest(string subjectId, Dictionary<string, string> metadata)
        {
            metadata.TryGetValue("full_name", out var fullName);
            metadata.TryGetValue("date_of_birth", out var dob);
            metadata.TryGetValue("country", out var country);

            return new StartKycVerificationRequest
            {
                FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName : subjectId,
                DateOfBirth = dob,
                Country = country,
                Metadata = new Dictionary<string, string>(metadata)
            };
        }

        private static ComplianceDecisionState MapKycStatus(KycStatus status) => status switch
        {
            KycStatus.Approved => ComplianceDecisionState.Approved,
            KycStatus.Rejected => ComplianceDecisionState.Rejected,
            KycStatus.NeedsReview => ComplianceDecisionState.NeedsReview,
            KycStatus.Pending => ComplianceDecisionState.Pending,
            KycStatus.Expired => ComplianceDecisionState.Expired,
            _ => ComplianceDecisionState.Error
        };

        private static ComplianceProviderErrorCode MapAmlErrorCode(string? reasonCode) => reasonCode switch
        {
            "PROVIDER_TIMEOUT" => ComplianceProviderErrorCode.Timeout,
            "PROVIDER_UNAVAILABLE" => ComplianceProviderErrorCode.ProviderUnavailable,
            "MALFORMED_RESPONSE" => ComplianceProviderErrorCode.MalformedResponse,
            _ => ComplianceProviderErrorCode.InternalError
        };

        private static void AppendAuditEvent(
            NormalizedComplianceDecision decision,
            string eventType,
            ComplianceDecisionState state,
            string correlationId,
            string? providerRefId,
            string? message)
        {
            decision.AuditTrail.Add(new ComplianceAuditEvent
            {
                OccurredAt = DateTimeOffset.UtcNow,
                EventType = eventType,
                State = state,
                CorrelationId = correlationId,
                ProviderReferenceId = providerRefId,
                Message = message
            });
        }

        private static bool IsTerminal(ComplianceDecisionState state) =>
            state is ComplianceDecisionState.Approved
                  or ComplianceDecisionState.Rejected
                  or ComplianceDecisionState.NeedsReview
                  or ComplianceDecisionState.Error
                  or ComplianceDecisionState.ProviderUnavailable
                  or ComplianceDecisionState.InsufficientData;

        private static ComplianceCheckResponse ToResponse(NormalizedComplianceDecision d, bool isReplay) =>
            new()
            {
                Success = true,
                DecisionId = d.DecisionId,
                State = d.State,
                ReasonCode = d.ReasonCode,
                ProviderErrorCode = d.ProviderErrorCode,
                CorrelationId = d.CorrelationId,
                IsIdempotentReplay = isReplay,
                InitiatedAt = d.InitiatedAt,
                CompletedAt = d.CompletedAt,
                EvidenceExpiresAt = d.EvidenceExpiresAt,
                SubjectType = d.SubjectType,
                MatchedWatchlistCategories = d.MatchedWatchlistCategories.ToList(),
                ConfidenceScore = d.ConfidenceScore,
                AuditTrail = d.AuditTrail.ToList(),
                ReviewerNotes = d.ReviewerNotes.ToList()
            };

        private static ComplianceCheckResponse ErrorResponse(string errorCode, string errorMessage, string correlationId) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                CorrelationId = correlationId
            };

        /// <summary>
        /// Derives human-readable watchlist category labels from well-known AML reason codes.
        /// Returns an empty list for non-sanctions reason codes.
        /// </summary>
        private static List<string> DeriveWatchlistCategories(string? reasonCode) => reasonCode switch
        {
            "SANCTIONS_MATCH" => new List<string> { "OFAC_SDN", "EU_SANCTIONS" },
            "REVIEW_REQUIRED" => new List<string> { "PEP_WATCHLIST" },
            _ => new List<string>(0)
        };
    }
}

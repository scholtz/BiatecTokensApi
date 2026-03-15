using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
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
        private readonly TimeProvider _timeProvider;
        private readonly string? _kycWebhookSecret;

        // Primary store: decisionId → decision
        private readonly ConcurrentDictionary<string, NormalizedComplianceDecision> _decisions = new();
        // Idempotency store: idempotencyKey → decisionId
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();
        // Subject index: subjectId → list of decisionIds (using ConcurrentBag for lock-free add)
        private readonly ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentBag<string>> _subjectIndex = new();
        // O(1) reverse-lookup: providerReferenceId → decisionId
        private readonly ConcurrentDictionary<string, string> _providerRefIndex = new();

        public ComplianceOrchestrationService(
            IKycProvider kycProvider,
            IAmlProvider amlProvider,
            ILogger<ComplianceOrchestrationService> logger,
            TimeProvider? timeProvider = null,
            IOptions<KycConfig>? kycConfig = null)
        {
            _kycProvider = kycProvider;
            _amlProvider = amlProvider;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _kycWebhookSecret = kycConfig?.Value.WebhookSecret;
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
            var now = _timeProvider.GetUtcNow();
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
                decision.CompletedAt = _timeProvider.GetUtcNow();

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
                    && _timeProvider.GetUtcNow() > decision.EvidenceExpiresAt.Value
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
                AppendedAt = _timeProvider.GetUtcNow(),
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

            // Register in O(1) reverse-lookup index
            if (!string.IsNullOrEmpty(providerRefId))
                _providerRefIndex.TryAdd(providerRefId, decision.DecisionId);

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

            // Register in O(1) reverse-lookup index
            if (!string.IsNullOrEmpty(providerRefId))
                _providerRefIndex.TryAdd(providerRefId, decision.DecisionId);

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
                OccurredAt = TimeProvider.System.GetUtcNow(),
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

        // ── Idempotency index for provider callbacks ────────────────────────────
        // callbackIdempotencyKey → ProviderReferenceId that registered it
        // Storing the originating ProviderReferenceId ensures that a key collision
        // between two different callbacks (different provider references sharing the
        // same idempotency key) is detected and rejected fail-closed, rather than
        // silently suppressing the second, legitimate callback.
        private readonly ConcurrentDictionary<string, string> _callbackIdempotencyIndex = new();

        /// <inheritdoc/>
        public async Task<RescreenResponse> RescreenAsync(
            string decisionId,
            RescreenRequest request,
            string actorId,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
            {
                return new RescreenResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "DecisionId is required.",
                    CorrelationId = correlationId
                };
            }

            if (!_decisions.TryGetValue(decisionId, out var original))
            {
                _logger.LogWarning(
                    "Rescreen requested for unknown DecisionId={DecisionId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(decisionId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new RescreenResponse
                {
                    Success = false,
                    ErrorCode = "COMPLIANCE_CHECK_NOT_FOUND",
                    ErrorMessage = $"No compliance decision found with ID '{decisionId}'.",
                    CorrelationId = correlationId
                };
            }

            var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "OperatorRequested" : request.Reason;

            _logger.LogInformation(
                "Rescreen initiated. OriginalDecisionId={DecisionId}, SubjectId={SubjectId}, Reason={Reason}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(original.SubjectId),
                LoggingHelper.SanitizeLogInput(reason),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            // Build a new initiation request from the original, allowing overrides.
            // Use a fresh, stable ContextId for the rescreen to avoid unbounded string growth
            // when a decision is rescreened multiple times.
            var rescreenContextId = $"rescreen:{decisionId.Substring(0, Math.Min(8, decisionId.Length))}:{Guid.NewGuid():N}";
            var newRequest = new InitiateComplianceCheckRequest
            {
                SubjectId = original.SubjectId,
                ContextId = rescreenContextId,
                CheckType = request?.CheckType ?? original.CheckType,
                SubjectType = original.SubjectType,
                SubjectMetadata = request?.SubjectMetadata ?? new Dictionary<string, string>(),
                EvidenceValidityHours = request?.EvidenceValidityHours
            };

            // Append an audit note to the original decision so the history is traceable
            AppendAuditEvent(
                original,
                "RescreenTriggered",
                original.State,
                correlationId,
                null,
                $"Rescreen triggered by actor {actorId}. Reason: {reason}");

            var newDecisionResponse = await InitiateCheckAsync(newRequest, actorId, correlationId);

            return new RescreenResponse
            {
                Success = newDecisionResponse.Success,
                NewDecision = newDecisionResponse.Success ? newDecisionResponse : null,
                PreviousDecisionId = decisionId,
                ErrorMessage = newDecisionResponse.Success ? null : newDecisionResponse.ErrorMessage,
                ErrorCode = newDecisionResponse.Success ? null : newDecisionResponse.ErrorCode,
                CorrelationId = correlationId
            };
        }

        /// <inheritdoc/>
        public Task<ProviderCallbackResponse> ProcessProviderCallbackAsync(
            ProviderCallbackRequest request,
            string correlationId)
        {
            if (request == null)
            {
                return Task.FromResult(new ProviderCallbackResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Callback request is required.",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.ProviderReferenceId))
            {
                return Task.FromResult(new ProviderCallbackResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "ProviderReferenceId is required.",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.OutcomeStatus))
            {
                return Task.FromResult(new ProviderCallbackResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "OutcomeStatus is required.",
                    CorrelationId = correlationId
                });
            }

            // Signature validation: when a signature is provided AND a webhook secret is configured,
            // validate the HMAC before processing. Fail-closed: if a secret is configured but
            // the signature is missing or invalid, reject the callback.
            if (!string.IsNullOrWhiteSpace(_kycWebhookSecret))
            {
                if (string.IsNullOrWhiteSpace(request.Signature))
                {
                    _logger.LogWarning(
                        "Provider callback rejected: signature required but not provided. Provider={Provider}, ProviderRefId={RefId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(request.ProviderName),
                        LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return Task.FromResult(new ProviderCallbackResponse
                    {
                        Success = false,
                        ErrorCode = "SIGNATURE_REQUIRED",
                        ErrorMessage = "A webhook signature is required when a webhook secret is configured.",
                        CorrelationId = correlationId
                    });
                }

                // Serialise the request payload and validate using the KYC provider's HMAC logic.
                // This uses the same HMAC-SHA256 mechanism as KycController webhook validation.
                var payloadJson = System.Text.Json.JsonSerializer.Serialize(request);
                if (!_kycProvider.ValidateWebhookSignature(payloadJson, request.Signature, _kycWebhookSecret))
                {
                    _logger.LogWarning(
                        "Provider callback rejected: invalid signature. Provider={Provider}, ProviderRefId={RefId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(request.ProviderName),
                        LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return Task.FromResult(new ProviderCallbackResponse
                    {
                        Success = false,
                        ErrorCode = "INVALID_SIGNATURE",
                        ErrorMessage = "The provided webhook signature is invalid.",
                        CorrelationId = correlationId
                    });
                }
            }

            // Atomic idempotency gate: attempt to register (key → providerReferenceId) BEFORE
            // processing. ConcurrentDictionary.TryAdd is inherently thread-safe — only the first
            // thread to insert a key succeeds; all concurrent duplicates receive false and are
            // routed to the replay/mismatch branch without touching decision state.
            //
            // Scoping the value to ProviderReferenceId closes a second security gap:
            // a reused idempotency key that arrives for a *different* provider reference
            // would previously be silently treated as a successful replay, suppressing the
            // new legitimate callback. With this design:
            //  - Same key + same ProviderReferenceId → exact replay: IsIdempotentReplay = true
            //  - Same key + different ProviderReferenceId → fail-closed: IDEMPOTENCY_KEY_CONFLICT
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                if (!_callbackIdempotencyIndex.TryAdd(request.IdempotencyKey, request.ProviderReferenceId))
                {
                    // Key already exists — determine if this is a safe exact replay or a conflict.
                    _callbackIdempotencyIndex.TryGetValue(request.IdempotencyKey, out var registeredProviderRef);
                    bool isExactReplay = string.Equals(registeredProviderRef, request.ProviderReferenceId,
                        StringComparison.Ordinal);

                    if (isExactReplay)
                    {
                        _logger.LogInformation(
                            "Provider callback idempotent replay detected. IdempotencyKey={Key}, ProviderRefId={RefId}, CorrelationId={CorrelationId}",
                            LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                            LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                            LoggingHelper.SanitizeLogInput(correlationId));

                        return Task.FromResult(new ProviderCallbackResponse
                        {
                            Success = true,
                            IsIdempotentReplay = true,
                            CorrelationId = correlationId
                        });
                    }
                    else
                    {
                        // Different ProviderReferenceId is attempting to reuse an already-registered
                        // idempotency key. Reject fail-closed to prevent suppression of legitimate updates.
                        _logger.LogWarning(
                            "Provider callback rejected: idempotency key conflict. IdempotencyKey={Key} is already registered for ProviderRefId={RegisteredRefId}, incoming ProviderRefId={IncomingRefId}, CorrelationId={CorrelationId}",
                            LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                            LoggingHelper.SanitizeLogInput(registeredProviderRef),
                            LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                            LoggingHelper.SanitizeLogInput(correlationId));

                        return Task.FromResult(new ProviderCallbackResponse
                        {
                            Success = false,
                            ErrorCode = "IDEMPOTENCY_KEY_CONFLICT",
                            ErrorMessage = "The idempotency key is already registered for a different provider reference.",
                            CorrelationId = correlationId
                        });
                    }
                }
            }

            // Find the decision that corresponds to this provider reference ID.
            // Use O(1) index lookup; fall back to linear scan for backwards compatibility
            // (e.g. for decisions created before the index was populated).
            NormalizedComplianceDecision? decision = null;
            if (_providerRefIndex.TryGetValue(request.ProviderReferenceId, out var indexedDecisionId))
            {
                _decisions.TryGetValue(indexedDecisionId, out decision);
            }

            if (decision == null)
            {
                // Fallback linear scan (covers edge cases / legacy data)
                decision = _decisions.Values.FirstOrDefault(d =>
                    d.KycProviderReferenceId == request.ProviderReferenceId ||
                    d.AmlProviderReferenceId == request.ProviderReferenceId);
            }

            if (decision == null)
            {
                _logger.LogWarning(
                    "Provider callback received for unknown ProviderReferenceId={RefId}, Provider={Provider}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(request.ProviderName),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return Task.FromResult(new ProviderCallbackResponse
                {
                    Success = false,
                    ErrorCode = "COMPLIANCE_CHECK_NOT_FOUND",
                    ErrorMessage = $"No compliance decision found for provider reference '{request.ProviderReferenceId}'.",
                    CorrelationId = correlationId
                });
            }

            // Map the incoming outcome string to a ComplianceDecisionState
            var newState = MapOutcomeStringToState(request.OutcomeStatus);

            var previousState = decision.State;
            decision.State = newState;
            decision.ReasonCode = request.ReasonCode ?? decision.ReasonCode;
            decision.CorrelationId = correlationId;

            if (IsTerminal(newState))
            {
                decision.CompletedAt = _timeProvider.GetUtcNow();
            }

            // Update watchlist categories if AML-related
            if (!string.IsNullOrWhiteSpace(request.ReasonCode))
            {
                var derived = DeriveWatchlistCategories(request.ReasonCode);
                if (derived.Count > 0)
                {
                    decision.MatchedWatchlistCategories = derived;
                }
            }

            AppendAuditEvent(
                decision,
                $"ProviderCallback:{request.EventType ?? "Unknown"}",
                newState,
                correlationId,
                request.ProviderReferenceId,
                request.Message ?? $"Provider '{request.ProviderName}' updated outcome to '{request.OutcomeStatus}'");

            _logger.LogInformation(
                "Provider callback processed. DecisionId={DecisionId}, ProviderRefId={RefId}, OldState={OldState}, NewState={NewState}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decision.DecisionId),
                LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                previousState,
                newState,
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new ProviderCallbackResponse
            {
                Success = true,
                DecisionId = decision.DecisionId,
                NewState = newState,
                IsIdempotentReplay = false,
                CorrelationId = correlationId
            });
        }

        /// <summary>
        /// Maps a provider-supplied outcome string to a <see cref="ComplianceDecisionState"/>.
        /// Unrecognised strings are treated as <see cref="ComplianceDecisionState.Error"/> (fail-closed).
        /// </summary>
        private static ComplianceDecisionState MapOutcomeStringToState(string outcome) =>
            outcome?.ToLowerInvariant() switch
            {
                "approved" or "verified" or "passed" or "clear" => ComplianceDecisionState.Approved,
                "rejected" or "failed" or "declined" or "blocked" => ComplianceDecisionState.Rejected,
                "needs_review" or "needsreview" or "review" or "manual_review" => ComplianceDecisionState.NeedsReview,
                "pending" or "processing" or "in_progress" => ComplianceDecisionState.Pending,
                "provider_unavailable" or "unavailable" or "offline" => ComplianceDecisionState.ProviderUnavailable,
                "insufficient_data" or "insufficientdata" or "incomplete" => ComplianceDecisionState.InsufficientData,
                "expired" or "stale" => ComplianceDecisionState.Expired,
                _ => ComplianceDecisionState.Error
            };
    }
}

using System.Collections.Concurrent;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.KycWorkflow;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of the KYC workflow service.
    /// Provides a deterministic state machine, full audit history, evidence management,
    /// and eligibility evaluation for production-grade compliance workflows.
    /// </summary>
    public class KycWorkflowService : IKycWorkflowService
    {
        private readonly ILogger<KycWorkflowService> _logger;

        // In-memory stores (thread-safe)
        private readonly ConcurrentDictionary<string, KycWorkflowRecord> _records = new();

        // Allowed transitions: key = from-state, value = set of reachable states
        private static readonly IReadOnlyDictionary<KycVerificationState, IReadOnlySet<KycVerificationState>> AllowedTransitionMap =
            new Dictionary<KycVerificationState, IReadOnlySet<KycVerificationState>>
            {
                [KycVerificationState.NotStarted]          = new HashSet<KycVerificationState> { KycVerificationState.Pending },
                [KycVerificationState.Pending]             = new HashSet<KycVerificationState> { KycVerificationState.ManualReviewRequired, KycVerificationState.Approved, KycVerificationState.Rejected, KycVerificationState.Expired },
                [KycVerificationState.ManualReviewRequired]= new HashSet<KycVerificationState> { KycVerificationState.Approved, KycVerificationState.Rejected, KycVerificationState.Pending },
                [KycVerificationState.Approved]            = new HashSet<KycVerificationState> { KycVerificationState.Expired },
                [KycVerificationState.Rejected]            = new HashSet<KycVerificationState> { KycVerificationState.Pending },
                [KycVerificationState.Expired]             = new HashSet<KycVerificationState> { KycVerificationState.Pending },
            };

        /// <summary>
        /// Initializes a new instance of <see cref="KycWorkflowService"/>.
        /// </summary>
        public KycWorkflowService(ILogger<KycWorkflowService> logger)
        {
            _logger = logger;
        }

        // ── State machine ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public KycStateTransitionValidationResult ValidateTransition(KycVerificationState from, KycVerificationState to)
        {
            if (AllowedTransitionMap.TryGetValue(from, out var allowed) && allowed.Contains(to))
            {
                return new KycStateTransitionValidationResult { IsValid = true, FromState = from, ToState = to };
            }

            return new KycStateTransitionValidationResult
            {
                IsValid = false,
                FromState = from,
                ToState = to,
                ErrorMessage = $"Transition from {from} to {to} is not permitted by the workflow rules."
            };
        }

        /// <inheritdoc/>
        public IReadOnlySet<KycVerificationState> GetAllowedTransitions(KycVerificationState from)
        {
            if (AllowedTransitionMap.TryGetValue(from, out var allowed))
                return allowed;
            return new HashSet<KycVerificationState>();
        }

        // ── CRUD ───────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<KycVerificationResponse> CreateVerificationAsync(
            CreateKycVerificationRequest request,
            string actorId,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(request.ParticipantId))
            {
                _logger.LogWarning("CreateVerification rejected: empty participantId. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(correlationId));

                return Task.FromResult(new KycVerificationResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "ParticipantId is required."
                });
            }

            var now = DateTime.UtcNow;
            var record = new KycWorkflowRecord
            {
                ParticipantId       = request.ParticipantId,
                ProviderName        = request.ProviderName,
                ExternalReference   = request.ExternalReference,
                State               = KycVerificationState.Pending,
                CurrentReviewNote   = request.InitialNote,
                CreatedAt           = now,
                UpdatedAt           = now,
                CorrelationId       = correlationId,
                CreatedByActorId    = actorId,
                Metadata            = request.Metadata ?? new Dictionary<string, string>()
            };

            var auditEntry = new KycAuditEntry
            {
                KycId           = record.KycId,
                FromState       = KycVerificationState.NotStarted,
                ToState         = KycVerificationState.Pending,
                ActorId         = actorId,
                ActorType       = "System",
                ReviewNote      = request.InitialNote,
                Timestamp       = now,
                CorrelationId   = correlationId
            };
            record.AuditHistory.Add(auditEntry);

            _records[record.KycId] = record;

            _logger.LogInformation(
                "KYC workflow record created. KycId={KycId} ParticipantId={ParticipantId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(record.KycId),
                LoggingHelper.SanitizeLogInput(request.ParticipantId),
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new KycVerificationResponse { Success = true, Record = record });
        }

        /// <inheritdoc/>
        public Task<KycVerificationResponse> GetVerificationAsync(string kycId)
        {
            if (!_records.TryGetValue(kycId, out var record))
            {
                return Task.FromResult(new KycVerificationResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"KYC record '{kycId}' not found."
                });
            }

            AutoExpireIfNeeded(record);
            return Task.FromResult(new KycVerificationResponse { Success = true, Record = record });
        }

        /// <inheritdoc/>
        public Task<KycVerificationResponse> GetActiveVerificationByParticipantAsync(string participantId)
        {
            var activeStates = new HashSet<KycVerificationState>
            {
                KycVerificationState.Approved,
                KycVerificationState.Pending,
                KycVerificationState.ManualReviewRequired
            };

            var record = _records.Values
                .Where(r => r.ParticipantId == participantId && activeStates.Contains(r.State))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            if (record == null)
            {
                return Task.FromResult(new KycVerificationResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"No active KYC record found for participant '{participantId}'."
                });
            }

            AutoExpireIfNeeded(record);
            return Task.FromResult(new KycVerificationResponse { Success = true, Record = record });
        }

        /// <inheritdoc/>
        public Task<KycVerificationResponse> UpdateStatusAsync(
            string kycId,
            UpdateKycVerificationStatusRequest request,
            string actorId,
            string correlationId)
        {
            if (!_records.TryGetValue(kycId, out var record))
            {
                return Task.FromResult(new KycVerificationResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"KYC record '{kycId}' not found."
                });
            }

            var validation = ValidateTransition(record.State, request.NewState);
            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Invalid KYC state transition. KycId={KycId} From={From} To={To} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(kycId),
                    record.State,
                    request.NewState,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return Task.FromResult(new KycVerificationResponse
                {
                    Success = false,
                    ErrorCode = "INVALID_STATE_TRANSITION",
                    ErrorMessage = validation.ErrorMessage
                });
            }

            var now = DateTime.UtcNow;
            var fromState = record.State;

            var auditEntry = new KycAuditEntry
            {
                KycId           = kycId,
                FromState       = fromState,
                ToState         = request.NewState,
                ActorId         = actorId,
                ActorType       = "Admin",
                ReviewNote      = request.ReviewNote,
                ReasonCode      = request.ReasonCode,
                RejectionReason = request.RejectionReason,
                Timestamp       = now,
                CorrelationId   = correlationId,
                Metadata        = request.Metadata ?? new Dictionary<string, string>()
            };

            record.State               = request.NewState;
            record.UpdatedAt           = now;
            record.CurrentReviewNote   = request.ReviewNote ?? record.CurrentReviewNote;
            record.LastRejectionReason = request.RejectionReason;
            record.LastReviewerActorId = actorId;

            if (request.NewState == KycVerificationState.Approved)
            {
                record.ApprovedAt   = now;
                record.CompletedAt  = now;
                int expiryDays      = request.ExpirationDays ?? 365;
                record.ExpiresAt    = now.AddDays(expiryDays);
            }
            else if (request.NewState == KycVerificationState.Rejected)
            {
                record.CompletedAt = now;
            }

            record.AuditHistory.Add(auditEntry);

            _logger.LogInformation(
                "KYC status updated. KycId={KycId} From={From} To={To} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(kycId),
                fromState,
                request.NewState,
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new KycVerificationResponse { Success = true, Record = record });
        }

        // ── History ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<KycHistoryResponse> GetHistoryAsync(string kycId)
        {
            if (!_records.TryGetValue(kycId, out var record))
            {
                return Task.FromResult(new KycHistoryResponse
                {
                    Success = false,
                    KycId = kycId,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"KYC record '{kycId}' not found."
                });
            }

            return Task.FromResult(new KycHistoryResponse
            {
                Success        = true,
                KycId          = kycId,
                ParticipantId  = record.ParticipantId,
                CurrentState   = record.State,
                History        = record.AuditHistory.OrderBy(e => e.Timestamp).ToList()
            });
        }

        // ── Evidence ───────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<KycEvidenceResponse> AddEvidenceAsync(string kycId, AddKycEvidenceRequest request, string actorId)
        {
            if (!_records.TryGetValue(kycId, out var record))
            {
                return Task.FromResult(new KycEvidenceResponse
                {
                    Success = false,
                    KycId = kycId,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"KYC record '{kycId}' not found."
                });
            }

            var evidence = new KycEvidenceMetadata
            {
                KycId               = kycId,
                EvidenceType        = request.EvidenceType,
                DocumentReference   = request.DocumentReference,
                ContentHash         = request.ContentHash,
                IssuingCountry      = request.IssuingCountry,
                DocumentExpiresAt   = request.DocumentExpiresAt,
                UploadedAt          = DateTime.UtcNow,
                Metadata            = request.Metadata ?? new Dictionary<string, string>()
            };

            record.Evidence.Add(evidence);
            record.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Evidence added to KYC record. KycId={KycId} EvidenceType={EvidenceType} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(kycId),
                evidence.EvidenceType,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new KycEvidenceResponse
            {
                Success  = true,
                KycId    = kycId,
                Evidence = record.Evidence.ToList()
            });
        }

        /// <inheritdoc/>
        public Task<KycEvidenceResponse> GetEvidenceAsync(string kycId)
        {
            if (!_records.TryGetValue(kycId, out var record))
            {
                return Task.FromResult(new KycEvidenceResponse
                {
                    Success = false,
                    KycId = kycId,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"KYC record '{kycId}' not found."
                });
            }

            return Task.FromResult(new KycEvidenceResponse
            {
                Success  = true,
                KycId    = kycId,
                Evidence = record.Evidence.ToList()
            });
        }

        // ── Eligibility ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<KycEligibilityResult> EvaluateEligibilityAsync(KycEligibilityRequest request)
        {
            var record = _records.Values
                .Where(r => r.ParticipantId == request.ParticipantId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            if (record == null)
            {
                return Task.FromResult(new KycEligibilityResult
                {
                    IsEligible          = false,
                    ParticipantId       = request.ParticipantId,
                    OfferingId          = request.OfferingId,
                    CurrentState        = KycVerificationState.NotStarted,
                    IneligibilityReason = "No KYC verification record found for this participant.",
                    KycRequired         = true,
                    EvaluatedAt         = DateTime.UtcNow
                });
            }

            // Auto-expire if applicable
            AutoExpireIfNeeded(record);

            if (request.RequireApproved && record.State != KycVerificationState.Approved)
            {
                return Task.FromResult(new KycEligibilityResult
                {
                    IsEligible          = false,
                    ParticipantId       = request.ParticipantId,
                    OfferingId          = request.OfferingId,
                    CurrentState        = record.State,
                    KycId               = record.KycId,
                    IneligibilityReason = $"KYC is not in Approved state (current: {record.State}).",
                    ExpiresAt           = record.ExpiresAt,
                    KycRequired         = true,
                    EvaluatedAt         = DateTime.UtcNow
                });
            }

            if (request.CheckExpiry && record.IsExpired)
            {
                return Task.FromResult(new KycEligibilityResult
                {
                    IsEligible          = false,
                    ParticipantId       = request.ParticipantId,
                    OfferingId          = request.OfferingId,
                    CurrentState        = record.State,
                    KycId               = record.KycId,
                    IneligibilityReason = "KYC approval has expired.",
                    ExpiresAt           = record.ExpiresAt,
                    KycRequired         = true,
                    EvaluatedAt         = DateTime.UtcNow
                });
            }

            return Task.FromResult(new KycEligibilityResult
            {
                IsEligible    = true,
                ParticipantId = request.ParticipantId,
                OfferingId    = request.OfferingId,
                CurrentState  = record.State,
                KycId         = record.KycId,
                ExpiresAt     = record.ExpiresAt,
                KycRequired   = true,
                EvaluatedAt   = DateTime.UtcNow
            });
        }

        // ── Expiry ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<int> ProcessExpiredVerificationsAsync()
        {
            int count = 0;
            var now = DateTime.UtcNow;

            foreach (var record in _records.Values)
            {
                if (record.State == KycVerificationState.Approved
                    && record.ExpiresAt.HasValue
                    && record.ExpiresAt.Value < now)
                {
                    var auditEntry = new KycAuditEntry
                    {
                        KycId       = record.KycId,
                        FromState   = KycVerificationState.Approved,
                        ToState     = KycVerificationState.Expired,
                        ActorId     = "System",
                        ActorType   = "System",
                        ReviewNote  = "Automatically expired by batch processor.",
                        Timestamp   = now
                    };

                    record.State     = KycVerificationState.Expired;
                    record.UpdatedAt = now;
                    record.AuditHistory.Add(auditEntry);
                    count++;

                    _logger.LogInformation(
                        "KYC record auto-expired. KycId={KycId} ParticipantId={ParticipantId}",
                        LoggingHelper.SanitizeLogInput(record.KycId),
                        LoggingHelper.SanitizeLogInput(record.ParticipantId));
                }
            }

            return Task.FromResult(count);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void AutoExpireIfNeeded(KycWorkflowRecord record)
        {
            if (record.State == KycVerificationState.Approved
                && record.ExpiresAt.HasValue
                && record.ExpiresAt.Value < DateTime.UtcNow)
            {
                var now = DateTime.UtcNow;
                var auditEntry = new KycAuditEntry
                {
                    KycId       = record.KycId,
                    FromState   = KycVerificationState.Approved,
                    ToState     = KycVerificationState.Expired,
                    ActorId     = "System",
                    ActorType   = "System",
                    ReviewNote  = "Automatically expired on read.",
                    Timestamp   = now
                };

                record.State     = KycVerificationState.Expired;
                record.UpdatedAt = now;
                record.AuditHistory.Add(auditEntry);
            }
        }
    }
}

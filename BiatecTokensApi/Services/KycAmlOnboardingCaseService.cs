using System.Collections.Concurrent;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Provider-backed KYC/AML onboarding case lifecycle service.
    ///
    /// <para>
    /// Manages cases from initial creation through provider checks, reviewer decisions,
    /// and a final approved or rejected outcome. State transitions are strictly enforced;
    /// invalid transitions and absent provider configuration surface explicitly —
    /// there are no silent success-shaped fallbacks.
    /// </para>
    ///
    /// <para>
    /// Storage is in-process via <see cref="ConcurrentDictionary{TKey,TValue}"/>;
    /// a persistent backing store can be substituted without changing the API contract.
    /// </para>
    /// </summary>
    public class KycAmlOnboardingCaseService : IKycAmlOnboardingCaseService
    {
        // ── In-memory stores ─────────────────────────────────────────────────────

        private readonly ConcurrentDictionary<string, KycAmlOnboardingCase> _cases = new();

        // SubjectId:IdempotencyKey → CaseId reverse index
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();

        // ── Static state sets (O(1) lookup) ──────────────────────────────────────

        private static readonly HashSet<KycAmlOnboardingCaseState> TerminalStates =
        [
            KycAmlOnboardingCaseState.Approved,
            KycAmlOnboardingCaseState.Rejected,
            KycAmlOnboardingCaseState.Expired
        ];

        private static readonly HashSet<KycAmlOnboardingCaseState> ReviewableStates =
        [
            KycAmlOnboardingCaseState.PendingReview,
            KycAmlOnboardingCaseState.UnderReview
        ];

        // ── Dependencies ─────────────────────────────────────────────────────────

        private readonly ILiveProviderVerificationJourneyService? _journeyService;
        private readonly IWebhookService? _webhookService;
        private readonly ILogger<KycAmlOnboardingCaseService> _logger;
        private readonly TimeProvider _time;

        // ── Constructor ──────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the onboarding case service.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="journeyService">Optional live-provider journey service.
        /// When null, provider-check operations fail closed with
        /// <see cref="KycAmlOnboardingCaseState.ConfigurationMissing"/>.</param>
        /// <param name="webhookService">Optional webhook service for event emission.</param>
        /// <param name="timeProvider">Substitutable time provider for deterministic testing.</param>
        public KycAmlOnboardingCaseService(
            ILogger<KycAmlOnboardingCaseService> logger,
            ILiveProviderVerificationJourneyService? journeyService = null,
            IWebhookService? webhookService = null,
            TimeProvider? timeProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _journeyService = journeyService;
            _webhookService = webhookService;
            _time = timeProvider ?? TimeProvider.System;
        }

        // ── CreateCaseAsync ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CreateOnboardingCaseResponse> CreateCaseAsync(
            CreateOnboardingCaseRequest request,
            string actorId)
        {
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            // ── 1. Validation ────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return Task.FromResult(new CreateOnboardingCaseResponse
                {
                    Success = false,
                    ErrorCode = "INVALID_SUBJECT_ID",
                    ErrorMessage = "SubjectId is required to create an onboarding case.",
                    CorrelationId = correlationId
                });
            }

            // ── 2. Idempotency check ─────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var idempotencyKey = $"{request.SubjectId}:{request.IdempotencyKey}";
                if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId) &&
                    _cases.TryGetValue(existingId, out var existingCase))
                {
                    _logger.LogInformation(
                        "CreateCase idempotency hit: returning existing case {CaseId} for subject {SubjectId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(existingId),
                        LoggingHelper.SanitizeLogInput(request.SubjectId),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return Task.FromResult(new CreateOnboardingCaseResponse
                    {
                        Success = true,
                        Case = existingCase,
                        CorrelationId = correlationId
                    });
                }
            }

            // ── 3. Create case ───────────────────────────────────────────────────
            var now = _time.GetUtcNow();
            var caseId = $"kyc-case-{Guid.NewGuid():N}";

            var newCase = new KycAmlOnboardingCase
            {
                CaseId = caseId,
                SubjectId = request.SubjectId,
                SubjectKind = request.SubjectKind,
                State = KycAmlOnboardingCaseState.Initiated,
                CreatedAt = now,
                UpdatedAt = now,
                EvidenceState = KycAmlOnboardingEvidenceState.PendingVerification,
                IsProviderConfigured = _journeyService != null,
                CorrelationId = correlationId,
                SubjectMetadata = request.SubjectMetadata != null
                    ? new Dictionary<string, string>(request.SubjectMetadata)
                    : new Dictionary<string, string>(),
                OrganizationName = request.OrganizationName
            };

            _cases[caseId] = newCase;
            newCase.Timeline.Add(BuildTimelineEvent(
                KycAmlOnboardingTimelineEventType.CaseCreated,
                actorId,
                now,
                $"Onboarding case created for subject {request.SubjectId}.",
                toState: newCase.State,
                correlationId: correlationId));

            // Register idempotency key
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var idempotencyKey = $"{request.SubjectId}:{request.IdempotencyKey}";
                _idempotencyIndex.TryAdd(idempotencyKey, caseId);
            }

            _logger.LogInformation(
                "CreateCase: created case {CaseId} for subject {SubjectId}, IsProviderConfigured={IsProviderConfigured}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                newCase.IsProviderConfigured,
                LoggingHelper.SanitizeLogInput(correlationId));

            // ── 4. Optional webhook ──────────────────────────────────────────────
            EmitWebhookFireAndForget(new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceCaseCreated,
                Actor = actorId
            });

            return Task.FromResult(new CreateOnboardingCaseResponse
            {
                Success = true,
                Case = newCase,
                CorrelationId = correlationId
            });
        }

        // ── GetCaseAsync ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetOnboardingCaseResponse> GetCaseAsync(string caseId)
        {
            if (!_cases.TryGetValue(caseId, out var kycCase))
            {
                return Task.FromResult(new GetOnboardingCaseResponse
                {
                    Success = false,
                    ErrorCode = "CASE_NOT_FOUND",
                    ErrorMessage = $"No onboarding case found with ID '{LoggingHelper.SanitizeLogInput(caseId)}'."
                });
            }

            return Task.FromResult(new GetOnboardingCaseResponse
            {
                Success = true,
                Case = kycCase
            });
        }

        // ── InitiateProviderChecksAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<InitiateProviderChecksResponse> InitiateProviderChecksAsync(
            string caseId,
            InitiateProviderChecksRequest request,
            string actorId)
        {
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            // ── 1. Case lookup ───────────────────────────────────────────────────
            if (!_cases.TryGetValue(caseId, out var kycCase))
            {
                return new InitiateProviderChecksResponse
                {
                    Success = false,
                    ErrorCode = "CASE_NOT_FOUND",
                    ErrorMessage = $"No onboarding case found with ID '{LoggingHelper.SanitizeLogInput(caseId)}'.",
                    CorrelationId = correlationId
                };
            }

            // ── 2. Provider configured? ──────────────────────────────────────────
            if (_journeyService == null)
            {
                var configurationMissingPreviousState = kycCase.State;
                kycCase.State = KycAmlOnboardingCaseState.ConfigurationMissing;
                kycCase.EvidenceState = KycAmlOnboardingEvidenceState.MissingConfiguration;
                kycCase.IsProviderConfigured = false;
                kycCase.UpdatedAt = _time.GetUtcNow();
                kycCase.Timeline.Add(BuildTimelineEvent(
                    KycAmlOnboardingTimelineEventType.ProviderConfigurationMissing,
                    actorId,
                    kycCase.UpdatedAt,
                    "Provider checks could not start because no live-provider journey service is configured.",
                    fromState: configurationMissingPreviousState,
                    toState: kycCase.State,
                    correlationId: correlationId));

                _logger.LogWarning(
                    "InitiateProviderChecks: provider not configured for case {CaseId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new InitiateProviderChecksResponse
                {
                    Success = false,
                    Case = kycCase,
                    ErrorCode = "PROVIDER_NOT_CONFIGURED",
                    ErrorMessage = "No live-provider journey service is configured. Register ILiveProviderVerificationJourneyService in DI.",
                    CorrelationId = correlationId
                };
            }

            // ── 3. State guard ───────────────────────────────────────────────────
            if (kycCase.State != KycAmlOnboardingCaseState.Initiated)
            {
                return new InitiateProviderChecksResponse
                {
                    Success = false,
                    Case = kycCase,
                    ErrorCode = "INVALID_STATE",
                    ErrorMessage = $"Provider checks can only be initiated when the case is in Initiated state. Current state: {kycCase.State}.",
                    CorrelationId = correlationId
                };
            }

            // ── 4. Map execution mode ────────────────────────────────────────────
            var journeyMode = request.ExecutionMode switch
            {
                KycAmlOnboardingExecutionMode.LiveProvider => VerificationJourneyExecutionMode.LiveProvider,
                KycAmlOnboardingExecutionMode.ProtectedSandbox => VerificationJourneyExecutionMode.ProtectedSandbox,
                KycAmlOnboardingExecutionMode.Simulated => VerificationJourneyExecutionMode.Simulated,
                _ => VerificationJourneyExecutionMode.LiveProvider
            };

            // ── 5. Call provider ─────────────────────────────────────────────────
            StartVerificationJourneyResponse journeyResponse;
            try
            {
                journeyResponse = await _journeyService.StartJourneyAsync(
                    new StartVerificationJourneyRequest
                    {
                        SubjectId = kycCase.SubjectId,
                        RequestedExecutionMode = journeyMode,
                        IdempotencyKey = request.IdempotencyKey,
                        CorrelationId = correlationId
                    },
                    actorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "InitiateProviderChecks: journey service threw for case {CaseId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var providerErrorPreviousState = kycCase.State;
                kycCase.State = KycAmlOnboardingCaseState.ProviderUnavailable;
                kycCase.EvidenceState = KycAmlOnboardingEvidenceState.ProviderUnavailable;
                kycCase.UpdatedAt = _time.GetUtcNow();
                kycCase.Timeline.Add(BuildTimelineEvent(
                    KycAmlOnboardingTimelineEventType.ProviderUnavailable,
                    actorId,
                    kycCase.UpdatedAt,
                    "Provider checks could not be started because the provider journey service threw an exception.",
                    fromState: providerErrorPreviousState,
                    toState: kycCase.State,
                    correlationId: correlationId));

                return new InitiateProviderChecksResponse
                {
                    Success = false,
                    Case = kycCase,
                    ErrorCode = "PROVIDER_ERROR",
                    ErrorMessage = "The provider journey service threw an unexpected exception.",
                    CorrelationId = correlationId
                };
            }

            // ── 6. Handle degraded journey ───────────────────────────────────────
            if (journeyResponse.Journey?.CurrentStage == VerificationJourneyStage.Degraded)
            {
                var providerDegradedPreviousState = kycCase.State;
                kycCase.State = KycAmlOnboardingCaseState.ProviderUnavailable;
                kycCase.EvidenceState = KycAmlOnboardingEvidenceState.ProviderUnavailable;
                kycCase.UpdatedAt = _time.GetUtcNow();
                kycCase.Timeline.Add(BuildTimelineEvent(
                    KycAmlOnboardingTimelineEventType.ProviderUnavailable,
                    actorId,
                    kycCase.UpdatedAt,
                    "Provider checks entered a degraded state and cannot be trusted as complete proof.",
                    fromState: providerDegradedPreviousState,
                    toState: kycCase.State,
                    correlationId: correlationId,
                    metadata: new Dictionary<string, string>
                    {
                        ["journeyId"] = journeyResponse.Journey.JourneyId
                    }));

                _logger.LogWarning(
                    "InitiateProviderChecks: journey degraded for case {CaseId}, JourneyId={JourneyId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(journeyResponse.Journey.JourneyId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new InitiateProviderChecksResponse
                {
                    Success = false,
                    Case = kycCase,
                    VerificationJourneyId = journeyResponse.Journey.JourneyId,
                    ErrorCode = "PROVIDER_DEGRADED",
                    ErrorMessage = "The provider journey entered a Degraded state. Review provider configuration.",
                    CorrelationId = correlationId
                };
            }

            // ── 7. Success path ──────────────────────────────────────────────────
            var journeyId = journeyResponse.Journey?.JourneyId;
            var providerStartedPreviousState = kycCase.State;
            kycCase.State = KycAmlOnboardingCaseState.ProviderChecksStarted;
            kycCase.EvidenceState = KycAmlOnboardingEvidenceState.PendingVerification;
            kycCase.VerificationJourneyId = journeyId;
            kycCase.UpdatedAt = _time.GetUtcNow();

            kycCase.Actions.Add(new KycAmlOnboardingActorAction
            {
                ActionId = Guid.NewGuid().ToString(),
                Kind = KycAmlOnboardingActionKind.AddNote,
                ActorId = actorId,
                Timestamp = _time.GetUtcNow(),
                Rationale = "Provider checks initiated.",
                Notes = $"JourneyId={journeyId}"
            });
            kycCase.Timeline.Add(BuildTimelineEvent(
                KycAmlOnboardingTimelineEventType.ProviderChecksInitiated,
                actorId,
                kycCase.UpdatedAt,
                $"Provider checks initiated for subject {kycCase.SubjectId}.",
                fromState: providerStartedPreviousState,
                toState: kycCase.State,
                correlationId: correlationId,
                metadata: new Dictionary<string, string>
                {
                    ["journeyId"] = journeyId ?? string.Empty,
                    ["executionMode"] = request.ExecutionMode.ToString()
                }));

            _logger.LogInformation(
                "InitiateProviderChecks: checks started for case {CaseId}, JourneyId={JourneyId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(journeyId ?? "(none)"),
                LoggingHelper.SanitizeLogInput(correlationId));

            EmitWebhookFireAndForget(new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceCaseStateTransitioned,
                Actor = actorId
            });

            return new InitiateProviderChecksResponse
            {
                Success = true,
                Case = kycCase,
                VerificationJourneyId = journeyId,
                CorrelationId = correlationId
            };
        }

        // ── RecordReviewerActionAsync ────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<RecordReviewerActionResponse> RecordReviewerActionAsync(
            string caseId,
            RecordReviewerActionRequest request,
            string actorId)
        {
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            // ── 1. Case lookup ───────────────────────────────────────────────────
            if (!_cases.TryGetValue(caseId, out var kycCase))
            {
                return Task.FromResult(new RecordReviewerActionResponse
                {
                    Success = false,
                    ErrorCode = "CASE_NOT_FOUND",
                    ErrorMessage = $"No onboarding case found with ID '{LoggingHelper.SanitizeLogInput(caseId)}'.",
                    CorrelationId = correlationId
                });
            }

            // ── 2. Terminal state guard ──────────────────────────────────────────
            if (TerminalStates.Contains(kycCase.State) &&
                request.Kind != KycAmlOnboardingActionKind.AddNote)
            {
                return Task.FromResult(new RecordReviewerActionResponse
                {
                    Success = false,
                    Case = kycCase,
                    ErrorCode = "INVALID_STATE_TRANSITION",
                    ErrorMessage = $"Cannot record action '{request.Kind}' on a case in terminal state '{kycCase.State}'.",
                    CorrelationId = correlationId
                });
            }

            // ── 3. Apply state transition ────────────────────────────────────────
            KycAmlOnboardingCaseState? nextState = request.Kind switch
            {
                KycAmlOnboardingActionKind.Approve when ReviewableStates.Contains(kycCase.State)
                    => KycAmlOnboardingCaseState.Approved,

                KycAmlOnboardingActionKind.Approve
                    => null, // invalid transition

                KycAmlOnboardingActionKind.Reject
                    => KycAmlOnboardingCaseState.Rejected,

                KycAmlOnboardingActionKind.Escalate when ReviewableStates.Contains(kycCase.State)
                    => KycAmlOnboardingCaseState.Escalated,

                KycAmlOnboardingActionKind.Escalate
                    => null, // invalid transition

                KycAmlOnboardingActionKind.RequestAdditionalInfo when ReviewableStates.Contains(kycCase.State)
                    => KycAmlOnboardingCaseState.RequiresAdditionalInfo,

                KycAmlOnboardingActionKind.RequestAdditionalInfo
                    => null, // invalid transition

                KycAmlOnboardingActionKind.AddNote
                    => kycCase.State, // no change

                _ => null
            };

            if (nextState == null)
            {
                return Task.FromResult(new RecordReviewerActionResponse
                {
                    Success = false,
                    Case = kycCase,
                    ErrorCode = "INVALID_STATE_TRANSITION",
                    ErrorMessage = $"Action '{request.Kind}' is not valid for case in state '{kycCase.State}'.",
                    CorrelationId = correlationId
                });
            }

            // ── 4. Persist action ────────────────────────────────────────────────
            var action = new KycAmlOnboardingActorAction
            {
                ActionId = Guid.NewGuid().ToString(),
                Kind = request.Kind,
                ActorId = actorId,
                Timestamp = _time.GetUtcNow(),
                Rationale = request.Rationale,
                Notes = request.Notes
            };

            var previousState = kycCase.State;
            kycCase.Actions.Add(action);
            kycCase.State = nextState.Value;
            kycCase.UpdatedAt = _time.GetUtcNow();
            kycCase.Timeline.Add(BuildTimelineEvent(
                KycAmlOnboardingTimelineEventType.ReviewerActionRecorded,
                actorId,
                action.Timestamp,
                BuildReviewerActionSummary(request.Kind, kycCase.SubjectId, nextState.Value),
                fromState: previousState,
                toState: nextState.Value,
                correlationId: correlationId,
                metadata: new Dictionary<string, string>
                {
                    ["actionKind"] = request.Kind.ToString(),
                    ["rationale"] = request.Rationale ?? string.Empty
                }));

            _logger.LogInformation(
                "RecordReviewerAction: action {Kind} on case {CaseId} by {Actor}, new state {State}, CorrelationId={CorrelationId}",
                request.Kind,
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(actorId),
                nextState,
                LoggingHelper.SanitizeLogInput(correlationId));

            EmitWebhookFireAndForget(new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceCaseStateTransitioned,
                Actor = actorId
            });

            return Task.FromResult(new RecordReviewerActionResponse
            {
                Success = true,
                Action = action,
                Case = kycCase,
                CorrelationId = correlationId
            });
        }

        // ── GetEvidenceSummaryAsync ──────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<GetOnboardingEvidenceSummaryResponse> GetEvidenceSummaryAsync(string caseId)
        {
            if (!_cases.TryGetValue(caseId, out var kycCase))
            {
                return new GetOnboardingEvidenceSummaryResponse
                {
                    Success = false,
                    ErrorCode = "CASE_NOT_FOUND",
                    ErrorMessage = $"No onboarding case found with ID '{LoggingHelper.SanitizeLogInput(caseId)}'."
                };
            }

            // ── Provider not configured ───────────────────────────────────────────
            if (_journeyService == null)
            {
                return new GetOnboardingEvidenceSummaryResponse
                {
                    Success = true,
                    Summary = new KycAmlOnboardingEvidenceSummary
                    {
                        CaseId = caseId,
                        EvidenceState = KycAmlOnboardingEvidenceState.MissingConfiguration,
                        IsProviderBacked = false,
                        IsReleaseGrade = false,
                        ChecksCompleted = 0,
                        Blockers = new List<string> { "Provider service is not configured." },
                        ActionableGuidance = "Register ILiveProviderVerificationJourneyService in DI to enable provider-backed evidence.",
                        IsProviderConfigured = false
                    }
                };
            }

            // ── No journey yet ────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(kycCase.VerificationJourneyId))
            {
                var (evidenceState, guidance) = kycCase.State switch
                {
                    KycAmlOnboardingCaseState.Initiated =>
                        (KycAmlOnboardingEvidenceState.PendingVerification,
                         "Call POST /cases/{caseId}/initiate-checks to start provider checks."),
                    KycAmlOnboardingCaseState.ConfigurationMissing =>
                        (KycAmlOnboardingEvidenceState.MissingConfiguration,
                         "Provider configuration is missing. Register ILiveProviderVerificationJourneyService."),
                    KycAmlOnboardingCaseState.ProviderUnavailable =>
                        (KycAmlOnboardingEvidenceState.ProviderUnavailable,
                         "Provider was unavailable when checks were attempted. Retry initiation."),
                    _ => (KycAmlOnboardingEvidenceState.PendingVerification,
                          "Provider checks have not been initiated yet.")
                };

                return new GetOnboardingEvidenceSummaryResponse
                {
                    Success = true,
                    Summary = new KycAmlOnboardingEvidenceSummary
                    {
                        CaseId = caseId,
                        EvidenceState = evidenceState,
                        IsProviderBacked = false,
                        IsReleaseGrade = false,
                        ChecksCompleted = 0,
                        ActionableGuidance = guidance,
                        IsProviderConfigured = true
                    }
                };
            }

            // ── Query journey status ──────────────────────────────────────────────
            GetVerificationJourneyStatusResponse? journeyStatus = null;
            try
            {
                journeyStatus = await _journeyService.GetJourneyStatusAsync(kycCase.VerificationJourneyId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "GetEvidenceSummary: failed to query journey {JourneyId} for case {CaseId}",
                    LoggingHelper.SanitizeLogInput(kycCase.VerificationJourneyId),
                    LoggingHelper.SanitizeLogInput(caseId));
            }

            var journeyDegraded = journeyStatus?.Journey?.CurrentStage == VerificationJourneyStage.Degraded
                               || journeyStatus?.Journey?.CurrentStage == VerificationJourneyStage.Failed;

            if (journeyDegraded)
            {
                return new GetOnboardingEvidenceSummaryResponse
                {
                    Success = true,
                    Summary = new KycAmlOnboardingEvidenceSummary
                    {
                        CaseId = caseId,
                        EvidenceState = KycAmlOnboardingEvidenceState.ProviderUnavailable,
                        IsProviderBacked = false,
                        IsReleaseGrade = false,
                        ChecksCompleted = 0,
                        ProviderNames = new List<string>(),
                        Blockers = new List<string> { "Provider journey is in a degraded or failed state." },
                        ActionableGuidance = "Review provider configuration and retry provider checks.",
                        IsProviderConfigured = true
                    }
                };
            }

            // ── Derive evidence state from case outcome ───────────────────────────
            var (finalEvidenceState, finalGuidance, isProviderBacked, isReleaseGrade) =
                kycCase.State switch
                {
                    KycAmlOnboardingCaseState.Approved =>
                        (KycAmlOnboardingEvidenceState.AuthoritativeProviderBacked,
                         "Case approved with provider-backed evidence.",
                         true, true),

                    KycAmlOnboardingCaseState.Rejected =>
                        (KycAmlOnboardingEvidenceState.DegradedPartialEvidence,
                         "Case has been rejected. Evidence is archived.",
                         true, false),

                    KycAmlOnboardingCaseState.ProviderChecksStarted
                    or KycAmlOnboardingCaseState.PendingReview
                    or KycAmlOnboardingCaseState.UnderReview
                    or KycAmlOnboardingCaseState.RequiresAdditionalInfo
                    or KycAmlOnboardingCaseState.Escalated =>
                        (KycAmlOnboardingEvidenceState.PendingVerification,
                         "Provider checks are in progress or pending review.",
                         false, false),

                    KycAmlOnboardingCaseState.Initiated =>
                        (KycAmlOnboardingEvidenceState.PendingVerification,
                         "Provider checks have not completed yet.",
                         false, false),

                    _ => (KycAmlOnboardingEvidenceState.DegradedPartialEvidence,
                          "Evidence state could not be determined. Review case history.",
                          false, false)
                };

            return new GetOnboardingEvidenceSummaryResponse
            {
                Success = true,
                Summary = new KycAmlOnboardingEvidenceSummary
                {
                    CaseId = caseId,
                    EvidenceState = finalEvidenceState,
                    IsProviderBacked = isProviderBacked,
                    IsReleaseGrade = isReleaseGrade,
                    ChecksCompleted = isProviderBacked ? 1 : 0,
                    LastCheckedAt = kycCase.UpdatedAt,
                    ProviderNames = isProviderBacked ? new List<string> { "LiveProvider" } : new List<string>(),
                    ActionableGuidance = finalGuidance,
                    IsProviderConfigured = true
                }
            };
        }

        // ── ListCasesAsync ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListOnboardingCasesResponse> ListCasesAsync(ListOnboardingCasesRequest? request = null)
        {
            var correlationId = Guid.NewGuid().ToString();
            var pageSize = Math.Clamp(request?.PageSize ?? 50, 1, 500);

            IEnumerable<KycAmlOnboardingCase> query = _cases.Values;

            if (!string.IsNullOrWhiteSpace(request?.SubjectId))
                query = query.Where(c => c.SubjectId == request.SubjectId);

            if (request?.State.HasValue == true)
                query = query.Where(c => c.State == request.State.Value);

            // Simple offset-based pagination via numeric page token
            int skip = 0;
            if (!string.IsNullOrWhiteSpace(request?.PageToken) &&
                int.TryParse(request.PageToken, out var parsedSkip))
            {
                skip = parsedSkip;
            }

            var orderedQuery = query.OrderBy(c => c.CreatedAt);
            var totalCount = orderedQuery.Count();
            var page = orderedQuery.Skip(skip).Take(pageSize).ToList();

            return Task.FromResult(new ListOnboardingCasesResponse
            {
                Success = true,
                Cases = page,
                TotalCount = totalCount,
                CorrelationId = correlationId
            });
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void EmitWebhookFireAndForget(WebhookEvent evt)
        {
            if (_webhookService == null) return;
            _ = Task.Run(async () =>
            {
                try { await _webhookService.EmitEventAsync(evt); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Webhook emission failed for event {EventType}", evt.EventType);
                }
            });
        }

        private static string BuildReviewerActionSummary(
            KycAmlOnboardingActionKind actionKind,
            string subjectId,
            KycAmlOnboardingCaseState resultingState)
        {
            var sanitizedSubjectId = LoggingHelper.SanitizeLogInput(subjectId);
            var sanitizedResultingState = LoggingHelper.SanitizeLogInput(resultingState.ToString());
            return actionKind switch
            {
                KycAmlOnboardingActionKind.Approve => $"Reviewer approved onboarding for subject {sanitizedSubjectId}.",
                KycAmlOnboardingActionKind.Reject => $"Reviewer rejected onboarding for subject {sanitizedSubjectId}.",
                KycAmlOnboardingActionKind.Escalate => $"Reviewer escalated onboarding for subject {sanitizedSubjectId}.",
                KycAmlOnboardingActionKind.RequestAdditionalInfo => $"Reviewer requested additional information for subject {sanitizedSubjectId}.",
                _ => $"Reviewer note recorded for subject {sanitizedSubjectId}. Case remains in state {sanitizedResultingState}."
            };
        }

        private static KycAmlOnboardingTimelineEvent BuildTimelineEvent(
            KycAmlOnboardingTimelineEventType eventType,
            string actorId,
            DateTimeOffset occurredAt,
            string summary,
            KycAmlOnboardingCaseState? fromState = null,
            KycAmlOnboardingCaseState? toState = null,
            string? correlationId = null,
            Dictionary<string, string>? metadata = null)
        {
            return new KycAmlOnboardingTimelineEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                OccurredAt = occurredAt,
                ActorId = actorId,
                Summary = summary,
                FromState = fromState,
                ToState = toState,
                CorrelationId = correlationId,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
        }
    }
}

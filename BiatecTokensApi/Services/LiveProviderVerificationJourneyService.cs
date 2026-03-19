using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Orchestrates end-to-end live-provider KYC/AML verification journeys.
    ///
    /// Builds on <see cref="IKycAmlSignOffEvidenceService"/> to initiate and track
    /// identity-verification, AML-screening, and evidence-collection flows with
    /// structured diagnostics, approval-decision observability, and release-grade
    /// evidence generation. Fails closed when provider configuration is absent or
    /// providers are unreachable.
    /// </summary>
    public class LiveProviderVerificationJourneyService : ILiveProviderVerificationJourneyService
    {
        // ── In-memory store ───────────────────────────────────────────────────────

        private readonly ConcurrentDictionary<string, VerificationJourneyRecord> _journeys = new();

        // Idempotency key → journey ID reverse index
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();

        // ── Dependencies ──────────────────────────────────────────────────────────

        private readonly IKycAmlSignOffEvidenceService? _kycAmlService;
        private readonly IWebhookService? _webhookService;
        private readonly ILogger<LiveProviderVerificationJourneyService> _logger;
        private readonly TimeProvider _time;

        // ── Constructor ───────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the live-provider verification journey service.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="kycAmlService">Optional KYC/AML sign-off service. When null, journeys
        /// are created in <see cref="VerificationJourneyStage.Degraded"/> with configuration
        /// diagnostics indicating the missing dependency.</param>
        /// <param name="webhookService">Optional webhook service for event emission.</param>
        /// <param name="timeProvider">Substitutable time provider for deterministic testing.</param>
        public LiveProviderVerificationJourneyService(
            ILogger<LiveProviderVerificationJourneyService> logger,
            IKycAmlSignOffEvidenceService? kycAmlService = null,
            IWebhookService? webhookService = null,
            TimeProvider? timeProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kycAmlService = kycAmlService;
            _webhookService = webhookService;
            _time = timeProvider ?? TimeProvider.System;
        }

        // ── StartJourneyAsync ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<StartVerificationJourneyResponse> StartJourneyAsync(
            StartVerificationJourneyRequest request,
            string actorId)
        {
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            // ── 1. Input validation ───────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return StartFail("INVALID_SUBJECT_ID",
                    "SubjectId is required to start a verification journey.",
                    "Provide a non-empty SubjectId in the request.",
                    correlationId);
            }

            // ── 2. Idempotency check ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var idempotencyKey = $"{request.SubjectId}:{request.IdempotencyKey}";
                if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId) &&
                    _journeys.TryGetValue(existingId, out var existingJourney))
                {
                    _logger.LogInformation(
                        "Idempotency hit: returning existing journey {JourneyId} for subject {SubjectId}, CorrelationId={CorrelationId}",
                        existingId,
                        LoggingHelper.SanitizeLogInput(request.SubjectId),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return new StartVerificationJourneyResponse
                    {
                        Success = true,
                        Journey = existingJourney,
                        Diagnostics = existingJourney.Diagnostics,
                        CorrelationId = correlationId
                    };
                }
            }

            // ── 3. Fail-closed: RequireProviderBacked + Simulated ─────────────────
            var resolvedMode = ResolveExecutionMode(request.RequestedExecutionMode);
            if (request.RequireProviderBacked && resolvedMode == VerificationJourneyExecutionMode.Simulated)
            {
                return StartFail("SIMULATED_PROVIDER_REJECTED",
                    "RequireProviderBacked is true but execution mode resolved to Simulated. " +
                    "A simulated provider cannot produce release-grade evidence.",
                    "Configure a live or sandbox provider and set RequestedExecutionMode to " +
                    "LiveProvider or ProtectedSandbox before starting this journey.",
                    correlationId);
            }

            var now = _time.GetUtcNow();

            // ── 4. Build initial journey record ────────────────────────────────────
            var journey = new VerificationJourneyRecord
            {
                JourneyId = Guid.NewGuid().ToString(),
                SubjectId = request.SubjectId,
                CaseId = request.CaseId,
                ContextId = request.ContextId,
                ExecutionMode = resolvedMode,
                IsProviderBacked = resolvedMode != VerificationJourneyExecutionMode.Simulated,
                CreatedAt = now,
                UpdatedAt = now,
                InitiatedBy = actorId,
                CorrelationId = correlationId,
                CurrentStage = VerificationJourneyStage.NotStarted
            };

            // ── 5. Fail-closed: KycAmlService not registered ──────────────────────
            if (_kycAmlService == null)
            {
                journey.CurrentStage = VerificationJourneyStage.Degraded;
                journey.LatestProviderError =
                    "IKycAmlSignOffEvidenceService is not registered. KYC/AML provider configuration is missing.";
                journey.StageExplanation =
                    "Journey is degraded: the KYC/AML sign-off service is not configured. " +
                    "No provider calls were made. Register IKycAmlSignOffEvidenceService with " +
                    "a live or sandbox provider in Program.cs to activate this path.";

                var degradedDiagnostics = BuildDegradedDiagnostics(
                    journey,
                    "IKycAmlSignOffEvidenceService is not registered in the DI container.",
                    "Register IKycAmlSignOffEvidenceService with a live or sandbox provider " +
                    "configuration in Program.cs before starting verification journeys.");

                journey.Diagnostics = degradedDiagnostics;

                AddStep(journey, new VerificationJourneyStep
                {
                    StepName = "Configuration Validation",
                    StageAfterStep = VerificationJourneyStage.Degraded,
                    IsProviderBacked = false,
                    IsReleaseGrade = false,
                    OccurredAt = now,
                    Description = "Journey degraded: KYC/AML sign-off service is not configured.",
                    Success = false,
                    ErrorCode = "CONFIGURATION_MISSING",
                    ErrorMessage = "IKycAmlSignOffEvidenceService is not registered."
                });

                _journeys[journey.JourneyId] = journey;
                RegisterIdempotencyKey(request, journey.JourneyId);

                _ = EmitEventAsync(WebhookEventType.VerificationJourneyDegraded, journey, correlationId);

                _logger.LogWarning(
                    "Verification journey {JourneyId} degraded: KYC/AML service not configured. CorrelationId={CorrelationId}",
                    journey.JourneyId,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new StartVerificationJourneyResponse
                {
                    Success = false,
                    ErrorCode = "CONFIGURATION_MISSING",
                    ErrorMessage = "KYC/AML sign-off service is not configured. " +
                                   "Register IKycAmlSignOffEvidenceService in the DI container.",
                    Journey = journey,
                    Diagnostics = degradedDiagnostics,
                    NextAction = "Register IKycAmlSignOffEvidenceService with a live or sandbox " +
                                 "provider configuration in Program.cs.",
                    CorrelationId = correlationId
                };
            }

            // ── 6. Initiate KYC sign-off ───────────────────────────────────────────
            journey.CurrentStage = VerificationJourneyStage.KycInitiated;

            AddStep(journey, new VerificationJourneyStep
            {
                StepName = "KYC Initiation",
                StageAfterStep = VerificationJourneyStage.KycInitiated,
                IsProviderBacked = journey.IsProviderBacked,
                IsReleaseGrade = false,
                OccurredAt = now,
                Description = $"KYC identity check initiated in {resolvedMode} mode.",
                Success = true
            });

            try
            {
                var kycSignOffMode = MapToKycExecutionMode(resolvedMode);

                var initiateRequest = new InitiateKycAmlSignOffRequest
                {
                    SubjectId = request.SubjectId,
                    ContextId = request.ContextId,
                    CheckKind = KycAmlSignOffCheckKind.Combined,
                    RequestedExecutionMode = kycSignOffMode,
                    SubjectMetadata = request.SubjectMetadata,
                    IdempotencyKey = request.IdempotencyKey,
                    EvidenceValidityHours = 72
                };

                var initiateResponse = await _kycAmlService.InitiateSignOffAsync(
                    initiateRequest, actorId, correlationId);

                now = _time.GetUtcNow();

                if (!initiateResponse.Success || initiateResponse.Record == null)
                {
                    // Provider initiation failed — degrade the journey
                    journey.CurrentStage = VerificationJourneyStage.Degraded;
                    journey.LatestProviderError = initiateResponse.ErrorMessage ?? "Provider initiation returned no record.";
                    journey.StageExplanation =
                        $"Journey degraded during KYC initiation: {journey.LatestProviderError}";

                    journey.Diagnostics = BuildDegradedDiagnostics(
                        journey,
                        journey.LatestProviderError,
                        "Verify provider credentials, check network connectivity, and retry the journey.");

                    AddStep(journey, new VerificationJourneyStep
                    {
                        StepName = "KYC Provider Response",
                        StageAfterStep = VerificationJourneyStage.Degraded,
                        IsProviderBacked = journey.IsProviderBacked,
                        IsReleaseGrade = false,
                        OccurredAt = now,
                        Description = "KYC provider initiation failed.",
                        Success = false,
                        ErrorCode = initiateResponse.ErrorCode ?? "PROVIDER_INITIATION_FAILED",
                        ErrorMessage = initiateResponse.ErrorMessage
                    });

                    journey.UpdatedAt = now;
                    _journeys[journey.JourneyId] = journey;
                    RegisterIdempotencyKey(request, journey.JourneyId);

                    _ = EmitEventAsync(WebhookEventType.VerificationJourneyDegraded, journey, correlationId);

                    return new StartVerificationJourneyResponse
                    {
                        Success = false,
                        ErrorCode = initiateResponse.ErrorCode ?? "PROVIDER_INITIATION_FAILED",
                        ErrorMessage = initiateResponse.ErrorMessage ?? "KYC provider initiation failed.",
                        Journey = journey,
                        Diagnostics = journey.Diagnostics,
                        NextAction = "Verify KYC provider credentials and retry. " +
                                     "Check provider connectivity if the error persists.",
                        CorrelationId = correlationId
                    };
                }

                var record = initiateResponse.Record;
                journey.KycSignOffRecordId = record.RecordId;
                journey.AmlSignOffRecordId = record.RecordId; // combined check uses same record

                // ── 7. Evaluate readiness after initiation ─────────────────────────
                var readiness = await _kycAmlService.GetReadinessAsync(record.RecordId);
                now = _time.GetUtcNow();

                journey.IsProviderBacked = record.IsProviderBacked;
                journey.IsReleaseGrade = readiness.IsApprovalReady && record.IsProviderBacked;

                // Advance stage based on readiness
                if (readiness.IsApprovalReady)
                {
                    journey.CurrentStage = VerificationJourneyStage.ApprovalReady;
                    journey.StageExplanation =
                        "All KYC/AML checks completed with provider-backed evidence. Journey is approval-ready.";

                    AddStep(journey, new VerificationJourneyStep
                    {
                        StepName = "KYC/AML Completed — Approval Ready",
                        StageAfterStep = VerificationJourneyStage.ApprovalReady,
                        IsProviderBacked = record.IsProviderBacked,
                        IsReleaseGrade = journey.IsReleaseGrade,
                        OccurredAt = now,
                        Description = "All checks passed with provider-backed evidence. Ready for approval.",
                        ProviderName = record.KycProviderName ?? record.AmlProviderName,
                        ProviderReferenceId = record.KycProviderReferenceId ?? record.AmlProviderReferenceId,
                        Success = true
                    });

                    _ = EmitEventAsync(WebhookEventType.VerificationJourneyApprovalReady, journey, correlationId);
                }
                else if (record.Outcome == KycAmlSignOffOutcome.Rejected)
                {
                    journey.CurrentStage = VerificationJourneyStage.Rejected;
                    journey.StageExplanation =
                        $"KYC/AML verification resulted in rejection. Reason: {record.ReasonCode ?? "unspecified"}.";

                    AddStep(journey, new VerificationJourneyStep
                    {
                        StepName = "KYC/AML Rejection",
                        StageAfterStep = VerificationJourneyStage.Rejected,
                        IsProviderBacked = record.IsProviderBacked,
                        IsReleaseGrade = false,
                        OccurredAt = now,
                        Description = $"KYC/AML provider returned a rejection outcome: {record.ReasonCode}",
                        ProviderName = record.KycProviderName,
                        Success = false,
                        ErrorCode = "PROVIDER_REJECTION",
                        ErrorMessage = record.ReasonCode
                    });

                    _ = EmitEventAsync(WebhookEventType.VerificationJourneyFailed, journey, correlationId);
                }
                else if (record.Outcome == KycAmlSignOffOutcome.AdverseFindings ||
                         record.Outcome == KycAmlSignOffOutcome.Blocked)
                {
                    journey.CurrentStage = VerificationJourneyStage.SanctionsReview;
                    journey.StageExplanation =
                        "Adverse findings or watchlist match detected. Sanctions review is required.";

                    AddStep(journey, new VerificationJourneyStep
                    {
                        StepName = "Sanctions/Adverse Findings Review",
                        StageAfterStep = VerificationJourneyStage.SanctionsReview,
                        IsProviderBacked = record.IsProviderBacked,
                        IsReleaseGrade = false,
                        OccurredAt = now,
                        Description = "Provider returned adverse findings. Sanctions review required.",
                        ProviderName = record.AmlProviderName ?? record.KycProviderName,
                        ProviderReferenceId = record.AmlProviderReferenceId ?? record.KycProviderReferenceId,
                        Success = false,
                        ErrorCode = "ADVERSE_FINDINGS",
                        ErrorMessage = "Adverse findings or watchlist match detected."
                    });

                    _ = EmitEventAsync(WebhookEventType.VerificationJourneyStageAdvanced, journey, correlationId);
                }
                else if (record.Outcome == KycAmlSignOffOutcome.ProviderUnavailable ||
                         record.Outcome == KycAmlSignOffOutcome.Error ||
                         record.Outcome == KycAmlSignOffOutcome.MalformedCallback)
                {
                    journey.CurrentStage = VerificationJourneyStage.Degraded;
                    journey.LatestProviderError =
                        $"Provider returned degraded outcome: {record.Outcome}. ReasonCode: {record.ReasonCode}";
                    journey.StageExplanation =
                        $"Journey degraded: provider returned {record.Outcome}. " +
                        "No approval should be issued. Retry after resolving the provider condition.";

                    journey.Diagnostics = BuildDegradedDiagnostics(
                        journey,
                        journey.LatestProviderError,
                        "Check provider status, verify configuration, and retry the journey.");

                    AddStep(journey, new VerificationJourneyStep
                    {
                        StepName = "Provider Degraded Response",
                        StageAfterStep = VerificationJourneyStage.Degraded,
                        IsProviderBacked = record.IsProviderBacked,
                        IsReleaseGrade = false,
                        OccurredAt = now,
                        Description = $"Provider returned degraded outcome: {record.Outcome}.",
                        Success = false,
                        ErrorCode = record.Outcome.ToString().ToUpperInvariant(),
                        ErrorMessage = record.ReasonCode
                    });

                    _ = EmitEventAsync(WebhookEventType.VerificationJourneyDegraded, journey, correlationId);
                }
                else
                {
                    // KycPending / NeedsManualReview / Stale — awaiting provider or analyst
                    journey.CurrentStage = record.Outcome == KycAmlSignOffOutcome.NeedsManualReview
                        ? VerificationJourneyStage.UnderReview
                        : VerificationJourneyStage.KycPending;

                    journey.StageExplanation = record.Outcome == KycAmlSignOffOutcome.NeedsManualReview
                        ? "KYC/AML check requires manual analyst review before a decision can be made."
                        : "KYC/AML check initiated; awaiting provider callback or polling result.";

                    AddStep(journey, new VerificationJourneyStep
                    {
                        StepName = "KYC/AML Pending",
                        StageAfterStep = journey.CurrentStage,
                        IsProviderBacked = record.IsProviderBacked,
                        IsReleaseGrade = false,
                        OccurredAt = now,
                        Description = journey.StageExplanation,
                        ProviderName = record.KycProviderName,
                        ProviderReferenceId = record.KycProviderReferenceId,
                        Success = true
                    });

                    _ = EmitEventAsync(WebhookEventType.VerificationJourneyStageAdvanced, journey, correlationId);
                }

                // ── 8. Build final diagnostics ─────────────────────────────────────
                journey.Diagnostics = BuildDiagnostics(journey, record, readiness);
                journey.UpdatedAt = _time.GetUtcNow();

                _journeys[journey.JourneyId] = journey;
                RegisterIdempotencyKey(request, journey.JourneyId);

                _ = EmitEventAsync(WebhookEventType.VerificationJourneyStarted, journey, correlationId);

                _logger.LogInformation(
                    "Verification journey {JourneyId} started for subject {SubjectId} in stage {Stage}. CorrelationId={CorrelationId}",
                    journey.JourneyId,
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    journey.CurrentStage,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new StartVerificationJourneyResponse
                {
                    Success = true,
                    Journey = journey,
                    Diagnostics = journey.Diagnostics,
                    NextAction = journey.CurrentStage is VerificationJourneyStage.ApprovalReady or VerificationJourneyStage.Approved
                        ? null
                        : BuildNextActionGuidance(journey),
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                now = _time.GetUtcNow();

                journey.CurrentStage = VerificationJourneyStage.Degraded;
                journey.LatestProviderError = ex.Message;
                journey.StageExplanation =
                    "Journey degraded due to an unexpected error during provider initiation. " +
                    "Check diagnostics and provider configuration.";

                journey.Diagnostics = BuildDegradedDiagnostics(
                    journey,
                    ex.Message,
                    "Check provider configuration and connectivity. Review application logs for details.");

                AddStep(journey, new VerificationJourneyStep
                {
                    StepName = "Unexpected Error",
                    StageAfterStep = VerificationJourneyStage.Degraded,
                    IsProviderBacked = false,
                    IsReleaseGrade = false,
                    OccurredAt = now,
                    Description = "An unexpected error occurred during provider initiation.",
                    Success = false,
                    ErrorCode = "UNEXPECTED_ERROR",
                    ErrorMessage = ex.Message
                });

                journey.UpdatedAt = now;
                _journeys[journey.JourneyId] = journey;
                RegisterIdempotencyKey(request, journey.JourneyId);

                _ = EmitEventAsync(WebhookEventType.VerificationJourneyDegraded, journey, correlationId);

                _logger.LogError(ex,
                    "Verification journey {JourneyId} degraded due to unexpected exception. CorrelationId={CorrelationId}",
                    journey.JourneyId,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new StartVerificationJourneyResponse
                {
                    Success = false,
                    ErrorCode = "UNEXPECTED_ERROR",
                    ErrorMessage = "An unexpected error occurred during KYC/AML provider initiation.",
                    Journey = journey,
                    Diagnostics = journey.Diagnostics,
                    NextAction = "Check provider configuration and application logs for details.",
                    CorrelationId = correlationId
                };
            }
        }

        // ── GetJourneyStatusAsync ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<GetVerificationJourneyStatusResponse> GetJourneyStatusAsync(
            string journeyId,
            string? correlationId = null)
        {
            correlationId ??= Guid.NewGuid().ToString();

            if (!_journeys.TryGetValue(journeyId, out var journey))
            {
                return new GetVerificationJourneyStatusResponse
                {
                    Success = false,
                    ErrorMessage = $"Verification journey '{journeyId}' not found.",
                    CorrelationId = correlationId
                };
            }

            var decisionResp = await EvaluateApprovalDecisionAsync(journeyId, correlationId);

            return new GetVerificationJourneyStatusResponse
            {
                Success = true,
                Journey = journey,
                ApprovalDecision = decisionResp.Decision,
                CorrelationId = correlationId
            };
        }

        // ── EvaluateApprovalDecisionAsync ─────────────────────────────────────────

        /// <inheritdoc/>
        public Task<EvaluateApprovalDecisionResponse> EvaluateApprovalDecisionAsync(
            string journeyId,
            string? correlationId = null)
        {
            correlationId ??= Guid.NewGuid().ToString();

            if (!_journeys.TryGetValue(journeyId, out var journey))
            {
                return Task.FromResult(new EvaluateApprovalDecisionResponse
                {
                    Success = false,
                    ErrorMessage = $"Verification journey '{journeyId}' not found.",
                    CorrelationId = correlationId
                });
            }

            var explanation = BuildApprovalDecisionExplanation(journey);

            return Task.FromResult(new EvaluateApprovalDecisionResponse
            {
                Success = true,
                Decision = explanation,
                CorrelationId = correlationId
            });
        }

        // ── GenerateReleaseEvidenceAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GenerateVerificationJourneyEvidenceResponse> GenerateReleaseEvidenceAsync(
            string journeyId,
            GenerateVerificationJourneyEvidenceRequest request,
            string actorId)
        {
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            if (!_journeys.TryGetValue(journeyId, out var journey))
            {
                return Task.FromResult(new GenerateVerificationJourneyEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "JOURNEY_NOT_FOUND",
                    ErrorMessage = $"Verification journey '{journeyId}' not found.",
                    CorrelationId = correlationId
                });
            }

            // Fail-closed: RequireProviderBacked + Simulated
            if (request.RequireProviderBacked &&
                journey.ExecutionMode == VerificationJourneyExecutionMode.Simulated)
            {
                return Task.FromResult(new GenerateVerificationJourneyEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "SIMULATED_EVIDENCE_REJECTED",
                    ErrorMessage =
                        "RequireProviderBacked is true but the journey was executed in Simulated mode. " +
                        "Simulated evidence does not qualify as release-grade.",
                    CorrelationId = correlationId
                });
            }

            var now = _time.GetUtcNow();
            var decision = BuildApprovalDecisionExplanation(journey);
            var diagnostics = journey.Diagnostics;

            // Build provider references map
            var providerRefs = new Dictionary<string, string>();
            foreach (var step in journey.Steps)
            {
                if (!string.IsNullOrEmpty(step.ProviderName) &&
                    !string.IsNullOrEmpty(step.ProviderReferenceId))
                {
                    providerRefs[step.StepName] = step.ProviderReferenceId;
                }
            }

            // Compute content hash
            var contentHash = ComputeContentHash(journey, decision);

            var evidence = new VerificationJourneyReleaseEvidence
            {
                JourneyId = journeyId,
                SubjectId = journey.SubjectId,
                CaseId = journey.CaseId,
                ReleaseTag = request.ReleaseTag,
                WorkflowRunReference = request.WorkflowRunReference,
                ContentHash = contentHash,
                GeneratedAt = now,
                IsReleaseGrade = journey.IsReleaseGrade,
                IsProviderBacked = journey.IsProviderBacked,
                ExecutionMode = journey.ExecutionMode,
                JourneyStageAtGeneration = journey.CurrentStage,
                Steps = journey.Steps.ToList(),
                ApprovalDecision = decision,
                ProviderReferences = providerRefs,
                Diagnostics = diagnostics
            };

            _logger.LogInformation(
                "Release evidence generated for journey {JourneyId}: IsReleaseGrade={IsReleaseGrade}, " +
                "IsProviderBacked={IsProviderBacked}, ReleaseTag={ReleaseTag}, CorrelationId={CorrelationId}",
                journeyId,
                evidence.IsReleaseGrade,
                evidence.IsProviderBacked,
                LoggingHelper.SanitizeLogInput(request.ReleaseTag ?? "(none)"),
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new GenerateVerificationJourneyEvidenceResponse
            {
                Success = true,
                Evidence = evidence,
                CorrelationId = correlationId
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static VerificationJourneyExecutionMode ResolveExecutionMode(
            VerificationJourneyExecutionMode requested) =>
            requested == VerificationJourneyExecutionMode.Unknown
                ? VerificationJourneyExecutionMode.Simulated
                : requested;

        private static KycAmlSignOffExecutionMode MapToKycExecutionMode(
            VerificationJourneyExecutionMode mode) =>
            mode switch
            {
                VerificationJourneyExecutionMode.LiveProvider => KycAmlSignOffExecutionMode.LiveProvider,
                VerificationJourneyExecutionMode.ProtectedSandbox => KycAmlSignOffExecutionMode.ProtectedSandbox,
                _ => KycAmlSignOffExecutionMode.Simulated
            };

        private static void AddStep(VerificationJourneyRecord journey, VerificationJourneyStep step)
        {
            journey.Steps.Add(step);
        }

        private void RegisterIdempotencyKey(StartVerificationJourneyRequest request, string journeyId)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var key = $"{request.SubjectId}:{request.IdempotencyKey}";
                _idempotencyIndex.TryAdd(key, journeyId);
            }
        }

        private static VerificationJourneyDiagnostics BuildDiagnostics(
            VerificationJourneyRecord journey,
            KycAmlSignOffRecord record,
            KycAmlSignOffReadinessResponse readiness)
        {
            var now = DateTimeOffset.UtcNow;
            var isKycComplete = record.CheckKind is KycAmlSignOffCheckKind.IdentityKyc or KycAmlSignOffCheckKind.Combined
                && record.Outcome != KycAmlSignOffOutcome.Pending
                && record.Outcome != KycAmlSignOffOutcome.ProviderUnavailable
                && record.Outcome != KycAmlSignOffOutcome.Error;

            var isAmlComplete = record.CheckKind is KycAmlSignOffCheckKind.AmlScreening or KycAmlSignOffCheckKind.Combined
                && record.Outcome != KycAmlSignOffOutcome.Pending
                && record.Outcome != KycAmlSignOffOutcome.ProviderUnavailable
                && record.Outcome != KycAmlSignOffOutcome.Error;

            var diag = new VerificationJourneyDiagnostics
            {
                IsConfigurationValid = true,
                IsProviderReachable = record.Outcome != KycAmlSignOffOutcome.ProviderUnavailable,
                IsKycComplete = isKycComplete,
                IsAmlComplete = isAmlComplete,
                IsProviderBacked = record.IsProviderBacked,
                IsReleaseGrade = readiness.IsApprovalReady && record.IsProviderBacked,
                ExecutionMode = journey.ExecutionMode,
                EvaluatedAt = now
            };

            if (!readiness.IsApprovalReady)
            {
                diag.ActionableGuidance = BuildNextActionGuidance(journey);
            }

            if (journey.CurrentStage == VerificationJourneyStage.Degraded)
            {
                diag.DegradedStateReason = journey.LatestProviderError;
                diag.ActionableGuidance ??= "Journey is degraded. Resolve the provider condition and retry.";
            }

            return diag;
        }

        private VerificationJourneyDiagnostics BuildDegradedDiagnostics(
            VerificationJourneyRecord journey,
            string? degradedReason,
            string? actionableGuidance)
        {
            return new VerificationJourneyDiagnostics
            {
                IsConfigurationValid = false,
                IsProviderReachable = false,
                IsKycComplete = false,
                IsAmlComplete = false,
                IsProviderBacked = false,
                IsReleaseGrade = false,
                ExecutionMode = journey.ExecutionMode,
                DegradedStateReason = degradedReason,
                ActionableGuidance = actionableGuidance,
                ActiveBlockers = new List<VerificationJourneyBlocker>
                {
                    new()
                    {
                        Code = "DEGRADED_STATE",
                        Description = degradedReason ?? "Provider degraded or configuration missing.",
                        Severity = JourneyBlockerSeverity.Critical,
                        IsRemediable = true,
                        RemediationGuidance = actionableGuidance,
                        RecordedAt = _time.GetUtcNow()
                    }
                },
                EvaluatedAt = _time.GetUtcNow()
            };
        }

        private static string BuildNextActionGuidance(VerificationJourneyRecord journey) =>
            journey.CurrentStage switch
            {
                VerificationJourneyStage.NotStarted =>
                    "Call StartJourney to initiate the verification journey.",
                VerificationJourneyStage.KycInitiated or VerificationJourneyStage.KycPending =>
                    "Awaiting KYC provider response. Poll journey status or wait for provider callback.",
                VerificationJourneyStage.KycCompleted or VerificationJourneyStage.AmlInitiated or
                VerificationJourneyStage.AmlPending =>
                    "Awaiting AML provider response. Poll journey status or wait for provider callback.",
                VerificationJourneyStage.AmlCompleted =>
                    "KYC and AML checks completed. Evaluate approval decision.",
                VerificationJourneyStage.UnderReview =>
                    "Journey is under manual analyst review. Await analyst decision.",
                VerificationJourneyStage.ApprovalReady =>
                    "Journey is approval-ready. Issue approval decision.",
                VerificationJourneyStage.RequiresAction =>
                    "Additional information or action required. Review active blockers.",
                VerificationJourneyStage.SanctionsReview =>
                    "Sanctions or adverse-findings review is active. Analyst decision required.",
                VerificationJourneyStage.Escalated =>
                    "Journey is escalated for senior review. Await escalation decision.",
                VerificationJourneyStage.Degraded =>
                    "Journey is degraded. Resolve the provider condition and start a new journey.",
                VerificationJourneyStage.Failed =>
                    "Journey has failed. Review diagnostics and start a new journey after resolving root cause.",
                VerificationJourneyStage.Rejected =>
                    "Subject has been rejected. No further action is possible on this journey.",
                VerificationJourneyStage.Approved =>
                    "Subject has been approved. Journey is complete.",
                _ => "Review journey diagnostics for next steps."
            };

        private static ApprovalDecisionExplanation BuildApprovalDecisionExplanation(
            VerificationJourneyRecord journey)
        {
            var now = DateTimeOffset.UtcNow;

            var explanation = new ApprovalDecisionExplanation
            {
                JourneyId = journey.JourneyId,
                CurrentStage = journey.CurrentStage,
                IsApprovalReady = journey.CurrentStage is VerificationJourneyStage.ApprovalReady or
                                  VerificationJourneyStage.Approved,
                IsReleaseGrade = journey.IsReleaseGrade,
                IsProviderBacked = journey.IsProviderBacked,
                Diagnostics = journey.Diagnostics,
                EvaluatedAt = now
            };

            // Classify checks from step history
            foreach (var step in journey.Steps)
            {
                if (step.Success)
                    explanation.ChecksPassed.Add(step.StepName);
                else if (step.StageAfterStep is VerificationJourneyStage.KycPending
                         or VerificationJourneyStage.AmlPending
                         or VerificationJourneyStage.UnderReview)
                    explanation.ChecksPending.Add(step.StepName);
                else if (!step.Success)
                    explanation.ChecksFailed.Add(step.StepName);
            }

            switch (journey.CurrentStage)
            {
                case VerificationJourneyStage.ApprovalReady:
                    explanation.OutcomeSummary =
                        "All KYC/AML checks passed with provider-backed evidence. Subject is approval-ready.";
                    explanation.ApprovalRationale =
                        $"Identity verification and AML screening completed in {journey.ExecutionMode} mode " +
                        "with no adverse findings. Evidence is" +
                        (journey.IsReleaseGrade ? " release-grade." : " not release-grade (check provider mode).");
                    explanation.RequiredNextAction = null;
                    break;

                case VerificationJourneyStage.Approved:
                    explanation.OutcomeSummary = "Subject has been approved.";
                    explanation.ApprovalRationale = "All checks passed and approval decision was issued.";
                    explanation.RequiredNextAction = null;
                    break;

                case VerificationJourneyStage.Rejected:
                    explanation.OutcomeSummary = "Subject has been rejected.";
                    explanation.RejectionReason = journey.LatestProviderError ?? "Rejection reason unspecified.";
                    explanation.RequiredNextAction = "No further action possible. Review rejection reason.";
                    break;

                case VerificationJourneyStage.SanctionsReview:
                    explanation.OutcomeSummary =
                        "Adverse findings or watchlist match detected. Sanctions review is required.";
                    explanation.RequiredNextAction =
                        "Assign a senior compliance analyst to review adverse findings before proceeding.";
                    break;

                case VerificationJourneyStage.UnderReview:
                    explanation.OutcomeSummary = "KYC/AML checks require manual analyst review.";
                    explanation.RequiredNextAction =
                        "Assign an analyst to review the case and submit a manual decision.";
                    break;

                case VerificationJourneyStage.Degraded:
                    explanation.OutcomeSummary =
                        "Journey is in a degraded state. No approval should be issued until the " +
                        "provider condition is resolved.";
                    explanation.RequiredNextAction =
                        journey.Diagnostics.ActionableGuidance ??
                        "Resolve the provider configuration or connectivity issue and start a new journey.";
                    break;

                case VerificationJourneyStage.Failed:
                    explanation.OutcomeSummary = "Journey has failed. Evidence cannot be used for approval gating.";
                    explanation.RequiredNextAction =
                        "Review diagnostics, resolve root cause, and start a new journey.";
                    break;

                case VerificationJourneyStage.Escalated:
                    explanation.OutcomeSummary = "Journey has been escalated for senior review.";
                    explanation.RequiredNextAction = "Await senior analyst decision.";
                    break;

                case VerificationJourneyStage.RequiresAction:
                    explanation.OutcomeSummary = "Journey requires operator action before it can advance.";
                    explanation.RequiredNextAction = "Review active blockers and provide required information.";
                    break;

                default:
                    explanation.OutcomeSummary =
                        $"Journey is at stage {journey.CurrentStage}. {journey.StageExplanation}";
                    explanation.RequiredNextAction = BuildNextActionGuidance(journey);
                    break;
            }

            return explanation;
        }

        private static string ComputeContentHash(
            VerificationJourneyRecord journey,
            ApprovalDecisionExplanation decision)
        {
            var payload = JsonSerializer.Serialize(new
            {
                journey.JourneyId,
                journey.SubjectId,
                journey.CaseId,
                journey.ExecutionMode,
                JourneyCurrentStage = journey.CurrentStage,
                JourneyIsReleaseGrade = journey.IsReleaseGrade,
                Steps = journey.Steps.Select(s => new { s.StepId, s.StepName, s.Success, s.OccurredAt }),
                DecisionStage = decision.CurrentStage,
                decision.IsApprovalReady,
                DecisionIsReleaseGrade = decision.IsReleaseGrade
            });

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static StartVerificationJourneyResponse StartFail(
            string errorCode, string errorMessage, string nextAction, string correlationId) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                NextAction = nextAction,
                Diagnostics = new VerificationJourneyDiagnostics
                {
                    IsConfigurationValid = false,
                    IsProviderReachable = false,
                    ActionableGuidance = nextAction,
                    EvaluatedAt = DateTimeOffset.UtcNow
                },
                CorrelationId = correlationId
            };

        private Task EmitEventAsync(
            WebhookEventType eventType,
            VerificationJourneyRecord journey,
            string correlationId)
        {
            if (_webhookService == null) return Task.CompletedTask;

            var ev = new WebhookEvent
            {
                EventType = eventType,
                Timestamp = _time.GetUtcNow().UtcDateTime,
                Actor = "VerificationJourneyService",
                Data = new Dictionary<string, object>
                {
                    ["journeyId"] = journey.JourneyId,
                    ["subjectId"] = journey.SubjectId,
                    ["caseId"] = journey.CaseId ?? string.Empty,
                    ["currentStage"] = journey.CurrentStage.ToString(),
                    ["executionMode"] = journey.ExecutionMode.ToString(),
                    ["isProviderBacked"] = journey.IsProviderBacked,
                    ["isReleaseGrade"] = journey.IsReleaseGrade,
                    ["correlationId"] = correlationId
                }
            };

            return _webhookService.EmitEventAsync(ev);
        }
    }
}

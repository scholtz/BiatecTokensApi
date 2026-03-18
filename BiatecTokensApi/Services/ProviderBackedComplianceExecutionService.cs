using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.ProviderBackedCompliance;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Provider-backed compliance case execution service.
    ///
    /// <para>
    /// This service bridges the compliance case lifecycle (approve / reject /
    /// return-for-information / sanctions review / escalation) with external
    /// provider validation, enforces fail-closed behaviour for protected paths,
    /// and produces durable, release-grade evidence artifacts.
    /// </para>
    ///
    /// <para>
    /// Fail-closed rules (all produce a non-success response with diagnostics, never
    /// a silent approval):
    /// <list type="bullet">
    ///   <item>RequireProviderBacked=true but ExecutionMode=Simulated → ConfigurationMissing</item>
    ///   <item>RequireKycAmlSignOff=true but sign-off evidence absent/stale → InsufficientEvidence</item>
    ///   <item>Case not found → Failed with actionable guidance</item>
    ///   <item>Invalid state transition → InvalidState with prior/target state info</item>
    ///   <item>Underlying service throws → Failed with structured diagnostics</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ProviderBackedComplianceExecutionService : IProviderBackedComplianceExecutionService
    {
        private readonly IComplianceCaseManagementService _caseService;
        private readonly IKycAmlSignOffEvidenceService? _kycAmlService;
        private readonly IWebhookService? _webhookService;
        private readonly ILogger<ProviderBackedComplianceExecutionService> _logger;
        private readonly TimeProvider _timeProvider;

        // Execution history keyed by caseId — ConcurrentQueue is safe for concurrent Enqueue calls
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ProviderBackedCaseExecutionEvidence>> _history = new();

        /// <summary>
        /// Initialises the service.
        /// </summary>
        /// <param name="caseService">Compliance case management service (required).</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="kycAmlService">Optional KYC/AML sign-off evidence service for provider validation.</param>
        /// <param name="timeProvider">Substitutable time provider (inject fake for tests).</param>
        /// <param name="webhookService">Optional webhook service for event emission.</param>
        public ProviderBackedComplianceExecutionService(
            IComplianceCaseManagementService caseService,
            ILogger<ProviderBackedComplianceExecutionService> logger,
            IKycAmlSignOffEvidenceService? kycAmlService = null,
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            _caseService = caseService;
            _logger = logger;
            _kycAmlService = kycAmlService;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _webhookService = webhookService;
        }

        // ── ExecuteDecisionAsync ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ExecuteProviderBackedDecisionResponse> ExecuteDecisionAsync(
            string caseId,
            ExecuteProviderBackedDecisionRequest request,
            string actorId)
        {
            var executionId = Guid.NewGuid().ToString("N");
            var correlationId = request?.CorrelationId ?? executionId;

            _logger.LogInformation(
                "ProviderBackedExecution.Start. ExecutionId={ExecutionId} CaseId={CaseId} DecisionKind={DecisionKind} ExecutionMode={ExecutionMode} Actor={Actor} CorrelationId={CorrelationId}",
                executionId,
                LoggingHelper.SanitizeLogInput(caseId),
                request?.DecisionKind.ToString() ?? "null",
                request?.ExecutionMode.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (request == null)
            {
                return FailResponse(executionId, correlationId,
                    ProviderBackedCaseExecutionStatus.Failed,
                    "REQUEST_NULL", "Request body is required.",
                    "Provide a valid ExecuteProviderBackedDecisionRequest body.");
            }

            if (string.IsNullOrWhiteSpace(caseId))
            {
                return FailResponse(executionId, correlationId,
                    ProviderBackedCaseExecutionStatus.Failed,
                    "MISSING_CASE_ID", "CaseId is required.",
                    "Provide a non-empty case ID in the route.");
            }

            // Fail closed: provider-backed requirement check
            if (request.RequireProviderBacked && request.ExecutionMode == ProviderBackedCaseExecutionMode.Simulated)
            {
                _logger.LogWarning(
                    "ProviderBackedExecution.FailClosed.SimulatedNotAllowed. ExecutionId={ExecutionId} CaseId={CaseId} CorrelationId={CorrelationId}",
                    executionId,
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return FailResponse(executionId, correlationId,
                    ProviderBackedCaseExecutionStatus.ConfigurationMissing,
                    "PROVIDER_BACKED_REQUIRED",
                    "RequireProviderBacked=true but ExecutionMode=Simulated. " +
                    "This execution path requires LiveProvider or ProtectedSandbox mode.",
                    "Set ExecutionMode to LiveProvider or ProtectedSandbox and configure " +
                    "the appropriate provider credentials before retrying.",
                    new ProviderBackedCaseExecutionDiagnostics
                    {
                        IsConfigurationPresent = false,
                        IsProviderAvailable = false,
                        IsKycSignOffComplete = false,
                        IsAmlSignOffComplete = false,
                        ExecutionMode = request.ExecutionMode,
                        ConfigurationFailures = new List<string>
                        {
                            "ExecutionMode=Simulated is not permitted when RequireProviderBacked=true. " +
                            "Configure provider credentials and set ExecutionMode to LiveProvider or ProtectedSandbox."
                        },
                        ActionableGuidance =
                            "To run the protected execution path, configure live or sandbox provider credentials " +
                            "and set ExecutionMode to LiveProvider or ProtectedSandbox in the request."
                    });
            }

            // Load the case
            GetComplianceCaseResponse caseResp;
            try
            {
                caseResp = await _caseService.GetCaseAsync(caseId, actorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ProviderBackedExecution.CaseLoadFailed. ExecutionId={ExecutionId} CaseId={CaseId} CorrelationId={CorrelationId}",
                    executionId,
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return FailResponse(executionId, correlationId,
                    ProviderBackedCaseExecutionStatus.Failed,
                    "CASE_LOAD_ERROR",
                    $"Failed to load compliance case '{caseId}': {ex.Message}",
                    "Verify the case ID is valid and the compliance case service is operational.");
            }

            if (!caseResp.Success || caseResp.Case == null)
            {
                _logger.LogWarning(
                    "ProviderBackedExecution.CaseNotFound. ExecutionId={ExecutionId} CaseId={CaseId} CorrelationId={CorrelationId}",
                    executionId,
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return FailResponse(executionId, correlationId,
                    ProviderBackedCaseExecutionStatus.Failed,
                    "CASE_NOT_FOUND",
                    caseResp.ErrorMessage ?? $"Compliance case '{caseId}' not found.",
                    "Verify the case ID and ensure the case has not been purged.");
            }

            var complianceCase = caseResp.Case;
            var previousState = complianceCase.State.ToString();

            // Build base diagnostics
            var diagnostics = new ProviderBackedCaseExecutionDiagnostics
            {
                IsConfigurationPresent = request.ExecutionMode != ProviderBackedCaseExecutionMode.LiveProvider ||
                                         _kycAmlService != null,
                IsProviderAvailable = _kycAmlService != null,
                ExecutionMode = request.ExecutionMode
            };

            // Provider-backed KYC/AML sign-off validation
            if (request.RequireKycAmlSignOff &&
                request.ExecutionMode != ProviderBackedCaseExecutionMode.Simulated)
            {
                var (kycOk, amlOk, kycAmlGuidance) =
                    await ValidateKycAmlSignOffAsync(complianceCase.SubjectId, request.ExecutionMode, diagnostics);

                diagnostics.IsKycSignOffComplete = kycOk;
                diagnostics.IsAmlSignOffComplete = amlOk;

                if (!kycOk || !amlOk)
                {
                    _logger.LogWarning(
                        "ProviderBackedExecution.InsufficientEvidence. ExecutionId={ExecutionId} CaseId={CaseId} " +
                        "KycOk={KycOk} AmlOk={AmlOk} CorrelationId={CorrelationId}",
                        executionId,
                        LoggingHelper.SanitizeLogInput(caseId),
                        kycOk, amlOk,
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return FailResponse(executionId, correlationId,
                        ProviderBackedCaseExecutionStatus.InsufficientEvidence,
                        "KYC_AML_SIGNOFF_REQUIRED",
                        "RequireKycAmlSignOff=true but KYC/AML sign-off evidence is absent, stale, or not provider-backed.",
                        kycAmlGuidance ?? "Initiate and complete KYC/AML sign-off via /api/v1/kyc-aml-signoff before executing this decision.",
                        diagnostics);
                }
            }
            else
            {
                // For simulated mode or when KYC/AML check not required, mark as not checked
                diagnostics.IsKycSignOffComplete = request.ExecutionMode == ProviderBackedCaseExecutionMode.Simulated;
                diagnostics.IsAmlSignOffComplete = request.ExecutionMode == ProviderBackedCaseExecutionMode.Simulated;
            }

            // Validate reason is provided for rejection/RFI/sanctions
            if (string.IsNullOrWhiteSpace(request.Reason) &&
                request.DecisionKind is ProviderBackedCaseDecisionKind.Reject
                    or ProviderBackedCaseDecisionKind.ReturnForInformation
                    or ProviderBackedCaseDecisionKind.SanctionsReview)
            {
                return FailResponse(executionId, correlationId,
                    ProviderBackedCaseExecutionStatus.Failed,
                    "REASON_REQUIRED",
                    $"A reason is required for decision kind '{request.DecisionKind}'.",
                    "Provide a non-empty Reason in the request body.",
                    diagnostics);
            }

            // Execute the decision
            var auditSteps = new List<string>();
            auditSteps.Add($"Execution started. Mode={request.ExecutionMode}, Decision={request.DecisionKind}");

            string targetState;
            bool decisionSuccess;
            string? decisionError;
            string? decisionErrorCode;

            (decisionSuccess, targetState, decisionError, decisionErrorCode) =
                await DispatchDecisionAsync(caseId, request, actorId, complianceCase, auditSteps);

            if (!decisionSuccess)
            {
                var status = (decisionErrorCode == "INVALID_TRANSITION" ||
                              decisionErrorCode == "INVALID_STATE_TRANSITION")
                    ? ProviderBackedCaseExecutionStatus.InvalidState
                    : ProviderBackedCaseExecutionStatus.Failed;

                _logger.LogWarning(
                    "ProviderBackedExecution.DecisionFailed. ExecutionId={ExecutionId} CaseId={CaseId} " +
                    "ErrorCode={ErrorCode} CorrelationId={CorrelationId}",
                    executionId,
                    LoggingHelper.SanitizeLogInput(caseId),
                    decisionErrorCode,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return FailResponse(executionId, correlationId, status,
                    decisionErrorCode ?? "DECISION_FAILED",
                    decisionError ?? "The compliance case decision could not be executed.",
                    BuildNextAction(request.DecisionKind, complianceCase.State, decisionErrorCode),
                    diagnostics);
            }

            auditSteps.Add($"Decision executed. PreviousState={previousState}, TargetState={targetState}");

            // Build evidence artifact
            bool isReleaseGrade = request.ExecutionMode != ProviderBackedCaseExecutionMode.Simulated &&
                                  diagnostics.IsProviderAvailable &&
                                  diagnostics.IsConfigurationPresent;

            var evidence = new ProviderBackedCaseExecutionEvidence
            {
                ExecutionId = executionId,
                ExecutedAt = _timeProvider.GetUtcNow(),
                CaseId = caseId,
                ExecutionMode = request.ExecutionMode,
                DecisionKind = request.DecisionKind,
                TargetState = targetState,
                PreviousState = previousState,
                IsReleaseGradeEvidence = isReleaseGrade,
                Diagnostics = diagnostics,
                DecisionReason = request.Reason,
                ActorId = actorId,
                AuditSteps = auditSteps,
                CorrelationId = correlationId
            };

            // Persist to in-memory history — GetOrAdd returns the canonical queue, Enqueue is atomic
            _history.GetOrAdd(caseId, _ => new ConcurrentQueue<ProviderBackedCaseExecutionEvidence>())
                    .Enqueue(evidence);

            // Emit webhook events
            EmitEventFireAndForget(WebhookEventType.ComplianceCaseExecutionCompleted, actorId, caseId, new
            {
                caseId,
                executionId,
                decisionKind = request.DecisionKind.ToString(),
                executionMode = request.ExecutionMode.ToString(),
                isProviderBacked = evidence.IsProviderBacked,
                isReleaseGrade,
                targetState,
                correlationId
            });

            if (request.DecisionKind == ProviderBackedCaseDecisionKind.SanctionsReview)
            {
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseSanctionsReviewRequested, actorId, caseId, new
                {
                    caseId,
                    executionId,
                    reason = request.Reason,
                    correlationId
                });
            }

            _logger.LogInformation(
                "ProviderBackedExecution.Completed. ExecutionId={ExecutionId} CaseId={CaseId} " +
                "TargetState={TargetState} IsReleaseGrade={IsReleaseGrade} CorrelationId={CorrelationId}",
                executionId,
                LoggingHelper.SanitizeLogInput(caseId),
                targetState,
                isReleaseGrade,
                LoggingHelper.SanitizeLogInput(correlationId));

            return new ExecuteProviderBackedDecisionResponse
            {
                Success = true,
                Status = ProviderBackedCaseExecutionStatus.Completed,
                Evidence = evidence,
                Diagnostics = diagnostics,
                ExecutionId = executionId,
                CorrelationId = correlationId
            };
        }

        // ── GetExecutionStatusAsync ───────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<GetProviderBackedExecutionStatusResponse> GetExecutionStatusAsync(
            string caseId,
            string actorId,
            string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(caseId))
            {
                return new GetProviderBackedExecutionStatusResponse
                {
                    Success = false,
                    CaseId = caseId ?? string.Empty,
                    Status = ProviderBackedCaseExecutionStatus.NotStarted,
                    ErrorMessage = "CaseId is required.",
                    CorrelationId = correlationId
                };
            }

            // Verify case exists
            GetComplianceCaseResponse caseResp;
            try
            {
                caseResp = await _caseService.GetCaseAsync(caseId, actorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ProviderBackedExecution.StatusCheck.CaseLoadFailed. CaseId={CaseId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new GetProviderBackedExecutionStatusResponse
                {
                    Success = false,
                    CaseId = caseId,
                    Status = ProviderBackedCaseExecutionStatus.Failed,
                    ErrorMessage = $"Failed to load compliance case '{caseId}': {ex.Message}",
                    CorrelationId = correlationId
                };
            }

            if (!caseResp.Success || caseResp.Case == null)
            {
                return new GetProviderBackedExecutionStatusResponse
                {
                    Success = false,
                    CaseId = caseId,
                    Status = ProviderBackedCaseExecutionStatus.NotStarted,
                    ErrorMessage = caseResp.ErrorMessage ?? $"Compliance case '{caseId}' not found.",
                    CorrelationId = correlationId
                };
            }

            if (!_history.TryGetValue(caseId, out var history))
            {
                return new GetProviderBackedExecutionStatusResponse
                {
                    Success = true,
                    CaseId = caseId,
                    Status = ProviderBackedCaseExecutionStatus.NotStarted,
                    ExecutionHistory = new List<ProviderBackedCaseExecutionEvidence>(),
                    HasReleaseGradeEvidence = false,
                    CorrelationId = correlationId
                };
            }

            List<ProviderBackedCaseExecutionEvidence> historySnapshot;
            historySnapshot = history.ToList();

            var latestCompleted = historySnapshot
                .LastOrDefault(e => e.Diagnostics != null);

            var hasReleaseGrade = historySnapshot.Any(e => e.IsReleaseGradeEvidence);

            var overallStatus = historySnapshot.Count == 0
                ? ProviderBackedCaseExecutionStatus.NotStarted
                : ProviderBackedCaseExecutionStatus.Completed;

            return new GetProviderBackedExecutionStatusResponse
            {
                Success = true,
                CaseId = caseId,
                Status = overallStatus,
                LatestDiagnostics = latestCompleted?.Diagnostics,
                ExecutionHistory = historySnapshot,
                HasReleaseGradeEvidence = hasReleaseGrade,
                CorrelationId = correlationId
            };
        }

        // ── BuildSignOffEvidenceAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<BuildProviderBackedSignOffEvidenceResponse> BuildSignOffEvidenceAsync(
            string caseId,
            BuildProviderBackedSignOffEvidenceRequest request,
            string actorId)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(caseId))
            {
                return new BuildProviderBackedSignOffEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_CASE_ID",
                    ErrorMessage = "CaseId is required.",
                    CorrelationId = correlationId
                };
            }

            // Load case
            GetComplianceCaseResponse caseResp;
            try
            {
                caseResp = await _caseService.GetCaseAsync(caseId, actorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ProviderBackedExecution.BuildEvidence.CaseLoadFailed. CaseId={CaseId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(caseId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new BuildProviderBackedSignOffEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "CASE_LOAD_ERROR",
                    ErrorMessage = $"Failed to load compliance case '{caseId}': {ex.Message}",
                    CorrelationId = correlationId
                };
            }

            if (!caseResp.Success || caseResp.Case == null)
            {
                return new BuildProviderBackedSignOffEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "CASE_NOT_FOUND",
                    ErrorMessage = caseResp.ErrorMessage ?? $"Compliance case '{caseId}' not found.",
                    CorrelationId = correlationId
                };
            }

            var complianceCase = caseResp.Case;

            // Get execution history
            List<ProviderBackedCaseExecutionEvidence> history = new();
            if (_history.TryGetValue(caseId, out var stored))
            {
                history = stored.ToList();
            }

            // Fail-closed: enforce provider-backed evidence requirement
            if (request?.RequireProviderBackedEvidence == true)
            {
                var simulatedCount = history.Count(e => !e.IsProviderBacked);
                if (simulatedCount > 0)
                {
                    _logger.LogWarning(
                        "ProviderBackedExecution.BuildEvidence.SimulatedEvidenceRejected. " +
                        "CaseId={CaseId} SimulatedCount={SimulatedCount} CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(caseId),
                        simulatedCount,
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return new BuildProviderBackedSignOffEvidenceResponse
                    {
                        Success = false,
                        ErrorCode = "SIMULATED_EVIDENCE_NOT_ALLOWED",
                        ErrorMessage =
                            $"RequireProviderBackedEvidence=true but {simulatedCount} simulated execution(s) " +
                            $"are present in the history. All executions must use LiveProvider or ProtectedSandbox " +
                            $"mode to qualify as release-grade evidence.",
                        CorrelationId = correlationId
                    };
                }

                if (history.Count == 0)
                {
                    return new BuildProviderBackedSignOffEvidenceResponse
                    {
                        Success = false,
                        ErrorCode = "NO_EXECUTION_HISTORY",
                        ErrorMessage =
                            "RequireProviderBackedEvidence=true but no execution history exists for this case. " +
                            "Execute at least one provider-backed decision before building sign-off evidence.",
                        CorrelationId = correlationId
                    };
                }
            }

            // Compute content hash
            var historyJson = JsonSerializer.Serialize(history);
            var contentHash = ComputeSha256(historyJson);

            bool isProviderBacked = history.Count > 0 && history.All(e => e.IsProviderBacked);
            bool isReleaseGrade = isProviderBacked && history.Any(e => e.IsReleaseGradeEvidence);

            var bundle = new ProviderBackedCaseSignOffEvidenceBundle
            {
                BundleId = Guid.NewGuid().ToString("N"),
                CaseId = caseId,
                BundledAt = _timeProvider.GetUtcNow(),
                IsProviderBackedEvidence = isProviderBacked,
                IsReleaseGrade = isReleaseGrade,
                ReleaseTag = request?.ReleaseTag,
                ContentHash = contentHash,
                ExecutionHistory = history,
                CurrentCaseState = complianceCase.State.ToString(),
                IssuerId = complianceCase.IssuerId,
                SubjectId = complianceCase.SubjectId,
                CaseType = complianceCase.Type.ToString(),
                LatestDiagnostics = history.Count > 0 ? history.Last().Diagnostics : null
            };

            _logger.LogInformation(
                "ProviderBackedExecution.BuildEvidence.Completed. CaseId={CaseId} BundleId={BundleId} " +
                "IsReleaseGrade={IsReleaseGrade} HistoryCount={HistoryCount} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(caseId),
                bundle.BundleId,
                isReleaseGrade,
                history.Count,
                LoggingHelper.SanitizeLogInput(correlationId));

            return new BuildProviderBackedSignOffEvidenceResponse
            {
                Success = true,
                Bundle = bundle,
                CorrelationId = correlationId
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private async Task<(bool kycOk, bool amlOk, string? guidance)> ValidateKycAmlSignOffAsync(
            string subjectId,
            ProviderBackedCaseExecutionMode mode,
            ProviderBackedCaseExecutionDiagnostics diagnostics)
        {
            if (_kycAmlService == null)
            {
                diagnostics.ConfigurationFailures.Add(
                    "KycAmlSignOffEvidenceService is not registered. Configure the KYC/AML provider " +
                    "and register IKycAmlSignOffEvidenceService in the DI container.");
                diagnostics.ActionableGuidance =
                    "Register IKycAmlSignOffEvidenceService with a live or sandbox provider configuration " +
                    "in Program.cs before running provider-backed executions.";
                return (false, false, diagnostics.ActionableGuidance);
            }

            try
            {
                var records = await _kycAmlService.ListRecordsForSubjectAsync(subjectId);
                if (records.Records == null || records.Records.Count == 0)
                {
                    diagnostics.ProviderFailures.Add(
                        $"No KYC/AML sign-off records found for subject '{subjectId}'. " +
                        "Initiate a sign-off check before executing a provider-backed decision.");
                    diagnostics.ActionableGuidance =
                        $"Call POST /api/v1/kyc-aml-signoff/initiate for subject '{subjectId}' " +
                        "and wait for provider completion before retrying.";
                    return (false, false, diagnostics.ActionableGuidance);
                }

                bool kycOk = false;
                bool amlOk = false;

                foreach (var record in records.Records)
                {
                    if (!record.IsProviderBacked &&
                        mode != ProviderBackedCaseExecutionMode.Simulated)
                    {
                        continue; // Skip simulated records for provider-backed check
                    }

                    var readiness = await _kycAmlService.GetReadinessAsync(record.RecordId);
                    if (readiness?.IsApprovalReady == true)
                    {
                        if (record.CheckKind == Models.KycAmlSignOff.KycAmlSignOffCheckKind.IdentityKyc)
                            kycOk = true;
                        else if (record.CheckKind == Models.KycAmlSignOff.KycAmlSignOffCheckKind.AmlScreening)
                            amlOk = true;
                        else if (record.CheckKind == Models.KycAmlSignOff.KycAmlSignOffCheckKind.Combined)
                        {
                            kycOk = true;
                            amlOk = true;
                        }
                    }
                }

                if (!kycOk)
                    diagnostics.ProviderFailures.Add(
                        "KYC sign-off is not complete or not provider-backed for this subject.");
                if (!amlOk)
                    diagnostics.ProviderFailures.Add(
                        "AML sign-off is not complete or not provider-backed for this subject.");

                if (!kycOk || !amlOk)
                {
                    diagnostics.ActionableGuidance =
                        "Ensure both KYC and AML sign-off records exist, are provider-backed, and " +
                        "have IsApprovalReady=true before executing a protected compliance decision.";
                }

                return (kycOk, amlOk, diagnostics.ActionableGuidance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ProviderBackedExecution.KycAmlValidation.Error. SubjectId={SubjectId}",
                    LoggingHelper.SanitizeLogInput(subjectId));

                diagnostics.ProviderFailures.Add($"KYC/AML validation failed with exception: {ex.Message}");
                diagnostics.ActionableGuidance =
                    "The KYC/AML sign-off service returned an error. Check provider connectivity and retry.";
                return (false, false, diagnostics.ActionableGuidance);
            }
        }

        private async Task<(bool success, string targetState, string? error, string? errorCode)> DispatchDecisionAsync(
            string caseId,
            ExecuteProviderBackedDecisionRequest request,
            string actorId,
            ComplianceCase complianceCase,
            List<string> auditSteps)
        {
            auditSteps.Add($"Dispatching decision: {request.DecisionKind}");

            switch (request.DecisionKind)
            {
                case ProviderBackedCaseDecisionKind.Approve:
                {
                    var r = await _caseService.ApproveComplianceCaseAsync(caseId,
                        new ApproveComplianceCaseRequest
                        {
                            Rationale = request.Reason,
                            ApprovalNotes = request.Notes,
                            ApprovedBy = actorId
                        }, actorId);

                    if (!r.Success)
                    {
                        auditSteps.Add($"Approval failed: {r.ErrorCode} – {r.ErrorMessage}");
                        return (false, string.Empty, r.ErrorMessage, r.ErrorCode);
                    }

                    var newState = r.Case?.State.ToString() ?? "Approved";
                    auditSteps.Add($"Case approved. DecisionId={r.DecisionId}");
                    return (true, newState, null, null);
                }

                case ProviderBackedCaseDecisionKind.Reject:
                {
                    var r = await _caseService.RejectComplianceCaseAsync(caseId,
                        new RejectComplianceCaseRequest
                        {
                            Reason = request.Reason,
                            RejectionNotes = request.Notes,
                            RejectedBy = actorId
                        }, actorId);

                    if (!r.Success)
                    {
                        auditSteps.Add($"Rejection failed: {r.ErrorCode} – {r.ErrorMessage}");
                        return (false, string.Empty, r.ErrorMessage, r.ErrorCode);
                    }

                    var newState = r.Case?.State.ToString() ?? "Rejected";
                    auditSteps.Add($"Case rejected. DecisionId={r.DecisionId}");
                    return (true, newState, null, null);
                }

                case ProviderBackedCaseDecisionKind.ReturnForInformation:
                {
                    var r = await _caseService.ReturnForInformationAsync(caseId,
                        new ReturnForInformationRequest
                        {
                            Reason = request.Reason,
                            AdditionalNotes = request.Notes
                        }, actorId);

                    if (!r.Success)
                    {
                        auditSteps.Add($"RFI failed: {r.ErrorCode} – {r.ErrorMessage}");
                        return (false, string.Empty, r.ErrorMessage, r.ErrorCode);
                    }

                    var newState = r.Case?.State.ToString() ?? r.ReturnedToStage?.ToString() ?? "EvidencePending";
                    auditSteps.Add($"Case returned for information. ReturnedTo={newState}");
                    return (true, newState, null, null);
                }

                case ProviderBackedCaseDecisionKind.SanctionsReview:
                {
                    // Sanctions review uses escalation to transition the case to Escalated state
                    var r = await _caseService.AddEscalationAsync(caseId,
                        new AddEscalationRequest
                        {
                            Type = EscalationType.SanctionsHit,
                            Description = request.Reason ?? "Sanctions review triggered via provider-backed execution.",
                            RequiresManualReview = true
                        }, actorId);

                    if (!r.Success)
                    {
                        auditSteps.Add($"Sanctions escalation failed: {r.ErrorMessage}");
                        return (false, string.Empty, r.ErrorMessage, "INVALID_TRANSITION");
                    }

                    var newState = r.Case?.State.ToString() ?? "Escalated";
                    auditSteps.Add($"Sanctions review escalation created. CaseState={newState}");
                    return (true, newState, null, null);
                }

                case ProviderBackedCaseDecisionKind.Escalate:
                {
                    var r = await _caseService.AddEscalationAsync(caseId,
                        new AddEscalationRequest
                        {
                            Type = EscalationType.ManualEscalation,
                            Description = request.Reason ?? "Escalation triggered via provider-backed execution.",
                            RequiresManualReview = true
                        }, actorId);

                    if (!r.Success)
                    {
                        auditSteps.Add($"Escalation failed: {r.ErrorMessage}");
                        return (false, string.Empty, r.ErrorMessage, "INVALID_TRANSITION");
                    }

                    var newState = r.Case?.State.ToString() ?? "Escalated";
                    auditSteps.Add($"Escalation created. CaseState={newState}");
                    return (true, newState, null, null);
                }

                default:
                    return (false, string.Empty,
                        $"Unsupported decision kind: {request.DecisionKind}",
                        "UNSUPPORTED_DECISION");
            }
        }

        private static ExecuteProviderBackedDecisionResponse FailResponse(
            string executionId,
            string correlationId,
            ProviderBackedCaseExecutionStatus status,
            string errorCode,
            string errorMessage,
            string? nextAction,
            ProviderBackedCaseExecutionDiagnostics? diagnostics = null)
        {
            return new ExecuteProviderBackedDecisionResponse
            {
                Success = false,
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                NextAction = nextAction,
                Diagnostics = diagnostics ?? new ProviderBackedCaseExecutionDiagnostics(),
                ExecutionId = executionId,
                CorrelationId = correlationId
            };
        }

        private void EmitEventFireAndForget(WebhookEventType eventType, string actorId, string entityId, object payload)
        {
            if (_webhookService == null) return;
            _ = Task.Run(async () =>
            {
                try
                {
                    Dictionary<string, object>? data = null;
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(payload);
                        data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    }
                    catch
                    {
                        data = new Dictionary<string, object> { ["entityId"] = entityId };
                    }

                    await _webhookService.EmitEventAsync(new WebhookEvent
                    {
                        EventType = eventType,
                        Actor     = actorId,
                        Data      = data
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "ProviderBackedExecution.WebhookEmitFailed. EventType={EventType} EntityId={EntityId}",
                        eventType, LoggingHelper.SanitizeLogInput(entityId));
                }
            });
        }

        private static string BuildNextAction(
            ProviderBackedCaseDecisionKind kind,
            ComplianceCaseState currentState,
            string? errorCode)
        {
            return errorCode switch
            {
                "INVALID_TRANSITION" or "INVALID_STATE_TRANSITION" =>
                    $"The case is in state '{currentState}' which does not support '{kind}'. " +
                    $"Transition the case to a valid prior state first.",
                "CASE_NOT_FOUND" =>
                    "Verify the case ID and retry.",
                _ =>
                    $"Investigate the error details and retry the '{kind}' decision when the blocking condition is resolved."
            };
        }

        private static string ComputeSha256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

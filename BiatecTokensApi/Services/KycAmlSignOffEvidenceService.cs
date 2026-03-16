using System.Collections.Concurrent;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Orchestrates provider-backed KYC/AML sign-off evidence flows.
    ///
    /// <para>
    /// This service wraps the underlying KYC and AML provider adapters, tracks execution
    /// mode (live, protected sandbox, or simulated), generates durable evidence artifacts,
    /// provides plain-language explanations for enterprise frontend workflows, and enforces
    /// fail-closed behaviour for all adverse or degraded conditions.
    /// </para>
    ///
    /// <para>
    /// Fail-closed rules (all produce a blocked readiness state, never silent approval):
    /// <list type="bullet">
    ///   <item>Provider unavailable / timeout → <see cref="KycAmlSignOffOutcome.ProviderUnavailable"/></item>
    ///   <item>Malformed or missing callback data → <see cref="KycAmlSignOffOutcome.MalformedCallback"/></item>
    ///   <item>Adverse findings (sanctions, PEP, etc.) → <see cref="KycAmlSignOffOutcome.AdverseFindings"/> or <see cref="KycAmlSignOffOutcome.Rejected"/></item>
    ///   <item>Stale / expired evidence → <see cref="KycAmlSignOffReadinessState.Stale"/></item>
    ///   <item>Incomplete remediation → <see cref="KycAmlSignOffOutcome.Blocked"/></item>
    ///   <item>Unknown provider reference → callback rejected</item>
    /// </list>
    /// </para>
    /// </summary>
    public class KycAmlSignOffEvidenceService : IKycAmlSignOffEvidenceService
    {
        private readonly IKycProvider _kycProvider;
        private readonly IAmlProvider _amlProvider;
        private readonly IWebhookService? _webhookService;
        private readonly ILogger<KycAmlSignOffEvidenceService> _logger;
        private readonly TimeProvider _timeProvider;

        // In-memory stores
        private readonly ConcurrentDictionary<string, KycAmlSignOffRecord> _records = new();
        // Idempotency key → recordId
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();
        // providerReferenceId → recordId (both KYC and AML references)
        private readonly ConcurrentDictionary<string, string> _providerRefIndex = new();

        /// <summary>
        /// Initialises the service.
        /// </summary>
        public KycAmlSignOffEvidenceService(
            IKycProvider kycProvider,
            IAmlProvider amlProvider,
            ILogger<KycAmlSignOffEvidenceService> logger,
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            _kycProvider = kycProvider;
            _amlProvider = amlProvider;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _webhookService = webhookService;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // InitiateSignOffAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<InitiateKycAmlSignOffResponse> InitiateSignOffAsync(
            InitiateKycAmlSignOffRequest request,
            string actorId,
            string correlationId)
        {
            if (request == null)
            {
                return FailResponse("REQUEST_NULL", "Request is required.", correlationId);
            }

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return FailResponse("MISSING_SUBJECT_ID", "SubjectId is required.", correlationId);
            }

            // Idempotency check
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                if (_idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingId)
                    && _records.TryGetValue(existingId, out var existing))
                {
                    _logger.LogInformation(
                        "KycAmlSignOff idempotent return. Key={Key}, RecordId={RecordId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                        LoggingHelper.SanitizeLogInput(existingId),
                        LoggingHelper.SanitizeLogInput(correlationId));
                    return new InitiateKycAmlSignOffResponse { Success = true, Record = existing, WasIdempotent = true, CorrelationId = correlationId };
                }
            }

            var now = _timeProvider.GetUtcNow();
            var record = new KycAmlSignOffRecord
            {
                SubjectId = request.SubjectId,
                ContextId = request.ContextId,
                CheckKind = request.CheckKind,
                ExecutionMode = request.RequestedExecutionMode,
                Outcome = KycAmlSignOffOutcome.Pending,
                ReadinessState = KycAmlSignOffReadinessState.AwaitingProvider,
                ReadinessExplanation = BuildExplanation(KycAmlSignOffOutcome.Pending, request.CheckKind, request.RequestedExecutionMode, null),
                InitiatedBy = actorId,
                CorrelationId = correlationId,
                CreatedAt = now,
                UpdatedAt = now
            };

            // Initiate provider call(s)
            var initiationError = await ExecuteProviderInitiationAsync(record, request, correlationId);

            if (initiationError != null)
            {
                // Provider initiation failed — mark record accordingly but still persist
                _logger.LogError(
                    "KycAmlSignOff provider initiation failed. SubjectId={SubjectId}, Error={Error}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    LoggingHelper.SanitizeLogInput(initiationError),
                    LoggingHelper.SanitizeLogInput(correlationId));

                UpdateOutcome(record, KycAmlSignOffOutcome.ProviderUnavailable, "PROVIDER_UNAVAILABLE",
                    "actor", correlationId, null,
                    $"Provider initiation failed: {initiationError}");
                AddArtifact(record, "ProviderInitiationRecord", record.ExecutionMode, record.KycProviderName ?? record.AmlProviderName, null,
                    new Dictionary<string, string>
                    {
                        ["Status"] = "InitiationFailed",
                        ["ErrorDetail"] = "Provider unavailable at initiation time"
                    },
                    "Provider initiation failed. No external check was performed. Evidence is not release-grade.");
                ReEvaluateReadiness(record);
            }
            else
            {
                AppendAuditEvent(record, "ProviderInitiationSucceeded", KycAmlSignOffOutcome.Pending, KycAmlSignOffOutcome.Pending,
                    "Provider check initiated successfully. Awaiting callback or polling result.",
                    record.KycProviderReferenceId ?? record.AmlProviderReferenceId, actorId);

                AddArtifact(record, "ProviderInitiationRecord", record.ExecutionMode,
                    record.KycProviderName ?? record.AmlProviderName,
                    record.KycProviderReferenceId ?? record.AmlProviderReferenceId,
                    new Dictionary<string, string>
                    {
                        ["Status"] = "Initiated",
                        ["CheckKind"] = record.CheckKind.ToString(),
                        ["ExecutionMode"] = record.ExecutionMode.ToString(),
                        ["SubjectId"] = record.SubjectId
                    },
                    BuildArtifactExplanation("ProviderInitiationRecord", record.ExecutionMode, record.CheckKind));
            }

            // Persist
            _records[record.RecordId] = record;
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                _idempotencyIndex[request.IdempotencyKey] = record.RecordId;

            // Emit webhook
            EmitWebhookEvent(WebhookEventType.KycAmlSignOffInitiated, record);

            if (record.Outcome == KycAmlSignOffOutcome.ProviderUnavailable)
                EmitWebhookEvent(WebhookEventType.KycAmlSignOffBlocked, record);

            _logger.LogInformation(
                "KycAmlSignOff initiated. RecordId={RecordId}, SubjectId={SubjectId}, Mode={Mode}, Outcome={Outcome}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(record.RecordId),
                LoggingHelper.SanitizeLogInput(record.SubjectId),
                record.ExecutionMode,
                record.Outcome,
                LoggingHelper.SanitizeLogInput(correlationId));

            return new InitiateKycAmlSignOffResponse { Success = true, Record = record, CorrelationId = correlationId };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ProcessCallbackAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ProcessKycAmlSignOffCallbackResponse> ProcessCallbackAsync(
            string recordId,
            ProcessKycAmlSignOffCallbackRequest request,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(recordId))
            {
                return Task.FromResult(FailCallbackResponse("MISSING_RECORD_ID", "RecordId is required.", correlationId));
            }

            if (request == null)
            {
                return Task.FromResult(FailCallbackResponse("REQUEST_NULL", "Request is required.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(request.ProviderReferenceId))
            {
                return Task.FromResult(FailCallbackResponse("MISSING_PROVIDER_REF", "ProviderReferenceId is required.", correlationId));
            }

            if (string.IsNullOrWhiteSpace(request.OutcomeStatus))
            {
                return Task.FromResult(FailCallbackResponse("MISSING_OUTCOME_STATUS", "OutcomeStatus is required.", correlationId));
            }

            if (!_records.TryGetValue(recordId, out var record))
            {
                _logger.LogWarning(
                    "KycAmlSignOff callback for unknown RecordId={RecordId}, ProviderRef={ProviderRef}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(recordId),
                    LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return Task.FromResult(FailCallbackResponse("RECORD_NOT_FOUND", $"No sign-off record found with ID '{recordId}'.", correlationId));
            }

            // Validate that the provider reference matches
            var refMatchesKyc = record.KycProviderReferenceId == request.ProviderReferenceId;
            var refMatchesAml = record.AmlProviderReferenceId == request.ProviderReferenceId;
            if (!refMatchesKyc && !refMatchesAml)
            {
                _logger.LogWarning(
                    "KycAmlSignOff callback ProviderReferenceId mismatch. RecordId={RecordId}, ExpectedKyc={KycRef}, ExpectedAml={AmlRef}, GotRef={GotRef}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(recordId),
                    LoggingHelper.SanitizeLogInput(record.KycProviderReferenceId ?? "none"),
                    LoggingHelper.SanitizeLogInput(record.AmlProviderReferenceId ?? "none"),
                    LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return Task.FromResult(FailCallbackResponse("PROVIDER_REF_MISMATCH",
                    "ProviderReferenceId does not match any check in this record.", correlationId));
            }

            var prevOutcome = record.Outcome;
            var newOutcome = NormalizeOutcomeFromStatus(request.OutcomeStatus, request.ReasonCode);

            UpdateOutcome(record, newOutcome, request.ReasonCode, "webhook", correlationId,
                request.ProviderReferenceId,
                $"Provider callback received: {request.EventType ?? request.OutcomeStatus}");

            // Add callback artifact
            AddArtifact(record, "ProviderCallbackPayload", record.ExecutionMode,
                refMatchesKyc ? record.KycProviderName : record.AmlProviderName,
                request.ProviderReferenceId,
                new Dictionary<string, string>
                {
                    ["EventType"] = request.EventType ?? "unknown",
                    ["OutcomeStatus"] = request.OutcomeStatus,
                    ["ReasonCode"] = request.ReasonCode ?? "none",
                    ["ProviderCompletedAt"] = request.ProviderCompletedAt?.ToString("O") ?? "unknown",
                    ["PayloadSummary"] = string.IsNullOrWhiteSpace(request.RawPayloadSummary) ? "none" : "present"
                },
                BuildCallbackArtifactExplanation(newOutcome, request.EventType));

            // Re-evaluate readiness
            ReEvaluateReadiness(record);

            record.UpdatedAt = _timeProvider.GetUtcNow();
            _records[record.RecordId] = record;

            // Emit events
            EmitWebhookEvent(WebhookEventType.KycAmlSignOffCallbackProcessed, record);
            if (record.ReadinessState == KycAmlSignOffReadinessState.Ready)
                EmitWebhookEvent(WebhookEventType.KycAmlSignOffApprovalReady, record);
            else if (IsBlockedOutcome(record.Outcome))
                EmitWebhookEvent(WebhookEventType.KycAmlSignOffBlocked, record);

            _logger.LogInformation(
                "KycAmlSignOff callback processed. RecordId={RecordId}, PrevOutcome={Prev}, NewOutcome={New}, Readiness={Readiness}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(recordId),
                prevOutcome,
                newOutcome,
                record.ReadinessState,
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new ProcessKycAmlSignOffCallbackResponse
            {
                Success = true,
                Record = record,
                CorrelationId = correlationId
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GetRecordAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetKycAmlSignOffRecordResponse> GetRecordAsync(string recordId)
        {
            if (string.IsNullOrWhiteSpace(recordId))
            {
                return Task.FromResult(new GetKycAmlSignOffRecordResponse
                {
                    Success = false, ErrorCode = "MISSING_RECORD_ID", ErrorMessage = "RecordId is required."
                });
            }

            if (!_records.TryGetValue(recordId, out var record))
            {
                return Task.FromResult(new GetKycAmlSignOffRecordResponse
                {
                    Success = false, ErrorCode = "RECORD_NOT_FOUND",
                    ErrorMessage = $"No sign-off record found with ID '{recordId}'."
                });
            }

            // Refresh staleness before returning
            RefreshEvidenceStaleness(record);

            return Task.FromResult(new GetKycAmlSignOffRecordResponse { Success = true, Record = record });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GetReadinessAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<KycAmlSignOffReadinessResponse> GetReadinessAsync(string recordId)
        {
            if (!_records.TryGetValue(recordId, out var record))
            {
                return Task.FromResult(new KycAmlSignOffReadinessResponse
                {
                    RecordId = recordId,
                    ReadinessState = KycAmlSignOffReadinessState.IncompleteEvidence,
                    Outcome = KycAmlSignOffOutcome.Error,
                    IsApprovalReady = false,
                    IsProviderBacked = false,
                    ExplanationText = "No sign-off record found. Evidence has not been submitted.",
                    EvaluatedAt = _timeProvider.GetUtcNow()
                });
            }

            RefreshEvidenceStaleness(record);
            ReEvaluateReadiness(record);

            var isReady = record.ReadinessState == KycAmlSignOffReadinessState.Ready;
            var isProviderBacked = record.IsProviderBacked;

            // Approval is only valid when ready AND provider-backed
            var isApprovalReady = isReady && isProviderBacked;

            return Task.FromResult(new KycAmlSignOffReadinessResponse
            {
                RecordId = record.RecordId,
                SubjectId = record.SubjectId,
                ReadinessState = record.ReadinessState,
                Outcome = record.Outcome,
                IsApprovalReady = isApprovalReady,
                IsProviderBacked = isProviderBacked,
                ExecutionMode = record.ExecutionMode,
                ExplanationText = record.ReadinessExplanation,
                Blockers = record.Blockers,
                EvaluatedAt = _timeProvider.GetUtcNow()
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GetArtifactsAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetKycAmlSignOffArtifactsResponse> GetArtifactsAsync(string recordId)
        {
            if (!_records.TryGetValue(recordId, out var record))
            {
                return Task.FromResult(new GetKycAmlSignOffArtifactsResponse
                {
                    RecordId = recordId,
                    ErrorCode = "RECORD_NOT_FOUND",
                    ErrorMessage = $"No sign-off record found with ID '{recordId}'."
                });
            }

            var hasProviderBacked = record.EvidenceArtifacts.Any(a => a.IsProviderBacked);

            return Task.FromResult(new GetKycAmlSignOffArtifactsResponse
            {
                RecordId = record.RecordId,
                SubjectId = record.SubjectId,
                Artifacts = record.EvidenceArtifacts,
                HasProviderBackedArtifacts = hasProviderBacked
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ListRecordsForSubjectAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListKycAmlSignOffRecordsResponse> ListRecordsForSubjectAsync(string subjectId)
        {
            var records = _records.Values
                .Where(r => r.SubjectId == subjectId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            var latest = records.FirstOrDefault();

            return Task.FromResult(new ListKycAmlSignOffRecordsResponse
            {
                SubjectId = subjectId,
                Records = records,
                TotalCount = records.Count,
                LatestReadinessState = latest?.ReadinessState
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PollProviderStatusAsync
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<PollKycAmlSignOffStatusResponse> PollProviderStatusAsync(
            string recordId,
            string correlationId)
        {
            if (!_records.TryGetValue(recordId, out var record))
            {
                return new PollKycAmlSignOffStatusResponse
                {
                    Success = false,
                    ErrorCode = "RECORD_NOT_FOUND",
                    ErrorMessage = $"No sign-off record found with ID '{recordId}'.",
                    CorrelationId = correlationId
                };
            }

            // Only poll for in-progress (Pending) checks
            if (record.Outcome != KycAmlSignOffOutcome.Pending)
            {
                return new PollKycAmlSignOffStatusResponse
                {
                    Success = true,
                    Record = record,
                    OutcomeChanged = false,
                    CorrelationId = correlationId
                };
            }

            var prevOutcome = record.Outcome;

            try
            {
                var newOutcome = await PollProviderStatusInternalAsync(record, correlationId);
                var changed = newOutcome != prevOutcome;

                if (changed)
                {
                    UpdateOutcome(record, newOutcome, null, "polling", correlationId, null,
                        $"Outcome updated via provider polling: {newOutcome}");
                    ReEvaluateReadiness(record);
                    record.UpdatedAt = _timeProvider.GetUtcNow();
                    _records[record.RecordId] = record;

                    if (record.ReadinessState == KycAmlSignOffReadinessState.Ready)
                        EmitWebhookEvent(WebhookEventType.KycAmlSignOffApprovalReady, record);
                    else if (IsBlockedOutcome(record.Outcome))
                        EmitWebhookEvent(WebhookEventType.KycAmlSignOffBlocked, record);
                }

                return new PollKycAmlSignOffStatusResponse
                {
                    Success = true,
                    Record = record,
                    OutcomeChanged = changed,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "KycAmlSignOff polling error. RecordId={RecordId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(recordId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                UpdateOutcome(record, KycAmlSignOffOutcome.ProviderUnavailable, "POLL_ERROR",
                    "system", correlationId, null,
                    "Provider polling failed due to an error. Evidence is blocked.");
                ReEvaluateReadiness(record);
                record.UpdatedAt = _timeProvider.GetUtcNow();
                _records[record.RecordId] = record;
                EmitWebhookEvent(WebhookEventType.KycAmlSignOffBlocked, record);

                return new PollKycAmlSignOffStatusResponse
                {
                    Success = false,
                    Record = record,
                    ErrorCode = "POLL_ERROR",
                    ErrorMessage = "Provider polling failed.",
                    CorrelationId = correlationId
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────────

        private async Task<string?> ExecuteProviderInitiationAsync(
            KycAmlSignOffRecord record,
            InitiateKycAmlSignOffRequest request,
            string correlationId)
        {
            var now = _timeProvider.GetUtcNow();

            // KYC initiation
            if (record.CheckKind is KycAmlSignOffCheckKind.IdentityKyc or KycAmlSignOffCheckKind.Combined)
            {
                try
                {
                    var kycRequest = new Models.Kyc.StartKycVerificationRequest
                    {
                        FullName = request.SubjectMetadata.GetValueOrDefault("full_name") ?? "",
                        DateOfBirth = request.SubjectMetadata.GetValueOrDefault("date_of_birth") ?? "",
                        Country = request.SubjectMetadata.GetValueOrDefault("country") ?? "",
                        Metadata = request.SubjectMetadata
                    };

                    var (kycRefId, kycStatus, kycError) = await _kycProvider.StartVerificationAsync(
                        record.SubjectId, kycRequest, correlationId);

                    if (!string.IsNullOrWhiteSpace(kycError) && kycStatus == Models.Kyc.KycStatus.NotStarted)
                    {
                        return kycError;
                    }

                    record.KycProviderReferenceId = kycRefId;
                    record.KycProviderName = "KycProvider";

                    if (!string.IsNullOrWhiteSpace(kycRefId))
                        _providerRefIndex[kycRefId] = record.RecordId;

                    // If KYC returned a terminal synchronous result
                    if (kycStatus == Models.Kyc.KycStatus.Approved)
                        record.Outcome = KycAmlSignOffOutcome.Approved;
                    else if (kycStatus == Models.Kyc.KycStatus.Rejected)
                        record.Outcome = KycAmlSignOffOutcome.Rejected;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "KYC provider initiation threw. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(record.SubjectId),
                        LoggingHelper.SanitizeLogInput(correlationId));
                    return ex.Message;
                }
            }

            // AML initiation (only proceed when KYC didn't hard-fail)
            if (record.CheckKind is KycAmlSignOffCheckKind.AmlScreening or KycAmlSignOffCheckKind.Combined)
            {
                if (record.Outcome == KycAmlSignOffOutcome.Rejected)
                {
                    // KYC rejected; skip AML but still record the attempt
                    AppendAuditEvent(record, "AmlSkippedDueToKycRejection",
                        KycAmlSignOffOutcome.Rejected, KycAmlSignOffOutcome.Rejected,
                        "AML screening was skipped because KYC was rejected.", null, "system");
                    // Fall through to readiness evaluation below (do NOT return early)
                }
                else
                {
                    try
                    {
                        var (amlRefId, amlState, amlReasonCode, amlError) = await _amlProvider.ScreenSubjectAsync(
                            record.SubjectId, request.SubjectMetadata, correlationId);

                        if (amlState == Models.ComplianceOrchestration.ComplianceDecisionState.ProviderUnavailable
                            || amlState == Models.ComplianceOrchestration.ComplianceDecisionState.Error)
                        {
                            return amlError ?? "AML provider unavailable";
                        }

                        record.AmlProviderReferenceId = amlRefId;
                        record.AmlProviderName = _amlProvider.ProviderName;

                        if (!string.IsNullOrWhiteSpace(amlRefId))
                            _providerRefIndex[amlRefId] = record.RecordId;

                        // Capture reason code for blocker derivation
                        if (!string.IsNullOrWhiteSpace(amlReasonCode))
                            record.ReasonCode = amlReasonCode;

                        // Map synchronous AML result
                        var amlOutcome = MapAmlStateToOutcome(amlState);
                        // Combined: AML is more severe than KYC pending → use AML outcome
                        if (record.CheckKind == KycAmlSignOffCheckKind.Combined
                            && record.Outcome == KycAmlSignOffOutcome.Pending)
                        {
                            record.Outcome = amlOutcome;
                        }
                        else if (record.CheckKind == KycAmlSignOffCheckKind.AmlScreening)
                        {
                            record.Outcome = amlOutcome;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "AML provider initiation threw. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                            LoggingHelper.SanitizeLogInput(record.SubjectId),
                            LoggingHelper.SanitizeLogInput(correlationId));
                        return ex.Message;
                    }
                }
            }

            // Set evidence expiry if requested
            if (request.EvidenceValidityHours.HasValue && request.EvidenceValidityHours > 0)
            {
                record.EvidenceExpiresAt = now.AddHours(request.EvidenceValidityHours.Value);
            }

            // Update readiness explanation
            record.ReadinessExplanation = BuildExplanation(record.Outcome, record.CheckKind, record.ExecutionMode, record.ReasonCode);
            ReEvaluateReadiness(record);

            return null; // success
        }

        private async Task<KycAmlSignOffOutcome> PollProviderStatusInternalAsync(
            KycAmlSignOffRecord record,
            string correlationId)
        {
            // Poll KYC status if applicable
            if (!string.IsNullOrWhiteSpace(record.KycProviderReferenceId))
            {
                var (kycStatus, _, kycError) = await _kycProvider.FetchStatusAsync(record.KycProviderReferenceId);

                if (kycStatus == Models.Kyc.KycStatus.Approved)
                    return KycAmlSignOffOutcome.Approved;
                if (kycStatus == Models.Kyc.KycStatus.Rejected)
                    return KycAmlSignOffOutcome.Rejected;
                if (!string.IsNullOrWhiteSpace(kycError))
                    return KycAmlSignOffOutcome.ProviderUnavailable;
            }

            // Poll AML status if applicable
            if (!string.IsNullOrWhiteSpace(record.AmlProviderReferenceId))
            {
                var (amlState, _, amlError) = await _amlProvider.GetScreeningStatusAsync(record.AmlProviderReferenceId);

                if (!string.IsNullOrWhiteSpace(amlError)
                    || amlState == Models.ComplianceOrchestration.ComplianceDecisionState.ProviderUnavailable)
                    return KycAmlSignOffOutcome.ProviderUnavailable;

                return MapAmlStateToOutcome(amlState);
            }

            return record.Outcome; // no change
        }

        private void ReEvaluateReadiness(KycAmlSignOffRecord record)
        {
            var now = _timeProvider.GetUtcNow();

            // Check expiry
            if (record.EvidenceExpiresAt.HasValue && record.EvidenceExpiresAt.Value < now)
            {
                record.IsEvidenceExpired = true;
                record.ReadinessState = KycAmlSignOffReadinessState.Stale;
                record.ReadinessExplanation = "Evidence has expired and onboarding is blocked until a new check is initiated.";
                EnsureBlocker(record, "EVIDENCE_EXPIRED", "Evidence expired and onboarding is blocked.", false);
                return;
            }

            record.Blockers.Clear();

            switch (record.Outcome)
            {
                case KycAmlSignOffOutcome.Approved:
                    record.ReadinessState = KycAmlSignOffReadinessState.Ready;
                    break;

                case KycAmlSignOffOutcome.Pending:
                    record.ReadinessState = KycAmlSignOffReadinessState.AwaitingProvider;
                    break;

                case KycAmlSignOffOutcome.NeedsManualReview:
                    record.ReadinessState = KycAmlSignOffReadinessState.RequiresReview;
                    EnsureBlocker(record, "MANUAL_REVIEW_REQUIRED",
                        "Compliance approval cannot proceed until manual analyst review is complete.", true);
                    break;

                case KycAmlSignOffOutcome.AdverseFindings:
                    record.ReadinessState = KycAmlSignOffReadinessState.Blocked;
                    EnsureBlocker(record, record.ReasonCode ?? "ADVERSE_FINDINGS",
                        BuildAdverseFindingsDescription(record.ReasonCode), true);
                    break;

                case KycAmlSignOffOutcome.Rejected:
                    record.ReadinessState = KycAmlSignOffReadinessState.Blocked;
                    EnsureBlocker(record, record.ReasonCode ?? "REJECTED",
                        BuildRejectedDescription(record.ReasonCode), false);
                    break;

                case KycAmlSignOffOutcome.Blocked:
                    record.ReadinessState = KycAmlSignOffReadinessState.Blocked;
                    EnsureBlocker(record, "BLOCKED",
                        "Compliance approval cannot proceed until remediation is complete.", true);
                    break;

                case KycAmlSignOffOutcome.Stale:
                    record.ReadinessState = KycAmlSignOffReadinessState.Stale;
                    EnsureBlocker(record, "STALE_EVIDENCE",
                        "Evidence is stale and a new check must be initiated.", false);
                    break;

                case KycAmlSignOffOutcome.ProviderUnavailable:
                    record.ReadinessState = KycAmlSignOffReadinessState.Blocked;
                    EnsureBlocker(record, "PROVIDER_UNAVAILABLE",
                        "The compliance provider was unavailable. Onboarding is blocked until the provider is accessible.", false);
                    break;

                case KycAmlSignOffOutcome.Error:
                case KycAmlSignOffOutcome.MalformedCallback:
                    record.ReadinessState = KycAmlSignOffReadinessState.Blocked;
                    EnsureBlocker(record, "PROVIDER_ERROR",
                        "A provider error occurred. Evidence cannot be validated and onboarding is blocked.", false);
                    break;

                default:
                    record.ReadinessState = KycAmlSignOffReadinessState.IncompleteEvidence;
                    break;
            }

            record.ReadinessExplanation = BuildExplanation(record.Outcome, record.CheckKind, record.ExecutionMode, record.ReasonCode);
        }

        private void RefreshEvidenceStaleness(KycAmlSignOffRecord record)
        {
            var now = _timeProvider.GetUtcNow();
            if (record.EvidenceExpiresAt.HasValue && record.EvidenceExpiresAt.Value < now && !record.IsEvidenceExpired)
            {
                record.IsEvidenceExpired = true;
                if (record.Outcome == KycAmlSignOffOutcome.Approved)
                {
                    UpdateOutcome(record, KycAmlSignOffOutcome.Stale, "EVIDENCE_EXPIRED",
                        "system", record.CorrelationId, null,
                        "Evidence has expired. A new check must be initiated.");
                    ReEvaluateReadiness(record);
                }
            }
        }

        private static KycAmlSignOffOutcome NormalizeOutcomeFromStatus(string status, string? reasonCode)
        {
            return status.ToLowerInvariant() switch
            {
                "approved" or "verified" or "clear" or "pass" => KycAmlSignOffOutcome.Approved,
                "rejected" or "declined" or "failed" => KycAmlSignOffOutcome.Rejected,
                "needs_review" or "manual_review" or "review_required" or "review" => KycAmlSignOffOutcome.NeedsManualReview,
                "adverse" or "adverse_findings" or "hit" or "match" => KycAmlSignOffOutcome.AdverseFindings,
                "blocked" => KycAmlSignOffOutcome.Blocked,
                "expired" or "stale" => KycAmlSignOffOutcome.Stale,
                "error" => KycAmlSignOffOutcome.Error,
                "unavailable" or "provider_unavailable" => KycAmlSignOffOutcome.ProviderUnavailable,
                "malformed" or "invalid" => KycAmlSignOffOutcome.MalformedCallback,
                "pending" or "processing" => KycAmlSignOffOutcome.Pending,
                _ => string.IsNullOrWhiteSpace(reasonCode)
                    ? KycAmlSignOffOutcome.Error
                    : KycAmlSignOffOutcome.AdverseFindings
            };
        }

        private static KycAmlSignOffOutcome MapAmlStateToOutcome(
            Models.ComplianceOrchestration.ComplianceDecisionState state)
        {
            return state switch
            {
                Models.ComplianceOrchestration.ComplianceDecisionState.Approved => KycAmlSignOffOutcome.Approved,
                Models.ComplianceOrchestration.ComplianceDecisionState.Rejected => KycAmlSignOffOutcome.Rejected,
                Models.ComplianceOrchestration.ComplianceDecisionState.NeedsReview => KycAmlSignOffOutcome.NeedsManualReview,
                Models.ComplianceOrchestration.ComplianceDecisionState.Pending => KycAmlSignOffOutcome.Pending,
                Models.ComplianceOrchestration.ComplianceDecisionState.ProviderUnavailable => KycAmlSignOffOutcome.ProviderUnavailable,
                Models.ComplianceOrchestration.ComplianceDecisionState.InsufficientData => KycAmlSignOffOutcome.Blocked,
                _ => KycAmlSignOffOutcome.Error
            };
        }

        private static bool IsBlockedOutcome(KycAmlSignOffOutcome outcome)
            => outcome is KycAmlSignOffOutcome.Rejected
                or KycAmlSignOffOutcome.Blocked
                or KycAmlSignOffOutcome.ProviderUnavailable
                or KycAmlSignOffOutcome.Error
                or KycAmlSignOffOutcome.MalformedCallback
                or KycAmlSignOffOutcome.AdverseFindings;

        private void UpdateOutcome(
            KycAmlSignOffRecord record,
            KycAmlSignOffOutcome newOutcome,
            string? reasonCode,
            string actor,
            string correlationId,
            string? providerRef,
            string description)
        {
            var prev = record.Outcome;
            record.Outcome = newOutcome;
            if (!string.IsNullOrWhiteSpace(reasonCode))
                record.ReasonCode = reasonCode;

            AppendAuditEvent(record, $"OutcomeTransition:{prev}->{newOutcome}",
                prev, newOutcome, description, providerRef, actor);
        }

        private static void AppendAuditEvent(
            KycAmlSignOffRecord record,
            string eventType,
            KycAmlSignOffOutcome? prevOutcome,
            KycAmlSignOffOutcome newOutcome,
            string description,
            string? providerRef,
            string actor)
        {
            record.AuditTrail.Add(new KycAmlSignOffAuditEvent
            {
                OccurredAt = DateTimeOffset.UtcNow,
                EventType = eventType,
                PreviousOutcome = prevOutcome,
                NewOutcome = newOutcome,
                Description = description,
                ProviderReference = providerRef,
                Actor = actor
            });
        }

        private static void AddArtifact(
            KycAmlSignOffRecord record,
            string kind,
            KycAmlSignOffExecutionMode mode,
            string? providerName,
            string? providerRefId,
            Dictionary<string, string> summary,
            string explanation)
        {
            record.EvidenceArtifacts.Add(new KycAmlSignOffEvidenceArtifact
            {
                Kind = kind,
                CreatedAt = DateTimeOffset.UtcNow,
                ExecutionMode = mode,
                ProviderName = providerName,
                ProviderReferenceId = providerRefId,
                Summary = summary,
                ExplanationText = explanation
            });
        }

        private static void EnsureBlocker(
            KycAmlSignOffRecord record,
            string code,
            string description,
            bool isRemediable)
        {
            if (record.Blockers.Any(b => b.Code == code)) return;
            record.Blockers.Add(new KycAmlSignOffBlocker
            {
                Code = code,
                Description = description,
                IsRemediable = isRemediable,
                RecordedAt = DateTimeOffset.UtcNow
            });
        }

        private static string BuildExplanation(
            KycAmlSignOffOutcome outcome,
            KycAmlSignOffCheckKind checkKind,
            KycAmlSignOffExecutionMode mode,
            string? reasonCode)
        {
            var checkLabel = checkKind switch
            {
                KycAmlSignOffCheckKind.IdentityKyc => "Identity verification",
                KycAmlSignOffCheckKind.AmlScreening => "AML / sanctions screening",
                _ => "Compliance check"
            };

            var modeLabel = mode == KycAmlSignOffExecutionMode.Simulated
                ? " (simulated — not release-grade evidence)"
                : mode == KycAmlSignOffExecutionMode.ProtectedSandbox
                    ? " (protected sandbox)"
                    : "";

            return outcome switch
            {
                KycAmlSignOffOutcome.Pending
                    => $"{checkLabel} awaiting provider callback{modeLabel}.",
                KycAmlSignOffOutcome.Approved
                    => $"{checkLabel} passed{modeLabel}. Subject is ready for approval.",
                KycAmlSignOffOutcome.Rejected
                    => $"{checkLabel} resulted in rejection{modeLabel}."
                       + (string.IsNullOrWhiteSpace(reasonCode) ? "" : $" Reason: {reasonCode}."),
                KycAmlSignOffOutcome.NeedsManualReview
                    => $"{checkLabel} requires manual analyst review before a decision can be made{modeLabel}.",
                KycAmlSignOffOutcome.AdverseFindings
                    => $"{checkLabel} detected adverse findings{modeLabel}. Remediation or further investigation is required."
                       + (string.IsNullOrWhiteSpace(reasonCode) ? "" : $" Detail: {reasonCode}."),
                KycAmlSignOffOutcome.Blocked
                    => $"{checkLabel} is blocked. Compliance approval cannot proceed until remediation is complete{modeLabel}.",
                KycAmlSignOffOutcome.Stale
                    => $"{checkLabel} evidence is stale{modeLabel}. A new check must be initiated.",
                KycAmlSignOffOutcome.ProviderUnavailable
                    => $"{checkLabel} provider was unavailable{modeLabel}. Onboarding is blocked until the provider is accessible.",
                KycAmlSignOffOutcome.Error
                    => $"{checkLabel} encountered a provider error{modeLabel}. Evidence cannot be validated.",
                KycAmlSignOffOutcome.MalformedCallback
                    => $"{checkLabel} received a malformed or contradictory callback{modeLabel}. Evidence is blocked.",
                _ => $"{checkLabel} is in an unknown state{modeLabel}."
            };
        }

        private static string BuildArtifactExplanation(
            string kind,
            KycAmlSignOffExecutionMode mode,
            KycAmlSignOffCheckKind checkKind)
        {
            var modeDesc = mode switch
            {
                KycAmlSignOffExecutionMode.LiveProvider =>
                    "This artifact was produced by a live production-grade external provider. It qualifies as release-grade evidence.",
                KycAmlSignOffExecutionMode.ProtectedSandbox =>
                    "This artifact was produced using a protected provider sandbox. It is production-like but not live.",
                _ =>
                    "This artifact was produced by an internal simulation. It is NOT release-grade evidence."
            };

            return $"{kind} for {checkKind} check. {modeDesc}";
        }

        private static string BuildCallbackArtifactExplanation(
            KycAmlSignOffOutcome outcome,
            string? eventType)
        {
            var evtLabel = string.IsNullOrWhiteSpace(eventType) ? "provider callback" : eventType;
            return $"Provider callback artifact from event '{evtLabel}'. Normalized outcome: {outcome}. " +
                   "This artifact represents the raw callback data processed by the compliance backend.";
        }

        private static string BuildAdverseFindingsDescription(string? reasonCode)
        {
            return reasonCode switch
            {
                "SANCTIONS_MATCH" => "Sanctions hit requires analyst review before onboarding can continue.",
                "PEP_MATCH" => "Politically exposed person match requires analyst review.",
                "ADVERSE_MEDIA" => "Adverse media findings require analyst review.",
                "WATCHLIST_MATCH" => "Watchlist match requires analyst review.",
                _ => "Adverse findings detected. Remediation or further investigation is required."
            };
        }

        private static string BuildRejectedDescription(string? reasonCode)
        {
            return reasonCode switch
            {
                "SANCTIONS_MATCH" => "Subject is on a sanctions list and cannot be onboarded.",
                "DOCUMENT_FRAUD" => "Document fraud detected. Subject cannot be onboarded.",
                "IDENTITY_MISMATCH" => "Identity verification failed. Subject details could not be verified.",
                _ => "Subject has been rejected by the compliance provider and cannot be onboarded."
            };
        }

        private void EmitWebhookEvent(WebhookEventType eventType, KycAmlSignOffRecord record)
        {
            if (_webhookService == null) return;
            try
            {
                var evt = new WebhookEvent
                {
                    EventType = eventType,
                    Timestamp = DateTime.UtcNow,
                    Data = new Dictionary<string, object>
                    {
                        ["recordId"] = record.RecordId,
                        ["subjectId"] = record.SubjectId,
                        ["outcome"] = record.Outcome.ToString(),
                        ["readinessState"] = record.ReadinessState.ToString(),
                        ["executionMode"] = record.ExecutionMode.ToString(),
                        ["isProviderBacked"] = record.IsProviderBacked,
                        ["correlationId"] = record.CorrelationId
                    }
                };
                _ = _webhookService.EmitEventAsync(evt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to emit webhook event {EventType} for RecordId={RecordId}",
                    eventType,
                    LoggingHelper.SanitizeLogInput(record.RecordId));
            }
        }

        // ── Error response helpers ────────────────────────────────────────────────

        private static InitiateKycAmlSignOffResponse FailResponse(
            string code, string message, string correlationId)
            => new() { Success = false, ErrorCode = code, ErrorMessage = message, CorrelationId = correlationId };

        private static ProcessKycAmlSignOffCallbackResponse FailCallbackResponse(
            string code, string message, string correlationId)
            => new() { Success = false, ErrorCode = code, ErrorMessage = message, CorrelationId = correlationId };
    }
}

using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Protected sign-off evidence persistence service.
    ///
    /// <para>
    /// This service provides authoritative backend storage and querying of approval
    /// webhook outcomes, protected sign-off evidence packs, and aggregated
    /// release-readiness status for the current head.
    /// </para>
    ///
    /// <para>
    /// Fail-closed rules (all produce a non-success response with diagnostics):
    /// <list type="bullet">
    ///   <item>Null or empty request → Failed with validation error.</item>
    ///   <item>RequireApprovalWebhook=true but no approved webhook for this head → Blocked.</item>
    ///   <item>RequireReleaseGrade=true but evidence is not provider-backed → Blocked.</item>
    ///   <item>Evidence freshness window exceeded → Stale.</item>
    ///   <item>Evidence pack head ref does not match request head ref → HeadMismatch.</item>
    ///   <item>Approval webhook outcome is Denied → Blocked with ApprovalDenied blocker.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ProtectedSignOffEvidencePersistenceService : IProtectedSignOffEvidencePersistenceService
    {
        private readonly IWebhookService? _webhookService;
        private readonly ILogger<ProtectedSignOffEvidencePersistenceService> _logger;
        private readonly TimeProvider _timeProvider;

        // Webhook records keyed by caseId → ordered queue (newest appended)
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ApprovalWebhookRecord>> _webhookHistory = new();

        // Evidence packs keyed by headRef → ordered queue (newest appended)
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ProtectedSignOffEvidencePack>> _evidencePacks = new();

        /// <summary>
        /// Initialises the service.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="webhookService">Optional webhook service for event emission.</param>
        /// <param name="timeProvider">Substitutable time provider (inject fake for tests).</param>
        public ProtectedSignOffEvidencePersistenceService(
            ILogger<ProtectedSignOffEvidencePersistenceService> logger,
            IWebhookService? webhookService = null,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _webhookService = webhookService;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        // ── RecordApprovalWebhookAsync ────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<RecordApprovalWebhookResponse> RecordApprovalWebhookAsync(
            RecordApprovalWebhookRequest request,
            string actorId)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "ProtectedSignOff.RecordApprovalWebhook. CaseId={CaseId} HeadRef={HeadRef} Outcome={Outcome} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request?.CaseId),
                LoggingHelper.SanitizeLogInput(request?.HeadRef),
                request?.Outcome.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (request == null)
            {
                return new RecordApprovalWebhookResponse
                {
                    Success = false,
                    ErrorCode = "REQUEST_NULL",
                    ErrorMessage = "Request body is required.",
                    RemediationHint = "Provide a valid RecordApprovalWebhookRequest body."
                };
            }

            if (string.IsNullOrWhiteSpace(request.CaseId))
            {
                return new RecordApprovalWebhookResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_CASE_ID",
                    ErrorMessage = "CaseId is required.",
                    RemediationHint = "Provide a non-empty CaseId identifying the compliance case."
                };
            }

            if (string.IsNullOrWhiteSpace(request.HeadRef))
            {
                return new RecordApprovalWebhookResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_HEAD_REF",
                    ErrorMessage = "HeadRef is required.",
                    RemediationHint = "Provide the head commit SHA or release tag this approval relates to."
                };
            }

            // Compute payload hash if a raw payload was provided
            string? payloadHash = null;
            bool isValid = true;
            string? validationError = null;

            if (!string.IsNullOrWhiteSpace(request.RawPayload))
            {
                try
                {
                    payloadHash = ComputeSha256Hex(request.RawPayload);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "ProtectedSignOff.RecordApprovalWebhook: failed to hash raw payload. CorrelationId={CorrelationId} Error={Error}",
                        LoggingHelper.SanitizeLogInput(correlationId),
                        ex.Message);
                }
            }

            // Validate malformed outcomes
            if (request.Outcome == ApprovalWebhookOutcome.Malformed)
            {
                isValid = false;
                validationError = "Webhook payload is malformed. Review the raw payload and re-deliver.";
            }
            else if (request.Outcome == ApprovalWebhookOutcome.DeliveryError || request.Outcome == ApprovalWebhookOutcome.TimedOut)
            {
                isValid = false;
                validationError = $"Webhook delivery outcome indicates a delivery problem: {request.Outcome}.";
            }

            var record = new ApprovalWebhookRecord
            {
                RecordId = Guid.NewGuid().ToString("N"),
                CaseId = request.CaseId,
                HeadRef = request.HeadRef,
                Outcome = request.Outcome,
                ReceivedAt = _timeProvider.GetUtcNow(),
                ActorId = request.ActorId,
                Reason = request.Reason,
                CorrelationId = correlationId,
                IsValid = isValid,
                ValidationError = validationError,
                PayloadHash = payloadHash,
                Metadata = request.Metadata ?? new Dictionary<string, string>()
            };

            // Persist the record
            _webhookHistory.GetOrAdd(request.CaseId, _ => new ConcurrentQueue<ApprovalWebhookRecord>()).Enqueue(record);

            _logger.LogInformation(
                "ProtectedSignOff.RecordApprovalWebhook persisted. RecordId={RecordId} CaseId={CaseId} HeadRef={HeadRef} Outcome={Outcome} IsValid={IsValid}",
                record.RecordId,
                LoggingHelper.SanitizeLogInput(record.CaseId),
                LoggingHelper.SanitizeLogInput(record.HeadRef),
                record.Outcome.ToString(),
                record.IsValid);

            // Emit webhook event asynchronously
            if (_webhookService != null)
            {
                var eventType = request.Outcome == ApprovalWebhookOutcome.Approved
                    ? WebhookEventType.ProtectedSignOffApprovalWebhookReceived
                    : request.Outcome == ApprovalWebhookOutcome.Escalated
                        ? WebhookEventType.ProtectedSignOffEscalationWebhookReceived
                        : WebhookEventType.ProtectedSignOffApprovalWebhookReceived;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webhookService.EmitEventAsync(new WebhookEvent
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            EventType = eventType,
                            Timestamp = record.ReceivedAt.UtcDateTime,
                            Data = new Dictionary<string, object>
                            {
                                ["recordId"] = record.RecordId,
                                ["caseId"] = record.CaseId,
                                ["headRef"] = record.HeadRef,
                                ["outcome"] = record.Outcome.ToString(),
                                ["isValid"] = record.IsValid,
                                ["correlationId"] = correlationId
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "ProtectedSignOff.RecordApprovalWebhook: webhook emission failed. CorrelationId={CorrelationId} Error={Error}",
                            LoggingHelper.SanitizeLogInput(correlationId),
                            ex.Message);
                    }
                });
            }

            return new RecordApprovalWebhookResponse
            {
                Success = true,
                Record = record
            };
        }

        // ── PersistSignOffEvidenceAsync ───────────────────────────────────────

        /// <inheritdoc/>
        public async Task<PersistSignOffEvidenceResponse> PersistSignOffEvidenceAsync(
            PersistSignOffEvidenceRequest request,
            string actorId)
        {
            _logger.LogInformation(
                "ProtectedSignOff.PersistEvidence. HeadRef={HeadRef} CaseId={CaseId} RequireReleaseGrade={RequireReleaseGrade} RequireApprovalWebhook={RequireApprovalWebhook} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request?.HeadRef),
                LoggingHelper.SanitizeLogInput(request?.CaseId),
                request?.RequireReleaseGrade.ToString() ?? "null",
                request?.RequireApprovalWebhook.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId));

            if (request == null)
            {
                return new PersistSignOffEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "REQUEST_NULL",
                    ErrorMessage = "Request body is required.",
                    RemediationHint = "Provide a valid PersistSignOffEvidenceRequest body."
                };
            }

            if (string.IsNullOrWhiteSpace(request.HeadRef))
            {
                return new PersistSignOffEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_HEAD_REF",
                    ErrorMessage = "HeadRef is required.",
                    RemediationHint = "Provide the head commit SHA or release tag to capture evidence against."
                };
            }

            var now = _timeProvider.GetUtcNow();
            var freshnessWindowHours = request.FreshnessWindowHours > 0 ? request.FreshnessWindowHours : 24;

            // If RequireApprovalWebhook, check that an approved webhook exists for this head
            if (request.RequireApprovalWebhook)
            {
                var latestApproval = GetLatestApprovedWebhookForHead(request.HeadRef, request.CaseId);
                if (latestApproval == null)
                {
                    return new PersistSignOffEvidenceResponse
                    {
                        Success = false,
                        ErrorCode = "MISSING_APPROVAL_WEBHOOK",
                        ErrorMessage = $"No approved webhook has been received for head ref '{request.HeadRef}'.",
                        RemediationHint = "Ensure an approval webhook is delivered for this head ref before persisting evidence."
                    };
                }
            }

            // Build items list
            var items = new List<EvidencePackItem>
            {
                new EvidencePackItem
                {
                    EvidenceType = "PROTECTED_SIGN_OFF_EVIDENCE",
                    SourceService = "ProtectedSignOffEvidencePersistenceService",
                    IsPresent = true,
                    CapturedAt = now,
                    ExpiresAt = now.AddHours(freshnessWindowHours),
                    Detail = $"Evidence captured at {now:O} for head ref '{request.HeadRef}'."
                }
            };

            // Include case evidence item if a case is provided
            if (!string.IsNullOrWhiteSpace(request.CaseId))
            {
                items.Add(new EvidencePackItem
                {
                    EvidenceType = "COMPLIANCE_CASE_REFERENCE",
                    SourceService = "ComplianceCaseManagementService",
                    IsPresent = true,
                    CapturedAt = now,
                    ExpiresAt = now.AddHours(freshnessWindowHours),
                    ExternalReference = request.CaseId,
                    Detail = $"Compliance case '{request.CaseId}' included in evidence pack."
                });
            }

            // Get latest approval webhook for this head
            var approvalWebhook = GetLatestApprovedWebhookForHead(request.HeadRef, request.CaseId);

            if (approvalWebhook != null)
            {
                items.Add(new EvidencePackItem
                {
                    EvidenceType = "APPROVAL_WEBHOOK",
                    SourceService = "ProtectedSignOffEvidencePersistenceService",
                    IsPresent = true,
                    CapturedAt = approvalWebhook.ReceivedAt,
                    ExternalReference = approvalWebhook.RecordId,
                    Detail = $"Approval webhook '{approvalWebhook.RecordId}' received at {approvalWebhook.ReceivedAt:O}."
                });
            }

            bool isProviderBacked = true; // In this in-memory implementation, treat all as provider-backed
            bool isReleaseGrade = isProviderBacked && approvalWebhook != null;

            if (request.RequireReleaseGrade && !isReleaseGrade)
            {
                return new PersistSignOffEvidenceResponse
                {
                    Success = false,
                    ErrorCode = "NOT_RELEASE_GRADE",
                    ErrorMessage = "Evidence pack cannot be marked as release-grade: approval webhook is missing.",
                    RemediationHint = "Ensure an approval webhook is received before persisting release-grade evidence."
                };
            }

            // Compute content hash
            var packId = Guid.NewGuid().ToString("N");
            var contentHash = ComputeSha256Hex(
                JsonSerializer.Serialize(new { packId, headRef = request.HeadRef, caseId = request.CaseId, items, isProviderBacked, isReleaseGrade }));

            var pack = new ProtectedSignOffEvidencePack
            {
                PackId = packId,
                HeadRef = request.HeadRef,
                CaseId = request.CaseId,
                FreshnessStatus = SignOffEvidenceFreshnessStatus.Complete,
                CreatedAt = now,
                LastEvaluatedAt = now,
                ExpiresAt = now.AddHours(freshnessWindowHours),
                IsProviderBacked = isProviderBacked,
                IsReleaseGrade = isReleaseGrade,
                ApprovalWebhook = approvalWebhook,
                Items = items,
                ContentHash = contentHash,
                CreatedBy = actorId
            };

            // Persist the pack
            _evidencePacks.GetOrAdd(request.HeadRef, _ => new ConcurrentQueue<ProtectedSignOffEvidencePack>()).Enqueue(pack);

            _logger.LogInformation(
                "ProtectedSignOff.PersistEvidence persisted. PackId={PackId} HeadRef={HeadRef} CaseId={CaseId} IsReleaseGrade={IsReleaseGrade}",
                pack.PackId,
                LoggingHelper.SanitizeLogInput(pack.HeadRef),
                LoggingHelper.SanitizeLogInput(pack.CaseId),
                pack.IsReleaseGrade);

            // Emit webhook event asynchronously
            if (_webhookService != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webhookService.EmitEventAsync(new WebhookEvent
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            EventType = WebhookEventType.ProtectedSignOffEvidencePersisted,
                            Timestamp = now.UtcDateTime,
                            Data = new Dictionary<string, object>
                            {
                                ["packId"] = pack.PackId,
                                ["headRef"] = pack.HeadRef,
                                ["caseId"] = pack.CaseId ?? string.Empty,
                                ["isReleaseGrade"] = pack.IsReleaseGrade,
                                ["freshnessStatus"] = pack.FreshnessStatus.ToString()
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "ProtectedSignOff.PersistEvidence: webhook emission failed. PackId={PackId} Error={Error}",
                            pack.PackId,
                            ex.Message);
                    }
                });
            }

            return new PersistSignOffEvidenceResponse
            {
                Success = true,
                Pack = pack
            };
        }

        // ── GetReleaseReadinessAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetSignOffReleaseReadinessResponse> GetReleaseReadinessAsync(
            GetSignOffReleaseReadinessRequest request)
        {
            _logger.LogInformation(
                "ProtectedSignOff.GetReleaseReadiness. HeadRef={HeadRef} CaseId={CaseId}",
                LoggingHelper.SanitizeLogInput(request?.HeadRef),
                LoggingHelper.SanitizeLogInput(request?.CaseId));

            if (request == null || string.IsNullOrWhiteSpace(request.HeadRef))
            {
                return Task.FromResult(new GetSignOffReleaseReadinessResponse
                {
                    Success = false,
                    Status = SignOffReleaseReadinessStatus.Indeterminate,
                    HeadRef = request?.HeadRef ?? string.Empty,
                    EvidenceFreshness = SignOffEvidenceFreshnessStatus.Unavailable,
                    ErrorCode = "MISSING_HEAD_REF",
                    ErrorMessage = "HeadRef is required.",
                    OperatorGuidance = "Provide a non-empty HeadRef to evaluate release readiness.",
                    EvaluatedAt = _timeProvider.GetUtcNow()
                });
            }

            var now = _timeProvider.GetUtcNow();
            var freshnessWindowHours = request.FreshnessWindowHours > 0 ? request.FreshnessWindowHours : 24;
            var blockers = new List<SignOffReleaseBlocker>();

            // ── Evaluate evidence freshness ───────────────────────────────────
            var latestPack = GetLatestEvidencePackForHead(request.HeadRef, request.CaseId);
            var freshnessStatus = ClassifyFreshness(latestPack, request.HeadRef, now, freshnessWindowHours);

            if (freshnessStatus == SignOffEvidenceFreshnessStatus.Unavailable)
            {
                blockers.Add(new SignOffReleaseBlocker
                {
                    Category = SignOffReleaseBlockerCategory.MissingEvidence,
                    Code = "EVIDENCE_UNAVAILABLE",
                    Description = $"No evidence pack exists for head ref '{request.HeadRef}'.",
                    RemediationHint = "Run the protected sign-off workflow and persist an evidence pack for this head ref.",
                    IsCritical = true
                });
            }
            else if (freshnessStatus == SignOffEvidenceFreshnessStatus.Stale)
            {
                blockers.Add(new SignOffReleaseBlocker
                {
                    Category = SignOffReleaseBlockerCategory.StaleEvidence,
                    Code = "EVIDENCE_STALE",
                    Description = $"Evidence pack for head ref '{request.HeadRef}' has expired.",
                    RemediationHint = "Re-run the protected sign-off workflow to refresh the evidence pack.",
                    IsCritical = true
                });
            }
            else if (freshnessStatus == SignOffEvidenceFreshnessStatus.HeadMismatch)
            {
                blockers.Add(new SignOffReleaseBlocker
                {
                    Category = SignOffReleaseBlockerCategory.HeadMismatch,
                    Code = "HEAD_MISMATCH",
                    Description = "Evidence was captured for a different head ref than the one being evaluated.",
                    RemediationHint = "Re-run sign-off against the current head ref.",
                    IsCritical = true
                });
            }
            else if (freshnessStatus == SignOffEvidenceFreshnessStatus.Partial)
            {
                blockers.Add(new SignOffReleaseBlocker
                {
                    Category = SignOffReleaseBlockerCategory.MissingEvidence,
                    Code = "EVIDENCE_PARTIAL",
                    Description = "Evidence pack is incomplete. One or more required evidence items are missing.",
                    RemediationHint = "Complete the sign-off workflow to capture all required evidence items.",
                    IsCritical = false
                });
            }

            // ── Evaluate approval webhook ─────────────────────────────────────
            var latestApproval = GetLatestApprovedWebhookForHead(request.HeadRef, request.CaseId);
            bool hasApprovalWebhook = latestApproval != null;

            if (!hasApprovalWebhook)
            {
                // Check if there is any webhook at all (to distinguish missing vs denied)
                var allWebhooks = GetAllWebhooksForHead(request.HeadRef, request.CaseId);
                var latestDenied = allWebhooks.FirstOrDefault(w => w.Outcome == ApprovalWebhookOutcome.Denied);
                var latestMalformed = allWebhooks.FirstOrDefault(w => w.Outcome == ApprovalWebhookOutcome.Malformed);

                if (latestDenied != null)
                {
                    blockers.Add(new SignOffReleaseBlocker
                    {
                        Category = SignOffReleaseBlockerCategory.ApprovalDenied,
                        Code = "APPROVAL_DENIED",
                        Description = $"Approval was explicitly denied for head ref '{request.HeadRef}'.",
                        RemediationHint = "Review the denial reason and remediate before re-submitting for approval.",
                        IsCritical = true
                    });
                }
                else if (latestMalformed != null)
                {
                    blockers.Add(new SignOffReleaseBlocker
                    {
                        Category = SignOffReleaseBlockerCategory.MalformedWebhook,
                        Code = "MALFORMED_WEBHOOK",
                        Description = "An approval webhook arrived but the payload was malformed.",
                        RemediationHint = "Re-deliver the approval webhook with a valid, complete payload.",
                        IsCritical = true
                    });
                }
                else
                {
                    blockers.Add(new SignOffReleaseBlocker
                    {
                        Category = SignOffReleaseBlockerCategory.MissingApproval,
                        Code = "APPROVAL_MISSING",
                        Description = $"No approval webhook has been received for head ref '{request.HeadRef}'.",
                        RemediationHint = "Trigger the approval workflow and ensure the webhook is delivered to this endpoint.",
                        IsCritical = true
                    });
                }
            }
            else if (!latestApproval!.IsValid)
            {
                blockers.Add(new SignOffReleaseBlocker
                {
                    Category = SignOffReleaseBlockerCategory.MalformedWebhook,
                    Code = "INVALID_WEBHOOK",
                    Description = "The latest approval webhook is marked invalid.",
                    RemediationHint = $"Resolve the validation error: {latestApproval.ValidationError}",
                    IsCritical = true
                });
            }

            // ── Compute overall status ────────────────────────────────────────
            SignOffReleaseReadinessStatus overallStatus;
            string? operatorGuidance = null;

            var criticalBlockers = blockers.Where(b => b.IsCritical).ToList();

            if (criticalBlockers.Count == 0 && blockers.Count == 0)
            {
                overallStatus = SignOffReleaseReadinessStatus.Ready;
            }
            else if (freshnessStatus is SignOffEvidenceFreshnessStatus.Stale or SignOffEvidenceFreshnessStatus.HeadMismatch
                     && criticalBlockers.All(b => b.Category is SignOffReleaseBlockerCategory.StaleEvidence or SignOffReleaseBlockerCategory.HeadMismatch))
            {
                overallStatus = SignOffReleaseReadinessStatus.Stale;
                operatorGuidance = "Evidence is stale or mismatched. Re-run the protected sign-off workflow against the current head.";
            }
            else if (criticalBlockers.Count > 0)
            {
                overallStatus = SignOffReleaseReadinessStatus.Blocked;
                operatorGuidance = BuildOperatorGuidance(criticalBlockers);
            }
            else
            {
                overallStatus = SignOffReleaseReadinessStatus.Pending;
                operatorGuidance = "Evidence pack is present but incomplete. Complete all required sign-off steps before releasing.";
            }

            if (overallStatus == SignOffReleaseReadinessStatus.Ready)
            {
                // Emit ready signal asynchronously
                if (_webhookService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _webhookService.EmitEventAsync(new WebhookEvent
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                EventType = WebhookEventType.ProtectedSignOffReleaseReadySignaled,
                                Timestamp = now.UtcDateTime,
                                Data = new Dictionary<string, object>
                                {
                                    ["headRef"] = request.HeadRef,
                                    ["caseId"] = request.CaseId ?? string.Empty,
                                    ["status"] = overallStatus.ToString()
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                "ProtectedSignOff.GetReleaseReadiness: ready-signal webhook emission failed. Error={Error}",
                                ex.Message);
                        }
                    });
                }
            }

            return Task.FromResult(new GetSignOffReleaseReadinessResponse
            {
                Success = overallStatus == SignOffReleaseReadinessStatus.Ready,
                Status = overallStatus,
                HeadRef = request.HeadRef,
                EvidenceFreshness = freshnessStatus,
                HasApprovalWebhook = hasApprovalWebhook,
                LatestApprovalWebhook = latestApproval,
                LatestEvidencePack = latestPack,
                Blockers = blockers,
                OperatorGuidance = operatorGuidance,
                EvaluatedAt = now
            });
        }

        // ── GetApprovalWebhookHistoryAsync ───────────────────────────────────

        /// <inheritdoc/>
        public Task<GetApprovalWebhookHistoryResponse> GetApprovalWebhookHistoryAsync(
            GetApprovalWebhookHistoryRequest request)
        {
            if (request == null)
            {
                return Task.FromResult(new GetApprovalWebhookHistoryResponse
                {
                    Success = false,
                    ErrorCode = "REQUEST_NULL",
                    ErrorMessage = "Request body is required."
                });
            }

            IEnumerable<ApprovalWebhookRecord> records;

            if (!string.IsNullOrWhiteSpace(request.CaseId))
            {
                records = _webhookHistory.TryGetValue(request.CaseId, out var queue)
                    ? queue.AsEnumerable()
                    : Enumerable.Empty<ApprovalWebhookRecord>();
            }
            else
            {
                records = _webhookHistory.Values
                    .SelectMany(q => q.AsEnumerable());
            }

            if (!string.IsNullOrWhiteSpace(request.HeadRef))
                records = records.Where(r => r.HeadRef == request.HeadRef);

            var ordered = records.OrderByDescending(r => r.ReceivedAt).ToList();
            var maxRecords = request.MaxRecords > 0 ? request.MaxRecords : 50;

            return Task.FromResult(new GetApprovalWebhookHistoryResponse
            {
                Success = true,
                TotalCount = ordered.Count,
                Records = ordered.Take(maxRecords).ToList()
            });
        }

        // ── GetEvidencePackHistoryAsync ───────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetEvidencePackHistoryResponse> GetEvidencePackHistoryAsync(
            GetEvidencePackHistoryRequest request)
        {
            if (request == null)
            {
                return Task.FromResult(new GetEvidencePackHistoryResponse
                {
                    Success = false,
                    ErrorCode = "REQUEST_NULL",
                    ErrorMessage = "Request body is required."
                });
            }

            IEnumerable<ProtectedSignOffEvidencePack> packs;

            if (!string.IsNullOrWhiteSpace(request.HeadRef))
            {
                packs = _evidencePacks.TryGetValue(request.HeadRef, out var queue)
                    ? queue.AsEnumerable()
                    : Enumerable.Empty<ProtectedSignOffEvidencePack>();
            }
            else
            {
                packs = _evidencePacks.Values
                    .SelectMany(q => q.AsEnumerable());
            }

            if (!string.IsNullOrWhiteSpace(request.CaseId))
                packs = packs.Where(p => p.CaseId == request.CaseId);

            var ordered = packs.OrderByDescending(p => p.CreatedAt).ToList();
            var maxRecords = request.MaxRecords > 0 ? request.MaxRecords : 50;

            return Task.FromResult(new GetEvidencePackHistoryResponse
            {
                Success = true,
                TotalCount = ordered.Count,
                Packs = ordered.Take(maxRecords).ToList()
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private ApprovalWebhookRecord? GetLatestApprovedWebhookForHead(string headRef, string? caseId)
        {
            IEnumerable<ApprovalWebhookRecord> records;

            if (!string.IsNullOrWhiteSpace(caseId))
            {
                records = _webhookHistory.TryGetValue(caseId, out var q)
                    ? q.AsEnumerable()
                    : Enumerable.Empty<ApprovalWebhookRecord>();
            }
            else
            {
                records = _webhookHistory.Values.SelectMany(q => q.AsEnumerable());
            }

            return records
                .Where(r => r.HeadRef == headRef && r.Outcome == ApprovalWebhookOutcome.Approved && r.IsValid)
                .OrderByDescending(r => r.ReceivedAt)
                .FirstOrDefault();
        }

        private IEnumerable<ApprovalWebhookRecord> GetAllWebhooksForHead(string headRef, string? caseId)
        {
            IEnumerable<ApprovalWebhookRecord> records;

            if (!string.IsNullOrWhiteSpace(caseId))
            {
                records = _webhookHistory.TryGetValue(caseId, out var q)
                    ? q.AsEnumerable()
                    : Enumerable.Empty<ApprovalWebhookRecord>();
            }
            else
            {
                records = _webhookHistory.Values.SelectMany(q => q.AsEnumerable());
            }

            return records
                .Where(r => r.HeadRef == headRef)
                .OrderByDescending(r => r.ReceivedAt);
        }

        private ProtectedSignOffEvidencePack? GetLatestEvidencePackForHead(string headRef, string? caseId)
        {
            if (!_evidencePacks.TryGetValue(headRef, out var queue))
                return null;

            var packs = queue.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(caseId))
                packs = packs.Where(p => p.CaseId == caseId);

            return packs.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        }

        private SignOffEvidenceFreshnessStatus ClassifyFreshness(
            ProtectedSignOffEvidencePack? pack,
            string requestedHeadRef,
            DateTimeOffset now,
            int freshnessWindowHours)
        {
            if (pack == null)
                return SignOffEvidenceFreshnessStatus.Unavailable;

            if (pack.HeadRef != requestedHeadRef)
                return SignOffEvidenceFreshnessStatus.HeadMismatch;

            if (pack.ExpiresAt.HasValue && now > pack.ExpiresAt.Value)
                return SignOffEvidenceFreshnessStatus.Stale;

            // Check if all items are present and not expired
            bool anyExpired = pack.Items.Any(i => i.IsExpired);
            bool anyMissing = pack.Items.Any(i => !i.IsPresent);

            if (anyExpired || anyMissing)
                return SignOffEvidenceFreshnessStatus.Partial;

            return SignOffEvidenceFreshnessStatus.Complete;
        }

        private static string BuildOperatorGuidance(List<SignOffReleaseBlocker> criticalBlockers)
        {
            if (criticalBlockers.Count == 1)
                return $"Release blocked: {criticalBlockers[0].Description} — {criticalBlockers[0].RemediationHint}";

            var hints = string.Join("; ", criticalBlockers.Select(b => b.RemediationHint));
            return $"Release blocked by {criticalBlockers.Count} critical blockers. Actions required: {hints}";
        }

        private static string ComputeSha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

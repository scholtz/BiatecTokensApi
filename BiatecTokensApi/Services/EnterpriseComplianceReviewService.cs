using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.EnterpriseComplianceReview;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of the enterprise compliance review service.
    ///
    /// Provides role-aware reviewer queues, compliance evidence bundles, structured review
    /// decision recording, audit history export, and operational diagnostics for enterprise
    /// team operations.
    ///
    /// This service builds on top of <see cref="IIssuerWorkflowService"/> to add:
    ///   - Evidence bundle synthesis with contradiction detection
    ///   - Role-capability calculation for frontend permission surfacing
    ///   - Structured diagnostics for operator observability
    ///   - Enriched audit history with actor roles and evidence references
    ///   - Audit export suitable for regulators and auditors
    /// </summary>
    public class EnterpriseComplianceReviewService : IEnterpriseComplianceReviewService
    {
        private readonly IIssuerWorkflowService _workflowService;
        private readonly ILogger<EnterpriseComplianceReviewService> _logger;

        // Diagnostics event log (per issuer, bounded to last 200 events)
        private readonly ConcurrentDictionary<string, List<ReviewDiagnosticsEvent>> _diagnosticsLog = new();

        // Decision metadata store (enriches workflow audit entries with decision context)
        private readonly ConcurrentDictionary<string, ReviewDecisionMetadata> _decisionMetadata = new();

        private static readonly IssuerTeamRole[] _approverRoles =
            { IssuerTeamRole.ComplianceReviewer, IssuerTeamRole.FinanceReviewer, IssuerTeamRole.Admin };
        private static readonly IssuerTeamRole[] _operatorRoles =
            { IssuerTeamRole.Operator, IssuerTeamRole.Admin };
        private static readonly IssuerTeamRole[] _diagnosticsRoles =
            { IssuerTeamRole.ComplianceReviewer, IssuerTeamRole.Admin };
        private static readonly IssuerTeamRole[] _allActiveRoles =
            { IssuerTeamRole.Operator, IssuerTeamRole.ComplianceReviewer, IssuerTeamRole.FinanceReviewer, IssuerTeamRole.Admin, IssuerTeamRole.ReadOnlyObserver };

        private const int MaxDiagnosticsEventsPerIssuer = 200;

        /// <summary>
        /// Initializes a new instance of <see cref="EnterpriseComplianceReviewService"/>.
        /// </summary>
        public EnterpriseComplianceReviewService(
            IIssuerWorkflowService workflowService,
            ILogger<EnterpriseComplianceReviewService> logger)
        {
            _workflowService = workflowService;
            _logger          = logger;
        }

        // ── Review Queue ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ReviewQueueResponse> GetReviewQueueAsync(string issuerId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return FailQueue(ErrorCodes.MISSING_REQUIRED_FIELD, "IssuerId is required.", actorId, correlationId);
            if (string.IsNullOrWhiteSpace(actorId))
                return FailQueue(ErrorCodes.MISSING_REQUIRED_FIELD, "ActorId is required.", actorId, correlationId);

            // Verify actor is a member and get their role
            var memberResponse = await _workflowService.GetAssignedQueueAsync(issuerId, actorId, actorId);
            if (!memberResponse.Success)
            {
                if (IsAuthError(memberResponse.ErrorCode))
                {
                    AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                    {
                        Category         = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                        Severity         = EvidenceIssueSeverity.Warning,
                        Description      = $"Actor '{LoggingHelper.SanitizeLogInput(actorId)}' was denied access to the review queue for issuer '{LoggingHelper.SanitizeLogInput(issuerId)}'.",
                        OperatorGuidance = $"Verify that '{actorId}' is an active member of issuer '{issuerId}'. Use POST /api/v1/issuer-workflow/{{issuerId}}/members to add them.",
                        ActorId          = actorId,
                        Metadata         = new() { ["CorrelationId"] = correlationId, ["ErrorCode"] = memberResponse.ErrorCode ?? "UNKNOWN" }
                    });
                }
                return FailQueue(memberResponse.ErrorCode ?? ErrorCodes.UNAUTHORIZED, memberResponse.ErrorMessage ?? "Access denied.", actorId, correlationId);
            }

            // Get actor's role
            var membersResponse = await _workflowService.ListMembersAsync(issuerId, actorId);
            IssuerTeamRole? actorRole = null;
            if (membersResponse.Success)
            {
                var membership = membersResponse.Members.FirstOrDefault(m => m.UserId == actorId && m.IsActive);
                actorRole = membership?.Role;
            }

            // Get all workflow items the actor can see
            var allItemsResponse = await _workflowService.ListWorkflowItemsAsync(issuerId, actorId);
            if (!allItemsResponse.Success)
                return FailQueue(allItemsResponse.ErrorCode ?? ErrorCodes.UNAUTHORIZED, allItemsResponse.ErrorMessage ?? "Access denied.", actorId, correlationId);

            var queueItems = new List<ReviewQueueItem>();
            foreach (var item in allItemsResponse.Items)
            {
                var capabilities = CalculateCapabilities(actorRole, item);
                var evidenceIssueSummary = SynthesizeEvidenceIssueSummary(item);
                bool isBlocked = evidenceIssueSummary.CriticalCount > 0 &&
                                 item.State == WorkflowApprovalState.PendingReview;

                queueItems.Add(new ReviewQueueItem
                {
                    WorkflowItem          = item,
                    AvailableCapabilities = capabilities,
                    HasEvidenceBundle     = true,
                    EvidenceIssueSummary  = evidenceIssueSummary,
                    IsEvidenceBlocked     = isBlocked,
                    EvidenceBlockReason   = isBlocked
                        ? $"This item has {evidenceIssueSummary.CriticalCount} critical evidence issue(s) that should be resolved before approval."
                        : null
                });
            }

            _logger.LogInformation(
                "ReviewQueue fetched. IssuerId={IssuerId} Actor={Actor} Items={Count} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId),
                queueItems.Count,
                LoggingHelper.SanitizeLogInput(correlationId));

            return new ReviewQueueResponse
            {
                Success              = true,
                ActorId              = actorId,
                ActorRole            = actorRole,
                Items                = queueItems,
                TotalCount           = queueItems.Count,
                EvidenceBlockedCount = queueItems.Count(i => i.IsEvidenceBlocked),
                ActionableCount      = queueItems.Count(i => i.AvailableCapabilities.Count > 1 && !i.IsEvidenceBlocked),
                CorrelationId        = correlationId
            };
        }

        // ── Evidence Bundle ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ReviewEvidenceBundleResponse> GetEvidenceBundleAsync(
            string issuerId, string workflowId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return FailBundle(ErrorCodes.MISSING_REQUIRED_FIELD, "IssuerId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(workflowId))
                return FailBundle(ErrorCodes.MISSING_REQUIRED_FIELD, "WorkflowId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(actorId))
                return FailBundle(ErrorCodes.MISSING_REQUIRED_FIELD, "ActorId is required.", correlationId);

            var itemResponse = await _workflowService.GetWorkflowItemAsync(issuerId, workflowId, actorId);
            if (!itemResponse.Success)
            {
                if (IsAuthError(itemResponse.ErrorCode))
                {
                    AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                    {
                        Category         = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                        Severity         = EvidenceIssueSeverity.Warning,
                        Description      = $"Actor '{LoggingHelper.SanitizeLogInput(actorId)}' was denied access to evidence bundle for workflow '{LoggingHelper.SanitizeLogInput(workflowId)}'.",
                        OperatorGuidance = $"Ensure actor '{actorId}' is an active team member of issuer '{issuerId}'.",
                        WorkflowId       = workflowId,
                        ActorId          = actorId,
                        Metadata         = new() { ["CorrelationId"] = correlationId }
                    });
                }
                return FailBundle(itemResponse.ErrorCode ?? ErrorCodes.UNAUTHORIZED, itemResponse.ErrorMessage ?? "Access denied.", correlationId);
            }

            var item   = itemResponse.WorkflowItem!;
            var bundle = AssembleEvidenceBundle(item, issuerId, actorId);

            _logger.LogInformation(
                "EvidenceBundle assembled. IssuerId={IssuerId} WorkflowId={WorkflowId} Actor={Actor} IsReviewReady={IsReviewReady} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(workflowId),
                LoggingHelper.SanitizeLogInput(actorId),
                bundle.IsReviewReady,
                LoggingHelper.SanitizeLogInput(correlationId));

            if (!bundle.IsReviewReady)
            {
                AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                {
                    Category         = ReviewDiagnosticsEventCategory.MissingEvidence,
                    Severity         = EvidenceIssueSeverity.Warning,
                    Description      = $"Evidence bundle for workflow '{workflowId}' is not review-ready: {bundle.ReviewReadinessSummary}",
                    OperatorGuidance = "Review the MissingEvidence and Contradictions fields to understand what must be resolved before approval.",
                    WorkflowId       = workflowId,
                    ActorId          = actorId,
                    Metadata         = new() { ["CorrelationId"] = correlationId, ["CriticalIssues"] = bundle.IssueSummary.CriticalCount.ToString() }
                });
            }

            return new ReviewEvidenceBundleResponse
            {
                Success       = true,
                Bundle        = bundle,
                CorrelationId = correlationId
            };
        }

        // ── Review Decisions ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<SubmitReviewDecisionResponse> SubmitReviewDecisionAsync(
            string issuerId, string workflowId, SubmitReviewDecisionRequest request,
            string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD, "IssuerId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(workflowId))
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD, "WorkflowId is required.", correlationId);
            if (request == null)
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD, "Request body is required.", correlationId);

            // For Reject/RequestChanges, rationale is required
            if (request.DecisionType != ReviewDecisionType.Approve &&
                string.IsNullOrWhiteSpace(request.Rationale))
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD,
                    $"Rationale is required for {request.DecisionType} decisions.", correlationId);

            // Check if approval with open critical issues requires acknowledgement
            if (request.DecisionType == ReviewDecisionType.Approve)
            {
                var bundleResponse = await GetEvidenceBundleAsync(issuerId, workflowId, actorId, correlationId);
                if (bundleResponse.Success && bundleResponse.Bundle != null)
                {
                    var bundle = bundleResponse.Bundle;
                    if (bundle.IssueSummary.CriticalCount > 0 && !request.AcknowledgesOpenIssues)
                    {
                        AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                        {
                            Category         = ReviewDiagnosticsEventCategory.EvidenceContradiction,
                            Severity         = EvidenceIssueSeverity.Critical,
                            Description      = $"Approval attempted for workflow '{workflowId}' with {bundle.IssueSummary.CriticalCount} unacknowledged critical evidence issue(s).",
                            OperatorGuidance = "The reviewer must set AcknowledgesOpenIssues=true to explicitly accept the open issues before approval proceeds.",
                            WorkflowId       = workflowId,
                            ActorId          = actorId,
                            Metadata         = new() { ["CorrelationId"] = correlationId, ["CriticalCount"] = bundle.IssueSummary.CriticalCount.ToString() }
                        });
                        return FailDecision("UNACKNOWLEDGED_EVIDENCE_ISSUES",
                            $"This item has {bundle.IssueSummary.CriticalCount} critical evidence issue(s). " +
                            "Set AcknowledgesOpenIssues=true to explicitly acknowledge these issues and proceed with approval.", correlationId);
                    }
                }
            }

            // Delegate to the underlying workflow service based on decision type
            WorkflowItemResponse workflowResponse;
            string decisionNote = BuildDecisionNote(request);

            switch (request.DecisionType)
            {
                case ReviewDecisionType.Approve:
                    workflowResponse = await _workflowService.ApproveAsync(
                        issuerId, workflowId,
                        new ApproveWorkflowItemRequest { ApprovalNote = decisionNote },
                        actorId, correlationId);
                    break;

                case ReviewDecisionType.Reject:
                    workflowResponse = await _workflowService.RejectAsync(
                        issuerId, workflowId,
                        new RejectWorkflowItemRequest { RejectionReason = request.Rationale! },
                        actorId, correlationId);
                    break;

                case ReviewDecisionType.RequestChanges:
                    workflowResponse = await _workflowService.RequestChangesAsync(
                        issuerId, workflowId,
                        new RequestChangesRequest { ChangeDescription = request.Rationale! },
                        actorId, correlationId);
                    break;

                default:
                    return FailDecision("UNSUPPORTED_DECISION_TYPE", $"Decision type '{request.DecisionType}' is not supported.", correlationId);
            }

            if (!workflowResponse.Success)
            {
                if (IsAuthError(workflowResponse.ErrorCode))
                {
                    AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                    {
                        Category         = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                        Severity         = EvidenceIssueSeverity.Warning,
                        Description      = $"Actor '{LoggingHelper.SanitizeLogInput(actorId)}' was denied permission to {request.DecisionType} workflow '{LoggingHelper.SanitizeLogInput(workflowId)}'.",
                        OperatorGuidance = $"Verify that actor '{actorId}' has a Reviewer or Admin role. Role required: ComplianceReviewer, FinanceReviewer, or Admin.",
                        WorkflowId       = workflowId,
                        ActorId          = actorId,
                        Metadata         = new() { ["CorrelationId"] = correlationId, ["DecisionType"] = request.DecisionType.ToString() }
                    });
                }
                else if (workflowResponse.ErrorCode == "INVALID_STATE_TRANSITION")
                {
                    AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                    {
                        Category         = ReviewDiagnosticsEventCategory.InvalidTransition,
                        Severity         = EvidenceIssueSeverity.Warning,
                        Description      = $"Invalid state transition attempted: {request.DecisionType} on workflow '{LoggingHelper.SanitizeLogInput(workflowId)}'. Reason: {workflowResponse.ErrorMessage}",
                        OperatorGuidance = "Check the current state of the workflow item and ensure it is in PendingReview before attempting Approve, Reject, or RequestChanges.",
                        WorkflowId       = workflowId,
                        ActorId          = actorId,
                        Metadata         = new() { ["CorrelationId"] = correlationId }
                    });
                }
                return FailDecision(workflowResponse.ErrorCode ?? "DECISION_FAILED", workflowResponse.ErrorMessage ?? "Decision could not be recorded.", correlationId);
            }

            var decisionId = Guid.NewGuid().ToString();
            var decisionTimestamp = DateTime.UtcNow;

            // Store decision metadata for enriched audit history
            _decisionMetadata[decisionId] = new ReviewDecisionMetadata
            {
                DecisionId        = decisionId,
                WorkflowId        = workflowId,
                IssuerId          = issuerId,
                ActorId           = actorId,
                DecisionType      = request.DecisionType,
                Rationale         = request.Rationale,
                EvidenceRefs      = request.EvidenceReferences,
                ReviewNote        = request.ReviewNote,
                CorrelationId     = correlationId,
                Timestamp         = decisionTimestamp
            };

            _logger.LogInformation(
                "ReviewDecision recorded. IssuerId={IssuerId} WorkflowId={WorkflowId} Actor={Actor} Decision={Decision} DecisionId={DecisionId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(workflowId),
                LoggingHelper.SanitizeLogInput(actorId),
                request.DecisionType,
                decisionId,
                LoggingHelper.SanitizeLogInput(correlationId));

            return new SubmitReviewDecisionResponse
            {
                Success             = true,
                UpdatedWorkflowItem = workflowResponse.WorkflowItem,
                DecisionType        = request.DecisionType,
                DecisionId          = decisionId,
                DecisionTimestamp   = decisionTimestamp,
                CorrelationId       = correlationId
            };
        }

        // ── Audit History ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ReviewAuditHistoryResponse> GetAuditHistoryAsync(
            string issuerId, string workflowId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return FailHistory(ErrorCodes.MISSING_REQUIRED_FIELD, "IssuerId is required.", workflowId, correlationId);
            if (string.IsNullOrWhiteSpace(workflowId))
                return FailHistory(ErrorCodes.MISSING_REQUIRED_FIELD, "WorkflowId is required.", workflowId, correlationId);

            var itemResponse = await _workflowService.GetWorkflowItemAsync(issuerId, workflowId, actorId);
            if (!itemResponse.Success)
            {
                if (IsAuthError(itemResponse.ErrorCode))
                {
                    AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                    {
                        Category         = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                        Severity         = EvidenceIssueSeverity.Warning,
                        Description      = $"Actor '{LoggingHelper.SanitizeLogInput(actorId)}' was denied audit history for workflow '{LoggingHelper.SanitizeLogInput(workflowId)}'.",
                        OperatorGuidance = $"Verify that '{actorId}' is an active member of issuer '{issuerId}'.",
                        WorkflowId       = workflowId,
                        ActorId          = actorId,
                        Metadata         = new() { ["CorrelationId"] = correlationId }
                    });
                }
                return FailHistory(itemResponse.ErrorCode ?? ErrorCodes.UNAUTHORIZED, itemResponse.ErrorMessage ?? "Access denied.", workflowId, correlationId);
            }

            var item    = itemResponse.WorkflowItem!;
            var members = new Dictionary<string, IssuerTeamMember>();

            var membersResponse = await _workflowService.ListMembersAsync(issuerId, actorId);
            if (membersResponse.Success)
                members = membersResponse.Members.ToDictionary(m => m.UserId);

            var enrichedEntries = item.AuditHistory.Select(entry =>
            {
                members.TryGetValue(entry.ActorId, out var member);

                // Try to match a stored decision for enrichment
                var decision = _decisionMetadata.Values.FirstOrDefault(d =>
                    d.WorkflowId == workflowId &&
                    d.ActorId    == entry.ActorId &&
                    Math.Abs((d.Timestamp - entry.Timestamp).TotalSeconds) < 5);

                return new ReviewAuditEntry
                {
                    EntryId           = entry.EntryId,
                    WorkflowId        = entry.WorkflowId,
                    FromState         = entry.FromState,
                    ToState           = entry.ToState,
                    ActorId           = entry.ActorId,
                    ActorDisplayName  = member?.DisplayName,
                    ActorRole         = member?.Role,
                    ActionDescription = DescribeTransition(entry.FromState, entry.ToState),
                    Rationale         = decision?.Rationale ?? entry.Note,
                    EvidenceReferences = decision?.EvidenceRefs ?? new(),
                    Timestamp         = entry.Timestamp,
                    CorrelationId     = entry.CorrelationId ?? correlationId
                };
            }).ToList();

            return new ReviewAuditHistoryResponse
            {
                Success       = true,
                WorkflowId    = workflowId,
                Entries       = enrichedEntries,
                TotalCount    = enrichedEntries.Count,
                CorrelationId = correlationId
            };
        }

        // ── Audit Export ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ReviewAuditExportResponse> ExportAuditAsync(
            string issuerId, ReviewAuditExportRequest request, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return FailExport(ErrorCodes.MISSING_REQUIRED_FIELD, "IssuerId is required.", issuerId, actorId, correlationId);

            var allItemsResponse = await _workflowService.ListWorkflowItemsAsync(issuerId, actorId);
            if (!allItemsResponse.Success)
            {
                if (IsAuthError(allItemsResponse.ErrorCode))
                {
                    AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                    {
                        Category         = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                        Severity         = EvidenceIssueSeverity.Warning,
                        Description      = $"Actor '{LoggingHelper.SanitizeLogInput(actorId)}' was denied access to audit export for issuer '{LoggingHelper.SanitizeLogInput(issuerId)}'.",
                        OperatorGuidance = $"Ensure actor '{actorId}' is an active team member of issuer '{issuerId}'.",
                        ActorId          = actorId,
                        Metadata         = new() { ["CorrelationId"] = correlationId }
                    });
                }
                return FailExport(allItemsResponse.ErrorCode ?? ErrorCodes.UNAUTHORIZED, allItemsResponse.ErrorMessage ?? "Access denied.", issuerId, actorId, correlationId);
            }

            var items = allItemsResponse.Items.AsEnumerable();

            // Apply filters
            if (request.StateFilter.HasValue)
                items = items.Where(i => i.State == request.StateFilter.Value);
            if (request.ItemTypeFilter.HasValue)
                items = items.Where(i => i.ItemType == request.ItemTypeFilter.Value);
            if (!string.IsNullOrWhiteSpace(request.ActorFilter))
                items = items.Where(i =>
                    i.CreatedBy == request.ActorFilter ||
                    i.ApproverActorId == request.ActorFilter ||
                    i.LatestReviewerActorId == request.ActorFilter);
            if (request.FromUtc.HasValue)
                items = items.Where(i => i.CreatedAt >= request.FromUtc.Value);
            if (request.ToUtc.HasValue)
                items = items.Where(i => i.CreatedAt <= request.ToUtc.Value);

            var records = items
                .OrderByDescending(i => i.UpdatedAt)
                .Take(request.MaxItems > 0 ? request.MaxItems : 500)
                .Select(item =>
                {
                    // Find the rejection/approval actor from audit history
                    var approveEntry = item.AuditHistory.LastOrDefault(e => e.ToState == WorkflowApprovalState.Approved);
                    var rejectEntry  = item.AuditHistory.LastOrDefault(e => e.ToState == WorkflowApprovalState.Rejected);

                    return new ReviewAuditExportRecord
                    {
                        WorkflowId              = item.WorkflowId,
                        IssuerId                = item.IssuerId,
                        ItemType                = item.ItemType,
                        Title                   = item.Title,
                        CurrentState            = item.State,
                        CreatedBy               = item.CreatedBy,
                        CreatedAt               = item.CreatedAt,
                        ApprovedBy              = approveEntry?.ActorId ?? item.ApproverActorId,
                        ApprovedAt              = item.ApprovedAt,
                        RejectedBy              = rejectEntry?.ActorId,
                        RejectedAt              = item.RejectedAt,
                        RejectionOrChangeReason = item.RejectionOrChangeReason,
                        AuditEntryCount         = item.AuditHistory.Count,
                        ExternalReference       = item.ExternalReference,
                        LastUpdatedAt           = item.UpdatedAt
                    };
                })
                .ToList();

            _logger.LogInformation(
                "AuditExport generated. IssuerId={IssuerId} Actor={Actor} RecordCount={Count} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId),
                records.Count,
                LoggingHelper.SanitizeLogInput(correlationId));

            return new ReviewAuditExportResponse
            {
                Success         = true,
                IssuerId        = issuerId,
                Records         = records,
                TotalCount      = records.Count,
                ExportCriteria  = request,
                ExportedAt      = DateTime.UtcNow,
                ExportedBy      = actorId,
                CorrelationId   = correlationId
            };
        }

        // ── Diagnostics ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ReviewDiagnosticsResponse> GetDiagnosticsAsync(
            string issuerId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return FailDiagnostics(ErrorCodes.MISSING_REQUIRED_FIELD, "IssuerId is required.", issuerId, correlationId);

            // Check actor has diagnostics-level access (ComplianceReviewer or Admin)
            var membersResponse = await _workflowService.ListMembersAsync(issuerId, actorId);
            if (!membersResponse.Success)
            {
                AppendDiagnosticsEvent(issuerId, new ReviewDiagnosticsEvent
                {
                    Category         = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                    Severity         = EvidenceIssueSeverity.Warning,
                    Description      = $"Actor '{LoggingHelper.SanitizeLogInput(actorId)}' was denied diagnostics access for issuer '{LoggingHelper.SanitizeLogInput(issuerId)}'.",
                    OperatorGuidance = $"Only ComplianceReviewer or Admin roles may access diagnostics. Add the actor to the team with an appropriate role.",
                    ActorId          = actorId,
                    Metadata         = new() { ["CorrelationId"] = correlationId }
                });
                return FailDiagnostics(membersResponse.ErrorCode ?? ErrorCodes.UNAUTHORIZED, membersResponse.ErrorMessage ?? "Access denied.", issuerId, correlationId);
            }

            var membership = membersResponse.Members.FirstOrDefault(m => m.UserId == actorId && m.IsActive);
            if (membership == null || !_diagnosticsRoles.Contains(membership.Role))
            {
                return FailDiagnostics("INSUFFICIENT_ROLE",
                    $"Diagnostics requires ComplianceReviewer or Admin role. Current role: {membership?.Role}. " +
                    "Contact your issuer Admin to update your role.", issuerId, correlationId);
            }

            var allItemsResponse = await _workflowService.ListWorkflowItemsAsync(issuerId, actorId);
            var items = allItemsResponse.Success ? allItemsResponse.Items : new List<WorkflowItem>();

            var recentEvents = _diagnosticsLog.TryGetValue(issuerId, out var events)
                ? events.OrderByDescending(e => e.Timestamp).Take(50).ToList()
                : new List<ReviewDiagnosticsEvent>();

            var categoryCounts = recentEvents
                .GroupBy(e => e.Category.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            int evidenceBlockedCount = items.Count(item =>
            {
                var issueSummary = SynthesizeEvidenceIssueSummary(item);
                return issueSummary.CriticalCount > 0 && item.State == WorkflowApprovalState.PendingReview;
            });

            return new ReviewDiagnosticsResponse
            {
                Success               = true,
                IssuerId              = issuerId,
                ActiveMemberCount     = membersResponse.Members.Count(m => m.IsActive),
                TotalWorkflowItems    = items.Count,
                PendingReviewCount    = items.Count(i => i.State == WorkflowApprovalState.PendingReview),
                EvidenceBlockedCount  = evidenceBlockedCount,
                NeedsChangesCount     = items.Count(i => i.State == WorkflowApprovalState.NeedsChanges),
                RecentEvents          = recentEvents,
                EventCategoryCounts   = categoryCounts,
                CollectedAt           = DateTime.UtcNow,
                CorrelationId         = correlationId
            };
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Calculates which capabilities an actor has for a given workflow item based on their role.
        /// Always includes View; other capabilities depend on role and current item state.
        /// </summary>
        private static List<ReviewCapability> CalculateCapabilities(
            IssuerTeamRole? actorRole, WorkflowItem item)
        {
            var caps = new List<ReviewCapability> { ReviewCapability.View };

            if (actorRole == null) return caps;

            bool isOperatorLike = actorRole == IssuerTeamRole.Operator || actorRole == IssuerTeamRole.Admin;
            bool isApproverLike = actorRole == IssuerTeamRole.ComplianceReviewer
                                  || actorRole == IssuerTeamRole.FinanceReviewer
                                  || actorRole == IssuerTeamRole.Admin;

            switch (item.State)
            {
                case WorkflowApprovalState.Prepared:
                    if (isOperatorLike) caps.Add(ReviewCapability.Submit);
                    if (isOperatorLike) caps.Add(ReviewCapability.Reassign);
                    break;

                case WorkflowApprovalState.PendingReview:
                    if (isApproverLike)
                    {
                        caps.Add(ReviewCapability.Approve);
                        caps.Add(ReviewCapability.Reject);
                        caps.Add(ReviewCapability.RequestChanges);
                    }
                    caps.Add(ReviewCapability.Reassign);
                    break;

                case WorkflowApprovalState.NeedsChanges:
                    if (isOperatorLike)
                    {
                        caps.Add(ReviewCapability.Resubmit);
                        caps.Add(ReviewCapability.Reassign);
                    }
                    break;

                case WorkflowApprovalState.Approved:
                    if (isOperatorLike) caps.Add(ReviewCapability.Complete);
                    break;

                case WorkflowApprovalState.Rejected:
                case WorkflowApprovalState.Completed:
                    // No further actions available on terminal states
                    break;
            }

            return caps;
        }

        /// <summary>
        /// Synthesizes an evidence issue summary for a workflow item based on its metadata and state.
        /// Produces representative evidence issues without calling external services.
        /// </summary>
        private static EvidenceIssueSummary SynthesizeEvidenceIssueSummary(WorkflowItem item)
        {
            int critical = 0, warnings = 0, info = 0;

            // Items with no description may lack context for reviewers
            if (string.IsNullOrWhiteSpace(item.Description))
                warnings++;

            // Items in NeedsChanges with no rejection reason are informationally incomplete
            if (item.State == WorkflowApprovalState.NeedsChanges &&
                string.IsNullOrWhiteSpace(item.RejectionOrChangeReason))
                info++;

            // ComplianceEvidenceReview items always need an external reference
            if (item.ItemType == WorkflowItemType.ComplianceEvidenceReview &&
                string.IsNullOrWhiteSpace(item.ExternalReference))
                critical++;

            // WhitelistPolicyUpdate items should always have an external reference (the policy ID)
            if (item.ItemType == WorkflowItemType.WhitelistPolicyUpdate &&
                string.IsNullOrWhiteSpace(item.ExternalReference))
                warnings++;

            return new EvidenceIssueSummary
            {
                CriticalCount = critical,
                WarningCount  = warnings,
                InfoCount     = info
            };
        }

        /// <summary>
        /// Assembles a full evidence bundle for a workflow item.
        /// </summary>
        private static ReviewEvidenceBundle AssembleEvidenceBundle(
            WorkflowItem item, string issuerId, string requestedBy)
        {
            var evidenceItems  = BuildEvidenceItems(item);
            var contradictions = DetectContradictions(item, evidenceItems);
            var missingEvidence = DetectMissingEvidence(item);
            var issueSummary   = new EvidenceIssueSummary
            {
                CriticalCount = contradictions.Count(c => c.Severity == EvidenceIssueSeverity.Critical)
                              + missingEvidence.Count(m => m.IsBlocking),
                WarningCount  = contradictions.Count(c => c.Severity == EvidenceIssueSeverity.Warning)
                              + missingEvidence.Count(m => !m.IsBlocking),
                InfoCount     = 0
            };

            bool isReviewReady = issueSummary.CriticalCount == 0;

            string reviewReadinessSummary = isReviewReady
                ? "All required evidence is present and no critical contradictions were detected. This item is ready for review."
                : $"Review is blocked: {issueSummary.CriticalCount} critical issue(s) detected. " +
                  $"Resolve the {contradictions.Count(c => c.Severity == EvidenceIssueSeverity.Critical)} critical contradiction(s) " +
                  $"and {missingEvidence.Count(m => m.IsBlocking)} blocking missing evidence item(s) before approving.";

            return new ReviewEvidenceBundle
            {
                WorkflowId            = item.WorkflowId,
                IssuerId              = issuerId,
                AssembledAt           = DateTime.UtcNow,
                RequestedBy           = requestedBy,
                EvidenceItems         = evidenceItems,
                Contradictions        = contradictions,
                MissingEvidence       = missingEvidence,
                IsReviewReady         = isReviewReady,
                ReviewReadinessSummary = reviewReadinessSummary,
                IssueSummary          = issueSummary
            };
        }

        private static List<ReviewEvidenceItem> BuildEvidenceItems(WorkflowItem item)
        {
            var items = new List<ReviewEvidenceItem>
            {
                new()
                {
                    Title            = "Workflow Item Snapshot",
                    Category         = ReviewEvidenceCategory.Workflow,
                    Source           = "IssuerWorkflowService",
                    ValidationStatus = ReviewEvidenceValidationStatus.Valid,
                    Rationale        = $"Workflow item '{item.Title}' in state '{item.State}', created by '{item.CreatedBy}' on {item.CreatedAt:O}.",
                    CollectedAt      = DateTime.UtcNow,
                    Metadata         = new()
                    {
                        ["WorkflowId"] = item.WorkflowId,
                        ["ItemType"]   = item.ItemType.ToString(),
                        ["State"]      = item.State.ToString(),
                        ["CreatedBy"]  = item.CreatedBy
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(item.ExternalReference))
            {
                items.Add(new()
                {
                    Title            = "External Reference",
                    Category         = ReviewEvidenceCategory.Policy,
                    Source           = "IssuerWorkflowService",
                    ValidationStatus = ReviewEvidenceValidationStatus.Valid,
                    Rationale        = $"This workflow item references external entity '{item.ExternalReference}'. Verify this reference is current before approving.",
                    CollectedAt      = DateTime.UtcNow,
                    Metadata         = new() { ["ExternalReference"] = item.ExternalReference }
                });
            }

            if (item.AuditHistory.Count > 0)
            {
                items.Add(new()
                {
                    Title            = "Workflow Audit Trail",
                    Category         = ReviewEvidenceCategory.AuditTrail,
                    Source           = "IssuerWorkflowService",
                    ValidationStatus = ReviewEvidenceValidationStatus.Valid,
                    Rationale        = $"{item.AuditHistory.Count} audit event(s) recorded for this workflow item. Last action: {item.AuditHistory.Last().ToState} by {item.AuditHistory.Last().ActorId}.",
                    CollectedAt      = DateTime.UtcNow,
                    Metadata         = new() { ["AuditEntryCount"] = item.AuditHistory.Count.ToString() }
                });
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                items.Add(new()
                {
                    Title            = "Preparer Description",
                    Category         = ReviewEvidenceCategory.Workflow,
                    Source           = "IssuerWorkflowService",
                    ValidationStatus = ReviewEvidenceValidationStatus.Valid,
                    Rationale        = item.Description,
                    CollectedAt      = DateTime.UtcNow
                });
            }
            else
            {
                items.Add(new()
                {
                    Title            = "Preparer Description",
                    Category         = ReviewEvidenceCategory.Workflow,
                    Source           = "IssuerWorkflowService",
                    ValidationStatus = ReviewEvidenceValidationStatus.Unavailable,
                    Rationale        = "No description provided by the preparer. Consider requesting additional context before approving.",
                    CollectedAt      = DateTime.UtcNow
                });
            }

            return items;
        }

        private static List<ContradictionDetail> DetectContradictions(
            WorkflowItem item, List<ReviewEvidenceItem> evidenceItems)
        {
            var contradictions = new List<ContradictionDetail>();

            // If ComplianceEvidenceReview has no external reference but an audit trail exists, flag it
            if (item.ItemType == WorkflowItemType.ComplianceEvidenceReview &&
                string.IsNullOrWhiteSpace(item.ExternalReference))
            {
                var auditEvidence     = evidenceItems.FirstOrDefault(e => e.Category == ReviewEvidenceCategory.AuditTrail);
                var workflowEvidence  = evidenceItems.FirstOrDefault(e => e.Title == "Workflow Item Snapshot");

                if (auditEvidence != null && workflowEvidence != null)
                {
                    contradictions.Add(new ContradictionDetail
                    {
                        Severity            = EvidenceIssueSeverity.Critical,
                        Description         = "A ComplianceEvidenceReview workflow item has no ExternalReference. This is required to link the review to the specific compliance evidence being reviewed.",
                        InvolvedEvidenceIds = new() { workflowEvidence.EvidenceId, auditEvidence.EvidenceId },
                        RecommendedAction   = "Update the workflow item's ExternalReference to the ID of the compliance evidence being reviewed, then resubmit."
                    });
                }
            }

            // If item has been NeedsChanges multiple times but no description changes, flag as potential stale loop
            int needsChangesCount = item.AuditHistory.Count(e => e.ToState == WorkflowApprovalState.NeedsChanges);
            if (needsChangesCount >= 2)
            {
                contradictions.Add(new ContradictionDetail
                {
                    Severity            = EvidenceIssueSeverity.Warning,
                    Description         = $"This item has been returned for changes {needsChangesCount} time(s), suggesting the underlying issue may not have been resolved.",
                    InvolvedEvidenceIds = new(),
                    RecommendedAction   = "Investigate the change history and ensure the root cause has been addressed. Consider escalating to Admin."
                });
            }

            return contradictions;
        }

        private static List<MissingEvidenceIndicator> DetectMissingEvidence(WorkflowItem item)
        {
            var missing = new List<MissingEvidenceIndicator>();

            if (string.IsNullOrWhiteSpace(item.Description))
            {
                missing.Add(new MissingEvidenceIndicator
                {
                    Category        = ReviewEvidenceCategory.Workflow,
                    Description     = "The workflow item has no description from the preparer.",
                    IsBlocking      = false,
                    SuggestedAction = "Ask the preparer to update the item with a description explaining the change and its compliance rationale."
                });
            }

            if (item.ItemType == WorkflowItemType.ComplianceEvidenceReview &&
                string.IsNullOrWhiteSpace(item.ExternalReference))
            {
                missing.Add(new MissingEvidenceIndicator
                {
                    Category        = ReviewEvidenceCategory.Policy,
                    Description     = "ComplianceEvidenceReview items must reference the specific compliance evidence being reviewed (ExternalReference).",
                    IsBlocking      = true,
                    SuggestedAction = "Set the ExternalReference field to the relevant compliance evidence ID and resubmit."
                });
            }

            if (item.ItemType == WorkflowItemType.WhitelistPolicyUpdate &&
                string.IsNullOrWhiteSpace(item.ExternalReference))
            {
                missing.Add(new MissingEvidenceIndicator
                {
                    Category        = ReviewEvidenceCategory.Policy,
                    Description     = "WhitelistPolicyUpdate items should reference the whitelist policy being updated (ExternalReference).",
                    IsBlocking      = false,
                    SuggestedAction = "Set the ExternalReference field to the whitelist policy ID so reviewers can access the policy context."
                });
            }

            if (item.State == WorkflowApprovalState.NeedsChanges &&
                string.IsNullOrWhiteSpace(item.RejectionOrChangeReason))
            {
                missing.Add(new MissingEvidenceIndicator
                {
                    Category        = ReviewEvidenceCategory.AuditTrail,
                    Description     = "Item is in NeedsChanges state but no change reason was recorded. Reviewers cannot understand what to fix.",
                    IsBlocking      = false,
                    SuggestedAction = "The reviewer should update the item with a specific description of required changes."
                });
            }

            return missing;
        }

        private static string DescribeTransition(WorkflowApprovalState from, WorkflowApprovalState to) =>
            (from, to) switch
            {
                (WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview)        => "Submitted for review",
                (WorkflowApprovalState.PendingReview, WorkflowApprovalState.Approved)        => "Approved",
                (WorkflowApprovalState.PendingReview, WorkflowApprovalState.Rejected)        => "Rejected",
                (WorkflowApprovalState.PendingReview, WorkflowApprovalState.NeedsChanges)    => "Returned for changes",
                (WorkflowApprovalState.NeedsChanges, WorkflowApprovalState.PendingReview)    => "Resubmitted after changes",
                (WorkflowApprovalState.Approved, WorkflowApprovalState.Completed)            => "Completed",
                _ => $"Transitioned from {from} to {to}"
            };

        private static string BuildDecisionNote(SubmitReviewDecisionRequest request)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Rationale))
                parts.Add($"Rationale: {request.Rationale}");
            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
                parts.Add($"Note: {request.ReviewNote}");
            if (request.EvidenceReferences.Count > 0)
                parts.Add($"Evidence: [{string.Join(", ", request.EvidenceReferences)}]");
            if (request.AcknowledgesOpenIssues)
                parts.Add("Reviewer acknowledged open evidence issues.");
            return string.Join(" | ", parts);
        }

        private void AppendDiagnosticsEvent(string issuerId, ReviewDiagnosticsEvent ev)
        {
            var log = _diagnosticsLog.GetOrAdd(issuerId, _ => new List<ReviewDiagnosticsEvent>());
            lock (log)
            {
                log.Add(ev);
                if (log.Count > MaxDiagnosticsEventsPerIssuer)
                    log.RemoveAt(0);
            }
        }

        private static bool IsAuthError(string? errorCode) =>
            errorCode == ErrorCodes.UNAUTHORIZED || errorCode == "INSUFFICIENT_ROLE";

        // ── Failure Factory Helpers ────────────────────────────────────────────

        private static ReviewQueueResponse FailQueue(string code, string msg, string actorId, string corrId) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = msg, ActorId = actorId, CorrelationId = corrId };

        private static ReviewEvidenceBundleResponse FailBundle(string code, string msg, string corrId) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = msg, CorrelationId = corrId };

        private static SubmitReviewDecisionResponse FailDecision(string code, string msg, string corrId) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = msg, CorrelationId = corrId };

        private static ReviewAuditHistoryResponse FailHistory(string code, string msg, string workflowId, string corrId) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = msg, WorkflowId = workflowId, CorrelationId = corrId };

        private static ReviewAuditExportResponse FailExport(string code, string msg, string issuerId, string actorId, string corrId) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = msg, IssuerId = issuerId, ExportedBy = actorId, CorrelationId = corrId };

        private static ReviewDiagnosticsResponse FailDiagnostics(string code, string msg, string issuerId, string corrId) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = msg, IssuerId = issuerId, CorrelationId = corrId };

        // ── Internal metadata type ─────────────────────────────────────────────

        private sealed class ReviewDecisionMetadata
        {
            public string DecisionId    { get; init; } = string.Empty;
            public string WorkflowId    { get; init; } = string.Empty;
            public string IssuerId      { get; init; } = string.Empty;
            public string ActorId       { get; init; } = string.Empty;
            public ReviewDecisionType DecisionType { get; init; }
            public string? Rationale    { get; init; }
            public List<string> EvidenceRefs { get; init; } = new();
            public string? ReviewNote   { get; init; }
            public string? CorrelationId { get; init; }
            public DateTime Timestamp   { get; init; }
        }
    }
}

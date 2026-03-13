using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of the issuer workflow service providing team role management
    /// and approval-state machine capabilities for enterprise collaboration workflows.
    /// </summary>
    public class IssuerWorkflowService : IIssuerWorkflowService
    {
        private readonly ILogger<IssuerWorkflowService> _logger;

        private readonly ConcurrentDictionary<string, IssuerTeamMember> _members = new();
        private readonly ConcurrentDictionary<string, WorkflowItem> _workflowItems = new();

        private static readonly IReadOnlyDictionary<WorkflowApprovalState, IReadOnlySet<WorkflowApprovalState>> _allowedTransitions =
            new Dictionary<WorkflowApprovalState, IReadOnlySet<WorkflowApprovalState>>
            {
                [WorkflowApprovalState.Prepared]      = new HashSet<WorkflowApprovalState> { WorkflowApprovalState.PendingReview },
                [WorkflowApprovalState.PendingReview] = new HashSet<WorkflowApprovalState> { WorkflowApprovalState.Approved, WorkflowApprovalState.Rejected, WorkflowApprovalState.NeedsChanges },
                [WorkflowApprovalState.Approved]      = new HashSet<WorkflowApprovalState> { WorkflowApprovalState.Completed },
                [WorkflowApprovalState.Rejected]      = new HashSet<WorkflowApprovalState>(),
                [WorkflowApprovalState.NeedsChanges]  = new HashSet<WorkflowApprovalState> { WorkflowApprovalState.PendingReview },
                [WorkflowApprovalState.Completed]     = new HashSet<WorkflowApprovalState>()
            };

        private static readonly IssuerTeamRole[] _adminOnly        = { IssuerTeamRole.Admin };
        private static readonly IssuerTeamRole[] _approverRoles    = { IssuerTeamRole.ComplianceReviewer, IssuerTeamRole.FinanceReviewer, IssuerTeamRole.Admin };
        private static readonly IssuerTeamRole[] _operatorRoles    = { IssuerTeamRole.Operator, IssuerTeamRole.Admin };
        private static readonly IssuerTeamRole[] _allActiveRoles   = { IssuerTeamRole.Operator, IssuerTeamRole.ComplianceReviewer, IssuerTeamRole.FinanceReviewer, IssuerTeamRole.Admin, IssuerTeamRole.ReadOnlyObserver };
        private static readonly IssuerTeamRole[] _nonReadonlyRoles = { IssuerTeamRole.Operator, IssuerTeamRole.ComplianceReviewer, IssuerTeamRole.FinanceReviewer, IssuerTeamRole.Admin };

        public IssuerWorkflowService(ILogger<IssuerWorkflowService> logger)
        {
            _logger = logger;
        }

        public WorkflowTransitionValidationResult ValidateTransition(WorkflowApprovalState from, WorkflowApprovalState to)
        {
            if (_allowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to))
                return new WorkflowTransitionValidationResult { IsValid = true, Reason = $"Transition from {from} to {to} is permitted." };

            return new WorkflowTransitionValidationResult
            {
                IsValid = false,
                Reason = $"Transition from {from} to {to} is not permitted. Allowed next states from {from}: [{string.Join(", ", _allowedTransitions.TryGetValue(from, out var a) ? a : new HashSet<WorkflowApprovalState>())}]."
            };
        }

        public IReadOnlySet<WorkflowApprovalState> GetAllowedTransitions(WorkflowApprovalState from)
            => _allowedTransitions.TryGetValue(from, out var allowed) ? allowed : new HashSet<WorkflowApprovalState>();

        // ── Team Membership ────────────────────────────────────────────────────

        public Task<IssuerTeamMemberResponse> AddMemberAsync(string issuerId, AddIssuerTeamMemberRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return Task.FromResult(Fail<IssuerTeamMemberResponse>("MISSING_ISSUER_ID", "IssuerId is required."));
            if (request == null || string.IsNullOrWhiteSpace(request.UserId))
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "UserId is required."));

            bool hasExistingMembers = _members.Values.Any(m => m.IssuerId == issuerId && m.IsActive);

            if (hasExistingMembers)
            {
                var authResult = RequireRole<IssuerTeamMemberResponse>(issuerId, actorId, _adminOnly);
                if (authResult != null) return Task.FromResult(authResult);
            }
            else
            {
                if (request.Role != IssuerTeamRole.Admin)
                    return Task.FromResult(Fail<IssuerTeamMemberResponse>(
                        "BOOTSTRAP_ROLE_REQUIRED",
                        "The first member of an issuer team must have the Admin role so they can manage the team."));
            }

            bool duplicate = _members.Values.Any(m => m.IssuerId == issuerId && m.UserId == request.UserId && m.IsActive);
            if (duplicate)
                return Task.FromResult(Fail<IssuerTeamMemberResponse>("DUPLICATE_MEMBER", $"UserId '{request.UserId}' is already an active member of this issuer team."));

            var member = new IssuerTeamMember
            {
                IssuerId    = issuerId,
                UserId      = request.UserId,
                DisplayName = request.DisplayName,
                Role        = request.Role,
                AddedBy     = actorId,
                IsActive    = true
            };

            _members[member.MemberId] = member;

            _logger.LogInformation(
                "IssuerTeamMember added. MemberId={MemberId} IssuerId={IssuerId} UserId={UserId} Role={Role} Actor={Actor}",
                member.MemberId,
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(request.UserId),
                member.Role,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new IssuerTeamMemberResponse { Success = true, Member = member });
        }

        public Task<IssuerTeamMemberResponse> UpdateMemberAsync(string issuerId, string memberId, UpdateIssuerTeamMemberRequest request, string actorId)
        {
            var authResult = RequireRole<IssuerTeamMemberResponse>(issuerId, actorId, _adminOnly);
            if (authResult != null) return Task.FromResult(authResult);

            if (!_members.TryGetValue(memberId, out var member))
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.NOT_FOUND, "Team member not found."));
            if (member.IssuerId != issuerId)
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: member does not belong to the specified issuer."));

            member.Role      = request.Role;
            member.UpdatedAt = DateTime.UtcNow;
            if (request.DisplayName != null)
                member.DisplayName = request.DisplayName;

            return Task.FromResult(new IssuerTeamMemberResponse { Success = true, Member = member });
        }

        public Task<IssuerTeamMemberResponse> RemoveMemberAsync(string issuerId, string memberId, string actorId)
        {
            var authResult = RequireRole<IssuerTeamMemberResponse>(issuerId, actorId, _adminOnly);
            if (authResult != null) return Task.FromResult(authResult);

            if (!_members.TryGetValue(memberId, out var member))
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.NOT_FOUND, "Team member not found."));
            if (member.IssuerId != issuerId)
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: member does not belong to the specified issuer."));

            member.IsActive  = false;
            member.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "IssuerTeamMember removed. MemberId={MemberId} IssuerId={IssuerId} Actor={Actor}",
                memberId,
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new IssuerTeamMemberResponse { Success = true, Member = member });
        }

        public Task<IssuerTeamMemberResponse> GetMemberAsync(string issuerId, string memberId, string actorId)
        {
            var authResult = RequireRole<IssuerTeamMemberResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return Task.FromResult(authResult);

            if (!_members.TryGetValue(memberId, out var member))
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.NOT_FOUND, "Team member not found."));
            if (member.IssuerId != issuerId)
                return Task.FromResult(Fail<IssuerTeamMemberResponse>(ErrorCodes.UNAUTHORIZED, "Access denied."));

            return Task.FromResult(new IssuerTeamMemberResponse { Success = true, Member = member });
        }

        public Task<IssuerTeamMembersResponse> ListMembersAsync(string issuerId, string actorId)
        {
            var authResult = RequireRole<IssuerTeamMembersResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var members = _members.Values
                .Where(m => m.IssuerId == issuerId && m.IsActive)
                .OrderBy(m => m.AddedAt)
                .ToList();

            return Task.FromResult(new IssuerTeamMembersResponse { Success = true, Members = members, TotalCount = members.Count });
        }

        // ── Workflow Items ─────────────────────────────────────────────────────

        public Task<WorkflowItemResponse> CreateWorkflowItemAsync(string issuerId, CreateWorkflowItemRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return Task.FromResult(Fail<WorkflowItemResponse>("MISSING_ISSUER_ID", "IssuerId is required."));
            if (request == null || string.IsNullOrWhiteSpace(request.Title))
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "Title is required."));

            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var item = new WorkflowItem
            {
                IssuerId          = issuerId,
                ItemType          = request.ItemType,
                State             = WorkflowApprovalState.Prepared,
                Title             = request.Title,
                Description       = request.Description,
                CreatedBy         = actorId,
                AssignedTo        = request.AssignedTo,
                ExternalReference = request.ExternalReference,
                Metadata          = request.Metadata ?? new Dictionary<string, string>()
            };

            item.AuditHistory.Add(CreateAuditEntry(item.WorkflowId, WorkflowApprovalState.Prepared, WorkflowApprovalState.Prepared, actorId, "Item created.", null));
            _workflowItems[item.WorkflowId] = item;

            _logger.LogInformation(
                "WorkflowItem created. WorkflowId={WorkflowId} IssuerId={IssuerId} ItemType={ItemType} Actor={Actor}",
                item.WorkflowId,
                LoggingHelper.SanitizeLogInput(issuerId),
                item.ItemType,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> GetWorkflowItemAsync(string issuerId, string workflowId, string actorId)
        {
            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return Task.FromResult(authResult);

            if (!_workflowItems.TryGetValue(workflowId, out var item))
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.NOT_FOUND, "Workflow item not found."));
            if (item.IssuerId != issuerId)
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: workflow item does not belong to the specified issuer."));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemListResponse> ListWorkflowItemsAsync(string issuerId, string actorId, WorkflowApprovalState? stateFilter = null, string? assignedTo = null)
        {
            var authResult = RequireRole<WorkflowItemListResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var query = _workflowItems.Values.Where(i => i.IssuerId == issuerId);
            if (stateFilter.HasValue)
                query = query.Where(i => i.State == stateFilter.Value);
            if (!string.IsNullOrWhiteSpace(assignedTo))
                query = query.Where(i => i.AssignedTo == assignedTo);

            var items = query.OrderByDescending(i => i.UpdatedAt).ToList();
            return Task.FromResult(new WorkflowItemListResponse { Success = true, Items = items, TotalCount = items.Count });
        }

        // ── Workflow Transitions ───────────────────────────────────────────────

        public Task<WorkflowItemResponse> SubmitForReviewAsync(string issuerId, string workflowId, SubmitWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var (item, err) = GetItemForTransition(issuerId, workflowId, WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview);
            if (err != null) return Task.FromResult(err);

            ApplyTransition(item!, WorkflowApprovalState.PendingReview, actorId, request?.SubmissionNote, correlationId);
            _logger.LogInformation("WorkflowItem submitted for review. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> ApproveAsync(string issuerId, string workflowId, ApproveWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _approverRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var (item, err) = GetItemForTransition(issuerId, workflowId, WorkflowApprovalState.PendingReview, WorkflowApprovalState.Approved);
            if (err != null) return Task.FromResult(err);

            item!.ApproverActorId      = actorId;
            item.LatestReviewerActorId = actorId;
            item.ApprovedAt            = DateTime.UtcNow;
            ApplyTransition(item, WorkflowApprovalState.Approved, actorId, request?.ApprovalNote, correlationId);

            _logger.LogInformation("WorkflowItem approved. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> RejectAsync(string issuerId, string workflowId, RejectWorkflowItemRequest request, string actorId, string correlationId)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RejectionReason))
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "RejectionReason is required."));

            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _approverRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var (item, err) = GetItemForTransition(issuerId, workflowId, WorkflowApprovalState.PendingReview, WorkflowApprovalState.Rejected);
            if (err != null) return Task.FromResult(err);

            item!.LatestReviewerActorId   = actorId;
            item.RejectedAt               = DateTime.UtcNow;
            item.RejectionOrChangeReason  = request.RejectionReason;
            ApplyTransition(item, WorkflowApprovalState.Rejected, actorId, request.RejectionReason, correlationId);

            _logger.LogInformation("WorkflowItem rejected. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> RequestChangesAsync(string issuerId, string workflowId, RequestChangesRequest request, string actorId, string correlationId)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ChangeDescription))
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "ChangeDescription is required."));

            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _approverRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var (item, err) = GetItemForTransition(issuerId, workflowId, WorkflowApprovalState.PendingReview, WorkflowApprovalState.NeedsChanges);
            if (err != null) return Task.FromResult(err);

            item!.LatestReviewerActorId  = actorId;
            item.RejectionOrChangeReason = request.ChangeDescription;
            ApplyTransition(item, WorkflowApprovalState.NeedsChanges, actorId, request.ChangeDescription, correlationId);

            _logger.LogInformation("WorkflowItem needs changes. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> ResubmitAsync(string issuerId, string workflowId, SubmitWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var (item, err) = GetItemForTransition(issuerId, workflowId, WorkflowApprovalState.NeedsChanges, WorkflowApprovalState.PendingReview);
            if (err != null) return Task.FromResult(err);

            ApplyTransition(item!, WorkflowApprovalState.PendingReview, actorId, request?.SubmissionNote, correlationId);
            _logger.LogInformation("WorkflowItem resubmitted. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> ReassignAsync(string issuerId, string workflowId, ReassignWorkflowItemRequest request, string actorId, string correlationId)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewAssigneeId))
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "NewAssigneeId is required."));

            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _nonReadonlyRoles);
            if (authResult != null) return Task.FromResult(authResult);

            if (!_workflowItems.TryGetValue(workflowId, out var item))
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.NOT_FOUND, "Workflow item not found."));
            if (item.IssuerId != issuerId)
                return Task.FromResult(Fail<WorkflowItemResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: workflow item does not belong to the specified issuer."));
            if (item.State == WorkflowApprovalState.Completed || item.State == WorkflowApprovalState.Rejected)
                return Task.FromResult(Fail<WorkflowItemResponse>("INVALID_STATE", $"Cannot reassign a {item.State} item."));

            bool assigneeIsValidMember = _members.Values.Any(m =>
                m.IssuerId == issuerId && m.UserId == request.NewAssigneeId && m.IsActive);
            if (!assigneeIsValidMember)
                return Task.FromResult(Fail<WorkflowItemResponse>("INVALID_ASSIGNEE", $"NewAssigneeId '{request.NewAssigneeId}' is not an active member of this issuer team."));

            string? previousAssignee = item.AssignedTo;
            item.AssignedTo = request.NewAssigneeId;
            item.UpdatedAt  = DateTime.UtcNow;

            item.AuditHistory.Add(CreateAuditEntry(
                workflowId, item.State, item.State, actorId,
                $"Reassigned from '{previousAssignee ?? "unassigned"}' to '{request.NewAssigneeId}'. {request.ReassignmentNote}".Trim(),
                correlationId));

            _logger.LogInformation("WorkflowItem reassigned. WorkflowId={WorkflowId} From={From} To={To} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(previousAssignee ?? "unassigned"),
                LoggingHelper.SanitizeLogInput(request.NewAssigneeId),
                LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        public Task<WorkflowItemResponse> CompleteAsync(string issuerId, string workflowId, CompleteWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = RequireRole<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var (item, err) = GetItemForTransition(issuerId, workflowId, WorkflowApprovalState.Approved, WorkflowApprovalState.Completed);
            if (err != null) return Task.FromResult(err);

            item!.CompletedAt = DateTime.UtcNow;
            ApplyTransition(item, WorkflowApprovalState.Completed, actorId, request?.CompletionNote, correlationId);

            _logger.LogInformation("WorkflowItem completed. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new WorkflowItemResponse { Success = true, WorkflowItem = item });
        }

        // ── Queries ────────────────────────────────────────────────────────────

        public Task<WorkflowApprovalSummaryResponse> GetApprovalSummaryAsync(string issuerId, string actorId)
        {
            var authResult = RequireRole<WorkflowApprovalSummaryResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var issuerItems   = _workflowItems.Values.Where(i => i.IssuerId == issuerId).ToList();
            var activeMembers = _members.Values.Count(m => m.IssuerId == issuerId && m.IsActive);

            var recentPending = issuerItems
                .Where(i => i.State == WorkflowApprovalState.PendingReview)
                .OrderByDescending(i => i.UpdatedAt)
                .Take(5)
                .ToList();

            var summary = new WorkflowApprovalSummary
            {
                IssuerId              = issuerId,
                PendingReviewCount    = issuerItems.Count(i => i.State == WorkflowApprovalState.PendingReview),
                ApprovedCount         = issuerItems.Count(i => i.State == WorkflowApprovalState.Approved),
                RejectedCount         = issuerItems.Count(i => i.State == WorkflowApprovalState.Rejected),
                NeedsChangesCount     = issuerItems.Count(i => i.State == WorkflowApprovalState.NeedsChanges),
                CompletedCount        = issuerItems.Count(i => i.State == WorkflowApprovalState.Completed),
                ActiveTeamMemberCount = activeMembers,
                RecentPendingItems    = recentPending
            };

            return Task.FromResult(new WorkflowApprovalSummaryResponse { Success = true, Summary = summary });
        }

        public Task<WorkflowItemListResponse> GetAssignedQueueAsync(string issuerId, string assigneeActorId, string actorId)
        {
            var authResult = RequireRole<WorkflowItemListResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return Task.FromResult(authResult);

            var items = _workflowItems.Values
                .Where(i => i.IssuerId == issuerId && i.AssignedTo == assigneeActorId)
                .OrderByDescending(i => i.UpdatedAt)
                .ToList();

            return Task.FromResult(new WorkflowItemListResponse { Success = true, Items = items, TotalCount = items.Count });
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the active membership record for <paramref name="actorId"/> within the issuer,
        /// or <c>null</c> if the actor is not an active member.
        /// </summary>
        private IssuerTeamMember? GetActorMembership(string issuerId, string actorId) =>
            _members.Values.FirstOrDefault(m => m.IssuerId == issuerId && m.UserId == actorId && m.IsActive);

        /// <summary>
        /// Verifies that <paramref name="actorId"/> is an active member of <paramref name="issuerId"/>
        /// with one of the <paramref name="allowedRoles"/>.
        /// Returns a pre-filled failure response when the check fails; <c>null</c> on success.
        /// </summary>
        private T? RequireRole<T>(string issuerId, string actorId, IssuerTeamRole[] allowedRoles) where T : new()
        {
            var membership = GetActorMembership(issuerId, actorId);
            if (membership == null)
                return Fail<T>(ErrorCodes.UNAUTHORIZED,
                    "You are not an active member of this issuer team. " +
                    "Contact your issuer Admin to be added before performing this operation.");

            if (!allowedRoles.Contains(membership.Role))
                return Fail<T>("INSUFFICIENT_ROLE",
                    $"Your current role ({membership.Role}) does not permit this operation. " +
                    $"Required roles: [{string.Join(", ", allowedRoles)}].");

            return default;
        }

        private (WorkflowItem? item, WorkflowItemResponse? error) GetItemForTransition(
            string issuerId, string workflowId,
            WorkflowApprovalState expectedCurrentState, WorkflowApprovalState targetState)
        {
            if (!_workflowItems.TryGetValue(workflowId, out var item))
                return (null, Fail<WorkflowItemResponse>(ErrorCodes.NOT_FOUND, "Workflow item not found."));
            if (item.IssuerId != issuerId)
                return (null, Fail<WorkflowItemResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: workflow item does not belong to the specified issuer."));

            var validation = ValidateTransition(item.State, targetState);
            if (!validation.IsValid)
                return (null, Fail<WorkflowItemResponse>("INVALID_STATE_TRANSITION", validation.Reason ?? "Transition not permitted."));

            return (item, null);
        }

        private static void ApplyTransition(WorkflowItem item, WorkflowApprovalState toState, string actorId, string? note, string? correlationId)
        {
            var fromState  = item.State;
            item.State     = toState;
            item.UpdatedAt = DateTime.UtcNow;
            item.AuditHistory.Add(CreateAuditEntry(item.WorkflowId, fromState, toState, actorId, note, correlationId));
        }

        private static WorkflowAuditEntry CreateAuditEntry(
            string workflowId, WorkflowApprovalState from, WorkflowApprovalState to,
            string actorId, string? note, string? correlationId) =>
            new WorkflowAuditEntry { WorkflowId = workflowId, FromState = from, ToState = to, ActorId = actorId, Note = note, CorrelationId = correlationId };

        private static T Fail<T>(string errorCode, string errorMessage) where T : new()
        {
            if (typeof(T) == typeof(IssuerTeamMemberResponse))
                return (T)(object)new IssuerTeamMemberResponse { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
            if (typeof(T) == typeof(IssuerTeamMembersResponse))
                return (T)(object)new IssuerTeamMembersResponse { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
            if (typeof(T) == typeof(WorkflowItemResponse))
                return (T)(object)new WorkflowItemResponse { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
            if (typeof(T) == typeof(WorkflowItemListResponse))
                return (T)(object)new WorkflowItemListResponse { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
            if (typeof(T) == typeof(WorkflowApprovalSummaryResponse))
                return (T)(object)new WorkflowApprovalSummaryResponse { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

            throw new InvalidOperationException($"Unsupported response type: {typeof(T).Name}");
        }
    }
}

using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Repository-backed implementation of the issuer workflow service providing team role
    /// management and approval-state machine capabilities for enterprise collaboration workflows.
    ///
    /// All mutable state is delegated to <see cref="IIssuerWorkflowRepository"/> so that
    /// review records survive DI scope changes and can be upgraded to durable storage
    /// by replacing the repository implementation.
    /// </summary>
    public class IssuerWorkflowService : IIssuerWorkflowService
    {
        private readonly ILogger<IssuerWorkflowService> _logger;
        private readonly IIssuerWorkflowRepository _repository;

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

        public IssuerWorkflowService(ILogger<IssuerWorkflowService> logger, IIssuerWorkflowRepository repository)
        {
            _logger     = logger;
            _repository = repository;
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

        public async Task<IssuerTeamMemberResponse> AddMemberAsync(string issuerId, AddIssuerTeamMemberRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return Fail<IssuerTeamMemberResponse>("MISSING_ISSUER_ID", "IssuerId is required.");
            if (request == null || string.IsNullOrWhiteSpace(request.UserId))
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "UserId is required.");

            bool hasExistingMembers = await _repository.HasActiveMembersAsync(issuerId);

            if (hasExistingMembers)
            {
                var authResult = await RequireRoleAsync<IssuerTeamMemberResponse>(issuerId, actorId, _adminOnly);
                if (authResult != null) return authResult;
            }
            else
            {
                if (request.Role != IssuerTeamRole.Admin)
                    return Fail<IssuerTeamMemberResponse>(
                        "BOOTSTRAP_ROLE_REQUIRED",
                        "The first member of an issuer team must have the Admin role so they can manage the team.");
            }

            bool duplicate = (await _repository.ListMembersAsync(issuerId))
                .Any(m => m.UserId == request.UserId && m.IsActive);
            if (duplicate)
                return Fail<IssuerTeamMemberResponse>("DUPLICATE_MEMBER", $"UserId '{request.UserId}' is already an active member of this issuer team.");

            var member = new IssuerTeamMember
            {
                IssuerId    = issuerId,
                UserId      = request.UserId,
                DisplayName = request.DisplayName,
                Role        = request.Role,
                AddedBy     = actorId,
                IsActive    = true
            };

            await _repository.UpsertMemberAsync(member);

            _logger.LogInformation(
                "IssuerTeamMember added. MemberId={MemberId} IssuerId={IssuerId} UserId={UserId} Role={Role} Actor={Actor}",
                member.MemberId,
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(request.UserId),
                member.Role,
                LoggingHelper.SanitizeLogInput(actorId));

            return new IssuerTeamMemberResponse { Success = true, Member = member };
        }

        public async Task<IssuerTeamMemberResponse> UpdateMemberAsync(string issuerId, string memberId, UpdateIssuerTeamMemberRequest request, string actorId)
        {
            var authResult = await RequireRoleAsync<IssuerTeamMemberResponse>(issuerId, actorId, _adminOnly);
            if (authResult != null) return authResult;

            var member = await _repository.GetMemberByIdAsync(issuerId, memberId);
            if (member == null)
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.NOT_FOUND, "Team member not found.");
            if (member.IssuerId != issuerId)
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: member does not belong to the specified issuer.");

            member.Role      = request.Role;
            member.UpdatedAt = DateTime.UtcNow;
            if (request.DisplayName != null)
                member.DisplayName = request.DisplayName;

            await _repository.UpsertMemberAsync(member);
            return new IssuerTeamMemberResponse { Success = true, Member = member };
        }

        public async Task<IssuerTeamMemberResponse> RemoveMemberAsync(string issuerId, string memberId, string actorId)
        {
            var authResult = await RequireRoleAsync<IssuerTeamMemberResponse>(issuerId, actorId, _adminOnly);
            if (authResult != null) return authResult;

            var member = await _repository.GetMemberByIdAsync(issuerId, memberId);
            if (member == null)
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.NOT_FOUND, "Team member not found.");
            if (member.IssuerId != issuerId)
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.UNAUTHORIZED, "Access denied: member does not belong to the specified issuer.");

            member.IsActive  = false;
            member.UpdatedAt = DateTime.UtcNow;
            await _repository.UpsertMemberAsync(member);

            _logger.LogInformation(
                "IssuerTeamMember removed. MemberId={MemberId} IssuerId={IssuerId} Actor={Actor}",
                memberId,
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId));

            return new IssuerTeamMemberResponse { Success = true, Member = member };
        }

        public async Task<IssuerTeamMemberResponse> GetMemberAsync(string issuerId, string memberId, string actorId)
        {
            var authResult = await RequireRoleAsync<IssuerTeamMemberResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return authResult;

            var member = await _repository.GetMemberByIdAsync(issuerId, memberId);
            if (member == null)
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.NOT_FOUND, "Team member not found.");
            if (member.IssuerId != issuerId)
                return Fail<IssuerTeamMemberResponse>(ErrorCodes.UNAUTHORIZED, "Access denied.");

            return new IssuerTeamMemberResponse { Success = true, Member = member };
        }

        public async Task<IssuerTeamMembersResponse> ListMembersAsync(string issuerId, string actorId)
        {
            var authResult = await RequireRoleAsync<IssuerTeamMembersResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return authResult;

            var members = (await _repository.ListMembersAsync(issuerId))
                .Where(m => m.IsActive)
                .OrderBy(m => m.AddedAt)
                .ToList();

            return new IssuerTeamMembersResponse { Success = true, Members = members, TotalCount = members.Count };
        }

        // ── Workflow Items ─────────────────────────────────────────────────────

        public async Task<WorkflowItemResponse> CreateWorkflowItemAsync(string issuerId, CreateWorkflowItemRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return Fail<WorkflowItemResponse>("MISSING_ISSUER_ID", "IssuerId is required.");
            if (request == null || string.IsNullOrWhiteSpace(request.Title))
                return Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "Title is required.");

            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return authResult;

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
            await _repository.UpsertWorkflowItemAsync(item);

            _logger.LogInformation(
                "WorkflowItem created. WorkflowId={WorkflowId} IssuerId={IssuerId} ItemType={ItemType} Actor={Actor}",
                item.WorkflowId,
                LoggingHelper.SanitizeLogInput(issuerId),
                item.ItemType,
                LoggingHelper.SanitizeLogInput(actorId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> GetWorkflowItemAsync(string issuerId, string workflowId, string actorId)
        {
            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return authResult;

            var item = await _repository.GetWorkflowItemAsync(issuerId, workflowId);
            if (item == null)
                return Fail<WorkflowItemResponse>(ErrorCodes.NOT_FOUND, "Workflow item not found.");

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemListResponse> ListWorkflowItemsAsync(string issuerId, string actorId, WorkflowApprovalState? stateFilter = null, string? assignedTo = null)
        {
            var authResult = await RequireRoleAsync<WorkflowItemListResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return authResult;

            var items = await _repository.ListWorkflowItemsAsync(issuerId, stateFilter, assignedTo);
            return new WorkflowItemListResponse { Success = true, Items = items, TotalCount = items.Count };
        }

        // ── Workflow Transitions ───────────────────────────────────────────────

        public async Task<WorkflowItemResponse> SubmitForReviewAsync(string issuerId, string workflowId, SubmitWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return authResult;

            var (item, err) = await GetItemForTransitionAsync(issuerId, workflowId, WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview);
            if (err != null) return err;

            ApplyTransition(item!, WorkflowApprovalState.PendingReview, actorId, request?.SubmissionNote, correlationId);
            await _repository.UpsertWorkflowItemAsync(item!);
            _logger.LogInformation("WorkflowItem submitted for review. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> ApproveAsync(string issuerId, string workflowId, ApproveWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _approverRoles);
            if (authResult != null) return authResult;

            var (item, err) = await GetItemForTransitionAsync(issuerId, workflowId, WorkflowApprovalState.PendingReview, WorkflowApprovalState.Approved);
            if (err != null) return err;

            item!.ApproverActorId      = actorId;
            item.LatestReviewerActorId = actorId;
            item.ApprovedAt            = DateTime.UtcNow;
            ApplyTransition(item, WorkflowApprovalState.Approved, actorId, request?.ApprovalNote, correlationId);
            await _repository.UpsertWorkflowItemAsync(item);

            _logger.LogInformation("WorkflowItem approved. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> RejectAsync(string issuerId, string workflowId, RejectWorkflowItemRequest request, string actorId, string correlationId)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RejectionReason))
                return Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "RejectionReason is required.");

            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _approverRoles);
            if (authResult != null) return authResult;

            var (item, err) = await GetItemForTransitionAsync(issuerId, workflowId, WorkflowApprovalState.PendingReview, WorkflowApprovalState.Rejected);
            if (err != null) return err;

            item!.LatestReviewerActorId   = actorId;
            item.RejectedAt               = DateTime.UtcNow;
            item.RejectionOrChangeReason  = request.RejectionReason;
            ApplyTransition(item, WorkflowApprovalState.Rejected, actorId, request.RejectionReason, correlationId);
            await _repository.UpsertWorkflowItemAsync(item);

            _logger.LogInformation("WorkflowItem rejected. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> RequestChangesAsync(string issuerId, string workflowId, RequestChangesRequest request, string actorId, string correlationId)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ChangeDescription))
                return Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "ChangeDescription is required.");

            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _approverRoles);
            if (authResult != null) return authResult;

            var (item, err) = await GetItemForTransitionAsync(issuerId, workflowId, WorkflowApprovalState.PendingReview, WorkflowApprovalState.NeedsChanges);
            if (err != null) return err;

            item!.LatestReviewerActorId  = actorId;
            item.RejectionOrChangeReason = request.ChangeDescription;
            ApplyTransition(item, WorkflowApprovalState.NeedsChanges, actorId, request.ChangeDescription, correlationId);
            await _repository.UpsertWorkflowItemAsync(item);

            _logger.LogInformation("WorkflowItem needs changes. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> ResubmitAsync(string issuerId, string workflowId, SubmitWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return authResult;

            var (item, err) = await GetItemForTransitionAsync(issuerId, workflowId, WorkflowApprovalState.NeedsChanges, WorkflowApprovalState.PendingReview);
            if (err != null) return err;

            ApplyTransition(item!, WorkflowApprovalState.PendingReview, actorId, request?.SubmissionNote, correlationId);
            await _repository.UpsertWorkflowItemAsync(item!);
            _logger.LogInformation("WorkflowItem resubmitted. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> ReassignAsync(string issuerId, string workflowId, ReassignWorkflowItemRequest request, string actorId, string correlationId)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewAssigneeId))
                return Fail<WorkflowItemResponse>(ErrorCodes.MISSING_REQUIRED_FIELD, "NewAssigneeId is required.");

            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _nonReadonlyRoles);
            if (authResult != null) return authResult;

            var item = await _repository.GetWorkflowItemAsync(issuerId, workflowId);
            if (item == null)
                return Fail<WorkflowItemResponse>(ErrorCodes.NOT_FOUND, "Workflow item not found.");
            if (item.State == WorkflowApprovalState.Completed || item.State == WorkflowApprovalState.Rejected)
                return Fail<WorkflowItemResponse>("INVALID_STATE", $"Cannot reassign a {item.State} item.");

            bool assigneeIsValidMember = await _repository.IsMemberAsync(issuerId, request.NewAssigneeId);
            if (!assigneeIsValidMember)
                return Fail<WorkflowItemResponse>("INVALID_ASSIGNEE", $"NewAssigneeId '{request.NewAssigneeId}' is not an active member of this issuer team.");

            string? previousAssignee = item.AssignedTo;
            item.AssignedTo = request.NewAssigneeId;
            item.UpdatedAt  = DateTime.UtcNow;

            item.AuditHistory.Add(CreateAuditEntry(
                workflowId, item.State, item.State, actorId,
                $"Reassigned from '{previousAssignee ?? "unassigned"}' to '{request.NewAssigneeId}'. {request.ReassignmentNote}".Trim(),
                correlationId));

            await _repository.UpsertWorkflowItemAsync(item);

            _logger.LogInformation("WorkflowItem reassigned. WorkflowId={WorkflowId} From={From} To={To} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(previousAssignee ?? "unassigned"),
                LoggingHelper.SanitizeLogInput(request.NewAssigneeId),
                LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        public async Task<WorkflowItemResponse> CompleteAsync(string issuerId, string workflowId, CompleteWorkflowItemRequest request, string actorId, string correlationId)
        {
            var authResult = await RequireRoleAsync<WorkflowItemResponse>(issuerId, actorId, _operatorRoles);
            if (authResult != null) return authResult;

            var (item, err) = await GetItemForTransitionAsync(issuerId, workflowId, WorkflowApprovalState.Approved, WorkflowApprovalState.Completed);
            if (err != null) return err;

            item!.CompletedAt = DateTime.UtcNow;
            ApplyTransition(item, WorkflowApprovalState.Completed, actorId, request?.CompletionNote, correlationId);
            await _repository.UpsertWorkflowItemAsync(item);

            _logger.LogInformation("WorkflowItem completed. WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                workflowId, LoggingHelper.SanitizeLogInput(actorId), LoggingHelper.SanitizeLogInput(correlationId));

            return new WorkflowItemResponse { Success = true, WorkflowItem = item };
        }

        // ── Queries ────────────────────────────────────────────────────────────

        public async Task<WorkflowApprovalSummaryResponse> GetApprovalSummaryAsync(string issuerId, string actorId)
        {
            var authResult = await RequireRoleAsync<WorkflowApprovalSummaryResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return authResult;

            var issuerItems   = await _repository.ListWorkflowItemsAsync(issuerId);
            var allMembers    = await _repository.ListMembersAsync(issuerId);
            int activeMembers = allMembers.Count(m => m.IsActive);

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

            return new WorkflowApprovalSummaryResponse { Success = true, Summary = summary };
        }

        public async Task<WorkflowItemListResponse> GetAssignedQueueAsync(string issuerId, string assigneeActorId, string actorId)
        {
            var authResult = await RequireRoleAsync<WorkflowItemListResponse>(issuerId, actorId, _allActiveRoles);
            if (authResult != null) return authResult;

            var items = await _repository.ListWorkflowItemsAsync(issuerId, assignedTo: assigneeActorId);
            return new WorkflowItemListResponse { Success = true, Items = items, TotalCount = items.Count };
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the active membership record for <paramref name="actorId"/> within the issuer,
        /// or <c>null</c> if the actor is not an active member.
        /// </summary>
        private async Task<IssuerTeamMember?> GetActorMembershipAsync(string issuerId, string actorId)
        {
            var members = await _repository.ListMembersAsync(issuerId);
            return members.FirstOrDefault(m => m.UserId == actorId && m.IsActive);
        }

        /// <summary>
        /// Verifies that <paramref name="actorId"/> is an active member of <paramref name="issuerId"/>
        /// with one of the <paramref name="allowedRoles"/>.
        /// Returns a pre-filled failure response when the check fails; <c>null</c> on success.
        /// </summary>
        private async Task<T?> RequireRoleAsync<T>(string issuerId, string actorId, IssuerTeamRole[] allowedRoles) where T : new()
        {
            var membership = await GetActorMembershipAsync(issuerId, actorId);
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

        private async Task<(WorkflowItem? item, WorkflowItemResponse? error)> GetItemForTransitionAsync(
            string issuerId, string workflowId,
            WorkflowApprovalState expectedCurrentState, WorkflowApprovalState targetState)
        {
            var item = await _repository.GetWorkflowItemAsync(issuerId, workflowId);
            if (item == null)
                return (null, Fail<WorkflowItemResponse>(ErrorCodes.NOT_FOUND, "Workflow item not found."));

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

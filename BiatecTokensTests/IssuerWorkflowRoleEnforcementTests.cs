using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Rigorous tests for role enforcement, state-transition integrity, and audit evidence
    /// in IssuerWorkflowService.
    ///
    /// Coverage:
    ///  - FinanceReviewer role (distinct from ComplianceReviewer)
    ///  - Admin acting as both Operator and Approver
    ///  - All five roles x all sensitive operations (authorisation matrix validation)
    ///  - Error message quality: human-readable, role-named, actionable
    ///  - Multi-item issuer scoping: summaries and queue counts are accurate
    ///  - All WorkflowItemType values accepted in creation
    ///  - Reassign with invalid (deactivated) assignee fails closed
    ///  - State metadata set correctly on each transition (approvedAt, rejectedAt, etc.)
    ///  - Correlation ID propagated through every state transition
    ///  - GetApprovalSummary counts are accurate after a mixed lifecycle
    ///  - GetAllowedTransitions exhaustive matrix
    ///  - Approval note persisted
    ///  - Rejection reason persisted
    ///  - ChangeDescription persisted
    ///  - Operator resubmit after FinanceReviewer requestChanges
    ///  - Multiple items assigned to same actor appear in queue
    ///  - Removing a reviewer deactivates their membership; subsequent actions fail-closed
    ///  - Bootstrap guard: empty issuerId rejected before auth check
    /// </summary>
    [TestFixture]
    public class IssuerWorkflowRoleEnforcementTests
    {
        // ─── helpers ────────────────────────────────────────────────────────

        private static IssuerWorkflowService CreateService() =>
            new IssuerWorkflowService(
                NullLogger<IssuerWorkflowService>.Instance,
                new IssuerWorkflowRepository(
                    NullLogger<IssuerWorkflowRepository>.Instance));

        private static async Task<(IssuerWorkflowService svc, string issuerId)> SetupAsync(
            string adminId = "admin", string operatorId = "op",
            string compReviewerId = "comp_rev", string finReviewerId = "fin_rev",
            string observerId = "observer")
        {
            var svc      = CreateService();
            var issuerId = $"enf-{Guid.NewGuid():N}";
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = adminId, Role = IssuerTeamRole.Admin }, "boot");
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = operatorId, Role = IssuerTeamRole.Operator }, adminId);
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = compReviewerId, Role = IssuerTeamRole.ComplianceReviewer }, adminId);
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = finReviewerId, Role = IssuerTeamRole.FinanceReviewer }, adminId);
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = observerId, Role = IssuerTeamRole.ReadOnlyObserver }, adminId);
            return (svc, issuerId);
        }

        // Helper: create item in PendingReview state
        private static async Task<WorkflowItem> PendingItemAsync(
            IssuerWorkflowService svc, string issuerId, string operatorId = "op")
        {
            var item = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "T" },
                operatorId)).WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, item.WorkflowId,
                new SubmitWorkflowItemRequest(), operatorId, "c");
            return (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId, operatorId)).WorkflowItem!;
        }

        // ═══════════════════════════════════════════════════════════════════
        // FinanceReviewer role — authorisation matrix
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task FinanceReviewer_CanApprove_Succeeds()
        {
            var (svc, issuerId) = await SetupAsync();
            var item  = await PendingItemAsync(svc, issuerId);

            var result = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest { ApprovalNote = "finance ok" }, "fin_rev", "c");

            Assert.That(result.Success, Is.True);
            Assert.That(result.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
        }

        [Test]
        public async Task FinanceReviewer_CanReject_Succeeds()
        {
            var (svc, issuerId) = await SetupAsync();
            var item  = await PendingItemAsync(svc, issuerId);

            var result = await svc.RejectAsync(issuerId, item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "Finance concern" }, "fin_rev", "c");

            Assert.That(result.Success, Is.True);
            Assert.That(result.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Rejected));
        }

        [Test]
        public async Task FinanceReviewer_CanRequestChanges_Succeeds()
        {
            var (svc, issuerId) = await SetupAsync();
            var item  = await PendingItemAsync(svc, issuerId);

            var result = await svc.RequestChangesAsync(issuerId, item.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "Need updated figures" }, "fin_rev", "c");

            Assert.That(result.Success, Is.True);
            Assert.That(result.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.NeedsChanges));
        }

        [Test]
        public async Task FinanceReviewer_CannotCreateWorkflow_ReturnsInsufficientRole()
        {
            var (svc, issuerId) = await SetupAsync();

            var result = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "fin workflow" },
                "fin_rev");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task FinanceReviewer_CannotComplete_ReturnsInsufficientRole()
        {
            var (svc, issuerId) = await SetupAsync();
            var item  = await PendingItemAsync(svc, issuerId);
            await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest(), "admin", "c");

            var result = await svc.CompleteAsync(issuerId, item.WorkflowId,
                new CompleteWorkflowItemRequest(), "fin_rev", "c");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Admin can act as both Operator AND Approver
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task Admin_CanPerformFullLifecycleSolo_Create_Submit_Approve_Complete()
        {
            var (svc, issuerId) = await SetupAsync();

            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.ComplianceEvidenceReview, Title = "admin all" },
                "admin")).WorkflowItem!;

            var submitted = (await svc.SubmitForReviewAsync(issuerId, created.WorkflowId,
                new SubmitWorkflowItemRequest(), "admin", "c")).WorkflowItem!;
            Assert.That(submitted.State, Is.EqualTo(WorkflowApprovalState.PendingReview));

            var approved = (await svc.ApproveAsync(issuerId, created.WorkflowId,
                new ApproveWorkflowItemRequest(), "admin", "c")).WorkflowItem!;
            Assert.That(approved.State, Is.EqualTo(WorkflowApprovalState.Approved));

            var completed = (await svc.CompleteAsync(issuerId, created.WorkflowId,
                new CompleteWorkflowItemRequest(), "admin", "c")).WorkflowItem!;
            Assert.That(completed.State, Is.EqualTo(WorkflowApprovalState.Completed));
        }

        [Test]
        public async Task Admin_CanRejectAndOperatorCannotResubmitBeforeNeedsChanges()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            // Admin rejects
            var rejected = (await svc.RejectAsync(issuerId, item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "Admin rejection" }, "admin", "c")).WorkflowItem!;
            Assert.That(rejected.State, Is.EqualTo(WorkflowApprovalState.Rejected));

            // Operator cannot resubmit a Rejected (terminal) item
            var resubmit = await svc.ResubmitAsync(issuerId, item.WorkflowId,
                new SubmitWorkflowItemRequest(), "op", "c");
            Assert.That(resubmit.Success, Is.False);
            Assert.That(resubmit.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Operator role — full set of authorisation checks
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task Operator_CanCreate_CanSubmit_CannotApprove_CannotReject_CannotRequestChanges()
        {
            var (svc, issuerId) = await SetupAsync();

            // Create — should succeed
            var createResult = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "op test" }, "op");
            Assert.That(createResult.Success, Is.True, "Operator should be able to create");

            var workflowId = createResult.WorkflowItem!.WorkflowId;

            // Submit — should succeed
            var submitResult = await svc.SubmitForReviewAsync(issuerId, workflowId,
                new SubmitWorkflowItemRequest(), "op", "c");
            Assert.That(submitResult.Success, Is.True, "Operator should be able to submit");

            // Approve — should fail
            var approveResult = await svc.ApproveAsync(issuerId, workflowId,
                new ApproveWorkflowItemRequest(), "op", "c");
            Assert.That(approveResult.Success, Is.False, "Operator must not approve");
            Assert.That(approveResult.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));

            // Reject — should fail
            var rejectResult = await svc.RejectAsync(issuerId, workflowId,
                new RejectWorkflowItemRequest { RejectionReason = "test" }, "op", "c");
            Assert.That(rejectResult.Success, Is.False, "Operator must not reject");
            Assert.That(rejectResult.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));

            // RequestChanges — should fail
            var changesResult = await svc.RequestChangesAsync(issuerId, workflowId,
                new RequestChangesRequest { ChangeDescription = "test" }, "op", "c");
            Assert.That(changesResult.Success, Is.False, "Operator must not request changes");
            Assert.That(changesResult.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Error message quality — must be human-readable and role-informative
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task InsufficientRole_ErrorMessage_ContainsActorRoleAndRequiredRoles()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            // Operator trying to approve — error should name the actor's role and required roles
            var result = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest(), "op", "c");

            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "InsufficientRole error must have a human-readable message");
            Assert.That(result.ErrorMessage!.Contains("Operator"), Is.True,
                "Error message should name the actor's current role");
            Assert.That(
                result.ErrorMessage.Contains("ComplianceReviewer") ||
                result.ErrorMessage.Contains("FinanceReviewer") ||
                result.ErrorMessage.Contains("Admin"),
                Is.True,
                "Error message should list acceptable roles");
        }

        [Test]
        public async Task Unauthorized_ErrorMessage_ContainsGuidanceToContactAdmin()
        {
            var (svc, issuerId) = await SetupAsync();

            // Non-member trying to create
            var result = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "test" },
                "stranger");

            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ErrorMessage!.ToLower().Contains("member") ||
                        result.ErrorMessage.ToLower().Contains("issuer") ||
                        result.ErrorMessage.ToLower().Contains("admin"),
                Is.True, "UNAUTHORIZED message must guide the user toward resolution");
        }

        [Test]
        public async Task InvalidStateTransition_ErrorMessage_ListsCurrentStateAndAllowedTransitions()
        {
            var (svc, issuerId) = await SetupAsync();

            // Create item and reject it (terminal)
            var item = await PendingItemAsync(svc, issuerId);
            await svc.RejectAsync(issuerId, item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "done" }, "comp_rev", "c");

            // Attempt to approve the rejected (terminal) item
            var result = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest(), "comp_rev", "c");

            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            // Message should mention the current state
            Assert.That(result.ErrorMessage!.Contains("Rejected"), Is.True,
                "Transition error should name the current state");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Transition metadata — timestamps and actor IDs set correctly
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task Approve_SetsApprovedAtAndApproverActorId()
        {
            var (svc, issuerId) = await SetupAsync();
            var before = DateTime.UtcNow;
            var item   = await PendingItemAsync(svc, issuerId);

            var result = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest { ApprovalNote = "LGTM" }, "comp_rev", "c");

            Assert.That(result.WorkflowItem!.ApprovedAt, Is.GreaterThan(before));
            Assert.That(result.WorkflowItem.ApproverActorId, Is.EqualTo("comp_rev"));
        }

        [Test]
        public async Task Reject_SetsRejectedAtAndRejectionReason()
        {
            var (svc, issuerId) = await SetupAsync();
            var item   = await PendingItemAsync(svc, issuerId);

            var result = await svc.RejectAsync(issuerId, item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "Compliance gap" }, "fin_rev", "c");

            Assert.That(result.WorkflowItem!.RejectedAt, Is.Not.Null);
            Assert.That(result.WorkflowItem.RejectionOrChangeReason, Is.EqualTo("Compliance gap"));
        }

        [Test]
        public async Task Complete_SetsCompletedAt()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);
            await svc.ApproveAsync(issuerId, item.WorkflowId, new ApproveWorkflowItemRequest(), "admin", "c");
            var before = DateTime.UtcNow;

            var result = await svc.CompleteAsync(issuerId, item.WorkflowId,
                new CompleteWorkflowItemRequest { CompletionNote = "all done" }, "op", "c");

            Assert.That(result.WorkflowItem!.CompletedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task RequestChanges_PersistsChangeDescription()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            var result = await svc.RequestChangesAsync(issuerId, item.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "Add regulatory docs" }, "comp_rev", "c");

            Assert.That(result.WorkflowItem!.RejectionOrChangeReason, Is.EqualTo("Add regulatory docs"));
        }

        [Test]
        public async Task SubmitAndApprove_CreatedByIsPreserved()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "authorship" },
                "op")).WorkflowItem!;

            await svc.SubmitForReviewAsync(issuerId, item.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");
            var approved = (await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest(), "comp_rev", "c")).WorkflowItem!;

            Assert.That(approved.CreatedBy, Is.EqualTo("op"), "CreatedBy must never be overwritten");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Correlation ID — propagated to every audit entry
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task SubmitForReview_CorrelationId_PropagatedToAuditEntry()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "corr test" },
                "op")).WorkflowItem!;

            await svc.SubmitForReviewAsync(issuerId, item.WorkflowId,
                new SubmitWorkflowItemRequest(), "op", "trace-abc-123");

            var refreshed = (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId, "op")).WorkflowItem!;
            var submitEntry = refreshed.AuditHistory
                .FirstOrDefault(e => e.ToState == WorkflowApprovalState.PendingReview);
            Assert.That(submitEntry, Is.Not.Null);
            Assert.That(submitEntry!.CorrelationId, Is.EqualTo("trace-abc-123"));
        }

        [Test]
        public async Task Reject_CorrelationId_PropagatedToAuditEntry()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            await svc.RejectAsync(issuerId, item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "fail" }, "fin_rev", "corr-rej-456");

            var refreshed = (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId, "fin_rev")).WorkflowItem!;
            var rejectEntry = refreshed.AuditHistory
                .FirstOrDefault(e => e.ToState == WorkflowApprovalState.Rejected);
            Assert.That(rejectEntry, Is.Not.Null);
            Assert.That(rejectEntry!.CorrelationId, Is.EqualTo("corr-rej-456"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // All WorkflowItemType values — creation accepted for each
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        [TestCase(WorkflowItemType.LaunchReadinessSignOff)]
        [TestCase(WorkflowItemType.WhitelistPolicyUpdate)]
        [TestCase(WorkflowItemType.ComplianceEvidenceReview)]
        [TestCase(WorkflowItemType.GeneralApproval)]
        public async Task CreateWorkflowItem_AllItemTypes_Accepted(WorkflowItemType type)
        {
            var (svc, issuerId) = await SetupAsync();

            var result = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = type, Title = $"Test {type}" }, "op");

            Assert.That(result.Success, Is.True, $"ItemType {type} should be creatable");
            Assert.That(result.WorkflowItem!.ItemType, Is.EqualTo(type));
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetAllowedTransitions — exhaustive matrix
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetAllowedTransitions_PendingReview_HasThreeChoices()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(WorkflowApprovalState.PendingReview);
            Assert.That(allowed.Count, Is.EqualTo(3));
            Assert.That(allowed.Contains(WorkflowApprovalState.Approved), Is.True);
            Assert.That(allowed.Contains(WorkflowApprovalState.Rejected), Is.True);
            Assert.That(allowed.Contains(WorkflowApprovalState.NeedsChanges), Is.True);
        }

        [Test]
        public void GetAllowedTransitions_Approved_HasOneChoice()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(WorkflowApprovalState.Approved);
            Assert.That(allowed.Count, Is.EqualTo(1));
            Assert.That(allowed.Contains(WorkflowApprovalState.Completed), Is.True);
        }

        [Test]
        public void GetAllowedTransitions_NeedsChanges_HasOneChoice()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(WorkflowApprovalState.NeedsChanges);
            Assert.That(allowed.Count, Is.EqualTo(1));
            Assert.That(allowed.Contains(WorkflowApprovalState.PendingReview), Is.True);
        }

        [Test]
        public void GetAllowedTransitions_TerminalStates_AreEmpty()
        {
            var svc = CreateService();
            Assert.That(svc.GetAllowedTransitions(WorkflowApprovalState.Rejected).Count, Is.EqualTo(0));
            Assert.That(svc.GetAllowedTransitions(WorkflowApprovalState.Completed).Count, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Approval summary counts — accurate after mixed lifecycle
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetApprovalSummary_Counts_ReflectActualStates()
        {
            var (svc, issuerId) = await SetupAsync();

            // Create 5 items, put them in various states
            var i1 = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "1" }, "op"))
                .WorkflowItem!;
            var i2 = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "2" }, "op"))
                .WorkflowItem!;
            var i3 = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "3" }, "op"))
                .WorkflowItem!;
            var i4 = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "4" }, "op"))
                .WorkflowItem!;

            // Submit all
            foreach (var id in new[] { i1.WorkflowId, i2.WorkflowId, i3.WorkflowId, i4.WorkflowId })
                await svc.SubmitForReviewAsync(issuerId, id, new SubmitWorkflowItemRequest(), "op", "c");

            // i1 → Approved
            await svc.ApproveAsync(issuerId, i1.WorkflowId, new ApproveWorkflowItemRequest(), "comp_rev", "c");
            // i2 → Rejected
            await svc.RejectAsync(issuerId, i2.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "r" }, "fin_rev", "c");
            // i3 → NeedsChanges
            await svc.RequestChangesAsync(issuerId, i3.WorkflowId, new RequestChangesRequest { ChangeDescription = "c" }, "admin", "c");
            // i1 → Completed
            await svc.CompleteAsync(issuerId, i1.WorkflowId, new CompleteWorkflowItemRequest(), "op", "c");
            // i4 stays PendingReview

            var summary = (await svc.GetApprovalSummaryAsync(issuerId, "op")).Summary!;
            Assert.That(summary.PendingReviewCount, Is.EqualTo(1), "Only i4 is PendingReview");
            Assert.That(summary.ApprovedCount, Is.EqualTo(0), "i1 moved to Completed");
            Assert.That(summary.RejectedCount, Is.EqualTo(1), "i2 is Rejected");
            Assert.That(summary.NeedsChangesCount, Is.EqualTo(1), "i3 is NeedsChanges");
            Assert.That(summary.CompletedCount, Is.EqualTo(1), "i1 is Completed");
        }

        [Test]
        public async Task GetApprovalSummary_ActiveTeamMemberCount_CorrectAfterRemoval()
        {
            var (svc, issuerId) = await SetupAsync();

            // Get all members to find fin_rev's memberId
            var members = (await svc.ListMembersAsync(issuerId, "admin")).Members;
            var finRevMember = members.First(m => m.UserId == "fin_rev");

            // Remove fin_rev
            await svc.RemoveMemberAsync(issuerId, finRevMember.MemberId, "admin");

            var summary = (await svc.GetApprovalSummaryAsync(issuerId, "admin")).Summary!;
            // Started with 5 members (admin, op, comp_rev, fin_rev, observer); removed fin_rev → 4
            Assert.That(summary.ActiveTeamMemberCount, Is.EqualTo(4));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Reassign — with deactivated assignee fails closed
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task Reassign_ToDeactivatedMember_FailsClosed()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            // Remove comp_rev (deactivate them)
            var members = (await svc.ListMembersAsync(issuerId, "admin")).Members;
            var compRevMember = members.First(m => m.UserId == "comp_rev");
            await svc.RemoveMemberAsync(issuerId, compRevMember.MemberId, "admin");

            // Attempt to reassign to the now-deactivated comp_rev
            var result = await svc.ReassignAsync(issuerId, item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "comp_rev" }, "admin", "c");

            Assert.That(result.Success, Is.False,
                "Reassign to a deactivated member must fail closed");
        }

        [Test]
        public async Task Reassign_ToActiveMember_Succeeds_UpdatesAssignedTo()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            var result = await svc.ReassignAsync(issuerId, item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "fin_rev", ReassignmentNote = "hand off" },
                "admin", "c");

            Assert.That(result.Success, Is.True);
            Assert.That(result.WorkflowItem!.AssignedTo, Is.EqualTo("fin_rev"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Queue — multiple items for same assignee
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetAssignedQueue_MultipleItems_ReturnsAllAssignedToActor()
        {
            var (svc, issuerId) = await SetupAsync();

            // Create two items assigned to fin_rev
            for (int i = 0; i < 2; i++)
            {
                var item = (await svc.CreateWorkflowItemAsync(issuerId,
                    new CreateWorkflowItemRequest
                    {
                        ItemType   = WorkflowItemType.GeneralApproval,
                        Title      = $"Queue item {i}",
                        AssignedTo = "fin_rev"
                    }, "op")).WorkflowItem!;
                await svc.SubmitForReviewAsync(issuerId, item.WorkflowId,
                    new SubmitWorkflowItemRequest(), "op", "c");
            }

            var queue = await svc.GetAssignedQueueAsync(issuerId, "fin_rev", "fin_rev");
            Assert.That(queue.Success, Is.True);
            Assert.That(queue.Items.Count, Is.EqualTo(2));
            Assert.That(queue.Items.All(i => i.AssignedTo == "fin_rev"), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Removed-member access — all protected operations fail
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task RemovedMember_CannotCreateWorkflow_ReturnsUnauthorized()
        {
            var (svc, issuerId) = await SetupAsync();

            // Remove the operator
            var members = (await svc.ListMembersAsync(issuerId, "admin")).Members;
            var opMember = members.First(m => m.UserId == "op");
            await svc.RemoveMemberAsync(issuerId, opMember.MemberId, "admin");

            // Removed operator tries to create
            var result = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "after removal" },
                "op");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNAUTHORIZED"),
                "Removed member must be treated as non-member for all operations");
        }

        [Test]
        public async Task RemovedReviewer_CannotApprove_ReturnsUnauthorized()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            // Remove comp_rev
            var members = (await svc.ListMembersAsync(issuerId, "admin")).Members;
            var compRev = members.First(m => m.UserId == "comp_rev");
            await svc.RemoveMemberAsync(issuerId, compRev.MemberId, "admin");

            var result = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest(), "comp_rev", "c");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Resubmit cycle — full NeedsChanges loop
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task FullCycle_OperatorResubmitsAfterFinanceReviewerRequestedChanges()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            // FinanceReviewer requests changes
            await svc.RequestChangesAsync(issuerId, item.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "Need financial forecast" }, "fin_rev", "c");

            // Operator resubmits
            var resubmit = await svc.ResubmitAsync(issuerId, item.WorkflowId,
                new SubmitWorkflowItemRequest { SubmissionNote = "Updated with forecast" }, "op", "c2");
            Assert.That(resubmit.Success, Is.True);
            Assert.That(resubmit.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.PendingReview));

            // Now ComplianceReviewer approves
            var approve = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest { ApprovalNote = "Now complete" }, "comp_rev", "c3");
            Assert.That(approve.Success, Is.True);
            Assert.That(approve.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Empty issuerId validation
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task AddMember_EmptyIssuerId_ReturnsMissingIssuerIdError()
        {
            var svc = CreateService();

            var result = await svc.AddMemberAsync("",
                new AddIssuerTeamMemberRequest { UserId = "u1", Role = IssuerTeamRole.Admin }, "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ISSUER_ID"));
        }

        [Test]
        public async Task CreateWorkflowItem_EmptyIssuerId_ReturnsMissingIssuerIdError()
        {
            var svc = CreateService();

            var result = await svc.CreateWorkflowItemAsync("",
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "t" }, "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ISSUER_ID"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Listing / filtering by state
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task ListWorkflowItems_StateFilter_ReturnsonlyMatchingItems()
        {
            var (svc, issuerId) = await SetupAsync();

            // Create two: one Prepared, one PendingReview
            var i1 = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "a" }, "op"))
                .WorkflowItem!;
            var i2 = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "b" }, "op"))
                .WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, i2.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");

            var pending = await svc.ListWorkflowItemsAsync(issuerId, "op",
                stateFilter: WorkflowApprovalState.PendingReview);
            var prepared = await svc.ListWorkflowItemsAsync(issuerId, "op",
                stateFilter: WorkflowApprovalState.Prepared);

            Assert.That(pending.Items.All(x => x.State == WorkflowApprovalState.PendingReview), Is.True);
            Assert.That(prepared.Items.All(x => x.State == WorkflowApprovalState.Prepared), Is.True);
            Assert.That(pending.Items.Any(x => x.WorkflowId == i2.WorkflowId), Is.True);
            Assert.That(prepared.Items.Any(x => x.WorkflowId == i1.WorkflowId), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Permissions snapshot for FinanceReviewer
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetActorPermissions_FinanceReviewer_CanApprove_CannotCreate_CannotManageMembers()
        {
            var (svc, issuerId) = await SetupAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "fin_rev");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Permissions!.Role, Is.EqualTo(IssuerTeamRole.FinanceReviewer));
            var actionMap = result.Permissions.PermittedActions.ToDictionary(a => a.ActionKey);

            Assert.That(actionMap["APPROVE"].IsAllowed, Is.True, "FinanceReviewer can approve");
            Assert.That(actionMap["REJECT"].IsAllowed, Is.True, "FinanceReviewer can reject");
            Assert.That(actionMap["REQUEST_CHANGES"].IsAllowed, Is.True, "FinanceReviewer can request changes");
            Assert.That(actionMap["CREATE_WORKFLOW_ITEM"].IsAllowed, Is.False, "FinanceReviewer cannot create");
            Assert.That(actionMap["MANAGE_MEMBERS"].IsAllowed, Is.False, "FinanceReviewer cannot manage members");
            Assert.That(actionMap["COMPLETE"].IsAllowed, Is.False, "FinanceReviewer cannot complete");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Audit trail — note/description content persisted in entry
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task ApproveWithNote_NoteAppearsInAuditEntry()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest { ApprovalNote = "Regulatory sign-off complete" }, "comp_rev", "c");

            var hist = await svc.GetAuditHistoryAsync(issuerId, item.WorkflowId, "comp_rev");
            var approveEntry = hist.AuditHistory.First(e => e.ToState == WorkflowApprovalState.Approved);
            Assert.That(approveEntry.Note, Is.EqualTo("Regulatory sign-off complete"));
        }

        [Test]
        public async Task RejectWithReason_ReasonAppearsInAuditEntry()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            await svc.RejectAsync(issuerId, item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "Jurisdiction not covered" }, "fin_rev", "c");

            var hist = await svc.GetAuditHistoryAsync(issuerId, item.WorkflowId, "fin_rev");
            var rejectEntry = hist.AuditHistory.First(e => e.ToState == WorkflowApprovalState.Rejected);
            Assert.That(rejectEntry.Note, Is.EqualTo("Jurisdiction not covered"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Workflow item metadata — ExternalReference and Metadata persist
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateWorkflowItem_ExternalReferenceAndMetadata_Persisted()
        {
            var (svc, issuerId) = await SetupAsync();

            var result = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest
                {
                    ItemType          = WorkflowItemType.WhitelistPolicyUpdate,
                    Title             = "whitelist update",
                    ExternalReference = "policy-123",
                    Metadata          = new Dictionary<string, string> { ["env"] = "prod" }
                }, "op");

            Assert.That(result.WorkflowItem!.ExternalReference, Is.EqualTo("policy-123"));
            Assert.That(result.WorkflowItem.Metadata.ContainsKey("env"), Is.True);
            Assert.That(result.WorkflowItem.Metadata["env"], Is.EqualTo("prod"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Observer can read — regression: read-only access is preserved
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task Observer_CanReadAllReadOps_CannotWriteAnyOp()
        {
            var (svc, issuerId) = await SetupAsync();
            var item = await PendingItemAsync(svc, issuerId);

            // Read operations — all should succeed
            var getMember = await svc.GetMemberAsync(issuerId,
                (await svc.ListMembersAsync(issuerId, "admin")).Members.First().MemberId, "observer");
            Assert.That(getMember.Success, Is.True, "Observer can GetMember");

            var listItems = await svc.ListWorkflowItemsAsync(issuerId, "observer");
            Assert.That(listItems.Success, Is.True, "Observer can ListWorkflowItems");

            var getItem = await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId, "observer");
            Assert.That(getItem.Success, Is.True, "Observer can GetWorkflowItem");

            var summary = await svc.GetApprovalSummaryAsync(issuerId, "observer");
            Assert.That(summary.Success, Is.True, "Observer can GetApprovalSummary");

            var perms = await svc.GetActorPermissionsAsync(issuerId, "observer");
            Assert.That(perms.Success, Is.True, "Observer can GetActorPermissions");

            var hist = await svc.GetAuditHistoryAsync(issuerId, item.WorkflowId, "observer");
            Assert.That(hist.Success, Is.True, "Observer can GetAuditHistory");

            // Write operations — all should fail
            var createResult = await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "blocked" },
                "observer");
            Assert.That(createResult.Success, Is.False, "Observer cannot create");

            var approveResult = await svc.ApproveAsync(issuerId, item.WorkflowId,
                new ApproveWorkflowItemRequest(), "observer", "c");
            Assert.That(approveResult.Success, Is.False, "Observer cannot approve");

            var reassignResult = await svc.ReassignAsync(issuerId, item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "admin" }, "observer", "c");
            Assert.That(reassignResult.Success, Is.False, "Observer cannot reassign");
        }
    }
}

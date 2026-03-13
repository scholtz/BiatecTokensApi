using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for the IssuerWorkflowService and IssuerWorkflowController.
    /// Covers:
    ///  - State machine transition rules (valid / invalid)
    ///  - Team membership CRUD with tenant isolation
    ///  - Workflow item lifecycle: create → submit → approve/reject/request-changes → complete
    ///  - Reassignment with active-member validation
    ///  - Tenant isolation: one issuer cannot read/mutate another issuer's records
    ///  - Unauthorized access behaviour (unauthenticated HTTP calls)
    ///  - Dashboard summary and assigned-queue queries
    ///  - Serialization shape (HTTP integration tests)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class IssuerWorkflowTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — IssuerWorkflowService directly
        // ═══════════════════════════════════════════════════════════════════════

        private static IssuerWorkflowService CreateService() =>
            new IssuerWorkflowService(NullLogger<IssuerWorkflowService>.Instance);

        // ── Transition validation ──────────────────────────────────────────────

        [Test]
        public void ValidateTransition_PreparedToPendingReview_IsValid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingReviewToApproved_IsValid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.PendingReview, WorkflowApprovalState.Approved);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingReviewToRejected_IsValid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.PendingReview, WorkflowApprovalState.Rejected);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingReviewToNeedsChanges_IsValid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.PendingReview, WorkflowApprovalState.NeedsChanges);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_ApprovedToCompleted_IsValid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.Approved, WorkflowApprovalState.Completed);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_NeedsChangesToPendingReview_IsValid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.NeedsChanges, WorkflowApprovalState.PendingReview);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_RejectedToAnyState_IsInvalid()
        {
            var svc = CreateService();
            foreach (WorkflowApprovalState next in Enum.GetValues<WorkflowApprovalState>())
            {
                var result = svc.ValidateTransition(WorkflowApprovalState.Rejected, next);
                Assert.That(result.IsValid, Is.False, $"Expected Rejected→{next} to be invalid (terminal state)");
            }
        }

        [Test]
        public void ValidateTransition_CompletedToAnyState_IsInvalid()
        {
            var svc = CreateService();
            foreach (WorkflowApprovalState next in Enum.GetValues<WorkflowApprovalState>())
            {
                var result = svc.ValidateTransition(WorkflowApprovalState.Completed, next);
                Assert.That(result.IsValid, Is.False, $"Expected Completed→{next} to be invalid (terminal state)");
            }
        }

        [Test]
        public void ValidateTransition_PreparedToApproved_IsInvalid()
        {
            var svc    = CreateService();
            var result = svc.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.Approved);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetAllowedTransitions_Prepared_ReturnsPendingReviewOnly()
        {
            var svc     = CreateService();
            var allowed = svc.GetAllowedTransitions(WorkflowApprovalState.Prepared);
            Assert.That(allowed, Is.EquivalentTo(new[] { WorkflowApprovalState.PendingReview }));
        }

        [Test]
        public void GetAllowedTransitions_Rejected_ReturnsEmpty()
        {
            var svc     = CreateService();
            var allowed = svc.GetAllowedTransitions(WorkflowApprovalState.Rejected);
            Assert.That(allowed, Is.Empty);
        }

        // ── Team membership ────────────────────────────────────────────────────

        [Test]
        public async Task AddMember_ValidRequest_ReturnsMember()
        {
            var svc = CreateService();
            var req = new AddIssuerTeamMemberRequest { UserId = "user1@test.com", Role = IssuerTeamRole.ComplianceReviewer };
            var res = await svc.AddMemberAsync("issuer-A", req, "admin1");

            Assert.That(res.Success, Is.True);
            Assert.That(res.Member, Is.Not.Null);
            Assert.That(res.Member!.UserId, Is.EqualTo("user1@test.com"));
            Assert.That(res.Member.Role, Is.EqualTo(IssuerTeamRole.ComplianceReviewer));
            Assert.That(res.Member.IssuerId, Is.EqualTo("issuer-A"));
        }

        [Test]
        public async Task AddMember_MissingUserId_ReturnsBadRequest()
        {
            var svc = CreateService();
            var req = new AddIssuerTeamMemberRequest { UserId = "", Role = IssuerTeamRole.Operator };
            var res = await svc.AddMemberAsync("issuer-A", req, "admin1");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AddMember_Duplicate_ReturnsError()
        {
            var svc = CreateService();
            var req = new AddIssuerTeamMemberRequest { UserId = "dup@test.com", Role = IssuerTeamRole.Operator };
            await svc.AddMemberAsync("issuer-A", req, "admin1");
            var res2 = await svc.AddMemberAsync("issuer-A", req, "admin1");

            Assert.That(res2.Success, Is.False);
            Assert.That(res2.ErrorCode, Is.EqualTo("DUPLICATE_MEMBER"));
        }

        [Test]
        public async Task ListMembers_OnlyReturnsActiveMembersForSameIssuer()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuerX", new AddIssuerTeamMemberRequest { UserId = "u1", Role = IssuerTeamRole.Operator }, "admin");
            await svc.AddMemberAsync("issuerY", new AddIssuerTeamMemberRequest { UserId = "u2", Role = IssuerTeamRole.Admin }, "admin");

            var listX = await svc.ListMembersAsync("issuerX");
            var listY = await svc.ListMembersAsync("issuerY");

            Assert.That(listX.Members.Select(m => m.UserId), Does.Contain("u1"));
            Assert.That(listX.Members.Select(m => m.UserId), Does.Not.Contain("u2"), "Tenant isolation violated");
            Assert.That(listY.Members.Select(m => m.UserId), Does.Contain("u2"));
        }

        [Test]
        public async Task RemoveMember_SoftDelete_MemberNoLongerInActiveList()
        {
            var svc = CreateService();
            var addRes = await svc.AddMemberAsync("issuerR", new AddIssuerTeamMemberRequest { UserId = "remove-me", Role = IssuerTeamRole.Operator }, "admin");
            var memberId = addRes.Member!.MemberId;

            var removeRes = await svc.RemoveMemberAsync("issuerR", memberId, "admin");
            Assert.That(removeRes.Success, Is.True);
            Assert.That(removeRes.Member!.IsActive, Is.False);

            var list = await svc.ListMembersAsync("issuerR");
            Assert.That(list.Members.Any(m => m.MemberId == memberId), Is.False);
        }

        [Test]
        public async Task RemoveMember_WrongIssuer_ReturnsUnauthorized()
        {
            var svc = CreateService();
            var addRes = await svc.AddMemberAsync("issuer1", new AddIssuerTeamMemberRequest { UserId = "x", Role = IssuerTeamRole.Operator }, "admin");
            var memberId = addRes.Member!.MemberId;

            var res = await svc.RemoveMemberAsync("issuer2", memberId, "admin");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task UpdateMember_RoleChange_Persists()
        {
            var svc    = CreateService();
            var addRes = await svc.AddMemberAsync("issuerU", new AddIssuerTeamMemberRequest { UserId = "upd@test.com", Role = IssuerTeamRole.Operator }, "admin");
            var memberId = addRes.Member!.MemberId;

            var updRes = await svc.UpdateMemberAsync("issuerU", memberId,
                new UpdateIssuerTeamMemberRequest { Role = IssuerTeamRole.Admin }, "admin");

            Assert.That(updRes.Success, Is.True);
            Assert.That(updRes.Member!.Role, Is.EqualTo(IssuerTeamRole.Admin));
        }

        // ── Workflow item lifecycle ────────────────────────────────────────────

        [Test]
        public async Task CreateWorkflowItem_ValidRequest_ReturnsItemInPreparedState()
        {
            var svc = CreateService();
            var req = new CreateWorkflowItemRequest
            {
                ItemType    = WorkflowItemType.LaunchReadinessSignOff,
                Title       = "Launch sign-off for ACME token"
            };
            var res = await svc.CreateWorkflowItemAsync("issuer1", req, "op1");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem, Is.Not.Null);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Prepared));
            Assert.That(res.WorkflowItem.CreatedBy, Is.EqualTo("op1"));
            Assert.That(res.WorkflowItem.AuditHistory, Has.Count.EqualTo(1), "Creation should add one audit entry");
        }

        [Test]
        public async Task CreateWorkflowItem_MissingTitle_ReturnsError()
        {
            var svc = CreateService();
            var req = new CreateWorkflowItemRequest { ItemType = WorkflowItemType.WhitelistPolicyUpdate, Title = "" };
            var res = await svc.CreateWorkflowItemAsync("issuer1", req, "op1");
            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task SubmitForReview_TransitionsToCorrectState()
        {
            var svc  = CreateService();
            var item = (await svc.CreateWorkflowItemAsync("i1", NewReq(), "op1")).WorkflowItem!;

            var res  = await svc.SubmitForReviewAsync("i1", item.WorkflowId, new SubmitWorkflowItemRequest { SubmissionNote = "Ready for review" }, "op1", "corr1");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.PendingReview));
            Assert.That(res.WorkflowItem.AuditHistory, Has.Count.GreaterThan(1));
        }

        [Test]
        public async Task Approve_TransitionsToApprovedState_WithMetadata()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i1");

            var res  = await svc.ApproveAsync("i1", item.WorkflowId, new ApproveWorkflowItemRequest { ApprovalNote = "Looks good" }, "reviewer1", "corr2");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
            Assert.That(res.WorkflowItem.ApproverActorId, Is.EqualTo("reviewer1"));
            Assert.That(res.WorkflowItem.ApprovedAt, Is.Not.Null);
        }

        [Test]
        public async Task Reject_TransitionsToRejectedState_WithReason()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i2");

            var res  = await svc.RejectAsync("i2", item.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "Policy not met" }, "reviewer2", "corr3");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Rejected));
            Assert.That(res.WorkflowItem.RejectionOrChangeReason, Is.EqualTo("Policy not met"));
            Assert.That(res.WorkflowItem.RejectedAt, Is.Not.Null);
        }

        [Test]
        public async Task Reject_MissingReason_ReturnsError()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i3");

            var res = await svc.RejectAsync("i3", item.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "" }, "reviewer3", "corr4");
            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task RequestChanges_TransitionsToNeedsChanges()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i4");

            var res  = await svc.RequestChangesAsync("i4", item.WorkflowId, new RequestChangesRequest { ChangeDescription = "Please add evidence" }, "reviewer4", "corr5");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.NeedsChanges));
            Assert.That(res.WorkflowItem.LatestReviewerActorId, Is.EqualTo("reviewer4"));
        }

        [Test]
        public async Task Resubmit_NeedsChangesToPendingReview()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i5");
            await svc.RequestChangesAsync("i5", item.WorkflowId, new RequestChangesRequest { ChangeDescription = "Add X" }, "rev", "c1");

            var res  = await svc.ResubmitAsync("i5", item.WorkflowId, new SubmitWorkflowItemRequest(), "op1", "c2");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.PendingReview));
        }

        [Test]
        public async Task Complete_ApprovedToCompleted()
        {
            var svc  = CreateService();
            var item = await CreateApprovedItemAsync(svc, "i6");

            var res  = await svc.CompleteAsync("i6", item.WorkflowId, new CompleteWorkflowItemRequest { CompletionNote = "Done" }, "op1", "c3");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Completed));
            Assert.That(res.WorkflowItem.CompletedAt, Is.Not.Null);
        }

        // ── Invalid state transitions (fail-closed) ───────────────────────────

        [Test]
        public async Task Approve_AlreadyApprovedItem_ReturnsError()
        {
            var svc  = CreateService();
            var item = await CreateApprovedItemAsync(svc, "i7");

            var res = await svc.ApproveAsync("i7", item.WorkflowId, new ApproveWorkflowItemRequest(), "rev", "c");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task Approve_CompletedItem_ReturnsError()
        {
            var svc  = CreateService();
            var item = await CreateCompletedItemAsync(svc, "i8");

            var res = await svc.ApproveAsync("i8", item.WorkflowId, new ApproveWorkflowItemRequest(), "rev", "c");
            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task Reject_AlreadyRejectedItem_ReturnsError()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i9");
            await svc.RejectAsync("i9", item.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "X" }, "r", "c");

            var res = await svc.RejectAsync("i9", item.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "Y" }, "r", "c");
            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task SubmitForReview_AlreadyPendingItem_ReturnsError()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "i10");

            var res = await svc.SubmitForReviewAsync("i10", item.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");
            Assert.That(res.Success, Is.False);
        }

        // ── Tenant isolation ──────────────────────────────────────────────────

        [Test]
        public async Task GetWorkflowItem_WrongIssuer_ReturnsUnauthorized()
        {
            var svc  = CreateService();
            var item = (await svc.CreateWorkflowItemAsync("issuer-OWNER", NewReq(), "op")).WorkflowItem!;

            var res = await svc.GetWorkflowItemAsync("issuer-ATTACKER", item.WorkflowId);
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task ListWorkflowItems_OnlyReturnsItemsForSameIssuer()
        {
            var svc = CreateService();
            await svc.CreateWorkflowItemAsync("tenantA", NewReq("Item A"), "op");
            await svc.CreateWorkflowItemAsync("tenantB", NewReq("Item B"), "op");

            var listA = await svc.ListWorkflowItemsAsync("tenantA");
            var listB = await svc.ListWorkflowItemsAsync("tenantB");

            Assert.That(listA.Items.All(i => i.IssuerId == "tenantA"), Is.True, "Tenant A must only see its own items");
            Assert.That(listB.Items.All(i => i.IssuerId == "tenantB"), Is.True, "Tenant B must only see its own items");
            Assert.That(listA.Items.Any(i => i.IssuerId == "tenantB"), Is.False, "Tenant isolation violated: tenantA sees tenantB items");
        }

        [Test]
        public async Task ApproveWorkflowItem_WrongIssuer_ReturnsUnauthorized()
        {
            var svc  = CreateService();
            var item = await CreatePendingReviewItemAsync(svc, "issuerOwner");

            var res = await svc.ApproveAsync("issuerAttacker", item.WorkflowId, new ApproveWorkflowItemRequest(), "attacker", "c");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        // ── Reassignment ──────────────────────────────────────────────────────

        [Test]
        public async Task Reassign_ToActiveMember_Succeeds()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuerR", new AddIssuerTeamMemberRequest { UserId = "newReviewer", Role = IssuerTeamRole.ComplianceReviewer }, "admin");
            var item = (await svc.CreateWorkflowItemAsync("issuerR", NewReq(), "op")).WorkflowItem!;

            var res = await svc.ReassignAsync("issuerR", item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "newReviewer", ReassignmentNote = "Escalated" }, "op", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.AssignedTo, Is.EqualTo("newReviewer"));
        }

        [Test]
        public async Task Reassign_ToNonMember_ReturnsError()
        {
            var svc  = CreateService();
            var item = (await svc.CreateWorkflowItemAsync("issuerR2", NewReq(), "op")).WorkflowItem!;

            var res  = await svc.ReassignAsync("issuerR2", item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "unknownUser" }, "op", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INVALID_ASSIGNEE"));
        }

        [Test]
        public async Task Reassign_CompletedItem_ReturnsError()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuerRC", new AddIssuerTeamMemberRequest { UserId = "member1", Role = IssuerTeamRole.ComplianceReviewer }, "admin");
            var item = await CreateCompletedItemAsync(svc, "issuerRC");

            var res = await svc.ReassignAsync("issuerRC", item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "member1" }, "op", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INVALID_STATE"));
        }

        // ── Ownership metadata consistency ────────────────────────────────────

        [Test]
        public async Task AuditHistory_IsPopulatedAfterFullLifecycle()
        {
            var svc  = CreateService();
            var item = (await svc.CreateWorkflowItemAsync("issuerAH", NewReq(), "op1")).WorkflowItem!;
            await svc.SubmitForReviewAsync("issuerAH", item.WorkflowId, new SubmitWorkflowItemRequest(), "op1", "c1");
            await svc.RequestChangesAsync("issuerAH", item.WorkflowId, new RequestChangesRequest { ChangeDescription = "Add doc" }, "rev1", "c2");
            await svc.ResubmitAsync("issuerAH", item.WorkflowId, new SubmitWorkflowItemRequest(), "op1", "c3");
            await svc.ApproveAsync("issuerAH", item.WorkflowId, new ApproveWorkflowItemRequest(), "rev1", "c4");
            var finalRes = await svc.CompleteAsync("issuerAH", item.WorkflowId, new CompleteWorkflowItemRequest(), "op1", "c5");

            var auditHistory = finalRes.WorkflowItem!.AuditHistory;
            Assert.That(auditHistory.Count, Is.GreaterThanOrEqualTo(6), "All transitions should be recorded");
            Assert.That(auditHistory.Any(a => a.ToState == WorkflowApprovalState.PendingReview), Is.True);
            Assert.That(auditHistory.Any(a => a.ToState == WorkflowApprovalState.NeedsChanges), Is.True);
            Assert.That(auditHistory.Any(a => a.ToState == WorkflowApprovalState.Approved), Is.True);
            Assert.That(auditHistory.Any(a => a.ToState == WorkflowApprovalState.Completed), Is.True);
        }

        [Test]
        public async Task LatestReviewerActorId_IsSetAfterApproval()
        {
            var svc  = CreateService();
            var item = await CreateApprovedItemAsync(svc, "issuerLA");

            Assert.That(item.LatestReviewerActorId, Is.Not.Null.And.Not.Empty);
        }

        // ── Dashboard summary ─────────────────────────────────────────────────

        [Test]
        public async Task GetApprovalSummary_ReflectsCorrectCounts()
        {
            var svc = CreateService();
            const string issuerId = "issuer-summary";

            // Create 1 PendingReview, 1 Approved, 1 Rejected
            var p1 = await CreatePendingReviewItemAsync(svc, issuerId);
            var p2 = await CreateApprovedItemAsync(svc, issuerId);
            var p3 = await CreatePendingReviewItemAsync(svc, issuerId);
            await svc.RejectAsync(issuerId, p3.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "r" }, "rev", "c");

            await svc.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest { UserId = "m1", Role = IssuerTeamRole.Operator }, "admin");

            var summaryRes = await svc.GetApprovalSummaryAsync(issuerId);
            var summary    = summaryRes.Summary!;

            Assert.That(summaryRes.Success, Is.True);
            Assert.That(summary.PendingReviewCount, Is.EqualTo(1));
            Assert.That(summary.ApprovedCount, Is.EqualTo(1));
            Assert.That(summary.RejectedCount, Is.EqualTo(1));
            Assert.That(summary.ActiveTeamMemberCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetAssignedQueue_ReturnsOnlyItemsForAssignee()
        {
            var svc = CreateService();
            const string issuerId = "issuer-queue";
            await svc.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest { Title = "T1", ItemType = WorkflowItemType.GeneralApproval, AssignedTo = "bob" }, "op");
            await svc.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest { Title = "T2", ItemType = WorkflowItemType.GeneralApproval, AssignedTo = "alice" }, "op");

            var queue = await svc.GetAssignedQueueAsync(issuerId, "bob");
            Assert.That(queue.Items.All(i => i.AssignedTo == "bob"), Is.True);
            Assert.That(queue.Items.Any(i => i.AssignedTo == "alice"), Is.False);
        }

        // ── State filter query ────────────────────────────────────────────────

        [Test]
        public async Task ListWorkflowItems_FilterByState_ReturnsOnlyMatchingItems()
        {
            var svc = CreateService();
            const string issuerId = "issuer-filter";
            await CreatePendingReviewItemAsync(svc, issuerId);
            await svc.CreateWorkflowItemAsync(issuerId, NewReq("Draft item"), "op"); // stays in Prepared

            var pending = await svc.ListWorkflowItemsAsync(issuerId, WorkflowApprovalState.PendingReview);
            var prepared = await svc.ListWorkflowItemsAsync(issuerId, WorkflowApprovalState.Prepared);

            Assert.That(pending.Items.All(i => i.State == WorkflowApprovalState.PendingReview), Is.True);
            Assert.That(prepared.Items.All(i => i.State == WorkflowApprovalState.Prepared), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — HTTP endpoint (WebApplicationFactory)
        // ═══════════════════════════════════════════════════════════════════════

        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private const string IssuerId = "integration-issuer-001";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory     = new CustomWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            var email = $"issuer-workflow-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Issuer Workflow Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<JsonDocument>();
            var jwtToken = regBody?.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        // ── Unauthenticated access ─────────────────────────────────────────────

        [Test]
        public async Task AddMember_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/members",
                new { userId = "u1", role = 0 });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ListMembers_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/members");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task CreateWorkflow_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows",
                new { title = "test", itemType = 0 });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Member management endpoints ────────────────────────────────────────

        [Test]
        public async Task AddMember_ValidRequest_Returns200WithMemberId()
        {
            var resp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/members",
                new { userId = $"member-{Guid.NewGuid():N}@test.com", role = (int)IssuerTeamRole.ComplianceReviewer });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("member").GetProperty("memberId").GetString(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ListMembers_AfterAdd_Returns200WithMemberList()
        {
            var userId = $"list-member-{Guid.NewGuid():N}@test.com";
            await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/members",
                new { userId, role = (int)IssuerTeamRole.Operator });

            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/members");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("members").GetArrayLength(), Is.GreaterThan(0));
        }

        [Test]
        public async Task GetMember_NotFound_Returns404()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/members/{Guid.NewGuid()}");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── Workflow CRUD endpoints ────────────────────────────────────────────

        [Test]
        public async Task CreateWorkflowItem_ValidRequest_Returns200WithWorkflowId()
        {
            var resp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows",
                new { title = "Test workflow item", itemType = (int)WorkflowItemType.GeneralApproval });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(doc.RootElement.GetProperty("workflowItem").GetProperty("state").GetInt32(), Is.EqualTo((int)WorkflowApprovalState.Prepared));
        }

        [Test]
        public async Task GetWorkflowItem_ValidId_Returns200()
        {
            var createResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows",
                new { title = "Fetch test", itemType = (int)WorkflowItemType.ComplianceEvidenceReview });
            var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var workflowId = createDoc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString()!;

            var getResp = await _client.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetWorkflowItem_NotFound_Returns404()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/workflows/{Guid.NewGuid()}");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task ListWorkflowItems_Returns200()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/workflows");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        // ── Full workflow lifecycle via HTTP ────────────────────────────────────

        [Test]
        public async Task WorkflowLifecycle_CreateSubmitApproveComplete_ReturnsCorrectStates()
        {
            // Create
            var createResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows",
                new { title = "E2E lifecycle test", itemType = (int)WorkflowItemType.LaunchReadinessSignOff });
            var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var workflowId = createDoc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString()!;

            // Submit
            var submitResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/submit",
                new { submissionNote = "Ready" });
            var submitDoc = JsonDocument.Parse(await submitResp.Content.ReadAsStringAsync());
            Assert.That(submitDoc.RootElement.GetProperty("workflowItem").GetProperty("state").GetInt32(),
                Is.EqualTo((int)WorkflowApprovalState.PendingReview));

            // Approve
            var approveResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/approve",
                new { approvalNote = "Approved!" });
            var approveDoc = JsonDocument.Parse(await approveResp.Content.ReadAsStringAsync());
            Assert.That(approveDoc.RootElement.GetProperty("workflowItem").GetProperty("state").GetInt32(),
                Is.EqualTo((int)WorkflowApprovalState.Approved));

            // Complete
            var completeResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/complete",
                new { completionNote = "Done" });
            var completeDoc = JsonDocument.Parse(await completeResp.Content.ReadAsStringAsync());
            Assert.That(completeDoc.RootElement.GetProperty("workflowItem").GetProperty("state").GetInt32(),
                Is.EqualTo((int)WorkflowApprovalState.Completed));
        }

        [Test]
        public async Task WorkflowLifecycle_SubmitRejectFlow_Returns400OnSecondReject()
        {
            var createResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows",
                new { title = "Reject flow test", itemType = (int)WorkflowItemType.WhitelistPolicyUpdate });
            var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var workflowId = createDoc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString()!;

            await _client.PostAsJsonAsync($"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/submit",
                new { submissionNote = "test" });

            var rejectResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/reject",
                new { rejectionReason = "Rejected" });
            Assert.That(rejectResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Second reject on a terminal item must fail
            var secondRejectResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/reject",
                new { rejectionReason = "Again" });
            Assert.That(secondRejectResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // ── Summary endpoint ──────────────────────────────────────────────────

        [Test]
        public async Task GetApprovalSummary_Returns200WithCounts()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{IssuerId}/summary");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("summary", out _), Is.True, "Response must contain 'summary' field");
        }

        // ── Schema contract assertions ────────────────────────────────────────

        [Test]
        public async Task CreateWorkflowItem_ResponseShape_HasRequiredFields()
        {
            var resp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows",
                new { title = "Schema test", itemType = (int)WorkflowItemType.GeneralApproval });

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var item = doc.RootElement.GetProperty("workflowItem");

            Assert.That(item.TryGetProperty("workflowId", out _), Is.True, "workflowId required");
            Assert.That(item.TryGetProperty("issuerId", out _), Is.True, "issuerId required");
            Assert.That(item.TryGetProperty("state", out _), Is.True, "state required");
            Assert.That(item.TryGetProperty("createdBy", out _), Is.True, "createdBy required");
            Assert.That(item.TryGetProperty("createdAt", out _), Is.True, "createdAt required");
            Assert.That(item.TryGetProperty("auditHistory", out _), Is.True, "auditHistory required");
        }

        [Test]
        public async Task AddMember_ResponseShape_HasRequiredFields()
        {
            var resp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/members",
                new { userId = $"shape-{Guid.NewGuid():N}@test.com", role = (int)IssuerTeamRole.FinanceReviewer });

            var doc    = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var member = doc.RootElement.GetProperty("member");

            Assert.That(member.TryGetProperty("memberId", out _), Is.True, "memberId required");
            Assert.That(member.TryGetProperty("issuerId", out _), Is.True, "issuerId required");
            Assert.That(member.TryGetProperty("userId", out _), Is.True, "userId required");
            Assert.That(member.TryGetProperty("role", out _), Is.True, "role required");
            Assert.That(member.TryGetProperty("isActive", out _), Is.True, "isActive required");
            Assert.That(member.TryGetProperty("addedAt", out _), Is.True, "addedAt required");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shared helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static CreateWorkflowItemRequest NewReq(string title = "Test workflow item") =>
            new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = title };

        private static async Task<WorkflowItem> CreatePendingReviewItemAsync(IssuerWorkflowService svc, string issuerId)
        {
            var created = (await svc.CreateWorkflowItemAsync(issuerId, NewReq(), "op")).WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, created.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");
            return (await svc.GetWorkflowItemAsync(issuerId, created.WorkflowId)).WorkflowItem!;
        }

        private static async Task<WorkflowItem> CreateApprovedItemAsync(IssuerWorkflowService svc, string issuerId)
        {
            var item = await CreatePendingReviewItemAsync(svc, issuerId);
            await svc.ApproveAsync(issuerId, item.WorkflowId, new ApproveWorkflowItemRequest(), "rev", "c");
            return (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId)).WorkflowItem!;
        }

        private static async Task<WorkflowItem> CreateCompletedItemAsync(IssuerWorkflowService svc, string issuerId)
        {
            var item = await CreateApprovedItemAsync(svc, issuerId);
            await svc.CompleteAsync(issuerId, item.WorkflowId, new CompleteWorkflowItemRequest(), "op", "c");
            return (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId)).WorkflowItem!;
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────

        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIssuerWorkflowIntegration32Chars",
                        ["JwtConfig:SecretKey"] = "IssuerWorkflowTestSecretKey32CharsReq!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }
    }
}

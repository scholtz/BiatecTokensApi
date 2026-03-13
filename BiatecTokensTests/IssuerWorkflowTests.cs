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
    /// Tests for IssuerWorkflowService and IssuerWorkflowController.
    ///
    /// Coverage:
    ///  - State machine transition rules (valid / invalid)
    ///  - Bootstrap: first issuer Admin may be added by any authenticated caller
    ///  - Team membership CRUD with tenant isolation
    ///  - Role-based authorization: Admin gates, approver gates, operator gates, read-only
    ///  - Non-member rejection for every protected operation
    ///  - Insufficient-role rejection with INSUFFICIENT_ROLE error code
    ///  - Workflow item lifecycle: create → submit → approve/reject/request-changes → complete
    ///  - Reassignment with active-member validation
    ///  - Tenant isolation: one issuer cannot read/mutate another issuer's records
    ///  - Unauthorized HTTP access (unauthenticated 401)
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

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [Test]
        public async Task Bootstrap_FirstMemberAsAdmin_AnyCallerSucceeds()
        {
            // The very first member of a new issuer may be added by any authenticated caller.
            var svc = CreateService();
            var res = await svc.AddMemberAsync("new-issuer-1",
                new AddIssuerTeamMemberRequest { UserId = "admin1", Role = IssuerTeamRole.Admin },
                "anonymous-caller-no-membership");

            Assert.That(res.Success, Is.True);
            Assert.That(res.Member!.Role, Is.EqualTo(IssuerTeamRole.Admin));
        }

        [Test]
        public async Task Bootstrap_FirstMemberMustBeAdmin_OperatorRejected()
        {
            var svc = CreateService();
            var res = await svc.AddMemberAsync("new-issuer-2",
                new AddIssuerTeamMemberRequest { UserId = "op1", Role = IssuerTeamRole.Operator },
                "anyone");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("BOOTSTRAP_ROLE_REQUIRED"));
        }

        [Test]
        public async Task Bootstrap_SubsequentAddRequiresAdminRole()
        {
            var svc = CreateService();
            // Bootstrap: add first admin
            await svc.AddMemberAsync("issuer-bootstrap-test", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "anyone");

            // Non-admin trying to add a member
            await svc.AddMemberAsync("issuer-bootstrap-test", new AddIssuerTeamMemberRequest { UserId = "op", Role = IssuerTeamRole.Operator }, "admin"); // OK - admin adding
            var nonAdminAdd = await svc.AddMemberAsync("issuer-bootstrap-test",
                new AddIssuerTeamMemberRequest { UserId = "another-user", Role = IssuerTeamRole.Operator },
                "op"); // op is NOT an admin

            Assert.That(nonAdminAdd.Success, Is.False);
            Assert.That(nonAdminAdd.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        // ── Team membership CRUD ──────────────────────────────────────────────

        [Test]
        public async Task AddMember_ValidRequest_ReturnsMember()
        {
            var svc = CreateService();
            // Bootstrap: first member must be Admin
            var res = await svc.AddMemberAsync("issuer-A",
                new AddIssuerTeamMemberRequest { UserId = "admin1@test.com", Role = IssuerTeamRole.Admin },
                "any-caller");

            Assert.That(res.Success, Is.True);
            Assert.That(res.Member, Is.Not.Null);
            Assert.That(res.Member!.UserId, Is.EqualTo("admin1@test.com"));
            Assert.That(res.Member.Role, Is.EqualTo(IssuerTeamRole.Admin));
            Assert.That(res.Member.IssuerId, Is.EqualTo("issuer-A"));
        }

        [Test]
        public async Task AddMember_MissingUserId_ReturnsBadRequest()
        {
            var svc = CreateService();
            var req = new AddIssuerTeamMemberRequest { UserId = "", Role = IssuerTeamRole.Admin };
            var res = await svc.AddMemberAsync("issuer-A", req, "admin1");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AddMember_Duplicate_ReturnsError()
        {
            var svc = CreateService();
            // Bootstrap admin
            await svc.AddMemberAsync("issuer-dup", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "any");
            // Admin adds an operator
            await svc.AddMemberAsync("issuer-dup", new AddIssuerTeamMemberRequest { UserId = "dup@test.com", Role = IssuerTeamRole.Operator }, "admin");
            // Admin tries to add the same operator again
            var res2 = await svc.AddMemberAsync("issuer-dup", new AddIssuerTeamMemberRequest { UserId = "dup@test.com", Role = IssuerTeamRole.Operator }, "admin");

            Assert.That(res2.Success, Is.False);
            Assert.That(res2.ErrorCode, Is.EqualTo("DUPLICATE_MEMBER"));
        }

        [Test]
        public async Task AddMember_NonAdminActor_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            // Bootstrap admin, then add operator
            await svc.AddMemberAsync("issuer-role", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "any");
            await svc.AddMemberAsync("issuer-role", new AddIssuerTeamMemberRequest { UserId = "op", Role = IssuerTeamRole.Operator }, "admin");

            // Operator tries to add a new member — must fail
            var res = await svc.AddMemberAsync("issuer-role",
                new AddIssuerTeamMemberRequest { UserId = "intruder", Role = IssuerTeamRole.ReadOnlyObserver },
                "op");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task ListMembers_OnlyReturnsActiveMembersForSameIssuer()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuerX", new AddIssuerTeamMemberRequest { UserId = "adminX", Role = IssuerTeamRole.Admin }, "any");
            await svc.AddMemberAsync("issuerY", new AddIssuerTeamMemberRequest { UserId = "adminY", Role = IssuerTeamRole.Admin }, "any");

            var listX = await svc.ListMembersAsync("issuerX", "adminX");
            var listY = await svc.ListMembersAsync("issuerY", "adminY");

            Assert.That(listX.Members.Select(m => m.UserId), Does.Contain("adminX"));
            Assert.That(listX.Members.Select(m => m.UserId), Does.Not.Contain("adminY"), "Tenant isolation violated");
            Assert.That(listY.Members.Select(m => m.UserId), Does.Contain("adminY"));
        }

        [Test]
        public async Task ListMembers_NonMember_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuer-lm", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "any");

            var res = await svc.ListMembersAsync("issuer-lm", "non-member-actor");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task RemoveMember_SoftDelete_MemberNoLongerInActiveList()
        {
            var svc = CreateService();
            // Bootstrap admin
            await svc.AddMemberAsync("issuerR", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "any");
            // Admin adds member to remove
            var addRes = await svc.AddMemberAsync("issuerR", new AddIssuerTeamMemberRequest { UserId = "remove-me", Role = IssuerTeamRole.Operator }, "admin");
            var memberId = addRes.Member!.MemberId;

            var removeRes = await svc.RemoveMemberAsync("issuerR", memberId, "admin");
            Assert.That(removeRes.Success, Is.True);
            Assert.That(removeRes.Member!.IsActive, Is.False);

            var list = await svc.ListMembersAsync("issuerR", "admin");
            Assert.That(list.Members.Any(m => m.MemberId == memberId), Is.False);
        }

        [Test]
        public async Task RemoveMember_WrongIssuer_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuer1", new AddIssuerTeamMemberRequest { UserId = "admin1", Role = IssuerTeamRole.Admin }, "any");
            var addRes = await svc.AddMemberAsync("issuer1", new AddIssuerTeamMemberRequest { UserId = "victim", Role = IssuerTeamRole.Operator }, "admin1");
            var memberId = addRes.Member!.MemberId;

            // Actor is not a member of issuer2
            var res = await svc.RemoveMemberAsync("issuer2", memberId, "admin1");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task UpdateMember_RoleChange_Persists()
        {
            var svc = CreateService();
            // Bootstrap admin
            await svc.AddMemberAsync("issuer-U", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "any");
            // Admin adds a user to update
            var addRes = await svc.AddMemberAsync("issuer-U", new AddIssuerTeamMemberRequest { UserId = "user-U", Role = IssuerTeamRole.Operator }, "admin");
            var memberId = addRes.Member!.MemberId;

            var updateRes = await svc.UpdateMemberAsync("issuer-U", memberId,
                new UpdateIssuerTeamMemberRequest { Role = IssuerTeamRole.FinanceReviewer }, "admin");

            Assert.That(updateRes.Success, Is.True);
            Assert.That(updateRes.Member!.Role, Is.EqualTo(IssuerTeamRole.FinanceReviewer));
        }

        [Test]
        public async Task UpdateMember_NonAdminActor_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await svc.AddMemberAsync("issuer-upd", new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "any");
            await svc.AddMemberAsync("issuer-upd", new AddIssuerTeamMemberRequest { UserId = "op", Role = IssuerTeamRole.Operator }, "admin");
            var addRes = await svc.AddMemberAsync("issuer-upd", new AddIssuerTeamMemberRequest { UserId = "target", Role = IssuerTeamRole.Operator }, "admin");

            var res = await svc.UpdateMemberAsync("issuer-upd", addRes.Member!.MemberId,
                new UpdateIssuerTeamMemberRequest { Role = IssuerTeamRole.Admin }, "op"); // op cannot promote

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        // ── Role-based operation gates ────────────────────────────────────────

        [Test]
        public async Task Operator_CannotApprove_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-op-approve");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-op-approve");

            var res = await svc.ApproveAsync("issuer-op-approve", item.WorkflowId,
                new ApproveWorkflowItemRequest(), "op", "c"); // op has Operator role

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task Operator_CannotReject_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-op-reject");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-op-reject");

            var res = await svc.RejectAsync("issuer-op-reject", item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "test" }, "op", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task Operator_CannotRequestChanges_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-op-rc");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-op-rc");

            var res = await svc.RequestChangesAsync("issuer-op-rc", item.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "fix it" }, "op", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task ComplianceReviewer_CanApprove_Succeeds()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-cr-approve");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-cr-approve");

            var res = await svc.ApproveAsync("issuer-cr-approve", item.WorkflowId,
                new ApproveWorkflowItemRequest(), "reviewer", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
        }

        [Test]
        public async Task ComplianceReviewer_CannotCreateWorkflow_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-cr-create");

            var res = await svc.CreateWorkflowItemAsync("issuer-cr-create",
                NewReq("Reviewer tries to create"), "reviewer"); // reviewer has ComplianceReviewer role

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task ReadOnlyObserver_CannotCreateWorkflow_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-ro-create");
            await svc.AddMemberAsync("issuer-ro-create",
                new AddIssuerTeamMemberRequest { UserId = "observer", Role = IssuerTeamRole.ReadOnlyObserver },
                "admin");

            var res = await svc.CreateWorkflowItemAsync("issuer-ro-create", NewReq(), "observer");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task ReadOnlyObserver_CanReadWorkflowItems_Succeeds()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-ro-read");
            await svc.AddMemberAsync("issuer-ro-read",
                new AddIssuerTeamMemberRequest { UserId = "observer", Role = IssuerTeamRole.ReadOnlyObserver },
                "admin");
            var item = (await svc.CreateWorkflowItemAsync("issuer-ro-read", NewReq(), "op")).WorkflowItem!;

            var res = await svc.GetWorkflowItemAsync("issuer-ro-read", item.WorkflowId, "observer");

            Assert.That(res.Success, Is.True);
        }

        [Test]
        public async Task ReadOnlyObserver_CannotReassign_ReturnsInsufficientRole()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-ro-reassign");
            await svc.AddMemberAsync("issuer-ro-reassign",
                new AddIssuerTeamMemberRequest { UserId = "observer", Role = IssuerTeamRole.ReadOnlyObserver },
                "admin");
            var item = (await svc.CreateWorkflowItemAsync("issuer-ro-reassign", NewReq(), "op")).WorkflowItem!;

            var res = await svc.ReassignAsync("issuer-ro-reassign", item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "op" }, "observer", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        // ── Non-member rejection (every protected operation) ──────────────────

        [Test]
        public async Task NonMember_CannotGetWorkflowItem_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-get");
            var item = (await svc.CreateWorkflowItemAsync("issuer-nm-get", NewReq(), "op")).WorkflowItem!;

            var res = await svc.GetWorkflowItemAsync("issuer-nm-get", item.WorkflowId, "non-member");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task NonMember_CannotListWorkflowItems_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-list");

            var res = await svc.ListWorkflowItemsAsync("issuer-nm-list", "non-member");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task NonMember_CannotCreateWorkflow_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-create");

            var res = await svc.CreateWorkflowItemAsync("issuer-nm-create", NewReq(), "non-member");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task NonMember_CannotSubmitWorkflow_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-submit");
            var item = (await svc.CreateWorkflowItemAsync("issuer-nm-submit", NewReq(), "op")).WorkflowItem!;

            var res = await svc.SubmitForReviewAsync("issuer-nm-submit", item.WorkflowId,
                new SubmitWorkflowItemRequest(), "non-member", "c");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task NonMember_CannotApprove_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-approve");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-nm-approve");

            var res = await svc.ApproveAsync("issuer-nm-approve", item.WorkflowId,
                new ApproveWorkflowItemRequest(), "non-member", "c");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task NonMember_CannotGetSummary_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-summary");

            var res = await svc.GetApprovalSummaryAsync("issuer-nm-summary", "non-member");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task NonMember_CannotGetQueue_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-nm-queue");

            var res = await svc.GetAssignedQueueAsync("issuer-nm-queue", "op", "non-member");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        // ── Tenant isolation ──────────────────────────────────────────────────

        [Test]
        public async Task GetWorkflowItem_WrongIssuer_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-OWNER");
            var item = (await svc.CreateWorkflowItemAsync("issuer-OWNER", NewReq(), "op")).WorkflowItem!;

            // Attacker is not a member of "issuer-ATTACKER" → UNAUTHORIZED
            var res = await svc.GetWorkflowItemAsync("issuer-ATTACKER", item.WorkflowId, "attacker");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task ListWorkflowItems_OnlyReturnsItemsForSameIssuer()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "tenantA");
            await SetupIssuerMembersAsync(svc, "tenantB", adminId: "adminB", operatorId: "opB", reviewerId: "reviewerB");

            await svc.CreateWorkflowItemAsync("tenantA", NewReq("Item A"), "op");
            await svc.CreateWorkflowItemAsync("tenantB", NewReq("Item B"), "opB");

            var listA = await svc.ListWorkflowItemsAsync("tenantA", "op");
            var listB = await svc.ListWorkflowItemsAsync("tenantB", "opB");

            Assert.That(listA.Items.All(i => i.IssuerId == "tenantA"), Is.True, "Tenant A must only see its own items");
            Assert.That(listB.Items.All(i => i.IssuerId == "tenantB"), Is.True, "Tenant B must only see its own items");
            Assert.That(listA.Items.Any(i => i.IssuerId == "tenantB"), Is.False, "Tenant isolation violated");
        }

        [Test]
        public async Task ApproveWorkflowItem_WrongIssuer_ReturnsUnauthorized()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuerOwner");
            var item = await CreatePendingReviewItemAsync(svc, "issuerOwner");

            // attacker is not a member of "issuerAttacker"
            var res = await svc.ApproveAsync("issuerAttacker", item.WorkflowId,
                new ApproveWorkflowItemRequest(), "attacker", "c");
            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        // ── Workflow item state transitions ────────────────────────────────────

        [Test]
        public async Task SubmitForReview_TransitionsToCorrectState()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-submit");
            var item = (await svc.CreateWorkflowItemAsync("issuer-submit", NewReq(), "op")).WorkflowItem!;

            var res = await svc.SubmitForReviewAsync("issuer-submit", item.WorkflowId,
                new SubmitWorkflowItemRequest { SubmissionNote = "Ready" }, "op", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.PendingReview));
        }

        [Test]
        public async Task SubmitForReview_AlreadyPendingItem_ReturnsError()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-sub2");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-sub2");

            var res = await svc.SubmitForReviewAsync("issuer-sub2", item.WorkflowId,
                new SubmitWorkflowItemRequest(), "op", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task Approve_TransitionsToApprovedState_WithMetadata()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-approve");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-approve");

            var res = await svc.ApproveAsync("issuer-approve", item.WorkflowId,
                new ApproveWorkflowItemRequest { ApprovalNote = "Good to go" }, "reviewer", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
            Assert.That(res.WorkflowItem.ApproverActorId, Is.EqualTo("reviewer"));
            Assert.That(res.WorkflowItem.ApprovedAt, Is.Not.Null);
        }

        [Test]
        public async Task Approve_AlreadyApprovedItem_ReturnsError()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-app2");
            var item = await CreateApprovedItemAsync(svc, "issuer-app2");

            var res = await svc.ApproveAsync("issuer-app2", item.WorkflowId,
                new ApproveWorkflowItemRequest(), "reviewer", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task Reject_TransitionsToRejectedState_WithReason()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-reject");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-reject");

            var res = await svc.RejectAsync("issuer-reject", item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "Non-compliant" }, "reviewer", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Rejected));
            Assert.That(res.WorkflowItem.RejectionOrChangeReason, Is.EqualTo("Non-compliant"));
        }

        [Test]
        public async Task Reject_AlreadyRejectedItem_ReturnsError()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-rej2");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-rej2");
            await svc.RejectAsync("issuer-rej2", item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "first" }, "reviewer", "c");

            var res = await svc.RejectAsync("issuer-rej2", item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "second" }, "reviewer", "c");

            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task Reject_MissingReason_ReturnsError()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-rej3");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-rej3");

            var res = await svc.RejectAsync("issuer-rej3", item.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "" }, "reviewer", "c");

            Assert.That(res.Success, Is.False);
        }

        [Test]
        public async Task RequestChanges_TransitionsToNeedsChanges()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-rc");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-rc");

            var res = await svc.RequestChangesAsync("issuer-rc", item.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "Please add section 3" }, "reviewer", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.NeedsChanges));
        }

        [Test]
        public async Task Resubmit_NeedsChangesToPendingReview()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuer-resub");
            var item = await CreatePendingReviewItemAsync(svc, "issuer-resub");
            await svc.RequestChangesAsync("issuer-resub", item.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "Add docs" }, "reviewer", "c");

            var res = await svc.ResubmitAsync("issuer-resub", item.WorkflowId,
                new SubmitWorkflowItemRequest { SubmissionNote = "Updated" }, "op", "c");

            Assert.That(res.Success, Is.True);
            Assert.That(res.WorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.PendingReview));
        }

        // ── Reassignment ──────────────────────────────────────────────────────

        [Test]
        public async Task Reassign_ToActiveMember_Succeeds()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuerR");
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
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuerR2");
            var item = (await svc.CreateWorkflowItemAsync("issuerR2", NewReq(), "op")).WorkflowItem!;

            var res = await svc.ReassignAsync("issuerR2", item.WorkflowId,
                new ReassignWorkflowItemRequest { NewAssigneeId = "unknownUser" }, "op", "c");

            Assert.That(res.Success, Is.False);
            Assert.That(res.ErrorCode, Is.EqualTo("INVALID_ASSIGNEE"));
        }

        [Test]
        public async Task Reassign_CompletedItem_ReturnsError()
        {
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuerRC");
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
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuerAH");
            var item = (await svc.CreateWorkflowItemAsync("issuerAH", NewReq(), "op")).WorkflowItem!;
            await svc.SubmitForReviewAsync("issuerAH", item.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c1");
            await svc.RequestChangesAsync("issuerAH", item.WorkflowId, new RequestChangesRequest { ChangeDescription = "Add doc" }, "reviewer", "c2");
            await svc.ResubmitAsync("issuerAH", item.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c3");
            await svc.ApproveAsync("issuerAH", item.WorkflowId, new ApproveWorkflowItemRequest(), "reviewer", "c4");
            var finalRes = await svc.CompleteAsync("issuerAH", item.WorkflowId, new CompleteWorkflowItemRequest(), "op", "c5");

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
            var svc = CreateService();
            await SetupIssuerMembersAsync(svc, "issuerLA");
            var item = await CreateApprovedItemAsync(svc, "issuerLA");

            Assert.That(item.LatestReviewerActorId, Is.Not.Null.And.Not.Empty);
        }

        // ── Dashboard summary ─────────────────────────────────────────────────

        [Test]
        public async Task GetApprovalSummary_ReflectsCorrectCounts()
        {
            var svc = CreateService();
            const string issuerId = "issuer-summary";
            await SetupIssuerMembersAsync(svc, issuerId);

            var p1 = await CreatePendingReviewItemAsync(svc, issuerId);
            var p2 = await CreateApprovedItemAsync(svc, issuerId);
            var p3 = await CreatePendingReviewItemAsync(svc, issuerId);
            await svc.RejectAsync(issuerId, p3.WorkflowId, new RejectWorkflowItemRequest { RejectionReason = "r" }, "reviewer", "c");

            var summaryRes = await svc.GetApprovalSummaryAsync(issuerId, "admin");
            var summary    = summaryRes.Summary!;

            Assert.That(summaryRes.Success, Is.True);
            Assert.That(summary.PendingReviewCount, Is.EqualTo(1));
            Assert.That(summary.ApprovedCount, Is.EqualTo(1));
            Assert.That(summary.RejectedCount, Is.EqualTo(1));
            Assert.That(summary.ActiveTeamMemberCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetAssignedQueue_ReturnsOnlyItemsForAssignee()
        {
            var svc = CreateService();
            const string issuerId = "issuer-queue";
            await SetupIssuerMembersAsync(svc, issuerId);
            await svc.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest { Title = "T1", ItemType = WorkflowItemType.GeneralApproval, AssignedTo = "bob" }, "op");
            await svc.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest { Title = "T2", ItemType = WorkflowItemType.GeneralApproval, AssignedTo = "alice" }, "op");

            var queue = await svc.GetAssignedQueueAsync(issuerId, "bob", "op");
            Assert.That(queue.Items.All(i => i.AssignedTo == "bob"), Is.True);
            Assert.That(queue.Items.Any(i => i.AssignedTo == "alice"), Is.False);
        }

        // ── State filter query ────────────────────────────────────────────────

        [Test]
        public async Task ListWorkflowItems_FilterByState_ReturnsOnlyMatchingItems()
        {
            var svc = CreateService();
            const string issuerId = "issuer-filter";
            await SetupIssuerMembersAsync(svc, issuerId);

            await CreatePendingReviewItemAsync(svc, issuerId);
            await svc.CreateWorkflowItemAsync(issuerId, NewReq("Draft item"), "op"); // stays in Prepared

            var pending  = await svc.ListWorkflowItemsAsync(issuerId, "op", WorkflowApprovalState.PendingReview);
            var prepared = await svc.ListWorkflowItemsAsync(issuerId, "op", WorkflowApprovalState.Prepared);

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
            _factory      = new CustomWebApplicationFactory();
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

            // Extract actor userId from JWT to bootstrap issuer membership.
            // The JWT 'nameid' claim contains the user's GUID identifier.
            var actorUserId = ExtractClaimFromJwt(jwtToken, "nameid")
                ?? ExtractClaimFromJwt(jwtToken, "sub")
                ?? email; // fallback to email

            // Bootstrap: add actor as Admin of the test issuer (first member, any caller allowed).
            var bootstrapResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/members",
                new { userId = actorUserId, role = (int)IssuerTeamRole.Admin });

            // If bootstrap fails, tests will fail with 403 — log but don't throw.
            if (!bootstrapResp.IsSuccessStatusCode)
            {
                var err = await bootstrapResp.Content.ReadAsStringAsync();
                Console.WriteLine($"[WARNING] Bootstrap AddMember returned {bootstrapResp.StatusCode}: {err}");
            }
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

        [Test]
        public async Task GetWorkflows_NonMemberIssuer_Returns403()
        {
            // Actor (test user) is Admin of IssuerId but NOT a member of this unrelated issuer.
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/unrelated-issuer-{Guid.NewGuid():N}/workflows");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GetSummary_NonMemberIssuer_Returns403()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/unrelated-issuer-{Guid.NewGuid():N}/summary");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task CreateWorkflowItem_NonMemberIssuer_Returns403()
        {
            var resp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/unrelated-issuer-{Guid.NewGuid():N}/workflows",
                new { title = "test", itemType = (int)WorkflowItemType.GeneralApproval });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
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
            var createDoc  = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
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
            var createDoc  = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var workflowId = createDoc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString()!;

            // Submit (Admin has Operator role permissions)
            var submitResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{IssuerId}/workflows/{workflowId}/submit",
                new { submissionNote = "Ready" });
            var submitDoc = JsonDocument.Parse(await submitResp.Content.ReadAsStringAsync());
            Assert.That(submitDoc.RootElement.GetProperty("workflowItem").GetProperty("state").GetInt32(),
                Is.EqualTo((int)WorkflowApprovalState.PendingReview));

            // Approve (Admin has approver role permissions)
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
            var createDoc  = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
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

            var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
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

        /// <summary>
        /// Bootstraps an issuer with admin, operator, and reviewer members.
        /// Idempotent: duplicate member errors are silently ignored.
        /// </summary>
        private static async Task SetupIssuerMembersAsync(
            IssuerWorkflowService svc,
            string issuerId,
            string adminId = "admin",
            string operatorId = "op",
            string reviewerId = "reviewer")
        {
            // Bootstrap: first member must be Admin (any caller is allowed)
            await svc.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest { UserId = adminId, Role = IssuerTeamRole.Admin }, "bootstrap");
            // Add operator and reviewer (Admin performs these)
            await EnsureMemberAsync(svc, issuerId, operatorId, IssuerTeamRole.Operator, adminId);
            await EnsureMemberAsync(svc, issuerId, reviewerId, IssuerTeamRole.ComplianceReviewer, adminId);
        }

        /// <summary>Adds a member, silently ignoring DUPLICATE_MEMBER errors (idempotent setup helper).</summary>
        private static async Task EnsureMemberAsync(
            IssuerWorkflowService svc, string issuerId, string userId, IssuerTeamRole role, string adminId)
        {
            var res = await svc.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest { UserId = userId, Role = role }, adminId);
            // DUPLICATE_MEMBER is acceptable in idempotent setup.
            if (!res.Success && res.ErrorCode != "DUPLICATE_MEMBER")
                throw new InvalidOperationException($"EnsureMemberAsync failed: {res.ErrorCode}: {res.ErrorMessage}");
        }

        /// <summary>Creates a workflow item in PendingReview state. Requires issuer members to be set up first.</summary>
        private static async Task<WorkflowItem> CreatePendingReviewItemAsync(IssuerWorkflowService svc, string issuerId)
        {
            var created = (await svc.CreateWorkflowItemAsync(issuerId, NewReq(), "op")).WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, created.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");
            return (await svc.GetWorkflowItemAsync(issuerId, created.WorkflowId, "op")).WorkflowItem!;
        }

        /// <summary>Creates a workflow item in Approved state. Requires issuer members to be set up first.</summary>
        private static async Task<WorkflowItem> CreateApprovedItemAsync(IssuerWorkflowService svc, string issuerId)
        {
            var item = await CreatePendingReviewItemAsync(svc, issuerId);
            await svc.ApproveAsync(issuerId, item.WorkflowId, new ApproveWorkflowItemRequest(), "reviewer", "c");
            return (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId, "op")).WorkflowItem!;
        }

        /// <summary>Creates a workflow item in Completed state. Requires issuer members to be set up first.</summary>
        private static async Task<WorkflowItem> CreateCompletedItemAsync(IssuerWorkflowService svc, string issuerId)
        {
            var item = await CreateApprovedItemAsync(svc, issuerId);
            await svc.CompleteAsync(issuerId, item.WorkflowId, new CompleteWorkflowItemRequest(), "op", "c");
            return (await svc.GetWorkflowItemAsync(issuerId, item.WorkflowId, "op")).WorkflowItem!;
        }

        /// <summary>Decodes a JWT claim value without verification (for test setup only).</summary>
        private static string? ExtractClaimFromJwt(string jwtToken, string claimName)
        {
            try
            {
                var parts  = jwtToken.Split('.');
                if (parts.Length < 2) return null;
                var padded = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
                var json   = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                var doc    = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty(claimName, out var val) ? val.GetString() : null;
            }
            catch { return null; }
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

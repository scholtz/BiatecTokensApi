using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Repositories;
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
    /// Tests for the permissions-discovery and audit-history extensions added to
    /// IssuerWorkflowService / IssuerWorkflowController.
    ///
    /// Coverage:
    ///  - GetActorPermissionsAsync: member with each role, non-member, all action keys present
    ///  - Role-differentiated permission flags (Admin, Operator, ComplianceReviewer, ReadOnlyObserver)
    ///  - Non-member receives IsMember=false with all actions denied and DeniedReason populated
    ///  - GetAuditHistoryAsync: audit entries created after state transitions, ordering, counts
    ///  - Audit history for non-member returns UNAUTHORIZED
    ///  - Audit history for unknown workflowId returns NOT_FOUND
    ///  - HTTP integration: GET /my-permissions (always 200, including non-member)
    ///  - HTTP integration: GET /workflows/{id}/audit-history (200, 403, 404)
    ///  - Schema contract: permissions response shape, audit history response shape
    ///  - Acceptance Criteria coverage: AC2, AC3, AC4, AC5, AC7
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class IssuerWorkflowGovernanceTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — IssuerWorkflowService directly
        // ═══════════════════════════════════════════════════════════════════════

        private static IssuerWorkflowService CreateService() =>
            new IssuerWorkflowService(
                NullLogger<IssuerWorkflowService>.Instance,
                new IssuerWorkflowRepository(
                    NullLogger<IssuerWorkflowRepository>.Instance));

        private static async Task<(IssuerWorkflowService svc, string issuerId)> SetupIssuerAsync()
        {
            var svc      = CreateService();
            var issuerId = $"gov-issuer-{Guid.NewGuid():N}";
            // Bootstrap Admin
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = "admin", Role = IssuerTeamRole.Admin }, "boot");
            // Operator
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = "op", Role = IssuerTeamRole.Operator }, "admin");
            // ComplianceReviewer
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = "reviewer", Role = IssuerTeamRole.ComplianceReviewer }, "admin");
            // ReadOnlyObserver
            await svc.AddMemberAsync(issuerId,
                new AddIssuerTeamMemberRequest { UserId = "observer", Role = IssuerTeamRole.ReadOnlyObserver }, "admin");
            return (svc, issuerId);
        }

        // ── GetActorPermissionsAsync: membership status ───────────────────────

        [Test]
        public async Task GetActorPermissions_NonMember_IsMemberFalse_AllActionsDenied()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "stranger");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Permissions, Is.Not.Null);
            Assert.That(result.Permissions!.IsMember, Is.False);
            Assert.That(result.Permissions.Role, Is.Null);
            Assert.That(result.Permissions.PermittedActions, Is.Not.Empty);
            Assert.That(result.Permissions.PermittedActions.All(a => !a.IsAllowed), Is.True,
                "Non-member should have all actions denied");
            Assert.That(result.Permissions.PermittedActions.All(a => a.DeniedReason != null), Is.True,
                "Every denied action must have a DeniedReason");
        }

        [Test]
        public async Task GetActorPermissions_Admin_IsMemberTrue_AllActionsAllowed()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "admin");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Permissions!.IsMember, Is.True);
            Assert.That(result.Permissions.Role, Is.EqualTo(IssuerTeamRole.Admin));
            // Admin can do everything
            var denied = result.Permissions.PermittedActions.Where(a => !a.IsAllowed).ToList();
            Assert.That(denied, Is.Empty, $"Admin should have all actions allowed. Denied: {string.Join(", ", denied.Select(d => d.ActionKey))}");
        }

        [Test]
        public async Task GetActorPermissions_Operator_CanCreateAndSubmit_CannotApprove_CannotManageMembers()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "op");

            Assert.That(result.Permissions!.Role, Is.EqualTo(IssuerTeamRole.Operator));
            Assert.That(GetAction(result, "CREATE_WORKFLOW_ITEM").IsAllowed, Is.True);
            Assert.That(GetAction(result, "SUBMIT_FOR_REVIEW").IsAllowed, Is.True);
            Assert.That(GetAction(result, "RESUBMIT").IsAllowed, Is.True);
            Assert.That(GetAction(result, "COMPLETE").IsAllowed, Is.True);
            Assert.That(GetAction(result, "APPROVE").IsAllowed, Is.False);
            Assert.That(GetAction(result, "REJECT").IsAllowed, Is.False);
            Assert.That(GetAction(result, "REQUEST_CHANGES").IsAllowed, Is.False);
            Assert.That(GetAction(result, "MANAGE_MEMBERS").IsAllowed, Is.False);
            Assert.That(GetAction(result, "VIEW_MEMBERS").IsAllowed, Is.True);
            Assert.That(GetAction(result, "VIEW_AUDIT_HISTORY").IsAllowed, Is.True);
        }

        [Test]
        public async Task GetActorPermissions_ComplianceReviewer_CanApprove_CannotCreate_CannotManageMembers()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "reviewer");

            Assert.That(result.Permissions!.Role, Is.EqualTo(IssuerTeamRole.ComplianceReviewer));
            Assert.That(GetAction(result, "APPROVE").IsAllowed, Is.True);
            Assert.That(GetAction(result, "REJECT").IsAllowed, Is.True);
            Assert.That(GetAction(result, "REQUEST_CHANGES").IsAllowed, Is.True);
            Assert.That(GetAction(result, "CREATE_WORKFLOW_ITEM").IsAllowed, Is.False);
            Assert.That(GetAction(result, "COMPLETE").IsAllowed, Is.False);
            Assert.That(GetAction(result, "MANAGE_MEMBERS").IsAllowed, Is.False);
            Assert.That(GetAction(result, "VIEW_AUDIT_HISTORY").IsAllowed, Is.True);
        }

        [Test]
        public async Task GetActorPermissions_ReadOnlyObserver_CanOnlyView_CannotMutate()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "observer");

            Assert.That(result.Permissions!.Role, Is.EqualTo(IssuerTeamRole.ReadOnlyObserver));
            Assert.That(GetAction(result, "VIEW_MEMBERS").IsAllowed, Is.True);
            Assert.That(GetAction(result, "VIEW_WORKFLOW_ITEMS").IsAllowed, Is.True);
            Assert.That(GetAction(result, "VIEW_AUDIT_HISTORY").IsAllowed, Is.True);
            Assert.That(GetAction(result, "VIEW_APPROVAL_SUMMARY").IsAllowed, Is.True);
            Assert.That(GetAction(result, "CREATE_WORKFLOW_ITEM").IsAllowed, Is.False);
            Assert.That(GetAction(result, "APPROVE").IsAllowed, Is.False);
            Assert.That(GetAction(result, "REJECT").IsAllowed, Is.False);
            Assert.That(GetAction(result, "MANAGE_MEMBERS").IsAllowed, Is.False);
            Assert.That(GetAction(result, "REASSIGN").IsAllowed, Is.False);
        }

        [Test]
        public async Task GetActorPermissions_AllExpectedActionKeysPresent()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "admin");

            var keys = result.Permissions!.PermittedActions.Select(a => a.ActionKey).ToHashSet();
            string[] expected =
            {
                "CREATE_WORKFLOW_ITEM", "SUBMIT_FOR_REVIEW", "APPROVE", "REJECT",
                "REQUEST_CHANGES", "RESUBMIT", "COMPLETE", "REASSIGN",
                "MANAGE_MEMBERS", "VIEW_MEMBERS", "VIEW_WORKFLOW_ITEMS",
                "VIEW_AUDIT_HISTORY", "VIEW_APPROVAL_SUMMARY"
            };
            foreach (var key in expected)
                Assert.That(keys.Contains(key), Is.True, $"Expected action key '{key}' missing from permissions snapshot.");
        }

        [Test]
        public async Task GetActorPermissions_AllowedActionsHaveNullDeniedReason()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "admin");

            var bad = result.Permissions!.PermittedActions
                .Where(a => a.IsAllowed && a.DeniedReason != null)
                .ToList();
            Assert.That(bad, Is.Empty, "Allowed actions must not have a DeniedReason.");
        }

        [Test]
        public async Task GetActorPermissions_DeniedActions_HaveDeniedReasonPopulated()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            // Operator cannot APPROVE or MANAGE_MEMBERS
            var result = await svc.GetActorPermissionsAsync(issuerId, "op");

            var denied = result.Permissions!.PermittedActions.Where(a => !a.IsAllowed).ToList();
            Assert.That(denied.All(a => !string.IsNullOrWhiteSpace(a.DeniedReason)), Is.True,
                "Every denied action must have a non-empty DeniedReason.");
        }

        [Test]
        public async Task GetActorPermissions_IssuedAt_IsRecent()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var result = await svc.GetActorPermissionsAsync(issuerId, "admin");

            Assert.That(result.Permissions!.GeneratedAt,
                Is.GreaterThan(DateTime.UtcNow.AddSeconds(-10)));
        }

        // ── GetAuditHistoryAsync: content and ordering ────────────────────────

        [Test]
        public async Task GetAuditHistory_AfterFullLifecycle_ReturnsChronologicalEntries()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            // Create → Submit → Approve → Complete
            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "audit test" }, "op"))
                .WorkflowItem!;

            await svc.SubmitForReviewAsync(issuerId, created.WorkflowId,
                new SubmitWorkflowItemRequest { SubmissionNote = "submit note" }, "op", "c1");
            await svc.ApproveAsync(issuerId, created.WorkflowId,
                new ApproveWorkflowItemRequest { ApprovalNote = "looks good" }, "reviewer", "c2");
            await svc.CompleteAsync(issuerId, created.WorkflowId,
                new CompleteWorkflowItemRequest { CompletionNote = "done" }, "op", "c3");

            var hist = await svc.GetAuditHistoryAsync(issuerId, created.WorkflowId, "op");

            Assert.That(hist.Success, Is.True);
            // creation + submit + approve + complete = 4 entries
            Assert.That(hist.AuditHistory.Count, Is.EqualTo(4), "creation + submit + approve + complete = 4 entries");
            Assert.That(hist.EntryCount, Is.EqualTo(4));
            Assert.That(hist.CurrentState, Is.EqualTo(WorkflowApprovalState.Completed));

            // Verify chronological order
            for (int i = 1; i < hist.AuditHistory.Count; i++)
                Assert.That(hist.AuditHistory[i].Timestamp,
                    Is.GreaterThanOrEqualTo(hist.AuditHistory[i - 1].Timestamp));
        }

        [Test]
        public async Task GetAuditHistory_AuditEntry_HasRequiredFields()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "field test" }, "op"))
                .WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, created.WorkflowId,
                new SubmitWorkflowItemRequest(), "op", "corr-123");

            var hist = await svc.GetAuditHistoryAsync(issuerId, created.WorkflowId, "admin");

            // Two entries: creation (idx 0) and submission (idx 1)
            Assert.That(hist.AuditHistory.Count, Is.EqualTo(2));
            var entry = hist.AuditHistory[1]; // submission transition
            Assert.That(entry.EntryId, Is.Not.Null.And.Not.Empty, "EntryId required");
            Assert.That(entry.WorkflowId, Is.EqualTo(created.WorkflowId), "WorkflowId required");
            Assert.That(entry.ActorId, Is.EqualTo("op"), "ActorId must match submitter");
            Assert.That(entry.FromState, Is.EqualTo(WorkflowApprovalState.Prepared));
            Assert.That(entry.ToState, Is.EqualTo(WorkflowApprovalState.PendingReview));
            Assert.That(entry.Timestamp, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-30)));
            Assert.That(entry.CorrelationId, Is.EqualTo("corr-123"), "CorrelationId must be propagated");
        }

        [Test]
        public async Task GetAuditHistory_NewItem_HasOneCreationEntry()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "new item" }, "op"))
                .WorkflowItem!;

            var hist = await svc.GetAuditHistoryAsync(issuerId, created.WorkflowId, "op");

            Assert.That(hist.Success, Is.True);
            // A creation audit entry is recorded when the item is created
            Assert.That(hist.EntryCount, Is.EqualTo(1), "One creation audit entry is recorded on item creation");
            Assert.That(hist.CurrentState, Is.EqualTo(WorkflowApprovalState.Prepared));
            Assert.That(hist.AuditHistory[0].ToState, Is.EqualTo(WorkflowApprovalState.Prepared));
        }

        [Test]
        public async Task GetAuditHistory_RejectedItem_RecordsRejectionActor()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.WhitelistPolicyUpdate, Title = "reject test" }, "op"))
                .WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, created.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");
            await svc.RejectAsync(issuerId, created.WorkflowId,
                new RejectWorkflowItemRequest { RejectionReason = "Non-compliant" }, "reviewer", "c");

            var hist = await svc.GetAuditHistoryAsync(issuerId, created.WorkflowId, "reviewer");

            Assert.That(hist.AuditHistory.Any(e => e.ToState == WorkflowApprovalState.Rejected && e.ActorId == "reviewer"),
                Is.True, "Rejection audit entry must record the reviewer actor");
        }

        [Test]
        public async Task GetAuditHistory_NonMember_ReturnsUnauthorized()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "perm test" }, "op"))
                .WorkflowItem!;

            var hist = await svc.GetAuditHistoryAsync(issuerId, created.WorkflowId, "stranger");

            Assert.That(hist.Success, Is.False);
            Assert.That(hist.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        [Test]
        public async Task GetAuditHistory_UnknownWorkflowId_ReturnsNotFound()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var hist = await svc.GetAuditHistoryAsync(issuerId, "nonexistent-workflow-id", "admin");

            Assert.That(hist.Success, Is.False);
            Assert.That(hist.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetAuditHistory_NeedsChanges_ThenResubmit_RecordsBothTransitions()
        {
            var (svc, issuerId) = await SetupIssuerAsync();

            var created = (await svc.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "changes test" }, "op"))
                .WorkflowItem!;
            await svc.SubmitForReviewAsync(issuerId, created.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");
            await svc.RequestChangesAsync(issuerId, created.WorkflowId,
                new RequestChangesRequest { ChangeDescription = "Need more data" }, "reviewer", "c");
            await svc.ResubmitAsync(issuerId, created.WorkflowId, new SubmitWorkflowItemRequest(), "op", "c");

            var hist = await svc.GetAuditHistoryAsync(issuerId, created.WorkflowId, "op");

            // creation + submit + request-changes + resubmit = 4 entries
            Assert.That(hist.EntryCount, Is.EqualTo(4));
            Assert.That(hist.AuditHistory.Any(e => e.ToState == WorkflowApprovalState.NeedsChanges), Is.True);
            Assert.That(hist.AuditHistory.Last().ToState, Is.EqualTo(WorkflowApprovalState.PendingReview));
        }

        // ── Tenant isolation for permissions and audit ────────────────────────

        [Test]
        public async Task GetActorPermissions_TenantIsolation_MemberOfACannotSeeB()
        {
            var svc      = CreateService();
            var issuerA  = $"tenant-A-{Guid.NewGuid():N}";
            var issuerB  = $"tenant-B-{Guid.NewGuid():N}";

            await svc.AddMemberAsync(issuerA,
                new AddIssuerTeamMemberRequest { UserId = "userA", Role = IssuerTeamRole.Admin }, "boot");
            await svc.AddMemberAsync(issuerB,
                new AddIssuerTeamMemberRequest { UserId = "userB", Role = IssuerTeamRole.Admin }, "boot");

            // userA queries permissions against issuerB — must be non-member
            var result = await svc.GetActorPermissionsAsync(issuerB, "userA");
            Assert.That(result.Permissions!.IsMember, Is.False);
            Assert.That(result.Permissions.PermittedActions.All(a => !a.IsAllowed), Is.True);
        }

        [Test]
        public async Task GetAuditHistory_TenantIsolation_MemberOfACannotReadBHistory()
        {
            var svc      = CreateService();
            var issuerA  = $"audit-A-{Guid.NewGuid():N}";
            var issuerB  = $"audit-B-{Guid.NewGuid():N}";

            await svc.AddMemberAsync(issuerA,
                new AddIssuerTeamMemberRequest { UserId = "userA", Role = IssuerTeamRole.Admin }, "boot");
            await svc.AddMemberAsync(issuerB,
                new AddIssuerTeamMemberRequest { UserId = "userB", Role = IssuerTeamRole.Admin }, "boot");

            // Create item in issuerB
            var itemB = (await svc.CreateWorkflowItemAsync(issuerB,
                new CreateWorkflowItemRequest { ItemType = WorkflowItemType.GeneralApproval, Title = "b item" }, "userB"))
                .WorkflowItem!;

            // userA (member of issuerA) tries to read issuerB audit history
            var hist = await svc.GetAuditHistoryAsync(issuerB, itemB.WorkflowId, "userA");
            Assert.That(hist.Success, Is.False);
            Assert.That(hist.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — HTTP endpoints
        // ═══════════════════════════════════════════════════════════════════════

        private GovWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;
        private const string GovIssuerId = "governance-issuer-tests-001";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory      = new GovWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            var email  = $"gov-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Governance Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<JsonDocument>();
            var jwtToken = regBody?.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

            var actorUserId = ExtractClaimFromJwt(jwtToken, "nameid")
                ?? ExtractClaimFromJwt(jwtToken, "sub")
                ?? email;

            // Bootstrap actor as Admin of the governance test issuer.
            var bootstrapResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/members",
                new { userId = actorUserId, role = (int)IssuerTeamRole.Admin });

            if (!bootstrapResp.IsSuccessStatusCode)
                Console.WriteLine($"[WARNING] Gov bootstrap returned {bootstrapResp.StatusCode}: {await bootstrapResp.Content.ReadAsStringAsync()}");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        // ── GET /my-permissions ───────────────────────────────────────────────

        [Test]
        public async Task GetMyPermissions_Authenticated_Returns200()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{GovIssuerId}/my-permissions");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetMyPermissions_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync($"/api/v1/issuer-workflow/{GovIssuerId}/my-permissions");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetMyPermissions_NonMemberIssuer_Returns200WithIsMemberFalse()
        {
            // Query against an issuer the test user is not a member of
            var resp = await _client.GetAsync(
                $"/api/v1/issuer-workflow/unknown-issuer-{Guid.NewGuid():N}/my-permissions");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Non-member queries must always return 200 (not 403), so the frontend can always retrieve guidance.");

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("permissions").GetProperty("isMember").GetBoolean(), Is.False);
        }

        [Test]
        public async Task GetMyPermissions_Member_Returns200WithIsMemberTrue()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{GovIssuerId}/my-permissions");
            var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("permissions").GetProperty("isMember").GetBoolean(), Is.True);
        }

        [Test]
        public async Task GetMyPermissions_ResponseShape_HasRequiredFields()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{GovIssuerId}/my-permissions");
            var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            var perms = doc.RootElement.GetProperty("permissions");
            Assert.That(perms.TryGetProperty("issuerId", out _), Is.True, "issuerId required");
            Assert.That(perms.TryGetProperty("actorId", out _), Is.True, "actorId required");
            Assert.That(perms.TryGetProperty("isMember", out _), Is.True, "isMember required");
            Assert.That(perms.TryGetProperty("role", out _), Is.True, "role required");
            Assert.That(perms.TryGetProperty("permittedActions", out _), Is.True, "permittedActions required");
            Assert.That(perms.TryGetProperty("generatedAt", out _), Is.True, "generatedAt required");
        }

        [Test]
        public async Task GetMyPermissions_PermittedActions_HaveRequiredFields()
        {
            var resp = await _client.GetAsync($"/api/v1/issuer-workflow/{GovIssuerId}/my-permissions");
            var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            var actions = doc.RootElement.GetProperty("permissions").GetProperty("permittedActions");
            Assert.That(actions.GetArrayLength(), Is.GreaterThan(0), "permittedActions must not be empty");

            foreach (var action in actions.EnumerateArray())
            {
                Assert.That(action.TryGetProperty("actionKey", out _), Is.True, "actionKey required");
                Assert.That(action.TryGetProperty("label", out _), Is.True, "label required");
                Assert.That(action.TryGetProperty("isAllowed", out _), Is.True, "isAllowed required");
            }
        }

        // ── GET /workflows/{id}/audit-history ─────────────────────────────────

        [Test]
        public async Task GetAuditHistory_UnknownWorkflowId_Returns404()
        {
            var resp = await _client.GetAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows/nonexistent-id/audit-history");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetAuditHistory_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows/some-id/audit-history");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetAuditHistory_NonMemberIssuer_Returns403()
        {
            var resp = await _client.GetAsync(
                $"/api/v1/issuer-workflow/unknown-issuer-{Guid.NewGuid():N}/workflows/some-id/audit-history");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GetAuditHistory_AfterSubmit_Returns200WithOneEntry()
        {
            // Create and submit a workflow item via HTTP
            var createResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows",
                new { title = "HTTP audit test", itemType = (int)WorkflowItemType.GeneralApproval });
            Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var createDoc  = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var workflowId = createDoc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString()!;

            await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows/{workflowId}/submit",
                new { submissionNote = "ready" });

            var histResp = await _client.GetAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows/{workflowId}/audit-history");
            Assert.That(histResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await histResp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            // creation entry + submission entry = 2
            Assert.That(doc.RootElement.GetProperty("entryCount").GetInt32(), Is.EqualTo(2));
        }

        [Test]
        public async Task GetAuditHistory_ResponseShape_HasRequiredFields()
        {
            // Create a workflow item so we have a valid ID
            var createResp = await _client.PostAsJsonAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows",
                new { title = "shape test", itemType = (int)WorkflowItemType.GeneralApproval });
            var createDoc  = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var workflowId = createDoc.RootElement.GetProperty("workflowItem").GetProperty("workflowId").GetString()!;

            var resp = await _client.GetAsync(
                $"/api/v1/issuer-workflow/{GovIssuerId}/workflows/{workflowId}/audit-history");
            var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.TryGetProperty("success", out _), Is.True, "success required");
            Assert.That(doc.RootElement.TryGetProperty("issuerId", out _), Is.True, "issuerId required");
            Assert.That(doc.RootElement.TryGetProperty("workflowId", out _), Is.True, "workflowId required");
            Assert.That(doc.RootElement.TryGetProperty("currentState", out _), Is.True, "currentState required");
            Assert.That(doc.RootElement.TryGetProperty("auditHistory", out _), Is.True, "auditHistory required");
            Assert.That(doc.RootElement.TryGetProperty("entryCount", out _), Is.True, "entryCount required");
            Assert.That(doc.RootElement.TryGetProperty("generatedAt", out _), Is.True, "generatedAt required");
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static WorkflowPermittedAction GetAction(ActorPermissionsResponse response, string key) =>
            response.Permissions!.PermittedActions.First(a => a.ActionKey == key);

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

        private class GovWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIssuerWorkflowGovernance32Chars",
                        ["JwtConfig:SecretKey"] = "IssuerWorkflowGovTestSecretKey32CharsReq",
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

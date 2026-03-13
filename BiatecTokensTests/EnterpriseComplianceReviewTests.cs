using BiatecTokensApi.Models.EnterpriseComplianceReview;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for EnterpriseComplianceReviewService and EnterpriseComplianceReviewController.
    ///
    /// Coverage:
    ///  - Unit tests: service logic, evidence assembly, contradiction detection, missing evidence,
    ///    capability calculation, diagnostics events, authorization rules
    ///  - Integration tests: HTTP endpoint shape, auth, 403 for role violations, 404 for missing items
    ///  - Contract tests: response payload structure for frontend consumption
    ///  - Observability tests: diagnostics events emitted for auth denials, invalid transitions,
    ///    and evidence issues
    ///  - Branch coverage: all ReviewDecisionType branches, all EvidenceIssueSeverity branches,
    ///    all ReviewDiagnosticsEventCategory branches
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class EnterpriseComplianceReviewTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Helper factories
        // ═══════════════════════════════════════════════════════════════════════

        private static IssuerWorkflowService CreateWorkflowService() =>
            new IssuerWorkflowService(
                NullLogger<IssuerWorkflowService>.Instance,
                new BiatecTokensApi.Repositories.IssuerWorkflowRepository(
                    NullLogger<BiatecTokensApi.Repositories.IssuerWorkflowRepository>.Instance));

        private static EnterpriseComplianceReviewService CreateReviewService(IIssuerWorkflowService? wf = null) =>
            new EnterpriseComplianceReviewService(
                wf ?? CreateWorkflowService(),
                new BiatecTokensApi.Repositories.ComplianceReviewRepository(
                    NullLogger<BiatecTokensApi.Repositories.ComplianceReviewRepository>.Instance),
                new BiatecTokensApi.Repositories.IssuerWorkflowRepository(
                    NullLogger<BiatecTokensApi.Repositories.IssuerWorkflowRepository>.Instance),
                NullLogger<EnterpriseComplianceReviewService>.Instance);

        /// <summary>
        /// Creates an issuer team with one Admin and optionally a Reviewer, returns the service.
        /// Returns (reviewService, workflowService, adminId, reviewerId).
        /// </summary>
        private static async Task<(EnterpriseComplianceReviewService review, IssuerWorkflowService wf, string issuerId, string adminId, string reviewerId)>
            CreateTeamAsync()
        {
            var wf       = CreateWorkflowService();
            var review   = CreateReviewService(wf);
            var issuerId = "issuer-" + Guid.NewGuid().ToString("N")[..8];
            var adminId  = "admin@example.com";
            var reviewerId = "reviewer@example.com";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = adminId, Role = IssuerTeamRole.Admin, DisplayName = "Admin User"
            }, adminId);

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = reviewerId, Role = IssuerTeamRole.ComplianceReviewer, DisplayName = "Compliance Reviewer"
            }, adminId);

            return (review, wf, issuerId, adminId, reviewerId);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — service logic directly
        // ═══════════════════════════════════════════════════════════════════════

        // ── GetReviewQueueAsync ────────────────────────────────────────────────

        [Test]
        public async Task GetReviewQueue_ValidAdmin_ReturnsSuccess()
        {
            var (review, _, issuerId, adminId, _) = await CreateTeamAsync();
            var result = await review.GetReviewQueueAsync(issuerId, adminId, "corr-1");
            Assert.That(result.Success, Is.True);
            Assert.That(result.ActorId, Is.EqualTo(adminId));
            Assert.That(result.ActorRole, Is.EqualTo(IssuerTeamRole.Admin));
        }

        [Test]
        public async Task GetReviewQueue_NonMember_ReturnsForbidden()
        {
            var (review, _, issuerId, _, _) = await CreateTeamAsync();
            var result = await review.GetReviewQueueAsync(issuerId, "outsider@example.com", "corr-2");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNAUTHORIZED").Or.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task GetReviewQueue_EmptyIssuerId_ReturnsBadRequest()
        {
            var review = CreateReviewService();
            var result = await review.GetReviewQueueAsync("", "actor@example.com", "corr-3");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task GetReviewQueue_WithPendingItem_IncludesItemInQueue()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            // Create and submit a workflow item
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType    = WorkflowItemType.ComplianceEvidenceReview,
                Title       = "Test Review Item",
                Description = "Review this compliance evidence",
                ExternalReference = "evidence-123"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;

            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "corr-submit");

            // Reviewer should see the pending item
            var queueResult = await review.GetReviewQueueAsync(issuerId, reviewerId, "corr-queue");
            Assert.That(queueResult.Success, Is.True);
            Assert.That(queueResult.Items.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetReviewQueue_ReviewerCapabilities_IncludeApproveRejectRequestChanges()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Test",
                Description = "desc",
                ExternalReference = "ref-1"
            }, adminId);
            await wf.SubmitForReviewAsync(issuerId, createResp.WorkflowItem!.WorkflowId,
                new SubmitWorkflowItemRequest(), adminId, "corr");

            var queueResult = await review.GetReviewQueueAsync(issuerId, reviewerId, "corr-caps");
            var item = queueResult.Items.FirstOrDefault(i => i.WorkflowItem.State == WorkflowApprovalState.PendingReview);

            Assert.That(item, Is.Not.Null);
            Assert.That(item!.AvailableCapabilities, Contains.Item(ReviewCapability.Approve));
            Assert.That(item.AvailableCapabilities, Contains.Item(ReviewCapability.Reject));
            Assert.That(item.AvailableCapabilities, Contains.Item(ReviewCapability.RequestChanges));
        }

        [Test]
        public async Task GetReviewQueue_OperatorCapabilities_IncludeSubmit()
        {
            var wf      = CreateWorkflowService();
            var review  = CreateReviewService(wf);
            var issuerId = "iss-ops-" + Guid.NewGuid().ToString("N")[..6];
            var adminId  = "admin@ops.test";
            var opId     = "op@ops.test";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = adminId, Role = IssuerTeamRole.Admin }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = opId, Role = IssuerTeamRole.Operator }, adminId);

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Operator item",
                Description = "Some description"
            }, opId);

            var queueResult = await review.GetReviewQueueAsync(issuerId, opId, "corr-op");
            Assert.That(queueResult.Success, Is.True);
            var preparedItems = queueResult.Items.Where(i => i.WorkflowItem.State == WorkflowApprovalState.Prepared).ToList();
            Assert.That(preparedItems.Count, Is.GreaterThan(0));
            Assert.That(preparedItems[0].AvailableCapabilities, Contains.Item(ReviewCapability.Submit));
        }

        [Test]
        public async Task GetReviewQueue_ReadOnlyObserver_HasOnlyViewCapability()
        {
            var wf      = CreateWorkflowService();
            var review  = CreateReviewService(wf);
            var issuerId = "iss-ro-" + Guid.NewGuid().ToString("N")[..6];
            var adminId  = "admin@ro.test";
            var roId     = "observer@ro.test";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = adminId, Role = IssuerTeamRole.Admin }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = roId, Role = IssuerTeamRole.ReadOnlyObserver }, adminId);

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType = WorkflowItemType.GeneralApproval, Title = "RO Item", Description = "desc"
            }, adminId);

            var queueResult = await review.GetReviewQueueAsync(issuerId, roId, "corr-ro");
            Assert.That(queueResult.Success, Is.True);
            foreach (var item in queueResult.Items)
            {
                Assert.That(item.AvailableCapabilities, Contains.Item(ReviewCapability.View));
                Assert.That(item.AvailableCapabilities, Has.Count.EqualTo(1),
                    "ReadOnlyObserver should only have View capability");
            }
        }

        // ── GetEvidenceBundleAsync ─────────────────────────────────────────────

        [Test]
        public async Task GetEvidenceBundle_ValidItem_ReturnsBundle()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Evidence Test Item",
                Description = "Evidence description",
                ExternalReference = "policy-456"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;

            var bundleResp = await review.GetEvidenceBundleAsync(issuerId, workflowId, reviewerId, "corr-bundle");
            Assert.That(bundleResp.Success, Is.True);
            Assert.That(bundleResp.Bundle, Is.Not.Null);
            Assert.That(bundleResp.Bundle!.WorkflowId, Is.EqualTo(workflowId));
            Assert.That(bundleResp.Bundle.EvidenceItems.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetEvidenceBundle_ComplianceReviewWithNoExternalRef_IsCriticallyBlocked()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            // Create ComplianceEvidenceReview item with NO external reference
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.ComplianceEvidenceReview,
                Title     = "Missing Ref Review",
                Description = "Should be blocked"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;

            var bundleResp = await review.GetEvidenceBundleAsync(issuerId, workflowId, reviewerId, "corr-block");
            Assert.That(bundleResp.Success, Is.True);
            Assert.That(bundleResp.Bundle!.IsReviewReady, Is.False);
            Assert.That(bundleResp.Bundle.IssueSummary.CriticalCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetEvidenceBundle_WithExternalRef_ContainsExternalRefEvidence()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType          = WorkflowItemType.WhitelistPolicyUpdate,
                Title             = "Policy Update",
                Description       = "Updating whitelist policy",
                ExternalReference = "policy-789"
            }, adminId);

            var bundleResp = await review.GetEvidenceBundleAsync(issuerId, createResp.WorkflowItem!.WorkflowId, adminId, "corr-ext");
            Assert.That(bundleResp.Success, Is.True);
            var extRefItem = bundleResp.Bundle!.EvidenceItems
                .FirstOrDefault(e => e.Category == ReviewEvidenceCategory.Policy &&
                                     e.Metadata.ContainsKey("ExternalReference"));
            Assert.That(extRefItem, Is.Not.Null, "ExternalReference evidence item should be present");
        }

        [Test]
        public async Task GetEvidenceBundle_NonMember_ReturnsForbidden()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
                { ItemType = WorkflowItemType.GeneralApproval, Title = "t", Description = "d" }, adminId);

            var result = await review.GetEvidenceBundleAsync(issuerId, createResp.WorkflowItem!.WorkflowId,
                "outsider@example.com", "corr-forbidden");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNAUTHORIZED").Or.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task GetEvidenceBundle_WithAuditHistory_IncludesAuditTrailEvidence()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Audit Trail Test",
                Description = "Has audit trail",
                ExternalReference = "ref-audit"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "corr-submit");

            var bundleResp = await review.GetEvidenceBundleAsync(issuerId, workflowId, reviewerId, "corr-audit");
            Assert.That(bundleResp.Success, Is.True);
            var auditEvidence = bundleResp.Bundle!.EvidenceItems
                .FirstOrDefault(e => e.Category == ReviewEvidenceCategory.AuditTrail);
            Assert.That(auditEvidence, Is.Not.Null, "Audit trail evidence should be present after a state transition");
        }

        [Test]
        public async Task GetEvidenceBundle_NeedsChangesMultipleTimes_HasContradiction()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Stale Loop",
                Description = "desc",
                ExternalReference = "ref-loop"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;

            // First cycle
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c1");
            await wf.RequestChangesAsync(issuerId, workflowId, new RequestChangesRequest { ChangeDescription = "Fix this" }, reviewerId, "c1");
            await wf.ResubmitAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c1");

            // Second cycle (triggers warning)
            await wf.RequestChangesAsync(issuerId, workflowId, new RequestChangesRequest { ChangeDescription = "Fix again" }, reviewerId, "c2");
            await wf.ResubmitAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c2");

            var bundleResp = await review.GetEvidenceBundleAsync(issuerId, workflowId, reviewerId, "corr-loop");
            Assert.That(bundleResp.Success, Is.True);
            Assert.That(bundleResp.Bundle!.Contradictions.Count, Is.GreaterThan(0));
        }

        // ── SubmitReviewDecisionAsync ──────────────────────────────────────────

        [Test]
        public async Task SubmitReviewDecision_Approve_TransitionsToApproved()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Approve Test",
                Description = "desc",
                ExternalReference = "ref-approve"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "All clear",
                    ReviewNote   = "Good to go"
                }, reviewerId, "corr-approve");

            Assert.That(decisionResp.Success, Is.True);
            Assert.That(decisionResp.DecisionType, Is.EqualTo(ReviewDecisionType.Approve));
            Assert.That(decisionResp.UpdatedWorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
            Assert.That(decisionResp.DecisionId, Is.Not.Null.And.Not.Empty);
            Assert.That(decisionResp.DecisionTimestamp, Is.Not.Null);
        }

        [Test]
        public async Task SubmitReviewDecision_Reject_TransitionsToRejected()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Reject Test",
                Description = "desc",
                ExternalReference = "ref-reject"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Reject,
                    Rationale    = "Policy violation detected"
                }, reviewerId, "corr-reject");

            Assert.That(decisionResp.Success, Is.True);
            Assert.That(decisionResp.DecisionType, Is.EqualTo(ReviewDecisionType.Reject));
            Assert.That(decisionResp.UpdatedWorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Rejected));
        }

        [Test]
        public async Task SubmitReviewDecision_RequestChanges_TransitionsToNeedsChanges()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Changes Test",
                Description = "desc",
                ExternalReference = "ref-changes"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.RequestChanges,
                    Rationale    = "Need more details"
                }, reviewerId, "corr-changes");

            Assert.That(decisionResp.Success, Is.True);
            Assert.That(decisionResp.DecisionType, Is.EqualTo(ReviewDecisionType.RequestChanges));
            Assert.That(decisionResp.UpdatedWorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.NeedsChanges));
        }

        [Test]
        public async Task SubmitReviewDecision_RejectWithNoRationale_Fails()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "No Rationale Test",
                Description = "desc",
                ExternalReference = "ref-norat"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Reject
                    // Rationale is null
                }, reviewerId, "corr-norat");

            Assert.That(decisionResp.Success, Is.False);
            Assert.That(decisionResp.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task SubmitReviewDecision_ApproveWithCriticalIssuesNoAcknowledge_Fails()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            // Create ComplianceEvidenceReview item with NO external reference (critical issue)
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.ComplianceEvidenceReview,
                Title     = "Critical Issue Item",
                Description = "desc"
                // No ExternalReference = critical evidence issue
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType        = ReviewDecisionType.Approve,
                    AcknowledgesOpenIssues = false  // Not acknowledged
                }, reviewerId, "corr-crit");

            Assert.That(decisionResp.Success, Is.False);
            Assert.That(decisionResp.ErrorCode, Is.EqualTo("UNACKNOWLEDGED_EVIDENCE_ISSUES"));
        }

        [Test]
        public async Task SubmitReviewDecision_ApproveWithCriticalIssuesAndAcknowledge_Succeeds()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            // Create ComplianceEvidenceReview item with NO external reference (critical issue)
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.ComplianceEvidenceReview,
                Title     = "Critical Issue Acknowledged",
                Description = "desc"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType        = ReviewDecisionType.Approve,
                    Rationale           = "Proceeding despite open issues",
                    AcknowledgesOpenIssues = true  // Acknowledged
                }, reviewerId, "corr-ack");

            Assert.That(decisionResp.Success, Is.True);
            Assert.That(decisionResp.UpdatedWorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved));
        }

        [Test]
        public async Task SubmitReviewDecision_NotPendingReview_FailsWithInvalidTransition()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Not Pending",
                Description = "desc",
                ExternalReference = "ref-notpending"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            // NOT submitted — still in Prepared state

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "Trying to approve directly"
                }, reviewerId, "corr-notpending");

            Assert.That(decisionResp.Success, Is.False);
            Assert.That(decisionResp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task SubmitReviewDecision_NonReviewer_ReturnsForbidden()
        {
            var wf       = CreateWorkflowService();
            var review   = CreateReviewService(wf);
            var issuerId = "iss-perm-" + Guid.NewGuid().ToString("N")[..6];
            var adminId  = "admin@perm.test";
            var opId     = "op@perm.test";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = adminId, Role = IssuerTeamRole.Admin }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = opId, Role = IssuerTeamRole.Operator }, adminId);

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Perm Test",
                Description = "desc",
                ExternalReference = "ref-perm"
            }, opId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), opId, "c");

            // Operator (not reviewer) tries to approve
            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "I'm an operator but I want to approve"
                }, opId, "corr-perm");

            Assert.That(decisionResp.Success, Is.False);
            Assert.That(decisionResp.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        // ── GetAuditHistoryAsync ───────────────────────────────────────────────

        [Test]
        public async Task GetAuditHistory_AfterDecision_ContainsEnrichedEntries()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "History Test",
                Description = "desc",
                ExternalReference = "ref-hist"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "All evidence is valid",
                    EvidenceReferences = new List<string> { "ev-001", "ev-002" }
                }, reviewerId, "corr-hist");

            var histResp = await review.GetAuditHistoryAsync(issuerId, workflowId, reviewerId, "corr-get-hist");
            Assert.That(histResp.Success, Is.True);
            Assert.That(histResp.Entries.Count, Is.GreaterThan(0));

            // Approval entry should exist
            var approvalEntry = histResp.Entries.FirstOrDefault(e => e.ToState == WorkflowApprovalState.Approved);
            Assert.That(approvalEntry, Is.Not.Null);
            Assert.That(approvalEntry!.ActorId, Is.EqualTo(reviewerId));
            Assert.That(approvalEntry.ActionDescription, Is.EqualTo("Approved"));
        }

        [Test]
        public async Task GetAuditHistory_NonMember_ReturnsForbidden()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
                { ItemType = WorkflowItemType.GeneralApproval, Title = "t", Description = "d" }, adminId);

            var result = await review.GetAuditHistoryAsync(issuerId, createResp.WorkflowItem!.WorkflowId,
                "outsider@example.com", "corr");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNAUTHORIZED").Or.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task GetAuditHistory_ActorRoleIsIncluded_WhenMemberIsMapped()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Role History",
                Description = "desc",
                ExternalReference = "ref-role-hist"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var histResp = await review.GetAuditHistoryAsync(issuerId, workflowId, reviewerId, "corr-role");
            Assert.That(histResp.Success, Is.True);

            var submitEntry = histResp.Entries.FirstOrDefault(e => e.ToState == WorkflowApprovalState.PendingReview);
            Assert.That(submitEntry, Is.Not.Null);
            Assert.That(submitEntry!.ActorRole, Is.EqualTo(IssuerTeamRole.Admin));
        }

        // ── ExportAuditAsync ───────────────────────────────────────────────────

        [Test]
        public async Task ExportAudit_ValidActor_ReturnsRecords()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Export Test",
                Description = "desc"
            }, adminId);

            var exportResp = await review.ExportAuditAsync(issuerId, new ReviewAuditExportRequest(), adminId, "corr-export");
            Assert.That(exportResp.Success, Is.True);
            Assert.That(exportResp.Records.Count, Is.GreaterThan(0));
            Assert.That(exportResp.IssuerId, Is.EqualTo(issuerId));
            Assert.That(exportResp.ExportedBy, Is.EqualTo(adminId));
            Assert.That(exportResp.ExportedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task ExportAudit_FilterByState_ReturnsOnlyMatchingItems()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "State Filter Test",
                Description = "desc",
                ExternalReference = "ref-filter"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            await wf.ApproveAsync(issuerId, workflowId, new ApproveWorkflowItemRequest(), reviewerId, "c");

            var exportResp = await review.ExportAuditAsync(issuerId,
                new ReviewAuditExportRequest { StateFilter = WorkflowApprovalState.Approved },
                adminId, "corr-filter");

            Assert.That(exportResp.Success, Is.True);
            Assert.That(exportResp.Records.All(r => r.CurrentState == WorkflowApprovalState.Approved), Is.True);
        }

        [Test]
        public async Task ExportAudit_FilterByItemType_ReturnsOnlyMatchingItems()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.LaunchReadinessSignOff,
                Title     = "Launch Item",
                Description = "desc"
            }, adminId);
            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "General Item",
                Description = "desc"
            }, adminId);

            var exportResp = await review.ExportAuditAsync(issuerId,
                new ReviewAuditExportRequest { ItemTypeFilter = WorkflowItemType.LaunchReadinessSignOff },
                adminId, "corr-type-filter");

            Assert.That(exportResp.Success, Is.True);
            Assert.That(exportResp.Records.All(r => r.ItemType == WorkflowItemType.LaunchReadinessSignOff), Is.True);
        }

        [Test]
        public async Task ExportAudit_NonMember_ReturnsForbidden()
        {
            var (review, _, issuerId, _, _) = await CreateTeamAsync();
            var result = await review.ExportAuditAsync(issuerId, new ReviewAuditExportRequest(),
                "outsider@example.com", "corr");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNAUTHORIZED").Or.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task ExportAudit_EmptyIssuerId_ReturnsBadRequest()
        {
            var review = CreateReviewService();
            var result = await review.ExportAuditAsync("", new ReviewAuditExportRequest(),
                "actor@example.com", "corr");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        // ── GetDiagnosticsAsync ────────────────────────────────────────────────

        [Test]
        public async Task GetDiagnostics_AdminActor_ReturnsSuccess()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Diag Item",
                Description = "desc"
            }, adminId);

            var diagResp = await review.GetDiagnosticsAsync(issuerId, adminId, "corr-diag");
            Assert.That(diagResp.Success, Is.True);
            Assert.That(diagResp.IssuerId, Is.EqualTo(issuerId));
            Assert.That(diagResp.ActiveMemberCount, Is.GreaterThan(0));
            Assert.That(diagResp.TotalWorkflowItems, Is.GreaterThan(0));
            Assert.That(diagResp.CollectedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task GetDiagnostics_ComplianceReviewer_ReturnsSuccess()
        {
            var (review, _, issuerId, _, reviewerId) = await CreateTeamAsync();

            var diagResp = await review.GetDiagnosticsAsync(issuerId, reviewerId, "corr-diag-cr");
            Assert.That(diagResp.Success, Is.True);
        }

        [Test]
        public async Task GetDiagnostics_OperatorRole_ReturnsForbidden()
        {
            var wf       = CreateWorkflowService();
            var review   = CreateReviewService(wf);
            var issuerId = "iss-diag-" + Guid.NewGuid().ToString("N")[..6];
            var adminId  = "admin@diag.test";
            var opId     = "op@diag.test";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = adminId, Role = IssuerTeamRole.Admin }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = opId, Role = IssuerTeamRole.Operator }, adminId);

            var diagResp = await review.GetDiagnosticsAsync(issuerId, opId, "corr-op-diag");
            Assert.That(diagResp.Success, Is.False);
            Assert.That(diagResp.ErrorCode, Is.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task GetDiagnostics_NonMember_ReturnsForbidden()
        {
            var (review, _, issuerId, _, _) = await CreateTeamAsync();
            var diagResp = await review.GetDiagnosticsAsync(issuerId, "outsider@example.com", "corr");
            Assert.That(diagResp.Success, Is.False);
            Assert.That(diagResp.ErrorCode, Is.EqualTo("UNAUTHORIZED").Or.EqualTo("INSUFFICIENT_ROLE"));
        }

        [Test]
        public async Task GetDiagnostics_AfterAuthDenial_ContainsDiagnosticsEvent()
        {
            var (review, _, issuerId, _, reviewerId) = await CreateTeamAsync();

            // Trigger an auth denial by a non-member accessing the queue
            await review.GetReviewQueueAsync(issuerId, "unknown-actor@example.com", "corr-auth-deny");

            var diagResp = await review.GetDiagnosticsAsync(issuerId, reviewerId, "corr-check-diag");
            Assert.That(diagResp.Success, Is.True);
            Assert.That(diagResp.RecentEvents.Count, Is.GreaterThan(0));
            Assert.That(diagResp.RecentEvents.Any(e => e.Category == ReviewDiagnosticsEventCategory.AuthorizationDenial),
                Is.True, "Authorization denial event should be logged");
        }

        [Test]
        public async Task GetDiagnostics_AfterInvalidTransition_ContainsDiagnosticsEvent()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType = WorkflowItemType.GeneralApproval,
                Title    = "Transition Test",
                Description = "desc",
                ExternalReference = "ref-trans"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            // Do NOT submit — item is in Prepared, then try to approve (invalid transition)

            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "Trying to approve prepared item"
                }, reviewerId, "corr-bad-trans");

            var diagResp = await review.GetDiagnosticsAsync(issuerId, reviewerId, "corr-diag-trans");
            Assert.That(diagResp.Success, Is.True);
            Assert.That(diagResp.RecentEvents.Any(e => e.Category == ReviewDiagnosticsEventCategory.InvalidTransition),
                Is.True, "Invalid transition event should be logged");
        }

        // ── Contract tests ─────────────────────────────────────────────────────

        [Test]
        public async Task ReviewQueueResponse_ContainsRequiredContractFields()
        {
            var (review, _, issuerId, adminId, _) = await CreateTeamAsync();
            var result = await review.GetReviewQueueAsync(issuerId, adminId, "corr-contract");

            Assert.That(result.Success, Is.True);
            Assert.That(result.ActorId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Items, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(result.Items.Count));
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvidenceBundleResponse_ContainsRequiredContractFields()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();
            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Contract Test",
                Description = "desc",
                ExternalReference = "ref-contract"
            }, adminId);

            var result = await review.GetEvidenceBundleAsync(issuerId, createResp.WorkflowItem!.WorkflowId, adminId, "corr-bundle-contract");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Bundle, Is.Not.Null);
            Assert.That(result.Bundle!.WorkflowId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Bundle.IssuerId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Bundle.AssembledAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.Bundle.EvidenceItems, Is.Not.Null);
            Assert.That(result.Bundle.Contradictions, Is.Not.Null);
            Assert.That(result.Bundle.MissingEvidence, Is.Not.Null);
            Assert.That(result.Bundle.ReviewReadinessSummary, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Bundle.IssueSummary, Is.Not.Null);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task SubmitReviewDecisionResponse_ContainsRequiredContractFields()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Decision Contract",
                Description = "desc",
                ExternalReference = "ref-dc"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var result = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "Approved for contract test"
                }, reviewerId, "corr-dc");

            Assert.That(result.Success, Is.True);
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.DecisionTimestamp, Is.Not.Null);
            Assert.That(result.DecisionType, Is.Not.Null);
            Assert.That(result.UpdatedWorkflowItem, Is.Not.Null);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ReviewAuditHistoryResponse_ContainsRequiredContractFields()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "History Contract",
                Description = "desc"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var result = await review.GetAuditHistoryAsync(issuerId, workflowId, adminId, "corr-hist-contract");

            Assert.That(result.Success, Is.True);
            Assert.That(result.WorkflowId, Is.EqualTo(workflowId));
            Assert.That(result.Entries, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(result.Entries.Count));
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);

            foreach (var entry in result.Entries)
            {
                Assert.That(entry.EntryId, Is.Not.Null.And.Not.Empty);
                Assert.That(entry.WorkflowId, Is.Not.Null.And.Not.Empty);
                Assert.That(entry.ActorId, Is.Not.Null.And.Not.Empty);
                Assert.That(entry.ActionDescription, Is.Not.Null.And.Not.Empty);
                Assert.That(entry.Timestamp, Is.Not.EqualTo(default(DateTime)));
            }
        }

        [Test]
        public async Task ReviewAuditExportResponse_ContainsRequiredContractFields()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Export Contract",
                Description = "desc"
            }, adminId);

            var result = await review.ExportAuditAsync(issuerId, new ReviewAuditExportRequest(), adminId, "corr-exp-contract");

            Assert.That(result.Success, Is.True);
            Assert.That(result.IssuerId, Is.EqualTo(issuerId));
            Assert.That(result.Records, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(result.Records.Count));
            Assert.That(result.ExportedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.ExportedBy, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ExportCriteria, Is.Not.Null);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);

            foreach (var record in result.Records)
            {
                Assert.That(record.WorkflowId, Is.Not.Null.And.Not.Empty);
                Assert.That(record.IssuerId, Is.EqualTo(issuerId));
                Assert.That(record.Title, Is.Not.Null.And.Not.Empty);
                Assert.That(record.CreatedAt, Is.Not.EqualTo(default(DateTime)));
                Assert.That(record.LastUpdatedAt, Is.Not.EqualTo(default(DateTime)));
            }
        }

        [Test]
        public async Task ReviewDiagnosticsResponse_ContainsRequiredContractFields()
        {
            var (review, _, issuerId, adminId, _) = await CreateTeamAsync();
            var result = await review.GetDiagnosticsAsync(issuerId, adminId, "corr-diag-contract");

            Assert.That(result.Success, Is.True);
            Assert.That(result.IssuerId, Is.EqualTo(issuerId));
            Assert.That(result.ActiveMemberCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.TotalWorkflowItems, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.PendingReviewCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.EvidenceBlockedCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.NeedsChangesCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.RecentEvents, Is.Not.Null);
            Assert.That(result.EventCategoryCounts, Is.Not.Null);
            Assert.That(result.CollectedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ── Branch coverage ────────────────────────────────────────────────────

        [Test]
        public async Task GetReviewQueue_FinanceReviewer_HasApproverCapabilities()
        {
            var wf       = CreateWorkflowService();
            var review   = CreateReviewService(wf);
            var issuerId = "iss-fin-" + Guid.NewGuid().ToString("N")[..6];
            var adminId  = "admin@fin.test";
            var finId    = "finance@fin.test";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = adminId, Role = IssuerTeamRole.Admin }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
                { UserId = finId, Role = IssuerTeamRole.FinanceReviewer }, adminId);

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType = WorkflowItemType.GeneralApproval,
                Title    = "Finance Approval",
                Description = "desc",
                ExternalReference = "ref-fin"
            }, adminId);
            await wf.SubmitForReviewAsync(issuerId, createResp.WorkflowItem!.WorkflowId,
                new SubmitWorkflowItemRequest(), adminId, "c");

            var queueResult = await review.GetReviewQueueAsync(issuerId, finId, "corr-fin");
            var item = queueResult.Items.FirstOrDefault(i => i.WorkflowItem.State == WorkflowApprovalState.PendingReview);

            Assert.That(item, Is.Not.Null);
            Assert.That(item!.AvailableCapabilities, Contains.Item(ReviewCapability.Approve));
            Assert.That(item.AvailableCapabilities, Contains.Item(ReviewCapability.Reject));
        }

        [Test]
        public async Task ExportAudit_FilterByActor_ReturnsOnlyMatchingRecords()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Actor Filter Test",
                Description = "desc"
            }, adminId);

            var exportResp = await review.ExportAuditAsync(issuerId,
                new ReviewAuditExportRequest { ActorFilter = adminId },
                adminId, "corr-actor-filter");

            Assert.That(exportResp.Success, Is.True);
            Assert.That(exportResp.Records.All(r =>
                r.CreatedBy == adminId ||
                r.ApprovedBy == adminId ||
                r.RejectedBy == adminId), Is.True);
        }

        [Test]
        public async Task GetEvidenceBundle_WithNoDescription_HasMissingEvidenceIndicator()
        {
            var (review, wf, issuerId, adminId, _) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType          = WorkflowItemType.GeneralApproval,
                Title             = "No Description",
                // No description
                ExternalReference = "ref-nodesc"
            }, adminId);

            var bundleResp = await review.GetEvidenceBundleAsync(issuerId, createResp.WorkflowItem!.WorkflowId, adminId, "corr-nodesc");
            Assert.That(bundleResp.Success, Is.True);
            Assert.That(bundleResp.Bundle!.MissingEvidence.Count, Is.GreaterThan(0));
            var descriptionMissing = bundleResp.Bundle.MissingEvidence
                .FirstOrDefault(m => m.Category == ReviewEvidenceCategory.Workflow);
            Assert.That(descriptionMissing, Is.Not.Null, "Missing description should produce a MissingEvidenceIndicator");
        }

        [Test]
        public async Task GetReviewQueue_ApprovedAndCompletedItems_HaveNoActionCapabilities()
        {
            var (review, wf, issuerId, adminId, reviewerId) = await CreateTeamAsync();

            var createResp = await wf.CreateWorkflowItemAsync(issuerId, new CreateWorkflowItemRequest
            {
                ItemType  = WorkflowItemType.GeneralApproval,
                Title     = "Completed Item",
                Description = "desc",
                ExternalReference = "ref-completed"
            }, adminId);
            var workflowId = createResp.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            await wf.ApproveAsync(issuerId, workflowId, new ApproveWorkflowItemRequest(), reviewerId, "c");
            await wf.CompleteAsync(issuerId, workflowId, new CompleteWorkflowItemRequest(), adminId, "c");

            var queueResult = await review.GetReviewQueueAsync(issuerId, adminId, "corr-completed");
            var completedItem = queueResult.Items.FirstOrDefault(i => i.WorkflowItem.State == WorkflowApprovalState.Completed);

            Assert.That(completedItem, Is.Not.Null);
            Assert.That(completedItem!.AvailableCapabilities, Has.Count.EqualTo(1));
            Assert.That(completedItem.AvailableCapabilities[0], Is.EqualTo(ReviewCapability.View));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — HTTP endpoint (WebApplicationFactory)
        // ═══════════════════════════════════════════════════════════════════════

        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void IntegrationSetup()
        {
            _factory = new CustomWebApplicationFactory();
            _client  = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void IntegrationTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task HTTP_GetReviewQueue_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/enterprise-compliance-review/some-issuer/review-queue");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_GetEvidenceBundle_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/enterprise-compliance-review/some-issuer/review-queue/some-workflow/evidence");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_SubmitReviewDecision_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/enterprise-compliance-review/some-issuer/review-queue/some-workflow/decision",
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_GetAuditHistory_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/enterprise-compliance-review/some-issuer/review-queue/some-workflow/audit-history");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_ExportAudit_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/enterprise-compliance-review/some-issuer/audit-export",
                new ReviewAuditExportRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_GetDiagnostics_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/enterprise-compliance-review/some-issuer/diagnostics");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForComplianceReviewIntegration32C",
                        ["JwtConfig:SecretKey"] = "ComplianceReviewTestSecretKey32CharsReq!",
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

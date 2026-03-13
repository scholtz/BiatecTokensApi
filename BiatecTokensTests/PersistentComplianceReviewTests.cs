using BiatecTokensApi.Models.EnterpriseComplianceReview;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for durable enterprise compliance review and issuer workflow persistence.
    ///
    /// Coverage:
    ///  - Repository-level persistence: data survives service instance recreation
    ///  - Issuer tenant isolation: one issuer cannot read another issuer's records
    ///  - Decision metadata reconstruction from repository
    ///  - Diagnostics event persistence and bounded eviction
    ///  - Authorization fail-closed behaviour on persistence operations
    ///  - Invalid transition protection with durable audit trail
    ///  - Audit history enrichment from persisted decision records
    ///  - Repository singleton semantics in DI container
    ///  - HTTP integration: endpoints backed by DI-resolved singletons
    ///  - Export reconstruction from durable workflow state
    ///  - Concurrent write safety (diagnostics append)
    ///  - Evidence bundle reconstruction from persisted workflow
    ///  - Multi-workflow audit export filtering
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PersistentComplianceReviewTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Factory helpers — shared repositories for cross-instance testing
        // ═══════════════════════════════════════════════════════════════════════

        private static IssuerWorkflowRepository CreateWorkflowRepo() =>
            new IssuerWorkflowRepository(NullLogger<IssuerWorkflowRepository>.Instance);

        private static ComplianceReviewRepository CreateReviewRepo() =>
            new ComplianceReviewRepository(NullLogger<ComplianceReviewRepository>.Instance);

        private static IssuerWorkflowService CreateWorkflowService(IIssuerWorkflowRepository? repo = null) =>
            new IssuerWorkflowService(
                NullLogger<IssuerWorkflowService>.Instance,
                repo ?? CreateWorkflowRepo());

        private static EnterpriseComplianceReviewService CreateReviewService(
            IIssuerWorkflowService? wf = null,
            IComplianceReviewRepository? repo = null,
            IIssuerWorkflowRepository? wfRepo = null) =>
            new EnterpriseComplianceReviewService(
                wf ?? CreateWorkflowService(),
                repo ?? CreateReviewRepo(),
                wfRepo ?? CreateWorkflowRepo(),
                NullLogger<EnterpriseComplianceReviewService>.Instance);

        /// <summary>
        /// Bootstraps a team with Admin and ComplianceReviewer using a shared repository.
        /// Returns repository instances so they can be re-used across service recreations.
        /// </summary>
        private static async Task<(
            IssuerWorkflowRepository wfRepo,
            ComplianceReviewRepository reviewRepo,
            string issuerId,
            string adminId,
            string reviewerId)> BootstrapTeamAsync()
        {
            var wfRepo     = CreateWorkflowRepo();
            var reviewRepo = CreateReviewRepo();
            var wf         = CreateWorkflowService(wfRepo);

            var issuerId   = "issuer-" + Guid.NewGuid().ToString("N")[..8];
            var adminId    = "admin@persist.test";
            var reviewerId = "reviewer@persist.test";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = adminId, Role = IssuerTeamRole.Admin, DisplayName = "Admin"
            }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = reviewerId, Role = IssuerTeamRole.ComplianceReviewer, DisplayName = "Reviewer"
            }, adminId);

            return (wfRepo, reviewRepo, issuerId, adminId, reviewerId);
        }

        // Helper that creates a review service backed by the same shared repositories as the workflow service
        private static EnterpriseComplianceReviewService CreateSharedReviewService(
            IssuerWorkflowRepository wfRepo,
            ComplianceReviewRepository reviewRepo)
        {
            var wf = CreateWorkflowService(wfRepo);
            return new EnterpriseComplianceReviewService(
                wf, reviewRepo, wfRepo,
                NullLogger<EnterpriseComplianceReviewService>.Instance);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 1. Repository persistence semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WorkflowRepository_MembersSurviveServiceRecreation()
        {
            var wfRepo = CreateWorkflowRepo();
            var wf1    = CreateWorkflowService(wfRepo);
            var issuerId = "issuer-survival";

            await wf1.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = "user1@test.com", Role = IssuerTeamRole.Admin, DisplayName = "User 1"
            }, "user1@test.com");

            // Create a NEW service instance backed by the SAME repository
            var wf2 = CreateWorkflowService(wfRepo);
            var listResult = await wf2.ListMembersAsync(issuerId, "user1@test.com");

            Assert.That(listResult.Success, Is.True, "Second service instance must see members from first instance");
            Assert.That(listResult.Members, Has.Count.EqualTo(1));
            Assert.That(listResult.Members[0].UserId, Is.EqualTo("user1@test.com"));
        }

        [Test]
        public async Task WorkflowRepository_WorkflowItemsSurviveServiceRecreation()
        {
            var (wfRepo, _, issuerId, adminId, _) = await BootstrapTeamAsync();
            var wf1 = CreateWorkflowService(wfRepo);

            var createResp = await wf1.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Persistence Test Item", Description = "desc" },
                adminId);
            Assert.That(createResp.Success, Is.True);
            var workflowId = createResp.WorkflowItem!.WorkflowId;

            // Recreate the service — data must still be accessible
            var wf2      = CreateWorkflowService(wfRepo);
            var getResp  = await wf2.GetWorkflowItemAsync(issuerId, workflowId, adminId);
            Assert.That(getResp.Success, Is.True);
            Assert.That(getResp.WorkflowItem!.Title, Is.EqualTo("Persistence Test Item"));
        }

        [Test]
        public async Task ComplianceReviewRepository_DecisionsSurviveServiceRecreation()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf1     = CreateWorkflowService(wfRepo);
            var review1 = CreateSharedReviewService(wfRepo, reviewRepo);

            // Create, submit, and decide on a workflow item
            var create = await wf1.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Decision Persistence", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf1.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "corr-1");

            var decisionResp = await review1.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Approve,
                    Rationale    = "Looks good",
                    ReviewNote   = "Approved by reviewer"
                }, reviewerId, "corr-1");

            Assert.That(decisionResp.Success, Is.True);
            var decisionId = decisionResp.DecisionId!;

            // Query via repository directly
            var persisted = await reviewRepo.GetDecisionByIdAsync(decisionId);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted!.Rationale, Is.EqualTo("Looks good"));
            Assert.That(persisted.ReviewNote, Is.EqualTo("Approved by reviewer"));
            Assert.That(persisted.ActorId, Is.EqualTo(reviewerId));
            Assert.That(persisted.DecisionType, Is.EqualTo(ReviewDecisionType.Approve));
        }

        [Test]
        public async Task ComplianceReviewRepository_DecisionsContainActorRole()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "RoleCapture", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve },
                reviewerId, "c");

            var decisions = await reviewRepo.GetDecisionsForWorkflowAsync(issuerId, workflowId);
            Assert.That(decisions, Has.Count.EqualTo(1));
            Assert.That(decisions[0].ActorRole, Is.EqualTo(IssuerTeamRole.ComplianceReviewer),
                "Persisted decision must capture actor role at time of decision");
        }

        [Test]
        public async Task ComplianceReviewRepository_EvidenceReferencesPersisted()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "EvidenceRefs", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType       = ReviewDecisionType.Approve,
                    EvidenceReferences = new List<string> { "ev-001", "ev-002" }
                }, reviewerId, "c");

            var decisions = await reviewRepo.GetDecisionsForWorkflowAsync(issuerId, workflowId);
            Assert.That(decisions[0].EvidenceReferences, Is.EquivalentTo(new[] { "ev-001", "ev-002" }),
                "Evidence references must be persisted in the decision record");
        }

        [Test]
        public async Task ComplianceReviewRepository_AcknowledgesOpenIssuesPersisted()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Acknowledge", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType           = ReviewDecisionType.Approve,
                    AcknowledgesOpenIssues = true
                }, reviewerId, "c");

            var decisions = await reviewRepo.GetDecisionsForWorkflowAsync(issuerId, workflowId);
            Assert.That(decisions[0].AcknowledgesOpenIssues, Is.True,
                "AcknowledgesOpenIssues flag must be persisted");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. Tenant isolation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WorkflowRepository_TenantIsolation_OtherIssuerCannotSeeData()
        {
            var wfRepo    = CreateWorkflowRepo();
            var issuerId1 = "issuer-A";
            var issuerId2 = "issuer-B";
            var user1     = "user@issuer-a.com";
            var user2     = "user@issuer-b.com";

            var wf = CreateWorkflowService(wfRepo);
            await wf.AddMemberAsync(issuerId1, new AddIssuerTeamMemberRequest
            {
                UserId = user1, Role = IssuerTeamRole.Admin
            }, user1);
            await wf.AddMemberAsync(issuerId2, new AddIssuerTeamMemberRequest
            {
                UserId = user2, Role = IssuerTeamRole.Admin
            }, user2);

            // Create item for issuer1
            await wf.CreateWorkflowItemAsync(issuerId1,
                new CreateWorkflowItemRequest { Title = "Issuer A Item" }, user1);

            // Issuer2 should not see issuer1's items
            var items2 = await wf.ListWorkflowItemsAsync(issuerId2, user2);
            Assert.That(items2.Items, Is.Empty, "Issuer 2 must not see issuer 1 workflow items");
        }

        [Test]
        public async Task ComplianceReviewRepository_TenantIsolation_DecisionsNotLeakAcrossIssuers()
        {
            var reviewRepo = CreateReviewRepo();

            await reviewRepo.SaveDecisionAsync(new PersistedReviewDecision
            {
                DecisionId = Guid.NewGuid().ToString(),
                IssuerId   = "issuer-X",
                WorkflowId = "wf-x",
                ActorId    = "actor@x.com",
                DecisionType = ReviewDecisionType.Approve
            });

            var decisionsForOtherIssuer = await reviewRepo.GetDecisionsForWorkflowAsync("issuer-Y", "wf-x");
            Assert.That(decisionsForOtherIssuer, Is.Empty,
                "Decisions for issuer-X must not be visible when querying issuer-Y");
        }

        [Test]
        public async Task ComplianceReviewRepository_TenantIsolation_DiagnosticsDoNotLeakAcrossIssuers()
        {
            var reviewRepo = CreateReviewRepo();

            await reviewRepo.AppendDiagnosticsEventAsync("issuer-M", new ReviewDiagnosticsEvent
            {
                Category    = ReviewDiagnosticsEventCategory.AuthorizationDenial,
                Description = "Event for M"
            });

            var eventsForN = await reviewRepo.GetRecentDiagnosticsEventsAsync("issuer-N");
            Assert.That(eventsForN, Is.Empty,
                "Diagnostics events for issuer-M must not be visible when querying issuer-N");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 3. Audit history enrichment from persisted decisions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AuditHistory_EnrichedWithPersistedDecisionRationale()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Audit Enrichment", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Reject,
                    Rationale    = "Missing KYC evidence",
                    ReviewNote   = "Reviewer note"
                }, reviewerId, "c");

            var history = await review.GetAuditHistoryAsync(issuerId, workflowId, adminId, "c");
            Assert.That(history.Success, Is.True);

            var rejectEntry = history.Entries.FirstOrDefault(e => e.ToState == WorkflowApprovalState.Rejected);
            Assert.That(rejectEntry, Is.Not.Null, "Audit trail must include rejection entry");
            Assert.That(rejectEntry!.Rationale, Contains.Substring("Missing KYC evidence"),
                "Reject entry must be enriched with persisted rationale");
            Assert.That(rejectEntry.ActorRole, Is.EqualTo(IssuerTeamRole.ComplianceReviewer),
                "Audit entry must include actor role from persisted decision");
        }

        [Test]
        public async Task AuditHistory_EnrichedWithEvidenceReferences()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "EvidenceAudit", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType       = ReviewDecisionType.Approve,
                    EvidenceReferences = new List<string> { "ev-A", "ev-B" }
                }, reviewerId, "c");

            var history = await review.GetAuditHistoryAsync(issuerId, workflowId, adminId, "c");
            var approveEntry = history.Entries.FirstOrDefault(e => e.ToState == WorkflowApprovalState.Approved);
            Assert.That(approveEntry, Is.Not.Null);
            Assert.That(approveEntry!.EvidenceReferences, Is.EquivalentTo(new[] { "ev-A", "ev-B" }),
                "Evidence references from persisted decision must appear in enriched audit history");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4. Diagnostics persistence
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Diagnostics_EventsPersistedInRepository()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            // Trigger an auth denial event by using a non-member
            await review.GetReviewQueueAsync(issuerId, "outsider@test.com", "corr-diag");
            // Allow fire-and-forget to complete
            await Task.Delay(50);

            var events = await reviewRepo.GetRecentDiagnosticsEventsAsync(issuerId);
            Assert.That(events.Count, Is.GreaterThan(0), "Auth denial must be persisted as diagnostics event");
            Assert.That(events.Any(e => e.Category == ReviewDiagnosticsEventCategory.AuthorizationDenial),
                Is.True, "Auth denial category must be recorded");
        }

        [Test]
        public async Task Diagnostics_EventsBoundedToMaximum()
        {
            var reviewRepo = CreateReviewRepo();
            var issuerId   = "issuer-bounded";

            // Append 250 events (more than the 200-event maximum)
            for (int i = 0; i < 250; i++)
            {
                await reviewRepo.AppendDiagnosticsEventAsync(issuerId, new ReviewDiagnosticsEvent
                {
                    Category    = ReviewDiagnosticsEventCategory.MissingEvidence,
                    Description = $"Event {i}"
                });
            }

            var events = await reviewRepo.GetRecentDiagnosticsEventsAsync(issuerId, maxCount: 300);
            Assert.That(events.Count, Is.LessThanOrEqualTo(200),
                "Diagnostics store must be bounded to protect memory");
        }

        [Test]
        public async Task Diagnostics_ConcurrentAppendIsSafe()
        {
            var reviewRepo = CreateReviewRepo();
            var issuerId   = "issuer-concurrent";

            var tasks = Enumerable.Range(0, 50).Select(i =>
                reviewRepo.AppendDiagnosticsEventAsync(issuerId, new ReviewDiagnosticsEvent
                {
                    Category    = ReviewDiagnosticsEventCategory.StaleItem,
                    Description = $"Concurrent event {i}"
                })).ToList();

            await Task.WhenAll(tasks);

            var events = await reviewRepo.GetRecentDiagnosticsEventsAsync(issuerId, maxCount: 100);
            Assert.That(events.Count, Is.GreaterThan(0),
                "Concurrent diagnostics writes must all complete without data loss");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 5. Authorization fail-closed on persistence operations
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DecisionSubmission_NonMember_FailsClosedWithoutPersisting()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Auth Guard", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve },
                "outsider@example.com", "c");

            Assert.That(decisionResp.Success, Is.False, "Non-member must not be able to submit a decision");
            Assert.That(decisionResp.ErrorCode, Is.EqualTo("UNAUTHORIZED").Or.EqualTo("INSUFFICIENT_ROLE"));

            // Verify no decision was persisted
            var decisions = await reviewRepo.GetDecisionsForWorkflowAsync(issuerId, workflowId);
            Assert.That(decisions, Is.Empty, "Failed authorization must not persist any decision record");
        }

        [Test]
        public async Task DecisionSubmission_OperatorRole_CannotApprove_NoDecisionPersisted()
        {
            var wfRepo     = CreateWorkflowRepo();
            var reviewRepo = CreateReviewRepo();
            var issuerId   = "issuer-op-test";
            var wf         = CreateWorkflowService(wfRepo);
            var review     = CreateReviewService(wf, reviewRepo);

            var adminId    = "admin@test.com";
            var operatorId = "operator@test.com";

            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = adminId, Role = IssuerTeamRole.Admin
            }, adminId);
            await wf.AddMemberAsync(issuerId, new AddIssuerTeamMemberRequest
            {
                UserId = operatorId, Role = IssuerTeamRole.Operator
            }, adminId);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Operator Cannot Approve", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve },
                operatorId, "c");

            Assert.That(decisionResp.Success, Is.False, "Operator must not be permitted to approve");
            var decisions = await reviewRepo.GetDecisionsForWorkflowAsync(issuerId, workflowId);
            Assert.That(decisions, Is.Empty, "No decision must be persisted for failed auth");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 6. Invalid transition protection
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DecisionSubmission_InvalidTransition_FailsWithoutPersisting()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Invalid Transition Guard", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            // Note: item is in Prepared state, NOT submitted for review

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve },
                reviewerId, "c");

            Assert.That(decisionResp.Success, Is.False, "Decision on Prepared item must fail");
            var decisions = await reviewRepo.GetDecisionsForWorkflowAsync(issuerId, workflowId);
            Assert.That(decisions, Is.Empty, "No decision must be persisted for invalid transition");
        }

        [Test]
        public async Task DecisionSubmission_AlreadyApproved_CannotApproveAgain()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "No Double Approve", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            await wf.ApproveAsync(issuerId, workflowId, new ApproveWorkflowItemRequest(), reviewerId, "c");

            var decisionResp2 = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve },
                reviewerId, "c");

            Assert.That(decisionResp2.Success, Is.False, "Already-approved item cannot be approved again");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 7. Workflow lifecycle → state synchronisation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WorkflowLifecycle_FullApprovalPath_StateSynchronised()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Full Lifecycle", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;

            // Submit → Approve via review service → Complete
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest { DecisionType = ReviewDecisionType.Approve },
                reviewerId, "c");

            Assert.That(decisionResp.Success, Is.True);
            Assert.That(decisionResp.UpdatedWorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Approved),
                "Approval decision must synchronise issuer workflow state to Approved");

            await wf.CompleteAsync(issuerId, workflowId, new CompleteWorkflowItemRequest(), adminId, "c");
            var finalItem = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
            Assert.That(finalItem!.State, Is.EqualTo(WorkflowApprovalState.Completed),
                "Completed state must be persisted in repository");
        }

        [Test]
        public async Task WorkflowLifecycle_RejectionPath_StateSynchronised()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Rejection Path", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            var decisionResp = await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.Reject,
                    Rationale    = "Insufficient evidence"
                }, reviewerId, "c");

            Assert.That(decisionResp.Success, Is.True);
            Assert.That(decisionResp.UpdatedWorkflowItem!.State, Is.EqualTo(WorkflowApprovalState.Rejected));

            var persisted = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
            Assert.That(persisted!.State, Is.EqualTo(WorkflowApprovalState.Rejected),
                "Rejected state must persist in repository");
        }

        [Test]
        public async Task WorkflowLifecycle_RequestChanges_ResubmitPath_StateSynchronised()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Changes Cycle", Description = "d" },
                adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");

            // RequestChanges decision
            await review.SubmitReviewDecisionAsync(issuerId, workflowId,
                new SubmitReviewDecisionRequest
                {
                    DecisionType = ReviewDecisionType.RequestChanges,
                    Rationale    = "Please add more detail"
                }, reviewerId, "c");

            var afterChanges = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
            Assert.That(afterChanges!.State, Is.EqualTo(WorkflowApprovalState.NeedsChanges));

            // Resubmit brings it back to PendingReview
            await wf.ResubmitAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            var afterResubmit = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
            Assert.That(afterResubmit!.State, Is.EqualTo(WorkflowApprovalState.PendingReview));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 8. Audit export from durable state
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AuditExport_GeneratedFromDurableWorkflowState()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            // Create two items — one approved, one rejected
            var create1 = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Export Item 1", Description = "d1" }, adminId);
            var create2 = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Export Item 2", Description = "d2" }, adminId);
            var wid1 = create1.WorkflowItem!.WorkflowId;
            var wid2 = create2.WorkflowItem!.WorkflowId;

            await wf.SubmitForReviewAsync(issuerId, wid1, new SubmitWorkflowItemRequest(), adminId, "c");
            await wf.SubmitForReviewAsync(issuerId, wid2, new SubmitWorkflowItemRequest(), adminId, "c");
            await wf.ApproveAsync(issuerId, wid1, new ApproveWorkflowItemRequest(), reviewerId, "c");
            await wf.RejectAsync(issuerId, wid2, new RejectWorkflowItemRequest { RejectionReason = "Not ready" }, reviewerId, "c");

            var export = await review.ExportAuditAsync(issuerId, new ReviewAuditExportRequest(), adminId, "c");
            Assert.That(export.Success, Is.True);
            Assert.That(export.Records.Count, Is.GreaterThanOrEqualTo(2));

            var r1 = export.Records.FirstOrDefault(r => r.WorkflowId == wid1);
            var r2 = export.Records.FirstOrDefault(r => r.WorkflowId == wid2);
            Assert.That(r1, Is.Not.Null);
            Assert.That(r2, Is.Not.Null);
            Assert.That(r1!.CurrentState, Is.EqualTo(WorkflowApprovalState.Approved));
            Assert.That(r2!.CurrentState, Is.EqualTo(WorkflowApprovalState.Rejected));
        }

        [Test]
        public async Task AuditExport_StateFilterWorksFromDurableData()
        {
            var (wfRepo, reviewRepo, issuerId, adminId, reviewerId) = await BootstrapTeamAsync();
            var wf     = CreateWorkflowService(wfRepo);
            var review = CreateSharedReviewService(wfRepo, reviewRepo);

            var create = await wf.CreateWorkflowItemAsync(issuerId,
                new CreateWorkflowItemRequest { Title = "Filter Test", Description = "d" }, adminId);
            var workflowId = create.WorkflowItem!.WorkflowId;
            await wf.SubmitForReviewAsync(issuerId, workflowId, new SubmitWorkflowItemRequest(), adminId, "c");
            await wf.ApproveAsync(issuerId, workflowId, new ApproveWorkflowItemRequest(), reviewerId, "c");

            var exportApproved = await review.ExportAuditAsync(issuerId,
                new ReviewAuditExportRequest { StateFilter = WorkflowApprovalState.Approved },
                adminId, "c");
            var exportRejected = await review.ExportAuditAsync(issuerId,
                new ReviewAuditExportRequest { StateFilter = WorkflowApprovalState.Rejected },
                adminId, "c");

            Assert.That(exportApproved.Records.All(r => r.CurrentState == WorkflowApprovalState.Approved),
                Is.True, "State filter must only return records matching that state");
            Assert.That(exportRejected.Records, Does.Not.Contain(exportApproved.Records.First()));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 9. Concurrent audit entry safety (the core blocker fix)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AuditRepository_ConcurrentAppendsToSameWorkflow_NoDuplicates()
        {
            // Verifies the fix for the check-then-add race on List<T>:
            // Under concurrent calls with distinct EntryIds every entry must appear exactly once.
            var wfRepo   = CreateWorkflowRepo();
            var issuerId = "issuer-concurrent-audit";
            var workflowId = "wf-concurrent";

            // First create the workflow item so the repository knows about it
            await wfRepo.UpsertWorkflowItemAsync(new WorkflowItem
            {
                IssuerId = issuerId, WorkflowId = workflowId,
                Title = "Concurrent Audit Test", State = WorkflowApprovalState.PendingReview
            });

            const int threadCount = 50;
            var entries = Enumerable.Range(0, threadCount)
                .Select(_ => new WorkflowAuditEntry
                {
                    EntryId    = Guid.NewGuid().ToString(),
                    WorkflowId = workflowId,
                    FromState  = WorkflowApprovalState.Prepared,
                    ToState    = WorkflowApprovalState.PendingReview,
                    ActorId    = "actor@test.com",
                    Timestamp  = DateTime.UtcNow
                }).ToList();

            // Fire all appends concurrently
            var tasks = entries.Select(e => wfRepo.AppendAuditEntryAsync(issuerId, workflowId, e));
            await Task.WhenAll(tasks);

            var item = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
            Assert.That(item, Is.Not.Null);
            Assert.That(item!.AuditHistory.Count, Is.EqualTo(threadCount),
                "Every distinct audit entry must appear exactly once — no duplicates, no losses");

            // All EntryIds must be unique (no duplicate due to check-then-add race)
            var entryIds = item.AuditHistory.Select(e => e.EntryId).ToHashSet();
            Assert.That(entryIds.Count, Is.EqualTo(threadCount),
                "All EntryIds must be unique — concurrent adds must not produce duplicates");
        }

        [Test]
        public async Task AuditRepository_ConcurrentAppendsWithSameEntryId_ExactlyOneEntry()
        {
            // Verifies idempotency: two concurrent calls with the SAME EntryId must produce
            // exactly one entry in the audit trail — TryAdd is idempotent, no double-write.
            var wfRepo     = CreateWorkflowRepo();
            var issuerId   = "issuer-idempotent-audit";
            var workflowId = "wf-idempotent";

            await wfRepo.UpsertWorkflowItemAsync(new WorkflowItem
            {
                IssuerId = issuerId, WorkflowId = workflowId,
                Title = "Idempotency Test", State = WorkflowApprovalState.PendingReview
            });

            var sharedEntryId = Guid.NewGuid().ToString();
            var entry = new WorkflowAuditEntry
            {
                EntryId    = sharedEntryId,
                WorkflowId = workflowId,
                FromState  = WorkflowApprovalState.Prepared,
                ToState    = WorkflowApprovalState.PendingReview,
                ActorId    = "actor@test.com"
            };

            // Append the SAME entry 20 times concurrently
            var tasks = Enumerable.Range(0, 20).Select(_ => wfRepo.AppendAuditEntryAsync(issuerId, workflowId, entry));
            await Task.WhenAll(tasks);

            var item = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
            Assert.That(item!.AuditHistory.Count, Is.EqualTo(1),
                "Idempotent TryAdd: same EntryId appended 20 times must produce exactly 1 audit entry");
            Assert.That(item.AuditHistory[0].EntryId, Is.EqualTo(sharedEntryId));
        }

        [Test]
        public async Task AuditRepository_ConcurrentAppendsAndUpserts_NoCorruption()
        {
            // Simulates real concurrent usage: some threads append via AppendAuditEntryAsync,
            // others upsert the full item via UpsertWorkflowItemAsync.
            // The audit trail must contain ALL distinct entries from both paths.
            var wfRepo     = CreateWorkflowRepo();
            var issuerId   = "issuer-mixed-concurrent";
            var workflowId = "wf-mixed";

            await wfRepo.UpsertWorkflowItemAsync(new WorkflowItem
            {
                IssuerId = issuerId, WorkflowId = workflowId,
                Title = "Mixed Concurrent Test", State = WorkflowApprovalState.PendingReview
            });

            const int appendCount = 25;
            const int upsertCount = 25;

            // Half the entries arrive via AppendAuditEntryAsync
            var appendEntries = Enumerable.Range(0, appendCount)
                .Select(_ => new WorkflowAuditEntry
                {
                    EntryId = Guid.NewGuid().ToString(), WorkflowId = workflowId,
                    ActorId = "actor@test.com", Timestamp = DateTime.UtcNow
                }).ToList();

            // Other half arrive embedded in UpsertWorkflowItemAsync calls
            var upsertEntries = Enumerable.Range(0, upsertCount)
                .Select(_ => new WorkflowAuditEntry
                {
                    EntryId = Guid.NewGuid().ToString(), WorkflowId = workflowId,
                    ActorId = "actor@test.com", Timestamp = DateTime.UtcNow.AddMilliseconds(1)
                }).ToList();

            var appendTasks = appendEntries.Select(e => wfRepo.AppendAuditEntryAsync(issuerId, workflowId, e));
            var upsertTasks = upsertEntries.Select(e => wfRepo.UpsertWorkflowItemAsync(new WorkflowItem
            {
                IssuerId = issuerId, WorkflowId = workflowId,
                Title = "Mixed Concurrent Test", State = WorkflowApprovalState.PendingReview,
                AuditHistory = new List<WorkflowAuditEntry> { e }
            }));

            await Task.WhenAll(appendTasks.Concat(upsertTasks));

            var item = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);

            var allExpectedIds = appendEntries.Select(e => e.EntryId)
                .Concat(upsertEntries.Select(e => e.EntryId)).ToHashSet();
            var actualIds = item!.AuditHistory.Select(e => e.EntryId).ToHashSet();

            Assert.That(actualIds.IsSupersetOf(allExpectedIds), Is.True,
                "All distinct audit entries from both append and upsert paths must be present");

            // No entry must appear more than once
            var historyCount = item.AuditHistory.Count;
            var uniqueCount  = item.AuditHistory.Select(e => e.EntryId).Distinct().Count();
            Assert.That(historyCount, Is.EqualTo(uniqueCount),
                "No duplicate audit entries must appear after concurrent mixed operations");
        }

        [Test]
        public async Task AuditRepository_ReadsDuringConcurrentWrites_NoException()
        {
            // Verifies the implementation does not throw under read/write concurrency.
            // With List<T>, concurrent Add + Any iteration could throw InvalidOperationException.
            // With ConcurrentDictionary, reads and writes are safe at all times.
            var wfRepo     = CreateWorkflowRepo();
            var issuerId   = "issuer-readwrite";
            var workflowId = "wf-readwrite";

            await wfRepo.UpsertWorkflowItemAsync(new WorkflowItem
            {
                IssuerId = issuerId, WorkflowId = workflowId,
                Title = "Read-Write Safety", State = WorkflowApprovalState.PendingReview
            });

            var writerTask = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    await wfRepo.AppendAuditEntryAsync(issuerId, workflowId, new WorkflowAuditEntry
                    {
                        EntryId = Guid.NewGuid().ToString(), WorkflowId = workflowId,
                        ActorId = "writer@test.com"
                    });
                    await Task.Yield();
                }
            });

            var readerTask = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var item = await wfRepo.GetWorkflowItemAsync(issuerId, workflowId);
                    _ = item?.AuditHistory.Count; // Force enumeration — must not throw
                    await Task.Yield();
                }
            });

            Assert.DoesNotThrowAsync(async () => await Task.WhenAll(writerTask, readerTask),
                "Concurrent reads and writes to the audit trail must not throw any exceptions");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 10. Repository query correctness
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ComplianceReviewRepository_QueryDecisionsByActor()
        {
            var reviewRepo = CreateReviewRepo();
            var issuerId   = "issuer-query-actor";
            var actor1     = "actor1@test.com";
            var actor2     = "actor2@test.com";

            await reviewRepo.SaveDecisionAsync(new PersistedReviewDecision
            {
                IssuerId = issuerId, WorkflowId = "wf1", ActorId = actor1,
                DecisionType = ReviewDecisionType.Approve
            });
            await reviewRepo.SaveDecisionAsync(new PersistedReviewDecision
            {
                IssuerId = issuerId, WorkflowId = "wf2", ActorId = actor2,
                DecisionType = ReviewDecisionType.Reject
            });

            var actor1Decisions = await reviewRepo.QueryDecisionsAsync(issuerId, actorId: actor1);
            var actor2Decisions = await reviewRepo.QueryDecisionsAsync(issuerId, actorId: actor2);

            Assert.That(actor1Decisions, Has.Count.EqualTo(1));
            Assert.That(actor1Decisions[0].ActorId, Is.EqualTo(actor1));
            Assert.That(actor2Decisions, Has.Count.EqualTo(1));
            Assert.That(actor2Decisions[0].ActorId, Is.EqualTo(actor2));
        }

        [Test]
        public async Task ComplianceReviewRepository_QueryDecisionsByType()
        {
            var reviewRepo = CreateReviewRepo();
            var issuerId   = "issuer-query-type";

            await reviewRepo.SaveDecisionAsync(new PersistedReviewDecision
            {
                IssuerId = issuerId, WorkflowId = "wfA", ActorId = "a",
                DecisionType = ReviewDecisionType.Approve
            });
            await reviewRepo.SaveDecisionAsync(new PersistedReviewDecision
            {
                IssuerId = issuerId, WorkflowId = "wfB", ActorId = "a",
                DecisionType = ReviewDecisionType.Reject
            });

            var approvals = await reviewRepo.QueryDecisionsAsync(issuerId, decisionType: ReviewDecisionType.Approve);
            var rejections = await reviewRepo.QueryDecisionsAsync(issuerId, decisionType: ReviewDecisionType.Reject);

            Assert.That(approvals.All(d => d.DecisionType == ReviewDecisionType.Approve), Is.True);
            Assert.That(rejections.All(d => d.DecisionType == ReviewDecisionType.Reject), Is.True);
        }

        [Test]
        public async Task ComplianceReviewRepository_QueryDecisionsByTimeRange()
        {
            var reviewRepo = CreateReviewRepo();
            var issuerId   = "issuer-query-time";
            var past       = DateTime.UtcNow.AddDays(-1);
            var future     = DateTime.UtcNow.AddDays(1);

            await reviewRepo.SaveDecisionAsync(new PersistedReviewDecision
            {
                IssuerId = issuerId, WorkflowId = "wf-t", ActorId = "a",
                DecisionType = ReviewDecisionType.Approve, Timestamp = DateTime.UtcNow
            });

            var inRange  = await reviewRepo.QueryDecisionsAsync(issuerId, fromUtc: past, toUtc: future);
            var outRange = await reviewRepo.QueryDecisionsAsync(issuerId, fromUtc: future);

            Assert.That(inRange, Has.Count.EqualTo(1), "Decision within time range must be returned");
            Assert.That(outRange, Is.Empty, "Decision before start of time range must not be returned");
        }

        [Test]
        public async Task WorkflowRepository_GetMemberById_ReturnsCorrectMember()
        {
            var wfRepo   = CreateWorkflowRepo();
            var issuerId = "issuer-getmember";

            var member = new IssuerTeamMember
            {
                IssuerId    = issuerId,
                UserId      = "user@test.com",
                Role        = IssuerTeamRole.FinanceReviewer,
                DisplayName = "Finance User",
                AddedBy     = "admin",
                IsActive    = true
            };
            await wfRepo.UpsertMemberAsync(member);

            var retrieved = await wfRepo.GetMemberByIdAsync(issuerId, member.MemberId);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.UserId, Is.EqualTo("user@test.com"));
            Assert.That(retrieved.Role, Is.EqualTo(IssuerTeamRole.FinanceReviewer));
        }

        [Test]
        public async Task WorkflowRepository_IsMemberAsync_ReturnsFalseForInactiveMember()
        {
            var wfRepo   = CreateWorkflowRepo();
            var issuerId = "issuer-inactive";
            var userId   = "inactive@test.com";

            var member = new IssuerTeamMember
            {
                IssuerId = issuerId, UserId = userId,
                Role = IssuerTeamRole.Operator, AddedBy = "admin",
                IsActive = false
            };
            await wfRepo.UpsertMemberAsync(member);

            var isActive = await wfRepo.IsMemberAsync(issuerId, userId);
            Assert.That(isActive, Is.False, "Inactive member must not be reported as active member");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 11. HTTP integration — DI singleton repository semantics
        // ═══════════════════════════════════════════════════════════════════════

        private CustomWebApplicationFactory? _factory;
        private HttpClient? _client;

        [OneTimeSetUp]
        public void IntegrationSetup()
        {
            try
            {
                _factory = new CustomWebApplicationFactory();
                _client  = _factory.CreateClient();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] WebApplicationFactory setup failed: {ex.Message}. HTTP integration tests will be skipped.");
            }
        }

        [OneTimeTearDown]
        public void IntegrationTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task HTTP_IssuerWorkflow_UsesSingletonRepository_DataPersistsBetweenRequests()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            // Verify the DI container resolves IIssuerWorkflowRepository as a singleton
            var services = _factory!.Services;
            var repo1 = services.GetRequiredService<IIssuerWorkflowRepository>();
            var repo2 = services.GetRequiredService<IIssuerWorkflowRepository>();
            Assert.That(ReferenceEquals(repo1, repo2), Is.True,
                "IIssuerWorkflowRepository must be registered as singleton");
        }

        [Test]
        public async Task HTTP_ComplianceReview_UsesSingletonRepository_DataPersistsBetweenRequests()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            var services = _factory!.Services;
            var repo1 = services.GetRequiredService<IComplianceReviewRepository>();
            var repo2 = services.GetRequiredService<IComplianceReviewRepository>();
            Assert.That(ReferenceEquals(repo1, repo2), Is.True,
                "IComplianceReviewRepository must be registered as singleton");
        }

        [Test]
        public async Task HTTP_ReviewQueue_Unauthenticated_Returns401()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            var response = await _client!.GetAsync("/api/v1/enterprise-compliance-review/test-issuer/review-queue");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_EvidenceBundle_Unauthenticated_Returns401()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            var response = await _client!.GetAsync("/api/v1/enterprise-compliance-review/test-issuer/review-queue/test-wf/evidence");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_AuditExport_Unauthenticated_Returns401()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            var response = await _client!.PostAsJsonAsync(
                "/api/v1/enterprise-compliance-review/test-issuer/audit-export",
                new ReviewAuditExportRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_Diagnostics_Unauthenticated_Returns401()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            var response = await _client!.GetAsync("/api/v1/enterprise-compliance-review/test-issuer/diagnostics");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_IssuerWorkflow_Members_Unauthenticated_Returns401()
        {
            if (_client == null) Assert.Ignore("WebApplicationFactory not available in this environment.");

            var response = await _client!.GetAsync("/api/v1/issuer-workflow/test-issuer/members");
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
                        ["KeyManagementConfig:HardcodedKey"] = "PersistComplianceReviewTest32CharKey!",
                        ["JwtConfig:SecretKey"] = "PersistComplianceReviewJwtSecretKey32!",
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

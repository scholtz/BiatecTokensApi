using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.ComplianceCaseManagement;
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
    /// Comprehensive tests for <see cref="ComplianceCaseManagementService"/> and
    /// <see cref="BiatecTokensApi.Controllers.ComplianceCaseManagementController"/>.
    ///
    /// Coverage:
    ///   - Unit tests: all service methods, validation, idempotency
    ///   - State transition matrix: valid and invalid transitions
    ///   - Evidence freshness tests with FakeTimeProvider
    ///   - Integration tests: full HTTP pipeline via WebApplicationFactory
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseManagementTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider for evidence freshness tests
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value) => _now = value;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helper factories
        // ═══════════════════════════════════════════════════════════════════════

        private static ComplianceCaseManagementService CreateService(
            TimeProvider? timeProvider = null,
            TimeSpan? evidenceValidity = null)
        {
            return new ComplianceCaseManagementService(
                NullLogger<ComplianceCaseManagementService>.Instance,
                timeProvider,
                evidenceValidity);
        }

        private static CreateComplianceCaseRequest BuildCreateRequest(
            string? issuerId = null,
            string? subjectId = null,
            CaseType type = CaseType.InvestorEligibility,
            CasePriority priority = CasePriority.Medium)
        {
            return new CreateComplianceCaseRequest
            {
                IssuerId = issuerId ?? "issuer-" + Guid.NewGuid().ToString("N")[..8],
                SubjectId = subjectId ?? "subject-" + Guid.NewGuid().ToString("N")[..8],
                Type = type,
                Priority = priority,
                Jurisdiction = "US",
                CorrelationId = "corr-" + Guid.NewGuid().ToString("N")[..8]
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — CreateCaseAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateCase_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            var result = await svc.CreateCaseAsync(req, "actor@example.com");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case, Is.Not.Null);
            Assert.That(result.Case!.CaseId, Is.Not.Empty);
            Assert.That(result.Case.State, Is.EqualTo(ComplianceCaseState.Intake));
            Assert.That(result.Case.IssuerId, Is.EqualTo(req.IssuerId));
            Assert.That(result.Case.SubjectId, Is.EqualTo(req.SubjectId));
            Assert.That(result.WasIdempotent, Is.False);
        }

        [Test]
        public async Task CreateCase_MissingIssuerId_ReturnsMissingField()
        {
            var svc = CreateService();
            var req = BuildCreateRequest(issuerId: "");
            var result = await svc.CreateCaseAsync(req, "actor@example.com");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task CreateCase_MissingSubjectId_ReturnsMissingField()
        {
            var svc = CreateService();
            var req = BuildCreateRequest(subjectId: "");
            var result = await svc.CreateCaseAsync(req, "actor@example.com");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task CreateCase_IdempotentCreate_ReturnsSameCase()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            var r1 = await svc.CreateCaseAsync(req, "actor@example.com");
            var r2 = await svc.CreateCaseAsync(req, "actor@example.com");

            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.WasIdempotent, Is.True);
            Assert.That(r2.Case!.CaseId, Is.EqualTo(r1.Case!.CaseId));
        }

        [Test]
        public async Task CreateCase_AfterTerminal_AllowsNewCase()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            var r1 = await svc.CreateCaseAsync(req, "actor@example.com");
            var caseId = r1.Case!.CaseId;

            // Transition to EvidencePending then Approved (terminal)
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "actor");

            // Creating with same key should now create a new case
            var r2 = await svc.CreateCaseAsync(req, "actor@example.com");
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.WasIdempotent, Is.False);
            Assert.That(r2.Case!.CaseId, Is.Not.EqualTo(caseId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — GetCaseAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetCase_ExistingCase_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            var created = await svc.CreateCaseAsync(req, "actor@example.com");
            var caseId = created.Case!.CaseId;

            var result = await svc.GetCaseAsync(caseId, "actor@example.com");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.CaseId, Is.EqualTo(caseId));
        }

        [Test]
        public async Task GetCase_NonExistentCase_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.GetCaseAsync("nonexistent-id", "actor@example.com");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — ListCasesAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ListCases_NoFilter_ReturnsAll()
        {
            var svc = CreateService();
            var issuerId = "issuer-" + Guid.NewGuid().ToString("N");

            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId, type: CaseType.InvestorEligibility), "a");
            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId, type: CaseType.LaunchPackage), "a");
            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId, type: CaseType.OngoingMonitoring), "a");

            var result = await svc.ListCasesAsync(new ListComplianceCasesRequest { IssuerId = issuerId, PageSize = 100 }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ListCases_FilterByState_ReturnsMatching()
        {
            var svc = CreateService();
            var issuerId = "issuer-" + Guid.NewGuid().ToString("N");

            var r = await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId), "a");
            await svc.TransitionStateAsync(r.Case!.CaseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "a");

            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId), "a"); // stays in Intake

            var result = await svc.ListCasesAsync(new ListComplianceCasesRequest
            {
                IssuerId = issuerId,
                State = ComplianceCaseState.EvidencePending,
                PageSize = 100
            }, "a");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Cases[0].State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task ListCases_FilterByPriority_ReturnsMatching()
        {
            var svc = CreateService();
            var issuerId = "issuer-" + Guid.NewGuid().ToString("N");

            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId, priority: CasePriority.Critical), "a");
            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId, priority: CasePriority.Low), "a");

            var result = await svc.ListCasesAsync(new ListComplianceCasesRequest
            {
                IssuerId = issuerId,
                Priority = CasePriority.Critical,
                PageSize = 100
            }, "a");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Cases[0].Priority, Is.EqualTo(CasePriority.Critical));
        }

        [Test]
        public async Task ListCases_FilterByAssignedReviewer_ReturnsMatching()
        {
            var svc = CreateService();
            var issuerId = "issuer-" + Guid.NewGuid().ToString("N");
            var reviewerId = "reviewer@example.com";

            var r = await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId), "a");
            await svc.UpdateCaseAsync(r.Case!.CaseId, new UpdateComplianceCaseRequest { AssignedReviewerId = reviewerId }, "a");
            await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId), "a"); // unassigned

            var result = await svc.ListCasesAsync(new ListComplianceCasesRequest
            {
                IssuerId = issuerId,
                AssignedReviewerId = reviewerId,
                PageSize = 100
            }, "a");

            Assert.That(result.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ListCases_FilterByStale_ReturnsStale()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime, evidenceValidity: TimeSpan.FromDays(1));

            var issuerId = "issuer-" + Guid.NewGuid().ToString("N");
            var r = await svc.CreateCaseAsync(BuildCreateRequest(issuerId: issuerId), "a");

            // Advance time past evidence expiry
            fakeTime.Advance(TimeSpan.FromDays(2));
            await svc.GetCaseAsync(r.Case!.CaseId, "a"); // triggers freshness check

            var result = await svc.ListCasesAsync(new ListComplianceCasesRequest
            {
                IssuerId = issuerId,
                HasStaleEvidence = true,
                PageSize = 100
            }, "a");

            Assert.That(result.TotalCount, Is.GreaterThanOrEqualTo(1));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — UpdateCaseAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpdateCase_ValidUpdate_Success()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            var result = await svc.UpdateCaseAsync(caseId, new UpdateComplianceCaseRequest
            {
                Priority = CasePriority.High,
                AssignedReviewerId = "reviewer@example.com",
                Jurisdiction = "EU"
            }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.Priority, Is.EqualTo(CasePriority.High));
            Assert.That(result.Case.AssignedReviewerId, Is.EqualTo("reviewer@example.com"));
            Assert.That(result.Case.Jurisdiction, Is.EqualTo("EU"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — TransitionStateAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TransitionState_ValidTransition_Success()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            var result = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending, Reason = "Evidence requested" }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task TransitionState_InvalidTransition_ReturnsError()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Intake → Approved is not valid
            var result = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task TransitionState_TerminalState_ReturnsError()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Navigate to Approved
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "a");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "a");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "a");

            // Now try to transition from terminal state
            var result = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // State transition matrix tests
        // ═══════════════════════════════════════════════════════════════════════

        // Valid transitions
        [TestCase(ComplianceCaseState.Intake, ComplianceCaseState.EvidencePending)]
        [TestCase(ComplianceCaseState.Intake, ComplianceCaseState.Blocked)]
        [TestCase(ComplianceCaseState.EvidencePending, ComplianceCaseState.UnderReview)]
        [TestCase(ComplianceCaseState.EvidencePending, ComplianceCaseState.Blocked)]
        [TestCase(ComplianceCaseState.UnderReview, ComplianceCaseState.Approved)]
        [TestCase(ComplianceCaseState.UnderReview, ComplianceCaseState.Rejected)]
        [TestCase(ComplianceCaseState.UnderReview, ComplianceCaseState.Escalated)]
        [TestCase(ComplianceCaseState.UnderReview, ComplianceCaseState.Remediating)]
        [TestCase(ComplianceCaseState.UnderReview, ComplianceCaseState.Blocked)]
        [TestCase(ComplianceCaseState.Escalated, ComplianceCaseState.UnderReview)]
        [TestCase(ComplianceCaseState.Escalated, ComplianceCaseState.Rejected)]
        [TestCase(ComplianceCaseState.Escalated, ComplianceCaseState.Blocked)]
        [TestCase(ComplianceCaseState.Remediating, ComplianceCaseState.UnderReview)]
        [TestCase(ComplianceCaseState.Remediating, ComplianceCaseState.Approved)]
        [TestCase(ComplianceCaseState.Remediating, ComplianceCaseState.Rejected)]
        [TestCase(ComplianceCaseState.Remediating, ComplianceCaseState.Blocked)]
        [TestCase(ComplianceCaseState.Stale, ComplianceCaseState.EvidencePending)]
        [TestCase(ComplianceCaseState.Stale, ComplianceCaseState.Rejected)]
        [TestCase(ComplianceCaseState.Stale, ComplianceCaseState.Blocked)]
        [TestCase(ComplianceCaseState.Blocked, ComplianceCaseState.Intake)]
        public async Task TransitionMatrix_ValidTransition_Succeeds(ComplianceCaseState from, ComplianceCaseState to)
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Drive to the 'from' state via valid path
            await DriveToStateAsync(svc, caseId, from);

            var result = await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = to }, "a");
            Assert.That(result.Success, Is.True, $"Expected {from}→{to} to succeed but got: {result.ErrorMessage}");
            Assert.That(result.Case!.State, Is.EqualTo(to));
        }

        [TestCase(ComplianceCaseState.Approved, ComplianceCaseState.UnderReview)]
        [TestCase(ComplianceCaseState.Approved, ComplianceCaseState.Rejected)]
        [TestCase(ComplianceCaseState.Approved, ComplianceCaseState.Intake)]
        [TestCase(ComplianceCaseState.Rejected, ComplianceCaseState.UnderReview)]
        [TestCase(ComplianceCaseState.Rejected, ComplianceCaseState.Approved)]
        [TestCase(ComplianceCaseState.Rejected, ComplianceCaseState.Intake)]
        public async Task TransitionMatrix_TerminalState_RejectAll(ComplianceCaseState terminal, ComplianceCaseState to)
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            await DriveToStateAsync(svc, caseId, terminal);

            var result = await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = to }, "a");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>Drives a case to the specified state using valid transition paths.</summary>
        private static async Task DriveToStateAsync(ComplianceCaseManagementService svc, string caseId, ComplianceCaseState target)
        {
            var current = (await svc.GetCaseAsync(caseId, "a")).Case!.State;
            if (current == target) return;

            // State path map: target → chain of transitions to get there
            var paths = new Dictionary<ComplianceCaseState, ComplianceCaseState[]>
            {
                [ComplianceCaseState.Intake]          = Array.Empty<ComplianceCaseState>(),
                [ComplianceCaseState.EvidencePending] = new[] { ComplianceCaseState.EvidencePending },
                [ComplianceCaseState.UnderReview]     = new[] { ComplianceCaseState.EvidencePending, ComplianceCaseState.UnderReview },
                [ComplianceCaseState.Escalated]       = new[] { ComplianceCaseState.EvidencePending, ComplianceCaseState.UnderReview, ComplianceCaseState.Escalated },
                [ComplianceCaseState.Remediating]     = new[] { ComplianceCaseState.EvidencePending, ComplianceCaseState.UnderReview, ComplianceCaseState.Remediating },
                [ComplianceCaseState.Approved]        = new[] { ComplianceCaseState.EvidencePending, ComplianceCaseState.UnderReview, ComplianceCaseState.Approved },
                [ComplianceCaseState.Rejected]        = new[] { ComplianceCaseState.EvidencePending, ComplianceCaseState.UnderReview, ComplianceCaseState.Rejected },
                [ComplianceCaseState.Blocked]         = new[] { ComplianceCaseState.Blocked },
                [ComplianceCaseState.Stale]           = new[] { ComplianceCaseState.EvidencePending, ComplianceCaseState.Stale },
            };

            if (!paths.TryGetValue(target, out var path)) return;

            foreach (var state in path)
            {
                current = (await svc.GetCaseAsync(caseId, "a")).Case!.State;
                if (current == state) continue;
                var tr = await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = state }, "a");
                if (!tr.Success) throw new InvalidOperationException($"Could not drive to {state}: {tr.ErrorMessage}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — AddEvidenceAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AddEvidence_ValidRequest_AddsEvidenceAndTimeline()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            var result = await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                Status = CaseEvidenceStatus.Valid,
                ProviderName = "TestProvider",
                CapturedAt = DateTimeOffset.UtcNow,
                Summary = "KYC passed"
            }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.EvidenceSummaries, Has.Count.EqualTo(1));
            Assert.That(result.Case.EvidenceSummaries[0].EvidenceType, Is.EqualTo("KYC"));
            Assert.That(result.Case.Timeline.Any(t => t.EventType == CaseTimelineEventType.EvidenceAdded), Is.True);
        }

        [Test]
        public async Task AddEvidence_MissingEvidenceType_ReturnsMissingField()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.AddEvidenceAsync(r.Case!.CaseId, new AddEvidenceRequest
            {
                EvidenceType = "", // missing
                Status = CaseEvidenceStatus.Valid
            }, "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task AddEvidence_NotFound_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.AddEvidenceAsync("bad-id", new AddEvidenceRequest { EvidenceType = "KYC" }, "a");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — AddRemediationTaskAsync / ResolveRemediationTaskAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AddRemediationTask_ValidRequest_Success()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.AddRemediationTaskAsync(r.Case!.CaseId, new AddRemediationTaskRequest
            {
                Title = "Re-submit KYC documents",
                IsBlockingCase = true,
                BlockerSeverity = EvidenceIssueSeverityLevel.High
            }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.RemediationTasks, Has.Count.EqualTo(1));
            Assert.That(result.Case.RemediationTasks[0].Status, Is.EqualTo(RemediationTaskStatus.Open));
        }

        [Test]
        public async Task AddRemediationTask_BlockingTask_BlocksCaseReadiness()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Add evidence with CapturedAt so we have evidence
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                Status = CaseEvidenceStatus.Valid,
                CapturedAt = DateTimeOffset.UtcNow
            }, "a");

            // Add a blocking task
            await svc.AddRemediationTaskAsync(caseId, new AddRemediationTaskRequest
            {
                Title = "Must do this",
                IsBlockingCase = true
            }, "a");

            var readiness = await svc.GetReadinessSummaryAsync(caseId, "a");
            Assert.That(readiness.Summary!.IsReady, Is.False);
            Assert.That(readiness.Summary.BlockingIssues.Any(b => b.Contains("Must do this")), Is.True);
        }

        [Test]
        public async Task ResolveRemediationTask_ValidRequest_UpdatesStatus()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            var addResult = await svc.AddRemediationTaskAsync(caseId, new AddRemediationTaskRequest { Title = "Fix it" }, "a");
            var taskId = addResult.Case!.RemediationTasks[0].TaskId;

            var resolveResult = await svc.ResolveRemediationTaskAsync(caseId, taskId,
                new ResolveRemediationTaskRequest { Status = RemediationTaskStatus.Resolved, ResolutionNotes = "Done" }, "a");

            Assert.That(resolveResult.Success, Is.True);
            Assert.That(resolveResult.Case!.RemediationTasks[0].Status, Is.EqualTo(RemediationTaskStatus.Resolved));
            Assert.That(resolveResult.Case.RemediationTasks[0].ResolutionNotes, Is.EqualTo("Done"));
        }

        [Test]
        public async Task ResolveRemediationTask_NotFound_ReturnsNotFound()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.ResolveRemediationTaskAsync(r.Case!.CaseId, "bad-task-id",
                new ResolveRemediationTaskRequest(), "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — AddEscalationAsync / ResolveEscalationAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AddEscalation_SanctionsHit_AddsEscalationWithCritical()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.AddEscalationAsync(r.Case!.CaseId, new AddEscalationRequest
            {
                Type = EscalationType.SanctionsHit,
                Description = "OFAC sanctions match detected",
                ConfidenceScore = 0.95,
                RequiresManualReview = true
            }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.Escalations, Has.Count.EqualTo(1));
            Assert.That(result.Case.Escalations[0].Type, Is.EqualTo(EscalationType.SanctionsHit));
            Assert.That(result.Case.Escalations[0].Status, Is.EqualTo(EscalationStatus.Open));
            Assert.That(result.Case.Timeline.Any(t => t.EventType == CaseTimelineEventType.EscalationRaised), Is.True);
        }

        [Test]
        public async Task AddEscalation_JurisdictionConflict_Success()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.AddEscalationAsync(r.Case!.CaseId, new AddEscalationRequest
            {
                Type = EscalationType.JurisdictionConflict,
                Description = "Subject registered in sanctioned jurisdiction"
            }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.Escalations[0].Type, Is.EqualTo(EscalationType.JurisdictionConflict));
        }

        [Test]
        public async Task AddEscalation_MissingDescription_ReturnsMissingField()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.AddEscalationAsync(r.Case!.CaseId, new AddEscalationRequest
            {
                Type = EscalationType.ManualEscalation,
                Description = "" // missing
            }, "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task ResolveEscalation_ValidRequest_UpdatesStatus()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            var addResult = await svc.AddEscalationAsync(caseId, new AddEscalationRequest
            {
                Type = EscalationType.WatchlistMatch,
                Description = "Possible match"
            }, "a");
            var escalationId = addResult.Case!.Escalations[0].EscalationId;

            var resolveResult = await svc.ResolveEscalationAsync(caseId, escalationId,
                new ResolveEscalationRequest { ResolutionNotes = "False positive confirmed" }, "a");

            Assert.That(resolveResult.Success, Is.True);
            Assert.That(resolveResult.Case!.Escalations[0].Status, Is.EqualTo(EscalationStatus.Resolved));
            Assert.That(resolveResult.Case.Escalations[0].ResolutionNotes, Is.EqualTo("False positive confirmed"));
        }

        [Test]
        public async Task ResolveEscalation_NotFound_ReturnsNotFound()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var result = await svc.ResolveEscalationAsync(r.Case!.CaseId, "bad-esc-id",
                new ResolveEscalationRequest(), "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — GetTimelineAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetTimeline_NewCase_HasCreateEntry()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var timeline = await svc.GetTimelineAsync(r.Case!.CaseId, "a");

            Assert.That(timeline.Success, Is.True);
            Assert.That(timeline.Entries.Any(e => e.EventType == CaseTimelineEventType.CaseCreated), Is.True);
        }

        [Test]
        public async Task GetTimeline_AfterStateTransition_HasTransitionEntry()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "a");

            var timeline = await svc.GetTimelineAsync(caseId, "a");
            Assert.That(timeline.Entries.Any(e => e.EventType == CaseTimelineEventType.StateTransition), Is.True);
        }

        [Test]
        public async Task GetTimeline_NonExistentCase_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.GetTimelineAsync("bad-id", "a");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — GetReadinessSummaryAsync (fail-closed)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetReadinessSummary_NoEvidence_FailsClosed()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");

            var readiness = await svc.GetReadinessSummaryAsync(r.Case!.CaseId, "a");

            Assert.That(readiness.Success, Is.True);
            Assert.That(readiness.Summary!.IsReady, Is.False);
            Assert.That(readiness.Summary.FailedClosed, Is.True);
            Assert.That(readiness.Summary.BlockingIssues.Any(b => b.Contains("No evidence")), Is.True);
        }

        [Test]
        public async Task GetReadinessSummary_WithValidEvidence_NoBlockers_IsReady()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                Status = CaseEvidenceStatus.Valid,
                CapturedAt = DateTimeOffset.UtcNow,
                IsBlockingReadiness = false
            }, "a");

            var readiness = await svc.GetReadinessSummaryAsync(caseId, "a");

            Assert.That(readiness.Summary!.IsReady, Is.True);
            Assert.That(readiness.Summary.FailedClosed, Is.False);
            Assert.That(readiness.Summary.BlockingIssues, Is.Empty);
        }

        [Test]
        public async Task GetReadinessSummary_WithBlockingEvidence_IsNotReady()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "AML",
                Status = CaseEvidenceStatus.Rejected,
                CapturedAt = DateTimeOffset.UtcNow,
                IsBlockingReadiness = true,
                BlockingReason = "AML screening failed"
            }, "a");

            var readiness = await svc.GetReadinessSummaryAsync(caseId, "a");

            Assert.That(readiness.Summary!.IsReady, Is.False);
            Assert.That(readiness.Summary.BlockingIssues.Any(b => b.Contains("AML screening failed")), Is.True);
        }

        [Test]
        public async Task GetReadinessSummary_WithOpenCriticalEscalation_IsNotReady()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                CapturedAt = DateTimeOffset.UtcNow
            }, "a");

            await svc.AddEscalationAsync(caseId, new AddEscalationRequest
            {
                Type = EscalationType.SanctionsHit,
                Description = "Sanctions match",
                RequiresManualReview = true
            }, "a");

            var readiness = await svc.GetReadinessSummaryAsync(caseId, "a");

            Assert.That(readiness.Summary!.IsReady, Is.False);
            Assert.That(readiness.Summary.CriticalEscalations, Is.EqualTo(1));
        }

        [Test]
        public async Task GetReadinessSummary_WithOpenBlockingRemediationTask_IsNotReady()
        {
            var svc = CreateService();
            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                CapturedAt = DateTimeOffset.UtcNow
            }, "a");

            await svc.AddRemediationTaskAsync(caseId, new AddRemediationTaskRequest
            {
                Title = "Upload passport scan",
                IsBlockingCase = true
            }, "a");

            var readiness = await svc.GetReadinessSummaryAsync(caseId, "a");

            Assert.That(readiness.Summary!.IsReady, Is.False);
            Assert.That(readiness.Summary.OpenRemediationTasks, Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Evidence freshness tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidenceFreshness_NotExpired_CaseRemainsActive()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime, evidenceValidity: TimeSpan.FromDays(30));

            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Advance only 10 days — should not expire
            fakeTime.Advance(TimeSpan.FromDays(10));
            var result = await svc.GetCaseAsync(caseId, "a");

            Assert.That(result.Case!.State, Is.Not.EqualTo(ComplianceCaseState.Stale));
            Assert.That(result.Case.IsEvidenceStale, Is.False);
        }

        [Test]
        public async Task EvidenceFreshness_Expired_CaseTransitionsToStale()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime, evidenceValidity: TimeSpan.FromDays(1));

            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Move to EvidencePending so Stale transition is valid
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "a");

            // Advance past expiry
            fakeTime.Advance(TimeSpan.FromDays(2));
            var result = await svc.GetCaseAsync(caseId, "a");

            Assert.That(result.Case!.State, Is.EqualTo(ComplianceCaseState.Stale));
            Assert.That(result.Case.IsEvidenceStale, Is.True);
        }

        [Test]
        public async Task EvidenceFreshness_AlreadyTerminal_NoTransition()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime, evidenceValidity: TimeSpan.FromDays(1));

            var r = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
            var caseId = r.Case!.CaseId;

            // Approve the case
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "a");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "a");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "a");

            // Advance past expiry
            fakeTime.Advance(TimeSpan.FromDays(2));
            var result = await svc.GetCaseAsync(caseId, "a");

            // Terminal state should not change
            Assert.That(result.Case!.State, Is.EqualTo(ComplianceCaseState.Approved));
        }

        [Test]
        public async Task RunEvidenceFreshnessCheck_ExpiredCases_TransitionsAll()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime, evidenceValidity: TimeSpan.FromDays(1));

            // Create 3 cases and move them to EvidencePending (Stale transition is valid)
            for (int i = 0; i < 3; i++)
            {
                var cr = await svc.CreateCaseAsync(BuildCreateRequest(), "a");
                await svc.TransitionStateAsync(cr.Case!.CaseId,
                    new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "a");
            }

            fakeTime.Advance(TimeSpan.FromDays(2));
            var count = await svc.RunEvidenceFreshnessCheckAsync();

            Assert.That(count, Is.EqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — HTTP pipeline
        // ═══════════════════════════════════════════════════════════════════════

        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [OneTimeSetUp]
        public async Task IntegrationSetup()
        {
            _factory = new CustomWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            // Register and login to get JWT
            var email = $"casetest-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Case Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var token = regBody?.AccessToken ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        [OneTimeTearDown]
        public void IntegrationTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task IntegrationTest_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = "i1", SubjectId = "s1" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IntegrationTest_Unauthenticated_GetCase_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/compliance-cases/some-case-id");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IntegrationTest_CreateAndRetrieve_RoundTrip()
        {
            var createReq = new CreateComplianceCaseRequest
            {
                IssuerId = "integration-issuer-" + Guid.NewGuid().ToString("N")[..8],
                SubjectId = "integration-subject-" + Guid.NewGuid().ToString("N")[..8],
                Type = CaseType.InvestorEligibility,
                Priority = CasePriority.High,
                Jurisdiction = "US"
            };

            var createResp = await _client.PostAsJsonAsync("/api/v1/compliance-cases", createReq);
            Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var created = await createResp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions);
            Assert.That(created!.Success, Is.True);
            Assert.That(created.Case!.CaseId, Is.Not.Empty);

            var getResp = await _client.GetAsync($"/api/v1/compliance-cases/{created.Case.CaseId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var retrieved = await getResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>(_jsonOptions);
            Assert.That(retrieved!.Success, Is.True);
            Assert.That(retrieved.Case!.CaseId, Is.EqualTo(created.Case.CaseId));
            Assert.That(retrieved.Case.IssuerId, Is.EqualTo(createReq.IssuerId));
        }

        [Test]
        public async Task IntegrationTest_GetCase_NotFound_Returns404()
        {
            var resp = await _client.GetAsync("/api/v1/compliance-cases/nonexistent-case-id-12345");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task IntegrationTest_ListCases_FilterByState()
        {
            var issuerId = "int-issuer-" + Guid.NewGuid().ToString("N")[..8];

            // Create a case
            var createResp = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "s1", Type = CaseType.LaunchPackage });
            var created = await createResp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions);

            // List with filter
            var listResp = await _client.PostAsJsonAsync("/api/v1/compliance-cases/list",
                new ListComplianceCasesRequest { IssuerId = issuerId, State = ComplianceCaseState.Intake, PageSize = 100 });
            Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var listed = await listResp.Content.ReadFromJsonAsync<ListComplianceCasesResponse>(_jsonOptions);
            Assert.That(listed!.Success, Is.True);
            Assert.That(listed.TotalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(listed.Cases.All(c => c.State == ComplianceCaseState.Intake), Is.True);
        }

        [Test]
        public async Task IntegrationTest_FullLifecycle_CreateEvidenceEscalateResolveApprove()
        {
            var issuerId = "int-full-" + Guid.NewGuid().ToString("N")[..8];
            var subjectId = "sub-" + Guid.NewGuid().ToString("N")[..8];

            // 1. Create case
            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = subjectId, Type = CaseType.InvestorEligibility });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;
            var caseId = created.Case!.CaseId;

            // 2. Add evidence
            var evResp = await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/evidence",
                new AddEvidenceRequest { EvidenceType = "KYC", CapturedAt = DateTimeOffset.UtcNow, Status = CaseEvidenceStatus.Valid });
            Assert.That(evResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 3. Transition: Intake → EvidencePending
            var t1 = await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending });
            Assert.That(t1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 4. Transition: EvidencePending → UnderReview
            var t2 = await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview });
            Assert.That(t2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 5. Add escalation
            var escResp = await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/escalations",
                new AddEscalationRequest { Type = EscalationType.AdverseMedia, Description = "News article found", RequiresManualReview = true });
            Assert.That(escResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var escCase = (await escResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>(_jsonOptions))!;
            var escalationId = escCase.Case!.Escalations[0].EscalationId;

            // 6. Resolve escalation
            var resEscResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/escalations/{escalationId}/resolve",
                new ResolveEscalationRequest { ResolutionNotes = "Article unrelated" });
            Assert.That(resEscResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 7. Transition: UnderReview → Approved
            var t3 = await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved, Reason = "All checks passed" });
            Assert.That(t3.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var approved = (await t3.Content.ReadFromJsonAsync<UpdateComplianceCaseResponse>(_jsonOptions))!;
            Assert.That(approved.Case!.State, Is.EqualTo(ComplianceCaseState.Approved));
        }

        [Test]
        public async Task IntegrationTest_Timeline_HasAllEvents()
        {
            var issuerId = "int-timeline-" + Guid.NewGuid().ToString("N")[..8];

            // Create
            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "sub1", Type = CaseType.LaunchPackage });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;
            var caseId = created.Case!.CaseId;

            // Add evidence
            await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/evidence",
                new AddEvidenceRequest { EvidenceType = "KYC", CapturedAt = DateTimeOffset.UtcNow });

            // Transition
            await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending });

            // Get timeline
            var tlResp = await _client.GetAsync($"/api/v1/compliance-cases/{caseId}/timeline");
            Assert.That(tlResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var tl = (await tlResp.Content.ReadFromJsonAsync<CaseTimelineResponse>(_jsonOptions))!;
            Assert.That(tl.Success, Is.True);
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.CaseCreated), Is.True);
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.EvidenceAdded), Is.True);
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.StateTransition), Is.True);
        }

        [Test]
        public async Task IntegrationTest_Readiness_FailsClosedWhenNoEvidence()
        {
            var issuerId = "int-readiness-" + Guid.NewGuid().ToString("N")[..8];

            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "sub1", Type = CaseType.InvestorEligibility });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;

            var rdResp = await _client.GetAsync($"/api/v1/compliance-cases/{created.Case!.CaseId}/readiness");
            Assert.That(rdResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var rd = (await rdResp.Content.ReadFromJsonAsync<CaseReadinessSummaryResponse>(_jsonOptions))!;
            Assert.That(rd.Success, Is.True);
            Assert.That(rd.Summary!.IsReady, Is.False);
            Assert.That(rd.Summary.FailedClosed, Is.True);
        }

        [Test]
        public async Task IntegrationTest_RemediationTask_BlocksReadiness()
        {
            var issuerId = "int-task-" + Guid.NewGuid().ToString("N")[..8];

            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "sub1", Type = CaseType.LaunchPackage });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;
            var caseId = created.Case!.CaseId;

            // Add evidence so it doesn't fail-closed
            await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/evidence",
                new AddEvidenceRequest { EvidenceType = "Identity", CapturedAt = DateTimeOffset.UtcNow });

            // Add blocking task
            var taskResp = await _client.PostAsJsonAsync($"/api/v1/compliance-cases/{caseId}/remediation-tasks",
                new AddRemediationTaskRequest { Title = "Verify source of funds", IsBlockingCase = true });
            Assert.That(taskResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var taskCase = (await taskResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>(_jsonOptions))!;
            var taskId = taskCase.Case!.RemediationTasks[0].TaskId;

            // Readiness should be blocked
            var rdResp1 = await _client.GetAsync($"/api/v1/compliance-cases/{caseId}/readiness");
            var rd1 = (await rdResp1.Content.ReadFromJsonAsync<CaseReadinessSummaryResponse>(_jsonOptions))!;
            Assert.That(rd1.Summary!.IsReady, Is.False);

            // Resolve the task
            var resolveResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/remediation-tasks/{taskId}/resolve",
                new ResolveRemediationTaskRequest { Status = RemediationTaskStatus.Resolved });
            Assert.That(resolveResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Readiness should now pass
            var rdResp2 = await _client.GetAsync($"/api/v1/compliance-cases/{caseId}/readiness");
            var rd2 = (await rdResp2.Content.ReadFromJsonAsync<CaseReadinessSummaryResponse>(_jsonOptions))!;
            Assert.That(rd2.Summary!.IsReady, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Ongoing Monitoring (SetMonitoringSchedule)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task SetMonitoringSchedule_Annual_SetsCorrectInterval()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            var resp = await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Schedule, Is.Not.Null);
            Assert.That(resp.Schedule!.IntervalDays, Is.EqualTo(365));
            Assert.That(resp.Schedule.Frequency, Is.EqualTo(MonitoringFrequency.Annual));
            Assert.That(resp.Case!.MonitoringSchedule, Is.Not.Null);
        }

        [Test]
        public async Task SetMonitoringSchedule_Monthly_SetsCorrectInterval()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Monthly }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Schedule!.IntervalDays, Is.EqualTo(30));
        }

        [Test]
        public async Task SetMonitoringSchedule_Quarterly_SetsCorrectInterval()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Schedule!.IntervalDays, Is.EqualTo(90));
        }

        [Test]
        public async Task SetMonitoringSchedule_SemiAnnual_SetsCorrectInterval()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.SemiAnnual }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Schedule!.IntervalDays, Is.EqualTo(180));
        }

        [Test]
        public async Task SetMonitoringSchedule_Custom_SetsCustomInterval()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest
                {
                    Frequency = MonitoringFrequency.Custom,
                    CustomIntervalDays = 45
                }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Schedule!.IntervalDays, Is.EqualTo(45));
        }

        [Test]
        public async Task SetMonitoringSchedule_CustomMissingInterval_ReturnsError()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest
                {
                    Frequency = MonitoringFrequency.Custom,
                    CustomIntervalDays = null   // missing
                }, "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo(BiatecTokensApi.Models.ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task SetMonitoringSchedule_CustomZeroInterval_ReturnsError()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest
                {
                    Frequency = MonitoringFrequency.Custom,
                    CustomIntervalDays = 0
                }, "actor");

            Assert.That(resp.Success, Is.False);
        }

        [Test]
        public async Task SetMonitoringSchedule_NotFoundCase_ReturnsNotFound()
        {
            var svc  = CreateService();
            var resp = await svc.SetMonitoringScheduleAsync("nonexistent",
                new SetMonitoringScheduleRequest(), "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo(BiatecTokensApi.Models.ErrorCodes.NOT_FOUND));
        }

        [Test]
        public async Task SetMonitoringSchedule_SetsNextReviewDueAt()
        {
            var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var svc = CreateService(fakeTime);
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.SetMonitoringScheduleAsync(cr.Case!.CaseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly }, "actor");

            // NextReviewDueAt should be 90 days from the fake "now"
            Assert.That(resp.Schedule!.NextReviewDueAt, Is.EqualTo(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public async Task SetMonitoringSchedule_AddsTimelineEntry()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            var tl = await svc.GetTimelineAsync(caseId, "actor");
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.MonitoringScheduleSet), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Ongoing Monitoring (RecordMonitoringReview)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordMonitoringReview_Clear_RecordsReview()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            var resp = await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = "All checks passed. No concerns found."
                }, "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Review, Is.Not.Null);
            Assert.That(resp.Review!.Outcome, Is.EqualTo(MonitoringReviewOutcome.Clear));
            Assert.That(resp.Case!.MonitoringReviews.Count, Is.EqualTo(1));
            Assert.That(resp.FollowUpCase, Is.Null);
        }

        [Test]
        public async Task RecordMonitoringReview_UpdatesScheduleTimestamps()
        {
            var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var svc = CreateService(fakeTime);
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly }, "actor");

            // Advance 95 days and record review
            fakeTime.Advance(TimeSpan.FromDays(95));

            var resp = await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.AdvisoryNote,
                    ReviewNotes = "Minor change in address. Monitoring continues."
                }, "reviewer");

            Assert.That(resp.Success, Is.True);
            var sched = resp.Case!.MonitoringSchedule!;
            Assert.That(sched.LastReviewAt, Is.Not.Null);
            // NextReviewDueAt should be 90 days from review date
            Assert.That(sched.NextReviewDueAt, Is.GreaterThan(fakeTime.GetUtcNow()));
        }

        [Test]
        public async Task RecordMonitoringReview_MissingNotes_ReturnsError()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            var resp = await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = ""   // missing
                }, "reviewer");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo(BiatecTokensApi.Models.ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task RecordMonitoringReview_NoSchedule_ReturnsError()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            // No schedule set
            var resp = await svc.RecordMonitoringReviewAsync(cr.Case!.CaseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = "Test notes"
                }, "reviewer");

            Assert.That(resp.Success, Is.False);
        }

        [Test]
        public async Task RecordMonitoringReview_NotFoundCase_ReturnsNotFound()
        {
            var svc  = CreateService();
            var resp = await svc.RecordMonitoringReviewAsync("nonexistent",
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = "notes"
                }, "reviewer");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo(BiatecTokensApi.Models.ErrorCodes.NOT_FOUND));
        }

        [Test]
        public async Task RecordMonitoringReview_EscalationRequired_CreatesFollowUpCase()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(
                BuildCreateRequest(issuerId: "iss-monitor", subjectId: "sub-monitor"), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            var resp = await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome          = MonitoringReviewOutcome.EscalationRequired,
                    ReviewNotes      = "Sanctions hit detected during re-screening.",
                    CreateFollowUpCase = true
                }, "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Review!.FollowUpCaseCreated, Is.True);
            Assert.That(resp.Review.FollowUpCaseId, Is.Not.Null);
            Assert.That(resp.FollowUpCase, Is.Not.Null);
            Assert.That(resp.FollowUpCase!.Type, Is.EqualTo(CaseType.OngoingMonitoring));
            Assert.That(resp.FollowUpCase.Priority, Is.EqualTo(CasePriority.High));
        }

        [Test]
        public async Task RecordMonitoringReview_EscalationNoFollowUp_DoesNotCreate()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            var resp = await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome          = MonitoringReviewOutcome.EscalationRequired,
                    ReviewNotes      = "Issue found but follow-up handled externally.",
                    CreateFollowUpCase = false
                }, "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.FollowUpCase, Is.Null);
        }

        [Test]
        public async Task RecordMonitoringReview_AddsTimelineEntries()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.ConcernIdentified,
                    ReviewNotes = "Adverse media article found — monitoring continues."
                }, "reviewer");

            var tl = await svc.GetTimelineAsync(caseId, "actor");
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.MonitoringReviewRecorded), Is.True);
        }

        [Test]
        public async Task RecordMonitoringReview_MultipleReviews_AllPreserved()
        {
            var svc = CreateService();
            var cr  = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly }, "actor");

            for (int i = 1; i <= 3; i++)
            {
                await svc.RecordMonitoringReviewAsync(caseId,
                    new RecordMonitoringReviewRequest
                    {
                        Outcome     = MonitoringReviewOutcome.Clear,
                        ReviewNotes = $"Review #{i}: no concerns."
                    }, "reviewer");
            }

            var caseResp = await svc.GetCaseAsync(caseId, "actor");
            Assert.That(caseResp.Case!.MonitoringReviews.Count, Is.EqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Ongoing Monitoring (TriggerPeriodicReviewCheck)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TriggerPeriodicReviewCheck_NoScheduledCases_ReturnsZero()
        {
            var svc = CreateService();
            // Create a case but don't set a schedule
            await svc.CreateCaseAsync(BuildCreateRequest(), "actor");

            var resp = await svc.TriggerPeriodicReviewCheckAsync("system");
            Assert.That(resp.Success, Is.True);
            Assert.That(resp.CasesInspected, Is.EqualTo(0));
            Assert.That(resp.OverdueCasesFound, Is.EqualTo(0));
        }

        [Test]
        public async Task TriggerPeriodicReviewCheck_OverdueCases_MarkedAsOverdue()
        {
            var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var svc = CreateService(fakeTime);

            var cr = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            // Set quarterly schedule
            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly }, "actor");

            // Advance 95 days past the due date (next review was at day 90)
            fakeTime.Advance(TimeSpan.FromDays(95));

            var resp = await svc.TriggerPeriodicReviewCheckAsync("system");
            Assert.That(resp.Success, Is.True);
            Assert.That(resp.OverdueCasesFound, Is.EqualTo(1));
            Assert.That(resp.OverdueCaseIds, Contains.Item(caseId));
        }

        [Test]
        public async Task TriggerPeriodicReviewCheck_NotYetDue_NotMarkedOverdue()
        {
            var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var svc = CreateService(fakeTime);

            var cr = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            // Set annual schedule
            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual }, "actor");

            // Advance only 30 days — not yet due
            fakeTime.Advance(TimeSpan.FromDays(30));

            var resp = await svc.TriggerPeriodicReviewCheckAsync("system");
            Assert.That(resp.OverdueCasesFound, Is.EqualTo(0));
            Assert.That(resp.CasesInspected, Is.EqualTo(1));
        }

        [Test]
        public async Task TriggerPeriodicReviewCheck_AfterReview_OverdueReset()
        {
            var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var svc = CreateService(fakeTime);

            var cr = await svc.CreateCaseAsync(BuildCreateRequest(), "actor");
            var caseId = cr.Case!.CaseId;

            await svc.SetMonitoringScheduleAsync(caseId,
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly }, "actor");

            // Advance past due date, record review (resets next due date)
            fakeTime.Advance(TimeSpan.FromDays(95));

            await svc.RecordMonitoringReviewAsync(caseId,
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = "All clear after scheduled review."
                }, "reviewer");

            // Now trigger check — should NOT be overdue because review was just recorded
            var resp = await svc.TriggerPeriodicReviewCheckAsync("system");
            Assert.That(resp.OverdueCasesFound, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — Monitoring HTTP pipeline
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IntegrationTest_SetMonitoringSchedule_Returns200()
        {
            var issuerId = "int-mon-" + Guid.NewGuid().ToString("N")[..8];

            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "s1", Type = CaseType.InvestorEligibility });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;
            var caseId = created.Case!.CaseId;

            var schedResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/monitoring-schedule",
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual });

            Assert.That(schedResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = (await schedResp.Content.ReadFromJsonAsync<SetMonitoringScheduleResponse>(_jsonOptions))!;
            Assert.That(body.Success, Is.True);
            Assert.That(body.Schedule, Is.Not.Null);
            Assert.That(body.Schedule!.IntervalDays, Is.EqualTo(365));
        }

        [Test]
        public async Task IntegrationTest_SetMonitoringSchedule_Unauthorized_Returns401()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-cases/some-id/monitoring-schedule",
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Annual });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IntegrationTest_RecordMonitoringReview_Returns200()
        {
            var issuerId = "int-rev-" + Guid.NewGuid().ToString("N")[..8];

            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "s1", Type = CaseType.InvestorEligibility });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;
            var caseId = created.Case!.CaseId;

            // Must set schedule first
            await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/monitoring-schedule",
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.Quarterly });

            var reviewResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/monitoring-reviews",
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = "Quarterly review complete. No issues identified."
                });

            Assert.That(reviewResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = (await reviewResp.Content.ReadFromJsonAsync<RecordMonitoringReviewResponse>(_jsonOptions))!;
            Assert.That(body.Success, Is.True);
            Assert.That(body.Review!.Outcome, Is.EqualTo(MonitoringReviewOutcome.Clear));
        }

        [Test]
        public async Task IntegrationTest_RecordMonitoringReview_NoSchedule_Returns400()
        {
            var issuerId = "int-no-sched-" + Guid.NewGuid().ToString("N")[..8];

            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = "s1", Type = CaseType.InvestorEligibility });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;

            // Attempt review without setting schedule
            var reviewResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{created.Case!.CaseId}/monitoring-reviews",
                new RecordMonitoringReviewRequest
                {
                    Outcome     = MonitoringReviewOutcome.Clear,
                    ReviewNotes = "notes"
                });

            Assert.That(reviewResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task IntegrationTest_TriggerPeriodicReviewCheck_Returns200()
        {
            var resp = await _client.PostAsync("/api/v1/compliance-cases/periodic-review-check", null);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = (await resp.Content.ReadFromJsonAsync<TriggerPeriodicReviewCheckResponse>(_jsonOptions))!;
            Assert.That(body.Success, Is.True);
            Assert.That(body.CheckedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task IntegrationTest_TriggerPeriodicReviewCheck_Unauthorized_Returns401()
        {
            var resp = await _unauthClient.PostAsync("/api/v1/compliance-cases/periodic-review-check", null);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IntegrationTest_FullMonitoringWorkflow_ScheduleReviewFollowUp()
        {
            var issuerId  = "int-full-mon-" + Guid.NewGuid().ToString("N")[..8];
            var subjectId = "sub-" + Guid.NewGuid().ToString("N")[..8];

            // 1. Create case
            var cr = await _client.PostAsJsonAsync("/api/v1/compliance-cases",
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = subjectId, Type = CaseType.OngoingMonitoring });
            var created = (await cr.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>(_jsonOptions))!;
            var caseId = created.Case!.CaseId;

            // 2. Set monitoring schedule
            var schedResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/monitoring-schedule",
                new SetMonitoringScheduleRequest { Frequency = MonitoringFrequency.SemiAnnual, Notes = "MICA annual review" });
            var sched = (await schedResp.Content.ReadFromJsonAsync<SetMonitoringScheduleResponse>(_jsonOptions))!;
            Assert.That(sched.Success, Is.True);
            Assert.That(sched.Schedule!.IntervalDays, Is.EqualTo(180));

            // 3. Record monitoring review that triggers a follow-up
            var reviewResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-cases/{caseId}/monitoring-reviews",
                new RecordMonitoringReviewRequest
                {
                    Outcome            = MonitoringReviewOutcome.EscalationRequired,
                    ReviewNotes        = "Re-screening returned a new sanctions hit on OFAC list.",
                    CreateFollowUpCase = true,
                    Attributes         = new Dictionary<string, string> { ["watchlist"] = "OFAC", ["matchScore"] = "0.95" }
                });

            Assert.That(reviewResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var review = (await reviewResp.Content.ReadFromJsonAsync<RecordMonitoringReviewResponse>(_jsonOptions))!;
            Assert.That(review.Success, Is.True);
            Assert.That(review.Review!.FollowUpCaseCreated, Is.True);
            Assert.That(review.FollowUpCase, Is.Not.Null);
            Assert.That(review.FollowUpCase!.Type, Is.EqualTo(CaseType.OngoingMonitoring));

            // 4. Validate timeline has monitoring events
            var tlResp = await _client.GetAsync($"/api/v1/compliance-cases/{caseId}/timeline");
            var tl = (await tlResp.Content.ReadFromJsonAsync<CaseTimelineResponse>(_jsonOptions))!;
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.MonitoringScheduleSet), Is.True);
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.MonitoringReviewRecorded), Is.True);
            Assert.That(tl.Entries.Any(e => e.EventType == CaseTimelineEventType.MonitoringFollowUpCreated), Is.True);
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
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForComplianceCaseManagementTests32C",
                        ["JwtConfig:SecretKey"] = "TestSecretKeyForComplianceCaseManagementTests32Chars!",
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
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["KycConfig:MockAutoApprove"] = "true",
                        ["StripeConfig:SecretKey"] = "sk_test_placeholder",
                        ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
                        ["StripeConfig:WebhookSecret"] = "whsec_placeholder",
                    });
                });
            }
        }
    }
}

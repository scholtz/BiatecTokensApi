using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for the compliance case-management maturity additions:
    ///   - Case summary endpoint (GetCaseSummaryAsync / ListCaseSummariesAsync)
    ///   - Structured blocker taxonomy (EvaluateBlockersAsync / ComputeBlockers)
    ///   - Decision history tracking (AddDecisionRecordAsync / GetDecisionHistoryAsync)
    ///   - Downstream handoff status (UpdateHandoffStatusAsync / GetHandoffStatusAsync)
    ///   - Webhook events for decision records and handoff status changes
    ///   - Fail-closed semantics and plain-language next-action derivation
    ///
    /// Coverage: ~50 tests across state transitions, role/ownership changes,
    /// degraded dependency handling, and API contract shape.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseMaturityTests
    {
        // ═════════════════════════════════════════════════════════════════════
        // Fake providers
        // ═════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta)         => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value) => _now = value;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private sealed class CapturingWebhookService : IWebhookService
        {
            public List<WebhookEvent> EmittedEvents { get; } = new();

            public Task EmitEventAsync(WebhookEvent e)
            {
                lock (EmittedEvents) EmittedEvents.Add(e);
                return Task.CompletedTask;
            }

            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string createdBy)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string userId)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId)
                => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string userId)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string userId)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string userId)
                => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helper factories
        // ═════════════════════════════════════════════════════════════════════

        private static readonly DateTimeOffset _baseline = new DateTimeOffset(2025, 8, 1, 10, 0, 0, TimeSpan.Zero);

        private static ComplianceCaseManagementService CreateService(
            IWebhookService? webhookService = null,
            TimeProvider? timeProvider = null) =>
            new(NullLogger<ComplianceCaseManagementService>.Instance,
                timeProvider,
                defaultEvidenceValidity: null,
                webhookService: webhookService,
                repository: null);

        private static (ComplianceCaseManagementService svc, CapturingWebhookService ws, FakeTimeProvider tp)
            CreateServiceWithCapture()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var ws  = new CapturingWebhookService();
            var svc = CreateService(webhookService: ws, timeProvider: tp);
            return (svc, ws, tp);
        }

        private static async Task<string> CreateCaseAsync(ComplianceCaseManagementService svc,
            string issuerId = "issuer-1", string subjectId = "subject-1",
            CaseType type = CaseType.InvestorEligibility)
        {
            var resp = await svc.CreateCaseAsync(
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = subjectId, Type = type },
                "actor");
            return resp.Case!.CaseId;
        }

        private static async Task WaitForEventAsync(CapturingWebhookService ws, WebhookEventType type, int maxWaitMs = 500)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (ws.EmittedEvents)
                    if (ws.EmittedEvents.Any(e => e.EventType == type))
                        return;
                await Task.Delay(10);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // GetCaseSummaryAsync tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetCaseSummary_ExistingCase_ReturnsCorrectFields()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc, "issuer-1", "subject-1");

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.CaseId, Is.EqualTo(caseId));
            Assert.That(result.Summary.IssuerId, Is.EqualTo("issuer-1"));
            Assert.That(result.Summary.SubjectId, Is.EqualTo("subject-1"));
            Assert.That(result.Summary.Type, Is.EqualTo(CaseType.InvestorEligibility));
            Assert.That(result.Summary.State, Is.EqualTo(ComplianceCaseState.Intake));
        }

        [Test]
        public async Task GetCaseSummary_NotFound_ReturnsNotFoundError()
        {
            var svc = CreateService();

            var result = await svc.GetCaseSummaryAsync("nonexistent-case-id", "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetCaseSummary_NoEvidence_HasBlockerCount_GreaterThanZero()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            // No evidence captured → MissingEvidence fail-closed blocker
            Assert.That(result.Summary!.BlockerCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetCaseSummary_WithEvidence_HasZeroBlockers()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline,
                ExpiresAt = _baseline.AddDays(180)
            }, "actor");

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Summary!.BlockerCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetCaseSummary_WithSlaMetadata_ReturnsCorrectUrgencyBand()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);

            // Set SLA with review due in 2 days → Critical band
            await svc.SetSlaMetadataAsync(caseId, new SetSlaMetadataRequest
            {
                ReviewDueAt = _baseline.AddDays(2)
            }, "actor");

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Summary!.UrgencyBand, Is.EqualTo(CaseUrgencyBand.Critical));
        }

        [Test]
        public async Task GetCaseSummary_WithAssignment_ReturnsAssignedReviewer()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "reviewer-42" }, "actor");

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Summary!.AssignedReviewerId, Is.EqualTo("reviewer-42"));
        }

        [Test]
        public async Task GetCaseSummary_WithOpenEscalation_CountsCorrectly()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEscalationAsync(caseId, new AddEscalationRequest
            {
                Type = EscalationType.SanctionsHit,
                Description = "Potential sanctions match",
                RequiresManualReview = true
            }, "actor");

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Summary!.OpenEscalations, Is.EqualTo(1));
        }

        [Test]
        public async Task GetCaseSummary_NextActionDescription_NotNullOrEmpty()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Summary!.NextActionDescription, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetCaseSummary_ApprovedState_NextActionMentionsHandoff()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);
            // Add evidence so case can progress
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");

            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "actor");

            var result = await svc.GetCaseSummaryAsync(caseId, "actor");

            Assert.That(result.Summary!.NextActionDescription, Does.Contain("handoff").IgnoreCase
                .Or.Contain("approved").IgnoreCase);
        }

        // ═════════════════════════════════════════════════════════════════════
        // ListCaseSummariesAsync tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ListCaseSummaries_EmptyStore_ReturnsEmptyList()
        {
            var svc = CreateService();

            var result = await svc.ListCaseSummariesAsync(new ListComplianceCasesRequest(), "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Summaries, Is.Empty);
        }

        [Test]
        public async Task ListCaseSummaries_MultipleCases_ReturnsSummariesForAll()
        {
            var svc = CreateService();
            await CreateCaseAsync(svc, subjectId: "subject-1");
            await CreateCaseAsync(svc, subjectId: "subject-2");
            await CreateCaseAsync(svc, subjectId: "subject-3");

            var result = await svc.ListCaseSummariesAsync(new ListComplianceCasesRequest(), "actor");

            Assert.That(result.Summaries.Count, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ListCaseSummaries_FilterByState_ReturnsOnlyMatchingCases()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc, subjectId: "subject-1");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor");
            await CreateCaseAsync(svc, subjectId: "subject-2");

            var result = await svc.ListCaseSummariesAsync(
                new ListComplianceCasesRequest { State = ComplianceCaseState.EvidencePending }, "actor");

            Assert.That(result.Summaries.Count, Is.EqualTo(1));
            Assert.That(result.Summaries[0].State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task ListCaseSummaries_SummaryContainsBlockerCount()
        {
            var svc = CreateService();
            await CreateCaseAsync(svc);

            var result = await svc.ListCaseSummariesAsync(new ListComplianceCasesRequest(), "actor");

            // No evidence → MissingEvidence blocker
            Assert.That(result.Summaries[0].BlockerCount, Is.GreaterThan(0));
        }

        // ═════════════════════════════════════════════════════════════════════
        // EvaluateBlockersAsync — blocker taxonomy tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateBlockers_NotFound_ReturnsNotFoundError()
        {
            var svc = CreateService();

            var result = await svc.EvaluateBlockersAsync("bad-id", "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task EvaluateBlockers_NoEvidence_HasMissingEvidenceBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.MissingEvidence));
        }

        [Test]
        public async Task EvaluateBlockers_WithValidEvidence_CanProceed()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.CanProceed, Is.True);
            Assert.That(result.FailClosedBlockers, Is.Empty);
        }

        [Test]
        public async Task EvaluateBlockers_StaleEvidence_HasStaleEvidenceBlocker()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "AML", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(30)
            }, "actor");

            // Advance time past evidence expiry
            tp.Advance(TimeSpan.FromDays(60));

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.StaleEvidence));
        }

        [Test]
        public async Task EvaluateBlockers_OpenSanctionsEscalation_HasUnresolvedSanctionsBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.AddEscalationAsync(caseId, new AddEscalationRequest
            {
                Type = EscalationType.SanctionsHit,
                Description = "OFAC sanctions match detected",
                RequiresManualReview = true
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            var sanctionsBlocker = result.FailClosedBlockers
                .FirstOrDefault(b => b.Category == CaseBlockerCategory.UnresolvedSanctions);
            Assert.That(sanctionsBlocker, Is.Not.Null);
            Assert.That(sanctionsBlocker!.Title, Does.Contain("Sanctions review").IgnoreCase);
        }

        [Test]
        public async Task EvaluateBlockers_OpenWatchlistEscalation_HasOpenEscalationBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "AML", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.AddEscalationAsync(caseId, new AddEscalationRequest
            {
                Type = EscalationType.WatchlistMatch,
                Description = "Potential watchlist match",
                RequiresManualReview = true
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.OpenEscalation));
        }

        [Test]
        public async Task EvaluateBlockers_OpenBlockingRemediationTask_HasOpenRemediationTaskBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.AddRemediationTaskAsync(caseId, new AddRemediationTaskRequest
            {
                Title = "Provide source of funds documentation",
                IsBlockingCase = true,
                BlockerSeverity = EvidenceIssueSeverityLevel.High
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.OpenRemediationTask));
        }

        [Test]
        public async Task EvaluateBlockers_SlaBreached_HasSlaBreachedAdvisory()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");

            // Set SLA due date in the past
            await svc.SetSlaMetadataAsync(caseId, new SetSlaMetadataRequest
            {
                ReviewDueAt = _baseline.AddDays(-1) // already overdue
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            var slaBlocker = result.Warnings.FirstOrDefault(b => b.Category == CaseBlockerCategory.SlaBreached);
            Assert.That(slaBlocker, Is.Not.Null, "Expected SlaBreached advisory in warnings");
            Assert.That(slaBlocker!.IsFailClosed, Is.False); // advisory only
        }

        [Test]
        public async Task EvaluateBlockers_NoAssignment_HasMissingAssignmentAdvisory()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.Warnings, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.MissingAssignment));
        }

        [Test]
        public async Task EvaluateBlockers_WithAssignment_NoMissingAssignmentAdvisory()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "reviewer-1" }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.Warnings, Has.None.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.MissingAssignment));
        }

        [Test]
        public async Task EvaluateBlockers_AdverseKycDecision_HasPendingKycDecisionBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycRejection,
                DecisionSummary = "Identity verification failed",
                Outcome = "Rejected",
                IsAdverse = true
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.PendingKycDecision));
        }

        [Test]
        public async Task EvaluateBlockers_AdverseAmlDecision_HasPendingAmlDecisionBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "AML", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlHit,
                DecisionSummary = "Suspicious transaction pattern detected",
                Outcome = "Hit",
                IsAdverse = true
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.PendingAmlDecision));
        }

        [Test]
        public async Task EvaluateBlockers_HandoffFailed_HasDownstreamDeliveryFailureBlocker()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(90)
            }, "actor");
            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.Failed,
                BlockingReason = "Distribution system returned HTTP 500"
            }, "actor");

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.FailClosedBlockers, Has.Some.Matches<CaseBlocker>(
                b => b.Category == CaseBlockerCategory.DownstreamDeliveryFailure));
        }

        [Test]
        public async Task EvaluateBlockers_AllBlockersHaveRemediationHints()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            foreach (var blocker in result.Blockers)
                Assert.That(blocker.RemediationHint, Is.Not.Null.And.Not.Empty,
                    $"Blocker '{blocker.Category}' is missing a RemediationHint");
        }

        [Test]
        public async Task EvaluateBlockers_BlockersSnapshottedOnCase()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.EvaluateBlockersAsync(caseId, "actor");

            // After evaluation, blockers should be snapshotted on the case
            var getResult = await svc.GetCaseAsync(caseId, "actor");
            Assert.That(getResult.Case!.Blockers, Is.Not.Null);
        }

        // ═════════════════════════════════════════════════════════════════════
        // AddDecisionRecordAsync tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AddDecisionRecord_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval,
                DecisionSummary = "Identity verified successfully",
                Outcome = "Approved",
                IsAdverse = false
            }, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.DecisionRecord, Is.Not.Null);
            Assert.That(result.DecisionRecord!.DecisionId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.DecisionRecord.Kind, Is.EqualTo(CaseDecisionKind.KycApproval));
        }

        [Test]
        public async Task AddDecisionRecord_MissingSummary_ReturnsInvalidRequestError()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval,
                DecisionSummary = "" // empty
            }, "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        }

        [Test]
        public async Task AddDecisionRecord_NotFound_ReturnsNotFoundError()
        {
            var svc = CreateService();

            var result = await svc.AddDecisionRecordAsync("bad-id", new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval,
                DecisionSummary = "Test"
            }, "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task AddDecisionRecord_AppendsTimelineEntry()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlClear,
                DecisionSummary = "AML screening passed",
                Outcome = "Clear",
                IsAdverse = false
            }, "actor");

            var timeline = await svc.GetTimelineAsync(caseId, "actor");
            Assert.That(timeline.Entries, Has.Some.Matches<CaseTimelineEntry>(
                e => e.EventType == CaseTimelineEventType.DecisionRecorded));
        }

        [Test]
        public async Task AddDecisionRecord_EmitsWebhookEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId = await CreateCaseAsync(svc);

            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.SanctionsReview,
                DecisionSummary = "Sanctions review: no confirmed match",
                Outcome = "Clear",
                IsAdverse = false
            }, "actor");

            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseDecisionRecorded);

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents,
                    Has.Some.Matches<WebhookEvent>(e => e.EventType == WebhookEventType.ComplianceCaseDecisionRecorded));
        }

        [Test]
        public async Task AddDecisionRecord_MultipleRecords_AllPersistedToHistory()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval, DecisionSummary = "KYC passed", IsAdverse = false
            }, "actor");
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlClear, DecisionSummary = "AML passed", IsAdverse = false
            }, "actor");
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.ApprovalWorkflowOutcome, DecisionSummary = "Compliance approved", IsAdverse = false
            }, "actor");

            var history = await svc.GetDecisionHistoryAsync(caseId, "actor");
            Assert.That(history.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task AddDecisionRecord_WithAttributes_AttributesPersisted()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlClear,
                DecisionSummary = "AML screening clear",
                Attributes = new Dictionary<string, string>
                {
                    ["screeningEngine"] = "Acme AML v2.1",
                    ["matchScore"] = "0.12"
                }
            }, "actor");

            var history = await svc.GetDecisionHistoryAsync(caseId, "actor");
            Assert.That(history.Decisions[0].Attributes["screeningEngine"], Is.EqualTo("Acme AML v2.1"));
            Assert.That(history.Decisions[0].Attributes["matchScore"], Is.EqualTo("0.12"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // GetDecisionHistoryAsync tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetDecisionHistory_EmptyCase_ReturnsEmptyHistory()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.GetDecisionHistoryAsync(caseId, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decisions, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetDecisionHistory_NotFound_ReturnsNotFoundError()
        {
            var svc = CreateService();

            var result = await svc.GetDecisionHistoryAsync("bad-id", "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetDecisionHistory_AdverseCount_CountsCorrectly()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval, DecisionSummary = "OK", IsAdverse = false
            }, "actor");
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlHit, DecisionSummary = "AML hit detected", IsAdverse = true
            }, "actor");
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.SanctionsReview, DecisionSummary = "Sanctions match", IsAdverse = true
            }, "actor");

            var result = await svc.GetDecisionHistoryAsync(caseId, "actor");

            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.AdverseCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GetDecisionHistory_OrderedChronologically()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);
            var caseId = await CreateCaseAsync(svc);

            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval, DecisionSummary = "First", IsAdverse = false
            }, "actor");
            tp.Advance(TimeSpan.FromMinutes(10));
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlClear, DecisionSummary = "Second", IsAdverse = false
            }, "actor");

            var result = await svc.GetDecisionHistoryAsync(caseId, "actor");

            Assert.That(result.Decisions[0].DecisionSummary, Is.EqualTo("First"));
            Assert.That(result.Decisions[1].DecisionSummary, Is.EqualTo("Second"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // UpdateHandoffStatusAsync tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpdateHandoffStatus_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.ApprovalWorkflowPending,
                BlockingReason = "Awaiting compliance manager approval"
            }, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.HandoffStatus, Is.Not.Null);
            Assert.That(result.HandoffStatus!.Stage, Is.EqualTo(CaseHandoffStage.ApprovalWorkflowPending));
            Assert.That(result.HandoffStatus.IsHandoffReady, Is.False);
        }

        [Test]
        public async Task UpdateHandoffStatus_CompletedStage_IsHandoffReadyTrue()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.Completed
            }, "actor");

            Assert.That(result.HandoffStatus!.IsHandoffReady, Is.True);
            Assert.That(result.HandoffStatus.HandoffCompletedAt, Is.Not.Null);
        }

        [Test]
        public async Task UpdateHandoffStatus_NotFound_ReturnsNotFoundError()
        {
            var svc = CreateService();

            var result = await svc.UpdateHandoffStatusAsync("bad-id", new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.RegulatoryPackagePending
            }, "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task UpdateHandoffStatus_EmitsWebhookEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId = await CreateCaseAsync(svc);

            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.DistributionPending,
                BlockingReason = "Waiting for distribution system"
            }, "actor");

            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseHandoffStatusChanged);

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents,
                    Has.Some.Matches<WebhookEvent>(e => e.EventType == WebhookEventType.ComplianceCaseHandoffStatusChanged));
        }

        [Test]
        public async Task UpdateHandoffStatus_AppendsTimelineEntry()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.RegulatoryPackagePending,
                BlockingReason = "Regulatory package not yet prepared"
            }, "actor");

            var timeline = await svc.GetTimelineAsync(caseId, "actor");
            Assert.That(timeline.Entries, Has.Some.Matches<CaseTimelineEntry>(
                e => e.EventType == CaseTimelineEventType.HandoffStatusChanged));
        }

        [Test]
        public async Task UpdateHandoffStatus_FailedStage_BlockingReasonPersisted()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.Failed,
                BlockingReason = "Distribution service returned 503"
            }, "actor");

            var get = await svc.GetHandoffStatusAsync(caseId, "actor");
            Assert.That(get.HandoffStatus!.BlockingReason, Is.EqualTo("Distribution service returned 503"));
        }

        [Test]
        public async Task UpdateHandoffStatus_WithUnresolvedDependencies_PersistsDependencies()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.ApprovalWorkflowPending,
                UnresolvedDependencies = new List<string> { "approval-001", "approval-002" }
            }, "actor");

            var get = await svc.GetHandoffStatusAsync(caseId, "actor");
            Assert.That(get.HandoffStatus!.UnresolvedDependencies, Has.Member("approval-001"));
            Assert.That(get.HandoffStatus.UnresolvedDependencies, Has.Member("approval-002"));
        }

        [Test]
        public async Task UpdateHandoffStatus_OverwritesPreviousStatus()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.ApprovalWorkflowPending
            }, "actor");
            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.Completed
            }, "actor");

            var get = await svc.GetHandoffStatusAsync(caseId, "actor");
            Assert.That(get.HandoffStatus!.Stage, Is.EqualTo(CaseHandoffStage.Completed));
            Assert.That(get.HandoffStatus.IsHandoffReady, Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // GetHandoffStatusAsync tests
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetHandoffStatus_NotFound_ReturnsNotFoundError()
        {
            var svc = CreateService();

            var result = await svc.GetHandoffStatusAsync("bad-id", "actor");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetHandoffStatus_NoHandoffSet_ReturnsSuccessWithNullHandoff()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAsync(svc);

            var result = await svc.GetHandoffStatusAsync(caseId, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.HandoffStatus, Is.Null); // no handoff set yet
        }

        // ═════════════════════════════════════════════════════════════════════
        // End-to-end case lifecycle with maturity features
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task E2E_CaseLifecycle_IntakeToApprovalWithDecisionsAndHandoff()
        {
            var (svc, ws, tp) = CreateServiceWithCapture();

            // 1. Create case
            var caseId = await CreateCaseAsync(svc, "issuer-e2e", "investor-001");

            // 2. Add evidence
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC",
                Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline,
                ExpiresAt = _baseline.AddDays(365),
                Summary = "Full identity verification passed"
            }, "kyc-provider");

            // 3. Evaluate blockers — should have MissingAssignment advisory only
            var blockerResult = await svc.EvaluateBlockersAsync(caseId, "actor");
            Assert.That(blockerResult.FailClosedBlockers, Is.Empty, "No fail-closed blockers after valid evidence");

            // 4. Assign reviewer
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest
            {
                ReviewerId = "senior-analyst-1",
                Reason = "Assigned for investor onboarding review"
            }, "manager");

            // 5. Transition to review
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest
            {
                NewState = ComplianceCaseState.EvidencePending
            }, "senior-analyst-1");
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest
            {
                NewState = ComplianceCaseState.UnderReview
            }, "senior-analyst-1");

            // 6. Record KYC approval decision
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.KycApproval,
                DecisionSummary = "Full KYC passed: identity confirmed",
                Outcome = "Approved",
                IsAdverse = false,
                ProviderName = "Jumio",
                Explanation = "Passport and selfie match confirmed"
            }, "senior-analyst-1");

            // 7. Record AML clear decision
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlClear,
                DecisionSummary = "AML screening: no matches",
                Outcome = "Clear",
                IsAdverse = false
            }, "senior-analyst-1");

            // 8. Approve case
            await svc.TransitionStateAsync(caseId, new TransitionCaseStateRequest
            {
                NewState = ComplianceCaseState.Approved,
                Reason = "All checks passed"
            }, "senior-analyst-1");

            // 9. Update handoff to approval workflow pending
            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.ApprovalWorkflowPending,
                BlockingReason = "Distribution awaiting final approval"
            }, "senior-analyst-1");

            // 10. Complete handoff
            await svc.UpdateHandoffStatusAsync(caseId, new UpdateHandoffStatusRequest
            {
                Stage = CaseHandoffStage.Completed
            }, "senior-analyst-1");

            // ── Verify final state ──
            var summary = await svc.GetCaseSummaryAsync(caseId, "actor");
            Assert.That(summary.Summary!.State, Is.EqualTo(ComplianceCaseState.Approved));
            Assert.That(summary.Summary.IsHandoffReady, Is.True);
            Assert.That(summary.Summary.DecisionCount, Is.EqualTo(2));
            Assert.That(summary.Summary.HandoffStage, Is.EqualTo(CaseHandoffStage.Completed));

            var decisionHistory = await svc.GetDecisionHistoryAsync(caseId, "actor");
            Assert.That(decisionHistory.TotalCount, Is.EqualTo(2));
            Assert.That(decisionHistory.AdverseCount, Is.EqualTo(0));

            var handoff = await svc.GetHandoffStatusAsync(caseId, "actor");
            Assert.That(handoff.HandoffStatus!.IsHandoffReady, Is.True);
            Assert.That(handoff.HandoffStatus.HandoffCompletedAt, Is.Not.Null);

            // Timeline should include decision and handoff entries
            var timeline = await svc.GetTimelineAsync(caseId, "actor");
            Assert.That(timeline.Entries, Has.Some.Matches<CaseTimelineEntry>(
                e => e.EventType == CaseTimelineEventType.DecisionRecorded));
            Assert.That(timeline.Entries, Has.Some.Matches<CaseTimelineEntry>(
                e => e.EventType == CaseTimelineEventType.HandoffStatusChanged));
        }

        [Test]
        public async Task E2E_BlockedCase_AllBlockerCategoriesExplicitlyRepresented()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var svc = CreateService(timeProvider: tp);

            // Create case
            var caseId = await CreateCaseAsync(svc);

            // Add stale evidence
            await svc.AddEvidenceAsync(caseId, new AddEvidenceRequest
            {
                EvidenceType = "KYC", Status = CaseEvidenceStatus.Valid,
                CapturedAt = _baseline, ExpiresAt = _baseline.AddDays(10)
            }, "actor");

            // Add sanctions escalation
            await svc.AddEscalationAsync(caseId, new AddEscalationRequest
            {
                Type = EscalationType.SanctionsHit,
                Description = "OFAC match",
                RequiresManualReview = true
            }, "actor");

            // Add blocking remediation task
            await svc.AddRemediationTaskAsync(caseId, new AddRemediationTaskRequest
            {
                Title = "Provide source of wealth proof",
                IsBlockingCase = true
            }, "actor");

            // Add adverse AML decision
            await svc.AddDecisionRecordAsync(caseId, new AddDecisionRecordRequest
            {
                Kind = CaseDecisionKind.AmlHit, DecisionSummary = "AML: high-risk flag", IsAdverse = true
            }, "actor");

            // Advance time to make evidence stale
            tp.Advance(TimeSpan.FromDays(30));

            var result = await svc.EvaluateBlockersAsync(caseId, "actor");

            Assert.That(result.CanProceed, Is.False);

            var categories = result.FailClosedBlockers.Select(b => b.Category).ToHashSet();
            Assert.That(categories, Has.Member(CaseBlockerCategory.UnresolvedSanctions));
            Assert.That(categories, Has.Member(CaseBlockerCategory.OpenRemediationTask));
            Assert.That(categories, Has.Member(CaseBlockerCategory.PendingAmlDecision));
            Assert.That(categories, Has.Member(CaseBlockerCategory.StaleEvidence));

            // Top blocker title should be non-null and human-readable
            var topBlocker = result.FailClosedBlockers
                .OrderByDescending(b => b.Severity).First();
            Assert.That(topBlocker.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(topBlocker.RemediationHint, Is.Not.Null.And.Not.Empty);
        }
    }
}

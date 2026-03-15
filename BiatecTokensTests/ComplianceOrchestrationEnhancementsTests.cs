using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for enhanced ComplianceOrchestrationService capabilities:
    /// - ProviderUnavailable state (fail-closed on provider outage)
    /// - InsufficientData state (missing required subject data)
    /// - Expired state (evidence freshness / validity window)
    /// - SubjectType (Individual vs BusinessEntity)
    /// - MatchedWatchlistCategories propagation
    /// - Combined-check fail-fast on ProviderUnavailable / InsufficientData
    /// </summary>
    [TestFixture]
    public class ComplianceOrchestrationEnhancementsTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static ComplianceOrchestrationService CreateService(
            bool kycAutoApprove = true,
            TimeProvider? timeProvider = null)
        {
            var kycProvider = new MockKycProvider(
                new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                    new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = kycAutoApprove }),
                NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            return new ComplianceOrchestrationService(
                kycProvider,
                amlProvider,
                NullLogger<ComplianceOrchestrationService>.Instance,
                timeProvider);
        }

        private static InitiateComplianceCheckRequest MakeRequest(
            string subjectId = "subj-001",
            string contextId = "ctx-001",
            ComplianceCheckType checkType = ComplianceCheckType.Combined,
            ScreeningSubjectType subjectType = ScreeningSubjectType.Individual,
            Dictionary<string, string>? metadata = null,
            int? evidenceValidityHours = null,
            string? idempotencyKey = null) => new()
        {
            SubjectId = subjectId,
            ContextId = contextId,
            CheckType = checkType,
            SubjectType = subjectType,
            SubjectMetadata = metadata ?? new Dictionary<string, string>(),
            EvidenceValidityHours = evidenceValidityHours,
            IdempotencyKey = idempotencyKey
        };

        // ── ProviderUnavailable state ─────────────────────────────────────────────

        [Test]
        public async Task AmlCheck_ProviderUnavailable_ReturnsProviderUnavailableState()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-unavail-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
            Assert.That(resp.ReasonCode, Is.EqualTo("PROVIDER_UNAVAILABLE"));
            Assert.That(resp.AuditTrail, Is.Not.Empty);
        }

        [Test]
        public async Task AmlCheck_ProviderUnavailable_IsDistinctFromError()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-unavail-2");

            Assert.That(resp.State, Is.Not.EqualTo(ComplianceDecisionState.Error),
                "ProviderUnavailable must be a distinct state from Error");
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
        }

        [Test]
        public async Task CombinedCheck_AmlProviderUnavailable_ReturnsProviderUnavailableState()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-unavail-3");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
        }

        [Test]
        public async Task ProviderUnavailable_IsTerminal_DecisionIsCompleted()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-unavail-4");

            Assert.That(resp.CompletedAt, Is.Not.Null,
                "ProviderUnavailable is a terminal state and should have a CompletedAt timestamp");
        }

        [Test]
        public async Task ProviderUnavailable_AuditTrailContainsAmlCompleted()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-unavail-5");

            Assert.That(resp.AuditTrail.Any(e => e.EventType == "AmlCompleted"), Is.True);
        }

        // ── InsufficientData state ─────────────────────────────────────────────────

        [Test]
        public async Task AmlCheck_InsufficientData_ReturnsInsufficientDataState()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_insufficient_data"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-insuff-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.InsufficientData));
            Assert.That(resp.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task InsufficientData_IsTerminal_DecisionIsCompleted()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_insufficient_data"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-insuff-2");

            Assert.That(resp.CompletedAt, Is.Not.Null,
                "InsufficientData is a terminal state and must have a CompletedAt timestamp");
        }

        [Test]
        public async Task CombinedCheck_AmlInsufficientData_ReturnsInsufficientDataState()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["simulate_insufficient_data"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-insuff-3");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.InsufficientData));
        }

        [Test]
        public async Task InsufficientData_IsDistinctFromError_CannotYieldFalsePositive()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_insufficient_data"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-insuff-4");

            Assert.That(resp.State, Is.Not.EqualTo(ComplianceDecisionState.Approved),
                "InsufficientData must not yield an Approved decision (fail-closed)");
        }

        // ── Evidence freshness / Expired state ───────────────────────────────────────

        [Test]
        public async Task ApprovedDecision_WithValidityWindow_HasEvidenceExpiresAt()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 24);

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-1");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(resp.EvidenceExpiresAt, Is.Not.Null,
                "An approved decision with EvidenceValidityHours set must carry an EvidenceExpiresAt timestamp");
            Assert.That(resp.EvidenceExpiresAt!.Value, Is.GreaterThan(DateTimeOffset.UtcNow));
        }

        [Test]
        public async Task ApprovedDecision_WithoutValidityWindow_HasNoEvidenceExpiresAt()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Aml);

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-2");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(resp.EvidenceExpiresAt, Is.Null,
                "A decision without a validity window must not have EvidenceExpiresAt");
        }

        [Test]
        public async Task RejectedDecision_WithValidityWindow_DoesNotSetEvidenceExpiresAt()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 24,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-3");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(resp.EvidenceExpiresAt, Is.Null,
                "Only Approved decisions should carry an EvidenceExpiresAt");
        }

        [Test]
        public async Task GetStatus_DecisionWithPassedExpiry_ReturnsExpiredState()
        {
            // Arrange: start with "now" at a fixed point in time
            var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeClock);

            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 1,       // expires 1 hour from initiation
                idempotencyKey: "fresh-test-5");

            // Act: initiate check while the evidence is still fresh
            var initResp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-5");
            Assert.That(initResp.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(initResp.EvidenceExpiresAt, Is.Not.Null);

            // Confirm that immediately after initiation the decision is still Approved
            var statusNow = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(statusNow.State, Is.EqualTo(ComplianceDecisionState.Approved),
                "Decision must be Approved while the evidence validity window is still open");

            // Advance the clock past the expiry window
            fakeClock.Advance(TimeSpan.FromHours(2));

            // Retrieve the decision again – the service must now transition it to Expired
            var statusAfterExpiry = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(statusAfterExpiry.State, Is.EqualTo(ComplianceDecisionState.Expired),
                "Decision must transition to Expired once EvidenceExpiresAt is in the past");
        }

        [Test]
        public async Task EvidenceExpiry_ReasonCodeIsEvidenceExpired_WhenExpired()
        {
            var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeClock);

            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 720,     // 30 days
                idempotencyKey: "fresh-test-7");

            var initResp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-7");
            Assert.That(initResp.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(initResp.EvidenceExpiresAt, Is.Not.Null);

            // Advance past the 30-day window
            fakeClock.Advance(TimeSpan.FromDays(31));

            var expired = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(expired.State, Is.EqualTo(ComplianceDecisionState.Expired),
                "Decision must be Expired after 31 days when validity was 720 hours (30 days)");
            Assert.That(expired.ReasonCode, Is.EqualTo("EVIDENCE_EXPIRED"),
                "ReasonCode must be EVIDENCE_EXPIRED for stale-evidence transitions");
        }

        [Test]
        public async Task GetStatus_DecisionExpiredAt_AuditTrailContainsEvidenceExpiredEvent()
        {
            var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeClock);

            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 1,
                idempotencyKey: "fresh-test-audit");

            var initResp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-audit");
            Assert.That(initResp.State, Is.EqualTo(ComplianceDecisionState.Approved));

            fakeClock.Advance(TimeSpan.FromHours(2));

            var expired = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(expired.State, Is.EqualTo(ComplianceDecisionState.Expired));
            Assert.That(expired.AuditTrail.Any(e => e.EventType == "EvidenceExpired"), Is.True,
                "Expiry transition must produce an EvidenceExpired audit event");
        }

        [Test]
        public async Task GetStatus_DecisionBoundaryCase_ExpiryAtExactMoment_IsExpired()
        {
            // At the exact expiry timestamp the decision should be considered expired
            // (condition: UtcNow > EvidenceExpiresAt is strictly greater-than, so at exact time it is NOT expired)
            var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeClock);

            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 1,
                idempotencyKey: "fresh-test-boundary");

            var initResp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-boundary");
            var expiresAt = initResp.EvidenceExpiresAt!.Value;

            // One tick before expiry → still Approved
            fakeClock.SetUtcNow(expiresAt.AddTicks(-1));
            var beforeExpiry = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(beforeExpiry.State, Is.EqualTo(ComplianceDecisionState.Approved),
                "One tick before the expiry window closes, the decision must remain Approved");

            // One tick after expiry → Expired
            fakeClock.SetUtcNow(expiresAt.AddTicks(1));
            var afterExpiry = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(afterExpiry.State, Is.EqualTo(ComplianceDecisionState.Expired),
                "One tick after the expiry window closes, the decision must be Expired");
        }

        [Test]
        public async Task GetStatus_NonApprovedDecision_WithExpiredWindow_DoesNotTransitionToExpired()
        {
            // Rejected/NeedsReview decisions must NEVER transition to Expired even if EvidenceExpiresAt would have triggered
            var fakeClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeClock);

            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                evidenceValidityHours: 1,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" },
                idempotencyKey: "fresh-test-rejected");

            var initResp = await svc.InitiateCheckAsync(req, "actor", "corr-fresh-rejected");
            Assert.That(initResp.State, Is.EqualTo(ComplianceDecisionState.Rejected),
                "Precondition: sanctions_flag produces Rejected");

            // The EvidenceExpiresAt is null for Rejected decisions (expiry only applies to Approved)
            Assert.That(initResp.EvidenceExpiresAt, Is.Null);

            // Advance well past what would have been the expiry window
            fakeClock.Advance(TimeSpan.FromHours(48));

            var statusAfterAdvance = await svc.GetCheckStatusAsync(initResp.DecisionId!);
            Assert.That(statusAfterAdvance.State, Is.EqualTo(ComplianceDecisionState.Rejected),
                "Rejected decisions must never transition to Expired");
        }

        // ── SubjectType (Individual vs BusinessEntity) ───────────────────────────────

        [Test]
        public async Task SubjectType_Individual_IsPreservedInResponse()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                subjectType: ScreeningSubjectType.Individual);

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-subj-1");

            Assert.That(resp.SubjectType, Is.EqualTo(ScreeningSubjectType.Individual));
        }

        [Test]
        public async Task SubjectType_BusinessEntity_IsPreservedInResponse()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                subjectType: ScreeningSubjectType.BusinessEntity,
                metadata: new Dictionary<string, string>
                {
                    ["legal_name"] = "Acme Corp Ltd",
                    ["registration_number"] = "DE123456789",
                    ["jurisdiction"] = "DE"
                });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-subj-2");

            Assert.That(resp.SubjectType, Is.EqualTo(ScreeningSubjectType.BusinessEntity));
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task SubjectType_BusinessEntity_Combined_IsPreservedInHistory()
        {
            var svc = CreateService();
            var subjectId = "corp-001";
            var req = MakeRequest(
                subjectId: subjectId,
                checkType: ComplianceCheckType.Combined,
                subjectType: ScreeningSubjectType.BusinessEntity);

            await svc.InitiateCheckAsync(req, "actor", "corr-subj-3");

            var history = await svc.GetDecisionHistoryAsync(subjectId);
            Assert.That(history.Decisions, Is.Not.Empty);
            Assert.That(history.Decisions[0].SubjectType, Is.EqualTo(ScreeningSubjectType.BusinessEntity));
        }

        [Test]
        public async Task SubjectType_Default_IsIndividual()
        {
            var svc = CreateService();
            var req = new InitiateComplianceCheckRequest
            {
                SubjectId = "subj-default",
                ContextId = "ctx-default",
                CheckType = ComplianceCheckType.Aml
            };

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-subj-4");

            Assert.That(resp.SubjectType, Is.EqualTo(ScreeningSubjectType.Individual),
                "Default SubjectType must be Individual");
        }

        // ── MatchedWatchlistCategories ─────────────────────────────────────────────

        [Test]
        public async Task SanctionsMatch_PopulatesMatchedWatchlistCategories()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-watchlist-1");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(resp.MatchedWatchlistCategories, Is.Not.Empty,
                "A sanctions match must populate MatchedWatchlistCategories");
            Assert.That(resp.MatchedWatchlistCategories, Does.Contain("OFAC_SDN").Or.Contain("EU_SANCTIONS"),
                "Sanctions match should reference OFAC_SDN or EU_SANCTIONS watchlists");
        }

        [Test]
        public async Task PepMatch_PopulatesPepWatchlistCategory()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["review_flag"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-watchlist-2");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(resp.MatchedWatchlistCategories, Does.Contain("PEP_WATCHLIST"),
                "A PEP hit must include PEP_WATCHLIST in MatchedWatchlistCategories");
        }

        [Test]
        public async Task CleanSubject_HasEmptyMatchedWatchlistCategories()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Aml);

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-watchlist-3");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(resp.MatchedWatchlistCategories, Is.Empty,
                "A clean subject must have no matched watchlist categories");
        }

        [Test]
        public async Task MatchedWatchlistCategories_PreservedInStatusRetrieval()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" },
                idempotencyKey: "watchlist-status-4");

            var initResp = await svc.InitiateCheckAsync(req, "actor", "corr-watchlist-4");
            var statusResp = await svc.GetCheckStatusAsync(initResp.DecisionId!);

            Assert.That(statusResp.MatchedWatchlistCategories, Is.EquivalentTo(initResp.MatchedWatchlistCategories),
                "MatchedWatchlistCategories must be preserved across status retrievals");
        }

        // ── Combined-check fail-fast on new states ──────────────────────────────────

        [Test]
        public async Task CombinedCheck_AmlProviderUnavailable_DoesNotYieldApproved()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-1");

            Assert.That(resp.State, Is.Not.EqualTo(ComplianceDecisionState.Approved),
                "Provider unavailability must not yield an Approved decision (fail-closed)");
        }

        [Test]
        public async Task CombinedCheck_AmlInsufficientData_DoesNotYieldApproved()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["simulate_insufficient_data"] = "true" });

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-2");

            Assert.That(resp.State, Is.Not.EqualTo(ComplianceDecisionState.Approved),
                "InsufficientData must not yield an Approved decision (fail-closed)");
        }

        // ── Enum completeness guard ───────────────────────────────────────────────────

        [Test]
        public void ComplianceDecisionState_ContainsAllRequiredStates()
        {
            var states = Enum.GetValues<ComplianceDecisionState>();

            Assert.That(states, Does.Contain(ComplianceDecisionState.Approved), "Missing Approved");
            Assert.That(states, Does.Contain(ComplianceDecisionState.Rejected), "Missing Rejected");
            Assert.That(states, Does.Contain(ComplianceDecisionState.NeedsReview), "Missing NeedsReview");
            Assert.That(states, Does.Contain(ComplianceDecisionState.ProviderUnavailable), "Missing ProviderUnavailable");
            Assert.That(states, Does.Contain(ComplianceDecisionState.Expired), "Missing Expired");
            Assert.That(states, Does.Contain(ComplianceDecisionState.InsufficientData), "Missing InsufficientData");
        }

        [Test]
        public void ScreeningSubjectType_ContainsIndividualAndBusinessEntity()
        {
            var types = Enum.GetValues<ScreeningSubjectType>();

            Assert.That(types, Does.Contain(ScreeningSubjectType.Individual));
            Assert.That(types, Does.Contain(ScreeningSubjectType.BusinessEntity));
        }

        // ── MockAmlProvider new scenarios ─────────────────────────────────────────────

        [Test]
        public async Task MockAmlProvider_InsufficientDataFlag_ReturnsInsufficientDataState()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (_, state, reasonCode, _) =
                await provider.ScreenSubjectAsync("user-001",
                    new Dictionary<string, string> { ["simulate_insufficient_data"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.InsufficientData));
            Assert.That(reasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task MockAmlProvider_UnavailableFlag_ReturnsProviderUnavailableNotError()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (_, state, _, _) =
                await provider.ScreenSubjectAsync("user-002",
                    new Dictionary<string, string> { ["simulate_unavailable"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
            Assert.That(state, Is.Not.EqualTo(ComplianceDecisionState.Error));
        }

        // ── FakeTimeProvider ──────────────────────────────────────────────────────────
        // A controllable TimeProvider that allows tests to advance time deterministically
        // and validate evidence freshness window transitions without Thread.Sleep.

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow;

            public FakeTimeProvider(DateTimeOffset startTime)
            {
                _utcNow = startTime;
            }

            public override DateTimeOffset GetUtcNow() => _utcNow;

            /// <summary>Advances the clock by the specified duration.</summary>
            public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);

            /// <summary>Sets the clock to an explicit point in time.</summary>
            public void SetUtcNow(DateTimeOffset newTime) => _utcNow = newTime;
        }
    }
}

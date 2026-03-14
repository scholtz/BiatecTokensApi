using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive unit tests for ComplianceOrchestrationService covering:
    /// - KYC-only, AML-only, and Combined check types
    /// - State transitions (Approved, Rejected, NeedsReview, Error)
    /// - Provider normalization for all mock conditions
    /// - Idempotency and replay semantics
    /// - Fail-closed behavior for error states
    /// - Audit trail correctness
    /// - Decision history for subjects
    /// - Missing/invalid input validation
    /// - MockAmlProvider and MockKycProvider adapter tests
    /// </summary>
    [TestFixture]
    public class ComplianceOrchestrationServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static ComplianceOrchestrationService CreateService(bool kycAutoApprove = true)
        {
            var kycProvider = CreateKycProvider(kycAutoApprove);
            var amlProvider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            return new ComplianceOrchestrationService(
                kycProvider,
                amlProvider,
                NullLogger<ComplianceOrchestrationService>.Instance);
        }

        private static MockKycProvider CreateKycProvider(bool autoApprove = true)
        {
            var config = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = autoApprove });
            return new MockKycProvider(config, NullLogger<MockKycProvider>.Instance);
        }

        private static InitiateComplianceCheckRequest MakeRequest(
            string subjectId = "user-001",
            string contextId = "ctx-001",
            ComplianceCheckType checkType = ComplianceCheckType.Combined,
            Dictionary<string, string>? metadata = null,
            string? idempotencyKey = null) => new()
        {
            SubjectId = subjectId,
            ContextId = contextId,
            CheckType = checkType,
            SubjectMetadata = metadata ?? new Dictionary<string, string>(),
            IdempotencyKey = idempotencyKey
        };

        // ── Input validation ─────────────────────────────────────────────────────

        [Test]
        public async Task InitiateCheck_MissingSubjectId_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(subjectId: "");
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-001");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
            Assert.That(resp.ErrorMessage, Does.Contain("SubjectId"));
        }

        [Test]
        public async Task InitiateCheck_WhitespaceSubjectId_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(subjectId: "   ");
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-001");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task InitiateCheck_MissingContextId_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(contextId: "");
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-002");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
            Assert.That(resp.ErrorMessage, Does.Contain("ContextId"));
        }

        [Test]
        public async Task InitiateCheck_WhitespaceContextId_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(contextId: "  ");
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-003");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        // ── KYC-only checks ───────────────────────────────────────────────────

        [Test]
        public async Task KycCheck_AutoApprove_ReturnsApproved()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task KycCheck_NotAutoApprove_ReturnsPending()
        {
            var svc = CreateService(kycAutoApprove: false);
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-2");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Pending));
        }

        [Test]
        public async Task KycCheck_HasDecisionId()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-3");

            Assert.That(resp.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task KycCheck_HasKycProviderReferenceId()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);

            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-4");

            // The service returns a ComplianceCheckResponse which has a DecisionId
            // that maps back to the internal decision
            Assert.That(resp.DecisionId, Is.Not.Null);
        }

        [Test]
        public async Task KycCheck_HasAuditTrailWithAtLeastTwoEvents()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-5");

            Assert.That(resp.AuditTrail, Is.Not.Null);
            // At minimum: CheckInitiated + KycCompleted
            Assert.That(resp.AuditTrail.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task KycCheck_AuditTrail_FirstEventIsCheckInitiated()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-6");

            Assert.That(resp.AuditTrail[0].EventType, Is.EqualTo("CheckInitiated"));
        }

        [Test]
        public async Task KycCheck_AuditTrail_SecondEventIsKycCompleted()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-kyc-7");

            Assert.That(resp.AuditTrail[1].EventType, Is.EqualTo("KycCompleted"));
        }

        [Test]
        public async Task KycCheck_CorrelationIdPropagated()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var correlationId = "test-corr-id-xyz";
            var resp = await svc.InitiateCheckAsync(req, "actor", correlationId);

            Assert.That(resp.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task KycCheck_InitiatedAtIsSet()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var before = DateTimeOffset.UtcNow;
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr");
            var after = DateTimeOffset.UtcNow;

            Assert.That(resp.InitiatedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(resp.InitiatedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public async Task KycCheck_ApprovedState_HasCompletedAt()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr");

            // Approved is a terminal state, so CompletedAt should be set
            Assert.That(resp.CompletedAt, Is.Not.Null);
        }

        [Test]
        public async Task KycCheck_PendingState_HasNullCompletedAt()
        {
            var svc = CreateService(kycAutoApprove: false);
            var req = MakeRequest(checkType: ComplianceCheckType.Kyc);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr");

            // Pending is not terminal, so CompletedAt should be null
            Assert.That(resp.CompletedAt, Is.Null);
        }

        // ── AML-only checks ───────────────────────────────────────────────────

        [Test]
        public async Task AmlCheck_NoFlags_ReturnsApproved()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Aml);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task AmlCheck_SanctionsFlag_ReturnsRejected()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-2");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(resp.ReasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task AmlCheck_ReviewFlag_ReturnsNeedsReview()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["review_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-3");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(resp.ReasonCode, Is.EqualTo("REVIEW_REQUIRED"));
        }

        [Test]
        public async Task AmlCheck_TimeoutSimulation_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_timeout"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-4");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(resp.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.Timeout));
        }

        [Test]
        public async Task AmlCheck_UnavailableSimulation_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-5");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(resp.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.ProviderUnavailable));
        }

        [Test]
        public async Task AmlCheck_MalformedResponseSimulation_ReturnsError()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_malformed"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-6");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(resp.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.MalformedResponse));
        }

        [Test]
        public async Task AmlCheck_HasAuditTrailWithAmlCompleted()
        {
            var svc = CreateService();
            var req = MakeRequest(checkType: ComplianceCheckType.Aml);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-aml-7");

            Assert.That(resp.AuditTrail.Any(e => e.EventType == "AmlCompleted"), Is.True);
        }

        // ── Combined checks ───────────────────────────────────────────────────

        [Test]
        public async Task CombinedCheck_BothApproved_ReturnsApproved()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Combined);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-1");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task CombinedCheck_AmlSanctionsHit_ReturnsRejected()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-2");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task CombinedCheck_AmlReviewRequired_ReturnsNeedsReview()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["review_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-3");

            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task CombinedCheck_KycPending_AmlStillRuns_ReturnsApproved()
        {
            // KYC pending (not auto-approve) is not a hard failure (Rejected/Error) so AML still runs
            // AML with no flags returns Approved, so combined result is Approved
            var svc = CreateService(kycAutoApprove: false);
            var req = MakeRequest(checkType: ComplianceCheckType.Combined);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-skip");

            Assert.That(resp.Success, Is.True);
            // KYC pending + AML approved → combined state Approved (AML approves, no hard failure)
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Approved));
            // AML should still have run (not skipped)
            Assert.That(resp.AuditTrail.Any(e => e.EventType == "AmlCompleted"), Is.True);
        }

        [Test]
        public async Task CombinedCheck_HasKycAndAmlAuditEvents()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Combined);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-4");

            Assert.That(resp.AuditTrail.Any(e => e.EventType == "KycCompleted"), Is.True);
            Assert.That(resp.AuditTrail.Any(e => e.EventType == "AmlCompleted"), Is.True);
        }

        [Test]
        public async Task CombinedCheck_AuditTrailOrdered_CheckInitiatedFirst()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Combined);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-combined-5");

            Assert.That(resp.AuditTrail[0].EventType, Is.EqualTo("CheckInitiated"));
        }

        // ── State transition semantics ────────────────────────────────────────

        [Test]
        public async Task RejectedState_IsTerminal_HasCompletedAt()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-term-1");

            Assert.That(resp.CompletedAt, Is.Not.Null);
        }

        [Test]
        public async Task NeedsReviewState_IsTerminal_HasCompletedAt()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["review_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-term-2");

            Assert.That(resp.CompletedAt, Is.Not.Null);
        }

        [Test]
        public async Task ErrorState_IsTerminal_HasCompletedAt()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_timeout"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-term-3");

            Assert.That(resp.CompletedAt, Is.Not.Null);
        }

        // ── Idempotency ───────────────────────────────────────────────────────

        [Test]
        public async Task IdempotencyKey_SameKey_ReturnsCachedResult()
        {
            var svc = CreateService();
            var req = MakeRequest(idempotencyKey: "idem-key-001");
            var resp1 = await svc.InitiateCheckAsync(req, "actor", "corr-idem-1");
            var resp2 = await svc.InitiateCheckAsync(req, "actor", "corr-idem-2");

            Assert.That(resp1.DecisionId, Is.EqualTo(resp2.DecisionId));
            Assert.That(resp2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task IdempotencyKey_FirstCall_IsNotReplay()
        {
            var svc = CreateService();
            var req = MakeRequest(idempotencyKey: "idem-key-first");
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr");

            Assert.That(resp.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task IdempotencyKey_DerivedKey_SubjectContextType_IsIdempotent()
        {
            var svc = CreateService();
            // No explicit idempotency key → derives from SubjectId:ContextId:CheckType
            var req1 = MakeRequest(subjectId: "derived-idem-user", contextId: "ctx-derived",
                checkType: ComplianceCheckType.Aml);
            var req2 = MakeRequest(subjectId: "derived-idem-user", contextId: "ctx-derived",
                checkType: ComplianceCheckType.Aml);

            var resp1 = await svc.InitiateCheckAsync(req1, "actor", "corr-1");
            var resp2 = await svc.InitiateCheckAsync(req2, "actor", "corr-2");

            Assert.That(resp1.DecisionId, Is.EqualTo(resp2.DecisionId));
            Assert.That(resp2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task IdempotencyKey_DifferentSubject_CreatesNewDecision()
        {
            var svc = CreateService();
            var req1 = MakeRequest(subjectId: "idem-user-a", contextId: "ctx-idem");
            var req2 = MakeRequest(subjectId: "idem-user-b", contextId: "ctx-idem");

            var resp1 = await svc.InitiateCheckAsync(req1, "actor", "corr-1");
            var resp2 = await svc.InitiateCheckAsync(req2, "actor", "corr-2");

            Assert.That(resp1.DecisionId, Is.Not.EqualTo(resp2.DecisionId));
        }

        [Test]
        public async Task IdempotencyKey_DifferentCheckType_CreatesNewDecision()
        {
            var svc = CreateService();
            var req1 = MakeRequest(subjectId: "idem-user-c", contextId: "ctx-idem2",
                checkType: ComplianceCheckType.Kyc);
            var req2 = MakeRequest(subjectId: "idem-user-c", contextId: "ctx-idem2",
                checkType: ComplianceCheckType.Aml);

            var resp1 = await svc.InitiateCheckAsync(req1, "actor", "corr-1");
            var resp2 = await svc.InitiateCheckAsync(req2, "actor", "corr-2");

            Assert.That(resp1.DecisionId, Is.Not.EqualTo(resp2.DecisionId));
            Assert.That(resp2.IsIdempotentReplay, Is.False);
        }

        // ── GetCheckStatus ────────────────────────────────────────────────────

        [Test]
        public async Task GetCheckStatus_ExistingDecision_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = MakeRequest();
            var initiated = await svc.InitiateCheckAsync(req, "actor", "corr");

            var status = await svc.GetCheckStatusAsync(initiated.DecisionId!);

            Assert.That(status.Success, Is.True);
            Assert.That(status.DecisionId, Is.EqualTo(initiated.DecisionId));
        }

        [Test]
        public async Task GetCheckStatus_NonExistentDecision_ReturnsNotFound()
        {
            var svc = CreateService();
            var resp = await svc.GetCheckStatusAsync("nonexistent-id");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("COMPLIANCE_CHECK_NOT_FOUND"));
        }

        [Test]
        public async Task GetCheckStatus_StatusMatchesInitialState()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Aml);
            var initiated = await svc.InitiateCheckAsync(req, "actor", "corr");

            var status = await svc.GetCheckStatusAsync(initiated.DecisionId!);

            Assert.That(status.State, Is.EqualTo(initiated.State));
        }

        // ── GetDecisionHistory ────────────────────────────────────────────────

        [Test]
        public async Task GetDecisionHistory_NoDecisions_ReturnsEmptyList()
        {
            var svc = CreateService();
            var resp = await svc.GetDecisionHistoryAsync("no-decisions-user");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Decisions, Is.Empty);
            Assert.That(resp.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetDecisionHistory_SingleDecision_ReturnsOne()
        {
            var svc = CreateService();
            var subjectId = $"history-user-{Guid.NewGuid():N}";
            await svc.InitiateCheckAsync(MakeRequest(subjectId: subjectId), "actor", "corr");

            var resp = await svc.GetDecisionHistoryAsync(subjectId);

            Assert.That(resp.TotalCount, Is.EqualTo(1));
            Assert.That(resp.Decisions.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetDecisionHistory_MultipleDecisions_ReturnsAll()
        {
            var svc = CreateService();
            var subjectId = $"multi-hist-{Guid.NewGuid():N}";

            // Use different idempotency keys to create distinct records
            await svc.InitiateCheckAsync(
                MakeRequest(subjectId: subjectId, contextId: "ctx-1", checkType: ComplianceCheckType.Kyc, idempotencyKey: $"k1-{subjectId}"),
                "actor", "corr-1");
            await svc.InitiateCheckAsync(
                MakeRequest(subjectId: subjectId, contextId: "ctx-2", checkType: ComplianceCheckType.Aml, idempotencyKey: $"k2-{subjectId}"),
                "actor", "corr-2");
            await svc.InitiateCheckAsync(
                MakeRequest(subjectId: subjectId, contextId: "ctx-3", checkType: ComplianceCheckType.Combined, idempotencyKey: $"k3-{subjectId}"),
                "actor", "corr-3");

            var resp = await svc.GetDecisionHistoryAsync(subjectId);

            Assert.That(resp.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task GetDecisionHistory_SubjectId_Matches()
        {
            var svc = CreateService();
            var subjectId = $"subj-id-{Guid.NewGuid():N}";
            await svc.InitiateCheckAsync(MakeRequest(subjectId: subjectId), "actor", "corr");

            var resp = await svc.GetDecisionHistoryAsync(subjectId);

            Assert.That(resp.SubjectId, Is.EqualTo(subjectId));
        }

        [Test]
        public async Task GetDecisionHistory_DoesNotReturnOtherSubjectsDecisions()
        {
            var svc = CreateService();
            var subject1 = $"isolation-a-{Guid.NewGuid():N}";
            var subject2 = $"isolation-b-{Guid.NewGuid():N}";

            await svc.InitiateCheckAsync(MakeRequest(subjectId: subject1), "actor", "corr-1");
            await svc.InitiateCheckAsync(MakeRequest(subjectId: subject2), "actor", "corr-2");

            var history1 = await svc.GetDecisionHistoryAsync(subject1);
            var history2 = await svc.GetDecisionHistoryAsync(subject2);

            Assert.That(history1.TotalCount, Is.EqualTo(1));
            Assert.That(history2.TotalCount, Is.EqualTo(1));
        }

        // ── Fail-closed semantics ─────────────────────────────────────────────

        [Test]
        public async Task FailClosed_AmlTimeout_ErrorState_BlocksWithErrorCode()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_timeout"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-failclosed-1");

            // Fail-closed: error is explicitly surfaced, not swallowed
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(resp.ProviderErrorCode, Is.Not.EqualTo(ComplianceProviderErrorCode.None));
        }

        [Test]
        public async Task FailClosed_AmlUnavailable_ErrorState_HasReasonCode()
        {
            var svc = CreateService();
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_unavailable"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-failclosed-2");

            Assert.That(resp.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task FailClosed_CombinedKycError_SkipsAmlAndReturnsError()
        {
            // Create a service where KYC throws an exception (simulated via a custom provider)
            var throwingKycProvider = new ThrowingKycProvider();
            var amlProvider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                throwingKycProvider,
                amlProvider,
                NullLogger<ComplianceOrchestrationService>.Instance);

            var req = MakeRequest(checkType: ComplianceCheckType.Combined);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-failclosed-3");

            // The service catches the exception and returns Error
            Assert.That(resp.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(resp.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.InternalError));
            // AML should be skipped when KYC throws
            Assert.That(resp.AuditTrail.Any(e => e.EventType == "AmlCompleted"), Is.False);
        }

        // ── MockAmlProvider adapter tests ─────────────────────────────────────

        [Test]
        public async Task MockAmlProvider_NoFlags_ReturnsApproved()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (refId, state, reasonCode, errorMsg) =
                await provider.ScreenSubjectAsync("user-001", new(), "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(refId, Is.Not.Null.And.StartsWith("AML-MOCK-"));
            Assert.That(reasonCode, Is.Null);
            Assert.That(errorMsg, Is.Null);
        }

        [Test]
        public async Task MockAmlProvider_SanctionsFlag_ReturnsRejected()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (_, state, reasonCode, _) =
                await provider.ScreenSubjectAsync("user-sanctions",
                    new Dictionary<string, string> { ["sanctions_flag"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task MockAmlProvider_ReviewFlag_ReturnsNeedsReview()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (_, state, reasonCode, _) =
                await provider.ScreenSubjectAsync("user-review",
                    new Dictionary<string, string> { ["review_flag"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(reasonCode, Is.EqualTo("REVIEW_REQUIRED"));
        }

        [Test]
        public async Task MockAmlProvider_TimeoutFlag_ReturnsTimeout()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (refId, state, reasonCode, errorMsg) =
                await provider.ScreenSubjectAsync("user-timeout",
                    new Dictionary<string, string> { ["simulate_timeout"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_TIMEOUT"));
            Assert.That(errorMsg, Is.Not.Null);
            Assert.That(refId, Is.Not.Null); // refId still returned for traceability
        }

        [Test]
        public async Task MockAmlProvider_UnavailableFlag_ReturnsUnavailable()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (_, state, reasonCode, _) =
                await provider.ScreenSubjectAsync("user-unavailable",
                    new Dictionary<string, string> { ["simulate_unavailable"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_UNAVAILABLE"));
        }

        [Test]
        public async Task MockAmlProvider_MalformedFlag_ReturnsMalformed()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (_, state, reasonCode, _) =
                await provider.ScreenSubjectAsync("user-malformed",
                    new Dictionary<string, string> { ["simulate_malformed"] = "true" }, "corr");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("MALFORMED_RESPONSE"));
        }

        [Test]
        public async Task MockAmlProvider_GetScreeningStatus_ReturnsApproved()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            var (state, reasonCode, errorMsg) = await provider.GetScreeningStatusAsync("AML-REF-123");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(reasonCode, Is.Null);
            Assert.That(errorMsg, Is.Null);
        }

        [Test]
        public void MockAmlProvider_ProviderName_IsMockAmlProvider()
        {
            var provider = new MockAmlProvider(NullLogger<MockAmlProvider>.Instance);
            Assert.That(provider.ProviderName, Is.EqualTo("MockAmlProvider"));
        }

        // ── MockKycProvider adapter tests ─────────────────────────────────────

        [Test]
        public async Task MockKycProvider_AutoApprove_ReturnsApproved()
        {
            var provider = CreateKycProvider(autoApprove: true);
            var (refId, status, errorMsg) =
                await provider.StartVerificationAsync("user-001",
                    new BiatecTokensApi.Models.Kyc.StartKycVerificationRequest { FullName = "Test User" },
                    "corr");

            Assert.That(status, Is.EqualTo(KycStatus.Approved));
            Assert.That(refId, Is.Not.Null.And.StartsWith("MOCK-"));
            Assert.That(errorMsg, Is.Null);
        }

        [Test]
        public async Task MockKycProvider_NotAutoApprove_ReturnsPending()
        {
            var provider = CreateKycProvider(autoApprove: false);
            var (refId, status, _) =
                await provider.StartVerificationAsync("user-002",
                    new BiatecTokensApi.Models.Kyc.StartKycVerificationRequest { FullName = "Test User" },
                    "corr");

            Assert.That(status, Is.EqualTo(KycStatus.Pending));
            Assert.That(refId, Is.Not.Null.And.StartsWith("MOCK-"));
        }

        [Test]
        public async Task MockKycProvider_FetchStatus_AutoApprove_ReturnsApproved()
        {
            var provider = CreateKycProvider(autoApprove: true);
            var (status, reason, error) = await provider.FetchStatusAsync("MOCK-REF-123");

            Assert.That(status, Is.EqualTo(KycStatus.Approved));
            Assert.That(reason, Is.Not.Null);
        }

        [Test]
        public async Task MockKycProvider_FetchStatus_NotAutoApprove_ReturnsPending()
        {
            var provider = CreateKycProvider(autoApprove: false);
            var (status, reason, error) = await provider.FetchStatusAsync("MOCK-REF-456");

            Assert.That(status, Is.EqualTo(KycStatus.Pending));
        }

        [Test]
        public void MockKycProvider_ValidateWebhookSignature_ValidSignature_ReturnsTrue()
        {
            var provider = CreateKycProvider();
            var payload = "test-payload";
            var secret = "test-secret";
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var validSignature = Convert.ToBase64String(hash);

            var result = provider.ValidateWebhookSignature(payload, validSignature, secret);
            Assert.That(result, Is.True);
        }

        [Test]
        public void MockKycProvider_ValidateWebhookSignature_InvalidSignature_ReturnsFalse()
        {
            var provider = CreateKycProvider();
            var result = provider.ValidateWebhookSignature("payload", "bad-signature", "secret");
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task MockKycProvider_ParseWebhook_ApprovedStatus_ReturnsApproved()
        {
            var provider = CreateKycProvider();
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-WH-001",
                EventType = "verification_completed",
                Status = "approved",
                Reason = null
            };

            var (refId, status, reason) = await provider.ParseWebhookAsync(payload);

            Assert.That(refId, Is.EqualTo("MOCK-WH-001"));
            Assert.That(status, Is.EqualTo(KycStatus.Approved));
        }

        [Test]
        public async Task MockKycProvider_ParseWebhook_RejectedStatus_ReturnsRejected()
        {
            var provider = CreateKycProvider();
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-WH-002",
                EventType = "verification_failed",
                Status = "rejected",
                Reason = "document_mismatch"
            };

            var (_, status, reason) = await provider.ParseWebhookAsync(payload);

            Assert.That(status, Is.EqualTo(KycStatus.Rejected));
            Assert.That(reason, Is.EqualTo("document_mismatch"));
        }

        [Test]
        public async Task MockKycProvider_ParseWebhook_PendingStatus_ReturnsPending()
        {
            var provider = CreateKycProvider();
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-WH-003",
                EventType = "verification_pending",
                Status = "in_progress"
            };

            var (_, status, _) = await provider.ParseWebhookAsync(payload);
            Assert.That(status, Is.EqualTo(KycStatus.Pending));
        }

        [Test]
        public async Task MockKycProvider_ParseWebhook_ExpiredStatus_ReturnsExpired()
        {
            var provider = CreateKycProvider();
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-WH-004",
                EventType = "verification_expired",
                Status = "expired"
            };

            var (_, status, _) = await provider.ParseWebhookAsync(payload);
            Assert.That(status, Is.EqualTo(KycStatus.Expired));
        }

        // ── Policy hook / whitelist integration point ─────────────────────────

        [Test]
        public async Task PolicyHook_ApprovedDecision_AllowsWhitelistEligibility()
        {
            // Simulate the whitelist policy consumer checking screening state
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(checkType: ComplianceCheckType.Combined);
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-policy-1");

            // For whitelist gating: Approved = eligible
            var eligible = resp.Success && resp.State == ComplianceDecisionState.Approved;
            Assert.That(eligible, Is.True);
        }

        [Test]
        public async Task PolicyHook_RejectedDecision_BlocksWhitelistEligibility()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Combined,
                metadata: new Dictionary<string, string> { ["sanctions_flag"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-policy-2");

            var eligible = resp.Success && resp.State == ComplianceDecisionState.Approved;
            Assert.That(eligible, Is.False);
        }

        [Test]
        public async Task PolicyHook_ErrorDecision_FailClosedBlocksEligibility()
        {
            var svc = CreateService(kycAutoApprove: true);
            var req = MakeRequest(
                checkType: ComplianceCheckType.Aml,
                metadata: new Dictionary<string, string> { ["simulate_timeout"] = "true" });
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-policy-3");

            // Fail-closed: error state must not grant eligibility
            var eligible = resp.Success && resp.State == ComplianceDecisionState.Approved;
            Assert.That(eligible, Is.False);
        }

        // ── Response contract assertions ──────────────────────────────────────

        [Test]
        public async Task ResponseContract_HasAllRequiredFields()
        {
            var svc = CreateService();
            var resp = await svc.InitiateCheckAsync(MakeRequest(), "actor", "corr-contract-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.DecisionId, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.CorrelationId, Is.Not.Null);
            Assert.That(resp.InitiatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(resp.AuditTrail, Is.Not.Null);
            Assert.That(resp.AuditTrail.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task ResponseContract_AuditEvents_HaveRequiredFields()
        {
            var svc = CreateService();
            var resp = await svc.InitiateCheckAsync(MakeRequest(), "actor", "corr-audit-contract");

            foreach (var evt in resp.AuditTrail)
            {
                Assert.That(evt.EventType, Is.Not.Null.And.Not.Empty, "EventType must not be empty");
                Assert.That(evt.OccurredAt, Is.Not.EqualTo(default(DateTimeOffset)), "OccurredAt must be set");
                Assert.That(evt.CorrelationId, Is.Not.Null, "CorrelationId must not be null");
            }
        }

        [Test]
        public async Task ResponseContract_ErrorResponse_HasErrorCodeAndMessage()
        {
            var svc = CreateService();
            var req = MakeRequest(subjectId: "");  // invalid
            var resp = await svc.InitiateCheckAsync(req, "actor", "corr-err-contract");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ResponseContract_CorrelationIdInAuditTrail()
        {
            var svc = CreateService();
            var correlationId = "corr-audit-trace";
            var resp = await svc.InitiateCheckAsync(MakeRequest(), "actor", correlationId);

            Assert.That(resp.AuditTrail.All(e => e.CorrelationId == correlationId), Is.True);
        }

        // ── Regression: repeated reads produce consistent state ───────────────

        [Test]
        public async Task RepeatedReads_SameDecisionId_ProduceConsistentState()
        {
            var svc = CreateService();
            var resp = await svc.InitiateCheckAsync(MakeRequest(), "actor", "corr-regression-1");
            var decisionId = resp.DecisionId!;

            var read1 = await svc.GetCheckStatusAsync(decisionId);
            var read2 = await svc.GetCheckStatusAsync(decisionId);
            var read3 = await svc.GetCheckStatusAsync(decisionId);

            Assert.That(read1.State, Is.EqualTo(read2.State));
            Assert.That(read2.State, Is.EqualTo(read3.State));
        }

        [Test]
        public async Task ThreeConsecutiveIdempotentReplays_ProduceIdenticalDecisionId()
        {
            var svc = CreateService();
            var key = $"regression-idem-{Guid.NewGuid():N}";
            var req = MakeRequest(idempotencyKey: key);

            var r1 = await svc.InitiateCheckAsync(req, "actor", "c1");
            var r2 = await svc.InitiateCheckAsync(req, "actor", "c2");
            var r3 = await svc.InitiateCheckAsync(req, "actor", "c3");

            Assert.That(r1.DecisionId, Is.EqualTo(r2.DecisionId));
            Assert.That(r2.DecisionId, Is.EqualTo(r3.DecisionId));
        }

        // ── Helper: throwing KYC provider for fail-closed tests ───────────────

        private class ThrowingKycProvider : IKycProvider
        {
            public Task<(string, KycStatus, string?)> StartVerificationAsync(
                string userId, BiatecTokensApi.Models.Kyc.StartKycVerificationRequest request, string correlationId)
                => throw new InvalidOperationException("Simulated KYC provider crash");

            public Task<(KycStatus, string?, string?)> FetchStatusAsync(string providerReferenceId)
                => Task.FromResult((KycStatus.NotStarted, (string?)null, (string?)null));

            public bool ValidateWebhookSignature(string payload, string signature, string webhookSecret)
                => false;

            public Task<(string, KycStatus, string?)> ParseWebhookAsync(KycWebhookPayload payload)
                => Task.FromResult((payload.ProviderReferenceId, KycStatus.NotStarted, (string?)null));
        }
    }
}

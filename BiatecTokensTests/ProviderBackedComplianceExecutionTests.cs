using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Models.ProviderBackedCompliance;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for ProviderBackedComplianceExecutionService covering:
    ///   - ExecuteDecisionAsync: happy paths for all 5 decision kinds (Approve/Reject/RFI/SanctionsReview/Escalate)
    ///   - Fail-closed behaviour: RequireProviderBacked+Simulated, RequireKycAmlSignOff failures
    ///   - Invalid inputs: null request, empty caseId, missing reason for required kinds
    ///   - State machine integration: case must be in valid state for transitions
    ///   - GetExecutionStatusAsync: no history, existing history, release-grade detection
    ///   - BuildSignOffEvidenceAsync: empty history, simulated rejected, provider-backed accepted
    ///   - Evidence artifact integrity: content hash, IsReleaseGradeEvidence flag
    ///   - Webhook emission: ComplianceCaseExecutionCompleted, ComplianceCaseSanctionsReviewRequested
    ///   - Failure paths: case not found, underlying service throws
    ///   - Concurrent executions: thread safety of history store
    ///   - Determinism: same inputs produce stable evidence shape
    ///   - Schema contract: all required fields populated on success
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProviderBackedComplianceExecutionTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Fakes
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();

            public Task EmitEventAsync(WebhookEvent e)
            {
                lock (Events) Events.Add(e);
                return Task.CompletedTask;
            }

            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u)
                => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u)
                => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        /// <summary>
        /// Configurable fake for KYC/AML sign-off evidence service.
        /// </summary>
        private sealed class FakeKycAmlService : IKycAmlSignOffEvidenceService
        {
            public bool ReturnReadyRecords { get; set; } = true;
            public KycAmlSignOffCheckKind CheckKind { get; set; } = KycAmlSignOffCheckKind.Combined;
            public bool ThrowOnList { get; set; }

            public Task<ListKycAmlSignOffRecordsResponse> ListRecordsForSubjectAsync(string subjectId)
            {
                if (ThrowOnList)
                    throw new InvalidOperationException("Simulated provider failure");

                if (!ReturnReadyRecords)
                    return Task.FromResult(new ListKycAmlSignOffRecordsResponse { Records = new List<KycAmlSignOffRecord>() });

                var record = new KycAmlSignOffRecord
                {
                    RecordId = "rec-1",
                    SubjectId = subjectId,
                    ExecutionMode = KycAmlSignOffExecutionMode.ProtectedSandbox,
                    CheckKind = CheckKind
                };
                return Task.FromResult(new ListKycAmlSignOffRecordsResponse
                {
                    SubjectId = subjectId,
                    Records = new List<KycAmlSignOffRecord> { record }
                });
            }

            public Task<KycAmlSignOffReadinessResponse> GetReadinessAsync(string recordId) =>
                Task.FromResult(new KycAmlSignOffReadinessResponse
                {
                    RecordId = recordId,
                    IsApprovalReady = ReturnReadyRecords,
                    IsProviderBacked = ReturnReadyRecords,
                    ReadinessState = ReturnReadyRecords
                        ? KycAmlSignOffReadinessState.Ready
                        : KycAmlSignOffReadinessState.IncompleteEvidence
                });

            // Unused interface members
            public Task<InitiateKycAmlSignOffResponse> InitiateSignOffAsync(
                InitiateKycAmlSignOffRequest r, string a, string c) =>
                Task.FromResult(new InitiateKycAmlSignOffResponse { Success = true });

            public Task<ProcessKycAmlSignOffCallbackResponse> ProcessCallbackAsync(
                string id, ProcessKycAmlSignOffCallbackRequest r, string c) =>
                Task.FromResult(new ProcessKycAmlSignOffCallbackResponse { Success = true });

            public Task<GetKycAmlSignOffRecordResponse> GetRecordAsync(string id) =>
                Task.FromResult(new GetKycAmlSignOffRecordResponse { Success = true });

            public Task<GetKycAmlSignOffArtifactsResponse> GetArtifactsAsync(string id) =>
                Task.FromResult(new GetKycAmlSignOffArtifactsResponse());

            public Task<PollKycAmlSignOffStatusResponse> PollProviderStatusAsync(string id, string c) =>
                Task.FromResult(new PollKycAmlSignOffStatusResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static ComplianceCaseManagementService CreateCaseService(CapturingWebhook? wh = null) =>
            new(NullLogger<ComplianceCaseManagementService>.Instance, webhookService: wh);

        private static ProviderBackedComplianceExecutionService CreateService(
            ComplianceCaseManagementService? caseService = null,
            FakeKycAmlService? kycAml = null,
            CapturingWebhook? webhook = null) =>
            new(
                caseService ?? CreateCaseService(),
                NullLogger<ProviderBackedComplianceExecutionService>.Instance,
                kycAml,
                null,
                webhook);

        private static async Task<string> CreateAndAdvanceCaseToUnderReview(
            ComplianceCaseManagementService svc,
            string issuerId = "issuer-1",
            string subjectId = "subject-1")
        {
            var createResp = await svc.CreateCaseAsync(
                new CreateComplianceCaseRequest
                {
                    IssuerId = issuerId,
                    SubjectId = subjectId,
                    Type = CaseType.InvestorEligibility
                }, "actor-sys");

            Assert.That(createResp.Success, Is.True, createResp.ErrorMessage);
            var caseId = createResp.Case!.CaseId;

            var r1 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor-sys");
            Assert.That(r1.Success, Is.True, $"→EvidencePending: {r1.ErrorMessage}");

            var r2 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-sys");
            Assert.That(r2.Success, Is.True, $"→UnderReview: {r2.ErrorMessage}");

            return caseId;
        }

        private static async Task PollForWebhook(
            CapturingWebhook wh, WebhookEventType type, int count = 1, int maxMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
            while (DateTime.UtcNow < deadline)
            {
                int n;
                lock (wh.Events) n = wh.Events.Count(e => e.EventType == type);
                if (n >= count) return;
                await Task.Delay(20);
            }
            int final;
            lock (wh.Events) final = wh.Events.Count(e => e.EventType == type);
            Assert.Fail($"Expected {count}× {type} but got {final} after {maxMs}ms.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 1. ExecuteDecisionAsync — input validation (fail-fast)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_NullRequest_ReturnsFailed()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("case-1", null!, "actor");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("REQUEST_NULL"));
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.Failed));
        }

        [Test]
        public async Task ExecuteDecision_EmptyCaseId_ReturnsFailed()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("", new ExecuteProviderBackedDecisionRequest(), "actor");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_CASE_ID"));
        }

        [Test]
        public async Task ExecuteDecision_WhitespaceCaseId_ReturnsFailed()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("   ", new ExecuteProviderBackedDecisionRequest(), "actor");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_CASE_ID"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. Fail-closed: RequireProviderBacked + Simulated mode
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_RequireProviderBacked_SimulatedMode_ReturnsConfigurationMissing()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("case-1",
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireProviderBacked = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.ConfigurationMissing));
            Assert.That(r.ErrorCode, Is.EqualTo("PROVIDER_BACKED_REQUIRED"));
            Assert.That(r.Diagnostics, Is.Not.Null);
            Assert.That(r.Diagnostics!.IsConfigurationPresent, Is.False);
        }

        [Test]
        public async Task ExecuteDecision_RequireProviderBacked_LiveProviderMode_DoesNotFailOnModeCheck()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            // LiveProvider mode with RequireProviderBacked=true should not fail on mode check
            // (may fail on actual case transition for other reasons)
            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireProviderBacked = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.LiveProvider,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            // Should NOT fail with PROVIDER_BACKED_REQUIRED
            Assert.That(r.ErrorCode, Is.Not.EqualTo("PROVIDER_BACKED_REQUIRED"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 3. Fail-closed: RequireKycAmlSignOff — no records → InsufficientEvidence
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_RequireKycAmlSignOff_NoRecords_ReturnsInsufficientEvidence()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);

            var kycAml = new FakeKycAmlService { ReturnReadyRecords = false };
            var svc = CreateService(caseService, kycAml);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireKycAmlSignOff = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.ProtectedSandbox,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.InsufficientEvidence));
            Assert.That(r.ErrorCode, Is.EqualTo("KYC_AML_SIGNOFF_REQUIRED"));
            Assert.That(r.Diagnostics, Is.Not.Null);
            Assert.That(r.Diagnostics!.IsKycSignOffComplete, Is.False);
            Assert.That(r.Diagnostics.IsAmlSignOffComplete, Is.False);
        }

        [Test]
        public async Task ExecuteDecision_RequireKycAmlSignOff_KycOnlyRecord_AmlFails()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);

            var kycAml = new FakeKycAmlService
            {
                ReturnReadyRecords = true,
                CheckKind = KycAmlSignOffCheckKind.IdentityKyc  // Only KYC, no AML
            };
            var svc = CreateService(caseService, kycAml);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireKycAmlSignOff = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.ProtectedSandbox,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.InsufficientEvidence));
            Assert.That(r.Diagnostics!.IsKycSignOffComplete, Is.True);
            Assert.That(r.Diagnostics.IsAmlSignOffComplete, Is.False);
        }

        [Test]
        public async Task ExecuteDecision_RequireKycAmlSignOff_SimulatedMode_SkipsCheck()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);

            // No KYC/AML service provided, but Simulated mode — check should be skipped
            var svc = CreateService(caseService, kycAml: null);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireKycAmlSignOff = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            // Should not fail with InsufficientEvidence in Simulated mode
            Assert.That(r.Status, Is.Not.EqualTo(ProviderBackedCaseExecutionStatus.InsufficientEvidence));
        }

        [Test]
        public async Task ExecuteDecision_RequireKycAmlSignOff_ProviderThrows_ReturnsInsufficientEvidence()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);

            var kycAml = new FakeKycAmlService { ThrowOnList = true };
            var svc = CreateService(caseService, kycAml);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireKycAmlSignOff = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.ProtectedSandbox,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.InsufficientEvidence));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4. Reason validation
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(ProviderBackedCaseDecisionKind.Reject)]
        [TestCase(ProviderBackedCaseDecisionKind.ReturnForInformation)]
        [TestCase(ProviderBackedCaseDecisionKind.SanctionsReview)]
        public async Task ExecuteDecision_MissingReason_ForRequiredKind_ReturnsFailed(
            ProviderBackedCaseDecisionKind kind)
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = kind,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = null  // Missing reason
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("REASON_REQUIRED"));
        }

        [Test]
        public async Task ExecuteDecision_Approve_WithoutReason_DoesNotRequireReason()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = null
                }, "actor");

            // Should not fail with REASON_REQUIRED
            Assert.That(r.ErrorCode, Is.Not.EqualTo("REASON_REQUIRED"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 5. Happy paths — all 5 decision kinds
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_Approve_FromUnderReview_Succeeds()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Notes = "All clear"
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.Completed));
            Assert.That(r.Evidence, Is.Not.Null);
            Assert.That(r.Evidence!.DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.Approve));
            Assert.That(r.Evidence.CaseId, Is.EqualTo(caseId));
            Assert.That(r.Evidence.ExecutionId, Is.Not.Empty);
        }

        [Test]
        public async Task ExecuteDecision_Reject_FromUnderReview_Succeeds()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Reject,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = "Identity mismatch"
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.Completed));
            Assert.That(r.Evidence!.DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.Reject));
            Assert.That(r.Evidence.DecisionReason, Is.EqualTo("Identity mismatch"));
        }

        [Test]
        public async Task ExecuteDecision_ReturnForInformation_FromUnderReview_Succeeds()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.ReturnForInformation,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = "Need additional ID document"
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Evidence!.DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.ReturnForInformation));
        }

        [Test]
        public async Task ExecuteDecision_SanctionsReview_FromUnderReview_Succeeds()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.SanctionsReview,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = "Name matched watchlist"
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Evidence!.DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.SanctionsReview));
        }

        [Test]
        public async Task ExecuteDecision_Escalate_FromUnderReview_Succeeds()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Escalate,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = "Complex PEP scenario"
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Evidence!.DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.Escalate));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 6. Evidence artifact assertions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_Approve_EvidenceArtifact_IsPopulatedCorrectly()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService, subjectId: "subject-99");
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    CorrelationId = "corr-abc"
                }, "actor-42");

            Assert.That(r.Success, Is.True);
            var ev = r.Evidence!;

            Assert.That(ev.ExecutionId, Is.Not.Empty, "ExecutionId must be populated");
            Assert.That(ev.CaseId, Is.EqualTo(caseId), "CaseId must match");
            Assert.That(ev.DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.Approve));
            Assert.That(ev.ExecutionMode, Is.EqualTo(ProviderBackedCaseExecutionMode.Simulated));
            Assert.That(ev.IsProviderBacked, Is.False, "Simulated mode → IsProviderBacked=false");
            Assert.That(ev.ActorId, Is.EqualTo("actor-42"), "ActorId must be set");
            Assert.That(ev.CorrelationId, Is.EqualTo("corr-abc"), "CorrelationId echoed");
            Assert.That(ev.AuditSteps, Is.Not.Empty, "Audit steps must be populated");
            Assert.That(ev.PreviousState, Is.Not.Null, "PreviousState must be recorded");
            Assert.That(ev.TargetState, Is.Not.Empty, "TargetState must be recorded");
        }

        [Test]
        public async Task ExecuteDecision_ProtectedSandbox_IsProviderBacked_IsTrue()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.ProtectedSandbox
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Evidence!.IsProviderBacked, Is.True, "ProtectedSandbox → IsProviderBacked=true");
        }

        [Test]
        public async Task ExecuteDecision_LiveProvider_WithKycAmlService_IsReleaseGrade_IsTrue()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var kycAml = new FakeKycAmlService();
            var svc = CreateService(caseService, kycAml);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.LiveProvider
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Evidence!.IsReleaseGradeEvidence, Is.True);
        }

        [Test]
        public async Task ExecuteDecision_Simulated_IsReleaseGrade_IsFalse()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Evidence!.IsReleaseGradeEvidence, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 7. Case not found / service failures
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_NonExistentCase_ReturnsFailed_CaseNotFound()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("nonexistent-case-id",
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        [Test]
        public async Task ExecuteDecision_InvalidStateTransition_ReturnsInvalidState()
        {
            var caseService = CreateCaseService();

            // Create case in Intake state (no advance) — Approve from Intake is invalid
            var createResp = await caseService.CreateCaseAsync(
                new CreateComplianceCaseRequest
                {
                    IssuerId = "i1",
                    SubjectId = "s1",
                    Type = CaseType.InvestorEligibility
                }, "actor");
            var caseId = createResp.Case!.CaseId;

            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.InvalidState));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 8. Correlation ID propagation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_CorrelationId_IsEchoedInResponse()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    CorrelationId = "my-trace-id-123"
                }, "actor");

            Assert.That(r.CorrelationId, Is.EqualTo("my-trace-id-123"));
        }

        [Test]
        public async Task ExecuteDecision_NoCorrelationId_GeneratesOne()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    CorrelationId = null
                }, "actor");

            Assert.That(r.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 9. GetExecutionStatusAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetStatus_EmptyCaseId_ReturnsFailed()
        {
            var svc = CreateService();
            var r = await svc.GetExecutionStatusAsync("", "actor");
            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task GetStatus_NonExistentCase_ReturnsFailed()
        {
            var svc = CreateService();
            var r = await svc.GetExecutionStatusAsync("does-not-exist", "actor");
            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task GetStatus_NoExecutions_ReturnsNotStarted()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.GetExecutionStatusAsync(caseId, "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.NotStarted));
            Assert.That(r.ExecutionHistory, Is.Empty);
            Assert.That(r.HasReleaseGradeEvidence, Is.False);
        }

        [Test]
        public async Task GetStatus_AfterApproval_ReturnsCompleted_WithHistory()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            var r = await svc.GetExecutionStatusAsync(caseId, "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Status, Is.EqualTo(ProviderBackedCaseExecutionStatus.Completed));
            Assert.That(r.ExecutionHistory, Has.Count.EqualTo(1));
            Assert.That(r.HasReleaseGradeEvidence, Is.False, "Simulated → no release-grade evidence");
        }

        [Test]
        public async Task GetStatus_AfterProviderBackedExecution_HasReleaseGradeEvidence_IsTrue()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var kycAml = new FakeKycAmlService();
            var svc = CreateService(caseService, kycAml);

            await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.LiveProvider
                }, "actor");

            var r = await svc.GetExecutionStatusAsync(caseId, "actor");

            Assert.That(r.HasReleaseGradeEvidence, Is.True);
        }

        [Test]
        public async Task GetStatus_CorrelationId_IsEchoed()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.GetExecutionStatusAsync(caseId, "actor", "trace-99");
            Assert.That(r.CorrelationId, Is.EqualTo("trace-99"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 10. BuildSignOffEvidenceAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task BuildSignOffEvidence_EmptyCaseId_ReturnsFailed()
        {
            var svc = CreateService();
            var r = await svc.BuildSignOffEvidenceAsync("",
                new BuildProviderBackedSignOffEvidenceRequest(), "actor");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_CASE_ID"));
        }

        [Test]
        public async Task BuildSignOffEvidence_NonExistentCase_ReturnsCaseNotFound()
        {
            var svc = CreateService();
            var r = await svc.BuildSignOffEvidenceAsync("ghost-case",
                new BuildProviderBackedSignOffEvidenceRequest(), "actor");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        [Test]
        public async Task BuildSignOffEvidence_NoHistory_ReturnsSuccessWithEmptyBundle()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest(), "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Bundle, Is.Not.Null);
            Assert.That(r.Bundle!.ExecutionHistory, Is.Empty);
            Assert.That(r.Bundle.IsReleaseGrade, Is.False);
        }

        [Test]
        public async Task BuildSignOffEvidence_RequireProviderBacked_WithSimulatedHistory_ReturnsFailure()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            // Execute a simulated decision to populate history
            await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            // Build with RequireProviderBackedEvidence — should fail because we have simulated evidence
            var r = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest
                {
                    RequireProviderBackedEvidence = true
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("SIMULATED_EVIDENCE_NOT_ALLOWED"));
        }

        [Test]
        public async Task BuildSignOffEvidence_RequireProviderBacked_NoHistory_ReturnsNoExecutionHistory()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest
                {
                    RequireProviderBackedEvidence = true
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("NO_EXECUTION_HISTORY"));
        }

        [Test]
        public async Task BuildSignOffEvidence_ProviderBackedHistory_ReturnsReleaseGradeBundle()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var kycAml = new FakeKycAmlService();
            var svc = CreateService(caseService, kycAml);

            // Execute a provider-backed decision
            var exec = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.LiveProvider
                }, "actor");
            Assert.That(exec.Success, Is.True, exec.ErrorMessage);

            var r = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest
                {
                    RequireProviderBackedEvidence = true,
                    ReleaseTag = "v1.2026.03.18"
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Bundle, Is.Not.Null);
            Assert.That(r.Bundle!.IsReleaseGrade, Is.True);
            Assert.That(r.Bundle.IsProviderBackedEvidence, Is.True);
            Assert.That(r.Bundle.ReleaseTag, Is.EqualTo("v1.2026.03.18"));
            Assert.That(r.Bundle.ContentHash, Is.Not.Empty);
            Assert.That(r.Bundle.ExecutionHistory, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task BuildSignOffEvidence_Bundle_ContainsCorrectCaseMetadata()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService, "issuer-X", "subject-X");
            var svc = CreateService(caseService);

            var r = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest(), "actor");

            Assert.That(r.Success, Is.True);
            var bundle = r.Bundle!;
            Assert.That(bundle.CaseId, Is.EqualTo(caseId));
            Assert.That(bundle.IssuerId, Is.EqualTo("issuer-X"));
            Assert.That(bundle.SubjectId, Is.EqualTo("subject-X"));
            Assert.That(bundle.BundleId, Is.Not.Empty);
            Assert.That(bundle.ContentHash, Is.Not.Empty);
        }

        [Test]
        public async Task BuildSignOffEvidence_ContentHash_IsDeterministicForSameHistory()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    CorrelationId = "same-corr"
                }, "actor");

            var r1 = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest(), "actor");
            var r2 = await svc.BuildSignOffEvidenceAsync(caseId,
                new BuildProviderBackedSignOffEvidenceRequest(), "actor");

            // Content hash should be same for same history
            Assert.That(r1.Bundle!.ContentHash, Is.EqualTo(r2.Bundle!.ContentHash));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 11. Webhook emission
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_Approve_EmitsComplianceCaseExecutionCompleted_Webhook()
        {
            var wh = new CapturingWebhook();
            var caseService = CreateCaseService(wh);
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService, webhook: wh);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.True);
            await PollForWebhook(wh, WebhookEventType.ComplianceCaseExecutionCompleted);
        }

        [Test]
        public async Task ExecuteDecision_SanctionsReview_EmitsBothWebhooks()
        {
            var wh = new CapturingWebhook();
            var caseService = CreateCaseService(wh);
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService, webhook: wh);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.SanctionsReview,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = "Watchlist hit"
                }, "actor");

            Assert.That(r.Success, Is.True);
            await PollForWebhook(wh, WebhookEventType.ComplianceCaseExecutionCompleted);
            await PollForWebhook(wh, WebhookEventType.ComplianceCaseSanctionsReviewRequested);
        }

        [Test]
        public async Task ExecuteDecision_NoWebhookService_DoesNotThrow()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService, webhook: null);  // No webhook service

            // Should complete without errors
            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 12. Multiple executions / history accumulation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_MultipleDecisions_HistoryAccumulates()
        {
            var caseService = CreateCaseService();
            // Create case and advance to UnderReview
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            // Execute a ReturnForInformation (case → EvidencePending)
            var rfi = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.ReturnForInformation,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    Reason = "Need more docs"
                }, "actor");
            Assert.That(rfi.Success, Is.True, rfi.ErrorMessage);

            // Advance back to UnderReview for second decision
            var r2 = await caseService.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-sys");
            Assert.That(r2.Success, Is.True, r2.ErrorMessage);

            // Execute Approve
            var approve = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");
            Assert.That(approve.Success, Is.True, approve.ErrorMessage);

            var status = await svc.GetExecutionStatusAsync(caseId, "actor");
            Assert.That(status.ExecutionHistory, Has.Count.EqualTo(2));
            Assert.That(status.ExecutionHistory[0].DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.ReturnForInformation));
            Assert.That(status.ExecutionHistory[1].DecisionKind, Is.EqualTo(ProviderBackedCaseDecisionKind.Approve));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 13. KYC/AML sign-off with combined record — both KYC and AML pass
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_RequireKycAmlSignOff_CombinedRecord_BothFlagsSet()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);

            var kycAml = new FakeKycAmlService
            {
                ReturnReadyRecords = true,
                CheckKind = KycAmlSignOffCheckKind.Combined
            };
            var svc = CreateService(caseService, kycAml);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireKycAmlSignOff = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.ProtectedSandbox,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Diagnostics!.IsKycSignOffComplete, Is.True);
            Assert.That(r.Diagnostics.IsAmlSignOffComplete, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 14. Diagnostics always populated
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_OnSuccess_DiagnosticsArePopulated()
        {
            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            var r = await svc.ExecuteDecisionAsync(caseId,
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Diagnostics, Is.Not.Null, "Diagnostics must be populated even on success");
            Assert.That(r.Diagnostics!.ExecutionMode, Is.EqualTo(ProviderBackedCaseExecutionMode.Simulated));
        }

        [Test]
        public async Task ExecuteDecision_OnFailure_DiagnosticsArePopulated()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("ghost",
                new ExecuteProviderBackedDecisionRequest
                {
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated
                }, "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Diagnostics, Is.Not.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 15. NextAction on failure
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_RequireProviderBacked_SimulatedFail_HasNextAction()
        {
            var svc = CreateService();
            var r = await svc.ExecuteDecisionAsync("case-1",
                new ExecuteProviderBackedDecisionRequest
                {
                    RequireProviderBacked = true,
                    ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                    DecisionKind = ProviderBackedCaseDecisionKind.Approve
                }, "actor");

            Assert.That(r.NextAction, Is.Not.Null.And.Not.Empty,
                "Fail-closed responses must include actionable guidance");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 16. Concurrent executions — thread safety
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExecuteDecision_ConcurrentExecutions_SameCase_AllEvidenceRetained()
        {
            // This test proves that concurrent decisions targeting the SAME case do not
            // cause lost evidence due to the previously-buggy AddOrUpdate+List pattern.
            // We use Escalate (which is valid from UnderReview) and allow some calls to
            // succeed and some to fail (the case may already be Escalated for subsequent
            // ones). The key invariant is that every *successful* execution must appear
            // in the history — no evidence artifact is silently dropped.

            var caseService = CreateCaseService();
            var caseId = await CreateAndAdvanceCaseToUnderReview(caseService);
            var svc = CreateService(caseService);

            const int concurrency = 8;

            // Fire all concurrent calls against the single case
            var tasks = Enumerable.Range(0, concurrency)
                .Select(i => svc.ExecuteDecisionAsync(caseId,
                    new ExecuteProviderBackedDecisionRequest
                    {
                        DecisionKind = ProviderBackedCaseDecisionKind.Escalate,
                        ExecutionMode = ProviderBackedCaseExecutionMode.Simulated,
                        Reason = $"Concurrent escalation {i}",
                        CorrelationId = $"corr-{i}"
                    }, $"actor-{i}"))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Count how many succeeded vs failed (state machine allows only some transitions)
            var successCount = results.Count(r => r.Success);
            var failCount = results.Count(r => !r.Success);

            // At least one must have succeeded
            Assert.That(successCount, Is.GreaterThanOrEqualTo(1),
                "At least one concurrent escalation must succeed");

            // CRITICAL: history must contain exactly successCount entries — no lost evidence
            var status = await svc.GetExecutionStatusAsync(caseId, "actor");
            Assert.That(status.ExecutionHistory, Has.Count.EqualTo(successCount),
                $"History must contain exactly {successCount} evidence artifact(s) — " +
                $"one per successful execution. Had {failCount} failures. " +
                $"Any discrepancy means evidence was silently dropped.");

            // Every successful execution ID must be traceable in history
            var historyExecutionIds = status.ExecutionHistory
                .Select(e => e.ExecutionId)
                .ToHashSet();
            foreach (var result in results.Where(r => r.Success))
            {
                Assert.That(historyExecutionIds, Does.Contain(result.ExecutionId),
                    $"ExecutionId {result.ExecutionId} from a successful execution is missing from history");
            }
        }
    }
}

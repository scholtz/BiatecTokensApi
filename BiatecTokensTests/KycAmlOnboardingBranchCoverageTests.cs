using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Branch-coverage tests for KycAmlOnboardingCaseService (BC01–BC52).
    ///
    /// Targets every reachable code path in the service:
    ///   BC01–BC08  : RecordReviewerAction — Approve valid/invalid from every meaningful state
    ///   BC09–BC12  : RecordReviewerAction — Escalate valid/invalid
    ///   BC13–BC15  : RecordReviewerAction — RequestAdditionalInfo valid/invalid
    ///   BC16–BC20  : RecordReviewerAction — AddNote on every state
    ///   BC21–BC23  : RecordReviewerAction — Reject scenarios
    ///   BC24–BC26  : RecordReviewerAction / GetCase / InitiateProviderChecks — non-existent case
    ///   BC27–BC29  : InitiateProviderChecks — all three ExecutionMode values
    ///   BC30–BC31  : InitiateProviderChecks — exception and degraded-journey paths
    ///   BC32–BC35  : CreateCase — all four SubjectKind enum values
    ///   BC36–BC41  : ListCases — filter by subject, state, combined, page-size clamping, pagination
    ///   BC42–BC46  : GetEvidenceSummary — state-based evidence derivation
    ///   BC47–BC48  : Idempotency — same key returns same case, no key creates distinct cases
    ///   BC49–BC50  : Schema contract — required fields always populated
    ///   BC51–BC52  : Edge cases — null CorrelationId generates one, webhook null-safe
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlOnboardingBranchCoverageTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Configurable fake for ILiveProviderVerificationJourneyService
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class ConfigurableJourney : ILiveProviderVerificationJourneyService
        {
            public bool ThrowOnStart { get; set; }
            public VerificationJourneyStage StartStage { get; set; } = VerificationJourneyStage.KycInitiated;
            public VerificationJourneyStage StatusStage { get; set; } = VerificationJourneyStage.KycInitiated;
            public string JourneyId { get; set; } = "bc-journey-001";
            public VerificationJourneyExecutionMode? CapturedMode { get; private set; }

            public Task<StartVerificationJourneyResponse> StartJourneyAsync(
                StartVerificationJourneyRequest request, string actorId)
            {
                CapturedMode = request.RequestedExecutionMode;
                if (ThrowOnStart) throw new InvalidOperationException("BC-simulated-exception");
                return Task.FromResult(new StartVerificationJourneyResponse
                {
                    Success = true,
                    Journey = new VerificationJourneyRecord
                    {
                        JourneyId = JourneyId,
                        SubjectId = request.SubjectId,
                        CurrentStage = StartStage
                    }
                });
            }

            public Task<GetVerificationJourneyStatusResponse> GetJourneyStatusAsync(
                string journeyId, string? correlationId = null)
                => Task.FromResult(new GetVerificationJourneyStatusResponse
                {
                    Success = true,
                    Journey = new VerificationJourneyRecord
                    {
                        JourneyId = journeyId,
                        CurrentStage = StatusStage
                    }
                });

            public Task<EvaluateApprovalDecisionResponse> EvaluateApprovalDecisionAsync(
                string journeyId, string? correlationId = null)
                => Task.FromResult(new EvaluateApprovalDecisionResponse { Success = true });

            public Task<GenerateVerificationJourneyEvidenceResponse> GenerateReleaseEvidenceAsync(
                string journeyId, GenerateVerificationJourneyEvidenceRequest request, string actorId)
                => Task.FromResult(new GenerateVerificationJourneyEvidenceResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helper factories
        // ═══════════════════════════════════════════════════════════════════════

        private static KycAmlOnboardingCaseService Build(
            ConfigurableJourney? journey = null,
            IWebhookService? webhookService = null)
            => new(NullLogger<KycAmlOnboardingCaseService>.Instance, journey, webhookService);

        private static async Task<string> CreateCaseId(
            KycAmlOnboardingCaseService svc,
            string subjectId = "subject-bc",
            KycAmlOnboardingSubjectKind kind = KycAmlOnboardingSubjectKind.Individual)
        {
            var response = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = subjectId, SubjectKind = kind },
                "actor-bc");
            return response.Case!.CaseId;
        }

        private static async Task RejectCase(KycAmlOnboardingCaseService svc, string caseId)
        {
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject },
                "actor-setup");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC01–BC08 : RecordReviewerAction — Approve
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC01: Approve from Initiated (not ReviewableState) returns INVALID_STATE_TRANSITION.</summary>
        [Test]
        public async Task BC01_Approve_FromInitiated_ReturnsInvalidStateTransition()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "actor-bc01");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC02: Approve from ProviderChecksStarted (not ReviewableState) fails.</summary>
        [Test]
        public async Task BC02_Approve_FromProviderChecksStarted_ReturnsInvalidStateTransition()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc02");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "actor-bc02");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC03: Approve from ConfigurationMissing state fails.</summary>
        [Test]
        public async Task BC03_Approve_FromConfigurationMissing_ReturnsInvalidStateTransition()
        {
            var svc = Build(); // no journey → ConfigurationMissing after InitiateProviderChecks
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc03");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "actor-bc03");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC04: Approve from ProviderUnavailable state fails.</summary>
        [Test]
        public async Task BC04_Approve_FromProviderUnavailable_ReturnsInvalidStateTransition()
        {
            var journey = new ConfigurableJourney { ThrowOnStart = true };
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc04");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "actor-bc04");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC05: Approve on Rejected terminal state is blocked by terminal state guard.</summary>
        [Test]
        public async Task BC05_Approve_OnRejectedTerminalState_BlockedByTerminalGuard()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);
            await RejectCase(svc, caseId);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "actor-bc05");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC06: Reject from Initiated state succeeds (Reject works from any non-terminal state).</summary>
        [Test]
        public async Task BC06_Reject_FromInitiated_Succeeds()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "BC06" },
                "actor-bc06");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        /// <summary>BC07: Reject from ProviderChecksStarted state succeeds.</summary>
        [Test]
        public async Task BC07_Reject_FromProviderChecksStarted_Succeeds()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc07");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "BC07" },
                "actor-bc07");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        /// <summary>BC08: Reject on already-Rejected state is blocked by terminal guard.</summary>
        [Test]
        public async Task BC08_Reject_OnAlreadyRejected_BlockedByTerminalGuard()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);
            await RejectCase(svc, caseId);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject },
                "actor-bc08");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC09–BC12 : RecordReviewerAction — Escalate
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC09: Escalate from Initiated (not ReviewableState) returns INVALID_STATE_TRANSITION.</summary>
        [Test]
        public async Task BC09_Escalate_FromInitiated_ReturnsInvalidStateTransition()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate },
                "actor-bc09");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC10: Escalate from ProviderChecksStarted (not ReviewableState) returns INVALID_STATE_TRANSITION.</summary>
        [Test]
        public async Task BC10_Escalate_FromProviderChecksStarted_ReturnsInvalidStateTransition()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc10");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate },
                "actor-bc10");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC11: Escalate on Rejected terminal state is blocked by terminal guard.</summary>
        [Test]
        public async Task BC11_Escalate_OnRejectedTerminalState_BlockedByTerminalGuard()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);
            await RejectCase(svc, caseId);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate },
                "actor-bc11");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC12: Escalate from ConfigurationMissing returns INVALID_STATE_TRANSITION.</summary>
        [Test]
        public async Task BC12_Escalate_FromConfigurationMissing_ReturnsInvalidStateTransition()
        {
            var svc = Build(); // no journey → ConfigurationMissing
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc12");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate },
                "actor-bc12");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC13–BC15 : RecordReviewerAction — RequestAdditionalInfo
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC13: RequestAdditionalInfo from Initiated (not ReviewableState) returns INVALID_STATE_TRANSITION.</summary>
        [Test]
        public async Task BC13_RequestAdditionalInfo_FromInitiated_ReturnsInvalidStateTransition()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.RequestAdditionalInfo },
                "actor-bc13");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC14: RequestAdditionalInfo from ProviderChecksStarted fails.</summary>
        [Test]
        public async Task BC14_RequestAdditionalInfo_FromProviderChecksStarted_Fails()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc14");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.RequestAdditionalInfo },
                "actor-bc14");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        /// <summary>BC15: RequestAdditionalInfo on Rejected terminal state is blocked.</summary>
        [Test]
        public async Task BC15_RequestAdditionalInfo_OnRejectedTerminal_BlockedByTerminalGuard()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);
            await RejectCase(svc, caseId);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.RequestAdditionalInfo },
                "actor-bc15");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC16–BC20 : RecordReviewerAction — AddNote on every reachable state
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC16: AddNote on Initiated state succeeds and preserves state.</summary>
        [Test]
        public async Task BC16_AddNote_OnInitiated_SucceedsAndPreservesState()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "BC16" },
                "actor-bc16");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated));
            Assert.That(resp.Action!.Kind, Is.EqualTo(KycAmlOnboardingActionKind.AddNote));
        }

        /// <summary>BC17: AddNote on ProviderChecksStarted state succeeds.</summary>
        [Test]
        public async Task BC17_AddNote_OnProviderChecksStarted_Succeeds()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc17");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "BC17" },
                "actor-bc17");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
        }

        /// <summary>BC18: AddNote on Rejected terminal state succeeds (AddNote bypasses terminal guard).</summary>
        [Test]
        public async Task BC18_AddNote_OnRejectedTerminalState_Succeeds()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);
            await RejectCase(svc, caseId);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "post-rejection note" },
                "actor-bc18");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
            Assert.That(resp.Action!.Notes, Is.EqualTo("post-rejection note"));
        }

        /// <summary>BC19: AddNote on ConfigurationMissing state succeeds.</summary>
        [Test]
        public async Task BC19_AddNote_OnConfigurationMissing_Succeeds()
        {
            var svc = Build(); // no journey → ConfigurationMissing
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc19");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "BC19" },
                "actor-bc19");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ConfigurationMissing));
        }

        /// <summary>BC20: AddNote on ProviderUnavailable state succeeds.</summary>
        [Test]
        public async Task BC20_AddNote_OnProviderUnavailable_Succeeds()
        {
            var journey = new ConfigurableJourney { ThrowOnStart = true };
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc20");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "BC20" },
                "actor-bc20");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC21–BC23 : Reject scenarios
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC21: Reject from ConfigurationMissing succeeds.</summary>
        [Test]
        public async Task BC21_Reject_FromConfigurationMissing_Succeeds()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc21");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "BC21" },
                "actor-bc21");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        /// <summary>BC22: Reject from ProviderUnavailable succeeds.</summary>
        [Test]
        public async Task BC22_Reject_FromProviderUnavailable_Succeeds()
        {
            var journey = new ConfigurableJourney { ThrowOnStart = true };
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc22");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "BC22" },
                "actor-bc22");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        /// <summary>BC23: Reject from ProviderChecksStarted succeeds.</summary>
        [Test]
        public async Task BC23_Reject_FromProviderChecksStarted_Succeeds()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc23");

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "BC23" },
                "actor-bc23");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC24–BC26 : Non-existent case paths
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC24: RecordReviewerAction on non-existent caseId returns CASE_NOT_FOUND.</summary>
        [Test]
        public async Task BC24_RecordReviewerAction_NonExistentCase_ReturnsCaseNotFound()
        {
            var svc = Build();

            var resp = await svc.RecordReviewerActionAsync("ghost-case-id",
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "BC24" },
                "actor-bc24");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        /// <summary>BC25: GetCase on non-existent caseId returns CASE_NOT_FOUND.</summary>
        [Test]
        public async Task BC25_GetCase_NonExistentCase_ReturnsCaseNotFound()
        {
            var svc = Build();
            var resp = await svc.GetCaseAsync("ghost-case-id");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        /// <summary>BC26: InitiateProviderChecks on non-existent caseId returns CASE_NOT_FOUND.</summary>
        [Test]
        public async Task BC26_InitiateProviderChecks_NonExistentCase_ReturnsCaseNotFound()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);

            var resp = await svc.InitiateProviderChecksAsync("ghost-case-id",
                new InitiateProviderChecksRequest(), "actor-bc26");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC27–BC29 : InitiateProviderChecks — ExecutionMode enum values
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC27: ExecutionMode.LiveProvider is mapped to VerificationJourneyExecutionMode.LiveProvider.</summary>
        [Test]
        public async Task BC27_InitiateProviderChecks_LiveProviderMode_MappedCorrectly()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);

            await svc.InitiateProviderChecksAsync(caseId,
                new InitiateProviderChecksRequest { ExecutionMode = KycAmlOnboardingExecutionMode.LiveProvider },
                "actor-bc27");

            Assert.That(journey.CapturedMode, Is.EqualTo(VerificationJourneyExecutionMode.LiveProvider));
        }

        /// <summary>BC28: ExecutionMode.ProtectedSandbox is mapped to VerificationJourneyExecutionMode.ProtectedSandbox.</summary>
        [Test]
        public async Task BC28_InitiateProviderChecks_ProtectedSandboxMode_MappedCorrectly()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);

            await svc.InitiateProviderChecksAsync(caseId,
                new InitiateProviderChecksRequest { ExecutionMode = KycAmlOnboardingExecutionMode.ProtectedSandbox },
                "actor-bc28");

            Assert.That(journey.CapturedMode, Is.EqualTo(VerificationJourneyExecutionMode.ProtectedSandbox));
        }

        /// <summary>BC29: ExecutionMode.Simulated is mapped to VerificationJourneyExecutionMode.Simulated.</summary>
        [Test]
        public async Task BC29_InitiateProviderChecks_SimulatedMode_MappedCorrectly()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);

            await svc.InitiateProviderChecksAsync(caseId,
                new InitiateProviderChecksRequest { ExecutionMode = KycAmlOnboardingExecutionMode.Simulated },
                "actor-bc29");

            Assert.That(journey.CapturedMode, Is.EqualTo(VerificationJourneyExecutionMode.Simulated));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC30–BC31 : InitiateProviderChecks — exception and degraded paths
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC30: Journey service throws → case state becomes ProviderUnavailable and PROVIDER_ERROR returned.</summary>
        [Test]
        public async Task BC30_InitiateProviderChecks_JourneyThrows_ReturnsProviderError()
        {
            var journey = new ConfigurableJourney { ThrowOnStart = true };
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);

            var resp = await svc.InitiateProviderChecksAsync(caseId,
                new InitiateProviderChecksRequest(), "actor-bc30");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_ERROR"));
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
        }

        /// <summary>BC31: Journey returns Degraded stage → case becomes ProviderUnavailable and PROVIDER_DEGRADED returned.</summary>
        [Test]
        public async Task BC31_InitiateProviderChecks_JourneyDegraded_ReturnsProviderDegraded()
        {
            var journey = new ConfigurableJourney { StartStage = VerificationJourneyStage.Degraded };
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);

            var resp = await svc.InitiateProviderChecksAsync(caseId,
                new InitiateProviderChecksRequest(), "actor-bc31");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_DEGRADED"));
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC32–BC35 : CreateCase — all SubjectKind enum values
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC32: CreateCase with SubjectKind.Individual stores correctly.</summary>
        [Test]
        public async Task BC32_CreateCase_SubjectKindIndividual_Stored()
        {
            var svc = Build();
            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "bc32-sub", SubjectKind = KycAmlOnboardingSubjectKind.Individual },
                "actor-bc32");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.SubjectKind, Is.EqualTo(KycAmlOnboardingSubjectKind.Individual));
        }

        /// <summary>BC33: CreateCase with SubjectKind.Business stores correctly.</summary>
        [Test]
        public async Task BC33_CreateCase_SubjectKindBusiness_Stored()
        {
            var svc = Build();
            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "bc33-sub", SubjectKind = KycAmlOnboardingSubjectKind.Business },
                "actor-bc33");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.SubjectKind, Is.EqualTo(KycAmlOnboardingSubjectKind.Business));
        }

        /// <summary>BC34: CreateCase with SubjectKind.Trust stores correctly.</summary>
        [Test]
        public async Task BC34_CreateCase_SubjectKindTrust_Stored()
        {
            var svc = Build();
            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "bc34-sub", SubjectKind = KycAmlOnboardingSubjectKind.Trust },
                "actor-bc34");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.SubjectKind, Is.EqualTo(KycAmlOnboardingSubjectKind.Trust));
        }

        /// <summary>BC35: CreateCase with SubjectKind.Unknown stores correctly.</summary>
        [Test]
        public async Task BC35_CreateCase_SubjectKindUnknown_Stored()
        {
            var svc = Build();
            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "bc35-sub", SubjectKind = KycAmlOnboardingSubjectKind.Unknown },
                "actor-bc35");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.SubjectKind, Is.EqualTo(KycAmlOnboardingSubjectKind.Unknown));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC36–BC41 : ListCases — filter and pagination
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC36: ListCases with SubjectId filter returns only matching cases.</summary>
        [Test]
        public async Task BC36_ListCases_FilterBySubjectId_ReturnsOnlyMatchingCases()
        {
            var svc = Build();
            await CreateCaseId(svc, "subj-alpha-bc36");
            await CreateCaseId(svc, "subj-alpha-bc36");
            await CreateCaseId(svc, "subj-beta-bc36");

            var resp = await svc.ListCasesAsync(
                new ListOnboardingCasesRequest { SubjectId = "subj-alpha-bc36" });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Cases, Has.All.Matches<KycAmlOnboardingCase>(c => c.SubjectId == "subj-alpha-bc36"));
        }

        /// <summary>BC37: ListCases with State filter returns only cases in that state.</summary>
        [Test]
        public async Task BC37_ListCases_FilterByState_ReturnsOnlyMatchingCases()
        {
            var svc = Build();
            var caseA = await CreateCaseId(svc, "subj-bc37-a");
            var caseB = await CreateCaseId(svc, "subj-bc37-b");

            await RejectCase(svc, caseA);

            var resp = await svc.ListCasesAsync(
                new ListOnboardingCasesRequest { State = KycAmlOnboardingCaseState.Rejected });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Cases, Has.All.Matches<KycAmlOnboardingCase>(c => c.State == KycAmlOnboardingCaseState.Rejected));
            Assert.That(resp.Cases.Any(c => c.CaseId == caseA), Is.True);
            Assert.That(resp.Cases.Any(c => c.CaseId == caseB), Is.False);
        }

        /// <summary>BC38: ListCases with both SubjectId and State filters returns intersection.</summary>
        [Test]
        public async Task BC38_ListCases_FilterBySubjectAndState_ReturnsIntersection()
        {
            var svc = Build();
            var caseX = await CreateCaseId(svc, "subj-bc38");
            await CreateCaseId(svc, "subj-bc38"); // same subject, not rejected
            await RejectCase(svc, caseX);

            var resp = await svc.ListCasesAsync(new ListOnboardingCasesRequest
            {
                SubjectId = "subj-bc38",
                State = KycAmlOnboardingCaseState.Rejected
            });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Cases, Has.Count.EqualTo(1));
            Assert.That(resp.Cases[0].CaseId, Is.EqualTo(caseX));
        }

        /// <summary>BC39: ListCases page-size 0 is clamped to 1.</summary>
        [Test]
        public async Task BC39_ListCases_PageSizeZero_ClampedToOne()
        {
            var svc = Build();
            for (int i = 0; i < 5; i++) await CreateCaseId(svc, $"subj-bc39-{i}");

            var resp = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 0 });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Cases, Has.Count.EqualTo(1));
        }

        /// <summary>BC40: ListCases page-size 501 is clamped to 500; returns all cases when total is less than 500.</summary>
        [Test]
        public async Task BC40_ListCases_PageSizeOver500_ClampedTo500()
        {
            var svc = Build();
            for (int i = 0; i < 10; i++) await CreateCaseId(svc, $"subj-bc40-{i}");

            var resp = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 501 });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Cases, Has.Count.EqualTo(10));
        }

        /// <summary>BC41: ListCases with pagination PageToken skips correctly.</summary>
        [Test]
        public async Task BC41_ListCases_WithPageToken_SkipsCorrectly()
        {
            var svc = Build();
            for (int i = 0; i < 5; i++) await CreateCaseId(svc, $"subj-bc41-{i}");

            var page1 = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 3, PageToken = "0" });
            var page2 = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 3, PageToken = "3" });

            Assert.That(page1.Cases, Has.Count.EqualTo(3));
            Assert.That(page2.Cases, Has.Count.EqualTo(2));
            var ids1 = page1.Cases.Select(c => c.CaseId).ToHashSet();
            var ids2 = page2.Cases.Select(c => c.CaseId).ToHashSet();
            Assert.That(ids1.Intersect(ids2), Is.Empty, "Pages must not overlap");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC42–BC46 : GetEvidenceSummary — state-based evidence derivation
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC42: Evidence for Rejected case with journey = DegradedPartialEvidence.</summary>
        [Test]
        public async Task BC42_GetEvidenceSummary_RejectedCaseWithJourney_ReturnsDegradedPartialEvidence()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc42");
            await RejectCase(svc, caseId);

            var resp = await svc.GetEvidenceSummaryAsync(caseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.DegradedPartialEvidence));
            Assert.That(resp.Summary.IsReleaseGrade, Is.False);
            Assert.That(resp.Summary.IsProviderBacked, Is.True);
        }

        /// <summary>BC43: Evidence for ProviderChecksStarted case = PendingVerification.</summary>
        [Test]
        public async Task BC43_GetEvidenceSummary_ProviderChecksStarted_ReturnsPendingVerification()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc43");

            var resp = await svc.GetEvidenceSummaryAsync(caseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.PendingVerification));
            Assert.That(resp.Summary.IsReleaseGrade, Is.False);
        }

        /// <summary>BC44: Evidence for Initiated case with provider configured (no journey) = PendingVerification.</summary>
        [Test]
        public async Task BC44_GetEvidenceSummary_InitiatedWithProvider_ReturnsPendingVerification()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);

            var resp = await svc.GetEvidenceSummaryAsync(caseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.PendingVerification));
            Assert.That(resp.Summary.IsProviderConfigured, Is.True);
            Assert.That(resp.Summary.ActionableGuidance, Does.Contain("initiate-checks"));
        }

        /// <summary>BC45: Evidence for ConfigurationMissing case (no provider) = MissingConfiguration.</summary>
        [Test]
        public async Task BC45_GetEvidenceSummary_NoProviderService_ReturnsMissingConfiguration()
        {
            var svc = Build(); // no journey
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc45");

            var resp = await svc.GetEvidenceSummaryAsync(caseId);

            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.MissingConfiguration));
            Assert.That(resp.Summary.IsProviderConfigured, Is.False);
        }

        /// <summary>BC46: Evidence when journey status is Degraded = ProviderUnavailable.</summary>
        [Test]
        public async Task BC46_GetEvidenceSummary_DegradedJourneyStatus_ReturnsProviderUnavailable()
        {
            var journey = new ConfigurableJourney { StatusStage = VerificationJourneyStage.Degraded };
            var svc = Build(journey);
            var caseId = await CreateCaseId(svc);
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-bc46");

            var resp = await svc.GetEvidenceSummaryAsync(caseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.ProviderUnavailable));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC47–BC48 : Idempotency
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC47: CreateCase with same IdempotencyKey and SubjectId returns same CaseId.</summary>
        [Test]
        public async Task BC47_CreateCase_SameIdempotencyKey_ReturnsSameCaseId()
        {
            var svc = Build();
            var req = new CreateOnboardingCaseRequest
            {
                SubjectId = "subj-idem-bc47",
                IdempotencyKey = "idem-key-bc47"
            };

            var resp1 = await svc.CreateCaseAsync(req, "actor-bc47");
            var resp2 = await svc.CreateCaseAsync(req, "actor-bc47");

            Assert.That(resp1.Case!.CaseId, Is.EqualTo(resp2.Case!.CaseId));
        }

        /// <summary>BC48: CreateCase with no IdempotencyKey always creates new distinct cases.</summary>
        [Test]
        public async Task BC48_CreateCase_NoIdempotencyKey_CreatesDistinctCases()
        {
            var svc = Build();

            var resp1 = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "subj-bc48" }, "actor-bc48");
            var resp2 = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "subj-bc48" }, "actor-bc48");

            Assert.That(resp1.Case!.CaseId, Is.Not.EqualTo(resp2.Case!.CaseId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC49–BC50 : Schema contract — required fields always populated
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC49: CreateCase response always has CaseId, State, and CorrelationId populated.</summary>
        [Test]
        public async Task BC49_CreateCase_ResponseSchemaContract_RequiredFieldsPopulated()
        {
            var svc = Build();
            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "bc49-sub" }, "actor-bc49");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.CaseId, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.Case.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated));
        }

        /// <summary>BC50: RecordReviewerAction response includes Action with ActionId, Timestamp, and ActorId.</summary>
        [Test]
        public async Task BC50_RecordReviewerAction_ActionSchemaContract_RequiredFieldsPopulated()
        {
            var svc = Build();
            var caseId = await CreateCaseId(svc);

            var resp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest
                {
                    Kind = KycAmlOnboardingActionKind.AddNote,
                    Notes = "BC50 schema test",
                    Rationale = "BC50"
                }, "actor-bc50");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Action!.ActionId, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.Action.Timestamp, Is.GreaterThan(DateTimeOffset.MinValue));
            Assert.That(resp.Action.ActorId, Is.EqualTo("actor-bc50"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BC51–BC52 : Edge cases
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>BC51: CreateCase with null CorrelationId still generates one in response.</summary>
        [Test]
        public async Task BC51_CreateCase_NullCorrelationId_GeneratesOneInResponse()
        {
            var svc = Build();
            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest
                {
                    SubjectId = "bc51-sub",
                    CorrelationId = null
                }, "actor-bc51");

            Assert.That(resp.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>BC52: EmitWebhookFireAndForget does not throw when IWebhookService is null.</summary>
        [Test]
        public async Task BC52_WebhookNull_DoesNotThrow()
        {
            var svc = new KycAmlOnboardingCaseService(
                NullLogger<KycAmlOnboardingCaseService>.Instance,
                webhookService: null); // explicitly null webhook

            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "bc52-sub" }, "actor-bc52");

            Assert.That(resp.Success, Is.True, "Service must not throw when webhook is null");
        }
    }
}

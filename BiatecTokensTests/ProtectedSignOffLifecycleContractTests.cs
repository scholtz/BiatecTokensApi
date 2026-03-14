using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Models.DeploymentSignOff;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Models.ProtectedSignOff;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Lifecycle contract tests for <see cref="ProtectedSignOffEnvironmentService"/>.
    ///
    /// These tests lock in the per-stage field contracts required by the strict sign-off
    /// Playwright suite and the evidence manifest:
    ///
    /// LC1:  Authentication stage is always Verified when DI is healthy (structural guarantee).
    /// LC2:  Initiation stage is Verified when state-machine returns a valid transition.
    /// LC3:  StatusPolling stage is Verified when contract service returns a non-null response.
    /// LC4:  TerminalState stage is Verified when contract service returns a structured response.
    /// LC5:  Validation stage is Verified when sign-off service returns a proof with ProofId + DeploymentId.
    /// LC6:  Complete stage is always the last stage in the list.
    /// LC7:  Stages are in enum-declaration order (Authentication → … → Complete).
    /// LC8:  UserGuidance is null for every Verified stage (no noise in evidence artifact).
    /// LC9:  UserGuidance is non-null for every Failed stage (operator can act on guidance).
    /// LC10: Skipped stages have non-empty Description and Detail.
    /// LC11: VerifiedStageCount equals the number of stages whose Outcome is Verified.
    /// LC12: FailedStageCount equals the number of stages whose Outcome is Failed.
    /// LC13: IsLifecycleVerified is false when any required stage fails.
    /// LC14: IsLifecycleVerified is true only when all stages are Verified.
    /// LC15: ActionableGuidance is null when IsLifecycleVerified is true.
    /// LC16: ActionableGuidance is non-null when IsLifecycleVerified is false.
    /// LC17: ExecutedAt is a parseable ISO 8601 timestamp.
    /// LC18: VerificationId uniqueness across two calls with different correlation IDs.
    /// LC19: IssuerId from request is echoed in the response.
    /// LC20: DeploymentId from request is echoed in the response.
    /// LC21: Authentication stage Detail includes the correlation ID.
    /// LC22: Validation stage Detail includes the ProofId when proof is generated successfully.
    /// LC23: TerminalState stage fails when contract service throws; subsequent stages are Skipped.
    /// LC24: Validation stage fails when proof is null (service returns null); not an exception.
    /// LC25: VerifiedStageCount is exactly 6 when all stages pass (full lifecycle verified).
    /// LC26: FailedStageCount is exactly 0 when all stages pass (no noise in evidence).
    /// LC27: Stage.Stage property matches the expected enum for each position in the list.
    /// LC28: Stage.Detail is non-null and non-empty for every stage (verified and skipped).
    /// LC29: ReachedStage in response matches Complete when all stages verify successfully.
    /// LC30: Validation fails when proof has an empty ProofId (structurally incomplete proof is rejected).
    /// </summary>
    [TestFixture]
    public class ProtectedSignOffLifecycleContractTests
    {
        private Mock<IIssuerWorkflowService> _issuerWorkflowMock = null!;
        private Mock<IDeploymentSignOffService> _signOffServiceMock = null!;
        private Mock<IBackendDeploymentLifecycleContractService> _contractServiceMock = null!;
        private ProtectedSignOffEnvironmentService _service = null!;

        private static IConfiguration BuildValidConfiguration() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtConfig:SecretKey"] = "LifecycleContractTestSecretKey32CharsMin!!",
                    ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                })
                .Build();

        private static BackendDeploymentContractResponse BuildStructuredStatusResponse(
            string deploymentId = "sign-off-fixture-deployment-001") =>
            new BackendDeploymentContractResponse
            {
                DeploymentId = deploymentId,
                State = ContractLifecycleState.Completed,
                Message = "Deployment completed."
            };

        private static DeploymentSignOffProof BuildValidProof(string deploymentId = "sign-off-fixture-deployment-001") =>
            new DeploymentSignOffProof
            {
                ProofId = "proof-test-abc1234567890",
                DeploymentId = deploymentId,
                CorrelationId = "test-correlation",
                Verdict = SignOffVerdict.Approved,
                IsReadyForSignOff = true,
                GeneratedAt = DateTime.UtcNow.ToString("O")
            };

        [SetUp]
        public void SetUp()
        {
            _issuerWorkflowMock = new Mock<IIssuerWorkflowService>();
            _signOffServiceMock = new Mock<IDeploymentSignOffService>();
            _contractServiceMock = new Mock<IBackendDeploymentLifecycleContractService>();

            // Happy-path defaults
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = true, Reason = "Valid." });

            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse { Success = true, Members = new List<IssuerTeamMember>() });

            _issuerWorkflowMock
                .Setup(s => s.AddMemberAsync(It.IsAny<string>(), It.IsAny<AddIssuerTeamMemberRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMemberResponse
                {
                    Success = true,
                    Member = new IssuerTeamMember
                    {
                        MemberId = "member-001",
                        IssuerId = ProtectedSignOffEnvironmentService.DefaultSignOffIssuerId,
                        UserId = ProtectedSignOffEnvironmentService.DefaultSignOffUserId,
                        Role = IssuerTeamRole.Admin,
                        IsActive = true,
                        AddedAt = DateTime.UtcNow
                    }
                });

            _contractServiceMock
                .Setup(s => s.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(BuildStructuredStatusResponse());

            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync(BuildValidProof());

            _service = new ProtectedSignOffEnvironmentService(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                BuildValidConfiguration(),
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);
        }

        // ── LC1: Authentication stage structural guarantee ────────────────────

        [Test]
        public async Task LC1_AuthenticationStage_IsAlwaysVerified_WhenDIIsHealthy()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc1-auth-stage" });

            SignOffLifecycleTransition? authStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Authentication);

            Assert.That(authStage, Is.Not.Null, "Authentication stage must be present in response");
            Assert.That(authStage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Verified),
                "Authentication stage must always be Verified when DI is healthy (structural guarantee)");
        }

        // ── LC2: Initiation stage ─────────────────────────────────────────────

        [Test]
        public async Task LC2_InitiationStage_IsVerified_WhenStateMachineReturnsValidTransition()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc2-initiation" });

            SignOffLifecycleTransition? stage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Initiation);

            Assert.That(stage, Is.Not.Null, "Initiation stage must be present");
            Assert.That(stage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Verified),
                "Initiation must be Verified when state machine accepts the transition");
        }

        // ── LC3: StatusPolling stage ──────────────────────────────────────────

        [Test]
        public async Task LC3_StatusPollingStage_IsVerified_WhenContractServiceReturnsNonNull()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc3-status-polling" });

            SignOffLifecycleTransition? stage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.StatusPolling);

            Assert.That(stage, Is.Not.Null, "StatusPolling stage must be present");
            Assert.That(stage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Verified),
                "StatusPolling must be Verified when contract service returns a non-null response");
        }

        // ── LC4: TerminalState stage ──────────────────────────────────────────

        [Test]
        public async Task LC4_TerminalStateStage_IsVerified_WhenContractServiceReturnsStructuredResponse()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc4-terminal" });

            SignOffLifecycleTransition? stage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.TerminalState);

            Assert.That(stage, Is.Not.Null, "TerminalState stage must be present");
            Assert.That(stage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Verified),
                "TerminalState must be Verified when the contract service returns a structured response");
        }

        // ── LC5: Validation stage ─────────────────────────────────────────────

        [Test]
        public async Task LC5_ValidationStage_IsVerified_WhenSignOffServiceReturnsProofWithRequiredFields()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc5-validation" });

            SignOffLifecycleTransition? stage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Validation);

            Assert.That(stage, Is.Not.Null, "Validation stage must be present");
            Assert.That(stage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Verified),
                "Validation must be Verified when proof has ProofId and DeploymentId");
        }

        // ── LC6: Complete stage is always last ────────────────────────────────

        [Test]
        public async Task LC6_CompleteStage_IsAlwaysLastInList_WhenLifecycleVerified()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc6-complete-last" });

            Assert.That(result.IsLifecycleVerified, Is.True);
            SignOffLifecycleTransition? lastStage = result.Stages.LastOrDefault();

            Assert.That(lastStage, Is.Not.Null);
            Assert.That(lastStage!.Stage, Is.EqualTo(SignOffLifecycleStage.Complete),
                "Complete must be the last stage in the Stages list when the lifecycle is verified");
        }

        // ── LC7: Stages are in enum-declaration order ─────────────────────────

        [Test]
        public async Task LC7_Stages_AreInEnumDeclarationOrder()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc7-stage-order" });

            SignOffLifecycleStage[] expectedOrder =
            [
                SignOffLifecycleStage.Authentication,
                SignOffLifecycleStage.Initiation,
                SignOffLifecycleStage.StatusPolling,
                SignOffLifecycleStage.TerminalState,
                SignOffLifecycleStage.Validation,
                SignOffLifecycleStage.Complete
            ];

            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.That(result.Stages[i].Stage, Is.EqualTo(expectedOrder[i]),
                    $"Stage at index {i} must be {expectedOrder[i]}");
            }
        }

        // ── LC8: UserGuidance is null for Verified stages ─────────────────────

        [Test]
        public async Task LC8_UserGuidance_IsNull_ForEveryVerifiedStage()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc8-user-guidance-null" });

            foreach (SignOffLifecycleTransition stage in result.Stages.Where(s => s.Outcome == LifecycleStageOutcome.Verified))
            {
                Assert.That(stage.UserGuidance, Is.Null,
                    $"UserGuidance must be null for Verified stage {stage.Stage} (no noise in evidence artifact)");
            }
        }

        // ── LC9: UserGuidance is non-null for Failed stages ───────────────────

        [Test]
        public async Task LC9_UserGuidance_IsNonNull_ForEveryFailedStage()
        {
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "Blocked for LC9 test." });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc9-user-guidance-nonnull" });

            foreach (SignOffLifecycleTransition stage in result.Stages.Where(s => s.Outcome == LifecycleStageOutcome.Failed))
            {
                Assert.That(stage.UserGuidance, Is.Not.Null.And.Not.Empty,
                    $"UserGuidance must be non-null for Failed stage {stage.Stage} so operators know how to act");
            }
        }

        // ── LC10: Skipped stages have non-empty Description and Detail ─────────

        [Test]
        public async Task LC10_SkippedStages_HaveNonEmpty_DescriptionAndDetail()
        {
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "Blocked for LC10 test." });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc10-skipped-fields" });

            List<SignOffLifecycleTransition> skipped = result.Stages
                .Where(s => s.Outcome == LifecycleStageOutcome.Skipped)
                .ToList();

            Assert.That(skipped, Is.Not.Empty, "There must be at least one Skipped stage when Initiation fails");

            foreach (SignOffLifecycleTransition stage in skipped)
            {
                Assert.That(stage.Description, Is.Not.Null.And.Not.Empty,
                    $"Skipped stage {stage.Stage} must have a Description");
                Assert.That(stage.Detail, Is.Not.Null.And.Not.Empty,
                    $"Skipped stage {stage.Stage} must have a Detail");
            }
        }

        // ── LC11 & LC12: Count fields are accurate ────────────────────────────

        [Test]
        public async Task LC11_LC12_StageCounts_MatchActualOutcomes()
        {
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "Count test." });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc11-lc12-counts" });

            int actualVerified = result.Stages.Count(s => s.Outcome == LifecycleStageOutcome.Verified);
            int actualFailed = result.Stages.Count(s => s.Outcome == LifecycleStageOutcome.Failed);

            Assert.That(result.VerifiedStageCount, Is.EqualTo(actualVerified),
                "VerifiedStageCount must exactly match the count of Verified outcomes");
            Assert.That(result.FailedStageCount, Is.EqualTo(actualFailed),
                "FailedStageCount must exactly match the count of Failed outcomes");
        }

        // ── LC13 & LC14: IsLifecycleVerified semantics ────────────────────────

        [Test]
        public async Task LC13_IsLifecycleVerified_IsFalse_WhenAnyRequiredStageFails()
        {
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "LC13 test." });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc13-verified-false" });

            Assert.That(result.IsLifecycleVerified, Is.False,
                "IsLifecycleVerified must be false when any stage fails");
        }

        [Test]
        public async Task LC14_IsLifecycleVerified_IsTrue_OnlyWhenAllStagesAreVerified()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc14-verified-true" });

            bool allVerified = result.Stages.All(s => s.Outcome == LifecycleStageOutcome.Verified);
            Assert.That(result.IsLifecycleVerified, Is.EqualTo(allVerified),
                "IsLifecycleVerified must be true if and only if all stages are Verified");
        }

        // ── LC15 & LC16: ActionableGuidance semantics ─────────────────────────

        [Test]
        public async Task LC15_ActionableGuidance_IsNull_WhenIsLifecycleVerifiedIsTrue()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc15-guidance-null" });

            Assert.That(result.IsLifecycleVerified, Is.True);
            Assert.That(result.ActionableGuidance, Is.Null,
                "ActionableGuidance must be null when lifecycle is verified (no noise in evidence artifact)");
        }

        [Test]
        public async Task LC16_ActionableGuidance_IsNonNull_WhenIsLifecycleVerifiedIsFalse()
        {
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "LC16 test." });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc16-guidance-nonnull" });

            Assert.That(result.IsLifecycleVerified, Is.False);
            Assert.That(result.ActionableGuidance, Is.Not.Null.And.Not.Empty,
                "ActionableGuidance must be non-null when lifecycle verification failed");
        }

        // ── LC17: ExecutedAt is a parseable ISO 8601 timestamp ────────────────

        [Test]
        public async Task LC17_ExecutedAt_IsValidISO8601Timestamp()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc17-timestamp" });

            Assert.That(result.ExecutedAt, Is.Not.Null.And.Not.Empty, "ExecutedAt must be populated");
            Assert.That(DateTime.TryParse(result.ExecutedAt, out _), Is.True,
                "ExecutedAt must be a valid parseable timestamp");
        }

        // ── LC18: VerificationId uniqueness ───────────────────────────────────

        [Test]
        public async Task LC18_VerificationId_IsUnique_AcrossTwoCalls()
        {
            EnterpriseSignOffLifecycleResponse r1 = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc18-run-a" });
            EnterpriseSignOffLifecycleResponse r2 = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc18-run-b" });

            Assert.That(r1.VerificationId, Is.Not.EqualTo(r2.VerificationId),
                "Each lifecycle execution must produce a unique VerificationId");
        }

        // ── LC19 & LC20: IssuerId and DeploymentId echo ───────────────────────

        [Test]
        public async Task LC19_IssuerId_EchoedFromRequest()
        {
            const string requestIssuerId = "my-custom-issuer-for-lc19";

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest
                {
                    CorrelationId = "lc19-issuer-echo",
                    IssuerId = requestIssuerId
                });

            Assert.That(result.IssuerId, Is.EqualTo(requestIssuerId),
                "IssuerId in the response must echo the value supplied in the request");
        }

        [Test]
        public async Task LC20_DeploymentId_EchoedFromRequest()
        {
            const string requestDeploymentId = "my-custom-deployment-for-lc20";

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest
                {
                    CorrelationId = "lc20-deploy-echo",
                    DeploymentId = requestDeploymentId
                });

            Assert.That(result.DeploymentId, Is.EqualTo(requestDeploymentId),
                "DeploymentId in the response must echo the value supplied in the request");
        }

        // ── LC21: Authentication stage Detail includes correlation ID ─────────

        [Test]
        public async Task LC21_AuthenticationStage_Detail_IncludesCorrelationId()
        {
            const string correlationId = "lc21-auth-detail-check";

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = correlationId });

            SignOffLifecycleTransition? authStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Authentication);

            Assert.That(authStage, Is.Not.Null);
            Assert.That(authStage!.Detail, Does.Contain(correlationId),
                "Authentication stage Detail must mention the correlation ID for traceability");
        }

        // ── LC22: Validation stage Detail includes ProofId ────────────────────

        [Test]
        public async Task LC22_ValidationStage_Detail_IncludesProofId_WhenProofGenerated()
        {
            const string proofId = "proof-lc22-testabc1234567";
            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync(new DeploymentSignOffProof
                {
                    ProofId = proofId,
                    DeploymentId = ProtectedSignOffEnvironmentService.DefaultSignOffDeploymentId,
                    GeneratedAt = DateTime.UtcNow.ToString("O")
                });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc22-proof-id-in-detail" });

            SignOffLifecycleTransition? validationStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Validation);

            Assert.That(validationStage, Is.Not.Null);
            Assert.That(validationStage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Verified));
            Assert.That(validationStage.Detail, Does.Contain(proofId),
                "Validation stage Detail must include the ProofId for evidence traceability");
        }

        // ── LC23: When contract service throws, lifecycle stops and subsequent stages are Skipped ──

        [Test]
        public async Task LC23_ContractServiceThrows_StatusPollingFails_LaterStagesSkipped()
        {
            // StatusPolling and TerminalState both use GetStatusAsync.
            // When it throws, StatusPolling is the first to fail, which causes
            // TerminalState, Validation and Complete to be Skipped.
            _contractServiceMock
                .Setup(s => s.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Contract service unavailable for LC23."));

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc23-contract-throw" });

            Assert.That(result.IsLifecycleVerified, Is.False);

            // StatusPolling must fail
            SignOffLifecycleTransition? pollStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.StatusPolling);
            Assert.That(pollStage, Is.Not.Null);
            Assert.That(pollStage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Failed),
                "StatusPolling must be Failed when the contract service throws");

            // All stages after StatusPolling must be Skipped
            var skippedStages = result.Stages
                .Where(s => s.Stage > SignOffLifecycleStage.StatusPolling)
                .ToList();
            Assert.That(skippedStages, Is.Not.Empty, "Stages after StatusPolling must be present");
            foreach (SignOffLifecycleTransition stage in skippedStages)
            {
                Assert.That(stage.Outcome, Is.EqualTo(LifecycleStageOutcome.Skipped),
                    $"Stage {stage.Stage} must be Skipped when StatusPolling failed");
            }
        }

        // ── LC24: Validation fails when sign-off service returns null proof ────

        [Test]
        public async Task LC24_ValidationStage_Fails_WhenSignOffServiceReturnsNullProof()
        {
            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync((DeploymentSignOffProof)null!);

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc24-null-proof" });

            Assert.That(result.IsLifecycleVerified, Is.False);

            SignOffLifecycleTransition? validationStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Validation);

            Assert.That(validationStage, Is.Not.Null);
            Assert.That(validationStage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Failed),
                "Validation must fail when GenerateSignOffProofAsync returns null");
            Assert.That(validationStage.UserGuidance, Is.Not.Null.And.Not.Empty,
                "UserGuidance must be present when Validation fails due to null proof");
        }

        // ── LC25 & LC26: Count values when all stages pass ────────────────────

        [Test]
        public async Task LC25_VerifiedStageCount_IsExactlySix_WhenAllStagesPass()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc25-count-six" });

            Assert.That(result.IsLifecycleVerified, Is.True);
            Assert.That(result.VerifiedStageCount, Is.EqualTo(6),
                "VerifiedStageCount must be exactly 6 when all stages pass");
        }

        [Test]
        public async Task LC26_FailedStageCount_IsExactlyZero_WhenAllStagesPass()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc26-count-zero" });

            Assert.That(result.IsLifecycleVerified, Is.True);
            Assert.That(result.FailedStageCount, Is.EqualTo(0),
                "FailedStageCount must be exactly 0 when all stages pass");
        }

        // ── LC27: Stage.Stage property matches expected enum for each position ─

        [Test]
        public async Task LC27_EachStage_StageProperty_MatchesExpectedEnum()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc27-stage-enum" });

            var expectedStages = new SignOffLifecycleStage[]
            {
                SignOffLifecycleStage.Authentication,
                SignOffLifecycleStage.Initiation,
                SignOffLifecycleStage.StatusPolling,
                SignOffLifecycleStage.TerminalState,
                SignOffLifecycleStage.Validation,
                SignOffLifecycleStage.Complete
            };

            Assert.That(result.Stages.Count, Is.EqualTo(expectedStages.Length));
            for (int i = 0; i < expectedStages.Length; i++)
            {
                Assert.That(result.Stages[i].Stage, Is.EqualTo(expectedStages[i]),
                    $"Stage at index {i} must have Stage={expectedStages[i]}");
            }
        }

        // ── LC28: Stage.Detail is non-null and non-empty for all stages ────────

        [Test]
        public async Task LC28_EachStage_Detail_IsNonNullAndNonEmpty()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc28-detail-nonnull" });

            foreach (SignOffLifecycleTransition stage in result.Stages)
            {
                Assert.That(stage.Detail, Is.Not.Null.And.Not.Empty,
                    $"Stage {stage.Stage} Detail must be non-null and non-empty");
            }
        }

        // ── LC29: ReachedStage is Complete when all stages verify ─────────────

        [Test]
        public async Task LC29_ReachedStage_IsComplete_WhenAllStagesVerify()
        {
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc29-reached-complete" });

            Assert.That(result.IsLifecycleVerified, Is.True);
            Assert.That(result.ReachedStage, Is.EqualTo(SignOffLifecycleStage.Complete),
                "ReachedStage must be Complete when all stages verify (required by Playwright suite contract)");
        }

        // ── LC30: Validation proof with empty ProofId fails the stage ─────────

        [Test]
        public async Task LC30_ValidationStage_Fails_WhenProofHasEmptyProofId()
        {
            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync(new DeploymentSignOffProof
                {
                    ProofId = string.Empty,       // empty ProofId = structurally incomplete
                    DeploymentId = "deploy-lc30",
                    GeneratedAt = DateTime.UtcNow.ToString("O")
                });

            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lc30-empty-proofid" });

            Assert.That(result.IsLifecycleVerified, Is.False);

            SignOffLifecycleTransition? validationStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Validation);

            Assert.That(validationStage, Is.Not.Null);
            Assert.That(validationStage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Failed),
                "Validation must fail when the proof has an empty ProofId");
        }
    }
}

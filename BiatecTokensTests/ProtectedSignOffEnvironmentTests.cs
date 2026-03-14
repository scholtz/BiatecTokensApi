using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Models.ProtectedSignOff;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for <see cref="ProtectedSignOffEnvironmentService"/> and
    /// <see cref="BiatecTokensApi.Controllers.ProtectedSignOffController"/>.
    ///
    /// Coverage:
    ///
    /// AC1: Protected backend environment is documented and checkable for sign-off readiness.
    /// AC2: Lifecycle contract returns deterministic, fail-closed behavior for all stages.
    /// AC3: Enterprise fixtures are provisioned with realistic permissions.
    /// AC4: Backend responses expose structured information for operator guidance.
    /// AC5: Diagnostics clearly distinguish configuration, authorization, contract, and lifecycle failures.
    /// AC6: Automated tests lock in the protected contract and authorization behavior.
    /// AC7: Result enables a credible green protected strict-signoff run without security weakening.
    /// </summary>
    [TestFixture]
    public class ProtectedSignOffEnvironmentTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — ProtectedSignOffEnvironmentService directly
        // ═══════════════════════════════════════════════════════════════════════

        private Mock<IIssuerWorkflowService> _issuerWorkflowMock = null!;
        private Mock<IDeploymentSignOffService> _signOffServiceMock = null!;
        private Mock<IBackendDeploymentLifecycleContractService> _contractServiceMock = null!;
        private ProtectedSignOffEnvironmentService _service = null!;
        private IConfiguration _validConfiguration = null!;

        /// <summary>
        /// Builds a minimal in-memory IConfiguration containing all required keys for
        /// a fully-configured protected sign-off environment. Used as the default configuration
        /// for unit tests that do not specifically test missing-configuration behaviour.
        /// </summary>
        private static IConfiguration BuildValidConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtConfig:SecretKey"] = "ProtectedSignOffUnitTestSecretKey32CharsMin!!",
                    ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                })
                .Build();
        }

        [SetUp]
        public void SetUp()
        {
            _issuerWorkflowMock = new Mock<IIssuerWorkflowService>();
            _signOffServiceMock = new Mock<IDeploymentSignOffService>();
            _contractServiceMock = new Mock<IBackendDeploymentLifecycleContractService>();
            _validConfiguration = BuildValidConfiguration();

            // Default: ListMembersAsync returns empty (no fixtures provisioned)
            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse { Success = true, Members = new List<IssuerTeamMember>() });

            // Default: state machine works
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = true, Reason = "Valid transition." });

            // Default: GetStatusAsync returns a not-found structured response
            _contractServiceMock
                .Setup(s => s.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new BackendDeploymentContractResponse
                {
                    DeploymentId = "sign-off-fixture-deployment-001",
                    ErrorCode = DeploymentErrorCode.RequiredFieldMissing,
                    Message = "Deployment not found."
                });

            // Default: GenerateSignOffProofAsync returns a structured (blocked) proof
            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync(new BiatecTokensApi.Models.DeploymentSignOff.DeploymentSignOffProof
                {
                    ProofId = "proof-abc1234567890123",
                    DeploymentId = "sign-off-fixture-deployment-001",
                    CorrelationId = "test-correlation-id",
                    Verdict = BiatecTokensApi.Models.DeploymentSignOff.SignOffVerdict.Blocked,
                    IsReadyForSignOff = false,
                    GeneratedAt = DateTime.UtcNow.ToString("O")
                });

            // Default: AddMemberAsync succeeds
            _issuerWorkflowMock
                .Setup(s => s.AddMemberAsync(
                    It.IsAny<string>(),
                    It.IsAny<AddIssuerTeamMemberRequest>(),
                    It.IsAny<string>()))
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

            _service = new ProtectedSignOffEnvironmentService(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                _validConfiguration,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);
        }

        // ── CheckEnvironmentReadiness — happy path ────────────────────────────

        [Test]
        public async Task CheckEnvironmentReadiness_AllServicesAvailable_ReturnsReady()
        {
            // Arrange: ListMembersAsync returns an admin member (fixtures provisioned)
            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse
                {
                    Success = true,
                    Members = new List<IssuerTeamMember>
                    {
                        new IssuerTeamMember
                        {
                            IssuerId = ProtectedSignOffEnvironmentService.DefaultSignOffIssuerId,
                            UserId = ProtectedSignOffEnvironmentService.DefaultSignOffUserId,
                            Role = IssuerTeamRole.Admin,
                            IsActive = true
                        }
                    }
                });

            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "test-corr-001",
                    IncludeFixtureCheck = true,
                    IncludeObservabilityCheck = true
                });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Ready));
            Assert.That(result.IsReadyForProtectedRun, Is.True);
            Assert.That(result.CriticalFailCount, Is.EqualTo(0));
            Assert.That(result.CheckId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Is.EqualTo("test-corr-001"));
            Assert.That(result.ContractVersion, Is.EqualTo("1.0"));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_ResponseHasStructuredChecks()
        {
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "test-corr-002" });

            // Assert: response must include checks for each category
            Assert.That(result.Checks, Is.Not.Null.And.Not.Empty);
            Assert.That(result.TotalCheckCount, Is.GreaterThan(0));
            Assert.That(result.Checks.Count, Is.EqualTo(result.TotalCheckCount));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_AutoGeneratesCorrelationIdWhenAbsent()
        {
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = null });

            // Assert
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Does.StartWith("auto-"));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_CheckIdIsUniquePerCall()
        {
            // Act
            ProtectedSignOffEnvironmentResponse r1 = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "id-1" });
            ProtectedSignOffEnvironmentResponse r2 = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "id-2" });

            // Assert
            Assert.That(r1.CheckId, Is.Not.EqualTo(r2.CheckId));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_WithoutFixtureCheck_SkipsFixtureCategory()
        {
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    IncludeFixtureCheck = false,
                    IncludeObservabilityCheck = false
                });

            // Assert: no fixture or observability checks
            Assert.That(result.Checks.Any(c => c.Category == EnvironmentCheckCategory.EnterpriseFixtures),
                Is.False, "Fixture check should be skipped when IncludeFixtureCheck=false");
            Assert.That(result.Checks.Any(c => c.Category == EnvironmentCheckCategory.Observability),
                Is.False, "Observability check should be skipped when IncludeObservabilityCheck=false");
        }

        [Test]
        public async Task CheckEnvironmentReadiness_FixtureCheckDegraded_WhenNoAdminMember()
        {
            // Arrange: no active admin members
            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse { Success = true, Members = new List<IssuerTeamMember>() });

            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { IncludeFixtureCheck = true });

            // Assert: fixture check is degraded (non-critical), overall is degraded
            EnvironmentCheck? fixtureCheck = result.Checks
                .FirstOrDefault(c => c.Category == EnvironmentCheckCategory.EnterpriseFixtures);

            Assert.That(fixtureCheck, Is.Not.Null);
            Assert.That(fixtureCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail));
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Degraded));
            Assert.That(result.IsReadyForProtectedRun, Is.False);
        }

        [Test]
        public async Task CheckEnvironmentReadiness_ProvidedCorrelationId_PassesObservabilityCheck()
        {
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "test-obs-123",
                    IncludeObservabilityCheck = true
                });

            // Assert
            EnvironmentCheck? obsCheck = result.Checks
                .FirstOrDefault(c => c.Category == EnvironmentCheckCategory.Observability);
            Assert.That(obsCheck, Is.Not.Null);
            Assert.That(obsCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_NullRequest_DoesNotThrow()
        {
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(null!);

            // Assert: returns a valid response even with null request
            Assert.That(result, Is.Not.Null);
            Assert.That(result.CheckId, Is.Not.Null.And.Not.Empty);
        }

        // ── ExecuteSignOffLifecycle — happy path ──────────────────────────────

        [Test]
        public async Task ExecuteSignOffLifecycle_AllStagesVerified_ReturnsComplete()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest
                {
                    IssuerId = "test-issuer-001",
                    DeploymentId = "deploy-abc123",
                    CorrelationId = "lifecycle-test-001"
                });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsLifecycleVerified, Is.True);
            Assert.That(result.ReachedStage, Is.EqualTo(SignOffLifecycleStage.Complete));
            Assert.That(result.FailedStageCount, Is.EqualTo(0));
            Assert.That(result.VerifiedStageCount, Is.GreaterThan(0));
            Assert.That(result.VerificationId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Is.EqualTo("lifecycle-test-001"));
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_AllSixStagesPresent()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lifecycle-stages-test" });

            // Assert: must have all 6 stages
            int expectedStages = Enum.GetValues<SignOffLifecycleStage>().Length;
            Assert.That(result.Stages.Count, Is.EqualTo(expectedStages));
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_AllStagesVerified_WhenServicesAvailable()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lifecycle-verified" });

            // Assert: no skipped stages, none failed
            Assert.That(result.Stages.Any(s => s.Outcome == LifecycleStageOutcome.Skipped), Is.False,
                "No stages should be skipped when all services are available");
            Assert.That(result.Stages.Any(s => s.Outcome == LifecycleStageOutcome.Failed), Is.False,
                "No stages should fail when all services return valid responses");
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_UsesDefaultIssuerId_WhenNotProvided()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { IssuerId = null });

            // Assert
            Assert.That(result.IssuerId, Is.EqualTo(ProtectedSignOffEnvironmentService.DefaultSignOffIssuerId));
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_UsesDefaultDeploymentId_WhenNotProvided()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { DeploymentId = null });

            // Assert
            Assert.That(result.DeploymentId, Is.EqualTo(ProtectedSignOffEnvironmentService.DefaultSignOffDeploymentId));
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_InitiationFails_SkipsRemainingStages()
        {
            // Arrange: state machine returns invalid transition
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "Blocked for test." });

            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "initiation-fail-test" });

            // Assert
            Assert.That(result.IsLifecycleVerified, Is.False);
            Assert.That(result.ReachedStage, Is.EqualTo(SignOffLifecycleStage.Initiation));
            Assert.That(result.FailedStageCount, Is.GreaterThan(0));
            Assert.That(result.Stages.Any(s => s.Outcome == LifecycleStageOutcome.Skipped), Is.True,
                "Subsequent stages should be skipped after initiation failure");
            Assert.That(result.ActionableGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_StatusPollingFails_SkipsLaterStages()
        {
            // Arrange: contract service throws exception
            _contractServiceMock
                .Setup(s => s.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Service unavailable."));

            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "status-fail-test" });

            // Assert
            Assert.That(result.IsLifecycleVerified, Is.False);
            Assert.That(result.FailedStageCount, Is.GreaterThan(0));
            Assert.That(result.Stages.Any(s => s.Outcome == LifecycleStageOutcome.Skipped), Is.True);
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_ValidationFails_WhenSignOffServiceThrows()
        {
            // Arrange: GenerateSignOffProofAsync throws
            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Proof generation failure."));

            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "validation-fail-test" });

            // Assert
            Assert.That(result.IsLifecycleVerified, Is.False);
            SignOffLifecycleTransition? validationStage = result.Stages
                .FirstOrDefault(s => s.Stage == SignOffLifecycleStage.Validation);
            Assert.That(validationStage, Is.Not.Null);
            Assert.That(validationStage!.Outcome, Is.EqualTo(LifecycleStageOutcome.Failed));
            Assert.That(validationStage.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_EachStageHasVerifiedAtTimestamp()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "timestamp-test" });

            // Assert
            foreach (SignOffLifecycleTransition stage in result.Stages)
            {
                Assert.That(stage.VerifiedAt, Is.Not.Null.And.Not.Empty,
                    $"Stage {stage.Stage} must have a VerifiedAt timestamp");
            }
        }

        [Test]
        public async Task ExecuteSignOffLifecycle_EachStageHasDescription()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "description-test" });

            // Assert
            foreach (SignOffLifecycleTransition stage in result.Stages)
            {
                Assert.That(stage.Description, Is.Not.Null.And.Not.Empty,
                    $"Stage {stage.Stage} must have a description");
            }
        }

        // ── ProvisionEnterpriseFixtures ───────────────────────────────────────

        [Test]
        public async Task ProvisionEnterpriseFixtures_NewIssuer_SucceedsAndReturnsProvisioningId()
        {
            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { CorrelationId = "provision-test-001" });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsProvisioned, Is.True);
            Assert.That(result.WasAlreadyProvisioned, Is.False);
            Assert.That(result.ProvisioningId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Is.EqualTo("provision-test-001"));
            Assert.That(result.IssuerId, Is.EqualTo(ProtectedSignOffEnvironmentService.DefaultSignOffIssuerId));
            Assert.That(result.AdminUserId, Is.EqualTo(ProtectedSignOffEnvironmentService.DefaultSignOffUserId));
            Assert.That(result.ErrorCode, Is.Null);
        }

        [Test]
        public async Task ProvisionEnterpriseFixtures_ExistingIssuer_ReturnsAlreadyProvisioned()
        {
            // Arrange: issuer already has an admin member
            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse
                {
                    Success = true,
                    Members = new List<IssuerTeamMember>
                    {
                        new IssuerTeamMember { IssuerId = "existing-issuer", UserId = "admin", Role = IssuerTeamRole.Admin, IsActive = true }
                    }
                });

            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { ResetIfExists = false });

            // Assert
            Assert.That(result.IsProvisioned, Is.True);
            Assert.That(result.WasAlreadyProvisioned, Is.True);
            Assert.That(result.ErrorCode, Is.Null);

            // AddMemberAsync should NOT have been called
            _issuerWorkflowMock.Verify(
                s => s.AddMemberAsync(It.IsAny<string>(), It.IsAny<AddIssuerTeamMemberRequest>(), It.IsAny<string>()),
                Times.Never,
                "Should not re-provision when already provisioned and ResetIfExists=false");
        }

        [Test]
        public async Task ProvisionEnterpriseFixtures_AddMemberFails_ReturnsErrorResponse()
        {
            // Arrange: AddMemberAsync fails
            _issuerWorkflowMock
                .Setup(s => s.AddMemberAsync(It.IsAny<string>(), It.IsAny<AddIssuerTeamMemberRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMemberResponse
                {
                    Success = false,
                    ErrorCode = "SOME_ERROR",
                    ErrorMessage = "Could not add member."
                });

            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { CorrelationId = "provision-fail-001" });

            // Assert
            Assert.That(result.IsProvisioned, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty);
            Assert.That(result.OperatorGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ProvisionEnterpriseFixtures_AddMemberThrows_ReturnsErrorResponse()
        {
            // Arrange: AddMemberAsync throws
            _issuerWorkflowMock
                .Setup(s => s.AddMemberAsync(It.IsAny<string>(), It.IsAny<AddIssuerTeamMemberRequest>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Unexpected error."));

            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { CorrelationId = "provision-throw-001" });

            // Assert
            Assert.That(result.IsProvisioned, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNEXPECTED_EXCEPTION"));
            Assert.That(result.OperatorGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ProvisionEnterpriseFixtures_UsesCustomIssuerId_WhenProvided()
        {
            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { IssuerId = "custom-issuer-99" });

            // Assert
            Assert.That(result.IssuerId, Is.EqualTo("custom-issuer-99"));
        }

        [Test]
        public async Task ProvisionEnterpriseFixtures_UsesCustomUserId_WhenProvided()
        {
            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { SignOffUserId = "custom-admin@company.com" });

            // Assert
            Assert.That(result.AdminUserId, Is.EqualTo("custom-admin@company.com"));
        }

        [Test]
        public async Task ProvisionEnterpriseFixtures_AdminRoleIsAssigned()
        {
            // Act
            await _service.ProvisionEnterpriseFixturesAsync(new EnterpriseFixtureProvisionRequest());

            // Assert: AddMemberAsync was called with Admin role
            _issuerWorkflowMock.Verify(
                s => s.AddMemberAsync(
                    It.IsAny<string>(),
                    It.Is<AddIssuerTeamMemberRequest>(r => r.Role == IssuerTeamRole.Admin),
                    It.IsAny<string>()),
                Times.Once,
                "Fixtures must be provisioned with Admin role for the sign-off user");
        }

        // ── GetDiagnostics ────────────────────────────────────────────────────

        [Test]
        public async Task GetDiagnostics_AllServicesAvailable_IsOperational()
        {
            // Act
            ProtectedSignOffDiagnosticsResponse result = await _service.GetDiagnosticsAsync("diag-test-001");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsOperational, Is.True);
            Assert.That(result.DiagnosticsId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Is.EqualTo("diag-test-001"));
            Assert.That(result.ApiVersion, Is.EqualTo("v1"));
        }

        [Test]
        public async Task GetDiagnostics_HasFiveServiceAvailabilityEntries()
        {
            // Act
            ProtectedSignOffDiagnosticsResponse result = await _service.GetDiagnosticsAsync(null);

            // Assert: five availability entries reported:
            //   3 service entries: IIssuerWorkflowService, IBackendDeploymentLifecycleContractService,
            //                      IDeploymentSignOffService
            //   2 configuration entries: JwtConfig:SecretKey, App:Account
            // Update this count if new required services or configuration keys are added.
            Assert.That(result.ServiceAvailability.Count, Is.EqualTo(5));
            Assert.That(result.ServiceAvailability.All(s => s.IsAvailable), Is.True);
        }

        [Test]
        public async Task GetDiagnostics_AllServicesAvailable_NoBlockingNotes()
        {
            // Act
            ProtectedSignOffDiagnosticsResponse result = await _service.GetDiagnosticsAsync("diag-no-blocking");

            // Assert: no blocking notes when all services available
            Assert.That(result.Notes.Any(n => n.IsBlocking), Is.False);
        }

        [Test]
        public async Task GetDiagnostics_AutoGeneratesCorrelationId_WhenNull()
        {
            // Act
            ProtectedSignOffDiagnosticsResponse result = await _service.GetDiagnosticsAsync(null);

            // Assert
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Does.StartWith("auto-"));
        }

        [Test]
        public async Task GetDiagnostics_FailureCategorySummary_NoFailures_WhenOperational()
        {
            // Act
            ProtectedSignOffDiagnosticsResponse result = await _service.GetDiagnosticsAsync("diag-categories");

            // Assert: no failure categories when operational
            Assert.That(result.FailureCategories.HasServiceAvailabilityFailure, Is.False);
            Assert.That(result.FailureCategories.HasConfigurationFailure, Is.False);
            Assert.That(result.FailureCategories.HasAuthorizationFailure, Is.False);
            Assert.That(result.FailureCategories.HasContractFailure, Is.False);
            Assert.That(result.FailureCategories.HasLifecycleFailure, Is.False);
        }

        [Test]
        public async Task GetDiagnostics_GeneratedAtIsPopulated()
        {
            // Act
            ProtectedSignOffDiagnosticsResponse result = await _service.GetDiagnosticsAsync("diag-timestamp");

            // Assert
            Assert.That(result.GeneratedAt, Is.Not.Null.And.Not.Empty);
            Assert.That(DateTime.TryParse(result.GeneratedAt, out _), Is.True, "GeneratedAt should be a valid ISO 8601 timestamp");
        }

        // ── Contract shape validation ─────────────────────────────────────────

        [Test]
        public async Task EnvironmentResponse_ContractShape_AllRequiredFieldsPresent()
        {
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "contract-shape-test" });

            // Assert: all required contract fields present
            Assert.That(result.CheckId, Is.Not.Null.And.Not.Empty, "CheckId must be populated");
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId must be populated");
            Assert.That(result.CheckedAt, Is.Not.Null.And.Not.Empty, "CheckedAt must be populated");
            Assert.That(result.ContractVersion, Is.Not.Null.And.Not.Empty, "ContractVersion must be populated");
            Assert.That(result.Checks, Is.Not.Null, "Checks must be a non-null list");
            Assert.That(result.TotalCheckCount, Is.GreaterThan(0), "TotalCheckCount must be > 0");
            Assert.That(result.TotalCheckCount, Is.EqualTo(result.Checks.Count), "TotalCheckCount must match Checks.Count");
        }

        [Test]
        public async Task LifecycleResponse_ContractShape_AllRequiredFieldsPresent()
        {
            // Act
            EnterpriseSignOffLifecycleResponse result = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "lifecycle-contract-test" });

            // Assert
            Assert.That(result.VerificationId, Is.Not.Null.And.Not.Empty, "VerificationId must be populated");
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId must be populated");
            Assert.That(result.ExecutedAt, Is.Not.Null.And.Not.Empty, "ExecutedAt must be populated");
            Assert.That(result.Stages, Is.Not.Null, "Stages must be non-null");
            Assert.That(result.VerifiedStageCount + result.FailedStageCount,
                Is.EqualTo(result.Stages.Count(s => s.Outcome != LifecycleStageOutcome.Skipped)),
                "VerifiedStageCount + FailedStageCount must equal non-skipped stage count");
        }

        [Test]
        public async Task ProvisionResponse_ContractShape_AllRequiredFieldsPresent()
        {
            // Act
            EnterpriseFixtureProvisionResponse result = await _service.ProvisionEnterpriseFixturesAsync(
                new EnterpriseFixtureProvisionRequest { CorrelationId = "provision-contract-test" });

            // Assert
            Assert.That(result.ProvisioningId, Is.Not.Null.And.Not.Empty, "ProvisioningId must be populated");
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId must be populated");
            Assert.That(result.ProvisionedAt, Is.Not.Null.And.Not.Empty, "ProvisionedAt must be populated");
            Assert.That(result.IssuerId, Is.Not.Null.And.Not.Empty, "IssuerId must be populated");
            Assert.That(result.AdminUserId, Is.Not.Null.And.Not.Empty, "AdminUserId must be populated");
        }

        // ── Determinism tests ─────────────────────────────────────────────────

        [Test]
        public async Task ExecuteSignOffLifecycle_DeterministicAcrossThreeRuns()
        {
            // Act: run 3 times
            EnterpriseSignOffLifecycleResponse r1 = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "determ-run-1" });
            EnterpriseSignOffLifecycleResponse r2 = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "determ-run-2" });
            EnterpriseSignOffLifecycleResponse r3 = await _service.ExecuteSignOffLifecycleAsync(
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "determ-run-3" });

            // Assert: all three runs produce the same outcome
            Assert.That(r1.IsLifecycleVerified, Is.EqualTo(r2.IsLifecycleVerified), "Run 1 vs 2: IsLifecycleVerified must be identical");
            Assert.That(r2.IsLifecycleVerified, Is.EqualTo(r3.IsLifecycleVerified), "Run 2 vs 3: IsLifecycleVerified must be identical");
            Assert.That(r1.ReachedStage, Is.EqualTo(r2.ReachedStage), "Run 1 vs 2: ReachedStage must be identical");
            Assert.That(r2.ReachedStage, Is.EqualTo(r3.ReachedStage), "Run 2 vs 3: ReachedStage must be identical");
            Assert.That(r1.Stages.Count, Is.EqualTo(r2.Stages.Count), "Stage count must be identical across runs");
            Assert.That(r2.Stages.Count, Is.EqualTo(r3.Stages.Count), "Stage count must be identical across runs");
        }

        [Test]
        public async Task CheckEnvironmentReadiness_DeterministicAcrossThreeRuns()
        {
            // Arrange: provision fixtures to get consistent state
            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse
                {
                    Success = true,
                    Members = new List<IssuerTeamMember>
                    {
                        new IssuerTeamMember { Role = IssuerTeamRole.Admin, IsActive = true }
                    }
                });

            // Act
            ProtectedSignOffEnvironmentResponse r1 = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "determ-env-1" });
            ProtectedSignOffEnvironmentResponse r2 = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "determ-env-2" });
            ProtectedSignOffEnvironmentResponse r3 = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "determ-env-3" });

            // Assert: same status across all three runs
            Assert.That(r1.Status, Is.EqualTo(r2.Status), "Status must be identical across runs");
            Assert.That(r2.Status, Is.EqualTo(r3.Status), "Status must be identical across runs");
            Assert.That(r1.IsReadyForProtectedRun, Is.EqualTo(r2.IsReadyForProtectedRun));
            Assert.That(r2.IsReadyForProtectedRun, Is.EqualTo(r3.IsReadyForProtectedRun));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Configuration guard tests — fail-closed behavior for missing config
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CheckEnvironmentReadiness_MissingJwtSecretKey_ReturnsMisconfigured()
        {
            // Arrange: configuration missing JwtConfig:SecretKey
            IConfiguration missingJwtConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // JwtConfig:SecretKey intentionally absent
                    ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                })
                .Build();

            ProtectedSignOffEnvironmentService serviceWithBadConfig = new(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                missingJwtConfig,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);

            // Act
            ProtectedSignOffEnvironmentResponse result = await serviceWithBadConfig.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "config-guard-jwt-test" });

            // Assert: must be Misconfigured (not Ready, not Unavailable, not Degraded)
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Misconfigured),
                "Missing JwtConfig:SecretKey must cause Misconfigured status");
            Assert.That(result.IsReadyForProtectedRun, Is.False);
            Assert.That(result.ActionableGuidance, Is.Not.Null.And.Not.Empty,
                "Actionable guidance must be provided when misconfigured");

            // The configuration check must appear in the checks array
            EnvironmentCheck? configCheck = result.Checks
                .FirstOrDefault(c => c.Category == EnvironmentCheckCategory.Configuration);
            Assert.That(configCheck, Is.Not.Null, "Configuration check must be present in the checks array");
            Assert.That(configCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.CriticalFail));
            Assert.That(configCheck.Detail, Does.Contain("JwtConfig:SecretKey"),
                "Failure detail must identify the missing key");
            Assert.That(configCheck.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "Operator guidance must be provided for the configuration check failure");
        }

        [Test]
        public async Task CheckEnvironmentReadiness_MissingAppAccount_ReturnsMisconfigured()
        {
            // Arrange: configuration missing App:Account
            IConfiguration missingAccountConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtConfig:SecretKey"] = "ProtectedSignOffUnitTestSecretKey32CharsMin!!",
                    // App:Account intentionally absent
                })
                .Build();

            ProtectedSignOffEnvironmentService serviceWithBadConfig = new(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                missingAccountConfig,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);

            // Act
            ProtectedSignOffEnvironmentResponse result = await serviceWithBadConfig.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "config-guard-account-test" });

            // Assert
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Misconfigured),
                "Missing App:Account must cause Misconfigured status");
            Assert.That(result.IsReadyForProtectedRun, Is.False);

            EnvironmentCheck? configCheck = result.Checks
                .FirstOrDefault(c => c.Category == EnvironmentCheckCategory.Configuration);
            Assert.That(configCheck, Is.Not.Null);
            Assert.That(configCheck!.Detail, Does.Contain("App:Account"),
                "Failure detail must identify the missing App:Account key");
        }

        [Test]
        public async Task CheckEnvironmentReadiness_BothRequiredKeysMissing_ReturnsMisconfigured()
        {
            // Arrange: empty configuration
            IConfiguration emptyConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            ProtectedSignOffEnvironmentService serviceWithEmptyConfig = new(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                emptyConfig,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);

            // Act
            ProtectedSignOffEnvironmentResponse result = await serviceWithEmptyConfig.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "config-guard-empty-test" });

            // Assert: Misconfigured takes precedence over Unavailable / Degraded
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Misconfigured));
            Assert.That(result.IsReadyForProtectedRun, Is.False);

            EnvironmentCheck? configCheck = result.Checks
                .FirstOrDefault(c => c.Category == EnvironmentCheckCategory.Configuration);
            Assert.That(configCheck, Is.Not.Null);
            Assert.That(configCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.CriticalFail));
            // Both keys should appear in the detail
            Assert.That(configCheck.Detail, Does.Contain("JwtConfig:SecretKey"));
            Assert.That(configCheck.Detail, Does.Contain("App:Account"));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_AllRequiredConfigPresent_PassesConfigCheck()
        {
            // Arrange: valid configuration already set up by SetUp()
            // Act
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "config-guard-pass-test" });

            // Assert: configuration check passes
            EnvironmentCheck? configCheck = result.Checks
                .FirstOrDefault(c => c.Category == EnvironmentCheckCategory.Configuration);
            Assert.That(configCheck, Is.Not.Null, "Configuration check must always be present when IncludeConfigCheck=true");
            Assert.That(configCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "Configuration check must pass when all required keys are present");
            Assert.That(configCheck.Name, Is.EqualTo("RequiredConfigurationPresent"));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_WithIncludeConfigCheckFalse_SkipsConfigCategory()
        {
            // Arrange: empty config but config check disabled
            IConfiguration emptyConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            ProtectedSignOffEnvironmentService serviceWithEmptyConfig = new(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                emptyConfig,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);

            // Act: explicitly disable config check
            ProtectedSignOffEnvironmentResponse result = await serviceWithEmptyConfig.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "config-check-disabled",
                    IncludeConfigCheck = false
                });

            // Assert: no Configuration category check present
            Assert.That(result.Checks.Any(c => c.Category == EnvironmentCheckCategory.Configuration),
                Is.False, "Configuration category check must be absent when IncludeConfigCheck=false");
            // Status must NOT be Misconfigured when config check is disabled
            Assert.That(result.Status, Is.Not.EqualTo(ProtectedEnvironmentStatus.Misconfigured));
        }

        [Test]
        public async Task CheckEnvironmentReadiness_MisconfiguredStatus_PrecedesOtherFailures()
        {
            // Arrange: both missing config AND state machine failure
            IConfiguration emptyConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = false, Reason = "Blocked." });

            ProtectedSignOffEnvironmentService svc = new(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                emptyConfig,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);

            // Act
            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "precedence-test" });

            // Assert: Misconfigured takes precedence over all other statuses
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Misconfigured),
                "Misconfigured must take precedence over other failure types (Unavailable, Degraded)");
        }

        [Test]
        public async Task GetDiagnostics_MissingRequiredConfig_ReportsConfigurationFailure()
        {
            // Arrange: configuration missing both required keys
            IConfiguration emptyConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            ProtectedSignOffEnvironmentService svc = new(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                emptyConfig,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);

            // Act
            BiatecTokensApi.Models.ProtectedSignOff.ProtectedSignOffDiagnosticsResponse result =
                await svc.GetDiagnosticsAsync("config-diag-test");

            // Assert: configuration failure is explicitly reported
            Assert.That(result.FailureCategories.HasConfigurationFailure, Is.True,
                "Diagnostics must report HasConfigurationFailure when required keys are absent");
            Assert.That(result.IsOperational, Is.False,
                "IsOperational must be false when configuration is incomplete");

            // Configuration-related diagnostic notes must be present
            DiagnosticNote? jwtNote = result.Notes
                .FirstOrDefault(n => n.Category == "Configuration" && n.Note.Contains("JwtConfig:SecretKey"));
            DiagnosticNote? accountNote = result.Notes
                .FirstOrDefault(n => n.Category == "Configuration" && n.Note.Contains("App:Account"));

            Assert.That(jwtNote, Is.Not.Null, "Diagnostics must include a note about missing JwtConfig:SecretKey");
            Assert.That(jwtNote!.IsBlocking, Is.True);
            Assert.That(jwtNote.Remediation, Is.Not.Null.And.Not.Empty);

            Assert.That(accountNote, Is.Not.Null, "Diagnostics must include a note about missing App:Account");
            Assert.That(accountNote!.IsBlocking, Is.True);
        }

        [Test]
        public async Task GetDiagnostics_AllConfigPresent_ReportsNoConfigurationFailure()
        {
            // Arrange: valid configuration from SetUp()
            // Act
            BiatecTokensApi.Models.ProtectedSignOff.ProtectedSignOffDiagnosticsResponse result =
                await _service.GetDiagnosticsAsync("config-diag-pass-test");

            // Assert
            Assert.That(result.FailureCategories.HasConfigurationFailure, Is.False,
                "Diagnostics must not report HasConfigurationFailure when all required keys are present");
            Assert.That(result.ServiceAvailability, Is.Not.Null.And.Not.Empty);

            // JwtConfig:SecretKey and App:Account availability must be reported
            ServiceAvailabilityDiagnostic? jwtDiag = result.ServiceAvailability
                .FirstOrDefault(s => s.ServiceName == "JwtConfig:SecretKey");
            ServiceAvailabilityDiagnostic? accountDiag = result.ServiceAvailability
                .FirstOrDefault(s => s.ServiceName == "App:Account");

            Assert.That(jwtDiag, Is.Not.Null, "Diagnostics must include JwtConfig:SecretKey availability");
            Assert.That(jwtDiag!.IsAvailable, Is.True);
            Assert.That(accountDiag, Is.Not.Null, "Diagnostics must include App:Account availability");
            Assert.That(accountDiag!.IsAvailable, Is.True);
        }

        [Test]
        public async Task CheckEnvironmentReadiness_ConfigCheckIncludedByDefault()
        {
            // Act: use default request (IncludeConfigCheck defaults to true)
            ProtectedSignOffEnvironmentResponse result = await _service.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "default-config-check" });

            // Assert: configuration check is included by default
            Assert.That(result.Checks.Any(c => c.Category == EnvironmentCheckCategory.Configuration),
                Is.True, "Configuration check must be included by default");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — ProtectedSignOffController via HTTP
        // ═══════════════════════════════════════════════════════════════════════

        [TestFixture]
        [NonParallelizable]
        public class ProtectedSignOffIntegrationTests
        {
            private CustomWebApplicationFactory _factory = null!;
            private HttpClient _client = null!;
            private HttpClient _unauthClient = null!;

            [OneTimeSetUp]
            public async Task OneTimeSetUp()
            {
                _factory = new CustomWebApplicationFactory();
                _unauthClient = _factory.CreateClient();

                // Register and authenticate so protected endpoints accept requests
                string email = $"sign-off-test-{Guid.NewGuid():N}@biatec-test.example.com";
                object regReq = new
                {
                    Email = email,
                    Password = "SecurePass123!",
                    ConfirmPassword = "SecurePass123!",
                    FullName = "Protected Sign-Off Test User"
                };
                HttpResponseMessage regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
                JsonDocument? regBody = await regResp.Content.ReadFromJsonAsync<JsonDocument>();
                string jwtToken = regBody?.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;

                _client = _factory.CreateClient();
                _client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
            }

            [OneTimeTearDown]
            public void OneTimeTearDown()
            {
                _client?.Dispose();
                _unauthClient?.Dispose();
                _factory?.Dispose();
            }

            [Test]
            public async Task EnvironmentCheck_ReturnsOk_WithStructuredResponse()
            {
                // Arrange
                ProtectedSignOffEnvironmentRequest request = new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "integration-env-001",
                    IncludeFixtureCheck = true,
                    IncludeObservabilityCheck = true
                };

                // Act
                HttpResponseMessage response = await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/environment/check", request);
                string body = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Environment check should return 200 OK. Body: {body}");

                ProtectedSignOffEnvironmentResponse? result = JsonSerializer.Deserialize<ProtectedSignOffEnvironmentResponse>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.CheckId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Checks, Is.Not.Null.And.Not.Empty);
                Assert.That(result.TotalCheckCount, Is.GreaterThan(0));
            }

            [Test]
            public async Task LifecycleExecute_ReturnsOk_WithStructuredResponse()
            {
                // Arrange
                EnterpriseSignOffLifecycleRequest request = new EnterpriseSignOffLifecycleRequest
                {
                    CorrelationId = "integration-lifecycle-001"
                };

                // Act
                HttpResponseMessage response = await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/lifecycle/execute", request);
                string body = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Lifecycle execute should return 200 OK. Body: {body}");

                EnterpriseSignOffLifecycleResponse? result = JsonSerializer.Deserialize<EnterpriseSignOffLifecycleResponse>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.VerificationId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Stages, Is.Not.Null.And.Not.Empty);
            }

            [Test]
            public async Task FixtureProvision_ReturnsOk_WithStructuredResponse()
            {
                // Arrange
                EnterpriseFixtureProvisionRequest request = new EnterpriseFixtureProvisionRequest
                {
                    CorrelationId = "integration-fixtures-001"
                };

                // Act
                HttpResponseMessage response = await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/fixtures/provision", request);
                string body = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Fixture provision should return 200 OK. Body: {body}");

                EnterpriseFixtureProvisionResponse? result = JsonSerializer.Deserialize<EnterpriseFixtureProvisionResponse>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.ProvisioningId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.IssuerId, Is.Not.Null.And.Not.Empty);
            }

            [Test]
            public async Task Diagnostics_ReturnsOk_WithStructuredResponse()
            {
                // Act
                HttpResponseMessage response = await _client.GetAsync(
                    "/api/v1/protected-sign-off/diagnostics?correlationId=integration-diag-001");
                string body = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Diagnostics endpoint should return 200 OK. Body: {body}");

                ProtectedSignOffDiagnosticsResponse? result = JsonSerializer.Deserialize<ProtectedSignOffDiagnosticsResponse>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.DiagnosticsId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.ServiceAvailability, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Notes, Is.Not.Null);
            }

            [Test]
            public async Task EnvironmentCheck_WithNullBody_ReturnsBadRequest()
            {
                // Act
                HttpResponseMessage response = await _client.PostAsync(
                    "/api/v1/protected-sign-off/environment/check",
                    new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                    "Null body should return 400 Bad Request");
            }

            [Test]
            public async Task SwaggerDocumentIncludes_ProtectedSignOffEndpoints()
            {
                // Act
                HttpResponseMessage response = await _unauthClient.GetAsync("/swagger/v1/swagger.json");
                string swaggerJson = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Swagger endpoint should return 200. Body snippet: {swaggerJson[..Math.Min(500, swaggerJson.Length)]}");
                Assert.That(swaggerJson, Does.Contain("/api/v1/protected-sign-off"),
                    "Swagger JSON should include the protected sign-off endpoints");
            }

            [Test]
            public async Task FixtureProvision_IsIdempotent_WhenCalledTwice()
            {
                // Arrange
                EnterpriseFixtureProvisionRequest request = new EnterpriseFixtureProvisionRequest
                {
                    CorrelationId = "idempotency-test-001",
                    ResetIfExists = false
                };

                // Act: call twice
                HttpResponseMessage r1 = await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/fixtures/provision", request);
                HttpResponseMessage r2 = await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/fixtures/provision", request);

                // Assert: both return 200
                Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

                EnterpriseFixtureProvisionResponse? result2 = await r2.Content.ReadFromJsonAsync<EnterpriseFixtureProvisionResponse>();
                Assert.That(result2, Is.Not.Null);
                Assert.That(result2!.IsProvisioned, Is.True);
                // Second call should see already-provisioned fixtures
                Assert.That(result2.WasAlreadyProvisioned, Is.True,
                    "Second provision call should detect existing fixtures and skip re-provisioning");
            }

            [Test]
            public async Task LifecycleExecute_DeterministicAcrossThreeRuns()
            {
                // Arrange
                EnterpriseSignOffLifecycleRequest request = new EnterpriseSignOffLifecycleRequest
                {
                    CorrelationId = "determ-integration-run"
                };

                // Act
                EnterpriseSignOffLifecycleResponse? r1 = await (await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/lifecycle/execute", request))
                    .Content.ReadFromJsonAsync<EnterpriseSignOffLifecycleResponse>();
                EnterpriseSignOffLifecycleResponse? r2 = await (await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/lifecycle/execute", request))
                    .Content.ReadFromJsonAsync<EnterpriseSignOffLifecycleResponse>();
                EnterpriseSignOffLifecycleResponse? r3 = await (await _client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/lifecycle/execute", request))
                    .Content.ReadFromJsonAsync<EnterpriseSignOffLifecycleResponse>();

                // Assert: identical outcomes across runs
                Assert.That(r1!.IsLifecycleVerified, Is.EqualTo(r2!.IsLifecycleVerified));
                Assert.That(r2.IsLifecycleVerified, Is.EqualTo(r3!.IsLifecycleVerified));
                Assert.That(r1.Stages.Count, Is.EqualTo(r2.Stages.Count));
                Assert.That(r2.Stages.Count, Is.EqualTo(r3.Stages.Count));
            }

            [Test]
            public async Task UnauthenticatedRequest_EnvironmentCheck_ReturnsUnauthorized()
            {
                // Act
                HttpResponseMessage response = await _unauthClient.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/environment/check",
                    new ProtectedSignOffEnvironmentRequest());

                // Assert: endpoint is protected
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    "Unauthenticated requests to protected sign-off endpoints must return 401");
            }

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
                            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForProtectedSignOffIntegration32Chars",
                            ["JwtConfig:SecretKey"] = "ProtectedSignOffTestSecretKey32CharsRequired!!",
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
}

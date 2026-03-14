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
    /// Tests for the workflow governance check added to
    /// <see cref="ProtectedSignOffEnvironmentService.CheckEnvironmentReadinessAsync"/>.
    ///
    /// Coverage:
    ///
    /// AC1: Governance check passes when WorkflowGovernanceConfig:Enabled is true (default).
    /// AC2: Governance check produces DegradedFail when governance is explicitly disabled.
    /// AC3: Governance check passes when WorkflowGovernanceConfig key is absent (safe default).
    /// AC4: Environment status reflects governance degradation correctly.
    /// AC5: Policy version is reported in the governance check detail.
    /// AC6: Governance check category is Configuration.
    /// AC7: All existing checks remain unaffected by the new governance check.
    /// AC8: Governance check is not required (IsRequired = false) — degradation does not block run.
    /// </summary>
    [TestFixture]
    public class ProtectedSignOffWorkflowGovernanceTests
    {
        private Mock<IIssuerWorkflowService> _issuerWorkflowMock = null!;
        private Mock<IDeploymentSignOffService> _signOffServiceMock = null!;
        private Mock<IBackendDeploymentLifecycleContractService> _contractServiceMock = null!;

        /// <summary>
        /// Builds a minimal IConfiguration containing required keys plus optional governance keys.
        /// </summary>
        private static IConfiguration BuildConfiguration(
            string? governanceEnabled = null,
            string? policyVersion = null)
        {
            var dict = new Dictionary<string, string?>
            {
                ["JwtConfig:SecretKey"] = "GovernanceTestSecretKey32CharsMinimum!!",
                ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            };

            if (governanceEnabled != null)
                dict["WorkflowGovernanceConfig:Enabled"] = governanceEnabled;

            if (policyVersion != null)
                dict["WorkflowGovernanceConfig:PolicyVersion"] = policyVersion;

            return new ConfigurationBuilder()
                .AddInMemoryCollection(dict)
                .Build();
        }

        /// <summary>
        /// Creates a service instance using the supplied configuration.
        /// </summary>
        private ProtectedSignOffEnvironmentService CreateService(IConfiguration configuration)
        {
            return new ProtectedSignOffEnvironmentService(
                _issuerWorkflowMock.Object,
                _signOffServiceMock.Object,
                _contractServiceMock.Object,
                configuration,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance);
        }

        [SetUp]
        public void SetUp()
        {
            _issuerWorkflowMock = new Mock<IIssuerWorkflowService>();
            _signOffServiceMock = new Mock<IDeploymentSignOffService>();
            _contractServiceMock = new Mock<IBackendDeploymentLifecycleContractService>();

            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new IssuerTeamMembersResponse { Success = true, Members = new List<IssuerTeamMember>() });

            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(WorkflowApprovalState.Prepared, WorkflowApprovalState.PendingReview))
                .Returns(new WorkflowTransitionValidationResult { IsValid = true, Reason = "Valid." });

            _contractServiceMock
                .Setup(s => s.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new BiatecTokensApi.Models.BackendDeploymentLifecycle.BackendDeploymentContractResponse
                {
                    DeploymentId = "sign-off-fixture-deployment-001",
                    ErrorCode = BiatecTokensApi.Models.BackendDeploymentLifecycle.DeploymentErrorCode.RequiredFieldMissing,
                    Message = "Deployment not found."
                });

            _signOffServiceMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync(new DeploymentSignOffProof
                {
                    ProofId = "proof-governance-test",
                    DeploymentId = "sign-off-fixture-deployment-001",
                    CorrelationId = "governance-test",
                    Verdict = SignOffVerdict.Blocked,
                    IsReadyForSignOff = false,
                    GeneratedAt = DateTime.UtcNow.ToString("O")
                });
        }

        // ── AC1: Governance enabled (explicit true) ────────────────────────────

        [Test]
        public async Task GovernanceCheck_WhenExplicitlyEnabled_IsPass()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "true");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-test-enabled",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null, "WorkflowGovernanceEnabled check must be present");
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "Governance check must pass when governance is explicitly enabled");
            Assert.That(govCheck.Category, Is.EqualTo(EnvironmentCheckCategory.Configuration),
                "Governance check must be in Configuration category");
        }

        // ── AC2: Governance explicitly disabled → DegradedFail ────────────────

        [Test]
        public async Task GovernanceCheck_WhenExplicitlyDisabled_IsDegradedFail()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "false");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-test-disabled",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null, "WorkflowGovernanceEnabled check must be present");
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail),
                "Governance check must be DegradedFail when governance is explicitly disabled");
        }

        // ── AC3: Missing governance key → Pass (safe default) ─────────────────

        [Test]
        public async Task GovernanceCheck_WhenKeyAbsent_IsPass()
        {
            // Arrange: no WorkflowGovernanceConfig:Enabled key in config
            IConfiguration config = BuildConfiguration(governanceEnabled: null);
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-test-absent",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null, "WorkflowGovernanceEnabled check must be present");
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "Governance check must pass when key is absent (defaults to enabled)");
        }

        // ── AC4: Status reflects governance degradation ────────────────────────

        [Test]
        public async Task EnvironmentStatus_WhenGovernanceDisabled_IsDegraded()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "false");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-status-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: governance disabled causes Degraded status (not Misconfigured, not Unavailable)
            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Degraded),
                "Status must be Degraded when governance is disabled");
            Assert.That(result.IsReadyForProtectedRun, Is.False,
                "IsReadyForProtectedRun must be false when governance is disabled");
            Assert.That(result.DegradedFailCount, Is.GreaterThanOrEqualTo(1),
                "DegradedFailCount must be at least 1 when governance check fails");
        }

        // ── AC5: Policy version appears in governance check detail ─────────────

        [Test]
        public async Task GovernanceCheck_IncludesPolicyVersion_WhenEnabled()
        {
            // Arrange
            const string policyVer = "2.0.0";
            IConfiguration config = BuildConfiguration(governanceEnabled: "true", policyVersion: policyVer);
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-policy-ver-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass));
            Assert.That(govCheck.Detail, Does.Contain(policyVer),
                "Policy version must appear in governance check detail");
        }

        [Test]
        public async Task GovernanceCheck_DefaultsPolicyVersion_WhenKeyAbsent()
        {
            // Arrange: no policy version key
            IConfiguration config = BuildConfiguration(governanceEnabled: "true", policyVersion: null);
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-default-ver-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: defaults to "1.0.0"
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.Detail, Does.Contain("1.0.0"),
                "Default policy version 1.0.0 must appear when key is absent");
        }

        // ── AC6: Governance check category is Configuration ────────────────────

        [Test]
        public async Task GovernanceCheck_Category_IsConfiguration()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "false");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-cat-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.Category, Is.EqualTo(EnvironmentCheckCategory.Configuration),
                "Governance check must always be in Configuration category");
        }

        // ── AC7: Existing required checks are unaffected ───────────────────────

        [Test]
        public async Task ExistingChecks_ArePresent_WhenGovernanceCheckAdded()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "true");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-existing-checks-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: existing checks still present
            Assert.That(result.Checks.Any(c => c.Name == "RequiredConfigurationPresent"),
                Is.True, "RequiredConfigurationPresent check must still be present");
            Assert.That(result.Checks.Any(c => c.Name == "AuthServiceAvailable"),
                Is.True, "AuthServiceAvailable check must still be present");
            Assert.That(result.Checks.Any(c => c.Name == "IssuerWorkflowServiceAvailable"),
                Is.True, "IssuerWorkflowServiceAvailable check must still be present");
            Assert.That(result.Checks.Any(c => c.Name == "DeploymentContractServiceAvailable"),
                Is.True, "DeploymentContractServiceAvailable check must still be present");
            Assert.That(result.Checks.Any(c => c.Name == "SignOffValidationServiceAvailable"),
                Is.True, "SignOffValidationServiceAvailable check must still be present");
            Assert.That(result.Checks.Any(c => c.Name == "WorkflowGovernanceEnabled"),
                Is.True, "WorkflowGovernanceEnabled check must be present");
        }

        // ── AC8: IsRequired = false — degradation does not block run ───────────

        [Test]
        public async Task GovernanceCheck_IsNotRequired_SoItDoesNotIncreaseCriticalFail()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "false");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-required-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: governance failure does not raise critical fail count
            Assert.That(result.CriticalFailCount, Is.EqualTo(0),
                "CriticalFailCount must remain 0 when only the non-required governance check fails");
            Assert.That(result.DegradedFailCount, Is.GreaterThanOrEqualTo(1),
                "DegradedFailCount must increase when governance check fails");
        }

        // ── Governance check skipped when IncludeConfigCheck = false ──────────

        [Test]
        public async Task GovernanceCheck_IsAbsent_WhenIncludeConfigCheckFalse()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "false");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-skip-test",
                IncludeConfigCheck = false,  // governance check only runs if config check is included
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: governance check is not included
            bool hasGovCheck = result.Checks.Any(c => c.Name == "WorkflowGovernanceEnabled");
            Assert.That(hasGovCheck, Is.False,
                "Governance check must not be included when IncludeConfigCheck is false");
        }

        // ── Governance enabled with case-insensitive match ──────────────────

        [Test]
        [TestCase("True")]
        [TestCase("TRUE")]
        [TestCase("true")]
        public async Task GovernanceCheck_CaseInsensitiveEnabled_IsPass(string enabledValue)
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: enabledValue);
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-case-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                $"Governance check must pass for enabled value '{enabledValue}'");
        }

        // ── TotalCheckCount stays consistent ──────────────────────────────────

        [Test]
        public async Task TotalCheckCount_MatchesChecksCount_WithGovernanceCheck()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "true");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-count-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: TotalCheckCount is always consistent with Checks list
            Assert.That(result.TotalCheckCount, Is.EqualTo(result.Checks.Count),
                "TotalCheckCount must always equal Checks.Count");
            Assert.That(result.TotalCheckCount, Is.GreaterThan(0));
        }

        // ── Governance disabled → guidance mentions governance ─────────────────

        [Test]
        public async Task GovernanceCheck_WhenDisabled_GuidanceMentionsGovernance()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "false");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-guidance-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse result = await service.CheckEnvironmentReadinessAsync(request);

            // Assert
            EnvironmentCheck? govCheck = result.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "OperatorGuidance must be provided when governance check fails");
            Assert.That(govCheck.OperatorGuidance, Does.Contain("WorkflowGovernanceConfig:Enabled"),
                "OperatorGuidance must mention the config key to fix");
        }

        // ── Determinism: same config produces same result across 3 runs ────────

        [Test]
        public async Task GovernanceCheck_IsDeterministic_AcrossMultipleRuns()
        {
            // Arrange
            IConfiguration config = BuildConfiguration(governanceEnabled: "true", policyVersion: "1.0.0");
            ProtectedSignOffEnvironmentService service = CreateService(config);
            var request = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "gov-determinism-test",
                IncludeConfigCheck = true,
                IncludeFixtureCheck = false,
                IncludeObservabilityCheck = false
            };

            // Act
            ProtectedSignOffEnvironmentResponse r1 = await service.CheckEnvironmentReadinessAsync(request);
            ProtectedSignOffEnvironmentResponse r2 = await service.CheckEnvironmentReadinessAsync(request);
            ProtectedSignOffEnvironmentResponse r3 = await service.CheckEnvironmentReadinessAsync(request);

            // Assert: identical governance outcomes across runs
            EnvironmentCheck? gov1 = r1.Checks.FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");
            EnvironmentCheck? gov2 = r2.Checks.FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");
            EnvironmentCheck? gov3 = r3.Checks.FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(gov1!.Outcome, Is.EqualTo(gov2!.Outcome));
            Assert.That(gov2.Outcome, Is.EqualTo(gov3!.Outcome));
            Assert.That(gov1.Category, Is.EqualTo(gov2.Category));
            Assert.That(gov2.Category, Is.EqualTo(gov3.Category));
        }
    }
}

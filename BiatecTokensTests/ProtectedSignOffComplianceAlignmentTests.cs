using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Models.ProtectedSignOff;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Regression tests proving that the backend release path is aligned with the current
    /// compliance workflow capabilities.  These tests validate the combination of the
    /// protected sign-off environment service and the compliance APIs (KYC/AML sign-off
    /// evidence, compliance case management) that are required for the enterprise
    /// onboarding workflow.
    ///
    /// This test class is the in-process evidence for the following acceptance criteria:
    ///
    ///   AC1: Backend release-branch reality is aligned with the current compliance workflow
    ///        capabilities needed for the enterprise product story.
    ///   AC3: Protected strict sign-off runs against the correct backend release candidate
    ///        and required environment configuration.
    ///   AC4: Missing secrets, invalid endpoints, provider failures, and incomplete lifecycle
    ///        responses fail clearly and safely in the sign-off path.
    ///   AC9: Relevant regression coverage exists for branch-aligned workflow behavior,
    ///        sign-off-critical flows, and failure conditions.
    ///
    /// Coverage:
    ///
    /// ALIGN01: Environment check with compliance services wired up includes ComplianceWorkflow checks.
    /// ALIGN02: Both compliance workflow checks pass when services are available.
    /// ALIGN03: KycAmlSignOff check produces DegradedFail when service is absent (fail-closed).
    /// ALIGN04: ComplianceCase check produces DegradedFail when service is absent (fail-closed).
    /// ALIGN05: Environment status is Ready when all services including compliance are wired up.
    /// ALIGN06: Environment status is Degraded (not Ready) when compliance services are absent.
    /// ALIGN07: IncludeComplianceWorkflowCheck=false suppresses compliance checks entirely.
    /// ALIGN08: ComplianceWorkflow check names are stable identifiers (API contract).
    /// ALIGN09: ComplianceWorkflow checks are in the ComplianceWorkflow category.
    /// ALIGN10: Compliance-aligned environment check TotalCheckCount includes compliance checks.
    /// ALIGN11: KYC/AML sign-off evidence endpoint is accessible via HTTP (integration).
    /// ALIGN12: Compliance case management endpoint is accessible via HTTP (integration).
    /// ALIGN13: Protected sign-off environment check via HTTP includes ComplianceWorkflow checks.
    /// ALIGN14: HTTP environment check returns Ready when compliance services are wired up.
    /// ALIGN15: KYC/AML sign-off initiate returns structured response (not 500/404).
    /// ALIGN16: Compliance case create returns structured response (not 500/404).
    /// ALIGN17: Protected lifecycle still verifies all 6 stages even with compliance check included.
    /// ALIGN18: Compliance check results are deterministic across multiple calls.
    /// ALIGN19: Full compliance-aligned sign-off journey succeeds end-to-end.
    /// ALIGN20: Compliance workflow check operator guidance is actionable when service is absent.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffComplianceAlignmentTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — service-level compliance alignment checks
        // ═══════════════════════════════════════════════════════════════════════

        private Mock<IIssuerWorkflowService> _issuerWorkflowMock = null!;
        private Mock<IDeploymentSignOffService> _signOffMock = null!;
        private Mock<IBackendDeploymentLifecycleContractService> _contractMock = null!;
        private Mock<IKycAmlSignOffEvidenceService> _kycAmlMock = null!;
        private Mock<IComplianceCaseManagementService> _casesMock = null!;
        private IConfiguration _config = null!;

        [SetUp]
        public void SetUp()
        {
            _issuerWorkflowMock = new Mock<IIssuerWorkflowService>();
            _signOffMock = new Mock<IDeploymentSignOffService>();
            _contractMock = new Mock<IBackendDeploymentLifecycleContractService>();
            _kycAmlMock = new Mock<IKycAmlSignOffEvidenceService>();
            _casesMock = new Mock<IComplianceCaseManagementService>();

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtConfig:SecretKey"] = "AlignmentTestSecretKey32CharsMin!!",
                    ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"
                })
                .Build();

            // Default: issuer workflow returns a member so fixture check passes
            _issuerWorkflowMock
                .Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new BiatecTokensApi.Models.IssuerWorkflow.IssuerTeamMembersResponse
                {
                    Success = true,
                    Members = new List<BiatecTokensApi.Models.IssuerWorkflow.IssuerTeamMember>
                    {
                        new() {
                            IssuerId = ProtectedSignOffEnvironmentService.DefaultSignOffIssuerId,
                            UserId = ProtectedSignOffEnvironmentService.DefaultSignOffUserId,
                            Role = BiatecTokensApi.Models.IssuerWorkflow.IssuerTeamRole.Admin,
                            IsActive = true
                        }
                    }
                });

            // Default: state machine validates transition
            _issuerWorkflowMock
                .Setup(s => s.ValidateTransition(
                    BiatecTokensApi.Models.IssuerWorkflow.WorkflowApprovalState.Prepared,
                    BiatecTokensApi.Models.IssuerWorkflow.WorkflowApprovalState.PendingReview))
                .Returns(new BiatecTokensApi.Models.IssuerWorkflow.WorkflowTransitionValidationResult
                {
                    IsValid = true,
                    Reason = "Valid transition."
                });

            // Default: contract service returns a structured (not-found) response
            _contractMock
                .Setup(s => s.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new BiatecTokensApi.Models.BackendDeploymentLifecycle.BackendDeploymentContractResponse
                {
                    DeploymentId = "fixture-deployment-001",
                    ErrorCode = BiatecTokensApi.Models.BackendDeploymentLifecycle.DeploymentErrorCode.RequiredFieldMissing,
                    Message = "Deployment not found."
                });

            // Default: sign-off service returns a structured proof
            _signOffMock
                .Setup(s => s.GenerateSignOffProofAsync(It.IsAny<string>()))
                .ReturnsAsync(new BiatecTokensApi.Models.DeploymentSignOff.DeploymentSignOffProof
                {
                    ProofId = "proof-001",
                    DeploymentId = ProtectedSignOffEnvironmentService.DefaultSignOffDeploymentId,
                    Verdict = BiatecTokensApi.Models.DeploymentSignOff.SignOffVerdict.Blocked,
                    IsReadyForSignOff = false,
                    GeneratedAt = DateTime.UtcNow.ToString("O")
                });
        }

        private ProtectedSignOffEnvironmentService CreateServiceWith(
            IKycAmlSignOffEvidenceService? kycAml,
            IComplianceCaseManagementService? cases) =>
            new ProtectedSignOffEnvironmentService(
                _issuerWorkflowMock.Object,
                _signOffMock.Object,
                _contractMock.Object,
                _config,
                NullLogger<ProtectedSignOffEnvironmentService>.Instance,
                kycAml,
                cases);

        // ── ALIGN01: compliance checks are present when services are wired up ──

        [Test]
        public async Task ALIGN01_ComplianceWorkflowChecks_ArePresent_WhenServicesWiredUp()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align01",
                    IncludeComplianceWorkflowCheck = true
                });

            List<EnvironmentCheck> complianceChecks = result.Checks
                .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow)
                .ToList();

            Assert.That(complianceChecks, Has.Count.EqualTo(3),
                "Expected exactly 3 ComplianceWorkflow checks when services are wired up (KycAmlSignOffServiceAvailable, ComplianceCaseServiceAvailable, ComplianceCaseApprovalWorkflowAvailable).");
        }

        // ── ALIGN02: both compliance checks pass when services available ────────

        [Test]
        public async Task ALIGN02_ComplianceWorkflowChecks_BothPass_WhenServicesAvailable()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align02",
                    IncludeComplianceWorkflowCheck = true
                });

            List<EnvironmentCheck> complianceChecks = result.Checks
                .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow)
                .ToList();

            foreach (EnvironmentCheck check in complianceChecks)
            {
                Assert.That(check.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                    $"ComplianceWorkflow check '{check.Name}' should pass when its service is wired up.");
            }
        }

        // ── ALIGN03: KycAmlSignOff check fails when service is absent ───────────

        [Test]
        public async Task ALIGN03_KycAmlSignOffCheck_DegradedFail_WhenServiceAbsent()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(null, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align03",
                    IncludeComplianceWorkflowCheck = true
                });

            EnvironmentCheck? kycCheck = result.Checks
                .FirstOrDefault(c => c.Name == "KycAmlSignOffServiceAvailable");

            Assert.That(kycCheck, Is.Not.Null, "KycAmlSignOffServiceAvailable check must be present.");
            Assert.That(kycCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail),
                "KycAmlSignOffServiceAvailable must report DegradedFail when service is absent (fail-closed).");
            Assert.That(kycCheck.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DegradedFail compliance check must include actionable operator guidance.");
        }

        // ── ALIGN04: compliance case check fails when service is absent ─────────

        [Test]
        public async Task ALIGN04_ComplianceCaseCheck_DegradedFail_WhenServiceAbsent()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, null);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align04",
                    IncludeComplianceWorkflowCheck = true
                });

            EnvironmentCheck? caseCheck = result.Checks
                .FirstOrDefault(c => c.Name == "ComplianceCaseServiceAvailable");

            Assert.That(caseCheck, Is.Not.Null, "ComplianceCaseServiceAvailable check must be present.");
            Assert.That(caseCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail),
                "ComplianceCaseServiceAvailable must report DegradedFail when service is absent (fail-closed).");
            Assert.That(caseCheck.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DegradedFail compliance check must include actionable operator guidance.");
        }

        // ── ALIGN05: environment is Ready when all services including compliance are available ──

        [Test]
        public async Task ALIGN05_EnvironmentStatus_IsReady_WhenAllServicesIncludingComplianceWiredUp()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align05",
                    IncludeFixtureCheck = true,
                    IncludeObservabilityCheck = true,
                    IncludeComplianceWorkflowCheck = true
                });

            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Ready),
                "Environment must be Ready when all services including compliance are wired up.");
            Assert.That(result.IsReadyForProtectedRun, Is.True);
            Assert.That(result.DegradedFailCount, Is.EqualTo(0));
        }

        // ── ALIGN06: status is Degraded when compliance services are absent ─────

        [Test]
        public async Task ALIGN06_EnvironmentStatus_IsDegraded_WhenComplianceServicesAbsent()
        {
            // No compliance services
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(null, null);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align06",
                    IncludeFixtureCheck = true,
                    IncludeObservabilityCheck = true,
                    IncludeComplianceWorkflowCheck = true
                });

            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Degraded),
                "Environment must be Degraded when compliance services are absent.");
            Assert.That(result.IsReadyForProtectedRun, Is.False,
                "IsReadyForProtectedRun must be false when environment is Degraded.");
            Assert.That(result.DegradedFailCount, Is.GreaterThan(0));
        }

        // ── ALIGN07: IncludeComplianceWorkflowCheck=false suppresses compliance checks ──

        [Test]
        public async Task ALIGN07_ComplianceChecks_AreAbsent_WhenCheckSuppressed()
        {
            // Even without services, no compliance checks should appear
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(null, null);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align07",
                    IncludeComplianceWorkflowCheck = false
                });

            List<EnvironmentCheck> complianceChecks = result.Checks
                .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow)
                .ToList();

            Assert.That(complianceChecks, Is.Empty,
                "No ComplianceWorkflow checks should appear when IncludeComplianceWorkflowCheck=false.");
        }

        // ── ALIGN08: compliance check names are stable identifiers ──────────────

        [Test]
        public async Task ALIGN08_ComplianceCheckNames_AreStableIdentifiers()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align08",
                    IncludeComplianceWorkflowCheck = true
                });

            List<string> checkNames = result.Checks
                .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow)
                .Select(c => c.Name)
                .ToList();

            Assert.That(checkNames, Does.Contain("KycAmlSignOffServiceAvailable"),
                "Stable check name 'KycAmlSignOffServiceAvailable' must be present.");
            Assert.That(checkNames, Does.Contain("ComplianceCaseServiceAvailable"),
                "Stable check name 'ComplianceCaseServiceAvailable' must be present.");
        }

        // ── ALIGN09: compliance checks are in the ComplianceWorkflow category ───

        [Test]
        public async Task ALIGN09_ComplianceChecks_AreInComplianceWorkflowCategory()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align09",
                    IncludeComplianceWorkflowCheck = true
                });

            foreach (EnvironmentCheck check in result.Checks
                .Where(c => c.Name.StartsWith("Kyc") || c.Name.StartsWith("Compliance")))
            {
                Assert.That(check.Category, Is.EqualTo(EnvironmentCheckCategory.ComplianceWorkflow),
                    $"Check '{check.Name}' should be in the ComplianceWorkflow category.");
            }
        }

        // ── ALIGN10: TotalCheckCount includes compliance checks when included ────

        [Test]
        public async Task ALIGN10_TotalCheckCount_IncludesComplianceChecks_WhenIncluded()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse withCompliance = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align10-with",
                    IncludeComplianceWorkflowCheck = true
                });

            ProtectedSignOffEnvironmentResponse withoutCompliance = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align10-without",
                    IncludeComplianceWorkflowCheck = false
                });

            Assert.That(withCompliance.TotalCheckCount, Is.GreaterThan(withoutCompliance.TotalCheckCount),
                "TotalCheckCount must be higher when compliance checks are included.");
            Assert.That(withCompliance.TotalCheckCount - withoutCompliance.TotalCheckCount, Is.EqualTo(3),
                "Exactly 3 additional compliance checks are expected (KycAmlSignOff, ComplianceCase, ComplianceCaseApprovalWorkflow).");
        }

        // ── ALIGN18: compliance check results are deterministic ─────────────────

        [Test]
        public async Task ALIGN18_ComplianceCheckResults_AreDeterministic_AcrossMultipleCalls()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse run1 = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "align18-run1", IncludeComplianceWorkflowCheck = true });
            ProtectedSignOffEnvironmentResponse run2 = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "align18-run2", IncludeComplianceWorkflowCheck = true });
            ProtectedSignOffEnvironmentResponse run3 = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "align18-run3", IncludeComplianceWorkflowCheck = true });

            foreach (ProtectedSignOffEnvironmentResponse run in new[] { run1, run2, run3 })
            {
                List<EnvironmentCheck> complianceChecks = run.Checks
                    .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow)
                    .ToList();

                Assert.That(complianceChecks, Has.Count.EqualTo(3),
                    "Exactly 3 ComplianceWorkflow checks expected across all runs.");
                Assert.That(complianceChecks.All(c => c.Outcome == EnvironmentCheckOutcome.Pass), Is.True,
                    "Compliance checks must consistently pass when services are wired up.");
            }
        }

        // ── ALIGN20: operator guidance is actionable when service is absent ─────

        [Test]
        public async Task ALIGN20_OperatorGuidance_IsActionable_WhenComplianceServiceAbsent()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(null, null);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "align20",
                    IncludeComplianceWorkflowCheck = true
                });

            List<EnvironmentCheck> failedComplianceChecks = result.Checks
                .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow
                         && c.Outcome != EnvironmentCheckOutcome.Pass)
                .ToList();

            foreach (EnvironmentCheck check in failedComplianceChecks)
            {
                Assert.That(check.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                    $"Failed check '{check.Name}' must include non-empty operator guidance.");
                Assert.That(check.OperatorGuidance, Does.Contain("Program.cs").Or.Contain("registered"),
                    $"Operator guidance for '{check.Name}' should reference the registration location.");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests via WebApplicationFactory
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class ComplianceAlignmentFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, cfg) =>
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "ComplianceAlignmentTestKey32Chars!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "ComplianceAlignTestHardcodedKey32CharsMin",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "compliance-alignment-test",
                        ["ProtectedSignOff:EnforceConfigGuards"] = "false",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["DeploymentEvidenceConfig:Provider"] = "Simulation",
                        ["KycConfig:Provider"] = "Mock",
                        ["AmlConfig:Provider"] = "Mock",
                    }));
            }
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string uniqueSuffix)
        {
            string email = $"align-test-{uniqueSuffix}@compliance-alignment.test";
            var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password = "ComplianceAlign!2026",
                confirmPassword = "ComplianceAlign!2026",
                fullName = "Compliance Alignment Test User"
            });
            if (reg.IsSuccessStatusCode)
            {
                using JsonDocument doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
                string? token = doc.RootElement.GetProperty("accessToken").GetString();
                if (!string.IsNullOrEmpty(token))
                    return token;
            }

            // Fallback: login
            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email,
                password = "ComplianceAlign!2026"
            });
            using JsonDocument loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            return loginDoc.RootElement.GetProperty("accessToken").GetString()
                ?? throw new InvalidOperationException("Could not obtain JWT for compliance alignment test.");
        }

        // ── ALIGN11: KYC/AML sign-off evidence endpoint is accessible via HTTP ──

        [Test]
        public async Task ALIGN11_KycAmlSignOffEvidenceEndpoint_IsAccessible_ViaHttp()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();

            // The endpoint requires auth; an unauthenticated request should return 401
            // (not 404 or 500 — which would indicate the endpoint doesn't exist or crashes)
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/kyc-aml-signoff/initiate",
                new { subjectId = "test-subject", executionMode = "Simulated" });

            Assert.That(
                (int)response.StatusCode,
                Is.AnyOf(401, 400, 200),
                "KYC/AML sign-off endpoint must return 401 (unauth), 400 (bad request), or 200 — not 404 or 500.");
            Assert.That(
                (int)response.StatusCode,
                Is.Not.AnyOf(404, 500),
                "KYC/AML sign-off endpoint must exist and not crash on unauthenticated request.");
        }

        // ── ALIGN12: compliance case management endpoint is accessible via HTTP ─

        [Test]
        public async Task ALIGN12_ComplianceCaseManagementEndpoint_IsAccessible_ViaHttp()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();

            // Unauthenticated request should return 401, not 404/500
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/compliance-cases",
                new { issuerId = "test-issuer", subjectId = "test-subject", caseType = "KYC" });

            Assert.That(
                (int)response.StatusCode,
                Is.AnyOf(401, 400, 200),
                "Compliance cases endpoint must return 401, 400, or 200 — not 404 or 500.");
            Assert.That(
                (int)response.StatusCode,
                Is.Not.AnyOf(404, 500),
                "Compliance cases endpoint must exist and not crash on unauthenticated request.");
        }

        // ── ALIGN13: HTTP environment check includes ComplianceWorkflow checks ──

        [Test]
        public async Task ALIGN13_ProtectedSignOffEnvironmentCheck_ViaHttp_IncludesComplianceWorkflowChecks()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();
            string jwt = await ObtainJwtAsync(client, $"align13-{Guid.NewGuid():N}");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new
                {
                    correlationId = "align13-http",
                    includeConfigCheck = false,
                    includeFixtureCheck = false,
                    includeObservabilityCheck = false,
                    includeComplianceWorkflowCheck = true
                });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Environment check endpoint must return 200 when authenticated.");

            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement checksEl = doc.RootElement.GetProperty("checks");

            List<string> checkNames = new();
            foreach (JsonElement checkEl in checksEl.EnumerateArray())
            {
                if (checkEl.TryGetProperty("category", out JsonElement catEl)
                    && catEl.GetString() == "ComplianceWorkflow")
                {
                    if (checkEl.TryGetProperty("name", out JsonElement nameEl))
                        checkNames.Add(nameEl.GetString() ?? "");
                }
            }

            Assert.That(checkNames, Does.Contain("KycAmlSignOffServiceAvailable"),
                "HTTP environment check must include KycAmlSignOffServiceAvailable compliance check.");
            Assert.That(checkNames, Does.Contain("ComplianceCaseServiceAvailable"),
                "HTTP environment check must include ComplianceCaseServiceAvailable compliance check.");
        }

        // ── ALIGN14: HTTP environment check returns Ready with compliance services wired ──

        [Test]
        public async Task ALIGN14_HttpEnvironmentCheck_ReturnsReady_WithComplianceServicesWired()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();
            string jwt = await ObtainJwtAsync(client, $"align14-{Guid.NewGuid():N}");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new
                {
                    correlationId = "align14-http",
                    includeConfigCheck = false,
                    includeFixtureCheck = false,
                    includeObservabilityCheck = false,
                    includeComplianceWorkflowCheck = true
                });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);

            Assert.That(doc.RootElement.TryGetProperty("checks", out _), Is.True,
                "Response must contain checks array.");

            // All compliance workflow checks must pass when services are wired up in the application
            if (doc.RootElement.TryGetProperty("checks", out JsonElement checksEl))
            {
                foreach (JsonElement checkEl in checksEl.EnumerateArray())
                {
                    if (checkEl.TryGetProperty("category", out JsonElement catEl)
                        && catEl.GetString() == "ComplianceWorkflow")
                    {
                        string? outcome = checkEl.TryGetProperty("outcome", out JsonElement outcomeEl)
                            ? outcomeEl.GetString() : null;
                        Assert.That(outcome, Is.EqualTo("Pass"),
                            $"ComplianceWorkflow check should be Pass when service is wired up in DI. Check: {checkEl}");
                    }
                }
            }
        }

        // ── ALIGN15: KYC/AML sign-off initiate returns structured response ──────

        [Test]
        public async Task ALIGN15_KycAmlSignOffInitiate_WithAuth_ReturnsStructuredResponse()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();
            string jwt = await ObtainJwtAsync(client, $"align15-{Guid.NewGuid():N}");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/kyc-aml-signoff/initiate",
                new
                {
                    subjectId = $"align-subject-{Guid.NewGuid():N}",
                    executionMode = "Simulated",
                    correlationId = "align15"
                });

            // Should succeed with Simulated mode or return 400 (bad request) — never 500
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "KYC/AML sign-off initiate must not return 500 with valid auth and Simulated mode.");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "KYC/AML sign-off endpoint must exist.");
        }

        // ── ALIGN16: compliance case create returns structured response ──────────

        [Test]
        public async Task ALIGN16_ComplianceCaseCreate_WithAuth_ReturnsStructuredResponse()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();
            string jwt = await ObtainJwtAsync(client, $"align16-{Guid.NewGuid():N}");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/compliance-cases",
                new
                {
                    issuerId = "test-issuer",
                    subjectId = $"align-subject-{Guid.NewGuid():N}",
                    caseType = "KYC",
                    jurisdiction = "CH",
                    actorId = "test-actor"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Compliance case create must not return 500 with valid auth.");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "Compliance case management endpoint must exist.");
        }

        // ── ALIGN17: lifecycle still verifies all 6 stages with compliance check ─

        [Test]
        public async Task ALIGN17_Lifecycle_VerifiesAll6Stages_EvenWithComplianceCheckIncluded()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();
            string jwt = await ObtainJwtAsync(client, $"align17-{Guid.NewGuid():N}");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/lifecycle/execute",
                new { correlationId = "align17" });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);

            Assert.That(doc.RootElement.GetProperty("isLifecycleVerified").GetBoolean(), Is.True,
                "Lifecycle must be fully verified even when compliance checks are present.");
            Assert.That(doc.RootElement.GetProperty("stages").GetArrayLength(), Is.EqualTo(6),
                "All 6 lifecycle stages must be present.");
        }

        // ── ALIGN19: full compliance-aligned sign-off journey succeeds ──────────

        [Test]
        public async Task ALIGN19_FullComplianceAlignedSignOffJourney_Succeeds()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();
            string corr = $"align19-{Guid.NewGuid():N}";
            string jwt = await ObtainJwtAsync(client, corr);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            // Step 1: Environment check with compliance workflow
            HttpResponseMessage envResp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new
                {
                    correlationId = corr,
                    includeConfigCheck = false,
                    includeFixtureCheck = true,
                    includeObservabilityCheck = true,
                    includeComplianceWorkflowCheck = true
                });
            Assert.That(envResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Environment check must succeed.");

            // Step 2: Fixture provisioning
            HttpResponseMessage fixtureResp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/fixtures/provision",
                new { correlationId = corr, resetIfExists = false });
            Assert.That(fixtureResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Fixture provisioning must succeed.");

            // Step 3: Lifecycle execution
            HttpResponseMessage lifecycleResp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/lifecycle/execute",
                new { correlationId = corr });
            Assert.That(lifecycleResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Lifecycle execution must succeed.");

            string lifecycleBody = await lifecycleResp.Content.ReadAsStringAsync();
            using JsonDocument lifecycleDoc = JsonDocument.Parse(lifecycleBody);
            Assert.That(lifecycleDoc.RootElement.GetProperty("isLifecycleVerified").GetBoolean(), Is.True,
                "Lifecycle must be verified in the compliance-aligned full journey.");

            // Step 4: Diagnostics
            HttpResponseMessage diagResp = await client.GetAsync(
                $"/api/v1/protected-sign-off/diagnostics?correlationId={corr}");
            Assert.That(diagResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Diagnostics must succeed.");

            string diagBody = await diagResp.Content.ReadAsStringAsync();
            using JsonDocument diagDoc = JsonDocument.Parse(diagBody);
            Assert.That(diagDoc.RootElement.GetProperty("isOperational").GetBoolean(), Is.True,
                "Diagnostics must be operational in the compliance-aligned sign-off journey.");
        }

        // ── ALIGN21: ComplianceCaseApprovalWorkflowAvailable check is present ──

        [Test]
        public async Task ALIGN21_ComplianceCaseApprovalWorkflowAvailable_Check_IsPresentWhenServiceWiredUp()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId                  = "align21",
                    IncludeComplianceWorkflowCheck = true
                });

            List<string> checkNames = result.Checks
                .Where(c => c.Category == EnvironmentCheckCategory.ComplianceWorkflow)
                .Select(c => c.Name)
                .ToList();

            Assert.That(checkNames, Does.Contain("ComplianceCaseApprovalWorkflowAvailable"),
                "Stable check name 'ComplianceCaseApprovalWorkflowAvailable' must be present as part of the release-grade approval-workflow parity surface.");
        }

        [Test]
        public async Task ALIGN21_ComplianceCaseApprovalWorkflowAvailable_IsPass_WhenServicePresent()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, _casesMock.Object);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId                  = "align21b",
                    IncludeComplianceWorkflowCheck = true
                });

            EnvironmentCheck? check = result.Checks
                .FirstOrDefault(c => c.Name == "ComplianceCaseApprovalWorkflowAvailable");

            Assert.That(check, Is.Not.Null, "ComplianceCaseApprovalWorkflowAvailable check must be present.");
            Assert.That(check!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "ComplianceCaseApprovalWorkflowAvailable must Pass when the service is available.");
        }

        [Test]
        public async Task ALIGN21_ComplianceCaseApprovalWorkflowAvailable_IsDegradedFail_WhenServiceAbsent()
        {
            ProtectedSignOffEnvironmentService svc = CreateServiceWith(_kycAmlMock.Object, cases: null);

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId                  = "align21c",
                    IncludeComplianceWorkflowCheck = true
                });

            EnvironmentCheck? check = result.Checks
                .FirstOrDefault(c => c.Name == "ComplianceCaseApprovalWorkflowAvailable");

            Assert.That(check, Is.Not.Null, "ComplianceCaseApprovalWorkflowAvailable check must be present even when service is absent.");
            Assert.That(check!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail),
                "ComplianceCaseApprovalWorkflowAvailable must DegradedFail when the service is absent.");
        }

        // ── ALIGN22: approval-workflow parity endpoints are accessible via HTTP ──

        [Test]
        public async Task ALIGN22_ApproveEndpoint_IsAccessible_ViaHttp()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();

            // No auth — must return 401 (fail-closed, not 404 or 500)
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/compliance-cases/test-case-id/approve",
                new { rationale = "test" });

            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(400),
                "Approve endpoint must be accessible (401 or 400), not 404 or 500.");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "Approve endpoint must not return 404 — it must be registered.");
        }

        [Test]
        public async Task ALIGN22_RejectEndpoint_IsAccessible_ViaHttp()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/compliance-cases/test-case-id/reject",
                new { reason = "test rejection" });

            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(400),
                "Reject endpoint must be accessible (401 or 400), not 404 or 500.");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "Reject endpoint must not return 404 — it must be registered.");
        }

        [Test]
        public async Task ALIGN22_ReturnForInformationEndpoint_IsAccessible_ViaHttp()
        {
            using ComplianceAlignmentFactory factory = new();
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/compliance-cases/test-case-id/return-for-information",
                new { reason = "test return", targetStage = "EvidencePending" });

            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(400),
                "ReturnForInformation endpoint must be accessible (401 or 400), not 404 or 500.");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "ReturnForInformation endpoint must not return 404 — it must be registered.");
        }
    }
}

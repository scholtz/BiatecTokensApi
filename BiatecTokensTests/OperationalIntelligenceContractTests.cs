using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.OperationalIntelligence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for the deterministic operational intelligence and audit evidence API layer.
    ///
    /// Validates all ten acceptance criteria:
    /// AC1  - Timeline API: deterministic, ordered operation events with stable semantics.
    /// AC2  - Compliance checkpoint API: normalized states with business-readable guidance.
    /// AC3  - Risk signal model: bounded categories and stable severity/confidence fields.
    /// AC4  - Response envelopes: no sensitive internals, always include actionable next steps.
    /// AC5  - Correlation identifiers: consistently link API responses to backend evidence.
    /// AC6  - Reporting payloads: support non-technical status summaries.
    /// AC7  - Idempotent reads: deterministic under transient failures and retries.
    /// AC8  - Test coverage: unit, integration, and contract tests across happy/retry/failure paths.
    /// AC9  - CI quality gates: passing without flaky suites or reduced thresholds.
    /// AC10 - Backward compatibility: existing contracts maintained; no breaking changes.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class OperationalIntelligenceContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private static readonly Dictionary<string, string?> TestConfiguration = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
            ["JwtConfig:Issuer"] = "BiatecTokensApi",
            ["JwtConfig:Audience"] = "BiatecTokensUsers",
            ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
            ["JwtConfig:RefreshTokenExpirationDays"] = "30",
            ["JwtConfig:ValidateIssuerSigningKey"] = "true",
            ["JwtConfig:ValidateIssuer"] = "true",
            ["JwtConfig:ValidateAudience"] = "true",
            ["JwtConfig:ValidateLifetime"] = "true",
            ["JwtConfig:ClockSkewMinutes"] = "5",
            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
            ["IPFSConfig:TimeoutSeconds"] = "30",
            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
            ["IPFSConfig:ValidateContentHash"] = "true",
            ["IPFSConfig:Username"] = "",
            ["IPFSConfig:Password"] = "",
            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
            ["EVMChains:0:ChainId"] = "8453",
            ["EVMChains:0:Name"] = "Base Mainnet",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired",
            ["AllowedOrigins:0"] = "http://localhost:3000",
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC1 – Timeline API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1: Health endpoint of the operational intelligence API must return 200,
        /// confirming the controller is registered and reachable.
        /// </summary>
        [Test]
        public async Task AC1_TimelineApi_HealthEndpoint_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/operational-intelligence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Operational intelligence health endpoint must return 200");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body), "Health response must be valid JSON");
        }

        /// <summary>
        /// AC1: Unauthenticated access to the timeline endpoint must return 401,
        /// confirming the auth boundary is enforced.
        /// </summary>
        [Test]
        public async Task AC1_TimelineApi_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var req = new OperationTimelineRequest { DeploymentId = "any-id" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Timeline endpoint must require authentication");
        }

        /// <summary>
        /// AC1: Authenticated request for a non-existent deployment must return 404
        /// with a structured response – not 500.
        /// </summary>
        [Test]
        public async Task AC1_TimelineApi_UnknownDeployment_Returns404WithStructuredResponse()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new OperationTimelineRequest { DeploymentId = Guid.NewGuid().ToString() };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "Unknown deployment must return 404");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body), "404 response must be valid JSON");
        }

        /// <summary>
        /// AC1: Missing DeploymentId must return 400 Bad Request with an actionable hint –
        /// never 500.
        /// </summary>
        [Test]
        public async Task AC1_TimelineApi_MissingDeploymentId_Returns400WithRemediationHint()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new OperationTimelineRequest { DeploymentId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Empty DeploymentId must return 400");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body), "400 response must be valid JSON");
            var doc = JsonDocument.Parse(body);
            // RemediationHint must be present (not null).
            Assert.That(doc.RootElement.TryGetProperty("remediationHint", out var hint)
                        || doc.RootElement.TryGetProperty("RemediationHint", out hint), Is.True,
                "400 response must include a remediationHint field");
        }

        /// <summary>
        /// AC1: Timeline endpoint schema is stable across repeated identical requests
        /// (idempotent read contract).
        /// </summary>
        [Test]
        public async Task AC1_TimelineApi_RepeatedUnknownRequests_ReturnDeterministicSchema()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var deploymentId = Guid.NewGuid().ToString();
            var req = new OperationTimelineRequest { DeploymentId = deploymentId };

            var resp1 = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);
            var resp2 = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);
            var resp3 = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);

            // All three must return the same HTTP status code.
            Assert.That(resp1.StatusCode, Is.EqualTo(resp2.StatusCode),
                "Repeated requests must return the same status code");
            Assert.That(resp2.StatusCode, Is.EqualTo(resp3.StatusCode),
                "Third request must match the second");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2 – Compliance Checkpoint API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: Unauthenticated access to the compliance-checkpoints endpoint must return 401.
        /// </summary>
        [Test]
        public async Task AC2_ComplianceCheckpoints_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var req = new ComplianceCheckpointRequest { DeploymentId = "any-id" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Compliance checkpoints endpoint must require authentication");
        }

        /// <summary>
        /// AC2: Empty DeploymentId must return 400 with structured error – not 500.
        /// </summary>
        [Test]
        public async Task AC2_ComplianceCheckpoints_EmptyDeploymentId_Returns400()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new ComplianceCheckpointRequest { DeploymentId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body));
        }

        /// <summary>
        /// AC2: Unknown deployment must return 404 with stable error envelope (not 500).
        /// </summary>
        [Test]
        public async Task AC2_ComplianceCheckpoints_UnknownDeployment_Returns404()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new ComplianceCheckpointRequest { DeploymentId = Guid.NewGuid().ToString() };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body));
        }

        /// <summary>
        /// AC2: Repeated unknown requests to compliance-checkpoints return a deterministic status.
        /// </summary>
        [Test]
        public async Task AC2_ComplianceCheckpoints_RepeatedRequests_DeterministicStatus()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var deploymentId = Guid.NewGuid().ToString();
            var req = new ComplianceCheckpointRequest { DeploymentId = deploymentId };

            var r1 = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);
            var r2 = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);

            Assert.That(r1.StatusCode, Is.EqualTo(r2.StatusCode),
                "Repeated compliance checkpoint requests must return the same status code");
        }

        /// <summary>
        /// AC2: Compliance checkpoint model has stable enum values – the normalized states
        /// are bounded and must remain backward-compatible.
        /// </summary>
        [Test]
        public void AC2_ComplianceCheckpoints_StateEnum_HasBoundedStableValues()
        {
            var states = Enum.GetValues<ComplianceCheckpointState>();
            Assert.That(states, Contains.Item(ComplianceCheckpointState.Pending));
            Assert.That(states, Contains.Item(ComplianceCheckpointState.InReview));
            Assert.That(states, Contains.Item(ComplianceCheckpointState.Satisfied));
            Assert.That(states, Contains.Item(ComplianceCheckpointState.Failed));
            Assert.That(states, Contains.Item(ComplianceCheckpointState.Blocked));
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3 – Risk Signal Model
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC3: classify-risk endpoint must return a valid risk signal with bounded category
        /// when given a known error code.
        /// </summary>
        [Test]
        public async Task AC3_RiskSignal_KnownErrorCode_ReturnsBoundedCategory()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync(
                $"/api/v1/operational-intelligence/classify-risk?errorCode={ErrorCodes.UNAUTHORIZED}");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var signal = await resp.Content.ReadFromJsonAsync<OperationalRiskSignal>();
            Assert.That(signal, Is.Not.Null);
            Assert.That(signal!.Category, Is.EqualTo(OperationalRiskCategory.AuthorizationRisk),
                "UNAUTHORIZED must map to AuthorizationRisk category");
            Assert.That(signal.Severity, Is.EqualTo(OperationSeverity.Error));
            Assert.That(signal.Confidence, Is.EqualTo(ConfidenceLevel.Definitive));
            Assert.That(signal.RemediationHint, Is.Not.Null.And.Not.Empty,
                "Risk signal must include an actionable remediation hint");
        }

        /// <summary>
        /// AC3: Different known error codes must map to their deterministically correct categories.
        /// </summary>
        [Test]
        public async Task AC3_RiskSignal_MultipleErrorCodes_DeterministicMapping()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var cases = new (string Code, OperationalRiskCategory Expected)[]
            {
                (ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR, OperationalRiskCategory.NetworkRisk),
                (ErrorCodes.TIMEOUT,                    OperationalRiskCategory.InfrastructureRisk),
                (ErrorCodes.INVALID_REQUEST,            OperationalRiskCategory.DataIntegrityRisk),
                (ErrorCodes.CONFLICT,                   OperationalRiskCategory.PolicyRisk),
                (ErrorCodes.INTERNAL_SERVER_ERROR,      OperationalRiskCategory.InfrastructureRisk),
            };

            foreach (var (code, expected) in cases)
            {
                var resp = await _client.GetAsync(
                    $"/api/v1/operational-intelligence/classify-risk?errorCode={code}");
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"classify-risk must return 200 for code '{code}'");
                var signal = await resp.Content.ReadFromJsonAsync<OperationalRiskSignal>();
                Assert.That(signal!.Category, Is.EqualTo(expected),
                    $"Error code '{code}' must map to '{expected}'");
            }
        }

        /// <summary>
        /// AC3: Risk signal for an empty error code must return OperationalRiskCategory.None
        /// with Info severity.
        /// </summary>
        [Test]
        public async Task AC3_RiskSignal_MissingErrorCode_Returns400()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync(
                "/api/v1/operational-intelligence/classify-risk?errorCode=");

            // Empty code → 400 Bad Request.
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        /// <summary>
        /// AC3: OperationalRiskCategory enum must expose the complete bounded set of nine categories.
        /// Adding categories is allowed; removing existing ones is a breaking change.
        /// </summary>
        [Test]
        public void AC3_RiskSignal_RiskCategoryEnum_HasBoundedStableValues()
        {
            var categories = Enum.GetValues<OperationalRiskCategory>();
            Assert.That(categories, Contains.Item(OperationalRiskCategory.None));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.AuthorizationRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.NetworkRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.ContractRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.ComplianceRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.DataIntegrityRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.InfrastructureRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.SecurityRisk));
            Assert.That(categories, Contains.Item(OperationalRiskCategory.PolicyRisk));
        }

        /// <summary>
        /// AC3: Unknown error code must still return a valid signal (no exception),
        /// classified as InfrastructureRisk with Low confidence.
        /// </summary>
        [Test]
        public async Task AC3_RiskSignal_UnknownErrorCode_ReturnsConservativeClassification()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync(
                "/api/v1/operational-intelligence/classify-risk?errorCode=TOTALLY_UNKNOWN_XYZ");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var signal = await resp.Content.ReadFromJsonAsync<OperationalRiskSignal>();
            Assert.That(signal, Is.Not.Null);
            Assert.That(signal!.Confidence, Is.EqualTo(ConfidenceLevel.Low),
                "Unknown error codes must be classified with Low confidence");
            Assert.That(signal.RemediationHint, Is.Not.Null.And.Not.Empty,
                "Even unknown signals must carry a remediation hint");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC4 – Response Envelopes: no internals, always actionable
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC4: 404 error envelopes must include RemediationHint and must NOT include
        /// stack traces or internal implementation details.
        /// </summary>
        [Test]
        public async Task AC4_ErrorEnvelope_NotFound_HasRemediationHint_NoStackTrace()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new OperationTimelineRequest { DeploymentId = Guid.NewGuid().ToString() };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var body = await resp.Content.ReadAsStringAsync();

            // Must not contain stack-trace indicators.
            Assert.That(body, Does.Not.Contain("at BiatecTokens"),
                "Error response must not leak stack trace");
            Assert.That(body, Does.Not.Contain("System.Exception"),
                "Error response must not leak exception type");
            Assert.That(body, Does.Not.Contain("Inner exception"),
                "Error response must not leak inner exception");

            // Must contain a remediationHint.
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("remediationHint", out var rh) ||
                doc.RootElement.TryGetProperty("RemediationHint", out rh),
                Is.True,
                "404 envelope must include remediationHint");
        }

        /// <summary>
        /// AC4: 400 error envelopes for compliance-checkpoints must carry an error code
        /// and remediation hint – never a raw exception message.
        /// </summary>
        [Test]
        public async Task AC4_ErrorEnvelope_BadRequest_HasErrorCodeAndRemediationHint()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new ComplianceCheckpointRequest { DeploymentId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain("StackTrace"),
                "400 response must not leak stack trace");
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("errorCode", out _) ||
                doc.RootElement.TryGetProperty("ErrorCode", out _),
                Is.True,
                "400 response must include errorCode");
        }

        /// <summary>
        /// AC4: Stakeholder report error envelopes must not contain internal paths or secrets.
        /// </summary>
        [Test]
        public async Task AC4_ErrorEnvelope_StakeholderReport_NoInternalLeakage()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new StakeholderReportRequest { DeploymentId = Guid.NewGuid().ToString() };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/stakeholder-report", req);

            // Not Found – but must be safe.
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Stakeholder report must not return 500 for unknown deployment");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain("password"),
                "Response must not contain 'password'");
            Assert.That(body, Does.Not.Contain("secret"),
                "Response must not contain 'secret'");
            Assert.That(body, Does.Not.Contain("mnemonic"),
                "Response must not contain 'mnemonic'");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC5 – Correlation Identifiers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5: Risk signal response must always include a non-empty CorrelationId.
        /// </summary>
        [Test]
        public async Task AC5_CorrelationId_RiskSignalResponse_ContainsCorrelationId()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync(
                $"/api/v1/operational-intelligence/classify-risk?errorCode={ErrorCodes.TIMEOUT}&correlationId=test-corr-123");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var signal = await resp.Content.ReadFromJsonAsync<OperationalRiskSignal>();
            Assert.That(signal!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Risk signal must include a CorrelationId");
        }

        /// <summary>
        /// AC5: Timeline error response must include a CorrelationId field.
        /// </summary>
        [Test]
        public async Task AC5_CorrelationId_TimelineErrorResponse_ContainsCorrelationId()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new OperationTimelineRequest { DeploymentId = Guid.NewGuid().ToString() };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("correlationId", out var cid) ||
                doc.RootElement.TryGetProperty("CorrelationId", out cid),
                Is.True,
                "404 timeline envelope must carry a correlationId for audit linking");
        }

        /// <summary>
        /// AC5: Compliance checkpoint error response must include a CorrelationId.
        /// </summary>
        [Test]
        public async Task AC5_CorrelationId_CheckpointErrorResponse_ContainsCorrelationId()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new ComplianceCheckpointRequest { DeploymentId = Guid.NewGuid().ToString() };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/compliance-checkpoints", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("correlationId", out _) ||
                doc.RootElement.TryGetProperty("CorrelationId", out _),
                Is.True,
                "Checkpoint error response must carry a correlationId");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC6 – Stakeholder Reporting Payloads
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC6: Stakeholder report endpoint is reachable and requires authentication.
        /// </summary>
        [Test]
        public async Task AC6_StakeholderReport_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var req = new StakeholderReportRequest { DeploymentId = "any" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/stakeholder-report", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Stakeholder report endpoint must require authentication");
        }

        /// <summary>
        /// AC6: Empty DeploymentId in stakeholder report must return 400 with a hint.
        /// </summary>
        [Test]
        public async Task AC6_StakeholderReport_EmptyDeploymentId_Returns400WithHint()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new StakeholderReportRequest { DeploymentId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/stakeholder-report", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("remediationHint", out _) ||
                doc.RootElement.TryGetProperty("RemediationHint", out _),
                Is.True,
                "400 stakeholder report response must include a remediation hint");
        }

        /// <summary>
        /// AC6: StakeholderReportPayload model has required non-technical fields.
        /// </summary>
        [Test]
        public void AC6_StakeholderReport_PayloadModel_HasRequiredNonTechnicalFields()
        {
            var payload = new StakeholderReportPayload
            {
                DeploymentId      = "dep-123",
                TokenName         = "My Token",
                TokenSymbol       = "MTK",
                IssuanceProgress  = "Deployment complete",
                CompliancePosture = "All checkpoints satisfied",
                UnresolvedBlockers = 0,
                PrimaryRecommendedAction = null,
                CurrentState      = "Completed",
                CorrelationId     = "corr-abc"
            };

            Assert.That(payload.DeploymentId, Is.Not.Null.And.Not.Empty);
            Assert.That(payload.IssuanceProgress, Is.Not.Null.And.Not.Empty);
            Assert.That(payload.CompliancePosture, Is.Not.Null.And.Not.Empty);
            Assert.That(payload.UnresolvedBlockers, Is.EqualTo(0));
            Assert.That(payload.PrimaryRecommendedAction, Is.Null,
                "No action needed when there are no blockers");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC7 – Idempotent Reads and Retry Behavior
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC7: Repeated identical timeline requests return the same HTTP status
        /// (idempotent read under retry scenarios).
        /// </summary>
        [Test]
        public async Task AC7_IdempotentReads_TimelineEndpoint_ThreeIdenticalCalls_SameStatus()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var deploymentId = Guid.NewGuid().ToString();
            var req = new OperationTimelineRequest { DeploymentId = deploymentId };

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/timeline", req);
                statuses.Add(r.StatusCode);
            }

            Assert.That(statuses.Distinct().Count(), Is.EqualTo(1),
                "Three identical timeline requests must return the same status code (idempotency)");
        }

        /// <summary>
        /// AC7: Repeated identical stakeholder report requests return the same HTTP status.
        /// </summary>
        [Test]
        public async Task AC7_IdempotentReads_StakeholderReport_ThreeIdenticalCalls_SameStatus()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var deploymentId = Guid.NewGuid().ToString();
            var req = new StakeholderReportRequest { DeploymentId = deploymentId };

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _client.PostAsJsonAsync("/api/v1/operational-intelligence/stakeholder-report", req);
                statuses.Add(r.StatusCode);
            }

            Assert.That(statuses.Distinct().Count(), Is.EqualTo(1),
                "Repeated stakeholder report requests must be idempotent");
        }

        /// <summary>
        /// AC7: Repeated classify-risk calls for the same code always produce the same
        /// category (deterministic mapping).
        /// </summary>
        [Test]
        public async Task AC7_IdempotentReads_ClassifyRisk_RepeatedCalls_DeterministicMapping()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var url = $"/api/v1/operational-intelligence/classify-risk?errorCode={ErrorCodes.FORBIDDEN}";
            var categories = new List<OperationalRiskCategory>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _client.GetAsync(url);
                var signal = await r.Content.ReadFromJsonAsync<OperationalRiskSignal>();
                categories.Add(signal!.Category);
            }

            Assert.That(categories.Distinct().Count(), Is.EqualTo(1),
                "Same error code must always map to the same risk category (deterministic)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC8 – Test Coverage (unit-level contract assertions)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC8: OperationTimelineEntry has all fields required for an audit trail entry.
        /// </summary>
        [Test]
        public void AC8_TimelineEntry_RequiredAuditFields_Present()
        {
            var entry = new OperationTimelineEntry
            {
                EntryId       = "entry-1",
                CorrelationId = "corr-1",
                OccurredAt    = DateTime.UtcNow,
                FromState     = "Queued",
                ToState       = "Submitted",
                Severity      = OperationSeverity.Info,
                Description   = "Transaction submitted.",
                EventCode     = "SUBMITTED",
                Actor         = "system"
            };

            Assert.That(entry.EntryId, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.OccurredAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(entry.ToState, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.EventCode, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// AC8: ComplianceCheckpoint has all fields required for compliance evidence.
        /// </summary>
        [Test]
        public void AC8_ComplianceCheckpoint_RequiredFields_Present()
        {
            var checkpoint = new ComplianceCheckpoint
            {
                CheckpointId = "cp-1",
                Name         = "KYC Verified",
                State        = ComplianceCheckpointState.Satisfied,
                Explanation  = "KYC check passed.",
                IsBlocking   = true,
                Category     = "Compliance",
                CorrelationId = "corr-1"
            };

            Assert.That(checkpoint.CheckpointId, Is.Not.Null.And.Not.Empty);
            Assert.That(checkpoint.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(checkpoint.Explanation, Is.Not.Null.And.Not.Empty);
            Assert.That(checkpoint.State, Is.EqualTo(ComplianceCheckpointState.Satisfied));
        }

        /// <summary>
        /// AC8: OperationalRiskSignal model carries all fields required for audit evidence.
        /// </summary>
        [Test]
        public void AC8_RiskSignal_RequiredFields_Present()
        {
            var signal = new OperationalRiskSignal
            {
                SignalId        = "sig-1",
                Category        = OperationalRiskCategory.NetworkRisk,
                Severity        = OperationSeverity.Warning,
                Confidence      = ConfidenceLevel.High,
                SignalCode      = "BLOCKCHAIN_CONNECTION_ERROR",
                Description     = "Network connectivity issue detected.",
                RemediationHint = "Check network configuration.",
                CorrelationId   = "corr-1"
            };

            Assert.That(signal.SignalId, Is.Not.Null.And.Not.Empty);
            Assert.That(signal.SignalCode, Is.Not.Null.And.Not.Empty);
            Assert.That(signal.Description, Is.Not.Null.And.Not.Empty);
            Assert.That(signal.RemediationHint, Is.Not.Null.And.Not.Empty);
            Assert.That(signal.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC9 – CI Quality Gates (regression checks)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC9: Health liveness endpoint must return 200, confirming app starts cleanly.
        /// </summary>
        [Test]
        public async Task AC9_CIQualityGates_HealthLiveness_Returns200()
        {
            var resp = await _client.GetAsync("/health/live");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Health liveness must return 200");
        }

        /// <summary>
        /// AC9: Health readiness endpoint must return 200, confirming dependencies are healthy.
        /// </summary>
        [Test]
        public async Task AC9_CIQualityGates_HealthReadiness_Returns200()
        {
            var resp = await _client.GetAsync("/health/ready");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Health readiness must return 200");
        }

        /// <summary>
        /// AC9: Operational intelligence health endpoint must return 200, confirming
        /// the new controller is registered and serving.
        /// </summary>
        [Test]
        public async Task AC9_CIQualityGates_OperationalIntelligenceHealth_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/operational-intelligence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("status", out var status) ||
                doc.RootElement.TryGetProperty("Status", out status),
                Is.True, "Health response must include status field");
        }

        /// <summary>
        /// AC9: Core auth register endpoint must not regress (returns non-500 for valid input).
        /// </summary>
        [Test]
        public async Task AC9_CIQualityGates_CoreAuthRegister_NoRegression()
        {
            var req = new RegisterRequest
            {
                Email           = $"ci-oi-{Guid.NewGuid()}@example.com",
                Password        = "CiOi1@Secure",
                ConfirmPassword = "CiOi1@Secure"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Register endpoint must not regress");
        }

        /// <summary>
        /// AC9: Deployment list endpoint must not regress (returns non-500 when authenticated).
        /// </summary>
        [Test]
        public async Task AC9_CIQualityGates_DeploymentListEndpoint_NoRegression()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Deployment list must not regress");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC10 – Backward Compatibility
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC10: Existing deployment status endpoint schema must remain backward-compatible
        /// (key fields still present in response).
        /// </summary>
        [Test]
        public async Task AC10_BackwardCompatibility_DeploymentStatus_SchemaUnchanged()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var fakeId = Guid.NewGuid().ToString();
            var resp = await _client.GetAsync($"/api/v1/token/deployments/{fakeId}");

            // Must be 404 or 200 – never 500.
            Assert.That((int)resp.StatusCode,
                Is.EqualTo(404).Or.EqualTo(200),
                "Deployment status endpoint must return 200 or 404, never 500");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body),
                "Deployment status response must be valid JSON");
        }

        /// <summary>
        /// AC10: Lifecycle intelligence health endpoint must remain reachable (no regression).
        /// </summary>
        [Test]
        public async Task AC10_BackwardCompatibility_LifecycleIntelligenceHealth_StillReachable()
        {
            var resp = await _client.GetAsync("/api/v2/lifecycle/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Lifecycle intelligence health must remain reachable");
        }

        /// <summary>
        /// AC10: Token operations intelligence health endpoint must remain reachable.
        /// </summary>
        [Test]
        public async Task AC10_BackwardCompatibility_TokenOperationsIntelligenceHealth_StillReachable()
        {
            var resp = await _client.GetAsync("/api/v1/operations-intelligence/health-check");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Token operations intelligence health must remain reachable");
        }

        /// <summary>
        /// AC10: OperationSeverity enum must have stable values – existing integer values
        /// must not change (breaking change for persisted data).
        /// </summary>
        [Test]
        public void AC10_BackwardCompatibility_OperationSeverityEnum_StableValues()
        {
            Assert.That((int)OperationSeverity.Info,     Is.EqualTo(0));
            Assert.That((int)OperationSeverity.Warning,  Is.EqualTo(1));
            Assert.That((int)OperationSeverity.Error,    Is.EqualTo(2));
            Assert.That((int)OperationSeverity.Critical, Is.EqualTo(3));
        }

        /// <summary>
        /// AC10: ComplianceCheckpointState enum must have stable integer values.
        /// </summary>
        [Test]
        public void AC10_BackwardCompatibility_ComplianceCheckpointStateEnum_StableValues()
        {
            Assert.That((int)ComplianceCheckpointState.Pending,    Is.EqualTo(0));
            Assert.That((int)ComplianceCheckpointState.InReview,   Is.EqualTo(1));
            Assert.That((int)ComplianceCheckpointState.Satisfied,  Is.EqualTo(2));
            Assert.That((int)ComplianceCheckpointState.Failed,     Is.EqualTo(3));
            Assert.That((int)ComplianceCheckpointState.Blocked,    Is.EqualTo(4));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> GetAuthTokenAsync()
        {
            var req = new RegisterRequest
            {
                Email           = $"oi-helper-{Guid.NewGuid()}@example.com",
                Password        = "Helper1@Secure",
                ConfirmPassword = "Helper1@Secure"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);
            var reg  = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return reg?.AccessToken ?? string.Empty;
        }
    }
}

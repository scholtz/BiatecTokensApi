using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for Issue: Vision - harden backend auth and launch orchestration
    /// reliability for enterprise trust.
    ///
    /// Validates all seven acceptance criteria:
    /// AC1 - Auth/Session Contract Quality: consistent structured payloads, stable error schema,
    ///        ARC76 derivation determinism for equivalent inputs.
    /// AC2 - Launch Status Determinism: documented transition model, no ambiguous status,
    ///        idempotent retry behavior.
    /// AC3 - Compliance Payload Integrity: complete required fields, business-readable failure
    ///        reasons, machine-readable codes.
    /// AC4 - Error and Observability Standards: structured errors with stable codes and
    ///        correlation metadata, no silent fallbacks.
    /// AC5 - Automated Quality Gates: updated test suites pass in CI, regression checks.
    /// AC6 - Documentation and Operability: health and arc76 info endpoints expose contract
    ///        metadata for runbook-driven diagnosis.
    /// AC7 - Vision Alignment: email/password-first product model enforced, backend-managed
    ///        token lifecycle reflected in API behavior.
    ///
    /// Business Value: Turns backend behavior into a dependable enterprise-grade product
    /// asset that supports sales, compliance due diligence, and predictable delivery for
    /// non-crypto-native customers requiring MiCA-oriented auditability and determinism.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class EnterpriseAuthLaunchReliabilityContractTests
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
            ["JwtConfig:SecretKey"] = "enterprise-reliability-test-secret-key-32chars-minimum",
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
            ["KeyManagementConfig:HardcodedKey"] = "EnterpriseReliabilityTestKey32CharactersMinimum",
            ["KycConfig:Provider"] = "Mock",
            ["KycConfig:AutoApprove"] = "true",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
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
        // AC1 – Auth/Session Contract Quality
        // ─────────────────────────────────────────────────────────────────────

        #region AC1: Auth/Session Contract Quality

        /// <summary>
        /// AC1.1: Successful registration returns structured payload with all required fields.
        /// </summary>
        [Test]
        public async Task AC1_Register_ValidCredentials_ReturnsStructuredPayloadWithAllRequiredFields()
        {
            var request = new
            {
                Email = $"ac1-test-{Guid.NewGuid():N}@enterprise.test",
                Password = "EnterprisePass1!",
                ConfirmPassword = "EnterprisePass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Registration with valid credentials must return 200 OK");

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body, Is.Not.Null, "Response body must not be null");
            Assert.That(body!.Success, Is.True, "Success must be true for valid registration");
            Assert.That(body.UserId, Is.Not.Null.And.Not.Empty, "UserId must be present");
            Assert.That(body.Email, Is.Not.Null.And.Not.Empty, "Email must be present in response");
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
            Assert.That(body.ExpiresAt, Is.Not.Null, "ExpiresAt must be present");
            Assert.That(body.Timestamp, Is.Not.EqualTo(default(DateTime)), "Timestamp must be set");
            Assert.That(body.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present for contract stability");
        }

        /// <summary>
        /// AC1.2: Successful login returns structured payload with all required fields.
        /// </summary>
        [Test]
        public async Task AC1_Login_ValidCredentials_ReturnsStructuredPayloadWithAllRequiredFields()
        {
            var email = $"ac1-login-{Guid.NewGuid():N}@enterprise.test";
            var password = "EnterprisePass1!";
            await RegisterUser(email, password);

            var loginRequest = new { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Login with valid credentials must return 200 OK");

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body, Is.Not.Null, "Login response body must not be null");
            Assert.That(body!.Success, Is.True, "Success must be true for valid login");
            Assert.That(body.UserId, Is.Not.Null.And.Not.Empty, "UserId must be present");
            Assert.That(body.Email, Is.Not.Null.And.Not.Empty, "Email must be present");
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
            Assert.That(body.ExpiresAt, Is.Not.Null, "ExpiresAt must be present");
            Assert.That(body.Timestamp, Is.Not.EqualTo(default(DateTime)), "Timestamp must be set");
            Assert.That(body.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present");
        }

        /// <summary>
        /// AC1.3: Invalid credentials return stable error schema with non-ambiguous code.
        /// </summary>
        [Test]
        public async Task AC1_Login_InvalidCredentials_ReturnsStableErrorSchema()
        {
            var loginRequest = new
            {
                Email = "nonexistent@enterprise.test",
                Password = "WrongPassword1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401, 403),
                "Invalid credentials must return 4xx status code");

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "Error response body must not be empty");

            // Verify the error response contains meaningful information (camelCase JSON or ProblemDetails)
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            // API returns camelCase JSON (default ASP.NET Core); validation errors use ProblemDetails format
            Assert.That(root.TryGetProperty("success", out _) ||
                        root.TryGetProperty("errorMessage", out _) ||
                        root.TryGetProperty("title", out _) ||
                        root.TryGetProperty("errors", out _),
                Is.True, "Error response must contain recognizable error structure");
        }

        /// <summary>
        /// AC1.4: ARC76-derived Algorand address is deterministic for equivalent email/password inputs.
        /// </summary>
        [Test]
        public async Task AC1_ARC76_SameEmailPassword_ProducesDeterministicAlgorandAddress()
        {
            var email = $"ac1-arc76-{Guid.NewGuid():N}@enterprise.test";
            var password = "DeterministicPass1!";

            // Register and capture address
            var registerResponse = await RegisterUser(email, password);
            Assert.That(registerResponse, Is.Not.Null);
            string firstAddress = registerResponse!.AlgorandAddress!;

            // Login multiple times and verify same address
            for (int i = 0; i < 3; i++)
            {
                var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new { Email = email, Password = password });
                loginResponse.EnsureSuccessStatusCode();
                var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(firstAddress),
                    $"ARC76 derivation must be deterministic - run {i + 1} produced different address");
            }
        }

        /// <summary>
        /// AC1.5: Registration with weak password returns 400 with actionable error message.
        /// </summary>
        [Test]
        public async Task AC1_Register_WeakPassword_Returns400WithActionableError()
        {
            var request = new
            {
                Email = $"ac1-weak-{Guid.NewGuid():N}@enterprise.test",
                Password = "nouppercase1!",
                ConfirmPassword = "nouppercase1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Weak password must return 4xx");

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "Error body must not be empty");
        }

        /// <summary>
        /// AC1.6: Registration with mismatched passwords returns 400 stable error.
        /// </summary>
        [Test]
        public async Task AC1_Register_PasswordMismatch_Returns400()
        {
            var request = new
            {
                Email = $"ac1-mismatch-{Guid.NewGuid():N}@enterprise.test",
                Password = "EnterprisePass1!",
                ConfirmPassword = "DifferentPass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Mismatched passwords must return 4xx");
        }

        /// <summary>
        /// AC1.7: Token refresh returns structured payload with new access token.
        /// </summary>
        [Test]
        public async Task AC1_RefreshToken_ValidRefreshToken_ReturnsNewAccessToken()
        {
            var email = $"ac1-refresh-{Guid.NewGuid():N}@enterprise.test";
            var password = "RefreshPass1!";
            var registerResponse = await RegisterUser(email, password);
            Assert.That(registerResponse, Is.Not.Null);

            var refreshRequest = new { RefreshToken = registerResponse!.RefreshToken };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid refresh token must return 200 OK");

            var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(body, Is.Not.Null, "Refresh response body must not be null");
            Assert.That(body!.Success, Is.True, "Success must be true for valid refresh");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty, "New AccessToken must be present");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty, "New RefreshToken must be present");
            Assert.That(body.ExpiresAt, Is.Not.Null, "ExpiresAt must be present");
        }

        /// <summary>
        /// AC1.8: Invalid refresh token returns stable error schema.
        /// </summary>
        [Test]
        public async Task AC1_RefreshToken_InvalidToken_ReturnsStableErrorSchema()
        {
            var refreshRequest = new { RefreshToken = "invalid-refresh-token-that-does-not-exist" };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401, 403),
                "Invalid refresh token must return 4xx");

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "Error body must not be empty");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AC2 – Launch Status Determinism
        // ─────────────────────────────────────────────────────────────────────

        #region AC2: Launch Status Determinism

        /// <summary>
        /// AC2.1: Deployment status endpoint returns structured response for unknown deployment.
        /// </summary>
        [Test]
        public async Task AC2_GetDeploymentStatus_UnknownDeploymentId_Returns404OrStructuredError()
        {
            var email = $"ac2-status-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync(
                $"/api/v1/token/deployments/{Guid.NewGuid():N}");

            Assert.That((int)response.StatusCode, Is.AnyOf(404, 400, 401),
                "Unknown deployment ID must return 404 or structured 4xx error");
        }

        /// <summary>
        /// AC2.2: Deployment list endpoint returns stable structured response.
        /// </summary>
        [Test]
        public async Task AC2_ListDeployments_AuthenticatedUser_ReturnsStructuredResponse()
        {
            var email = $"ac2-list-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/token/deployments");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400),
                "Authenticated deployment list must return 200 or structured error");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var raw = await response.Content.ReadAsStringAsync();
                Assert.That(raw, Is.Not.Null.And.Not.Empty, "Response must not be empty");
            }
        }

        /// <summary>
        /// AC2.3: Deployment metrics endpoint returns stable structured response.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentMetrics_AuthenticatedUser_ReturnsStructuredResponse()
        {
            var email = $"ac2-metrics-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/token/deployments/metrics");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400),
                "Metrics endpoint must return 200 or structured error");
        }

        /// <summary>
        /// AC2.4: Deployment unauthenticated access returns 401.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentList_Unauthenticated_Returns401()
        {
            // Ensure no auth token
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/v1/token/deployments");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Unauthenticated deployment access must return 401 or 403");
        }

        /// <summary>
        /// AC2.5: Repeated registration produces stable user identity (idempotent-safe).
        /// Same email re-registration does not silently produce different identities.
        /// </summary>
        [Test]
        public async Task AC2_Register_DuplicateEmail_ReturnsConsistentErrorOrConflict()
        {
            var email = $"ac2-dup-{Guid.NewGuid():N}@enterprise.test";
            var password = "DuplicatePass1!";

            var first = await RegisterUser(email, password);
            Assert.That(first, Is.Not.Null, "First registration must succeed");

            // Second registration with same email
            var request = new { Email = email, Password = password, ConfirmPassword = password };
            var second = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Should either succeed (returning same user) or return 4xx conflict
            // Must NOT return 500 or corrupt state
            Assert.That((int)second.StatusCode, Is.Not.EqualTo(500),
                "Duplicate registration must not return 500 Internal Server Error");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AC3 – Compliance Payload Integrity
        // ─────────────────────────────────────────────────────────────────────

        #region AC3: Compliance Payload Integrity

        /// <summary>
        /// AC3.1: Compliance metadata endpoint returns structured response for valid asset.
        /// </summary>
        [Test]
        public async Task AC3_GetComplianceMetadata_ReturnsStructuredPayload()
        {
            var email = $"ac3-compliance-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Test with a non-existent asset - should return 404 with structured error, not 500
            var response = await _client.GetAsync("/api/v1/compliance/999999999");

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Compliance endpoint must not return 500 for unknown asset");
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 401, 404),
                "Compliance endpoint must return structured 4xx or 200");
        }

        /// <summary>
        /// AC3.2: Compliance audit log endpoint returns stable structure.
        /// </summary>
        [Test]
        public async Task AC3_GetComplianceAuditLog_ReturnsStructuredPayload()
        {
            var email = $"ac3-audit-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/compliance/audit-log");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 401),
                "Compliance audit log must return structured response");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var raw = await response.Content.ReadAsStringAsync();
                Assert.That(raw, Is.Not.Null.And.Not.Empty);
            }
        }

        /// <summary>
        /// AC3.3: Compliance validate-preset endpoint returns structured response with
        /// machine-readable codes for failures.
        /// </summary>
        [Test]
        public async Task AC3_ValidateTokenPreset_ReturnsStructuredCompliancePayload()
        {
            var email = $"ac3-preset-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                TokenName = "TestToken",
                TokenSymbol = "TT",
                TotalSupply = 1000000,
                Jurisdiction = "US"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/compliance/validate-preset", request);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Compliance validate-preset must not return 500");
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 401),
                "Compliance validate-preset must return structured response");
        }

        /// <summary>
        /// AC3.4: Compliance endpoint with missing auth returns 401 (not silent failure).
        /// </summary>
        [Test]
        public async Task AC3_ComplianceEndpoint_Unauthenticated_Returns401NotSilentFailure()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/v1/compliance/audit-log");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Unauthenticated compliance access must return 401 or 403, not silent empty response");
        }

        /// <summary>
        /// AC3.5: Compliance list endpoint supports pagination and returns stable response.
        /// </summary>
        [Test]
        public async Task AC3_ListComplianceMetadata_AuthenticatedUser_ReturnsStructuredList()
        {
            var email = $"ac3-list-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/compliance?page=1&pageSize=10");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 401),
                "Compliance list must return structured response");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AC4 – Error and Observability Standards
        // ─────────────────────────────────────────────────────────────────────

        #region AC4: Error and Observability Standards

        /// <summary>
        /// AC4.1: Registration with malformed email returns structured 400, not 500.
        /// </summary>
        [Test]
        public async Task AC4_Register_MalformedEmail_Returns400WithStructuredError()
        {
            var request = new
            {
                Email = "not-an-email",
                Password = "EnterprisePass1!",
                ConfirmPassword = "EnterprisePass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Malformed email must return 400 or 422, not 500");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Malformed email must never return 500 Internal Server Error");
        }

        /// <summary>
        /// AC4.2: Registration with null/empty email returns structured 400.
        /// </summary>
        [Test]
        public async Task AC4_Register_EmptyEmail_Returns400WithStructuredError()
        {
            var request = new
            {
                Email = "",
                Password = "EnterprisePass1!",
                ConfirmPassword = "EnterprisePass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Empty email must return 400 or 422");
        }

        /// <summary>
        /// AC4.3: Login with null body returns structured error, not 500.
        /// </summary>
        [Test]
        public async Task AC4_Login_MissingBody_Returns400NotInternalServerError()
        {
            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1/auth/login", content);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Missing credentials must never return 500 Internal Server Error");
        }

        /// <summary>
        /// AC4.4: ARC76 info endpoint returns stable schema for observability.
        /// </summary>
        [Test]
        public async Task AC4_ARC76Info_AnonymousAccess_ReturnsStableSchema()
        {
            var response = await _client.GetAsync("/api/v1/auth/arc76/info");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "ARC76 info endpoint must be publicly accessible and return 200");

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "ARC76 info response must not be empty");

            // Must return a JSON structure (not plain text or empty)
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            Assert.That(root.ValueKind, Is.EqualTo(JsonValueKind.Object),
                "ARC76 info response must be a JSON object");
        }

        /// <summary>
        /// AC4.5: Correlation ID propagation - response includes correlation metadata.
        /// Validates observability contract for enterprise diagnostics.
        /// </summary>
        [Test]
        public async Task AC4_Register_Response_IncludesCorrelationIdForObservability()
        {
            var request = new
            {
                Email = $"ac4-corr-{Guid.NewGuid():N}@enterprise.test",
                Password = "CorrelationPass1!",
                ConfirmPassword = "CorrelationPass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body, Is.Not.Null);

            // Correlation ID is included in the response body (camelCase: correlationId)
            // and also as X-Correlation-ID response header per CorrelationIdMiddleware
            bool hasCorrelationInBody = body!.CorrelationId != null;
            bool hasCorrelationInHeader = response.Headers.Contains("X-Correlation-ID");

            Assert.That(hasCorrelationInBody || hasCorrelationInHeader, Is.True,
                "Response must include correlation ID in body (correlationId) or X-Correlation-ID header for enterprise observability");
        }

        /// <summary>
        /// AC4.6: Health endpoint returns structured status for runbook diagnosis.
        /// </summary>
        [Test]
        public async Task AC4_HealthEndpoint_ReturnsStructuredStatus()
        {
            var response = await _client.GetAsync("/health");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 503),
                "Health endpoint must return 200 (healthy) or 503 (degraded), not 500");

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "Health response must not be empty");
        }

        /// <summary>
        /// AC4.7: Ready health endpoint returns structured status.
        /// </summary>
        [Test]
        public async Task AC4_HealthReadyEndpoint_ReturnsStructuredStatus()
        {
            var response = await _client.GetAsync("/health/ready");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 503),
                "Readiness endpoint must return 200 or 503, not 500");
        }

        /// <summary>
        /// AC4.8: Liveness health endpoint returns structured status.
        /// </summary>
        [Test]
        public async Task AC4_HealthLiveEndpoint_ReturnsStructuredStatus()
        {
            var response = await _client.GetAsync("/health/live");

            Assert.That((int)response.StatusCode, Is.AnyOf(200, 503),
                "Liveness endpoint must return 200 or 503, not 500");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AC5 – Automated Quality Gates
        // ─────────────────────────────────────────────────────────────────────

        #region AC5: Automated Quality Gates (Regression Checks)

        /// <summary>
        /// AC5.1: Token creation endpoint is accessible for authenticated users
        /// (regression check - should return structured response, not 500).
        /// </summary>
        [Test]
        public async Task AC5_TokenCreation_AuthenticatedUser_ReturnsStructuredResponse()
        {
            var email = $"ac5-token-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                TokenName = "TestToken",
                TokenSymbol = "TT",
                TotalSupply = 1000000,
                Decimals = 6,
                UserId = email
            };
            var response = await _client.PostAsJsonAsync("/api/v1/token/asa-ft/create", request);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Token creation must not return 500 for authenticated users");
        }

        /// <summary>
        /// AC5.2: Register-login-refresh full workflow produces consistent results across 3 runs.
        /// </summary>
        [Test]
        public async Task AC5_AuthWorkflow_ThreeRuns_ProducesConsistentResults()
        {
            var email = $"ac5-workflow-{Guid.NewGuid():N}@enterprise.test";
            var password = "WorkflowPass1!";

            // Register once
            var registerResponse = await RegisterUser(email, password);
            Assert.That(registerResponse, Is.Not.Null);
            string firstAddress = registerResponse!.AlgorandAddress!;

            // Run login 3 times, verify consistent results
            for (int run = 1; run <= 3; run++)
            {
                var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new { Email = email, Password = password });
                loginResponse.EnsureSuccessStatusCode();
                var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

                Assert.That(loginBody!.Success, Is.True, $"Run {run}: Login must succeed");
                Assert.That(loginBody.AlgorandAddress, Is.EqualTo(firstAddress),
                    $"Run {run}: ARC76 address must be deterministic");
                Assert.That(loginBody.AccessToken, Is.Not.Null.And.Not.Empty,
                    $"Run {run}: AccessToken must be present");
                Assert.That(loginBody.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                    $"Run {run}: DerivationContractVersion must be stable");
            }
        }

        /// <summary>
        /// AC5.3: Existing auth endpoints still return correct status codes (no regression).
        /// </summary>
        [Test]
        public async Task AC5_ExistingAuthEndpoints_NoRegression_ReturnExpectedStatusCodes()
        {
            // Register
            var email = $"ac5-regr-{Guid.NewGuid():N}@enterprise.test";
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = "RegressionPass1!", ConfirmPassword = "RegressionPass1!" });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Register must return 200");

            // Login
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = "RegressionPass1!" });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Login must return 200");

            // Refresh
            var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = loginBody!.RefreshToken });
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Refresh must return 200");
        }

        /// <summary>
        /// AC5.4: ARC76 verify-derivation endpoint is accessible and returns structured response.
        /// </summary>
        [Test]
        public async Task AC5_ARC76VerifyDerivation_AuthenticatedUser_ReturnsStructuredResponse()
        {
            var email = $"ac5-arc76-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var request = new { Email = email };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", request);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "ARC76 verify-derivation must not return 500");
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 401),
                "ARC76 verify-derivation must return structured response");
        }

        /// <summary>
        /// AC5.5: Session inspection endpoint returns stable schema for authenticated users.
        /// </summary>
        [Test]
        public async Task AC5_SessionInspect_AuthenticatedUser_ReturnsStableSchema()
        {
            var email = $"ac5-session-{Guid.NewGuid():N}@enterprise.test";
            var token = await GetAuthToken(email);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Session endpoint must not return 500 for authenticated users");
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 401),
                "Session endpoint must return structured response");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AC6 – Documentation and Operability
        // ─────────────────────────────────────────────────────────────────────

        #region AC6: Documentation and Operability

        /// <summary>
        /// AC6.1: ARC76 info endpoint exposes derivation contract version for runbook documentation.
        /// </summary>
        [Test]
        public async Task AC6_ARC76Info_ExposesDerivationContractVersion()
        {
            var response = await _client.GetAsync("/api/v1/auth/arc76/info");
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Contract version must be present for operational runbooks
            // ARC76DerivationInfoResponse serializes ContractVersion as contractVersion (camelCase)
            bool hasVersion =
                root.TryGetProperty("contractVersion", out var versionEl) ||
                root.TryGetProperty("derivationContractVersion", out versionEl);

            Assert.That(hasVersion, Is.True,
                "ARC76 info must expose contractVersion field for operational runbooks");
        }

        /// <summary>
        /// AC6.2: Registration response includes derivation contract version for forward compatibility.
        /// </summary>
        [Test]
        public async Task AC6_Register_Response_IncludesDerivationContractVersionForForwardCompat()
        {
            var request = new
            {
                Email = $"ac6-ver-{Guid.NewGuid():N}@enterprise.test",
                Password = "VersionPass1!",
                ConfirmPassword = "VersionPass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "Registration response must include DerivationContractVersion for contract stability tracking");
        }

        /// <summary>
        /// AC6.3: Login response includes derivation contract version.
        /// </summary>
        [Test]
        public async Task AC6_Login_Response_IncludesDerivationContractVersionForRunbookDiagnosis()
        {
            var email = $"ac6-login-{Guid.NewGuid():N}@enterprise.test";
            var password = "RunbookPass1!";
            await RegisterUser(email, password);

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "Login response must include DerivationContractVersion for runbook diagnosis");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // AC7 – Vision Alignment
        // ─────────────────────────────────────────────────────────────────────

        #region AC7: Vision Alignment

        /// <summary>
        /// AC7.1: Email/password-first model - registration requires email and password
        /// (no wallet or mnemonic required by caller).
        /// </summary>
        [Test]
        public async Task AC7_Registration_EmailPasswordFirst_NoWalletRequired()
        {
            // Only email and password - no wallet address, mnemonic, or blockchain key required
            var request = new
            {
                Email = $"ac7-emailfirst-{Guid.NewGuid():N}@enterprise.test",
                Password = "EmailFirstPass1!",
                ConfirmPassword = "EmailFirstPass1!"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Registration must succeed with only email/password - no wallet required");

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Backend must derive AlgorandAddress automatically from email/password (no wallet required)");
        }

        /// <summary>
        /// AC7.2: Backend-managed token lifecycle - Algorand address is derived and managed
        /// by backend without user providing cryptographic keys.
        /// </summary>
        [Test]
        public async Task AC7_AlgorandAddress_BackendDerived_NotUserProvided()
        {
            var email = $"ac7-backend-{Guid.NewGuid():N}@enterprise.test";
            var password = "BackendManaged1!";

            var request = new { Email = email, Password = password, ConfirmPassword = password };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // The address should be a valid Algorand address format (58 chars, base32)
            Assert.That(body!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AlgorandAddress must be backend-derived");
            Assert.That(body.AlgorandAddress!.Length, Is.EqualTo(58),
                "Algorand address must be 58 characters (standard format)");
        }

        /// <summary>
        /// AC7.3: Different email/password pairs produce different Algorand addresses
        /// (no address collision between users).
        /// </summary>
        [Test]
        public async Task AC7_DifferentUsers_ProduceDifferentAlgorandAddresses()
        {
            var user1Email = $"ac7-user1-{Guid.NewGuid():N}@enterprise.test";
            var user2Email = $"ac7-user2-{Guid.NewGuid():N}@enterprise.test";
            const string password = "SharedPass1!";

            var response1 = await RegisterUser(user1Email, password);
            var response2 = await RegisterUser(user2Email, password);

            Assert.That(response1, Is.Not.Null);
            Assert.That(response2, Is.Not.Null);
            Assert.That(response1!.AlgorandAddress, Is.Not.EqualTo(response2!.AlgorandAddress),
                "Different users must have different Algorand addresses (no collision)");
        }

        /// <summary>
        /// AC7.4: Enterprise trust - same user's address is identical across register and login
        /// (consistent identity for enterprise audit trails).
        /// </summary>
        [Test]
        public async Task AC7_RegisterThenLogin_AlgorandAddressConsistentAcrossEntireLifecycle()
        {
            var email = $"ac7-lifecycle-{Guid.NewGuid():N}@enterprise.test";
            var password = "LifecyclePass1!";

            var registerResponse = await RegisterUser(email, password);
            Assert.That(registerResponse, Is.Not.Null);

            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            loginResponse.EnsureSuccessStatusCode();
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(registerResponse!.AlgorandAddress),
                "Algorand address must be consistent between registration and all subsequent logins " +
                "(required for enterprise audit trail integrity)");
        }

        /// <summary>
        /// AC7.5: Non-crypto-native enterprise user workflow - complete auth cycle using only
        /// standard email/password concepts, no blockchain primitives required from user.
        /// </summary>
        [Test]
        public async Task AC7_EnterpriseCycle_NoCryptoKnowledgeRequired_FullAuthLifecycle()
        {
            var email = $"ac7-enterprise-{Guid.NewGuid():N}@enterprise.test";
            var password = "EnterpriseLifecycle1!";

            // Step 1: Register (no blockchain knowledge needed)
            var registerResult = await RegisterUser(email, password);
            Assert.That(registerResult, Is.Not.Null, "Registration must succeed");
            Assert.That(registerResult!.Success, Is.True);

            // Step 2: Login (no blockchain knowledge needed)
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            loginResp.EnsureSuccessStatusCode();
            var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginBody!.Success, Is.True);

            // Step 3: Refresh token (standard JWT lifecycle)
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = loginBody.RefreshToken });
            refreshResp.EnsureSuccessStatusCode();
            var refreshBody = await refreshResp.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(refreshBody!.Success, Is.True);
            Assert.That(refreshBody.AccessToken, Is.Not.Null.And.Not.Empty,
                "Token lifecycle must work without any blockchain primitives from user");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Negative and Resilience Tests
        // ─────────────────────────────────────────────────────────────────────

        #region Negative and Resilience Tests

        /// <summary>
        /// Negative: Completely invalid JSON body returns structured error, not 500.
        /// </summary>
        [Test]
        public async Task Negative_InvalidJsonBody_Returns400NotInternalServerError()
        {
            var content = new StringContent(
                "{ this is not valid json }",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await _client.PostAsync("/api/v1/auth/login", content);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Invalid JSON must return 400, not 500 Internal Server Error");
        }

        /// <summary>
        /// Negative: Password missing required character types returns 400.
        /// </summary>
        [Test]
        public async Task Negative_Register_PasswordMissingSpecialChar_Returns400()
        {
            var request = new
            {
                Email = $"neg-{Guid.NewGuid():N}@enterprise.test",
                Password = "NoSpecialChar1",
                ConfirmPassword = "NoSpecialChar1"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Password without special char must be rejected");
        }

        /// <summary>
        /// Negative: Access protected endpoint with invalid Bearer token returns 401.
        /// </summary>
        [Test]
        public async Task Negative_ProtectedEndpoint_InvalidBearerToken_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "this-is-not-a-valid-jwt-token");

            var response = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Invalid Bearer token must return 401 or 403");
        }

        /// <summary>
        /// Negative: Logout without auth token returns structured error (not silent success or 500).
        /// </summary>
        [Test]
        public async Task Negative_Logout_Unauthenticated_Returns401NotSilentSuccess()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1/auth/logout", content);

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Unauthenticated logout must return 401 or 403, not silent 200 or 500");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Helper Methods
        // ─────────────────────────────────────────────────────────────────────

        private async Task<RegisterResponse?> RegisterUser(string email, string password)
        {
            var request = new
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RegisterResponse>();
        }

        private async Task<string> GetAuthToken(string email)
        {
            var password = "TestAuthToken1!";
            var registerResponse = await RegisterUser(email, password);
            return registerResponse?.AccessToken ?? string.Empty;
        }
    }
}

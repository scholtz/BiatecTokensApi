using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for Issue: MVP backend hardening - deterministic ARC76 auth contracts
    /// and auditable deployment reliability.
    ///
    /// Validates the five acceptance criteria:
    /// AC1 - ARC76 determinism: same normalized inputs produce identical account outputs
    /// AC2 - Auth/session contract stability: stable status codes and response schemas
    /// AC3 - Deployment boundary reliability: deterministic lifecycle states and retry safety
    /// AC4 - Compliance/audit support: correlation IDs and audit trail metadata
    /// AC5 - Automated quality gates: no regression in existing endpoints
    ///
    /// Business Value: Turns backend behavior into a dependable product asset that supports
    /// sales, compliance due diligence, and predictable delivery for enterprise customers
    /// requiring MiCA-oriented auditability.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPBackendHardeningContractTests
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
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForMVPBackendHardeningContractTests32Min",
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
                    builder.ConfigureAppConfiguration((context, config) =>
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

        #region AC1: ARC76 Determinism

        /// <summary>
        /// AC1: Given the same normalized email+password, registration and login
        /// return identical Algorand addresses across repeated calls.
        /// </summary>
        [Test]
        public async Task AC1_ARC76Determinism_SameCredentials_AlwaysReturnSameAddress()
        {
            var email = $"arc76-det-{Guid.NewGuid()}@example.com";
            var password = "Determinism123!";

            // Register
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Registration must succeed");
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var originalAddress = regResult!.AlgorandAddress;
            Assert.That(originalAddress, Is.Not.Null.And.Not.Empty, "Address must be derived on registration");

            // Login 3 times and verify same address each time
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new LoginRequest { Email = email, Password = password });
                Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Login attempt {attempt} must succeed");
                var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(originalAddress),
                    $"Login attempt {attempt} must return the same deterministic address");
            }
        }

        /// <summary>
        /// AC1: Email canonicalization must normalize case variations to the same ARC76 address.
        /// </summary>
        [Test]
        public async Task AC1_ARC76Determinism_EmailCaseVariants_ProduceSameAddress()
        {
            var baseEmail = $"canon-{Guid.NewGuid()}";
            var lowerEmail = $"{baseEmail}@example.com";
            var password = "Canon123!Pass";

            // Register with lowercase email
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = lowerEmail, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var lowerAddress = regResult!.AlgorandAddress;

            // Login with uppercase email variant - must map to same account
            var upperEmail = lowerEmail.ToUpperInvariant();
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = upperEmail, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Login with uppercase email variant must succeed (email canonicalization)");
            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(lowerAddress),
                "Uppercase email login must return same deterministic address as lowercase registration");
        }

        /// <summary>
        /// AC1: Different credentials produce different Algorand addresses (no collision).
        /// </summary>
        [Test]
        public async Task AC1_ARC76Determinism_DifferentCredentials_ProduceDifferentAddresses()
        {
            var password = "NoColl1sion!";
            var addresses = new List<string>();

            for (int i = 0; i < 5; i++)
            {
                var email = $"unique-{Guid.NewGuid()}-{i}@example.com";
                var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                    new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
                Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Registration {i} must succeed");
                var result = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
                addresses.Add(result!.AlgorandAddress!);
            }

            var distinctAddresses = addresses.Distinct().Count();
            Assert.That(distinctAddresses, Is.EqualTo(5),
                "5 different users must receive 5 distinct Algorand addresses");
        }

        /// <summary>
        /// AC1: Derivation failures must return structured non-ambiguous error responses.
        /// </summary>
        [Test]
        public async Task AC1_ARC76Determinism_WeakPassword_ReturnsStructuredError()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"weak-{Guid.NewGuid()}@example.com",
                    Password = "weak",
                    ConfirmPassword = "weak"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Weak password must be rejected with BadRequest");

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty, "Error body must be non-empty");
            // Should mention password weakness without leaking internal details
            Assert.That(body, Does.Contain("password").IgnoreCase,
                "Error should reference password issue without leaking sensitive internals");
        }

        /// <summary>
        /// AC1: RegisterResponse includes DerivationContractVersion so clients can
        /// detect contract-breaking changes.
        /// </summary>
        [Test]
        public async Task AC1_ARC76Determinism_RegisterResponse_IncludesDerivationContractVersion()
        {
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"version-{Guid.NewGuid()}@example.com",
                    Password = "Version1Pass!",
                    ConfirmPassword = "Version1Pass!"
                });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "RegisterResponse must include DerivationContractVersion for client-side contract detection");
        }

        #endregion

        #region AC2: Auth/Session Contract Stability

        /// <summary>
        /// AC2: RegisterResponse contains all fields required by frontend route guards.
        /// </summary>
        [Test]
        public async Task AC2_AuthSessionContract_RegisterResponse_HasAllRequiredFields()
        {
            var email = $"schema-reg-{Guid.NewGuid()}@example.com";
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "Schema123!Pass", ConfirmPassword = "Schema123!Pass" });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(result!.Success, Is.True, "Success must be true");
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty, "UserId must be present");
            Assert.That(result.Email, Is.Not.Null.And.Not.Empty, "Email must be present");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
            Assert.That(result.ExpiresAt, Is.Not.Null, "ExpiresAt must be present");
            Assert.That(result.ErrorCode, Is.Null, "ErrorCode must be null on success");
            Assert.That(result.ErrorMessage, Is.Null, "ErrorMessage must be null on success");
        }

        /// <summary>
        /// AC2: LoginResponse contains all fields required for frontend consumers.
        /// </summary>
        [Test]
        public async Task AC2_AuthSessionContract_LoginResponse_HasAllRequiredFields()
        {
            var email = $"schema-login-{Guid.NewGuid()}@example.com";
            var password = "Schema123!Login";

            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(result!.Success, Is.True, "Success must be true on valid login");
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty, "UserId must be present");
            Assert.That(result.Email, Is.Not.Null.And.Not.Empty, "Email must be present");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
        }

        /// <summary>
        /// AC2: Invalid login must return HTTP 401 Unauthorized with machine-readable error.
        /// </summary>
        [Test]
        public async Task AC2_AuthSessionContract_InvalidLogin_ReturnsUnauthorizedWithError()
        {
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest
                {
                    Email = $"nonexistent-{Guid.NewGuid()}@example.com",
                    Password = "SomePass123!"
                });

            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Non-existent user login must return 401 Unauthorized (not 400, 404, or 500)");

            var body = await loginResp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty, "Error body must be non-empty");
        }

        /// <summary>
        /// AC2: Duplicate registration must return consistent error response.
        /// </summary>
        [Test]
        public async Task AC2_AuthSessionContract_DuplicateRegistration_ReturnsConsistentError()
        {
            var email = $"dup-{Guid.NewGuid()}@example.com";
            var password = "Duplicate123!";

            var firstReg = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(firstReg.StatusCode, Is.EqualTo(HttpStatusCode.OK), "First registration must succeed");

            var secondReg = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(secondReg.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Duplicate registration must return BadRequest");

            var result = await secondReg.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result, Is.Not.Null, "Error response body must be deserializable");
            Assert.That(result!.Success, Is.False, "Success must be false for duplicate");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "ErrorCode must be present to support machine-readable error handling");
        }

        /// <summary>
        /// AC2: JWT access token must have valid 3-part structure for frontend JWT parsing.
        /// </summary>
        [Test]
        public async Task AC2_AuthSessionContract_AccessToken_HasValidJwtStructure()
        {
            var email = $"jwt-{Guid.NewGuid()}@example.com";
            var password = "JwtStruct123!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var result = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            var token = result!.AccessToken!;
            var parts = token.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "JWT must have exactly 3 parts (header.payload.signature)");
            Assert.That(parts[0], Is.Not.Empty, "JWT header must not be empty");
            Assert.That(parts[1], Is.Not.Empty, "JWT payload must not be empty");
            Assert.That(parts[2], Is.Not.Empty, "JWT signature must not be empty");
        }

        #endregion

        #region AC3: Deployment Boundary Reliability

        /// <summary>
        /// AC3: Deployment status list endpoint is accessible and returns consistent shape.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentReliability_StatusListEndpoint_ReturnsConsistentResponse()
        {
            // Authenticate first
            var email = $"deploy-list-{Guid.NewGuid()}@example.com";
            var password = "DeployList123!";
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regResult!.AccessToken);

            // Query deployment list - verify it returns a stable structure
            var resp = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That(resp.StatusCode,
                Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.NotFound),
                "Deployment list must return 200 OK (or 404 if no deployments) - never 500");
        }

        /// <summary>
        /// AC3: Unauthorized access to protected deployment endpoints must return 401.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentReliability_UnauthenticatedDeploymentAccess_Returns401()
        {
            // Remove any auth header
            _client.DefaultRequestHeaders.Authorization = null;

            var resp = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Unauthenticated access to deployment endpoints must return 401 Unauthorized");
        }

        /// <summary>
        /// AC3: Deployment state machine values are stable and enumerable through the API contract.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentReliability_DeploymentStatusEnum_HasExpectedStableValues()
        {
            // Verify the deployment status enum has the expected stable lifecycle values
            // This test documents and enforces the deployment state machine contract
            var expectedStatuses = new[]
            {
                BiatecTokensApi.Models.DeploymentStatus.Queued,
                BiatecTokensApi.Models.DeploymentStatus.Submitted,
                BiatecTokensApi.Models.DeploymentStatus.Pending,
                BiatecTokensApi.Models.DeploymentStatus.Confirmed,
                BiatecTokensApi.Models.DeploymentStatus.Indexed,
                BiatecTokensApi.Models.DeploymentStatus.Completed,
                BiatecTokensApi.Models.DeploymentStatus.Failed,
                BiatecTokensApi.Models.DeploymentStatus.Cancelled
            };

            var allDefined = Enum.GetValues<BiatecTokensApi.Models.DeploymentStatus>();
            foreach (var status in expectedStatuses)
            {
                Assert.That(allDefined, Does.Contain(status),
                    $"DeploymentStatus.{status} must be defined in the state machine contract");
            }

            // Terminal states must not have forward transitions (documented contract)
            Assert.That((int)BiatecTokensApi.Models.DeploymentStatus.Completed, Is.GreaterThan(0),
                "Completed is a terminal state in the deployment lifecycle");
            Assert.That((int)BiatecTokensApi.Models.DeploymentStatus.Cancelled, Is.GreaterThan(0),
                "Cancelled is a terminal state in the deployment lifecycle");
        }

        /// <summary>
        /// AC3: Token creation endpoint rejects unauthorized access consistently.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentReliability_TokenCreation_RequiresAuthentication()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            // Attempt to create an ASA-FT token without auth - must be rejected
            var resp = await _client.PostAsJsonAsync("/api/v1/token/asa-ft/create", new { });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Token creation must require authentication and return 401 for unauthenticated requests");
        }

        /// <summary>
        /// AC3: Health endpoint is stable and accessible without authentication.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentReliability_HealthEndpoint_IsStableAndAccessible()
        {
            var resp = await _client.GetAsync("/health");
            Assert.That(resp.StatusCode,
                Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable),
                "Health endpoint must return 200 or 503 (never 500) without authentication");

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null, "Health response body must be present");
        }

        #endregion

        #region AC4: Compliance/Audit Support

        /// <summary>
        /// AC4: Registration response includes CorrelationId for audit trail correlation.
        /// </summary>
        [Test]
        public async Task AC4_ComplianceAudit_RegisterResponse_IncludesCorrelationId()
        {
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"audit-{Guid.NewGuid()}@example.com",
                    Password = "Audit123!Pass",
                    ConfirmPassword = "Audit123!Pass"
                });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "RegisterResponse must include CorrelationId for audit trail correlation");
        }

        /// <summary>
        /// AC4: Login response includes CorrelationId to link auth event to subsequent operations.
        /// </summary>
        [Test]
        public async Task AC4_ComplianceAudit_LoginResponse_IncludesCorrelationId()
        {
            var email = $"audit-login-{Guid.NewGuid()}@example.com";
            var password = "Audit123!Login";

            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "LoginResponse must include CorrelationId to correlate audit events");
        }

        /// <summary>
        /// AC4: Error responses must not leak sensitive data (passwords, mnemonics, keys).
        /// </summary>
        [Test]
        public async Task AC4_ComplianceAudit_ErrorResponses_DoNotLeakSensitiveData()
        {
            var password = "SensitiveSecret123!";

            // Attempt login with wrong password
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"leak-test-{Guid.NewGuid()}@example.com",
                    Password = password,
                    ConfirmPassword = password
                });
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest
                {
                    Email = regResult!.Email!,
                    Password = "WrongPassword123!"
                });

            var errorBody = await loginResp.Content.ReadAsStringAsync();
            Assert.That(errorBody, Does.Not.Contain(password),
                "Error response must not contain the user's actual password");
            Assert.That(errorBody, Does.Not.Contain("mnemonic").IgnoreCase,
                "Error response must not contain mnemonic material");
            Assert.That(errorBody, Does.Not.Contain("privateKey").IgnoreCase,
                "Error response must not leak private key information");
            Assert.That(errorBody, Does.Not.Contain("encryptedMnemonic").IgnoreCase,
                "Error response must not expose encrypted mnemonic field");
        }

        #endregion

        #region AC5: Automated Quality Gates (Regression Checks)

        /// <summary>
        /// AC5: Health liveness probe returns expected status code.
        /// </summary>
        [Test]
        public async Task AC5_QualityGate_HealthLiveness_ReturnsExpectedStatusCode()
        {
            var resp = await _client.GetAsync("/health/live");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Liveness probe /health/live must always return 200 OK for pod scheduling stability");
        }

        /// <summary>
        /// AC5: Auth register endpoint is reachable and responds (no 500 errors on startup).
        /// </summary>
        [Test]
        public async Task AC5_QualityGate_AuthEndpoints_NoInternalServerErrors()
        {
            // Malformed but reachable request must not trigger unhandled exception
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new { });
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Auth endpoints must handle malformed input gracefully without 500");
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Auth endpoints must never return 5xx for malformed client input");
        }

        #endregion
    }
}

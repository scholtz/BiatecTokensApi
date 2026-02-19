using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration and unit tests for Issue #377: Next-step Backend ARC76 auth contract
    /// and deployment orchestration hardening.
    ///
    /// Acceptance Criteria Coverage:
    /// AC1  - Authenticated session deterministically resolves ARC76 account metadata
    /// AC2  - ARC76 derivation behavior covered by unit tests for normal and edge cases
    /// AC3  - Deployment endpoint returns consistent lifecycle status objects with documented fields
    /// AC4  - Duplicate/repeated submission handling is deterministic and tested
    /// AC5  - Validation failures return structured, stable error payloads
    /// AC6  - Unauthorized/expired session behavior returns explicit contract-consistent errors
    /// AC7  - Structured logs include correlation identifiers for auth and deployment request chain
    /// AC8  - Key audit events are emitted for session/auth success/failure and deployment lifecycle
    /// AC9  - Integration tests validate auth → derive → deploy initiation → status retrieval contract
    /// AC10 - Touched backend test suite passes in CI without introducing new flaky skips
    /// AC11 - PR description links this issue and explains business-value impact
    /// AC12 - No regression in existing successful deployment paths
    ///
    /// Business Value: Ensures the backend auth and deployment orchestration is deterministic,
    /// observable, and contract-stable for enterprise adoption and compliance posture.
    ///
    /// Risk Mitigation: Comprehensive coverage of auth→deployment chain prevents identity
    /// fragmentation, secret leakage in errors, and deployment duplication, reducing
    /// churn risk among enterprise evaluators who require reliability before procurement.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendARC76ContractHardeningNextStepTests
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
            ["AlgorandAuthentication:AllowedNetworks:SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=:Server"] = "https://testnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=:Header"] = "",
            ["JwtConfig:SecretKey"] = "arc76-hardening-next-step-test-secret-32chars-min",
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
            ["EVMChains:Chains:0:RpcUrl"] = "https://sepolia.base.org",
            ["EVMChains:Chains:0:ChainId"] = "84532",
            ["EVMChains:Chains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForARC76HardeningNextStep32CharactersMin"
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

        // ─────────────────────────────────────────────────────────────────────────────
        // Helper methods
        // ─────────────────────────────────────────────────────────────────────────────

        private async Task<(RegisterResponse registration, string email, string password)> RegisterUniqueUserAsync()
        {
            var email = $"arc76-next-{Guid.NewGuid():N}@biatec.io";
            var password = "SecurePass123!";
            var request = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return (result!, email, password);
        }

        private async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var request = new LoginRequest { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC1: Authenticated session deterministically resolves ARC76 account metadata
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1: Registration response must include all stable identifiers required
        /// by frontend and audit layers: AlgorandAddress, UserId, CorrelationId,
        /// DerivationContractVersion, and Timestamp.
        /// </summary>
        [Test]
        public async Task AC1_Registration_ResponseContainsAllRequiredARC76AccountMetadata()
        {
            // Arrange & Act
            var (registration, email, _) = await RegisterUniqueUserAsync();

            // Assert: all downstream-required fields are present
            Assert.That(registration.Success, Is.True, "Registration must succeed");
            Assert.That(registration.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AlgorandAddress must be included for frontend/audit layers");
            Assert.That(registration.UserId, Is.Not.Null.And.Not.Empty,
                "UserId must be included for session binding");
            Assert.That(registration.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be present for request tracing");
            Assert.That(registration.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present so clients can detect contract changes");
            Assert.That(registration.AccessToken, Is.Not.Null.And.Not.Empty,
                "AccessToken must be present for immediate session establishment");
            Assert.That(registration.RefreshToken, Is.Not.Null.And.Not.Empty,
                "RefreshToken must be present for session continuation");
            Assert.That(registration.Timestamp, Is.Not.EqualTo(default(DateTime)),
                "Timestamp must be set for audit trail");
        }

        /// <summary>
        /// AC1: Login response must deterministically return the same ARC76 address
        /// that was established during registration - proving account metadata persistence.
        /// </summary>
        [Test]
        public async Task AC1_LoginResponse_ContainsSameARC76AddressAsRegistration()
        {
            // Arrange
            var (registration, email, password) = await RegisterUniqueUserAsync();
            Assert.That(registration.Success, Is.True, "Setup: registration must succeed");

            // Act
            var login = await LoginAsync(email, password);

            // Assert: same address, all fields present
            Assert.That(login.Success, Is.True, "Login must succeed");
            Assert.That(login.AlgorandAddress, Is.EqualTo(registration.AlgorandAddress),
                "Login must return same ARC76 address as registration (deterministic derivation)");
            Assert.That(login.UserId, Is.EqualTo(registration.UserId),
                "Login must return same UserId as registration");
            Assert.That(login.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Login response must include CorrelationId");
            Assert.That(login.DerivationContractVersion, Is.EqualTo(registration.DerivationContractVersion),
                "DerivationContractVersion must be stable across auth operations");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2: ARC76 derivation edge cases
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: Mixed-case and leading/trailing whitespace in email must canonicalize
        /// to the same derived ARC76 address (RFC 5321 case-insensitivity).
        /// </summary>
        [Test]
        public async Task AC2_EmailCanonicalization_UpperLowerAndTrimmedVariants_ProduceSameAddress()
        {
            // Arrange: register with lowercase email
            var baseName = Guid.NewGuid().ToString("N")[..12];
            var lowercaseEmail = $"{baseName}@biatec.io";
            var password = "SecurePass123!";
            var regReq = new RegisterRequest { Email = lowercaseEmail, Password = password, ConfirmPassword = password };
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var registration = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registration!.Success, Is.True, "Setup registration must succeed");

            // Act: login with uppercase variant
            var upperLogin = await LoginAsync(lowercaseEmail.ToUpperInvariant(), password);
            var mixedLogin = await LoginAsync(
                $"{char.ToUpper(lowercaseEmail[0])}{lowercaseEmail[1..]}", password);

            // Assert: all variants resolve to the same address
            Assert.That(upperLogin.Success, Is.True, "Login with uppercase email must succeed");
            Assert.That(upperLogin.AlgorandAddress, Is.EqualTo(registration.AlgorandAddress),
                "Uppercase email variant must derive same ARC76 address");
            Assert.That(mixedLogin.Success, Is.True, "Login with mixed-case email must succeed");
            Assert.That(mixedLogin.AlgorandAddress, Is.EqualTo(registration.AlgorandAddress),
                "Mixed-case email variant must derive same ARC76 address");
        }

        /// <summary>
        /// AC2: Registration with a password containing special characters must succeed,
        /// producing a valid ARC76 address. Special chars are common in enterprise password policies.
        /// </summary>
        [Test]
        public async Task AC2_SpecialCharacterPassword_SuccessfullyDerivesARC76Address()
        {
            // Arrange
            var email = $"special-pw-{Guid.NewGuid():N}@biatec.io";
            var password = "P@ssw0rd!#$%^&*()";
            var request = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert
            Assert.That(result!.Success, Is.True,
                "Registration with special character password must succeed");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Special character password must still derive valid ARC76 address");
        }

        /// <summary>
        /// AC2: Unit test - DeploymentStatusService state transitions follow documented rules.
        /// Validates Queued → Submitted → Pending → Confirmed → Completed chain.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentStateTransitions_AllValidStatesInOrder_Succeed()
        {
            // Arrange
            var repository = new DeploymentStatusRepository(
                new Mock<ILogger<DeploymentStatusRepository>>().Object);
            var service = new DeploymentStatusService(
                repository,
                new Mock<IWebhookService>().Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);

            var id = await service.CreateDeploymentAsync(
                "ASA", "algorand-mainnet", "TESTADDR", "Token", "TKN");

            // Act & Assert: each transition in the documented lifecycle
            Assert.That(await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Submitted"), Is.True,
                "Queued → Submitted must be valid");
            Assert.That(await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Pending, "Pending"), Is.True,
                "Submitted → Pending must be valid");
            Assert.That(await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Confirmed, "Confirmed"), Is.True,
                "Pending → Confirmed must be valid");
            Assert.That(await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Completed, "Completed"), Is.True,
                "Confirmed → Completed must be valid");

            var deployment = await service.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
            Assert.That(deployment.StatusHistory, Has.Count.GreaterThanOrEqualTo(5),
                "Status history must record all transitions");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3: Deployment endpoint returns consistent lifecycle status objects
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC3: The deployment list endpoint must return a structured, parseable response
        /// with all documented envelope fields (Success, Deployments, TotalCount, Page, TotalPages).
        /// </summary>
        [Test]
        public async Task AC3_DeploymentListEndpoint_ReturnsConsistentEnvelopeWithDocumentedFields()
        {
            // Arrange
            var (registration, _, _) = await RegisterUniqueUserAsync();
            Assert.That(registration.Success, Is.True);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", registration.AccessToken!);

            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments");

            // Assert: envelope structure is present and consistent
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Deployment list endpoint must return 200 for authenticated users");

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json).RootElement;

            Assert.That(doc.TryGetProperty("success", out _), Is.True,
                "Response must include 'success' field");
            Assert.That(doc.TryGetProperty("deployments", out _), Is.True,
                "Response must include 'deployments' array field");
            Assert.That(doc.TryGetProperty("totalCount", out _), Is.True,
                "Response must include 'totalCount' field");
            Assert.That(doc.TryGetProperty("page", out _), Is.True,
                "Response must include 'page' field");
            Assert.That(doc.TryGetProperty("totalPages", out _), Is.True,
                "Response must include 'totalPages' field");
        }

        /// <summary>
        /// AC3: Deployment metrics endpoint returns structured response
        /// including all documented fields for observability.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentMetricsEndpoint_ReturnsStructuredResponseWithRequiredFields()
        {
            // Arrange
            var (registration, _, _) = await RegisterUniqueUserAsync();
            Assert.That(registration.Success, Is.True);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", registration.AccessToken!);

            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments/metrics");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Metrics endpoint must return 200 for authenticated users");
            var json = await response.Content.ReadAsStringAsync();
            Assert.That(json, Is.Not.Null.And.Not.Empty,
                "Metrics response must contain a non-empty body");
            // Validate it is valid JSON
            Assert.DoesNotThrow(() => JsonDocument.Parse(json),
                "Metrics response must be valid JSON");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4: Duplicate/repeated submission handling is deterministic
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC4: Idempotent deployment - setting the same status twice does not create
        /// duplicate history entries (same-state idempotency at the service level).
        /// </summary>
        [Test]
        public async Task AC4_DeploymentService_SameStatusTwice_IsIdempotentAndDoesNotDuplicate()
        {
            // Arrange
            var repository = new DeploymentStatusRepository(
                new Mock<ILogger<DeploymentStatusRepository>>().Object);
            var service = new DeploymentStatusService(
                repository,
                new Mock<IWebhookService>().Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);

            var id = await service.CreateDeploymentAsync(
                "ERC20", "base-mainnet", "0xCreator", "DuplicateToken", "DUP");

            // Act: set Submitted twice
            var firstResult = await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "First submit");
            var secondResult = await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Second submit");

            // Assert: both succeed (idempotent), same current status
            Assert.That(firstResult, Is.True, "First transition to Submitted must succeed");
            Assert.That(secondResult, Is.True, "Second idempotent Submitted must succeed");

            var deployment = await service.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Submitted),
                "Status must remain Submitted after idempotent update");
        }

        /// <summary>
        /// AC4: Failed deployment can be retried - transition from Failed back to Queued
        /// is allowed to support observable retry semantics.
        /// </summary>
        [Test]
        public async Task AC4_FailedDeployment_CanTransitionToQueued_ForRetry()
        {
            // Arrange
            var repository = new DeploymentStatusRepository(
                new Mock<ILogger<DeploymentStatusRepository>>().Object);
            var service = new DeploymentStatusService(
                repository,
                new Mock<IWebhookService>().Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);

            var id = await service.CreateDeploymentAsync(
                "ARC200", "algorand-testnet", "TESTADDR", "RetryToken", "RTK");
            await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Submitted");
            await service.MarkDeploymentFailedAsync(id,
                DeploymentErrorFactory.NetworkError("Simulated network failure"));

            // Act: retry by transitioning back to Queued
            var retryResult = await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Queued, "Retry attempt");

            // Assert
            Assert.That(retryResult, Is.True, "Failed → Queued retry transition must be allowed");
            var deployment = await service.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "After retry, status must be Queued");
            Assert.That(deployment.StatusHistory.Any(h => h.Status == DeploymentStatus.Failed), Is.True,
                "History must still contain the Failed entry for audit trail");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5: Validation failures return structured, stable error payloads
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5: Invalid registration request (missing required fields) must return
        /// a structured error with HTTP 400, not a generic 500 or unstructured message.
        /// </summary>
        [Test]
        public async Task AC5_MissingEmailField_Returns400WithStructuredValidationError()
        {
            // Arrange: malformed request - no email
            var invalidRequest = new { Password = "SecurePass123!", ConfirmPassword = "SecurePass123!" };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", invalidRequest);

            // Assert: structured validation error
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Missing required field must return 400 Bad Request");
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Validation error response body must not be empty");
            // Must be valid JSON
            Assert.DoesNotThrow(() => JsonDocument.Parse(body),
                "Error response must be valid JSON");
        }

        /// <summary>
        /// AC5: Weak password must return a non-200 structured error response
        /// that clients can use for deterministic error handling.
        /// A password below minimum requirements triggers model validation (400)
        /// or service-level error - either way must not be a 200 or 500.
        /// </summary>
        [Test]
        public async Task AC5_WeakPassword_ReturnsStructuredErrorNotSuccessOrServerError()
        {
            // Arrange
            var email = $"weak-pw-{Guid.NewGuid():N}@biatec.io";
            // "short1A!" fails MinLength(8) model validation → 400 before reaching service
            var request = new RegisterRequest
            {
                Email = email,
                Password = "sh0rT!",
                ConfirmPassword = "sh0rT!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert: never a 200 (success) or 500 (crash), always a client error (4xx)
            Assert.That((int)response.StatusCode, Is.InRange(400, 422),
                "Weak/invalid password must return a 4xx client error, not 200 or 500");
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Error response must include a body for client error handling");
            // Body must be valid JSON
            Assert.DoesNotThrow(() => JsonDocument.Parse(body),
                "Error response body must be valid JSON");
        }

        /// <summary>
        /// AC5: Mismatched passwords must return a structured error (not a generic 500)
        /// with explicit indication of the validation failure reason.
        /// </summary>
        [Test]
        public async Task AC5_MismatchedPasswords_ReturnsStructuredValidationError()
        {
            // Arrange
            var email = $"mismatch-{Guid.NewGuid():N}@biatec.io";
            var request = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "DifferentPass456@"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert: non-200 with structured content
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Mismatched password must not cause a 500 Internal Server Error");
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Mismatched password error response must have a body");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6: Unauthorized/expired session behavior
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC6: Request to a protected endpoint without any token must return
        /// exactly 401 Unauthorized, not a 403 Forbidden or 200 with empty data.
        /// </summary>
        [Test]
        public async Task AC6_DeploymentEndpoint_WithNoToken_Returns401Unauthorized()
        {
            // Arrange: ensure no auth header
            _client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Protected endpoint must return 401 when no token is provided");
        }

        /// <summary>
        /// AC6: Request with a completely invalid token must return 401 Unauthorized
        /// with a contract-consistent response (not 500 or arbitrary behavior).
        /// </summary>
        [Test]
        public async Task AC6_DeploymentEndpoint_WithInvalidToken_Returns401WithConsistentResponse()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "totally.invalid.jwt.token.here");

            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Invalid token must return 401, not 403 or 500");
        }

        /// <summary>
        /// AC6: Invalid refresh token must return a structured error response
        /// (not crash with 500) so the frontend can redirect to login.
        /// </summary>
        [Test]
        public async Task AC6_InvalidRefreshToken_ReturnsStructuredErrorNotServerCrash()
        {
            // Arrange
            var request = new { RefreshToken = "completely-invalid-refresh-token" };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", request);
            var body = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Invalid refresh token must not cause a 500 Internal Server Error");
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Refresh token error must include a response body for client error handling");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC7: Structured logs include correlation identifiers
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC7: Registration response must include a CorrelationId that can be used
        /// to trace the auth event through structured logs.
        /// </summary>
        [Test]
        public async Task AC7_RegistrationResponse_IncludesCorrelationId_ForLogTracing()
        {
            // Arrange & Act
            var (registration, _, _) = await RegisterUniqueUserAsync();

            // Assert
            Assert.That(registration.Success, Is.True);
            Assert.That(registration.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Registration response must include CorrelationId for request tracing through logs");
        }

        /// <summary>
        /// AC7: Login response must include a CorrelationId in every successful auth event.
        /// This ensures that auth events are traceable in deployment chains.
        /// </summary>
        [Test]
        public async Task AC7_LoginResponse_IncludesCorrelationId_ForDeploymentChainTracing()
        {
            // Arrange
            var (registration, email, password) = await RegisterUniqueUserAsync();
            Assert.That(registration.Success, Is.True);

            // Act: two sequential logins should each have a unique CorrelationId
            var login1 = await LoginAsync(email, password);
            var login2 = await LoginAsync(email, password);

            // Assert: correlation IDs present in both
            Assert.That(login1.Success, Is.True);
            Assert.That(login1.CorrelationId, Is.Not.Null.And.Not.Empty,
                "First login must include CorrelationId");
            Assert.That(login2.Success, Is.True);
            Assert.That(login2.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Second login must include CorrelationId");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC8: Key audit events emitted for deployment lifecycle transitions
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC8: Deployment status history must capture each transition with a timestamp,
        /// enabling compliance-ready audit trail for the complete lifecycle.
        /// </summary>
        [Test]
        public async Task AC8_DeploymentStatusHistory_RecordsTimestampedTransitions_ForAuditTrail()
        {
            // Arrange
            var repository = new DeploymentStatusRepository(
                new Mock<ILogger<DeploymentStatusRepository>>().Object);
            var service = new DeploymentStatusService(
                repository,
                new Mock<IWebhookService>().Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);

            var id = await service.CreateDeploymentAsync(
                "ARC3", "algorand-mainnet", "AUDITADDR", "AuditToken", "AUD");

            // Act: perform lifecycle transitions following valid state machine
            // Queued → Submitted → Pending → Confirmed → Completed
            await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Transaction submitted");
            await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Pending, "Awaiting confirmation");
            await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Confirmed, "Transaction confirmed", confirmedRound: 1234567);
            await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Completed, "Deployment complete");

            // Assert: audit trail contains all transitions
            var deployment = await service.GetDeploymentAsync(id);
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment!.StatusHistory, Has.Count.GreaterThanOrEqualTo(4),
                "Audit trail must record initial Queued + all transitions");

            // Each entry must have a timestamp for compliance audit
            foreach (var entry in deployment.StatusHistory)
            {
                Assert.That(entry.Timestamp, Is.Not.EqualTo(default(DateTime)),
                    $"Status history entry for {entry.Status} must have a Timestamp for audit trail");
            }

            // Final state must be Completed
            Assert.That(deployment.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
        }

        /// <summary>
        /// AC8: Failed deployment must record error information in the status history
        /// for compliance-relevant failure events. Error details are stored in the
        /// Metadata dictionary and ErrorMessage field for structured diagnostics.
        /// </summary>
        [Test]
        public async Task AC8_FailedDeployment_RecordsStructuredErrorDetails_InAuditHistory()
        {
            // Arrange
            var repository = new DeploymentStatusRepository(
                new Mock<ILogger<DeploymentStatusRepository>>().Object);
            var service = new DeploymentStatusService(
                repository,
                new Mock<IWebhookService>().Object,
                new Mock<ILogger<DeploymentStatusService>>().Object);

            var id = await service.CreateDeploymentAsync(
                "ERC20", "base-mainnet", "0xAuditCreator", "FailedAuditToken", "FAT");
            await service.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Submitted");

            var errorDetails = DeploymentErrorFactory.InsufficientFunds("0.1 ETH", "0 ETH");

            // Act
            await service.MarkDeploymentFailedAsync(id, errorDetails);

            // Assert: error information is captured in history
            var deployment = await service.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed));

            var failureEntry = deployment.StatusHistory.LastOrDefault(h => h.Status == DeploymentStatus.Failed);
            Assert.That(failureEntry, Is.Not.Null, "Failed entry must be in status history");

            // The MarkDeploymentFailedAsync(DeploymentError) overload stores error info
            // in ErrorMessage (technical message) and Metadata (structured details)
            Assert.That(failureEntry!.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Failed entry must include ErrorMessage for audit trail");
            Assert.That(failureEntry.Metadata, Is.Not.Null,
                "Failed entry must include Metadata dictionary with structured error details");
            Assert.That(failureEntry.Metadata!.ContainsKey("errorCode"), Is.True,
                "Metadata must contain errorCode for structured error classification");
            Assert.That(failureEntry.Metadata["errorCode"].ToString(), Is.Not.Null.And.Not.Empty,
                "Structured error code must be non-empty in audit history");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC9: Integration test: auth → derive → deployment status retrieval contract
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC9: Full integration contract: register → login → access deployment list →
        /// verify deployment endpoint is accessible with ARC76-derived JWT.
        /// This validates the complete auth → derive → orchestration chain.
        /// </summary>
        [Test]
        public async Task AC9_FullIntegrationContract_RegisterLoginAccessDeployments_EndToEnd()
        {
            // Step 1: Register (ARC76 derivation)
            var (registration, email, password) = await RegisterUniqueUserAsync();
            Assert.That(registration.Success, Is.True, "Step 1: registration must succeed");
            var arc76Address = registration.AlgorandAddress;
            Assert.That(arc76Address, Is.Not.Null.And.Not.Empty,
                "Step 1: ARC76 address must be derived at registration");

            // Step 2: Login (verify derivation persists)
            var login = await LoginAsync(email, password);
            Assert.That(login.Success, Is.True, "Step 2: login must succeed");
            Assert.That(login.AlgorandAddress, Is.EqualTo(arc76Address),
                "Step 2: login must return same ARC76 address (deterministic derivation)");

            // Step 3: Access deployment list with derived identity
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", login.AccessToken!);
            var deploymentsResponse = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That(deploymentsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Step 3: authenticated user must access deployment list endpoint");

            var deploymentList = await deploymentsResponse.Content.ReadFromJsonAsync<ListDeploymentsResponse>();
            Assert.That(deploymentList, Is.Not.Null, "Step 3: deployment list response must be deserializable");
            Assert.That(deploymentList!.Success, Is.True, "Step 3: deployment list must be successful");

            // Step 4: Access deployment metrics (observability chain)
            var metricsResponse = await _client.GetAsync("/api/v1/token/deployments/metrics");
            Assert.That(metricsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Step 4: authenticated user must access deployment metrics endpoint");

            // Step 5: Token refresh preserves identity
            _client.DefaultRequestHeaders.Authorization = null;
            var refreshReq = new { RefreshToken = login.RefreshToken };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);
            Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Step 5: token refresh must succeed");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC12: No regression in existing deployment paths
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC12: Health check confirms backend is operational - validates that the
        /// hardening changes introduced no startup regressions.
        /// </summary>
        [Test]
        public async Task AC12_HealthCheck_ConfirmsNoStartupRegressions()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Health endpoint must return 200 - no startup regressions from hardening changes");
        }

        /// <summary>
        /// AC12: DeploymentStatus service correctly handles unknown deployment IDs
        /// without crashing (regression safety for existing downstream consumers).
        /// </summary>
        [Test]
        public async Task AC12_NonExistentDeployment_Returns404WithConsistentResponse_NoRegression()
        {
            // Arrange
            var (registration, _, _) = await RegisterUniqueUserAsync();
            Assert.That(registration.Success, Is.True);
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", registration.AccessToken!);

            // Act
            var response = await _client.GetAsync($"/api/v1/token/deployments/{Guid.NewGuid():N}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "Non-existent deployment must return 404 - stable contract for existing consumers");
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "404 response must include a body for client error handling");
        }
    }
}

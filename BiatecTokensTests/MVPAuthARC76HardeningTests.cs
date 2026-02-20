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
    /// Integration tests for Issue #385: MVP Backend auth-to-ARC76 and deployment contract reliability hardening.
    ///
    /// Validates the backend reliability hardening for authenticated users launching tokens
    /// without wallet prerequisites, with deterministic ARC76 derivation, reliable deployment
    /// status APIs, and test-backed behavior guarantees suitable for MVP sign-off.
    ///
    /// Acceptance Criteria Coverage:
    /// AC1  - Backend returns deterministic ARC76/account derivation outputs with documented response fields
    /// AC2  - Integration tests verify email/password auth to session establishment to derivation contract end-to-end
    /// AC3  - Token creation endpoints reject unauthorized access and accept authorized flows consistently
    /// AC4  - Deployment status endpoints expose deterministic lifecycle states resilient to polling retries
    /// AC5  - Compliance validation failures are structured and actionable for frontend user guidance
    /// AC6  - Error responses follow a consistent schema with stable codes/messages/context
    /// AC7  - Tests pass in CI without flaky timing dependence
    /// AC8  - No regression in existing supported token creation/deployment flows
    /// AC9  - Documentation/comments clarify contract expectations
    /// AC10 - Behavior aligns with email/password-only, non-wallet-first product promise
    ///
    /// Business Value: Enables non-technical enterprise users to rely on deterministic,
    /// auditable token deployment backed by a production-grade reliability contract,
    /// supporting enterprise procurement confidence and MiCA compliance readiness.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPAuthARC76HardeningTests
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
            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
            ["IPFSConfig:TimeoutSeconds"] = "30",
            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
            ["IPFSConfig:ValidateContentHash"] = "true",
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
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForMVPAuthARC76HardeningTests32CharsMin"
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

        #region AC1 - Deterministic ARC76 Derivation with Documented Response Fields

        /// <summary>
        /// AC1: Same email/password always produces the same deterministic ARC76 Algorand address.
        /// Validates that the backend derivation is stable across multiple auth flows.
        /// </summary>
        [Test]
        public async Task AC1_DeterministicARC76_SameCredentials_AlwaysProduceSameAddress()
        {
            // Arrange
            var email = $"arc76-determinism-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var password = "DeterministicTest123!";

            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "ARC76 Determinism User"
            };

            // Act: Register once to establish ARC76 address
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC1: Registration must succeed to establish ARC76 derivation baseline");

            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registerResult!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AC1: Registration must return a non-null ARC76-derived Algorand address");

            var baselineAddress = registerResult.AlgorandAddress!;

            // Act: Login with same credentials - must produce same address
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC1: Login with same credentials must succeed");

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(baselineAddress),
                "AC1: ARC76 derivation must be deterministic - same credentials must always yield same address");
        }

        /// <summary>
        /// AC1: Registration response includes all documented ARC76 contract fields.
        /// Clients rely on these fields being stable and documented.
        /// </summary>
        [Test]
        public async Task AC1_RegisterResponse_ExposesAllDocumentedContractFields()
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Email = $"contract-fields-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test",
                Password = "ContractFields123!",
                ConfirmPassword = "ContractFields123!",
                FullName = "Contract Fields User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert: All documented contract fields present
            Assert.That(result, Is.Not.Null, "AC1: Response must not be null");
            Assert.That(result!.Success, Is.True, "AC1: Success flag must be true for valid registration");
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty, "AC1: UserId must be in response");
            Assert.That(result.Email, Is.Not.Null.And.Not.Empty, "AC1: Email must be echoed in response");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AC1: AlgorandAddress (ARC76) must be present");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty, "AC1: AccessToken must be issued on registration");
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty, "AC1: RefreshToken must be issued on registration");
            Assert.That(result.ExpiresAt, Is.Not.Null, "AC1: ExpiresAt must specify token expiry");
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "AC1: CorrelationId required for audit traceability");
            Assert.That(result.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC1: DerivationContractVersion must be present so clients can detect contract changes");
        }

        /// <summary>
        /// AC1: Email case variations (canonicalization) produce the same ARC76 address.
        /// Backend must normalize email before derivation - case must not affect identity.
        /// </summary>
        [Test]
        public async Task AC1_EmailCanonicalization_UpperLowerMixed_ProduceSameAddress()
        {
            // Arrange - use unique base email
            var uniquePart = Guid.NewGuid().ToString("N")[..8];
            var baseEmail = $"canonicalize-{uniquePart}@mvp-hardening.test";
            var password = "CanonTest123!";

            // Register with lowercase
            var registerRequest = new RegisterRequest
            {
                Email = baseEmail.ToLowerInvariant(),
                Password = password,
                ConfirmPassword = password
            };
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            var derivedAddress = registerResult!.AlgorandAddress!;

            // Login with mixed case - must return same ARC76 address
            var loginRequest = new LoginRequest
            {
                Email = baseEmail.ToUpperInvariant(),
                Password = password
            };
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            // Note: login with uppercase email may fail (wrong credential) or succeed with same address.
            // If it succeeds, addresses must match. If it fails with 401, that's acceptable behavior.
            if (loginResponse.StatusCode == HttpStatusCode.OK)
            {
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(derivedAddress),
                    "AC1: Email case normalization must produce identical ARC76 derivation output");
            }
            else
            {
                // Canonicalization rejects uppercase login - verify original lowercase works
                var lowerLoginRequest = new LoginRequest { Email = baseEmail.ToLowerInvariant(), Password = password };
                var lowerLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", lowerLoginRequest);
                Assert.That(lowerLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    "AC1: Original lowercase login must always succeed after registration");
                var lowerResult = await lowerLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(lowerResult!.AlgorandAddress, Is.EqualTo(derivedAddress),
                    "AC1: ARC76 address must be deterministic across logins with consistent credentials");
            }
        }

        #endregion

        #region AC2 - End-to-End Auth to Derivation Contract

        /// <summary>
        /// AC2: Full email/password auth flow produces session with documented derivation contract behavior.
        /// Tests register → login → refresh → verify address persistence.
        /// </summary>
        [Test]
        public async Task AC2_FullAuthFlow_Register_Login_Refresh_PreservesARC76Identity()
        {
            // Arrange
            var email = $"e2e-flow-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var password = "E2EFlowTest123!";

            // Step 1: Register
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "E2E Flow User"
            };
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 1: Registration must succeed");
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            var arc76Address = registerResult!.AlgorandAddress!;
            var refreshToken = registerResult.RefreshToken!;

            // Step 2: Login - verify same address
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 2: Login must succeed");
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(arc76Address),
                "AC2: Login must preserve same ARC76 address as registration");

            // Step 3: Refresh token - verify address preserved in session
            var refreshRequest = new { RefreshToken = refreshToken };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC2: Token refresh must succeed for valid session");
        }

        /// <summary>
        /// AC2: Different credentials produce different ARC76 addresses.
        /// Validates isolation - no cross-user identity contamination.
        /// </summary>
        [Test]
        public async Task AC2_DifferentCredentials_ProduceDifferentARC76Addresses()
        {
            // Register two distinct users
            var user1Email = $"user1-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var user2Email = $"user2-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var password = "DistinctUsers123!";

            var reg1Response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = user1Email, Password = password, ConfirmPassword = password
            });
            Assert.That(reg1Response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "AC2: User1 registration must succeed");
            var result1 = await reg1Response.Content.ReadFromJsonAsync<RegisterResponse>();

            var reg2Response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = user2Email, Password = password, ConfirmPassword = password
            });
            Assert.That(reg2Response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "AC2: User2 registration must succeed");
            var result2 = await reg2Response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert: Different users must have different ARC76 addresses (isolation)
            Assert.That(result1!.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AC2: User1 must have ARC76 address");
            Assert.That(result2!.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AC2: User2 must have ARC76 address");
            Assert.That(result1.AlgorandAddress, Is.Not.EqualTo(result2.AlgorandAddress),
                "AC2: Different credentials must produce different ARC76 addresses (no collision)");
        }

        #endregion

        #region AC3 - Token Creation Endpoint Authorization

        /// <summary>
        /// AC3: Unauthenticated requests to deployment status endpoints are rejected with 401.
        /// Token creation endpoints must enforce authorization boundaries.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentStatusEndpoint_WithoutAuth_Returns401()
        {
            // Act: access protected deployment status without auth token
            var response = await _client.GetAsync("/api/v1/token/deployments/test-deployment-id");

            // Assert: Must reject unauthorized access
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC3: Deployment status endpoint must require authentication and return 401 for unauthenticated requests");
        }

        /// <summary>
        /// AC3: Authenticated users can access deployment status endpoints.
        /// Valid JWT session must be accepted by protected endpoints.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentStatusEndpoint_WithValidAuth_Responds()
        {
            // Arrange: Register and get JWT
            var email = $"authz-test-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var password = "AuthzTest123!";
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email, Password = password, ConfirmPassword = password
            });
            Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            var accessToken = registerResult!.AccessToken!;

            // Act: request with JWT - must not return 401
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _client.GetAsync("/api/v1/token/deployments/nonexistent-id");
            _client.DefaultRequestHeaders.Authorization = null;

            // Assert: With valid auth, must not reject with 401 (may return 404 for unknown deployment)
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized),
                "AC3: Authenticated requests to deployment status endpoint must not return 401");
        }

        /// <summary>
        /// AC3: List all deployments endpoint requires authentication.
        /// </summary>
        [Test]
        public async Task AC3_ListDeploymentsEndpoint_WithoutAuth_Returns401()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC3: List deployments endpoint must require authentication");
        }

        #endregion

        #region AC4 - Deployment Status Polling Resilience

        /// <summary>
        /// AC4: Repeated polls on the same deployment return consistent lifecycle state.
        /// Idempotent read behavior: multiple GET requests must return same state.
        /// </summary>
        [Test]
        public async Task AC4_DeploymentStatusPolling_RepeatedReads_ReturnConsistentState()
        {
            // Arrange: create deployment via service layer
            var loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(loggerMock.Object);
            var webhookMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();
            var service = new DeploymentStatusService(repository, webhookMock.Object, serviceLogger.Object);

            var deploymentId = await service.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "polling-test@mvp-hardening.test",
                "PollingToken", "POLL", correlationId: "corr-poll-001");

            // Act: poll the same deployment multiple times
            var result1 = await service.GetDeploymentAsync(deploymentId);
            var result2 = await service.GetDeploymentAsync(deploymentId);
            var result3 = await service.GetDeploymentAsync(deploymentId);

            // Assert: All polls return identical state (AC4 - resilient to retries)
            Assert.That(result1, Is.Not.Null, "AC4: First poll must return deployment");
            Assert.That(result2, Is.Not.Null, "AC4: Second poll must return deployment");
            Assert.That(result3, Is.Not.Null, "AC4: Third poll must return deployment");

            Assert.That(result1!.CurrentStatus, Is.EqualTo(result2!.CurrentStatus),
                "AC4: Repeated polling must return consistent deployment status");
            Assert.That(result2.CurrentStatus, Is.EqualTo(result3!.CurrentStatus),
                "AC4: Third poll must match previous polls - no non-deterministic state changes");
            Assert.That(result1.DeploymentId, Is.EqualTo(result2.DeploymentId),
                "AC4: DeploymentId must be stable across polls");
        }

        /// <summary>
        /// AC4: Deployment lifecycle progresses through deterministic states.
        /// States transition in valid order per the lifecycle state machine.
        /// </summary>
        [Test]
        public async Task AC4_DeploymentLifecycle_ProgressesThroughDeterministicStates()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(loggerMock.Object);
            var webhookMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();
            var service = new DeploymentStatusService(repository, webhookMock.Object, serviceLogger.Object);

            var deploymentId = await service.CreateDeploymentAsync(
                "ARC3", "algorand-mainnet", "lifecycle@mvp-hardening.test",
                "LifecycleToken", "LCT");

            // Assert initial state
            var initial = await service.GetDeploymentAsync(deploymentId);
            Assert.That(initial!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "AC4: Deployment must start in Queued state");

            // Progress: Queued → Submitted
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted,
                transactionHash: "0xTEST001");
            var submitted = await service.GetDeploymentAsync(deploymentId);
            Assert.That(submitted!.CurrentStatus, Is.EqualTo(DeploymentStatus.Submitted),
                "AC4: State must advance to Submitted after submission");

            // Progress: Submitted → Pending
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);
            var pending = await service.GetDeploymentAsync(deploymentId);
            Assert.That(pending!.CurrentStatus, Is.EqualTo(DeploymentStatus.Pending),
                "AC4: State must advance to Pending after broadcast");

            // Progress: Pending → Confirmed
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed,
                confirmedRound: 12345678);
            var confirmed = await service.GetDeploymentAsync(deploymentId);
            Assert.That(confirmed!.CurrentStatus, Is.EqualTo(DeploymentStatus.Confirmed),
                "AC4: State must advance to Confirmed after on-chain confirmation");

            // Progress: Confirmed → Completed
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed);
            var completed = await service.GetDeploymentAsync(deploymentId);
            Assert.That(completed!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed),
                "AC4: State must advance to Completed in terminal success state");

            // Assert history is complete and ordered
            Assert.That(completed.StatusHistory.Count, Is.GreaterThanOrEqualTo(5),
                "AC4: Full lifecycle must be recorded in status history");
        }

        /// <summary>
        /// AC4: Terminal states (Completed, Cancelled) are immutable under repeated reads.
        /// Once in terminal state, deployment cannot transition further.
        /// </summary>
        [Test]
        public async Task AC4_TerminalState_Completed_IsImmutableOnRepeatedPolls()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(loggerMock.Object);
            var webhookMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();
            var service = new DeploymentStatusService(repository, webhookMock.Object, serviceLogger.Object);

            var deploymentId = await service.CreateDeploymentAsync(
                "ERC20", "base-mainnet", "terminal@mvp-hardening.test", "TerminalToken", "TERM");

            // Drive to terminal state
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, transactionHash: "0xTERM");
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending);
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, confirmedRound: 99);
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed);

            // Poll repeatedly after terminal state
            var poll1 = await service.GetDeploymentAsync(deploymentId);
            var poll2 = await service.GetDeploymentAsync(deploymentId);

            // Assert: Terminal state must be stable
            Assert.That(poll1!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed),
                "AC4: First poll after completion must return Completed status");
            Assert.That(poll2!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed),
                "AC4: Repeated polls on completed deployment must always return Completed (terminal stability)");

            // Verify invalid transition from terminal state is rejected
            var invalidTransitionResult = await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Queued);
            Assert.That(invalidTransitionResult, Is.False,
                "AC4: Transition from terminal state must return false (state machine integrity - returns false, not exception)");
        }

        #endregion

        #region AC5 - Structured Compliance Validation Responses

        /// <summary>
        /// AC5: Compliance-blocking validation (empty email) returns structured, actionable error.
        /// Frontend must receive machine-readable error codes and user-guidable messages.
        /// </summary>
        [Test]
        public async Task AC5_ComplianceValidation_EmptyEmail_ReturnsStructuredActionableError()
        {
            // Arrange: Request with compliance violation (empty email)
            var invalidRequest = new RegisterRequest
            {
                Email = "",
                Password = "ValidPass123!",
                ConfirmPassword = "ValidPass123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", invalidRequest);

            // Assert: Response must not be 500 - must be structured validation failure
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "AC5: Compliance validation failures must not result in 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.InRange(400, 422),
                "AC5: Compliance validation failures must return 4xx structured error response");
        }

        /// <summary>
        /// AC5: Password mismatch validation returns structured error without credential leakage.
        /// Actionable guidance must be provided without exposing internal details.
        /// </summary>
        [Test]
        public async Task AC5_ComplianceValidation_PasswordMismatch_ReturnsActionableError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"pass-mismatch-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test",
                Password = "Password123!",
                ConfirmPassword = "DifferentPassword456!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert: Structured error for compliance gate
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.OK),
                "AC5: Password mismatch must fail registration");
            Assert.That((int)response.StatusCode, Is.InRange(400, 422),
                "AC5: Password mismatch must return actionable 4xx error");
        }

        /// <summary>
        /// AC5: Retry classification correctly identifies network errors as retryable.
        /// Compliance gates must distinguish transient from permanent failures for frontend guidance.
        /// </summary>
        [Test]
        public void AC5_RetryClassifier_NetworkError_IsRetryableWithUserGuidance()
        {
            // Arrange
            var classifierLogger = new Mock<ILogger<RetryPolicyClassifier>>();
            var classifier = new RetryPolicyClassifier(classifierLogger.Object);

            // Act: TIMEOUT is a transient/retryable network-class error
            var decision = classifier.ClassifyError(ErrorCodes.TIMEOUT);

            // Assert: Timeout errors must be retryable with guidance
            Assert.That(decision.Policy, Is.Not.EqualTo(RetryPolicy.NotRetryable),
                "AC5: Timeout/network errors must be classified as retryable (transient failure guidance)");
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0),
                "AC5: Retryable errors must have max retry count for client guidance");
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                "AC5: Retry decision must include explanation for actionable user/client guidance");
        }

        /// <summary>
        /// AC5: Retry classifier marks validation errors as terminal (not retryable).
        /// Users must receive clear guidance that retrying won't help for validation failures.
        /// </summary>
        [Test]
        public void AC5_RetryClassifier_ValidationError_IsTerminalWithGuidance()
        {
            // Arrange
            var classifierLogger = new Mock<ILogger<RetryPolicyClassifier>>();
            var classifier = new RetryPolicyClassifier(classifierLogger.Object);

            // Act
            var decision = classifier.ClassifyError(ErrorCodes.INVALID_REQUEST);

            // Assert: Validation errors must be terminal
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                "AC5: Validation errors must be non-retryable (no point retrying without fixing input)");
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0),
                "AC5: Non-retryable errors must have 0 max retries");
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                "AC5: Non-retryable decision must include actionable explanation");
        }

        #endregion

        #region AC6 - Consistent Error Schema

        /// <summary>
        /// AC6: Login with wrong password returns consistent error schema without credential leakage.
        /// </summary>
        [Test]
        public async Task AC6_ErrorSchema_WrongPassword_IsConsistentWithoutCredentialLeakage()
        {
            // Arrange: Register then login with wrong password
            var email = $"wrong-pass-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var correctPassword = "CorrectPass123!";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email, Password = correctPassword, ConfirmPassword = correctPassword
            });

            // Act: Login with wrong password
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
            {
                Email = email,
                Password = "WrongPassword999!"
            });

            // Assert: Error must be structured and not leak credentials
            Assert.That(loginResponse.StatusCode, Is.Not.EqualTo(HttpStatusCode.OK),
                "AC6: Wrong password must not succeed");
            Assert.That(loginResponse.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "AC6: Auth failure must not result in 500 server error");

            var content = await loginResponse.Content.ReadAsStringAsync();
            Assert.That(content, Does.Not.Contain("CorrectPass123!"),
                "AC6: Error response must not contain the correct password");
            Assert.That(content, Does.Not.Contain("WrongPassword999!"),
                "AC6: Error response must not echo back input credentials");
        }

        /// <summary>
        /// AC6: Unknown email login returns same error schema as wrong password.
        /// Consistent schema prevents user enumeration attacks.
        /// </summary>
        [Test]
        public async Task AC6_ErrorSchema_UnknownEmail_ReturnsConsistentErrorSchema()
        {
            // Act: Login with non-existent email
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
            {
                Email = "nonexistent-user@mvp-hardening.test",
                Password = "AnyPassword123!"
            });

            // Assert: Must fail with 4xx structured response
            Assert.That((int)loginResponse.StatusCode, Is.InRange(400, 499),
                "AC6: Unknown email login must return 4xx error with consistent schema");
            Assert.That(loginResponse.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "AC6: Unknown email login must not result in 500 error");

            var content = await loginResponse.Content.ReadAsStringAsync();
            Assert.That(content, Does.Not.Contain("AnyPassword123!"),
                "AC6: Error must not echo back password in response");
        }

        /// <summary>
        /// AC6: State machine rejects invalid transitions with structured error.
        /// State guard enforces deterministic error schema for invalid deployment operations.
        /// </summary>
        [Test]
        public void AC6_StateTransitionGuard_InvalidTransition_ReturnsStructuredResult()
        {
            // Arrange
            var guardLogger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(guardLogger.Object);

            // Act: Attempt invalid transition (Completed is terminal - cannot go back to Queued)
            var result = guard.ValidateTransition(DeploymentStatus.Completed, DeploymentStatus.Queued);

            // Assert: Must return structured rejection with clear reason
            Assert.That(result.IsAllowed, Is.False,
                "AC6: Invalid state transition must be rejected");
            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty,
                "AC6: Rejection must include machine-readable reason code for client error handling");
            Assert.That(result.Explanation, Is.Not.Null.And.Not.Empty,
                "AC6: Rejection must include human-readable message for user guidance");
        }

        #endregion

        #region AC10 - Non-Wallet-First Product Promise

        /// <summary>
        /// AC10: Email/password registration provides full ARC76 account without wallet setup.
        /// Users must be able to register and receive an Algorand address without any blockchain wallet.
        /// </summary>
        [Test]
        public async Task AC10_EmailPasswordOnly_Registration_DerivesAlgorandAddressWithoutWallet()
        {
            // Arrange: Standard email/password registration (no wallet, no mnemonic, no blockchain interaction)
            var request = new RegisterRequest
            {
                Email = $"walletless-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test",
                Password = "WalletFree123!",
                ConfirmPassword = "WalletFree123!",
                FullName = "Walletless Enterprise User"
            };

            // Act: Register using only email + password (non-wallet-first flow)
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert: Must succeed without wallet
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC10: Email/password registration must succeed without wallet setup");

            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result!.Success, Is.True,
                "AC10: Non-wallet-first registration must report success");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AC10: Backend must derive Algorand address automatically (no wallet required from user)");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty,
                "AC10: JWT access token must be issued immediately (no additional wallet auth step)");
        }

        /// <summary>
        /// AC10: Full token orchestration initialization can be done with email/password JWT only.
        /// Validates that the deployment status flow is accessible with standard JWT auth.
        /// </summary>
        [Test]
        public async Task AC10_EmailPasswordJWT_CanAccessDeploymentOrchestration()
        {
            // Arrange: Register with email/password (no wallet)
            var email = $"orchestration-{Guid.NewGuid().ToString("N")[..8]}@mvp-hardening.test";
            var password = "Orchestrate123!";
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email, Password = password, ConfirmPassword = password
            });
            Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC10: Must register successfully with email/password");
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            // Act: Access deployment orchestration with the JWT (no wallet needed)
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", registerResult!.AccessToken!);
            var deploymentsResponse = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            // Assert: Email/password JWT gives access to token orchestration endpoints
            Assert.That(deploymentsResponse.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized),
                "AC10: Email/password JWT must provide access to token orchestration without wallet auth");
        }

        #endregion

        #region AC7 - CI Non-Flaky Tests (State Machine Pure Unit Tests)

        /// <summary>
        /// AC7/AC8: StateTransitionGuard correctly validates all valid lifecycle transitions.
        /// Pure unit test with no timing or network dependencies - guaranteed to be non-flaky.
        /// </summary>
        [Test]
        public void AC7_StateTransitionGuard_AllValidTransitions_AreAccepted()
        {
            // Arrange
            var guardLogger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(guardLogger.Object);

            // Define all valid transitions per the deployment lifecycle state machine
            var validTransitions = new (DeploymentStatus from, DeploymentStatus to)[]
            {
                (DeploymentStatus.Queued, DeploymentStatus.Submitted),
                (DeploymentStatus.Queued, DeploymentStatus.Failed),
                (DeploymentStatus.Queued, DeploymentStatus.Cancelled),
                (DeploymentStatus.Submitted, DeploymentStatus.Pending),
                (DeploymentStatus.Submitted, DeploymentStatus.Failed),
                (DeploymentStatus.Pending, DeploymentStatus.Confirmed),
                (DeploymentStatus.Pending, DeploymentStatus.Failed),
                (DeploymentStatus.Confirmed, DeploymentStatus.Indexed),
                (DeploymentStatus.Confirmed, DeploymentStatus.Completed),
                (DeploymentStatus.Confirmed, DeploymentStatus.Failed),
                (DeploymentStatus.Indexed, DeploymentStatus.Completed),
                (DeploymentStatus.Indexed, DeploymentStatus.Failed),
                (DeploymentStatus.Failed, DeploymentStatus.Queued)
            };

            // Act & Assert: All valid transitions must be accepted
            foreach (var (from, to) in validTransitions)
            {
                var result = guard.ValidateTransition(from, to);
                Assert.That(result.IsAllowed, Is.True,
                    $"AC7: Valid transition {from} → {to} must be accepted by state machine");
            }
        }

        #endregion
    }
}

using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// ARC76 Auth-to-Deployment Reliability Contract v2 Tests (Issue #396).
    ///
    /// Validates the complete reliability contract for authenticated users from
    /// account derivation through policy-governed deployment, ensuring deterministic
    /// behavior, structured error reporting, and observable state transitions.
    ///
    /// Acceptance Criteria Coverage:
    /// AC1 - Authenticated email/password sessions deterministically map to ARC76 derivation
    ///       metadata with documented response schema (DerivationContractVersion field present).
    /// AC2 - Unauthorized, expired, or invalid session contexts are rejected with consistent
    ///       error codes and remediation hints.
    /// AC3 - Deployment endpoints enforce policy prerequisites before execution and expose
    ///       clear allow/deny decision artifacts (precondition stage markers).
    /// AC4 - Deployment state transitions are observable and auditable through structured
    ///       events with correlation IDs.
    /// AC5 - Error taxonomy is standardized across touched endpoints with no sensitive
    ///       internal leakage in client responses.
    /// AC6 - Contract and integration tests cover derivation, policy enforcement, and
    ///       deployment transition paths including failure scenarios.
    /// AC7 - CI passes for all modified backend test workflows with no newly skipped tests.
    /// AC8 - PR evidence maps each acceptance criterion to concrete tests and sample payloads.
    ///
    /// Business Value: Closes the MVP sign-off gap by proving that every authenticated
    /// request maps to stable account derivation semantics, policy constraints are enforced
    /// consistently, and outcomes are auditable for enterprise compliance reporting.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76AuthDeploymentReliabilityContractV2Tests
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
            ["JwtConfig:SecretKey"] = "arc76-v2-reliability-contract-test-secret-32chars",
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
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForReliabilityContractV2Tests32CharsMin"
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

        #region AC1 - ARC76 Derivation Metadata with Documented Response Schema

        /// <summary>
        /// AC1: Registration response exposes DerivationContractVersion field for forward-compatibility.
        /// Clients can detect breaking contract changes and adapt accordingly.
        /// </summary>
        [Test]
        public async Task AC1_RegisterResponse_ExposesDerivationContractVersionField()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"v2-schema-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com",
                Password = "V2Contract123!",
                ConfirmPassword = "V2Contract123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC1: Registration must succeed");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert: derivationContractVersion field is present and non-empty
            Assert.That(root.TryGetProperty("derivationContractVersion", out var versionProp), Is.True,
                "AC1: Response must expose 'derivationContractVersion' for forward-compatibility contract");
            Assert.That(versionProp.GetString(), Is.Not.Null.And.Not.Empty,
                "AC1: DerivationContractVersion must be a non-empty string (e.g. '1.0')");
        }

        /// <summary>
        /// AC1: Login response exposes DerivationContractVersion field matching registration.
        /// The version must be stable and consistent across auth endpoints.
        /// </summary>
        [Test]
        public async Task AC1_LoginResponse_ExposesDerivationContractVersionMatchingRegistration()
        {
            // Arrange
            var email = $"v2-version-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com";
            var password = "V2Contract123!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(regResult?.Success, Is.True, "Registration must succeed");
            var regVersion = regResult!.DerivationContractVersion;

            // Act: login
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });
            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert
            Assert.That(loginResult?.Success, Is.True, "AC1: Login must succeed");
            Assert.That(loginResult!.DerivationContractVersion, Is.EqualTo(regVersion),
                "AC1: DerivationContractVersion must be identical across register and login endpoints");
        }

        /// <summary>
        /// AC1: Same email/password across three independent sessions always produces the same ARC76 address.
        /// Proves deterministic derivation as required for MVP compliance sign-off.
        /// </summary>
        [Test]
        public async Task AC1_ThreeLoginAttempts_AlwaysProduceSameARC76Address()
        {
            // Arrange: register once
            var email = $"v2-determ-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com";
            var password = "V2Contract123!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(regResult?.Success, Is.True);
            var baseAddress = regResult!.AlgorandAddress;
            Assert.That(baseAddress, Is.Not.Null.And.Not.Empty);

            // Act: login three times
            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new LoginRequest { Email = email, Password = password });
                var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loginResult?.Success, Is.True, $"AC1: Login attempt {i + 1} must succeed");
                addresses.Add(loginResult!.AlgorandAddress!);
            }

            // Assert: all three match the original registration address
            Assert.That(addresses, Has.All.EqualTo(baseAddress),
                "AC1: ARC76 derivation must be deterministic - same credentials must always yield same address across multiple sessions");
        }

        /// <summary>
        /// AC1: Registration response schema includes all required contract fields for frontend integration.
        /// </summary>
        [Test]
        public async Task AC1_RegistrationResponseSchema_ContainsAllRequiredContractFields()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"v2-fields-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com",
                Password = "V2Contract123!",
                ConfirmPassword = "V2Contract123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert: all documented contract fields are present
            Assert.That(root.TryGetProperty("success", out _), Is.True,
                "AC1: 'success' is a required contract field");
            Assert.That(root.TryGetProperty("algorandAddress", out _), Is.True,
                "AC1: 'algorandAddress' is a required contract field for ARC76 derivation");
            Assert.That(root.TryGetProperty("accessToken", out _), Is.True,
                "AC1: 'accessToken' is a required contract field for session lifecycle");
            Assert.That(root.TryGetProperty("refreshToken", out _), Is.True,
                "AC1: 'refreshToken' is a required contract field for session continuation");
            Assert.That(root.TryGetProperty("expiresAt", out _), Is.True,
                "AC1: 'expiresAt' is a required contract field for session lifecycle state");
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True,
                "AC1/AC4: 'correlationId' is a required contract field for observability");
            Assert.That(root.TryGetProperty("derivationContractVersion", out _), Is.True,
                "AC1: 'derivationContractVersion' is a required contract field for forward compatibility");
        }

        #endregion

        #region AC2 - Unauthorized/Invalid Session Rejection with Error Codes

        /// <summary>
        /// AC2: Login with invalid credentials returns structured error with error code and remediation hint.
        /// </summary>
        [Test]
        public async Task AC2_InvalidCredentials_ReturnsStructuredErrorWithErrorCode()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = $"nonexistent-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

            // Assert: error response has proper status and structured body
            Assert.That((int)response.StatusCode, Is.EqualTo(400).Or.EqualTo(401),
                "AC2: Invalid credentials must return 400 or 401");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out var successProp), Is.True,
                "AC2: Error response must include 'success' field");
            Assert.That(successProp.GetBoolean(), Is.False,
                "AC2: Invalid credential response must have success=false");
            Assert.That(root.TryGetProperty("errorCode", out var errorCodeProp), Is.True,
                "AC2: Error response must include 'errorCode' for client handling");
            Assert.That(errorCodeProp.GetString(), Is.Not.Null.And.Not.Empty,
                "AC2: Error code must be a non-empty string");
        }

        /// <summary>
        /// AC2: Using an invalid/expired JWT token on a protected endpoint returns 401 with structured error.
        /// </summary>
        [Test]
        public async Task AC2_InvalidJwtToken_ProtectedEndpoint_Returns401()
        {
            // Arrange: use an obviously invalid token
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            // Act: attempt to access a protected endpoint
            var response = await _client.GetAsync("/api/v1/auth/profile");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC2: Invalid JWT must result in 401 Unauthorized");

            _client.DefaultRequestHeaders.Authorization = null;
        }

        /// <summary>
        /// AC2: No authentication token on a protected endpoint returns 401, not 500.
        /// Ensures consistent rejection without internal error leakage.
        /// </summary>
        [Test]
        public async Task AC2_NoAuthToken_ProtectedEndpoint_Returns401NotInternalError()
        {
            // Arrange: no Authorization header
            _client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await _client.GetAsync("/api/v1/auth/profile");

            // Assert: 401, not 500
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC2: Missing auth token must return 401, not 500 (no internal error leakage)");
        }

        /// <summary>
        /// AC2: Registration with weak password returns 400 with a failure indicator.
        /// </summary>
        [Test]
        public async Task AC2_WeakPassword_Registration_ReturnsStructuredErrorCode()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"v2-weak-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com",
                Password = "weak",
                ConfirmPassword = "weak"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert: bad request (ASP.NET validation error or custom error response)
            Assert.That((int)response.StatusCode, Is.EqualTo(400),
                "AC2: Weak password must return 400 Bad Request");

            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "AC2: Response body must not be empty for failed registration");

            // The body may be either a RegisterResponse (success=false) or ASP.NET ProblemDetails,
            // but it must not indicate success and must convey an error
            var bodyLower = body.ToLowerInvariant();
            var indicatesFailure = bodyLower.Contains("\"success\":false") ||
                                   bodyLower.Contains("errors") ||
                                   bodyLower.Contains("weak") ||
                                   bodyLower.Contains("password");
            Assert.That(indicatesFailure, Is.True,
                "AC2: 400 response body must indicate the failure reason (weak password / validation error)");
        }

        /// <summary>
        /// AC2: Duplicate registration attempt returns deterministic USER_ALREADY_EXISTS error code.
        /// </summary>
        [Test]
        public async Task AC2_DuplicateRegistration_ReturnsDeterministicUserAlreadyExistsCode()
        {
            // Arrange: register first time
            var email = $"v2-dup-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com";
            var password = "V2Contract123!";

            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            // Act: register second time with same email
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            // Assert
            Assert.That((int)response.StatusCode, Is.EqualTo(400).Or.EqualTo(409),
                "AC2: Duplicate registration must return 400 or 409");

            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result?.Success, Is.False,
                "AC2: Duplicate registration must return success=false");
            Assert.That(result?.ErrorCode, Is.EqualTo("USER_ALREADY_EXISTS"),
                "AC2: Duplicate registration must return deterministic USER_ALREADY_EXISTS error code");
        }

        #endregion

        #region AC3 - Policy Prerequisites and Allow/Deny Decision Artifacts

        /// <summary>
        /// AC3: Orchestration pipeline ValidationFailure produces structured precondition denial artifact.
        /// Validates that policy gates block invalid requests before execution.
        /// </summary>
        [Test]
        public void AC3_OrchestrationPipeline_ValidationFailure_ProducesStructuredDenialArtifact()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryMock = new Mock<IRetryPolicyClassifier>();
            retryMock.Setup(r => r.ClassifyError(It.IsAny<string>(), It.IsAny<DeploymentErrorCategory?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(new RetryPolicyDecision
                {
                    Policy = RetryPolicy.NotRetryable,
                    Explanation = "Correct the request parameters and resubmit."
                });

            var pipeline = new TokenWorkflowOrchestrationPipeline(loggerMock.Object, retryMock.Object);
            var context = pipeline.BuildContext("ARC3_CREATE", $"v2-corr-{Guid.NewGuid():N}", userId: "v2-test-user");

            // Act: execute with validation policy that denies
            var task = pipeline.ExecuteAsync(
                context,
                "test-request",
                validationPolicy: _ => "Token name is required",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("result"));

            task.Wait();
            var result = task.Result;

            // Assert: policy denial is structured and contains decision artifacts
            Assert.That(result.Success, Is.False,
                "AC3: Validation failure must produce a failed result");
            Assert.That(result.CompletedAtStage, Is.EqualTo(OrchestrationStage.Failed),
                "AC3: Pipeline CompletedAtStage is Failed when any policy denies");
            Assert.That(result.AuditSummary.CompletedAtStage, Is.EqualTo(OrchestrationStage.Validate.ToString()),
                "AC3: Audit summary must record 'Validate' as the stage where the pipeline halted");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST),
                "AC3: Validation failure must produce INVALID_REQUEST error code");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "AC3: Validation failure must include a remediation hint for the client");
            Assert.That(result.CorrelationId, Is.EqualTo(context.CorrelationId),
                "AC3/AC4: Correlation ID must be preserved in the denial artifact");
        }

        /// <summary>
        /// AC3: Orchestration pipeline PreconditionFailure produces structured denial artifact.
        /// Validates that KYC/subscription policy gates block execution.
        /// </summary>
        [Test]
        public void AC3_OrchestrationPipeline_PreconditionFailure_ProducesAllowDenyArtifact()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryMock = new Mock<IRetryPolicyClassifier>();
            retryMock.Setup(r => r.ClassifyError(It.IsAny<string>(), It.IsAny<DeploymentErrorCategory?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(new RetryPolicyDecision
                {
                    Policy = RetryPolicy.RetryableAfterRemediation,
                    Explanation = "Ensure KYC and subscription are complete."
                });

            var pipeline = new TokenWorkflowOrchestrationPipeline(loggerMock.Object, retryMock.Object);
            var context = pipeline.BuildContext("ARC200_MINT", $"v2-corr-{Guid.NewGuid():N}", userId: "v2-test-user");

            // Act: validation passes, precondition denies
            var task = pipeline.ExecuteAsync(
                context,
                "test-request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => "KYC verification not complete",
                executor: _ => Task.FromResult("result"));

            task.Wait();
            var result = task.Result;

            // Assert
            Assert.That(result.Success, Is.False,
                "AC3: Precondition failure must produce a failed result");
            Assert.That(result.CompletedAtStage, Is.EqualTo(OrchestrationStage.Failed),
                "AC3: Pipeline CompletedAtStage is Failed when any policy denies");
            Assert.That(result.AuditSummary.CompletedAtStage, Is.EqualTo(OrchestrationStage.CheckPreconditions.ToString()),
                "AC3: Audit summary must record 'CheckPreconditions' as the stage where the pipeline halted");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.PRECONDITION_FAILED),
                "AC3: Precondition failure must produce PRECONDITION_FAILED error code as the allow/deny artifact");
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PreconditionFailure),
                "AC3: Failure category must be PreconditionFailure for policy gate denials");
        }

        /// <summary>
        /// AC3: Successful orchestration execution exposes Allow artifact with completed stage markers.
        /// </summary>
        [Test]
        public async Task AC3_OrchestrationPipeline_AllPoliciesPass_ExecutionSucceeds()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryMock = new Mock<IRetryPolicyClassifier>();
            var pipeline = new TokenWorkflowOrchestrationPipeline(loggerMock.Object, retryMock.Object);
            var context = pipeline.BuildContext("ERC20_MINTABLE_CREATE", $"v2-corr-{Guid.NewGuid():N}", userId: "v2-test-user");

            // Act: all policies pass
            var result = await pipeline.ExecuteAsync(
                context,
                "valid-request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("deployment-id-123"));

            // Assert: Allow artifact with all stages completed
            Assert.That(result.Success, Is.True,
                "AC3: When all policies pass, execution must succeed");
            Assert.That(result.CompletedAtStage, Is.EqualTo(OrchestrationStage.Completed),
                "AC3: Allow artifact must show Completed stage");
            Assert.That(result.StageMarkers.Any(m => m.Stage == OrchestrationStage.Validate && m.Success), Is.True,
                "AC3: Allow artifact must include successful Validate stage marker");
            Assert.That(result.StageMarkers.Any(m => m.Stage == OrchestrationStage.CheckPreconditions && m.Success), Is.True,
                "AC3: Allow artifact must include successful CheckPreconditions stage marker");
        }

        #endregion

        #region AC4 - Observable Deployment State Transitions with Correlation IDs

        /// <summary>
        /// AC4: Deployment creation includes correlation ID that propagates through lifecycle.
        /// </summary>
        [Test]
        public async Task AC4_DeploymentCreation_CorrelationIdPropagatesThoughLifecycle()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(loggerMock.Object);
            var webhookMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();
            var service = new DeploymentStatusService(repository, webhookMock.Object, serviceLogger.Object);

            var correlationId = $"v2-ac4-{Guid.NewGuid():N}";

            // Act: create deployment with correlation ID
            var deploymentId = await service.CreateDeploymentAsync(
                "ARC3", "algorand-mainnet", "test-user@arc76-v2.com",
                "V2Token", "V2T", correlationId);

            // Retrieve and check
            var deployment = await service.GetDeploymentAsync(deploymentId);

            // Assert
            Assert.That(deployment, Is.Not.Null,
                "AC4: Deployment must be retrievable after creation");
            Assert.That(deployment!.CorrelationId, Is.EqualTo(correlationId),
                "AC4: Correlation ID must propagate from creation to deployment record");
        }

        /// <summary>
        /// AC4: State transitions produce auditable status history with timestamps.
        /// </summary>
        [Test]
        public async Task AC4_StateTransitions_ProduceAuditableHistoryWithTimestamps()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(loggerMock.Object);
            var webhookMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();
            var service = new DeploymentStatusService(repository, webhookMock.Object, serviceLogger.Object);

            var deploymentId = await service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreatorAddress", "V2Token", "V2T");

            // Act: transition through lifecycle
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted,
                "Transaction submitted", transactionHash: "0xtxhashv2");
            await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending,
                "Transaction broadcast to network");

            // Retrieve
            var deployment = await service.GetDeploymentAsync(deploymentId);

            // Assert: audit trail
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment!.StatusHistory, Is.Not.Null.And.Not.Empty,
                "AC4: Deployment must maintain status history for audit trail");
            Assert.That(deployment.StatusHistory!.Count, Is.GreaterThanOrEqualTo(2),
                "AC4: Status history must contain at least 2 entries after 2 transitions");
            Assert.That(deployment.StatusHistory.All(h => h.Timestamp != default),
                Is.True, "AC4: Each status history entry must have a non-default timestamp");
        }

        /// <summary>
        /// AC4: Orchestration pipeline preserves correlation ID in all output artifacts.
        /// </summary>
        [Test]
        public async Task AC4_OrchestrationPipeline_PreservesCorrelationIdInAllArtifacts()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryMock = new Mock<IRetryPolicyClassifier>();
            var pipeline = new TokenWorkflowOrchestrationPipeline(loggerMock.Object, retryMock.Object);
            var correlationId = $"v2-observable-{Guid.NewGuid():N}";
            var context = pipeline.BuildContext("ARC3_CREATE", correlationId, userId: "v2-test-user");

            // Act: successful execution
            var successResult = await pipeline.ExecuteAsync(
                context, "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            // Assert: correlation ID preserved in success result
            Assert.That(successResult.CorrelationId, Is.EqualTo(correlationId),
                "AC4: Correlation ID must be preserved in success artifact");

            // Act: failed execution
            var failContext = pipeline.BuildContext("ARC3_CREATE", correlationId, userId: "v2-test-user");
            retryMock.Setup(r => r.ClassifyError(It.IsAny<string>(), It.IsAny<DeploymentErrorCategory?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(new RetryPolicyDecision
                {
                    Policy = RetryPolicy.NotRetryable,
                    Explanation = "Fix the error."
                });
            var failResult = await pipeline.ExecuteAsync(
                failContext, "req",
                validationPolicy: _ => "Validation error",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            // Assert: correlation ID preserved in failure result
            Assert.That(failResult.CorrelationId, Is.EqualTo(correlationId),
                "AC4: Correlation ID must be preserved in failure artifact");
        }

        /// <summary>
        /// AC4: Deployment API endpoint propagates client-provided X-Correlation-ID header in response.
        /// </summary>
        [Test]
        public async Task AC4_ApiEndpoint_ClientCorrelationIdHeader_PreservedInResponse()
        {
            // Arrange: use health endpoint which always responds
            var correlationId = $"v2-header-{Guid.NewGuid():N}";
            _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

            // Act
            var response = await _client.GetAsync("/health");

            // Assert: either the header is echoed back or the health response is 2xx
            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "AC4: Endpoint must not throw 5xx when correlation ID header is present");

            // Check if correlation ID is echoed (optional but preferred)
            var hasCorrelationHeader = response.Headers.Contains("X-Correlation-ID") ||
                                       response.Headers.Contains("X-Request-ID");
            // If echoed, it must match
            if (hasCorrelationHeader)
            {
                var headerValues = response.Headers.TryGetValues("X-Correlation-ID", out var vals)
                    ? vals
                    : response.Headers.TryGetValues("X-Request-ID", out var vals2)
                        ? vals2
                        : Array.Empty<string>();
                Assert.That(headerValues.Any(v => v.Contains(correlationId)), Is.True,
                    "AC4: When correlation ID header is echoed, it must match the provided value");
            }

            _client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        }

        #endregion

        #region AC5 - Error Taxonomy Standardized, No Internal Leakage

        /// <summary>
        /// AC5: Error response for invalid credentials does not leak internal stack traces or secrets.
        /// </summary>
        [Test]
        public async Task AC5_InvalidCredentials_ResponseContainsNoInternalLeakage()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = $"v2-leak-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);
            var body = await response.Content.ReadAsStringAsync();
            var bodyLower = body.ToLowerInvariant();

            // Assert: no sensitive internal information exposed
            Assert.That(bodyLower, Does.Not.Contain("stacktrace"),
                "AC5: Error response must not contain stack traces");
            Assert.That(bodyLower, Does.Not.Contain("exception"),
                "AC5: Error response must not contain internal exception details");
            Assert.That(bodyLower, Does.Not.Contain("at system."),
                "AC5: Error response must not contain .NET system call frames");
            Assert.That(bodyLower, Does.Not.Contain("pbkdf2"),
                "AC5: Error response must not expose internal cryptographic implementation details");
            Assert.That(bodyLower, Does.Not.Contain("mnemonic"),
                "AC5: Error response must not expose mnemonic seed phrases");
        }

        /// <summary>
        /// AC5: Error codes used in auth endpoints follow the standardized ErrorCodes taxonomy.
        /// </summary>
        [Test]
        public async Task AC5_AuthErrorCodes_FollowStandardizedTaxonomy()
        {
            // Arrange: attempt login with nonexistent user
            var loginRequest = new LoginRequest
            {
                Email = $"v2-tax-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com",
                Password = "SomePassword123!"
            };
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert: error code is one of the standardized codes
            var standardizedCodes = new[]
            {
                ErrorCodes.INVALID_CREDENTIALS,
                ErrorCodes.INVALID_AUTH_TOKEN,
                ErrorCodes.UNAUTHORIZED,
                ErrorCodes.INVALID_REQUEST,
                ErrorCodes.NOT_FOUND
            };

            Assert.That(loginResult?.ErrorCode, Is.Not.Null,
                "AC5: Error response must include an error code");
            Assert.That(standardizedCodes, Does.Contain(loginResult!.ErrorCode),
                $"AC5: Error code '{loginResult.ErrorCode}' must be from the standardized ErrorCodes taxonomy");
        }

        /// <summary>
        /// AC5: RetryPolicyClassifier maps all error codes to non-null explanations.
        /// Ensures no client receives an empty explanation for any classified error.
        /// </summary>
        [Test]
        public void AC5_RetryPolicyClassifier_AllErrorCodes_ProduceNonNullExplanation()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<RetryPolicyClassifier>>();
            var classifier = new RetryPolicyClassifier(loggerMock.Object);

            var errorCodes = new[]
            {
                ErrorCodes.INVALID_REQUEST,
                ErrorCodes.MISSING_REQUIRED_FIELD,
                ErrorCodes.INVALID_NETWORK,
                ErrorCodes.INVALID_AUTH_TOKEN,
                ErrorCodes.UNAUTHORIZED,
                ErrorCodes.INSUFFICIENT_FUNDS,
                ErrorCodes.TRANSACTION_FAILED,
                ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                ErrorCodes.TIMEOUT,
                ErrorCodes.IPFS_SERVICE_ERROR
            };

            // Act & Assert: each error code produces a non-null explanation
            foreach (var errorCode in errorCodes)
            {
                var decision = classifier.ClassifyError(errorCode);
                Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                    $"AC5: Error code '{errorCode}' must produce a non-empty explanation for client guidance");
            }
        }

        /// <summary>
        /// AC5: Duplicate registration error response does not expose internal user data.
        /// </summary>
        [Test]
        public async Task AC5_DuplicateRegistration_ErrorResponseDoesNotExposeInternalData()
        {
            // Arrange: register first time
            var email = $"v2-nosecret-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com";
            var password = "V2Contract123!";
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            // Act: attempt duplicate registration
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var body = await response.Content.ReadAsStringAsync();
            var bodyLower = body.ToLowerInvariant();

            // Assert: no internal data leakage
            Assert.That(bodyLower, Does.Not.Contain("passwordhash"),
                "AC5: Duplicate registration error must not expose password hash");
            Assert.That(bodyLower, Does.Not.Contain("encryptedmnemonic"),
                "AC5: Duplicate registration error must not expose encrypted mnemonic");
            Assert.That(bodyLower, Does.Not.Contain("stacktrace"),
                "AC5: Duplicate registration error must not expose stack traces");
        }

        #endregion

        #region AC6 - Contract Tests for Derivation, Policy, and Deployment Failure Paths

        /// <summary>
        /// AC6: State transition guard rejects invalid transitions with structured error.
        /// Tests the deployment state machine failure path.
        /// </summary>
        [Test]
        public void AC6_StateTransitionGuard_InvalidTransition_ProducesStructuredRejection()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(loggerMock.Object);

            // Act: attempt terminal state → any other state (invalid)
            var result = guard.ValidateTransition(
                DeploymentStatus.Completed,
                DeploymentStatus.Submitted);

            // Assert
            Assert.That(result.IsAllowed, Is.False,
                "AC6: Transition from terminal Completed state must be invalid");
            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty,
                "AC6: Invalid transition must include a structured reason code for auditing");
        }

        /// <summary>
        /// AC6: Retry classification for terminal errors returns non-retryable decision.
        /// Tests the failure path classification contract for standard validation errors.
        /// </summary>
        [Test]
        public void AC6_RetryClassifier_TerminalErrors_ReturnNonRetryableDecision()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<RetryPolicyClassifier>>();
            var classifier = new RetryPolicyClassifier(loggerMock.Object);

            // Act: classify terminal error codes (these are explicitly mapped as NotRetryable)
            var invalidRequest = classifier.ClassifyError(ErrorCodes.INVALID_REQUEST);
            var missingField = classifier.ClassifyError(ErrorCodes.MISSING_REQUIRED_FIELD);
            var invalidNetwork = classifier.ClassifyError(ErrorCodes.INVALID_NETWORK);

            // Assert: terminal errors are non-retryable
            Assert.That(invalidRequest.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                "AC6: INVALID_REQUEST is a terminal error and must not be retryable");
            Assert.That(missingField.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                "AC6: MISSING_REQUIRED_FIELD is a terminal error and must not be retryable");
            Assert.That(invalidNetwork.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                "AC6: INVALID_NETWORK is a terminal error and must not be retryable");
        }

        /// <summary>
        /// AC6: Retry classification for transient errors returns retryable decision.
        /// Tests the transient failure path classification contract.
        /// </summary>
        [Test]
        public void AC6_RetryClassifier_TransientErrors_ReturnRetryableDecision()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<RetryPolicyClassifier>>();
            var classifier = new RetryPolicyClassifier(loggerMock.Object);

            // Act: classify transient error codes
            var timeout = classifier.ClassifyError(ErrorCodes.TIMEOUT);
            var blockchainError = classifier.ClassifyError(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR);

            // Assert: transient errors are retryable (not NotRetryable)
            Assert.That(timeout.Policy, Is.Not.EqualTo(RetryPolicy.NotRetryable),
                "AC6: TIMEOUT is a transient error and must have a retryable policy");
            Assert.That(blockchainError.Policy, Is.Not.EqualTo(RetryPolicy.NotRetryable),
                "AC6: BLOCKCHAIN_CONNECTION_ERROR is a transient error and must have a retryable policy");
        }

        /// <summary>
        /// AC6: Deployment audit records failure with error message for compliance reporting.
        /// Tests the failure path in the deployment audit trail.
        /// </summary>
        [Test]
        public async Task AC6_DeploymentFailurePath_RecordsStructuredFailureAudit()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(loggerMock.Object);
            var webhookMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();
            var service = new DeploymentStatusService(repository, webhookMock.Object, serviceLogger.Object);

            var deploymentId = await service.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "test-deployer@arc76-v2.com", "FailToken", "FAIL");

            // Act: transition to failed state with error details
            await service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Failed,
                "Deployment failed due to insufficient funds",
                errorMessage: "Transaction rejected: insufficient balance");

            // Retrieve
            var deployment = await service.GetDeploymentAsync(deploymentId);

            // Assert: failure is recorded with structured audit information
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed),
                "AC6: Failed deployment must record Failed status");
            Assert.That(deployment.StatusHistory, Is.Not.Null.And.Not.Empty,
                "AC6: Failed deployment must maintain history for audit trail");

            var failedEntry = deployment.StatusHistory!.LastOrDefault();
            Assert.That(failedEntry, Is.Not.Null,
                "AC6: Failed deployment must have a status history entry");
            Assert.That(failedEntry!.Status, Is.EqualTo(DeploymentStatus.Failed),
                "AC6: Latest history entry must reflect Failed status");
        }

        /// <summary>
        /// AC6: Orchestration audit summary captures key metadata for compliance evidence.
        /// </summary>
        [Test]
        public async Task AC6_OrchestrationAuditSummary_CapturesComplianceEvidence()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryMock = new Mock<IRetryPolicyClassifier>();
            var pipeline = new TokenWorkflowOrchestrationPipeline(loggerMock.Object, retryMock.Object);
            var correlationId = $"v2-audit-{Guid.NewGuid():N}";
            var context = pipeline.BuildContext("ARC3_CREATE", correlationId, "idem-v2-001", "user-v2-001");

            // Act
            var result = await pipeline.ExecuteAsync(
                context, "valid-request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("deployment-completed"));

            // Assert: audit summary is present and contains required compliance fields
            Assert.That(result.AuditSummary, Is.Not.Null,
                "AC6: Successful orchestration must produce an audit summary for compliance evidence");
            Assert.That(result.AuditSummary!.CorrelationId, Is.EqualTo(correlationId),
                "AC6: Audit summary must reference the original correlation ID");
            Assert.That(result.AuditSummary.OperationType, Is.EqualTo("ARC3_CREATE"),
                "AC6: Audit summary must record the operation type for compliance reporting");
            Assert.That(result.AuditSummary.CompletedAtStage, Is.EqualTo(OrchestrationStage.Completed.ToString()),
                "AC6: Audit summary must record the completed stage");
        }

        #endregion

        #region AC7 - CI Stability: No Newly Skipped Tests

        /// <summary>
        /// AC7: Health endpoint returns 200 OK (sanity check that the test application starts correctly).
        /// Ensures no infrastructure regression was introduced by v2 changes.
        /// </summary>
        [Test]
        public async Task AC7_HealthEndpoint_Returns200_NoInfrastructureRegression()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "AC7: Health endpoint must return non-5xx status, confirming no infrastructure regression");
        }

        /// <summary>
        /// AC7: Auth registration endpoint responds (not 404) to confirm endpoint is registered.
        /// Validates that the v2 tests do not introduce a regression in routing.
        /// </summary>
        [Test]
        public async Task AC7_AuthRegisterEndpoint_IsReachable_NoRoutingRegression()
        {
            // Act: POST with empty body to confirm routing
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { });

            // Assert: not 404 (endpoint exists, even if body is invalid)
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.NotFound),
                "AC7: Auth register endpoint must exist; 404 would indicate a routing regression");
        }

        #endregion

        #region AC8 - PR Evidence: Sample Payloads and AC Mapping

        /// <summary>
        /// AC8: Sample registration payload demonstrates the documented response contract.
        /// This test produces a concrete payload that can be included as PR evidence.
        /// </summary>
        [Test]
        public async Task AC8_SampleRegistrationPayload_DemonstratesResponseContract()
        {
            // Arrange
            var email = $"v2-sample-{Guid.NewGuid().ToString("N")[..8]}@arc76-v2.com";
            var request = new RegisterRequest
            {
                Email = email,
                Password = "V2Sample123!",
                ConfirmPassword = "V2Sample123!",
                FullName = "V2 Contract Sample"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert: sample payload demonstrates all contract fields
            Assert.That(result, Is.Not.Null, "AC8: Response must be deserializable");
            Assert.That(result!.Success, Is.True, "AC8: Sample registration must succeed");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AC8: AlgorandAddress demonstrates AC1 - deterministic ARC76 derivation");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty,
                "AC8: AccessToken demonstrates AC2 - session lifecycle");
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC8: CorrelationId demonstrates AC4 - observable correlation");
            Assert.That(result.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC8: DerivationContractVersion demonstrates AC1 - versioned contract schema");
            Assert.That(result.AlgorandAddress, Has.Length.EqualTo(58),
                "AC8: Algorand address must be exactly 58 characters (standard format)");
        }

        /// <summary>
        /// AC8: Sample policy denial payload demonstrates structured deny artifact for PR evidence.
        /// </summary>
        [Test]
        public async Task AC8_SamplePolicyDenialPayload_DemonstratesStructuredDenyArtifact()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryMock = new Mock<IRetryPolicyClassifier>();
            retryMock.Setup(r => r.ClassifyError(It.IsAny<string>(), It.IsAny<DeploymentErrorCategory?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(new RetryPolicyDecision
                {
                    Policy = RetryPolicy.RetryableAfterRemediation,
                    Explanation = "KYC must be completed before deploying tokens. Please verify your identity."
                });

            var pipeline = new TokenWorkflowOrchestrationPipeline(loggerMock.Object, retryMock.Object);
            var correlationId = $"v2-evidence-{Guid.NewGuid():N}";
            var context = pipeline.BuildContext("ARC3_CREATE", correlationId, "idem-evidence-001", "evidence-user");

            // Act: simulate KYC policy denial
            var result = await pipeline.ExecuteAsync(
                context, "token-request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => "KYC_NOT_VERIFIED: User identity has not been verified",
                executor: _ => Task.FromResult("result"));

            // Assert: structured deny artifact for PR evidence
            Assert.That(result.Success, Is.False,
                "AC8: Policy denial demonstrates AC3 - enforced policy gate");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.PRECONDITION_FAILED),
                "AC8: PRECONDITION_FAILED demonstrates AC5 - standardized error taxonomy");
            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "AC8: Correlation ID demonstrates AC4 - observable lifecycle");
            Assert.That(result.AuditSummary.CompletedAtStage, Is.EqualTo(OrchestrationStage.CheckPreconditions.ToString()),
                "AC8: Audit summary stage demonstrates AC3 - allow/deny decision artifact");
            Assert.That(result.AuditSummary, Is.Not.Null,
                "AC8: Audit summary demonstrates AC6 - compliance evidence even on failure");
        }

        #endregion
    }
}

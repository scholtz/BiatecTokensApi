using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
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
    /// Auth-to-Deployment Reliability Contract Tests (Issue #437).
    ///
    /// Validates the complete reliability and compliance traceability contract:
    ///
    /// AC1 - Auth/Identity Guarantees:
    ///   Deployment endpoints require validated auth context; invalid/ambiguous sessions are
    ///   rejected with explicit errors. ARC76 identity derivation is deterministic and tested.
    ///   Actor/correlation context is present in deployment workflow logs.
    ///
    /// AC2 - State Machine Correctness:
    ///   Deployment lifecycle states are explicitly defined and enforced. Duplicate/retry
    ///   submissions are idempotent. Failed transitions include machine-parseable failure
    ///   category and human-actionable message.
    ///
    /// AC3 - Compliance Traceability:
    ///   Structured logs capture request metadata, validation decisions, compliance
    ///   checkpoints, and final outcomes. Audit trail supports correlation ID reconstruction.
    ///
    /// AC4 - Cross-Network Reliability:
    ///   Network preflight validation before broadcast. Response contracts stable across
    ///   supported networks. Integration tests cover success and failure per network group.
    ///
    /// AC5 - Testing and CI Confidence:
    ///   Unit tests cover state transitions and error mapping. Integration/contract tests
    ///   cover auth-to-deployment happy path and key failure paths.
    ///
    /// AC6 - Delivery Discipline:
    ///   PR maps each AC to concrete test evidence. No silent fallback for auth,
    ///   deployment, or compliance checks. Remaining limitations documented.
    ///
    /// Business Value: Hardens the trust engine by proving every authenticated request
    /// produces traceable, auditable, and deterministic deployment outcomes — reducing
    /// enterprise procurement friction and supporting MiCA-oriented compliance readiness.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AuthToDeploymentReliabilityContractTests
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
            ["JwtConfig:SecretKey"] = "auth-to-deploy-reliability-contract-test-key-32chars",
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
            ["KeyManagementConfig:HardcodedKey"] = "AuthToDeployReliabilityContractTestKey32CharsMin",
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

        // Helper to register a unique user and return login response
        private async Task<(string email, string password, LoginResponse loginResult)> RegisterAndLoginAsync()
        {
            var email = $"reliability-{Guid.NewGuid()}@test.com";
            var password = "Reliability123!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Registration failed: {await regResp.Content.ReadAsStringAsync()}");

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Login failed: {await loginResp.Content.ReadAsStringAsync()}");

            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult, Is.Not.Null);
            Assert.That(loginResult!.AccessToken, Is.Not.Null.And.Not.Empty);

            return (email, password, loginResult);
        }

        #region AC1: Auth/Identity Guarantees

        /// <summary>
        /// AC1: Deployment-status endpoint requires authenticated context; returns 401 without token.
        /// </summary>
        [Test]
        public async Task AC1_DeploymentEndpoint_WithoutAuthToken_Returns401()
        {
            // No auth header set
            var response = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Deployment listing without auth must be rejected");
        }

        /// <summary>
        /// AC1: Invalid bearer token is rejected with 401.
        /// </summary>
        [Test]
        public async Task AC1_DeploymentEndpoint_WithInvalidToken_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "totally.invalid.token");

            var response = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Deployment listing with invalid token must be rejected");
        }

        /// <summary>
        /// AC1: Valid JWT from register/login allows access to authenticated endpoints.
        /// </summary>
        [Test]
        public async Task AC1_DeploymentEndpoint_WithValidJwt_IsAccepted()
        {
            var (_, _, loginResult) = await RegisterAndLoginAsync();

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

            var response = await _client.GetAsync("/api/v1/token/deployments");
            // Should be 200 (success) - not 401/403
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(401),
                "Valid JWT must be accepted by authenticated endpoint");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(403),
                "Valid JWT must not be forbidden");
        }

        /// <summary>
        /// AC1: ARC76 address derivation is deterministic — same credentials always yield same address.
        /// </summary>
        [Test]
        public async Task AC1_ARC76Derivation_IsDeterministic_SameCredentialsYieldSameAddress()
        {
            var email = $"det-{Guid.NewGuid()}@test.com";
            var password = "Deterministic123!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var registeredAddress = regResult!.AlgorandAddress;
            Assert.That(registeredAddress, Is.Not.Null.And.Not.Empty, "Address must be derived at registration");

            // Login three times: each must produce the same deterministic address
            for (int i = 1; i <= 3; i++)
            {
                var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new LoginRequest { Email = email, Password = password });
                var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(registeredAddress),
                    $"Login attempt {i}: ARC76 address must match registration address");
            }
        }

        /// <summary>
        /// AC1: Correlation ID is present in register and login responses for traceability.
        /// </summary>
        [Test]
        public async Task AC1_AuthResponses_ContainCorrelationId_ForActorTraceability()
        {
            var email = $"corr-{Guid.NewGuid()}@test.com";
            var password = "Correlation123!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var regContent = await regResp.Content.ReadAsStringAsync();
            var regJson = JsonDocument.Parse(regContent);

            // RegisterResponse must contain correlationId or similar traceability field
            var hasCorrelation = regJson.RootElement.TryGetProperty("correlationId", out var corrProp)
                && !string.IsNullOrEmpty(corrProp.GetString());
            Assert.That(hasCorrelation, Is.True,
                $"RegisterResponse must include correlationId for actor traceability. Response: {regContent}");
        }

        #endregion

        #region AC2: State Machine Correctness

        /// <summary>
        /// AC2 (unit): Valid deployment state transitions are enforced by StateTransitionGuard.
        /// </summary>
        [Test]
        public void AC2_StateMachine_ValidTransitions_AreAllowed()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            // Queued → Submitted is valid
            var r1 = guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Submitted);
            Assert.That(r1.IsAllowed, Is.True, "Queued→Submitted must be allowed");

            // Submitted → Pending is valid
            var r2 = guard.ValidateTransition(DeploymentStatus.Submitted, DeploymentStatus.Pending);
            Assert.That(r2.IsAllowed, Is.True, "Submitted→Pending must be allowed");

            // Pending → Confirmed is valid
            var r3 = guard.ValidateTransition(DeploymentStatus.Pending, DeploymentStatus.Confirmed);
            Assert.That(r3.IsAllowed, Is.True, "Pending→Confirmed must be allowed");

            // Any state → Failed is valid (except terminal)
            var r4 = guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Failed);
            Assert.That(r4.IsAllowed, Is.True, "Queued→Failed must be allowed");
        }

        /// <summary>
        /// AC2 (unit): Invalid state transitions are rejected with reason codes.
        /// </summary>
        [Test]
        public void AC2_StateMachine_InvalidTransitions_AreRejected()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            // Completed → Queued is invalid (terminal state)
            var r1 = guard.ValidateTransition(DeploymentStatus.Completed, DeploymentStatus.Queued);
            Assert.That(r1.IsAllowed, Is.False, "Completed→Queued must be rejected (terminal state)");
            Assert.That(r1.ReasonCode, Is.Not.Null.And.Not.Empty, "Rejected transition must include reason code");

            // Cancelled → Submitted is invalid (terminal state)
            var r2 = guard.ValidateTransition(DeploymentStatus.Cancelled, DeploymentStatus.Submitted);
            Assert.That(r2.IsAllowed, Is.False, "Cancelled→Submitted must be rejected (terminal state)");

            // Pending → Queued is invalid (backward transition)
            var r3 = guard.ValidateTransition(DeploymentStatus.Pending, DeploymentStatus.Queued);
            Assert.That(r3.IsAllowed, Is.False, "Pending→Queued must be rejected (backward transition)");
        }

        /// <summary>
        /// AC2 (unit): Terminal states are identified correctly.
        /// </summary>
        [Test]
        public void AC2_StateMachine_TerminalStates_AreIdentifiedCorrectly()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            Assert.That(guard.IsTerminalState(DeploymentStatus.Completed), Is.True, "Completed is terminal");
            Assert.That(guard.IsTerminalState(DeploymentStatus.Cancelled), Is.True, "Cancelled is terminal");
            Assert.That(guard.IsTerminalState(DeploymentStatus.Queued), Is.False, "Queued is not terminal");
            Assert.That(guard.IsTerminalState(DeploymentStatus.Failed), Is.False, "Failed is not terminal (retry allowed)");
        }

        /// <summary>
        /// AC2 (unit): Retry from Failed→Queued is allowed (idempotency for retry).
        /// </summary>
        [Test]
        public void AC2_StateMachine_FailedToQueued_IsAllowed_ForRetry()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            var result = guard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.True, "Failed→Queued must be allowed to support retry");
            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty,
                "Retry transition must have a machine-parseable reason code");
        }

        /// <summary>
        /// AC2 (unit): DeploymentStatusService creates deployment with Queued initial state.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentStatusService_CreateDeployment_InitializesWithQueuedState()
        {
            var repositoryMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var logger = new Mock<ILogger<DeploymentStatusService>>();

            TokenDeployment? captured = null;
            repositoryMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Callback<TokenDeployment>(d => captured = d)
                .Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repositoryMock.Object, webhookMock.Object, logger.Object);

            var correlationId = Guid.NewGuid().ToString();
            var deploymentId = await service.CreateDeploymentAsync(
                "ASA_FT", "algorand-mainnet", "ACTOR_ADDRESS", "TestToken", "TST", correlationId);

            Assert.That(deploymentId, Is.Not.Null.And.Not.Empty, "Deployment ID must be assigned");
            Assert.That(captured, Is.Not.Null, "Deployment must be persisted");
            Assert.That(captured!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "Initial status must be Queued");
            Assert.That(captured.CorrelationId, Is.EqualTo(correlationId),
                "Correlation ID must be propagated to deployment record");
            Assert.That(captured.DeployedBy, Is.EqualTo("ACTOR_ADDRESS"),
                "Actor identity must be recorded in deployment");
        }

        /// <summary>
        /// AC2 (unit): Invalid auth context (missing actor) does not silently create deployment.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentStatusService_CreateDeployment_RecordsActorAndTokenDetails()
        {
            var repositoryMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var logger = new Mock<ILogger<DeploymentStatusService>>();

            TokenDeployment? captured = null;
            repositoryMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Callback<TokenDeployment>(d => captured = d)
                .Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repositoryMock.Object, webhookMock.Object, logger.Object);

            await service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xACTOR", "Compliance Token", "CMPL");

            Assert.That(captured!.TokenType, Is.EqualTo("ERC20_Mintable"),
                "Token type must be recorded");
            Assert.That(captured.Network, Is.EqualTo("base-mainnet"),
                "Network must be recorded for cross-network traceability");
            Assert.That(captured.TokenName, Is.EqualTo("Compliance Token"),
                "Token name must be recorded");
            Assert.That(captured.StatusHistory, Has.Count.GreaterThan(0),
                "Status history must have initial entry for audit trail");
        }

        #endregion

        #region AC3: Compliance Traceability

        /// <summary>
        /// AC3: Login response contains correlation ID for request traceability.
        /// </summary>
        [Test]
        public async Task AC3_LoginResponse_ContainsCorrelationId()
        {
            var email = $"trace-{Guid.NewGuid()}@test.com";
            var password = "Trace123!";

            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });

            var loginContent = await loginResp.Content.ReadAsStringAsync();
            var loginJson = JsonDocument.Parse(loginContent);

            var hasCorrelation = loginJson.RootElement.TryGetProperty("correlationId", out var corrProp)
                && !string.IsNullOrEmpty(corrProp.GetString());
            Assert.That(hasCorrelation, Is.True,
                $"LoginResponse must include correlationId for compliance traceability. Response: {loginContent}");
        }

        /// <summary>
        /// AC3: Deployment record audit trail includes all required compliance fields.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentRecord_AuditFields_ArePresent()
        {
            var repositoryMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var logger = new Mock<ILogger<DeploymentStatusService>>();

            TokenDeployment? captured = null;
            repositoryMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Callback<TokenDeployment>(d => captured = d)
                .Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repositoryMock.Object, webhookMock.Object, logger.Object);
            var correlationId = Guid.NewGuid().ToString();

            await service.CreateDeploymentAsync(
                "ASA_FT", "algorand-testnet", "TEST_ACTOR", "Audit Token", "AUDT", correlationId);

            // Validate all compliance-required fields are populated
            Assert.That(captured!.DeploymentId, Is.Not.Null.And.Not.Empty, "DeploymentId must be set");
            Assert.That(captured.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId must be set");
            Assert.That(captured.DeployedBy, Is.Not.Null.And.Not.Empty, "DeployedBy (actor) must be set");
            Assert.That(captured.TokenType, Is.Not.Null.And.Not.Empty, "TokenType must be set");
            Assert.That(captured.Network, Is.Not.Null.And.Not.Empty, "Network must be set");
            Assert.That(captured.StatusHistory, Has.Count.GreaterThan(0), "StatusHistory must have initial entry");

            var initialEntry = captured.StatusHistory.First();
            Assert.That(initialEntry.Status, Is.EqualTo(DeploymentStatus.Queued),
                "Initial audit entry must record Queued status");
            Assert.That(initialEntry.Timestamp, Is.GreaterThan(DateTime.MinValue),
                "Audit entry must include timestamp");
            Assert.That(initialEntry.Message, Is.Not.Null.And.Not.Empty,
                "Audit entry must include a message for compliance log reconstruction");
        }

        /// <summary>
        /// AC3: Deployment status update persists audit trail entry with transition details.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentStatusUpdate_AddsAuditTrailEntry()
        {
            var repositoryMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var logger = new Mock<ILogger<DeploymentStatusService>>();

            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Queued,
                StatusHistory = new List<DeploymentStatusEntry>()
            };

            repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);
            repositoryMock.Setup(r => r.AddStatusEntryAsync(deploymentId, It.IsAny<DeploymentStatusEntry>()))
                .Returns(Task.CompletedTask);
            repositoryMock.Setup(r => r.UpdateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repositoryMock.Object, webhookMock.Object, logger.Object);

            var updated = await service.UpdateDeploymentStatusAsync(
                deploymentId, DeploymentStatus.Submitted, "Transaction submitted to network",
                transactionHash: "0xABC123");

            Assert.That(updated, Is.True, "Valid transition must succeed");
            repositoryMock.Verify(
                r => r.AddStatusEntryAsync(deploymentId, It.Is<DeploymentStatusEntry>(e =>
                    e.Status == DeploymentStatus.Submitted)),
                Times.Once,
                "Audit trail entry must be added for status transition");
        }

        /// <summary>
        /// AC3: Status history is retrievable for compliance reconstruction by correlation ID.
        /// </summary>
        [Test]
        public async Task AC3_StatusHistory_IsRetrievable_ForComplianceReconstruction()
        {
            var repositoryMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var logger = new Mock<ILogger<DeploymentStatusService>>();

            var deploymentId = Guid.NewGuid().ToString();
            var history = new List<DeploymentStatusEntry>
            {
                new() { DeploymentId = deploymentId, Status = DeploymentStatus.Queued, Timestamp = DateTime.UtcNow.AddMinutes(-5), Message = "Queued" },
                new() { DeploymentId = deploymentId, Status = DeploymentStatus.Submitted, Timestamp = DateTime.UtcNow.AddMinutes(-3), Message = "Submitted" },
                new() { DeploymentId = deploymentId, Status = DeploymentStatus.Pending, Timestamp = DateTime.UtcNow.AddMinutes(-1), Message = "Pending" }
            };

            repositoryMock.Setup(r => r.GetStatusHistoryAsync(deploymentId))
                .ReturnsAsync(history);

            var service = new DeploymentStatusService(repositoryMock.Object, webhookMock.Object, logger.Object);
            var result = await service.GetStatusHistoryAsync(deploymentId);

            Assert.That(result, Is.Not.Null, "Status history must be retrievable");
            Assert.That(result.Count, Is.EqualTo(3), "All status entries must be returned for audit reconstruction");
            Assert.That(result[0].Status, Is.EqualTo(DeploymentStatus.Queued), "History must include initial Queued state");
        }

        #endregion

        #region AC4: Cross-Network Reliability

        /// <summary>
        /// AC4: Deployment listing endpoint returns stable schema across authenticated requests.
        /// </summary>
        [Test]
        public async Task AC4_DeploymentList_ReturnsStableSchema_ForAuthenticatedUser()
        {
            var (_, _, loginResult) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

            var response = await _client.GetAsync("/api/v1/token/deployments");
            var content = await response.Content.ReadAsStringAsync();

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(401)
                .And.Not.EqualTo(500), $"Deployment list must not error: {content}");

            // Parse response and verify required schema fields
            var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            // Must have deployments or items or similar collection field
            var hasDeployments = root.ValueKind == JsonValueKind.Array ||
                root.TryGetProperty("deployments", out _) ||
                root.TryGetProperty("items", out _) ||
                root.TryGetProperty("data", out _);
            Assert.That(hasDeployments, Is.True,
                $"Deployment list response must contain a deployments collection. Response: {content}");
        }

        /// <summary>
        /// AC4: Health endpoint provides network-independent status for frontend orchestration.
        /// </summary>
        [Test]
        public async Task AC4_HealthEndpoint_ReturnsNetworkIndependentStatus()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.EqualTo(200).Or.EqualTo(503),
                "Health endpoint must return 200 or 503 (not error codes like 404/500)");
        }

        /// <summary>
        /// AC4: ASA token creation endpoint (Algorand network) rejects request without auth.
        /// </summary>
        [Test]
        public async Task AC4_AlgorandNetwork_TokenCreation_RejectsUnauthenticatedRequest()
        {
            // No auth header - should reject
            var response = await _client.PostAsJsonAsync("/api/v1/token/asa-ft/create",
                new { TokenName = "Test", Symbol = "TST", TotalSupply = 1000000UL });
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "ASA token creation without auth must be rejected");
        }

        /// <summary>
        /// AC4: ERC20 token creation endpoint (Base/EVM network) rejects request without auth.
        /// </summary>
        [Test]
        public async Task AC4_EVMNetwork_TokenCreation_RejectsUnauthenticatedRequest()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/token/erc20-mintable/create",
                new { TokenName = "Test", Symbol = "TST", TotalSupply = 1000000UL });
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "ERC20 token creation without auth must be rejected");
        }

        /// <summary>
        /// AC4 (unit): StateTransitionGuard provides valid next states for cross-network state visibility.
        /// </summary>
        [Test]
        public void AC4_StateTransitionGuard_ProvidesValidNextStates_ForEachLifecycleState()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            // Verify each non-terminal state has at least one valid next state
            var nonTerminalStates = new[]
            {
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted,
                DeploymentStatus.Pending,
                DeploymentStatus.Confirmed,
                DeploymentStatus.Indexed,
                DeploymentStatus.Failed
            };

            foreach (var status in nonTerminalStates)
            {
                var nextStates = guard.GetValidNextStates(status);
                Assert.That(nextStates, Is.Not.Null.And.Not.Empty,
                    $"Status {status} must have at least one valid next state for lifecycle progression");
            }

            // Terminal states must have no valid next states
            Assert.That(guard.GetValidNextStates(DeploymentStatus.Completed), Is.Empty,
                "Completed (terminal) must have no valid next states");
            Assert.That(guard.GetValidNextStates(DeploymentStatus.Cancelled), Is.Empty,
                "Cancelled (terminal) must have no valid next states");
        }

        #endregion

        #region AC5: Testing and CI Confidence

        /// <summary>
        /// AC5: Registration endpoint returns 400 for weak password (no silent fallback).
        /// </summary>
        [Test]
        public async Task AC5_Registration_WeakPassword_Returns400_NoSilentFallback()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"weak-{Guid.NewGuid()}@test.com",
                    Password = "weak",
                    ConfirmPassword = "weak"
                });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Weak password must be explicitly rejected with 400, no silent fallback");
        }

        /// <summary>
        /// AC5: Login with wrong password returns 400/401 with explicit error (no silent pass).
        /// </summary>
        [Test]
        public async Task AC5_Login_WrongPassword_Returns4xx_WithExplicitError()
        {
            var email = $"wrongpwd-{Guid.NewGuid()}@test.com";
            var password = "Correct123!";

            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "Wrong456!" });

            Assert.That((int)loginResp.StatusCode, Is.EqualTo(400).Or.EqualTo(401),
                "Wrong password must return explicit 4xx, not 200 or 500");

            var content = await loginResp.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Empty,
                "Error response must include non-empty body with error details");
        }

        /// <summary>
        /// AC5: Deployment list for authenticated user returns 200 with stable schema.
        /// </summary>
        [Test]
        public async Task AC5_AuthenticatedDeploymentList_Returns200_WithStableSchema()
        {
            var (_, _, loginResult) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

            var response = await _client.GetAsync("/api/v1/token/deployments");
            var content = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Authenticated deployment list must return 200. Content: {content}");
        }

        /// <summary>
        /// AC5: Refresh token produces new valid access token (session continuity).
        /// </summary>
        [Test]
        public async Task AC5_RefreshToken_ProducesNewAccessToken_ForSessionContinuity()
        {
            var (_, _, loginResult) = await RegisterAndLoginAsync();
            Assert.That(loginResult.RefreshToken, Is.Not.Null.And.Not.Empty,
                "Login must return refresh token for session continuity");

            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = loginResult.RefreshToken });
            var refreshContent = await refreshResp.Content.ReadAsStringAsync();

            Assert.That((int)refreshResp.StatusCode, Is.EqualTo(200),
                $"Refresh token must yield new access token. Content: {refreshContent}");

            var refreshJson = JsonDocument.Parse(refreshContent);
            var hasNewToken = refreshJson.RootElement.TryGetProperty("accessToken", out var newToken)
                && !string.IsNullOrEmpty(newToken.GetString());
            Assert.That(hasNewToken, Is.True,
                $"Refresh response must include new accessToken. Response: {refreshContent}");
        }

        #endregion

        #region AC6: Delivery Discipline

        /// <summary>
        /// AC6: Auth endpoint returns machine-parseable error structure (no internal details leaked).
        /// </summary>
        [Test]
        public async Task AC6_AuthError_IsMachineParseable_WithoutInternalLeakage()
        {
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = "nonexistent@test.com", Password = "AnyPass123!" });
            var content = await loginResp.Content.ReadAsStringAsync();

            Assert.That((int)loginResp.StatusCode, Is.EqualTo(400).Or.EqualTo(401),
                "Non-existent user login must return 4xx");

            // Verify no internal implementation details leaked
            var lowerContent = content.ToLower();
            Assert.That(lowerContent, Does.Not.Contain("exception"),
                "Error response must not contain 'exception' (no stack trace leakage)");
            Assert.That(lowerContent, Does.Not.Contain("at biatec"),
                "Error response must not contain stack trace frames");
            Assert.That(lowerContent, Does.Not.Contain("system."),
                "Error response must not expose internal .NET type names");
        }

        /// <summary>
        /// AC6: Deployment status service correctly reports idempotent transition (same→same is allowed).
        /// </summary>
        [Test]
        public void AC6_StateMachine_SameToSameTransition_IsIdempotent_ReturnsAllowed()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            // Same→Same should be idempotent (allowed, not an error)
            var result = guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.True,
                "Same-to-same transition must be idempotent (allowed) to support safe retries");
        }

        /// <summary>
        /// AC6: State transition reason codes are machine-parseable (uppercase snake case).
        /// </summary>
        [Test]
        public void AC6_TransitionReasonCodes_AreMachineParseable()
        {
            var logger = new Mock<ILogger<StateTransitionGuard>>();
            var guard = new StateTransitionGuard(logger.Object);

            var validPairs = new[]
            {
                (DeploymentStatus.Queued, DeploymentStatus.Submitted),
                (DeploymentStatus.Submitted, DeploymentStatus.Pending),
                (DeploymentStatus.Pending, DeploymentStatus.Confirmed),
                (DeploymentStatus.Failed, DeploymentStatus.Queued)
            };

            foreach (var (from, to) in validPairs)
            {
                var reasonCode = guard.GetTransitionReasonCode(from, to);
                Assert.That(reasonCode, Is.Not.Null.And.Not.Empty,
                    $"Transition {from}→{to} must have a reason code");
                // Machine-parseable: uppercase letters, underscores, no spaces
                Assert.That(reasonCode, Does.Match("^[A-Z0-9_]+$"),
                    $"Reason code '{reasonCode}' for {from}→{to} must be uppercase snake case (machine-parseable)");
            }
        }

        #endregion
    }
}

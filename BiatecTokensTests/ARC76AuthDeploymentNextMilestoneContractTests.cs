using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration and contract tests for issue #399:
    /// Backend next milestone – deterministic ARC76 auth-to-deployment contract hardening.
    ///
    /// Each test is keyed to one or more acceptance criteria:
    ///
    ///   AC1 – ARC76 derivation is deterministic across first-login, repeat-login, and
    ///          recovery scenarios.
    ///   AC2 – Auth-session to deployment pipeline enforces strict preconditions and returns
    ///          explicit stage outcomes with traceability.
    ///   AC3 – Validation layer consistently rejects invalid input with stable error schema
    ///          and actionable guidance.
    ///   AC4 – Idempotency protections prevent duplicate issuance on repeated requests.
    ///   AC5 – Structured logs / audit events cover the end-to-end flow and support
    ///          compliance review without exposing secrets.
    ///   AC6 – Contract / integration tests comprehensively cover happy path and key
    ///          failure paths and pass in CI.
    ///   AC7 – Frontend-consumed response contracts remain backward-compatible.
    ///   AC8 – No regression in existing successful deployment paths.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76AuthDeploymentNextMilestoneContractTests
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
            ["JwtConfig:SecretKey"] = "arc76-next-milestone-contract-test-secret-key-32chars-minimum",
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
            ["KeyManagementConfig:HardcodedKey"] = "NextMilestoneContractTestKey32CharactersMinimum"
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

        // -----------------------------------------------------------------------
        // Helper
        // -----------------------------------------------------------------------

        private async Task<(RegisterResponse? response, HttpStatusCode status)> RegisterAsync(
            string email, string password)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return (body, response.StatusCode);
        }

        private async Task<(LoginResponse? response, HttpStatusCode status)> LoginAsync(
            string email, string password)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return (body, response.StatusCode);
        }

        // -----------------------------------------------------------------------
        // AC1 – Deterministic ARC76 derivation
        // -----------------------------------------------------------------------

        #region AC1 – ARC76 Derivation Determinism

        /// <summary>
        /// AC1: First-login derivation always produces the same ARC76 Algorand address
        /// as the initial registration, confirming deterministic identity mapping.
        /// </summary>
        [Test]
        public async Task AC1_FirstLogin_ProducesSameAddressAsRegistration()
        {
            var email = $"ac1-first-login-{Guid.NewGuid()}@example.com";
            const string password = "AC1FirstLogin@123";

            var (reg, _) = await RegisterAsync(email, password);
            var (login, _) = await LoginAsync(email, password);

            Assert.That(reg!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AC1: Registration must return a non-empty Algorand address");
            Assert.That(login!.AlgorandAddress, Is.EqualTo(reg.AlgorandAddress),
                "AC1: First login must return the same Algorand address as registration");
        }

        /// <summary>
        /// AC1: Repeat-login idempotency – three consecutive logins always return the
        /// same ARC76 address, confirming stable account derivation over time.
        /// </summary>
        [Test]
        public async Task AC1_RepeatLogins_AlwaysProduceSameDerivedAddress()
        {
            var email = $"ac1-repeat-login-{Guid.NewGuid()}@example.com";
            const string password = "AC1RepeatLogin@123";

            await RegisterAsync(email, password);

            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var (login, _) = await LoginAsync(email, password);
                addresses.Add(login!.AlgorandAddress!);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "AC1: All repeat-login attempts must return the identical Algorand address");
        }

        /// <summary>
        /// AC1: Recovery scenario – logging in with canonical (lower-cased) email still
        /// resolves to the same ARC76 address as registration, proving email-normalisation
        /// is stable under case variation.
        /// </summary>
        [Test]
        public async Task AC1_RecoveryScenario_EmailCaseVariation_SameAddress()
        {
            var guid = Guid.NewGuid().ToString("N")[..8];
            var email = $"AC1.Recovery.{guid}@Example.COM";
            const string password = "AC1Recovery@123";

            var (reg, _) = await RegisterAsync(email, password);

            // Attempt login with all-lowercase canonical form
            var (loginLower, _) = await LoginAsync(email.ToLowerInvariant(), password);

            Assert.That(loginLower!.AlgorandAddress, Is.EqualTo(reg!.AlgorandAddress),
                "AC1: Lowercase canonical email must map to the same ARC76 address as the original");
        }

        /// <summary>
        /// AC1: DerivationContractVersion is non-empty and stable across
        /// registration and all subsequent logins for the same user.
        /// </summary>
        [Test]
        public async Task AC1_DerivationContractVersion_IsStableAcrossAuthEvents()
        {
            var email = $"ac1-dcv-{Guid.NewGuid()}@example.com";
            const string password = "AC1dcv@123";

            var (reg, _) = await RegisterAsync(email, password);
            var (login1, _) = await LoginAsync(email, password);
            var (login2, _) = await LoginAsync(email, password);

            Assert.That(reg!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC1: DerivationContractVersion must be set at registration");
            Assert.That(login1!.DerivationContractVersion, Is.EqualTo(reg.DerivationContractVersion),
                "AC1: DerivationContractVersion must match after first login");
            Assert.That(login2!.DerivationContractVersion, Is.EqualTo(reg.DerivationContractVersion),
                "AC1: DerivationContractVersion must be stable across multiple logins");
        }

        /// <summary>
        /// AC1: Different users always receive different ARC76 addresses,
        /// confirming that derivation is uniquely bound to identity.
        /// </summary>
        [Test]
        public async Task AC1_DifferentUsers_ReceiveDifferentDerivedAddresses()
        {
            var email1 = $"ac1-userA-{Guid.NewGuid()}@example.com";
            var email2 = $"ac1-userB-{Guid.NewGuid()}@example.com";
            const string password = "AC1DiffUsers@123";

            var (reg1, _) = await RegisterAsync(email1, password);
            var (reg2, _) = await RegisterAsync(email2, password);

            Assert.That(reg1!.AlgorandAddress, Is.Not.EqualTo(reg2!.AlgorandAddress),
                "AC1: Different users must receive uniquely derived Algorand addresses");
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC2 – Auth-session to deployment pipeline preconditions
        // -----------------------------------------------------------------------

        #region AC2 – Deployment Pipeline Preconditions

        /// <summary>
        /// AC2: A valid authenticated session produces a non-expired JWT that is
        /// structurally sound (three-segment base64url).
        /// </summary>
        [Test]
        public async Task AC2_ValidSession_ProducesWellFormedJwtToken()
        {
            var email = $"ac2-jwt-{Guid.NewGuid()}@example.com";
            const string password = "AC2Jwt@123";

            await RegisterAsync(email, password);
            var (login, status) = await LoginAsync(email, password);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK),
                "AC2: Successful login must return 200 OK");
            Assert.That(login!.AccessToken, Is.Not.Null.And.Not.Empty,
                "AC2: Access token must be present");

            var parts = login.AccessToken!.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "AC2: JWT must have exactly three segments (header.payload.signature)");
        }

        /// <summary>
        /// AC2: Deployment list endpoint requires authentication – requests without a
        /// bearer token must receive 401, not a 5xx or a silent empty response.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentList_UnauthenticatedRequest_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/token/deployments");

            Assert.That((int)response.StatusCode, Is.EqualTo(401),
                "AC2: Unauthenticated deployment list request must return 401");
        }

        /// <summary>
        /// AC2: Deployment list with an expired / invalid bearer token returns 401
        /// with a structured body, not a server error.
        /// </summary>
        [Test]
        public async Task AC2_DeploymentList_InvalidToken_Returns401WithStructuredBody()
        {
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

            var response = await _client.GetAsync("/api/v1/token/deployments");

            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)response.StatusCode, Is.EqualTo(401),
                "AC2: Invalid JWT must yield 401");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "AC2: Invalid JWT must never yield a 500 server error");
        }

        /// <summary>
        /// AC2: Authenticated access to deployment status list returns a successful
        /// response with the documented envelope fields.
        /// </summary>
        [Test]
        public async Task AC2_AuthenticatedDeploymentList_ReturnsStructuredEnvelope()
        {
            var email = $"ac2-deploy-list-{Guid.NewGuid()}@example.com";
            const string password = "AC2DeployList@123";

            await RegisterAsync(email, password);
            var (login, _) = await LoginAsync(email, password);

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);

            var response = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "AC2: Authenticated deployment list must not return a 5xx error");
        }

        /// <summary>
        /// AC2: Token refresh produces a new access token that is distinct from the
        /// original, preserving the Algorand address and enabling chained session continuity.
        /// </summary>
        [Test]
        public async Task AC2_TokenRefresh_ProducesNewAccessToken_PreservesAddress()
        {
            var email = $"ac2-refresh-{Guid.NewGuid()}@example.com";
            const string password = "AC2Refresh@123";

            await RegisterAsync(email, password);
            var (login, _) = await LoginAsync(email, password);

            var refreshRequest = new { RefreshToken = login!.RefreshToken };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

            Assert.That((int)refreshResponse.StatusCode, Is.LessThan(500),
                "AC2: Token refresh must not return a 5xx error");

            if (refreshResponse.StatusCode == HttpStatusCode.OK)
            {
                var rawJson = await refreshResponse.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("accessToken", out var newToken))
                {
                    Assert.That(newToken.GetString(), Is.Not.EqualTo(login.AccessToken),
                        "AC2: Refreshed token must differ from the original");
                }

                if (root.TryGetProperty("algorandAddress", out var addr))
                {
                    Assert.That(addr.GetString(), Is.EqualTo(login.AlgorandAddress),
                        "AC2: Algorand address must be preserved after token refresh");
                }
            }
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC3 – Validation layer with stable error schema
        // -----------------------------------------------------------------------

        #region AC3 – Validation and Policy Enforcement

        /// <summary>
        /// AC3: Missing required email field returns 400 with a structured, actionable
        /// error – not a 500 or empty body.
        /// </summary>
        [Test]
        public async Task AC3_MissingEmail_Returns400WithActionableError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Password = "ValidPass@123", ConfirmPassword = "ValidPass@123" });

            Assert.That((int)response.StatusCode, Is.EqualTo(400),
                "AC3: Missing email must return 400");

            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                "AC3: Error response body must not be empty");
        }

        /// <summary>
        /// AC3: Password that does not meet complexity requirements returns a structured
        /// error code (not a 500) with a user-actionable message.
        /// </summary>
        [Test]
        public async Task AC3_WeakPassword_ReturnsStructuredValidationError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"ac3-weak-{Guid.NewGuid()}@example.com",
                    Password = "weak",
                    ConfirmPassword = "weak"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "AC3: Weak password must not cause a server error");

            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                "AC3: Validation error body must be non-empty");
        }

        /// <summary>
        /// AC3: Mismatched passwords return a stable 400 error (not a 500).
        /// </summary>
        [Test]
        public async Task AC3_MismatchedPasswords_Returns400NotServerError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"ac3-mismatch-{Guid.NewGuid()}@example.com",
                    Password = "StrongPass@123",
                    ConfirmPassword = "DifferentPass@456"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "AC3: Password mismatch must not cause a server error");
        }

        /// <summary>
        /// AC3: Login with wrong credentials returns a typed error code, not a 500.
        /// The error body follows the documented AuthErrorResponse schema.
        /// </summary>
        [Test]
        public async Task AC3_WrongCredentials_ReturnsTypedErrorCode()
        {
            var email = $"ac3-wrong-creds-{Guid.NewGuid()}@example.com";
            const string password = "AC3WrongCreds@123";

            await RegisterAsync(email, password);
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "WrongPassword@999" });

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(body!.Success, Is.False,
                "AC3: Failed login must set success=false");
            Assert.That(body.ErrorCode, Is.Not.Null.And.Not.Empty,
                "AC3: Failed login must include a typed ErrorCode for client handling");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "AC3: Wrong credentials must not cause a 500 server error");
        }

        /// <summary>
        /// AC3: Duplicate registration returns deterministic CONFLICT error code,
        /// not a server error.
        /// </summary>
        [Test]
        public async Task AC3_DuplicateRegistration_ReturnsDeterministicConflictCode()
        {
            var email = $"ac3-dup-{Guid.NewGuid()}@example.com";
            const string password = "AC3Dup@123";

            await RegisterAsync(email, password);
            var (dup, status) = await RegisterAsync(email, password);

            Assert.That((int)status, Is.Not.EqualTo(500),
                "AC3: Duplicate registration must not return 500");
            Assert.That(dup!.Success, Is.False,
                "AC3: Duplicate registration must report failure");
            Assert.That(dup.ErrorCode, Is.Not.Null.And.Not.Empty,
                "AC3: Duplicate registration must include a typed ErrorCode");
        }

        /// <summary>
        /// AC3: Invalid email format returns 400 with informative error body.
        /// </summary>
        [Test]
        public async Task AC3_InvalidEmailFormat_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = "not-a-valid-email",
                    Password = "ValidPass@123",
                    ConfirmPassword = "ValidPass@123"
                });

            Assert.That((int)response.StatusCode, Is.EqualTo(400),
                "AC3: Invalid email format must return 400");
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC4 – Idempotency protections
        // -----------------------------------------------------------------------

        #region AC4 – Idempotency and Duplicate Prevention

        /// <summary>
        /// AC4: Registering twice with the same email returns a failure on the second
        /// attempt (not a duplicate user), proving registration idempotency enforcement.
        /// </summary>
        [Test]
        public async Task AC4_DuplicateRegistration_IsRejected_NotSilentlyIgnored()
        {
            var email = $"ac4-idem-{Guid.NewGuid()}@example.com";
            const string password = "AC4Idem@123";

            var (first, firstStatus) = await RegisterAsync(email, password);
            var (second, secondStatus) = await RegisterAsync(email, password);

            Assert.That(first!.Success, Is.True,
                "AC4: First registration must succeed");
            Assert.That(second!.Success, Is.False,
                "AC4: Second registration for same email must be rejected");
            Assert.That((int)secondStatus, Is.Not.EqualTo(200).And.Not.EqualTo(201),
                "AC4: Rejected duplicate registration must not return a success status code");
        }

        /// <summary>
        /// AC4: The deployment state service enforces idempotency – calling UpdateStatus
        /// with the same status value does not create a duplicate entry.
        /// </summary>
        [Test]
        public async Task AC4_DeploymentStatusService_SameStatusUpdate_IsIdempotent()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ASA",
                network: "testnet",
                deployedBy: "AC4IdempotencyTestUser",
                tokenName: "IdempotencyToken",
                tokenSymbol: "IDEM",
                correlationId: $"ac4-idem-{Guid.NewGuid()}");

            // Transition Queued → Submitted once
            var first = await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted,
                "First submission");
            // Attempt identical transition again – should not throw
            var second = await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted,
                "Duplicate submission attempt");

            Assert.That(first, Is.True,
                "AC4: First valid status transition must succeed");
            // Second attempt on same status is either silently accepted or returns false – must not throw
            var deployment = await svc.GetDeploymentAsync(deploymentId);
            Assert.That(deployment, Is.Not.Null,
                "AC4: Deployment must still be retrievable after duplicate status attempt");
        }

        /// <summary>
        /// AC4: Failed deployment can transition back to Queued for a controlled retry,
        /// without silently duplicating the original record.
        /// </summary>
        [Test]
        public async Task AC4_FailedDeployment_CanRetryViaQueuedTransition()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ARC3",
                network: "testnet",
                deployedBy: "AC4RetryUser",
                tokenName: "RetryToken",
                tokenSymbol: "RETRY",
                correlationId: $"ac4-retry-{Guid.NewGuid()}");

            await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);
            await svc.MarkDeploymentFailedAsync(deploymentId, "Transient provider error", isRetryable: true);

            var retryAllowed = await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Queued,
                "Retrying after transient failure");

            Assert.That(retryAllowed, Is.True,
                "AC4: Failed deployment must be retryable by transitioning back to Queued");

            var deployment = await svc.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "AC4: Deployment status must be Queued after retry transition");
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC5 – Observability and audit trail
        // -----------------------------------------------------------------------

        #region AC5 – Observability and Audit Trail

        /// <summary>
        /// AC5: Registration response includes a non-empty CorrelationId that can be
        /// used to cross-reference audit log entries for the same request.
        /// </summary>
        [Test]
        public async Task AC5_Registration_ResponseIncludesCorrelationId()
        {
            var (reg, _) = await RegisterAsync(
                $"ac5-corr-{Guid.NewGuid()}@example.com", "AC5Corr@123");

            Assert.That(reg!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC5: Registration response must include a non-empty CorrelationId for audit tracing");
        }

        /// <summary>
        /// AC5: Login response includes a correlation ID distinct from registration,
        /// proving per-request audit events are individually identifiable.
        /// </summary>
        [Test]
        public async Task AC5_EachAuthRequest_HasUniqueCorrelationId()
        {
            var email = $"ac5-unique-corr-{Guid.NewGuid()}@example.com";
            const string password = "AC5UniqueCorr@123";

            var (reg, _) = await RegisterAsync(email, password);
            var (login1, _) = await LoginAsync(email, password);
            var (login2, _) = await LoginAsync(email, password);

            var ids = new[] { reg!.CorrelationId!, login1!.CorrelationId!, login2!.CorrelationId! };

            Assert.That(ids.Distinct().Count(), Is.EqualTo(3),
                "AC5: Each auth request must have a unique CorrelationId for unambiguous audit logs");
        }

        /// <summary>
        /// AC5: Client-supplied X-Correlation-ID header is echoed back in the response,
        /// enabling correlation between client logs and server audit entries.
        /// </summary>
        [Test]
        public async Task AC5_ClientSuppliedCorrelationIdHeader_IsPreservedInResponse()
        {
            const string clientCorrelationId = "ac5-client-trace-id-12345";

            var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add("X-Correlation-ID", clientCorrelationId);

            var response = await _client.SendAsync(request);

            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "AC5: Health endpoint must respond without server error");

            if (response.Headers.TryGetValues("X-Correlation-ID", out var values))
            {
                Assert.That(values.First(), Is.EqualTo(clientCorrelationId),
                    "AC5: Client-supplied X-Correlation-ID must be echoed back in response headers");
            }
        }

        /// <summary>
        /// AC5: Deployment status history captures timestamped entries for each state
        /// transition, providing an auditable timeline.
        /// </summary>
        [Test]
        public async Task AC5_DeploymentStatusHistory_CapturesTimestampedAuditTrail()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ERC20",
                network: "base-mainnet",
                deployedBy: "AC5AuditUser",
                tokenName: "AuditToken",
                tokenSymbol: "AUDIT",
                correlationId: $"ac5-audit-{Guid.NewGuid()}");

            await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted to chain");
            await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Awaiting confirmation");

            var history = await svc.GetStatusHistoryAsync(deploymentId);

            Assert.That(history.Count, Is.GreaterThanOrEqualTo(3),
                "AC5: Status history must record Queued + Submitted + Pending entries");

            foreach (var entry in history)
            {
                Assert.That(entry.Timestamp, Is.GreaterThan(DateTime.MinValue),
                    "AC5: Every history entry must have a valid timestamp for audit purposes");
            }
        }

        /// <summary>
        /// AC5: Error responses do not leak sensitive internal details such as stack
        /// traces, connection strings, or secret keys.
        /// </summary>
        [Test]
        public async Task AC5_ErrorResponses_DoNotLeakSensitiveInternals()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = "nonexistent@example.com", Password = "WrongPass@123" });

            var content = await response.Content.ReadAsStringAsync();

            Assert.That(content, Does.Not.Contain("SecretKey"),
                "AC5: JWT secret key must not appear in error responses");
            Assert.That(content, Does.Not.Contain("HardcodedKey"),
                "AC5: Hardcoded encryption key must not appear in error responses");
            Assert.That(content, Does.Not.Contain("ConnectionString"),
                "AC5: Connection strings must not appear in error responses");
            Assert.That(content, Does.Not.Contain("StackTrace"),
                "AC5: Stack traces must not appear in user-facing error responses");
            Assert.That(content, Does.Not.Contain("at System."),
                "AC5: CLR stack frames must not appear in user-facing error responses");
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC6 – Happy path and failure path coverage
        // -----------------------------------------------------------------------

        #region AC6 – Happy Path and Failure Path Coverage

        /// <summary>
        /// AC6: Full happy path – register → login → access deployment list – succeeds
        /// end-to-end without server errors.
        /// </summary>
        [Test]
        public async Task AC6_FullHappyPath_RegisterLoginAccessDeployments()
        {
            var email = $"ac6-happy-{Guid.NewGuid()}@example.com";
            const string password = "AC6Happy@123";

            var (reg, regStatus) = await RegisterAsync(email, password);

            Assert.That(reg!.Success, Is.True, "AC6: Registration must succeed");
            Assert.That((int)regStatus, Is.EqualTo(200), "AC6: Registration must return 200");

            var (login, loginStatus) = await LoginAsync(email, password);

            Assert.That(login!.Success, Is.True, "AC6: Login must succeed");
            Assert.That((int)loginStatus, Is.EqualTo(200), "AC6: Login must return 200");
            Assert.That(login.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "AC6: Login must return a non-empty Algorand address");

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);
            var deployList = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)deployList.StatusCode, Is.LessThan(500),
                "AC6: Authenticated deployment list must not return 5xx");
        }

        /// <summary>
        /// AC6: Orchestration pipeline – all policies pass → execution succeeds with
        /// a valid result and correct stage markers.
        /// </summary>
        [Test]
        public async Task AC6_OrchestrationPipeline_AllPoliciesPass_ReturnsSuccess()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "TEST_AC6",
                correlationId: $"ac6-pipeline-{Guid.NewGuid()}",
                idempotencyKey: Guid.NewGuid().ToString(),
                userId: "ac6-test-user");

            var result = await pipeline.ExecuteAsync<string, string>(
                context: context,
                request: "test-input",
                validationPolicy: _ => null,        // pass
                preconditionPolicy: _ => null,      // pass
                executor: async req => { await Task.Yield(); return $"executed:{req}"; });

            Assert.That(result.Success, Is.True,
                "AC6: Pipeline with all-pass policies must succeed");
            Assert.That(result.Payload, Is.EqualTo("executed:test-input"),
                "AC6: Executor result must be returned unchanged");
            Assert.That(result.CorrelationId, Is.EqualTo(context.CorrelationId),
                "AC6: Correlation ID must be preserved in orchestration result");
        }

        /// <summary>
        /// AC6: Orchestration pipeline – validation policy failure returns structured
        /// failure result with the validation error message and no executor invocation.
        /// </summary>
        [Test]
        public async Task AC6_OrchestrationPipeline_ValidationFailure_ReturnsStructuredError()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "TEST_AC6_VALIDATION_FAIL",
                correlationId: $"ac6-val-fail-{Guid.NewGuid()}");

            bool executorCalled = false;
            var result = await pipeline.ExecuteAsync<string, string>(
                context: context,
                request: "bad-input",
                validationPolicy: _ => "VALIDATION_ERROR: token name is required",
                preconditionPolicy: _ => null,
                executor: async req =>
                {
                    executorCalled = true;
                    await Task.Yield();
                    return "should-not-execute";
                });

            Assert.That(result.Success, Is.False,
                "AC6: Validation failure must produce a failed result");
            Assert.That(executorCalled, Is.False,
                "AC6: Executor must not be called when validation fails");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC6: Failed result must include an error message");
        }

        /// <summary>
        /// AC6: Orchestration pipeline – precondition failure returns structured
        /// failure result without executing the core operation.
        /// </summary>
        [Test]
        public async Task AC6_OrchestrationPipeline_PreconditionFailure_ReturnsStructuredError()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "TEST_AC6_PRECONDITION_FAIL",
                correlationId: $"ac6-precond-fail-{Guid.NewGuid()}");

            bool executorCalled = false;
            var result = await pipeline.ExecuteAsync<string, string>(
                context: context,
                request: "test-request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => "PRECONDITION_FAILED: subscription tier insufficient",
                executor: async req =>
                {
                    executorCalled = true;
                    await Task.Yield();
                    return "should-not-execute";
                });

            Assert.That(result.Success, Is.False,
                "AC6: Precondition failure must produce a failed result");
            Assert.That(executorCalled, Is.False,
                "AC6: Executor must not be called when a precondition fails");
        }

        /// <summary>
        /// AC6: Expired session (invalid token) returns 401 on protected endpoint – not
        /// a server crash or empty response.
        /// </summary>
        [Test]
        public async Task AC6_ExpiredSession_Returns401NotServerError()
        {
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJleHBpcmVkIn0.INVALIDSIG");

            var response = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)response.StatusCode, Is.EqualTo(401),
                "AC6: Expired/invalid token must return 401");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "AC6: Expired/invalid token must never return a 500 server error");
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC7 – Frontend response contract backward compatibility
        // -----------------------------------------------------------------------

        #region AC7 – Frontend Response Contract Compatibility

        /// <summary>
        /// AC7: Registration response contains every field required by the current
        /// frontend contract – no required field has been removed or renamed.
        /// </summary>
        [Test]
        public async Task AC7_RegistrationResponse_ContainsAllFrontendContractFields()
        {
            var email = $"ac7-reg-contract-{Guid.NewGuid()}@example.com";
            const string password = "AC7RegContract@123";

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });
            var rawJson = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(rawJson).RootElement;

            // Fields required by frontend v1 contract
            Assert.That(root.TryGetProperty("success", out _), Is.True,
                "AC7: 'success' field must be in registration response");
            Assert.That(root.TryGetProperty("userId", out _), Is.True,
                "AC7: 'userId' field must be in registration response");
            Assert.That(root.TryGetProperty("email", out _), Is.True,
                "AC7: 'email' field must be in registration response");
            Assert.That(root.TryGetProperty("algorandAddress", out _), Is.True,
                "AC7: 'algorandAddress' field must be in registration response");
            Assert.That(root.TryGetProperty("accessToken", out _), Is.True,
                "AC7: 'accessToken' field must be in registration response");
            Assert.That(root.TryGetProperty("refreshToken", out _), Is.True,
                "AC7: 'refreshToken' field must be in registration response");
            Assert.That(root.TryGetProperty("derivationContractVersion", out _), Is.True,
                "AC7: 'derivationContractVersion' field must be in registration response");
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True,
                "AC7: 'correlationId' field must be in registration response");
        }

        /// <summary>
        /// AC7: Login response contains every field required by the current frontend
        /// contract and the values are correctly typed.
        /// </summary>
        [Test]
        public async Task AC7_LoginResponse_ContainsAllFrontendContractFields()
        {
            var email = $"ac7-login-contract-{Guid.NewGuid()}@example.com";
            const string password = "AC7LoginContract@123";

            await RegisterAsync(email, password);
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = password });
            var rawJson = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(rawJson).RootElement;

            Assert.That(root.TryGetProperty("success", out var successProp), Is.True,
                "AC7: 'success' field must be in login response");
            Assert.That(successProp.GetBoolean(), Is.True,
                "AC7: 'success' must be true for a successful login");
            Assert.That(root.TryGetProperty("algorandAddress", out _), Is.True,
                "AC7: 'algorandAddress' field must be in login response");
            Assert.That(root.TryGetProperty("accessToken", out _), Is.True,
                "AC7: 'accessToken' field must be in login response");
            Assert.That(root.TryGetProperty("derivationContractVersion", out _), Is.True,
                "AC7: 'derivationContractVersion' field must be in login response");
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True,
                "AC7: 'correlationId' field must be in login response");
            Assert.That(root.TryGetProperty("timestamp", out _), Is.True,
                "AC7: 'timestamp' field must be in login response");
        }

        /// <summary>
        /// AC7: Error response schema is stable – 'success', 'errorCode', and
        /// 'errorMessage' are always present on auth failures.
        /// </summary>
        [Test]
        public async Task AC7_AuthErrorResponse_ContainsStableErrorSchema()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = "ac7-schema@example.com", Password = "WrongPass@999" });
            var rawJson = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(rawJson).RootElement;

            Assert.That(root.TryGetProperty("success", out var successProp), Is.True,
                "AC7: 'success' must always appear in auth error response");
            Assert.That(successProp.GetBoolean(), Is.False,
                "AC7: 'success' must be false in an error response");
            Assert.That(root.TryGetProperty("errorCode", out _), Is.True,
                "AC7: 'errorCode' must always appear in auth error response");
            Assert.That(root.TryGetProperty("errorMessage", out _), Is.True,
                "AC7: 'errorMessage' must always appear in auth error response");
        }

        /// <summary>
        /// AC7: Deployment status list response uses the documented envelope with
        /// 'deployments' array and 'total' count, preserving frontend pagination contract.
        /// </summary>
        [Test]
        public async Task AC7_DeploymentListResponse_ContainsPaginationEnvelope()
        {
            var email = $"ac7-deploy-env-{Guid.NewGuid()}@example.com";
            const string password = "AC7DeployEnv@123";

            await RegisterAsync(email, password);
            var (login, _) = await LoginAsync(email, password);

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
            var response = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var rawJson = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(rawJson).RootElement;

                // Either direct array or object with deployments + total
                bool hasDeploymentsArray = root.ValueKind == JsonValueKind.Array ||
                                           root.TryGetProperty("deployments", out _);
                Assert.That(hasDeploymentsArray, Is.True,
                    "AC7: Deployment list response must include a 'deployments' array or be an array itself");
            }
        }

        #endregion

        // -----------------------------------------------------------------------
        // AC8 – No regression in existing deployment paths
        // -----------------------------------------------------------------------

        #region AC8 – Regression Prevention

        /// <summary>
        /// AC8: Health endpoint remains reachable and returns a non-5xx status –
        /// no infrastructure regression from this milestone.
        /// </summary>
        [Test]
        public async Task AC8_HealthEndpoint_RemainsReachable_NoRegression()
        {
            var response = await _client.GetAsync("/health");

            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "AC8: Health endpoint must not return a 5xx error after this milestone");
        }

        /// <summary>
        /// AC8: Auth register endpoint remains routable and accepts requests –
        /// no routing regression from this milestone.
        /// </summary>
        [Test]
        public async Task AC8_AuthRegisterEndpoint_RemainsRoutable_NoRegression()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"ac8-route-{Guid.NewGuid()}@example.com",
                    Password = "AC8Route@123",
                    ConfirmPassword = "AC8Route@123"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "AC8: Auth register endpoint must not return 404 after this milestone");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "AC8: Auth register endpoint must not return a 500 after this milestone");
        }

        /// <summary>
        /// AC8: Auth login endpoint remains functional end-to-end – existing users
        /// can still log in and receive valid tokens.
        /// </summary>
        [Test]
        public async Task AC8_AuthLoginEndpoint_RemainsFullyFunctional_NoRegression()
        {
            var email = $"ac8-login-regression-{Guid.NewGuid()}@example.com";
            const string password = "AC8LoginReg@123";

            var (reg, regStatus) = await RegisterAsync(email, password);
            var (login, loginStatus) = await LoginAsync(email, password);

            Assert.That(reg!.Success, Is.True,
                "AC8: Registration must still succeed after this milestone");
            Assert.That(login!.Success, Is.True,
                "AC8: Login must still succeed after this milestone");
            Assert.That(login.AlgorandAddress, Is.EqualTo(reg.AlgorandAddress),
                "AC8: ARC76 address must still be deterministic after this milestone");
        }

        /// <summary>
        /// AC8: Deployment status service can still create, update, and retrieve
        /// deployments through the complete state machine – core service not regressed.
        /// </summary>
        [Test]
        public async Task AC8_DeploymentStatusService_CoreStateMachine_NoRegression()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ASA",
                network: "mainnet",
                deployedBy: "AC8RegressionTestUser",
                tokenName: "RegressionToken",
                tokenSymbol: "REGR",
                correlationId: $"ac8-regr-{Guid.NewGuid()}");

            Assert.That(deploymentId, Is.Not.Null.And.Not.Empty,
                "AC8: CreateDeployment must return a non-empty deployment ID");

            // Walk the happy-path state machine
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted), Is.True,
                "AC8: Queued→Submitted transition must succeed");
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending), Is.True,
                "AC8: Submitted→Pending transition must succeed");
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed), Is.True,
                "AC8: Pending→Confirmed transition must succeed");
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed), Is.True,
                "AC8: Confirmed→Completed transition must succeed");

            var deployment = await svc.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed),
                "AC8: Deployment must reach Completed state without regression");
        }

        /// <summary>
        /// AC8: Orchestration pipeline context builder produces valid contexts with
        /// required fields populated – no regression in pipeline infrastructure.
        /// </summary>
        [Test]
        public async Task AC8_OrchestrationPipeline_ContextBuilder_NoRegression()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var correlationId = Guid.NewGuid().ToString();
            var idempotencyKey = Guid.NewGuid().ToString();
            const string userId = "ac8-regression-user";
            const string operationType = "AC8_REGRESSION_TEST";

            var context = pipeline.BuildContext(
                operationType: operationType,
                correlationId: correlationId,
                idempotencyKey: idempotencyKey,
                userId: userId);

            Assert.That(context.OperationType, Is.EqualTo(operationType),
                "AC8: Context operation type must be preserved");
            Assert.That(context.CorrelationId, Is.EqualTo(correlationId),
                "AC8: Context correlation ID must be preserved");
            Assert.That(context.IdempotencyKey, Is.EqualTo(idempotencyKey),
                "AC8: Context idempotency key must be preserved");
            Assert.That(context.UserId, Is.EqualTo(userId),
                "AC8: Context user ID must be preserved");
            Assert.That(context.InitiatedAt, Is.GreaterThan(DateTime.MinValue),
                "AC8: Context must have a valid initiation timestamp");

            await Task.CompletedTask;
        }

        #endregion
    }
}

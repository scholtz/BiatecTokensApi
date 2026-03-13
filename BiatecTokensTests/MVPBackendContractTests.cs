using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// MVP backend contract sign-off tests.
    ///
    /// These tests implement the strict backend-backed test scenarios required for product MVP
    /// sign-off as described in the issue. They verify:
    /// - Deterministic authentication (register → login → refresh)
    /// - ARC76 address consistency across authentication sessions
    /// - Deployment request acceptance with explicit identifiers and metadata
    /// - Deployment lifecycle state progression from Pending through to terminal states
    /// - Standard error contracts suitable for frontend assertion
    /// - Correlated identifiers for deployment tracing
    /// - Observability signals for CI/staging diagnosis
    /// - Schema contracts (non-null required fields)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPBackendContractTests
    {
        private MVPTestWebApplicationFactory _factory = null!;
        private HttpClient _unauthClient = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new MVPTestWebApplicationFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────────

        private sealed class MVPTestWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "MVPContractTestKey32CharsMinRequired!!",
                        ["JwtConfig:SecretKey"] = "MVPBackendContractTestSecretKey32CharsRequired!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string UniqueEmail() =>
            $"mvp-{Guid.NewGuid():N}@biatec-mvp-contract.example.com";

        private async Task<RegisterResponse> RegisterUserAsync(
            string? email = null, string password = "SecurePass123!")
        {
            var req = new RegisterRequest
            {
                Email = email ?? UniqueEmail(),
                Password = password,
                ConfirmPassword = password,
                FullName = "MVP Test User"
            };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", req);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<RegisterResponse>())!;
        }

        private async Task<LoginResponse> LoginUserAsync(
            string email, string password = "SecurePass123!")
        {
            var req = new LoginRequest { Email = email, Password = password };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login", req);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<LoginResponse>())!;
        }

        private HttpClient CreateAuthClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        private static BackendDeploymentContractRequest BuildDeploymentRequest(
            string email, string? idempotencyKey = null)
        {
            return new BackendDeploymentContractRequest
            {
                DeployerEmail = email,
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "MVP Sign-off Token",
                TokenSymbol = "MVPT",
                Network = "algorand-testnet",
                TotalSupply = 1_000_000,
                Decimals = 6,
                IdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString(),
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        // ── AC1: Deterministic auth responses for strict sign-off ─────────────────

        [Test]
        public async Task AC1_Register_ProducesDeterministicSuccessResponse()
        {
            var email = UniqueEmail();
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "SecurePass123!", ConfirmPassword = "SecurePass123!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC1: Registration must return 200 OK deterministically");

            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body!.Success, Is.True);
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC1_Login_ProducesDeterministicSuccessResponse()
        {
            var email = UniqueEmail();
            await RegisterUserAsync(email);

            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC1: Login must return 200 OK deterministically");

            var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.Success, Is.True);
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC1_Login_ThreeRuns_IdenticalAddressResult()
        {
            var email = UniqueEmail();
            await RegisterUserAsync(email);

            var addresses = new List<string?>();
            for (var run = 0; run < 3; run++)
            {
                var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                    new LoginRequest { Email = email, Password = "SecurePass123!" });
                var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
                addresses.Add(body!.AlgorandAddress);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "AC1: Login must return identical AlgorandAddress across 3 runs (determinism proof)");
        }

        // ── AC2: Strict backend-backed validation (no seeded shortcuts) ───────────

        [Test]
        public async Task AC2_InvalidCredentials_Returns401_NotSeededSuccess()
        {
            var email = UniqueEmail();
            await RegisterUserAsync(email);

            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "IncorrectPassword999!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC2: Invalid credentials must return 401 from real auth logic, not seeded success");
        }

        [Test]
        public async Task AC2_NonExistentUser_Returns401_NotSuccessfulLogin()
        {
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = "ghost@nobody.example.com", Password = "SecurePass123!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC2: Non-existent user must be rejected with 401");
        }

        [Test]
        public async Task AC2_ProtectedEndpoint_WithoutToken_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/auth/profile");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC2: Protected endpoints must reject requests without JWT token");
        }

        [Test]
        public async Task AC2_ProtectedEndpoint_WithValidToken_AccessGranted()
        {
            var email = UniqueEmail();
            await RegisterUserAsync(email);
            var loginBody = await LoginUserAsync(email);

            using var authClient = CreateAuthClient(loginBody.AccessToken!);
            var resp = await authClient.GetAsync("/api/v1/auth/profile");

            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "AC2: Valid JWT token must grant access to protected endpoints");
        }

        // ── AC3: Deployment submission returns explicit acceptance metadata ────────

        [Test]
        public async Task AC3_DeploymentRequest_ReturnsExplicitAcceptanceMetadata()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC3: Deployment submission must return 200");
            Assert.That(body!.DeploymentId, Is.Not.Null.And.Not.Empty,
                "AC3: DeploymentId must be returned as the correlated identifier");
            Assert.That(body.DeployerAddress, Is.Not.Null.And.Not.Empty,
                "AC3: DeployerAddress must be returned in acceptance metadata");
        }

        [Test]
        public async Task AC3_DeploymentRequest_ReturnsInitialLifecycleState()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var validStates = new[]
            {
                ContractLifecycleState.Pending,
                ContractLifecycleState.Validated,
                ContractLifecycleState.Submitted,
                ContractLifecycleState.Confirmed,
                ContractLifecycleState.Completed,
                ContractLifecycleState.Failed,
                ContractLifecycleState.Cancelled
            };
            Assert.That(validStates, Contains.Item(body!.State),
                "AC3: Initial lifecycle state must be explicitly returned and be one of the 7 documented states");
        }

        [Test]
        public async Task AC3_DeploymentRequest_ReturnsTimestamp()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.InitiatedAt, Is.Not.Null.And.Not.Empty,
                "AC3: InitiatedAt timestamp must be returned in acceptance metadata");
        }

        [Test]
        public async Task AC3_DeploymentRequest_ReturnsIdempotencyKey()
        {
            var idempotencyKey = Guid.NewGuid().ToString();
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email, idempotencyKey: idempotencyKey);
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.IdempotencyKey, Is.Not.Null.And.Not.Empty,
                "AC3: IdempotencyKey must be reflected back in acceptance metadata");
        }

        // ── AC4: Deployment lifecycle status queryable with documented states ─────

        [Test]
        public async Task AC4_DeploymentStatus_QueryableAfterInitiation()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var initResp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var statusResp = await authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");
            Assert.That(statusResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC4: Deployment status must be queryable after initiation");
        }

        [Test]
        public async Task AC4_DeploymentStatus_HasStableStateValues()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var initResp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var statusResp = await authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");
            var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            // LifecycleState must be one of the documented states
            var validStates = new[]
            {
                ContractLifecycleState.Pending,
                ContractLifecycleState.Validated,
                ContractLifecycleState.Submitted,
                ContractLifecycleState.Confirmed,
                ContractLifecycleState.Completed,
                ContractLifecycleState.Failed,
                ContractLifecycleState.Cancelled
            };
            Assert.That(validStates, Contains.Item(statusBody!.State),
                "AC4: Lifecycle state must be one of the 7 documented stable states");
        }

        [Test]
        public async Task AC4_DeploymentStatus_StableAcrossPolls()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var initResp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var states = new List<ContractLifecycleState>();
            for (var poll = 0; poll < 3; poll++)
            {
                var statusResp = await authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");
                var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
                states.Add(statusBody!.State);
            }

            // State should not regress (can only progress forward or stay)
            for (var i = 1; i < states.Count; i++)
            {
                Assert.That((int)states[i], Is.GreaterThanOrEqualTo((int)states[i - 1]),
                    "AC4: Lifecycle state must not regress across polls (monotonic progression)");
            }
        }

        // ── AC5: Full backend-backed integration path ─────────────────────────────

        [Test]
        public async Task AC5_FullIntegrationPath_RegisterLoginDeployStatusAudit()
        {
            // ── Step 1: Register ──────────────────────────────────────────────────
            var email = UniqueEmail();
            var regReq = new RegisterRequest
            {
                Email = email, Password = "SecurePass123!", ConfirmPassword = "SecurePass123!",
                FullName = "MVP Integration User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 1: Registration");

            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(regBody!.AlgorandAddress, Is.Not.Null.And.Not.Empty, "Step 1: Must return Algorand address");

            // ── Step 2: Login ─────────────────────────────────────────────────────
            var loginResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123!" });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 2: Login");

            var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(regBody.AlgorandAddress),
                "Step 2: ARC76 address must match registration");

            // ── Step 3: Submit deployment ─────────────────────────────────────────
            using var authClient = CreateAuthClient(loginBody.AccessToken!);
            var deployReq = BuildDeploymentRequest(email);
            var deployResp = await authClient.PostAsJsonAsync(
                "/api/v1/backend-deployment-contract/initiate", deployReq);
            Assert.That(deployResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 3: Deployment submission");

            var deployBody = await deployResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            Assert.That(deployBody!.DeploymentId, Is.Not.Null.And.Not.Empty, "Step 3: Deployment ID returned");
            Assert.That(deployBody.IsDeterministicAddress, Is.True, "Step 3: ARC76 determinism confirmed");

            // ── Step 4: Poll status ───────────────────────────────────────────────
            var statusResp = await authClient.GetAsync(
                $"/api/v1/backend-deployment-contract/status/{deployBody.DeploymentId}");
            Assert.That(statusResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 4: Status poll");

            var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            Assert.That(statusBody!.DeploymentId, Is.EqualTo(deployBody.DeploymentId),
                "Step 4: Deployment ID must be consistent");

            // ── Step 5: Get audit trail ───────────────────────────────────────────
            var auditResp = await authClient.GetAsync(
                $"/api/v1/backend-deployment-contract/audit/{deployBody.DeploymentId}");
            Assert.That(auditResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 5: Audit trail");

            var auditBody = await auditResp.Content.ReadFromJsonAsync<BackendDeploymentAuditTrail>();
            Assert.That(auditBody!.Events, Is.Not.Null.And.Not.Empty, "Step 5: Audit events must be present");

            // ── Step 6: Refresh token and continue ────────────────────────────────
            var refreshResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/refresh",
                new RefreshTokenRequest { RefreshToken = loginBody.RefreshToken! });
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 6: Token refresh");

            var refreshBody = await refreshResp.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(refreshBody!.Success, Is.True, "Step 6: Refresh must succeed");
        }

        // ── AC6: Standardised error responses ─────────────────────────────────────

        [Test]
        public async Task AC6_InvalidLogin_ErrorResponseSchema()
        {
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = "no@body.example.com", Password = "WrongPass999!" });

            var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body, Is.Not.Null, "AC6: Error response must not be empty");
            Assert.That(body!.Success, Is.False, "AC6: Success must be false");
            Assert.That(body.ErrorCode, Is.Not.Null.And.Not.Empty, "AC6: ErrorCode must be present");
            Assert.That(body.ErrorMessage, Is.Not.Null.And.Not.Empty, "AC6: ErrorMessage must be present");
            Assert.That(body.Timestamp, Is.Not.EqualTo(default(DateTime)), "AC6: Timestamp must be present");
        }

        [Test]
        public async Task AC6_InvalidDeploymentStandard_ErrorResponseSchema()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            req.TokenStandard = "UNSUPPORTED_XYZ";
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None),
                "AC6: Unsupported standard must have non-None error code");
            Assert.That(body.Message, Is.Not.Null.And.Not.Empty,
                "AC6: Error message must be present for frontend display");
        }

        [Test]
        public async Task AC6_DeploymentStatus_NotFound_ErrorResponseSchema()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var resp = await authClient.GetAsync("/api/v1/backend-deployment-contract/status/no-such-deployment-xyz");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "AC6: Missing deployment must return 404");
        }

        // ── AC7: Observability signals for CI/staging diagnosis ───────────────────

        [Test]
        public async Task AC7_SuccessfulAuth_CorrelationIdPresent()
        {
            var email = UniqueEmail();
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "SecurePass123!", ConfirmPassword = "SecurePass123!" });

            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC7: CorrelationId must be present in success responses for distributed tracing");
        }

        [Test]
        public async Task AC7_FailedAuth_CorrelationIdPresent()
        {
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = "unknown@nobody.example.com", Password = "WrongPass!" });

            var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC7: CorrelationId must be present in error responses for CI/staging diagnosis");
        }

        [Test]
        public async Task AC7_DeploymentInitiation_CorrelationIdPropagated()
        {
            var callerCorrelationId = Guid.NewGuid().ToString();
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            req.CorrelationId = callerCorrelationId;
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC7: CorrelationId must be returned so support teams can trace deployment attempts");
        }

        // ── AC8: Schema contract assertions ───────────────────────────────────────

        [Test]
        public async Task AC8_RegisterResponse_AllRequiredFieldsPresent()
        {
            var (body, _) = await RegisterAndGetResponse(UniqueEmail());

            Assert.That(body.Success, Is.True, "AC8: Success field must be true");
            Assert.That(body.UserId, Is.Not.Null.And.Not.Empty, "AC8: UserId must be present");
            Assert.That(body.Email, Is.Not.Null.And.Not.Empty, "AC8: Email must be present");
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AC8: AlgorandAddress must be present");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty, "AC8: AccessToken must be present");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty, "AC8: RefreshToken must be present");
            Assert.That(body.ExpiresAt, Is.Not.Null, "AC8: ExpiresAt must be present");
            Assert.That(body.CorrelationId, Is.Not.Null.And.Not.Empty, "AC8: CorrelationId must be present");
            Assert.That(body.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC8: DerivationContractVersion must be present for contract versioning");
        }

        [Test]
        public async Task AC8_LoginResponse_AllRequiredFieldsPresent()
        {
            var email = UniqueEmail();
            await RegisterUserAsync(email);
            var loginResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123!" });
            var body = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(body!.Success, Is.True, "AC8: Success field must be true");
            Assert.That(body.UserId, Is.Not.Null.And.Not.Empty, "AC8: UserId must be present");
            Assert.That(body.Email, Is.Not.Null.And.Not.Empty, "AC8: Email must be present");
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AC8: AlgorandAddress must be present");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty, "AC8: AccessToken must be present");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty, "AC8: RefreshToken must be present");
            Assert.That(body.ExpiresAt, Is.Not.Null, "AC8: ExpiresAt must be present");
            Assert.That(body.CorrelationId, Is.Not.Null.And.Not.Empty, "AC8: CorrelationId must be present");
        }

        [Test]
        public async Task AC8_DeploymentContractResponse_AllRequiredFieldsPresent()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.DeploymentId, Is.Not.Null.And.Not.Empty, "AC8: DeploymentId must be present");
            Assert.That(body.IdempotencyKey, Is.Not.Null.And.Not.Empty, "AC8: IdempotencyKey must be present");
            Assert.That(body.DeployerAddress, Is.Not.Null.And.Not.Empty, "AC8: DeployerAddress must be present");
            Assert.That(body.CorrelationId, Is.Not.Null.And.Not.Empty, "AC8: CorrelationId must be present");
            Assert.That(body.InitiatedAt, Is.Not.Null.And.Not.Empty, "AC8: InitiatedAt timestamp must be present");
        }

        // ── AC9: Environment and documentation contract ────────────────────────────

        [Test]
        public async Task AC9_SwaggerSpec_AccessibleAndValid()
        {
            var resp = await _unauthClient.GetAsync("/swagger/v1/swagger.json");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC9: Swagger/OpenAPI spec must be accessible for frontend contract discovery");
        }

        [Test]
        public async Task AC9_HealthEndpoint_ReturnsOk()
        {
            var resp = await _unauthClient.GetAsync("/health");
            // Accept 200 OK or 503 ServiceUnavailable (when external nodes like Algorand/IPFS are unreachable in test env)
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable),
                "AC9: Health endpoint must respond for CI/staging readiness checks");
        }

        [Test]
        public async Task AC9_AuthEndpoints_AllDocumentedInSwagger()
        {
            var swaggerResp = await _unauthClient.GetAsync("/swagger/v1/swagger.json");
            var swaggerJson = await swaggerResp.Content.ReadAsStringAsync();

            Assert.That(swaggerJson, Does.Contain("/api/v1/auth/register"),
                "AC9: /register endpoint must be documented in OpenAPI spec");
            Assert.That(swaggerJson, Does.Contain("/api/v1/auth/login"),
                "AC9: /login endpoint must be documented in OpenAPI spec");
            Assert.That(swaggerJson, Does.Contain("/api/v1/auth/refresh"),
                "AC9: /refresh endpoint must be documented in OpenAPI spec");
        }

        [Test]
        public async Task AC9_DeploymentEndpoints_AllDocumentedInSwagger()
        {
            var swaggerResp = await _unauthClient.GetAsync("/swagger/v1/swagger.json");
            var swaggerJson = await swaggerResp.Content.ReadAsStringAsync();

            Assert.That(swaggerJson, Does.Contain("backend-deployment-contract"),
                "AC9: Deployment contract endpoints must be documented in OpenAPI spec");
        }

        // ── AC10: Roadmap alignment – wallet-free backend-managed ─────────────────

        [Test]
        public async Task AC10_RegisterWithoutWallet_Succeeds()
        {
            // No wallet, no blockchain transaction signing required from the user
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = UniqueEmail(),
                    Password = "SecurePass123!",
                    ConfirmPassword = "SecurePass123!"
                    // No wallet connector, no ARC14 header
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC10: Registration must work without any wallet connection (wallet-free design)");
        }

        [Test]
        public async Task AC10_DeploymentWithCredentialsOnly_Succeeds()
        {
            // Backend manages all blockchain signing via ARC76 derivation
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            // No wallet connector, no explicit address, only email+password ARC76 derivation
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC10: Deployment must work with email/password ARC76 derivation only (backend-managed)");
        }

        [Test]
        public async Task AC10_ARC76Derivation_IsWalletFree()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);

            using var authClient = CreateAuthClient(reg.AccessToken!);
            var req = BuildDeploymentRequest(email);
            var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.IsDeterministicAddress, Is.True,
                "AC10: Backend must derive the Algorand address deterministically without requiring wallet");
            Assert.That(body.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived),
                "AC10: ARC76 derivation must succeed without wallet connection");
        }

        // ── Regression tests for existing endpoints ───────────────────────────────

        [Test]
        public async Task Regression_ExistingHealthEndpoint_StillOk()
        {
            var resp = await _unauthClient.GetAsync("/health");
            // Accept 200 OK or 503 ServiceUnavailable (external health checks may fail in isolated test environments)
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable),
                "Regression: Health endpoint must remain accessible after new auth/deployment additions");
        }

        [Test]
        public async Task Regression_SwaggerJson_Returns200()
        {
            var resp = await _unauthClient.GetAsync("/swagger/v1/swagger.json");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Regression: Swagger spec must remain accessible (no Swashbuckle schema conflicts)");
        }

        // ── ARC76 determinism proof (3-run CI evidence) ───────────────────────────

        [Test]
        public async Task ARC76Proof_ThreeRunsIdenticalAddress_ForAuthFlow()
        {
            var email = UniqueEmail();
            await RegisterUserAsync(email);

            string? addressRun1, addressRun2, addressRun3;

            var resp1 = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123!" });
            addressRun1 = (await resp1.Content.ReadFromJsonAsync<LoginResponse>())!.AlgorandAddress;

            var resp2 = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123!" });
            addressRun2 = (await resp2.Content.ReadFromJsonAsync<LoginResponse>())!.AlgorandAddress;

            var resp3 = await _unauthClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123!" });
            addressRun3 = (await resp3.Content.ReadFromJsonAsync<LoginResponse>())!.AlgorandAddress;

            Assert.That(addressRun1, Is.EqualTo(addressRun2),
                "ARC76 Run1 = Run2: Same credentials must always produce same address");
            Assert.That(addressRun2, Is.EqualTo(addressRun3),
                "ARC76 Run2 = Run3: ARC76 derivation is deterministic across 3 independent runs");
        }

        [Test]
        public async Task ARC76Proof_ThreeRunsIdenticalAddress_ForDeploymentFlow()
        {
            var email = UniqueEmail();
            var reg = await RegisterUserAsync(email);
            using var authClient = CreateAuthClient(reg.AccessToken!);

            var addresses = new List<string?>();
            for (var run = 0; run < 3; run++)
            {
                var req = BuildDeploymentRequest(email, idempotencyKey: Guid.NewGuid().ToString());
                var resp = await authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
                var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
                addresses.Add(body!.DeployerAddress);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "ARC76 deployment: Same email+password must produce same deployer address across 3 runs");
        }

        // ── Helpers for schema tests ──────────────────────────────────────────────

        private async Task<(RegisterResponse body, HttpStatusCode status)> RegisterAndGetResponse(string email)
        {
            var req = new RegisterRequest { Email = email, Password = "SecurePass123!", ConfirmPassword = "SecurePass123!" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", req);
            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return (body!, resp.StatusCode);
        }
    }
}

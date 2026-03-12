using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the backend deployment lifecycle contract endpoints.
    ///
    /// These tests validate:
    /// - BackendDeploymentLifecycleContractController (POST /api/v1/backend-deployment-contract/initiate,
    ///   GET /status/{id}, POST /validate, GET /audit/{id})
    /// - Deployment contract lifecycle state progression
    /// - Idempotency semantics
    /// - ARC76 address derivation within deployment context
    /// - Error taxonomy for deployment failures
    /// - Compliance audit trail availability
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeploymentLifecycleIntegrationTests
    {
        private DeploymentTestWebApplicationFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string _jwtToken = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new DeploymentTestWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            // Register and login to get a JWT for authenticated endpoint tests
            var email = $"deploy-test-{Guid.NewGuid():N}@biatec-mvp-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Deployment Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            _jwtToken = regBody?.AccessToken ?? string.Empty;

            _authClient = _factory.CreateClient();
            _authClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _unauthClient?.Dispose();
            _authClient?.Dispose();
            _factory?.Dispose();
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────────

        private sealed class DeploymentTestWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "DeployTestKey32CharsMinimumRequired!!",
                        ["JwtConfig:SecretKey"] = "DeploymentLifecycleTestSecretKey32CharsRequired!!",
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

        private static BackendDeploymentContractRequest BuildValidARC76Request(
            string? email = null,
            string? idempotencyKey = null,
            string network = "algorand-testnet",
            string standard = "ASA")
        {
            return new BackendDeploymentContractRequest
            {
                DeployerEmail = email ?? $"deployer-{Guid.NewGuid():N}@biatec-mvp-test.example.com",
                DeployerPassword = "SecurePass123!",
                TokenStandard = standard,
                TokenName = "MVP Test Token",
                TokenSymbol = "MVT",
                Network = network,
                TotalSupply = 1_000_000,
                Decimals = 6,
                IdempotencyKey = idempotencyKey,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        private static BackendDeploymentContractRequest BuildExplicitAddressRequest(
            string algorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
        {
            return new BackendDeploymentContractRequest
            {
                ExplicitDeployerAddress = algorandAddress,
                TokenStandard = "ASA",
                TokenName = "Explicit Address Token",
                TokenSymbol = "EAT",
                Network = "algorand-testnet",
                TotalSupply = 500_000,
                Decimals = 0,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        // ── Contract initiation tests ─────────────────────────────────────────────

        [Test]
        public async Task Initiate_WithoutAuth_Returns401()
        {
            var req = BuildValidARC76Request();
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Deployment initiation must require authentication");
        }

        [Test]
        public async Task Initiate_ValidARC76Request_Returns200()
        {
            var req = BuildValidARC76Request();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid ARC76 deployment initiation must return 200");
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsDeploymentId()
        {
            var req = BuildValidARC76Request();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body, Is.Not.Null);
            Assert.That(body!.DeploymentId, Is.Not.Null.And.Not.Empty,
                "Deployment ID must be returned as the correlated identifier");
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsLifecycleState()
        {
            var req = BuildValidARC76Request();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.State, Is.Not.EqualTo(default(ContractLifecycleState)),
                "Lifecycle state must be returned after initiation");
        }

        [Test]
        public async Task Initiate_ARC76Request_IsDeterministicAddress()
        {
            var email = $"arc76-det-{Guid.NewGuid():N}@biatec-mvp-test.example.com";
            var req = BuildValidARC76Request(email: email);
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.IsDeterministicAddress, Is.True,
                "ARC76-derived deployments must flag IsDeterministicAddress=true");
        }

        [Test]
        public async Task Initiate_ARC76Request_ReturnsDerivationStatus()
        {
            var req = BuildValidARC76Request();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived),
                "Successful ARC76 derivation must return Derived status");
        }

        [Test]
        public async Task Initiate_ARC76Request_ReturnsDeployerAddress()
        {
            var req = BuildValidARC76Request();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.DeployerAddress, Is.Not.Null.And.Not.Empty,
                "Derived deployer address must be returned for frontend display");
        }

        [Test]
        public async Task Initiate_SameEmailAndPassword_ReturnsSameAddress()
        {
            var email = $"determ-{Guid.NewGuid():N}@biatec-mvp-test.example.com";
            var req1 = BuildValidARC76Request(email: email, idempotencyKey: Guid.NewGuid().ToString());
            var req2 = BuildValidARC76Request(email: email, idempotencyKey: Guid.NewGuid().ToString());

            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req1);
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req2);

            var body1 = await resp1.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            var body2 = await resp2.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body1!.DeployerAddress, Is.EqualTo(body2!.DeployerAddress),
                "Same email+password must always derive the same ARC76 address (determinism contract)");
        }

        [Test]
        public async Task Initiate_NullBody_Returns400()
        {
            var resp = await _authClient.PostAsync("/api/v1/backend-deployment-contract/initiate",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Null request body must return 400");
        }

        [Test]
        public async Task Initiate_MissingTokenStandard_ReturnsBadRequest()
        {
            var req = BuildValidARC76Request();
            req.TokenStandard = string.Empty;
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

            Assert.That((int)resp.StatusCode, Is.AnyOf(400, 200),
                "Missing token standard should be handled gracefully");
            // If 200, check error code in body
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
                Assert.That(body!.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing),
                    "Missing TokenStandard must produce RequiredFieldMissing error code");
            }
        }

        [Test]
        public async Task Initiate_UnsupportedTokenStandard_ReturnsUnsupportedError()
        {
            var req = BuildValidARC76Request();
            req.TokenStandard = "UNSUPPORTED_STANDARD";
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.ErrorCode, Is.EqualTo(DeploymentErrorCode.UnsupportedStandard),
                "Unsupported token standard must return UnsupportedStandard error code");
        }

        [Test]
        public async Task Initiate_InvalidNetwork_ReturnsNetworkError()
        {
            var req = BuildValidARC76Request(network: "nonexistent-chain-xyz");
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.ErrorCode, Is.EqualTo(DeploymentErrorCode.NetworkUnavailable),
                "Invalid network must return NetworkUnavailable error code");
        }

        // ── Idempotency tests ─────────────────────────────────────────────────────

        [Test]
        public async Task Initiate_SameIdempotencyKey_ReturnsCachedResult()
        {
            var idempotencyKey = Guid.NewGuid().ToString();
            var req = BuildValidARC76Request(idempotencyKey: idempotencyKey);

            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

            var body1 = await resp1.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            var body2 = await resp2.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body1!.DeploymentId, Is.EqualTo(body2!.DeploymentId),
                "Same idempotency key must return the same deployment ID");
            Assert.That(body2.IsIdempotentReplay, Is.True,
                "Replay must be flagged with IsIdempotentReplay=true");
        }

        [Test]
        public async Task Initiate_IdempotentReplay_ThreeTimesDeterministic()
        {
            var idempotencyKey = Guid.NewGuid().ToString();
            var req = BuildValidARC76Request(idempotencyKey: idempotencyKey);

            var responses = new List<BackendDeploymentContractResponse?>();
            for (var i = 0; i < 3; i++)
            {
                var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
                var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
                responses.Add(body);
            }

            var deploymentIds = responses.Select(r => r!.DeploymentId).ToList();
            Assert.That(deploymentIds.Distinct().Count(), Is.EqualTo(1),
                "Same idempotency key must return same deployment ID across 3 replays");
        }

        [Test]
        public async Task Initiate_DifferentIdempotencyKeys_CreateDifferentDeployments()
        {
            var req1 = BuildValidARC76Request(idempotencyKey: Guid.NewGuid().ToString());
            var req2 = BuildValidARC76Request(idempotencyKey: Guid.NewGuid().ToString());

            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req1);
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req2);

            var body1 = await resp1.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            var body2 = await resp2.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body1!.DeploymentId, Is.Not.EqualTo(body2!.DeploymentId),
                "Different idempotency keys must create different deployment contracts");
        }

        // ── Status query tests ────────────────────────────────────────────────────

        [Test]
        public async Task GetStatus_WithoutAuth_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/backend-deployment-contract/status/some-id");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Status query must require authentication");
        }

        [Test]
        public async Task GetStatus_ExistingDeployment_Returns200()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var statusResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");

            Assert.That(statusResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Status query for existing deployment must return 200");
        }

        [Test]
        public async Task GetStatus_ExistingDeployment_ReturnsLifecycleState()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var statusResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");
            var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(statusBody!.DeploymentId, Is.EqualTo(initBody.DeploymentId),
                "Status response must contain the correct deployment ID");
            Assert.That(statusBody.State, Is.Not.EqualTo(default(ContractLifecycleState)),
                "Lifecycle state must be present in status response");
        }

        [Test]
        public async Task GetStatus_NonExistentDeployment_Returns404()
        {
            var resp = await _authClient.GetAsync("/api/v1/backend-deployment-contract/status/nonexistent-deployment-id-xyz");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "Status query for non-existent deployment must return 404");
        }

        [Test]
        public async Task GetStatus_MatchesInitiateResponse()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var statusResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");
            var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(statusBody!.DeployerAddress, Is.EqualTo(initBody.DeployerAddress),
                "Deployer address in status must match the one from initiation");
        }

        // ── Validation endpoint tests ─────────────────────────────────────────────

        [Test]
        public async Task Validate_WithoutAuth_Returns401()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = "test@test.com",
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 1000
            };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Validate endpoint must require authentication");
        }

        [Test]
        public async Task Validate_ValidRequest_Returns200()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = $"valid-{Guid.NewGuid():N}@biatec-mvp-test.example.com",
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "Validation Test Token",
                TokenSymbol = "VTT",
                Network = "algorand-testnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid inputs must pass validation and return 200");
        }

        [Test]
        public async Task Validate_ValidRequest_ReturnsIsValidTrue()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = $"valid-{Guid.NewGuid():N}@biatec-mvp-test.example.com",
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "Validation Test Token",
                TokenSymbol = "VTT",
                Network = "algorand-testnet",
                TotalSupply = 1_000_000,
                Decimals = 0
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractValidationResponse>();

            Assert.That(body!.IsValid, Is.True,
                "Valid request must return IsValid=true from validation endpoint");
        }

        [Test]
        public async Task Validate_ValidRequest_ReturnsARC76DerivationResult()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = $"arc76-val-{Guid.NewGuid():N}@biatec-mvp-test.example.com",
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "ARC76 Validation Token",
                TokenSymbol = "AVT",
                Network = "algorand-testnet",
                TotalSupply = 1000
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractValidationResponse>();

            Assert.That(body!.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived),
                "Validation with valid credentials must show Derived ARC76 status");
            Assert.That(body.DeployerAddress, Is.Not.Null.And.Not.Empty,
                "Validation must return the derived Algorand address");
        }

        [Test]
        public async Task Validate_MissingRequired_Returns200WithErrors()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                TokenStandard = string.Empty, // Missing
                TokenName = string.Empty,     // Missing
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 1000
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractValidationResponse>();

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Validation endpoint returns 200 even for invalid inputs (reports errors in body)");
            Assert.That(body!.IsValid, Is.False,
                "Invalid inputs must return IsValid=false");
            Assert.That(body.ValidationResults, Is.Not.Null.And.Not.Empty,
                "Validation errors must be returned as a list");
        }

        [Test]
        public async Task Validate_InvalidNetwork_Returns200WithNetworkError()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = $"net-{Guid.NewGuid():N}@biatec-mvp-test.example.com",
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "Network Test",
                TokenSymbol = "NT",
                Network = "invalid-network-xyz",
                TotalSupply = 1000
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractValidationResponse>();

            Assert.That(body!.IsValid, Is.False,
                "Invalid network must fail validation");
        }

        [Test]
        public async Task Validate_NullBody_Returns400()
        {
            var resp = await _authClient.PostAsync("/api/v1/backend-deployment-contract/validate",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Null request body must return 400");
        }

        // ── Audit trail tests ─────────────────────────────────────────────────────

        [Test]
        public async Task Audit_WithoutAuth_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/backend-deployment-contract/audit/some-id");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Audit endpoint must require authentication");
        }

        [Test]
        public async Task Audit_ExistingDeployment_Returns200()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var auditResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/audit/{initBody!.DeploymentId}");

            Assert.That(auditResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Audit trail for existing deployment must return 200");
        }

        [Test]
        public async Task Audit_ExistingDeployment_HasAuditEvents()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var auditResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/audit/{initBody!.DeploymentId}");
            var auditBody = await auditResp.Content.ReadFromJsonAsync<BackendDeploymentAuditTrail>();

            Assert.That(auditBody!.Events, Is.Not.Null.And.Not.Empty,
                "Audit trail must contain at least the initiation event");
        }

        [Test]
        public async Task Audit_ExistingDeployment_FirstEventIsInitiation()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var auditResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/audit/{initBody!.DeploymentId}");
            var auditBody = await auditResp.Content.ReadFromJsonAsync<BackendDeploymentAuditTrail>();

            Assert.That(auditBody!.Events.First().EventKind, Is.EqualTo(ComplianceAuditEventKind.DeploymentInitiated),
                "First audit event must be DeploymentInitiated");
        }

        [Test]
        public async Task Audit_ExistingDeployment_ContainsCorrelationId()
        {
            var correlationId = Guid.NewGuid().ToString();
            var req = BuildValidARC76Request();
            req.CorrelationId = correlationId;
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var auditResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/audit/{initBody!.DeploymentId}");
            var auditBody = await auditResp.Content.ReadFromJsonAsync<BackendDeploymentAuditTrail>();

            Assert.That(auditBody!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Audit trail must carry a correlation ID for distributed tracing");
        }

        [Test]
        public async Task Audit_NonExistentDeployment_Returns404()
        {
            var resp = await _authClient.GetAsync("/api/v1/backend-deployment-contract/audit/nonexistent-xyz-999");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "Audit for non-existent deployment must return 404");
        }

        // ── Error taxonomy tests ──────────────────────────────────────────────────

        [Test]
        public async Task Initiate_WithBothARC76AndExplicitAddress_ProducesValidResponse()
        {
            var req = BuildValidARC76Request();
            req.ExplicitDeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Providing both ARC76 credentials and explicit address must not cause 5xx");
        }

        [Test]
        public async Task Initiate_AllSupportedStandards_DoNotReturn5xx()
        {
            var standards = new[] { "ASA", "ARC3", "ARC200", "ERC20" };

            foreach (var standard in standards)
            {
                var network = standard == "ERC20" ? "base-mainnet" : "algorand-testnet";
                var req = BuildValidARC76Request(network: network, standard: standard);
                req.IdempotencyKey = Guid.NewGuid().ToString();
                var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);

                Assert.That((int)resp.StatusCode, Is.LessThan(500),
                    $"Token standard {standard} must not cause 5xx error");
            }
        }

        [Test]
        public async Task Initiate_ZeroTotalSupply_ProducesValidationError()
        {
            var req = BuildValidARC76Request();
            req.TotalSupply = 0;
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            // Either returns validation error or a non-5xx status
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Zero total supply must not cause 5xx error");
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                Assert.That(body!.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None),
                    "Zero total supply must produce an error code");
            }
        }

        [Test]
        public async Task Initiate_MissingBothAddressAndCredentials_ReturnsAuthError()
        {
            var req = new BackendDeploymentContractRequest
            {
                TokenStandard = "ASA",
                TokenName = "No Auth Token",
                TokenSymbol = "NAT",
                Network = "algorand-testnet",
                TotalSupply = 1000,
                Decimals = 0
                // No DeployerEmail, DeployerPassword, or ExplicitDeployerAddress
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(body!.ErrorCode, Is.AnyOf(
                DeploymentErrorCode.RequiredFieldMissing,
                DeploymentErrorCode.InvalidCredentials,
                DeploymentErrorCode.AuthenticationFailed),
                "Missing deployer information must produce appropriate error code");
        }

        // ── State machine tests ───────────────────────────────────────────────────

        [Test]
        public async Task Initiate_NewRequest_StartsInValidLifecycleState()
        {
            var req = BuildValidARC76Request();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var validInitialStates = new[]
            {
                ContractLifecycleState.Pending,
                ContractLifecycleState.Validated,
                ContractLifecycleState.Submitted,
                ContractLifecycleState.Confirmed,
                ContractLifecycleState.Completed,
                ContractLifecycleState.Failed,
                ContractLifecycleState.Cancelled
            };
            Assert.That(validInitialStates, Contains.Item(body!.State),
                "New deployment must start in a valid documented lifecycle state");
        }

        [Test]
        public async Task GetStatus_AfterInitiate_ReturnsSameStateAsInitiate()
        {
            var req = BuildValidARC76Request();
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            var statusResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody!.DeploymentId}");
            var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            Assert.That(statusBody!.State, Is.EqualTo(initBody.State),
                "Status query immediately after initiation must return same lifecycle state");
        }

        // ── Full contract flow E2E tests ───────────────────────────────────────────

        [Test]
        public async Task E2E_InitiateGetStatusGetAudit_FullContractFlow()
        {
            var correlationId = Guid.NewGuid().ToString();
            var email = $"e2e-{Guid.NewGuid():N}@biatec-mvp-test.example.com";

            // Step 1: Initiate
            var req = BuildValidARC76Request(email: email);
            req.CorrelationId = correlationId;
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            Assert.That(initiateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 1: Initiate must succeed");

            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            Assert.That(initBody!.DeploymentId, Is.Not.Null.And.Not.Empty, "Step 1: Deployment ID must be returned");

            // Step 2: Get status
            var statusResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/status/{initBody.DeploymentId}");
            Assert.That(statusResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 2: Status query must succeed");

            var statusBody = await statusResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();
            Assert.That(statusBody!.DeploymentId, Is.EqualTo(initBody.DeploymentId), "Step 2: Same deployment ID");

            // Step 3: Get audit trail
            var auditResp = await _authClient.GetAsync($"/api/v1/backend-deployment-contract/audit/{initBody.DeploymentId}");
            Assert.That(auditResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 3: Audit trail must be accessible");

            var auditBody = await auditResp.Content.ReadFromJsonAsync<BackendDeploymentAuditTrail>();
            Assert.That(auditBody!.Events, Is.Not.Null.And.Not.Empty, "Step 3: Audit must have events");
        }

        [Test]
        public async Task E2E_ValidateBeforeInitiate_ConsistentAddressResult()
        {
            var email = $"pre-val-{Guid.NewGuid():N}@biatec-mvp-test.example.com";

            // Step 1: Validate (dry-run)
            var valReq = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = email,
                DeployerPassword = "SecurePass123!",
                TokenStandard = "ASA",
                TokenName = "PreValidate Token",
                TokenSymbol = "PVT",
                Network = "algorand-testnet",
                TotalSupply = 1000,
                Decimals = 0
            };
            var valResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/validate", valReq);
            var valBody = await valResp.Content.ReadFromJsonAsync<BackendDeploymentContractValidationResponse>();
            Assert.That(valBody!.IsValid, Is.True, "Step 1: Validation must pass");

            // Step 2: Initiate
            var req = BuildValidARC76Request(email: email);
            var initiateResp = await _authClient.PostAsJsonAsync("/api/v1/backend-deployment-contract/initiate", req);
            var initBody = await initiateResp.Content.ReadFromJsonAsync<BackendDeploymentContractResponse>();

            // Addresses should match (same email+password = same ARC76 address)
            Assert.That(initBody!.DeployerAddress, Is.EqualTo(valBody.DeployerAddress),
                "Validate and Initiate must produce same ARC76 address for same credentials");
        }
    }
}

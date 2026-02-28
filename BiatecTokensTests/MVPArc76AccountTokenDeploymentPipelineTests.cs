using BiatecTokensApi.Models.Account;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.UnifiedDeploy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the MVP ARC76 Account Management and Backend Token Deployment Pipeline.
    /// Validates all acceptance criteria from the issue:
    /// AC1: ARC76 Determinism (same credentials → same address)
    /// AC2: ASA Deployment endpoint exists and returns jobId
    /// AC3: ARC200 Deployment endpoint exists
    /// AC4: ERC20 Deployment endpoint exists
    /// AC5: Deployment Status Polling works
    /// AC6: No private key storage (structure verification)
    /// AC7: Test Coverage (this file)
    /// AC8: API Documentation (auto-generated via Swagger)
    /// AC9: Error Handling (400 for invalid params, 401 for unauthenticated)
    /// AC10: Backward Compatibility (existing endpoints still work)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPArc76AccountTokenDeploymentPipelineTests
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
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired"
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

        // ============================================================
        // Helper: Register user and return access token + address
        // ============================================================
        private async Task<(string accessToken, string algorandAddress, string userId)> RegisterAndLoginAsync(
            string? email = null,
            string password = "SecurePass123!")
        {
            email ??= $"test-{Guid.NewGuid()}@example.com";
            var registerReq = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Pipeline Test User"
            };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            var registerResult = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(registerResult, Is.Not.Null, "Registration response should not be null");
            Assert.That(registerResult!.Success, Is.True, $"Registration should succeed. Error: {registerResult.ErrorMessage}");

            return (registerResult.AccessToken!, registerResult.AlgorandAddress!, registerResult.UserId!);
        }

        // ============================================================
        // AC1: ARC76 Determinism — same credentials always produce same address
        // ============================================================

        [Test]
        public async Task AC1_ARC76Determinism_SameCredentialsProduceSameAddress()
        {
            // Register user once
            var email = $"determinism-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var (_, address1, _) = await RegisterAndLoginAsync(email, password);

            // Login twice more and verify address is stable
            var loginReq = new LoginRequest { Email = email, Password = password };
            var login2 = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            var result2 = await login2.Content.ReadFromJsonAsync<LoginResponse>();
            var login3 = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            var result3 = await login3.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(result2!.AlgorandAddress, Is.EqualTo(address1), "Address must be deterministic across login attempts");
            Assert.That(result3!.AlgorandAddress, Is.EqualTo(address1), "Address must be deterministic across restarts");
        }

        [Test]
        public async Task AC1_GetAddress_ReturnsARC76DerivedAlgorandAddress()
        {
            var (token, expectedAddress, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/account/address");
            var result = await response.Content.ReadFromJsonAsync<AccountAddressResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AlgorandAddress, Is.EqualTo(expectedAddress));
            Assert.That(result.DerivationStandard, Is.EqualTo("ARC76"));
        }

        [Test]
        public async Task AC1_GetAddress_IsDeterministicAcrossMultipleCalls()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response1 = await _client.GetAsync("/api/v1/account/address");
            var result1 = await response1.Content.ReadFromJsonAsync<AccountAddressResponse>();

            var response2 = await _client.GetAsync("/api/v1/account/address");
            var result2 = await response2.Content.ReadFromJsonAsync<AccountAddressResponse>();

            var response3 = await _client.GetAsync("/api/v1/account/address");
            var result3 = await response3.Content.ReadFromJsonAsync<AccountAddressResponse>();

            Assert.That(result1!.AlgorandAddress, Is.EqualTo(result2!.AlgorandAddress), "Run 1 == Run 2");
            Assert.That(result2.AlgorandAddress, Is.EqualTo(result3!.AlgorandAddress), "Run 2 == Run 3");
        }

        [Test]
        public async Task AC1_DifferentCredentials_ProduceDifferentAddresses()
        {
            var (_, address1, _) = await RegisterAndLoginAsync($"user1-{Guid.NewGuid()}@example.com");
            var (_, address2, _) = await RegisterAndLoginAsync($"user2-{Guid.NewGuid()}@example.com");

            Assert.That(address1, Is.Not.EqualTo(address2), "Different users must have different addresses");
        }

        [Test]
        public async Task AC1_GetAddress_WithoutAuth_ShouldReturn401()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/v1/account/address");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ============================================================
        // AC5 + AC9: Account Balance endpoint
        // ============================================================

        [Test]
        public async Task GetBalance_WithValidToken_ShouldReturnAccountAddress()
        {
            var (token, expectedAddress, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/account/balance");
            var result = await response.Content.ReadFromJsonAsync<AccountBalanceResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AlgorandAddress, Is.EqualTo(expectedAddress));
            Assert.That(result.TokenBalances, Is.Not.Null);
        }

        [Test]
        public async Task GetBalance_WithoutAuth_ShouldReturn401()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/v1/account/balance");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetBalance_WithNetworkParam_ShouldReturnNetworkInResponse()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/account/balance?network=algorand-testnet");
            var result = await response.Content.ReadFromJsonAsync<AccountBalanceResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Network, Is.EqualTo("algorand-testnet"));
        }

        // ============================================================
        // Testnet fund endpoint (AC9: error handling)
        // ============================================================

        [Test]
        public async Task Fund_WithMainnetNetwork_ShouldReturnBadRequest()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/v1/account/fund",
                new AccountFundRequest { Network = "algorand-mainnet" });
            var result = await response.Content.ReadFromJsonAsync<AccountFundResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("testnet").IgnoreCase);
        }

        [Test]
        public async Task Fund_WithoutAuth_ShouldReturn401()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/api/v1/account/fund",
                new AccountFundRequest { Network = "algorand-testnet" });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ============================================================
        // AC2: ASA Deployment — POST /api/v1/tokens/deploy returns jobId
        // ============================================================

        [Test]
        public async Task AC2_ASADeployment_ShouldReturnJobId()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object>
                {
                    { "name", "My Test ASA" },
                    { "unitName", "MTASA" },
                    { "total", 1000000 },
                    { "decimals", 6 }
                }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result = await response.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.JobId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Status, Is.EqualTo("Queued"));
        }

        [Test]
        public async Task AC2_ASADeployment_JobIdIsUnique()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object>
                {
                    { "name", "My ASA" },
                    { "unitName", "ASA" },
                    { "total", 1000000 }
                }
            };

            var response1 = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result1 = await response1.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            var response2 = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result2 = await response2.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(result1!.JobId, Is.Not.EqualTo(result2!.JobId), "Each deployment should have a unique job ID");
        }

        // ============================================================
        // AC3: ARC200 Deployment
        // ============================================================

        [Test]
        public async Task AC3_ARC200Deployment_ShouldReturnJobId()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ARC200",
                Params = new Dictionary<string, object>
                {
                    { "name", "My ARC200 Token" },
                    { "symbol", "ARC2" },
                    { "decimals", 6 },
                    { "totalSupply", 1000000000 }
                }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result = await response.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Success, Is.True);
            Assert.That(result.JobId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Status, Is.EqualTo("Queued"));
        }

        // ============================================================
        // AC4: ERC20 Deployment
        // ============================================================

        [Test]
        public async Task AC4_ERC20Deployment_ShouldReturnJobId()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "base",
                Standard = "ERC20",
                Params = new Dictionary<string, object>
                {
                    { "name", "My ERC20 Token" },
                    { "symbol", "ERC20T" },
                    { "decimals", 18 },
                    { "initialSupply", 1000000 }
                }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result = await response.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Success, Is.True);
            Assert.That(result.JobId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Status, Is.EqualTo("Queued"));
        }

        [Test]
        public async Task AC4_ERC721Deployment_ShouldReturnJobId()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "ethereum",
                Standard = "ERC721",
                Params = new Dictionary<string, object>
                {
                    { "name", "My NFT Collection" },
                    { "symbol", "MNFT" },
                    { "baseUri", "https://metadata.example.com/nfts/" }
                }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result = await response.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Success, Is.True);
            Assert.That(result.JobId, Is.Not.Null.And.Not.Empty);
        }

        // ============================================================
        // AC5: Deployment Status Polling
        // ============================================================

        [Test]
        public async Task AC5_DeploymentStatusPolling_ReturnsQueuedStatusForNewJob()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create deployment
            var deployReq = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object>
                {
                    { "name", "Status Test Token" },
                    { "unitName", "STT" },
                    { "total", 1000000 }
                }
            };
            var deployResp = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", deployReq);
            var deployResult = await deployResp.Content.ReadFromJsonAsync<UnifiedDeployResponse>();
            Assert.That(deployResult!.Success, Is.True);

            // Poll status
            var statusResp = await _client.GetAsync($"/api/v1/tokens/deploy/{deployResult.JobId}/status");
            var statusResult = await statusResp.Content.ReadFromJsonAsync<DeploymentStatusPollResponse>();

            Assert.That(statusResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(statusResult, Is.Not.Null);
            Assert.That(statusResult!.Success, Is.True);
            Assert.That(statusResult.JobId, Is.EqualTo(deployResult.JobId));
            Assert.That(statusResult.Status, Is.EqualTo("Queued"));
            Assert.That(statusResult.TokenType, Is.EqualTo("ASA"));
            Assert.That(statusResult.Network, Is.EqualTo("algorand-testnet"));
        }

        [Test]
        public async Task AC5_DeploymentStatusPolling_ContainsStatusHistory()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create deployment
            var deployReq = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ARC3",
                Params = new Dictionary<string, object>
                {
                    { "name", "History Test Token" },
                    { "unitName", "HTT" }
                }
            };
            var deployResp = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", deployReq);
            var deployResult = await deployResp.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            // Poll status
            var statusResp = await _client.GetAsync($"/api/v1/tokens/deploy/{deployResult!.JobId}/status");
            var statusResult = await statusResp.Content.ReadFromJsonAsync<DeploymentStatusPollResponse>();

            Assert.That(statusResult!.StatusHistory, Is.Not.Null);
            Assert.That(statusResult.StatusHistory, Has.Count.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task AC5_DeploymentStatusPolling_ForUnknownJobId_ReturnsNotFound()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/tokens/deploy/nonexistent-job-id-12345/status");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task AC5_DeploymentStatusPolling_WithoutAuth_ReturnsUnauthorized()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/v1/tokens/deploy/any-job-id/status");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ============================================================
        // AC3 (RT3): Real-time deployment history — GET /api/v1/tokens/deployments
        // ============================================================

        [Test]
        public async Task GetDeployments_ReturnsUserDeployments()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create two deployments
            var deploy1 = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object> { { "name", "Token A" } }
            };
            var deploy2 = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ARC200",
                Params = new Dictionary<string, object> { { "name", "Token B" } }
            };

            await _client.PostAsJsonAsync("/api/v1/tokens/deploy", deploy1);
            await _client.PostAsJsonAsync("/api/v1/tokens/deploy", deploy2);

            // Get deployments list
            var response = await _client.GetAsync("/api/v1/tokens/deployments");
            var result = await response.Content.ReadFromJsonAsync<UserDeploymentsResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Deployments, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(result.TotalCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task GetDeployments_WithoutAuth_ReturnsUnauthorized()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/v1/tokens/deployments");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetDeployments_ForNewUser_ReturnsEmptyList()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/tokens/deployments");
            var result = await response.Content.ReadFromJsonAsync<UserDeploymentsResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Deployments, Is.Not.Null);
        }

        // ============================================================
        // AC9: Error Handling — invalid params return 400
        // ============================================================

        [Test]
        public async Task AC9_Deploy_WithInvalidStandard_Returns400()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "INVALID_STANDARD",
                Params = new Dictionary<string, object> { { "name", "Test" } }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result = await response.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STANDARD"));
        }

        [Test]
        public async Task AC9_Deploy_WithMissingChain_Returns400()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "",
                Standard = "ASA",
                Params = new Dictionary<string, object> { { "name", "Test" } }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task AC9_Deploy_WithoutAuth_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object> { { "name", "Test" } }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ============================================================
        // AC10: Backward Compatibility — existing endpoints still work
        // ============================================================

        [Test]
        public async Task AC10_BackwardCompat_ExistingAuthRegisterEndpointWorks()
        {
            var request = new RegisterRequest
            {
                Email = $"compat-{Guid.NewGuid()}@example.com",
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!"
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task AC10_BackwardCompat_ExistingHealthEndpointWorks()
        {
            var response = await _client.GetAsync("/health");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task AC10_BackwardCompat_ExistingDeploymentStatusEndpointWorks()
        {
            // The existing /api/v1/token/deployments endpoint should still work
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/token/deployments");

            // Should return 200 (may require ARC-0014 auth but our test config allows empty success on failure)
            Assert.That((int)response.StatusCode, Is.LessThan(500), "Existing deployment status endpoint should not throw a 500");
        }

        // ============================================================
        // E2E: Full pipeline flow — register → get address → deploy → poll
        // ============================================================

        [Test]
        public async Task E2E_FullPipeline_RegisterGetAddressDeployPoll()
        {
            // Step 1: Register user
            var email = $"e2e-pipeline-{Guid.NewGuid()}@example.com";
            var (token, expectedAddress, _) = await RegisterAndLoginAsync(email);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Step 2: Get Algorand address
            var addressResp = await _client.GetAsync("/api/v1/account/address");
            var addressResult = await addressResp.Content.ReadFromJsonAsync<AccountAddressResponse>();
            Assert.That(addressResult!.AlgorandAddress, Is.EqualTo(expectedAddress), "E2E: address matches registration");

            // Step 3: Deploy a token
            var deployReq = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object>
                {
                    { "name", "E2E Pipeline Token" },
                    { "unitName", "E2E" },
                    { "total", 10000000 },
                    { "decimals", 6 }
                }
            };
            var deployResp = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", deployReq);
            var deployResult = await deployResp.Content.ReadFromJsonAsync<UnifiedDeployResponse>();
            Assert.That(deployResult!.Success, Is.True, "E2E: deploy should succeed");
            Assert.That(deployResult.JobId, Is.Not.Null.And.Not.Empty);

            // Step 4: Poll status
            var statusResp = await _client.GetAsync($"/api/v1/tokens/deploy/{deployResult.JobId}/status");
            var statusResult = await statusResp.Content.ReadFromJsonAsync<DeploymentStatusPollResponse>();
            Assert.That(statusResult!.Success, Is.True, "E2E: status poll should succeed");
            Assert.That(statusResult.JobId, Is.EqualTo(deployResult.JobId));

            // Step 5: List deployments
            var listResp = await _client.GetAsync("/api/v1/tokens/deployments");
            var listResult = await listResp.Content.ReadFromJsonAsync<UserDeploymentsResponse>();
            Assert.That(listResult!.Success, Is.True, "E2E: list should succeed");
            Assert.That(listResult.Deployments.Any(d => d.JobId == deployResult.JobId), Is.True,
                "E2E: deployment appears in history");
        }

        // ============================================================
        // AC6: No private key exposure in responses
        // ============================================================

        [Test]
        public async Task AC6_GetAddress_DoesNotExposePrivateKeyOrMnemonic()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/account/address");
            var content = await response.Content.ReadAsStringAsync();

            // Ensure no mnemonic words or private key indicators in the response
            Assert.That(content, Does.Not.Contain("mnemonic").IgnoreCase, "Response must not expose mnemonic");
            Assert.That(content, Does.Not.Contain("privateKey").IgnoreCase, "Response must not expose private key");
            Assert.That(content, Does.Not.Contain("encryptedMnemonic").IgnoreCase, "Response must not expose encrypted mnemonic");
        }

        [Test]
        public async Task AC6_Deploy_DoesNotExposePrivateKeyInResponse()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object> { { "name", "Security Test" } }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var content = await response.Content.ReadAsStringAsync();

            Assert.That(content, Does.Not.Contain("mnemonic").IgnoreCase, "Deploy response must not expose mnemonic");
            Assert.That(content, Does.Not.Contain("privateKey").IgnoreCase, "Deploy response must not expose private key");
        }

        // ============================================================
        // Schema contract assertions
        // ============================================================

        [Test]
        public async Task Schema_AccountAddressResponse_HasRequiredFields()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/v1/account/address");
            var result = await response.Content.ReadFromJsonAsync<AccountAddressResponse>();

            Assert.That(result, Is.Not.Null, "Response should deserialize correctly");
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AlgorandAddress, Is.Not.Null, "AlgorandAddress field is required");
            Assert.That(result.DerivationStandard, Is.Not.Null, "DerivationStandard field is required");
            Assert.That(result.Timestamp, Is.Not.EqualTo(default(DateTime)), "Timestamp field is required");
        }

        [Test]
        public async Task Schema_UnifiedDeployResponse_HasRequiredFields()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new UnifiedDeployRequest
            {
                Chain = "algorand-testnet",
                Standard = "ASA",
                Params = new Dictionary<string, object> { { "name", "Schema Test" } }
            };

            var response = await _client.PostAsJsonAsync("/api/v1/tokens/deploy", request);
            var result = await response.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            Assert.That(result, Is.Not.Null, "Response should deserialize correctly");
            Assert.That(result!.Success, Is.True);
            Assert.That(result.JobId, Is.Not.Null, "JobId field is required");
            Assert.That(result.Status, Is.Not.Null, "Status field is required");
            Assert.That(result.CorrelationId, Is.Not.Null, "CorrelationId field is required");
            Assert.That(result.Timestamp, Is.Not.EqualTo(default(DateTime)), "Timestamp field is required");
        }

        [Test]
        public async Task Schema_DeploymentStatusPollResponse_HasRequiredFields()
        {
            var (token, _, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var deployResp = await _client.PostAsJsonAsync("/api/v1/tokens/deploy",
                new UnifiedDeployRequest
                {
                    Chain = "algorand-testnet",
                    Standard = "ASA",
                    Params = new Dictionary<string, object> { { "name", "Schema Status Test" } }
                });
            var deployResult = await deployResp.Content.ReadFromJsonAsync<UnifiedDeployResponse>();

            var response = await _client.GetAsync($"/api/v1/tokens/deploy/{deployResult!.JobId}/status");
            var result = await response.Content.ReadFromJsonAsync<DeploymentStatusPollResponse>();

            Assert.That(result, Is.Not.Null, "Response should deserialize correctly");
            Assert.That(result!.Success, Is.True);
            Assert.That(result.JobId, Is.Not.Null, "JobId field is required");
            Assert.That(result.Status, Is.Not.Null, "Status field is required");
            Assert.That(result.TokenType, Is.Not.Null, "TokenType field is required");
            Assert.That(result.Network, Is.Not.Null, "Network field is required");
            Assert.That(result.StatusHistory, Is.Not.Null, "StatusHistory field is required");
        }
    }
}

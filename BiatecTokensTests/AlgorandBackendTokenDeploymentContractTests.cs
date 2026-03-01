using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.AlgorandApi;
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
    /// Integration tests covering the backend token deployment acceptance criteria from the
    /// "Complete ARC76 Account Management and backend token deployment" issue.
    ///
    /// These tests validate:
    /// - AC3: POST /api/algorand/asa/create — Create ASA token
    /// - AC4: POST /api/algorand/arc3/create — Create ARC3 asset
    /// - AC5: POST /api/algorand/arc200/deploy — Deploy ARC200 smart contract
    /// - AC6: GET /api/algorand/account/info — Account address + balance
    /// - AC7: GET /api/algorand/transaction/{txId}/status — Transaction status
    /// - AC8: Error handling (400 / 401 / 503)
    /// - AC9: Audit logging (CorrelationId present in all responses)
    /// - AC10: OpenAPI schema registration for all endpoints
    ///
    /// Note: Actual blockchain interactions are not tested (no testnet keys available in CI).
    /// Tests focus on authentication gates, validation, and HTTP contract compliance.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AlgorandBackendTokenDeploymentContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        // ─────────────────────────────────────────────────────────────────────
        // Setup / TearDown
        // ─────────────────────────────────────────────────────────────────────

        [SetUp]
        public void Setup()
        {
            var configuration = new Dictionary<string, string?>
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
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired"
            };

            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(configuration);
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
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> GetAccessTokenAsync(string? email = null, string? password = null)
        {
            email ??= $"algo-test-{Guid.NewGuid()}@example.com";
            password ??= "AlgoTest@Pass1!";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult?.AccessToken, Is.Not.Null.And.Not.Empty, "Login must return access token");
            return loginResult!.AccessToken!;
        }

        private HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC6: GET /api/algorand/account/info
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC6_GetAccountInfo_Returns401_WhenUnauthenticated()
        {
            var response = await _client.GetAsync("/api/algorand/account/info");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "GET /api/algorand/account/info must return 401 for unauthenticated requests");
        }

        [Test]
        public async Task AC6_GetAccountInfo_ReturnsAlgorandAddress_WhenAuthenticated()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/account/info");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "GET /api/algorand/account/info must return 200 for authenticated users");

            var result = await response.Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True, "Response must indicate success");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Response must include the user's ARC76-derived Algorand address");
            Assert.That(result.DerivationStandard, Is.EqualTo("ARC76"),
                "DerivationStandard must be ARC76");
        }

        [Test]
        public async Task AC6_GetAccountInfo_ContainsCorrelationId()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/account/info");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>();
            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be present in response for audit logging (AC9)");
        }

        [Test]
        public async Task AC6_GetAccountInfo_ReturnsDeterministicAddress_AcrossMultipleCalls()
        {
            // Same user should always get the same address (ARC76 determinism)
            var email = $"algo-det-{Guid.NewGuid()}@example.com";
            const string password = "AlgoDet@Pass1!";

            await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            using var authClient = CreateAuthenticatedClient(loginResult!.AccessToken!);

            var response1 = await authClient.GetAsync("/api/algorand/account/info");
            var response2 = await authClient.GetAsync("/api/algorand/account/info");

            var result1 = await response1.Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>();
            var result2 = await response2.Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>();

            Assert.That(result1!.AlgorandAddress, Is.EqualTo(result2!.AlgorandAddress),
                "ARC76 account info must return the same address on every call (determinism)");
        }

        [Test]
        public async Task AC6_GetAccountInfo_AcceptsNetworkParameter()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/account/info?network=testnet-v1.0");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "GET /api/algorand/account/info with network param must return 200");

            var result = await response.Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>();
            Assert.That(result!.Network, Is.EqualTo("testnet-v1.0"),
                "Response must echo back the requested network");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3: POST /api/algorand/asa/create
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC3_CreateASAToken_Returns401_WhenUnauthenticated()
        {
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1_000_000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            var response = await _client.PostAsJsonAsync("/api/algorand/asa/create", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "POST /api/algorand/asa/create must return 401 for unauthenticated requests (AC8)");
        }

        [Test]
        public async Task AC3_CreateASAToken_Returns400_WhenNameMissing()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            // Missing required Name field
            var response = await authClient.PostAsJsonAsync("/api/algorand/asa/create", new
            {
                UnitName = "TEST",
                TotalSupply = 1_000_000,
                Network = "testnet-v1.0"
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "POST /api/algorand/asa/create must return 400 when required Name is missing (AC8)");
        }

        [Test]
        public async Task AC3_CreateASAToken_Returns400_WhenTotalSupplyIsZero()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 0, // invalid - must be > 0
                Network = "testnet-v1.0"
            };

            var response = await authClient.PostAsJsonAsync("/api/algorand/asa/create", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "POST /api/algorand/asa/create must return 400 when TotalSupply is 0 (AC8)");
        }

        [Test]
        public async Task AC3_CreateASAToken_Returns400_WhenNetworkMissing()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            // Send only a partial body without Network
            var response = await authClient.PostAsJsonAsync("/api/algorand/asa/create", new
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1_000_000
                // Network missing
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "POST /api/algorand/asa/create must return 400 when Network is missing (AC8)");
        }

        [Test]
        public async Task AC3_CreateASAToken_Endpoint_IsRegisteredInOpenAPI()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var paths = doc.RootElement.GetProperty("paths");

            Assert.That(paths.TryGetProperty("/api/algorand/asa/create", out _), Is.True,
                "POST /api/algorand/asa/create must be registered in OpenAPI spec (AC10)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC4: POST /api/algorand/arc3/create
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC4_CreateARC3Token_Returns401_WhenUnauthenticated()
        {
            var response = await _client.PostAsJsonAsync("/api/algorand/arc3/create", new
            {
                Name = "My ARC3 Token",
                UnitName = "ARC3",
                TotalSupply = 1_000_000,
                Network = "testnet-v1.0",
                Metadata = new { }
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "POST /api/algorand/arc3/create must return 401 for unauthenticated requests (AC8)");
        }

        [Test]
        public async Task AC4_CreateARC3Token_Returns400_WhenNameMissing()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.PostAsJsonAsync("/api/algorand/arc3/create", new
            {
                UnitName = "ARC3",
                TotalSupply = 1_000_000,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = "Test",
                    Description = "Test token"
                }
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "POST /api/algorand/arc3/create must return 400 when Name is missing (AC8)");
        }

        [Test]
        public async Task AC4_CreateARC3Token_Endpoint_IsRegisteredInOpenAPI()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var paths = doc.RootElement.GetProperty("paths");

            Assert.That(paths.TryGetProperty("/api/algorand/arc3/create", out _), Is.True,
                "POST /api/algorand/arc3/create must be registered in OpenAPI spec (AC10)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC5: POST /api/algorand/arc200/deploy
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC5_DeployARC200Token_Returns401_WhenUnauthenticated()
        {
            var response = await _client.PostAsJsonAsync("/api/algorand/arc200/deploy", new
            {
                Name = "My ARC200 Token",
                Symbol = "ARC200",
                InitialSupply = 1_000_000,
                Cap = 10_000_000,
                Network = "testnet-v1.0"
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "POST /api/algorand/arc200/deploy must return 401 for unauthenticated requests (AC8)");
        }

        [Test]
        public async Task AC5_DeployARC200Token_Returns400_WhenNameMissing()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.PostAsJsonAsync("/api/algorand/arc200/deploy", new
            {
                Symbol = "ARC200",
                InitialSupply = 1_000_000,
                Cap = 10_000_000,
                Network = "testnet-v1.0"
                // Name missing
            });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "POST /api/algorand/arc200/deploy must return 400 when Name is missing (AC8)");
        }

        [Test]
        public async Task AC5_DeployARC200Token_Endpoint_IsRegisteredInOpenAPI()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var paths = doc.RootElement.GetProperty("paths");

            Assert.That(paths.TryGetProperty("/api/algorand/arc200/deploy", out _), Is.True,
                "POST /api/algorand/arc200/deploy must be registered in OpenAPI spec (AC10)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC7: GET /api/algorand/transaction/{txId}/status
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC7_GetTransactionStatus_Returns401_WhenUnauthenticated()
        {
            var response = await _client.GetAsync("/api/algorand/transaction/SOME_TX_ID/status");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "GET /api/algorand/transaction/{txId}/status must return 401 for unauthenticated requests (AC8)");
        }

        [Test]
        public async Task AC7_GetTransactionStatus_Returns404_ForUnknownTxId()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/transaction/UNKNOWN_TX_ID_THAT_DOES_NOT_EXIST/status");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "GET /api/algorand/transaction/{txId}/status must return 404 for unknown transaction IDs");

            var result = await response.Content.ReadFromJsonAsync<AlgorandTransactionStatusResponse>();
            Assert.That(result!.Success, Is.False,
                "Response must indicate failure for unknown transaction");
            Assert.That(result.Status, Is.EqualTo("Unknown"),
                "Status must be 'Unknown' for untracked transaction IDs");
        }

        [Test]
        public async Task AC7_GetTransactionStatus_Endpoint_IsRegisteredInOpenAPI()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var paths = doc.RootElement.GetProperty("paths");

            Assert.That(paths.TryGetProperty("/api/algorand/transaction/{txId}/status", out _), Is.True,
                "GET /api/algorand/transaction/{txId}/status must be registered in OpenAPI spec (AC10)");
        }

        [Test]
        public async Task AC7_GetTransactionStatus_ContainsCorrelationId()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/transaction/ANY_TX_ID/status");

            // Should be 404 for unknown txId, but the response must still have correlation ID (AC9)
            var result = await response.Content.ReadFromJsonAsync<AlgorandTransactionStatusResponse>();
            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be present in all transaction status responses for audit logging (AC9)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC8: Error handling — various status codes
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC8_AllAlgorandEndpoints_Return401_WithInvalidToken()
        {
            using var invalidClient = _factory.CreateClient();
            invalidClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            var asaResponse = await invalidClient.PostAsJsonAsync("/api/algorand/asa/create", new { });
            var arc3Response = await invalidClient.PostAsJsonAsync("/api/algorand/arc3/create", new { });
            var arc200Response = await invalidClient.PostAsJsonAsync("/api/algorand/arc200/deploy", new { });
            var accountInfoResponse = await invalidClient.GetAsync("/api/algorand/account/info");
            var txStatusResponse = await invalidClient.GetAsync("/api/algorand/transaction/TX_ID/status");

            Assert.That(asaResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "POST /api/algorand/asa/create must return 401 with invalid token");
            Assert.That(arc3Response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "POST /api/algorand/arc3/create must return 401 with invalid token");
            Assert.That(arc200Response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "POST /api/algorand/arc200/deploy must return 401 with invalid token");
            Assert.That(accountInfoResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "GET /api/algorand/account/info must return 401 with invalid token");
            Assert.That(txStatusResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "GET /api/algorand/transaction/{txId}/status must return 401 with invalid token");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC9: Audit logging — CorrelationId in all responses
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC9_AccountInfo_ResponseContainsAuditFields()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/account/info");
            var result = await response.Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be present for audit logging");
            Assert.That(result.Timestamp, Is.Not.EqualTo(default(DateTime)),
                "Timestamp must be present for audit logging");
        }

        [Test]
        public async Task AC9_TransactionStatus_ResponseContainsAuditFields()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/transaction/AUDIT_TEST_TX_ID/status");
            var result = await response.Content.ReadFromJsonAsync<AlgorandTransactionStatusResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be present for audit logging (AC9)");
            Assert.That(result.Timestamp, Is.Not.EqualTo(default(DateTime)),
                "Timestamp must be present for audit logging (AC9)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC10: OpenAPI schema — all 5 endpoints registered
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC10_AllAlgorandEndpoints_AreRegisteredInOpenAPI()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var paths = doc.RootElement.GetProperty("paths");

            Assert.That(paths.TryGetProperty("/api/algorand/asa/create", out _), Is.True,
                "POST /api/algorand/asa/create must be registered in OpenAPI");
            Assert.That(paths.TryGetProperty("/api/algorand/arc3/create", out _), Is.True,
                "POST /api/algorand/arc3/create must be registered in OpenAPI");
            Assert.That(paths.TryGetProperty("/api/algorand/arc200/deploy", out _), Is.True,
                "POST /api/algorand/arc200/deploy must be registered in OpenAPI");
            Assert.That(paths.TryGetProperty("/api/algorand/account/info", out _), Is.True,
                "GET /api/algorand/account/info must be registered in OpenAPI");
            Assert.That(paths.TryGetProperty("/api/algorand/transaction/{txId}/status", out _), Is.True,
                "GET /api/algorand/transaction/{txId}/status must be registered in OpenAPI");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2: No key persistence — private key not in responses
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC2_AccountInfo_DoesNotExposePrivateKeyMaterial()
        {
            var token = await GetAccessTokenAsync();
            using var authClient = CreateAuthenticatedClient(token);

            var response = await authClient.GetAsync("/api/algorand/account/info");
            var rawJson = await response.Content.ReadAsStringAsync();

            // Response must not contain any field that looks like a mnemonic or private key
            Assert.That(rawJson.ToLower(), Does.Not.Contain("mnemonic"),
                "Response must not expose mnemonic phrase (AC2)");
            Assert.That(rawJson.ToLower(), Does.Not.Contain("privatekey"),
                "Response must not expose private key (AC2)");
            Assert.That(rawJson.ToLower(), Does.Not.Contain("private_key"),
                "Response must not expose private key (AC2)");
            Assert.That(rawJson.ToLower(), Does.Not.Contain("secretkey"),
                "Response must not expose secret key (AC2)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC1: ARC76 derivation — same user always gets same address
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC1_AccountInfo_ReturnsDeterministicAddress_AcrossLoginSessions()
        {
            var email = $"algo-arc76-{Guid.NewGuid()}@example.com";
            const string password = "AlgoARC76@Pass1!";

            // Register
            await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });

            // Session 1
            var login1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var token1 = (await login1.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken!;
            using var client1 = CreateAuthenticatedClient(token1);
            var info1 = (await (await client1.GetAsync("/api/algorand/account/info"))
                .Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>())!;

            // Session 2
            var login2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var token2 = (await login2.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken!;
            using var client2 = CreateAuthenticatedClient(token2);
            var info2 = (await (await client2.GetAsync("/api/algorand/account/info"))
                .Content.ReadFromJsonAsync<AlgorandAccountInfoResponse>())!;

            Assert.That(info1.AlgorandAddress, Is.EqualTo(info2.AlgorandAddress),
                "ARC76 derivation must be deterministic: same user always gets same Algorand address across sessions (AC1)");
        }
    }
}

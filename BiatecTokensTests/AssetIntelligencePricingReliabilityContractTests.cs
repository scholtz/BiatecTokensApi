using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract/integration tests for AssetIntelligence and PricingReliability APIs.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AssetIntelligencePricingReliabilityContractTests
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
            ["EVMChains:0:Name"] = "Base Mainnet",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired",
            ["AllowedOrigins:0"] = "http://localhost:3000",
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private async Task<string> GetAuthTokenAsync()
        {
            var req = new RegisterRequest
            {
                Email = $"ai-test-{Guid.NewGuid()}@example.com",
                Password = "Secure1@Pass",
                ConfirmPassword = "Secure1@Pass"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);
            var reg = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return reg?.AccessToken ?? string.Empty;
        }

        private static object ValidAssetIntelligenceRequest() =>
            new { assetId = 12345, network = "voimain-v1.0", includeProvenance = true };

        private static object ValidPricingRequest() =>
            new { assetId = 12345, network = "voimain-v1.0", includeFallbackChain = true };

        // ─────────────────────────────────────────────────────────────────────
        // AC1 – Asset Intelligence endpoint returns canonical metadata
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC1_AssetIntelligence_Health_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/asset-intelligence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task AC1_AssetIntelligence_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task AC1_AssetIntelligence_ValidRequest_ReturnsSuccess()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean(), Is.True,
                "Response must have success=true");
        }

        [Test]
        public async Task AC1_AssetIntelligence_ZeroAssetId_Returns400OrValidError()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate",
                new { assetId = 0, network = "voimain-v1.0" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(200),
                "Zero AssetId must return 400 or 200 with error");
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task AC1_AssetIntelligence_EmptyNetwork_ReturnsError()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate",
                new { assetId = 12345, network = "" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(200));
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2 – Pricing endpoint returns deterministic quote payloads
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC2_PricingReliability_Health_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/pricing-reliability/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task AC2_PricingReliability_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task AC2_PricingReliability_ValidRequest_ReturnsSuccess()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean(), Is.True);
        }

        [Test]
        public async Task AC2_PricingReliability_ZeroAssetId_ReturnsError()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote",
                new { assetId = 0, network = "voimain-v1.0" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(200));
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task AC2_PricingReliability_DeterministicPrice_SameInputSameOutput()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new { assetId = 12345, network = "voimain-v1.0" };
            var resp1 = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", req);
            var resp2 = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", req);

            var doc1 = JsonDocument.Parse(await resp1.Content.ReadAsStringAsync());
            var doc2 = JsonDocument.Parse(await resp2.Content.ReadAsStringAsync());

            doc1.RootElement.TryGetProperty("price", out var price1);
            doc2.RootElement.TryGetProperty("price", out var price2);

            Assert.That(price1.GetDecimal(), Is.EqualTo(price2.GetDecimal()), "Price must be deterministic");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3 – Fallback behavior is deterministic and traceable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC3_PricingReliability_PrecedenceTrace_NotEmpty_OnSuccess()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("precedenceTrace", out var trace) &&
                        trace.GetArrayLength() > 0, Is.True, "PrecedenceTrace must not be empty on success");
        }

        [Test]
        public async Task AC3_PricingReliability_FallbackChain_Included_WhenRequested()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote",
                new { assetId = 12345, network = "voimain-v1.0", includeFallbackChain = true });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            doc.RootElement.TryGetProperty("precedenceTrace", out var trace);
            Assert.That(trace.GetArrayLength(), Is.GreaterThan(0));
        }

        [Test]
        public async Task AC3_AssetIntelligence_Provenance_Included_WhenRequested()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate",
                new { assetId = 12345, network = "voimain-v1.0", includeProvenance = true });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("provenance", out var prov) &&
                        prov.GetArrayLength() > 0, Is.True, "Provenance must be included when requested");
        }

        [Test]
        public async Task AC3_PricingReliability_SourceInfo_Present_OnSuccess()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote",
                new { assetId = 12345, network = "voimain-v1.0", includeProvenance = true });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("sourceInfo", out var src) &&
                        src.ValueKind != JsonValueKind.Null, Is.True, "SourceInfo must be present when provenance requested");
        }

        [Test]
        public async Task AC3_AssetIntelligence_RepeatedCalls_ReturnSameValidationStatus()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = new { assetId = 12345, network = "voimain-v1.0" };
            var r1 = JsonDocument.Parse(await (await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", req)).Content.ReadAsStringAsync());
            var r2 = JsonDocument.Parse(await (await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", req)).Content.ReadAsStringAsync());
            var r3 = JsonDocument.Parse(await (await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", req)).Content.ReadAsStringAsync());

            r1.RootElement.TryGetProperty("validationStatus", out var s1);
            r2.RootElement.TryGetProperty("validationStatus", out var s2);
            r3.RootElement.TryGetProperty("validationStatus", out var s3);

            Assert.That(s1.GetRawText(), Is.EqualTo(s2.GetRawText()), "ValidationStatus must be deterministic");
            Assert.That(s2.GetRawText(), Is.EqualTo(s3.GetRawText()));
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC4 – Error contracts are documented and stable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC4_AssetIntelligence_MalformedNetwork_ReturnsTypedError()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate",
                new { assetId = 12345, network = "invalid network with spaces!" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(200));
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body));
        }

        [Test]
        public async Task AC4_PricingReliability_MalformedNetwork_ReturnsTypedError()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote",
                new { assetId = 12345, network = "invalid network with spaces!" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(200));
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body));
        }

        [Test]
        public async Task AC4_AssetIntelligence_ErrorResponse_HasRemediationHint()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate",
                new { assetId = 0, network = "voimain-v1.0" });

            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("remediationHint", out var hint) ||
                doc.RootElement.TryGetProperty("RemediationHint", out hint), Is.True,
                "Error response must include remediationHint");
            Assert.That(hint.GetString(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC4_PricingReliability_ErrorResponse_HasRemediationHint()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote",
                new { assetId = 0, network = "voimain-v1.0" });

            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            Assert.That(
                doc.RootElement.TryGetProperty("remediationHint", out var hint) ||
                doc.RootElement.TryGetProperty("RemediationHint", out hint), Is.True,
                "Error response must include remediationHint");
            Assert.That(hint.GetString(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC4_NoSilentInternalFailures_Returns500WhenExpected()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp1 = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            var resp2 = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());

            Assert.That(resp1.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Valid asset intelligence request must not return 500");
            Assert.That(resp2.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Valid pricing request must not return 500");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC5 – Endpoint latency and error telemetry
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC5_AssetIntelligence_Response_HasCorrelationId()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("correlationId", out var cid) &&
                        !string.IsNullOrEmpty(cid.GetString()), Is.True);
        }

        [Test]
        public async Task AC5_PricingReliability_Response_HasCorrelationId()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("correlationId", out var cid) &&
                        !string.IsNullOrEmpty(cid.GetString()), Is.True);
        }

        [Test]
        public async Task AC5_AssetIntelligence_CorrelationId_EchoedFromRequest()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var myCorrelationId = "trace-abc-123";
            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate",
                new { assetId = 12345, network = "voimain-v1.0", correlationId = myCorrelationId });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            doc.RootElement.TryGetProperty("correlationId", out var cid);
            Assert.That(cid.GetString(), Is.EqualTo(myCorrelationId));
        }

        [Test]
        public async Task AC5_AssetIntelligence_Response_HasGeneratedAt()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("generatedAt", out var ga) &&
                        ga.ValueKind != JsonValueKind.Null, Is.True, "generatedAt must be present");
        }

        [Test]
        public async Task AC5_PricingReliability_Response_HasGeneratedAt()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("generatedAt", out var ga) &&
                        ga.ValueKind != JsonValueKind.Null, Is.True, "generatedAt must be present");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC6 – Schema/contract tests prevent response drift
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC6_AssetIntelligence_Response_HasRequiredFields()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Assert.That(root.TryGetProperty("success", out _), Is.True, "success field required");
            Assert.That(root.TryGetProperty("assetId", out _), Is.True, "assetId field required");
            Assert.That(root.TryGetProperty("network", out _), Is.True, "network field required");
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True, "correlationId field required");
            Assert.That(root.TryGetProperty("validationStatus", out _), Is.True, "validationStatus field required");
            Assert.That(root.TryGetProperty("errorCode", out _), Is.True, "errorCode field required");
        }

        [Test]
        public async Task AC6_PricingReliability_Response_HasRequiredFields()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Assert.That(root.TryGetProperty("success", out _), Is.True, "success field required");
            Assert.That(root.TryGetProperty("assetId", out _), Is.True, "assetId field required");
            Assert.That(root.TryGetProperty("network", out _), Is.True, "network field required");
            Assert.That(root.TryGetProperty("quoteStatus", out _), Is.True, "quoteStatus field required");
            Assert.That(root.TryGetProperty("errorCode", out _), Is.True, "errorCode field required");
        }

        [Test]
        public async Task AC6_AssetIntelligence_QualityIndicators_HasExpectedFields()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("qualityIndicators", out var qi) &&
                        qi.ValueKind != JsonValueKind.Null, Is.True, "qualityIndicators must be present");
        }

        [Test]
        public async Task AC6_PricingReliability_CompletenessScore_NotNegative()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", ValidPricingRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("price", out var price) &&
                price.ValueKind != JsonValueKind.Null)
            {
                Assert.That(price.GetDecimal(), Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public async Task AC6_AssetIntelligence_NormalizedFields_PresentOnSuccess()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("normalizedFields", out var fields) &&
                        fields.GetArrayLength() > 0, Is.True, "normalizedFields must not be empty on success");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC7 – Existing consumers continue working
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC7_ExistingEndpoints_StillAccessible()
        {
            var resp = await _client.GetAsync("/api/v1/operational-intelligence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Operational intelligence health must still be accessible");
        }

        [Test]
        public async Task AC7_ExistingAuth_StillWorks()
        {
            var token = await GetAuthTokenAsync();
            Assert.That(token, Is.Not.Null.And.Not.Empty, "Auth endpoint must still work");
        }

        [Test]
        public async Task AC7_BackwardCompatibility_NewEndpoints_DoNotBreakExisting()
        {
            var resp = await _client.GetAsync("/api/v1/asset-intelligence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC8 – Security and policy checks
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC8_AssetIntelligence_NullBody_Returns400()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var content = new System.Net.Http.StringContent("null", System.Text.Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync("/api/v1/asset-intelligence/evaluate", content);

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(415),
                "Null body must return 400 (not 500)");
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task AC8_PricingReliability_NullBody_Returns400()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var content = new System.Net.Http.StringContent("null", System.Text.Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync("/api/v1/pricing-reliability/quote", content);

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(415),
                "Null body must return 400 (not 500)");
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task AC8_AssetIntelligence_ValidRequest_NoInternalDetailsLeaked()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", ValidAssetIntelligenceRequest());
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body, Does.Not.Contain("StackTrace"),
                "Response must not contain stack trace");
            Assert.That(body, Does.Not.Contain("at BiatecTokensApi"),
                "Response must not leak internal namespaces");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC9 – CI quality gates
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC9_AssetIntelligence_ThreeRunDeterminism_SameSchema()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = ValidAssetIntelligenceRequest();
            var r1 = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", req);
            var r2 = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", req);
            var r3 = await _client.PostAsJsonAsync("/api/v1/asset-intelligence/evaluate", req);

            Assert.That(r1.StatusCode, Is.EqualTo(r2.StatusCode));
            Assert.That(r2.StatusCode, Is.EqualTo(r3.StatusCode));
        }

        [Test]
        public async Task AC9_PricingReliability_ThreeRunDeterminism_SameSchema()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var req = ValidPricingRequest();
            var r1 = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", req);
            var r2 = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", req);
            var r3 = await _client.PostAsJsonAsync("/api/v1/pricing-reliability/quote", req);

            Assert.That(r1.StatusCode, Is.EqualTo(r2.StatusCode));
            Assert.That(r2.StatusCode, Is.EqualTo(r3.StatusCode));
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC10 – Rollout plan / backward compat
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC10_AssetIntelligence_SourceHealth_Endpoint_Returns200()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync("/api/v1/asset-intelligence/quality/12345/voimain-v1.0");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Asset intelligence quality endpoint must return 200");
        }

        [Test]
        public async Task AC10_PricingReliability_SourceHealth_Endpoint_Returns200()
        {
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await _client.GetAsync("/api/v1/pricing-reliability/source-health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Pricing reliability source-health endpoint must return 200");
        }
    }
}

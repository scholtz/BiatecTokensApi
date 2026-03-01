using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Compliance;
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
    /// Integration and contract tests for the Enterprise Compliance Risk Scoring API.
    ///
    /// Validates all 10 acceptance criteria for the enterprise compliance foundation issue:
    ///
    /// AC1  – Compliance decision endpoints are secured behind existing auth/session requirements.
    /// AC2  – Response payload includes decision status (allow/review/deny), aggregate risk score,
    ///        reason codes, and structured evidence blocks.
    /// AC3  – Risk scoring thresholds are deterministic and verified across boundary conditions.
    /// AC4  – Request validation rejects malformed input with explicit machine-readable error codes.
    /// AC5  – Audit logging captures decision outcomes with correlation IDs and actor context.
    /// AC6  – API documentation is reflected in accurate endpoint behavior.
    /// AC7  – Unit tests cover scoring logic (see IssuanceRiskScoringServiceUnitTests.cs).
    /// AC8  – Integration tests validate endpoint behavior using authenticated request flows.
    /// AC9  – No broad test skip bypasses; CI for touched suites passes.
    /// AC10 – Implementation aligns with regulated enterprise-grade issuance workflows.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class EnterpriseComplianceRiskScoringContractTests
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
            ["JwtConfig:SecretKey"] = "enterprise-compliance-risk-scoring-contract-test-32chars",
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
            ["KeyManagementConfig:HardcodedKey"] = "EnterpriseComplianceRiskScoringTestKey32CharMin!",
            ["AllowedOrigins:0"] = "http://localhost:3000",
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

        // ══════════════════════════════════════════════════════════════════════════════
        // AC1 – Endpoint security: unauthenticated requests rejected
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC1_UnauthenticatedRequest_Returns401()
        {
            // No Authorization header
            var response = await _client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload("org-unauth", "issuer-unauth"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC1: Unauthenticated issuance compliance request must return 401");
        }

        [Test]
        public async Task AC1_InvalidBearerToken_Returns401()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "this-is-not-a-valid-jwt");

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload("org-bad-token", "issuer-bad-token"));

            // JWT middleware returns 401 for invalid token
            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "AC1: Invalid bearer token must return 401 or 403");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC2 – Response payload schema: decision, risk score, reason codes, evidence blocks
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC2_AuthenticatedRequest_LowRisk_Returns200WithAllowDecision()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload($"org-ac2-{Guid.NewGuid()}", "issuer-ac2"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC2: Low-risk authenticated request must return 200");

            var body = await ReadResponseAsync(response);

            Assert.Multiple(() =>
            {
                Assert.That(body.GetProperty("success").GetBoolean(), Is.True, "AC2: success must be true");
                Assert.That(body.GetProperty("decision").GetString(), Is.EqualTo("allow"), "AC2: Low-risk must produce 'allow'");
                Assert.That(body.GetProperty("aggregateRiskScore").GetInt32(), Is.LessThanOrEqualTo(39), "AC2: Low-risk score must be ≤39");
                Assert.That(body.GetProperty("riskBand").GetString(), Is.EqualTo("Low"), "AC2: Risk band must be 'Low'");
            });
        }

        [Test]
        public async Task AC2_ResponseContainsAllRequiredFields()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload($"org-fields-{Guid.NewGuid()}", "issuer-fields"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await ReadResponseAsync(response);

            // Verify all AC2 mandatory fields are present
            Assert.Multiple(() =>
            {
                Assert.That(body.TryGetProperty("success", out _), Is.True, "success field required");
                Assert.That(body.TryGetProperty("decision", out _), Is.True, "decision field required");
                Assert.That(body.TryGetProperty("aggregateRiskScore", out _), Is.True, "aggregateRiskScore field required");
                Assert.That(body.TryGetProperty("riskBand", out _), Is.True, "riskBand field required");
                Assert.That(body.TryGetProperty("reasonCodes", out _), Is.True, "reasonCodes field required");
                Assert.That(body.TryGetProperty("primaryReason", out _), Is.True, "primaryReason field required");
                Assert.That(body.TryGetProperty("correlationId", out _), Is.True, "correlationId field required");
                Assert.That(body.TryGetProperty("evaluatedAt", out _), Is.True, "evaluatedAt field required");
                Assert.That(body.TryGetProperty("policyVersion", out _), Is.True, "policyVersion field required");
                Assert.That(body.TryGetProperty("kycEvidence", out _), Is.True, "kycEvidence block required");
                Assert.That(body.TryGetProperty("sanctionsEvidence", out _), Is.True, "sanctionsEvidence block required");
                Assert.That(body.TryGetProperty("jurisdictionEvidence", out _), Is.True, "jurisdictionEvidence block required");
                Assert.That(body.TryGetProperty("componentScores", out _), Is.True, "componentScores block required");
            });
        }

        [Test]
        public async Task AC2_HighRiskRequest_ReturnsDenyDecision()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildHighRiskPayload($"org-deny-{Guid.NewGuid()}", "issuer-deny"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await ReadResponseAsync(response);

            Assert.Multiple(() =>
            {
                Assert.That(body.GetProperty("decision").GetString(), Is.EqualTo("deny"), "AC2: High-risk must produce 'deny'");
                Assert.That(body.GetProperty("aggregateRiskScore").GetInt32(), Is.GreaterThanOrEqualTo(70), "AC2: Deny score must be ≥70");
                Assert.That(body.GetProperty("riskBand").GetString(), Is.EqualTo("High"), "AC2: Risk band must be 'High'");
            });
        }

        [Test]
        public async Task AC2_MediumRiskRequest_ReturnsReviewDecision()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildMediumRiskPayload($"org-review-{Guid.NewGuid()}", "issuer-review"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await ReadResponseAsync(response);

            Assert.Multiple(() =>
            {
                Assert.That(body.GetProperty("decision").GetString(), Is.EqualTo("review"), "AC2: Medium-risk must produce 'review'");
                var score = body.GetProperty("aggregateRiskScore").GetInt32();
                Assert.That(score, Is.InRange(40, 69), "AC2: Review score must be 40–69");
                Assert.That(body.GetProperty("riskBand").GetString(), Is.EqualTo("Medium"), "AC2: Risk band must be 'Medium'");
            });
        }

        [Test]
        public async Task AC2_EvidenceBlocks_ContainStructuredFields()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload($"org-evidence-{Guid.NewGuid()}", "issuer-evidence"));

            var body = await ReadResponseAsync(response);

            var kyc = body.GetProperty("kycEvidence");
            var sanctions = body.GetProperty("sanctionsEvidence");
            var jurisdiction = body.GetProperty("jurisdictionEvidence");
            var scores = body.GetProperty("componentScores");

            Assert.Multiple(() =>
            {
                // KYC block fields
                Assert.That(kyc.TryGetProperty("status", out _), Is.True, "kycEvidence.status required");
                Assert.That(kyc.TryGetProperty("completenessPercent", out _), Is.True, "kycEvidence.completenessPercent required");
                Assert.That(kyc.TryGetProperty("riskPenalty", out _), Is.True, "kycEvidence.riskPenalty required");
                Assert.That(kyc.TryGetProperty("issueCodes", out _), Is.True, "kycEvidence.issueCodes required");

                // Sanctions block fields
                Assert.That(sanctions.TryGetProperty("screened", out _), Is.True, "sanctionsEvidence.screened required");
                Assert.That(sanctions.TryGetProperty("hitDetected", out _), Is.True, "sanctionsEvidence.hitDetected required");
                Assert.That(sanctions.TryGetProperty("hitConfidence", out _), Is.True, "sanctionsEvidence.hitConfidence required");
                Assert.That(sanctions.TryGetProperty("riskPenalty", out _), Is.True, "sanctionsEvidence.riskPenalty required");

                // Jurisdiction block fields
                Assert.That(jurisdiction.TryGetProperty("jurisdictionCode", out _), Is.True, "jurisdictionEvidence.jurisdictionCode required");
                Assert.That(jurisdiction.TryGetProperty("riskLevel", out _), Is.True, "jurisdictionEvidence.riskLevel required");
                Assert.That(jurisdiction.TryGetProperty("micaCompliant", out _), Is.True, "jurisdictionEvidence.micaCompliant required");
                Assert.That(jurisdiction.TryGetProperty("riskPenalty", out _), Is.True, "jurisdictionEvidence.riskPenalty required");

                // Component scores
                Assert.That(scores.TryGetProperty("kycScore", out _), Is.True, "componentScores.kycScore required");
                Assert.That(scores.TryGetProperty("sanctionsScore", out _), Is.True, "componentScores.sanctionsScore required");
                Assert.That(scores.TryGetProperty("jurisdictionScore", out _), Is.True, "componentScores.jurisdictionScore required");
                Assert.That(scores.TryGetProperty("total", out _), Is.True, "componentScores.total required");
            });
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC3 – Determinism: same inputs produce identical results on repeated calls
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC3_SameInputThreeRuns_IdenticalDecisionAndScore()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);
            var orgId = $"org-determinism-{Guid.NewGuid()}";
            var payload = BuildLowRiskPayload(orgId, "issuer-determinism");

            var body1 = await ReadResponseAsync(await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload));
            var body2 = await ReadResponseAsync(await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload));
            var body3 = await ReadResponseAsync(await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload));

            var decision1 = body1.GetProperty("decision").GetString();
            var score1 = body1.GetProperty("aggregateRiskScore").GetInt32();

            Assert.Multiple(() =>
            {
                Assert.That(body2.GetProperty("decision").GetString(), Is.EqualTo(decision1), "AC3: Decision must be deterministic");
                Assert.That(body3.GetProperty("decision").GetString(), Is.EqualTo(decision1), "AC3: Decision must be deterministic");
                Assert.That(body2.GetProperty("aggregateRiskScore").GetInt32(), Is.EqualTo(score1), "AC3: Score must be deterministic");
                Assert.That(body3.GetProperty("aggregateRiskScore").GetInt32(), Is.EqualTo(score1), "AC3: Score must be deterministic");
            });
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC4 – Validation: malformed inputs return machine-readable error codes
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC4_MissingOrganizationId_Returns400WithErrorCode()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var payload = BuildLowRiskPayload("", "issuer-test");

            var response = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "AC4: Missing organizationId must return 400");

            var body = await ReadResponseAsync(response);
            Assert.That(body.TryGetProperty("errorCode", out var errorCode), Is.True, "AC4: errorCode field required in error response");
            Assert.That(errorCode.GetString(), Is.Not.Null.And.Not.Empty, "AC4: errorCode must be non-empty");
        }

        [Test]
        public async Task AC4_InvalidKycCompleteness_Returns400()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var payload = new
            {
                organizationId = $"org-invalid-{Guid.NewGuid()}",
                issuerId = "issuer-test",
                kycEvidence = new { status = "Verified", completenessPercent = 150 }, // invalid: > 100
                sanctionsEvidence = new { screened = true, hitDetected = false, hitConfidence = 0.0 },
                jurisdictionEvidence = new { jurisdictionCode = "DE", riskLevel = "Low", micaCompliant = true }
            };

            var response = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "AC4: Invalid KYC completeness must return 400");
        }

        [Test]
        public async Task AC4_InvalidSanctionsConfidence_Returns400()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var payload = new
            {
                organizationId = $"org-sanctions-{Guid.NewGuid()}",
                issuerId = "issuer-test",
                kycEvidence = new { status = "Verified", completenessPercent = 95 },
                sanctionsEvidence = new { screened = true, hitDetected = true, hitConfidence = 2.5 }, // invalid: > 1.0
                jurisdictionEvidence = new { jurisdictionCode = "DE", riskLevel = "Low", micaCompliant = true }
            };

            var response = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "AC4: Invalid sanctions confidence must return 400");
        }

        [Test]
        public async Task AC4_MissingJurisdictionCode_Returns400()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var payload = new
            {
                organizationId = $"org-nojuris-{Guid.NewGuid()}",
                issuerId = "issuer-test",
                kycEvidence = new { status = "Verified", completenessPercent = 95 },
                sanctionsEvidence = new { screened = true, hitDetected = false, hitConfidence = 0.0 },
                jurisdictionEvidence = new { jurisdictionCode = "", riskLevel = "Low", micaCompliant = true } // empty code
            };

            var response = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "AC4: Missing jurisdictionCode must return 400");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC5 – Correlation IDs and audit tracing
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC5_CorrelationIdPresentInEveryResponse()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload($"org-corr-{Guid.NewGuid()}", "issuer-corr"));

            var body = await ReadResponseAsync(response);
            Assert.That(body.TryGetProperty("correlationId", out var corrId), Is.True);
            Assert.That(corrId.GetString(), Is.Not.Null.And.Not.Empty,
                "AC5: correlationId must be present in every successful evaluation response");
        }

        [Test]
        public async Task AC5_CorrelationIdPreserved_WhenProvidedInRequest()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);
            var customCorrelationId = $"custom-corr-{Guid.NewGuid()}";

            var payload = new
            {
                organizationId = $"org-corr-preserve-{Guid.NewGuid()}",
                issuerId = "issuer-corr",
                correlationId = customCorrelationId,
                kycEvidence = new { status = "Verified", completenessPercent = 95 },
                sanctionsEvidence = new { screened = true, hitDetected = false, hitConfidence = 0.0 },
                jurisdictionEvidence = new { jurisdictionCode = "DE", riskLevel = "Low", micaCompliant = true }
            };

            var response = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate", payload);
            var body = await ReadResponseAsync(response);

            Assert.That(body.GetProperty("correlationId").GetString(), Is.EqualTo(customCorrelationId),
                "AC5: Provided correlationId must be preserved in response");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC6 – Policy version present in every response
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC6_PolicyVersionPresentInResponse()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload($"org-pv-{Guid.NewGuid()}", "issuer-pv"));

            var body = await ReadResponseAsync(response);

            Assert.That(body.TryGetProperty("policyVersion", out var pv), Is.True);
            Assert.That(pv.GetString(), Is.EqualTo("1.0.0"), "AC6: policyVersion must be '1.0.0'");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC8 – Contract-level: all three decision outcomes are reachable via endpoint
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC8_AllThreeDecisionOutcomesReachable()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var lowResponse = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate",
                BuildLowRiskPayload($"org-low-{Guid.NewGuid()}", "issuer-low"));
            var mediumResponse = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate",
                BuildMediumRiskPayload($"org-medium-{Guid.NewGuid()}", "issuer-medium"));
            var highResponse = await client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate",
                BuildHighRiskPayload($"org-high-{Guid.NewGuid()}", "issuer-high"));

            var lowBody = await ReadResponseAsync(lowResponse);
            var mediumBody = await ReadResponseAsync(mediumResponse);
            var highBody = await ReadResponseAsync(highResponse);

            Assert.Multiple(() =>
            {
                Assert.That(lowBody.GetProperty("decision").GetString(), Is.EqualTo("allow"),
                    "AC8: Low-risk path must produce 'allow'");
                Assert.That(mediumBody.GetProperty("decision").GetString(), Is.EqualTo("review"),
                    "AC8: Medium-risk path must produce 'review'");
                Assert.That(highBody.GetProperty("decision").GetString(), Is.EqualTo("deny"),
                    "AC8: High-risk path must produce 'deny'");
            });
        }

        [Test]
        public async Task AC8_ComponentScores_SumToAggregateScore()
        {
            var token = await RegisterAndGetTokenAsync();
            var client = CreateAuthClient(token);

            var response = await client.PostAsJsonAsync(
                "/api/v1/compliance/issuance/evaluate",
                BuildMediumRiskPayload($"org-sum-{Guid.NewGuid()}", "issuer-sum"));

            var body = await ReadResponseAsync(response);
            var scores = body.GetProperty("componentScores");

            var kycScore = scores.GetProperty("kycScore").GetInt32();
            var sanctionsScore = scores.GetProperty("sanctionsScore").GetInt32();
            var jurisdictionScore = scores.GetProperty("jurisdictionScore").GetInt32();
            var total = scores.GetProperty("total").GetInt32();
            var aggregateScore = body.GetProperty("aggregateRiskScore").GetInt32();

            Assert.That(kycScore + sanctionsScore + jurisdictionScore, Is.EqualTo(total),
                "AC8: Component scores must sum to total");
            Assert.That(total, Is.EqualTo(aggregateScore),
                "AC8: Component total must equal aggregateRiskScore");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC10 – Regression: existing auth endpoints still function correctly
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC10_Regression_HealthEndpointStillWorks()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "AC10: Health endpoint must not be broken by compliance API changes");
        }

        [Test]
        public async Task AC10_Regression_AuthRegisterStillWorks()
        {
            var token = await RegisterAndGetTokenAsync();
            Assert.That(token, Is.Not.Null.And.Not.Empty,
                "AC10: Auth register must still return a valid access token");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════════════

        private async Task<string> RegisterAndGetTokenAsync()
        {
            var req = new RegisterRequest
            {
                Email = $"risk-scoring-{Guid.NewGuid()}@enterprise-test.com",
                Password = "EnterprisePass1!A",
                ConfirmPassword = "EnterprisePass1!A",
                FullName = "Compliance Risk Test"
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);
            resp.EnsureSuccessStatusCode();
            var reg = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return reg!.AccessToken ?? string.Empty;
        }

        private HttpClient CreateAuthClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        private static async Task<JsonElement> ReadResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static object BuildLowRiskPayload(string orgId, string issuerId) => new
        {
            organizationId = orgId,
            issuerId = issuerId,
            kycEvidence = new
            {
                status = "Verified",
                completenessPercent = 95,
                provider = "Sumsub",
                verificationDate = DateTime.UtcNow.AddDays(-10).ToString("o")
            },
            sanctionsEvidence = new
            {
                screened = true,
                hitDetected = false,
                hitConfidence = 0.0,
                screeningProvider = "Chainalysis"
            },
            jurisdictionEvidence = new
            {
                jurisdictionCode = "DE",
                riskLevel = "Low",
                micaCompliant = true,
                regulatoryFrameworks = new[] { "MICA", "FATF" }
            },
            issuanceContext = new
            {
                tokenType = "ARC3",
                tokenName = "TestToken",
                network = "voimain-v1.0"
            }
        };

        private static object BuildMediumRiskPayload(string orgId, string issuerId) => new
        {
            organizationId = orgId,
            issuerId = issuerId,
            kycEvidence = new
            {
                // Failed KYC = 40 pts penalty
                status = "Failed",
                completenessPercent = 0
            },
            sanctionsEvidence = new
            {
                // Screened, no hit = 0 pts
                screened = true,
                hitDetected = false,
                hitConfidence = 0.0
            },
            jurisdictionEvidence = new
            {
                // Medium jurisdiction = 10 pts
                jurisdictionCode = "US",
                riskLevel = "Medium",
                micaCompliant = false
            }
            // Total = 40 + 0 + 10 = 50 → "review"
        };

        private static object BuildHighRiskPayload(string orgId, string issuerId) => new
        {
            organizationId = orgId,
            issuerId = issuerId,
            kycEvidence = new
            {
                status = "Failed",       // 40 pts
                completenessPercent = 0
            },
            sanctionsEvidence = new
            {
                screened = true,
                hitDetected = true,
                hitConfidence = 0.95     // 30 pts (high confidence)
            },
            jurisdictionEvidence = new
            {
                jurisdictionCode = "KP",
                riskLevel = "Prohibited", // 30 pts
                micaCompliant = false
            }
            // Total = 40 + 30 + 30 = 100 → "deny"
        };
    }
}

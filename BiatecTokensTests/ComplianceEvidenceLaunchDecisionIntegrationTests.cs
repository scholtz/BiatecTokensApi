using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// HTTP integration tests for the Compliance Evidence and Launch Decision API.
    ///
    /// Tests the full HTTP pipeline:
    /// - Authentication and authorization (AC1, AC6)
    /// - POST /api/v1/compliance-evidence/decision – launch decision evaluation (AC1, AC2)
    /// - GET  /api/v1/compliance-evidence/decision/{id} – decision retrieval
    /// - GET  /api/v1/compliance-evidence/decision/{id}/trace – decision trace
    /// - GET  /api/v1/compliance-evidence/decisions/{ownerId} – decision listing
    /// - POST /api/v1/compliance-evidence/evidence – evidence bundle (AC2, AC4, AC5)
    /// - POST /api/v1/compliance-evidence/evidence/export/json – JSON download (AC3)
    /// - POST /api/v1/compliance-evidence/evidence/export/csv – CSV download (AC3)
    /// - GET  /api/v1/compliance-evidence/health – health endpoint
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEvidenceLaunchDecisionIntegrationTests
    {
        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;       // authenticated
        private HttpClient _unauthClient = null!; // unauthenticated

        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        // ── Setup ─────────────────────────────────────────────────────────────

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            // Register and login to get JWT for authenticated tests
            var email = $"evidence-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Evidence Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var token = regBody?.AccessToken ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForEvidenceApiIntegrationTests32C",
                        ["JwtConfig:SecretKey"] = "EvidenceApiIntegrationTestsSecretKey32C!",
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
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["KycConfig:MockAutoApprove"] = "true",
                        ["StripeConfig:SecretKey"] = "sk_test_placeholder",
                        ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
                        ["StripeConfig:WebhookSecret"] = "whsec_placeholder",
                    });
                });
            }
        }

        // ── Health ────────────────────────────────────────────────────────────

        [Test]
        public async Task Health_ReturnsOk()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/compliance-evidence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Authentication (AC6) ──────────────────────────────────────────────

        [Test]
        public async Task Decision_Unauthenticated_Returns401()
        {
            var req = new LaunchDecisionRequest { OwnerId = "o1", TokenStandard = "ASA", Network = "testnet" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance-evidence/decision", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Evidence_Unauthenticated_Returns401()
        {
            var req = new EvidenceBundleRequest { OwnerId = "o1" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance-evidence/evidence", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ExportJson_Unauthenticated_Returns401()
        {
            var req = new EvidenceExportRequest { OwnerId = "o1" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/json", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ExportCsv_Unauthenticated_Returns401()
        {
            var req = new EvidenceExportRequest { OwnerId = "o1" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/csv", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Launch decision (AC1, AC2) ────────────────────────────────────────

        [Test]
        public async Task PostDecision_ValidRequest_Returns200WithDecision()
        {
            var req = new LaunchDecisionRequest
            {
                OwnerId = $"api-owner-{Guid.NewGuid():N}",
                TokenStandard = "ASA",
                Network = "testnet"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<LaunchDecisionResponse>(_json);
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.DecisionId, Is.Not.Null.And.Not.Empty);
            Assert.That(body.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task PostDecision_Response_IncludesReleaseGradeFields()
        {
            var req = new LaunchDecisionRequest
            {
                OwnerId = $"grade-owner-{Guid.NewGuid():N}",
                TokenStandard = "ASA",
                Network = "testnet"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await resp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("isReleaseGradeEvidence"),
                "POST /decision response must include isReleaseGradeEvidence field (AC4).");
            Assert.That(json, Does.Contain("releaseGradeNote"),
                "POST /decision response must include releaseGradeNote field.");
        }

        [Test]
        public async Task PostDecision_MissingOwnerId_Returns400()
        {
            var req = new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PostDecision_InvalidTokenStandard_Returns400()
        {
            var req = new LaunchDecisionRequest { OwnerId = "owner-1", TokenStandard = "UNKNOWN", Network = "testnet" };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // ── Decision retrieval ────────────────────────────────────────────────

        [Test]
        public async Task GetDecision_AfterPost_Returns200()
        {
            var ownerId = $"get-owner-{Guid.NewGuid():N}";
            var postReq = new LaunchDecisionRequest
            {
                OwnerId = ownerId,
                TokenStandard = "ARC3",
                Network = "testnet"
            };
            var postResp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision", postReq);
            var postBody = await postResp.Content.ReadFromJsonAsync<LaunchDecisionResponse>(_json);

            var getResp = await _client.GetAsync($"/api/v1/compliance-evidence/decision/{postBody!.DecisionId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var getBody = await getResp.Content.ReadFromJsonAsync<LaunchDecisionResponse>(_json);
            Assert.That(getBody!.DecisionId, Is.EqualTo(postBody.DecisionId));
        }

        [Test]
        public async Task GetDecision_UnknownId_Returns404()
        {
            var resp = await _client.GetAsync("/api/v1/compliance-evidence/decision/nonexistent-id-xyz");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── Decision trace ────────────────────────────────────────────────────

        [Test]
        public async Task GetTrace_AfterPost_Returns200WithRules()
        {
            var ownerId = $"trace-owner-{Guid.NewGuid():N}";
            var postReq = new LaunchDecisionRequest
            {
                OwnerId = ownerId,
                TokenStandard = "ARC200",
                Network = "testnet"
            };
            var postResp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision", postReq);
            var postBody = await postResp.Content.ReadFromJsonAsync<LaunchDecisionResponse>(_json);

            var traceResp = await _client.GetAsync($"/api/v1/compliance-evidence/decision/{postBody!.DecisionId}/trace");
            Assert.That(traceResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var traceBody = await traceResp.Content.ReadFromJsonAsync<DecisionTraceResponse>(_json);
            Assert.That(traceBody!.Success, Is.True);
            Assert.That(traceBody.Rules, Has.Count.GreaterThan(0));
        }

        // ── Evidence bundle (AC2, AC4, AC5) ──────────────────────────────────

        [Test]
        public async Task PostEvidence_ValidRequest_Returns200WithBundle()
        {
            var ownerId = $"bundle-owner-{Guid.NewGuid():N}";
            // First evaluate a decision to generate evidence
            await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision",
                new LaunchDecisionRequest { OwnerId = ownerId, TokenStandard = "ASA", Network = "testnet" });

            var req = new EvidenceBundleRequest { OwnerId = ownerId };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<EvidenceBundleResponse>(_json);
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.BundleId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task PostEvidence_Response_IncludesReleaseGradeFields()
        {
            var ownerId = $"bundle-grade-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision",
                new LaunchDecisionRequest { OwnerId = ownerId, TokenStandard = "ASA", Network = "testnet" });

            var req = new EvidenceBundleRequest { OwnerId = ownerId };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await resp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("isReleaseGradeEvidence"), "Bundle must include isReleaseGradeEvidence (AC4).");
            Assert.That(json, Does.Contain("releaseGradeNote"), "Bundle must include releaseGradeNote.");
            Assert.That(json, Does.Contain("freshnessStatus"), "Bundle must include freshnessStatus (AC2).");
            Assert.That(json, Does.Contain("policySnapshot"), "Bundle must include policySnapshot (AC2).");
            Assert.That(json, Does.Contain("exportManifest"), "Bundle must include exportManifest (AC2).");
        }

        [Test]
        public async Task PostEvidence_EmptyOwner_Returns400()
        {
            var req = new EvidenceBundleRequest { OwnerId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PostEvidence_NoEvidenceOwner_IsNotReleaseGrade()
        {
            var req = new EvidenceBundleRequest { OwnerId = $"no-evidence-{Guid.NewGuid():N}" };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<EvidenceBundleResponse>(_json);
            Assert.That(body!.IsReleaseGradeEvidence, Is.False,
                "Bundle with no evidence must never be labeled release-grade (AC4).");
            Assert.That(body.RemediationGuidance, Is.Not.Empty,
                "Bundle with no evidence must include remediation guidance (AC5).");
        }

        // ── JSON export (AC3) ─────────────────────────────────────────────────

        [Test]
        public async Task PostExportJson_ValidRequest_ReturnsJsonFile()
        {
            var ownerId = $"json-export-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision",
                new LaunchDecisionRequest { OwnerId = ownerId, TokenStandard = "ASA", Network = "testnet" });

            var req = new EvidenceExportRequest { OwnerId = ownerId };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/json", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(resp.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

            var json = await resp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("schemaVersion"));
            Assert.That(json, Does.Contain("isReleaseGradeEvidence"));
            Assert.That(json, Does.Contain("evidenceItems"));
        }

        [Test]
        public async Task PostExportJson_EmptyOwnerId_Returns400()
        {
            var req = new EvidenceExportRequest { OwnerId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/json", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PostExportJson_Response_HasContentDispositionFilename()
        {
            var ownerId = $"json-cd-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision",
                new LaunchDecisionRequest { OwnerId = ownerId, TokenStandard = "ARC3", Network = "testnet" });

            var req = new EvidenceExportRequest { OwnerId = ownerId };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/json", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            // Content-Disposition header should contain filename
            var cd = resp.Content.Headers.ContentDisposition;
            Assert.That(cd?.FileName ?? cd?.FileNameStar, Is.Not.Null.And.Not.Empty.Or.EqualTo(null),
                "Response should include a filename for the downloadable artifact.");
        }

        // ── CSV export (AC3) ──────────────────────────────────────────────────

        [Test]
        public async Task PostExportCsv_ValidRequest_ReturnsCsvFile()
        {
            var ownerId = $"csv-export-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision",
                new LaunchDecisionRequest { OwnerId = ownerId, TokenStandard = "ERC20", Network = "base-testnet" });

            var req = new EvidenceExportRequest { OwnerId = ownerId };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/csv", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(resp.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/csv"));

            var csv = await resp.Content.ReadAsStringAsync();
            Assert.That(csv, Does.Contain("EvidenceId"),
                "CSV must include EvidenceId column header.");
            Assert.That(csv, Does.Contain("ValidationStatus"),
                "CSV must include ValidationStatus column header.");
        }

        [Test]
        public async Task PostExportCsv_EmptyOwnerId_Returns400()
        {
            var req = new EvidenceExportRequest { OwnerId = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/csv", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PostExportCsv_ContainsPolicyVersionInHeader()
        {
            var ownerId = $"csv-policy-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/api/v1/compliance-evidence/decision",
                new LaunchDecisionRequest { OwnerId = ownerId, TokenStandard = "ARC200", Network = "testnet" });

            var req = new EvidenceExportRequest { OwnerId = ownerId };
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-evidence/evidence/export/csv", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var csv = await resp.Content.ReadAsStringAsync();
            Assert.That(csv, Does.Contain("Policy Version"),
                "CSV must include Policy Version in header section for auditor traceability.");
        }
    }
}

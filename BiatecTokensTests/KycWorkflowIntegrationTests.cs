using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.KycWorkflow;
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
    /// Integration tests for the KYC Workflow HTTP API endpoints.
    /// Uses WebApplicationFactory with JWT authentication (register → login → Bearer token).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycWorkflowIntegrationTests
    {
        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            // Register and login to get a JWT for authenticated endpoint tests
            var email = $"kyc-workflow-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "KYC Workflow Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var jwtToken = regBody?.AccessToken ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
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
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForKycWorkflowIntegrationTests32Chars",
                        ["JwtConfig:SecretKey"] = "KycWorkflowTestSecretKey32CharsRequired!!",
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

        // ── Helper methods ─────────────────────────────────────────────────────

        private async Task<(HttpResponseMessage Response, JsonDocument? Body)> PostJsonAsync(string url, object body)
        {
            var response = await _client.PostAsJsonAsync(url, body);
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); } catch { }
            return (response, doc);
        }

        private async Task<string> CreateParticipantAndGetKycId(string participantId)
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId });
            resp.EnsureSuccessStatusCode();
            return doc!.RootElement.GetProperty("record").GetProperty("kycId").GetString()!;
        }

        // ── POST / (create) ────────────────────────────────────────────────────

        [Test]
        public async Task Create_ValidRequest_Returns200WithRecord()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = "integration-p1" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("record").GetProperty("participantId").GetString(), Is.EqualTo("integration-p1"));
        }

        [Test]
        public async Task Create_RecordHasPendingState()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = "state-check-p1" });

            resp.EnsureSuccessStatusCode();
            var state = doc!.RootElement.GetProperty("record").GetProperty("state").GetInt32();
            Assert.That(state, Is.EqualTo((int)KycVerificationState.Pending));
        }

        [Test]
        public async Task Create_EmptyParticipantId_Returns400()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = "" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Create_NullParticipantId_Returns400()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = (string?)null });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Create_ResponseContract_HasRequiredFields()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = "contract-check" });

            resp.EnsureSuccessStatusCode();
            var record = doc!.RootElement.GetProperty("record");
            Assert.That(record.TryGetProperty("kycId", out _), Is.True);
            Assert.That(record.TryGetProperty("participantId", out _), Is.True);
            Assert.That(record.TryGetProperty("state", out _), Is.True);
            Assert.That(record.TryGetProperty("createdAt", out _), Is.True);
        }

        // ── GET /{kycId} ───────────────────────────────────────────────────────

        [Test]
        public async Task GetById_ExistingRecord_Returns200()
        {
            var kycId = await CreateParticipantAndGetKycId("get-by-id-1");

            var response = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetById_NonExistentId_Returns404()
        {
            var response = await _client.GetAsync("/api/v1/kyc-workflow/nonexistent-kyc-id-123");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetById_ReturnsCorrectParticipantId()
        {
            var kycId = await CreateParticipantAndGetKycId("get-by-id-2");

            var response = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}");
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.GetProperty("record").GetProperty("participantId").GetString(), Is.EqualTo("get-by-id-2"));
        }

        // ── PUT /{kycId}/status ────────────────────────────────────────────────

        [Test]
        public async Task UpdateStatus_ValidTransition_Returns200()
        {
            var kycId = await CreateParticipantAndGetKycId("update-p1");

            var (resp, doc) = await PostJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Approved });

            // Note: PUT uses PostAsJsonAsync won't work; use PutAsJsonAsync
            var putResp = await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.ManualReviewRequired });

            Assert.That(putResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task UpdateStatus_ValidApprovalTransition_Returns200AndApprovedState()
        {
            var kycId = await CreateParticipantAndGetKycId("update-approval-1");

            var putResp = await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Approved, expirationDays = 365 });

            Assert.That(putResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync());
            var state = doc.RootElement.GetProperty("record").GetProperty("state").GetInt32();
            Assert.That(state, Is.EqualTo((int)KycVerificationState.Approved));
        }

        [Test]
        public async Task UpdateStatus_InvalidTransition_Returns400()
        {
            var kycId = await CreateParticipantAndGetKycId("invalid-transition-1");

            // Pending → Pending is invalid
            var putResp = await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Pending });

            Assert.That(putResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task UpdateStatus_NonExistentId_Returns404()
        {
            var putResp = await _client.PutAsJsonAsync("/api/v1/kyc-workflow/nonexistent/status",
                new { newState = (int)KycVerificationState.Approved });

            Assert.That(putResp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task UpdateStatus_InvalidTransition_ReturnsErrorCode()
        {
            var kycId = await CreateParticipantAndGetKycId("err-code-p1");

            var putResp = await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Pending });

            var doc = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ── GET /{kycId}/history ───────────────────────────────────────────────

        [Test]
        public async Task GetHistory_Returns200WithHistory()
        {
            var kycId = await CreateParticipantAndGetKycId("history-p1");

            var response = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/history");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("history").GetArrayLength(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task GetHistory_NotFound_Returns404()
        {
            var response = await _client.GetAsync("/api/v1/kyc-workflow/nosuchid/history");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetHistory_AfterMultipleTransitions_ShowsAllEntries()
        {
            var kycId = await CreateParticipantAndGetKycId("history-multi-1");
            await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status", new { newState = (int)KycVerificationState.ManualReviewRequired });
            await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status", new { newState = (int)KycVerificationState.Approved });

            var response = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/history");
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.GetProperty("history").GetArrayLength(), Is.EqualTo(3));
        }

        // ── POST /{kycId}/evidence ─────────────────────────────────────────────

        [Test]
        public async Task AddEvidence_Returns200()
        {
            var kycId = await CreateParticipantAndGetKycId("evidence-p1");

            var (resp, doc) = await PostJsonAsync($"/api/v1/kyc-workflow/{kycId}/evidence",
                new { evidenceType = (int)KycEvidenceType.Passport, issuingCountry = "US" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task AddEvidence_NotFound_Returns404()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/kyc-workflow/noexist/evidence",
                new { evidenceType = (int)KycEvidenceType.Passport });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── GET /{kycId}/evidence ──────────────────────────────────────────────

        [Test]
        public async Task GetEvidence_Returns200WithEvidenceList()
        {
            var kycId = await CreateParticipantAndGetKycId("get-evidence-1");
            await PostJsonAsync($"/api/v1/kyc-workflow/{kycId}/evidence",
                new { evidenceType = (int)KycEvidenceType.DriversLicense });

            var response = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/evidence");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("evidence").GetArrayLength(), Is.EqualTo(1));
        }

        [Test]
        public async Task GetEvidence_EmptyRecord_ReturnsEmptyList()
        {
            var kycId = await CreateParticipantAndGetKycId("empty-evidence-1");

            var response = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/evidence");
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.GetProperty("evidence").GetArrayLength(), Is.EqualTo(0));
        }

        // ── GET /participant/{participantId} ───────────────────────────────────

        [Test]
        public async Task GetActiveByParticipant_ExistingParticipant_Returns200()
        {
            await CreateParticipantAndGetKycId("active-participant-1");

            var response = await _client.GetAsync("/api/v1/kyc-workflow/participant/active-participant-1");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetActiveByParticipant_NonExistent_Returns404()
        {
            var response = await _client.GetAsync("/api/v1/kyc-workflow/participant/unknown-xyz-999");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── POST /eligibility ──────────────────────────────────────────────────

        [Test]
        public async Task Eligibility_ApprovedParticipant_IsEligible()
        {
            var kycId = await CreateParticipantAndGetKycId("eligible-integration-1");
            await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Approved, expirationDays = 365 });

            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow/eligibility",
                new { participantId = "eligible-integration-1" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("isEligible").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Eligibility_PendingParticipant_IsNotEligible()
        {
            await CreateParticipantAndGetKycId("pending-elig-1");

            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow/eligibility",
                new { participantId = "pending-elig-1" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("isEligible").GetBoolean(), Is.False);
        }

        [Test]
        public async Task Eligibility_NoRecord_IsNotEligible()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/kyc-workflow/eligibility",
                new { participantId = "no-kyc-record-xyz" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("isEligible").GetBoolean(), Is.False);
        }

        [Test]
        public async Task Eligibility_EmptyParticipantId_Returns400()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/kyc-workflow/eligibility",
                new { participantId = "" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // ── Swagger spec ───────────────────────────────────────────────────────

        [Test]
        public async Task Swagger_IsAccessible_Returns200()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Full workflow: Create → ManualReview → Approved ───────────────────

        [Test]
        public async Task FullWorkflow_CreateToManualReviewToApproved_Succeeds()
        {
            var kycId = await CreateParticipantAndGetKycId("full-workflow-1");

            // Pending → ManualReview
            var r1 = await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.ManualReviewRequired, reviewNote = "Check docs" });
            Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // ManualReview → Approved
            var r2 = await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Approved, expirationDays = 365 });
            Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await r2.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("record").GetProperty("state").GetInt32(), Is.EqualTo((int)KycVerificationState.Approved));
        }

        [Test]
        public async Task FullWorkflow_CreateToRejectedToPendingToApproved_AuditLengthCorrect()
        {
            var kycId = await CreateParticipantAndGetKycId("audit-length-1");

            await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Rejected });
            await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Pending });
            await _client.PutAsJsonAsync($"/api/v1/kyc-workflow/{kycId}/status",
                new { newState = (int)KycVerificationState.Approved });

            var histResp = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/history");
            var doc = JsonDocument.Parse(await histResp.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.GetProperty("history").GetArrayLength(), Is.EqualTo(4));
        }

        // ── Process expired ────────────────────────────────────────────────────

        [Test]
        public async Task ProcessExpired_Returns200()
        {
            var response = await _client.PostAsync("/api/v1/kyc-workflow/process-expired", null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ProcessExpired_ResponseContract_HasExpiredCount()
        {
            var response = await _client.PostAsync("/api/v1/kyc-workflow/process-expired", null);
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("expiredCount", out _), Is.True);
        }

        // ── Schema contract assertions ─────────────────────────────────────────

        [Test]
        public async Task CreateResponse_SuccessField_IsNotNull()
        {
            var (_, doc) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = "schema-1" });
            Assert.That(doc!.RootElement.TryGetProperty("success", out _), Is.True);
        }

        [Test]
        public async Task CreateResponse_Record_KycIdIsNonEmpty()
        {
            var (_, doc) = await PostJsonAsync("/api/v1/kyc-workflow", new { participantId = "schema-2" });
            var kycId = doc!.RootElement.GetProperty("record").GetProperty("kycId").GetString();
            Assert.That(kycId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HistoryResponse_KycIdAndParticipantId_Present()
        {
            var kycId = await CreateParticipantAndGetKycId("schema-hist-1");
            var resp = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/history");
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.TryGetProperty("kycId", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("participantId", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("currentState", out _), Is.True);
        }

        [Test]
        public async Task EvidenceResponse_KycIdAndEvidenceList_Present()
        {
            var kycId = await CreateParticipantAndGetKycId("schema-ev-1");
            var resp = await _client.GetAsync($"/api/v1/kyc-workflow/{kycId}/evidence");
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            Assert.That(doc.RootElement.TryGetProperty("kycId", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("evidence", out _), Is.True);
        }

        [Test]
        public async Task EligibilityResponse_AllRequiredFields_Present()
        {
            var (_, doc) = await PostJsonAsync("/api/v1/kyc-workflow/eligibility",
                new { participantId = "elig-schema-1" });

            Assert.That(doc!.RootElement.TryGetProperty("isEligible", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("participantId", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("currentState", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("kycRequired", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("evaluatedAt", out _), Is.True);
        }
    }
}

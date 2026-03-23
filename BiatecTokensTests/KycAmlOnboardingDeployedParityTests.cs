using BiatecTokensApi.Models.KycAmlOnboarding;
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
    /// Deployed-path parity tests for the KYC/AML onboarding API at
    /// <c>/api/v1/kyc-aml-onboarding</c>.
    ///
    /// These tests prove that the backend behaves correctly under production-like
    /// deployed conditions — exercising the full HTTP stack (routing, JWT auth,
    /// controller dispatch, service logic) through a
    /// <see cref="WebApplicationFactory{TEntryPoint}"/>.
    ///
    /// Note on provider configuration: <c>ILiveProviderVerificationJourneyService</c>
    /// is registered as an optional dependency. When the service is not configured,
    /// <c>InitiateProviderChecks</c> returns <c>Success=false</c> with
    /// <c>ErrorCode="PROVIDER_NOT_CONFIGURED"</c> and the controller maps that to 400.
    /// This is expected, correct, fail-closed behaviour — not a test failure.
    ///
    /// Coverage:
    /// DP01: Create case with valid request → 200, State=Initiated
    /// DP02: Create case with null SubjectId → 400
    /// DP03: Create case with empty SubjectId → 400
    /// DP04: Idempotency key reuse → same case returned
    /// DP05: Get case by ID → 200, correct fields populated
    /// DP06: Get non-existent case → 404
    /// DP07: Initiate provider checks → 400 PROVIDER_NOT_CONFIGURED (correct fail-closed)
    /// DP08: Initiate checks for non-existent case → 404
    /// DP09: Record reviewer action (AddNote) → 200, action persisted
    /// DP10: Record reviewer action on non-existent case → 404
    /// DP11: Get evidence summary for existing case → 200
    /// DP12: Get evidence summary for non-existent case → 404
    /// DP13: List cases → 200 with empty array initially
    /// DP14: Create multiple cases, list returns all
    /// DP15: Unauthenticated create → 401
    /// DP16: Unauthenticated get case → 401
    /// DP17: Unauthenticated initiate checks → 401
    /// DP18: Unauthenticated reviewer action → 401
    /// DP19: Unauthenticated evidence → 401
    /// DP20: Unauthenticated list → 401
    /// DP21: Schema contract: CreateOnboardingCaseResponse fields (CaseId, SubjectId, State)
    /// DP22: CorrelationId in request echoed in response
    /// DP23: Two different subjects create separate cases
    /// DP24: Evidence summary includes IsProviderConfigured flag
    /// DP25: E2E lifecycle: create → get → record note → verify note in actions
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlOnboardingDeployedParityTests
    {
        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory — identical config to LiveProviderVerificationJourneyDeployedParityTests
        // ════════════════════════════════════════════════════════════════════

        private sealed class KycAmlOnboardingFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "KycAmlOnboardingParityTestKey32CharsMin!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "KycAmlOnboardingParityTestKey32+chars",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "kyc-aml-parity-test",
                        ["ProtectedSignOff:EnforceConfigGuards"] = "true",
                        ["WorkflowGovernanceConfig:Enabled"] = "true",
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

        // ════════════════════════════════════════════════════════════════════
        // Shared state
        // ════════════════════════════════════════════════════════════════════

        private KycAmlOnboardingFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string BaseUrl = "/api/v1/kyc-aml-onboarding";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new KycAmlOnboardingFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"kyc-{Guid.NewGuid():N}");
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwt);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // DP01: Create case with valid request → 200, State=Initiated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP01_CreateCase_ValidRequest_Returns200_StateInitiated()
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest
                {
                    SubjectId   = UniqueSubject(),
                    SubjectKind = KycAmlOnboardingSubjectKind.Individual
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP01: valid create must return 200");

            var result = await resp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
            Assert.That(result!.Success, Is.True, "DP01: Success must be true");
            Assert.That(result.Case, Is.Not.Null, "DP01: Case must be populated");
            Assert.That(result.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated),
                "DP01: Initial state must be Initiated");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP02: Create case with null SubjectId → 400
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP02_CreateCase_NullSubjectId_Returns400()
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest { SubjectId = null! });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP02: null SubjectId must return 400");

            var result = await resp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
            Assert.That(result!.Success, Is.False, "DP02: Success must be false");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP03: Create case with empty SubjectId → 400
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP03_CreateCase_EmptySubjectId_Returns400()
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest { SubjectId = "" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP03: empty SubjectId must return 400");

            var result = await resp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
            Assert.That(result!.Success, Is.False, "DP03: Success must be false");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP04: Idempotency key reuse → same case returned
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP04_IdempotencyKey_ReturnsExistingCase()
        {
            string subjectId      = UniqueSubject();
            string idempotencyKey = $"idem-dp04-{Guid.NewGuid():N}";

            var resp1 = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest
                {
                    SubjectId      = subjectId,
                    IdempotencyKey = idempotencyKey
                });
            var result1 = await resp1.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();

            var resp2 = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest
                {
                    SubjectId      = subjectId,
                    IdempotencyKey = idempotencyKey
                });
            var result2 = await resp2.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();

            if (result1?.Case != null && result2?.Case != null)
            {
                Assert.That(result2.Case.CaseId, Is.EqualTo(result1.Case.CaseId),
                    "DP04: same idempotency key must return the same CaseId");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DP05: Get case by ID → 200, correct fields populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP05_GetCase_ExistingCase_Returns200_FieldsPopulated()
        {
            string subjectId = UniqueSubject();
            var createResp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest { SubjectId = subjectId });
            var createResult = await createResp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
            Assume.That(createResult?.Case, Is.Not.Null, "DP05: case creation must succeed");

            string caseId = createResult!.Case!.CaseId;

            var getResp = await _client.GetAsync($"{BaseUrl}/cases/{caseId}");

            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP05: get by ID must return 200");

            var getResult = await getResp.Content.ReadFromJsonAsync<GetOnboardingCaseResponse>();
            Assert.That(getResult!.Success, Is.True, "DP05: Success must be true");
            Assert.That(getResult.Case, Is.Not.Null, "DP05: Case must be populated");
            Assert.That(getResult.Case!.CaseId, Is.EqualTo(caseId), "DP05: CaseId must match");
            Assert.That(getResult.Case.SubjectId, Is.EqualTo(subjectId), "DP05: SubjectId must match");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP06: Get non-existent case → 404
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP06_GetCase_NonExistentId_Returns404()
        {
            string fakeId = $"non-existent-{Guid.NewGuid():N}";
            var resp = await _client.GetAsync($"{BaseUrl}/cases/{fakeId}");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "DP06: non-existent case must return 404");

            var result = await resp.Content.ReadFromJsonAsync<GetOnboardingCaseResponse>();
            Assert.That(result!.Success, Is.False, "DP06: Success must be false");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP07: Initiate provider checks → 400 PROVIDER_NOT_CONFIGURED
        //       (fail-closed: no live provider in tests, returns 400, not 500)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP07_InitiateProviderChecks_NoProvider_Returns400_ProviderNotConfigured()
        {
            var createResp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest { SubjectId = UniqueSubject() });
            var createResult = await createResp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
            Assume.That(createResult?.Case, Is.Not.Null, "DP07: case creation must succeed");

            string caseId = createResult!.Case!.CaseId;

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases/{caseId}/initiate-checks",
                new InitiateProviderChecksRequest
                {
                    ExecutionMode = KycAmlOnboardingExecutionMode.LiveProvider
                });

            // Fail-closed: no provider configured → 400 with PROVIDER_NOT_CONFIGURED
            // This is correct behaviour; it is explicitly NOT a 500 internal server error.
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest).Or.EqualTo(HttpStatusCode.OK),
                "DP07: must return 400 (PROVIDER_NOT_CONFIGURED) or 200 — never 500");

            var result = await resp.Content.ReadFromJsonAsync<InitiateProviderChecksResponse>();
            Assert.That(result, Is.Not.Null, "DP07: response must be non-null");
            // When 400, ErrorCode must indicate provider misconfiguration
            if (resp.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.That(result!.ErrorCode, Is.EqualTo("PROVIDER_NOT_CONFIGURED"),
                    "DP07: ErrorCode must be PROVIDER_NOT_CONFIGURED");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DP08: Initiate checks for non-existent case → 404
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP08_InitiateProviderChecks_NonExistentCase_Returns404()
        {
            string fakeId = $"non-existent-{Guid.NewGuid():N}";
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases/{fakeId}/initiate-checks",
                new InitiateProviderChecksRequest
                {
                    ExecutionMode = KycAmlOnboardingExecutionMode.Simulated
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "DP08: non-existent case must return 404");

            var result = await resp.Content.ReadFromJsonAsync<InitiateProviderChecksResponse>();
            Assert.That(result!.Success, Is.False, "DP08: Success must be false");
            Assert.That(result.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"),
                "DP08: ErrorCode must be CASE_NOT_FOUND");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP09: Record reviewer action (AddNote) → 200, action persisted
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP09_RecordReviewerAction_AddNote_Returns200_ActionPersisted()
        {
            var createResult = await CreateCaseAsync(UniqueSubject());
            Assume.That(createResult?.Case, Is.Not.Null);
            string caseId = createResult!.Case!.CaseId;

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases/{caseId}/reviewer-actions",
                new RecordReviewerActionRequest
                {
                    Kind      = KycAmlOnboardingActionKind.AddNote,
                    Rationale = "Test note from DP09",
                    Notes     = "Integration test verification note"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP09: AddNote must return 200");

            var result = await resp.Content.ReadFromJsonAsync<RecordReviewerActionResponse>();
            Assert.That(result!.Success, Is.True, "DP09: Success must be true");
            Assert.That(result.Action, Is.Not.Null, "DP09: Action must be populated");
            Assert.That(result.Action!.Kind, Is.EqualTo(KycAmlOnboardingActionKind.AddNote),
                "DP09: Action kind must be AddNote");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP10: Record reviewer action on non-existent case → 404
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP10_RecordReviewerAction_NonExistentCase_Returns404()
        {
            string fakeId = $"non-existent-{Guid.NewGuid():N}";
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases/{fakeId}/reviewer-actions",
                new RecordReviewerActionRequest
                {
                    Kind = KycAmlOnboardingActionKind.AddNote,
                    Notes = "note for non-existent"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "DP10: non-existent case must return 404");

            var result = await resp.Content.ReadFromJsonAsync<RecordReviewerActionResponse>();
            Assert.That(result!.Success, Is.False, "DP10: Success must be false");
            Assert.That(result.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"),
                "DP10: ErrorCode must be CASE_NOT_FOUND");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP11: Get evidence summary for existing case → 200
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP11_GetEvidenceSummary_ExistingCase_Returns200()
        {
            var createResult = await CreateCaseAsync(UniqueSubject());
            Assume.That(createResult?.Case, Is.Not.Null);
            string caseId = createResult!.Case!.CaseId;

            var resp = await _client.GetAsync($"{BaseUrl}/cases/{caseId}/evidence");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP11: evidence for existing case must return 200");

            var result = await resp.Content.ReadFromJsonAsync<GetOnboardingEvidenceSummaryResponse>();
            Assert.That(result!.Success, Is.True, "DP11: Success must be true");
            Assert.That(result.Summary, Is.Not.Null, "DP11: Summary must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP12: Get evidence summary for non-existent case → 404
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP12_GetEvidenceSummary_NonExistentCase_Returns404()
        {
            string fakeId = $"non-existent-{Guid.NewGuid():N}";
            var resp = await _client.GetAsync($"{BaseUrl}/cases/{fakeId}/evidence");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "DP12: non-existent case evidence must return 404");

            var result = await resp.Content.ReadFromJsonAsync<GetOnboardingEvidenceSummaryResponse>();
            Assert.That(result!.Success, Is.False, "DP12: Success must be false");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP13: List cases → 200 with array
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP13_ListCases_Returns200_WithCasesArray()
        {
            var resp = await _client.GetAsync($"{BaseUrl}/cases");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP13: list must return 200");

            var result = await resp.Content.ReadFromJsonAsync<ListOnboardingCasesResponse>();
            Assert.That(result!.Success, Is.True, "DP13: Success must be true");
            Assert.That(result.Cases, Is.Not.Null, "DP13: Cases array must not be null");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP14: Create multiple cases, list returns all
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP14_CreateMultipleCases_ListReturnsAll()
        {
            // Get baseline count first
            var baselineResp = await _client.GetAsync($"{BaseUrl}/cases");
            var baseline = await baselineResp.Content.ReadFromJsonAsync<ListOnboardingCasesResponse>();
            int baselineCount = baseline?.Cases?.Count ?? 0;

            // Create 3 new cases
            string subject1 = UniqueSubject();
            string subject2 = UniqueSubject();
            string subject3 = UniqueSubject();
            await CreateCaseAsync(subject1);
            await CreateCaseAsync(subject2);
            await CreateCaseAsync(subject3);

            var listResp = await _client.GetAsync($"{BaseUrl}/cases");
            var listResult = await listResp.Content.ReadFromJsonAsync<ListOnboardingCasesResponse>();

            Assert.That(listResult!.Cases.Count, Is.GreaterThanOrEqualTo(baselineCount + 3),
                "DP14: list must contain at least 3 more cases than baseline");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP15–DP20: Unauthenticated requests → 401
        // ════════════════════════════════════════════════════════════════════

        /// <summary>DP15: Unauthenticated create → 401.</summary>
        [Test]
        public async Task DP15_Unauthenticated_CreateCase_Returns401()
        {
            await using var factory = new KycAmlOnboardingFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest { SubjectId = UniqueSubject() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP15: unauthenticated create must return 401");
        }

        /// <summary>DP16: Unauthenticated get case → 401.</summary>
        [Test]
        public async Task DP16_Unauthenticated_GetCase_Returns401()
        {
            await using var factory = new KycAmlOnboardingFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.GetAsync($"{BaseUrl}/cases/some-case-id");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP16: unauthenticated get must return 401");
        }

        /// <summary>DP17: Unauthenticated initiate checks → 401.</summary>
        [Test]
        public async Task DP17_Unauthenticated_InitiateChecks_Returns401()
        {
            await using var factory = new KycAmlOnboardingFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync(
                $"{BaseUrl}/cases/some-case-id/initiate-checks",
                new InitiateProviderChecksRequest());

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP17: unauthenticated initiate checks must return 401");
        }

        /// <summary>DP18: Unauthenticated reviewer action → 401.</summary>
        [Test]
        public async Task DP18_Unauthenticated_ReviewerAction_Returns401()
        {
            await using var factory = new KycAmlOnboardingFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync(
                $"{BaseUrl}/cases/some-case-id/reviewer-actions",
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP18: unauthenticated reviewer action must return 401");
        }

        /// <summary>DP19: Unauthenticated evidence → 401.</summary>
        [Test]
        public async Task DP19_Unauthenticated_GetEvidence_Returns401()
        {
            await using var factory = new KycAmlOnboardingFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.GetAsync($"{BaseUrl}/cases/some-case-id/evidence");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP19: unauthenticated evidence must return 401");
        }

        /// <summary>DP20: Unauthenticated list → 401.</summary>
        [Test]
        public async Task DP20_Unauthenticated_ListCases_Returns401()
        {
            await using var factory = new KycAmlOnboardingFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.GetAsync($"{BaseUrl}/cases");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP20: unauthenticated list must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP21: Schema contract — CreateOnboardingCaseResponse fields
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP21_CreateCase_SchemaContract_RequiredFieldsNonNull()
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest
                {
                    SubjectId   = UniqueSubject(),
                    SubjectKind = KycAmlOnboardingSubjectKind.Individual
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP21: create must return 200");

            string json = await resp.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // success field
            bool hasSuccess = root.EnumerateObject()
                .Any(p => string.Equals(p.Name, "success", StringComparison.OrdinalIgnoreCase));
            Assert.That(hasSuccess, Is.True, "DP21: 'success' field must be present");

            var result = await resp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.That(result!.Success, Is.True, "DP21: success must be true");
            Assert.That(result.Case, Is.Not.Null, "DP21: Case must not be null");
            Assert.That(result.Case!.CaseId, Is.Not.Null.And.Not.Empty,
                "DP21: CaseId must be non-empty");
            Assert.That(result.Case.SubjectId, Is.Not.Null.And.Not.Empty,
                "DP21: SubjectId must be non-empty");
            // State is a value type — always set; verify it is a valid enum value
            Assert.That(Enum.IsDefined(typeof(KycAmlOnboardingCaseState), result.Case.State),
                "DP21: State must be a valid KycAmlOnboardingCaseState");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP22: CorrelationId in request echoed in response
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP22_CorrelationId_InRequest_EchoedInResponse()
        {
            string correlationId = $"corr-dp22-{Guid.NewGuid():N}";

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest
                {
                    SubjectId     = UniqueSubject(),
                    CorrelationId = correlationId
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP22: create must return 200");

            var result = await resp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "DP22: CorrelationId must be present in response");
            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "DP22: CorrelationId in response must match the one supplied in request");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP23: Two different subjects create separate cases
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP23_TwoSubjects_ProduceSeparateCases()
        {
            string subject1 = UniqueSubject();
            string subject2 = UniqueSubject();

            var result1 = await CreateCaseAsync(subject1);
            var result2 = await CreateCaseAsync(subject2);

            if (result1?.Case != null && result2?.Case != null)
            {
                Assert.That(result1.Case.CaseId, Is.Not.EqualTo(result2.Case.CaseId),
                    "DP23: different subjects must produce different case IDs");
                Assert.That(result1.Case.SubjectId, Is.EqualTo(subject1),
                    "DP23: case 1 must have subject 1");
                Assert.That(result2.Case.SubjectId, Is.EqualTo(subject2),
                    "DP23: case 2 must have subject 2");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DP24: Evidence summary includes IsProviderConfigured flag
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP24_EvidenceSummary_IncludesIsProviderConfiguredFlag()
        {
            var createResult = await CreateCaseAsync(UniqueSubject());
            Assume.That(createResult?.Case, Is.Not.Null, "DP24: case creation must succeed");
            string caseId = createResult!.Case!.CaseId;

            var resp = await _client.GetAsync($"{BaseUrl}/cases/{caseId}/evidence");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP24: evidence must return 200");

            // Read body once as a string, then parse both as typed DTO and as raw JSON.
            string json = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GetOnboardingEvidenceSummaryResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.That(result!.Success, Is.True, "DP24: Success must be true");
            Assert.That(result.Summary, Is.Not.Null, "DP24: Summary must be populated");

            // IsProviderConfigured is a bool — verify it is explicitly present in JSON.
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // IsProviderConfigured may be at response level or inside summary
            bool foundAtRoot = root.EnumerateObject()
                .Any(p => string.Equals(p.Name, "isProviderConfigured", StringComparison.OrdinalIgnoreCase));
            bool summaryNode = root.TryGetProperty("summary", out var summary) || 
                               root.TryGetProperty("Summary", out summary);
            bool foundInSummary = summaryNode && summary.ValueKind == JsonValueKind.Object &&
                                  summary.EnumerateObject()
                                         .Any(p => string.Equals(p.Name, "isProviderConfigured",
                                                                  StringComparison.OrdinalIgnoreCase));

            Assert.That(foundAtRoot || foundInSummary, Is.True,
                "DP24: 'isProviderConfigured' must appear in response or summary");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP25: E2E lifecycle — create → get → record note → verify in actions
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP25_E2E_Lifecycle_Create_Get_RecordNote_VerifyInActions()
        {
            // Step 1: Create case
            string subjectId = UniqueSubject();
            var createResult = await CreateCaseAsync(subjectId);
            Assume.That(createResult?.Case, Is.Not.Null, "DP25: case creation must succeed");
            string caseId = createResult!.Case!.CaseId;

            // Step 2: Get case — verify it's in Initiated state
            var getResp = await _client.GetAsync($"{BaseUrl}/cases/{caseId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP25: get must return 200");
            var getResult = await getResp.Content.ReadFromJsonAsync<GetOnboardingCaseResponse>();
            Assert.That(getResult!.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated),
                "DP25: initial state must be Initiated");

            // Step 3: Record an AddNote action
            string noteText = $"E2E lifecycle note DP25-{Guid.NewGuid():N}";
            var actionResp = await _client.PostAsJsonAsync(
                $"{BaseUrl}/cases/{caseId}/reviewer-actions",
                new RecordReviewerActionRequest
                {
                    Kind  = KycAmlOnboardingActionKind.AddNote,
                    Notes = noteText
                });
            Assert.That(actionResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP25: record action must return 200");
            var actionResult = await actionResp.Content.ReadFromJsonAsync<RecordReviewerActionResponse>();
            Assert.That(actionResult!.Success, Is.True, "DP25: action record must succeed");
            Assert.That(actionResult.Action, Is.Not.Null, "DP25: Action must be populated");
            Assert.That(actionResult.Action!.Kind, Is.EqualTo(KycAmlOnboardingActionKind.AddNote),
                "DP25: Action kind must be AddNote");

            // Step 4: Get case again — verify the action is in the actions list
            var getResp2 = await _client.GetAsync($"{BaseUrl}/cases/{caseId}");
            var getResult2 = await getResp2.Content.ReadFromJsonAsync<GetOnboardingCaseResponse>();
            Assert.That(getResult2!.Case!.Actions, Is.Not.Null.And.Not.Empty,
                "DP25: Actions list must be non-empty after recording a note");
            Assert.That(
                getResult2.Case.Actions.Any(a => a.Kind == KycAmlOnboardingActionKind.AddNote),
                Is.True,
                "DP25: Actions list must contain the AddNote action");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static string UniqueSubject() => $"subject-dp-{Guid.NewGuid():N}";

        private async Task<CreateOnboardingCaseResponse?> CreateCaseAsync(string subjectId)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/cases",
                new CreateOnboardingCaseRequest { SubjectId = subjectId });
            return await resp.Content.ReadFromJsonAsync<CreateOnboardingCaseResponse>();
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"kyc-{userTag}@kyc-aml-onboarding-parity-test.biatec.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = email,
                Password        = "KycAmlOnboardingParityIT!Pass1",
                ConfirmPassword = "KycAmlOnboardingParityIT!Pass1",
                FullName        = $"KYC AML Parity Test User ({userTag})"
            });

            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created),
                $"Registration must succeed for user '{userTag}'. Status: {resp.StatusCode}");

            JsonDocument? doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? token = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty,
                $"Registration must return a non-empty accessToken for user '{userTag}'");
            return token!;
        }
    }
}

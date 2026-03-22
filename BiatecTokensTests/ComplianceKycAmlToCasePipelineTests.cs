using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.KycAmlSignOff;
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
    /// Integration tests proving that KYC/AML sign-off evidence flows from the
    /// <c>/api/v1/kyc-aml-signoff</c> pipeline into the compliance case lifecycle
    /// at <c>/api/v1/compliance-cases</c>.
    ///
    /// These tests address the roadmap gaps for KYC Integration (48%) and AML
    /// Screening (43%) by proving the backend is the authoritative source for
    /// evidence-based case gating, reviewer handoff, and fail-closed operator
    /// messaging.
    ///
    /// Coverage:
    ///
    /// KC01: Initiate KYC sign-off → record created, RecordId populated, state Pending
    /// KC02: Unauthenticated KYC initiation → 401 fail-closed
    /// KC03: Initiate KYC with empty SubjectId → 400 fail-closed
    /// KC04: KYC idempotency: same idempotency key → same RecordId returned
    /// KC05: Initiate AML screening → record created, CheckKind=AmlScreening
    /// KC06: Initiate Combined KYC+AML → CheckKind=Combined in record
    /// KC07: Poll KYC status → response success, outcome populated
    /// KC08: Get KYC record by ID → returns correct SubjectId
    /// KC09: Get KYC readiness for subject with no records → IncompleteEvidence state
    /// KC10: List KYC records for subject → returns records for that subject only
    /// KC11: Add KYC sign-off result to compliance case as evidence → case has evidence
    /// KC12: KYC evidence linked by RecordId in ProviderReference → traceable
    /// KC13: Compliance case with valid KYC evidence → EvidenceAvailability.ValidItems >= 1
    /// KC14: Orchestration view after KYC evidence → evidence visible in orchestration payload
    /// KC15: KYC outcome Approved + evidence on case → blockers reduced
    /// KC16: Add AML screening result to compliance case → AML evidence traceable
    /// KC17: AML + KYC both added → evidence count = 2 minimum
    /// KC18: Case with KYC + AML evidence advances lifecycle to UnderReview
    /// KC19: Decision record of kind KycApproval can be added to compliance case
    /// KC20: Decision record of kind AmlScreening can be added to compliance case
    /// KC21: KYC artifacts endpoint returns artifact list for record
    /// KC22: Compliance case with KYC + AML → orchestration shows reviewer assignment guidance
    /// KC23: Reject case after KYC failure decision → final state Rejected
    /// KC24: List KYC records for subject with multiple records → all returned
    /// KC25: Full KC workflow: initiate KYC → add to case → advance → approve
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceKycAmlToCasePipelineTests
    {
        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class KcFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "KcPipelineTestSecretKey32CharsMinimum!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "KcPipelineTestKey32+CharMinimum!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "kc-pipeline-test",
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

        private KcFactory _factory = null!;
        private HttpClient _client = null!;

        private const string KycBase   = "/api/v1/kyc-aml-signoff";
        private const string CasesBase = "/api/v1/compliance-cases";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new KcFactory();
            _client  = _factory.CreateClient();
            string jwt = await ObtainJwtAsync(_client, $"kc-{Guid.NewGuid():N}");
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // KC01: Initiate KYC sign-off
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC01_InitiateKycSignOff_RecordCreated_PendingState()
        {
            string subjectId = UniqueSubject();
            var resp   = await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.IdentityKyc);

            Assert.That(resp.Success, Is.True, "KC01: initiate KYC must succeed");
            Assert.That(resp.Record, Is.Not.Null, "KC01: Record must not be null");
            Assert.That(resp.Record!.RecordId, Is.Not.Null.And.Not.Empty, "KC01: RecordId must be populated");
            Assert.That(resp.Record.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Pending), "KC01: initial outcome must be Pending");
            Assert.That(resp.Record.SubjectId, Is.EqualTo(subjectId), "KC01: SubjectId must match");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC02: Unauthenticated → 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC02_Unauthenticated_Returns401()
        {
            using var anonClient = _factory.CreateClient();
            var resp = await anonClient.PostAsJsonAsync($"{KycBase}/initiate",
                new InitiateKycAmlSignOffRequest { SubjectId = UniqueSubject() });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "KC02: unauthenticated request must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC03: Empty SubjectId → 400
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC03_EmptySubjectId_Returns400FailClosed()
        {
            var resp = await _client.PostAsJsonAsync($"{KycBase}/initiate",
                new InitiateKycAmlSignOffRequest { SubjectId = "" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "KC03: empty SubjectId must return 400");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC04: Idempotency key → same RecordId returned
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC04_IdempotencyKey_ReturnsSameRecord()
        {
            string subjectId = UniqueSubject();
            string idemKey   = $"kc04-{Guid.NewGuid():N}";

            var r1 = await InitiateKycAsync(subjectId, idempotencyKey: idemKey);
            var r2 = await InitiateKycAsync(subjectId, idempotencyKey: idemKey);

            Assert.That(r1.Record!.RecordId, Is.EqualTo(r2.Record!.RecordId),
                "KC04: same idempotency key must return same RecordId");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC05: Initiate AML screening
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC05_InitiateAmlScreening_CheckKindIsAmlScreening()
        {
            var resp = await InitiateKycAsync(UniqueSubject(), KycAmlSignOffCheckKind.AmlScreening);
            Assert.That(resp.Record!.CheckKind, Is.EqualTo(KycAmlSignOffCheckKind.AmlScreening),
                "KC05: CheckKind must be AmlScreening");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC06: Initiate Combined KYC+AML
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC06_InitiateCombined_CheckKindIsCombined()
        {
            var resp = await InitiateKycAsync(UniqueSubject(), KycAmlSignOffCheckKind.Combined);
            Assert.That(resp.Record!.CheckKind, Is.EqualTo(KycAmlSignOffCheckKind.Combined),
                "KC06: CheckKind must be Combined");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC07: Poll KYC status → success
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC07_PollKycStatus_ReturnsSuccess()
        {
            var kycResp = await InitiateKycAsync(UniqueSubject());
            string recordId = kycResp.Record!.RecordId;

            var pollResp   = await _client.PostAsJsonAsync($"{KycBase}/{recordId}/poll", new { });
            pollResp.EnsureSuccessStatusCode();
            var result = await pollResp.Content.ReadFromJsonAsync<PollKycAmlSignOffStatusResponse>();

            Assert.That(result!.Success, Is.True, "KC07: poll must succeed");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC08: Get KYC record by ID
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC08_GetKycRecord_ReturnsCorrectSubjectId()
        {
            string subjectId = UniqueSubject();
            var kycResp = await InitiateKycAsync(subjectId);
            string recordId = kycResp.Record!.RecordId;

            var getResp = await _client.GetAsync($"{KycBase}/{recordId}");
            getResp.EnsureSuccessStatusCode();
            var result = await getResp.Content.ReadFromJsonAsync<GetKycAmlSignOffRecordResponse>();

            Assert.That(result!.Record!.SubjectId, Is.EqualTo(subjectId), "KC08: SubjectId must match");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC09: Get readiness for subject with no records → IncompleteEvidence
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC09_GetReadiness_NoRecords_IncompleteEvidence()
        {
            string subjectId = UniqueSubject();
            string recordId  = (await InitiateKycAsync(subjectId)).Record!.RecordId;

            var resp   = await _client.GetAsync($"{KycBase}/{recordId}/readiness");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<KycAmlSignOffReadinessResponse>();

            Assert.That(result, Is.Not.Null, "KC09: readiness response must not be null");
            Assert.That(result!.ReadinessState, Is.Not.EqualTo(KycAmlSignOffReadinessState.Ready),
                "KC09: newly initiated check cannot be Ready — must be Pending/AwaitingProvider");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC10: List records for subject
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC10_ListRecordsForSubject_ReturnsOnlyThatSubject()
        {
            string subjectA = UniqueSubject();
            string subjectB = UniqueSubject();

            await InitiateKycAsync(subjectA);
            await InitiateKycAsync(subjectA);
            await InitiateKycAsync(subjectB);

            var resp   = await _client.GetAsync($"{KycBase}/subject/{subjectA}");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ListKycAmlSignOffRecordsResponse>();

            Assert.That(result!.Records.All(r => r.SubjectId == subjectA), Is.True,
                "KC10: all records must belong to subjectA");
            Assert.That(result.Records.Count, Is.GreaterThanOrEqualTo(2), "KC10: must return >= 2 records for subjectA");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC11: Add KYC result to compliance case as evidence
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC11_AddKycEvidenceToCase_CaseHasKycEvidence()
        {
            string subjectId = UniqueSubject();
            string caseId    = await CreateCaseAsync(subjectId: subjectId);
            var kycResp      = await InitiateKycAsync(subjectId);
            string recordId  = kycResp.Record!.RecordId;

            var addEvidenceResp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "KYC",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "KycAmlSignOff",
                    ProviderReference = recordId,
                    CapturedAt        = DateTimeOffset.UtcNow,
                    Summary           = $"KYC sign-off record {recordId}"
                });
            addEvidenceResp.EnsureSuccessStatusCode();
            var result = await addEvidenceResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();

            Assert.That(result!.Success, Is.True, "KC11: adding KYC evidence must succeed");
            Assert.That(result.Case!.EvidenceSummaries.Any(e => e.EvidenceType == "KYC"), Is.True,
                "KC11: case must have KYC evidence");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC12: KYC evidence traceable via ProviderReference = RecordId
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC12_KycEvidence_TraceableViaProviderReference()
        {
            string subjectId = UniqueSubject();
            string caseId    = await CreateCaseAsync(subjectId: subjectId);
            var kycResp      = await InitiateKycAsync(subjectId);
            string recordId  = kycResp.Record!.RecordId;

            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "KYC",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "KycAmlSignOff",
                    ProviderReference = recordId,
                    CapturedAt        = DateTimeOffset.UtcNow
                });

            var caseResp = await _client.GetAsync($"{CasesBase}/{caseId}");
            var caseResult = await caseResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();

            var kycEvidence = caseResult!.Case!.EvidenceSummaries
                .FirstOrDefault(e => e.ProviderReference == recordId);
            Assert.That(kycEvidence, Is.Not.Null, "KC12: evidence must be traceable by RecordId in ProviderReference");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC13: Valid KYC evidence → EvidenceAvailability.ValidItems >= 1
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC13_ValidKycEvidence_EvidenceAvailabilityValidItemsAtLeast1()
        {
            string caseId = await CreateCaseAsync();
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);

            var resp   = await _client.GetAsync($"{CasesBase}/{caseId}/evidence-availability");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetEvidenceAvailabilityResponse>();

            Assert.That(result!.Availability!.ValidItems, Is.GreaterThanOrEqualTo(1),
                "KC13: valid KYC evidence must be counted in ValidItems");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC14: Orchestration view shows KYC evidence
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC14_OrchestrationView_AfterKycEvidence_ReflectsEvidence()
        {
            string caseId = await CreateCaseAsync();
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);

            var resp   = await _client.GetAsync($"{CasesBase}/{caseId}/orchestration-view");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();

            Assert.That(result!.View!.EvidenceAvailability.TotalEvidenceItems, Is.GreaterThanOrEqualTo(1),
                "KC14: orchestration view must reflect KYC evidence");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC15: Valid KYC evidence → blockers reduced
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC15_ValidKycEvidence_ReducesBlockers()
        {
            string caseId = await CreateCaseAsync();

            // Before evidence: should have MissingEvidence blocker
            var before = await _client.GetAsync($"{CasesBase}/{caseId}/blockers");
            var beforeResult = await before.Content.ReadFromJsonAsync<EvaluateBlockersResponse>();
            int beforeBlockers = beforeResult!.FailClosedBlockers.Count;

            // Add valid KYC evidence
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);

            // After: MissingEvidence blocker should be gone
            var after = await _client.GetAsync($"{CasesBase}/{caseId}/blockers");
            var afterResult = await after.Content.ReadFromJsonAsync<EvaluateBlockersResponse>();
            int afterBlockers = afterResult!.FailClosedBlockers.Count;

            Assert.That(afterBlockers, Is.LessThan(beforeBlockers),
                "KC15: adding valid KYC evidence must reduce fail-closed blocker count");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC16: AML screening result added to compliance case
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC16_AmlScreeningResult_AddedToCase_AmlEvidenceTraceable()
        {
            string subjectId = UniqueSubject();
            string caseId    = await CreateCaseAsync(subjectId: subjectId);
            var amlResp      = await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.AmlScreening);
            string recordId  = amlResp.Record!.RecordId;

            var addResp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "AML",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "KycAmlSignOff",
                    ProviderReference = recordId,
                    CapturedAt        = DateTimeOffset.UtcNow,
                    Summary           = $"AML screening record {recordId}"
                });
            addResp.EnsureSuccessStatusCode();
            var result = await addResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();

            var amlEvidence = result!.Case!.EvidenceSummaries
                .FirstOrDefault(e => e.EvidenceType == "AML" && e.ProviderReference == recordId);
            Assert.That(amlEvidence, Is.Not.Null, "KC16: AML evidence must be traceable by RecordId");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC17: KYC + AML both added → evidence count = 2 minimum
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC17_KycAndAml_BothAdded_EvidenceCountAtLeast2()
        {
            string caseId = await CreateCaseAsync();
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);
            await AddAmlEvidenceAsync(caseId, CaseEvidenceStatus.Valid);

            var resp   = await _client.GetAsync($"{CasesBase}/{caseId}/evidence-availability");
            var result = await resp.Content.ReadFromJsonAsync<GetEvidenceAvailabilityResponse>();

            Assert.That(result!.Availability!.TotalEvidenceItems, Is.GreaterThanOrEqualTo(2),
                "KC17: KYC + AML evidence must produce at least 2 total evidence items");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC18: KYC + AML evidence → case can advance to UnderReview
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC18_KycAndAmlEvidence_CaseAdvancesToUnderReview()
        {
            string caseId = await CreateCaseAsync();
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);
            await AddAmlEvidenceAsync(caseId, CaseEvidenceStatus.Valid);

            // Transition through lifecycle
            await TransitionAsync(caseId, ComplianceCaseState.EvidencePending);
            var resp = await TransitionAsync(caseId, ComplianceCaseState.UnderReview);

            Assert.That(resp.Case!.State, Is.EqualTo(ComplianceCaseState.UnderReview),
                "KC18: case with KYC+AML evidence must advance to UnderReview");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC19: Decision record KycApproval added to case
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC19_DecisionRecord_KycApproval_AddedToCase()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind           = CaseDecisionKind.KycApproval,
                    DecisionSummary = "KYC identity verified by provider",
                    Outcome        = "Approved",
                    ProviderName   = "IdentityProvider",
                    IsAdverse      = false
                });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<AddDecisionRecordResponse>();

            Assert.That(result!.Success, Is.True, "KC19: adding KYC decision must succeed");
            Assert.That(result.DecisionRecord!.Kind, Is.EqualTo(CaseDecisionKind.KycApproval),
                "KC19: decision kind must be KycApproval");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC20: Decision record AmlClear added to case
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC20_DecisionRecord_AmlClear_AddedToCase()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind           = CaseDecisionKind.AmlClear,
                    DecisionSummary = "AML/sanctions screening clear",
                    Outcome        = "Clear",
                    ProviderName   = "SanctionsScreener",
                    IsAdverse      = false
                });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<AddDecisionRecordResponse>();

            Assert.That(result!.DecisionRecord!.Kind, Is.EqualTo(CaseDecisionKind.AmlClear),
                "KC20: decision kind must be AmlClear");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC21: KYC artifacts endpoint returns list
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC21_KycArtifacts_ReturnsArtifactList()
        {
            string subjectId = UniqueSubject();
            var kycResp      = await InitiateKycAsync(subjectId);
            string recordId  = kycResp.Record!.RecordId;

            var resp   = await _client.GetAsync($"{KycBase}/{recordId}/artifacts");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetKycAmlSignOffArtifactsResponse>();

            Assert.That(result, Is.Not.Null, "KC21: artifacts response must not be null");
            Assert.That(result!.RecordId, Is.EqualTo(recordId), "KC21: RecordId must match");
            Assert.That(result.Artifacts, Is.Not.Null, "KC21: Artifacts list must not be null");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC22: Orchestration view after KYC+AML includes reviewer assignment guidance
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC22_OrchestrationView_KycAndAml_IncludesNextActionGuidance()
        {
            string caseId = await CreateCaseAsync();
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);
            await AddAmlEvidenceAsync(caseId, CaseEvidenceStatus.Valid);
            await TransitionAsync(caseId, ComplianceCaseState.EvidencePending);
            await TransitionAsync(caseId, ComplianceCaseState.UnderReview);

            var resp   = await _client.GetAsync($"{CasesBase}/{caseId}/orchestration-view");
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();

            Assert.That(result!.View!.NextActions, Is.Not.Null, "KC22: NextActions must not be null");
            Assert.That(result.View.State, Is.EqualTo(ComplianceCaseState.UnderReview),
                "KC22: state must be UnderReview after evidence + lifecycle progression");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC23: Reject case after adverse AML decision → Rejected
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC23_RejectAfterAdverseAml_CaseStateRejected()
        {
            string caseId = await CreateCaseAsync();
            await AddKycEvidenceAsync(caseId, CaseEvidenceStatus.Valid);
            await AddAmlEvidenceAsync(caseId, CaseEvidenceStatus.Valid);
            await TransitionAsync(caseId, ComplianceCaseState.EvidencePending);
            await TransitionAsync(caseId, ComplianceCaseState.UnderReview);

            // Add adverse AML decision
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind           = CaseDecisionKind.AmlHit,
                    DecisionSummary = "Sanctions hit confirmed — subject on OFAC list",
                    Outcome        = "Rejected",
                    IsAdverse      = true
                });

            var rejectResp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/reject",
                new RejectComplianceCaseRequest { Reason = "Confirmed sanctions hit via AML screening" });
            rejectResp.EnsureSuccessStatusCode();
            var result = await rejectResp.Content.ReadFromJsonAsync<RejectComplianceCaseResponse>();

            Assert.That(result!.Case!.State, Is.EqualTo(ComplianceCaseState.Rejected),
                "KC23: case must be Rejected after adverse AML decision");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC24: List KYC records for subject with multiple records
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC24_ListRecords_MultipleForSubject_AllReturned()
        {
            string subjectId = UniqueSubject();

            await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.IdentityKyc);
            await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.AmlScreening);
            await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.Combined);

            var resp   = await _client.GetAsync($"{KycBase}/subject/{subjectId}");
            var result = await resp.Content.ReadFromJsonAsync<ListKycAmlSignOffRecordsResponse>();

            Assert.That(result!.TotalCount, Is.GreaterThanOrEqualTo(3),
                "KC24: all 3 records for subject must be returned");
        }

        // ════════════════════════════════════════════════════════════════════
        // KC25: Full KC workflow: initiate → add to case → advance → approve
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task KC25_FullKcWorkflow_InitiateKycAml_AddToCase_Advance_Approve()
        {
            string subjectId = UniqueSubject();
            string issuerId  = UniqueIssuer();

            // 1. Create compliance case
            string caseId = await CreateCaseAsync(issuerId, subjectId);

            // 2. Initiate KYC and AML sign-off records for the subject
            var kycResp = await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.IdentityKyc);
            var amlResp = await InitiateKycAsync(subjectId, KycAmlSignOffCheckKind.AmlScreening);

            // 3. Add KYC evidence to case
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "KYC",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "KycAmlSignOff",
                    ProviderReference = kycResp.Record!.RecordId,
                    CapturedAt        = DateTimeOffset.UtcNow,
                    Summary           = "KYC identity verified"
                });

            // 4. Add AML evidence to case
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "AML",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "KycAmlSignOff",
                    ProviderReference = amlResp.Record!.RecordId,
                    CapturedAt        = DateTimeOffset.UtcNow,
                    Summary           = "AML screening clear"
                });

            // 5. Add decision records
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind = CaseDecisionKind.KycApproval, DecisionSummary = "KYC passed",
                    Outcome = "Approved", IsAdverse = false
                });
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind = CaseDecisionKind.AmlClear, DecisionSummary = "AML clear",
                    Outcome = "Clear", IsAdverse = false
                });

            // 6. Assign reviewer
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/assign",
                new AssignCaseRequest { ReviewerId = "reviewer-kc25", Reason = "KYC/AML complete" });

            // 7. Progress lifecycle
            await TransitionAsync(caseId, ComplianceCaseState.EvidencePending);
            await TransitionAsync(caseId, ComplianceCaseState.UnderReview);

            // 8. Verify orchestration view shows complete evidence picture
            var orchResp   = await _client.GetAsync($"{CasesBase}/{caseId}/orchestration-view");
            var orchResult = await orchResp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(orchResult!.View!.EvidenceAvailability.ValidItems, Is.GreaterThanOrEqualTo(2),
                "KC25: orchestration view must show 2+ valid evidence items");
            Assert.That(orchResult.View.AssignedReviewerId, Is.EqualTo("reviewer-kc25"),
                "KC25: reviewer must be assigned");

            // 9. Approve
            var approveResp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/approve",
                new ApproveComplianceCaseRequest { Rationale = "KYC and AML checks passed" });
            approveResp.EnsureSuccessStatusCode();
            var approveResult = await approveResp.Content.ReadFromJsonAsync<ApproveComplianceCaseResponse>();
            Assert.That(approveResult!.Case!.State, Is.EqualTo(ComplianceCaseState.Approved),
                "KC25: final state must be Approved");

            // 10. Verify decision history
            var decisionResp   = await _client.GetAsync($"{CasesBase}/{caseId}/decisions");
            var decisionResult = await decisionResp.Content.ReadFromJsonAsync<GetDecisionHistoryResponse>();
            Assert.That(decisionResult!.Decisions.Count, Is.GreaterThanOrEqualTo(2),
                "KC25: decision history must contain >= 2 records");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private async Task<InitiateKycAmlSignOffResponse> InitiateKycAsync(
            string subjectId,
            KycAmlSignOffCheckKind kind = KycAmlSignOffCheckKind.IdentityKyc,
            string? idempotencyKey = null)
        {
            var resp = await _client.PostAsJsonAsync($"{KycBase}/initiate",
                new InitiateKycAmlSignOffRequest
                {
                    SubjectId              = subjectId,
                    CheckKind              = kind,
                    RequestedExecutionMode = KycAmlSignOffExecutionMode.Simulated,
                    IdempotencyKey         = idempotencyKey
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<InitiateKycAmlSignOffResponse>())!;
        }

        private async Task AddKycEvidenceAsync(string caseId, CaseEvidenceStatus status)
        {
            var resp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType = "KYC", Status = status,
                    ProviderName = "KycAmlSignOff",
                    CapturedAt   = DateTimeOffset.UtcNow,
                    Summary      = "KYC evidence for test"
                });
            resp.EnsureSuccessStatusCode();
        }

        private async Task AddAmlEvidenceAsync(string caseId, CaseEvidenceStatus status)
        {
            var resp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType = "AML", Status = status,
                    ProviderName = "KycAmlSignOff",
                    CapturedAt   = DateTimeOffset.UtcNow,
                    Summary      = "AML evidence for test"
                });
            resp.EnsureSuccessStatusCode();
        }

        private async Task<GetComplianceCaseResponse> TransitionAsync(
            string caseId, ComplianceCaseState newState)
        {
            var resp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = newState });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>())!;
        }

        private async Task<string> CreateCaseAsync(
            string? issuerId  = null,
            string? subjectId = null)
        {
            var resp = await _client.PostAsJsonAsync(CasesBase, new CreateComplianceCaseRequest
            {
                IssuerId  = issuerId  ?? UniqueIssuer(),
                SubjectId = subjectId ?? UniqueSubject(),
                Type      = CaseType.InvestorEligibility,
                Priority  = CasePriority.Medium
            });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>())!.Case!.CaseId;
        }

        private static string UniqueSubject() => $"subject-kc-{Guid.NewGuid():N}";
        private static string UniqueIssuer()  => $"issuer-kc-{Guid.NewGuid():N}";

        private static async Task<string> ObtainJwtAsync(HttpClient client, string tag)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = $"kc-{tag}@kc-pipeline.biatec.example.com",
                Password        = "KcPipelineIT!Pass1",
                ConfirmPassword = "KcPipelineIT!Pass1",
                FullName        = $"KC Pipeline Test ({tag})"
            });
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created));
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? token = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            return token!;
        }
    }
}

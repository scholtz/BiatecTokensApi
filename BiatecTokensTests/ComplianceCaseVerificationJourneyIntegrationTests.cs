using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
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
    /// Integration tests proving that the live-provider verification journey can be
    /// connected to compliance cases, and that evidence from a journey flows into
    /// the compliance case lifecycle.
    ///
    /// These tests verify the end-to-end connection between:
    ///   - <c>/api/v1/verification-journey</c> (journey creation, status, evidence)
    ///   - <c>/api/v1/compliance-cases</c> (case creation, evidence, orchestration view)
    ///
    /// Coverage:
    ///
    /// VJ01: Start journey with CaseId → journey record has CaseId populated
    /// VJ02: Journey CaseId matches the compliance case that was created first
    /// VJ03: Verification journey evidence can be added to the linked compliance case
    /// VJ04: Journey for non-existent subject still succeeds (CaseId is optional binding)
    /// VJ05: Start journey without CaseId → journey still succeeds (CaseId null)
    /// VJ06: Journey status endpoint returns CaseId in the journey record
    /// VJ07: Two journeys for same subject with different CaseIds are independent
    /// VJ08: Journey decision evaluation returns structured outcome
    /// VJ09: Generate release evidence for journey → EvidenceId non-null
    /// VJ10: Adding journey evidence to compliance case transitions evidence readiness
    /// VJ11: Orchestration view of case with journey-linked evidence shows evidence status
    /// VJ12: Journey idempotency key reuse → same journey returned (not duplicated)
    /// VJ13: Unauthenticated journey start → 401
    /// VJ14: Journey start with empty SubjectId → 400 fail-closed
    /// VJ15: Full integration: create case → start journey linked to case → add evidence → verify orchestration
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseVerificationJourneyIntegrationTests
    {
        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class VjIntegrationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "VjIntegrationTestSecretKey32CharsMinimum!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "VjIntegrationTestKey32+CharMinimum!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "vj-integration-test",
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

        private VjIntegrationFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string JourneyBase = "/api/v1/verification-journey";
        private const string CasesBase   = "/api/v1/compliance-cases";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new VjIntegrationFactory();
            _client  = _factory.CreateClient();
            _jwt     = await ObtainJwtAsync(_client, $"vj-{Guid.NewGuid():N}");
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
        // VJ01: Start journey with CaseId → journey has CaseId populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ01_StartJourney_WithCaseId_JourneyHasCaseId()
        {
            string caseId    = await CreateCaseAsync();
            string subjectId = UniqueSubject();

            var resp = await StartJourneyAsync(subjectId, caseId: caseId);
            Assert.That(resp.Success, Is.True, "VJ01: start journey must succeed");
            Assert.That(resp.Journey, Is.Not.Null, "VJ01: Journey must not be null");
            Assert.That(resp.Journey!.CaseId, Is.EqualTo(caseId),
                "VJ01: Journey.CaseId must match the provided CaseId");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ02: Journey CaseId matches the compliance case created first
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ02_JourneyCaseId_MatchesComplianceCaseCreatedFirst()
        {
            string issuerId  = UniqueIssuer();
            string subjectId = UniqueSubject();

            // Create compliance case first
            string caseId = await CreateCaseAsync(issuerId, subjectId);

            // Start journey linked to that case
            var journeyResp = await StartJourneyAsync(subjectId, caseId: caseId);
            Assert.That(journeyResp.Journey!.CaseId, Is.EqualTo(caseId),
                "VJ02: Journey CaseId must match the pre-created compliance case");

            // Verify the compliance case still exists
            var caseResp = await _client.GetAsync($"{CasesBase}/{caseId}");
            caseResp.EnsureSuccessStatusCode();
            var caseResult = await caseResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();
            Assert.That(caseResult!.Case!.CaseId, Is.EqualTo(caseId),
                "VJ02: compliance case must still be retrievable");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ03: Verification journey evidence can be added to the linked compliance case
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ03_JourneyEvidence_CanBeAddedToLinkedComplianceCase()
        {
            string caseId    = await CreateCaseAsync();
            string subjectId = UniqueSubject();

            // Start journey
            var journeyResp = await StartJourneyAsync(subjectId, caseId: caseId);
            string journeyId = journeyResp.Journey!.JourneyId;

            // Add evidence to the compliance case referencing the journey
            var evidenceResp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "KYC",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "VerificationJourney",
                    ProviderReference = journeyId,
                    Summary           = $"Journey {journeyId} completed KYC check"
                });
            evidenceResp.EnsureSuccessStatusCode();
            var result = await evidenceResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();

            Assert.That(result!.Success, Is.True, "VJ03: adding evidence must succeed");
            Assert.That(
                result.Case!.EvidenceSummaries.Any(e => e.ProviderReference == journeyId),
                Is.True,
                "VJ03: evidence with journeyId as ProviderReference must appear in case");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ04: Journey for non-existent CaseId still succeeds (optional binding)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ04_JourneyWithNonExistentCaseId_StillSucceeds()
        {
            // The journey service doesn't validate that the CaseId actually exists
            var resp = await StartJourneyAsync(UniqueSubject(), caseId: "non-existent-case-vj04");
            Assert.That(resp.Success, Is.True,
                "VJ04: journey with non-existent CaseId must succeed (binding is optional)");
            Assert.That(resp.Journey!.CaseId, Is.EqualTo("non-existent-case-vj04"),
                "VJ04: Journey.CaseId must be stored as provided");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ05: Start journey without CaseId → CaseId null in record
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ05_StartJourney_WithoutCaseId_JourneyCaseIdIsNull()
        {
            var resp = await StartJourneyAsync(UniqueSubject(), caseId: null);
            Assert.That(resp.Success, Is.True, "VJ05: journey without CaseId must succeed");
            Assert.That(resp.Journey!.CaseId, Is.Null,
                "VJ05: Journey.CaseId must be null when not provided");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ06: Journey status endpoint returns CaseId in journey record
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ06_JourneyStatus_ReturnsCaseIdInJourneyRecord()
        {
            string caseId    = await CreateCaseAsync();
            string subjectId = UniqueSubject();

            var startResp = await StartJourneyAsync(subjectId, caseId: caseId);
            string journeyId = startResp.Journey!.JourneyId;

            var statusResp = await _client.GetAsync($"{JourneyBase}/{journeyId}");
            statusResp.EnsureSuccessStatusCode();
            var result = await statusResp.Content.ReadFromJsonAsync<GetVerificationJourneyStatusResponse>();

            Assert.That(result!.Success, Is.True, "VJ06: status query must succeed");
            Assert.That(result.Journey, Is.Not.Null, "VJ06: Journey must not be null");
            Assert.That(result.Journey!.CaseId, Is.EqualTo(caseId),
                "VJ06: status response must include the CaseId from journey creation");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ07: Two journeys for same subject with different CaseIds are independent
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ07_TwoJourneys_SameSubject_DifferentCaseIds_AreIndependent()
        {
            string subjectId = UniqueSubject();
            string caseId1   = await CreateCaseAsync(subjectId: subjectId);
            string caseId2   = await CreateCaseAsync(subjectId: subjectId + "-2");

            var j1 = await StartJourneyAsync(subjectId, caseId: caseId1);
            var j2 = await StartJourneyAsync(subjectId + "-2", caseId: caseId2);

            Assert.That(j1.Journey!.JourneyId, Is.Not.EqualTo(j2.Journey!.JourneyId),
                "VJ07: different subjects must produce separate journeys");
            Assert.That(j1.Journey.CaseId, Is.EqualTo(caseId1), "VJ07: first journey must link to first case");
            Assert.That(j2.Journey.CaseId, Is.EqualTo(caseId2), "VJ07: second journey must link to second case");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ08: Journey decision evaluation returns structured outcome
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ08_JourneyDecisionEvaluation_ReturnsStructuredOutcome()
        {
            var startResp = await StartJourneyAsync(UniqueSubject());
            string journeyId = startResp.Journey!.JourneyId;

            var evalResp = await _client.PostAsJsonAsync($"{JourneyBase}/{journeyId}/evaluate-decision",
                new { });
            evalResp.EnsureSuccessStatusCode();
            var result = await evalResp.Content.ReadFromJsonAsync<EvaluateApprovalDecisionResponse>();

            Assert.That(result, Is.Not.Null, "VJ08: evaluate-decision response must not be null");
            Assert.That(result!.Success, Is.True, "VJ08: evaluate-decision must succeed");
            Assert.That(result.Decision, Is.Not.Null, "VJ08: Decision must not be null");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ09: Generate release evidence for journey → EvidenceId non-null
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ09_GenerateReleaseEvidence_EvidenceIdPopulated()
        {
            var startResp = await StartJourneyAsync(UniqueSubject());
            string journeyId = startResp.Journey!.JourneyId;

            var evidenceResp = await _client.PostAsJsonAsync($"{JourneyBase}/{journeyId}/release-evidence",
                new GenerateVerificationJourneyEvidenceRequest { RequireProviderBacked = false });
            evidenceResp.EnsureSuccessStatusCode();
            var result = await evidenceResp.Content.ReadFromJsonAsync<GenerateVerificationJourneyEvidenceResponse>();

            Assert.That(result!.Success, Is.True, "VJ09: release evidence must succeed");
            Assert.That(result.Evidence, Is.Not.Null, "VJ09: Evidence must not be null");
            Assert.That(result.Evidence!.EvidenceId, Is.Not.Null.And.Not.Empty,
                "VJ09: EvidenceId must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ10: Adding journey evidence to compliance case improves evidence readiness
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ10_JourneyEvidence_AddedToCase_ImprovesEvidenceReadiness()
        {
            string caseId = await CreateCaseAsync();

            // Before: evidence availability should be Unavailable
            var beforeResp = await _client.GetAsync($"{CasesBase}/{caseId}/evidence-availability");
            beforeResp.EnsureSuccessStatusCode();
            var before = await beforeResp.Content.ReadFromJsonAsync<GetEvidenceAvailabilityResponse>();
            Assert.That(before!.Availability!.TotalEvidenceItems, Is.EqualTo(0),
                "VJ10: before adding evidence, TotalEvidenceItems must be 0");

            // Add KYC evidence from journey
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType = "KYC",
                    Status       = CaseEvidenceStatus.Valid,
                    ProviderName = "VerificationJourney",
                    Summary      = "KYC completed via verification journey"
                });

            // After: evidence availability must reflect the added item
            var afterResp = await _client.GetAsync($"{CasesBase}/{caseId}/evidence-availability");
            afterResp.EnsureSuccessStatusCode();
            var after = await afterResp.Content.ReadFromJsonAsync<GetEvidenceAvailabilityResponse>();
            Assert.That(after!.Availability!.TotalEvidenceItems, Is.GreaterThanOrEqualTo(1),
                "VJ10: after adding evidence, TotalEvidenceItems must be >= 1");
            Assert.That(after.Availability.ValidItems, Is.GreaterThanOrEqualTo(1),
                "VJ10: after adding valid evidence, ValidItems must be >= 1");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ11: Orchestration view of case with journey-linked evidence shows evidence status
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ11_OrchestrationView_WithJourneyLinkedEvidence_ShowsEvidenceStatus()
        {
            string caseId    = await CreateCaseAsync();
            string subjectId = UniqueSubject();
            var journeyResp  = await StartJourneyAsync(subjectId, caseId: caseId);
            string journeyId = journeyResp.Journey!.JourneyId;

            // Add evidence with journey reference
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "KYC",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "VerificationJourney",
                    ProviderReference = journeyId,
                    Summary           = "KYC check completed by verification journey"
                });
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "AML",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "VerificationJourney",
                    ProviderReference = journeyId,
                    Summary           = "AML screening clear"
                });

            // Get orchestration view
            var orchResp   = await _client.GetAsync($"{CasesBase}/{caseId}/orchestration-view");
            orchResp.EnsureSuccessStatusCode();
            var orchResult = await orchResp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();

            Assert.That(orchResult!.View!.EvidenceAvailability.TotalEvidenceItems, Is.GreaterThanOrEqualTo(2),
                "VJ11: orchestration view must reflect journey-linked evidence");
            Assert.That(orchResult.View.EvidenceAvailability.ValidItems, Is.GreaterThanOrEqualTo(2),
                "VJ11: both valid evidence items from journey must be reflected");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ12: Journey idempotency key reuse → same journey returned
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ12_IdempotencyKeyReuse_ReturnsSameJourney()
        {
            string subjectId      = UniqueSubject();
            string idempotencyKey = $"idem-vj12-{Guid.NewGuid():N}";

            var r1 = await StartJourneyAsync(subjectId, idempotencyKey: idempotencyKey);
            var r2 = await StartJourneyAsync(subjectId, idempotencyKey: idempotencyKey);

            Assert.That(r1.Journey!.JourneyId, Is.EqualTo(r2.Journey!.JourneyId),
                "VJ12: same idempotency key must return same JourneyId");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ13: Unauthenticated journey start → 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ13_UnauthenticatedJourneyStart_Returns401()
        {
            using var anonClient = _factory.CreateClient();
            var resp = await anonClient.PostAsJsonAsync(JourneyBase,
                new StartVerificationJourneyRequest { SubjectId = UniqueSubject() });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "VJ13: unauthenticated journey start must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ14: Empty SubjectId → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ14_EmptySubjectId_Returns400FailClosed()
        {
            var resp = await _client.PostAsJsonAsync(JourneyBase,
                new StartVerificationJourneyRequest { SubjectId = "" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "VJ14: empty SubjectId must return 400");
            var result = await resp.Content.ReadFromJsonAsync<StartVerificationJourneyResponse>();
            Assert.That(result!.Success, Is.False, "VJ14: Success must be false");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty, "VJ14: ErrorCode must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // VJ15: Full integration: create case → start journey → add evidence → orchestration
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task VJ15_FullIntegration_CaseJourneyEvidenceOrchestration()
        {
            string issuerId  = UniqueIssuer();
            string subjectId = UniqueSubject();

            // 1. Create compliance case
            string caseId = await CreateCaseAsync(issuerId, subjectId);

            // 2. Start verification journey linked to the case
            var journeyResp = await StartJourneyAsync(subjectId, caseId: caseId);
            Assert.That(journeyResp.Success, Is.True, "VJ15: journey start must succeed");
            string journeyId = journeyResp.Journey!.JourneyId;
            Assert.That(journeyResp.Journey.CaseId, Is.EqualTo(caseId), "VJ15: CaseId must match");

            // 3. Assign reviewer
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/assign",
                new AssignCaseRequest { ReviewerId = "reviewer-vj15", Reason = "VJ15 integration" });

            // 4. Add KYC + AML evidence from journey to the compliance case
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "KYC",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "VerificationJourney",
                    ProviderReference = journeyId,
                    Summary           = "KYC verified via journey"
                });
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType      = "AML",
                    Status            = CaseEvidenceStatus.Valid,
                    ProviderName      = "VerificationJourney",
                    ProviderReference = journeyId,
                    Summary           = "AML screening clear via journey"
                });

            // 5. Progress through lifecycle
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending });
            await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview });

            // 6. Get orchestration view — must reflect full operator-visible state
            var orchResp   = await _client.GetAsync($"{CasesBase}/{caseId}/orchestration-view");
            orchResp.EnsureSuccessStatusCode();
            var orchResult = await orchResp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();

            Assert.That(orchResult!.View!.State, Is.EqualTo(ComplianceCaseState.UnderReview),
                "VJ15: state must be UnderReview");
            Assert.That(orchResult.View.AssignedReviewerId, Is.EqualTo("reviewer-vj15"),
                "VJ15: AssignedReviewerId must be 'reviewer-vj15'");
            Assert.That(orchResult.View.EvidenceAvailability.ValidItems, Is.GreaterThanOrEqualTo(2),
                "VJ15: 2 journey-linked evidence items must be reflected");

            // 7. Evaluate-decision on the journey
            var evalResp   = await _client.PostAsJsonAsync(
                $"{JourneyBase}/{journeyId}/evaluate-decision", new { });
            evalResp.EnsureSuccessStatusCode();
            var evalResult = await evalResp.Content.ReadFromJsonAsync<EvaluateApprovalDecisionResponse>();
            Assert.That(evalResult!.Decision, Is.Not.Null, "VJ15: Decision must not be null");

            // 8. Approve the compliance case
            var approveResp = await _client.PostAsJsonAsync($"{CasesBase}/{caseId}/approve",
                new ApproveComplianceCaseRequest { Rationale = "Journey evidence validates subject" });
            approveResp.EnsureSuccessStatusCode();
            var approveResult = await approveResp.Content.ReadFromJsonAsync<ApproveComplianceCaseResponse>();
            Assert.That(approveResult!.Case!.State, Is.EqualTo(ComplianceCaseState.Approved),
                "VJ15: final state must be Approved");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static string UniqueIssuer()  => $"issuer-vj-{Guid.NewGuid():N}";
        private static string UniqueSubject() => $"subject-vj-{Guid.NewGuid():N}";

        private async Task<StartVerificationJourneyResponse> StartJourneyAsync(
            string subjectId,
            string? caseId         = null,
            string? idempotencyKey = null)
        {
            var resp = await _client.PostAsJsonAsync(JourneyBase,
                new StartVerificationJourneyRequest
                {
                    SubjectId            = subjectId,
                    CaseId               = caseId,
                    IdempotencyKey       = idempotencyKey,
                    RequestedExecutionMode = VerificationJourneyExecutionMode.Simulated,
                    RequireProviderBacked  = false
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<StartVerificationJourneyResponse>())!;
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
            var result = await resp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            return result!.Case!.CaseId;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"vjint-{userTag}@vj-integration.biatec.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = email,
                Password        = "VjIntegrationIT!Pass1",
                ConfirmPassword = "VjIntegrationIT!Pass1",
                FullName        = $"VJ Integration Test User ({userTag})"
            });

            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created),
                $"Registration must succeed for user '{userTag}'");

            var doc   = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? token = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            return token!;
        }
    }
}

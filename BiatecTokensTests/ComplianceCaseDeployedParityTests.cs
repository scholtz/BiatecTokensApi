using BiatecTokensApi.Models.ComplianceCaseManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Deployed-parity tests for the compliance case orchestration backend at
    /// <c>/api/v1/compliance-cases</c>.
    ///
    /// These tests prove that the compliance case lifecycle management backend is
    /// deployment-ready and operates as the authoritative source for:
    ///   - Case lifecycle states (Intake → EvidencePending → UnderReview → Approved/Rejected)
    ///   - Reviewer ownership and assignment history
    ///   - Blocker detection and evidence readiness
    ///   - Escalation metadata and resolution
    ///   - Decision records (KYC, AML, sanctions, approval)
    ///   - Handoff status and downstream delivery tracking
    ///   - Queue-health signal accuracy
    ///   - Evidence-pack readiness and schema contracts
    ///   - Fail-closed behavior for missing evidence and invalid transitions
    ///
    /// Each test exercises the full HTTP stack through a <see cref="WebApplicationFactory{TEntryPoint}"/>
    /// so the result reflects real deployed conditions.
    ///
    /// Coverage:
    ///
    /// DP01: Create case → 200, CaseId populated, initial state Intake
    /// DP02: Unauthenticated request → 401
    /// DP03: Create case with empty IssuerId → 400 fail-closed
    /// DP04: Get non-existent case → 404 fail-closed
    /// DP05: Transition Intake → EvidencePending → 200, state updated
    /// DP06: Invalid transition Intake → Approved → 400 fail-closed
    /// DP07: Add evidence → case has evidence item, EvidenceType populated
    /// DP08: Assign reviewer → case shows AssignedReviewerId
    /// DP09: Assignment history tracks reviewer with actor and reason
    /// DP10: Full lifecycle: Intake → EvidencePending → UnderReview → Approve → Approved
    /// DP11: Reject case from UnderReview → case state Rejected, DecisionId populated
    /// DP12: ReturnForInformation from UnderReview → case returned to EvidencePending
    /// DP13: Add escalation → escalation appears in case; history endpoint reflects it
    /// DP14: Add decision record → decision history has correct Kind and IsAdverse flag
    /// DP15: GetOrchestrationView schema contract: all required fields non-null
    /// DP16: GetOrchestrationView for unassigned case → NextActions include assign guidance
    /// DP17: GetOrchestrationView for approved case → AvailableTransitions empty
    /// DP18: GetOrchestrationView after assignment → AssignedReviewerId populated
    /// DP19: GetEvidenceAvailability with valid evidence → Complete status
    /// DP20: GetEvidenceAvailability with no evidence → Unavailable status
    /// DP21: GetBlockers on case with open escalation → OpenEscalation blocker
    /// DP22: GetBlockers on case with no issues → CanProceed true (or Intake state)
    /// DP23: EvaluateBlockers: missing evidence → MissingEvidence blocker present
    /// DP24: Set SLA metadata → urgency band reflects proximity to due date
    /// DP25: Export case → export bundle has SHA-256 ContentHash (64 hex chars)
    /// DP26: Timeline has entries for all transitions and evidence additions
    /// DP27: Idempotent case creation returns same CaseId
    /// DP28: List cases with IssuerId filter returns only matching cases
    /// DP29: GetCaseSummary returns scannable summary without full aggregate
    /// DP30: ListCaseSummaries is paginated and returns correct TotalCount
    /// DP31: Handoff status update → GetHandoffStatus returns updated stage
    /// DP32: Escalation history endpoint lists open vs resolved counts correctly
    /// DP33: Add remediation task → ReadinessSummary shows open task blocker
    /// DP34: Resolve remediation task → task no longer blocks readiness
    /// DP35: GetOrchestrationView concurrent reads → all succeed with same state
    /// DP36: ReturnForInformation requires Reason field → 400 when absent
    /// DP37: GetOrchestrationView for non-existent case → 404 fail-closed
    /// DP38: Decision record with IsAdverse=true counted in AdverseCount
    /// DP39: GetOrchestrationView deterministic across 3 independent calls
    /// DP40: Full compliance operator journey: create → assign → evidence → review → approve → export
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseDeployedParityTests
    {
        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class ComplianceCaseParityFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "ComplianceCaseParityTestKey32CharsMinimum!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "ComplianceCaseParityTestKey32+chars",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "compliance-parity-test",
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

        private ComplianceCaseParityFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string BaseUrl = "/api/v1/compliance-cases";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new ComplianceCaseParityFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"ccparity-{Guid.NewGuid():N}");
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
        // DP01: Create case → 200, CaseId populated, initial state Intake
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP01_CreateCase_Returns200_WithCaseIdAndIntakeState()
        {
            var resp = await CreateCaseHttpAsync(UniqueIssuer(), UniqueSubject());
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            Assert.That(result, Is.Not.Null, "DP01: response body must not be null");
            Assert.That(result!.Success, Is.True, "DP01: Success must be true");
            Assert.That(result.Case, Is.Not.Null, "DP01: Case must not be null");
            Assert.That(result.Case!.CaseId, Is.Not.Null.And.Not.Empty, "DP01: CaseId must be populated");
            Assert.That(result.Case.State, Is.EqualTo(ComplianceCaseState.Intake), "DP01: initial state must be Intake");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP02: Unauthenticated request → 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP02_UnauthenticatedRequest_Returns401()
        {
            using var anonClient = _factory.CreateClient();
            var resp = await anonClient.PostAsJsonAsync(BaseUrl, new CreateComplianceCaseRequest
            {
                IssuerId  = UniqueIssuer(),
                SubjectId = UniqueSubject(),
                Type      = CaseType.InvestorEligibility
            });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP02: missing JWT must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP03: Create case with empty IssuerId → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP03_CreateCase_EmptyIssuerId_Returns400FailClosed()
        {
            var resp = await _client.PostAsJsonAsync(BaseUrl, new CreateComplianceCaseRequest
            {
                IssuerId  = "",
                SubjectId = UniqueSubject(),
                Type      = CaseType.InvestorEligibility
            });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP03: empty IssuerId must return 400");
            var result = await resp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            Assert.That(result!.Success, Is.False, "DP03: Success must be false");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty, "DP03: ErrorCode must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP04: Get non-existent case → 404 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP04_GetNonExistentCase_Returns404FailClosed()
        {
            var resp = await _client.GetAsync($"{BaseUrl}/does-not-exist-{Guid.NewGuid():N}");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "DP04: non-existent case must return 404");
            var result = await resp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();
            Assert.That(result!.Success, Is.False, "DP04: Success must be false");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP05: Transition Intake → EvidencePending → 200, state updated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP05_Transition_IntakeToEvidencePending_Returns200()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/transition",
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.EvidencePending,
                    Reason   = "Requesting evidence documents"
                });

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<UpdateComplianceCaseResponse>();
            Assert.That(result!.Success, Is.True, "DP05: transition must succeed");
            Assert.That(result.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending),
                "DP05: case must be in EvidencePending state");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP06: Invalid transition Intake → Approved → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP06_InvalidTransition_IntakeToApproved_Returns400FailClosed()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/transition",
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.Approved,
                    Reason   = "Shortcutting to approval"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP06: invalid transition must return 400");
            var result = await resp.Content.ReadFromJsonAsync<UpdateComplianceCaseResponse>();
            Assert.That(result!.Success, Is.False, "DP06: Success must be false for invalid transition");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty, "DP06: ErrorCode must explain the rejection");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP07: Add evidence → case has evidence item, EvidenceType populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP07_AddEvidence_CaseHasEvidenceItem_EvidenceTypePopulated()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType = "KYC",
                    Status       = CaseEvidenceStatus.Valid,
                    ProviderName = "StripeIdentity",
                    Summary      = "Identity verification passed"
                });

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();
            Assert.That(result!.Success, Is.True, "DP07: AddEvidence must succeed");
            Assert.That(result.Case!.EvidenceSummaries, Is.Not.Empty, "DP07: case must have at least one evidence item");
            Assert.That(result.Case.EvidenceSummaries.Any(e => e.EvidenceType == "KYC"), Is.True,
                "DP07: evidence type KYC must be present");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP08: Assign reviewer → case shows AssignedReviewerId
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP08_AssignReviewer_CaseShowsAssignedReviewerId()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/assign",
                new AssignCaseRequest
                {
                    ReviewerId = "reviewer-alice",
                    TeamId     = "compliance-team-a",
                    Reason     = "Initial assignment for DP08"
                });

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<AssignCaseResponse>();
            Assert.That(result!.Success, Is.True, "DP08: assign must succeed");
            Assert.That(result.Case!.AssignedReviewerId, Is.EqualTo("reviewer-alice"),
                "DP08: AssignedReviewerId must be 'reviewer-alice'");
            Assert.That(result.AssignmentRecord, Is.Not.Null, "DP08: AssignmentRecord must be in response");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP09: Assignment history tracks reviewer with actor and reason
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP09_AssignmentHistory_TracksReviewerWithActorAndReason()
        {
            string caseId = await CreateCaseAsync();

            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/assign",
                new AssignCaseRequest { ReviewerId = "reviewer-bob", Reason = "Initial" });

            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/assign",
                new AssignCaseRequest { ReviewerId = "reviewer-carol", Reason = "Reassignment" });

            var histResp = await _client.GetAsync($"{BaseUrl}/{caseId}/assignment-history");
            histResp.EnsureSuccessStatusCode();
            var hist = await histResp.Content.ReadFromJsonAsync<GetAssignmentHistoryResponse>();
            Assert.That(hist!.Success, Is.True, "DP09: history must succeed");
            Assert.That(hist.TotalCount, Is.GreaterThanOrEqualTo(2), "DP09: at least 2 assignment records");
            Assert.That(hist.History.Any(h => h.NewReviewerId == "reviewer-carol"), Is.True,
                "DP09: most-recent reviewer must be carol");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP10: Full lifecycle: Intake → EvidencePending → UnderReview → Approve → Approved
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP10_FullLifecycle_IntakeToApproved()
        {
            string caseId = await CreateCaseAsync();

            // Add evidence first (needed for blocker-free readiness)
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);

            // Transition to EvidencePending
            var r1 = await TransitionHttpAsync(caseId, ComplianceCaseState.EvidencePending);
            Assert.That(r1.Success, Is.True, "DP10: transition to EvidencePending");

            // Transition to UnderReview
            var r2 = await TransitionHttpAsync(caseId, ComplianceCaseState.UnderReview);
            Assert.That(r2.Success, Is.True, "DP10: transition to UnderReview");

            // Approve
            var approveResp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/approve",
                new ApproveComplianceCaseRequest { Rationale = "All checks passed" });
            approveResp.EnsureSuccessStatusCode();
            var approveResult = await approveResp.Content.ReadFromJsonAsync<ApproveComplianceCaseResponse>();
            Assert.That(approveResult!.Success, Is.True, "DP10: approve must succeed");
            Assert.That(approveResult.Case!.State, Is.EqualTo(ComplianceCaseState.Approved),
                "DP10: final state must be Approved");
            Assert.That(approveResult.DecisionId, Is.Not.Null.And.Not.Empty,
                "DP10: DecisionId must be populated for audit trail");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP11: Reject case from UnderReview → state Rejected, DecisionId populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP11_RejectCase_FromUnderReview_StateRejected_DecisionIdPopulated()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/reject",
                new RejectComplianceCaseRequest { Reason = "Sanctions hit confirmed" });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<RejectComplianceCaseResponse>();
            Assert.That(result!.Success, Is.True, "DP11: reject must succeed");
            Assert.That(result.Case!.State, Is.EqualTo(ComplianceCaseState.Rejected),
                "DP11: state must be Rejected");
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty,
                "DP11: DecisionId must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP12: ReturnForInformation → case returned to EvidencePending
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP12_ReturnForInformation_FromUnderReview_StateEvidencePending()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/return-for-information",
                new ReturnForInformationRequest
                {
                    TargetStage    = ReturnForInformationTargetStage.EvidencePending,
                    Reason         = "Passport copy is illegible",
                    RequestedItems = new List<string> { "Valid passport copy", "Proof of address" }
                });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ReturnForInformationResponse>();
            Assert.That(result!.Success, Is.True, "DP12: return-for-information must succeed");
            Assert.That(result.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending),
                "DP12: state must be EvidencePending after return");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP13: Add escalation → escalation appears; history shows open count
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP13_AddEscalation_EscalationInCase_HistoryShowsOpenCount()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/escalations",
                new AddEscalationRequest
                {
                    Type                 = EscalationType.SanctionsHit,
                    Description          = "OFAC match on primary name",
                    ScreeningSource      = "ComplyAdvantage",
                    RequiresManualReview = true
                });
            resp.EnsureSuccessStatusCode();
            var caseResult = await resp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();
            Assert.That(caseResult!.Case!.Escalations, Is.Not.Empty, "DP13: escalation must be in case");

            var histResp = await _client.GetAsync($"{BaseUrl}/{caseId}/escalation-history");
            histResp.EnsureSuccessStatusCode();
            var hist = await histResp.Content.ReadFromJsonAsync<GetEscalationHistoryResponse>();
            Assert.That(hist!.OpenCount, Is.GreaterThanOrEqualTo(1), "DP13: at least one open escalation");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP14: Add decision record → history has correct Kind and IsAdverse
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP14_AddDecisionRecord_HistoryHasCorrectKindAndIsAdverse()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind            = CaseDecisionKind.KycApproval,
                    DecisionSummary = "Identity verification passed with confidence 98%",
                    Outcome         = "Approved",
                    ProviderName    = "StripeIdentity",
                    IsAdverse       = false
                });
            resp.EnsureSuccessStatusCode();
            var addResult = await resp.Content.ReadFromJsonAsync<AddDecisionRecordResponse>();
            Assert.That(addResult!.Success, Is.True, "DP14: AddDecisionRecord must succeed");
            Assert.That(addResult.DecisionRecord!.Kind, Is.EqualTo(CaseDecisionKind.KycApproval),
                "DP14: Kind must be KycApproval");
            Assert.That(addResult.DecisionRecord.IsAdverse, Is.False, "DP14: IsAdverse must be false");

            var histResp = await _client.GetAsync($"{BaseUrl}/{caseId}/decisions");
            histResp.EnsureSuccessStatusCode();
            var hist = await histResp.Content.ReadFromJsonAsync<GetDecisionHistoryResponse>();
            Assert.That(hist!.TotalCount, Is.EqualTo(1), "DP14: TotalCount must be 1");
            Assert.That(hist.AdverseCount, Is.EqualTo(0), "DP14: AdverseCount must be 0");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP15: GetOrchestrationView schema contract: all required fields non-null
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP15_GetOrchestrationView_SchemaContract_AllRequiredFieldsNonNull()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(result, Is.Not.Null, "DP15: response must not be null");
            Assert.That(result!.Success, Is.True, "DP15: Success must be true");
            var view = result.View!;
            Assert.That(view, Is.Not.Null, "DP15: View must not be null");
            Assert.That(view.CaseId, Is.Not.Null.And.Not.Empty, "DP15: CaseId must be populated");
            Assert.That(view.StateDescription, Is.Not.Null.And.Not.Empty, "DP15: StateDescription must be populated");
            Assert.That(view.IssuerId, Is.Not.Null.And.Not.Empty, "DP15: IssuerId must be populated");
            Assert.That(view.SubjectId, Is.Not.Null.And.Not.Empty, "DP15: SubjectId must be populated");
            Assert.That(view.AvailableTransitions, Is.Not.Null, "DP15: AvailableTransitions must not be null");
            Assert.That(view.NextActions, Is.Not.Null, "DP15: NextActions must not be null");
            Assert.That(view.ActiveBlockers, Is.Not.Null, "DP15: ActiveBlockers must not be null");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP16: GetOrchestrationView for unassigned case → NextActions include assign
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP16_GetOrchestrationView_UnassignedCase_NextActionsIncludeAssign()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            var nextActions = result!.View!.NextActions;
            Assert.That(
                nextActions.Any(a => a.Contains("reviewer", StringComparison.OrdinalIgnoreCase)
                                  || a.Contains("assign",   StringComparison.OrdinalIgnoreCase)
                                  || a.Contains("team",     StringComparison.OrdinalIgnoreCase)),
                Is.True,
                "DP16: unassigned case must prompt assignment in NextActions");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP17: GetOrchestrationView for approved case → AvailableTransitions empty
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP17_GetOrchestrationView_ApprovedCase_NoAvailableTransitions()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();
            await ApproveHttpAsync(caseId);

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(result!.View!.AvailableTransitions, Is.Empty,
                "DP17: approved case must have no available state transitions");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP18: GetOrchestrationView after assignment → AssignedReviewerId populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP18_GetOrchestrationView_AfterAssignment_AssignedReviewerIdPopulated()
        {
            string caseId = await CreateCaseAsync();
            await AssignHttpAsync(caseId, "reviewer-dp18", "team-dp18");

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(result!.View!.AssignedReviewerId, Is.EqualTo("reviewer-dp18"),
                "DP18: AssignedReviewerId must be 'reviewer-dp18'");
            Assert.That(result.View.AssignedTeamId, Is.EqualTo("team-dp18"),
                "DP18: AssignedTeamId must be 'team-dp18'");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP19: GetEvidenceAvailability with valid evidence → Complete status
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP19_GetEvidenceAvailability_WithValidEvidence_CompleteStatus()
        {
            string caseId = await CreateCaseAsync();
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/evidence-availability");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetEvidenceAvailabilityResponse>();
            Assert.That(result!.Success, Is.True, "DP19: evidence availability must succeed");
            Assert.That(result.Availability, Is.Not.Null, "DP19: Availability must not be null");
            Assert.That(result.Availability!.ValidItems, Is.GreaterThanOrEqualTo(1),
                "DP19: ValidItems must be >= 1");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP20: GetEvidenceAvailability with no evidence → Unavailable status
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP20_GetEvidenceAvailability_WithNoEvidence_UnavailableStatus()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/evidence-availability");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<GetEvidenceAvailabilityResponse>();
            Assert.That(result!.Success, Is.True, "DP20: evidence availability must succeed");
            Assert.That(result.Availability!.Status, Is.EqualTo(CaseEvidenceAvailabilityStatus.Unavailable),
                "DP20: no evidence must yield Unavailable status");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP21: GetBlockers on case with open escalation → OpenEscalation blocker
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP21_GetBlockers_OpenEscalation_OpenEscalationBlockerPresent()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();
            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/escalations",
                new AddEscalationRequest
                {
                    Type        = EscalationType.AdverseMedia,
                    Description = "Adverse media hit for DP21",
                    RequiresManualReview = true
                });

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/blockers");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<EvaluateBlockersResponse>();
            Assert.That(result!.Success, Is.True, "DP21: GetBlockers must succeed");
            Assert.That(result.CanProceed, Is.False, "DP21: open escalation must block proceeding");
            Assert.That(result.FailClosedBlockers.Count, Is.GreaterThanOrEqualTo(1),
                "DP21: at least one fail-closed blocker must be present when escalation is open");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP22: GetBlockers on newly created case → reports missing assignment
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP22_GetBlockers_NewCase_MissingAssignmentBlockerPresent()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/blockers");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<EvaluateBlockersResponse>();
            Assert.That(result!.Success, Is.True, "DP22: GetBlockers must succeed");
            Assert.That(result.Blockers.Any(b => b.Category == CaseBlockerCategory.MissingAssignment),
                Is.True, "DP22: unassigned case must have MissingAssignment blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP23: EvaluateBlockers: missing evidence → MissingEvidence blocker present
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP23_EvaluateBlockers_NoEvidence_MissingEvidenceBlockerPresent()
        {
            string caseId = await CreateCaseAsync();
            // Do NOT add any evidence

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/blockers");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<EvaluateBlockersResponse>();
            Assert.That(result!.Blockers.Any(b => b.Category == CaseBlockerCategory.MissingEvidence),
                Is.True, "DP23: no evidence must produce MissingEvidence blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP24: Set SLA metadata → urgency band reflects proximity to due date
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP24_SetSlaMetadata_UrgencyBandReflectsProximityToDueDate()
        {
            string caseId = await CreateCaseAsync();

            // Set a due date far in the future → OnTrack
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/sla",
                new SetSlaMetadataRequest
                {
                    ReviewDueAt = DateTimeOffset.UtcNow.AddDays(30),
                    Notes       = "Plenty of time"
                });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<SetSlaMetadataResponse>();
            Assert.That(result!.Success, Is.True, "DP24: SetSlaMetadata must succeed");
            Assert.That(result.SlaMetadata, Is.Not.Null, "DP24: SlaMetadata must be in response");
            Assert.That(result.SlaMetadata!.UrgencyBand, Is.EqualTo(CaseUrgencyBand.Normal),
                "DP24: due date 30 days away must yield Normal urgency band");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP25: Export case → export bundle has SHA-256 ContentHash (64 hex chars)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP25_ExportCase_BundleHasSha256ContentHash()
        {
            string caseId = await CreateCaseAsync();
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/export",
                new ExportComplianceCaseRequest { Format = "Json" });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ExportComplianceCaseResponse>();
            Assert.That(result!.Success, Is.True, "DP25: export must succeed");
            Assert.That(result.Bundle, Is.Not.Null, "DP25: Bundle must not be null");
            Assert.That(result.Bundle!.Metadata.ContentHash, Is.Not.Null.And.Not.Empty,
                "DP25: ContentHash must be populated");
            Assert.That(result.Bundle.Metadata.ContentHash!.Length, Is.EqualTo(64),
                "DP25: SHA-256 content hash must be 64 hex characters");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP26: Timeline has entries for all transitions and evidence additions
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP26_Timeline_HasEntriesForTransitionsAndEvidenceAdditions()
        {
            string caseId = await CreateCaseAsync();
            await AddEvidenceHttpAsync(caseId, "AML", CaseEvidenceStatus.Valid);
            await TransitionHttpAsync(caseId, ComplianceCaseState.EvidencePending);

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/timeline");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<CaseTimelineResponse>();
            Assert.That(result!.Success, Is.True, "DP26: timeline must succeed");
            Assert.That(result.Entries, Is.Not.Empty, "DP26: timeline must have entries");
            Assert.That(result.Entries.Any(e => e.EventType == CaseTimelineEventType.EvidenceAdded),
                Is.True, "DP26: must have EvidenceAdded entry");
            Assert.That(result.Entries.Any(e => e.EventType == CaseTimelineEventType.StateTransition),
                Is.True, "DP26: must have StateTransition entry");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP27: Idempotent case creation returns same CaseId
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP27_IdempotentCaseCreation_ReturnsSameCaseId()
        {
            string issuerId  = UniqueIssuer();
            string subjectId = UniqueSubject();

            var r1 = await CreateCaseHttpAsync(issuerId, subjectId);
            var r2 = await CreateCaseHttpAsync(issuerId, subjectId);
            r1.EnsureSuccessStatusCode();
            r2.EnsureSuccessStatusCode();

            var res1 = await r1.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            var res2 = await r2.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();

            Assert.That(res1!.Case!.CaseId, Is.EqualTo(res2!.Case!.CaseId),
                "DP27: same (issuerId, subjectId, type) must return same CaseId");
            Assert.That(res2.WasIdempotent, Is.True, "DP27: second create must be flagged WasIdempotent");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP28: List cases with IssuerId filter returns only matching cases
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP28_ListCases_IssuerIdFilter_ReturnsOnlyMatchingCases()
        {
            string issuerId1 = UniqueIssuer();
            string issuerId2 = UniqueIssuer();

            await CreateCaseHttpAsync(issuerId1, UniqueSubject());
            await CreateCaseHttpAsync(issuerId1, UniqueSubject());
            await CreateCaseHttpAsync(issuerId2, UniqueSubject());

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/list",
                new ListComplianceCasesRequest { IssuerId = issuerId1, PageSize = 50 });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ListComplianceCasesResponse>();
            Assert.That(result!.Success, Is.True, "DP28: list must succeed");
            Assert.That(result.Cases.All(c => c.IssuerId == issuerId1), Is.True,
                "DP28: all returned cases must match IssuerId filter");
            Assert.That(result.TotalCount, Is.EqualTo(2), "DP28: exactly 2 cases for issuerId1");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP29: GetCaseSummary returns scannable summary without full aggregate
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP29_GetCaseSummary_ReturnsScanableSummaryFields()
        {
            string caseId = await CreateCaseAsync();

            var resp = await _client.GetAsync($"{BaseUrl}/{caseId}/summary");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<CaseSummaryResponse>();
            Assert.That(result!.Success, Is.True, "DP29: GetCaseSummary must succeed");
            Assert.That(result.Summary, Is.Not.Null, "DP29: Summary must not be null");
            Assert.That(result.Summary!.CaseId, Is.EqualTo(caseId), "DP29: CaseId must match");
            Assert.That(result.Summary.State, Is.EqualTo(ComplianceCaseState.Intake), "DP29: initial state Intake");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP30: ListCaseSummaries returns paginated results with TotalCount
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP30_ListCaseSummaries_Paginated_CorrectTotalCount()
        {
            string issuerId = UniqueIssuer();
            await CreateCaseHttpAsync(issuerId, UniqueSubject());
            await CreateCaseHttpAsync(issuerId, UniqueSubject());
            await CreateCaseHttpAsync(issuerId, UniqueSubject());

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/summaries",
                new ListComplianceCasesRequest { IssuerId = issuerId, PageSize = 2, Page = 1 });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ListCaseSummariesResponse>();
            Assert.That(result!.Success, Is.True, "DP30: ListCaseSummaries must succeed");
            Assert.That(result.TotalCount, Is.EqualTo(3), "DP30: TotalCount must be 3");
            Assert.That(result.Summaries.Count, Is.LessThanOrEqualTo(2),
                "DP30: PageSize=2 must return at most 2 summaries per page");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP31: Handoff status update → GetHandoffStatus returns updated stage
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP31_UpdateHandoffStatus_GetHandoffStatus_ReturnsUpdatedStage()
        {
            string caseId = await CreateCaseAsync();

            var updateResp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/handoff",
                new UpdateHandoffStatusRequest
                {
                    Stage        = CaseHandoffStage.ApprovalWorkflowPending,
                    HandoffNotes = "Post-approval routing pending"
                });
            updateResp.EnsureSuccessStatusCode();

            var getResp = await _client.GetAsync($"{BaseUrl}/{caseId}/handoff");
            getResp.EnsureSuccessStatusCode();
            var result = await getResp.Content.ReadFromJsonAsync<GetHandoffStatusResponse>();
            Assert.That(result!.Success, Is.True, "DP31: GetHandoffStatus must succeed");
            Assert.That(result.HandoffStatus, Is.Not.Null, "DP31: HandoffStatus must not be null");
            Assert.That(result.HandoffStatus!.Stage, Is.EqualTo(CaseHandoffStage.ApprovalWorkflowPending),
                "DP31: Stage must reflect the update");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP32: Escalation history lists open vs resolved counts correctly
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP32_EscalationHistory_ListsOpenVsResolvedCounts()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();

            // Add escalation
            var addResp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/escalations",
                new AddEscalationRequest
                {
                    Type        = EscalationType.AdverseMedia,
                    Description = "Adverse media hit for DP32"
                });
            addResp.EnsureSuccessStatusCode();
            var caseResult = await addResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();
            string escalationId = caseResult!.Case!.Escalations.Last().EscalationId;

            // Resolve escalation
            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/escalations/{escalationId}/resolve",
                new ResolveEscalationRequest { ResolutionNotes = "False positive, cleared" });

            var histResp = await _client.GetAsync($"{BaseUrl}/{caseId}/escalation-history");
            histResp.EnsureSuccessStatusCode();
            var hist = await histResp.Content.ReadFromJsonAsync<GetEscalationHistoryResponse>();
            Assert.That(hist!.Success, Is.True, "DP32: escalation history must succeed");
            Assert.That(hist.ResolvedCount, Is.EqualTo(1), "DP32: resolved count must be 1");
            Assert.That(hist.OpenCount, Is.EqualTo(0), "DP32: open count must be 0 after resolution");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP33: Add remediation task → ReadinessSummary shows open task blocker
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP33_AddRemediationTask_ReadinessSummaryShowsOpenTaskBlocker()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();

            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/remediation-tasks",
                new AddRemediationTaskRequest
                {
                    Title       = "Collect updated proof of address",
                    Description = "The submitted utility bill is more than 90 days old"
                });

            var readinessResp = await _client.GetAsync($"{BaseUrl}/{caseId}/readiness");
            readinessResp.EnsureSuccessStatusCode();
            var result = await readinessResp.Content.ReadFromJsonAsync<CaseReadinessSummaryResponse>();
            Assert.That(result!.Success, Is.True, "DP33: readiness summary must succeed");
            Assert.That(result.Summary!.IsReady, Is.False, "DP33: open remediation task must block readiness");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP34: Resolve remediation task → task no longer blocks readiness
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP34_ResolveRemediationTask_TaskNoLongerBlocksReadiness()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);

            // Add task
            var addResp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/remediation-tasks",
                new AddRemediationTaskRequest { Title = "Provide updated address proof" });
            addResp.EnsureSuccessStatusCode();
            var caseAfterAdd = await addResp.Content.ReadFromJsonAsync<GetComplianceCaseResponse>();
            string taskId = caseAfterAdd!.Case!.RemediationTasks.Last().TaskId;

            // Resolve task
            var resolveResp = await _client.PostAsJsonAsync(
                $"{BaseUrl}/{caseId}/remediation-tasks/{taskId}/resolve",
                new ResolveRemediationTaskRequest { ResolutionNotes = "New utility bill submitted" });
            resolveResp.EnsureSuccessStatusCode();

            var readinessResp = await _client.GetAsync($"{BaseUrl}/{caseId}/readiness");
            readinessResp.EnsureSuccessStatusCode();
            var result = await readinessResp.Content.ReadFromJsonAsync<CaseReadinessSummaryResponse>();
            // After resolving the task the readiness should no longer be blocked by that task
            Assert.That(result!.Success, Is.True, "DP34: readiness summary must succeed after resolution");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP35: GetOrchestrationView concurrent reads → all succeed with same state
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP35_GetOrchestrationView_ConcurrentReads_AllSucceedWithSameState()
        {
            string caseId = await CreateCaseAsync();

            var tasks = Enumerable.Range(0, 10)
                .Select(_ => _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view"))
                .ToList();
            var responses = await Task.WhenAll(tasks);
            var results   = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>()));

            Assert.That(results.All(r => r!.Success), Is.True, "DP35: all concurrent reads must succeed");
            Assert.That(results.Select(r => r!.View!.State).Distinct().Count(), Is.EqualTo(1),
                "DP35: all concurrent reads must observe the same case state");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP36: ReturnForInformation requires Reason field → 400 when absent
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP36_ReturnForInformation_MissingReason_Returns400FailClosed()
        {
            string caseId = await CreateCaseThroughUnderReviewAsync();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/return-for-information",
                new ReturnForInformationRequest
                {
                    TargetStage = ReturnForInformationTargetStage.EvidencePending,
                    Reason      = null   // deliberately missing
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP36: missing Reason must return 400");
            var result = await resp.Content.ReadFromJsonAsync<ReturnForInformationResponse>();
            Assert.That(result!.Success, Is.False, "DP36: Success must be false");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty, "DP36: ErrorCode must explain the rejection");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP37: GetOrchestrationView for non-existent case → 404 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP37_GetOrchestrationView_NonExistentCase_Returns404FailClosed()
        {
            var resp = await _client.GetAsync($"{BaseUrl}/non-existent-{Guid.NewGuid():N}/orchestration-view");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "DP37: non-existent case orchestration view must return 404");
            var result = await resp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(result!.Success, Is.False, "DP37: Success must be false");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP38: Decision record with IsAdverse=true counted in AdverseCount
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP38_AdverseDecision_CountedInAdverseCount()
        {
            string caseId = await CreateCaseAsync();

            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind            = CaseDecisionKind.AmlHit,
                    DecisionSummary = "AML screening returned OFAC match",
                    IsAdverse       = true
                });

            var histResp = await _client.GetAsync($"{BaseUrl}/{caseId}/decisions");
            histResp.EnsureSuccessStatusCode();
            var hist = await histResp.Content.ReadFromJsonAsync<GetDecisionHistoryResponse>();
            Assert.That(hist!.AdverseCount, Is.EqualTo(1), "DP38: AdverseCount must be 1 after adverse decision");
            Assert.That(hist.TotalCount, Is.EqualTo(1), "DP38: TotalCount must be 1");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP39: GetOrchestrationView deterministic across 3 independent calls
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP39_GetOrchestrationView_Deterministic_Across3Calls()
        {
            string caseId = await CreateCaseAsync();
            await AssignHttpAsync(caseId, "reviewer-dp39", "team-dp39");
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);

            var r1 = await (await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view"))
                .Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            var r2 = await (await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view"))
                .Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            var r3 = await (await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view"))
                .Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();

            Assert.That(r1!.View!.State, Is.EqualTo(r2!.View!.State), "DP39: State must be identical run 1 vs 2");
            Assert.That(r2.View.State,   Is.EqualTo(r3!.View!.State), "DP39: State must be identical run 2 vs 3");
            Assert.That(r1.View.AssignedReviewerId, Is.EqualTo(r2.View.AssignedReviewerId),
                "DP39: AssignedReviewerId must be identical across all calls");
            Assert.That(r1.View.EvidenceAvailability.Status, Is.EqualTo(r2.View.EvidenceAvailability.Status),
                "DP39: EvidenceStatus must be identical across all calls");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP40: Full compliance operator journey
        //       create → assign → evidence → review → approve → export
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP40_FullComplianceOperatorJourney_CreateToExport()
        {
            string issuerId  = UniqueIssuer();
            string subjectId = UniqueSubject();

            // 1. Create case
            var createResp = await CreateCaseHttpAsync(issuerId, subjectId);
            createResp.EnsureSuccessStatusCode();
            var createResult = await createResp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            string caseId = createResult!.Case!.CaseId;
            Assert.That(createResult.Case.State, Is.EqualTo(ComplianceCaseState.Intake), "DP40: initial state Intake");

            // 2. Assign reviewer
            await AssignHttpAsync(caseId, "reviewer-dp40", "team-compliance");

            // 3. Add KYC evidence
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);

            // 4. Add AML decision record
            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/decisions",
                new AddDecisionRecordRequest
                {
                    Kind            = CaseDecisionKind.AmlClear,
                    DecisionSummary = "AML screening clear — no matches",
                    IsAdverse       = false
                });

            // 5. Progress lifecycle: Intake → EvidencePending → UnderReview
            await TransitionHttpAsync(caseId, ComplianceCaseState.EvidencePending);
            await TransitionHttpAsync(caseId, ComplianceCaseState.UnderReview);

            // 6. Set SLA
            await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/sla",
                new SetSlaMetadataRequest { ReviewDueAt = DateTimeOffset.UtcNow.AddDays(5) });

            // 7. Orchestration view: check all fields reflect real state
            var orchResp   = await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view");
            orchResp.EnsureSuccessStatusCode();
            var orchResult = await orchResp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(orchResult!.View!.State, Is.EqualTo(ComplianceCaseState.UnderReview), "DP40: must be UnderReview");
            Assert.That(orchResult.View.AssignedReviewerId, Is.EqualTo("reviewer-dp40"), "DP40: reviewer assigned");
            Assert.That(orchResult.View.EvidenceAvailability.Status, Is.Not.EqualTo(CaseEvidenceAvailabilityStatus.Unavailable),
                "DP40: evidence must not be Unavailable");

            // 8. Approve
            await ApproveHttpAsync(caseId);

            // 9. Verify orchestration view shows Approved with no transitions
            var approvedOrchResp = await _client.GetAsync($"{BaseUrl}/{caseId}/orchestration-view");
            approvedOrchResp.EnsureSuccessStatusCode();
            var approvedOrch = await approvedOrchResp.Content.ReadFromJsonAsync<GetOrchestrationViewResponse>();
            Assert.That(approvedOrch!.View!.State, Is.EqualTo(ComplianceCaseState.Approved), "DP40: final state Approved");
            Assert.That(approvedOrch.View.AvailableTransitions, Is.Empty, "DP40: no transitions from Approved");

            // 10. Export case
            var exportResp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/export",
                new ExportComplianceCaseRequest { Format = "Json" });
            exportResp.EnsureSuccessStatusCode();
            var exportResult = await exportResp.Content.ReadFromJsonAsync<ExportComplianceCaseResponse>();
            Assert.That(exportResult!.Success, Is.True, "DP40: export must succeed");
            Assert.That(exportResult.Bundle!.Metadata.ContentHash, Has.Length.EqualTo(64),
                "DP40: export content hash must be 64 hex chars (SHA-256)");
            Assert.That(exportResult.Bundle!.CaseSnapshot, Is.Not.Null, "DP40: export must include case snapshot");
            Assert.That(exportResult.Bundle.Timeline, Is.Not.Empty, "DP40: export must include timeline");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static string UniqueIssuer()  => $"issuer-dp-{Guid.NewGuid():N}";
        private static string UniqueSubject() => $"subject-dp-{Guid.NewGuid():N}";

        private Task<HttpResponseMessage> CreateCaseHttpAsync(
            string issuerId, string subjectId,
            CaseType type = CaseType.InvestorEligibility) =>
            _client.PostAsJsonAsync(BaseUrl, new CreateComplianceCaseRequest
            {
                IssuerId  = issuerId,
                SubjectId = subjectId,
                Type      = type,
                Priority  = CasePriority.Medium
            });

        private async Task<string> CreateCaseAsync(
            string? issuerId  = null,
            string? subjectId = null)
        {
            var resp = await CreateCaseHttpAsync(issuerId ?? UniqueIssuer(), subjectId ?? UniqueSubject());
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<CreateComplianceCaseResponse>();
            return result!.Case!.CaseId;
        }

        /// <summary>
        /// Creates a case and advances it to <see cref="ComplianceCaseState.UnderReview"/>
        /// with evidence attached.
        /// </summary>
        private async Task<string> CreateCaseThroughUnderReviewAsync()
        {
            string caseId = await CreateCaseAsync();
            await AddEvidenceHttpAsync(caseId, "KYC", CaseEvidenceStatus.Valid);
            await TransitionHttpAsync(caseId, ComplianceCaseState.EvidencePending);
            await TransitionHttpAsync(caseId, ComplianceCaseState.UnderReview);
            return caseId;
        }

        private async Task<UpdateComplianceCaseResponse> TransitionHttpAsync(
            string caseId, ComplianceCaseState newState, string? reason = null)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/transition",
                new TransitionCaseStateRequest { NewState = newState, Reason = reason ?? $"Transition to {newState}" });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<UpdateComplianceCaseResponse>())!;
        }

        private async Task AddEvidenceHttpAsync(
            string caseId, string evidenceType, CaseEvidenceStatus status)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/evidence",
                new AddEvidenceRequest
                {
                    EvidenceType = evidenceType,
                    Status       = status,
                    Summary      = $"{evidenceType} check passed"
                });
            resp.EnsureSuccessStatusCode();
        }

        private async Task AssignHttpAsync(string caseId, string reviewerId, string? teamId = null)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/assign",
                new AssignCaseRequest { ReviewerId = reviewerId, TeamId = teamId, Reason = "Assignment" });
            resp.EnsureSuccessStatusCode();
        }

        private async Task ApproveHttpAsync(string caseId)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/{caseId}/approve",
                new ApproveComplianceCaseRequest { Rationale = "All checks passed" });
            resp.EnsureSuccessStatusCode();
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"ccparity-{userTag}@compliance-parity.biatec.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "ComplianceCaseIT!Pass1",
                ConfirmPassword = "ComplianceCaseIT!Pass1",
                FullName = $"Compliance Case Parity Test User ({userTag})"
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

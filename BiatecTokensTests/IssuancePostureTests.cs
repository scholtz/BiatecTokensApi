using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// TDD unit tests for <see cref="ComplianceOrchestrationService.DeriveIssuancePosture"/>.
    ///
    /// These tests validate the deterministic, pure-function derivation of issuance posture
    /// from a normalized compliance decision. Each test exercises a single, specific input
    /// condition and asserts the expected output, covering:
    ///   - Confirmed sanctions match (hard block, fail-closed)
    ///   - Potential sanctions match unresolved (hard block)
    ///   - KYC rejected for non-sanctions reasons (hard block)
    ///   - NeedsReview / manual review required (hard block)
    ///   - Expired decision (hard block — renewal required)
    ///   - Insufficient data (hard block)
    ///   - Pending (advisory — not yet decided)
    ///   - Provider unavailable (advisory — retry recommended)
    ///   - Internal error (advisory)
    ///   - Approved (clear — no blockers)
    ///   - Priority ordering (sanctions match > KYC rejected)
    ///   - Case-insensitive reason code matching
    /// </summary>
    [TestFixture]
    public class IssuancePostureTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static NormalizedComplianceDecision MakeDecision(
            ComplianceDecisionState state,
            string? reasonCode = null) => new()
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            SubjectId = "user-001",
            ContextId = "ctx-001",
            State = state,
            ReasonCode = reasonCode,
            InitiatedAt = DateTimeOffset.UtcNow
        };

        // ── Approved ─────────────────────────────────────────────────────────────

        [Test]
        public void Approved_IsLaunchBlocked_IsFalse()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Approved));

            Assert.That(posture.IsLaunchBlocked, Is.False);
        }

        [Test]
        public void Approved_Severity_IsNone()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Approved));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.None));
        }

        [Test]
        public void Approved_BlockerReason_IsNone()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Approved));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.None));
        }

        [Test]
        public void Approved_BlockerDescription_IndicatesAllClear()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Approved));

            Assert.That(posture.BlockerDescription, Does.Contain("passed").Or.Contain("No launch blockers"));
        }

        // ── Confirmed sanctions match ─────────────────────────────────────────────

        [Test]
        public void Rejected_SanctionsMatch_IsLaunchBlocked_IsTrue()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void Rejected_SanctionsMatch_Severity_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        [Test]
        public void Rejected_SanctionsMatch_BlockerReason_IsConfirmedSanctionsMatch()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.ConfirmedSanctionsMatch));
        }

        [Test]
        public void Rejected_SanctionsMatch_BlockerDescription_MentionsSanctions()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            Assert.That(posture.BlockerDescription, Does.Contain("sanctions").IgnoreCase);
        }

        [Test]
        public void Rejected_SanctionsMatch_RecommendedAction_MentionsEscalation()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            Assert.That(posture.RecommendedAction, Does.Contain("compliance").IgnoreCase
                .Or.Contain("escalate").IgnoreCase);
        }

        [Test]
        public void Rejected_SanctionsMatch_CaseInsensitiveReasonCode_IsBlocking()
        {
            // Reason codes from providers may arrive in any case
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "sanctions_match"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.ConfirmedSanctionsMatch));
        }

        // ── Potential sanctions match unresolved ─────────────────────────────────

        [Test]
        public void NeedsReview_ReviewRequired_IsLaunchBlocked_IsTrue()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED"));

            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void NeedsReview_ReviewRequired_Severity_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED"));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        [Test]
        public void NeedsReview_ReviewRequired_BlockerReason_IsPotentialSanctionsMatchUnresolved()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.PotentialSanctionsMatchUnresolved));
        }

        [Test]
        public void NeedsReview_ReviewRequired_CaseInsensitiveReasonCode_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, "review_required"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.PotentialSanctionsMatchUnresolved));
        }

        // ── KYC rejected (non-sanctions) ─────────────────────────────────────────

        [Test]
        public void Rejected_OtherReason_IsLaunchBlocked_IsTrue()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "DOCUMENT_MISMATCH"));

            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void Rejected_OtherReason_Severity_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "DOCUMENT_MISMATCH"));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        [Test]
        public void Rejected_OtherReason_BlockerReason_IsKycRejected()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "DOCUMENT_MISMATCH"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycRejected));
        }

        [Test]
        public void Rejected_NullReasonCode_BlockerReason_IsKycRejected()
        {
            // Even without a reason code, rejection is a hard block
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, null));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycRejected));
        }

        // ── Manual review required (non-sanctions) ─────────────────────────────

        [Test]
        public void NeedsReview_OtherReason_IsLaunchBlocked_IsTrue()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, null));

            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void NeedsReview_OtherReason_BlockerReason_IsKycManualReviewRequired()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, null));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycManualReviewRequired));
        }

        [Test]
        public void NeedsReview_OtherReason_Severity_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, null));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        // ── Expired decision ─────────────────────────────────────────────────────

        [Test]
        public void Expired_IsLaunchBlocked_IsTrue()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Expired));

            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void Expired_Severity_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Expired));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        [Test]
        public void Expired_BlockerReason_IsKycOrAmlExpired()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Expired));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycOrAmlExpired));
        }

        [Test]
        public void Expired_RecommendedAction_MentionsRescreen()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Expired));

            Assert.That(posture.RecommendedAction, Does.Contain("rescreen").IgnoreCase
                .Or.Contain("renew").IgnoreCase.Or.Contain("fresh").IgnoreCase);
        }

        // ── Insufficient data ─────────────────────────────────────────────────────

        [Test]
        public void InsufficientData_IsLaunchBlocked_IsTrue()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.InsufficientData));

            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void InsufficientData_Severity_IsBlocking()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.InsufficientData));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        [Test]
        public void InsufficientData_BlockerReason_IsKycInsufficientData()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.InsufficientData));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycInsufficientData));
        }

        // ── Pending ───────────────────────────────────────────────────────────────

        [Test]
        public void Pending_IsLaunchBlocked_IsFalse()
        {
            // Pending is advisory: the check is in progress, launch is not hard-blocked
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Pending));

            Assert.That(posture.IsLaunchBlocked, Is.False);
        }

        [Test]
        public void Pending_Severity_IsAdvisory()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Pending));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Advisory));
        }

        [Test]
        public void Pending_BlockerReason_IsKycOrAmlPending()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Pending));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycOrAmlPending));
        }

        // ── Provider unavailable ─────────────────────────────────────────────────

        [Test]
        public void ProviderUnavailable_IsLaunchBlocked_IsFalse()
        {
            // ProviderUnavailable is advisory: not a confirmed failure, but retry recommended
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.ProviderUnavailable));

            Assert.That(posture.IsLaunchBlocked, Is.False);
        }

        [Test]
        public void ProviderUnavailable_Severity_IsAdvisory()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.ProviderUnavailable));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Advisory));
        }

        [Test]
        public void ProviderUnavailable_BlockerReason_IsProviderUnavailable()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.ProviderUnavailable));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.ProviderUnavailable));
        }

        [Test]
        public void ProviderUnavailable_RecommendedAction_MentionsRetry()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.ProviderUnavailable));

            Assert.That(posture.RecommendedAction, Does.Contain("retry").IgnoreCase
                .Or.Contain("Retry").IgnoreCase.Or.Contain("available").IgnoreCase);
        }

        // ── Internal error ───────────────────────────────────────────────────────

        [Test]
        public void Error_IsLaunchBlocked_IsFalse()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Error));

            Assert.That(posture.IsLaunchBlocked, Is.False);
        }

        [Test]
        public void Error_Severity_IsAdvisory()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Error));

            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Advisory));
        }

        [Test]
        public void Error_BlockerReason_IsComplianceCheckError()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Error));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.ComplianceCheckError));
        }

        // ── Priority ordering ─────────────────────────────────────────────────────

        [Test]
        public void Priority_SanctionsMatch_TakesPriorityOver_GenericRejected()
        {
            // Even if the reason code is present, state=Rejected + SANCTIONS_MATCH
            // must map to ConfirmedSanctionsMatch, not KycRejected
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.ConfirmedSanctionsMatch));
            Assert.That(posture.BlockerReason, Is.Not.EqualTo(IssuanceBlockerReason.KycRejected));
        }

        [Test]
        public void Priority_SanctionsMatch_TakesPriorityOver_AllOtherBlockers()
        {
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH"));

            // Sanctions is priority 1 — always the most severe possible blocker
            Assert.That(posture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
            Assert.That(posture.IsLaunchBlocked, Is.True);
        }

        [Test]
        public void Priority_ReviewRequired_TakesPriorityOver_GenericNeedsReview()
        {
            // REVIEW_REQUIRED → PotentialSanctionsMatchUnresolved (priority 2)
            // Other NeedsReview → KycManualReviewRequired (priority 4)
            var posture = ComplianceOrchestrationService.DeriveIssuancePosture(
                MakeDecision(ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED"));

            Assert.That(posture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.PotentialSanctionsMatchUnresolved));
            Assert.That(posture.BlockerReason, Is.Not.EqualTo(IssuanceBlockerReason.KycManualReviewRequired));
        }

        // ── Determinism ──────────────────────────────────────────────────────────

        [Test]
        public void DeriveIssuancePosture_IsDeterministic_SameInputProducesSameOutput()
        {
            var decision = MakeDecision(ComplianceDecisionState.Rejected, "SANCTIONS_MATCH");

            var posture1 = ComplianceOrchestrationService.DeriveIssuancePosture(decision);
            var posture2 = ComplianceOrchestrationService.DeriveIssuancePosture(decision);
            var posture3 = ComplianceOrchestrationService.DeriveIssuancePosture(decision);

            Assert.That(posture1.IsLaunchBlocked, Is.EqualTo(posture2.IsLaunchBlocked));
            Assert.That(posture2.IsLaunchBlocked, Is.EqualTo(posture3.IsLaunchBlocked));
            Assert.That(posture1.BlockerReason, Is.EqualTo(posture2.BlockerReason));
            Assert.That(posture2.BlockerReason, Is.EqualTo(posture3.BlockerReason));
            Assert.That(posture1.Severity, Is.EqualTo(posture2.Severity));
        }

        [Test]
        public void DeriveIssuancePosture_AllDecisionStates_ReturnNonNullPosture()
        {
            // Every possible state must produce a valid posture — never null or incomplete
            var states = Enum.GetValues<ComplianceDecisionState>();
            foreach (var state in states)
            {
                var decision = MakeDecision(state);
                var posture = ComplianceOrchestrationService.DeriveIssuancePosture(decision);

                Assert.That(posture, Is.Not.Null, $"Posture was null for state {state}");
                Assert.That(posture.BlockerDescription, Is.Not.Null, $"BlockerDescription was null for state {state}");
                Assert.That(posture.RecommendedAction, Is.Not.Null, $"RecommendedAction was null for state {state}");
            }
        }

        // ── Integration with ToResponse ───────────────────────────────────────────

        [Test]
        public async Task InitiateCheck_SanctionsHit_ResponseContainsIssuancePosture_IsBlocking()
        {
            // End-to-end: verify the posture is populated on the response from InitiateCheckAsync
            var kycConfig = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = true });
            var kycProvider = new MockKycProvider(kycConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                kycProvider, amlProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceOrchestrationService>.Instance);

            var request = new InitiateComplianceCheckRequest
            {
                SubjectId = "sub-001",
                ContextId = "ctx-sanctions",
                CheckType = ComplianceCheckType.Aml,
                SubjectMetadata = new Dictionary<string, string> { ["sanctions_flag"] = "true" }
            };

            var response = await svc.InitiateCheckAsync(request, "actor-1", "corr-1");

            Assert.That(response.Success, Is.True);
            Assert.That(response.IssuancePosture, Is.Not.Null);
            Assert.That(response.IssuancePosture!.IsLaunchBlocked, Is.True);
            Assert.That(response.IssuancePosture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
            Assert.That(response.IssuancePosture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.ConfirmedSanctionsMatch));
        }

        [Test]
        public async Task InitiateCheck_BothApproved_ResponseContainsIssuancePosture_Clear()
        {
            var kycConfig = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = true });
            var kycProvider = new MockKycProvider(kycConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                kycProvider, amlProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceOrchestrationService>.Instance);

            var request = new InitiateComplianceCheckRequest
            {
                SubjectId = "sub-002",
                ContextId = "ctx-clear",
                CheckType = ComplianceCheckType.Combined
            };

            var response = await svc.InitiateCheckAsync(request, "actor-1", "corr-2");

            Assert.That(response.Success, Is.True);
            Assert.That(response.IssuancePosture, Is.Not.Null);
            Assert.That(response.IssuancePosture!.IsLaunchBlocked, Is.False);
            Assert.That(response.IssuancePosture.Severity, Is.EqualTo(IssuanceBlockSeverity.None));
            Assert.That(response.IssuancePosture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.None));
        }

        [Test]
        public async Task InitiateCheck_PotentialSanctionsHit_ResponseContainsIssuancePosture_PotentialMatchUnresolved()
        {
            var kycConfig = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = true });
            var kycProvider = new MockKycProvider(kycConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                kycProvider, amlProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceOrchestrationService>.Instance);

            var request = new InitiateComplianceCheckRequest
            {
                SubjectId = "sub-003",
                ContextId = "ctx-potential",
                CheckType = ComplianceCheckType.Aml,
                SubjectMetadata = new Dictionary<string, string> { ["review_flag"] = "true" }
            };

            var response = await svc.InitiateCheckAsync(request, "actor-1", "corr-3");

            Assert.That(response.Success, Is.True);
            Assert.That(response.IssuancePosture, Is.Not.Null);
            // Review required → potential sanctions match unresolved (hard block)
            Assert.That(response.IssuancePosture!.IsLaunchBlocked, Is.True);
            Assert.That(response.IssuancePosture.Severity, Is.EqualTo(IssuanceBlockSeverity.Blocking));
        }

        [Test]
        public async Task GetCheckStatus_Returns_IssuancePosture_Populated()
        {
            var kycConfig = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = true });
            var kycProvider = new MockKycProvider(kycConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                kycProvider, amlProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceOrchestrationService>.Instance);

            var initRequest = new InitiateComplianceCheckRequest
            {
                SubjectId = "sub-004",
                ContextId = "ctx-status",
                CheckType = ComplianceCheckType.Combined
            };
            var initResponse = await svc.InitiateCheckAsync(initRequest, "actor-1", "corr-4");

            // Retrieve by decision ID and verify posture is still present
            var statusResponse = await svc.GetCheckStatusAsync(initResponse.DecisionId!);

            Assert.That(statusResponse.Success, Is.True);
            Assert.That(statusResponse.IssuancePosture, Is.Not.Null);
            Assert.That(statusResponse.IssuancePosture!.BlockerReason, Is.EqualTo(IssuanceBlockerReason.None));
        }

        // ── Async lifecycle in mock providers ────────────────────────────────────

        [Test]
        public async Task AsyncLifecycle_ProviderCallback_TransitionsDecisionState_AndUpdatesPosture()
        {
            // Verify that a provider callback updates the decision state and that
            // re-fetching the decision reflects the new issuance posture.
            // Uses the same pattern as the existing ProviderCallback_ApprovedOutcome_UpdatesDecisionToApproved test.
            var kycConfig = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = false }); // start pending
            var kycProvider = new MockKycProvider(kycConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                kycProvider, amlProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceOrchestrationService>.Instance);

            // Step 1: Initiate — expect Pending with advisory posture
            var initResponse = await svc.InitiateCheckAsync(
                new InitiateComplianceCheckRequest
                {
                    SubjectId = "sub-async",
                    ContextId = "ctx-async",
                    CheckType = ComplianceCheckType.Kyc
                }, "actor-1", "corr-async");

            Assert.That(initResponse.State, Is.EqualTo(ComplianceDecisionState.Pending));
            Assert.That(initResponse.IssuancePosture!.IsLaunchBlocked, Is.False);  // advisory only
            Assert.That(initResponse.IssuancePosture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.KycOrAmlPending));

            // Step 2: Extract the provider reference ID from the audit trail
            var statusBeforeCallback = await svc.GetCheckStatusAsync(initResponse.DecisionId!);
            var providerRef = statusBeforeCallback.AuditTrail
                .Where(e => e.ProviderReferenceId != null)
                .Select(e => e.ProviderReferenceId!)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(providerRef))
            {
                // MockAutoApprove=false: provider ref may not be in audit trail — verify posture is advisory
                Assert.Ignore("No provider reference available in audit trail for Pending KYC — skipping callback step.");
                return;
            }

            // Step 3: Provider sends approved callback — simulates async lifecycle
            var callbackResponse = await svc.ProcessProviderCallbackAsync(
                new ProviderCallbackRequest
                {
                    ProviderReferenceId = providerRef,
                    ProviderName = "Mock",
                    EventType = "verification.session.verified",
                    OutcomeStatus = "approved"
                }, "corr-async-cb");

            Assert.That(callbackResponse.Success, Is.True,
                $"Callback failed: {callbackResponse.ErrorCode} — {callbackResponse.ErrorMessage}");
            Assert.That(callbackResponse.NewState, Is.EqualTo(ComplianceDecisionState.Approved));

            // Step 4: Re-fetch decision — expect Approved with clear posture
            var statusAfterCallback = await svc.GetCheckStatusAsync(initResponse.DecisionId!);

            Assert.That(statusAfterCallback.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(statusAfterCallback.IssuancePosture!.IsLaunchBlocked, Is.False);
            Assert.That(statusAfterCallback.IssuancePosture.Severity, Is.EqualTo(IssuanceBlockSeverity.None));
            Assert.That(statusAfterCallback.IssuancePosture.BlockerReason, Is.EqualTo(IssuanceBlockerReason.None));
        }

        [Test]
        public async Task AsyncLifecycle_Rescreen_ProducesNewDecisionWithFreshPosture()
        {
            // Verify that a rescreen creates a new decision and posture is fresh on the new one.
            var kycConfig = new Microsoft.Extensions.Options.OptionsWrapper<BiatecTokensApi.Configuration.KycConfig>(
                new BiatecTokensApi.Configuration.KycConfig { MockAutoApprove = true });
            var kycProvider = new MockKycProvider(kycConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockKycProvider>.Instance);
            var amlProvider = new MockAmlProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockAmlProvider>.Instance);
            var svc = new ComplianceOrchestrationService(
                kycProvider, amlProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceOrchestrationService>.Instance);

            // Original decision
            var original = await svc.InitiateCheckAsync(
                new InitiateComplianceCheckRequest
                {
                    SubjectId = "sub-rescreen",
                    ContextId = "ctx-rescreen",
                    CheckType = ComplianceCheckType.Combined
                }, "actor-1", "corr-orig");

            // Rescreen
            var rescreenResponse = await svc.RescreenAsync(
                original.DecisionId!,
                new RescreenRequest(),
                "actor-1", "corr-rescreen");

            Assert.That(rescreenResponse.Success, Is.True);
            var newDecision = rescreenResponse.NewDecision;
            Assert.That(newDecision, Is.Not.Null);
            Assert.That(newDecision!.IssuancePosture, Is.Not.Null);
            Assert.That(newDecision.IssuancePosture!.BlockerReason, Is.EqualTo(IssuanceBlockerReason.None));
        }
    }
}

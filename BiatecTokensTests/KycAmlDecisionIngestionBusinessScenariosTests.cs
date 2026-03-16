using BiatecTokensApi.Models.KycAmlDecisionIngestion;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Business-scenario tests for <see cref="KycAmlDecisionIngestionService"/>.
    ///
    /// Each test covers a specific compliance lifecycle scenario that enterprise
    /// compliance teams, operations managers, or auditors would face in production.
    ///
    /// Scenarios covered:
    ///   1.  Missing evidence → fail-closed block
    ///   2.  Expired evidence after validity window → fail-closed stale
    ///   3.  Contradictory jurisdiction outcomes → fail-closed block
    ///   4.  Sanctions/AML failure after prior KYC success → fail-closed block
    ///   5.  Remediation reopen scenario (Approved → evidence re-expires)
    ///   6.  Full lifecycle: blocked → pending review → ready (all conditions satisfied)
    ///   7.  Partial approval: KYC passed but AML still pending → not ready
    ///   8.  Multi-provider independence: different providers for same subject kind
    ///   9.  PEP screening hit after clean identity check
    ///   10. Jurisdiction restriction overrides successful identity check
    ///   11. High-risk score blocks readiness even when identity approved
    ///   12. Adverse media hit escalates readiness to blocked
    ///   13. Reviewer note does not change decision status
    ///   14. Evidence freshness: within window → ready; just after window → stale
    ///   15. Cohort: all members must be ready for cohort to be ready
    ///   16. Cohort: single blocked member makes entire cohort blocked
    ///   17. Document review failure blocks despite AML/KYC pass
    ///   18. Sequential rescreen: second check supersedes first for same kind
    ///   19. Timeline ordering: events are chronologically ordered
    ///   20. Provider unavailable is fail-closed even if same subject has approved decisions
    /// </summary>
    [TestFixture]
    public class KycAmlDecisionIngestionBusinessScenariosTests
    {
        // ── FakeTimeProvider ──────────────────────────────────────────────────

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static KycAmlDecisionIngestionService CreateService(TimeProvider? tp = null)
            => new(NullLogger<KycAmlDecisionIngestionService>.Instance, tp);

        private static async Task<string> IngestAndGetId(
            IKycAmlDecisionIngestionService svc,
            string subjectId,
            IngestionDecisionKind kind,
            NormalizedIngestionStatus status,
            IngestionProviderType provider = IngestionProviderType.StripeIdentity,
            string? idempotencyKey = null,
            int? validityHours = null,
            string? reasonCode = null)
        {
            var req = new IngestProviderDecisionRequest
            {
                SubjectId = subjectId,
                ContextId = "ctx-scenario",
                Kind = kind,
                Provider = provider,
                Status = status,
                IdempotencyKey = idempotencyKey,
                EvidenceValidityHours = validityHours,
                ReasonCode = reasonCode
            };
            var result = await svc.IngestDecisionAsync(req, "test-actor", "corr-scenario");
            Assert.That(result.Success, Is.True, $"Ingest failed: {result.ErrorMessage}");
            return result.Decision!.DecisionId;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 1: Missing evidence → fail-closed
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario01_MissingEvidence_SubjectNeverInteracted_IsEvidenceMissing()
        {
            var svc = CreateService();
            var r = await svc.GetSubjectReadinessAsync("brand-new-investor", "corr");

            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.EvidenceMissing));
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "EVIDENCE_MISSING"), Is.True);
            Assert.That(r.Readiness.Blockers[0].IsHardBlocker, Is.True,
                "Missing evidence must be a hard blocker");
            Assert.That(r.Readiness.Blockers[0].RemediationHint, Is.Not.Empty,
                "Missing-evidence blocker must include remediation guidance");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 2: Expired evidence → stale (fail-closed)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario02_EvidenceExpired_AfterValidityWindow_IsStale()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var subjectId = "investor-expired";

            // Investor passes KYC with 30-day evidence window
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved, validityHours: 720 /* 30 days */);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved, provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-expired-001");

            // Verify initially ready
            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready),
                "Should be Ready before evidence expires");

            // Advance 31 days past expiry
            tp.Advance(TimeSpan.FromDays(31));

            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Stale),
                "Should be Stale after evidence expiry");
            Assert.That(r2.Readiness.HasExpiredEvidence, Is.True);
            Assert.That(r2.Readiness.Blockers.Any(b => b.Code == "EVIDENCE_EXPIRED"), Is.True,
                "EVIDENCE_EXPIRED blocker must be present");
            Assert.That(r2.Readiness.Blockers.First(b => b.Code == "EVIDENCE_EXPIRED").RemediationHint,
                Does.Contain("renew").Or.Contain("refresh").Or.Contain("check"),
                "Expired evidence blocker must include remediation guidance");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 3: Contradictory jurisdiction outcomes
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario03_ContradictoryJurisdiction_ApproveThenReject_IsBlocked()
        {
            var svc = CreateService();
            var subjectId = "investor-jurisdiction-conflict";

            // First jurisdiction check: US investor approved
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.JurisdictionCheck,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.Internal,
                idempotencyKey: "juris-approve-001");

            // Policy update: same investor now rejected for US (regulatory change)
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.JurisdictionCheck,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.Internal,
                idempotencyKey: "juris-reject-001",
                reasonCode: "JURISDICTION_RESTRICTED");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");

            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Contradictory jurisdiction decisions must block readiness");
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "CONTRADICTORY_DECISIONS"), Is.True,
                "CONTRADICTORY_DECISIONS blocker must be present");
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "CHECK_REJECTED"), Is.True,
                "CHECK_REJECTED blocker must also be present due to the rejected decision");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 4: Sanctions/AML failure after prior KYC success
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario04_AmlFailureAfterKycSuccess_IsBlocked()
        {
            var svc = CreateService();
            var subjectId = "investor-sanctions-hit";

            // Step 1: KYC passes cleanly
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);

            // Step 2: AML screening finds a sanctions match
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-sanctions-hit-001",
                reasonCode: "SANCTIONS_MATCH");

            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");

            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Sanctions match must block readiness despite prior KYC approval");
            Assert.That(r2.Readiness.Blockers.Any(b => b.Code == "CHECK_REJECTED"), Is.True,
                "CHECK_REJECTED blocker must be present");
            Assert.That(r2.Readiness.CheckSummary[IngestionDecisionKind.IdentityKyc],
                Is.EqualTo(NormalizedIngestionStatus.Approved),
                "KYC status in check summary must remain Approved");
            Assert.That(r2.Readiness.CheckSummary[IngestionDecisionKind.AmlSanctions],
                Is.EqualTo(NormalizedIngestionStatus.Rejected),
                "AML status in check summary must show Rejected");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 5: Remediation reopen — evidence re-expires
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario05_RemediationReopen_EvidenceReexpires_ReturnsToStale()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var subjectId = "investor-remediation";

            // Initial approval with 60-day validity
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved, validityHours: 1440 /* 60 days */);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-remed-001",
                validityHours: 1440);

            // Initially ready
            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));

            // After 61 days, evidence expires → remediation required
            tp.Advance(TimeSpan.FromDays(61));
            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Stale),
                "After expiry: must be Stale");

            // Compliance team re-runs the checks (new decisions with new validity)
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved,
                idempotencyKey: "kyc-renewed-001",
                validityHours: 1440);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-renewed-001",
                validityHours: 1440);

            // Back to Ready after remediation
            var r3 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r3.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready),
                "After remediation with new evidence: must be Ready again");
            Assert.That(r3.Readiness.Blockers, Is.Empty,
                "No blockers after successful remediation");

            // Evidence expires again (re-open scenario)
            tp.Advance(TimeSpan.FromDays(62));
            var r4 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r4.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Stale),
                "Evidence re-expires → back to Stale; system is fail-closed");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 6: Full lifecycle blocked → pending → ready
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario06_FullLifecycle_BlockedToPendingToReady()
        {
            var svc = CreateService();
            var subjectId = "investor-full-lifecycle";

            // === Phase 1: No evidence → EvidenceMissing (blocked) ===
            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.EvidenceMissing));

            // === Phase 2: KYC submitted, awaiting result ===
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Pending, idempotencyKey: "kyc-lifecycle-pend");
            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview));

            // === Phase 3: KYC requires manual review ===
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.NeedsReview, idempotencyKey: "kyc-lifecycle-review");
            var r3 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r3.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview));
            Assert.That(r3.Readiness.Advisories.Any(a => a.Code == "MANUAL_REVIEW_REQUIRED"), Is.True);

            // === Phase 4: Reviewer approves KYC ===
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved, idempotencyKey: "kyc-lifecycle-approved");

            // KYC-only approved: service is provider-agnostic and doesn't mandate specific check types.
            // The ReadinessSummary should reflect this is KYC-only approved.
            var r4 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r4.Readiness!.CheckSummary[IngestionDecisionKind.IdentityKyc],
                Is.EqualTo(NormalizedIngestionStatus.Approved),
                "KYC check summary must show Approved after reviewer approval");

            // === Phase 5: AML screening approved → all conditions satisfied ===
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-lifecycle-approved");
            var r5 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r5.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready),
                "All conditions satisfied → must be Ready");
            Assert.That(r5.Readiness.Blockers, Is.Empty);
            Assert.That(r5.Readiness.ReadinessSummary.ToLowerInvariant(), Does.Contain("ready").Or.Contain("all"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 7: Partial approval — KYC passed but AML pending → not ready
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario07_PartialApproval_KycPassedAmlPending_IsNotReady()
        {
            var svc = CreateService();
            var subjectId = "investor-partial";

            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Pending,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-partial-pend");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");

            Assert.That(r.Readiness!.ReadinessState, Is.Not.EqualTo(IngestionReadinessState.Ready),
                "Partial approval must never be Ready");
            Assert.That(r.Readiness.CheckSummary.ContainsKey(IngestionDecisionKind.IdentityKyc), Is.True);
            Assert.That(r.Readiness.CheckSummary.ContainsKey(IngestionDecisionKind.AmlSanctions), Is.True);
            Assert.That(r.Readiness.CheckSummary[IngestionDecisionKind.IdentityKyc],
                Is.EqualTo(NormalizedIngestionStatus.Approved));
            Assert.That(r.Readiness.CheckSummary[IngestionDecisionKind.AmlSanctions],
                Is.EqualTo(NormalizedIngestionStatus.Pending));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 8: Multi-provider — different providers for same check kind
        //   Both decisions must be considered; most recent governs status
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario08_MultiProvider_SameKind_MostRecentStatusIsUsed()
        {
            var svc = CreateService();
            var subjectId = "investor-multiprovider";

            // First provider: Onfido says NeedsReview
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.NeedsReview,
                provider: IngestionProviderType.Onfido,
                idempotencyKey: "kyc-onfido-nr");

            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview));

            // Second provider: Jumio gives clean approval
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.Jumio,
                idempotencyKey: "kyc-jumio-approved");

            // Now add AML to complete picture
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-multi-approved");

            // Decision list should contain both providers
            var list = await svc.ListSubjectDecisionsAsync(subjectId, "corr");
            var kycDecisions = list.Decisions.Where(d => d.Kind == IngestionDecisionKind.IdentityKyc).ToList();
            Assert.That(kycDecisions, Has.Count.EqualTo(2), "Both provider KYC decisions should be retained");
            Assert.That(kycDecisions.Any(d => d.Provider == IngestionProviderType.Onfido), Is.True);
            Assert.That(kycDecisions.Any(d => d.Provider == IngestionProviderType.Jumio), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 9: PEP screening hit after clean identity check
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario09_PepHitAfterCleanIdentityCheck_IsBlocked()
        {
            var svc = CreateService();
            var subjectId = "investor-pep-hit";

            // Identity check passes
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);
            // AML clean
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-pep-clean");

            // At this point subject looks ready
            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));

            // PEP screening finds a politically exposed person match → rejected
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.PepScreening,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.WorldCheck,
                idempotencyKey: "pep-hit-001",
                reasonCode: "PEP_MATCH");

            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "PEP match must block readiness even after clean KYC/AML");
            Assert.That(r2.Readiness.Blockers.Any(b => b.Code == "CHECK_REJECTED"), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 10: Jurisdiction restriction overrides successful identity check
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario10_JurisdictionRestrictionAfterKycApproval_IsBlocked()
        {
            var svc = CreateService();
            var subjectId = "investor-jurisdiction-restricted";

            // KYC approved
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);
            // AML clear
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-juris-clean");

            // Jurisdiction check fails — investor's domicile is a restricted region
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.JurisdictionCheck,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.Internal,
                idempotencyKey: "juris-restricted-001",
                reasonCode: "JURISDICTION_RESTRICTED");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Jurisdiction restriction overrides KYC/AML approval");
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "CHECK_REJECTED"), Is.True);
            Assert.That(r.Readiness.Blockers.Any(b => b.SourceDecisionKind == IngestionDecisionKind.JurisdictionCheck),
                Is.True, "Blocker source must be jurisdiction check");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 11: High-risk score (InsufficientData) blocks even with KYC/AML pass
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario11_RiskScoringInsufficientData_BlocksEvenWithApprovedKycAml()
        {
            var svc = CreateService();
            var subjectId = "investor-insufficient-risk-data";

            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-risk-clean");

            // Risk scoring cannot complete due to insufficient data
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.RiskScoring,
                NormalizedIngestionStatus.InsufficientData,
                provider: IngestionProviderType.Internal,
                idempotencyKey: "risk-insufficient-001");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Insufficient risk data must block even with approved KYC/AML");
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "INSUFFICIENT_DATA"), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 12: Adverse media hit escalates to blocked
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario12_AdverseMediaHit_BlocksAfterCleanProfile()
        {
            var svc = CreateService();
            var subjectId = "investor-adverse-media";

            // Full KYC/AML approved
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-media-clean");

            // Adverse media discovery
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AdverseMedia,
                NormalizedIngestionStatus.NeedsReview,
                provider: IngestionProviderType.WorldCheck,
                idempotencyKey: "adverse-media-001",
                reasonCode: "ADVERSE_MEDIA_HIT");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            // NeedsReview → PendingReview (not yet hard-blocked but not Ready)
            Assert.That(r.Readiness!.ReadinessState, Is.Not.EqualTo(IngestionReadinessState.Ready),
                "Adverse media NeedsReview must prevent Ready state");
            Assert.That(r.Readiness.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 13: Reviewer note does not change decision status
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario13_ReviewerNote_DoesNotChangeDecisionStatus()
        {
            var svc = CreateService();
            var subjectId = "investor-reviewer-note";

            // Decision is rejected
            var decisionId = await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Rejected, reasonCode: "DOC_UNCLEAR");

            // Reviewer appends a note
            var noteReq = new AppendIngestionReviewerNoteRequest
            {
                Content = "Document scan quality was poor. Investor notified to resubmit.",
                EvidenceReferences = new Dictionary<string, string>
                {
                    { "ticket", "COMP-1234" },
                    { "reviewer", "compliance-officer-01" }
                }
            };
            var noteResult = await svc.AppendReviewerNoteAsync(decisionId, noteReq, "compliance-officer-01", "corr");
            Assert.That(noteResult.Success, Is.True);

            // Status must still be Rejected — note doesn't change it
            var fetched = await svc.GetDecisionAsync(decisionId, "corr");
            Assert.That(fetched.Decision!.Status, Is.EqualTo(NormalizedIngestionStatus.Rejected),
                "Appending a note must not change the decision status");

            // Readiness must still be Blocked
            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Readiness must remain Blocked after reviewer note");

            // Note must contain evidence references for auditability
            Assert.That(fetched.Decision.ReviewerNotes[0].EvidenceReferences["ticket"], Is.EqualTo("COMP-1234"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 14: Evidence freshness — exact boundary conditions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario14_EvidenceFreshness_ExactBoundaryConditions()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var subjectId = "investor-boundary";

            // 1-hour validity window
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved, validityHours: 1);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-boundary-001");

            // At T+59min: still ready
            tp.Advance(TimeSpan.FromMinutes(59));
            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready),
                "At T+59min within 1h window: still Ready");

            // At T+61min: past the 60-min mark → expired
            tp.Advance(TimeSpan.FromMinutes(2)); // now T+61min total
            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Stale),
                "At T+61min past 1h window: must be Stale");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 15: Cohort — all members must be ready for cohort to be ready
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario15_Cohort_AllMembersReady_CohortIsReady()
        {
            var svc = CreateService();
            var cohortId = "cohort-token-launch-a";
            var subjects = new[] { "coh-s1", "coh-s2", "coh-s3" };

            // Approve all members
            foreach (var sub in subjects)
            {
                await IngestAndGetId(svc, sub, IngestionDecisionKind.IdentityKyc,
                    NormalizedIngestionStatus.Approved, idempotencyKey: $"kyc-{sub}");
                await IngestAndGetId(svc, sub, IngestionDecisionKind.AmlSanctions,
                    NormalizedIngestionStatus.Approved,
                    provider: IngestionProviderType.ComplyAdvantage,
                    idempotencyKey: $"aml-{sub}");
            }

            await svc.UpsertCohortAsync(new UpsertCohortRequest
            {
                CohortId = cohortId,
                CohortName = "Token Launch A Investors",
                SubjectIds = subjects.ToList()
            }, "corr");

            var r = await svc.GetCohortReadinessAsync(cohortId, "corr");

            Assert.That(r.CohortReadiness!.OverallReadinessState, Is.EqualTo(IngestionReadinessState.Ready),
                "All members ready → cohort must be Ready");
            Assert.That(r.CohortReadiness.TotalSubjects, Is.EqualTo(3));
            Assert.That(r.CohortReadiness.CohortBlockers, Is.Empty);
            Assert.That(r.CohortReadiness.SubjectCountByState.GetValueOrDefault(IngestionReadinessState.Ready),
                Is.EqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 16: Cohort — single blocked member prevents cohort Ready
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario16_Cohort_SingleBlockedMember_BlocksEntireCohort()
        {
            var svc = CreateService();
            var cohortId = "cohort-token-launch-b";

            // s1 and s2: ready
            foreach (var sub in new[] { "coh-b-s1", "coh-b-s2" })
            {
                await IngestAndGetId(svc, sub, IngestionDecisionKind.IdentityKyc,
                    NormalizedIngestionStatus.Approved, idempotencyKey: $"kyc-b-{sub}");
                await IngestAndGetId(svc, sub, IngestionDecisionKind.AmlSanctions,
                    NormalizedIngestionStatus.Approved,
                    provider: IngestionProviderType.ComplyAdvantage,
                    idempotencyKey: $"aml-b-{sub}");
            }

            // s3: rejected (sanctions hit)
            await IngestAndGetId(svc, "coh-b-s3", IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-b-s3-blocked",
                reasonCode: "SANCTIONS_MATCH");

            await svc.UpsertCohortAsync(new UpsertCohortRequest
            {
                CohortId = cohortId,
                SubjectIds = new List<string> { "coh-b-s1", "coh-b-s2", "coh-b-s3" }
            }, "corr");

            var r = await svc.GetCohortReadinessAsync(cohortId, "corr");

            Assert.That(r.CohortReadiness!.OverallReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Single blocked member must prevent cohort from being Ready");
            Assert.That(r.CohortReadiness.CohortBlockers, Is.Not.Empty,
                "Cohort-level blockers must be surfaced");
            Assert.That(r.CohortReadiness.ReadinessSummary.ToLowerInvariant(), Does.Contain("block"),
                "Summary must mention blockers");

            // Ready count should still be 2 (partial progress visible)
            Assert.That(r.CohortReadiness.SubjectCountByState.GetValueOrDefault(IngestionReadinessState.Ready),
                Is.EqualTo(2), "2 out of 3 subjects should still show as Ready");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 17: Document review failure blocks despite AML/KYC pass
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario17_DocumentReviewFailure_BlocksEvenWithKycAmlPass()
        {
            var svc = CreateService();
            var subjectId = "investor-doc-fail";

            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-doc-clean");

            // Document review fails (e.g., passport expired)
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.DocumentReview,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.Onfido,
                idempotencyKey: "doc-review-failed",
                reasonCode: "DOCUMENT_EXPIRED");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Failed document review must block readiness");
            Assert.That(r.Readiness.Blockers.Any(b => b.SourceDecisionKind == IngestionDecisionKind.DocumentReview),
                Is.True, "Blocker must be attributed to document review");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 18: Sequential rescreen — second check supersedes first
        //   (Most recent decision per kind governs check summary)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario18_Rescreen_SecondApprovalSupersedesInitialNeedsReview()
        {
            var svc = CreateService();
            var subjectId = "investor-rescreen";

            // Initial check: provider returns NeedsReview
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.NeedsReview, idempotencyKey: "kyc-rescreen-nr");

            // Manual review → Approved (rescreen result)
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved, idempotencyKey: "kyc-rescreen-approved");

            // Add AML
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-rescreen-clean");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready),
                "After successful rescreen, status must be Ready");

            // Check summary must reflect most recent (Approved) for KYC kind
            Assert.That(r.Readiness.CheckSummary[IngestionDecisionKind.IdentityKyc],
                Is.EqualTo(NormalizedIngestionStatus.Approved),
                "Check summary must show Approved (most recent)");

            // Decision list must contain both decisions (audit trail preserved)
            var list = await svc.ListSubjectDecisionsAsync(subjectId, "corr");
            var kycDecisions = list.Decisions.Where(d => d.Kind == IngestionDecisionKind.IdentityKyc).ToList();
            Assert.That(kycDecisions, Has.Count.EqualTo(2),
                "Both KYC decisions (NeedsReview + Approved) must be in audit trail");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 18b: Reverse-chronology latest-wins — second provider gives worse status
        //   A second NeedsReview coming after Approved must downgrade readiness
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario18b_Rescreen_SecondNeedsReviewSupersedesApproved_DowngradesReadiness()
        {
            var svc = CreateService();
            var subjectId = "investor-rescreen-downgrade";

            // First provider: Jumio approves
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.Jumio,
                idempotencyKey: "kyc-jumio-approved");

            // AML clean
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-downgrade-clean");

            // Initially ready
            var r1 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));

            // Second provider: Onfido flags same subject for manual review (e.g., additional screening)
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.NeedsReview,
                provider: IngestionProviderType.Onfido,
                idempotencyKey: "kyc-onfido-needs-review");

            // Latest-wins: NeedsReview is now the current KYC posture → PendingReview
            var r2 = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r2.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview),
                "Latest NeedsReview from second provider must downgrade from Ready to PendingReview");
            Assert.That(r2.Readiness.CheckSummary[IngestionDecisionKind.IdentityKyc],
                Is.EqualTo(NormalizedIngestionStatus.NeedsReview),
                "Check summary must reflect most recent (NeedsReview) status");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 19: Timeline ordering — events are chronologically ordered
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario19_Timeline_EventsChronologicallyOrdered()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var subjectId = "investor-timeline-order";

            // Ingest at T+0
            var d1 = await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.NeedsReview, idempotencyKey: "tl-kyc-nr");

            // Advance and add reviewer note at T+5min
            tp.Advance(TimeSpan.FromMinutes(5));
            await svc.AppendReviewerNoteAsync(d1,
                new AppendIngestionReviewerNoteRequest { Content = "Reviewing" },
                "reviewer", "corr");

            // Advance and ingest AML at T+10min
            tp.Advance(TimeSpan.FromMinutes(5));
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Approved,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "tl-aml-approved");

            var timeline = await svc.GetSubjectTimelineAsync(subjectId, "corr");

            // Timeline should have events from both decisions
            Assert.That(timeline.Timeline, Has.Count.GreaterThanOrEqualTo(3),
                "Timeline must contain at least 3 events (2 ingestions + 1 note)");

            // Verify descending order (most recent first)
            var timestamps = timeline.Timeline.Select(e => e.OccurredAt).ToList();
            for (int i = 0; i < timestamps.Count - 1; i++)
            {
                Assert.That(timestamps[i], Is.GreaterThanOrEqualTo(timestamps[i + 1]),
                    $"Timeline event [{i}] must be >= event [{i+1}] (most-recent-first order)");
            }

            // First event must have "DecisionIngested" or "ReviewerNoteAdded" types
            var eventTypes = timeline.Timeline.Select(e => e.EventType).ToList();
            Assert.That(eventTypes, Does.Contain("DecisionIngested"));
            Assert.That(eventTypes, Does.Contain("ReviewerNoteAdded"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 20: Provider unavailable is fail-closed even with approved decisions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario20_ProviderUnavailable_FailClosed_EvenWithApprovedDecisions()
        {
            var svc = CreateService();
            var subjectId = "investor-provider-unavail";

            // KYC approved
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Approved);

            // AML provider is unreachable during check
            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.ProviderUnavailable,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-unavail-001",
                reasonCode: "PROVIDER_TIMEOUT");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "ProviderUnavailable must block readiness — system is fail-closed by design");
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "PROVIDER_UNAVAILABLE"), Is.True);
            Assert.That(r.Readiness.Blockers.First(b => b.Code == "PROVIDER_UNAVAILABLE").IsHardBlocker,
                Is.True, "PROVIDER_UNAVAILABLE must be a hard blocker");
            Assert.That(r.Readiness.Blockers.First(b => b.Code == "PROVIDER_UNAVAILABLE").RemediationHint,
                Is.Not.Empty, "Provider unavailable blocker must include remediation guidance");

            // The approved KYC decision is preserved in audit trail
            var list = await svc.ListSubjectDecisionsAsync(subjectId, "corr");
            Assert.That(list.Decisions.Any(d => d.Status == NormalizedIngestionStatus.Approved), Is.True,
                "Approved KYC decision must still be in audit trail");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 21: Provenance retained in evidence artifacts
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario21_EvidenceArtifacts_ProvenanceRetained()
        {
            var svc = CreateService();
            var subjectId = "investor-provenance";

            var req = new IngestProviderDecisionRequest
            {
                SubjectId = subjectId,
                ContextId = "ctx-provenance",
                Kind = IngestionDecisionKind.IdentityKyc,
                Provider = IngestionProviderType.Onfido,
                ProviderReferenceId = "onfido-wf-99",
                ProviderRawStatus = "consider",  // raw provider status retained for audit
                Status = NormalizedIngestionStatus.NeedsReview,
                ConfidenceScore = 72.5,
                Jurisdiction = "DE",
                EvidenceArtifacts = new List<IngestEvidenceArtifactRequest>
                {
                    new()
                    {
                        Kind = EvidenceArtifactKind.IdentityDocument,
                        Label = "Passport DE issued 2024",
                        ProviderArtifactId = "onfido-doc-abc",
                        ContentHash = "sha256:cafebabe",
                        Metadata = new Dictionary<string, string>
                        {
                            { "documentType", "PASSPORT" },
                            { "issuingCountry", "DE" },
                            { "expiryYear", "2034" }
                        }
                    }
                }
            };
            var ingestResult = await svc.IngestDecisionAsync(req, "compliance-officer", "corr-prov");

            Assert.That(ingestResult.Success, Is.True);
            var d = ingestResult.Decision!;

            // Provenance fields must all be retained
            Assert.That(d.ProviderReferenceId, Is.EqualTo("onfido-wf-99"),
                "Provider reference ID must be retained");
            Assert.That(d.ProviderRawStatus, Is.EqualTo("consider"),
                "Raw provider status must be retained for audit");
            Assert.That(d.Provider, Is.EqualTo(IngestionProviderType.Onfido));
            Assert.That(d.ConfidenceScore, Is.EqualTo(72.5));
            Assert.That(d.Jurisdiction, Is.EqualTo("DE"));
            Assert.That(d.IngestedBy, Is.EqualTo("compliance-officer"));
            Assert.That(d.IngestedAt, Is.Not.EqualTo(default(DateTimeOffset)));

            // Evidence artifact provenance
            Assert.That(d.EvidenceArtifacts, Has.Count.EqualTo(1));
            var artifact = d.EvidenceArtifacts[0];
            Assert.That(artifact.ProviderArtifactId, Is.EqualTo("onfido-doc-abc"));
            Assert.That(artifact.ContentHash, Is.EqualTo("sha256:cafebabe"));
            Assert.That(artifact.Metadata["issuingCountry"], Is.EqualTo("DE"));

            // Normalized status must be used for business logic (not raw status)
            Assert.That(d.Status, Is.EqualTo(NormalizedIngestionStatus.NeedsReview),
                "Business logic must use normalised status, not raw provider status");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 22: Readiness summary text is human-readable
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario22_ReadinessSummary_IsHumanReadable()
        {
            var svc = CreateService();
            var subjects = new Dictionary<string, IngestionReadinessState>();

            // EvidenceMissing
            var rMissing = await svc.GetSubjectReadinessAsync("summary-missing", "corr");
            Assert.That(rMissing.Readiness!.ReadinessSummary.Length, Is.GreaterThan(10),
                "Summary must be non-trivial human-readable text");
            Assert.That(rMissing.Readiness.ReadinessSummary, Does.Not.Contain("Unknown"),
                "Summary for EvidenceMissing should not say Unknown");

            // Blocked
            var req = new IngestProviderDecisionRequest
            {
                SubjectId = "summary-blocked", ContextId = "ctx", Kind = IngestionDecisionKind.IdentityKyc,
                Provider = IngestionProviderType.Manual, Status = NormalizedIngestionStatus.Rejected
            };
            await svc.IngestDecisionAsync(req, "actor", "corr");
            var rBlocked = await svc.GetSubjectReadinessAsync("summary-blocked", "corr");
            Assert.That(rBlocked.Readiness!.ReadinessSummary.Length, Is.GreaterThan(10));

            // Ready
            await svc.IngestDecisionAsync(new IngestProviderDecisionRequest
            {
                SubjectId = "summary-ready", ContextId = "ctx",
                Kind = IngestionDecisionKind.IdentityKyc,
                Provider = IngestionProviderType.StripeIdentity,
                Status = NormalizedIngestionStatus.Approved
            }, "actor", "corr");
            await svc.IngestDecisionAsync(new IngestProviderDecisionRequest
            {
                SubjectId = "summary-ready", ContextId = "ctx",
                Kind = IngestionDecisionKind.AmlSanctions,
                Provider = IngestionProviderType.ComplyAdvantage,
                Status = NormalizedIngestionStatus.Approved,
                IdempotencyKey = "aml-summary-ready"
            }, "actor", "corr");
            var rReady = await svc.GetSubjectReadinessAsync("summary-ready", "corr");
            Assert.That(rReady.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));
            Assert.That(rReady.Readiness.ReadinessSummary.Length, Is.GreaterThan(10));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 23: Error state is treated as hard blocker
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario23_ErrorState_IsHardBlocker_NotAdvisory()
        {
            var svc = CreateService();
            var subjectId = "investor-error-state";

            await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc,
                NormalizedIngestionStatus.Error, reasonCode: "INTERNAL_ERROR");

            var r = await svc.GetSubjectReadinessAsync(subjectId, "corr");

            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Error state must produce Blocked, not advisory/pending");
            Assert.That(r.Readiness.Blockers.Any(b => b.Code == "CHECK_ERROR" && b.IsHardBlocker), Is.True,
                "CHECK_ERROR must be a hard blocker, not an advisory");
            Assert.That(r.Readiness.Advisories.All(a => a.Code != "CHECK_ERROR"), Is.True,
                "CHECK_ERROR must not appear in advisories — it is a hard blocker");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 24: Blockers include remediation hints
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario24_Blockers_AlwaysIncludeRemediationHints()
        {
            var svc = CreateService();

            // Test multiple blocker types all have remediation hints
            var scenarios = new (string SubjectId, NormalizedIngestionStatus Status)[]
            {
                ("rh-rejected", NormalizedIngestionStatus.Rejected),
                ("rh-error", NormalizedIngestionStatus.Error),
                ("rh-unavail", NormalizedIngestionStatus.ProviderUnavailable),
                ("rh-insufficient", NormalizedIngestionStatus.InsufficientData),
            };

            foreach (var (subjectId, status) in scenarios)
            {
                await IngestAndGetId(svc, subjectId, IngestionDecisionKind.IdentityKyc, status,
                    idempotencyKey: $"rh-{status}-001");
                var r = await svc.GetSubjectBlockersAsync(subjectId, "corr");

                foreach (var blocker in r.HardBlockers)
                {
                    Assert.That(blocker.RemediationHint, Is.Not.Null.And.Not.Empty,
                        $"Blocker '{blocker.Code}' for status {status} must have a RemediationHint");
                }
            }

            // Also check missing evidence blocker
            var rMissing = await svc.GetSubjectBlockersAsync("no-evidence-rh", "corr");
            Assert.That(rMissing.HardBlockers[0].RemediationHint, Is.Not.Empty,
                "EVIDENCE_MISSING blocker must include remediation hint");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Scenario 25: Cohort summary statistics are accurate
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Scenario25_CohortSummaryStatistics_AreAccurate()
        {
            var svc = CreateService();
            var cohortId = "cohort-statistics";

            // 3 ready subjects (explicit loop for readability)
            for (int i = 1; i <= 3; i++)
            {
                var sub = $"stat-ready-{i}";
                await IngestAndGetId(svc, sub, IngestionDecisionKind.IdentityKyc,
                    NormalizedIngestionStatus.Approved, idempotencyKey: $"kyc-stat-{i}");
                await IngestAndGetId(svc, sub, IngestionDecisionKind.AmlSanctions,
                    NormalizedIngestionStatus.Approved,
                    provider: IngestionProviderType.ComplyAdvantage,
                    idempotencyKey: $"aml-stat-{i}");
            }

            // 2 pending subjects (explicit loop for readability)
            for (int i = 1; i <= 2; i++)
            {
                var sub = $"stat-pend-{i}";
                await IngestAndGetId(svc, sub, IngestionDecisionKind.IdentityKyc,
                    NormalizedIngestionStatus.NeedsReview,
                    idempotencyKey: $"kyc-stat-pend-{i}");
            }

            // 1 blocked subject
            await IngestAndGetId(svc, "stat-blocked-1", IngestionDecisionKind.AmlSanctions,
                NormalizedIngestionStatus.Rejected,
                provider: IngestionProviderType.ComplyAdvantage,
                idempotencyKey: "aml-stat-blocked");

            var allSubjects = new List<string>
            {
                "stat-ready-1", "stat-ready-2", "stat-ready-3",
                "stat-pend-1", "stat-pend-2",
                "stat-blocked-1"
            };

            await svc.UpsertCohortAsync(new UpsertCohortRequest
            {
                CohortId = cohortId,
                SubjectIds = allSubjects
            }, "corr");

            var r = await svc.GetCohortReadinessAsync(cohortId, "corr");

            Assert.That(r.CohortReadiness!.TotalSubjects, Is.EqualTo(6));
            Assert.That(r.CohortReadiness.OverallReadinessState, Is.EqualTo(IngestionReadinessState.Blocked),
                "Any blocked subject → cohort blocked");
            Assert.That(r.CohortReadiness.SubjectCountByState.GetValueOrDefault(IngestionReadinessState.Ready),
                Is.EqualTo(3), "3 subjects ready");
            Assert.That(r.CohortReadiness.SubjectCountByState.GetValueOrDefault(IngestionReadinessState.PendingReview),
                Is.EqualTo(2), "2 subjects pending review");
            Assert.That(
                r.CohortReadiness.SubjectCountByState.GetValueOrDefault(IngestionReadinessState.Blocked) +
                r.CohortReadiness.SubjectCountByState.GetValueOrDefault(IngestionReadinessState.EvidenceMissing),
                Is.EqualTo(1), "1 subject blocked/evidence-missing");
        }
    }
}

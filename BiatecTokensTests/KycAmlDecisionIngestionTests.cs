using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.KycAmlDecisionIngestion;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for <see cref="KycAmlDecisionIngestionService"/> and
    /// <see cref="BiatecTokensApi.Controllers.KycAmlDecisionIngestionController"/>.
    ///
    /// Coverage:
    ///   Unit tests:
    ///   - Decision ingestion (happy path, missing fields, idempotency)
    ///   - Fail-closed readiness rules (missing evidence, provider unavailable, error, contradiction, rejected, expired)
    ///   - Readiness aggregation: Ready, Blocked, PendingReview, AtRisk, Stale, EvidenceMissing
    ///   - Cohort readiness aggregation (most-severe-wins)
    ///   - Evidence freshness with FakeTimeProvider
    ///   - Reviewer note appending
    ///   - Timeline merging
    ///   - Subject decision listing
    ///   Integration tests:
    ///   - Full HTTP pipeline via WebApplicationFactory
    ///   - Auth required on all endpoints
    ///   - Decision ingestion and retrieval roundtrip
    ///   - Subject readiness and blockers via HTTP
    ///   - Cohort management via HTTP
    ///   - Reviewer note via HTTP
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlDecisionIngestionTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider for evidence freshness tests
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value) => _now = value;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Service factory helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static KycAmlDecisionIngestionService CreateService(TimeProvider? timeProvider = null)
            => new(NullLogger<KycAmlDecisionIngestionService>.Instance, timeProvider);

        private static IngestProviderDecisionRequest ApprovedKycRequest(
            string? subjectId = null,
            string? contextId = null,
            string? idempotencyKey = null,
            int? validityHours = null)
            => new()
            {
                SubjectId = subjectId ?? "subject-001",
                ContextId = contextId ?? "ctx-001",
                Kind = IngestionDecisionKind.IdentityKyc,
                Provider = IngestionProviderType.StripeIdentity,
                ProviderReferenceId = "stripe-ref-001",
                ProviderRawStatus = "verified",
                Status = NormalizedIngestionStatus.Approved,
                ConfidenceScore = 95.0,
                Rationale = "Identity verified successfully.",
                EvidenceValidityHours = validityHours,
                IdempotencyKey = idempotencyKey
            };

        private static IngestProviderDecisionRequest ApprovedAmlRequest(
            string? subjectId = null,
            string? contextId = null)
            => new()
            {
                SubjectId = subjectId ?? "subject-001",
                ContextId = contextId ?? "ctx-001",
                Kind = IngestionDecisionKind.AmlSanctions,
                Provider = IngestionProviderType.ComplyAdvantage,
                ProviderReferenceId = "ca-ref-001",
                Status = NormalizedIngestionStatus.Approved,
                Rationale = "No sanctions matches found."
            };

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — IngestDecisionAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IngestDecision_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest();
            var result = await svc.IngestDecisionAsync(req, "actor@example.com", "corr-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decision, Is.Not.Null);
            Assert.That(result.Decision!.DecisionId, Is.Not.Empty);
            Assert.That(result.Decision.Status, Is.EqualTo(NormalizedIngestionStatus.Approved));
            Assert.That(result.Decision.SubjectId, Is.EqualTo("subject-001"));
            Assert.That(result.Decision.Provider, Is.EqualTo(IngestionProviderType.StripeIdentity));
            Assert.That(result.WasIdempotentReplay, Is.False);
        }

        [Test]
        public async Task IngestDecision_MissingSubjectId_ReturnsMissingField()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest(subjectId: "");
            var result = await svc.IngestDecisionAsync(req, "actor", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task IngestDecision_MissingContextId_ReturnsMissingField()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest(contextId: "");
            var result = await svc.IngestDecisionAsync(req, "actor", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task IngestDecision_IdempotentKey_ReturnsSameRecord()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest(idempotencyKey: "my-key-001");
            var r1 = await svc.IngestDecisionAsync(req, "actor", "corr-1");
            var r2 = await svc.IngestDecisionAsync(req, "actor", "corr-2");

            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.WasIdempotentReplay, Is.True);
            Assert.That(r2.Decision!.DecisionId, Is.EqualTo(r1.Decision!.DecisionId));
        }

        [Test]
        public async Task IngestDecision_DefaultIdempotencyKey_DerivedFromFields()
        {
            var svc = CreateService();
            var req1 = ApprovedKycRequest(); // no explicit key
            var req2 = ApprovedKycRequest(); // same defaults → same derived key
            var r1 = await svc.IngestDecisionAsync(req1, "actor", "corr-1");
            var r2 = await svc.IngestDecisionAsync(req2, "actor", "corr-2");

            Assert.That(r2.WasIdempotentReplay, Is.True);
            Assert.That(r2.Decision!.DecisionId, Is.EqualTo(r1.Decision!.DecisionId));
        }

        [Test]
        public async Task IngestDecision_DifferentSubjects_AreIndependent()
        {
            var svc = CreateService();
            var r1 = await svc.IngestDecisionAsync(ApprovedKycRequest("subject-A"), "actor", "corr");
            var r2 = await svc.IngestDecisionAsync(ApprovedKycRequest("subject-B"), "actor", "corr");

            Assert.That(r1.Decision!.DecisionId, Is.Not.EqualTo(r2.Decision!.DecisionId));
        }

        [Test]
        public async Task IngestDecision_StoresEvidenceArtifacts()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest();
            req.EvidenceArtifacts = new List<IngestEvidenceArtifactRequest>
            {
                new() { Kind = EvidenceArtifactKind.IdentityDocument, Label = "Passport", ContentHash = "sha256:abc" }
            };
            var result = await svc.IngestDecisionAsync(req, "actor", "corr");

            Assert.That(result.Decision!.EvidenceArtifacts, Has.Count.EqualTo(1));
            Assert.That(result.Decision.EvidenceArtifacts[0].Label, Is.EqualTo("Passport"));
            Assert.That(result.Decision.EvidenceArtifacts[0].ContentHash, Is.EqualTo("sha256:abc"));
        }

        [Test]
        public async Task IngestDecision_WithEvidenceValidity_SetsExpiryDate()
        {
            var now = DateTimeOffset.UtcNow;
            var tp = new FakeTimeProvider(now);
            var svc = CreateService(tp);
            var req = ApprovedKycRequest(validityHours: 24);
            var result = await svc.IngestDecisionAsync(req, "actor", "corr");

            Assert.That(result.Decision!.EvidenceExpiresAt, Is.Not.Null);
            Assert.That(result.Decision.EvidenceExpiresAt!.Value, Is.EqualTo(now.AddHours(24)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(result.Decision.IsEvidenceExpired, Is.False);
        }

        [Test]
        public async Task IngestDecision_BuildsTimeline()
        {
            var svc = CreateService();
            var result = await svc.IngestDecisionAsync(ApprovedKycRequest(), "actor", "corr");

            Assert.That(result.Decision!.Timeline, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(result.Decision.Timeline[0].EventType, Is.EqualTo("DecisionIngested"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — GetDecisionAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetDecision_ExistingId_ReturnsDecision()
        {
            var svc = CreateService();
            var ingested = (await svc.IngestDecisionAsync(ApprovedKycRequest(), "actor", "corr")).Decision!;
            var result = await svc.GetDecisionAsync(ingested.DecisionId, "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decision!.DecisionId, Is.EqualTo(ingested.DecisionId));
        }

        [Test]
        public async Task GetDecision_UnknownId_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.GetDecisionAsync("nonexistent-id", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INGESTION_DECISION_NOT_FOUND"));
        }

        [Test]
        public async Task GetDecision_EmptyId_ReturnsMissingField()
        {
            var svc = CreateService();
            var result = await svc.GetDecisionAsync("", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Fail-closed readiness rules
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_NoDecisions_IsEvidenceMissing()
        {
            var svc = CreateService();
            var result = await svc.GetSubjectReadinessAsync("no-subject", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.EvidenceMissing));
            Assert.That(result.Readiness.Blockers, Has.Count.GreaterThan(0));
            Assert.That(result.Readiness.Blockers[0].Code, Is.EqualTo("EVIDENCE_MISSING"));
            Assert.That(result.Readiness.Blockers[0].IsHardBlocker, Is.True);
        }

        [Test]
        public async Task Readiness_AllApproved_IsReady()
        {
            var svc = CreateService();
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-r"), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-r"), "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-r", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));
            Assert.That(result.Readiness.Blockers, Is.Empty);
        }

        [Test]
        public async Task Readiness_ProviderUnavailable_IsBlocked()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-pu");
            req.Status = NormalizedIngestionStatus.ProviderUnavailable;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-pu", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
            Assert.That(result.Readiness.Blockers.Any(b => b.Code == "PROVIDER_UNAVAILABLE"), Is.True);
        }

        [Test]
        public async Task Readiness_Error_IsBlocked()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-err");
            req.Status = NormalizedIngestionStatus.Error;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-err", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
            Assert.That(result.Readiness.Blockers.Any(b => b.Code == "CHECK_ERROR"), Is.True);
        }

        [Test]
        public async Task Readiness_Rejected_IsBlocked()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-rej");
            req.Status = NormalizedIngestionStatus.Rejected;
            req.ReasonCode = "SANCTIONS_MATCH";
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-rej", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
            Assert.That(result.Readiness.Blockers.Any(b => b.Code == "CHECK_REJECTED"), Is.True);
            Assert.That(result.Readiness.Blockers.Any(b => b.IsHardBlocker), Is.True);
        }

        [Test]
        public async Task Readiness_InsufficientData_IsBlocked()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-ins");
            req.Status = NormalizedIngestionStatus.InsufficientData;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-ins", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
            Assert.That(result.Readiness.Blockers.Any(b => b.Code == "INSUFFICIENT_DATA"), Is.True);
        }

        [Test]
        public async Task Readiness_ContradictoryDecisions_IsBlocked()
        {
            var svc = CreateService();
            // Ingest approved then rejected for same kind on same subject
            var r1 = ApprovedKycRequest("sub-contra");
            await svc.IngestDecisionAsync(r1, "actor", "corr");

            var r2 = ApprovedKycRequest("sub-contra", idempotencyKey: "key-rejected");
            r2.Status = NormalizedIngestionStatus.Rejected;
            await svc.IngestDecisionAsync(r2, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-contra", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
            Assert.That(result.Readiness.Blockers.Any(b => b.Code == "CONTRADICTORY_DECISIONS"), Is.True);
        }

        [Test]
        public async Task Readiness_NeedsReview_IsPendingReview()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-nr");
            req.Status = NormalizedIngestionStatus.NeedsReview;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-nr", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview));
            Assert.That(result.Readiness.Advisories.Any(a => a.Code == "MANUAL_REVIEW_REQUIRED"), Is.True);
        }

        [Test]
        public async Task Readiness_Pending_IsPendingReview()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-pend");
            req.Status = NormalizedIngestionStatus.Pending;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-pend", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.PendingReview));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Evidence freshness / expiry
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_ExpiredEvidence_IsStale()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var req = ApprovedKycRequest("sub-exp", validityHours: 24);
            await svc.IngestDecisionAsync(req, "actor", "corr");

            // Advance time beyond expiry
            tp.Advance(TimeSpan.FromHours(25));

            var result = await svc.GetSubjectReadinessAsync("sub-exp", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Stale));
            Assert.That(result.Readiness.HasExpiredEvidence, Is.True);
            Assert.That(result.Readiness.Blockers.Any(b => b.Code == "EVIDENCE_EXPIRED"), Is.True);
        }

        [Test]
        public async Task Readiness_NotYetExpired_IsReady()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-notexp", validityHours: 48), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-notexp"), "actor", "corr");

            tp.Advance(TimeSpan.FromHours(24)); // still within validity

            var result = await svc.GetSubjectReadinessAsync("sub-notexp", "corr");

            Assert.That(result.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));
            Assert.That(result.Readiness.HasExpiredEvidence, Is.False);
        }

        [Test]
        public async Task Readiness_ArtifactExpiry_DetectedPerArtifact()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var req = ApprovedKycRequest("sub-art");
            req.EvidenceArtifacts = new List<IngestEvidenceArtifactRequest>
            {
                new()
                {
                    Kind = EvidenceArtifactKind.IdentityDocument,
                    Label = "Passport",
                    ExpiresAt = tp.GetUtcNow().AddHours(10)
                }
            };
            await svc.IngestDecisionAsync(req, "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-art"), "actor", "corr");

            // Before expiry → Ready
            var r1 = await svc.GetSubjectReadinessAsync("sub-art", "corr");
            Assert.That(r1.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));

            // After artifact expiry
            tp.Advance(TimeSpan.FromHours(11));
            var r2 = await svc.GetSubjectReadinessAsync("sub-art", "corr");
            Assert.That(r2.Readiness!.HasExpiredEvidence, Is.True);
            Assert.That(r2.Readiness.Blockers.Any(b => b.Code == "ARTIFACT_EXPIRED"), Is.True);
        }

        [Test]
        public async Task GetDecision_ReflectsCurrentExpiryState()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            var ingested = (await svc.IngestDecisionAsync(ApprovedKycRequest("sub-exp2", validityHours: 24), "actor", "corr")).Decision!;

            // Not expired yet
            var r1 = await svc.GetDecisionAsync(ingested.DecisionId, "corr");
            Assert.That(r1.Decision!.IsEvidenceExpired, Is.False);

            // Advance past expiry
            tp.Advance(TimeSpan.FromHours(25));
            var r2 = await svc.GetDecisionAsync(ingested.DecisionId, "corr");
            Assert.That(r2.Decision!.IsEvidenceExpired, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Timeline and history
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Timeline_ReturnsEventsForSubject()
        {
            var svc = CreateService();
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-tl"), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-tl"), "actor", "corr");

            var result = await svc.GetSubjectTimelineAsync("sub-tl", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Timeline, Has.Count.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task Timeline_EmptySubject_ReturnsEmptyList()
        {
            var svc = CreateService();
            var result = await svc.GetSubjectTimelineAsync("no-decisions-subject", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Timeline, Is.Empty);
        }

        [Test]
        public async Task ListDecisions_ReturnsAllForSubject()
        {
            var svc = CreateService();
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-list"), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-list"), "actor", "corr");

            var result = await svc.ListSubjectDecisionsAsync("sub-list", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decisions, Has.Count.EqualTo(2));
            Assert.That(result.SubjectId, Is.EqualTo("sub-list"));
        }

        [Test]
        public async Task ListDecisions_OtherSubject_ReturnsEmpty()
        {
            var svc = CreateService();
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-A"), "actor", "corr");

            var result = await svc.ListSubjectDecisionsAsync("sub-B", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decisions, Is.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Reviewer notes
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AppendNote_ValidDecision_Succeeds()
        {
            var svc = CreateService();
            var decision = (await svc.IngestDecisionAsync(ApprovedKycRequest("sub-note"), "actor", "corr")).Decision!;
            var noteReq = new AppendIngestionReviewerNoteRequest { Content = "Passport verified by reviewer." };

            var result = await svc.AppendReviewerNoteAsync(decision.DecisionId, noteReq, "reviewer@example.com", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Note, Is.Not.Null);
            Assert.That(result.Note!.Content, Is.EqualTo("Passport verified by reviewer."));
            Assert.That(result.Note.ActorId, Is.EqualTo("reviewer@example.com"));
        }

        [Test]
        public async Task AppendNote_UnknownDecision_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.AppendReviewerNoteAsync("unknown-id",
                new AppendIngestionReviewerNoteRequest { Content = "Note" }, "actor", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INGESTION_DECISION_NOT_FOUND"));
        }

        [Test]
        public async Task AppendNote_EmptyContent_ReturnsMissingField()
        {
            var svc = CreateService();
            var decision = (await svc.IngestDecisionAsync(ApprovedKycRequest("sub-nc"), "actor", "corr")).Decision!;
            var result = await svc.AppendReviewerNoteAsync(decision.DecisionId,
                new AppendIngestionReviewerNoteRequest { Content = "" }, "actor", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task AppendNote_AppendsToDecisionNotesList()
        {
            var svc = CreateService();
            var decision = (await svc.IngestDecisionAsync(ApprovedKycRequest("sub-notes-list"), "actor", "corr")).Decision!;
            await svc.AppendReviewerNoteAsync(decision.DecisionId, new AppendIngestionReviewerNoteRequest { Content = "Note 1" }, "rev1", "corr");
            await svc.AppendReviewerNoteAsync(decision.DecisionId, new AppendIngestionReviewerNoteRequest { Content = "Note 2" }, "rev2", "corr");

            var fetched = await svc.GetDecisionAsync(decision.DecisionId, "corr");
            Assert.That(fetched.Decision!.ReviewerNotes, Has.Count.EqualTo(2));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Cohort management
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpsertCohort_ValidRequest_Succeeds()
        {
            var svc = CreateService();
            var req = new UpsertCohortRequest { CohortId = "cohort-001", CohortName = "Token A Investors", SubjectIds = { "sub-1", "sub-2" } };
            var result = await svc.UpsertCohortAsync(req, "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.SubjectCount, Is.EqualTo(2));
        }

        [Test]
        public async Task UpsertCohort_EmptyCohortId_ReturnsMissingField()
        {
            var svc = CreateService();
            var result = await svc.UpsertCohortAsync(new UpsertCohortRequest { CohortId = "" }, "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task UpsertCohort_AddSubjects_Accumulates()
        {
            var svc = CreateService();
            await svc.UpsertCohortAsync(new UpsertCohortRequest { CohortId = "c-acc", SubjectIds = { "s-1", "s-2" } }, "corr");
            var r2 = await svc.UpsertCohortAsync(new UpsertCohortRequest { CohortId = "c-acc", SubjectIds = { "s-3" } }, "corr");

            Assert.That(r2.SubjectCount, Is.EqualTo(3));
        }

        [Test]
        public async Task GetCohortReadiness_UnknownCohort_ReturnsNotFound()
        {
            var svc = CreateService();
            var result = await svc.GetCohortReadinessAsync("unknown-cohort", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INGESTION_COHORT_NOT_FOUND"));
        }

        [Test]
        public async Task CohortReadiness_AllReady_IsReady()
        {
            var svc = CreateService();
            foreach (var sub in new[] { "c1-s1", "c1-s2" })
            {
                await svc.IngestDecisionAsync(ApprovedKycRequest(sub, contextId: "ctx-c1"), "actor", "corr");
                await svc.IngestDecisionAsync(ApprovedAmlRequest(sub, contextId: "ctx-c1"), "actor", "corr");
            }
            await svc.UpsertCohortAsync(new UpsertCohortRequest { CohortId = "c-ready", SubjectIds = { "c1-s1", "c1-s2" } }, "corr");

            var result = await svc.GetCohortReadinessAsync("c-ready", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.CohortReadiness!.OverallReadinessState, Is.EqualTo(IngestionReadinessState.Ready));
            Assert.That(result.CohortReadiness.TotalSubjects, Is.EqualTo(2));
        }

        [Test]
        public async Task CohortReadiness_OneBlocked_IsBlocked()
        {
            var svc = CreateService();
            // s-ok is ready
            await svc.IngestDecisionAsync(ApprovedKycRequest("c2-ok"), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("c2-ok"), "actor", "corr");
            // s-blocked is rejected
            var blocked = ApprovedKycRequest("c2-blocked");
            blocked.Status = NormalizedIngestionStatus.Rejected;
            await svc.IngestDecisionAsync(blocked, "actor", "corr");

            await svc.UpsertCohortAsync(new UpsertCohortRequest { CohortId = "c-mixed", SubjectIds = { "c2-ok", "c2-blocked" } }, "corr");
            var result = await svc.GetCohortReadinessAsync("c-mixed", "corr");

            Assert.That(result.CohortReadiness!.OverallReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
        }

        [Test]
        public async Task CohortReadiness_SubjectCountByState_IsCorrect()
        {
            var svc = CreateService();
            // s1 → Ready
            await svc.IngestDecisionAsync(ApprovedKycRequest("cs-s1"), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("cs-s1"), "actor", "corr");
            // s2 → EvidenceMissing (no decisions)

            await svc.UpsertCohortAsync(new UpsertCohortRequest { CohortId = "c-counts", SubjectIds = { "cs-s1", "cs-s2" } }, "corr");
            var result = await svc.GetCohortReadinessAsync("c-counts", "corr");

            var counts = result.CohortReadiness!.SubjectCountByState;
            Assert.That(counts.GetValueOrDefault(IngestionReadinessState.Ready), Is.EqualTo(1));
            Assert.That(counts.GetValueOrDefault(IngestionReadinessState.EvidenceMissing), Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Blockers endpoint
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetBlockers_AllApproved_EmptyBlockers()
        {
            var svc = CreateService();
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-bl-ok"), "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-bl-ok"), "actor", "corr");

            var result = await svc.GetSubjectBlockersAsync("sub-bl-ok", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.HardBlockers, Is.Empty);
            Assert.That(result.ReadinessState, Is.EqualTo(IngestionReadinessState.Ready));
        }

        [Test]
        public async Task GetBlockers_Rejected_HardBlockerPresent()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-bl-rej");
            req.Status = NormalizedIngestionStatus.Rejected;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectBlockersAsync("sub-bl-rej", "corr");

            Assert.That(result.HardBlockers, Has.Count.GreaterThan(0));
            Assert.That(result.HardBlockers.All(b => b.IsHardBlocker), Is.True);
        }

        [Test]
        public async Task GetBlockers_NeedsReview_Advisory()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-bl-nr");
            req.Status = NormalizedIngestionStatus.NeedsReview;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var result = await svc.GetSubjectBlockersAsync("sub-bl-nr", "corr");

            Assert.That(result.HardBlockers, Is.Empty);
            Assert.That(result.Advisories, Has.Count.GreaterThan(0));
            Assert.That(result.Advisories[0].IsHardBlocker, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — CheckSummary per kind
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_CheckSummaryContainsLatestPerKind()
        {
            var svc = CreateService();
            // Ingest first KYC (NeedsReview), then a replacement KYC (Approved) with different idempotency key
            var r1 = ApprovedKycRequest("sub-cs", idempotencyKey: "key-kyc-nr");
            r1.Status = NormalizedIngestionStatus.NeedsReview;
            await svc.IngestDecisionAsync(r1, "actor", "corr");

            var r2 = ApprovedKycRequest("sub-cs", idempotencyKey: "key-kyc-approved");
            await svc.IngestDecisionAsync(r2, "actor", "corr");
            await svc.IngestDecisionAsync(ApprovedAmlRequest("sub-cs"), "actor", "corr");

            var result = await svc.GetSubjectReadinessAsync("sub-cs", "corr");
            var checkSummary = result.Readiness!.CheckSummary;

            Assert.That(checkSummary.ContainsKey(IngestionDecisionKind.IdentityKyc), Is.True);
            Assert.That(checkSummary.ContainsKey(IngestionDecisionKind.AmlSanctions), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — AllProviders ingested correctly
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(IngestionProviderType.Onfido)]
        [TestCase(IngestionProviderType.Jumio)]
        [TestCase(IngestionProviderType.StripeIdentity)]
        [TestCase(IngestionProviderType.ComplyAdvantage)]
        [TestCase(IngestionProviderType.WorldCheck)]
        [TestCase(IngestionProviderType.Manual)]
        [TestCase(IngestionProviderType.Internal)]
        public async Task IngestDecision_AllProviders_AcceptedAndNormalised(IngestionProviderType provider)
        {
            var svc = CreateService();
            var req = ApprovedKycRequest($"sub-prov-{provider}");
            req.Provider = provider;
            var result = await svc.IngestDecisionAsync(req, "actor", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decision!.Provider, Is.EqualTo(provider));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — All IngestionDecisionKind accepted
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(IngestionDecisionKind.IdentityKyc)]
        [TestCase(IngestionDecisionKind.AmlSanctions)]
        [TestCase(IngestionDecisionKind.JurisdictionCheck)]
        [TestCase(IngestionDecisionKind.DocumentReview)]
        [TestCase(IngestionDecisionKind.RiskScoring)]
        [TestCase(IngestionDecisionKind.ManualReview)]
        [TestCase(IngestionDecisionKind.Combined)]
        [TestCase(IngestionDecisionKind.AdverseMedia)]
        [TestCase(IngestionDecisionKind.PepScreening)]
        public async Task IngestDecision_AllKinds_AcceptedAndNormalised(IngestionDecisionKind kind)
        {
            var svc = CreateService();
            var req = ApprovedKycRequest($"sub-kind-{kind}");
            req.Kind = kind;
            var result = await svc.IngestDecisionAsync(req, "actor", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Decision!.Kind, Is.EqualTo(kind));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Regression tests — Never emit Ready when evidence missing or expired
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Regression_NoDecisions_NeverReady()
        {
            var svc = CreateService();
            var r = await svc.GetSubjectReadinessAsync("never-ready", "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.Not.EqualTo(IngestionReadinessState.Ready));
        }

        [Test]
        public async Task Regression_ProviderUnavailable_NeverReady()
        {
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-pu-never");
            req.Status = NormalizedIngestionStatus.ProviderUnavailable;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var r = await svc.GetSubjectReadinessAsync("sub-pu-never", "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.Not.EqualTo(IngestionReadinessState.Ready));
        }

        [Test]
        public async Task Regression_ExpiredEvidence_NeverReady()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            await svc.IngestDecisionAsync(ApprovedKycRequest("sub-exp-never", validityHours: 1), "actor", "corr");
            tp.Advance(TimeSpan.FromHours(2)); // expired

            var r = await svc.GetSubjectReadinessAsync("sub-exp-never", "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.Not.EqualTo(IngestionReadinessState.Ready));
        }

        [Test]
        public async Task Regression_ProviderRawStatus_NotUsedInBusinessLogic()
        {
            // Even if raw status says "pass", normalised Rejected → Blocked
            var svc = CreateService();
            var req = ApprovedKycRequest("sub-raw");
            req.ProviderRawStatus = "pass";
            req.Status = NormalizedIngestionStatus.Rejected;
            await svc.IngestDecisionAsync(req, "actor", "corr");

            var r = await svc.GetSubjectReadinessAsync("sub-raw", "corr");
            Assert.That(r.Readiness!.ReadinessState, Is.EqualTo(IngestionReadinessState.Blocked));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — HTTP pipeline via WebApplicationFactory
        // ═══════════════════════════════════════════════════════════════════════

        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _authClient = null!;
        private HttpClient _unauthClient = null!;

        [OneTimeSetUp]
        public async Task IntegrationSetUp()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                            ["KeyManagementConfig:Provider"] = "Hardcoded",
                            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForKycAmlIngestionTests32CharsMin!",
                            ["JwtConfig:SecretKey"] = "KycAmlIngestionTestSecretKey32CharsRequired!",
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
                            ["KycConfig:MockAutoApprove"] = "true"
                        });
                    });
                });

            // Unauthenticated client
            _unauthClient = _factory.CreateClient();

            // Register and login to get a JWT for authenticated endpoint tests
            var email = $"kyc-aml-ingestion-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "TestPass123!",
                ConfirmPassword = "TestPass123!"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var jwtToken = regBody?.AccessToken ?? string.Empty;

            _authClient = _factory.CreateClient();
            _authClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        }

        [OneTimeTearDown]
        public void IntegrationTearDown()
        {
            _authClient?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task Http_IngestDecision_Unauthenticated_Returns401()
        {
            var req = new { subjectId = "sub-http", contextId = "ctx-001", kind = 0, provider = 0, status = 1 };
            var response = await _unauthClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/decisions", req);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Http_IngestDecision_Authenticated_ReturnsOk()
        {
            var req = new
            {
                subjectId = "http-sub-001",
                contextId = "http-ctx-001",
                kind = (int)IngestionDecisionKind.IdentityKyc,
                provider = (int)IngestionProviderType.StripeIdentity,
                providerReferenceId = "stripe-http-001",
                status = (int)NormalizedIngestionStatus.Approved,
                rationale = "Verified by HTTP test"
            };

            var response = await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/decisions", req);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body, Is.Not.Null);
            var root = body!.RootElement;
            Assert.That(root.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("decision").GetProperty("subjectId").GetString(), Is.EqualTo("http-sub-001"));
        }

        [Test]
        public async Task Http_IngestDecision_MissingBody_Returns400()
        {
            var response = await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/decisions", (object?)null);
            Assert.That((int)response.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task Http_GetDecision_RoundTrip()
        {
            // Ingest
            var ingestReq = new
            {
                subjectId = "http-rt-sub",
                contextId = "http-rt-ctx",
                kind = (int)IngestionDecisionKind.AmlSanctions,
                provider = (int)IngestionProviderType.ComplyAdvantage,
                status = (int)NormalizedIngestionStatus.Approved
            };
            var ingestResponse = await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/decisions", ingestReq);
            var ingestBody = await ingestResponse.Content.ReadFromJsonAsync<JsonDocument>();
            var decisionId = ingestBody!.RootElement.GetProperty("decision").GetProperty("decisionId").GetString();

            // Retrieve
            var getResponse = await _authClient.GetAsync($"/api/v1/kyc-aml-ingestion/decisions/{decisionId}");
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var getBody = await getResponse.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(getBody!.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(getBody.RootElement.GetProperty("decision").GetProperty("decisionId").GetString(), Is.EqualTo(decisionId));
        }

        [Test]
        public async Task Http_GetDecision_NotFound_Returns404()
        {
            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/decisions/nonexistent-decision-id-xyz");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Http_GetSubjectReadiness_ReturnsOk()
        {
            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/subjects/http-readiness-subject/readiness");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body!.RootElement.GetProperty("success").GetBoolean(), Is.True);
            // No decisions → EvidenceMissing
            Assert.That(body.RootElement.GetProperty("readiness").GetProperty("readinessState").GetInt32(),
                Is.EqualTo((int)IngestionReadinessState.EvidenceMissing));
        }

        [Test]
        public async Task Http_GetSubjectBlockers_ReturnsOk()
        {
            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/subjects/http-blockers-subject/blockers");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Http_GetSubjectTimeline_ReturnsOk()
        {
            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/subjects/http-timeline-subject/timeline");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Http_ListSubjectDecisions_ReturnsOk()
        {
            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/subjects/http-list-subject/decisions");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Http_UpsertCohort_ReturnsOk()
        {
            var req = new { cohortId = "http-cohort-001", cohortName = "Test Cohort", subjectIds = new[] { "c-sub-1", "c-sub-2" } };
            var response = await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/cohorts", req);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body!.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(body.RootElement.GetProperty("subjectCount").GetInt32(), Is.EqualTo(2));
        }

        [Test]
        public async Task Http_GetCohortReadiness_AfterUpsert_ReturnsOk()
        {
            var upsertReq = new { cohortId = "http-cohort-002", subjectIds = new[] { "c2-sub-1" } };
            await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/cohorts", upsertReq);

            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/cohorts/http-cohort-002/readiness");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(body!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Http_GetCohortReadiness_NotFound_Returns404()
        {
            var response = await _authClient.GetAsync("/api/v1/kyc-aml-ingestion/cohorts/nonexistent-cohort-xyz/readiness");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Http_AppendReviewerNote_ReturnsOk()
        {
            // Ingest a decision first
            var ingestReq = new
            {
                subjectId = "http-note-sub",
                contextId = "http-note-ctx",
                kind = (int)IngestionDecisionKind.IdentityKyc,
                provider = (int)IngestionProviderType.Manual,
                status = (int)NormalizedIngestionStatus.NeedsReview
            };
            var ingestResp = await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/decisions", ingestReq);
            var ingestBody = await ingestResp.Content.ReadFromJsonAsync<JsonDocument>();
            var decisionId = ingestBody!.RootElement.GetProperty("decision").GetProperty("decisionId").GetString();

            // Append a note
            var noteReq = new { content = "Reviewed and verified by compliance team." };
            var noteResp = await _authClient.PostAsJsonAsync($"/api/v1/kyc-aml-ingestion/decisions/{decisionId}/notes", noteReq);
            Assert.That(noteResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var noteBody = await noteResp.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.That(noteBody!.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(noteBody.RootElement.GetProperty("note").GetProperty("content").GetString(),
                Is.EqualTo("Reviewed and verified by compliance team."));
        }

        [Test]
        public async Task Http_AppendReviewerNote_UnknownDecision_Returns404()
        {
            var noteReq = new { content = "Note for unknown decision" };
            var response = await _authClient.PostAsJsonAsync("/api/v1/kyc-aml-ingestion/decisions/nonexistent-decision/notes", noteReq);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Http_Swagger_IsAccessible()
        {
            var response = await _authClient.GetAsync("/swagger/v1/swagger.json");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}

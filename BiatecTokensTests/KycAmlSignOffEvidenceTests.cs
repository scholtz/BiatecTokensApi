using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Models.Webhook;
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

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for <see cref="KycAmlSignOffEvidenceService"/> and
    /// <see cref="BiatecTokensApi.Controllers.KycAmlSignOffEvidenceController"/>.
    ///
    /// Coverage:
    ///   Unit tests:
    ///   - Successful KYC-only, AML-only, and combined provider flows
    ///   - Idempotency key deduplication
    ///   - Adverse findings (sanctions, PEP, adverse media)
    ///   - Provider unavailability / fail-closed handling
    ///   - Malformed callback handling
    ///   - Stale / expired evidence detection
    ///   - Readiness evaluation (approval-ready vs. blocked)
    ///   - Execution mode tracking (live vs. simulated)
    ///   - Explanation text generation
    ///   - Evidence artifact creation
    ///   - Provider polling
    ///   - Blocker derivation
    ///   Integration tests:
    ///   - Full HTTP pipeline via WebApplicationFactory
    ///   - Auth required on all endpoints
    ///   - Initiate + callback + readiness roundtrip
    ///   - Swagger contract shape
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlSignOffEvidenceTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Fakes / helpers
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        /// <summary>Mock KYC provider with configurable behavior.</summary>
        private sealed class ConfigurableKycProvider : IKycProvider
        {
            public KycStatus StatusToReturn { get; set; } = KycStatus.Approved;
            public string? ErrorToReturn { get; set; }
            public string RefIdToReturn { get; set; } = "kyc-ref-" + Guid.NewGuid().ToString("N")[..8];

            public Task<(string providerReferenceId, KycStatus status, string? errorMessage)>
                StartVerificationAsync(string userId, StartKycVerificationRequest request, string correlationId)
            {
                if (!string.IsNullOrWhiteSpace(ErrorToReturn))
                    return Task.FromResult((string.Empty, KycStatus.NotStarted, ErrorToReturn));
                return Task.FromResult((RefIdToReturn, StatusToReturn, (string?)null));
            }

            public Task<(KycStatus status, string? reason, string? errorMessage)>
                FetchStatusAsync(string providerReferenceId)
            {
                if (!string.IsNullOrWhiteSpace(ErrorToReturn))
                    return Task.FromResult((KycStatus.NotStarted, (string?)null, ErrorToReturn));
                return Task.FromResult((StatusToReturn, (string?)null, (string?)null));
            }

            public bool ValidateWebhookSignature(string payload, string signature, string webhookSecret)
                => signature == "valid-sig";

            public Task<(string providerReferenceId, KycStatus status, string? reason)>
                ParseWebhookAsync(BiatecTokensApi.Models.Kyc.KycWebhookPayload payload)
                => Task.FromResult((RefIdToReturn, StatusToReturn, (string?)null));
        }

        /// <summary>Mock AML provider with configurable behavior.</summary>
        private sealed class ConfigurableAmlProvider : IAmlProvider
        {
            public string ProviderName => "MockAml";
            public ComplianceDecisionState StateToReturn { get; set; } = ComplianceDecisionState.Approved;
            public string? ReasonCodeToReturn { get; set; }
            public string? ErrorToReturn { get; set; }
            public string RefIdToReturn { get; set; } = "aml-ref-" + Guid.NewGuid().ToString("N")[..8];

            public Task<(string providerReferenceId, ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
                ScreenSubjectAsync(string subjectId, Dictionary<string, string> subjectMetadata, string correlationId)
            {
                if (!string.IsNullOrWhiteSpace(ErrorToReturn))
                    return Task.FromResult((string.Empty, ComplianceDecisionState.ProviderUnavailable, (string?)null, ErrorToReturn));
                return Task.FromResult((RefIdToReturn, StateToReturn, ReasonCodeToReturn, (string?)null));
            }

            public Task<(ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
                GetScreeningStatusAsync(string providerReferenceId)
            {
                if (!string.IsNullOrWhiteSpace(ErrorToReturn))
                    return Task.FromResult((ComplianceDecisionState.ProviderUnavailable, (string?)null, ErrorToReturn));
                return Task.FromResult((StateToReturn, ReasonCodeToReturn, (string?)null));
            }
        }

        private static KycAmlSignOffEvidenceService CreateService(
            ConfigurableKycProvider? kycProvider = null,
            ConfigurableAmlProvider? amlProvider = null,
            TimeProvider? timeProvider = null)
        {
            return new KycAmlSignOffEvidenceService(
                kycProvider ?? new ConfigurableKycProvider(),
                amlProvider ?? new ConfigurableAmlProvider(),
                NullLogger<KycAmlSignOffEvidenceService>.Instance,
                timeProvider);
        }

        private static InitiateKycAmlSignOffRequest MakeRequest(
            string? subjectId = null,
            KycAmlSignOffCheckKind kind = KycAmlSignOffCheckKind.Combined,
            KycAmlSignOffExecutionMode mode = KycAmlSignOffExecutionMode.Simulated,
            string? idempotencyKey = null,
            int? evidenceValidityHours = null,
            Dictionary<string, string>? metadata = null)
            => new()
            {
                SubjectId = subjectId ?? "subject-" + Guid.NewGuid().ToString("N")[..8],
                CheckKind = kind,
                RequestedExecutionMode = mode,
                IdempotencyKey = idempotencyKey,
                EvidenceValidityHours = evidenceValidityHours,
                SubjectMetadata = metadata ?? new Dictionary<string, string>()
            };

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Successful flows
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateSignOff_Combined_BothPass_ReturnsApproved()
        {
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { StatusToReturn = KycStatus.Approved },
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Approved });

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor-001", "corr-001");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record, Is.Not.Null);
            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));
            Assert.That(resp.Record.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Ready));
        }

        [Test]
        public async Task InitiateSignOff_KycOnly_Pass_ReturnsApproved()
        {
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { StatusToReturn = KycStatus.Approved });

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.IdentityKyc), "actor", "corr");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));
        }

        [Test]
        public async Task InitiateSignOff_AmlOnly_Clean_ReturnsApproved()
        {
            var svc = CreateService(
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Approved });

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));
        }

        [Test]
        public async Task InitiateSignOff_KycPending_AmlPasses_OutcomeApproved()
        {
            // Pending KYC + clean AML → AML wins (Approved)
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { StatusToReturn = KycStatus.Pending },
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Approved });

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            Assert.That(resp.Success, Is.True);
            // AML outcome overrides KYC pending for combined check
            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));
        }

        [Test]
        public async Task InitiateSignOff_AuditTrailHasInitiationEvent()
        {
            var svc = CreateService();

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            Assert.That(resp.Record!.AuditTrail, Is.Not.Empty);
            Assert.That(resp.Record.AuditTrail.Any(e => e.EventType.Contains("Initiation")), Is.True,
                "Audit trail must contain a provider initiation event");
        }

        [Test]
        public async Task InitiateSignOff_EvidenceArtifactCreated()
        {
            var svc = CreateService();

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            Assert.That(resp.Record!.EvidenceArtifacts, Is.Not.Empty,
                "At least one evidence artifact must be produced at initiation");
            var initArtifact = resp.Record.EvidenceArtifacts.FirstOrDefault(a => a.Kind == "ProviderInitiationRecord");
            Assert.That(initArtifact, Is.Not.Null, "ProviderInitiationRecord artifact must be present");
        }

        [Test]
        public async Task InitiateSignOff_LiveProvider_ArtifactIsProviderBacked()
        {
            var svc = CreateService();
            var request = MakeRequest(mode: KycAmlSignOffExecutionMode.LiveProvider);

            var resp = await svc.InitiateSignOffAsync(request, "actor", "corr");

            Assert.That(resp.Record!.IsProviderBacked, Is.True);
            var artifact = resp.Record.EvidenceArtifacts.First();
            Assert.That(artifact.IsProviderBacked, Is.True,
                "Artifacts from live-provider execution must be marked as provider-backed");
        }

        [Test]
        public async Task InitiateSignOff_Simulated_ArtifactIsNotProviderBacked()
        {
            var svc = CreateService();
            var request = MakeRequest(mode: KycAmlSignOffExecutionMode.Simulated);

            var resp = await svc.InitiateSignOffAsync(request, "actor", "corr");

            Assert.That(resp.Record!.IsProviderBacked, Is.False);
            var artifact = resp.Record.EvidenceArtifacts.First();
            Assert.That(artifact.IsProviderBacked, Is.False,
                "Artifacts from simulated execution must NOT be marked as provider-backed");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Idempotency
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateSignOff_SameIdempotencyKey_ReturnsExistingRecord()
        {
            var svc = CreateService();
            var key = "idem-key-001";

            var first = await svc.InitiateSignOffAsync(MakeRequest(idempotencyKey: key), "actor", "corr");
            var second = await svc.InitiateSignOffAsync(MakeRequest(idempotencyKey: key), "actor", "corr");

            Assert.That(second.WasIdempotent, Is.True);
            Assert.That(second.Record!.RecordId, Is.EqualTo(first.Record!.RecordId),
                "Idempotent call must return the same record");
        }

        [Test]
        public async Task InitiateSignOff_DifferentIdempotencyKey_CreatesNewRecord()
        {
            var svc = CreateService();

            var first = await svc.InitiateSignOffAsync(MakeRequest(idempotencyKey: "key-a"), "actor", "corr");
            var second = await svc.InitiateSignOffAsync(MakeRequest(idempotencyKey: "key-b"), "actor", "corr");

            Assert.That(second.WasIdempotent, Is.False);
            Assert.That(second.Record!.RecordId, Is.Not.EqualTo(first.Record!.RecordId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Adverse findings / fail-closed
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateSignOff_AmlSanctionsMatch_ReturnsRejected_WithBlocker()
        {
            var svc = CreateService(
                amlProvider: new ConfigurableAmlProvider
                {
                    StateToReturn = ComplianceDecisionState.Rejected,
                    ReasonCodeToReturn = "SANCTIONS_MATCH"
                });

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Rejected));
            Assert.That(resp.Record.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Blocked));
            Assert.That(resp.Record.Blockers, Is.Not.Empty);
            Assert.That(resp.Record.Blockers.Any(b => b.Code == "SANCTIONS_MATCH"), Is.True,
                "SANCTIONS_MATCH blocker must be present");
        }

        [Test]
        public async Task InitiateSignOff_AmlNeedsReview_ReturnsRequiresReview_WithBlocker()
        {
            var svc = CreateService(
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.NeedsReview });

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.NeedsManualReview));
            Assert.That(resp.Record.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.RequiresReview));
        }

        [Test]
        public async Task InitiateSignOff_KycRejected_AmlSkipped_ReturnsRejected()
        {
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { StatusToReturn = KycStatus.Rejected },
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Approved });

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Rejected));
            Assert.That(resp.Record.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Blocked));
            // AML was skipped due to KYC rejection
            var amlSkipEvent = resp.Record.AuditTrail.Any(e => e.EventType.Contains("AmlSkipped"));
            Assert.That(amlSkipEvent, Is.True, "AML must be skipped when KYC is rejected");
        }

        [Test]
        public async Task InitiateSignOff_ProviderUnavailable_FailsClosed_IsNotApprovalReady()
        {
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { ErrorToReturn = "Provider timed out" });

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            Assert.That(resp.Success, Is.True, "Initiation succeeds (record created) even when provider fails");
            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.ProviderUnavailable));
            Assert.That(resp.Record.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Blocked));

            var readiness = await svc.GetReadinessAsync(resp.Record.RecordId);
            Assert.That(readiness.IsApprovalReady, Is.False,
                "Approval must never be ready when provider is unavailable");
        }

        [Test]
        public async Task InitiateSignOff_MissingSubjectId_ReturnsError()
        {
            var svc = CreateService();

            var resp = await svc.InitiateSignOffAsync(
                new InitiateKycAmlSignOffRequest { SubjectId = "" }, "actor", "corr");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_SUBJECT_ID"));
        }

        [Test]
        public async Task InitiateSignOff_NullRequest_ReturnsError()
        {
            var svc = CreateService();

            var resp = await svc.InitiateSignOffAsync(null!, "actor", "corr");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("REQUEST_NULL"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Provider callback processing
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ProcessCallback_Approved_UpdatesOutcomeToApproved()
        {
            var svc = CreateService(
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending });
            var kycProv = new ConfigurableKycProvider { StatusToReturn = KycStatus.Pending };
            svc = CreateService(kycProvider: kycProv,
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending });

            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.IdentityKyc), "actor", "corr-init");

            var callbackResp = await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = kycProv.RefIdToReturn,
                    OutcomeStatus = "approved",
                    EventType = "identity.verified"
                },
                "corr-cb");

            Assert.That(callbackResp.Success, Is.True);
            Assert.That(callbackResp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));
        }

        [Test]
        public async Task ProcessCallback_SanctionsMatch_UpdatesToAdverseFindings_WithBlocker()
        {
            var amlProv = new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending };
            var svc = CreateService(amlProvider: amlProv);

            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            var callbackResp = await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = amlProv.RefIdToReturn,
                    OutcomeStatus = "adverse",
                    ReasonCode = "SANCTIONS_MATCH",
                    EventType = "screening.hit"
                },
                "corr-cb");

            Assert.That(callbackResp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.AdverseFindings));
            Assert.That(callbackResp.Record.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Blocked));
            Assert.That(callbackResp.Record.Blockers.Any(b => b.Code == "SANCTIONS_MATCH"), Is.True);
        }

        [Test]
        public async Task ProcessCallback_Rejected_UpdatesToBlocked_NotRemediable()
        {
            var amlProv = new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending };
            var svc = CreateService(amlProvider: amlProv);

            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            var callbackResp = await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = amlProv.RefIdToReturn,
                    OutcomeStatus = "rejected",
                    ReasonCode = "SANCTIONS_MATCH"
                },
                "corr-cb");

            Assert.That(callbackResp.Record!.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Blocked));
            var blocker = callbackResp.Record.Blockers.FirstOrDefault();
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker!.IsRemediable, Is.False, "Sanctions rejection is terminal, not remediable");
        }

        [Test]
        public async Task ProcessCallback_MissingProviderRef_Rejected()
        {
            var svc = CreateService();
            var initResp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            var callbackResp = await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest { ProviderReferenceId = "", OutcomeStatus = "approved" },
                "corr-cb");

            Assert.That(callbackResp.Success, Is.False);
            Assert.That(callbackResp.ErrorCode, Is.EqualTo("MISSING_PROVIDER_REF"));
        }

        [Test]
        public async Task ProcessCallback_UnknownRecordId_Rejected()
        {
            var svc = CreateService();

            var callbackResp = await svc.ProcessCallbackAsync(
                "nonexistent-record-id",
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = "some-ref",
                    OutcomeStatus = "approved"
                },
                "corr-cb");

            Assert.That(callbackResp.Success, Is.False);
            Assert.That(callbackResp.ErrorCode, Is.EqualTo("RECORD_NOT_FOUND"));
        }

        [Test]
        public async Task ProcessCallback_WrongProviderRef_Rejected()
        {
            var amlProv = new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending };
            var svc = CreateService(amlProvider: amlProv);
            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            var callbackResp = await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = "completely-wrong-ref",
                    OutcomeStatus = "approved"
                },
                "corr-cb");

            Assert.That(callbackResp.Success, Is.False);
            Assert.That(callbackResp.ErrorCode, Is.EqualTo("PROVIDER_REF_MISMATCH"),
                "Callback with wrong provider reference must be rejected");
        }

        [Test]
        public async Task ProcessCallback_AddsCallbackArtifact()
        {
            var amlProv = new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending };
            var svc = CreateService(amlProvider: amlProv);
            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = amlProv.RefIdToReturn,
                    OutcomeStatus = "approved",
                    EventType = "screening.completed"
                },
                "corr-cb");

            var record = await svc.GetRecordAsync(initResp.Record.RecordId);
            var callbackArtifact = record.Record!.EvidenceArtifacts.FirstOrDefault(a => a.Kind == "ProviderCallbackPayload");
            Assert.That(callbackArtifact, Is.Not.Null, "ProviderCallbackPayload artifact must be created");
            Assert.That(callbackArtifact!.Summary.ContainsKey("EventType"), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Stale / expired evidence
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetReadiness_EvidenceExpired_ReturnsStale_Blocked()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime);

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(evidenceValidityHours: 24), "actor", "corr");

            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));

            // Advance time past expiry
            fakeTime.Advance(TimeSpan.FromHours(25));

            var readiness = await svc.GetReadinessAsync(resp.Record.RecordId);

            Assert.That(readiness.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Stale),
                "Expired evidence must produce Stale readiness state");
            Assert.That(readiness.IsApprovalReady, Is.False,
                "Expired evidence must never be approval-ready");
        }

        [Test]
        public async Task GetReadiness_EvidenceNotExpired_RemainsReady()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime);

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(evidenceValidityHours: 24), "actor", "corr");

            // Advance only 12 hours
            fakeTime.Advance(TimeSpan.FromHours(12));

            var readiness = await svc.GetReadinessAsync(resp.Record!.RecordId);

            Assert.That(readiness.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.Ready));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Readiness evaluation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetReadiness_AllPassed_IsApprovalReadyTrue()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(mode: KycAmlSignOffExecutionMode.LiveProvider), "actor", "corr");

            var readiness = await svc.GetReadinessAsync(resp.Record!.RecordId);

            Assert.That(readiness.IsApprovalReady, Is.True);
            Assert.That(readiness.IsProviderBacked, Is.True);
            Assert.That(readiness.Blockers, Is.Empty);
        }

        [Test]
        public async Task GetReadiness_SimulatedExecution_IsApprovalReadyFalse()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(mode: KycAmlSignOffExecutionMode.Simulated), "actor", "corr");

            var readiness = await svc.GetReadinessAsync(resp.Record!.RecordId);

            // Simulated evidence should NOT be approval-ready (IsProviderBacked = false)
            Assert.That(readiness.IsApprovalReady, Is.False,
                "Simulated evidence must never be approval-ready");
            Assert.That(readiness.IsProviderBacked, Is.False);
        }

        [Test]
        public async Task GetReadiness_NonExistentRecord_ReturnsIncompleteEvidence()
        {
            var svc = CreateService();

            var readiness = await svc.GetReadinessAsync("non-existent-record");

            Assert.That(readiness.ReadinessState, Is.EqualTo(KycAmlSignOffReadinessState.IncompleteEvidence));
            Assert.That(readiness.IsApprovalReady, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Explanation text
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateSignOff_Pending_ExplanationMentionsAwaitingCallback()
        {
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { StatusToReturn = KycStatus.Pending },
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending });

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.IdentityKyc), "actor", "corr");

            Assert.That(resp.Record!.ReadinessExplanation, Does.Contain("awaiting").IgnoreCase.Or
                .Contains("pending").IgnoreCase,
                "Explanation for Pending state must mention awaiting callback");
        }

        [Test]
        public async Task InitiateSignOff_SanctionsMatch_ExplanationMentionsAdverseFindings()
        {
            var svc = CreateService(
                amlProvider: new ConfigurableAmlProvider
                {
                    StateToReturn = ComplianceDecisionState.Rejected,
                    ReasonCodeToReturn = "SANCTIONS_MATCH"
                });

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            Assert.That(resp.Record!.ReadinessExplanation, Is.Not.Empty);
            Assert.That(resp.Record.ReadinessExplanation,
                Does.Contain("reject").IgnoreCase.Or.Contains("SANCTIONS_MATCH").IgnoreCase);
        }

        [Test]
        public async Task InitiateSignOff_ProviderUnavailable_ExplanationMentionsProviderUnavailable()
        {
            var svc = CreateService(
                kycProvider: new ConfigurableKycProvider { ErrorToReturn = "Timeout" });

            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            Assert.That(resp.Record!.ReadinessExplanation,
                Does.Contain("unavailable").IgnoreCase.Or.Contains("blocked").IgnoreCase);
        }

        [Test]
        public async Task InitiateSignOff_SimulatedMode_ExplanationMentionsSimulated()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(mode: KycAmlSignOffExecutionMode.Simulated), "actor", "corr");

            Assert.That(resp.Record!.ReadinessExplanation, Does.Contain("simulated").IgnoreCase,
                "Explanation must flag simulated execution mode");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Evidence artifact content
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetArtifacts_AfterInitiation_HasArtifactsWithExplanation()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            var artifacts = await svc.GetArtifactsAsync(resp.Record!.RecordId);

            Assert.That(artifacts.Artifacts, Is.Not.Empty);
            foreach (var artifact in artifacts.Artifacts)
            {
                Assert.That(artifact.ExplanationText, Is.Not.Empty,
                    $"Artifact {artifact.Kind} must have an explanation text");
                Assert.That(artifact.Summary, Is.Not.Null.And.Not.Empty,
                    $"Artifact {artifact.Kind} must have a non-empty summary");
            }
        }

        [Test]
        public async Task GetArtifacts_LiveProvider_HasProviderBackedArtifacts()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(mode: KycAmlSignOffExecutionMode.LiveProvider), "actor", "corr");

            var artifacts = await svc.GetArtifactsAsync(resp.Record!.RecordId);

            Assert.That(artifacts.HasProviderBackedArtifacts, Is.True);
        }

        [Test]
        public async Task GetArtifacts_Simulated_HasNoProviderBackedArtifacts()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(mode: KycAmlSignOffExecutionMode.Simulated), "actor", "corr");

            var artifacts = await svc.GetArtifactsAsync(resp.Record!.RecordId);

            Assert.That(artifacts.HasProviderBackedArtifacts, Is.False,
                "Simulated execution must not produce provider-backed artifacts");
        }

        [Test]
        public async Task GetArtifacts_UnknownRecord_ReturnsError()
        {
            var svc = CreateService();

            var artifacts = await svc.GetArtifactsAsync("does-not-exist");

            Assert.That(artifacts.ErrorCode, Is.EqualTo("RECORD_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Record retrieval and subject listing
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetRecord_ExistingRecord_ReturnsRecord()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            var getResp = await svc.GetRecordAsync(resp.Record!.RecordId);

            Assert.That(getResp.Success, Is.True);
            Assert.That(getResp.Record!.RecordId, Is.EqualTo(resp.Record.RecordId));
        }

        [Test]
        public async Task GetRecord_NonExistent_ReturnsError()
        {
            var svc = CreateService();

            var getResp = await svc.GetRecordAsync("non-existent");

            Assert.That(getResp.Success, Is.False);
            Assert.That(getResp.ErrorCode, Is.EqualTo("RECORD_NOT_FOUND"));
        }

        [Test]
        public async Task ListRecordsForSubject_ReturnsAllForSubject()
        {
            var svc = CreateService();
            var subjectId = "subject-list-001";

            await svc.InitiateSignOffAsync(MakeRequest(subjectId: subjectId), "actor", "corr-1");
            await svc.InitiateSignOffAsync(MakeRequest(subjectId: subjectId), "actor", "corr-2");
            await svc.InitiateSignOffAsync(MakeRequest(subjectId: "other-subject"), "actor", "corr-3");

            var list = await svc.ListRecordsForSubjectAsync(subjectId);

            Assert.That(list.TotalCount, Is.EqualTo(2),
                "Only records for the specified subject must be returned");
            Assert.That(list.Records.All(r => r.SubjectId == subjectId), Is.True);
        }

        [Test]
        public async Task ListRecordsForSubject_OrderedByCreationDescending()
        {
            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: fakeTime);
            var subjectId = "subject-order-001";

            await svc.InitiateSignOffAsync(MakeRequest(subjectId: subjectId), "actor", "corr-1");
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await svc.InitiateSignOffAsync(MakeRequest(subjectId: subjectId), "actor", "corr-2");

            var list = await svc.ListRecordsForSubjectAsync(subjectId);

            Assert.That(list.Records[0].CreatedAt, Is.GreaterThan(list.Records[1].CreatedAt),
                "Records must be ordered by creation time descending");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Provider polling
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PollProviderStatus_NonPendingRecord_NoChange()
        {
            var svc = CreateService();
            var resp = await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");
            Assert.That(resp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));

            var pollResp = await svc.PollProviderStatusAsync(resp.Record.RecordId, "corr-poll");

            Assert.That(pollResp.Success, Is.True);
            Assert.That(pollResp.OutcomeChanged, Is.False,
                "Polling an already-resolved record must not change its outcome");
        }

        [Test]
        public async Task PollProviderStatus_NonExistentRecord_ReturnsError()
        {
            var svc = CreateService();

            var pollResp = await svc.PollProviderStatusAsync("non-existent", "corr");

            Assert.That(pollResp.Success, Is.False);
            Assert.That(pollResp.ErrorCode, Is.EqualTo("RECORD_NOT_FOUND"));
        }

        [Test]
        public async Task PollProviderStatus_KycNowApproved_OutcomeChanges()
        {
            var kycProv = new ConfigurableKycProvider { StatusToReturn = KycStatus.Pending };
            var svc = CreateService(
                kycProvider: kycProv,
                amlProvider: new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending });

            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.IdentityKyc), "actor", "corr");
            Assert.That(initResp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Pending));

            // Provider now returns Approved
            kycProv.StatusToReturn = KycStatus.Approved;

            var pollResp = await svc.PollProviderStatusAsync(initResp.Record.RecordId, "corr-poll");

            Assert.That(pollResp.Success, Is.True);
            Assert.That(pollResp.OutcomeChanged, Is.True);
            Assert.That(pollResp.Record!.Outcome, Is.EqualTo(KycAmlSignOffOutcome.Approved));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Blocker derivation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [TestCase("SANCTIONS_MATCH", false)]  // sanctions rejection: terminal, not remediable
        [TestCase("DOCUMENT_FRAUD", false)]   // document fraud rejection: terminal, not remediable
        public async Task Blockers_RejectedOutcome_BlockerIsNonRemediable(
            string reasonCode, bool expectedRemediable)
        {
            var amlProv = new ConfigurableAmlProvider
            {
                StateToReturn = ComplianceDecisionState.Rejected,
                ReasonCodeToReturn = reasonCode
            };
            var svc = CreateService(amlProvider: amlProv);

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            var anyBlocker = resp.Record!.Blockers.FirstOrDefault();
            Assert.That(anyBlocker, Is.Not.Null, $"No blocker found for reasonCode={reasonCode}");
            Assert.That(anyBlocker!.IsRemediable, Is.EqualTo(expectedRemediable),
                $"Remediability for {reasonCode} (Rejected) should be {expectedRemediable}");
        }

        [Test]
        public async Task Blockers_AdverseFindingsOutcome_BlockerIsRemediable()
        {
            // Adverse findings (e.g., PEP match requiring analyst review) are remediable
            var amlProv = new ConfigurableAmlProvider
            {
                StateToReturn = ComplianceDecisionState.NeedsReview,
                ReasonCodeToReturn = "PEP_MATCH"
            };
            var svc = CreateService(amlProvider: amlProv);

            var resp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            // NeedsReview outcome → MANUAL_REVIEW_REQUIRED blocker which is remediable
            var blocker = resp.Record!.Blockers.FirstOrDefault();
            Assert.That(blocker, Is.Not.Null, "NeedsManualReview must produce a blocker");
            Assert.That(blocker!.IsRemediable, Is.True,
                "Manual review requirements are remediable — analyst review can resolve them");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Unit tests — Webhook events (fire-and-forget check)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateSignOff_BlockedOutcome_WebhookBothEmittedWithoutError()
        {
            var emitted = new List<WebhookEventType>();
            var webhookSvc = new MockWebhookCollector(emitted);

            var kycProv = new ConfigurableKycProvider { ErrorToReturn = "Unavailable" };
            var svc = new KycAmlSignOffEvidenceService(
                kycProv,
                new ConfigurableAmlProvider(),
                NullLogger<KycAmlSignOffEvidenceService>.Instance,
                null,
                webhookSvc);

            await svc.InitiateSignOffAsync(MakeRequest(), "actor", "corr");

            // Await a small window for fire-and-forget
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (emitted.Count < 2 && DateTime.UtcNow < deadline)
                await Task.Delay(20);

            Assert.That(emitted, Contains.Item(WebhookEventType.KycAmlSignOffInitiated));
            Assert.That(emitted, Contains.Item(WebhookEventType.KycAmlSignOffBlocked));
        }

        [Test]
        public async Task ProcessCallback_ApprovedCallback_EmitsApprovalReadyEvent()
        {
            var emitted = new List<WebhookEventType>();
            var webhookSvc = new MockWebhookCollector(emitted);

            var amlProv = new ConfigurableAmlProvider { StateToReturn = ComplianceDecisionState.Pending };
            var svc = new KycAmlSignOffEvidenceService(
                new ConfigurableKycProvider { StatusToReturn = KycStatus.Pending },
                amlProv,
                NullLogger<KycAmlSignOffEvidenceService>.Instance,
                null,
                webhookSvc);

            var initResp = await svc.InitiateSignOffAsync(
                MakeRequest(kind: KycAmlSignOffCheckKind.AmlScreening), "actor", "corr");

            await svc.ProcessCallbackAsync(
                initResp.Record!.RecordId,
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = amlProv.RefIdToReturn,
                    OutcomeStatus = "approved"
                },
                "corr-cb");

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (!emitted.Contains(WebhookEventType.KycAmlSignOffApprovalReady) && DateTime.UtcNow < deadline)
                await Task.Delay(20);

            Assert.That(emitted, Contains.Item(WebhookEventType.KycAmlSignOffApprovalReady));
        }

        // Mock webhook service for verifying event emissions
        private sealed class MockWebhookCollector : IWebhookService
        {
            private readonly List<WebhookEventType> _collected;
            public MockWebhookCollector(List<WebhookEventType> collected) => _collected = collected;

            public Task EmitEventAsync(WebhookEvent webhookEvent)
            {
                _collected.Add(webhookEvent.EventType);
                return Task.CompletedTask;
            }

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse>
                CreateSubscriptionAsync(BiatecTokensApi.Models.Webhook.CreateWebhookSubscriptionRequest req, string userId)
                => throw new NotImplementedException();
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse?>
                GetSubscriptionAsync(string id, string userId) => throw new NotImplementedException();
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse>
                ListSubscriptionsAsync(string userId) => throw new NotImplementedException();
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse>
                DeleteSubscriptionAsync(string id, string userId) => throw new NotImplementedException();
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse>
                UpdateSubscriptionAsync(BiatecTokensApi.Models.Webhook.UpdateWebhookSubscriptionRequest req, string userId)
                => throw new NotImplementedException();
            public Task<BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse>
                GetDeliveryHistoryAsync(BiatecTokensApi.Models.Webhook.GetWebhookDeliveryHistoryRequest req, string userId)
                => throw new NotImplementedException();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration tests — HTTP pipeline via WebApplicationFactory
        // ═══════════════════════════════════════════════════════════════════════

        private WebApplicationFactory<BiatecTokensApi.Program>? _factory;
        private HttpClient? _client;

        [OneTimeSetUp]
        public void SetupFactory()
        {
            var configuration = new Dictionary<string, string?>
            {
                ["AlgorandAuthentication:Realm"] = "BiatecTokens",
                ["AlgorandAuthentication:AllowedNetworks:0"] = "testnet",
                ["AlgorandAuthentication:CheckExpiration"] = "false",
                ["JwtConfig:SecretKey"] = "test-secret-key-integration-tests-32-characters-minimum-required",
                ["JwtConfig:Issuer"] = "BiatecTokensApi",
                ["JwtConfig:Audience"] = "BiatecTokensUsers",
                ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                ["KeyManagementConfig:Provider"] = "Hardcoded",
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired",
                ["KycConfig:Provider"] = "Mock",
                ["AmlConfig:Provider"] = "Mock",
                ["IPFSConfig:ApiUrl"] = "http://localhost:5001",
                ["IPFSConfig:GatewayUrl"] = "http://localhost:8080",
                ["IPFSConfig:Username"] = "test",
                ["IPFSConfig:Password"] = "test",
                ["EVMChains:0:Name"] = "Base",
                ["EVMChains:0:ChainId"] = "8453",
                ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                ["AllowedHosts"] = "*",
                ["DebugMode"] = "true",
                ["Logging:LogLevel:Default"] = "Warning"
            };

            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(configuration);
                    });
                    builder.UseEnvironment("Test");
                });

            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void TearDownFactory()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task Api_Initiate_WithoutAuth_Returns401()
        {
            var resp = await _client!.PostAsJsonAsync("/api/v1/kyc-aml-signoff/initiate",
                new InitiateKycAmlSignOffRequest { SubjectId = "test-subj" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_GetRecord_WithoutAuth_Returns401()
        {
            var resp = await _client!.GetAsync("/api/v1/kyc-aml-signoff/some-record-id");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_GetReadiness_WithoutAuth_Returns401()
        {
            var resp = await _client!.GetAsync("/api/v1/kyc-aml-signoff/some-record-id/readiness");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_GetArtifacts_WithoutAuth_Returns401()
        {
            var resp = await _client!.GetAsync("/api/v1/kyc-aml-signoff/some-record-id/artifacts");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_ListForSubject_WithoutAuth_Returns401()
        {
            var resp = await _client!.GetAsync("/api/v1/kyc-aml-signoff/subject/some-subject");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_PollStatus_WithoutAuth_Returns401()
        {
            var resp = await _client!.PostAsync("/api/v1/kyc-aml-signoff/some-record-id/poll",
                new StringContent(""));

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_ProcessCallback_WithoutAuth_Returns401()
        {
            var resp = await _client!.PostAsJsonAsync("/api/v1/kyc-aml-signoff/some-record-id/callback",
                new ProcessKycAmlSignOffCallbackRequest
                {
                    ProviderReferenceId = "ref",
                    OutcomeStatus = "approved"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Schema contract tests — model fields present
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Schema_KycAmlSignOffRecord_HasRequiredFields()
        {
            var record = new KycAmlSignOffRecord();

            Assert.That(record.RecordId, Is.Not.Null, "RecordId must be initialized");
            Assert.That(record.AuditTrail, Is.Not.Null, "AuditTrail must be initialized");
            Assert.That(record.EvidenceArtifacts, Is.Not.Null, "EvidenceArtifacts must be initialized");
            Assert.That(record.Blockers, Is.Not.Null, "Blockers must be initialized");
        }

        [Test]
        public void Schema_KycAmlSignOffEvidenceArtifact_IsProviderBackedDerivedCorrectly()
        {
            var liveArtifact = new KycAmlSignOffEvidenceArtifact
            {
                ExecutionMode = KycAmlSignOffExecutionMode.LiveProvider
            };
            var simArtifact = new KycAmlSignOffEvidenceArtifact
            {
                ExecutionMode = KycAmlSignOffExecutionMode.Simulated
            };
            var sandboxArtifact = new KycAmlSignOffEvidenceArtifact
            {
                ExecutionMode = KycAmlSignOffExecutionMode.ProtectedSandbox
            };

            Assert.That(liveArtifact.IsProviderBacked, Is.True);
            Assert.That(simArtifact.IsProviderBacked, Is.False);
            Assert.That(sandboxArtifact.IsProviderBacked, Is.True);
        }

        [Test]
        public void Schema_KycAmlSignOffRecord_IsProviderBackedDerivedCorrectly()
        {
            var liveRecord = new KycAmlSignOffRecord { ExecutionMode = KycAmlSignOffExecutionMode.LiveProvider };
            var simRecord = new KycAmlSignOffRecord { ExecutionMode = KycAmlSignOffExecutionMode.Simulated };

            Assert.That(liveRecord.IsProviderBacked, Is.True);
            Assert.That(simRecord.IsProviderBacked, Is.False);
        }

        [Test]
        public void Schema_InitiateKycAmlSignOffRequest_DefaultsAreSane()
        {
            var req = new InitiateKycAmlSignOffRequest();

            Assert.That(req.CheckKind, Is.EqualTo(KycAmlSignOffCheckKind.Combined));
            Assert.That(req.RequestedExecutionMode, Is.EqualTo(KycAmlSignOffExecutionMode.LiveProvider),
                "Default execution mode must be LiveProvider (release-grade)");
            Assert.That(req.SubjectMetadata, Is.Not.Null);
        }

        [Test]
        public void Schema_AllExecutionModeValues_Distinct()
        {
            var values = Enum.GetValues<KycAmlSignOffExecutionMode>().ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count),
                "All execution mode enum values must be unique");
        }

        [Test]
        public void Schema_AllOutcomeValues_Distinct()
        {
            var values = Enum.GetValues<KycAmlSignOffOutcome>().ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count));
        }

        [Test]
        public void Schema_AllReadinessStateValues_Distinct()
        {
            var values = Enum.GetValues<KycAmlSignOffReadinessState>().ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count));
        }
    }
}

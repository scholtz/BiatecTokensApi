using BiatecTokensApi.Models.KycWorkflow;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for KycWorkflowService covering state machine logic,
    /// CRUD operations, audit history, evidence management, and eligibility evaluation.
    /// </summary>
    [TestFixture]
    public class KycWorkflowServiceTests
    {
        private static KycWorkflowService CreateService() =>
            new KycWorkflowService(NullLogger<KycWorkflowService>.Instance);

        // ── ValidateTransition – valid transitions ─────────────────────────────

        [Test]
        public void ValidateTransition_NotStartedToPending_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.NotStarted, KycVerificationState.Pending);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingToManualReviewRequired_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Pending, KycVerificationState.ManualReviewRequired);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingToApproved_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Pending, KycVerificationState.Approved);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingToRejected_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Pending, KycVerificationState.Rejected);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_PendingToExpired_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Pending, KycVerificationState.Expired);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_ManualReviewRequiredToApproved_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.ManualReviewRequired, KycVerificationState.Approved);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_ManualReviewRequiredToRejected_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.ManualReviewRequired, KycVerificationState.Rejected);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_ManualReviewRequiredToPending_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.ManualReviewRequired, KycVerificationState.Pending);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_ApprovedToExpired_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Approved, KycVerificationState.Expired);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_RejectedToPending_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Rejected, KycVerificationState.Pending);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateTransition_ExpiredToPending_IsValid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Expired, KycVerificationState.Pending);
            Assert.That(result.IsValid, Is.True);
        }

        // ── ValidateTransition – invalid transitions ───────────────────────────

        [Test]
        public void ValidateTransition_NotStartedToApproved_IsInvalid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.NotStarted, KycVerificationState.Approved);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ValidateTransition_ApprovedToRejected_IsInvalid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Approved, KycVerificationState.Rejected);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateTransition_ApprovedToPending_IsInvalid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Approved, KycVerificationState.Pending);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateTransition_RejectedToApproved_IsInvalid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Rejected, KycVerificationState.Approved);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidateTransition_ExpiredToApproved_IsInvalid()
        {
            var svc = CreateService();
            var result = svc.ValidateTransition(KycVerificationState.Expired, KycVerificationState.Approved);
            Assert.That(result.IsValid, Is.False);
        }

        // ── GetAllowedTransitions ──────────────────────────────────────────────

        [Test]
        public void GetAllowedTransitions_NotStarted_ReturnsPendingOnly()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(KycVerificationState.NotStarted);
            Assert.That(allowed, Is.EquivalentTo(new[] { KycVerificationState.Pending }));
        }

        [Test]
        public void GetAllowedTransitions_Pending_ReturnsCorrectSet()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(KycVerificationState.Pending);
            Assert.That(allowed, Contains.Item(KycVerificationState.ManualReviewRequired));
            Assert.That(allowed, Contains.Item(KycVerificationState.Approved));
            Assert.That(allowed, Contains.Item(KycVerificationState.Rejected));
            Assert.That(allowed, Contains.Item(KycVerificationState.Expired));
        }

        [Test]
        public void GetAllowedTransitions_ManualReviewRequired_ReturnsCorrectSet()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(KycVerificationState.ManualReviewRequired);
            Assert.That(allowed, Contains.Item(KycVerificationState.Approved));
            Assert.That(allowed, Contains.Item(KycVerificationState.Rejected));
            Assert.That(allowed, Contains.Item(KycVerificationState.Pending));
        }

        [Test]
        public void GetAllowedTransitions_Approved_ReturnsExpiredOnly()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(KycVerificationState.Approved);
            Assert.That(allowed, Is.EquivalentTo(new[] { KycVerificationState.Expired }));
        }

        [Test]
        public void GetAllowedTransitions_Rejected_ReturnsPendingOnly()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(KycVerificationState.Rejected);
            Assert.That(allowed, Is.EquivalentTo(new[] { KycVerificationState.Pending }));
        }

        [Test]
        public void GetAllowedTransitions_Expired_ReturnsPendingOnly()
        {
            var svc = CreateService();
            var allowed = svc.GetAllowedTransitions(KycVerificationState.Expired);
            Assert.That(allowed, Is.EquivalentTo(new[] { KycVerificationState.Pending }));
        }

        // ── CreateVerification ─────────────────────────────────────────────────

        [Test]
        public async Task CreateVerification_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var request = new CreateKycVerificationRequest { ParticipantId = "user-001" };

            var result = await svc.CreateVerificationAsync(request, "actor-1", "corr-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Record, Is.Not.Null);
            Assert.That(result.Record!.ParticipantId, Is.EqualTo("user-001"));
            Assert.That(result.Record.State, Is.EqualTo(KycVerificationState.Pending));
        }

        [Test]
        public async Task CreateVerification_SetsCreatedByActorId()
        {
            var svc = CreateService();
            var result = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p1" }, "actor-X", "corr");

            Assert.That(result.Record!.CreatedByActorId, Is.EqualTo("actor-X"));
        }

        [Test]
        public async Task CreateVerification_EmptyParticipantId_Rejected()
        {
            var svc = CreateService();
            var request = new CreateKycVerificationRequest { ParticipantId = "" };

            var result = await svc.CreateVerificationAsync(request, "actor", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
        }

        [Test]
        public async Task CreateVerification_WhitespaceParticipantId_Rejected()
        {
            var svc = CreateService();
            var request = new CreateKycVerificationRequest { ParticipantId = "   " };

            var result = await svc.CreateVerificationAsync(request, "actor", "corr");

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task CreateVerification_AddsInitialAuditEntry()
        {
            var svc = CreateService();
            var result = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p2" }, "actor", "corr");

            Assert.That(result.Record!.AuditHistory, Has.Count.EqualTo(1));
            var entry = result.Record.AuditHistory[0];
            Assert.That(entry.FromState, Is.EqualTo(KycVerificationState.NotStarted));
            Assert.That(entry.ToState, Is.EqualTo(KycVerificationState.Pending));
        }

        // ── UpdateStatus ───────────────────────────────────────────────────────

        [Test]
        public async Task UpdateStatus_ValidTransition_Succeeds()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p3" }, "actor", "corr");
            var kycId = created.Record!.KycId;

            var update = new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved };
            var result = await svc.UpdateStatusAsync(kycId, update, "reviewer", "corr2");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Record!.State, Is.EqualTo(KycVerificationState.Approved));
        }

        [Test]
        public async Task UpdateStatus_InvalidTransition_ReturnsFalse()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p4" }, "actor", "corr");
            var kycId = created.Record!.KycId;

            // Pending → Pending is not allowed
            var update = new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Pending };
            var result = await svc.UpdateStatusAsync(kycId, update, "actor", "corr");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task UpdateStatus_AddsAuditEntryWithCorrectActorAndStates()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p5" }, "creator", "corr");
            var kycId = created.Record!.KycId;

            var update = new UpdateKycVerificationStatusRequest
            {
                NewState = KycVerificationState.ManualReviewRequired,
                ReviewNote = "Needs human check"
            };
            await svc.UpdateStatusAsync(kycId, update, "reviewer-99", "corr2");

            var history = await svc.GetHistoryAsync(kycId);
            var lastEntry = history.History.Last();

            Assert.That(lastEntry.ActorId, Is.EqualTo("reviewer-99"));
            Assert.That(lastEntry.FromState, Is.EqualTo(KycVerificationState.Pending));
            Assert.That(lastEntry.ToState, Is.EqualTo(KycVerificationState.ManualReviewRequired));
            Assert.That(lastEntry.ReviewNote, Is.EqualTo("Needs human check"));
        }

        [Test]
        public async Task UpdateStatus_NotFound_ReturnsFalse()
        {
            var svc = CreateService();
            var update = new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved };
            var result = await svc.UpdateStatusAsync("nonexistent-id", update, "actor", "corr");
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task UpdateStatus_Approval_SetsApprovedAtAndExpiresAt()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p6" }, "actor", "corr");

            var update = new UpdateKycVerificationStatusRequest
            {
                NewState = KycVerificationState.Approved,
                ExpirationDays = 180
            };
            var result = await svc.UpdateStatusAsync(created.Record!.KycId, update, "reviewer", "corr");

            Assert.That(result.Record!.ApprovedAt, Is.Not.Null);
            Assert.That(result.Record.ExpiresAt, Is.Not.Null);
            Assert.That(result.Record.ExpiresAt!.Value, Is.GreaterThan(DateTime.UtcNow.AddDays(179)));
        }

        [Test]
        public async Task UpdateStatus_RejectionReason_StoredInAuditEntry()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p7" }, "actor", "corr");

            var update = new UpdateKycVerificationStatusRequest
            {
                NewState = KycVerificationState.Rejected,
                RejectionReason = KycRejectionReason.SanctionsMatch
            };
            await svc.UpdateStatusAsync(created.Record!.KycId, update, "reviewer", "corr");

            var history = await svc.GetHistoryAsync(created.Record.KycId);
            var rejectionEntry = history.History.Last();
            Assert.That(rejectionEntry.RejectionReason, Is.EqualTo(KycRejectionReason.SanctionsMatch));
        }

        // ── GetHistory ─────────────────────────────────────────────────────────

        [Test]
        public async Task GetHistory_ReturnsChronologicalOrder()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p8" }, "actor", "corr");
            var kycId = created.Record!.KycId;

            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.ManualReviewRequired }, "a1", "c1");
            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved }, "a2", "c2");

            var history = await svc.GetHistoryAsync(kycId);

            Assert.That(history.Success, Is.True);
            Assert.That(history.History.Count, Is.EqualTo(3));
            for (int i = 1; i < history.History.Count; i++)
                Assert.That(history.History[i].Timestamp, Is.GreaterThanOrEqualTo(history.History[i - 1].Timestamp));
        }

        [Test]
        public async Task GetHistory_NotFound_ReturnsFalse()
        {
            var svc = CreateService();
            var result = await svc.GetHistoryAsync("does-not-exist");
            Assert.That(result.Success, Is.False);
        }

        // ── AddEvidence / GetEvidence ──────────────────────────────────────────

        [Test]
        public async Task AddEvidence_ValidRecord_AddsEvidence()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p9" }, "actor", "corr");
            var kycId = created.Record!.KycId;

            var evidenceRequest = new AddKycEvidenceRequest
            {
                EvidenceType = KycEvidenceType.Passport,
                DocumentReference = "ref-123",
                IssuingCountry = "DE"
            };
            var result = await svc.AddEvidenceAsync(kycId, evidenceRequest, "actor");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence, Has.Count.EqualTo(1));
            Assert.That(result.Evidence[0].EvidenceType, Is.EqualTo(KycEvidenceType.Passport));
        }

        [Test]
        public async Task GetEvidence_ReturnsAllEvidence()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "p10" }, "actor", "corr");
            var kycId = created.Record!.KycId;

            await svc.AddEvidenceAsync(kycId, new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.Passport }, "a");
            await svc.AddEvidenceAsync(kycId, new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.UtilityBill }, "a");

            var result = await svc.GetEvidenceAsync(kycId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task AddEvidence_NotFound_ReturnsFalse()
        {
            var svc = CreateService();
            var result = await svc.AddEvidenceAsync("bad-id", new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.NationalId }, "actor");
            Assert.That(result.Success, Is.False);
        }

        // ── EvaluateEligibility ────────────────────────────────────────────────

        [Test]
        public async Task EvaluateEligibility_ApprovedNonExpired_IsEligible()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "eligible-1" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 },
                "reviewer", "corr");

            var result = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "eligible-1" });

            Assert.That(result.IsEligible, Is.True);
        }

        [Test]
        public async Task EvaluateEligibility_Pending_IsNotEligible()
        {
            var svc = CreateService();
            await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "pending-1" }, "actor", "corr");

            var result = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "pending-1" });

            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.CurrentState, Is.EqualTo(KycVerificationState.Pending));
        }

        [Test]
        public async Task EvaluateEligibility_Rejected_IsNotEligible()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "rejected-1" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Rejected }, "reviewer", "corr");

            var result = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "rejected-1" });

            Assert.That(result.IsEligible, Is.False);
        }

        [Test]
        public async Task EvaluateEligibility_ManualReviewRequired_IsNotEligible()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "review-1" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.ManualReviewRequired }, "reviewer", "corr");

            var result = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "review-1" });

            Assert.That(result.IsEligible, Is.False);
        }

        [Test]
        public async Task EvaluateEligibility_NoRecord_IsNotEligible()
        {
            var svc = CreateService();
            var result = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "no-record" });
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.CurrentState, Is.EqualTo(KycVerificationState.NotStarted));
        }

        [Test]
        public async Task EvaluateEligibility_ApprovedButExpired_IsNotEligible()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "expired-eligibility" }, "actor", "corr");

            // Approve with 0 expiry days so it's already expired
            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 0 },
                "reviewer", "corr");

            // Process batch expiry
            await svc.ProcessExpiredVerificationsAsync();

            var result = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "expired-eligibility" });
            Assert.That(result.IsEligible, Is.False);
        }

        // ── ProcessExpiredVerifications ────────────────────────────────────────

        [Test]
        public async Task ProcessExpiredVerifications_ExpiredApprovedRecords_MarkedExpired()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "batch-expire-1" }, "actor", "corr");

            // Approve with 0 days expiry
            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 0 },
                "reviewer", "corr");

            var count = await svc.ProcessExpiredVerificationsAsync();

            Assert.That(count, Is.GreaterThanOrEqualTo(1));
            var recordResult = await svc.GetVerificationAsync(created.Record.KycId);
            Assert.That(recordResult.Record!.State, Is.EqualTo(KycVerificationState.Expired));
        }

        [Test]
        public async Task ProcessExpiredVerifications_NonExpiredRecords_NotMarkedExpired()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "no-expire-1" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 },
                "reviewer", "corr");

            await svc.ProcessExpiredVerificationsAsync();

            var recordResult = await svc.GetVerificationAsync(created.Record.KycId);
            Assert.That(recordResult.Record!.State, Is.EqualTo(KycVerificationState.Approved));
        }

        // ── ReviewNote persistence ─────────────────────────────────────────────

        [Test]
        public async Task UpdateStatus_ReviewNote_PersistedInRecord()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "note-1" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest
                {
                    NewState = KycVerificationState.ManualReviewRequired,
                    ReviewNote = "Suspicious document"
                },
                "reviewer", "corr");

            var recordResult = await svc.GetVerificationAsync(created.Record.KycId);
            Assert.That(recordResult.Record!.CurrentReviewNote, Is.EqualTo("Suspicious document"));
        }

        // ── AuditTrail completeness ────────────────────────────────────────────

        [Test]
        public async Task AuditTrail_FullStateMachineWalk_WritesCompleteHistory()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "full-walk" }, "actor", "corr");
            var kycId = created.Record!.KycId;

            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.ManualReviewRequired }, "r1", "c1");
            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Rejected }, "r2", "c2");
            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Pending }, "r3", "c3");
            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 }, "r4", "c4");

            var history = await svc.GetHistoryAsync(kycId);

            Assert.That(history.History.Count, Is.EqualTo(5));
            Assert.That(history.History[0].ToState, Is.EqualTo(KycVerificationState.Pending));
            Assert.That(history.History[1].ToState, Is.EqualTo(KycVerificationState.ManualReviewRequired));
            Assert.That(history.History[2].ToState, Is.EqualTo(KycVerificationState.Rejected));
            Assert.That(history.History[3].ToState, Is.EqualTo(KycVerificationState.Pending));
            Assert.That(history.History[4].ToState, Is.EqualTo(KycVerificationState.Approved));
        }

        // ── ExpiresAt calculation ──────────────────────────────────────────────

        [Test]
        public async Task UpdateStatus_Approval_ExpiresAtCalculatedFromExpirationDays()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "expiry-calc" }, "actor", "corr");

            var before = DateTime.UtcNow;
            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 90 },
                "reviewer", "corr");

            var recordResult = await svc.GetVerificationAsync(created.Record.KycId);
            var expiresAt = recordResult.Record!.ExpiresAt!.Value;

            Assert.That(expiresAt, Is.GreaterThan(before.AddDays(89)));
            Assert.That(expiresAt, Is.LessThan(before.AddDays(91)));
        }

        [Test]
        public async Task UpdateStatus_ApprovalDefaultExpiry_Is365Days()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "default-expiry" }, "actor", "corr");

            var before = DateTime.UtcNow;
            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved },
                "reviewer", "corr");

            var recordResult = await svc.GetVerificationAsync(created.Record.KycId);
            var expiresAt = recordResult.Record!.ExpiresAt!.Value;

            Assert.That(expiresAt, Is.GreaterThan(before.AddDays(364)));
        }

        // ── CorrelationId threading ────────────────────────────────────────────

        [Test]
        public async Task CreateVerification_CorrelationId_IsStoredInRecord()
        {
            var svc = CreateService();
            var result = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "corr-test" }, "actor", "MY-CORR-ID");

            Assert.That(result.Record!.CorrelationId, Is.EqualTo("MY-CORR-ID"));
        }

        [Test]
        public async Task UpdateStatus_CorrelationId_IsStoredInAuditEntry()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "corr-audit" }, "actor", "corr-1");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved },
                "reviewer", "corr-2");

            var history = await svc.GetHistoryAsync(created.Record.KycId);
            var updateEntry = history.History.Last();
            Assert.That(updateEntry.CorrelationId, Is.EqualTo("corr-2"));
        }
    }
}

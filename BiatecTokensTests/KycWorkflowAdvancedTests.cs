using BiatecTokensApi.Models.KycWorkflow;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced unit tests for KycWorkflowService covering edge cases, boundary conditions,
    /// multiple participants, full lifecycle walks, and machine-readable response contracts.
    /// </summary>
    [TestFixture]
    public class KycWorkflowAdvancedTests
    {
        private static KycWorkflowService CreateService() =>
            new KycWorkflowService(NullLogger<KycWorkflowService>.Instance);

        // ── All invalid state transition combinations ───────────────────────────

        [Test]
        public void AllInvalidTransitions_NotStarted_ToNonPending()
        {
            var svc = CreateService();
            var invalidTargets = new[]
            {
                KycVerificationState.NotStarted,
                KycVerificationState.ManualReviewRequired,
                KycVerificationState.Approved,
                KycVerificationState.Rejected,
                KycVerificationState.Expired
            };
            foreach (var target in invalidTargets)
            {
                var result = svc.ValidateTransition(KycVerificationState.NotStarted, target);
                Assert.That(result.IsValid, Is.False, $"NotStarted→{target} should be invalid");
            }
        }

        [Test]
        public void AllInvalidTransitions_Pending_ToInvalid()
        {
            var svc = CreateService();
            // Pending → Pending and Pending → NotStarted should be invalid
            Assert.That(svc.ValidateTransition(KycVerificationState.Pending, KycVerificationState.Pending).IsValid, Is.False);
            Assert.That(svc.ValidateTransition(KycVerificationState.Pending, KycVerificationState.NotStarted).IsValid, Is.False);
        }

        [Test]
        public void AllInvalidTransitions_ManualReviewRequired_ToInvalid()
        {
            var svc = CreateService();
            Assert.That(svc.ValidateTransition(KycVerificationState.ManualReviewRequired, KycVerificationState.NotStarted).IsValid, Is.False);
            Assert.That(svc.ValidateTransition(KycVerificationState.ManualReviewRequired, KycVerificationState.Expired).IsValid, Is.False);
            Assert.That(svc.ValidateTransition(KycVerificationState.ManualReviewRequired, KycVerificationState.ManualReviewRequired).IsValid, Is.False);
        }

        [Test]
        public void AllInvalidTransitions_Approved_ToNonExpired()
        {
            var svc = CreateService();
            var invalidTargets = new[]
            {
                KycVerificationState.NotStarted,
                KycVerificationState.Pending,
                KycVerificationState.ManualReviewRequired,
                KycVerificationState.Rejected,
                KycVerificationState.Approved
            };
            foreach (var target in invalidTargets)
            {
                Assert.That(svc.ValidateTransition(KycVerificationState.Approved, target).IsValid, Is.False,
                    $"Approved→{target} should be invalid");
            }
        }

        [Test]
        public void AllInvalidTransitions_Rejected_ToNonPending()
        {
            var svc = CreateService();
            var invalidTargets = new[]
            {
                KycVerificationState.NotStarted,
                KycVerificationState.ManualReviewRequired,
                KycVerificationState.Approved,
                KycVerificationState.Rejected,
                KycVerificationState.Expired
            };
            foreach (var target in invalidTargets)
            {
                Assert.That(svc.ValidateTransition(KycVerificationState.Rejected, target).IsValid, Is.False,
                    $"Rejected→{target} should be invalid");
            }
        }

        [Test]
        public void AllInvalidTransitions_Expired_ToNonPending()
        {
            var svc = CreateService();
            var invalidTargets = new[]
            {
                KycVerificationState.NotStarted,
                KycVerificationState.ManualReviewRequired,
                KycVerificationState.Approved,
                KycVerificationState.Rejected,
                KycVerificationState.Expired
            };
            foreach (var target in invalidTargets)
            {
                Assert.That(svc.ValidateTransition(KycVerificationState.Expired, target).IsValid, Is.False,
                    $"Expired→{target} should be invalid");
            }
        }

        // ── Boundary conditions for expiry ─────────────────────────────────────

        [Test]
        public async Task Expiry_RecordExpiredByBatchProcessor_MarkedExpired()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "boundary-expire-1" }, "actor", "corr");

            // Approve with 0 days = immediately expired
            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 0 },
                "reviewer", "corr");

            var count = await svc.ProcessExpiredVerificationsAsync();

            Assert.That(count, Is.GreaterThanOrEqualTo(1));
            var result = await svc.GetVerificationAsync(created.Record.KycId);
            Assert.That(result.Record!.State, Is.EqualTo(KycVerificationState.Expired));
        }

        [Test]
        public async Task Expiry_FutureExpiry_NotMarkedExpiredByBatch()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "future-expire-1" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 },
                "reviewer", "corr");

            await svc.ProcessExpiredVerificationsAsync();

            var result = await svc.GetVerificationAsync(created.Record.KycId);
            Assert.That(result.Record!.State, Is.EqualTo(KycVerificationState.Approved));
        }

        [Test]
        public async Task Expiry_IsExpiredProperty_TrueWhenPastExpiry()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "is-expired-1" }, "actor", "corr");

            // Approve with 0 days = ExpiresAt is set to now, so IsExpired should be true
            var updateResult = await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 0 },
                "reviewer", "corr");

            // Check on the record before auto-expiry runs on GET
            // IsExpired = State == Approved && ExpiresAt < UtcNow
            // ExpiresAt was set to now.AddDays(0) = now, so it may be exactly at or just before "now"
            // Wait a tick to ensure we're past the expiry
            await Task.Delay(10);
            Assert.That(updateResult.Record!.IsExpired, Is.True);
        }

        [Test]
        public async Task Expiry_IsExpiredProperty_FalseWhenFutureExpiry()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "is-expired-2" }, "actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 },
                "reviewer", "corr");

            var result = await svc.GetVerificationAsync(created.Record.KycId);
            Assert.That(result.Record!.IsExpired, Is.False);
        }

        // ── Multiple participants with separate records ─────────────────────────

        [Test]
        public async Task MultipleParticipants_HaveIndependentRecords()
        {
            var svc = CreateService();
            var r1 = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "multi-1" }, "a", "c");
            var r2 = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "multi-2" }, "a", "c");

            Assert.That(r1.Record!.KycId, Is.Not.EqualTo(r2.Record!.KycId));
            Assert.That(r1.Record.ParticipantId, Is.Not.EqualTo(r2.Record.ParticipantId));
        }

        [Test]
        public async Task MultipleParticipants_TransitionOneDoesNotAffectOther()
        {
            var svc = CreateService();
            var r1 = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "multi-ind-1" }, "a", "c");
            var r2 = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "multi-ind-2" }, "a", "c");

            await svc.UpdateStatusAsync(r1.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved },
                "reviewer", "corr");

            var result2 = await svc.GetVerificationAsync(r2.Record!.KycId);
            Assert.That(result2.Record!.State, Is.EqualTo(KycVerificationState.Pending));
        }

        [Test]
        public async Task MultipleParticipants_EligibilityEvaluatedIndependently()
        {
            var svc = CreateService();
            var r1 = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "elig-ind-1" }, "a", "c");
            await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "elig-ind-2" }, "a", "c");

            await svc.UpdateStatusAsync(r1.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 },
                "reviewer", "corr");

            var e1 = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "elig-ind-1" });
            var e2 = await svc.EvaluateEligibilityAsync(new KycEligibilityRequest { ParticipantId = "elig-ind-2" });

            Assert.That(e1.IsEligible, Is.True);
            Assert.That(e2.IsEligible, Is.False);
        }

        // ── Evidence integrity ─────────────────────────────────────────────────

        [Test]
        public async Task Evidence_MultipleItems_AllRetained()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "ev-multi-1" }, "a", "c");
            var kycId = created.Record!.KycId;

            await svc.AddEvidenceAsync(kycId, new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.Passport }, "a");
            await svc.AddEvidenceAsync(kycId, new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.UtilityBill }, "a");
            await svc.AddEvidenceAsync(kycId, new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.SelfieWithId }, "a");

            var result = await svc.GetEvidenceAsync(kycId);

            Assert.That(result.Evidence, Has.Count.EqualTo(3));
            Assert.That(result.Evidence.Select(e => e.EvidenceType),
                Is.EquivalentTo(new[] { KycEvidenceType.Passport, KycEvidenceType.UtilityBill, KycEvidenceType.SelfieWithId }));
        }

        [Test]
        public async Task Evidence_IssuingCountry_Persisted()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "ev-country-1" }, "a", "c");

            await svc.AddEvidenceAsync(created.Record!.KycId,
                new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.NationalId, IssuingCountry = "FR" }, "a");

            var result = await svc.GetEvidenceAsync(created.Record.KycId);
            Assert.That(result.Evidence[0].IssuingCountry, Is.EqualTo("FR"));
        }

        [Test]
        public async Task Evidence_ContentHash_Persisted()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "ev-hash-1" }, "a", "c");

            await svc.AddEvidenceAsync(created.Record!.KycId,
                new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.BankStatement, ContentHash = "sha256abc123" }, "a");

            var result = await svc.GetEvidenceAsync(created.Record.KycId);
            Assert.That(result.Evidence[0].ContentHash, Is.EqualTo("sha256abc123"));
        }

        [Test]
        public async Task Evidence_UnverifiedByDefault()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "ev-unverified-1" }, "a", "c");

            await svc.AddEvidenceAsync(created.Record!.KycId,
                new AddKycEvidenceRequest { EvidenceType = KycEvidenceType.TaxDocument }, "a");

            var result = await svc.GetEvidenceAsync(created.Record.KycId);
            Assert.That(result.Evidence[0].IsVerified, Is.False);
        }

        // ── ProcessExpiredVerifications precision ──────────────────────────────

        [Test]
        public async Task ProcessExpiredVerifications_OnlyTargetsApprovedExpiredRecords()
        {
            var svc = CreateService();

            // Create an approved (non-expired) record
            var notExpired = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "batch-not-expired" }, "a", "c");
            await svc.UpdateStatusAsync(notExpired.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 }, "r", "c");

            // Create a pending record (should not be affected)
            var pending = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "batch-pending" }, "a", "c");

            // Create an approved expired record
            var expired = await svc.CreateVerificationAsync(new CreateKycVerificationRequest { ParticipantId = "batch-expired" }, "a", "c");
            await svc.UpdateStatusAsync(expired.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 0 }, "r", "c");

            var count = await svc.ProcessExpiredVerificationsAsync();

            Assert.That(count, Is.EqualTo(1));
            var notExpiredResult = await svc.GetVerificationAsync(notExpired.Record.KycId);
            var pendingResult = await svc.GetVerificationAsync(pending.Record!.KycId);
            Assert.That(notExpiredResult.Record!.State, Is.EqualTo(KycVerificationState.Approved));
            Assert.That(pendingResult.Record!.State, Is.EqualTo(KycVerificationState.Pending));
        }

        // ── Audit entry actor fields ───────────────────────────────────────────

        [Test]
        public async Task AuditEntry_ActorId_PreservedCorrectly()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "actor-check-1" }, "original-actor", "corr");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved },
                "specific-reviewer-id", "corr");

            var history = await svc.GetHistoryAsync(created.Record.KycId);
            Assert.That(history.History.Last().ActorId, Is.EqualTo("specific-reviewer-id"));
        }

        [Test]
        public async Task AuditEntry_Timestamp_IsSetAtTimeOfUpdate()
        {
            var svc = CreateService();
            var before = DateTime.UtcNow;

            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "ts-check-1" }, "a", "c");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved },
                "reviewer", "corr");

            var after = DateTime.UtcNow;
            var history = await svc.GetHistoryAsync(created.Record.KycId);
            var updateEntry = history.History.Last();

            Assert.That(updateEntry.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(updateEntry.Timestamp, Is.LessThanOrEqualTo(after));
        }

        // ── Rejection reason codes ─────────────────────────────────────────────

        [Test]
        [TestCase(KycRejectionReason.DocumentExpired)]
        [TestCase(KycRejectionReason.DocumentInvalid)]
        [TestCase(KycRejectionReason.DocumentMismatch)]
        [TestCase(KycRejectionReason.FaceMatchFailed)]
        [TestCase(KycRejectionReason.AddressUnverifiable)]
        [TestCase(KycRejectionReason.SanctionsMatch)]
        [TestCase(KycRejectionReason.InsufficientEvidence)]
        [TestCase(KycRejectionReason.CountryRestricted)]
        [TestCase(KycRejectionReason.IdentityConflict)]
        [TestCase(KycRejectionReason.Other)]
        public async Task RejectionReason_AllCodes_MappedCorrectlyInAuditEntry(KycRejectionReason reason)
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = $"rej-reason-{reason}" }, "a", "c");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest
                {
                    NewState = KycVerificationState.Rejected,
                    RejectionReason = reason
                },
                "reviewer", "corr");

            var history = await svc.GetHistoryAsync(created.Record.KycId);
            Assert.That(history.History.Last().RejectionReason, Is.EqualTo(reason));
        }

        // ── Full lifecycle walk ────────────────────────────────────────────────

        [Test]
        public async Task FullLifecycle_NotStartedThroughAllStates_CompletesSuccessfully()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "lifecycle-full" }, "system", "c0");
            var kycId = created.Record!.KycId;

            // NotStarted → Pending (on creation)
            Assert.That(created.Record.State, Is.EqualTo(KycVerificationState.Pending));

            // Pending → ManualReviewRequired
            var r1 = await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.ManualReviewRequired }, "reviewer", "c1");
            Assert.That(r1.Record!.State, Is.EqualTo(KycVerificationState.ManualReviewRequired));

            // ManualReviewRequired → Rejected
            var r2 = await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Rejected, RejectionReason = KycRejectionReason.DocumentInvalid }, "reviewer", "c2");
            Assert.That(r2.Record!.State, Is.EqualTo(KycVerificationState.Rejected));

            // Rejected → Pending (re-submit)
            var r3 = await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Pending }, "system", "c3");
            Assert.That(r3.Record!.State, Is.EqualTo(KycVerificationState.Pending));

            // Pending → Approved
            var r4 = await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 }, "reviewer", "c4");
            Assert.That(r4.Record!.State, Is.EqualTo(KycVerificationState.Approved));

            // Approved → Expired
            var r5 = await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Expired }, "system", "c5");
            Assert.That(r5.Record!.State, Is.EqualTo(KycVerificationState.Expired));

            // Verify complete history
            var history = await svc.GetHistoryAsync(kycId);
            Assert.That(history.History.Count, Is.EqualTo(6));
        }

        // ── Correlation ID threading ───────────────────────────────────────────

        [Test]
        public async Task CorrelationId_ThreadedThroughEntireWorkflow()
        {
            var svc = CreateService();
            const string corrId = "TRACE-WORKFLOW-001";

            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "corr-full-1" }, "actor", corrId);
            var kycId = created.Record!.KycId;

            await svc.UpdateStatusAsync(kycId, new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved }, "reviewer", corrId);

            var history = await svc.GetHistoryAsync(kycId);

            // Creation entry should have correlation ID
            Assert.That(history.History[0].CorrelationId, Is.EqualTo(corrId));
            // Update entry should have correlation ID
            Assert.That(history.History[1].CorrelationId, Is.EqualTo(corrId));
        }

        // ── Review note across multiple updates ────────────────────────────────

        [Test]
        public async Task ReviewNote_UpdatedAcrossMultipleTransitions()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "note-multi-1" }, "a", "c");
            var kycId = created.Record!.KycId;

            await svc.UpdateStatusAsync(kycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.ManualReviewRequired, ReviewNote = "First note" },
                "r1", "c");

            await svc.UpdateStatusAsync(kycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ReviewNote = "Approved after review" },
                "r2", "c");

            var record = (await svc.GetVerificationAsync(kycId)).Record!;
            Assert.That(record.CurrentReviewNote, Is.EqualTo("Approved after review"));
        }

        // ── GetActiveVerificationByParticipant ─────────────────────────────────

        [Test]
        public async Task GetActiveByParticipant_ReturnsApprovedRecord()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "active-approved-1" }, "a", "c");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Approved, ExpirationDays = 365 },
                "reviewer", "c");

            var result = await svc.GetActiveVerificationByParticipantAsync("active-approved-1");
            Assert.That(result.Success, Is.True);
            Assert.That(result.Record!.State, Is.EqualTo(KycVerificationState.Approved));
        }

        [Test]
        public async Task GetActiveByParticipant_NoRecord_ReturnsFalse()
        {
            var svc = CreateService();
            var result = await svc.GetActiveVerificationByParticipantAsync("nobody-here");
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task GetActiveByParticipant_RejectedRecord_NotReturned()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "rejected-active-1" }, "a", "c");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest { NewState = KycVerificationState.Rejected },
                "reviewer", "c");

            var result = await svc.GetActiveVerificationByParticipantAsync("rejected-active-1");
            Assert.That(result.Success, Is.False);
        }

        // ── Machine-readable response contracts ────────────────────────────────

        [Test]
        public async Task KycVerificationResponse_AllFieldsPresent_OnSuccess()
        {
            var svc = CreateService();
            var result = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "contract-1" }, "a", "c");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Record, Is.Not.Null);
            Assert.That(result.Record!.KycId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Record.ParticipantId, Is.Not.Null);
            Assert.That(result.Record.AuditHistory, Is.Not.Null);
            Assert.That(result.Record.Evidence, Is.Not.Null);
            Assert.That(result.Record.Metadata, Is.Not.Null);
        }

        [Test]
        public async Task KycHistoryResponse_AllFieldsPresent_OnSuccess()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "hist-contract-1" }, "a", "c");

            var result = await svc.GetHistoryAsync(created.Record!.KycId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.KycId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ParticipantId, Is.Not.Null);
            Assert.That(result.History, Is.Not.Null);
        }

        [Test]
        public async Task KycEligibilityResult_AllFieldsPresent_OnEvaluation()
        {
            var svc = CreateService();
            var result = await svc.EvaluateEligibilityAsync(
                new KycEligibilityRequest { ParticipantId = "elig-contract-1" });

            Assert.That(result.ParticipantId, Is.EqualTo("elig-contract-1"));
            Assert.That(result.EvaluatedAt, Is.GreaterThan(DateTime.MinValue));
            Assert.That(result.KycRequired, Is.True);
        }

        // ── ReasonCode string field ────────────────────────────────────────────

        [Test]
        public async Task UpdateStatus_ReasonCode_StoredInAuditEntry()
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = "reason-code-1" }, "a", "c");

            await svc.UpdateStatusAsync(created.Record!.KycId,
                new UpdateKycVerificationStatusRequest
                {
                    NewState = KycVerificationState.Rejected,
                    ReasonCode = "MANUAL_REVIEW_FAILED"
                },
                "reviewer", "c");

            var history = await svc.GetHistoryAsync(created.Record.KycId);
            Assert.That(history.History.Last().ReasonCode, Is.EqualTo("MANUAL_REVIEW_FAILED"));
        }

        // ── GetVerification not found ──────────────────────────────────────────

        [Test]
        public async Task GetVerification_NotFound_ReturnsErrorCode()
        {
            var svc = CreateService();
            var result = await svc.GetVerificationAsync("completely-fake-id");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
        }

        // ── Evidence type coverage ─────────────────────────────────────────────

        [Test]
        [TestCase(KycEvidenceType.Passport)]
        [TestCase(KycEvidenceType.DriversLicense)]
        [TestCase(KycEvidenceType.NationalId)]
        [TestCase(KycEvidenceType.UtilityBill)]
        [TestCase(KycEvidenceType.BankStatement)]
        [TestCase(KycEvidenceType.TaxDocument)]
        [TestCase(KycEvidenceType.SelfieWithId)]
        [TestCase(KycEvidenceType.VideoVerification)]
        [TestCase(KycEvidenceType.Other)]
        public async Task Evidence_AllTypes_CanBeAdded(KycEvidenceType evidenceType)
        {
            var svc = CreateService();
            var created = await svc.CreateVerificationAsync(
                new CreateKycVerificationRequest { ParticipantId = $"ev-type-{evidenceType}" }, "a", "c");

            var result = await svc.AddEvidenceAsync(created.Record!.KycId,
                new AddKycEvidenceRequest { EvidenceType = evidenceType }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence[0].EvidenceType, Is.EqualTo(evidenceType));
        }
    }
}

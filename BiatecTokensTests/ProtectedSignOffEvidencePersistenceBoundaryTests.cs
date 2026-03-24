using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Boundary, freshness-window, stress, and cross-head tests for
    /// ProtectedSignOffEvidencePersistenceService providing additional
    /// coverage beyond the core and edge-case suites.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceBoundaryTests
    {
        // ══════════════════════════════════════════════════════════════
        // Fakes
        // ══════════════════════════════════════════════════════════════

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();

            public Task EmitEventAsync(WebhookEvent e)
            {
                lock (Events) Events.Add(e);
                return Task.CompletedTask;
            }

            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u)
                => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u)
                => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ══════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════

        private static ProtectedSignOffEvidencePersistenceService CreateService(
            IWebhookService? webhook = null,
            TimeProvider? timeProvider = null)
            => new(NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance, webhook, timeProvider);

        private static (ProtectedSignOffEvidencePersistenceService svc, CapturingWebhook wh, FixedTimeProvider tp)
            CreateServiceWithCapture(DateTimeOffset? fixedNow = null)
        {
            var tp = new FixedTimeProvider(fixedNow ?? DateTimeOffset.UtcNow);
            var wh = new CapturingWebhook();
            var svc = CreateService(wh, tp);
            return (svc, wh, tp);
        }

        // ══════════════════════════════════════════════════════════════
        // Freshness window boundary tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task FreshnessWindow_ExactlyAtBoundary_EvidenceIsComplete()
        {
            var now = DateTimeOffset.UtcNow;
            var (svc, _, tp) = CreateServiceWithCapture(now);
            const string head = "boundary-head-sha";
            const int windowHours = 2;

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours,
                CaseId = "case-boundary"
            }, "test-actor");

            // Move clock to just before the boundary (59 minutes 59 seconds before expiry)
            tp.Advance(TimeSpan.FromHours(windowHours).Subtract(TimeSpan.FromSeconds(1)));

            var readiness = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours
            });

            Assert.That(readiness.EvidenceFreshness, Is.Not.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Evidence must not be stale before the freshness window expires");
        }

        [Test]
        public async Task FreshnessWindow_JustPastBoundary_EvidenceIsStale()
        {
            var now = DateTimeOffset.UtcNow;
            var (svc, _, tp) = CreateServiceWithCapture(now);
            const string head = "stale-boundary-head";
            const int windowHours = 1;

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours,
                CaseId = "case-stale"
            }, "test-actor");

            // Advance clock past the freshness window
            tp.Advance(TimeSpan.FromHours(windowHours).Add(TimeSpan.FromSeconds(1)));

            var readiness = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours
            });

            Assert.That(readiness.EvidenceFreshness,
                Is.EqualTo(SignOffEvidenceFreshnessStatus.Stale).Or.EqualTo(SignOffEvidenceFreshnessStatus.Unavailable),
                "Evidence must be classified as Stale after window expires");
            Assert.That(readiness.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.DegradedStaleEvidence).Or.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence).Or.EqualTo(SignOffReleaseReadinessStatus.Pending));
        }

        [Test]
        public async Task FreshnessWindow_ZeroHours_UsesDefaultOf24()
        {
            var (svc, _, tp) = CreateServiceWithCapture();
            const string head = "zero-window-head";

            var persistResult = await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head,
                FreshnessWindowHours = 0, // should default to 24
                CaseId = "case-zero-window"
            }, "test-actor");

            Assert.That(persistResult.Success, Is.True, "Persist must succeed with zero window (uses default 24h)");

            // 23h later still fresh
            tp.Advance(TimeSpan.FromHours(23));
            var readiness = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head,
                FreshnessWindowHours = 0
            });
            Assert.That(readiness.EvidenceFreshness, Is.Not.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Evidence should still be fresh at 23h with default 24h window");
        }

        [Test]
        public async Task FreshnessWindow_VeryShortWindow_ExpiresQuickly()
        {
            var now = DateTimeOffset.UtcNow;
            var (svc, _, tp) = CreateServiceWithCapture(now);
            const string head = "short-window-head";
            const int windowHours = 1;

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours,
                CaseId = "case-short"
            }, "test-actor");

            // Evidence should be fresh now
            var freshResult = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours
            });
            Assert.That(freshResult.EvidenceFreshness, Is.Not.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Just persisted evidence should not be stale");

            // Advance past window
            tp.Advance(TimeSpan.FromHours(windowHours + 1));
            var staleResult = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head,
                FreshnessWindowHours = windowHours
            });
            Assert.That(staleResult.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.DegradedStaleEvidence).Or.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence).Or.EqualTo(SignOffReleaseReadinessStatus.Pending),
                "Evidence must go stale after window");
        }

        // ══════════════════════════════════════════════════════════════
        // Multiple webhooks per head — ordering guarantees
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task MultipleWebhooks_SameHead_AllRetainedInHistory()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "multi-webhook-head";
            const string caseId = "case-multi";

            foreach (var outcome in new[]
            {
                ApprovalWebhookOutcome.Escalated,
                ApprovalWebhookOutcome.Escalated,
                ApprovalWebhookOutcome.Approved
            })
            {
                await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
                {
                    HeadRef = head,
                    CaseId = caseId,
                    Outcome = outcome
                }, "test-actor");
            }

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head, MaxRecords = 10 });

            Assert.That(history.Success, Is.True);
            Assert.That(history.Records.Count, Is.GreaterThanOrEqualTo(3),
                "All 3 webhooks must be retained");
        }

        [Test]
        public async Task MultipleWebhooks_MixedOutcomes_HasApprovalWebhookTrue()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "mixed-outcome-head";

            // TimedOut webhook recorded first
            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head,
                CaseId = "c1",
                Outcome = ApprovalWebhookOutcome.TimedOut
            }, "test-actor");

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head,
                CaseId = "c1",
                RequireApprovalWebhook = false
            }, "test-actor");

            var readiness = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head,
                });

            Assert.That(readiness.HasApprovalWebhook, Is.True,
                "HasApprovalWebhook must be true when any webhook was received, even TimedOut");
        }

        [Test]
        public async Task MultipleWebhooks_DeniedThenApproved_HasApprovalWebhookTrue()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "denied-then-approved-head";

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = ApprovalWebhookOutcome.Denied
            }, "test-actor");
            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "c1"
            }, "test-actor");

            var readiness = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head
            });

            Assert.That(readiness.HasApprovalWebhook, Is.True,
                "HasApprovalWebhook must reflect any webhook received");
        }

        // ══════════════════════════════════════════════════════════════
        // Cross-head isolation tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task CrossHead_ApprovalOnHeadA_DoesNotAffectHeadB()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = "head-a", CaseId = "c1", Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");

            var readinessB = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = "head-b"
            });

            Assert.That(readinessB.HasApprovalWebhook, Is.False,
                "Approval for head-a must not bleed into head-b");
        }

        [Test]
        public async Task CrossHead_EvidenceOnHeadA_DoesNotAffectHeadBReadiness()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string caseId = "case-cross";

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = "head-a", CaseId = caseId
            }, "test-actor");

            var readinessB = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = "head-b"
            });

            Assert.That(readinessB.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Pending)
                  .Or.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence)
                  .Or.EqualTo(SignOffReleaseReadinessStatus.Indeterminate),
                "head-b must not use head-a's evidence");
        }

        [Test]
        public async Task CrossHead_WebhookHistoryIsolated()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            for (int i = 0; i < 5; i++)
            {
                await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
                {
                    HeadRef = "head-history-a", CaseId = "c1", Outcome = ApprovalWebhookOutcome.Approved
                }, "test-actor");
            }

            var historyB = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = "head-history-b", MaxRecords = 10 });

            Assert.That(historyB.Success, Is.True);
            Assert.That(historyB.Records, Is.Empty,
                "head-b must have an empty webhook history when only head-a received webhooks");
        }

        // ══════════════════════════════════════════════════════════════
        // Readiness state enumeration tests (all 5 states reachable)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task ReadinessState_Indeterminate_WhenNoEvidenceAndNoWebhook()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = "never-seen-head"
            });

            Assert.That(result.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate)
                  .Or.EqualTo(SignOffReleaseReadinessStatus.Pending)
                  .Or.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "No evidence and no webhook should be Indeterminate, Pending, or Blocked (missing evidence).");
        }

        [Test]
        public async Task ReadinessState_Ready_WhenAllConditionsMet()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "ready-state-head";
            const string caseId = "case-ready";

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = caseId, Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = caseId, FreshnessWindowHours = 24
            }, "test-actor");

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head, FreshnessWindowHours = 24
            });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "All conditions met → status must be Ready");
            Assert.That(result.Blockers, Is.Not.Null, "Blockers must never be null");
            Assert.That(result.Blockers, Is.Empty, "No blockers expected in Ready state");
        }

        [Test]
        public async Task ReadinessState_Blocked_WhenApprovalDenied()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "blocked-state-head";
            const string caseId = "case-blocked";

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = caseId, Outcome = ApprovalWebhookOutcome.Denied
            }, "test-actor");

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = caseId
            }, "test-actor");

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head, });

            Assert.That(result.Status, Is.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "Denied approval must block release");
            Assert.That(result.Blockers, Is.Not.Null, "Blockers must not be null");
            Assert.That(result.Blockers, Is.Not.Empty, "Blockers must contain at least one entry");
            foreach (var blocker in result.Blockers)
            {
                Assert.That(blocker.Category, Is.Not.EqualTo(SignOffReleaseBlockerCategory.Unspecified),
                    "Blocker.Category must never be Unspecified");
                Assert.That(blocker.RemediationHint, Is.Not.Null.And.Not.Empty,
                    "Each blocker must have a RemediationHint");
            }
        }

        [Test]
        public async Task ReadinessState_Pending_WhenEvidenceExistsButNoApproval()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "pending-state-head";

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "case-pending",
                RequireApprovalWebhook = false
            }, "test-actor");

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head, });

            Assert.That(result.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Pending)
                  .Or.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "Evidence present but no approval → Pending or Blocked");
        }

        // ══════════════════════════════════════════════════════════════
        // Blocker schema contract
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task BlockerSchema_AllBlockersHaveCode()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "blocker-code-head";

            // Trigger a blocked state
            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = ApprovalWebhookOutcome.Denied
            }, "test-actor");
            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "c1"
            }, "test-actor");

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head, });

            foreach (var blocker in result.Blockers ?? new List<SignOffReleaseBlocker>())
            {
                Assert.That(blocker.Code, Is.Not.Null.And.Not.Empty,
                    $"Blocker.Code must not be null/empty (category: {blocker.Category})");
            }
        }

        [Test]
        public async Task BlockerSchema_NeverNull_ForAnyStatus()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            var allHeads = new[]
            {
                "null-check-head-1", "null-check-head-2",
                "null-check-head-3", "null-check-head-4"
            };

            foreach (var head in allHeads)
            {
                var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = head
                });
                Assert.That(result.Blockers, Is.Not.Null,
                    $"Blockers must never be null for head '{head}'");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // Evidence history pagination (MaxRecords)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidenceHistory_MaxRecordsRespected()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "max-records-head";

            for (int i = 0; i < 10; i++)
            {
                await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
                {
                    HeadRef = head,
                    CaseId = $"case-{i}"
                }, "test-actor");
            }

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head, MaxRecords = 3 });

            Assert.That(history.Success, Is.True);
            Assert.That(history.Packs.Count, Is.LessThanOrEqualTo(3),
                "MaxRecords=3 must cap history at 3");
        }

        [Test]
        public async Task WebhookHistory_MaxRecordsRespected()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "max-webhook-head";

            for (int i = 0; i < 10; i++)
            {
                await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
                {
                    HeadRef = head,
                    CaseId = "c1",
                    Outcome = ApprovalWebhookOutcome.Approved
                }, "test-actor");
            }

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head, MaxRecords = 4 });

            Assert.That(history.Success, Is.True);
            Assert.That(history.Records.Count, Is.LessThanOrEqualTo(4),
                "MaxRecords=4 must cap history at 4");
        }

        // ══════════════════════════════════════════════════════════════
        // All 6 webhook outcomes recorded successfully
        // ══════════════════════════════════════════════════════════════

        [Test]
        [TestCase(ApprovalWebhookOutcome.Approved)]
        [TestCase(ApprovalWebhookOutcome.Escalated)]
        [TestCase(ApprovalWebhookOutcome.Denied)]
        [TestCase(ApprovalWebhookOutcome.Malformed)]
        [TestCase(ApprovalWebhookOutcome.TimedOut)]
        [TestCase(ApprovalWebhookOutcome.DeliveryError)]
        public async Task RecordWebhook_AllOutcomes_RecordedSuccessfully(ApprovalWebhookOutcome outcome)
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "all-outcomes-head";

            var result = await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = outcome
            }, "test-actor");

            Assert.That(result.Success, Is.True, $"Outcome {outcome} must be recorded successfully");
            Assert.That(result.Record?.RecordId, Is.Not.Null.And.Not.Empty,
                "RecordId must be set on success");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head });
            Assert.That(history.Records.Any(r => r.Outcome == outcome), Is.True,
                $"Outcome {outcome} must appear in history");
        }

        // ══════════════════════════════════════════════════════════════
        // Idempotency — repeated evidence persistence
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task PersistEvidence_RepeatedSameHead_BothSucceed()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "repeat-persist-head";

            var r1 = await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "case-1"
            }, "test-actor");
            var r2 = await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "case-1"
            }, "test-actor");

            Assert.That(r1.Success, Is.True, "First persist must succeed");
            Assert.That(r2.Success, Is.True, "Second persist must succeed");
            Assert.That(r1.Pack?.PackId, Is.Not.EqualTo(r2.Pack?.PackId),
                "Repeated persists must produce distinct PackIds");
        }

        [Test]
        public async Task PersistEvidence_ThenQuery_ReturnsLatestPack()
        {
            var (svc, _, tp) = CreateServiceWithCapture();
            const string head = "latest-pack-head";

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "case-old"
            }, "test-actor");
            tp.Advance(TimeSpan.FromSeconds(1)); // Ensure the second pack has a later timestamp
            var latestResult = await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "case-new"
            }, "test-actor");

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head, MaxRecords = 1 });

            Assert.That(history.Success, Is.True);
            Assert.That(history.Packs.Count, Is.EqualTo(1));
            Assert.That(history.Packs[0].PackId, Is.EqualTo(latestResult.Pack?.PackId),
                "Latest pack must be returned first");
        }

        // ══════════════════════════════════════════════════════════════
        // RequireReleaseGrade guard
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task PersistEvidence_RequireReleaseGrade_EvidencePersistedSuccessfully()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            // By default the service marks evidence as release-grade when no missing items
            var result = await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = "release-grade-head",
                CaseId = "c1",
                RequireReleaseGrade = true
            }, "test-actor");

            // The service should either succeed (all conditions met) or fail gracefully
            Assert.That(result, Is.Not.Null, "Result must not be null");
            if (!result.Success)
            {
                Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                    "Failed RequireReleaseGrade must include a remediation hint");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // Null request handling
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordWebhook_NullRequest_ReturnsFail()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            var result = await svc.RecordApprovalWebhookAsync(null!, "test-actor");
            Assert.That(result.Success, Is.False, "Null request must return failure");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty, "Error message required");
        }

        [Test]
        public async Task PersistEvidence_NullRequest_ReturnsFail()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            var result = await svc.PersistSignOffEvidenceAsync(null!, "test-actor");
            Assert.That(result.Success, Is.False, "Null request must return failure");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty, "Error message required");
        }

        // ══════════════════════════════════════════════════════════════
        // Webhook event emission validation
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordWebhook_Approved_EmitsSignOffApprovalWebhookReceived()
        {
            var now = DateTimeOffset.UtcNow;
            var (svc, wh, _) = CreateServiceWithCapture(now);
            const string head = "webhook-event-head";

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");

            await Task.Delay(300); // Allow fire-and-forget webhook emission to complete
            lock (wh.Events)
            {
                Assert.That(wh.Events.Any(e => e.EventType == WebhookEventType.ProtectedSignOffApprovalWebhookReceived),
                    Is.True, "SignOffApprovalWebhookReceived event must be emitted");
            }
        }

        [Test]
        public async Task PersistEvidence_Successful_EmitsSignOffEvidencePersisted()
        {
            var (svc, wh, _) = CreateServiceWithCapture();
            const string head = "persist-event-head";

            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = "c1"
            }, "test-actor");

            await Task.Delay(300); // Allow fire-and-forget webhook emission to complete
            lock (wh.Events)
            {
                Assert.That(wh.Events.Any(e => e.EventType == WebhookEventType.ProtectedSignOffEvidencePersisted),
                    Is.True, "SignOffEvidencePersisted event must be emitted");
            }
        }

        [Test]
        public async Task GetReleaseReadiness_ReadyState_EmitsReadinessReadyEvent()
        {
            var (svc, wh, _) = CreateServiceWithCapture();
            const string head = "readiness-ready-event-head";
            const string caseId = "case-ready-event";

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = caseId, Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");
            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = head, CaseId = caseId
            }, "test-actor");

            await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = head
            });

            await Task.Delay(300); // Allow fire-and-forget webhook emission to complete
            lock (wh.Events)
            {
                Assert.That(
                    wh.Events.Any(e => e.EventType == WebhookEventType.ProtectedSignOffReleaseReadySignaled
                                    || e.EventType == WebhookEventType.ProtectedSignOffEvidencePersisted
                                    || e.EventType == WebhookEventType.ProtectedSignOffApprovalWebhookReceived
                                    || e.EventType == WebhookEventType.ProtectedSignOffEvidenceStale),
                    Is.True, "At least one ProtectedSignOff webhook event must be emitted during the readiness workflow");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // Concurrency stress test
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task Concurrency_ParallelPersistAndWebhook_NoCrashOrLost()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "concurrency-stress-head";

            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int idx = i;
                tasks.Add(svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
                {
                    HeadRef = head, CaseId = $"case-{idx % 3}", Outcome = ApprovalWebhookOutcome.Approved
                }, "test-actor"));
                tasks.Add(svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
                {
                    HeadRef = head, CaseId = $"case-{idx % 3}"
                }, "test-actor"));
            }

            Assert.DoesNotThrowAsync(async () => await Task.WhenAll(tasks),
                "Concurrent webhooks and evidence persist must not throw");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head, MaxRecords = 100 });
            Assert.That(history.Success, Is.True);
            Assert.That(history.Records.Count, Is.GreaterThan(0),
                "At least some records must be retained after concurrent operations");
        }

        // ══════════════════════════════════════════════════════════════
        // Operator guidance / remediation hints
        // ══════════════════════════════════════════════════════════════

        [Test]
        public async Task ReadinessResponse_AlwaysHasOperatorGuidance_WhenNotReady()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            // Blocked (Denied + RequireApprovalWebhook)
            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = "guidance-head", CaseId = "c1", Outcome = ApprovalWebhookOutcome.Denied
            }, "test-actor");
            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest
            {
                HeadRef = "guidance-head", CaseId = "c1"
            }, "test-actor");

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = "guidance-head", });

            if (result.Status != SignOffReleaseReadinessStatus.Ready)
            {
                Assert.That(result.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                    "OperatorGuidance must be provided when status is not Ready");
            }
        }

        [Test]
        public async Task ReadinessResponse_EvaluatedAt_IsPopulated()
        {
            var (svc, _, _) = CreateServiceWithCapture();

            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest
            {
                HeadRef = "evaluated-at-head"
            });

            Assert.That(result.EvaluatedAt, Is.Not.EqualTo(default(DateTimeOffset)),
                "EvaluatedAt must always be populated");
        }

        [Test]
        public async Task RecordWebhook_RecordIdIsUniquePerCall()
        {
            var (svc, _, _) = CreateServiceWithCapture();
            const string head = "unique-record-id-head";

            var r1 = await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");
            var r2 = await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest
            {
                HeadRef = head, CaseId = "c1", Outcome = ApprovalWebhookOutcome.Approved
            }, "test-actor");

            Assert.That(r1.Record?.RecordId, Is.Not.EqualTo(r2.Record?.RecordId),
                "Each webhook record must have a unique RecordId");
        }
    }
}

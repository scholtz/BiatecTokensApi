using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.OperatorNotification;
using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for <see cref="OperatorNotificationCenterService"/> and
    /// <see cref="BiatecTokensApi.Controllers.OperatorNotificationCenterController"/>.
    ///
    /// Coverage:
    ///  ONC01-ONC10  – Unit: service lifecycle (mark-read, acknowledge, dismiss)
    ///  ONC11-ONC20  – Unit: filtering (severity, eventType, unreadOnly, excludeDismissed, dateRange)
    ///  ONC21-ONC30  – Unit: inbox summary and unread-count
    ///  ONC31-ONC40  – Unit: per-operator isolation and idempotency
    ///  ONC41-ONC50  – Integration (HTTP): deployed controller endpoints with JWT auth
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class OperatorNotificationCenterTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static ComplianceEventEnvelope MakeEvent(
            string id,
            ComplianceEventType type,
            ComplianceEventSeverity severity,
            DateTimeOffset? timestamp = null,
            string? caseId = null,
            string? subjectId = null) =>
            new()
            {
                EventId = id,
                EventType = type,
                EntityKind = ComplianceEventEntityKind.ComplianceCase,
                EntityId = id,
                CaseId = caseId,
                SubjectId = subjectId,
                Timestamp = timestamp ?? DateTimeOffset.Parse("2026-03-28T09:00:00Z"),
                Severity = severity,
                Source = ComplianceEventSource.System,
                Freshness = ComplianceEventFreshness.Current,
                DeliveryStatus = ComplianceEventDeliveryStatus.NotAttempted,
                Label = $"Event {id}",
                Summary = $"Summary for {id}"
            };

        private static FakeComplianceEventBackbone MakeBackbone(params ComplianceEventEnvelope[] events) =>
            new() { Events = events.ToList() };

        private static OperatorNotificationCenterService MakeService(
            FakeComplianceEventBackbone backbone,
            FakeTimeProvider? clock = null) =>
            new(backbone, NullLogger<OperatorNotificationCenterService>.Instance, clock);

        // ── ONC01: unread by default ──────────────────────────────────────────

        [Test]
        public async Task ONC01_NewEvents_AreUnreadByDefault()
        {
            var evt = MakeEvent("e1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt));

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Unread));
            Assert.That(result.Notifications[0].ReadAt, Is.Null);
            Assert.That(result.Notifications[0].AcknowledgedAt, Is.Null);
            Assert.That(result.Notifications[0].DismissedAt, Is.Null);
        }

        // ── ONC02: mark-read transitions Unread → Read ────────────────────────

        [Test]
        public async Task ONC02_MarkAsRead_TransitionsUnreadToRead()
        {
            var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-28T10:00:00Z"));
            var evt = MakeEvent("e2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning);
            var svc = MakeService(MakeBackbone(evt), clock);

            // Seed state
            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");

            var markResult = await svc.MarkAsReadAsync(
                new MarkNotificationsReadRequest { NotificationIds = ["e2"] }, "op-1");

            Assert.That(markResult.Success, Is.True);
            Assert.That(markResult.AffectedCount, Is.EqualTo(1));
            Assert.That(markResult.AppliedState, Is.EqualTo(NotificationLifecycleState.Read));
            Assert.That(markResult.ActionedAt, Is.EqualTo(clock.GetUtcNow()));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Read));
            Assert.That(list.Notifications[0].ReadAt, Is.EqualTo(clock.GetUtcNow()));
        }

        // ── ONC03: acknowledge transitions Unread/Read → Acknowledged ─────────

        [Test]
        public async Task ONC03_Acknowledge_TransitionsToAcknowledged()
        {
            var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-28T10:05:00Z"));
            var evt = MakeEvent("e3", ComplianceEventType.ComplianceEscalationRaised, ComplianceEventSeverity.Critical);
            var svc = MakeService(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");

            var ackResult = await svc.AcknowledgeAsync(
                new AcknowledgeNotificationsRequest
                {
                    NotificationIds = ["e3"],
                    OperatorNote = "Reviewed and escalated to compliance team."
                }, "op-1");

            Assert.That(ackResult.Success, Is.True);
            Assert.That(ackResult.AppliedState, Is.EqualTo(NotificationLifecycleState.Acknowledged));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Acknowledged));
            Assert.That(list.Notifications[0].AcknowledgedAt, Is.Not.Null);
            Assert.That(list.Notifications[0].OperatorNote, Is.EqualTo("Reviewed and escalated to compliance team."));
        }

        // ── ONC04: dismiss transitions any state → Dismissed ──────────────────

        [Test]
        public async Task ONC04_Dismiss_TransitionsToDismissed()
        {
            var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-28T10:10:00Z"));
            var evt = MakeEvent("e4", ComplianceEventType.ComplianceDecisionRecorded, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");

            var dismissResult = await svc.DismissAsync(
                new DismissNotificationsRequest
                {
                    NotificationIds = ["e4"],
                    OperatorNote = "No further action required."
                }, "op-1");

            Assert.That(dismissResult.Success, Is.True);
            Assert.That(dismissResult.AppliedState, Is.EqualTo(NotificationLifecycleState.Dismissed));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Dismissed));
            Assert.That(list.Notifications[0].DismissedAt, Is.Not.Null);
            Assert.That(list.Notifications[0].OperatorNote, Is.EqualTo("No further action required."));
        }

        // ── ONC05: full lifecycle chain Unread → Read → Acknowledged → Dismissed

        [Test]
        public async Task ONC05_FullLifecycleChain_AuditTimestampsPreserved()
        {
            var t0 = DateTimeOffset.Parse("2026-03-28T08:00:00Z");
            var clock = new FakeTimeProvider(t0);
            var evt = MakeEvent("e5", ComplianceEventType.ComplianceCaseEvidenceStale, ComplianceEventSeverity.Warning);
            var svc = MakeService(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");

            clock.Advance(TimeSpan.FromMinutes(1));
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["e5"] }, "op-1");

            clock.Advance(TimeSpan.FromMinutes(2));
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["e5"] }, "op-1");

            clock.Advance(TimeSpan.FromMinutes(3));
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["e5"] }, "op-1");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-1");
            var n = list.Notifications[0];

            Assert.That(n.ReadAt, Is.EqualTo(t0.AddMinutes(1)));
            Assert.That(n.AcknowledgedAt, Is.EqualTo(t0.AddMinutes(3)));
            Assert.That(n.DismissedAt, Is.EqualTo(t0.AddMinutes(6)));
            Assert.That(n.LifecycleState, Is.EqualTo(NotificationLifecycleState.Dismissed));
        }

        // ── ONC06: bulk mark-read when no IDs specified ───────────────────────

        [Test]
        public async Task ONC06_BulkMarkAsRead_MarksAllUnread()
        {
            var evt1 = MakeEvent("b1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational);
            var evt2 = MakeEvent("b2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var evt3 = MakeEvent("b3", ComplianceEventType.ComplianceEvidenceGenerated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt1, evt2, evt3));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-bulk");

            var result = await svc.MarkAsReadAsync(new MarkNotificationsReadRequest(), "op-bulk");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedCount, Is.EqualTo(3));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-bulk");
            Assert.That(list.Notifications.All(n => n.LifecycleState == NotificationLifecycleState.Read), Is.True);
        }

        // ── ONC07: bulk dismiss when no IDs specified ─────────────────────────

        [Test]
        public async Task ONC07_BulkDismiss_DismissesAllNonDismissed()
        {
            var evt1 = MakeEvent("d1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var evt2 = MakeEvent("d2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning);
            var svc = MakeService(MakeBackbone(evt1, evt2));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-dismiss-all");

            var result = await svc.DismissAsync(new DismissNotificationsRequest(), "op-dismiss-all");

            Assert.That(result.AffectedCount, Is.EqualTo(2));
            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-dismiss-all");
            Assert.That(list.Notifications.All(n => n.LifecycleState == NotificationLifecycleState.Dismissed), Is.True);
        }

        // ── ONC08: mark-read is idempotent for already-read notifications ──────

        [Test]
        public async Task ONC08_MarkAsRead_IsIdempotentForAlreadyRead()
        {
            var evt = MakeEvent("i1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-idem");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["i1"] }, "op-idem");

            // Second mark-read should affect 0 (already Read)
            var result = await svc.MarkAsReadAsync(
                new MarkNotificationsReadRequest { NotificationIds = ["i1"] }, "op-idem");
            Assert.That(result.AffectedCount, Is.EqualTo(0));
        }

        // ── ONC09: dismiss is idempotent for already-dismissed ─────────────────

        [Test]
        public async Task ONC09_Dismiss_IsIdempotentForAlreadyDismissed()
        {
            var evt = MakeEvent("i2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-idem2");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["i2"] }, "op-idem2");

            var result = await svc.DismissAsync(
                new DismissNotificationsRequest { NotificationIds = ["i2"] }, "op-idem2");
            Assert.That(result.AffectedCount, Is.EqualTo(0));
        }

        // ── ONC10: acknowledge auto-sets ReadAt if not yet read ───────────────

        [Test]
        public async Task ONC10_Acknowledge_SetsReadAtIfNotPreviouslyRead()
        {
            var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-28T12:00:00Z"));
            var evt = MakeEvent("a1", ComplianceEventType.ComplianceEscalationRaised, ComplianceEventSeverity.Critical);
            var svc = MakeService(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-autoread");

            await svc.AcknowledgeAsync(
                new AcknowledgeNotificationsRequest { NotificationIds = ["a1"] }, "op-autoread");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-autoread");
            var n = list.Notifications[0];

            // ReadAt should be set automatically even though MarkAsRead was never called
            Assert.That(n.ReadAt, Is.Not.Null);
            Assert.That(n.AcknowledgedAt, Is.Not.Null);
            Assert.That(n.LifecycleState, Is.EqualTo(NotificationLifecycleState.Acknowledged));
        }

        // ── ONC11: filter by severity ──────────────────────────────────────────

        [Test]
        public async Task ONC11_FilterBySeverity_ReturnsCriticalOnly()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("s1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("s2", ComplianceEventType.ComplianceEscalationRaised, ComplianceEventSeverity.Critical),
                MakeEvent("s3", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning)));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { Severity = ComplianceEventSeverity.Critical }, "op-sev");

            Assert.That(result.Notifications.All(n => n.Event.Severity == ComplianceEventSeverity.Critical), Is.True);
            Assert.That(result.Notifications, Has.Count.EqualTo(1));
        }

        // ── ONC12: filter unreadOnly ───────────────────────────────────────────

        [Test]
        public async Task ONC12_FilterUnreadOnly_ExcludesReadAndAcknowledged()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("u1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("u2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning),
                MakeEvent("u3", ComplianceEventType.ComplianceEvidenceGenerated, ComplianceEventSeverity.Informational)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-unread");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["u1"] }, "op-unread");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["u2"] }, "op-unread");

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { UnreadOnly = true }, "op-unread");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].NotificationId, Is.EqualTo("u3"));
        }

        // ── ONC13: filter excludeDismissed ─────────────────────────────────────

        [Test]
        public async Task ONC13_ExcludeDismissed_HidesDismissedNotifications()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("ex1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("ex2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-excl");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["ex1"] }, "op-excl");

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { ExcludeDismissed = true }, "op-excl");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].NotificationId, Is.EqualTo("ex2"));
        }

        // ── ONC14: filter by lifecycle state ───────────────────────────────────

        [Test]
        public async Task ONC14_FilterByLifecycleState_ReturnsMatchingStateOnly()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("ls1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("ls2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ls");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["ls1"] }, "op-ls");

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { LifecycleState = NotificationLifecycleState.Read }, "op-ls");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].NotificationId, Is.EqualTo("ls1"));
        }

        // ── ONC15: filter by event type ────────────────────────────────────────

        [Test]
        public async Task ONC15_FilterByEventType_ReturnsSingleType()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("et1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("et2", ComplianceEventType.ComplianceEscalationRaised, ComplianceEventSeverity.Critical),
                MakeEvent("et3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational)));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { EventType = ComplianceEventType.ComplianceCaseCreated }, "op-et");

            Assert.That(result.Notifications, Has.Count.EqualTo(2));
            Assert.That(result.Notifications.All(n => n.Event.EventType == ComplianceEventType.ComplianceCaseCreated), Is.True);
        }

        // ── ONC16: filter by date range ────────────────────────────────────────

        [Test]
        public async Task ONC16_FilterByDateRange_ReturnsEventsInRange()
        {
            var t1 = DateTimeOffset.Parse("2026-03-10T09:00:00Z");
            var t2 = DateTimeOffset.Parse("2026-03-20T09:00:00Z");
            var t3 = DateTimeOffset.Parse("2026-03-30T09:00:00Z");

            var svc = MakeService(MakeBackbone(
                MakeEvent("dr1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational, t1),
                MakeEvent("dr2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational, t2),
                MakeEvent("dr3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational, t3)));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest
                {
                    FromDate = DateTimeOffset.Parse("2026-03-15T00:00:00Z"),
                    ToDate = DateTimeOffset.Parse("2026-03-25T00:00:00Z")
                }, "op-dr");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].NotificationId, Is.EqualTo("dr2"));
        }

        // ── ONC17: pagination returns correct page ─────────────────────────────

        [Test]
        public async Task ONC17_Pagination_ReturnsCorrectPage()
        {
            var events = Enumerable.Range(1, 10)
                .Select(i => MakeEvent(
                    $"pg{i:D2}",
                    ComplianceEventType.ComplianceCaseCreated,
                    ComplianceEventSeverity.Informational,
                    DateTimeOffset.Parse($"2026-03-{10 + i:D2}T09:00:00Z")))
                .ToArray();

            var svc = MakeService(MakeBackbone(events));

            var page1 = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { Page = 1, PageSize = 4 }, "op-pg");
            var page2 = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { Page = 2, PageSize = 4 }, "op-pg");

            Assert.That(page1.TotalCount, Is.EqualTo(10));
            Assert.That(page1.Notifications, Has.Count.EqualTo(4));
            Assert.That(page2.Notifications, Has.Count.EqualTo(4));

            // No overlap
            var ids1 = page1.Notifications.Select(n => n.NotificationId).ToHashSet();
            var ids2 = page2.Notifications.Select(n => n.NotificationId).ToHashSet();
            Assert.That(ids1.Intersect(ids2), Is.Empty);
        }

        // ── ONC18: ordering is descending by creation time ─────────────────────

        [Test]
        public async Task ONC18_Ordering_IsDescendingByTimestamp()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("o1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational,
                    DateTimeOffset.Parse("2026-03-01T09:00:00Z")),
                MakeEvent("o2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational,
                    DateTimeOffset.Parse("2026-03-03T09:00:00Z")),
                MakeEvent("o3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational,
                    DateTimeOffset.Parse("2026-03-02T09:00:00Z"))));

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ord");

            var ids = result.Notifications.Select(n => n.NotificationId).ToList();
            Assert.That(ids, Is.EqualTo(new[] { "o2", "o3", "o1" }));
        }

        // ── ONC19: caseId filter scopes to matching case ───────────────────────

        [Test]
        public async Task ONC19_FilterByCaseId_ScopesToCase()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("c1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational,
                    caseId: "case-alpha"),
                MakeEvent("c2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    caseId: "case-beta")));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { CaseId = "case-alpha" }, "op-case");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].Event.CaseId, Is.EqualTo("case-alpha"));
        }

        // ── ONC20: malformed/empty request succeeds gracefully ────────────────

        [Test]
        public async Task ONC20_EmptyBackbone_ReturnsEmptyResultsSuccessfully()
        {
            var svc = MakeService(MakeBackbone());

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-empty");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Notifications, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        }

        // ── ONC21: inbox summary counts are correct ────────────────────────────

        [Test]
        public async Task ONC21_InboxSummary_CountsAreCorrect()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("sum1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical),
                MakeEvent("sum2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning),
                MakeEvent("sum3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("sum4", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("sum5", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational)));

            // Seed
            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-sum");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["sum2"] }, "op-sum");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["sum3"] }, "op-sum");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["sum4"] }, "op-sum");

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-sum");
            var summary = result.InboxSummary;

            Assert.That(summary.UnreadCount, Is.EqualTo(2));  // sum1, sum5
            Assert.That(summary.ReadCount, Is.EqualTo(1));    // sum2
            Assert.That(summary.AcknowledgedCount, Is.EqualTo(1)); // sum3
            Assert.That(summary.DismissedCount, Is.EqualTo(1));    // sum4
            Assert.That(summary.TotalActiveCount, Is.EqualTo(4));  // Unread+Read+Acked
            Assert.That(summary.ActiveBlockerCount, Is.EqualTo(1)); // sum1 is Critical+Unread
            Assert.That(summary.ActiveWarningCount, Is.EqualTo(1)); // sum2 is Warning+Read
        }

        // ── ONC22: unread count endpoint ──────────────────────────────────────

        [Test]
        public async Task ONC22_GetUnreadCount_ReturnsCorrectCounts()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("uc1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical),
                MakeEvent("uc2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning),
                MakeEvent("uc3", ComplianceEventType.ComplianceDecisionRecorded, ComplianceEventSeverity.Informational)));

            // Make uc3 read
            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-uc");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["uc3"] }, "op-uc");

            var count = await svc.GetUnreadCountAsync("op-uc");

            Assert.That(count.Success, Is.True);
            Assert.That(count.UnreadCount, Is.EqualTo(2));      // uc1, uc2 still unread
            Assert.That(count.CriticalUnreadCount, Is.EqualTo(1)); // only uc1 is Critical
        }

        // ── ONC23: inbox summary updates after dismiss ─────────────────────────

        [Test]
        public async Task ONC23_InboxSummary_UpdatesAfterDismiss()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("upd1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical),
                MakeEvent("upd2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-upd");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["upd1", "upd2"] }, "op-upd");

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-upd");
            var summary = result.InboxSummary;

            Assert.That(summary.UnreadCount, Is.EqualTo(0));
            Assert.That(summary.DismissedCount, Is.EqualTo(2));
            Assert.That(summary.TotalActiveCount, Is.EqualTo(0));
            Assert.That(summary.ActiveBlockerCount, Is.EqualTo(0));
        }

        // ── ONC24: inbox summary TotalActiveCount excludes dismissed ──────────

        [Test]
        public async Task ONC24_TotalActiveCount_ExcludesDismissed()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("ta1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("ta2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("ta3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ta");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["ta3"] }, "op-ta");

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ta");
            Assert.That(result.InboxSummary.TotalActiveCount, Is.EqualTo(2));
        }

        // ── ONC25: comutedAt reflects evaluation time ──────────────────────────

        [Test]
        public async Task ONC25_InboxSummary_ComputedAtIsNow()
        {
            var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-28T15:00:00Z"));
            var svc = MakeService(MakeBackbone(
                MakeEvent("ca1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational)),
                clock);

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ca");

            Assert.That(result.InboxSummary.ComputedAt, Is.EqualTo(clock.GetUtcNow()));
        }

        // ── ONC31: operator isolation — states are per-operator ───────────────

        [Test]
        public async Task ONC31_OperatorIsolation_StateIsPerOperator()
        {
            var evt = MakeEvent("iso1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeService(MakeBackbone(evt));

            // op-a marks as read
            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-a");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["iso1"] }, "op-a");

            // op-b has not interacted yet — should still be Unread
            var resultB = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-b");
            Assert.That(resultB.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Unread));

            // op-a should still be Read
            var resultA = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-a");
            Assert.That(resultA.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Read));
        }

        // ── ONC32: operator isolation — dismiss does not affect other operators

        [Test]
        public async Task ONC32_OperatorIsolation_DismissDoesNotAffectOthers()
        {
            var evt = MakeEvent("iso2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Critical);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-x");
            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-y");

            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["iso2"] }, "op-x");

            var resultY = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-y");
            Assert.That(resultY.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Unread));
        }

        // ── ONC33: operator note is per-operator ──────────────────────────────

        [Test]
        public async Task ONC33_OperatorNote_IsPerOperator()
        {
            var evt = MakeEvent("note1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-note-a");
            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-note-b");

            await svc.AcknowledgeAsync(
                new AcknowledgeNotificationsRequest { NotificationIds = ["note1"], OperatorNote = "Note from A" },
                "op-note-a");
            await svc.AcknowledgeAsync(
                new AcknowledgeNotificationsRequest { NotificationIds = ["note1"], OperatorNote = "Note from B" },
                "op-note-b");

            var listA = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-note-a");
            var listB = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-note-b");

            Assert.That(listA.Notifications[0].OperatorNote, Is.EqualTo("Note from A"));
            Assert.That(listB.Notifications[0].OperatorNote, Is.EqualTo("Note from B"));
        }

        // ── ONC34: acknowledge on already-acknowledged is idempotent ──────────

        [Test]
        public async Task ONC34_Acknowledge_IsIdempotentForAlreadyAcknowledged()
        {
            var evt = MakeEvent("idem3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-idem3");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["idem3"] }, "op-idem3");

            var result = await svc.AcknowledgeAsync(
                new AcknowledgeNotificationsRequest { NotificationIds = ["idem3"] }, "op-idem3");
            Assert.That(result.AffectedCount, Is.EqualTo(0));
        }

        // ── ONC35: explicitly provided IDs in bulk mark-read take priority ────

        [Test]
        public async Task ONC35_ExplicitIds_OnlySpecifiedNotificationsAreAffected()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("sp1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("sp2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning),
                MakeEvent("sp3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-sp");

            var result = await svc.MarkAsReadAsync(
                new MarkNotificationsReadRequest { NotificationIds = ["sp1", "sp3"] }, "op-sp");
            Assert.That(result.AffectedCount, Is.EqualTo(2));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-sp");
            var sp2 = list.Notifications.Single(n => n.NotificationId == "sp2");
            Assert.That(sp2.LifecycleState, Is.EqualTo(NotificationLifecycleState.Unread));
        }

        // ── ONC36: bulk acknowledge scoped to caseId ──────────────────────────

        [Test]
        public async Task ONC36_BulkAcknowledge_ScopedByCaseId()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("ba1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational, caseId: "case-A"),
                MakeEvent("ba2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning, caseId: "case-A"),
                MakeEvent("ba3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical, caseId: "case-B")));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ba");

            var result = await svc.AcknowledgeAsync(
                new AcknowledgeNotificationsRequest { CaseId = "case-A" }, "op-ba");
            Assert.That(result.AffectedCount, Is.EqualTo(2));

            // case-B notification should remain unread
            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ba");
            var ba3 = list.Notifications.Single(n => n.NotificationId == "ba3");
            Assert.That(ba3.LifecycleState, Is.EqualTo(NotificationLifecycleState.Unread));
        }

        // ── ONC37: lastActorId is recorded on each transition ─────────────────

        [Test]
        public async Task ONC37_LastActorId_IsRecordedOnTransition()
        {
            var evt = MakeEvent("act1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "reviewer-99");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["act1"] }, "reviewer-99");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "reviewer-99");
            Assert.That(list.Notifications[0].LastActorId, Is.EqualTo("reviewer-99"));
        }

        // ── ONC38: mark-read does not affect dismissed notifications ──────────

        [Test]
        public async Task ONC38_MarkAsRead_DoesNotAffectDismissed()
        {
            var evt = MakeEvent("dm1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeService(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-dm");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["dm1"] }, "op-dm");

            // mark-read should not transition Dismissed → Read
            var result = await svc.MarkAsReadAsync(
                new MarkNotificationsReadRequest { NotificationIds = ["dm1"] }, "op-dm");
            Assert.That(result.AffectedCount, Is.EqualTo(0));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-dm");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Dismissed));
        }

        // ── ONC39: subjectId filter scopes results ─────────────────────────────

        [Test]
        public async Task ONC39_FilterBySubjectId_ScopesToSubject()
        {
            var svc = MakeService(MakeBackbone(
                MakeEvent("sub1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational,
                    subjectId: "subject-alice"),
                MakeEvent("sub2", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational,
                    subjectId: "subject-bob")));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { SubjectId = "subject-alice" }, "op-sub");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].NotificationId, Is.EqualTo("sub1"));
        }

        // ── ONC40: pageSize clamped to 100 ────────────────────────────────────

        [Test]
        public async Task ONC40_PageSize_IsClampedToMax100()
        {
            var events = Enumerable.Range(1, 5)
                .Select(i => MakeEvent($"cl{i}", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational))
                .ToArray();
            var svc = MakeService(MakeBackbone(events));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { PageSize = 9999 }, "op-clamp");

            Assert.That(result.PageSize, Is.LessThanOrEqualTo(100));
        }

        // ── ONC41: HTTP GET returns 401 when unauthenticated ─────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC41_HTTP_GetNotifications_Returns401WhenUnauthenticated()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/operator-notifications");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── ONC42: HTTP GET returns 200 with valid JWT ─────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC42_HTTP_GetNotifications_Returns200WithValidJwt()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/api/v1/operator-notifications");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<OperatorNotificationListResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
        }

        // ── ONC43: HTTP GET unread-count returns 200 ─────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC43_HTTP_GetUnreadCount_Returns200()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/api/v1/operator-notifications/unread-count");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationUnreadCountResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
        }

        // ── ONC44: HTTP POST mark-read returns 200 ───────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC44_HTTP_MarkAsRead_Returns200()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/mark-read",
                new MarkNotificationsReadRequest());

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Read));
        }

        // ── ONC45: HTTP POST acknowledge returns 200 ─────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC45_HTTP_Acknowledge_Returns200()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/acknowledge",
                new AcknowledgeNotificationsRequest { OperatorNote = "Reviewed by operator." });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Acknowledged));
        }

        // ── ONC46: HTTP POST dismiss returns 200 ─────────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC46_HTTP_Dismiss_Returns200()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/dismiss",
                new DismissNotificationsRequest { OperatorNote = "No further action required." });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Dismissed));
        }

        // ── ONC47: HTTP lifecycle E2E – mark-read then acknowledge then dismiss

        [Test]
        [NonParallelizable]
        public async Task ONC47_HTTP_LifecycleE2E_MarkReadAcknowledgeDismiss()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Get notifications
            var listResp = await client.GetAsync("/api/v1/operator-notifications");
            var list = await listResp.Content.ReadFromJsonAsync<OperatorNotificationListResponse>();
            Assert.That(list, Is.Not.Null);
            Assert.That(list!.Success, Is.True);

            // 2. Mark all as read
            var markResp = await client.PostAsJsonAsync("/api/v1/operator-notifications/mark-read",
                new MarkNotificationsReadRequest());
            var markResult = await markResp.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(markResult, Is.Not.Null);
            Assert.That(markResult!.Success, Is.True);

            // 3. Acknowledge all
            var ackResp = await client.PostAsJsonAsync("/api/v1/operator-notifications/acknowledge",
                new AcknowledgeNotificationsRequest { OperatorNote = "All reviewed." });
            var ackResult = await ackResp.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(ackResult, Is.Not.Null);
            Assert.That(ackResult!.Success, Is.True);

            // 4. Dismiss all
            var dismissResp = await client.PostAsJsonAsync("/api/v1/operator-notifications/dismiss",
                new DismissNotificationsRequest { OperatorNote = "Clearing queue." });
            var dismissResult = await dismissResp.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(dismissResult, Is.Not.Null);
            Assert.That(dismissResult!.Success, Is.True);

            // 5. Confirm unread count is now 0
            var countResp = await client.GetAsync("/api/v1/operator-notifications/unread-count");
            var count = await countResp.Content.ReadFromJsonAsync<NotificationUnreadCountResponse>();
            Assert.That(count, Is.Not.Null);
            Assert.That(count!.Success, Is.True);
            Assert.That(count.UnreadCount, Is.EqualTo(0));
        }

        // ── ONC48: HTTP mark-read 401 without token ───────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC48_HTTP_MarkAsRead_Returns401WhenUnauthenticated()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/mark-read",
                new MarkNotificationsReadRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── ONC49: schema contract – all required response fields are present ──

        [Test]
        [NonParallelizable]
        public async Task ONC49_HTTP_ResponseSchema_RequiredFieldsPresent()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/api/v1/operator-notifications");
            var result = await response.Content.ReadFromJsonAsync<OperatorNotificationListResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.InboxSummary, Is.Not.Null);
            Assert.That(result.InboxSummary.ComputedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(result.Page, Is.GreaterThan(0));
            Assert.That(result.PageSize, Is.GreaterThan(0));
        }

        // ── ONC50: HTTP unread-count 401 without token ────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC50_HTTP_GetUnreadCount_Returns401WhenUnauthenticated()
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/operator-notifications/unread-count");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Fakes and factories ──────────────────────────────────────────────

        private sealed class FakeComplianceEventBackbone : IComplianceEventBackboneService
        {
            public List<ComplianceEventEnvelope> Events { get; init; } = new();

            public Task<ComplianceEventListResponse> GetEventsAsync(
                ComplianceEventQueryRequest request,
                string actorId)
            {
                IEnumerable<ComplianceEventEnvelope> filtered = Events;

                if (!string.IsNullOrWhiteSpace(request.CaseId))
                    filtered = filtered.Where(e => e.CaseId == request.CaseId);

                if (!string.IsNullOrWhiteSpace(request.SubjectId))
                    filtered = filtered.Where(e => e.SubjectId == request.SubjectId);

                if (!string.IsNullOrWhiteSpace(request.EntityId))
                    filtered = filtered.Where(e => e.EntityId == request.EntityId);

                if (request.Severity.HasValue)
                    filtered = filtered.Where(e => e.Severity == request.Severity.Value);

                if (request.EventType.HasValue)
                    filtered = filtered.Where(e => e.EventType == request.EventType.Value);

                if (request.EntityKind.HasValue)
                    filtered = filtered.Where(e => e.EntityKind == request.EntityKind.Value);

                var list = filtered.ToList();

                return Task.FromResult(new ComplianceEventListResponse
                {
                    Success = true,
                    Events = list,
                    TotalCount = list.Count,
                    Page = request.Page,
                    PageSize = request.PageSize
                });
            }

            public Task<ComplianceEventQueueSummaryResponse> GetQueueSummaryAsync(
                ComplianceEventQueryRequest request,
                string actorId) =>
                Task.FromResult(new ComplianceEventQueueSummaryResponse { Success = true });
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;

            public FakeTimeProvider(DateTimeOffset now) => _now = now;

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan delta) => _now += delta;
        }

        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForOperatorNotifTests32Chars!!",
                        ["JwtConfig:SecretKey"] = "TestSecretKeyForOperatorNotifTests32Chars!",
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
                        ["KycConfig:MockAutoApprove"] = "true",
                        ["StripeConfig:SecretKey"] = "sk_test_placeholder",
                        ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
                        ["StripeConfig:WebhookSecret"] = "whsec_placeholder",
                    });
                });
            }
        }

        private static async Task<string> GetJwtAsync(HttpClient client)
        {
            var email = $"operator-notif-{Guid.NewGuid():N}@test.biatec.io";
            const string password = "OperatorTest123!";

            await client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });

            var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });

            var loginJson = await loginResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return loginJson.GetProperty("accessToken").GetString()
                   ?? throw new InvalidOperationException("Could not retrieve access token.");
        }
    }
}

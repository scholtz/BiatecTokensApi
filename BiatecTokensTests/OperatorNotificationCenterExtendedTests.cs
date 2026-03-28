using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.OperatorNotification;
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
    /// Extended test coverage for <see cref="OperatorNotificationCenterService"/>.
    ///
    /// Coverage:
    ///  ONC51-ONC60  – Unit: Resolve/Reopen lifecycle and audit trail
    ///  ONC61-ONC70  – Unit: Role-aware targeting and workflow area filtering
    ///  ONC71-ONC80  – Unit: Escalation metadata and aged-only filtering
    ///  ONC81-ONC90  – Unit: Digest summary semantics
    ///  ONC91-ONC100 – Integration (HTTP): resolve, reopen, digest endpoints + degraded-state
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class OperatorNotificationCenterExtendedTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static ComplianceEventEnvelope MakeEvent(
            string id,
            ComplianceEventType type,
            ComplianceEventSeverity severity,
            DateTimeOffset? timestamp = null,
            string? caseId = null,
            string? subjectId = null,
            ComplianceEventFreshness freshness = ComplianceEventFreshness.Current) =>
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
                Freshness = freshness,
                DeliveryStatus = ComplianceEventDeliveryStatus.NotAttempted,
                Label = $"Event {id}",
                Summary = $"Summary for {id}"
            };

        private static FakeBackbone MakeBackbone(params ComplianceEventEnvelope[] events) =>
            new() { Events = events.ToList() };

        private static OperatorNotificationCenterService MakeSvc(
            IComplianceEventBackboneService backbone,
            FakeTime? clock = null) =>
            new(backbone, NullLogger<OperatorNotificationCenterService>.Instance, clock);

        // ── ONC51: Resolve single notification ──────────────────────────────

        [Test]
        public async Task ONC51_Resolve_SingleNotification_Succeeds()
        {
            var clock = new FakeTime(DateTimeOffset.Parse("2026-03-28T10:00:00Z"));
            var evt = MakeEvent("r1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeSvc(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-r");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["r1"] }, "op-r");

            var result = await svc.ResolveAsync(
                new ResolveNotificationsRequest { NotificationIds = ["r1"] }, "op-r");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedCount, Is.EqualTo(1));
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Resolved));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-r");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Resolved));
            Assert.That(list.Notifications[0].ResolvedAt, Is.EqualTo(clock.GetUtcNow()));
        }

        // ── ONC52: Resolve from Unread also works ────────────────────────────

        [Test]
        public async Task ONC52_Resolve_FromUnread_Succeeds()
        {
            var evt = MakeEvent("r2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-r2");

            var result = await svc.ResolveAsync(
                new ResolveNotificationsRequest { NotificationIds = ["r2"] }, "op-r2");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedCount, Is.EqualTo(1));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-r2");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Resolved));
        }

        // ── ONC53: Resolve is idempotent for already-resolved ────────────────

        [Test]
        public async Task ONC53_Resolve_AlreadyResolved_IsIdempotent()
        {
            var evt = MakeEvent("r3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-r3");
            await svc.ResolveAsync(new ResolveNotificationsRequest { NotificationIds = ["r3"] }, "op-r3");

            var second = await svc.ResolveAsync(
                new ResolveNotificationsRequest { NotificationIds = ["r3"] }, "op-r3");
            Assert.That(second.AffectedCount, Is.EqualTo(0));
        }

        // ── ONC54: Reopen from Resolved transitions to Reopened ──────────────

        [Test]
        public async Task ONC54_Reopen_FromResolved_TransitionsToReopened()
        {
            var clock = new FakeTime(DateTimeOffset.Parse("2026-03-28T11:00:00Z"));
            var evt = MakeEvent("rp1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeSvc(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rp");
            await svc.ResolveAsync(new ResolveNotificationsRequest { NotificationIds = ["rp1"] }, "op-rp");

            var result = await svc.ReopenAsync(
                new ReopenNotificationsRequest { NotificationIds = ["rp1"] }, "op-rp");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedCount, Is.EqualTo(1));
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Reopened));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rp");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Reopened));
            Assert.That(list.Notifications[0].ReopenedAt, Is.EqualTo(clock.GetUtcNow()));
        }

        // ── ONC55: Reopen from Dismissed transitions to Reopened ─────────────

        [Test]
        public async Task ONC55_Reopen_FromDismissed_TransitionsToReopened()
        {
            var evt = MakeEvent("rp2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rp2");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["rp2"] }, "op-rp2");

            var result = await svc.ReopenAsync(
                new ReopenNotificationsRequest { NotificationIds = ["rp2"] }, "op-rp2");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedCount, Is.EqualTo(1));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rp2");
            Assert.That(list.Notifications[0].LifecycleState, Is.EqualTo(NotificationLifecycleState.Reopened));
        }

        // ── ONC56: Reopen from Acknowledged does nothing ─────────────────────

        [Test]
        public async Task ONC56_Reopen_FromAcknowledged_DoesNothing()
        {
            var evt = MakeEvent("rp3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rp3");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["rp3"] }, "op-rp3");

            var result = await svc.ReopenAsync(
                new ReopenNotificationsRequest { NotificationIds = ["rp3"] }, "op-rp3");
            Assert.That(result.AffectedCount, Is.EqualTo(0));
        }

        // ── ONC57: Audit trail captures all transitions ──────────────────────

        [Test]
        public async Task ONC57_AuditTrail_CapturesAllTransitions()
        {
            var evt = MakeEvent("aud1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-aud");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["aud1"] }, "op-aud");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["aud1"] }, "op-aud");
            await svc.ResolveAsync(new ResolveNotificationsRequest { NotificationIds = ["aud1"] }, "op-aud");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-aud");
            List<NotificationAuditEntry> trail = list.Notifications[0].AuditTrail;

            Assert.That(trail, Has.Count.EqualTo(3));
            Assert.That(trail[0].NewState, Is.EqualTo(NotificationLifecycleState.Read));
            Assert.That(trail[1].NewState, Is.EqualTo(NotificationLifecycleState.Acknowledged));
            Assert.That(trail[2].NewState, Is.EqualTo(NotificationLifecycleState.Resolved));
        }

        // ── ONC58: Audit entry records PreviousState correctly ───────────────

        [Test]
        public async Task ONC58_AuditEntry_RecordsPreviousState()
        {
            var evt = MakeEvent("aud2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-aud2");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest { NotificationIds = ["aud2"] }, "op-aud2");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-aud2");
            NotificationAuditEntry entry = list.Notifications[0].AuditTrail.Last();

            Assert.That(entry.PreviousState, Is.EqualTo(NotificationLifecycleState.Unread));
            Assert.That(entry.NewState, Is.EqualTo(NotificationLifecycleState.Acknowledged));
            Assert.That(entry.ActorId, Is.EqualTo("op-aud2"));
        }

        // ── ONC59: Resolve with note records note in audit trail ─────────────

        [Test]
        public async Task ONC59_Resolve_WithNote_RecordsNoteInAuditTrail()
        {
            var evt = MakeEvent("rn1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeSvc(MakeBackbone(evt));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rn");
            await svc.ResolveAsync(
                new ResolveNotificationsRequest { NotificationIds = ["rn1"], OperatorNote = "Case closed per investigation." },
                "op-rn");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rn");
            NotificationAuditEntry entry = list.Notifications[0].AuditTrail.Last();
            Assert.That(entry.Note, Is.EqualTo("Case closed per investigation."));
            Assert.That(list.Notifications[0].OperatorNote, Is.EqualTo("Case closed per investigation."));
        }

        // ── ONC60: Bulk resolve resolves all non-resolved notifications ───────

        [Test]
        public async Task ONC60_BulkResolve_ResolvesAllAcknowledged()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("br1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("br2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-br");
            await svc.AcknowledgeAsync(new AcknowledgeNotificationsRequest(), "op-br");
            var result = await svc.ResolveAsync(new ResolveNotificationsRequest(), "op-br");

            Assert.That(result.AffectedCount, Is.EqualTo(2));
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Resolved));
        }

        // ── ONC61: KYC event maps to KycOnboarding workflow area ─────────────

        [Test]
        public async Task ONC61_KycEvent_MapsToKycOnboardingWorkflowArea()
        {
            var evt = MakeEvent("wa1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-wa");
            Assert.That(list.Notifications[0].WorkflowArea, Is.EqualTo(NotificationWorkflowArea.KycOnboarding));
        }

        // ── ONC62: Compliance case event maps to ComplianceCase area ─────────

        [Test]
        public async Task ONC62_ComplianceEvent_MapsToComplianceCaseArea()
        {
            var evt = MakeEvent("wa2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-wa2");
            Assert.That(list.Notifications[0].WorkflowArea, Is.EqualTo(NotificationWorkflowArea.ComplianceCase));
        }

        // ── ONC63: ProtectedSignOff event maps to ProtectedSignOff area ───────

        [Test]
        public async Task ONC63_ProtectedSignOffEvent_MapsToProtectedSignOffArea()
        {
            var evt = MakeEvent("wa3", ComplianceEventType.ProtectedSignOffEvidenceCaptured, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-wa3");
            Assert.That(list.Notifications[0].WorkflowArea, Is.EqualTo(NotificationWorkflowArea.ProtectedSignOff));
        }

        // ── ONC64: WorkflowArea filter scopes results ─────────────────────────

        [Test]
        public async Task ONC64_WorkflowAreaFilter_ScopesResults()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("wf1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("wf2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning)));

            var filtered = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { WorkflowArea = NotificationWorkflowArea.KycOnboarding },
                "op-wf");

            Assert.That(filtered.Notifications, Has.Count.EqualTo(1));
            Assert.That(filtered.Notifications[0].NotificationId, Is.EqualTo("wf1"));
        }

        // ── ONC65: AudienceRoles populated for KYC onboarding ────────────────

        [Test]
        public async Task ONC65_AudienceRoles_PopulatedForKycOnboarding()
        {
            var evt = MakeEvent("ar1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ar");
            List<OperatorRole> roles = list.Notifications[0].AudienceRoles;

            Assert.That(roles, Is.Not.Empty);
            Assert.That(roles, Does.Contain(OperatorRole.ComplianceReviewer));
        }

        // ── ONC66: Role filter returns only matching notifications ────────────

        [Test]
        public async Task ONC66_RoleFilter_ReturnsOnlyMatchingNotifications()
        {
            // ComplianceReviewer role should see KYC and compliance events
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("rf1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("rf2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning)));

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { Role = OperatorRole.ComplianceReviewer },
                "op-rf");

            Assert.That(result.Notifications, Is.Not.Empty);
            Assert.That(result.Notifications.All(n => n.AudienceRoles.Contains(OperatorRole.ComplianceReviewer)), Is.True);
        }

        // ── ONC67: Critical event includes Manager in AudienceRoles ──────────

        [Test]
        public async Task ONC67_CriticalEvent_IncludesManagerInAudienceRoles()
        {
            var evt = MakeEvent("cr1", ComplianceEventType.ComplianceEscalationRaised, ComplianceEventSeverity.Critical);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-cr");
            Assert.That(list.Notifications[0].AudienceRoles, Does.Contain(OperatorRole.Manager));
        }

        // ── ONC68: IsActionable = true for Critical severity ─────────────────

        [Test]
        public async Task ONC68_IsActionable_TrueForCriticalSeverity()
        {
            var evt = MakeEvent("ia1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ia");
            Assert.That(list.Notifications[0].IsActionable, Is.True);
        }

        // ── ONC69: IsActionable = true for escalation event types ────────────

        [Test]
        public async Task ONC69_IsActionable_TrueForEscalationEventType()
        {
            var evt = MakeEvent("ia2", ComplianceEventType.ComplianceEscalationRaised, ComplianceEventSeverity.Warning);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ia2");
            Assert.That(list.Notifications[0].IsActionable, Is.True);
        }

        // ── ONC70: IsActionable = false for informational non-escalation ──────

        [Test]
        public async Task ONC70_IsActionable_FalseForInformationalNonEscalation()
        {
            var evt = MakeEvent("ia3", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Informational);
            var svc = MakeSvc(MakeBackbone(evt));

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ia3");
            Assert.That(list.Notifications[0].IsActionable, Is.False);
        }

        // ── ONC71: Fresh notification has Fresh age bucket ────────────────────

        [Test]
        public async Task ONC71_FreshNotification_HasFreshAgeBucket()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("esc1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                timestamp: now.AddMinutes(-30));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-esc");
            Assert.That(list.Notifications[0].EscalationMetadata.AgeBucket, Is.EqualTo(NotificationAgeBucket.Fresh));
        }

        // ── ONC72: Notification >24h is Stale ────────────────────────────────

        [Test]
        public async Task ONC72_OldNotification_HasStaleAgeBucket()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("esc2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                timestamp: now.AddDays(-2));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-esc2");
            NotificationAgeBucket bucket = list.Notifications[0].EscalationMetadata.AgeBucket;
            Assert.That(bucket, Is.EqualTo(NotificationAgeBucket.Stale));
        }

        // ── ONC73: Notification >7d is Overdue ────────────────────────────────

        [Test]
        public async Task ONC73_VeryOldNotification_HasOverdueAgeBucket()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("esc3", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                timestamp: now.AddDays(-10));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-esc3");
            Assert.That(list.Notifications[0].EscalationMetadata.AgeBucket, Is.EqualTo(NotificationAgeBucket.Overdue));
        }

        // ── ONC74: AgedOnly filter returns Stale/Overdue notifications only ────

        [Test]
        public async Task ONC74_AgedOnlyFilter_ReturnsStaleAndOverdueOnly()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("ao1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    timestamp: now.AddMinutes(-10)),   // Fresh
                MakeEvent("ao2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    timestamp: now.AddDays(-4))),      // Stale
            clock);

            var result = await svc.GetNotificationsAsync(
                new OperatorNotificationQueryRequest { AgedOnly = true }, "op-ao");

            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0].NotificationId, Is.EqualTo("ao2"));
        }

        // ── ONC75: Unread critical >24h is escalated and SLA-breached ─────────

        [Test]
        public async Task ONC75_UnreadCritical_OlderThan24h_IsEscalatedAndSlaBreached()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("sl1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical,
                timestamp: now.AddDays(-2));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-sl");
            NotificationEscalationMetadata meta = list.Notifications[0].EscalationMetadata;

            Assert.That(meta.IsEscalated, Is.True);
            Assert.That(meta.IsSlaBreached, Is.True);
            Assert.That(meta.EscalationHint, Is.Not.Null.And.Not.Empty);
        }

        // ── ONC76: Inbox summary EscalatedCount counts escalated items ─────────

        [Test]
        public async Task ONC76_InboxSummary_EscalatedCount_CountsEscalatedItems()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("ec1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    timestamp: now.AddDays(-2)),
                MakeEvent("ec2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational,
                    timestamp: now.AddHours(-1))),
            clock);

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ec");
            Assert.That(result.InboxSummary.EscalatedCount, Is.EqualTo(1));
        }

        // ── ONC77: Dismissed notification is not escalated ───────────────────

        [Test]
        public async Task ONC77_DismissedNotification_IsNotEscalated()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("de1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                timestamp: now.AddDays(-3));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-de");
            await svc.DismissAsync(new DismissNotificationsRequest { NotificationIds = ["de1"] }, "op-de");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-de");
            // After dismiss, lifecycle is Dismissed; IsEscalated is computed from lifecycle state
            Assert.That(list.Notifications[0].EscalationMetadata.IsEscalated, Is.False);
        }

        // ── ONC78: Resolved notification is not escalated ────────────────────

        [Test]
        public async Task ONC78_ResolvedNotification_IsNotEscalated()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("re1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical,
                timestamp: now.AddDays(-3));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-re");
            await svc.ResolveAsync(new ResolveNotificationsRequest { NotificationIds = ["re1"] }, "op-re");

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-re");
            Assert.That(list.Notifications[0].EscalationMetadata.IsEscalated, Is.False);
        }

        // ── ONC79: InboxSummary ResolvedCount counts resolved notifications ────

        [Test]
        public async Task ONC79_InboxSummary_ResolvedCount_CountsResolvedItems()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("rc1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("rc2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rc");
            await svc.ResolveAsync(new ResolveNotificationsRequest { NotificationIds = ["rc1"] }, "op-rc");

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-rc");
            Assert.That(result.InboxSummary.ResolvedCount, Is.EqualTo(1));
        }

        // ── ONC80: AgeHours populated correctly ───────────────────────────────

        [Test]
        public async Task ONC80_AgeHours_PopulatedCorrectly()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var evt = MakeEvent("ah1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                timestamp: now.AddHours(-6));
            var svc = MakeSvc(MakeBackbone(evt), clock);

            var list = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-ah");
            Assert.That(list.Notifications[0].EscalationMetadata.AgeHours, Is.GreaterThanOrEqualTo(6));
        }

        // ── ONC81: GetDigestSummaryAsync returns digest grouped by area ────────

        [Test]
        public async Task ONC81_GetDigestSummary_ReturnsGroupedByWorkflowArea()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("dg1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("dg2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning),
                MakeEvent("dg3", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Informational)));

            var result = await svc.GetDigestSummaryAsync(new NotificationDigestRequest(), "op-dg");

            Assert.That(result.Success, Is.True);
            Assert.That(result.DigestGroups, Is.Not.Empty);
            Assert.That(result.DigestGroups.Select(g => g.WorkflowArea),
                Does.Contain(NotificationWorkflowArea.KycOnboarding));
            Assert.That(result.DigestGroups.Select(g => g.WorkflowArea),
                Does.Contain(NotificationWorkflowArea.ComplianceCase));
        }

        // ── ONC82: Digest filter by WorkflowArea ─────────────────────────────

        [Test]
        public async Task ONC82_DigestFilter_ByWorkflowArea_ScopesGroups()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("dg4", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("dg5", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning)));

            var result = await svc.GetDigestSummaryAsync(
                new NotificationDigestRequest { WorkflowArea = NotificationWorkflowArea.KycOnboarding },
                "op-dg2");

            Assert.That(result.Success, Is.True);
            Assert.That(result.DigestGroups, Has.Count.EqualTo(1));
            Assert.That(result.DigestGroups[0].WorkflowArea, Is.EqualTo(NotificationWorkflowArea.KycOnboarding));
        }

        // ── ONC83: Digest UnreadCount per group correct ───────────────────────

        [Test]
        public async Task ONC83_DigestUnreadCount_PerGroupIsCorrect()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("dg6", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning),
                MakeEvent("dg7", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Informational)));

            await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-dg3");
            await svc.MarkAsReadAsync(new MarkNotificationsReadRequest { NotificationIds = ["dg6"] }, "op-dg3");

            var result = await svc.GetDigestSummaryAsync(
                new NotificationDigestRequest { WorkflowArea = NotificationWorkflowArea.ComplianceCase },
                "op-dg3");

            NotificationDigestSummary group = result.DigestGroups.Single(g => g.WorkflowArea == NotificationWorkflowArea.ComplianceCase);
            Assert.That(group.TotalCount, Is.EqualTo(2));
            Assert.That(group.UnreadCount, Is.EqualTo(1));
        }

        // ── ONC84: Digest LatestAt set to most recent creation time ───────────

        [Test]
        public async Task ONC84_DigestLatestAt_IsSetToMostRecentCreationTime()
        {
            var t1 = DateTimeOffset.Parse("2026-03-26T09:00:00Z");
            var t2 = DateTimeOffset.Parse("2026-03-28T09:00:00Z");
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("la1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning, timestamp: t1),
                MakeEvent("la2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Informational, timestamp: t2)));

            var result = await svc.GetDigestSummaryAsync(
                new NotificationDigestRequest { WorkflowArea = NotificationWorkflowArea.ComplianceCase },
                "op-la");

            NotificationDigestSummary group = result.DigestGroups.Single();
            Assert.That(group.LatestAt, Is.EqualTo(t2));
        }

        // ── ONC85: Digest OverallSummary matches aggregate of groups ──────────

        [Test]
        public async Task ONC85_DigestOverallSummary_ReflectsAllNotifications()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("os1", ComplianceEventType.OnboardingCaseCreated, ComplianceEventSeverity.Informational),
                MakeEvent("os2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning)));

            var result = await svc.GetDigestSummaryAsync(new NotificationDigestRequest(), "op-os");

            Assert.That(result.OverallSummary.UnreadCount, Is.EqualTo(2));
        }

        // ── ONC86: Digest CriticalCount per group correct ─────────────────────

        [Test]
        public async Task ONC86_DigestCriticalCount_PerGroupIsCorrect()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("cc1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Critical),
                MakeEvent("cc2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Warning)));

            var result = await svc.GetDigestSummaryAsync(
                new NotificationDigestRequest { WorkflowArea = NotificationWorkflowArea.ComplianceCase },
                "op-cc");

            NotificationDigestSummary group = result.DigestGroups.Single();
            Assert.That(group.CriticalCount, Is.EqualTo(1));
        }

        // ── ONC87: Digest AgedOnly filter only includes stale/overdue ─────────

        [Test]
        public async Task ONC87_DigestAgedOnly_IncludesOnlyStaleOverdue()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("da1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    timestamp: now.AddMinutes(-10)),   // Fresh
                MakeEvent("da2", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    timestamp: now.AddDays(-4))),      // Stale
            clock);

            var result = await svc.GetDigestSummaryAsync(
                new NotificationDigestRequest { AgedOnly = true },
                "op-da");

            Assert.That(result.DigestGroups, Has.Count.EqualTo(1));
            Assert.That(result.DigestGroups[0].TotalCount, Is.EqualTo(1));
        }

        // ── ONC88: Digest empty for operator with no notifications ────────────

        [Test]
        public async Task ONC88_Digest_EmptyForOperatorWithNoNotifications()
        {
            var svc = MakeSvc(MakeBackbone());

            var result = await svc.GetDigestSummaryAsync(new NotificationDigestRequest(), "op-empty");

            Assert.That(result.Success, Is.True);
            Assert.That(result.DigestGroups, Is.Empty);
            Assert.That(result.OverallSummary.UnreadCount, Is.EqualTo(0));
        }

        // ── ONC89: Digest ComputedAt populated ────────────────────────────────

        [Test]
        public async Task ONC89_Digest_ComputedAtIsPopulated()
        {
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("ca1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Informational)));

            var result = await svc.GetDigestSummaryAsync(new NotificationDigestRequest(), "op-ca");
            Assert.That(result.ComputedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        // ── ONC90: Digest EscalatedCount per group correct ───────────────────

        [Test]
        public async Task ONC90_DigestEscalatedCount_PerGroupIsCorrect()
        {
            var now = DateTimeOffset.Parse("2026-03-28T10:00:00Z");
            var clock = new FakeTime(now);
            var svc = MakeSvc(MakeBackbone(
                MakeEvent("eg1", ComplianceEventType.ComplianceCaseCreated, ComplianceEventSeverity.Warning,
                    timestamp: now.AddDays(-2)),
                MakeEvent("eg2", ComplianceEventType.ComplianceCaseStateChanged, ComplianceEventSeverity.Informational,
                    timestamp: now.AddHours(-1))),
            clock);

            var result = await svc.GetDigestSummaryAsync(
                new NotificationDigestRequest { WorkflowArea = NotificationWorkflowArea.ComplianceCase },
                "op-eg");

            NotificationDigestSummary group = result.DigestGroups.Single();
            Assert.That(group.EscalatedCount, Is.EqualTo(1));
        }

        // ── ONC91: HTTP POST /resolve returns 200 OK ─────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC91_HTTP_Resolve_Returns200()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/resolve",
                new ResolveNotificationsRequest { OperatorNote = "Resolved via HTTP." });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Resolved));
        }

        // ── ONC92: HTTP POST /reopen returns 200 OK ──────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC92_HTTP_Reopen_Returns200()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/reopen",
                new ReopenNotificationsRequest());

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AppliedState, Is.EqualTo(NotificationLifecycleState.Reopened));
        }

        // ── ONC93: HTTP GET /digest returns 200 OK ────────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC93_HTTP_Digest_Returns200()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications/digest");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<NotificationDigestResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
        }

        // ── ONC94: HTTP GET /digest 401 without token ─────────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC94_HTTP_Digest_Returns401WhenUnauthenticated()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/operator-notifications/digest");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── ONC95: HTTP POST /resolve 401 without token ───────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC95_HTTP_Resolve_Returns401WhenUnauthenticated()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/v1/operator-notifications/resolve",
                new ResolveNotificationsRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── ONC96: HTTP lifecycle E2E – resolve and reopen ────────────────────

        [Test]
        [NonParallelizable]
        public async Task ONC96_HTTP_LifecycleE2E_ResolveAndReopen()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Resolve
            var resolveResp = await client.PostAsJsonAsync("/api/v1/operator-notifications/resolve",
                new ResolveNotificationsRequest());
            var resolveResult = await resolveResp.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(resolveResult!.Success, Is.True);

            // Reopen
            var reopenResp = await client.PostAsJsonAsync("/api/v1/operator-notifications/reopen",
                new ReopenNotificationsRequest());
            var reopenResult = await reopenResp.Content.ReadFromJsonAsync<NotificationLifecycleResponse>();
            Assert.That(reopenResult!.Success, Is.True);
        }

        // ── ONC97: HTTP GET / with role query param returns 200 ───────────────

        [Test]
        [NonParallelizable]
        public async Task ONC97_HTTP_GetNotifications_WithRoleFilter_Returns200()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications?role=0");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── ONC98: HTTP GET / with workflowArea query param returns 200 ───────

        [Test]
        [NonParallelizable]
        public async Task ONC98_HTTP_GetNotifications_WithWorkflowAreaFilter_Returns200()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications?workflowArea=1");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── ONC99: HTTP GET digest with workflowArea param returns 200 ─────────

        [Test]
        [NonParallelizable]
        public async Task ONC99_HTTP_GetDigest_WithWorkflowAreaParam_Returns200()
        {
            using var factory = new CustomFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications/digest?workflowArea=1");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── ONC100: Degraded-state flag set on IsDegradedState field ─────────

        [Test]
        public async Task ONC100_DegradedStateFlag_SetOnListResponse_WhenBackboneFails()
        {
            var svc = MakeSvc(new ErrorBackbone());

            var result = await svc.GetNotificationsAsync(new OperatorNotificationQueryRequest(), "op-deg");

            Assert.That(result.Success, Is.False);
            Assert.That(result.IsDegradedState, Is.True);
            Assert.That(result.DegradedReason, Is.Not.Null.And.Not.Empty);
        }

        // ── Fakes and factories ──────────────────────────────────────────────

        private sealed class FakeBackbone : IComplianceEventBackboneService
        {
            public List<ComplianceEventEnvelope> Events { get; init; } = new();

            public Task<ComplianceEventListResponse> GetEventsAsync(
                ComplianceEventQueryRequest request,
                string actorId)
            {
                IEnumerable<ComplianceEventEnvelope> filtered = Events;

                if (!string.IsNullOrWhiteSpace(request.CaseId))
                    filtered = filtered.Where(e => e.CaseId == request.CaseId);

                if (request.Severity.HasValue)
                    filtered = filtered.Where(e => e.Severity == request.Severity.Value);

                if (request.EventType.HasValue)
                    filtered = filtered.Where(e => e.EventType == request.EventType.Value);

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

        private sealed class ErrorBackbone : IComplianceEventBackboneService
        {
            public Task<ComplianceEventListResponse> GetEventsAsync(
                ComplianceEventQueryRequest request,
                string actorId) =>
                Task.FromResult(new ComplianceEventListResponse
                {
                    Success = false,
                    ErrorCode = "BACKBONE_UNAVAILABLE",
                    ErrorMessage = "Simulated backbone failure for degraded-state test."
                });

            public Task<ComplianceEventQueueSummaryResponse> GetQueueSummaryAsync(
                ComplianceEventQueryRequest request,
                string actorId) =>
                Task.FromResult(new ComplianceEventQueueSummaryResponse { Success = false });
        }

        private sealed class FakeTime : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTime(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private class CustomFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForOperatorNotifExtTests32Chars!!",
                        ["JwtConfig:SecretKey"] = "TestSecretKeyForOperatorNotifExtTests32Chars!",
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

        private static async Task<string> GetJwtAsync(System.Net.Http.HttpClient client)
        {
            var email = $"onc-ext-{Guid.NewGuid():N}@test.biatec.io";
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

using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.OperatorNotification;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for <see cref="NotificationPreferenceService"/> and the preference
    /// endpoints on <see cref="BiatecTokensApi.Controllers.OperatorNotificationCenterController"/>.
    /// NPT01-NPT50.
    /// </summary>
    [TestFixture]
    public class NotificationPreferenceTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static NotificationPreferenceService MakeSvc(TimeProvider? tp = null) =>
            new(NullLogger<NotificationPreferenceService>.Instance, tp);

        // ── NPT01: Get preferences returns default for unknown operator ────────

        [Test]
        public async Task NPT01_GetPreferences_UnknownOperator_ReturnsDefault()
        {
            var svc = MakeSvc();
            var result = await svc.GetPreferencesAsync("op-new");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Preference, Is.Not.Null);
            Assert.That(result.Preference!.OperatorId, Is.EqualTo("op-new"));
            Assert.That(result.Preference.SeverityThreshold, Is.EqualTo(NotificationSeverityThreshold.All));
        }

        // ── NPT02: Update preferences persists severity threshold ────────────

        [Test]
        public async Task NPT02_UpdatePreferences_SetsNewSeverityThreshold()
        {
            var svc = MakeSvc();
            var req = new UpdateNotificationPreferenceRequest
            {
                SeverityThreshold = NotificationSeverityThreshold.CriticalOnly
            };
            var result = await svc.UpdatePreferencesAsync(req, "op-threshold");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Preference!.SeverityThreshold, Is.EqualTo(NotificationSeverityThreshold.CriticalOnly));
        }

        // ── NPT03: Update preferences persists digest-enabled flag ────────────

        [Test]
        public async Task NPT03_UpdatePreferences_SetDigestEnabled_Persists()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(new UpdateNotificationPreferenceRequest { DigestEnabled = false }, "op-digest");
            var result = await svc.GetPreferencesAsync("op-digest");

            Assert.That(result.Preference!.DigestEnabled, Is.False);
        }

        // ── NPT04: Update preferences records audit entries ───────────────────

        [Test]
        public async Task NPT04_UpdatePreferences_RecordsAuditEntry()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest
                {
                    SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove,
                    Note = "Setting threshold to warning"
                }, "op-audit");

            var result = await svc.GetPreferencesAsync("op-audit");

            Assert.That(result.Preference!.AuditTrail, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(result.Preference.AuditTrail[0].FieldChanged, Is.EqualTo(nameof(NotificationPreference.SeverityThreshold)));
            Assert.That(result.Preference.AuditTrail[0].Note, Is.EqualTo("Setting threshold to warning"));
        }

        // ── NPT05: Multiple updates accumulate audit entries ─────────────────

        [Test]
        public async Task NPT05_MultipleUpdates_AccumulateAuditEntries()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest { SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove }, "op-multi");
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest { DigestEnabled = false }, "op-multi");

            var result = await svc.GetPreferencesAsync("op-multi");

            Assert.That(result.Preference!.AuditTrail, Has.Count.EqualTo(2));
        }

        // ── NPT06: GetEffectivePreferences creates default and stores it ──────

        [Test]
        public async Task NPT06_GetEffectivePreferences_CreatesAndStoresDefault()
        {
            var svc = MakeSvc();
            var pref = await svc.GetEffectivePreferencesAsync("op-eff");
            Assert.That(pref.OperatorId, Is.EqualTo("op-eff"));

            // Second call should return same object (from store)
            var pref2 = await svc.GetEffectivePreferencesAsync("op-eff");
            Assert.That(pref2.CreatedAt, Is.EqualTo(pref.CreatedAt));
        }

        // ── NPT07: Update digest policy persists frequency ────────────────────

        [Test]
        public async Task NPT07_UpdateDigestPolicy_PersistsFrequency()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest
                {
                    DigestPolicy = new NotificationDigestPolicy { Frequency = DigestFrequency.WeeklySummary }
                }, "op-pol");

            var result = await svc.GetPreferencesAsync("op-pol");
            Assert.That(result.Preference!.DigestPolicy.Frequency, Is.EqualTo(DigestFrequency.WeeklySummary));
        }

        // ── NPT08: Default preference has EscalationEnabled true ─────────────

        [Test]
        public async Task NPT08_DefaultPreference_EscalationEnabled_IsTrue()
        {
            var svc = MakeSvc();
            var result = await svc.GetPreferencesAsync("op-esc");
            Assert.That(result.Preference!.EscalationEnabled, Is.True);
        }

        // ── NPT09: Default preference has AllowBlockerSuppression false ───────

        [Test]
        public async Task NPT09_DefaultPreference_AllowBlockerSuppression_IsFalse()
        {
            var svc = MakeSvc();
            var result = await svc.GetPreferencesAsync("op-blocker");
            Assert.That(result.Preference!.AllowBlockerSuppression, Is.False);
        }

        // ── NPT10: Update WorkflowAreaSubscriptions persists value ────────────

        [Test]
        public async Task NPT10_UpdateWorkflowAreaSubscriptions_Persists()
        {
            var svc = MakeSvc();
            var areas = new List<NotificationWorkflowArea>
            {
                NotificationWorkflowArea.KycOnboarding,
                NotificationWorkflowArea.TokenOperations
            };
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest { WorkflowAreaSubscriptions = areas }, "op-areas");

            var result = await svc.GetPreferencesAsync("op-areas");
            Assert.That(result.Preference!.WorkflowAreaSubscriptions, Does.Contain(NotificationWorkflowArea.KycOnboarding));
            Assert.That(result.Preference.WorkflowAreaSubscriptions, Does.Contain(NotificationWorkflowArea.TokenOperations));
        }

        // ── NPT11: IsNotificationAllowed - Critical always allowed ────────────

        [Test]
        public void NPT11_IsNotificationAllowed_CriticalSeverity_AlwaysAllowed()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.CriticalOnly,
                AllowBlockerSuppression = false
            };
            // Even with CriticalOnly threshold, critical events pass
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Critical, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT12: IsNotificationAllowed - Informational blocked by WarningAndAbove ─

        [Test]
        public void NPT12_IsNotificationAllowed_Informational_BlockedByWarningAndAbove()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.False);
        }

        // ── NPT13: IsNotificationAllowed - Warning allowed by WarningAndAbove ─

        [Test]
        public void NPT13_IsNotificationAllowed_Warning_AllowedByWarningAndAbove()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT14: IsNotificationAllowed - Informational blocked by CriticalOnly ─

        [Test]
        public void NPT14_IsNotificationAllowed_Informational_BlockedByCriticalOnly()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.CriticalOnly
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.False);
        }

        // ── NPT15: IsNotificationAllowed - Warning blocked by CriticalOnly ────

        [Test]
        public void NPT15_IsNotificationAllowed_Warning_BlockedByCriticalOnly()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.CriticalOnly
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.False);
        }

        // ── NPT16: IsNotificationAllowed - All threshold allows informational ─

        [Test]
        public void NPT16_IsNotificationAllowed_All_AllowsInformational()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.All
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT17: IsNotificationAllowed - null preference allows everything ──

        [Test]
        public void NPT17_IsNotificationAllowed_NullPreference_AllowsAll()
        {
            var svc = MakeSvc();
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.General, null);
            Assert.That(result, Is.True);
        }

        // ── NPT18: IsNotificationAllowed - Critical not blocked by CriticalOnly preference ─

        [Test]
        public void NPT18_IsNotificationAllowed_Critical_NotBlockedEvenWithWarningAndAbove()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Critical, NotificationWorkflowArea.ProtectedSignOff, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT19: IsNotificationAllowed - Critical always allowed even with restricted subscriptions ─

        [Test]
        public void NPT19_IsNotificationAllowed_Critical_AlwaysAllowedEvenWhenAreaNotSubscribed()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.All,
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.KycOnboarding // only subscribed to KYC
                }
            };
            // Critical event in TokenOperations area that operator is NOT subscribed to
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Critical, NotificationWorkflowArea.TokenOperations, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT20: IsNotificationAllowed - below-threshold + wrong area both blocked ─

        [Test]
        public void NPT20_IsNotificationAllowed_InformationalWarningAreaNotSubscribed_Blocked()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove,
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.KycOnboarding
                }
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.TokenOperations, pref);
            Assert.That(result, Is.False);
        }

        // ── NPT21: IsNotificationAllowed - correct area allowed ──────────────

        [Test]
        public void NPT21_IsNotificationAllowed_CorrectArea_Allowed()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.All,
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.KycOnboarding,
                    NotificationWorkflowArea.TokenOperations
                }
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.TokenOperations, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT22: IsNotificationAllowed - wrong area not in subscriptions blocked ─

        [Test]
        public void NPT22_IsNotificationAllowed_WrongArea_Blocked()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.All,
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.KycOnboarding
                }
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.Reporting, pref);
            Assert.That(result, Is.False);
        }

        // ── NPT23: IsNotificationAllowed - null subscriptions means all areas allowed ─

        [Test]
        public void NPT23_IsNotificationAllowed_NullSubscriptions_AllAreasAllowed()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.All,
                WorkflowAreaSubscriptions = null
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.Reporting, pref);
            Assert.That(result, Is.True);
        }

        // ── NPT24: MutedWorkflowAreas - ComputeRoutingMetadata sets IsMuted ──

        [Test]
        public void NPT24_ComputeRoutingMetadata_MutedArea_IsMutedTrue()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                MutedWorkflowAreas = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.Reporting
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.Reporting, pref);
            Assert.That(meta.IsMuted, Is.True);
        }

        // ── NPT25: Non-muted area IsMuted is false ────────────────────────────

        [Test]
        public void NPT25_ComputeRoutingMetadata_NonMutedArea_IsMutedFalse()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                MutedWorkflowAreas = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.Reporting
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.TokenOperations, pref);
            Assert.That(meta.IsMuted, Is.False);
        }

        // ── NPT26: Update MutedWorkflowAreas persists ─────────────────────────

        [Test]
        public async Task NPT26_UpdateMutedWorkflowAreas_Persists()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest
                {
                    MutedWorkflowAreas = new List<NotificationWorkflowArea>
                    {
                        NotificationWorkflowArea.ReleaseReadiness
                    }
                }, "op-muted");

            var result = await svc.GetPreferencesAsync("op-muted");
            Assert.That(result.Preference!.MutedWorkflowAreas, Does.Contain(NotificationWorkflowArea.ReleaseReadiness));
        }

        // ── NPT27: Empty subscriptions list means all areas blocked (not null) ─

        [Test]
        public void NPT27_IsNotificationAllowed_EmptySubscriptionsList_BlocksNonCritical()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.All,
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>() // empty, not null
            };
            var result = svc.IsNotificationAllowed(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.General, pref);
            Assert.That(result, Is.False);
        }

        // ── NPT28: ComputeRoutingMetadata IsInSubscribedArea false when not subscribed ─

        [Test]
        public void NPT28_ComputeRoutingMetadata_NotInSubscribedArea_IsFalse()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.KycOnboarding
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.TokenOperations, pref);
            Assert.That(meta.IsInSubscribedArea, Is.False);
        }

        // ── NPT29: ComputeRoutingMetadata IsInSubscribedArea true when subscribed ─

        [Test]
        public void NPT29_ComputeRoutingMetadata_InSubscribedArea_IsTrue()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                WorkflowAreaSubscriptions = new List<NotificationWorkflowArea>
                {
                    NotificationWorkflowArea.KycOnboarding,
                    NotificationWorkflowArea.TokenOperations
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.TokenOperations, pref);
            Assert.That(meta.IsInSubscribedArea, Is.True);
        }

        // ── NPT30: ComputeRoutingMetadata null subscriptions means all areas subscribed ─

        [Test]
        public void NPT30_ComputeRoutingMetadata_NullSubscriptions_IsInSubscribedAreaTrue()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference { WorkflowAreaSubscriptions = null };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.Reporting, pref);
            Assert.That(meta.IsInSubscribedArea, Is.True);
        }

        // ── NPT31: DigestPolicy AlwaysImmediateForCritical overrides digest freq ─

        [Test]
        public void NPT31_ComputeRoutingMetadata_Critical_AlwaysImmediateDelivery()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                DigestEnabled = true,
                DigestPolicy = new NotificationDigestPolicy
                {
                    Frequency = DigestFrequency.WeeklySummary,
                    AlwaysImmediateForCritical = true
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Critical, NotificationWorkflowArea.ProtectedSignOff, pref);
            Assert.That(meta.ImmediateDelivery, Is.True);
        }

        // ── NPT32: Digest disabled → immediate delivery ────────────────────────

        [Test]
        public void NPT32_ComputeRoutingMetadata_DigestDisabled_ImmediateDelivery()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                DigestEnabled = false,
                DigestPolicy = new NotificationDigestPolicy { Frequency = DigestFrequency.WeeklySummary }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.General, pref);
            Assert.That(meta.ImmediateDelivery, Is.True);
        }

        // ── NPT33: DigestFrequency.Immediate → ImmediateDelivery true ─────────

        [Test]
        public void NPT33_ComputeRoutingMetadata_FrequencyImmediate_ImmediateDeliveryTrue()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                DigestEnabled = true,
                DigestPolicy = new NotificationDigestPolicy { Frequency = DigestFrequency.Immediate }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.General, pref);
            Assert.That(meta.ImmediateDelivery, Is.True);
        }

        // ── NPT34: DailySummary frequency → SuggestedDigestWindow is set ──────

        [Test]
        public void NPT34_ComputeRoutingMetadata_DailySummary_SuggestedDigestWindowSet()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                DigestEnabled = true,
                DigestPolicy = new NotificationDigestPolicy
                {
                    Frequency = DigestFrequency.DailySummary,
                    AlwaysImmediateForCritical = false
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Informational, NotificationWorkflowArea.General, pref);
            Assert.That(meta.ImmediateDelivery, Is.False);
            Assert.That(meta.SuggestedDigestWindow, Is.EqualTo(DigestFrequency.DailySummary.ToString()));
        }

        // ── NPT35: WeeklySummary frequency → SuggestedDigestWindow is WeeklySummary ─

        [Test]
        public void NPT35_ComputeRoutingMetadata_WeeklySummary_SuggestedDigestWindowCorrect()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                DigestEnabled = true,
                DigestPolicy = new NotificationDigestPolicy
                {
                    Frequency = DigestFrequency.WeeklySummary,
                    AlwaysImmediateForCritical = false
                }
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.ComplianceCase, pref);
            Assert.That(meta.SuggestedDigestWindow, Is.EqualTo(DigestFrequency.WeeklySummary.ToString()));
        }

        // ── NPT36: Fail-closed: critical passes even under CriticalOnly threshold ─

        [Test]
        public void NPT36_FailClosed_CriticalPassesCriticalOnlyThreshold()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.CriticalOnly
            };
            Assert.That(svc.IsNotificationAllowed(ComplianceEventSeverity.Critical,
                NotificationWorkflowArea.General, pref), Is.True);
        }

        // ── NPT37: Fail-closed: AllowBlockerSuppression update persists ───────

        [Test]
        public async Task NPT37_UpdateAllowBlockerSuppression_Persists()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest { AllowBlockerSuppression = true }, "op-bs");
            var result = await svc.GetPreferencesAsync("op-bs");
            Assert.That(result.Preference!.AllowBlockerSuppression, Is.True);
        }

        // ── NPT38: Audit entry has non-empty EntryId ──────────────────────────

        [Test]
        public async Task NPT38_AuditEntry_HasNonEmptyEntryId()
        {
            var svc = MakeSvc();
            await svc.UpdatePreferencesAsync(
                new UpdateNotificationPreferenceRequest { EscalationEnabled = false }, "op-entryid");
            var result = await svc.GetPreferencesAsync("op-entryid");
            Assert.That(result.Preference!.AuditTrail[0].EntryId, Is.Not.Null.And.Not.Empty);
        }

        // ── NPT39: ComputeRoutingMetadata null preference returns safe defaults ─

        [Test]
        public void NPT39_ComputeRoutingMetadata_NullPreference_SafeDefaults()
        {
            var svc = MakeSvc();
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.General, null);
            Assert.That(meta.PassesSeverityThreshold, Is.True);
            Assert.That(meta.IsInSubscribedArea, Is.True);
            Assert.That(meta.IsMuted, Is.False);
        }

        // ── NPT40: ComputeRoutingMetadata PassesSeverityThreshold correct ─────

        [Test]
        public void NPT40_ComputeRoutingMetadata_BelowThreshold_PassesFalse()
        {
            var svc = MakeSvc();
            var pref = new NotificationPreference
            {
                SeverityThreshold = NotificationSeverityThreshold.CriticalOnly
            };
            var meta = svc.ComputeRoutingMetadata(
                ComplianceEventSeverity.Warning, NotificationWorkflowArea.General, pref);
            Assert.That(meta.PassesSeverityThreshold, Is.False);
        }

        // ── NPT41: HTTP GET /preferences returns 401 without auth ────────────

        [Test]
        [NonParallelizable]
        public async Task NPT41_HTTP_GetPreferences_Unauthenticated_Returns401()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/operator-notifications/preferences");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── NPT42: HTTP GET /preferences with JWT returns 200 ────────────────

        [Test]
        [NonParallelizable]
        public async Task NPT42_HTTP_GetPreferences_Authenticated_Returns200()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications/preferences");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── NPT43: HTTP GET /preferences returns success=true in body ────────

        [Test]
        [NonParallelizable]
        public async Task NPT43_HTTP_GetPreferences_Returns_SuccessTrue()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications/preferences");
            var body = await response.Content.ReadFromJsonAsync<NotificationPreferenceResponse>();

            Assert.That(body!.Success, Is.True);
            Assert.That(body.Preference, Is.Not.Null);
        }

        // ── NPT44: HTTP PUT /preferences returns 401 without auth ─────────────

        [Test]
        [NonParallelizable]
        public async Task NPT44_HTTP_PutPreferences_Unauthenticated_Returns401()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();

            var response = await client.PutAsJsonAsync(
                "/api/v1/operator-notifications/preferences",
                new UpdateNotificationPreferenceRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── NPT45: HTTP PUT /preferences with JWT returns 200 ─────────────────

        [Test]
        [NonParallelizable]
        public async Task NPT45_HTTP_PutPreferences_Authenticated_Returns200()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PutAsJsonAsync(
                "/api/v1/operator-notifications/preferences",
                new UpdateNotificationPreferenceRequest
                {
                    SeverityThreshold = NotificationSeverityThreshold.WarningAndAbove
                });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── NPT46: HTTP PUT /preferences persists change and GET reflects it ──

        [Test]
        [NonParallelizable]
        public async Task NPT46_HTTP_PutPreferences_ChangePersistedInSubsequentGet()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Update preference
            await client.PutAsJsonAsync(
                "/api/v1/operator-notifications/preferences",
                new UpdateNotificationPreferenceRequest
                {
                    SeverityThreshold = NotificationSeverityThreshold.CriticalOnly,
                    DigestEnabled = false
                });

            // Retrieve and verify
            var getResp = await client.GetAsync("/api/v1/operator-notifications/preferences");
            var body = await getResp.Content.ReadFromJsonAsync<NotificationPreferenceResponse>();

            Assert.That(body!.Success, Is.True);
            Assert.That(body.Preference!.SeverityThreshold, Is.EqualTo(NotificationSeverityThreshold.CriticalOnly));
            Assert.That(body.Preference.DigestEnabled, Is.False);
        }

        // ── NPT47: HTTP PUT /preferences with empty body (default) returns 200 ─

        [Test]
        [NonParallelizable]
        public async Task NPT47_HTTP_PutPreferences_EmptyBody_Returns200()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PutAsJsonAsync(
                "/api/v1/operator-notifications/preferences",
                new UpdateNotificationPreferenceRequest());

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── NPT48: HTTP GET /preferences contains non-null Preference.OperatorId ─

        [Test]
        [NonParallelizable]
        public async Task NPT48_HTTP_GetPreferences_OperatorIdPopulated()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications/preferences");
            var body = await response.Content.ReadFromJsonAsync<NotificationPreferenceResponse>();

            Assert.That(body!.Preference!.OperatorId, Is.Not.Null.And.Not.Empty);
        }

        // ── NPT49: HTTP PUT /preferences with muted areas updates them ────────

        [Test]
        [NonParallelizable]
        public async Task NPT49_HTTP_PutPreferences_SetMutedAreas_ReflectedInGet()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            await client.PutAsJsonAsync(
                "/api/v1/operator-notifications/preferences",
                new UpdateNotificationPreferenceRequest
                {
                    MutedWorkflowAreas = new List<NotificationWorkflowArea>
                    {
                        NotificationWorkflowArea.Reporting
                    }
                });

            var getResp = await client.GetAsync("/api/v1/operator-notifications/preferences");
            var body = await getResp.Content.ReadFromJsonAsync<NotificationPreferenceResponse>();

            Assert.That(body!.Preference!.MutedWorkflowAreas, Is.Not.Null);
        }

        // ── NPT50: HTTP GET default preference DigestPolicy has AlwaysImmediateForCritical true ─

        [Test]
        [NonParallelizable]
        public async Task NPT50_HTTP_GetPreferences_DefaultDigestPolicy_AlwaysImmediateForCriticalTrue()
        {
            using var factory = new CustomPrefFactory();
            using var client = factory.CreateClient();
            var token = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/operator-notifications/preferences");
            var body = await response.Content.ReadFromJsonAsync<NotificationPreferenceResponse>();

            Assert.That(body!.Preference!.DigestPolicy.AlwaysImmediateForCritical, Is.True);
        }

        // ── Factory and helpers ───────────────────────────────────────────────

        private class CustomPrefFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForNotifPrefTests32Chars!!!!!",
                        ["JwtConfig:SecretKey"] = "TestSecretKeyForNotifPrefTests32Chars!!!",
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
            var email = $"npt-{Guid.NewGuid():N}@test.biatec.io";
            const string password = "PrefTest123!";

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

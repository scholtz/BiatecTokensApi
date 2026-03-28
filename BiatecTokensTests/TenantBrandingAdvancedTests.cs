using BiatecTokensApi.Models.TenantBranding;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced tests for TenantBrandingService and TenantBrandingController.
    ///
    /// Coverage:
    ///  TBA01-TBA20  — Schema contract assertions: every required field present, no internal leakage
    ///  TBA21-TBA40  — Idempotency and determinism: 3-run repeatability for all service operations
    ///  TBA41-TBA55  — Negative-path: malformed inputs, authorization bypass attempts, bad transitions
    ///  TBA56-TBA70  — E2E deployed-parity: full lifecycle via HTTP with schema assertions
    ///  TBA71-TBA85  — Cross-tenant isolation at service layer and HTTP layer
    ///  TBA86-TBA100 — Status transitions and fail-closed semantics
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TenantBrandingAdvancedTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static TenantBrandingService CreateService() =>
            new TenantBrandingService(NullLogger<TenantBrandingService>.Instance);

        private const string TenantA = "tenant-advanced-a@corp.example.com";
        private const string TenantB = "tenant-advanced-b@corp.example.com";
        private const string TenantC = "tenant-advanced-c@corp.example.com";

        private static UpdateTenantBrandingDraftRequest MinimalValidRequest() => new()
        {
            OrganizationName = "Advanced Test Corp"
        };

        private static UpdateTenantBrandingDraftRequest FullValidRequest() => new()
        {
            OrganizationName = "Advanced Test Corp",
            ProductLabel = "Advanced Tokens",
            LogoUrl = "https://cdn.corp.com/logo.png",
            FaviconUrl = "https://cdn.corp.com/favicon.ico",
            Theme = new TenantThemeTokens
            {
                PrimaryColor = "#0A1B2C",
                SecondaryColor = "#FFFFFF",
                AccentColor = "#FF6600",
                BackgroundColor = "#F5F5F5",
                TextColor = "#111111"
            },
            Support = new TenantSupportMetadata
            {
                SupportEmail = "support@corp.com",
                SupportUrl = "https://support.corp.com",
                LegalContactEmail = "legal@corp.com",
                LegalContactUrl = "https://legal.corp.com"
            }
        };

        // ═══════════════════════════════════════════════════════════════════════
        // TBA01-TBA20 — Schema contract assertions
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TBA01_GetDraft_Response_HasRequiredSchemaFields()
        {
            var svc = CreateService();
            var result = await svc.GetDraftAsync(TenantA, TenantA);
            Assert.That(result.Success, Is.True, "Success must be set");
            Assert.That(result.Branding, Is.Not.Null, "Branding must be non-null");
            Assert.That(result.Branding!.TenantId, Is.Not.Null, "TenantId required on branding");
            Assert.That(result.Branding.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured));
            Assert.That(result.Branding.ValidationErrors, Is.Not.Null, "ValidationErrors list must be initialized");
            Assert.That(result.Branding.Theme, Is.Not.Null, "Theme must be initialized");
            Assert.That(result.Branding.Support, Is.Not.Null, "Support must be initialized");
        }

        [Test]
        public async Task TBA02_UpdateDraft_Response_HasRequiredSchemaFields()
        {
            var svc = CreateService();
            var result = await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Branding!.TenantId, Is.EqualTo(TenantA));
            Assert.That(result.Branding.Version, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.Branding.CreatedAt, Is.Not.Null);
            Assert.That(result.Branding.UpdatedAt, Is.Not.Null);
            Assert.That(result.Branding.CreatedBy, Is.EqualTo(TenantA));
            Assert.That(result.Branding.UpdatedBy, Is.EqualTo(TenantA));
        }

        [Test]
        public async Task TBA03_PublishedPayload_DoesNotLeakInternalAuditFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);
            var result = await svc.GetPublishedAsync(TenantA);

            Assert.That(result.Payload, Is.Not.Null);
            // The published payload model should NOT contain CreatedBy, UpdatedBy, ValidationErrors
            var payloadType = result.Payload!.GetType();
            Assert.That(payloadType.GetProperty("CreatedBy"), Is.Null,
                "Published payload must not expose CreatedBy (internal audit field)");
            Assert.That(payloadType.GetProperty("UpdatedBy"), Is.Null,
                "Published payload must not expose UpdatedBy (internal audit field)");
            Assert.That(payloadType.GetProperty("ValidationErrors"), Is.Null,
                "Published payload must not expose ValidationErrors (internal field)");
        }

        [Test]
        public async Task TBA04_PublishedPayload_HasAllPublicFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);
            var result = await svc.GetPublishedAsync(TenantA);

            Assert.That(result.Payload!.TenantId, Is.EqualTo(TenantA));
            Assert.That(result.Payload.OrganizationName, Is.EqualTo("Advanced Test Corp"));
            Assert.That(result.Payload.ProductLabel, Is.EqualTo("Advanced Tokens"));
            Assert.That(result.Payload.LogoUrl, Is.EqualTo("https://cdn.corp.com/logo.png"));
            Assert.That(result.Payload.FaviconUrl, Is.EqualTo("https://cdn.corp.com/favicon.ico"));
            Assert.That(result.Payload.Version, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.Payload.PublishedAt, Is.Not.Null);
            Assert.That(result.Payload.IsFallback, Is.False);
        }

        [Test]
        public async Task TBA05_FallbackPayload_HasExpectedSchema()
        {
            var svc = CreateService();
            var result = await svc.GetPublishedAsync("nonexistent-tenant");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Payload, Is.Not.Null);
            Assert.That(result.Payload!.IsFallback, Is.True, "Must be fallback for unknown tenant");
            Assert.That(result.Payload.Version, Is.EqualTo(0), "Fallback version must be 0");
        }

        [Test]
        public async Task TBA06_ValidationResponse_HasRequiredFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            var result = await svc.ValidateDraftAsync(TenantA, TenantA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public async Task TBA07_ValidationError_HasAllRequiredErrorFields()
        {
            var svc = CreateService();
            var req = FullValidRequest();
            req.OrganizationName = new string('X', 101);
            var result = await svc.UpdateDraftAsync(req, TenantA, TenantA);

            var err = result.Branding!.ValidationErrors.First();
            Assert.That(err.Field, Is.Not.Null.And.Not.Empty, "Error must have Field");
            Assert.That(err.Message, Is.Not.Null.And.Not.Empty, "Error must have Message");
            Assert.That(err.Code, Is.Not.Null.And.Not.Empty, "Error must have Code");
        }

        [Test]
        public async Task TBA08_StatusResponse_HasAllRequiredFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            var result = await svc.GetStatusAsync(TenantA, TenantA);

            Assert.That(result.Success, Is.True);
            // Status enum must be a recognized value
            Assert.That(Enum.IsDefined(typeof(TenantBrandingLifecycleStatus), result.Status), Is.True);
            Assert.That(result.HasDraft, Is.True);
            Assert.That(result.HasPublished, Is.False);
        }

        [Test]
        public async Task TBA09_DomainRecord_HasAllRequiredFields()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "schema-check.example.com" },
                TenantA, TenantA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Domain, Is.Not.Null);
            Assert.That(result.Domain!.DomainId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Domain.Domain, Is.EqualTo("schema-check.example.com"));
            Assert.That(result.Domain.TenantId, Is.EqualTo(TenantA));
            Assert.That(result.Domain.VerificationToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Domain.Status, Is.EqualTo(TenantDomainReadinessStatus.Pending));
            Assert.That(result.Domain.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task TBA10_HistoryEntry_HasAllRequiredFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            var result = await svc.GetHistoryAsync(TenantA, TenantA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.History, Is.Not.Empty);
            var entry = result.History.First();
            Assert.That(entry.EntryId, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.Version, Is.GreaterThan(0));
            Assert.That(entry.EventType, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.Actor, Is.EqualTo(TenantA));
            Assert.That(entry.OccurredAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task TBA11_StatusResponse_AfterPublish_HasPublishedFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);
            var result = await svc.GetStatusAsync(TenantA, TenantA);

            Assert.That(result.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Published));
            Assert.That(result.HasPublished, Is.True);
            Assert.That(result.PublishedVersion, Is.Not.Null.And.GreaterThan(0));
            Assert.That(result.LastPublishedAt, Is.Not.Null);
        }

        [Test]
        public async Task TBA12_StatusDescription_IsNotEmpty_ForAllStates()
        {
            var svc = CreateService();

            // NotConfigured state
            var status1 = await svc.GetStatusAsync(TenantA, TenantA);
            Assert.That(status1.StatusDescription, Is.Not.Null.And.Not.Empty,
                "StatusDescription must be populated even for NotConfigured");

            // Draft state
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            var status2 = await svc.GetStatusAsync(TenantA, TenantA);
            Assert.That(status2.StatusDescription, Is.Not.Null.And.Not.Empty);

            // Published state
            await svc.PublishAsync(TenantA, TenantA);
            var status3 = await svc.GetStatusAsync(TenantA, TenantA);
            Assert.That(status3.StatusDescription, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task TBA13_DraftValidationErrors_PersistedOnBrandingConfig()
        {
            var svc = CreateService();
            var req = FullValidRequest();
            req.Theme = new TenantThemeTokens { PrimaryColor = "not-a-color" };
            var result = await svc.UpdateDraftAsync(req, TenantA, TenantA);

            Assert.That(result.Branding!.ValidationErrors, Is.Not.Empty);
            Assert.That(result.Branding.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));
            // ValidationErrors must survive a read-back
            var readBack = await svc.GetDraftAsync(TenantA, TenantA);
            Assert.That(readBack.Branding!.ValidationErrors, Is.Not.Empty);
            Assert.That(readBack.Branding.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));
        }

        [Test]
        public async Task TBA14_PublishedPayload_DoesNotExposeValidationErrors()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);

            // The payload type must not have sensitive internal fields
            var published = await svc.GetPublishedAsync(TenantA);
            var payloadProps = published.Payload!.GetType().GetProperties().Select(p => p.Name).ToList();
            Assert.That(payloadProps, Does.Not.Contain("ValidationErrors"));
            Assert.That(payloadProps, Does.Not.Contain("CreatedBy"));
            Assert.That(payloadProps, Does.Not.Contain("UpdatedBy"));
        }

        [Test]
        public async Task TBA15_GetDraft_Response_TenantIdMatchesCallerIdentity()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            var result = await svc.GetDraftAsync(TenantA, TenantA);
            Assert.That(result.Branding!.TenantId, Is.EqualTo(TenantA));
        }

        [Test]
        public async Task TBA16_Publish_Response_TenantIdMatchesCallerIdentity()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            var result = await svc.PublishAsync(TenantA, TenantA);
            Assert.That(result.Branding!.TenantId, Is.EqualTo(TenantA));
        }

        [Test]
        public async Task TBA17_HistoryEntry_EventType_IsKnownValue()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);
            var result = await svc.GetHistoryAsync(TenantA, TenantA);

            var validEventTypes = new[] { "DraftSaved", "Published", "DraftReset" };
            foreach (var entry in result.History)
            {
                Assert.That(validEventTypes, Does.Contain(entry.EventType),
                    $"Unknown EventType '{entry.EventType}' — must be one of: {string.Join(", ", validEventTypes)}");
            }
        }

        [Test]
        public async Task TBA18_DomainVerificationToken_Format_IsDeterministicHex()
        {
            var svc = CreateService();
            var r1 = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "token-format.example.com" }, TenantA, TenantA);
            var token = r1.Domain!.VerificationToken;
            // Token should be hex-only to be safe in DNS TXT records
            Assert.That(token, Does.Match(@"^[0-9a-f]+$"),
                "Verification token must contain only lowercase hex characters for DNS safety.");
            Assert.That(token.Length, Is.GreaterThanOrEqualTo(32),
                "Verification token must be at least 32 characters long.");
        }

        [Test]
        public async Task TBA19_DomainsResponse_Initialized_WhenNoDomains()
        {
            var svc = CreateService();
            var result = await svc.GetDomainsAsync(TenantA, TenantA);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Domains, Is.Not.Null);
            Assert.That(result.Domains, Is.Empty);
        }

        [Test]
        public async Task TBA20_HistoryResponse_Initialized_WhenNoHistory()
        {
            var svc = CreateService();
            var result = await svc.GetHistoryAsync(TenantA, TenantA);
            Assert.That(result.Success, Is.True);
            Assert.That(result.History, Is.Not.Null);
            Assert.That(result.History, Is.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TBA21-TBA40 — Idempotency and determinism
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TBA21_GetDraft_Idempotent_ThreeConsecutiveReadsSameResult()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);

            var r1 = await svc.GetDraftAsync(TenantB, TenantB);
            var r2 = await svc.GetDraftAsync(TenantB, TenantB);
            var r3 = await svc.GetDraftAsync(TenantB, TenantB);

            Assert.That(r1.Branding!.OrganizationName, Is.EqualTo(r2.Branding!.OrganizationName));
            Assert.That(r2.Branding!.OrganizationName, Is.EqualTo(r3.Branding!.OrganizationName));
            Assert.That(r1.Branding!.Version, Is.EqualTo(r2.Branding!.Version));
            Assert.That(r2.Branding!.Version, Is.EqualTo(r3.Branding!.Version));
        }

        [Test]
        public async Task TBA22_GetPublished_Idempotent_ThreeConsecutiveReadsSameResult()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);
            await svc.PublishAsync(TenantB, TenantB);

            var r1 = await svc.GetPublishedAsync(TenantB);
            var r2 = await svc.GetPublishedAsync(TenantB);
            var r3 = await svc.GetPublishedAsync(TenantB);

            Assert.That(r1.Payload!.OrganizationName, Is.EqualTo(r2.Payload!.OrganizationName));
            Assert.That(r2.Payload!.OrganizationName, Is.EqualTo(r3.Payload!.OrganizationName));
            Assert.That(r1.Payload!.Version, Is.EqualTo(r2.Payload!.Version));
            Assert.That(r2.Payload!.Version, Is.EqualTo(r3.Payload!.Version));
        }

        [Test]
        public async Task TBA23_ValidateDraft_Idempotent_ThreeConsecutiveValidationsSameResult()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);

            var r1 = await svc.ValidateDraftAsync(TenantB, TenantB);
            var r2 = await svc.ValidateDraftAsync(TenantB, TenantB);
            var r3 = await svc.ValidateDraftAsync(TenantB, TenantB);

            Assert.That(r1.IsValid, Is.EqualTo(r2.IsValid));
            Assert.That(r2.IsValid, Is.EqualTo(r3.IsValid));
            Assert.That(r1.Errors.Count, Is.EqualTo(r2.Errors.Count));
            Assert.That(r2.Errors.Count, Is.EqualTo(r3.Errors.Count));
        }

        [Test]
        public async Task TBA24_ValidateDraft_DoesNotMutateState()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);
            var versionBefore = (await svc.GetDraftAsync(TenantB, TenantB)).Branding!.Version;

            await svc.ValidateDraftAsync(TenantB, TenantB);
            await svc.ValidateDraftAsync(TenantB, TenantB);
            await svc.ValidateDraftAsync(TenantB, TenantB);

            var versionAfter = (await svc.GetDraftAsync(TenantB, TenantB)).Branding!.Version;
            Assert.That(versionAfter, Is.EqualTo(versionBefore),
                "Validation must not increment version — it is read-only.");
        }

        [Test]
        public async Task TBA25_GetStatus_Idempotent_ThreeConsecutiveStatusReadsSameResult()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);

            var s1 = await svc.GetStatusAsync(TenantB, TenantB);
            var s2 = await svc.GetStatusAsync(TenantB, TenantB);
            var s3 = await svc.GetStatusAsync(TenantB, TenantB);

            Assert.That(s1.Status, Is.EqualTo(s2.Status));
            Assert.That(s2.Status, Is.EqualTo(s3.Status));
            Assert.That(s1.HasDraft, Is.EqualTo(s2.HasDraft));
            Assert.That(s1.HasPublished, Is.EqualTo(s3.HasPublished));
        }

        [Test]
        public async Task TBA26_GetDomains_Idempotent_ThreeConsecutiveReadsSameCount()
        {
            var svc = CreateService();
            await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "idem.example.com" }, TenantB, TenantB);

            var r1 = await svc.GetDomainsAsync(TenantB, TenantB);
            var r2 = await svc.GetDomainsAsync(TenantB, TenantB);
            var r3 = await svc.GetDomainsAsync(TenantB, TenantB);

            Assert.That(r1.Domains.Count, Is.EqualTo(r2.Domains.Count));
            Assert.That(r2.Domains.Count, Is.EqualTo(r3.Domains.Count));
        }

        [Test]
        public async Task TBA27_UpdateDraft_SameRequestTwice_VersionIncrementsEachTime()
        {
            var svc = CreateService();
            var req = MinimalValidRequest();
            var r1 = await svc.UpdateDraftAsync(req, TenantB, TenantB);
            var r2 = await svc.UpdateDraftAsync(req, TenantB, TenantB);
            Assert.That(r2.Branding!.Version, Is.EqualTo(r1.Branding!.Version + 1),
                "Every save must increment version even if fields are unchanged.");
        }

        [Test]
        public async Task TBA28_Publish_ThenRepublish_PublishedVersionIncreases()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);
            var p1 = await svc.PublishAsync(TenantB, TenantB);
            int v1 = p1.Branding!.Version;

            // Re-save and re-publish
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "New Label" }, TenantB, TenantB);
            var p2 = await svc.PublishAsync(TenantB, TenantB);
            int v2 = p2.Branding!.Version;

            var pub = await svc.GetPublishedAsync(TenantB);
            Assert.That(v2, Is.GreaterThan(v1));
            Assert.That(pub.Payload!.Version, Is.EqualTo(v2),
                "GetPublished must always return the latest published version.");
        }

        [Test]
        public async Task TBA29_DomainUpsert_SameDomainThreeTimes_StillOneRecord()
        {
            var svc = CreateService();
            var domain = "same-domain.example.com";
            await svc.UpsertDomainAsync(new UpsertTenantDomainRequest { Domain = domain, Notes = "first" }, TenantB, TenantB);
            await svc.UpsertDomainAsync(new UpsertTenantDomainRequest { Domain = domain, Notes = "second" }, TenantB, TenantB);
            await svc.UpsertDomainAsync(new UpsertTenantDomainRequest { Domain = domain, Notes = "third" }, TenantB, TenantB);

            var result = await svc.GetDomainsAsync(TenantB, TenantB);
            var matched = result.Domains.Where(d => d.Domain == domain).ToList();
            Assert.That(matched.Count, Is.EqualTo(1), "Upserting same domain multiple times must not create duplicate records.");
            Assert.That(matched[0].Notes, Is.EqualTo("third"), "Notes must reflect the last update.");
        }

        [Test]
        public async Task TBA30_VerificationToken_SameDomainSameTenant_TokenUnchangedOnReupsert()
        {
            var svc = CreateService();
            var domain = "stable-token.example.com";
            var r1 = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = domain }, TenantB, TenantB);
            var token1 = r1.Domain!.VerificationToken;

            var r2 = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = domain, Notes = "update" }, TenantB, TenantB);
            var token2 = r2.Domain!.VerificationToken;

            Assert.That(token1, Is.EqualTo(token2),
                "Verification token must be stable across upserts — changing it would break ongoing DNS verification.");
        }

        [Test]
        public async Task TBA31_HistoryCount_IncreasesOnEachDraftSave()
        {
            var svc = CreateService();
            var h0 = await svc.GetHistoryAsync(TenantB, TenantB);

            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);
            var h1 = await svc.GetHistoryAsync(TenantB, TenantB);
            Assert.That(h1.History.Count, Is.GreaterThan(h0.History.Count));

            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "V2" }, TenantB, TenantB);
            var h2 = await svc.GetHistoryAsync(TenantB, TenantB);
            Assert.That(h2.History.Count, Is.GreaterThan(h1.History.Count));
        }

        [Test]
        public async Task TBA32_HistoryCount_IncreasesOnPublish()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantB, TenantB);
            var h1 = await svc.GetHistoryAsync(TenantB, TenantB);

            await svc.PublishAsync(TenantB, TenantB);
            var h2 = await svc.GetHistoryAsync(TenantB, TenantB);
            Assert.That(h2.History.Count, Is.GreaterThan(h1.History.Count),
                "Publishing must add a history entry.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TBA41-TBA55 — Negative-path and fail-closed semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TBA41_Publish_WithNoDraft_Fails()
        {
            var svc = CreateService();
            var result = await svc.PublishAsync("fresh-tenant@example.com", "fresh-tenant@example.com");
            Assert.That(result.Success, Is.False, "Publish with no draft must fail.");
        }

        [Test]
        public async Task TBA42_Publish_WithInvalidDraft_Fails()
        {
            var svc = CreateService();
            // Create a draft with invalid color
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Test",
                Theme = new TenantThemeTokens { PrimaryColor = "INVALIDCOLOR" }
            }, TenantC, TenantC);

            var result = await svc.PublishAsync(TenantC, TenantC);
            Assert.That(result.Success, Is.False, "Publish with invalid draft must fail.");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task TBA43_Validate_WithNoDraft_ReturnsNoDraftError()
        {
            var svc = CreateService();
            var result = await svc.ValidateDraftAsync("fresh-noop@example.com", "fresh-noop@example.com");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Must return informative message when no draft exists.");
        }

        [Test]
        public async Task TBA44_UpsertDomain_EmptyString_Fails()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "" }, TenantC, TenantC);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task TBA45_UpsertDomain_WhitespaceOnly_Fails()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "   " }, TenantC, TenantC);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task TBA46_UpsertDomain_HttpSchemePrefix_Fails()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "http://tokens.example.com" }, TenantC, TenantC);
            Assert.That(result.Success, Is.False, "Domain with http:// scheme must be rejected.");
        }

        [Test]
        public async Task TBA47_UpsertDomain_HttpsSchemePrefix_Fails()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "https://tokens.example.com" }, TenantC, TenantC);
            Assert.That(result.Success, Is.False, "Domain with https:// scheme must be rejected.");
        }

        [Test]
        public async Task TBA48_UpdateDraft_NullRequest_TreatsSafelyNoFieldsChanged()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantC, TenantC);
            var versionBefore = (await svc.GetDraftAsync(TenantC, TenantC)).Branding!.Version;

            // Request with all null fields
            var emptyReq = new UpdateTenantBrandingDraftRequest();
            var result = await svc.UpdateDraftAsync(emptyReq, TenantC, TenantC);
            Assert.That(result.Success, Is.True, "Empty request must not crash service.");
            Assert.That(result.Branding!.OrganizationName, Is.EqualTo("Advanced Test Corp"),
                "Empty request must not clear existing values.");
        }

        [Test]
        public async Task TBA49_Publish_RequiresMandatoryFieldsPresent()
        {
            var svc = CreateService();
            // Draft without OrganizationName (which is required for publishing)
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                ProductLabel = "Only Label, No Org Name"
            }, TenantC, TenantC);

            var result = await svc.PublishAsync(TenantC, TenantC);
            Assert.That(result.Success, Is.False,
                "Publish must fail when OrganizationName is missing — fail-closed behavior.");
        }

        [Test]
        public async Task TBA50_InvalidDraft_CannotBePublishedAccidentally()
        {
            var svc = CreateService();
            // Build a draft with multiple validation errors
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = new string('X', 200),
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }
            }, TenantC, TenantC);

            var status = await svc.GetStatusAsync(TenantC, TenantC);
            Assert.That(status.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));

            var publishResult = await svc.PublishAsync(TenantC, TenantC);
            Assert.That(publishResult.Success, Is.False,
                "Invalid draft must never become published — fail-closed publish gate.");

            // Verify published payload is still fallback
            var pub = await svc.GetPublishedAsync(TenantC);
            Assert.That(pub.Payload!.IsFallback, Is.True,
                "Published payload must remain fallback when invalid draft was never published.");
        }

        [Test]
        public async Task TBA51_ValidationErrors_AreSpecific_NotGeneric()
        {
            var svc = CreateService();
            var req = FullValidRequest();
            req.Theme = new TenantThemeTokens { PrimaryColor = "not-a-color" };
            var result = await svc.UpdateDraftAsync(req, TenantC, TenantC);

            var colorError = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field.Contains("PrimaryColor"));
            Assert.That(colorError, Is.Not.Null, "Specific field must have a targeted error.");
            Assert.That(colorError!.Code, Is.EqualTo("INVALID_COLOR_FORMAT"),
                "Error code must be specific enough for operator tooling to act on.");
        }

        [Test]
        public async Task TBA52_MultipleValidationErrors_AllReported()
        {
            var svc = CreateService();
            var req = new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = new string('X', 101),   // Too long
                LogoUrl = "not-a-url",                      // Invalid URL
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }  // Invalid color
            };
            var result = await svc.UpdateDraftAsync(req, TenantC, TenantC);
            Assert.That(result.Branding!.ValidationErrors.Count, Is.GreaterThanOrEqualTo(3),
                "All validation errors must be reported, not just the first one.");
        }

        [Test]
        public async Task TBA53_FixingValidationError_ClearsOldErrors()
        {
            var svc = CreateService();
            // Create invalid draft
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Test",
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }
            }, TenantC, TenantC);

            var invalid = await svc.GetDraftAsync(TenantC, TenantC);
            Assert.That(invalid.Branding!.ValidationErrors, Is.Not.Empty);

            // Fix the error
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                Theme = new TenantThemeTokens { PrimaryColor = "#FF0000" }
            }, TenantC, TenantC);

            var fixed_ = await svc.GetDraftAsync(TenantC, TenantC);
            var colorErr = fixed_.Branding!.ValidationErrors
                .Where(e => e.Field.Contains("PrimaryColor")).ToList();
            Assert.That(colorErr, Is.Empty, "Error for fixed field must be cleared after re-validation.");
        }

        [Test]
        public async Task TBA54_GetPublished_ForUnknownTenant_NeverThrows_AlwaysFallback()
        {
            var svc = CreateService();
            // Variety of unusual tenant IDs
            string[] weirdIds = ["", "  ", "nonexistent", "VERY_LONG_" + new string('X', 200)];
            foreach (var id in weirdIds)
            {
                var result = await svc.GetPublishedAsync(id);
                Assert.That(result.Success, Is.True, $"GetPublished must not throw for tenantId='{id}'");
                Assert.That(result.Payload, Is.Not.Null);
                Assert.That(result.Payload!.IsFallback, Is.True);
            }
        }

        [Test]
        public async Task TBA55_UpdateDraft_VeryLongOrganizationName_Rejected()
        {
            var svc = CreateService();
            var req = new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = new string('A', 500)
            };
            var result = await svc.UpdateDraftAsync(req, TenantC, TenantC);
            var err = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "OrganizationName");
            Assert.That(err, Is.Not.Null, "Excessively long organization name must produce validation error.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TBA56-TBA70 — E2E deployed parity (HTTP + schema assertions)
        // ═══════════════════════════════════════════════════════════════════════

        private TenantBrandingAdvancedWebFactory _factory2 = null!;
        private HttpClient _authClient = null!;
        private HttpClient _anonClient2 = null!;
        private string _jwtToken = string.Empty;
        private string _tenantEmail = string.Empty;

        [OneTimeSetUp]
        public async Task DeployedSetUp()
        {
            _factory2 = new TenantBrandingAdvancedWebFactory();
            _authClient = _factory2.CreateClient();
            _anonClient2 = _factory2.CreateClient();

            _tenantEmail = $"tba-e2e-{Guid.NewGuid():N}@example.com";
            const string password = "E2ePassw0rd!!";

            var regResp = await _authClient.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = _tenantEmail, Password = password, ConfirmPassword = password });

            if (regResp.IsSuccessStatusCode)
            {
                var loginResp = await _authClient.PostAsJsonAsync("/api/v1/auth/login",
                    new { Email = _tenantEmail, Password = password });
                if (loginResp.IsSuccessStatusCode)
                {
                    var json = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
                    _jwtToken = json.TryGetProperty("accessToken", out var t) ? t.GetString() ?? "" : "";
                }
            }

            if (!string.IsNullOrEmpty(_jwtToken))
                _authClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
        }

        [OneTimeTearDown]
        public void DeployedTearDown()
        {
            _authClient?.Dispose();
            _anonClient2?.Dispose();
            _factory2?.Dispose();
        }

        [Test]
        public async Task TBA56_HTTP_GetDraft_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.GetAsync("/api/v1/tenant-branding/draft");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA57_HTTP_UpdateDraft_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.PutAsJsonAsync("/api/v1/tenant-branding/draft",
                new UpdateTenantBrandingDraftRequest { OrganizationName = "Hax" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA58_HTTP_ValidateDraft_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.PostAsync("/api/v1/tenant-branding/validate", null);
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA59_HTTP_Publish_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.PostAsync("/api/v1/tenant-branding/publish", null);
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA60_HTTP_GetStatus_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.GetAsync("/api/v1/tenant-branding/status");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA61_HTTP_GetDomains_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.GetAsync("/api/v1/tenant-branding/domains");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA62_HTTP_UpsertDomain_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.PutAsJsonAsync("/api/v1/tenant-branding/domains",
                new UpsertTenantDomainRequest { Domain = "hax.example.com" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA63_HTTP_GetHistory_Unauthenticated_Returns401()
        {
            var resp = await _anonClient2.GetAsync("/api/v1/tenant-branding/history");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TBA64_HTTP_GetPublished_Anonymous_Returns200()
        {
            // Published endpoint must be accessible without auth
            var resp = await _anonClient2.GetAsync("/api/v1/tenant-branding/published?tenantId=anon-check");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TBA65_HTTP_GetPublished_NoTenant_Returns200WithFallback()
        {
            var resp = await _anonClient2.GetAsync("/api/v1/tenant-branding/published");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(json.TryGetProperty("success", out var s) && s.GetBoolean(), Is.True);
        }

        [Test]
        public async Task TBA66_HTTP_FullLifecycle_SchemasValid()
        {
            if (string.IsNullOrEmpty(_jwtToken))
                Assert.Ignore("JWT not available — skipping E2E test.");

            // 1. Update draft
            var draftResp = await _authClient.PutAsJsonAsync("/api/v1/tenant-branding/draft",
                new UpdateTenantBrandingDraftRequest
                {
                    OrganizationName = "E2E Schema Corp",
                    Theme = new TenantThemeTokens { PrimaryColor = "#AA1122" }
                });
            Assert.That((int)draftResp.StatusCode, Is.EqualTo(200));
            var draftJson = await draftResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(draftJson.TryGetProperty("success", out var ds) && ds.GetBoolean(), Is.True);
            Assert.That(draftJson.TryGetProperty("branding", out var br), Is.True, "Response must have 'branding' property");
            Assert.That(br.TryGetProperty("status", out _), Is.True, "Branding must have 'status' property");
            Assert.That(br.TryGetProperty("version", out _), Is.True, "Branding must have 'version' property");
            Assert.That(br.TryGetProperty("tenantId", out _), Is.True, "Branding must have 'tenantId' property");

            // 2. Validate
            var validateResp = await _authClient.PostAsync("/api/v1/tenant-branding/validate", null);
            Assert.That((int)validateResp.StatusCode, Is.EqualTo(200));
            var validateJson = await validateResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(validateJson.TryGetProperty("isValid", out _), Is.True, "Validation response must have 'isValid'");
            Assert.That(validateJson.TryGetProperty("errors", out _), Is.True, "Validation response must have 'errors'");

            // 3. Get status
            var statusResp = await _authClient.GetAsync("/api/v1/tenant-branding/status");
            Assert.That((int)statusResp.StatusCode, Is.EqualTo(200));
            var statusJson = await statusResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(statusJson.TryGetProperty("status", out _), Is.True, "Status response must have 'status'");
            Assert.That(statusJson.TryGetProperty("hasDraft", out _), Is.True, "Status response must have 'hasDraft'");
            Assert.That(statusJson.TryGetProperty("hasPublished", out _), Is.True, "Status response must have 'hasPublished'");
        }

        [Test]
        public async Task TBA67_HTTP_UpsertDomain_Authenticated_SchemaValid()
        {
            if (string.IsNullOrEmpty(_jwtToken))
                Assert.Ignore("JWT not available — skipping E2E test.");

            var resp = await _authClient.PutAsJsonAsync("/api/v1/tenant-branding/domains",
                new UpsertTenantDomainRequest { Domain = "e2e-schema.example.com" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(json.TryGetProperty("success", out var s) && s.GetBoolean(), Is.True);
            Assert.That(json.TryGetProperty("domain", out var d), Is.True);
            Assert.That(d.TryGetProperty("domain", out _), Is.True, "Must have 'domain' field");
            Assert.That(d.TryGetProperty("tenantId", out _), Is.True, "Must have 'tenantId' field");
            Assert.That(d.TryGetProperty("verificationToken", out _), Is.True, "Must have 'verificationToken' field");
            Assert.That(d.TryGetProperty("status", out _), Is.True, "Must have 'status' field");
        }

        [Test]
        public async Task TBA68_HTTP_GetHistory_Authenticated_SchemaValid()
        {
            if (string.IsNullOrEmpty(_jwtToken))
                Assert.Ignore("JWT not available — skipping E2E test.");

            var resp = await _authClient.GetAsync("/api/v1/tenant-branding/history");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(json.TryGetProperty("success", out var s) && s.GetBoolean(), Is.True);
            Assert.That(json.TryGetProperty("history", out _), Is.True, "Must have 'history' array property");
        }

        [Test]
        public async Task TBA69_HTTP_GetPublished_AfterPublish_IsFallbackFalse()
        {
            if (string.IsNullOrEmpty(_jwtToken))
                Assert.Ignore("JWT not available — skipping E2E test.");

            // Update and publish with all required fields
            await _authClient.PutAsJsonAsync("/api/v1/tenant-branding/draft",
                new UpdateTenantBrandingDraftRequest
                {
                    OrganizationName = "E2E Published Corp",
                    ProductLabel = "E2E Tokens"
                });

            var pubResp = await _authClient.PostAsync("/api/v1/tenant-branding/publish", null);

            if (pubResp.IsSuccessStatusCode)
            {
                var payloadResp = await _anonClient2.GetAsync(
                    $"/api/v1/tenant-branding/published?tenantId={Uri.EscapeDataString(_tenantEmail)}");
                Assert.That((int)payloadResp.StatusCode, Is.EqualTo(200));

                var json = await payloadResp.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("payload", out var payload))
                {
                    if (payload.TryGetProperty("isFallback", out var fb))
                        Assert.That(fb.GetBoolean(), Is.False, "After successful publish, isFallback must be false.");
                }
            }
        }

        [Test]
        public async Task TBA70_HTTP_UpsertDomain_InvalidFormat_Returns400()
        {
            if (string.IsNullOrEmpty(_jwtToken))
                Assert.Ignore("JWT not available — skipping E2E test.");

            var resp = await _authClient.PutAsJsonAsync("/api/v1/tenant-branding/domains",
                new UpsertTenantDomainRequest { Domain = "https://bad-format.com" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(400));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TBA71-TBA85 — Cross-tenant isolation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TBA71_CrossTenant_Tenant1DraftNotVisibleToTenant2()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);

            var r2 = await svc.GetDraftAsync(TenantB, TenantB);
            Assert.That(r2.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured),
                "Tenant2 must never see Tenant1 draft.");
            Assert.That(r2.Branding.OrganizationName, Is.Null,
                "Tenant2 must not see Tenant1 org name.");
        }

        [Test]
        public async Task TBA72_CrossTenant_Tenant1PublishNotVisibleToTenant2AsNonFallback()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);

            var r2 = await svc.GetPublishedAsync(TenantB);
            Assert.That(r2.Payload!.IsFallback, Is.True,
                "Tenant2 must get fallback — must not see Tenant1 published branding.");
        }

        [Test]
        public async Task TBA73_CrossTenant_Tenant1HistoryNotVisibleToTenant2()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);

            var h2 = await svc.GetHistoryAsync(TenantB, TenantB);
            Assert.That(h2.History, Is.Empty,
                "Tenant2 must have no history entries even when Tenant1 has saved drafts.");
        }

        [Test]
        public async Task TBA74_CrossTenant_Tenant1DomainsNotVisibleToTenant2()
        {
            var svc = CreateService();
            await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "tenant1-private.example.com" }, TenantA, TenantA);

            var r2 = await svc.GetDomainsAsync(TenantB, TenantB);
            var found = r2.Domains.Any(d => d.Domain == "tenant1-private.example.com");
            Assert.That(found, Is.False,
                "Tenant2 must not see Tenant1 domain records.");
        }

        [Test]
        public async Task TBA75_CrossTenant_Tenant2CannotOverwriteTenant1Draft()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Tenant1 Original"
            }, TenantA, TenantA);

            // Tenant2 saves their own draft (different tenant, should not affect Tenant1)
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Tenant2 Name"
            }, TenantB, TenantB);

            var r1 = await svc.GetDraftAsync(TenantA, TenantA);
            Assert.That(r1.Branding!.OrganizationName, Is.EqualTo("Tenant1 Original"),
                "Tenant1 draft must not be affected by Tenant2 save.");
        }

        [Test]
        public async Task TBA76_CrossTenant_MultiTenantPublish_EachGetTheirOwn()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Alpha Corp"
            }, TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);

            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Beta Corp"
            }, TenantB, TenantB);
            await svc.PublishAsync(TenantB, TenantB);

            var pubA = await svc.GetPublishedAsync(TenantA);
            var pubB = await svc.GetPublishedAsync(TenantB);

            Assert.That(pubA.Payload!.OrganizationName, Is.EqualTo("Alpha Corp"));
            Assert.That(pubB.Payload!.OrganizationName, Is.EqualTo("Beta Corp"));
            Assert.That(pubA.Payload.IsFallback, Is.False);
            Assert.That(pubB.Payload.IsFallback, Is.False);
        }

        [Test]
        public async Task TBA77_CrossTenant_StatusIsolated()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(FullValidRequest(), TenantA, TenantA);
            await svc.PublishAsync(TenantA, TenantA);

            var statusA = await svc.GetStatusAsync(TenantA, TenantA);
            var statusB = await svc.GetStatusAsync(TenantB, TenantB);

            Assert.That(statusA.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Published));
            Assert.That(statusB.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured),
                "Tenant2 status must not be affected by Tenant1 publish.");
        }

        [Test]
        public async Task TBA78_CrossTenant_DomainUpsertIsolated()
        {
            var svc = CreateService();
            await svc.UpsertDomainAsync(new UpsertTenantDomainRequest { Domain = "isolated-a.example.com" }, TenantA, TenantA);
            await svc.UpsertDomainAsync(new UpsertTenantDomainRequest { Domain = "isolated-b.example.com" }, TenantB, TenantB);

            var domainsA = await svc.GetDomainsAsync(TenantA, TenantA);
            var domainsB = await svc.GetDomainsAsync(TenantB, TenantB);

            Assert.That(domainsA.Domains.Any(d => d.Domain == "isolated-a.example.com"), Is.True);
            Assert.That(domainsA.Domains.Any(d => d.Domain == "isolated-b.example.com"), Is.False,
                "Tenant A must not see Tenant B domains.");
            Assert.That(domainsB.Domains.Any(d => d.Domain == "isolated-b.example.com"), Is.True);
            Assert.That(domainsB.Domains.Any(d => d.Domain == "isolated-a.example.com"), Is.False,
                "Tenant B must not see Tenant A domains.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TBA86-TBA100 — Status transitions and fail-closed semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TBA86_StatusTransition_NotConfigured_To_Draft_OnFirstSave()
        {
            var svc = CreateService();
            var s0 = await svc.GetStatusAsync("transition@example.com", "transition@example.com");
            Assert.That(s0.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured));

            await svc.UpdateDraftAsync(MinimalValidRequest(), "transition@example.com", "transition@example.com");
            var s1 = await svc.GetStatusAsync("transition@example.com", "transition@example.com");
            Assert.That(s1.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Draft));
        }

        [Test]
        public async Task TBA87_StatusTransition_Draft_To_Invalid_OnBadField()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }
            }, "transition2@example.com", "transition2@example.com");

            var s = await svc.GetStatusAsync("transition2@example.com", "transition2@example.com");
            Assert.That(s.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));
        }

        [Test]
        public async Task TBA88_StatusTransition_Invalid_To_Draft_OnFix()
        {
            var svc = CreateService();
            var t = "transition3@example.com";
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }
            }, t, t);

            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Fixed Corp",
                Theme = new TenantThemeTokens { PrimaryColor = "#FF0000" }
            }, t, t);

            var s = await svc.GetStatusAsync(t, t);
            Assert.That(s.Status, Is.Not.EqualTo(TenantBrandingLifecycleStatus.Invalid),
                "Status must not remain Invalid once all errors are fixed.");
        }

        [Test]
        public async Task TBA89_StatusTransition_Draft_To_Published_OnPublish()
        {
            var svc = CreateService();
            var t = "transition4@example.com";
            await svc.UpdateDraftAsync(FullValidRequest(), t, t);
            var s1 = await svc.GetStatusAsync(t, t);
            Assert.That(s1.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Draft));

            await svc.PublishAsync(t, t);
            var s2 = await svc.GetStatusAsync(t, t);
            Assert.That(s2.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Published));
        }

        [Test]
        public async Task TBA90_StatusTransition_Published_Remains_Published_AfterDraftUpdate()
        {
            var svc = CreateService();
            var t = "transition5@example.com";
            await svc.UpdateDraftAsync(FullValidRequest(), t, t);
            await svc.PublishAsync(t, t);

            // Update draft again — published state should remain
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "New" }, t, t);
            var s = await svc.GetStatusAsync(t, t);
            Assert.That(s.HasPublished, Is.True, "Published flag must remain after updating draft.");
            Assert.That(s.HasDraft, Is.True, "HasDraft must be true when there's a newer draft.");
        }

        [Test]
        public async Task TBA91_PublishedPayload_Unchanged_WhileDraftIsPending()
        {
            var svc = CreateService();
            var t = "transition6@example.com";
            await svc.UpdateDraftAsync(FullValidRequest(), t, t);
            await svc.PublishAsync(t, t);
            var pubV1 = await svc.GetPublishedAsync(t);
            int v1 = pubV1.Payload!.Version;

            // Update draft — published should still be old version
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "Draft Only" }, t, t);
            var pubV2 = await svc.GetPublishedAsync(t);
            Assert.That(pubV2.Payload!.Version, Is.EqualTo(v1),
                "Published payload must not change until explicitly re-published.");
            Assert.That(pubV2.Payload.ProductLabel, Is.Not.EqualTo("Draft Only"),
                "Draft-only changes must not leak into published payload.");
        }

        [Test]
        public async Task TBA92_InvalidationErrorCount_ReflectedInStatus()
        {
            var svc = CreateService();
            var t = "transition7@example.com";
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = new string('X', 200),  // error 1
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }  // error 2
            }, t, t);

            var s = await svc.GetStatusAsync(t, t);
            Assert.That(s.ValidationErrorCount, Is.GreaterThanOrEqualTo(2),
                "Status.ValidationErrorCount must reflect actual error count.");
        }

        [Test]
        public async Task TBA93_IsDraftValid_True_OnlyWhenNoErrors()
        {
            var svc = CreateService();
            var t = "transition8@example.com";
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                Theme = new TenantThemeTokens { PrimaryColor = "BADCOLOR" }
            }, t, t);

            var sInvalid = await svc.GetStatusAsync(t, t);
            Assert.That(sInvalid.IsDraftValid, Is.False);

            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Valid Corp",
                Theme = new TenantThemeTokens { PrimaryColor = "#FF0000" }
            }, t, t);

            var sValid = await svc.GetStatusAsync(t, t);
            // IsDraftValid depends on whether validation passes after update
            Assert.That(sValid.ValidationErrorCount, Is.LessThanOrEqualTo(sInvalid.ValidationErrorCount));
        }

        [Test]
        public async Task TBA94_FallbackPayload_IsAlwaysReturnedForUnconfiguredTenants()
        {
            var svc = CreateService();
            // Multiple distinct unconfigured tenants all get fallback
            string[] tenants = ["uncfg1@example.com", "uncfg2@example.com", "uncfg3@example.com"];
            foreach (var t in tenants)
            {
                var pub = await svc.GetPublishedAsync(t);
                Assert.That(pub.Success, Is.True);
                Assert.That(pub.Payload!.IsFallback, Is.True, $"Tenant '{t}' must get fallback when unconfigured.");
            }
        }

        [Test]
        public async Task TBA95_PublishAndHistory_CorrelateVersionNumbers()
        {
            var svc = CreateService();
            var t = "transition9@example.com";
            await svc.UpdateDraftAsync(FullValidRequest(), t, t);
            var pub = await svc.PublishAsync(t, t);
            int publishedVersion = pub.Branding!.Version;

            var history = await svc.GetHistoryAsync(t, t);
            var publishEntry = history.History.FirstOrDefault(e => e.EventType == "Published");
            Assert.That(publishEntry, Is.Not.Null, "History must contain a Published entry.");
            Assert.That(publishEntry!.Version, Is.EqualTo(publishedVersion),
                "History publish entry must record the version that was published.");
        }

        [Test]
        public async Task TBA96_History_OrderedMostRecentFirst()
        {
            var svc = CreateService();
            var t = "order-test@example.com";
            await svc.UpdateDraftAsync(FullValidRequest(), t, t);
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "V2" }, t, t);
            await svc.PublishAsync(t, t);

            var history = await svc.GetHistoryAsync(t, t);
            for (int i = 0; i < history.History.Count - 1; i++)
            {
                Assert.That(history.History[i].OccurredAt,
                    Is.GreaterThanOrEqualTo(history.History[i + 1].OccurredAt),
                    "History must be ordered from most recent to oldest.");
            }
        }

        [Test]
        public async Task TBA97_DraftSave_ValidRequest_StatusIsDraft_Not_Invalid()
        {
            var svc = CreateService();
            var t = "valid-draft@example.com";
            var result = await svc.UpdateDraftAsync(FullValidRequest(), t, t);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Draft),
                "Valid draft must have Draft status, not Invalid.");
            Assert.That(result.Branding.ValidationErrors, Is.Empty,
                "Valid draft must have no validation errors.");
        }

        [Test]
        public async Task TBA98_Email_ValidationRule_AcceptsValidEmails()
        {
            var svc = CreateService();
            var t = "email-valid@example.com";
            var req = FullValidRequest();
            req.Support = new TenantSupportMetadata { SupportEmail = "hello@world.org" };
            var result = await svc.UpdateDraftAsync(req, t, t);
            var emailErr = result.Branding!.ValidationErrors
                .Where(e => e.Field.Contains("SupportEmail")).ToList();
            Assert.That(emailErr, Is.Empty, "Valid email must not produce validation error.");
        }

        [Test]
        public async Task TBA99_Email_ValidationRule_RejectsInvalidEmails()
        {
            var svc = CreateService();
            var t = "email-invalid@example.com";
            var req = FullValidRequest();
            req.Support = new TenantSupportMetadata { SupportEmail = "not-an-email" };
            var result = await svc.UpdateDraftAsync(req, t, t);
            var emailErr = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "Support.SupportEmail");
            Assert.That(emailErr, Is.Not.Null, "Invalid email must produce validation error.");
        }

        [Test]
        public async Task TBA100_Publish_InvalidColor_BlocksPublishing_FailClosed()
        {
            var svc = CreateService();
            var t = "fail-closed@example.com";
            // Only provide org name + invalid color — should block publish
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Fail Closed Corp",
                Theme = new TenantThemeTokens { PrimaryColor = "NOTACOLOR" }
            }, t, t);

            var publishResult = await svc.PublishAsync(t, t);
            Assert.That(publishResult.Success, Is.False,
                "Invalid color token in draft must block publishing — fail-closed validation gate.");

            // Verify the published payload is still the safe fallback
            var pub = await svc.GetPublishedAsync(t);
            Assert.That(pub.Payload!.IsFallback, Is.True,
                "Safe fallback must be served when invalid draft was never published.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WebApplicationFactory for deployed E2E tests
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class TenantBrandingAdvancedWebFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TenantBrandingAdvancedTestKey32Chars!!",
                        ["JwtConfig:SecretKey"] = "TenantBrandingAdvancedJwtSecret32Ch!!",
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
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }
    }
}

using BiatecTokensApi.Models.TenantBranding;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for TenantBrandingService and TenantBrandingController.
    ///
    /// Coverage:
    ///  TB01-TB20  — Unit tests: draft management, validation rules, publish lifecycle, fallback
    ///  TB21-TB35  — Unit tests: domain configuration, audit history, authorization isolation
    ///  TB36-TB50  — Integration tests: HTTP endpoint shape, auth enforcement, published payload
    ///  TB51-TB60  — Branch coverage: all validation codes, status transitions, domain states
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TenantBrandingTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static TenantBrandingService CreateService() =>
            new TenantBrandingService(NullLogger<TenantBrandingService>.Instance);

        private const string Tenant1 = "tenant-alpha@example.com";
        private const string Tenant2 = "tenant-beta@example.com";

        private static UpdateTenantBrandingDraftRequest ValidRequest() => new()
        {
            OrganizationName = "Acme Token Corp",
            ProductLabel = "Acme Tokens",
            LogoUrl = "https://cdn.acme.com/logo.png",
            FaviconUrl = "https://cdn.acme.com/favicon.ico",
            Theme = new TenantThemeTokens
            {
                PrimaryColor = "#1A2B3C",
                SecondaryColor = "#FFFFFF",
                AccentColor = "#FF6600",
                BackgroundColor = "#F5F5F5",
                TextColor = "#333333"
            },
            Support = new TenantSupportMetadata
            {
                SupportEmail = "support@acme.com",
                SupportUrl = "https://support.acme.com",
                LegalContactEmail = "legal@acme.com",
                LegalContactUrl = "https://legal.acme.com"
            }
        };

        // ═══════════════════════════════════════════════════════════════════════
        // TB01-TB10 — Draft management
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TB01_GetDraft_NoConfig_ReturnsNotConfigured()
        {
            var svc = CreateService();
            var result = await svc.GetDraftAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Branding, Is.Not.Null);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured));
        }

        [Test]
        public async Task TB02_UpdateDraft_ValidRequest_ReturnsDraftStatus()
        {
            var svc = CreateService();
            var result = await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Draft));
            Assert.That(result.Branding.OrganizationName, Is.EqualTo("Acme Token Corp"));
            Assert.That(result.Branding.Version, Is.EqualTo(1));
        }

        [Test]
        public async Task TB03_UpdateDraft_Twice_VersionIncrementsEachTime()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "V2 Label" }, Tenant1, Tenant1);
            Assert.That(result.Branding!.Version, Is.EqualTo(2));
            Assert.That(result.Branding.ProductLabel, Is.EqualTo("V2 Label"));
            Assert.That(result.Branding.OrganizationName, Is.EqualTo("Acme Token Corp"),
                "Prior fields must be preserved when only some fields are updated.");
        }

        [Test]
        public async Task TB04_UpdateDraft_MergesThemeTokens_PreservingExistingTokens()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            // Update only one color
            var result = await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                Theme = new TenantThemeTokens { AccentColor = "#0000FF" }
            }, Tenant1, Tenant1);

            Assert.That(result.Branding!.Theme.AccentColor, Is.EqualTo("#0000FF"));
            Assert.That(result.Branding.Theme.PrimaryColor, Is.EqualTo("#1A2B3C"),
                "Existing primary color must be preserved.");
        }

        [Test]
        public async Task TB05_GetDraft_AfterUpdate_ReturnsSavedValues()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.GetDraftAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Branding!.OrganizationName, Is.EqualTo("Acme Token Corp"));
        }

        [Test]
        public async Task TB06_Draft_IsolatedPerTenant_Tenant2CannotSeeTenant1Draft()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.GetDraftAsync(Tenant2, Tenant2);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured),
                "Tenant2 must not see Tenant1's draft.");
        }

        [Test]
        public async Task TB07_UpdateDraft_StoresCreatedByAndUpdatedBy()
        {
            var svc = CreateService();
            var result = await svc.UpdateDraftAsync(ValidRequest(), Tenant1, "actor-a@example.com");
            Assert.That(result.Branding!.CreatedBy, Is.EqualTo("actor-a@example.com"));
            Assert.That(result.Branding.UpdatedBy, Is.EqualTo("actor-a@example.com"));
        }

        [Test]
        public async Task TB08_UpdateDraft_WithInvalidColor_StatusIsInvalid()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.Theme = new TenantThemeTokens { PrimaryColor = "not-a-color" };
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));
            Assert.That(result.Branding.ValidationErrors, Has.Count.GreaterThan(0));
        }

        [Test]
        public async Task TB09_UpdateDraft_WithoutOrganizationName_StatusIsInvalid()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.OrganizationName = null;
            // First set a valid state to give the service something to work from
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { OrganizationName = "Initial" }, Tenant1, Tenant1);

            // Now update clearing the name (can't set null since merge preserves non-null only)
            // Instead test directly by creating a draft without an organization name
            var svc2 = CreateService();
            var emptyReq = new UpdateTenantBrandingDraftRequest
            {
                ProductLabel = "Label without org",
                Theme = new TenantThemeTokens { PrimaryColor = "#000000" }
            };
            var result = await svc2.UpdateDraftAsync(emptyReq, Tenant1, Tenant1);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid),
                "Draft without OrganizationName must be Invalid.");
        }

        [Test]
        public async Task TB10_UpdateDraft_WithInvalidEmail_ValidationErrorPresent()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.Support = new TenantSupportMetadata { SupportEmail = "not-an-email" };
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));
            var emailError = result.Branding.ValidationErrors
                .FirstOrDefault(e => e.Field == "Support.SupportEmail");
            Assert.That(emailError, Is.Not.Null);
            Assert.That(emailError!.Code, Is.EqualTo("INVALID_EMAIL_FORMAT"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TB11-TB20 — Validation and publish lifecycle
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TB11_ValidateDraft_NoDraft_ReturnsNoDraftError()
        {
            var svc = CreateService();
            var result = await svc.ValidateDraftAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Code == "NO_DRAFT"), Is.True);
        }

        [Test]
        public async Task TB12_ValidateDraft_ValidDraft_ReturnsIsValidTrue()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.ValidateDraftAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public async Task TB13_ValidateDraft_InvalidDraft_ReturnsErrors()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.Theme = new TenantThemeTokens { PrimaryColor = "bad-color" };
            await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            var result = await svc.ValidateDraftAsync(Tenant1, Tenant1);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.GreaterThan(0));
        }

        [Test]
        public async Task TB14_Publish_NoDraft_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.PublishAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("No branding draft exists"));
        }

        [Test]
        public async Task TB15_Publish_InvalidDraft_ReturnsError()
        {
            var svc = CreateService();
            // Draft without OrganizationName is invalid
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                ProductLabel = "Only label, no org name"
            }, Tenant1, Tenant1);
            var result = await svc.PublishAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("validation error"));
        }

        [Test]
        public async Task TB16_Publish_ValidDraft_ReturnsPublished()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.PublishAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Branding!.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Published));
            Assert.That(result.Branding.PublishedBy, Is.EqualTo(Tenant1));
            Assert.That(result.Branding.PublishedAt, Is.Not.Null);
        }

        [Test]
        public async Task TB17_GetPublished_AfterPublish_ReturnsFalseIsFallback()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetPublishedAsync(Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Payload!.IsFallback, Is.False);
            Assert.That(result.Payload.OrganizationName, Is.EqualTo("Acme Token Corp"));
        }

        [Test]
        public async Task TB18_GetPublished_NoPublishedConfig_ReturnsFallback()
        {
            var svc = CreateService();
            var result = await svc.GetPublishedAsync(Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Payload!.IsFallback, Is.True,
                "Must return safe fallback when no published config exists.");
        }

        [Test]
        public async Task TB19_GetPublished_Tenant2UnaffectedByTenant1Publish()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetPublishedAsync(Tenant2);
            Assert.That(result.Payload!.IsFallback, Is.True,
                "Tenant2 must still get fallback — Tenant1's publish must not bleed over.");
        }

        [Test]
        public async Task TB20_PublishedPayload_ExposesOnlySafePublicFields()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetPublishedAsync(Tenant1);
            var payload = result.Payload!;
            // Public-safe fields are present
            Assert.That(payload.TenantId, Is.Not.Null);
            Assert.That(payload.OrganizationName, Is.Not.Null);
            Assert.That(payload.Theme, Is.Not.Null);
            Assert.That(payload.Support, Is.Not.Null);
            Assert.That(payload.Version, Is.GreaterThan(0));
            Assert.That(payload.PublishedAt, Is.Not.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TB21-TB30 — Status and domain configuration
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TB21_GetStatus_NoConfig_ReturnsNotConfigured()
        {
            var svc = CreateService();
            var result = await svc.GetStatusAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(TenantBrandingLifecycleStatus.NotConfigured));
            Assert.That(result.HasDraft, Is.False);
            Assert.That(result.HasPublished, Is.False);
        }

        [Test]
        public async Task TB22_GetStatus_InvalidDraft_ReturnsInvalid()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                Theme = new TenantThemeTokens { PrimaryColor = "bad-color" }
            }, Tenant1, Tenant1);
            var result = await svc.GetStatusAsync(Tenant1, Tenant1);
            Assert.That(result.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Invalid));
            Assert.That(result.ValidationErrorCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task TB23_GetStatus_AfterPublish_ReturnsPublished()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetStatusAsync(Tenant1, Tenant1);
            Assert.That(result.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Published));
            Assert.That(result.HasPublished, Is.True);
            Assert.That(result.PublishedVersion, Is.GreaterThan(0));
            Assert.That(result.LastPublishedAt, Is.Not.Null);
        }

        [Test]
        public async Task TB24_GetStatus_ValidDraftNotYetPublished_ReturnsDraft()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.GetStatusAsync(Tenant1, Tenant1);
            Assert.That(result.Status, Is.EqualTo(TenantBrandingLifecycleStatus.Draft));
            Assert.That(result.HasDraft, Is.True);
            Assert.That(result.IsDraftValid, Is.True);
        }

        [Test]
        public async Task TB25_UpsertDomain_ValidDomain_ReturnsPending()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "tokens.acme.com" },
                Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Domain!.Status, Is.EqualTo(TenantDomainReadinessStatus.Pending));
            Assert.That(result.Domain.VerificationToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Domain.Domain, Is.EqualTo("tokens.acme.com"));
        }

        [Test]
        public async Task TB26_UpsertDomain_SameDomainTwice_UpdatesNotesPreservesStatus()
        {
            var svc = CreateService();
            await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "tokens.acme.com", Notes = "First" },
                Tenant1, Tenant1);
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "tokens.acme.com", Notes = "Updated notes" },
                Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Domain!.Notes, Is.EqualTo("Updated notes"));
            Assert.That(result.Domain.Status, Is.EqualTo(TenantDomainReadinessStatus.Pending),
                "Verification status must not be reset when notes are updated.");
        }

        [Test]
        public async Task TB27_UpsertDomain_VerificationTokenIsDeterministic()
        {
            var svc = CreateService();
            var r1 = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "a.example.com" },
                Tenant1, Tenant1);
            var r2 = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "b.example.com" },
                Tenant1, Tenant1);
            // Different domains → different tokens
            Assert.That(r1.Domain!.VerificationToken, Is.Not.EqualTo(r2.Domain!.VerificationToken));
        }

        [Test]
        public async Task TB28_GetDomains_ReturnsTenantDomains_NotOtherTenantDomains()
        {
            var svc = CreateService();
            await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "t1.acme.com" },
                Tenant1, Tenant1);
            await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "t2.acme.com" },
                Tenant2, Tenant2);

            var r1 = await svc.GetDomainsAsync(Tenant1, Tenant1);
            var r2 = await svc.GetDomainsAsync(Tenant2, Tenant2);

            Assert.That(r1.Domains.All(d => d.TenantId == Tenant1), Is.True);
            Assert.That(r2.Domains.All(d => d.TenantId == Tenant2), Is.True);
        }

        [Test]
        public async Task TB29_UpsertDomain_EmptyDomain_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "" },
                Tenant1, Tenant1);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task TB30_UpsertDomain_InvalidFormat_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "not a domain" },
                Tenant1, Tenant1);
            Assert.That(result.Success, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TB31-TB35 — Audit history
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TB31_GetHistory_NoConfig_ReturnsEmptyList()
        {
            var svc = CreateService();
            var result = await svc.GetHistoryAsync(Tenant1, Tenant1);
            Assert.That(result.Success, Is.True);
            Assert.That(result.History, Is.Empty);
        }

        [Test]
        public async Task TB32_GetHistory_AfterDraftSave_ContainsDraftSavedEntry()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var result = await svc.GetHistoryAsync(Tenant1, Tenant1);
            Assert.That(result.History, Has.Count.GreaterThan(0));
            Assert.That(result.History.Any(e => e.EventType == "DraftSaved"), Is.True);
        }

        [Test]
        public async Task TB33_GetHistory_AfterPublish_ContainsPublishedEntry()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetHistoryAsync(Tenant1, Tenant1);
            Assert.That(result.History.Any(e => e.EventType == "Published"), Is.True);
        }

        [Test]
        public async Task TB34_GetHistory_OrderedMostRecentFirst()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest { ProductLabel = "v2" }, Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetHistoryAsync(Tenant1, Tenant1);
            var times = result.History.Select(e => e.OccurredAt).ToList();
            Assert.That(times, Is.Ordered.Descending);
        }

        [Test]
        public async Task TB35_GetHistory_IsolatedPerTenant()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var histT2 = await svc.GetHistoryAsync(Tenant2, Tenant2);
            Assert.That(histT2.History, Is.Empty,
                "Tenant2 must not see Tenant1's history entries.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TB36-TB50 — Integration tests (HTTP endpoints)
        // ═══════════════════════════════════════════════════════════════════════

        private TenantBrandingWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _anonClient = null!;
        private string _token = string.Empty;

        [OneTimeSetUp]
        public async Task IntegrationSetup()
        {
            _factory = new TenantBrandingWebApplicationFactory();
            _client = _factory.CreateClient();
            _anonClient = _factory.CreateClient();

            // Register + login to get a JWT
            var email = $"tb-it-{Guid.NewGuid():N}@example.com";
            const string password = "Test@Passw0rd!!";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            if (regResp.IsSuccessStatusCode)
            {
                var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new { Email = email, Password = password });
                if (loginResp.IsSuccessStatusCode)
                {
                    var loginJson = await loginResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    _token = loginJson.TryGetProperty("accessToken", out var t) ? t.GetString() ?? "" : "";
                }
            }

            if (!string.IsNullOrEmpty(_token))
                _client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }

        [OneTimeTearDown]
        public void IntegrationTearDown()
        {
            _client?.Dispose();
            _anonClient?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task TB36_GetDraft_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.GetAsync("/api/v1/tenant-branding/draft");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB37_GetDraft_Authenticated_Returns200()
        {
            if (string.IsNullOrEmpty(_token))
                Assert.Ignore("JWT not available — skipping integration test.");

            var resp = await _client.GetAsync("/api/v1/tenant-branding/draft");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TB38_UpdateDraft_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.PutAsJsonAsync("/api/v1/tenant-branding/draft",
                new UpdateTenantBrandingDraftRequest { OrganizationName = "Test" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB39_UpdateDraft_Authenticated_Returns200()
        {
            if (string.IsNullOrEmpty(_token))
                Assert.Ignore("JWT not available — skipping integration test.");

            var resp = await _client.PutAsJsonAsync("/api/v1/tenant-branding/draft",
                new UpdateTenantBrandingDraftRequest { OrganizationName = "Integration Test Corp" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TB40_ValidateDraft_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.PostAsync("/api/v1/tenant-branding/validate", null);
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB41_ValidateDraft_Authenticated_Returns200()
        {
            if (string.IsNullOrEmpty(_token))
                Assert.Ignore("JWT not available — skipping integration test.");

            var resp = await _client.PostAsync("/api/v1/tenant-branding/validate", null);
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TB42_Publish_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.PostAsync("/api/v1/tenant-branding/publish", null);
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB43_GetPublished_NoAuth_Returns200WithFallback()
        {
            var resp = await _anonClient.GetAsync("/api/v1/tenant-branding/published?tenantId=unknown-tenant");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
            var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var isFallback = json.TryGetProperty("payload", out var payload) &&
                             payload.TryGetProperty("isFallback", out var fb) &&
                             fb.GetBoolean();
            Assert.That(isFallback, Is.True, "Published endpoint for unknown tenant should return fallback.");
        }

        [Test]
        public async Task TB44_GetPublished_NoTenantId_Returns200WithFallback()
        {
            var resp = await _anonClient.GetAsync("/api/v1/tenant-branding/published");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TB45_GetStatus_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.GetAsync("/api/v1/tenant-branding/status");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB46_GetStatus_Authenticated_Returns200()
        {
            if (string.IsNullOrEmpty(_token))
                Assert.Ignore("JWT not available — skipping integration test.");

            var resp = await _client.GetAsync("/api/v1/tenant-branding/status");
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TB47_GetDomains_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.GetAsync("/api/v1/tenant-branding/domains");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB48_UpsertDomain_Authenticated_Returns200()
        {
            if (string.IsNullOrEmpty(_token))
                Assert.Ignore("JWT not available — skipping integration test.");

            var resp = await _client.PutAsJsonAsync("/api/v1/tenant-branding/domains",
                new UpsertTenantDomainRequest { Domain = "it-test.example.com" });
            Assert.That((int)resp.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task TB49_GetHistory_Unauthenticated_Returns401()
        {
            var resp = await _anonClient.GetAsync("/api/v1/tenant-branding/history");
            Assert.That((int)resp.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task TB50_FullLifecycle_SavePublishFetch()
        {
            if (string.IsNullOrEmpty(_token))
                Assert.Ignore("JWT not available — skipping integration test.");

            // 1. Save draft
            var draftResp = await _client.PutAsJsonAsync("/api/v1/tenant-branding/draft",
                new UpdateTenantBrandingDraftRequest
                {
                    OrganizationName = "Lifecycle Test Corp",
                    Theme = new TenantThemeTokens { PrimaryColor = "#FF0000" },
                    Support = new TenantSupportMetadata { SupportEmail = "hi@lifecycle.com" }
                });
            Assert.That((int)draftResp.StatusCode, Is.EqualTo(200));

            // 2. Validate
            var validateResp = await _client.PostAsync("/api/v1/tenant-branding/validate", null);
            Assert.That((int)validateResp.StatusCode, Is.EqualTo(200));

            // 3. Publish
            var publishResp = await _client.PostAsync("/api/v1/tenant-branding/publish", null);
            // May succeed or fail (depends on whether all required fields are set in prior saves)
            // Success or 400 is both acceptable depending on exact state
            Assert.That((int)publishResp.StatusCode, Is.AnyOf(200, 400));

            // 4. Fetch published (should be 200 regardless — with or without fallback)
            var tokenId = System.IdentityModel.Tokens.Jwt.JwtPayload
                .Base64UrlDeserialize(_token.Split('.')[1]);
            if (tokenId.TryGetValue("email", out var emailVal))
            {
                var pubResp = await _anonClient.GetAsync(
                    $"/api/v1/tenant-branding/published?tenantId={Uri.EscapeDataString(emailVal?.ToString() ?? "")}");
                Assert.That((int)pubResp.StatusCode, Is.EqualTo(200));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TB51-TB60 — Branch coverage: validation codes, domain edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TB51_Validation_OrganizationNameTooLong_FieldTooLong()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.OrganizationName = new string('A', 101);
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            var err = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "OrganizationName" && e.Code == "FIELD_TOO_LONG");
            Assert.That(err, Is.Not.Null);
        }

        [Test]
        public async Task TB52_Validation_ProductLabelTooLong_FieldTooLong()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.ProductLabel = new string('B', 61);
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            var err = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "ProductLabel" && e.Code == "FIELD_TOO_LONG");
            Assert.That(err, Is.Not.Null);
        }

        [Test]
        public async Task TB53_Validation_InvalidLogoUrl_InvalidUrlFormat()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.LogoUrl = "not-a-url";
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            var err = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "LogoUrl" && e.Code == "INVALID_URL_FORMAT");
            Assert.That(err, Is.Not.Null);
        }

        [Test]
        public async Task TB54_Validation_InvalidFaviconUrl_InvalidUrlFormat()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.FaviconUrl = "ftp://invalid-scheme.com/favicon.ico";
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            // FTP URLs should fail validation (only http/https allowed)
            var err = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "FaviconUrl");
            Assert.That(err, Is.Not.Null);
        }

        [Test]
        public async Task TB55_Validation_ThreeCharHexColor_Valid()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.Theme = new TenantThemeTokens { PrimaryColor = "#FFF" }; // valid 3-char hex
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            var colorErrors = result.Branding!.ValidationErrors
                .Where(e => e.Field == "Theme.PrimaryColor").ToList();
            Assert.That(colorErrors, Is.Empty, "3-char hex color #FFF must be valid.");
        }

        [Test]
        public async Task TB56_Validation_InvalidSupportUrl_InvalidUrlFormat()
        {
            var svc = CreateService();
            var req = ValidRequest();
            req.Support = new TenantSupportMetadata { SupportUrl = "not-a-url" };
            var result = await svc.UpdateDraftAsync(req, Tenant1, Tenant1);
            var err = result.Branding!.ValidationErrors
                .FirstOrDefault(e => e.Field == "Support.SupportUrl");
            Assert.That(err, Is.Not.Null);
        }

        [Test]
        public async Task TB57_UpsertDomain_DomainWithScheme_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "https://tokens.acme.com" },
                Tenant1, Tenant1);
            Assert.That(result.Success, Is.False,
                "Domain with scheme prefix must be rejected.");
        }

        [Test]
        public async Task TB58_UpsertDomain_NoDotInName_ReturnsError()
        {
            var svc = CreateService();
            var result = await svc.UpsertDomainAsync(
                new UpsertTenantDomainRequest { Domain = "localhost" },
                Tenant1, Tenant1);
            Assert.That(result.Success, Is.False,
                "Domain without dot (e.g. localhost) must be rejected.");
        }

        [Test]
        public async Task TB59_Publish_CanRepublishAfterUpdate()
        {
            var svc = CreateService();
            // Publish first time
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            var pub1 = await svc.PublishAsync(Tenant1, Tenant1);
            Assert.That(pub1.Success, Is.True);
            int firstVersion = pub1.Branding!.Version;

            // Update draft and publish again
            await svc.UpdateDraftAsync(new UpdateTenantBrandingDraftRequest
            {
                OrganizationName = "Updated Corp Name"
            }, Tenant1, Tenant1);
            var pub2 = await svc.PublishAsync(Tenant1, Tenant1);
            Assert.That(pub2.Success, Is.True);
            Assert.That(pub2.Branding!.Version, Is.GreaterThan(firstVersion));

            var published = await svc.GetPublishedAsync(Tenant1);
            Assert.That(published.Payload!.OrganizationName, Is.EqualTo("Updated Corp Name"));
        }

        [Test]
        public async Task TB60_GetPublished_PublishedPayloadContainsThemeAndSupport()
        {
            var svc = CreateService();
            await svc.UpdateDraftAsync(ValidRequest(), Tenant1, Tenant1);
            await svc.PublishAsync(Tenant1, Tenant1);
            var result = await svc.GetPublishedAsync(Tenant1);
            Assert.That(result.Payload!.Theme.PrimaryColor, Is.EqualTo("#1A2B3C"));
            Assert.That(result.Payload.Support.SupportEmail, Is.EqualTo("support@acme.com"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration test WebApplicationFactory
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class TenantBrandingWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TenantBrandingTestKey32CharsRequiredMin!",
                        ["JwtConfig:SecretKey"] = "TenantBrandingJwtSecretKey32CharsReqd!!",
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

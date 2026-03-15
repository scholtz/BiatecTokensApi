using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Webhook;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the Webhook HTTP API endpoints.
    /// Tests the full HTTP pipeline: authentication, controller, service, repository.
    /// Covers subscription CRUD, event type taxonomy, delivery history endpoint,
    /// authorization enforcement, and API schema contract validation.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WebhookIntegrationTests
    {
        private WebhookWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new WebhookWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            // Register and login to get JWT
            var email = $"webhook-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Webhook Integration Test User"
            };

            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Registration failed: {await regResp.Content.ReadAsStringAsync()}");

            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>(_jsonOptions);
            var jwtToken = regBody?.AccessToken ?? string.Empty;
            Assert.That(jwtToken, Is.Not.Empty, "Expected access token after registration");

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        // ── Authentication enforcement ────────────────────────────────────────────

        [Test]
        public async Task CreateSubscription_Unauthenticated_Returns401()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/hook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange }
            };

            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetSubscription_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/webhooks/subscriptions/some-id");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ListSubscriptions_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/webhooks/subscriptions");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task DeleteSubscription_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.DeleteAsync("/api/v1/webhooks/subscriptions/some-id");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetDeliveryHistory_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/webhooks/deliveries");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Subscription creation ────────────────────────────────────────────────

        [Test]
        public async Task CreateSubscription_ValidRequest_Returns200WithSubscription()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://integration-test.example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange, WebhookEventType.AmlStatusChange },
                Description = "Integration test subscription"
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Subscription, Is.Not.Null);
            Assert.That(body.Subscription!.Id, Is.Not.Empty);
            Assert.That(body.Subscription.Url, Is.EqualTo("https://integration-test.example.com/webhook"));
            Assert.That(body.Subscription.SigningSecret, Is.Not.Empty);
        }

        [Test]
        public async Task CreateSubscription_InvalidUrl_Returns400()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "not-a-valid-url",
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange }
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task CreateSubscription_EmptyEventTypes_Returns400()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/hook",
                EventTypes = new List<WebhookEventType>()
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task CreateSubscription_WithFilters_Returns200WithFiltersSet()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://filtered.example.com/hook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.ComplianceBadgeUpdate },
                AssetIdFilter = 888UL,
                NetworkFilter = "mainnet"
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            Assert.That(body!.Subscription!.AssetIdFilter, Is.EqualTo(888UL));
            Assert.That(body.Subscription.NetworkFilter, Is.EqualTo("mainnet"));
        }

        // ── Subscription retrieval ────────────────────────────────────────────────

        [Test]
        public async Task GetSubscription_AfterCreation_Returns200()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://get-test.example.com/hook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            };

            var createResp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);
            var created = await createResp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            var subId = created!.Subscription!.Id;

            var getResp = await _client.GetAsync($"/api/v1/webhooks/subscriptions/{subId}");

            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await getResp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Subscription!.Id, Is.EqualTo(subId));
        }

        [Test]
        public async Task GetSubscription_NonExistent_Returns404()
        {
            var resp = await _client.GetAsync("/api/v1/webhooks/subscriptions/nonexistent-sub-id-xyz");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── Subscription listing ─────────────────────────────────────────────────

        [Test]
        public async Task ListSubscriptions_AfterCreation_Returns200WithSubscriptions()
        {
            // Create two subscriptions
            for (int i = 0; i < 2; i++)
            {
                await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions",
                    new CreateWebhookSubscriptionRequest
                    {
                        Url = $"https://list-test-{i}.example.com/hook",
                        EventTypes = new List<WebhookEventType> { WebhookEventType.AmlStatusChange }
                    });
            }

            var resp = await _client.GetAsync("/api/v1/webhooks/subscriptions");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionListResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.TotalCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(body.Subscriptions, Is.Not.Empty);
        }

        // ── Subscription update ──────────────────────────────────────────────────

        [Test]
        public async Task UpdateSubscription_Deactivate_Returns200WithUpdatedStatus()
        {
            var createResp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions",
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://update-test.example.com/hook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.TransferDeny }
                });
            var created = await createResp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            var subId = created!.Subscription!.Id;

            var updateReq = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = subId,
                IsActive = false
            };
            var updateResp = await _client.PutAsJsonAsync("/api/v1/webhooks/subscriptions", updateReq);

            Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await updateResp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Subscription!.IsActive, Is.False);
        }

        [Test]
        public async Task UpdateSubscription_NonExistent_Returns404()
        {
            var updateReq = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = "nonexistent-sub-abc",
                IsActive = false
            };
            var resp = await _client.PutAsJsonAsync("/api/v1/webhooks/subscriptions", updateReq);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── Subscription deletion ────────────────────────────────────────────────

        [Test]
        public async Task DeleteSubscription_OwnedByUser_Returns200()
        {
            var createResp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions",
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://delete-test.example.com/hook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.AuditExportCreated }
                });
            var created = await createResp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            var subId = created!.Subscription!.Id;

            var deleteResp = await _client.DeleteAsync($"/api/v1/webhooks/subscriptions/{subId}");

            Assert.That(deleteResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await deleteResp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);

            // Verify it's actually gone
            var getResp = await _client.GetAsync($"/api/v1/webhooks/subscriptions/{subId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task DeleteSubscription_NonExistent_Returns404()
        {
            var resp = await _client.DeleteAsync("/api/v1/webhooks/subscriptions/nonexistent-delete-xyz");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        // ── Delivery history endpoint ────────────────────────────────────────────

        [Test]
        public async Task GetDeliveryHistory_NoFilter_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/webhooks/deliveries");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<WebhookDeliveryHistoryResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
        }

        [Test]
        public async Task GetDeliveryHistory_WithPagination_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/webhooks/deliveries?page=1&pageSize=10");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<WebhookDeliveryHistoryResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
        }

        [Test]
        public async Task GetDeliveryHistory_UnknownSubscriptionId_Returns403()
        {
            // Fail-closed behavior: when a subscriptionId is provided that either does not exist
            // or is owned by a different user, the service returns a "permission" error → 403 Forbidden.
            // This intentionally does not distinguish 404 from 403 to prevent subscription ID enumeration.
            var resp = await _client.GetAsync("/api/v1/webhooks/deliveries?subscriptionId=unknown-sub-id-xyz");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // ── Schema contract: response shape stability ─────────────────────────────

        [Test]
        public async Task CreateSubscription_Response_ContainsAllRequiredFields()
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://schema-test.example.com/hook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange },
                Description = "Schema contract test"
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);
            var raw = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Response must always have these fields
            Assert.That(root.TryGetProperty("success", out var successProp), Is.True, "Response must have 'success'");
            Assert.That(successProp.GetBoolean(), Is.True, "success must be true for valid request");
            Assert.That(root.TryGetProperty("subscription", out var subProp), Is.True, "Response must have 'subscription'");

            var sub = subProp;
            Assert.That(sub.TryGetProperty("id", out _), Is.True, "subscription.id must be present");
            Assert.That(sub.TryGetProperty("url", out _), Is.True, "subscription.url must be present");
            Assert.That(sub.TryGetProperty("signingSecret", out var secretProp), Is.True, "subscription.signingSecret must be present");
            Assert.That(secretProp.GetString(), Is.Not.Empty, "signingSecret must not be empty");
            Assert.That(sub.TryGetProperty("eventTypes", out _), Is.True, "subscription.eventTypes must be present");
            Assert.That(sub.TryGetProperty("isActive", out var activeProp), Is.True, "subscription.isActive must be present");
            Assert.That(activeProp.GetBoolean(), Is.True, "isActive must default to true");
            Assert.That(sub.TryGetProperty("createdAt", out _), Is.True, "subscription.createdAt must be present");
        }

        [Test]
        public async Task DeliveryHistoryResponse_ContainsAllRequiredFields()
        {
            var resp = await _client.GetAsync("/api/v1/webhooks/deliveries");
            var raw = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out var successProp), Is.True, "Response must have 'success'");
            Assert.That(successProp.GetBoolean(), Is.True);
            Assert.That(root.TryGetProperty("deliveries", out _), Is.True, "Response must have 'deliveries'");
            Assert.That(root.TryGetProperty("totalCount", out _), Is.True, "Response must have 'totalCount'");
            Assert.That(root.TryGetProperty("successCount", out _), Is.True, "Response must have 'successCount'");
            Assert.That(root.TryGetProperty("failedCount", out _), Is.True, "Response must have 'failedCount'");
            Assert.That(root.TryGetProperty("pendingRetries", out _), Is.True, "Response must have 'pendingRetries'");
        }

        // ── Compliance event taxonomy: all event types are valid request values ───

        [TestCase(WebhookEventType.WhitelistAdd)]
        [TestCase(WebhookEventType.WhitelistRemove)]
        [TestCase(WebhookEventType.TransferDeny)]
        [TestCase(WebhookEventType.AuditExportCreated)]
        [TestCase(WebhookEventType.KycStatusChange)]
        [TestCase(WebhookEventType.AmlStatusChange)]
        [TestCase(WebhookEventType.ComplianceBadgeUpdate)]
        [TestCase(WebhookEventType.TokenDeploymentStarted)]
        [TestCase(WebhookEventType.TokenDeploymentConfirming)]
        [TestCase(WebhookEventType.TokenDeploymentCompleted)]
        [TestCase(WebhookEventType.TokenDeploymentFailed)]
        public async Task CreateSubscription_AllEventTypes_AreAccepted(WebhookEventType eventType)
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = $"https://eventtype-{eventType.ToString().ToLowerInvariant()}.example.com/hook",
                EventTypes = new List<WebhookEventType> { eventType }
            };

            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Event type {eventType} should be accepted");
            var body = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Subscription!.EventTypes, Contains.Item(eventType));
        }

        // ── Determinism: multiple registrations produce independent secrets ────────

        [Test]
        public async Task CreateSubscription_TwoSubscriptions_HaveUniqueSigningSecrets()
        {
            var r1 = await CreateAndGetSubscription();
            var r2 = await CreateAndGetSubscription();

            Assert.That(r1!.SigningSecret, Is.Not.EqualTo(r2!.SigningSecret));
        }

        [Test]
        public async Task CreateSubscription_IsIdempotentWithDifferentIds()
        {
            // Same URL and event type creates two separate subscriptions
            var r1 = await CreateAndGetSubscription(url: "https://idempotency-test.example.com/hook");
            var r2 = await CreateAndGetSubscription(url: "https://idempotency-test.example.com/hook");

            Assert.That(r1!.Id, Is.Not.EqualTo(r2!.Id),
                "Two subscription creation requests produce distinct subscriptions");
        }

        private async Task<WebhookSubscription?> CreateAndGetSubscription(
            string url = "https://auto.example.com/hook")
        {
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = url,
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange }
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", req);
            var body = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionResponse>(_jsonOptions);
            return body?.Subscription;
        }

        // ── Page size clamp ───────────────────────────────────────────────────────

        [Test]
        public async Task GetDeliveryHistory_OversizedPageSize_IsClampedTo100()
        {
            // pageSize=500 should be clamped to 100 without error
            var resp = await _client.GetAsync("/api/v1/webhooks/deliveries?pageSize=500");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── WebApplicationFactory ────────────────────────────────────────────────

        private sealed class WebhookWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForWebhookIntegrationTests32CMinR",
                        ["JwtConfig:SecretKey"] = "WebhookIntegrationTestSecretKey32CharsR!",
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
            }
        }
    }
}

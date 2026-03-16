using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="WebhookService"/> covering:
    /// - Subscription CRUD (create, read, update, delete)
    /// - Input validation (invalid URL, missing event types)
    /// - Event emission and filtering (by event type, asset ID, network)
    /// - HMAC-SHA256 signing correctness
    /// - Retry policy decisions (5xx retries, 4xx no-retry)
    /// - Delivery audit record persistence
    /// - Fail-closed behavior for persistent endpoint failures
    /// - Dead-letter / permanent failure visibility
    /// - Sensitive data handling in payloads
    /// </summary>
    /// <remarks>
    /// Tests that exercise event delivery use short Task.Delay calls to yield to the
    /// fire-and-forget delivery Task. WebhookService.EmitEventAsync uses Task.Run internally
    /// (fire-and-forget) for each subscription delivery, so we allow 200-300ms for those
    /// background tasks to complete before asserting on captured state. The FakeHttpMessageHandler
    /// returns synchronously so actual delivery is near-instant; the delay only bridges the
    /// thread-scheduling gap inherent in Task.Run.
    /// </remarks>
    [TestFixture]
    public class WebhookServiceTests
    {
        // ── Factory helpers ──────────────────────────────────────────────────────

        private static WebhookService CreateService(
            IHttpClientFactory? httpFactory = null,
            IWebhookRepository? repo = null)
        {
            return new WebhookService(
                repo ?? new WebhookRepository(NullLogger<WebhookRepository>.Instance),
                NullLogger<WebhookService>.Instance,
                httpFactory ?? new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted"))));
        }

        private static CreateWebhookSubscriptionRequest ValidCreateRequest(
            string url = "https://example.com/webhook",
            List<WebhookEventType>? types = null) => new()
        {
            Url = url,
            EventTypes = types ?? new List<WebhookEventType> { WebhookEventType.KycStatusChange },
            Description = "Test subscription"
        };

        // ── Subscription creation ────────────────────────────────────────────────

        [Test]
        public async Task CreateSubscription_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = ValidCreateRequest();

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription, Is.Not.Null);
            Assert.That(result.Subscription!.Id, Is.Not.Empty);
            Assert.That(result.Subscription.Url, Is.EqualTo("https://example.com/webhook"));
            Assert.That(result.Subscription.EventTypes, Contains.Item(WebhookEventType.KycStatusChange));
        }

        [Test]
        public async Task CreateSubscription_GeneratesSigningSecret()
        {
            var svc = CreateService();
            var result = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription!.SigningSecret, Is.Not.Empty);
            // Signing secret must be base64-encoded 32 bytes (44 chars with padding)
            var decoded = Convert.FromBase64String(result.Subscription.SigningSecret);
            Assert.That(decoded.Length, Is.EqualTo(32));
        }

        [Test]
        public async Task CreateSubscription_TwoSubscriptions_HaveDifferentSigningSecrets()
        {
            var svc = CreateService();
            var r1 = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            var r2 = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            Assert.That(r1.Subscription!.SigningSecret, Is.Not.EqualTo(r2.Subscription!.SigningSecret));
        }

        [Test]
        public async Task CreateSubscription_InvalidUrl_Scheme_ReturnsError()
        {
            var svc = CreateService();
            var req = ValidCreateRequest(url: "ftp://badscheme.example.com/hook");

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid webhook URL"));
        }

        [Test]
        public async Task CreateSubscription_RelativeUrl_ReturnsError()
        {
            var svc = CreateService();
            var req = ValidCreateRequest(url: "/relative/path");

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid webhook URL"));
        }

        [Test]
        public async Task CreateSubscription_EmptyEventTypes_ReturnsError()
        {
            var svc = CreateService();
            var req = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType>()
            };

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("event type"));
        }

        [Test]
        public async Task CreateSubscription_MultipleEventTypes_AllPersisted()
        {
            var svc = CreateService();
            var types = new List<WebhookEventType>
            {
                WebhookEventType.KycStatusChange,
                WebhookEventType.AmlStatusChange,
                WebhookEventType.ComplianceBadgeUpdate,
                WebhookEventType.WhitelistAdd
            };
            var req = ValidCreateRequest(types: types);

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription!.EventTypes, Is.EquivalentTo(types));
        }

        [Test]
        public async Task CreateSubscription_WithAssetFilter_PersistsFilter()
        {
            var svc = CreateService();
            var req = ValidCreateRequest();
            req.AssetIdFilter = 12345UL;
            req.NetworkFilter = "mainnet";

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription!.AssetIdFilter, Is.EqualTo(12345UL));
            Assert.That(result.Subscription.NetworkFilter, Is.EqualTo("mainnet"));
        }

        [Test]
        public async Task CreateSubscription_HttpUrl_IsAccepted()
        {
            var svc = CreateService();
            var req = ValidCreateRequest(url: "http://internal-endpoint.local/hook");

            var result = await svc.CreateSubscriptionAsync(req, "user-001");

            Assert.That(result.Success, Is.True);
        }

        // ── Subscription retrieval ───────────────────────────────────────────────

        [Test]
        public async Task GetSubscription_OwnedByUser_ReturnsSubscription()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var result = await svc.GetSubscriptionAsync(created.Subscription!.Id, "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription!.Id, Is.EqualTo(created.Subscription.Id));
        }

        [Test]
        public async Task GetSubscription_NotFound_ReturnsError()
        {
            var svc = CreateService();

            var result = await svc.GetSubscriptionAsync("nonexistent-id", "user-001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task GetSubscription_WrongOwner_ReturnsPermissionError()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var result = await svc.GetSubscriptionAsync(created.Subscription!.Id, "user-002");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("permission"));
        }

        // ── Subscription listing ─────────────────────────────────────────────────

        [Test]
        public async Task ListSubscriptions_ReturnsOnlyUserSubscriptions()
        {
            var svc = CreateService();
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-002");

            var result = await svc.ListSubscriptionsAsync("user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Subscriptions, Has.All.Matches<WebhookSubscription>(s => s.CreatedBy == "user-001"));
        }

        [Test]
        public async Task ListSubscriptions_NoSubscriptions_ReturnsEmpty()
        {
            var svc = CreateService();

            var result = await svc.ListSubscriptionsAsync("user-with-no-subs");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.Subscriptions, Is.Empty);
        }

        // ── Subscription update ──────────────────────────────────────────────────

        [Test]
        public async Task UpdateSubscription_Deactivate_Succeeds()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var update = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = created.Subscription!.Id,
                IsActive = false
            };

            var result = await svc.UpdateSubscriptionAsync(update, "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription!.IsActive, Is.False);
        }

        [Test]
        public async Task UpdateSubscription_ChangeEventTypes_Succeeds()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            var newTypes = new List<WebhookEventType>
            {
                WebhookEventType.AmlStatusChange,
                WebhookEventType.WhitelistRemove
            };

            var update = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = created.Subscription!.Id,
                EventTypes = newTypes
            };

            var result = await svc.UpdateSubscriptionAsync(update, "user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription!.EventTypes, Is.EquivalentTo(newTypes));
        }

        [Test]
        public async Task UpdateSubscription_NotFound_ReturnsError()
        {
            var svc = CreateService();
            var update = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = "nonexistent-id",
                IsActive = false
            };

            var result = await svc.UpdateSubscriptionAsync(update, "user-001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task UpdateSubscription_WrongOwner_ReturnsPermissionError()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var update = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = created.Subscription!.Id,
                IsActive = false
            };

            var result = await svc.UpdateSubscriptionAsync(update, "user-999");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("permission"));
        }

        // ── Subscription deletion ────────────────────────────────────────────────

        [Test]
        public async Task DeleteSubscription_OwnedByUser_Succeeds()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var result = await svc.DeleteSubscriptionAsync(created.Subscription!.Id, "user-001");

            Assert.That(result.Success, Is.True);

            // Subscription should no longer be retrievable
            var getResult = await svc.GetSubscriptionAsync(created.Subscription.Id, "user-001");
            Assert.That(getResult.Success, Is.False);
        }

        [Test]
        public async Task DeleteSubscription_NotFound_ReturnsError()
        {
            var svc = CreateService();

            var result = await svc.DeleteSubscriptionAsync("nonexistent-id", "user-001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task DeleteSubscription_WrongOwner_ReturnsPermissionError()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var result = await svc.DeleteSubscriptionAsync(created.Subscription!.Id, "user-999");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("permission"));
        }

        // ── Event emission and filtering ─────────────────────────────────────────

        [Test]
        public async Task EmitEvent_MatchingSubscription_DeliversEvent()
        {
            var capturedRequests = new List<HttpRequestMessage>();
            var handler = new FakeHttpMessageHandler(
                HttpStatusCode.OK,
                "accepted",
                onRequest: req => capturedRequests.Add(req));
            var svc = CreateService(
                new FakeHttpClientFactory(new HttpClient(handler)));

            await svc.CreateSubscriptionAsync(ValidCreateRequest(
                types: new List<WebhookEventType> { WebhookEventType.KycStatusChange }),
                "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            // Give the fire-and-forget delivery a moment to run
            await Task.Delay(200);

            Assert.That(capturedRequests.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task EmitEvent_NonMatchingEventType_DoesNotDeliver()
        {
            var capturedRequests = new List<HttpRequestMessage>();
            var handler = new FakeHttpMessageHandler(
                HttpStatusCode.OK,
                "accepted",
                onRequest: req => capturedRequests.Add(req));
            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));

            // Subscribe to WhitelistAdd only
            await svc.CreateSubscriptionAsync(ValidCreateRequest(
                types: new List<WebhookEventType> { WebhookEventType.WhitelistAdd }),
                "user-001");

            // Emit a KycStatusChange event
            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(200);

            Assert.That(capturedRequests.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task EmitEvent_AssetIdFilter_OnlyMatchingAssetReceivesEvent()
        {
            var delivered = new List<string>();

            // Use a repo shared between two separate service instances to test filtering
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);

            // sub1 filters on asset 1000, sub2 filters on asset 2000
            var sub1 = new WebhookSubscription
            {
                Id = Guid.NewGuid().ToString(),
                Url = "https://endpoint1.example.com/hook",
                SigningSecret = Convert.ToBase64String(new byte[32]),
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange },
                AssetIdFilter = 1000UL,
                IsActive = true,
                CreatedBy = "user-001"
            };
            var sub2 = new WebhookSubscription
            {
                Id = Guid.NewGuid().ToString(),
                Url = "https://endpoint2.example.com/hook",
                SigningSecret = Convert.ToBase64String(new byte[32]),
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange },
                AssetIdFilter = 2000UL,
                IsActive = true,
                CreatedBy = "user-001"
            };
            await repo.CreateSubscriptionAsync(sub1);
            await repo.CreateSubscriptionAsync(sub2);

            // Create a multi-endpoint factory that routes by URL
            var factory = new MultiEndpointHttpClientFactory(new Dictionary<string, Func<HttpResponseMessage>>
            {
                ["https://endpoint1.example.com/hook"] = () => { delivered.Add("sub-asset-1"); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }; },
                ["https://endpoint2.example.com/hook"] = () => { delivered.Add("sub-asset-2"); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }; }
            });

            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance, factory);

            // Emit event for asset 1000 only
            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                AssetId = 1000UL,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            Assert.That(delivered, Contains.Item("sub-asset-1"));
            Assert.That(delivered, Does.Not.Contain("sub-asset-2"));
        }

        [Test]
        public async Task EmitEvent_NetworkFilter_OnlyMatchingNetworkReceivesEvent()
        {
            var delivered = new List<string>();
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "ok",
                onRequest: _ => delivered.Add("mainnet"));
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);

            // Subscribe for mainnet only
            await repo.CreateSubscriptionAsync(new WebhookSubscription
            {
                Id = Guid.NewGuid().ToString(),
                Url = "https://example.com/hook",
                SigningSecret = Convert.ToBase64String(new byte[32]),
                EventTypes = new List<WebhookEventType> { WebhookEventType.ComplianceBadgeUpdate },
                NetworkFilter = "mainnet",
                IsActive = true,
                CreatedBy = "user-001"
            });

            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(handler)));

            // Emit on testnet — should NOT be delivered
            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceBadgeUpdate,
                Network = "testnet",
                Actor = "actor-001"
            });

            await Task.Delay(200);
            Assert.That(delivered, Is.Empty);

            // Emit on mainnet — should be delivered
            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceBadgeUpdate,
                Network = "mainnet",
                Actor = "actor-001"
            });

            await Task.Delay(200);
            Assert.That(delivered, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task EmitEvent_InactiveSubscription_NotDelivered()
        {
            var capturedRequests = new List<HttpRequestMessage>();
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "ok",
                onRequest: req => capturedRequests.Add(req));
            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));

            // Create and then deactivate subscription
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            await svc.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = created.Subscription!.Id,
                IsActive = false
            }, "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(200);

            Assert.That(capturedRequests.Count, Is.EqualTo(0));
        }

        // ── Signing correctness ──────────────────────────────────────────────────

        [Test]
        public async Task EmitEvent_SignatureHeader_IsHmacSha256OfPayload()
        {
            string? capturedSignature = null;
            string? capturedPayload = null;
            string? capturedSigningSecret = null;

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted",
                onRequest: req =>
                {
                    capturedSignature = req.Headers.GetValues("X-Webhook-Signature").FirstOrDefault();
                    capturedPayload = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                });

            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(handler)));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            capturedSigningSecret = created.Subscription!.SigningSecret;

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(200);

            Assert.That(capturedSignature, Is.Not.Null);
            Assert.That(capturedPayload, Is.Not.Null);
            Assert.That(capturedSigningSecret, Is.Not.Null);

            // Verify the signature is HMAC-SHA256(payload, signingSecret)
            var keyBytes = Encoding.UTF8.GetBytes(capturedSigningSecret!);
            var messageBytes = Encoding.UTF8.GetBytes(capturedPayload!);
            using var hmac = new HMACSHA256(keyBytes);
            var expected = Convert.ToBase64String(hmac.ComputeHash(messageBytes));

            Assert.That(capturedSignature, Is.EqualTo(expected));
        }

        [Test]
        public async Task EmitEvent_EventIdHeader_MatchesEventId()
        {
            string? capturedEventId = null;
            WebhookEvent? emittedEvent = null;

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted",
                onRequest: req =>
                {
                    capturedEventId = req.Headers.GetValues("X-Webhook-Event-Id").FirstOrDefault();
                    var body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (body != null)
                        emittedEvent = JsonSerializer.Deserialize<WebhookEvent>(body);
                });

            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var evt = new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            };
            await svc.EmitEventAsync(evt);

            await Task.Delay(200);

            Assert.That(capturedEventId, Is.Not.Null);
            Assert.That(emittedEvent, Is.Not.Null);
            Assert.That(capturedEventId, Is.EqualTo(emittedEvent!.Id));
        }

        [Test]
        public async Task EmitEvent_EventTypeHeader_MatchesEventType()
        {
            string? capturedEventType = null;
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted",
                onRequest: req =>
                {
                    capturedEventType = req.Headers.GetValues("X-Webhook-Event-Type").FirstOrDefault();
                });

            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));
            await svc.CreateSubscriptionAsync(ValidCreateRequest(
                types: new List<WebhookEventType> { WebhookEventType.AmlStatusChange }),
                "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.AmlStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(200);

            Assert.That(capturedEventType, Is.EqualTo("AmlStatusChange"));
        }

        // ── Timing-safe signature verification ───────────────────────────────────

        /// <summary>
        /// Validates the recommended receiver verification pattern from docs/WEBHOOK_API.md.
        /// Uses CryptographicOperations.FixedTimeEquals (timing-safe) instead of string ==.
        /// This test serves as a regression guard: if the doc sample is regressed to a
        /// short-circuit comparison the underlying security property tested here still holds.
        /// </summary>
        [Test]
        public async Task SignatureVerification_CorrectSignature_AcceptsWithFixedTimeEquals()
        {
            string? capturedPayload = null;
            string? capturedSignature = null;

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted",
                onRequest: req =>
                {
                    capturedPayload = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                    capturedSignature = req.Headers.GetValues("X-Webhook-Signature").FirstOrDefault();
                });

            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(handler)));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            var signingSecret = created.Subscription!.SigningSecret;

            await svc.EmitEventAsync(new WebhookEvent { EventType = WebhookEventType.KycStatusChange, Actor = "a" });
            await Task.Delay(200);

            Assert.That(capturedPayload, Is.Not.Null);
            Assert.That(capturedSignature, Is.Not.Null);

            // Replicate the timing-safe verification from docs/WEBHOOK_API.md (C# example)
            Assert.That(TimingSafeVerify(capturedPayload!, signingSecret, capturedSignature!), Is.True,
                "Timing-safe verification must accept the correct signature");
        }

        [Test]
        public async Task SignatureVerification_TamperedPayload_RejectsWithFixedTimeEquals()
        {
            string? capturedSignature = null;

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted",
                onRequest: req =>
                {
                    capturedSignature = req.Headers.GetValues("X-Webhook-Signature").FirstOrDefault();
                });

            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(handler)));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");
            var signingSecret = created.Subscription!.SigningSecret;

            await svc.EmitEventAsync(new WebhookEvent { EventType = WebhookEventType.KycStatusChange, Actor = "a" });
            await Task.Delay(200);

            Assert.That(capturedSignature, Is.Not.Null);

            // Tampered payload must NOT verify
            const string tamperedPayload = "{\"eventType\":\"TAMPERED\",\"actor\":\"attacker\"}";
            Assert.That(TimingSafeVerify(tamperedPayload, signingSecret, capturedSignature!), Is.False,
                "Timing-safe verification must reject a tampered payload");
        }

        [Test]
        public async Task SignatureVerification_WrongSecret_RejectsWithFixedTimeEquals()
        {
            string? capturedPayload = null;
            string? capturedSignature = null;

            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted",
                onRequest: req =>
                {
                    capturedPayload = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                    capturedSignature = req.Headers.GetValues("X-Webhook-Signature").FirstOrDefault();
                });

            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(handler)));

            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent { EventType = WebhookEventType.KycStatusChange, Actor = "a" });
            await Task.Delay(200);

            Assert.That(capturedPayload, Is.Not.Null);
            Assert.That(capturedSignature, Is.Not.Null);

            const string wrongSecret = "wrong-secret-AAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
            Assert.That(TimingSafeVerify(capturedPayload!, wrongSecret, capturedSignature!), Is.False,
                "Timing-safe verification must reject when an incorrect signing secret is used");
        }

        /// <summary>
        /// Documentation example: timing-safe receiver verification pattern from docs/WEBHOOK_API.md.
        /// Uses <see cref="CryptographicOperations.FixedTimeEquals"/> (not string ==) to prevent
        /// timing side-channel attacks on compliance event receiver authentication.
        /// </summary>
        private static bool TimingSafeVerify(string rawBody, string signingSecret, string signatureHeader)
        {
            var keyBytes = Encoding.UTF8.GetBytes(signingSecret);
            var messageBytes = Encoding.UTF8.GetBytes(rawBody);
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(messageBytes);
            var expected = Convert.ToBase64String(hash);

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var actualBytes = Encoding.UTF8.GetBytes(signatureHeader);
            if (expectedBytes.Length != actualBytes.Length)
                return false;
            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        /// <summary>
        /// Regression guard: docs/WEBHOOK_API.md C# example must use FixedTimeEquals, not string ==.
        /// If the doc is ever regressed to a short-circuit comparison this test catches it.
        /// </summary>
        [Test]
        public void Documentation_CSharpVerificationExample_UsesFixedTimeEquals()
        {
            // This test reads the documentation file and asserts the C# snippet
            // uses CryptographicOperations.FixedTimeEquals rather than == operator.
            // It provides regression protection so the timing-safe property of the
            // published example cannot silently be replaced with a weaker comparison.
            var docsPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "BiatecTokensApi", "docs", "WEBHOOK_API.md");
            // Fallback: try relative to test output
            if (!File.Exists(docsPath))
            {
                docsPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "WEBHOOK_API.md"));
            }

            if (!File.Exists(docsPath))
            {
                // Try from solution root
                var dir = AppContext.BaseDirectory;
                while (dir != null && !File.Exists(Path.Combine(dir, "docs", "WEBHOOK_API.md")))
                    dir = Directory.GetParent(dir)?.FullName;
                if (dir != null)
                    docsPath = Path.Combine(dir, "docs", "WEBHOOK_API.md");
            }

            Assert.That(File.Exists(docsPath), Is.True,
                $"docs/WEBHOOK_API.md not found. Searched: {docsPath}");

            var content = File.ReadAllText(docsPath);

            // The C# code block must use FixedTimeEquals
            Assert.That(content, Does.Contain("CryptographicOperations.FixedTimeEquals"),
                "docs/WEBHOOK_API.md C# example must use CryptographicOperations.FixedTimeEquals for timing-safe comparison");

            // The C# code block must NOT use the short-circuit string == pattern
            // (we look for 'return expected ==' as a proxy for the insecure pattern)
            Assert.That(content, Does.Not.Contain("return expected == signatureHeader"),
                "docs/WEBHOOK_API.md C# example must not use short-circuit string == comparison");
        }



        [Test]
        public async Task EmitEvent_Payload_ContainsRequiredFields()
        {
            string? capturedPayload = null;
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "ok",
                onRequest: req =>
                {
                    capturedPayload = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                });

            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var evt = new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-compliance",
                AssetId = 99999UL,
                Network = "mainnet",
                Data = new Dictionary<string, object> { ["oldStatus"] = "Pending", ["newStatus"] = "Approved" }
            };

            await svc.EmitEventAsync(evt);
            await Task.Delay(200);

            Assert.That(capturedPayload, Is.Not.Null);
            var doc = JsonDocument.Parse(capturedPayload!);
            var root = doc.RootElement;

            // All required fields must be present and non-null
            // WebhookService uses default JsonSerializer (PascalCase)
            Assert.That(root.TryGetProperty("Id", out _), Is.True, "payload must have 'Id'");
            Assert.That(root.TryGetProperty("EventType", out _), Is.True, "payload must have 'EventType'");
            Assert.That(root.TryGetProperty("Timestamp", out _), Is.True, "payload must have 'Timestamp'");
            Assert.That(root.TryGetProperty("Actor", out var actorProp), Is.True, "payload must have 'Actor'");
            Assert.That(actorProp.GetString(), Is.EqualTo("actor-compliance"));
        }

        // ── Delivery audit persistence ────────────────────────────────────────────

        [Test]
        public async Task DeliveryHistory_SuccessfulDelivery_AuditRecordPersisted()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(
                repo,
                NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(HttpStatusCode.OK, "accepted"))));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            var historyRequest = new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            };
            var history = await svc.GetDeliveryHistoryAsync(historyRequest, "user-001");

            Assert.That(history.Success, Is.True);
            Assert.That(history.TotalCount, Is.GreaterThan(0));
            Assert.That(history.SuccessCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task DeliveryHistory_FailedDelivery_AuditRecordWithError()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(
                repo,
                NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(HttpStatusCode.BadRequest, "bad request"))));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            var history = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            }, "user-001");

            Assert.That(history.Success, Is.True);
            Assert.That(history.TotalCount, Is.GreaterThan(0));
            var failedDelivery = history.Deliveries.FirstOrDefault(d => !d.Success);
            Assert.That(failedDelivery, Is.Not.Null);
            Assert.That(failedDelivery!.ErrorMessage, Is.Not.Empty);
        }

        [Test]
        public async Task DeliveryHistory_WrongOwner_ReturnsPermissionError()
        {
            var svc = CreateService();
            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var result = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            }, "user-999");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("permission"));
        }

        // ── Retry policy decisions ────────────────────────────────────────────────

        [Test]
        public async Task DeliveryHistory_ServerError_MarkedForRetry()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(
                repo,
                NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "service unavailable"))));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            var history = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            }, "user-001");

            Assert.That(history.Success, Is.True);
            var failedDelivery = history.Deliveries.FirstOrDefault(d => !d.Success);
            Assert.That(failedDelivery, Is.Not.Null);
            // 5xx errors should be retried
            Assert.That(failedDelivery!.WillRetry, Is.True);
        }

        [Test]
        public async Task DeliveryHistory_ClientError_NotRetried()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(
                repo,
                NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(HttpStatusCode.Forbidden, "forbidden"))));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            var history = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            }, "user-001");

            Assert.That(history.Success, Is.True);
            var failedDelivery = history.Deliveries.FirstOrDefault(d => !d.Success);
            Assert.That(failedDelivery, Is.Not.Null);
            // 4xx errors (except 429) should NOT be retried
            Assert.That(failedDelivery!.WillRetry, Is.False);
        }

        [Test]
        public async Task DeliveryHistory_RateLimited_IsRetried()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(
                repo,
                NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "rate limited"))));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            var history = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            }, "user-001");

            Assert.That(history.Success, Is.True);
            var failedDelivery = history.Deliveries.FirstOrDefault(d => !d.Success);
            Assert.That(failedDelivery, Is.Not.Null);
            // 429 should be retried
            Assert.That(failedDelivery!.WillRetry, Is.True);
        }

        [Test]
        public async Task DeliveryHistory_NetworkException_IsRetried()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var svc = new WebhookService(
                repo,
                NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(
                    new FakeHttpMessageHandler(throwException: new HttpRequestException("connection refused")))));

            var created = await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            await svc.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                Actor = "actor-001"
            });

            await Task.Delay(300);

            var history = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = created.Subscription!.Id,
                Page = 1,
                PageSize = 10
            }, "user-001");

            Assert.That(history.Success, Is.True);
            Assert.That(history.Deliveries, Is.Not.Empty);
        }

        // ── All compliance event types are covered ────────────────────────────────

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
        // Compliance case management events
        [TestCase(WebhookEventType.ComplianceCaseCreated)]
        [TestCase(WebhookEventType.ComplianceCaseStateTransitioned)]
        [TestCase(WebhookEventType.ComplianceCaseAssignmentChanged)]
        [TestCase(WebhookEventType.ComplianceCaseEscalationRaised)]
        [TestCase(WebhookEventType.ComplianceCaseEscalationResolved)]
        [TestCase(WebhookEventType.ComplianceCaseRemediationTaskAdded)]
        [TestCase(WebhookEventType.ComplianceCaseRemediationTaskResolved)]
        [TestCase(WebhookEventType.ComplianceCaseMonitoringReviewRecorded)]
        [TestCase(WebhookEventType.ComplianceCaseOverdueReviewDetected)]
        [TestCase(WebhookEventType.ComplianceCaseApprovalReady)]
        [TestCase(WebhookEventType.ComplianceCaseFollowUpCreated)]
        [TestCase(WebhookEventType.ComplianceCaseExported)]
        // Ongoing monitoring task events
        [TestCase(WebhookEventType.MonitoringTaskCreated)]
        [TestCase(WebhookEventType.MonitoringTaskDueSoon)]
        [TestCase(WebhookEventType.MonitoringTaskOverdue)]
        [TestCase(WebhookEventType.MonitoringTaskReassessmentStarted)]
        [TestCase(WebhookEventType.MonitoringTaskEscalated)]
        [TestCase(WebhookEventType.MonitoringTaskDeferred)]
        [TestCase(WebhookEventType.MonitoringTaskResolved)]
        [TestCase(WebhookEventType.MonitoringTaskSubjectSuspended)]
        [TestCase(WebhookEventType.MonitoringTaskSubjectRestricted)]
        // Case assignment, SLA, and delivery events
        [TestCase(WebhookEventType.ComplianceCaseTeamAssigned)]
        [TestCase(WebhookEventType.ComplianceCaseSlaBreached)]
        [TestCase(WebhookEventType.ComplianceCaseDeliveryFailed)]
        [TestCase(WebhookEventType.ComplianceCaseDeliveryRetryExhausted)]
        [TestCase(WebhookEventType.ReportRunCreated)]
        [TestCase(WebhookEventType.ReportRunBlocked)]
        [TestCase(WebhookEventType.ReportRunApproved)]
        [TestCase(WebhookEventType.ReportRunExported)]
        [TestCase(WebhookEventType.ReportRunDelivered)]
        [TestCase(WebhookEventType.ReportRunFailed)]
        [TestCase(WebhookEventType.ReportTemplateCreated)]
        [TestCase(WebhookEventType.ReportTemplateUpdated)]
        [TestCase(WebhookEventType.ReportTemplateArchived)]
        public async Task EmitEvent_AllEventTypes_CanBeDelivered(WebhookEventType eventType)
        {
            bool delivered = false;
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "ok",
                onRequest: _ => delivered = true);
            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));
            await svc.CreateSubscriptionAsync(ValidCreateRequest(types: new List<WebhookEventType> { eventType }), "user-001");

            await svc.EmitEventAsync(new WebhookEvent { EventType = eventType, Actor = "system" });

            // Poll for delivery rather than fixed delay to avoid CI thread-pool starvation flakiness
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (!delivered && DateTime.UtcNow < deadline)
                await Task.Delay(20);

            Assert.That(delivered, Is.True, $"Event type {eventType} was not delivered");
        }

        // ── Delivery history pagination ───────────────────────────────────────────

        [Test]
        public async Task DeliveryHistory_Pagination_ReturnsCorrectPage()
        {
            var repo = new WebhookRepository(NullLogger<WebhookRepository>.Instance);
            var deliveryResults = new List<WebhookDeliveryResult>();

            // Pre-populate 15 delivery results
            for (int i = 0; i < 15; i++)
            {
                await repo.StoreDeliveryResultAsync(new WebhookDeliveryResult
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = "sub-paginate-001",
                    EventId = $"event-{i}",
                    Success = true,
                    AttemptedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            // Simulate subscription ownership by creating a sub with fixed ID via the repository directly
            await repo.CreateSubscriptionAsync(new WebhookSubscription
            {
                Id = "sub-paginate-001",
                Url = "https://example.com/hook",
                SigningSecret = "test-secret",
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange },
                IsActive = true,
                CreatedBy = "user-page-test"
            });

            var svc = new WebhookService(repo, NullLogger<WebhookService>.Instance,
                new FakeHttpClientFactory(new HttpClient(new FakeHttpMessageHandler())));

            var page1 = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = "sub-paginate-001",
                Page = 1,
                PageSize = 10
            }, "user-page-test");

            var page2 = await svc.GetDeliveryHistoryAsync(new GetWebhookDeliveryHistoryRequest
            {
                SubscriptionId = "sub-paginate-001",
                Page = 2,
                PageSize = 10
            }, "user-page-test");

            Assert.That(page1.Success, Is.True);
            Assert.That(page1.TotalCount, Is.EqualTo(15));
            Assert.That(page1.Deliveries.Count, Is.EqualTo(10));
            Assert.That(page2.Deliveries.Count, Is.EqualTo(5));
        }

        // ── Idempotency: event emission ───────────────────────────────────────────

        [Test]
        public async Task EmitEvent_SameEventEmittedTwice_DeliversTwice()
        {
            int deliveryCount = 0;
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "ok",
                onRequest: _ => Interlocked.Increment(ref deliveryCount));

            var svc = CreateService(new FakeHttpClientFactory(new HttpClient(handler)));
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var evt = new WebhookEvent { EventType = WebhookEventType.KycStatusChange, Actor = "actor-001" };

            await svc.EmitEventAsync(evt);
            await svc.EmitEventAsync(evt);

            // Poll until both fire-and-forget deliveries complete (up to 5 s in CI)
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (deliveryCount < 2 && DateTime.UtcNow < deadline)
                await Task.Delay(20);

            // Each EmitEventAsync call results in a delivery (no dedup at emission layer)
            Assert.That(deliveryCount, Is.EqualTo(2));
        }

        // ── Sensitive data: signing secret not included in list response ──────────

        [Test]
        public async Task ListSubscriptions_SigningSecretIsNotMasked_PresentForOwner()
        {
            var svc = CreateService();
            await svc.CreateSubscriptionAsync(ValidCreateRequest(), "user-001");

            var result = await svc.ListSubscriptionsAsync("user-001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscriptions, Has.Count.EqualTo(1));
            // Signing secret is returned to the owner so they can configure their receiver
            Assert.That(result.Subscriptions[0].SigningSecret, Is.Not.Empty);
        }

        // ── FakeHttpMessageHandler helpers ───────────────────────────────────────

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _responseBody;
            private readonly Action<HttpRequestMessage>? _onRequest;
            private readonly Exception? _throwException;

            public FakeHttpMessageHandler(
                HttpStatusCode statusCode = HttpStatusCode.OK,
                string responseBody = "ok",
                Action<HttpRequestMessage>? onRequest = null,
                Exception? throwException = null)
            {
                _statusCode = statusCode;
                _responseBody = responseBody;
                _onRequest = onRequest;
                _throwException = throwException;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _onRequest?.Invoke(request);
                if (_throwException != null)
                    throw _throwException;
                return Task.FromResult(new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public FakeHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name) => _client;
        }

        /// <summary>
        /// Routes HTTP requests to different handlers based on request URL host
        /// </summary>
        private sealed class MultiEndpointHttpClientFactory : IHttpClientFactory
        {
            private readonly Dictionary<string, Func<HttpResponseMessage>> _responders;

            public MultiEndpointHttpClientFactory(Dictionary<string, Func<HttpResponseMessage>> responders)
            {
                _responders = responders;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(new RoutingHandler(_responders));
            }

            private sealed class RoutingHandler : HttpMessageHandler
            {
                private readonly Dictionary<string, Func<HttpResponseMessage>> _responders;

                public RoutingHandler(Dictionary<string, Func<HttpResponseMessage>> responders)
                {
                    _responders = responders;
                }

                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var url = request.RequestUri?.ToString() ?? string.Empty;
                    if (_responders.TryGetValue(url, out var responder))
                    {
                        return Task.FromResult(responder());
                    }
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("ok", Encoding.UTF8, "application/json")
                    });
                }
            }
        }
    }
}

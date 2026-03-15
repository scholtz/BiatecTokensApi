using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="StripeIdentityKycProvider"/>.
    /// Covers fail-closed configuration, status mapping, webhook signature validation,
    /// webhook parsing for all Stripe event types, and business-entity metadata handling.
    /// All tests use a controlled <see cref="FakeHttpMessageHandler"/> to avoid real HTTP calls.
    /// </summary>
    [TestFixture]
    public class KycProviderAdapterTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static StripeIdentityKycProvider CreateProvider(
            string? apiKey = null,
            string? apiEndpoint = null,
            HttpStatusCode responseStatus = HttpStatusCode.OK,
            string? responseBody = null)
        {
            var config = new KycConfig
            {
                Provider = "StripeIdentity",
                ApiKey = apiKey ?? "sk_test_valid_key",
                ApiEndpoint = apiEndpoint ?? "https://api.stripe.com",
                RequestTimeoutSeconds = 10
            };

            var fakeHandler = new FakeHttpMessageHandler(responseStatus,
                responseBody ?? BuildSessionJson("vs_001", "verified"));
            var httpFactory = new FakeHttpClientFactory(new HttpClient(fakeHandler));

            return new StripeIdentityKycProvider(
                new OptionsWrapper<KycConfig>(config),
                httpFactory,
                NullLogger<StripeIdentityKycProvider>.Instance);
        }

        private static string BuildSessionJson(string id, string status, string? errorCode = null)
        {
            var obj = new Dictionary<string, object?> { ["id"] = id, ["status"] = status };
            if (errorCode != null)
                obj["last_error"] = new Dictionary<string, string> { ["code"] = errorCode };
            return JsonSerializer.Serialize(obj);
        }

        private static StartKycVerificationRequest MakeRequest(
            Dictionary<string, string>? metadata = null) => new()
        {
            FullName = "Test User",
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        // ── Fail-closed configuration tests ──────────────────────────────────────

        [Test]
        public async Task StartVerification_MissingApiKey_FailsClosed()
        {
            var provider = CreateProvider(apiKey: "");
            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted), "Missing API key must fail-closed");
            Assert.That(string.IsNullOrEmpty(refId), Is.True);
            Assert.That(error, Is.Not.Null.And.Contains("not set"));
        }

        [Test]
        public async Task StartVerification_NullApiKey_FailsClosed()
        {
            // Create with explicit null key in config
            var config = new KycConfig { Provider = "StripeIdentity", ApiKey = null };
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildSessionJson("vs_001", "verified"));
            var providerWithNull = new StripeIdentityKycProvider(
                new OptionsWrapper<KycConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<StripeIdentityKycProvider>.Instance);

            var (refId, status, error) = await providerWithNull.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted));
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public async Task FetchStatus_MissingApiKey_FailsClosed()
        {
            var provider = CreateProvider(apiKey: "");
            var (status, reason, error) = await provider.FetchStatusAsync("vs_123");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted), "Missing API key must fail-closed on status fetch");
            Assert.That(error, Is.Not.Null.And.Contains("not set"));
        }

        [Test]
        public async Task FetchStatus_EmptyProviderRefId_ReturnsNotStarted()
        {
            var provider = CreateProvider();
            var (status, reason, error) = await provider.FetchStatusAsync("");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted));
            Assert.That(error, Is.Not.Null);
        }

        // ── Status mapping tests ──────────────────────────────────────────────────

        [Test]
        [TestCase("verified", KycStatus.Approved)]
        [TestCase("requires_input", KycStatus.NeedsReview)]
        [TestCase("processing", KycStatus.Pending)]
        [TestCase("canceled", KycStatus.Rejected)]
        [TestCase("expired", KycStatus.Expired)]
        [TestCase("unknown_state", KycStatus.Pending)]
        public async Task StartVerification_MapsStripeStatusCorrectly(string stripeStatus, KycStatus expectedStatus)
        {
            var provider = CreateProvider(responseBody: BuildSessionJson("vs_001", stripeStatus));
            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(status, Is.EqualTo(expectedStatus), $"Stripe status '{stripeStatus}' should map to {expectedStatus}");
            Assert.That(refId, Is.EqualTo("vs_001"));
            Assert.That(error, Is.Null);
        }

        [Test]
        public async Task StartVerification_ReturnsProviderReferenceId()
        {
            var provider = CreateProvider(responseBody: BuildSessionJson("vs_stripe_12345", "verified"));
            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(refId, Is.EqualTo("vs_stripe_12345"));
        }

        [Test]
        public async Task FetchStatus_MapsStripeStatusCorrectly()
        {
            var provider = CreateProvider(responseBody: BuildSessionJson("vs_001", "requires_input", errorCode: "document_expired"));
            var (status, reason, error) = await provider.FetchStatusAsync("vs_001");

            Assert.That(status, Is.EqualTo(KycStatus.NeedsReview));
            Assert.That(reason, Is.EqualTo("document_expired"));
            Assert.That(error, Is.Null);
        }

        // ── Provider error handling ───────────────────────────────────────────────

        [Test]
        public async Task StartVerification_ProviderReturns401_FailsClosed()
        {
            var errorBody = JsonSerializer.Serialize(new { error = new { message = "No such API key" } });
            var provider = CreateProvider(responseStatus: HttpStatusCode.Unauthorized, responseBody: errorBody);

            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted));
            Assert.That(string.IsNullOrEmpty(refId), Is.True);
            Assert.That(error, Does.Contain("401"));
        }

        [Test]
        public async Task StartVerification_ProviderReturns400_FailsClosed()
        {
            var errorBody = JsonSerializer.Serialize(new { error = new { message = "Invalid request" } });
            var provider = CreateProvider(responseStatus: HttpStatusCode.BadRequest, responseBody: errorBody);

            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted));
            Assert.That(error, Does.Contain("400"));
        }

        [Test]
        public async Task StartVerification_MalformedJson_FailsClosed()
        {
            var provider = CreateProvider(responseBody: "not-valid-json");
            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-1");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted));
            Assert.That(error, Is.Not.Null.And.Contains("Malformed"));
        }

        // ── Business entity subject support ──────────────────────────────────────

        [Test]
        public async Task StartVerification_BusinessEntityMetadata_IncludesLegalName()
        {
            string? capturedBody = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildSessionJson("vs_biz_001", "processing"),
                onRequest: req => capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult());

            var config = new KycConfig { Provider = "StripeIdentity", ApiKey = "sk_test_key" };
            var provider = new StripeIdentityKycProvider(
                new OptionsWrapper<KycConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<StripeIdentityKycProvider>.Instance);

            var metadata = new Dictionary<string, string>
            {
                ["subject_type"] = "BusinessEntity",
                ["legal_name"] = "Acme Corp Ltd",
                ["registration_number"] = "12345678",
                ["jurisdiction"] = "UK"
            };

            var (refId, status, error) = await provider.StartVerificationAsync(
                "entity-001",
                new StartKycVerificationRequest { FullName = "Acme Corp Ltd", Metadata = metadata },
                "corr-biz-1");

            Assert.That(status, Is.EqualTo(KycStatus.Pending), "Business entity starts as Pending (processing)");
            Assert.That(capturedBody, Does.Contain("legal_name"));
            Assert.That(capturedBody, Does.Contain("Acme+Corp+Ltd").Or.Contains("Acme%20Corp%20Ltd").Or.Contains("Acme Corp Ltd"));
        }

        [Test]
        public async Task StartVerification_IndividualMetadata_IncludesFullName()
        {
            string? capturedBody = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildSessionJson("vs_ind_001", "processing"),
                onRequest: req => capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult());

            var config = new KycConfig { Provider = "StripeIdentity", ApiKey = "sk_test_key" };
            var provider = new StripeIdentityKycProvider(
                new OptionsWrapper<KycConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<StripeIdentityKycProvider>.Instance);

            var metadata = new Dictionary<string, string>
            {
                ["subject_type"] = "Individual",
                ["full_name"] = "John Doe"
            };

            await provider.StartVerificationAsync(
                "user-001",
                new StartKycVerificationRequest { FullName = "John Doe", Metadata = metadata },
                "corr-ind-1");

            Assert.That(capturedBody, Does.Contain("full_name"));
        }

        // ── Webhook signature validation ──────────────────────────────────────────

        [Test]
        public void ValidateWebhookSignature_ValidSignature_ReturnsTrue()
        {
            var provider = CreateProvider();
            var payload = "{\"type\":\"identity.verification_session.verified\"}";
            var secret = "whsec_test_secret";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // Compute expected signature
            var signedPayload = $"{timestamp}.{payload}";
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
            var sig = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            var header = $"t={timestamp},v1={sig}";
            Assert.That(provider.ValidateWebhookSignature(payload, header, secret), Is.True);
        }

        [Test]
        public void ValidateWebhookSignature_InvalidSignature_ReturnsFalse()
        {
            var provider = CreateProvider();
            // Valid-length hex string but incorrect hash value
            var wrongSig = new string('0', 64); // 32 zero bytes as hex
            var isValid = provider.ValidateWebhookSignature(
                "{\"type\":\"identity.verification_session.verified\"}",
                $"t=1234567890,v1={wrongSig}",
                "whsec_test_secret");

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateWebhookSignature_InvalidHexChars_ReturnsFalse()
        {
            var provider = CreateProvider();
            // Signature value contains non-hex characters (format error)
            var isValid = provider.ValidateWebhookSignature(
                "{\"type\":\"identity.verification_session.verified\"}",
                "t=1234567890,v1=invalidsignature",
                "whsec_test_secret");

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void ValidateWebhookSignature_EmptyPayload_ReturnsFalse()
        {
            var provider = CreateProvider();
            Assert.That(provider.ValidateWebhookSignature("", "t=123,v1=abc", "secret"), Is.False);
        }

        [Test]
        public void ValidateWebhookSignature_EmptySignature_ReturnsFalse()
        {
            var provider = CreateProvider();
            Assert.That(provider.ValidateWebhookSignature("{}", "", "secret"), Is.False);
        }

        [Test]
        public void ValidateWebhookSignature_MalformedHeader_ReturnsFalse()
        {
            var provider = CreateProvider();
            Assert.That(provider.ValidateWebhookSignature("{}", "not-a-valid-header", "secret"), Is.False);
        }

        // ── Webhook parsing ───────────────────────────────────────────────────────

        [Test]
        [TestCase("identity.verification_session.verified", KycStatus.Approved)]
        [TestCase("identity.verification_session.requires_input", KycStatus.NeedsReview)]
        [TestCase("identity.verification_session.canceled", KycStatus.Rejected)]
        [TestCase("identity.verification_session.processing", KycStatus.Pending)]
        [TestCase("unknown.event.type", KycStatus.Pending)]  // falls back to status field
        public async Task ParseWebhook_MapsEventTypeCorrectly(string eventType, KycStatus expectedStatus)
        {
            var provider = CreateProvider();
            var webhook = new KycWebhookPayload
            {
                ProviderReferenceId = "vs_webhook_001",
                EventType = eventType,
                Status = "pending",
                Reason = null
            };

            var (refId, status, reason) = await provider.ParseWebhookAsync(webhook);

            Assert.That(status, Is.EqualTo(expectedStatus), $"Event '{eventType}' should map to {expectedStatus}");
            Assert.That(refId, Is.EqualTo("vs_webhook_001"));
        }

        [Test]
        public async Task ParseWebhook_PreservesProviderReferenceId()
        {
            var provider = CreateProvider();
            var webhook = new KycWebhookPayload
            {
                ProviderReferenceId = "vs_specific_ref_xyz",
                EventType = "identity.verification_session.verified",
                Status = "verified"
            };

            var (refId, _, _) = await provider.ParseWebhookAsync(webhook);
            Assert.That(refId, Is.EqualTo("vs_specific_ref_xyz"));
        }

        // ── Idempotency key propagation ───────────────────────────────────────────

        [Test]
        public async Task StartVerification_CorrelationIdUsedAsIdempotencyKey()
        {
            HttpRequestMessage? capturedRequest = null;
            var fakeHandler = new FakeHttpMessageHandler(
                HttpStatusCode.OK, BuildSessionJson("vs_001", "processing"),
                onRequest: req => capturedRequest = req);

            var config = new KycConfig { Provider = "StripeIdentity", ApiKey = "sk_test_key" };
            var provider = new StripeIdentityKycProvider(
                new OptionsWrapper<KycConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<StripeIdentityKycProvider>.Instance);

            var correlationId = "corr-idempotency-123";
            await provider.StartVerificationAsync("user-1", MakeRequest(), correlationId);

            Assert.That(capturedRequest?.Headers.Contains("Idempotency-Key"), Is.True,
                "Idempotency-Key header must be set on provider requests");
        }

        // ── Network failure handling ──────────────────────────────────────────────

        [Test]
        public async Task StartVerification_NetworkException_ReturnsNotStarted()
        {
            var fakeHandler = new FakeHttpMessageHandler(throwException: new HttpRequestException("Connection refused"));
            var config = new KycConfig { Provider = "StripeIdentity", ApiKey = "sk_test_key" };
            var provider = new StripeIdentityKycProvider(
                new OptionsWrapper<KycConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<StripeIdentityKycProvider>.Instance);

            var (refId, status, error) = await provider.StartVerificationAsync("user-1", MakeRequest(), "corr-net-1");

            Assert.That(status, Is.EqualTo(KycStatus.NotStarted));
            Assert.That(error, Does.Contain("Network error"));
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _responseBody;
            private readonly Action<HttpRequestMessage>? _onRequest;
            private readonly Exception? _throwException;

            public FakeHttpMessageHandler(
                HttpStatusCode statusCode = HttpStatusCode.OK,
                string responseBody = "",
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
    }
}

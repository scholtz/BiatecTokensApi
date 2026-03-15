using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.ComplianceOrchestration;
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
    /// Unit tests for <see cref="ComplyAdvantageAmlProvider"/>.
    /// Covers fail-closed configuration, sanctions/PEP/adverse-media decision mapping,
    /// business-entity subject screening, transient vs terminal error classification,
    /// and screening status polling.
    /// All tests use a controlled <see cref="FakeHttpMessageHandler"/> to avoid real HTTP calls.
    /// </summary>
    [TestFixture]
    public class AmlProviderAdapterTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ComplyAdvantageAmlProvider CreateProvider(
            string? apiKey = null,
            string? apiEndpoint = null,
            HttpStatusCode responseStatus = HttpStatusCode.OK,
            string? responseBody = null,
            bool includePep = true,
            bool includeAdverseMedia = false)
        {
            var config = new AmlConfig
            {
                Provider = "ComplyAdvantage",
                ApiKey = apiKey ?? "ca_test_api_key",
                ApiEndpoint = apiEndpoint ?? "https://api.complyadvantage.com",
                RequestTimeoutSeconds = 10,
                IncludePepScreening = includePep,
                IncludeAdverseMedia = includeAdverseMedia,
                MinApprovalConfidence = 0.8m
            };

            var fakeHandler = new FakeHttpMessageHandler(responseStatus,
                responseBody ?? BuildCleanSearchJson("search_001"));
            var httpFactory = new FakeHttpClientFactory(new HttpClient(fakeHandler));

            return new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                httpFactory,
                NullLogger<ComplyAdvantageAmlProvider>.Instance);
        }

        /// <summary>Builds a CA response with no hits (clean).</summary>
        private static string BuildCleanSearchJson(string id)
            => WrapInEnvelope(id, 0, new List<object>());

        /// <summary>Builds a CA response with a sanctions hit.</summary>
        private static string BuildSanctionsHitJson(string id, string sanctionType = "sanction")
            => WrapInEnvelope(id, 1, new List<object>
            {
                new { doc = new { entity_type = "person", types = new[] { sanctionType }, name = "John Doe" } }
            });

        /// <summary>Builds a CA response with a PEP hit.</summary>
        private static string BuildPepHitJson(string id)
            => WrapInEnvelope(id, 1, new List<object>
            {
                new { doc = new { entity_type = "person", types = new[] { "pep" }, name = "Jane Political" } }
            });

        /// <summary>Builds a CA response with an adverse media hit.</summary>
        private static string BuildAdverseMediaHitJson(string id)
            => WrapInEnvelope(id, 1, new List<object>
            {
                new { doc = new { entity_type = "person", types = new[] { "adverse-media" }, name = "Bad Actor" } }
            });

        /// <summary>Builds a CA response with multiple hit types.</summary>
        private static string BuildMultiHitJson(string id, string[] types)
            => WrapInEnvelope(id, types.Length, types.Select(t =>
                (object)new { doc = new { entity_type = "person", types = new[] { t }, name = "Subject" } }).ToList());

        private static string WrapInEnvelope(string id, int totalHits, List<object> hits)
        {
            var data = new
            {
                id = id,
                search_term = "Test Subject",
                total_hits = totalHits,
                hits = hits
            };
            var envelope = new
            {
                content = new { data }
            };
            return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        // ── Fail-closed configuration tests ──────────────────────────────────────

        [Test]
        public async Task ScreenSubject_MissingApiKey_FailsClosedWithProviderUnavailable()
        {
            var provider = CreateProvider(apiKey: "");
            var (refId, state, reasonCode, error) = await provider.ScreenSubjectAsync(
                "subj-1", new Dictionary<string, string>(), "corr-1");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable),
                "Missing API key must fail-closed with ProviderUnavailable, not silently approve");
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_NOT_CONFIGURED"));
            Assert.That(error, Is.Not.Null.And.Contains("not set"));
        }

        [Test]
        public async Task ScreenSubject_NullApiKey_FailsClosedWithProviderUnavailable()
        {
            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = null };
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildCleanSearchJson("s1"));
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync("subj-1", new(), "corr-1");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_NOT_CONFIGURED"));
        }

        [Test]
        public async Task GetScreeningStatus_MissingApiKey_FailsClosed()
        {
            var provider = CreateProvider(apiKey: "");
            var (state, reasonCode, error) = await provider.GetScreeningStatusAsync("search_001");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_NOT_CONFIGURED"));
        }

        [Test]
        public async Task GetScreeningStatus_EmptyRefId_ReturnsError()
        {
            var provider = CreateProvider();
            var (state, reasonCode, error) = await provider.GetScreeningStatusAsync("");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("MISSING_REFERENCE_ID"));
        }

        // ── Provider name ─────────────────────────────────────────────────────────

        [Test]
        public void ProviderName_IsComplyAdvantage()
        {
            var provider = CreateProvider();
            Assert.That(provider.ProviderName, Is.EqualTo("ComplyAdvantage"));
        }

        // ── Decision state mapping ────────────────────────────────────────────────

        [Test]
        public async Task ScreenSubject_NoHits_ReturnsApproved()
        {
            var provider = CreateProvider(responseBody: BuildCleanSearchJson("s_clean_001"));
            var (refId, state, reasonCode, error) = await provider.ScreenSubjectAsync(
                "clean-subject", new Dictionary<string, string>(), "corr-clean");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(reasonCode, Is.Null);
            Assert.That(error, Is.Null);
            Assert.That(refId, Is.EqualTo("s_clean_001"));
        }

        [Test]
        public async Task ScreenSubject_SanctionsHit_ReturnsRejected()
        {
            var provider = CreateProvider(responseBody: BuildSanctionsHitJson("s_sanction_001"));
            var (refId, state, reasonCode, error) = await provider.ScreenSubjectAsync(
                "sanctioned-subject", new Dictionary<string, string>(), "corr-sanction");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
            Assert.That(error, Is.Null);
        }

        [Test]
        public async Task ScreenSubject_OfacSanctionsHit_ReturnsRejected()
        {
            var provider = CreateProvider(responseBody: BuildSanctionsHitJson("s_ofac_001", "sanction-ofac-sdn"));
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "ofac-subject", new Dictionary<string, string>(), "corr-ofac");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task ScreenSubject_PepHit_ReturnsNeedsReview()
        {
            var provider = CreateProvider(responseBody: BuildPepHitJson("s_pep_001"), includePep: true);
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "pep-subject", new Dictionary<string, string>(), "corr-pep");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(reasonCode, Is.EqualTo("PEP_MATCH"));
        }

        [Test]
        public async Task ScreenSubject_AdverseMediaHit_ReturnsNeedsReview()
        {
            var provider = CreateProvider(
                responseBody: BuildAdverseMediaHitJson("s_adv_001"),
                includeAdverseMedia: true);
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "adv-subject", new Dictionary<string, string>(), "corr-adv");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(reasonCode, Is.EqualTo("ADVERSE_MEDIA_MATCH"));
        }

        [Test]
        public async Task ScreenSubject_SanctionsTrumpsOtherHits_ReturnsRejected()
        {
            // Even with PEP hits, sanctions must produce Rejected (not NeedsReview)
            var body = WrapInEnvelope("s_multi_001", 2, new List<object>
            {
                new { doc = new { entity_type = "person", types = new[] { "pep" }, name = "PEP Person" } },
                new { doc = new { entity_type = "person", types = new[] { "sanction" }, name = "Sanctioned Person" } }
            });

            var provider = CreateProvider(responseBody: body);
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "multi-subject", new Dictionary<string, string>(), "corr-multi");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected),
                "Sanctions hit must produce Rejected even when combined with PEP hits");
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task ScreenSubject_UnknownHitTypes_ReturnsNeedsReview()
        {
            // Hits exist but none are sanctions/PEP/adverse → flag for review
            var body = WrapInEnvelope("s_unk_001", 1, new List<object>
            {
                new { doc = new { entity_type = "person", types = new[] { "other-risk" }, name = "Unknown Risk" } }
            });

            var provider = CreateProvider(responseBody: body);
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "unk-subject", new Dictionary<string, string>(), "corr-unk");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(reasonCode, Is.EqualTo("REVIEW_REQUIRED"));
        }

        // ── Business entity subject support ──────────────────────────────────────

        [Test]
        public async Task ScreenSubject_BusinessEntityUsingLegalName_Succeeds()
        {
            string? capturedBody = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildCleanSearchJson("s_biz_001"),
                onRequest: async req => capturedBody = await req.Content!.ReadAsStringAsync());

            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var metadata = new Dictionary<string, string>
            {
                ["subject_type"] = "BusinessEntity",
                ["legal_name"] = "Acme Industries AG",
                ["registration_number"] = "CHE-123.456.789",
                ["jurisdiction"] = "CH"
            };

            var (refId, state, reasonCode, error) = await provider.ScreenSubjectAsync(
                "entity-biz-001", metadata, "corr-biz-001");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
            // The request should use the legal_name as search term
            Assert.That(capturedBody, Does.Contain("Acme Industries AG").Or.Contain("Acme+Industries+AG"));
        }

        [Test]
        public async Task ScreenSubject_BusinessEntityFallsBackToSubjectId_WhenNoLegalName()
        {
            string? capturedBody = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildCleanSearchJson("s_biz_002"),
                onRequest: async req => capturedBody = await req.Content!.ReadAsStringAsync());

            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var metadata = new Dictionary<string, string> { ["subject_type"] = "BusinessEntity" };

            await provider.ScreenSubjectAsync("entity-no-name-001", metadata, "corr-biz-002");

            // Falls back to subjectId as search term
            Assert.That(capturedBody, Does.Contain("entity-no-name-001"));
        }

        [Test]
        public async Task ScreenSubject_EntityTypeForBusinessSetToCompany()
        {
            string? capturedBody = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildCleanSearchJson("s_biz_003"),
                onRequest: async req => capturedBody = await req.Content!.ReadAsStringAsync());

            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var metadata = new Dictionary<string, string>
            {
                ["subject_type"] = "BusinessEntity",
                ["legal_name"] = "CorpXYZ"
            };

            await provider.ScreenSubjectAsync("entity-003", metadata, "corr-biz-003");

            // entity_type in request must be "company" for business entities
            Assert.That(capturedBody, Does.Contain("company"));
        }

        [Test]
        public async Task ScreenSubject_IndividualEntityTypeSetToPerson()
        {
            string? capturedBody = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildCleanSearchJson("s_ind_001"),
                onRequest: async req => capturedBody = await req.Content!.ReadAsStringAsync());

            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            await provider.ScreenSubjectAsync("indiv-001", new Dictionary<string, string>(), "corr-ind-001");

            // entity_type for individual must be "person"
            Assert.That(capturedBody, Does.Contain("person"));
        }

        // ── Transient vs terminal error classification ────────────────────────────

        [Test]
        public async Task ScreenSubject_503ServiceUnavailable_ReturnsProviderUnavailable()
        {
            var provider = CreateProvider(
                responseStatus: HttpStatusCode.ServiceUnavailable,
                responseBody: "{\"message\":\"Service unavailable\"}");

            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "subj-503", new Dictionary<string, string>(), "corr-503");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable),
                "503 is transient; must produce ProviderUnavailable, not Error");
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_UNAVAILABLE"));
        }

        [Test]
        public async Task ScreenSubject_429TooManyRequests_ReturnsProviderUnavailable()
        {
            var provider = CreateProvider(
                responseStatus: HttpStatusCode.TooManyRequests,
                responseBody: "{\"message\":\"Rate limit exceeded\"}");

            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "subj-429", new Dictionary<string, string>(), "corr-429");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable),
                "429 is transient; must produce ProviderUnavailable");
        }

        [Test]
        public async Task ScreenSubject_400BadRequest_ReturnsError()
        {
            var provider = CreateProvider(
                responseStatus: HttpStatusCode.BadRequest,
                responseBody: "{\"message\":\"Invalid search term\"}");

            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "subj-400", new Dictionary<string, string>(), "corr-400");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error),
                "400 is a terminal failure; must produce Error, not ProviderUnavailable");
        }

        [Test]
        public async Task ScreenSubject_401Unauthorized_ReturnsError()
        {
            var provider = CreateProvider(
                responseStatus: HttpStatusCode.Unauthorized,
                responseBody: "{\"message\":\"Unauthorized\"}");

            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "subj-401", new Dictionary<string, string>(), "corr-401");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
        }

        [Test]
        public async Task ScreenSubject_MalformedJson_ReturnsError()
        {
            var provider = CreateProvider(responseBody: "definitely-not-json{{{");
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "subj-malformed", new Dictionary<string, string>(), "corr-malformed");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("MALFORMED_RESPONSE"));
        }

        [Test]
        public async Task ScreenSubject_NetworkException_ReturnsProviderUnavailable()
        {
            var fakeHandler = new FakeHttpMessageHandler(throwException: new HttpRequestException("Connection refused"));
            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var (_, state, reasonCode, error) = await provider.ScreenSubjectAsync(
                "subj-net", new Dictionary<string, string>(), "corr-net");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_NETWORK_ERROR"));
        }

        [Test]
        public async Task ScreenSubject_TaskCanceled_ReturnsProviderUnavailable()
        {
            var fakeHandler = new FakeHttpMessageHandler(throwException: new TaskCanceledException("Timeout"));
            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "subj-timeout", new Dictionary<string, string>(), "corr-timeout");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_TIMEOUT"));
        }

        // ── Status polling ────────────────────────────────────────────────────────

        [Test]
        public async Task GetScreeningStatus_CleanResult_ReturnsApproved()
        {
            var provider = CreateProvider(responseBody: BuildCleanSearchJson("s_status_001"));
            var (state, reasonCode, error) = await provider.GetScreeningStatusAsync("s_status_001");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(reasonCode, Is.Null);
            Assert.That(error, Is.Null);
        }

        [Test]
        public async Task GetScreeningStatus_SanctionsHit_ReturnsRejected()
        {
            var provider = CreateProvider(responseBody: BuildSanctionsHitJson("s_poll_001"));
            var (state, reasonCode, error) = await provider.GetScreeningStatusAsync("s_poll_001");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task GetScreeningStatus_503Response_ReturnsProviderUnavailable()
        {
            var provider = CreateProvider(
                responseStatus: HttpStatusCode.ServiceUnavailable,
                responseBody: "{\"message\":\"Down for maintenance\"}");

            var (state, reasonCode, _) = await provider.GetScreeningStatusAsync("s_poll_503");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.ProviderUnavailable));
        }

        // ── Correlation ID propagation ────────────────────────────────────────────

        [Test]
        public async Task ScreenSubject_CorrelationIdForwardedInHeader()
        {
            HttpRequestMessage? capturedRequest = null;
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildCleanSearchJson("s_001"),
                onRequest: req => capturedRequest = req);

            var config = new AmlConfig { Provider = "ComplyAdvantage", ApiKey = "ca_test_key" };
            var provider = new ComplyAdvantageAmlProvider(
                new OptionsWrapper<AmlConfig>(config),
                new FakeHttpClientFactory(new HttpClient(fakeHandler)),
                NullLogger<ComplyAdvantageAmlProvider>.Instance);

            var correlationId = "corr-propagation-test-xyz";
            await provider.ScreenSubjectAsync("subj-1", new Dictionary<string, string>(), correlationId);

            Assert.That(capturedRequest?.Headers.Contains("X-Correlation-Id"), Is.True,
                "Correlation ID must be forwarded as X-Correlation-Id header");
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

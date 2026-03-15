using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Production-oriented AML provider adapter for ComplyAdvantage.
    /// <para>
    /// Uses the ComplyAdvantage Screening API to screen subjects against sanctions lists,
    /// PEP watchlists, and adverse media sources. Supports both individual and business-entity
    /// subjects. Business entities use the <c>legal_name</c>, <c>registration_number</c>,
    /// and <c>jurisdiction</c> metadata fields.
    /// </para>
    /// <para>
    /// Fail-closed behaviour: when <c>AmlConfig.ApiKey</c> is missing or blank, every
    /// call returns <see cref="ComplianceDecisionState.ProviderUnavailable"/> rather than
    /// silently approving subjects.
    /// </para>
    /// <para>
    /// Configure via <c>appsettings.json</c>:
    /// <code>
    /// "AmlConfig": {
    ///   "Provider": "ComplyAdvantage",
    ///   "ApiKey": "...",          // inject via env: AmlConfig__ApiKey
    ///   "ApiEndpoint": "https://api.complyadvantage.com",
    ///   "IncludePepScreening": true,
    ///   "IncludeAdverseMedia": false,
    ///   "FuzzinessThreshold": 0,
    ///   "MinApprovalConfidence": 0.8,
    ///   "EvidenceValidityHours": 720
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public class ComplyAdvantageAmlProvider : IAmlProvider
    {
        private const string DefaultApiEndpoint = "https://api.complyadvantage.com";
        private const string SearchesPath = "/searches";

        private readonly AmlConfig _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ComplyAdvantageAmlProvider> _logger;

        /// <summary>
        /// Initialises the provider. Logs a warning at startup if the API key is not configured.
        /// </summary>
        public ComplyAdvantageAmlProvider(
            IOptions<AmlConfig> config,
            IHttpClientFactory httpClientFactory,
            ILogger<ComplyAdvantageAmlProvider> logger)
        {
            _config = config.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                _logger.LogWarning(
                    "ComplyAdvantageAmlProvider: AmlConfig.ApiKey is not configured. " +
                    "All screening attempts will fail-closed with ProviderUnavailable " +
                    "until a valid API key is provided via AmlConfig__ApiKey environment variable.");
            }
        }

        /// <inheritdoc/>
        public string ProviderName => "ComplyAdvantage";

        /// <inheritdoc/>
        public async Task<(string providerReferenceId, ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
            ScreenSubjectAsync(
                string subjectId,
                Dictionary<string, string> subjectMetadata,
                string correlationId)
        {
            if (!IsConfigured(out string? configError))
            {
                _logger.LogError(
                    "ComplyAdvantageAmlProvider: provider not configured: {ConfigError}. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    configError,
                    LoggingHelper.SanitizeLogInput(subjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, ComplianceDecisionState.ProviderUnavailable, "PROVIDER_NOT_CONFIGURED", configError);
            }

            try
            {
                var client = CreateHttpClient();
                var endpoint = $"{BaseEndpoint()}{SearchesPath}";

                var requestBody = BuildSearchRequest(subjectId, subjectMetadata, correlationId);
                var jsonContent = JsonSerializer.Serialize(requestBody);

                _logger.LogInformation(
                    "ComplyAdvantageAmlProvider screening subject. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(subjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("X-Correlation-Id", correlationId);

                using var response = await client.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetail = ParseApiError(responseBody);
                    _logger.LogError(
                        "ComplyAdvantageAmlProvider: non-success response {StatusCode} for SubjectId={SubjectId}. Error={Error}, CorrelationId={CorrelationId}",
                        (int)response.StatusCode,
                        LoggingHelper.SanitizeLogInput(subjectId),
                        LoggingHelper.SanitizeLogInput(errorDetail),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    // 503 / 429 are transient; map to ProviderUnavailable so the orchestrator can retry
                    var isTransient = (int)response.StatusCode is 429 or 503;
                    var state = isTransient
                        ? ComplianceDecisionState.ProviderUnavailable
                        : ComplianceDecisionState.Error;
                    var code = isTransient ? "PROVIDER_UNAVAILABLE" : "PROVIDER_REQUEST_FAILED";
                    return (string.Empty, state, code, $"Provider error {(int)response.StatusCode}: {errorDetail}");
                }

                var searchResponse = ParseSearchResponse(responseBody);
                if (searchResponse == null)
                {
                    _logger.LogError(
                        "ComplyAdvantageAmlProvider: failed to parse search response for SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(subjectId),
                        LoggingHelper.SanitizeLogInput(correlationId));
                    return (string.Empty, ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "Malformed provider response");
                }

                var refId = searchResponse.Id?.ToString() ?? string.Empty;
                var (decisionState, reasonCode) = MapSearchResult(searchResponse);

                _logger.LogInformation(
                    "ComplyAdvantageAmlProvider: screening complete. RefId={RefId}, State={State}, Reason={Reason}, SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(refId),
                    decisionState,
                    LoggingHelper.SanitizeLogInput(reasonCode ?? "none"),
                    LoggingHelper.SanitizeLogInput(subjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return (refId, decisionState, reasonCode, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "ComplyAdvantageAmlProvider: network error screening subject. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(subjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, ComplianceDecisionState.ProviderUnavailable, "PROVIDER_NETWORK_ERROR",
                    "Network error communicating with AML provider");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex,
                    "ComplyAdvantageAmlProvider: request timeout screening subject. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(subjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, ComplianceDecisionState.ProviderUnavailable, "PROVIDER_TIMEOUT",
                    "AML provider request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ComplyAdvantageAmlProvider: unexpected error screening subject. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(subjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, ComplianceDecisionState.Error, "INTERNAL_ERROR", $"Unexpected error: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<(ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
            GetScreeningStatusAsync(string providerReferenceId)
        {
            if (!IsConfigured(out string? configError))
            {
                _logger.LogError(
                    "ComplyAdvantageAmlProvider: cannot fetch status – provider not configured: {ConfigError}. ProviderRefId={ProviderRefId}",
                    configError,
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return (ComplianceDecisionState.ProviderUnavailable, "PROVIDER_NOT_CONFIGURED", configError);
            }

            if (string.IsNullOrWhiteSpace(providerReferenceId))
            {
                return (ComplianceDecisionState.Error, "MISSING_REFERENCE_ID", "providerReferenceId is required");
            }

            try
            {
                var client = CreateHttpClient();
                var endpoint = $"{BaseEndpoint()}{SearchesPath}/{Uri.EscapeDataString(providerReferenceId)}";

                _logger.LogInformation(
                    "ComplyAdvantageAmlProvider fetching status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));

                using var response = await client.GetAsync(endpoint);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetail = ParseApiError(responseBody);
                    _logger.LogError(
                        "ComplyAdvantageAmlProvider: non-success response {StatusCode} fetching status. ProviderRefId={ProviderRefId}, Error={Error}",
                        (int)response.StatusCode,
                        LoggingHelper.SanitizeLogInput(providerReferenceId),
                        LoggingHelper.SanitizeLogInput(errorDetail));

                    var isTransient = (int)response.StatusCode is 429 or 503;
                    return isTransient
                        ? (ComplianceDecisionState.ProviderUnavailable, "PROVIDER_UNAVAILABLE", $"Provider error {(int)response.StatusCode}: {errorDetail}")
                        : (ComplianceDecisionState.Error, "PROVIDER_REQUEST_FAILED", $"Provider error {(int)response.StatusCode}: {errorDetail}");
                }

                var searchResponse = ParseSearchResponse(responseBody);
                if (searchResponse == null)
                {
                    return (ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "Malformed provider response");
                }

                var (state, reasonCode) = MapSearchResult(searchResponse);

                _logger.LogInformation(
                    "ComplyAdvantageAmlProvider: status fetched. ProviderRefId={ProviderRefId}, State={State}, Reason={Reason}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId),
                    state,
                    LoggingHelper.SanitizeLogInput(reasonCode ?? "none"));

                return (state, reasonCode, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "ComplyAdvantageAmlProvider: network error fetching status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return (ComplianceDecisionState.ProviderUnavailable, "PROVIDER_NETWORK_ERROR",
                    "Network error communicating with AML provider");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex,
                    "ComplyAdvantageAmlProvider: timeout fetching status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return (ComplianceDecisionState.ProviderUnavailable, "PROVIDER_TIMEOUT", "AML provider request timed out");
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private bool IsConfigured(out string? error)
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                error = "AmlConfig.ApiKey is not set. Provide it via the AmlConfig__ApiKey environment variable.";
                return false;
            }
            error = null;
            return true;
        }

        private HttpClient CreateHttpClient()
        {
            var client = _httpClientFactory.CreateClient("default");
            // ComplyAdvantage uses API key as Bearer token
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", _config.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(
                _config.RequestTimeoutSeconds > 0 ? _config.RequestTimeoutSeconds : 30);
            return client;
        }

        private string BaseEndpoint()
            => string.IsNullOrWhiteSpace(_config.ApiEndpoint)
                ? DefaultApiEndpoint
                : _config.ApiEndpoint.TrimEnd('/');

        private ComplyAdvantageSearchRequest BuildSearchRequest(
            string subjectId,
            Dictionary<string, string> metadata,
            string correlationId)
        {
            // Determine entity type from metadata
            var isBusinessEntity = metadata.TryGetValue("subject_type", out var subjectType) &&
                string.Equals(subjectType, "BusinessEntity", StringComparison.OrdinalIgnoreCase);

            // Build search filters based on config
            var filters = new List<string> { "sanction" };
            if (_config.IncludePepScreening) filters.Add("pep");
            if (_config.IncludeAdverseMedia) filters.Add("adverse-media");

            // Build search term: prefer legal_name for entities, full_name for individuals
            string searchTerm;
            if (isBusinessEntity && metadata.TryGetValue("legal_name", out var legalName) && !string.IsNullOrWhiteSpace(legalName))
                searchTerm = legalName;
            else if (metadata.TryGetValue("full_name", out var fullName) && !string.IsNullOrWhiteSpace(fullName))
                searchTerm = fullName;
            else
                searchTerm = subjectId;

            return new ComplyAdvantageSearchRequest
            {
                SearchTerm = searchTerm,
                Fuzziness = _config.FuzzinessThreshold / 100.0, // normalize to 0.0–1.0
                Filters = new ComplyAdvantageSearchFilters
                {
                    Types = filters,
                    EntityType = isBusinessEntity ? "company" : "person"
                },
                ClientRef = subjectId,
                Tags = new List<string> { correlationId },
                ShareUrl = false
            };
        }

        private (ComplianceDecisionState state, string? reasonCode) MapSearchResult(ComplyAdvantageSearchResponse response)
        {
            // No hits: clean result → Approved
            if (response.TotalHits == 0 || response.Hits == null || response.Hits.Count == 0)
            {
                // MinApprovalConfidence is reserved for provider-supplied confidence scores
                // when ComplyAdvantage returns scored matches (future enhancement).
                return (ComplianceDecisionState.Approved, null);
            }

            // Analyse the hits
            var matchedCategories = new List<string>();
            bool hasSanctionsHit = false;
            bool hasPepHit = false;
            bool hasAdverseMedia = false;

            foreach (var hit in response.Hits)
            {
                if (hit.Types == null) continue;
                foreach (var type in hit.Types)
                {
                    var lower = type.ToLowerInvariant();
                    if (lower.Contains("sanction"))
                    {
                        hasSanctionsHit = true;
                        // Map to normalized category names
                        if (lower.Contains("ofac")) matchedCategories.Add("OFAC_SDN");
                        else if (lower.Contains("eu")) matchedCategories.Add("EU_SANCTIONS");
                        else if (lower.Contains("un")) matchedCategories.Add("UN_CONSOLIDATED");
                        else matchedCategories.Add("SANCTIONS_MATCH");
                    }
                    else if (lower.Contains("pep"))
                    {
                        hasPepHit = true;
                        matchedCategories.Add("PEP_WATCHLIST");
                    }
                    else if (lower.Contains("adverse"))
                    {
                        hasAdverseMedia = true;
                        matchedCategories.Add("ADVERSE_MEDIA");
                    }
                }
            }

            // Hard rejection for sanctions
            if (hasSanctionsHit)
                return (ComplianceDecisionState.Rejected, "SANCTIONS_MATCH");

            // PEP or adverse media → needs review
            if (hasPepHit)
                return (ComplianceDecisionState.NeedsReview, "PEP_MATCH");

            if (hasAdverseMedia)
                return (ComplianceDecisionState.NeedsReview, "ADVERSE_MEDIA_MATCH");

            // Hits exist but none are sanctions/PEP/adverse – possible false positives, flag for review
            return (ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED");
        }

        private ComplyAdvantageSearchResponse? ParseSearchResponse(string json)
        {
            try
            {
                // ComplyAdvantage wraps the result in a "content" envelope
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("data", out var data))
                {
                    return JsonSerializer.Deserialize<ComplyAdvantageSearchResponse>(data.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                // Also try direct deserialization if envelope is missing
                return JsonSerializer.Deserialize<ComplyAdvantageSearchResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ComplyAdvantageAmlProvider: failed to parse search response JSON ({ExceptionType})",
                    ex.GetType().Name);
                return null;
            }
        }

        private static string ParseApiError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var message))
                    return message.GetString() ?? "Unknown provider error";
                if (doc.RootElement.TryGetProperty("error", out var error))
                    return error.GetString() ?? "Unknown provider error";
            }
            catch (JsonException)
            {
                // Non-JSON error body; fall through to default
            }
            return "Unknown provider error";
        }

        // ── Request / Response DTOs ───────────────────────────────────────────────

        private sealed class ComplyAdvantageSearchRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("search_term")]
            public string SearchTerm { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("fuzziness")]
            public double Fuzziness { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("filters")]
            public ComplyAdvantageSearchFilters? Filters { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("client_ref")]
            public string? ClientRef { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("tags")]
            public List<string>? Tags { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("share_url")]
            public bool ShareUrl { get; set; }
        }

        private sealed class ComplyAdvantageSearchFilters
        {
            [System.Text.Json.Serialization.JsonPropertyName("types")]
            public List<string> Types { get; set; } = new();

            [System.Text.Json.Serialization.JsonPropertyName("entity_type")]
            public string? EntityType { get; set; }
        }

        private sealed class ComplyAdvantageSearchResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public object? Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("search_term")]
            public string? SearchTerm { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("total_hits")]
            public int TotalHits { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("hits")]
            public List<ComplyAdvantageHit>? Hits { get; set; }
        }

        private sealed class ComplyAdvantageHit
        {
            [System.Text.Json.Serialization.JsonPropertyName("doc")]
            public ComplyAdvantageHitDocument? Doc { get; set; }

            /// <summary>Flattened types list from the hit for convenience</summary>
            public List<string>? Types => Doc?.Types;
        }

        private sealed class ComplyAdvantageHitDocument
        {
            [System.Text.Json.Serialization.JsonPropertyName("entity_type")]
            public string? EntityType { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("types")]
            public List<string>? Types { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}

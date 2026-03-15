using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Production-oriented KYC provider adapter for Stripe Identity.
    /// <para>
    /// Uses the Stripe Identity API to create and retrieve identity verification sessions.
    /// Supports both individual and business-entity subjects via the <c>subject_type</c>
    /// metadata field in the verification session.
    /// </para>
    /// <para>
    /// Fail-closed behaviour: when <c>KycConfig.ApiKey</c> is missing or blank, every
    /// call returns <see cref="KycStatus.NotStarted"/> with an explanatory error rather
    /// than silently allowing subjects through.
    /// </para>
    /// <para>
    /// Configure via <c>appsettings.json</c>:
    /// <code>
    /// "KycConfig": {
    ///   "Provider": "StripeIdentity",
    ///   "ApiKey": "sk_live_...",          // inject via env: KycConfig__ApiKey
    ///   "ApiEndpoint": "https://api.stripe.com",
    ///   "WebhookSecret": "whsec_...",     // inject via env: KycConfig__WebhookSecret
    ///   "RequestTimeoutSeconds": 30
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public class StripeIdentityKycProvider : IKycProvider
    {
        private const string DefaultApiEndpoint = "https://api.stripe.com";
        private const string VerificationSessionsPath = "/v1/identity/verification_sessions";

        private readonly KycConfig _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StripeIdentityKycProvider> _logger;

        /// <summary>
        /// Initialises the provider. Logs a warning at startup if the API key is not configured
        /// so operators are alerted before any check is attempted.
        /// </summary>
        public StripeIdentityKycProvider(
            IOptions<KycConfig> config,
            IHttpClientFactory httpClientFactory,
            ILogger<StripeIdentityKycProvider> logger)
        {
            _config = config.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                _logger.LogWarning(
                    "StripeIdentityKycProvider: KycConfig.ApiKey is not configured. " +
                    "All verification attempts will fail-closed with KycStatus.NotStarted " +
                    "until a valid API key is provided via KycConfig__ApiKey environment variable.");
            }
        }

        /// <inheritdoc/>
        public async Task<(string providerReferenceId, KycStatus status, string? errorMessage)> StartVerificationAsync(
            string userId,
            StartKycVerificationRequest request,
            string correlationId)
        {
            if (!IsConfigured(out string? configError))
            {
                _logger.LogError(
                    "StripeIdentityKycProvider is not configured: {ConfigError}. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    configError,
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, KycStatus.NotStarted, $"Provider not configured: {configError}");
            }

            try
            {
                var client = CreateHttpClient();
                var endpoint = $"{BaseEndpoint()}{VerificationSessionsPath}";

                var formData = BuildStartVerificationFormData(userId, request, correlationId);

                _logger.LogInformation(
                    "StripeIdentityKycProvider starting verification. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                httpRequest.Headers.Add("Idempotency-Key", correlationId);

                using var response = await client.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var stripeError = ParseStripeError(responseBody);
                    _logger.LogError(
                        "StripeIdentityKycProvider: non-success response {StatusCode} for SubjectId={SubjectId}. Error={Error}, CorrelationId={CorrelationId}",
                        (int)response.StatusCode,
                        LoggingHelper.SanitizeLogInput(userId),
                        LoggingHelper.SanitizeLogInput(stripeError),
                        LoggingHelper.SanitizeLogInput(correlationId));
                    return (string.Empty, KycStatus.NotStarted, $"Provider error {(int)response.StatusCode}: {stripeError}");
                }

                var sessionResponse = ParseVerificationSessionResponse(responseBody);
                if (sessionResponse == null)
                {
                    _logger.LogError(
                        "StripeIdentityKycProvider: failed to parse session response for SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId),
                        LoggingHelper.SanitizeLogInput(correlationId));
                    return (string.Empty, KycStatus.NotStarted, "Malformed provider response");
                }

                var status = MapStripeStatus(sessionResponse.Status);

                _logger.LogInformation(
                    "StripeIdentityKycProvider: verification session created. ProviderRefId={ProviderRefId}, Status={Status}, SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(sessionResponse.Id),
                    status,
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return (sessionResponse.Id, status, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "StripeIdentityKycProvider: network error starting verification. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, KycStatus.NotStarted, "Network error communicating with KYC provider");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex,
                    "StripeIdentityKycProvider: request timeout starting verification. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, KycStatus.NotStarted, "Provider request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "StripeIdentityKycProvider: unexpected error starting verification. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return (string.Empty, KycStatus.NotStarted, $"Unexpected error: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<(KycStatus status, string? reason, string? errorMessage)> FetchStatusAsync(string providerReferenceId)
        {
            if (!IsConfigured(out string? configError))
            {
                _logger.LogError(
                    "StripeIdentityKycProvider: cannot fetch status – provider not configured: {ConfigError}. ProviderRefId={ProviderRefId}",
                    configError,
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return (KycStatus.NotStarted, null, $"Provider not configured: {configError}");
            }

            if (string.IsNullOrWhiteSpace(providerReferenceId))
            {
                return (KycStatus.NotStarted, null, "providerReferenceId is required");
            }

            try
            {
                var client = CreateHttpClient();
                var endpoint = $"{BaseEndpoint()}{VerificationSessionsPath}/{Uri.EscapeDataString(providerReferenceId)}";

                _logger.LogInformation(
                    "StripeIdentityKycProvider fetching status for ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));

                using var response = await client.GetAsync(endpoint);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var stripeError = ParseStripeError(responseBody);
                    _logger.LogError(
                        "StripeIdentityKycProvider: non-success response {StatusCode} fetching status for ProviderRefId={ProviderRefId}. Error={Error}",
                        (int)response.StatusCode,
                        LoggingHelper.SanitizeLogInput(providerReferenceId),
                        LoggingHelper.SanitizeLogInput(stripeError));
                    return (KycStatus.NotStarted, null, $"Provider error {(int)response.StatusCode}: {stripeError}");
                }

                var sessionResponse = ParseVerificationSessionResponse(responseBody);
                if (sessionResponse == null)
                {
                    return (KycStatus.NotStarted, null, "Malformed provider response");
                }

                var status = MapStripeStatus(sessionResponse.Status);
                var reason = string.IsNullOrWhiteSpace(sessionResponse.LastError?.Code)
                    ? null
                    : sessionResponse.LastError.Code;

                _logger.LogInformation(
                    "StripeIdentityKycProvider: status fetched. ProviderRefId={ProviderRefId}, Status={Status}, Reason={Reason}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId),
                    status,
                    LoggingHelper.SanitizeLogInput(reason ?? "none"));

                return (status, reason, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "StripeIdentityKycProvider: network error fetching status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return (KycStatus.NotStarted, null, "Network error communicating with KYC provider");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex,
                    "StripeIdentityKycProvider: timeout fetching status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return (KycStatus.NotStarted, null, "Provider request timed out");
            }
        }

        /// <inheritdoc/>
        public bool ValidateWebhookSignature(string payload, string signature, string webhookSecret)
        {
            // Stripe webhook signature format: "t=<timestamp>,v1=<hmac_sha256>"
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogWarning("StripeIdentityKycProvider: missing payload or signature in webhook validation");
                return false;
            }

            try
            {
                var parts = signature.Split(',');
                string? timestamp = null;
                string? v1Signature = null;

                foreach (var part in parts)
                {
                    if (part.StartsWith("t=", StringComparison.Ordinal))
                        timestamp = part[2..];
                    else if (part.StartsWith("v1=", StringComparison.Ordinal))
                        v1Signature = part[3..];
                }

                if (timestamp == null || v1Signature == null)
                {
                    _logger.LogWarning("StripeIdentityKycProvider: malformed webhook signature header");
                    return false;
                }

                var signedPayload = $"{timestamp}.{payload}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
                var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
                var expectedSignature = BitConverter.ToString(expectedHash).Replace("-", "").ToLowerInvariant();

                var isValid = string.Equals(expectedSignature, v1Signature, StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation("StripeIdentityKycProvider: webhook signature validation result: {IsValid}", isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StripeIdentityKycProvider: error validating webhook signature");
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<(string providerReferenceId, KycStatus status, string? reason)> ParseWebhookAsync(KycWebhookPayload payload)
        {
            try
            {
                _logger.LogInformation(
                    "StripeIdentityKycProvider: parsing webhook. ProviderRefId={ProviderRefId}, EventType={EventType}",
                    LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(payload.EventType));

                // Stripe event types: identity.verification_session.verified, identity.verification_session.requires_input
                var status = payload.EventType switch
                {
                    "identity.verification_session.verified" => KycStatus.Approved,
                    "identity.verification_session.requires_input" => KycStatus.NeedsReview,
                    "identity.verification_session.canceled" => KycStatus.Rejected,
                    "identity.verification_session.processing" => KycStatus.Pending,
                    _ => ParseStatusFromString(payload.Status)
                };

                return Task.FromResult((payload.ProviderReferenceId, status, payload.Reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StripeIdentityKycProvider: error parsing webhook payload");
                throw;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private bool IsConfigured(out string? error)
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                error = "KycConfig.ApiKey is not set. Provide it via the KycConfig__ApiKey environment variable.";
                return false;
            }
            error = null;
            return true;
        }

        private HttpClient CreateHttpClient()
        {
            var client = _httpClientFactory.CreateClient("default");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(
                _config.RequestTimeoutSeconds > 0 ? _config.RequestTimeoutSeconds : 30);
            return client;
        }

        private string BaseEndpoint()
            => string.IsNullOrWhiteSpace(_config.ApiEndpoint)
                ? DefaultApiEndpoint
                : _config.ApiEndpoint.TrimEnd('/');

        private static List<KeyValuePair<string, string>> BuildStartVerificationFormData(
            string userId,
            StartKycVerificationRequest request,
            string correlationId)
        {
            var data = new List<KeyValuePair<string, string>>
            {
                new("type", "document"),
                new("metadata[subject_id]", userId),
                new("metadata[correlation_id]", correlationId)
            };

            // Subject type metadata so the session can be routed correctly on the Stripe side
            if (request.Metadata != null)
            {
                if (request.Metadata.TryGetValue("subject_type", out var subjectType))
                    data.Add(new("metadata[subject_type]", subjectType));

                if (request.Metadata.TryGetValue("full_name", out var fullName))
                    data.Add(new("metadata[full_name]", fullName));

                if (request.Metadata.TryGetValue("legal_name", out var legalName))
                    data.Add(new("metadata[legal_name]", legalName));

                if (request.Metadata.TryGetValue("jurisdiction", out var jurisdiction))
                    data.Add(new("metadata[jurisdiction]", jurisdiction));

                if (request.Metadata.TryGetValue("registration_number", out var regNumber))
                    data.Add(new("metadata[registration_number]", regNumber));
            }

            return data;
        }

        private StripeVerificationSessionResponse? ParseVerificationSessionResponse(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<StripeVerificationSessionResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "StripeIdentityKycProvider: failed to parse verification session response JSON ({ExceptionType})",
                    ex.GetType().Name);
                return null;
            }
        }

        private static string ParseStripeError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? "Unknown provider error";
                }
            }
            catch { }
            return "Unknown provider error";
        }

        private static KycStatus MapStripeStatus(string? stripeStatus) => stripeStatus?.ToLowerInvariant() switch
        {
            "verified" => KycStatus.Approved,
            "requires_input" => KycStatus.NeedsReview,
            "processing" => KycStatus.Pending,
            "canceled" => KycStatus.Rejected,
            "expired" => KycStatus.Expired,
            _ => KycStatus.Pending
        };

        private static KycStatus ParseStatusFromString(string statusString)
        {
            return statusString.ToLowerInvariant() switch
            {
                "approved" or "verified" or "completed" => KycStatus.Approved,
                "rejected" or "denied" or "failed" or "canceled" => KycStatus.Rejected,
                "pending" or "in_progress" or "processing" => KycStatus.Pending,
                "needs_review" or "manual_review" or "review" or "requires_input" => KycStatus.NeedsReview,
                "expired" => KycStatus.Expired,
                _ => KycStatus.NotStarted
            };
        }

        // ── Response DTOs ─────────────────────────────────────────────────────────

        private sealed class StripeVerificationSessionResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string? Status { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("last_error")]
            public StripeVerificationError? LastError { get; set; }
        }

        private sealed class StripeVerificationError
        {
            [System.Text.Json.Serialization.JsonPropertyName("code")]
            public string? Code { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("reason")]
            public string? Reason { get; set; }
        }
    }
}

using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Mock KYC provider for testing and development
    /// </summary>
    public class MockKycProvider : IKycProvider
    {
        private readonly KycConfig _config;
        private readonly ILogger<MockKycProvider> _logger;

        public MockKycProvider(IOptions<KycConfig> config, ILogger<MockKycProvider> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<(string providerReferenceId, KycStatus status, string? errorMessage)> StartVerificationAsync(
            string userId,
            StartKycVerificationRequest request,
            string correlationId)
        {
            try
            {
                // Generate a mock provider reference ID
                var providerReferenceId = $"MOCK-{Guid.NewGuid():N}";

                // Determine initial status based on configuration
                var status = _config.MockAutoApprove ? KycStatus.Approved : KycStatus.Pending;

                _logger.LogInformation(
                    "Mock KYC verification started. UserId={UserId}, ProviderRefId={ProviderRefId}, Status={Status}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(providerReferenceId),
                    status,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return Task.FromResult((providerReferenceId, status, (string?)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting mock KYC verification. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));
                return Task.FromResult(((string)"", KycStatus.NotStarted, ex.Message));
            }
        }

        /// <inheritdoc/>
        public Task<(KycStatus status, string? reason, string? errorMessage)> FetchStatusAsync(string providerReferenceId)
        {
            try
            {
                _logger.LogInformation("Fetching mock KYC status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));

                // For mock provider, simulate different states based on reference ID patterns
                // In a real implementation, this would make an HTTP call to the provider API
                var status = _config.MockAutoApprove ? KycStatus.Approved : KycStatus.Pending;
                var reason = status == KycStatus.Approved ? "Auto-approved by mock provider" : "Pending review";

                return Task.FromResult((status, reason, (string?)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching mock KYC status. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(providerReferenceId));
                return Task.FromResult((KycStatus.NotStarted, (string?)null, ex.Message));
            }
        }

        /// <inheritdoc/>
        public bool ValidateWebhookSignature(string payload, string signature, string webhookSecret)
        {
            try
            {
                // For mock provider, implement a simple HMAC-SHA256 signature validation
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var computedSignature = Convert.ToBase64String(hash);

                var isValid = computedSignature == signature;

                _logger.LogInformation("Mock webhook signature validation result: {IsValid}", isValid);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating mock webhook signature");
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<(string providerReferenceId, KycStatus status, string? reason)> ParseWebhookAsync(KycWebhookPayload payload)
        {
            try
            {
                _logger.LogInformation("Parsing mock webhook. ProviderRefId={ProviderRefId}, EventType={EventType}, Status={Status}",
                    LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(payload.EventType),
                    LoggingHelper.SanitizeLogInput(payload.Status));

                // Parse status from webhook payload
                var status = ParseStatusFromString(payload.Status);

                return Task.FromResult((payload.ProviderReferenceId, status, payload.Reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing mock webhook payload");
                throw;
            }
        }

        /// <summary>
        /// Parses a status string into a KycStatus enum
        /// </summary>
        private KycStatus ParseStatusFromString(string statusString)
        {
            return statusString.ToLowerInvariant() switch
            {
                "approved" or "verified" or "completed" => KycStatus.Approved,
                "rejected" or "denied" or "failed" => KycStatus.Rejected,
                "pending" or "in_progress" => KycStatus.Pending,
                "needs_review" or "manual_review" or "review" => KycStatus.NeedsReview,
                "expired" => KycStatus.Expired,
                _ => KycStatus.NotStarted
            };
        }
    }
}

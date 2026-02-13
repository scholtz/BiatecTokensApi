using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing KYC verification lifecycle
    /// </summary>
    public class KycService : IKycService
    {
        private readonly IKycRepository _kycRepository;
        private readonly IKycProvider _kycProvider;
        private readonly KycConfig _config;
        private readonly ILogger<KycService> _logger;

        public KycService(
            IKycRepository kycRepository,
            IKycProvider kycProvider,
            IOptions<KycConfig> config,
            ILogger<KycService> logger)
        {
            _kycRepository = kycRepository;
            _kycProvider = kycProvider;
            _config = config.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<StartKycVerificationResponse> StartVerificationAsync(
            string userId,
            StartKycVerificationRequest request,
            string correlationId)
        {
            try
            {
                _logger.LogInformation(
                    "Starting KYC verification for user {UserId}. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                // Check if user already has an active verification
                var existingRecord = await _kycRepository.GetKycRecordByUserIdAsync(userId);
                if (existingRecord != null && existingRecord.Status == KycStatus.Pending)
                {
                    _logger.LogWarning(
                        "User {UserId} already has a pending KYC verification. CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return new StartKycVerificationResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.KYC_VERIFICATION_ALREADY_PENDING,
                        ErrorMessage = "A KYC verification is already in progress for this user"
                    };
                }

                // Start verification with provider
                var (providerReferenceId, status, errorMessage) = await _kycProvider.StartVerificationAsync(
                    userId,
                    request,
                    correlationId);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    _logger.LogError(
                        "Failed to start KYC verification with provider. UserId={UserId}, Error={Error}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId),
                        LoggingHelper.SanitizeLogInput(errorMessage),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return new StartKycVerificationResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.KYC_PROVIDER_ERROR,
                        ErrorMessage = errorMessage,
                        CorrelationId = correlationId
                    };
                }

                // Create KYC record
                var kycRecord = new KycRecord
                {
                    UserId = userId,
                    Status = status,
                    Provider = _config.Provider.ToLowerInvariant() == "mock" ? KycProvider.Mock : KycProvider.External,
                    ProviderReferenceId = providerReferenceId,
                    CorrelationId = correlationId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Set expiration date if approved
                if (status == KycStatus.Approved)
                {
                    kycRecord.CompletedAt = DateTime.UtcNow;
                    kycRecord.ExpiresAt = DateTime.UtcNow.AddDays(_config.ExpirationDays);
                }

                var createdRecord = await _kycRepository.CreateKycRecordAsync(kycRecord);

                _logger.LogInformation(
                    "KYC verification started successfully. UserId={UserId}, KycId={KycId}, Status={Status}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(createdRecord.KycId),
                    status,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new StartKycVerificationResponse
                {
                    Success = true,
                    KycId = createdRecord.KycId,
                    ProviderReferenceId = providerReferenceId,
                    Status = status,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error starting KYC verification. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new StartKycVerificationResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An internal error occurred while starting KYC verification",
                    CorrelationId = correlationId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<KycStatusResponse> GetStatusAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Getting KYC status for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var record = await _kycRepository.GetKycRecordByUserIdAsync(userId);

                if (record == null)
                {
                    return new KycStatusResponse
                    {
                        Success = true,
                        Status = KycStatus.NotStarted
                    };
                }

                // Check if verification has expired
                if (record.Status == KycStatus.Approved && 
                    record.ExpiresAt.HasValue && 
                    record.ExpiresAt.Value < DateTime.UtcNow)
                {
                    record.Status = KycStatus.Expired;
                    await _kycRepository.UpdateKycRecordAsync(record);

                    _logger.LogInformation("KYC verification expired for user {UserId}",
                        LoggingHelper.SanitizeLogInput(userId));
                }

                return new KycStatusResponse
                {
                    Success = true,
                    KycId = record.KycId,
                    Status = record.Status,
                    Provider = record.Provider,
                    CreatedAt = record.CreatedAt,
                    UpdatedAt = record.UpdatedAt,
                    CompletedAt = record.CompletedAt,
                    ExpiresAt = record.ExpiresAt,
                    Reason = record.Reason
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting KYC status. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return new KycStatusResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An internal error occurred while retrieving KYC status"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HandleWebhookAsync(KycWebhookPayload payload, string? signature)
        {
            try
            {
                _logger.LogInformation(
                    "Handling KYC webhook. ProviderRefId={ProviderRefId}, EventType={EventType}",
                    LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(payload.EventType));

                // Validate webhook signature if provided
                if (!string.IsNullOrEmpty(signature) && !string.IsNullOrEmpty(_config.WebhookSecret))
                {
                    var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
                    if (!_kycProvider.ValidateWebhookSignature(payloadJson, signature, _config.WebhookSecret))
                    {
                        _logger.LogWarning("Invalid webhook signature for ProviderRefId={ProviderRefId}",
                            LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId));
                        return false;
                    }
                }

                // Parse webhook data
                var (providerReferenceId, status, reason) = await _kycProvider.ParseWebhookAsync(payload);

                // Find KYC record
                var record = await _kycRepository.GetKycRecordByProviderReferenceIdAsync(providerReferenceId);
                if (record == null)
                {
                    _logger.LogWarning("KYC record not found for ProviderRefId={ProviderRefId}",
                        LoggingHelper.SanitizeLogInput(providerReferenceId));
                    return false;
                }

                // Update status
                var oldStatus = record.Status;
                record.Status = status;
                record.Reason = reason;
                record.UpdatedAt = DateTime.UtcNow;

                // Set completion date if status is terminal
                if (status == KycStatus.Approved || status == KycStatus.Rejected)
                {
                    record.CompletedAt = DateTime.UtcNow;
                }

                // Set expiration date if approved
                if (status == KycStatus.Approved)
                {
                    record.ExpiresAt = DateTime.UtcNow.AddDays(_config.ExpirationDays);
                }

                await _kycRepository.UpdateKycRecordAsync(record);

                _logger.LogInformation(
                    "KYC status updated via webhook. KycId={KycId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
                    LoggingHelper.SanitizeLogInput(record.KycId),
                    oldStatus,
                    status);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling KYC webhook. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(payload?.ProviderReferenceId ?? "unknown"));
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsUserVerifiedAsync(string userId)
        {
            try
            {
                var record = await _kycRepository.GetKycRecordByUserIdAsync(userId);

                if (record == null)
                {
                    return false;
                }

                // Check if verification is approved and not expired
                if (record.Status != KycStatus.Approved)
                {
                    return false;
                }

                if (record.ExpiresAt.HasValue && record.ExpiresAt.Value < DateTime.UtcNow)
                {
                    // Mark as expired
                    record.Status = KycStatus.Expired;
                    await _kycRepository.UpdateKycRecordAsync(record);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking KYC verification status. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return false;
            }
        }

        /// <inheritdoc/>
        public bool IsEnforcementEnabled()
        {
            return _config.EnforcementEnabled;
        }
    }
}

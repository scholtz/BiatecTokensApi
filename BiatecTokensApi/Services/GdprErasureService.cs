using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service implementing GDPR Article 17 right-to-erasure for KYC and AML personal data.
    /// PII is anonymized while audit record IDs, timestamps, and compliance references are
    /// retained for the 5-year retention period required by AMLD5.
    /// </summary>
    public class GdprErasureService : IGdprErasureService
    {
        private readonly IKycRepository _kycRepository;
        private readonly IAmlService _amlService;
        private readonly ILogger<GdprErasureService> _logger;

        public GdprErasureService(
            IKycRepository kycRepository,
            IAmlService amlService,
            ILogger<GdprErasureService> logger)
        {
            _kycRepository = kycRepository;
            _amlService = amlService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<GdprErasureResponse> EraseUserDataAsync(
            GdprErasureRequest request,
            string correlationId)
        {
            try
            {
                _logger.LogInformation(
                    "GDPR erasure request received. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var anonymizationReference = $"GDPR-{Guid.NewGuid():N}";
                var erasedAt = DateTime.UtcNow;

                // Anonymize KYC records
                var kycRecordsAnonymized = await AnonymizeKycRecordsAsync(request.UserId, anonymizationReference);

                // Anonymize AML records via AmlService
                var amlRecordsAnonymized = await _amlService.AnonymizeUserAmlDataAsync(request.UserId);

                _logger.LogInformation(
                    "GDPR erasure complete. UserId={UserId}, KycRecords={KycCount}, AmlRecords={AmlCount}, Ref={Ref}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    kycRecordsAnonymized,
                    amlRecordsAnonymized,
                    LoggingHelper.SanitizeLogInput(anonymizationReference),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new GdprErasureResponse
                {
                    Success = true,
                    AnonymizationReference = anonymizationReference,
                    KycRecordsAnonymized = kycRecordsAnonymized,
                    AmlRecordsAnonymized = amlRecordsAnonymized,
                    ErasedAt = erasedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error performing GDPR erasure. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new GdprErasureResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An internal error occurred during GDPR erasure"
                };
            }
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private async Task<int> AnonymizeKycRecordsAsync(string userId, string anonymizationReference)
        {
            var records = await _kycRepository.GetKycRecordsByUserIdAsync(userId);
            int count = 0;

            foreach (var record in records)
            {
                // Preserve audit references; anonymize PII fields
                record.Reason = $"[GDPR_ANONYMIZED:{anonymizationReference}]";
                record.EncryptedData = null;
                record.Metadata = new Dictionary<string, string>
                {
                    ["gdpr_erased_at"] = DateTime.UtcNow.ToString("O"),
                    ["anonymization_ref"] = anonymizationReference
                };
                await _kycRepository.UpdateKycRecordAsync(record);
                count++;
            }

            return count;
        }
    }
}

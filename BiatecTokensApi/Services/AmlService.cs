using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for AML (Anti-Money Laundering) sanctions screening, PEP checks,
    /// continuous monitoring webhooks, and compliance reporting.
    /// </summary>
    public class AmlService : IAmlService
    {
        /// <summary>Re-screening interval — AMLD5 requires at minimum periodic review.</summary>
        private const int RescreeningIntervalDays = 30;

        private readonly IAmlRepository _amlRepository;
        private readonly IAmlProvider _amlProvider;
        private readonly ILogger<AmlService> _logger;

        public AmlService(
            IAmlRepository amlRepository,
            IAmlProvider amlProvider,
            ILogger<AmlService> logger)
        {
            _amlRepository = amlRepository;
            _amlProvider = amlProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<AmlScreenResponse> ScreenUserAsync(
            string userId,
            Dictionary<string, string> metadata,
            string correlationId)
        {
            try
            {
                _logger.LogInformation(
                    "AML screening initiated. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var (providerRefId, decisionState, reasonCode, errorMessage) =
                    await _amlProvider.ScreenSubjectAsync(userId, metadata, correlationId);

                var (status, riskLevel) = MapDecisionState(decisionState, reasonCode);

                // Persist the AML record (create or update)
                var existingRecord = await _amlRepository.GetAmlRecordByUserIdAsync(userId);
                AmlRecord record;

                if (existingRecord != null)
                {
                    existingRecord.Status = status;
                    existingRecord.RiskLevel = riskLevel;
                    existingRecord.ProviderReferenceId = providerRefId;
                    existingRecord.ReasonCode = reasonCode;
                    existingRecord.Notes = errorMessage;
                    existingRecord.CorrelationId = correlationId;
                    existingRecord.NextScreeningDue = status == AmlScreeningStatus.Cleared
                        ? DateTime.UtcNow.AddDays(RescreeningIntervalDays)
                        : null;
                    existingRecord.Metadata = metadata;
                    record = await _amlRepository.UpdateAmlRecordAsync(existingRecord);
                }
                else
                {
                    record = new AmlRecord
                    {
                        UserId = userId,
                        Status = status,
                        RiskLevel = riskLevel,
                        ProviderReferenceId = providerRefId,
                        ReasonCode = reasonCode,
                        Notes = errorMessage,
                        CorrelationId = correlationId,
                        NextScreeningDue = status == AmlScreeningStatus.Cleared
                            ? DateTime.UtcNow.AddDays(RescreeningIntervalDays)
                            : null,
                        Metadata = metadata
                    };
                    record = await _amlRepository.CreateAmlRecordAsync(record);
                }

                _logger.LogInformation(
                    "AML screening complete. UserId={UserId}, Status={Status}, RiskLevel={RiskLevel}, AmlId={AmlId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    status,
                    riskLevel,
                    LoggingHelper.SanitizeLogInput(record.AmlId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new AmlScreenResponse
                {
                    Success = true,
                    AmlId = record.AmlId,
                    ProviderReferenceId = providerRefId,
                    Status = status,
                    RiskLevel = riskLevel,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error performing AML screening. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new AmlScreenResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An internal error occurred during AML screening",
                    CorrelationId = correlationId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<AmlStatusResponse> GetStatusAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Getting AML status for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var record = await _amlRepository.GetAmlRecordByUserIdAsync(userId);

                if (record == null)
                {
                    return new AmlStatusResponse
                    {
                        Success = true,
                        Status = AmlScreeningStatus.NotScreened,
                        RiskLevel = AmlRiskLevel.Unknown
                    };
                }

                return new AmlStatusResponse
                {
                    Success = true,
                    AmlId = record.AmlId,
                    Status = record.Status,
                    RiskLevel = record.RiskLevel,
                    ProviderReferenceId = record.ProviderReferenceId,
                    ReasonCode = record.ReasonCode,
                    Notes = record.Notes,
                    CreatedAt = record.CreatedAt,
                    UpdatedAt = record.UpdatedAt,
                    NextScreeningDue = record.NextScreeningDue
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AML status. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return new AmlStatusResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An internal error occurred while retrieving AML status"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HandleWebhookAsync(AmlWebhookPayload payload, string? signature)
        {
            try
            {
                _logger.LogInformation(
                    "Handling AML webhook. ProviderRefId={ProviderRefId}, AlertType={AlertType}",
                    LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(payload.AlertType));

                // Locate the AML record by provider reference ID
                var record = await _amlRepository.GetAmlRecordByProviderReferenceIdAsync(
                    payload.ProviderReferenceId);

                if (record == null)
                {
                    _logger.LogWarning("AML record not found for ProviderRefId={ProviderRefId}",
                        LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId));
                    return false;
                }

                var (newStatus, newRiskLevel) = ParseWebhookStatus(payload.Status, payload.RiskLevel);
                record.Status = newStatus;
                record.RiskLevel = newRiskLevel;
                record.ReasonCode = payload.ReasonCode;

                if (newStatus == AmlScreeningStatus.Cleared)
                {
                    record.NextScreeningDue = DateTime.UtcNow.AddDays(RescreeningIntervalDays);
                }

                await _amlRepository.UpdateAmlRecordAsync(record);

                _logger.LogInformation(
                    "AML record updated via webhook. AmlId={AmlId}, NewStatus={NewStatus}, RiskLevel={RiskLevel}",
                    LoggingHelper.SanitizeLogInput(record.AmlId),
                    newStatus,
                    newRiskLevel);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling AML webhook. ProviderRefId={ProviderRefId}",
                    LoggingHelper.SanitizeLogInput(payload?.ProviderReferenceId ?? "unknown"));
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<AmlReportResponse> GenerateReportAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Generating AML compliance report for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var history = await _amlRepository.GetAmlRecordsByUserIdAsync(userId);
                var latest = history.FirstOrDefault();

                var summary = latest == null
                    ? "No AML screening has been performed for this user."
                    : BuildComplianceSummary(latest);

                return new AmlReportResponse
                {
                    Success = true,
                    UserId = userId,
                    GeneratedAt = DateTime.UtcNow,
                    LatestRecord = latest,
                    ScreeningHistory = history,
                    ComplianceSummary = summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AML report for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return new AmlReportResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An internal error occurred while generating the AML report"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsUserClearedAsync(string userId)
        {
            try
            {
                var record = await _amlRepository.GetAmlRecordByUserIdAsync(userId);
                return record?.Status == AmlScreeningStatus.Cleared;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AML clearance. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<int> AnonymizeUserAmlDataAsync(string userId)
        {
            try
            {
                var records = await _amlRepository.GetAmlRecordsByUserIdAsync(userId);

                foreach (var record in records)
                {
                    record.Notes = "[GDPR_ANONYMIZED]";
                    record.ReasonCode = record.ReasonCode != null ? "[ANONYMIZED]" : null;
                    record.Metadata = new Dictionary<string, string>
                    {
                        ["gdpr_erased_at"] = DateTime.UtcNow.ToString("O")
                    };
                    await _amlRepository.UpdateAmlRecordAsync(record);
                }

                return records.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error anonymizing AML data for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return 0;
            }
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private static (AmlScreeningStatus status, AmlRiskLevel riskLevel) MapDecisionState(
            ComplianceDecisionState state, string? reasonCode)
        {
            return state switch
            {
                ComplianceDecisionState.Approved => (AmlScreeningStatus.Cleared, AmlRiskLevel.Low),
                ComplianceDecisionState.Rejected when reasonCode == "SANCTIONS_MATCH"
                    => (AmlScreeningStatus.SanctionsMatch, AmlRiskLevel.High),
                ComplianceDecisionState.Rejected when reasonCode == "PEP_MATCH"
                    => (AmlScreeningStatus.PepMatch, AmlRiskLevel.High),
                ComplianceDecisionState.Rejected => (AmlScreeningStatus.SanctionsMatch, AmlRiskLevel.High),
                ComplianceDecisionState.NeedsReview => (AmlScreeningStatus.NeedsReview, AmlRiskLevel.Medium),
                ComplianceDecisionState.Error => (AmlScreeningStatus.Error, AmlRiskLevel.Unknown),
                _ => (AmlScreeningStatus.Pending, AmlRiskLevel.Unknown)
            };
        }

        private static (AmlScreeningStatus status, AmlRiskLevel riskLevel) ParseWebhookStatus(
            string statusString, string riskLevelString)
        {
            var status = statusString.ToUpperInvariant() switch
            {
                "CLEARED" or "APPROVED" or "PASS" => AmlScreeningStatus.Cleared,
                "SANCTIONS_MATCH" or "REJECTED" or "BLOCKED" => AmlScreeningStatus.SanctionsMatch,
                "PEP_MATCH" => AmlScreeningStatus.PepMatch,
                "NEEDS_REVIEW" or "REVIEW_REQUIRED" or "MANUAL_REVIEW" => AmlScreeningStatus.NeedsReview,
                "ERROR" or "FAILED" => AmlScreeningStatus.Error,
                _ => AmlScreeningStatus.Pending
            };

            var riskLevel = riskLevelString?.ToUpperInvariant() switch
            {
                "LOW" => AmlRiskLevel.Low,
                "MEDIUM" or "MED" => AmlRiskLevel.Medium,
                "HIGH" => AmlRiskLevel.High,
                _ => AmlRiskLevel.Unknown
            };

            return (status, riskLevel);
        }

        private static string BuildComplianceSummary(AmlRecord record)
        {
            return record.Status switch
            {
                AmlScreeningStatus.Cleared =>
                    $"User cleared AML screening with {record.RiskLevel} risk. Last screened on {record.UpdatedAt:yyyy-MM-dd}. Next re-screening due: {record.NextScreeningDue?.ToString("yyyy-MM-dd") ?? "N/A"}.",
                AmlScreeningStatus.SanctionsMatch =>
                    "User matched a sanctions list. Token creation is blocked pending compliance review.",
                AmlScreeningStatus.PepMatch =>
                    "User identified as a Politically Exposed Person. Manual compliance review required before proceeding.",
                AmlScreeningStatus.NeedsReview =>
                    "AML screening result requires manual review by the compliance team.",
                AmlScreeningStatus.Error =>
                    "AML screening encountered a provider error. Re-screening is recommended.",
                _ =>
                    "AML screening is pending or incomplete."
            };
        }
    }
}

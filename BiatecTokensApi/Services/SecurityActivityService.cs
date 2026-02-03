using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service implementation for security activity and audit operations
    /// </summary>
    /// <remarks>
    /// Provides business logic for security activity tracking, audit trail export,
    /// and recovery guidance. Implements idempotency for export operations and
    /// subscription-based quota management.
    /// </remarks>
    public class SecurityActivityService : ISecurityActivityService
    {
        private readonly ISecurityActivityRepository _repository;
        private readonly IDeploymentStatusRepository _deploymentStatusRepository;
        private readonly ILogger<SecurityActivityService> _logger;
        private const int MaxExportRecords = 10000;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityActivityService"/> class.
        /// </summary>
        /// <param name="repository">The security activity repository</param>
        /// <param name="deploymentStatusRepository">The deployment status repository</param>
        /// <param name="logger">The logger instance</param>
        public SecurityActivityService(
            ISecurityActivityRepository repository,
            IDeploymentStatusRepository deploymentStatusRepository,
            ILogger<SecurityActivityService> logger)
        {
            _repository = repository;
            _deploymentStatusRepository = deploymentStatusRepository;
            _logger = logger;
        }

        /// <summary>
        /// Logs a security activity event
        /// </summary>
        public async Task LogEventAsync(SecurityActivityEvent @event)
        {
            await _repository.LogEventAsync(@event);
            _logger.LogInformation("Security event logged: {EventType} for account {AccountId}",
                @event.EventType,
                LoggingHelper.SanitizeLogInput(@event.AccountId));
        }

        /// <summary>
        /// Gets security activity events with filtering and pagination
        /// </summary>
        public async Task<SecurityActivityResponse> GetActivityAsync(GetSecurityActivityRequest request)
        {
            _logger.LogInformation("Retrieving security activity: AccountId={AccountId}, Page={Page}",
                LoggingHelper.SanitizeLogInput(request.AccountId),
                request.Page);

            try
            {
                // Validate and cap page size
                if (request.PageSize < 1)
                    request.PageSize = 50;
                if (request.PageSize > 100)
                    request.PageSize = 100;

                if (request.Page < 1)
                    request.Page = 1;

                // Get events and count
                var events = await _repository.GetActivityEventsAsync(request);
                var totalCount = await _repository.GetActivityEventsCountAsync(request);

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                var response = new SecurityActivityResponse
                {
                    Success = true,
                    Events = events,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };

                _logger.LogInformation("Retrieved {Count} security activity events (page {Page} of {TotalPages})",
                    events.Count, request.Page, totalPages);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving security activity");
                return new SecurityActivityResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = $"Error retrieving security activity: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets token deployment transaction history
        /// </summary>
        public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(GetTransactionHistoryRequest request)
        {
            _logger.LogInformation("Retrieving transaction history: AccountId={AccountId}, Network={Network}, Page={Page}",
                LoggingHelper.SanitizeLogInput(request.AccountId),
                LoggingHelper.SanitizeLogInput(request.Network),
                request.Page);

            try
            {
                // Validate and cap page size
                if (request.PageSize < 1)
                    request.PageSize = 50;
                if (request.PageSize > 100)
                    request.PageSize = 100;

                if (request.Page < 1)
                    request.Page = 1;

                // Get transactions and count
                var transactions = await _repository.GetTransactionHistoryAsync(request);
                var totalCount = await _repository.GetTransactionHistoryCountAsync(request);

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                var response = new TransactionHistoryResponse
                {
                    Success = true,
                    Transactions = transactions,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };

                _logger.LogInformation("Retrieved {Count} transactions (page {Page} of {TotalPages})",
                    transactions.Count, request.Page, totalPages);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction history");
                return new TransactionHistoryResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = $"Error retrieving transaction history: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Exports audit trail in specified format (CSV or JSON)
        /// </summary>
        public async Task<(ExportAuditTrailResponse Response, string? Content)> ExportAuditTrailAsync(ExportAuditTrailRequest request, string accountId)
        {
            _logger.LogInformation("Exporting audit trail: Format={Format}, AccountId={AccountId}",
                LoggingHelper.SanitizeLogInput(request.Format),
                LoggingHelper.SanitizeLogInput(accountId));

            try
            {
                // Validate format
                if (request.Format != "csv" && request.Format != "json")
                {
                    return (new ExportAuditTrailResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_EXPORT_FORMAT,
                        ErrorMessage = "Invalid export format. Supported formats: csv, json",
                        RemediationHint = "Use 'csv' or 'json' as the format parameter."
                    }, null);
                }

                // Check idempotency
                if (!string.IsNullOrEmpty(request.IdempotencyKey))
                {
                    var cachedExport = await _repository.GetCachedExportAsync(request.IdempotencyKey, accountId);
                    if (cachedExport != null)
                    {
                        _logger.LogInformation("Returning cached export for idempotency key: {IdempotencyKey}",
                            LoggingHelper.SanitizeLogInput(request.IdempotencyKey));
                        cachedExport.IdempotencyHit = true;
                        return (cachedExport, null);
                    }
                }

                // Get activity events for export
                var activityRequest = new GetSecurityActivityRequest
                {
                    AccountId = request.AccountId ?? accountId,
                    EventType = request.EventType,
                    Severity = request.Severity,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var events = await _repository.GetActivityEventsAsync(activityRequest);

                // Check quota (basic implementation - can be enhanced with subscription tier checking)
                var quota = GetExportQuota(accountId);
                if (quota.ExportsUsed >= quota.MaxExportsPerMonth)
                {
                    return (new ExportAuditTrailResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.EXPORT_QUOTA_EXCEEDED,
                        ErrorMessage = "Export quota exceeded for this month",
                        RemediationHint = "Upgrade your subscription plan to increase export limits.",
                        Quota = quota
                    }, null);
                }

                // Generate export content
                string? content = null;
                if (request.Format == "csv")
                {
                    content = GenerateCsv(events);
                }
                else if (request.Format == "json")
                {
                    content = GenerateJson(events);
                }

                var response = new ExportAuditTrailResponse
                {
                    Success = true,
                    Status = "completed",
                    Format = request.Format,
                    RecordCount = events.Count,
                    IdempotencyHit = false,
                    Quota = quota
                };

                // Cache the export if idempotency key is provided
                if (!string.IsNullOrEmpty(request.IdempotencyKey))
                {
                    await _repository.CacheExportAsync(request.IdempotencyKey, accountId, response);
                }

                // Log the export event
                await LogEventAsync(new SecurityActivityEvent
                {
                    AccountId = accountId,
                    EventType = SecurityEventType.AuditExport,
                    Severity = EventSeverity.Info,
                    Summary = $"Audit trail exported in {request.Format} format with {events.Count} records",
                    Success = true,
                    Metadata = new Dictionary<string, object>
                    {
                        { "format", request.Format },
                        { "recordCount", events.Count },
                        { "fromDate", request.FromDate?.ToString("o") ?? "N/A" },
                        { "toDate", request.ToDate?.ToString("o") ?? "N/A" }
                    }
                });

                _logger.LogInformation("Exported {Count} audit trail records as {Format}",
                    events.Count, request.Format);

                return (response, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit trail");
                return (new ExportAuditTrailResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = $"Error exporting audit trail: {ex.Message}"
                }, null);
            }
        }

        /// <summary>
        /// Gets recovery guidance for the account
        /// </summary>
        public async Task<RecoveryGuidanceResponse> GetRecoveryGuidanceAsync(string accountId)
        {
            _logger.LogInformation("Getting recovery guidance for account: {AccountId}",
                LoggingHelper.SanitizeLogInput(accountId));

            try
            {
                // This is a basic implementation - can be enhanced with actual recovery state checking
                var response = new RecoveryGuidanceResponse
                {
                    Success = true,
                    Eligibility = RecoveryEligibility.Eligible,
                    CooldownRemaining = 0,
                    Steps = new List<RecoveryStep>
                    {
                        new RecoveryStep
                        {
                            StepNumber = 1,
                            Title = "Verify Identity",
                            Instructions = "Verify your identity using your registered email or authentication method.",
                            Completed = false
                        },
                        new RecoveryStep
                        {
                            StepNumber = 2,
                            Title = "Confirm Recovery Request",
                            Instructions = "Confirm that you want to initiate account recovery. This will send a recovery link to your registered email.",
                            Completed = false
                        },
                        new RecoveryStep
                        {
                            StepNumber = 3,
                            Title = "Check Recovery Email",
                            Instructions = "Check your email inbox for the recovery link. The link will be valid for 24 hours.",
                            Completed = false
                        },
                        new RecoveryStep
                        {
                            StepNumber = 4,
                            Title = "Reset Access",
                            Instructions = "Follow the link in the email to reset your account access and set up new credentials.",
                            Completed = false
                        }
                    },
                    Notes = "Recovery process typically takes 5-10 minutes. If you don't receive the email, check your spam folder or contact support."
                };

                // Log the recovery guidance request
                await LogEventAsync(new SecurityActivityEvent
                {
                    AccountId = accountId,
                    EventType = SecurityEventType.Recovery,
                    Severity = EventSeverity.Info,
                    Summary = "Recovery guidance requested",
                    Success = true
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recovery guidance");
                return new RecoveryGuidanceResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = $"Error getting recovery guidance: {ex.Message}",
                    Eligibility = RecoveryEligibility.NotConfigured
                };
            }
        }

        /// <summary>
        /// Generates CSV content from security activity events
        /// </summary>
        private string GenerateCsv(List<SecurityActivityEvent> events)
        {
            var csv = new StringBuilder();

            // CSV Header
            csv.AppendLine("EventId,AccountId,EventType,Severity,Timestamp,Summary,Success,ErrorMessage,CorrelationId,SourceIp,UserAgent");

            // CSV Rows
            foreach (var evt in events)
            {
                csv.AppendLine(
                    $"{EscapeCsv(evt.EventId)}," +
                    $"{EscapeCsv(evt.AccountId)}," +
                    $"{EscapeCsv(evt.EventType.ToString())}," +
                    $"{EscapeCsv(evt.Severity.ToString())}," +
                    $"{EscapeCsv(evt.Timestamp.ToString("o"))}," +
                    $"{EscapeCsv(evt.Summary)}," +
                    $"{evt.Success}," +
                    $"{EscapeCsv(evt.ErrorMessage)}," +
                    $"{EscapeCsv(evt.CorrelationId)}," +
                    $"{EscapeCsv(evt.SourceIp)}," +
                    $"{EscapeCsv(evt.UserAgent)}"
                );
            }

            return csv.ToString();
        }

        /// <summary>
        /// Generates JSON content from security activity events
        /// </summary>
        private string GenerateJson(List<SecurityActivityEvent> events)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var exportData = new
            {
                exportedAt = DateTime.UtcNow,
                recordCount = events.Count,
                events = events
            };

            return JsonSerializer.Serialize(exportData, options);
        }

        /// <summary>
        /// Escapes a value for CSV format
        /// </summary>
        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Escape double quotes by doubling them
            var escaped = value.Replace("\"", "\"\"");

            // Wrap in quotes if contains comma, newline, or quote
            if (escaped.Contains(',') || escaped.Contains('\n') || escaped.Contains('\r') || escaped.Contains('"'))
            {
                return $"\"{escaped}\"";
            }

            return escaped;
        }

        /// <summary>
        /// Gets export quota for the account (basic implementation)
        /// </summary>
        private ExportQuota GetExportQuota(string accountId)
        {
            // Basic implementation - can be enhanced with subscription tier checking
            return new ExportQuota
            {
                MaxExportsPerMonth = 50,
                ExportsUsed = 0,
                ExportsRemaining = 50,
                MaxRecordsPerExport = MaxExportRecords
            };
        }
    }
}

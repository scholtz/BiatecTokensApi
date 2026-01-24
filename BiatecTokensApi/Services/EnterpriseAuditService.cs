using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service implementation for enterprise audit log operations
    /// </summary>
    /// <remarks>
    /// Provides business logic for retrieving and exporting unified audit logs
    /// across whitelist/blacklist and compliance systems for MICA reporting.
    /// Supports 7-year retention requirements and comprehensive filtering.
    /// </remarks>
    public class EnterpriseAuditService : IEnterpriseAuditService
    {
        private readonly IEnterpriseAuditRepository _repository;
        private readonly ILogger<EnterpriseAuditService> _logger;
        private readonly IWebhookService _webhookService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseAuditService"/> class.
        /// </summary>
        /// <param name="repository">The enterprise audit repository</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="webhookService">The webhook service</param>
        public EnterpriseAuditService(
            IEnterpriseAuditRepository repository,
            ILogger<EnterpriseAuditService> logger,
            IWebhookService webhookService)
        {
            _repository = repository;
            _logger = logger;
            _webhookService = webhookService;
        }

        /// <summary>
        /// Gets unified audit log entries with comprehensive filtering and pagination
        /// </summary>
        public async Task<EnterpriseAuditLogResponse> GetAuditLogAsync(GetEnterpriseAuditLogRequest request)
        {
            _logger.LogInformation("Retrieving enterprise audit log: AssetId={AssetId}, Network={Network}, Page={Page}",
                request.AssetId, request.Network, request.Page);

            try
            {
                // Validate and cap page size
                if (request.PageSize < 1)
                    request.PageSize = 50;
                if (request.PageSize > 100)
                    request.PageSize = 100;

                if (request.Page < 1)
                    request.Page = 1;

                // Get entries and count
                var entries = await _repository.GetAuditLogAsync(request);
                var totalCount = await _repository.GetAuditLogCountAsync(request);
                var summary = await _repository.GetAuditLogSummaryAsync(request);

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                var response = new EnterpriseAuditLogResponse
                {
                    Success = true,
                    Entries = entries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    RetentionPolicy = GetRetentionPolicy(),
                    Summary = summary
                };

                _logger.LogInformation("Retrieved {Count} enterprise audit entries (page {Page} of {TotalPages})",
                    entries.Count, request.Page, totalPages);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enterprise audit log");
                return new EnterpriseAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Error retrieving audit log: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Exports audit log entries as CSV for MICA compliance reporting
        /// </summary>
        public async Task<string> ExportAuditLogCsvAsync(GetEnterpriseAuditLogRequest request, int maxRecords = 10000)
        {
            _logger.LogInformation("Exporting enterprise audit log as CSV: AssetId={AssetId}, Network={Network}, MaxRecords={MaxRecords}",
                request.AssetId, request.Network, maxRecords);

            // Set page size to maxRecords to get all records in one request
            request.Page = 1;
            request.PageSize = Math.Min(maxRecords, 10000);

            var entries = await _repository.GetAuditLogAsync(request);

            var csv = new StringBuilder();
            
            // CSV Header
            csv.AppendLine("Id,AssetId,Network,Category,ActionType,PerformedBy,PerformedAt,Success,ErrorMessage,AffectedAddress,OldStatus,NewStatus,Notes,ToAddress,TransferAllowed,DenialReason,Amount,Role,ItemCount,SourceSystem,CorrelationId");

            // CSV Rows
            foreach (var entry in entries)
            {
                csv.AppendLine(
                    $"{EscapeCsv(entry.Id)}," +
                    $"{EscapeCsv(entry.AssetId?.ToString())}," +
                    $"{EscapeCsv(entry.Network)}," +
                    $"{EscapeCsv(entry.Category.ToString())}," +
                    $"{EscapeCsv(entry.ActionType)}," +
                    $"{EscapeCsv(entry.PerformedBy)}," +
                    $"{EscapeCsv(entry.PerformedAt.ToString("o"))}," +
                    $"{entry.Success}," +
                    $"{EscapeCsv(entry.ErrorMessage)}," +
                    $"{EscapeCsv(entry.AffectedAddress)}," +
                    $"{EscapeCsv(entry.OldStatus)}," +
                    $"{EscapeCsv(entry.NewStatus)}," +
                    $"{EscapeCsv(entry.Notes)}," +
                    $"{EscapeCsv(entry.ToAddress)}," +
                    $"{EscapeCsv(entry.TransferAllowed?.ToString())}," +
                    $"{EscapeCsv(entry.DenialReason)}," +
                    $"{EscapeCsv(entry.Amount?.ToString())}," +
                    $"{EscapeCsv(entry.Role)}," +
                    $"{EscapeCsv(entry.ItemCount?.ToString())}," +
                    $"{EscapeCsv(entry.SourceSystem)}," +
                    $"{EscapeCsv(entry.CorrelationId)}"
                );
            }

            _logger.LogInformation("Exported {Count} enterprise audit entries as CSV", entries.Count);
            
            // Emit webhook event for audit export creation
            _ = Task.Run(async () =>
            {
                try
                {
                    await _webhookService.EmitEventAsync(new WebhookEvent
                    {
                        EventType = WebhookEventType.AuditExportCreated,
                        AssetId = request.AssetId,
                        Network = request.Network,
                        Actor = "SYSTEM",
                        Timestamp = DateTime.UtcNow,
                        Data = new Dictionary<string, object>
                        {
                            { "format", "CSV" },
                            { "recordCount", entries.Count },
                            { "category", request.Category?.ToString() ?? "All" },
                            { "fromDate", request.FromDate?.ToString("o") ?? "N/A" },
                            { "toDate", request.ToDate?.ToString("o") ?? "N/A" }
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to emit webhook event for CSV audit export");
                }
            });
            
            return csv.ToString();
        }

        /// <summary>
        /// Exports audit log entries as JSON for MICA compliance reporting
        /// </summary>
        public async Task<string> ExportAuditLogJsonAsync(GetEnterpriseAuditLogRequest request, int maxRecords = 10000)
        {
            _logger.LogInformation("Exporting enterprise audit log as JSON: AssetId={AssetId}, Network={Network}, MaxRecords={MaxRecords}",
                request.AssetId, request.Network, maxRecords);

            // Set page size to maxRecords to get all records in one request
            request.Page = 1;
            request.PageSize = Math.Min(maxRecords, 10000);

            var response = await GetAuditLogAsync(request);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(response, options);

            _logger.LogInformation("Exported {Count} enterprise audit entries as JSON", response.Entries.Count);
            
            // Emit webhook event for audit export creation
            _ = Task.Run(async () =>
            {
                try
                {
                    await _webhookService.EmitEventAsync(new WebhookEvent
                    {
                        EventType = WebhookEventType.AuditExportCreated,
                        AssetId = request.AssetId,
                        Network = request.Network,
                        Actor = "SYSTEM",
                        Timestamp = DateTime.UtcNow,
                        Data = new Dictionary<string, object>
                        {
                            { "format", "JSON" },
                            { "recordCount", response.Entries.Count },
                            { "category", request.Category?.ToString() ?? "All" },
                            { "fromDate", request.FromDate?.ToString("o") ?? "N/A" },
                            { "toDate", request.ToDate?.ToString("o") ?? "N/A" }
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to emit webhook event for JSON audit export");
                }
            });
            
            return json;
        }

        /// <summary>
        /// Gets the 7-year MICA retention policy for audit logs
        /// </summary>
        public AuditRetentionPolicy GetRetentionPolicy()
        {
            return new AuditRetentionPolicy
            {
                MinimumRetentionYears = 7,
                RegulatoryFramework = "MICA",
                ImmutableEntries = true,
                Description = "Audit logs are retained for a minimum of 7 years to comply with MICA (Markets in Crypto-Assets Regulation) and other regulatory requirements. All entries are immutable and cannot be modified or deleted. This unified audit log includes whitelist, blacklist, and compliance events across all blockchain networks including VOI and Aramid."
            };
        }

        /// <summary>
        /// Escapes a value for CSV format
        /// </summary>
        /// <param name="value">The value to escape</param>
        /// <returns>Escaped CSV value</returns>
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
    }
}

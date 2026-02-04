using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for exporting deployment audit trails in various formats
    /// </summary>
    /// <remarks>
    /// Provides export functionality for compliance reporting and regulatory requirements.
    /// Supports JSON and CSV formats with idempotent operations to handle large exports safely.
    /// </remarks>
    public class DeploymentAuditService : IDeploymentAuditService
    {
        private readonly IDeploymentStatusRepository _repository;
        private readonly ILogger<DeploymentAuditService> _logger;
        private readonly Dictionary<string, AuditExportCache> _exportCache = new();
        private readonly object _cacheLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentAuditService"/> class.
        /// </summary>
        /// <param name="repository">The deployment status repository</param>
        /// <param name="logger">The logger instance</param>
        public DeploymentAuditService(
            IDeploymentStatusRepository repository,
            ILogger<DeploymentAuditService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Exports audit trail for a specific deployment as JSON
        /// </summary>
        public async Task<string> ExportAuditTrailAsJsonAsync(string deploymentId)
        {
            _logger.LogInformation("Exporting audit trail as JSON: DeploymentId={DeploymentId}", deploymentId);

            var deployment = await _repository.GetDeploymentByIdAsync(deploymentId);
            if (deployment == null)
            {
                throw new InvalidOperationException($"Deployment {deploymentId} not found");
            }

            var history = await _repository.GetStatusHistoryAsync(deploymentId);

            var auditTrail = new DeploymentAuditTrail
            {
                DeploymentId = deployment.DeploymentId,
                TokenType = deployment.TokenType,
                TokenName = deployment.TokenName,
                TokenSymbol = deployment.TokenSymbol,
                Network = deployment.Network,
                DeployedBy = deployment.DeployedBy,
                AssetIdentifier = deployment.AssetIdentifier,
                TransactionHash = deployment.TransactionHash,
                CurrentStatus = deployment.CurrentStatus,
                CreatedAt = deployment.CreatedAt,
                UpdatedAt = deployment.UpdatedAt,
                StatusHistory = history,
                ComplianceSummary = BuildComplianceSummary(history),
                TotalDurationMs = CalculateTotalDuration(history),
                ErrorSummary = deployment.ErrorMessage
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(auditTrail, options);
            _logger.LogInformation("Exported audit trail: DeploymentId={DeploymentId}, Size={Size} bytes",
                deploymentId, json.Length);

            return json;
        }

        /// <summary>
        /// Exports audit trail for a specific deployment as CSV
        /// </summary>
        public async Task<string> ExportAuditTrailAsCsvAsync(string deploymentId)
        {
            _logger.LogInformation("Exporting audit trail as CSV: DeploymentId={DeploymentId}", deploymentId);

            var deployment = await _repository.GetDeploymentByIdAsync(deploymentId);
            if (deployment == null)
            {
                throw new InvalidOperationException($"Deployment {deploymentId} not found");
            }

            var history = await _repository.GetStatusHistoryAsync(deploymentId);

            var csv = new StringBuilder();
            
            // Header row
            csv.AppendLine("DeploymentId,TokenType,TokenName,TokenSymbol,Network,DeployedBy,AssetIdentifier,TransactionHash,Status,Timestamp,Message,ReasonCode,ActorAddress,ConfirmedRound,ErrorMessage,DurationFromPreviousMs");

            // Data rows
            foreach (var entry in history)
            {
                csv.AppendLine($"\"{EscapeCsv(deployment.DeploymentId)}\"," +
                    $"\"{EscapeCsv(deployment.TokenType)}\"," +
                    $"\"{EscapeCsv(deployment.TokenName)}\"," +
                    $"\"{EscapeCsv(deployment.TokenSymbol)}\"," +
                    $"\"{EscapeCsv(deployment.Network)}\"," +
                    $"\"{EscapeCsv(deployment.DeployedBy)}\"," +
                    $"\"{EscapeCsv(deployment.AssetIdentifier)}\"," +
                    $"\"{EscapeCsv(entry.TransactionHash)}\"," +
                    $"\"{entry.Status}\"," +
                    $"\"{entry.Timestamp:O}\"," +
                    $"\"{EscapeCsv(entry.Message)}\"," +
                    $"\"{EscapeCsv(entry.ReasonCode)}\"," +
                    $"\"{EscapeCsv(entry.ActorAddress)}\"," +
                    $"\"{entry.ConfirmedRound}\"," +
                    $"\"{EscapeCsv(entry.ErrorMessage)}\"," +
                    $"\"{entry.DurationFromPreviousStatusMs}\"");
            }

            var csvString = csv.ToString();
            _logger.LogInformation("Exported audit trail as CSV: DeploymentId={DeploymentId}, Size={Size} bytes",
                deploymentId, csvString.Length);

            return csvString;
        }

        /// <summary>
        /// Exports audit trails for multiple deployments with idempotency support
        /// </summary>
        /// <param name="request">Export request with filters</param>
        /// <param name="idempotencyKey">Optional idempotency key for large exports</param>
        /// <returns>Export result with data or cache reference</returns>
        public async Task<AuditExportResult> ExportAuditTrailsAsync(
            AuditExportRequest request,
            string? idempotencyKey = null)
        {
            // Check cache if idempotency key provided
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                lock (_cacheLock)
                {
                    if (_exportCache.TryGetValue(idempotencyKey, out var cached))
                    {
                        if (cached.ExpiresAt > DateTime.UtcNow)
                        {
                            // Verify request matches cached request
                            if (AreRequestsEquivalent(cached.Request, request))
                            {
                                _logger.LogInformation("Returning cached export: IdempotencyKey={Key}", idempotencyKey);
                                return new AuditExportResult
                                {
                                    Success = true,
                                    Data = cached.Data,
                                    Format = cached.Format,
                                    RecordCount = cached.RecordCount,
                                    IsCached = true,
                                    GeneratedAt = cached.GeneratedAt
                                };
                            }
                            else
                            {
                                _logger.LogWarning("Idempotency key reused with different request parameters: Key={Key}", idempotencyKey);
                                return new AuditExportResult
                                {
                                    Success = false,
                                    ErrorMessage = "Idempotency key already used with different request parameters"
                                };
                            }
                        }
                        else
                        {
                            // Expired, remove from cache
                            _exportCache.Remove(idempotencyKey);
                        }
                    }
                }
            }

            // Validate request
            if (request.PageSize < 1 || request.PageSize > 1000)
            {
                return new AuditExportResult
                {
                    Success = false,
                    ErrorMessage = "Page size must be between 1 and 1000"
                };
            }

            // Get deployments
            var listRequest = new ListDeploymentsRequest
            {
                DeployedBy = request.DeployedBy,
                Network = request.Network,
                TokenType = request.TokenType,
                Status = request.Status,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Page = request.Page,
                PageSize = request.PageSize
            };

            var deployments = await _repository.GetDeploymentsAsync(listRequest);

            string data;
            if (request.Format == AuditExportFormat.Json)
            {
                data = await ExportMultipleDeploymentsAsJsonAsync(deployments);
            }
            else
            {
                data = await ExportMultipleDeploymentsAsCsvAsync(deployments);
            }

            var result = new AuditExportResult
            {
                Success = true,
                Data = data,
                Format = request.Format,
                RecordCount = deployments.Count,
                GeneratedAt = DateTime.UtcNow
            };

            // Cache if idempotency key provided
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                lock (_cacheLock)
                {
                    _exportCache[idempotencyKey] = new AuditExportCache
                    {
                        Request = request,
                        Data = data,
                        Format = request.Format,
                        RecordCount = deployments.Count,
                        GeneratedAt = result.GeneratedAt,
                        ExpiresAt = DateTime.UtcNow.AddHours(1) // Cache for 1 hour
                    };
                }
                _logger.LogInformation("Cached export result: IdempotencyKey={Key}", idempotencyKey);
            }

            _logger.LogInformation("Exported audit trails: Count={Count}, Format={Format}",
                deployments.Count, request.Format);

            return result;
        }

        private async Task<string> ExportMultipleDeploymentsAsJsonAsync(List<TokenDeployment> deployments)
        {
            var auditTrails = new List<DeploymentAuditTrail>();

            foreach (var deployment in deployments)
            {
                var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);
                auditTrails.Add(new DeploymentAuditTrail
                {
                    DeploymentId = deployment.DeploymentId,
                    TokenType = deployment.TokenType,
                    TokenName = deployment.TokenName,
                    TokenSymbol = deployment.TokenSymbol,
                    Network = deployment.Network,
                    DeployedBy = deployment.DeployedBy,
                    AssetIdentifier = deployment.AssetIdentifier,
                    TransactionHash = deployment.TransactionHash,
                    CurrentStatus = deployment.CurrentStatus,
                    CreatedAt = deployment.CreatedAt,
                    UpdatedAt = deployment.UpdatedAt,
                    StatusHistory = history,
                    ComplianceSummary = BuildComplianceSummary(history),
                    TotalDurationMs = CalculateTotalDuration(history),
                    ErrorSummary = deployment.ErrorMessage
                });
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(auditTrails, options);
        }

        private async Task<string> ExportMultipleDeploymentsAsCsvAsync(List<TokenDeployment> deployments)
        {
            var csv = new StringBuilder();
            
            // Header row
            csv.AppendLine("DeploymentId,TokenType,TokenName,TokenSymbol,Network,DeployedBy,AssetIdentifier,TransactionHash,Status,Timestamp,Message,ReasonCode,ActorAddress,ConfirmedRound,ErrorMessage,DurationFromPreviousMs");

            foreach (var deployment in deployments)
            {
                var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);
                
                foreach (var entry in history)
                {
                    csv.AppendLine($"\"{EscapeCsv(deployment.DeploymentId)}\"," +
                        $"\"{EscapeCsv(deployment.TokenType)}\"," +
                        $"\"{EscapeCsv(deployment.TokenName)}\"," +
                        $"\"{EscapeCsv(deployment.TokenSymbol)}\"," +
                        $"\"{EscapeCsv(deployment.Network)}\"," +
                        $"\"{EscapeCsv(deployment.DeployedBy)}\"," +
                        $"\"{EscapeCsv(deployment.AssetIdentifier)}\"," +
                        $"\"{EscapeCsv(entry.TransactionHash)}\"," +
                        $"\"{entry.Status}\"," +
                        $"\"{entry.Timestamp:O}\"," +
                        $"\"{EscapeCsv(entry.Message)}\"," +
                        $"\"{EscapeCsv(entry.ReasonCode)}\"," +
                        $"\"{EscapeCsv(entry.ActorAddress)}\"," +
                        $"\"{entry.ConfirmedRound}\"," +
                        $"\"{EscapeCsv(entry.ErrorMessage)}\"," +
                        $"\"{entry.DurationFromPreviousStatusMs}\"");
                }
            }

            return csv.ToString();
        }

        private string BuildComplianceSummary(List<DeploymentStatusEntry> history)
        {
            var complianceChecks = history
                .SelectMany(e => e.ComplianceChecks ?? new List<ComplianceCheckResult>())
                .ToList();

            if (!complianceChecks.Any())
            {
                return "No compliance checks performed";
            }

            var passed = complianceChecks.Count(c => c.Passed);
            var failed = complianceChecks.Count(c => !c.Passed);

            return $"{passed} passed, {failed} failed out of {complianceChecks.Count} checks";
        }

        private long CalculateTotalDuration(List<DeploymentStatusEntry> history)
        {
            if (history.Count < 2)
            {
                return 0;
            }

            var first = history.OrderBy(e => e.Timestamp).First();
            var last = history.OrderBy(e => e.Timestamp).Last();

            return (long)(last.Timestamp - first.Timestamp).TotalMilliseconds;
        }

        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Escape double quotes and remove newlines
            return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }

        private bool AreRequestsEquivalent(AuditExportRequest req1, AuditExportRequest req2)
        {
            return req1.DeployedBy == req2.DeployedBy &&
                   req1.Network == req2.Network &&
                   req1.TokenType == req2.TokenType &&
                   req1.Status == req2.Status &&
                   req1.FromDate == req2.FromDate &&
                   req1.ToDate == req2.ToDate &&
                   req1.Page == req2.Page &&
                   req1.PageSize == req2.PageSize &&
                   req1.Format == req2.Format;
        }

        private class AuditExportCache
        {
            public AuditExportRequest Request { get; set; } = null!;
            public string Data { get; set; } = string.Empty;
            public AuditExportFormat Format { get; set; }
            public int RecordCount { get; set; }
            public DateTime GeneratedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}

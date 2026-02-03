using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository implementation for compliance reports
    /// </summary>
    /// <remarks>
    /// Uses thread-safe concurrent collections for MVP implementation.
    /// Can be replaced with database-backed implementation without changing the interface.
    /// </remarks>
    public class ComplianceReportRepository : IComplianceReportRepository
    {
        private readonly ConcurrentDictionary<string, ComplianceReport> _reports = new();
        private readonly ILogger<ComplianceReportRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceReportRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public ComplianceReportRepository(ILogger<ComplianceReportRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<ComplianceReport> CreateReportAsync(ComplianceReport report)
        {
            if (string.IsNullOrEmpty(report.ReportId))
            {
                report.ReportId = Guid.NewGuid().ToString();
            }

            if (_reports.TryAdd(report.ReportId, report))
            {
                _logger.LogInformation("Created compliance report: ReportId={ReportId}, Type={ReportType}, IssuerId={IssuerId}",
                    report.ReportId, report.ReportType, report.IssuerId);
                return Task.FromResult(report);
            }

            throw new InvalidOperationException($"Report with ID {report.ReportId} already exists");
        }

        /// <inheritdoc/>
        public Task<ComplianceReport> UpdateReportAsync(ComplianceReport report)
        {
            if (!_reports.ContainsKey(report.ReportId))
            {
                throw new InvalidOperationException($"Report with ID {report.ReportId} not found");
            }

            _reports[report.ReportId] = report;
            _logger.LogInformation("Updated compliance report: ReportId={ReportId}, Status={Status}",
                report.ReportId, report.Status);
            return Task.FromResult(report);
        }

        /// <inheritdoc/>
        public Task<ComplianceReport?> GetReportAsync(string reportId, string issuerId)
        {
            if (_reports.TryGetValue(reportId, out var report))
            {
                // Access control: only return report if issuer matches
                if (report.IssuerId == issuerId)
                {
                    _logger.LogDebug("Retrieved compliance report: ReportId={ReportId}, IssuerId={IssuerId}",
                        LoggingHelper.SanitizeLogInput(reportId), LoggingHelper.SanitizeLogInput(issuerId));
                    return Task.FromResult<ComplianceReport?>(report);
                }

                _logger.LogWarning("Access denied: IssuerId={IssuerId} attempted to access report owned by {OwnerId}",
                    LoggingHelper.SanitizeLogInput(issuerId), LoggingHelper.SanitizeLogInput(report.IssuerId));
                return Task.FromResult<ComplianceReport?>(null);
            }

            _logger.LogDebug("Report not found: ReportId={ReportId}", LoggingHelper.SanitizeLogInput(reportId));
            return Task.FromResult<ComplianceReport?>(null);
        }

        /// <inheritdoc/>
        public Task<List<ComplianceReport>> ListReportsAsync(string issuerId, ListComplianceReportsRequest request)
        {
            // Filter by issuer first (access control)
            var query = _reports.Values.Where(r => r.IssuerId == issuerId);

            // Apply filters
            if (request.ReportType.HasValue)
            {
                query = query.Where(r => r.ReportType == request.ReportType.Value);
            }

            if (request.AssetId.HasValue)
            {
                query = query.Where(r => r.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(r => r.Network == request.Network);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(r => r.Status == request.Status.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt <= request.ToDate.Value);
            }

            // Order by most recent first
            query = query.OrderByDescending(r => r.CreatedAt);

            // Apply pagination
            if (request.PageSize < 1) request.PageSize = 50;
            if (request.PageSize > 100) request.PageSize = 100;
            if (request.Page < 1) request.Page = 1;

            var skip = (request.Page - 1) * request.PageSize;
            var reports = query.Skip(skip).Take(request.PageSize).ToList();

            _logger.LogInformation("Listed {Count} compliance reports for IssuerId={IssuerId} (page {Page})",
                reports.Count, issuerId, request.Page);

            return Task.FromResult(reports);
        }

        /// <inheritdoc/>
        public Task<int> GetReportCountAsync(string issuerId, ListComplianceReportsRequest request)
        {
            // Filter by issuer first (access control)
            var query = _reports.Values.Where(r => r.IssuerId == issuerId);

            // Apply filters
            if (request.ReportType.HasValue)
            {
                query = query.Where(r => r.ReportType == request.ReportType.Value);
            }

            if (request.AssetId.HasValue)
            {
                query = query.Where(r => r.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(r => r.Network == request.Network);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(r => r.Status == request.Status.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt <= request.ToDate.Value);
            }

            var count = query.Count();
            return Task.FromResult(count);
        }
    }
}

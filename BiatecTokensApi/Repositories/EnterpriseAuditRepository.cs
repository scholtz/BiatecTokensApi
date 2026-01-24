using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Repository implementation for enterprise audit log operations
    /// </summary>
    /// <remarks>
    /// Aggregates audit logs from whitelist and compliance repositories into a unified view
    /// for MICA reporting and regulatory compliance.
    /// </remarks>
    public class EnterpriseAuditRepository : IEnterpriseAuditRepository
    {
        private readonly IWhitelistRepository _whitelistRepository;
        private readonly IComplianceRepository _complianceRepository;
        private readonly ILogger<EnterpriseAuditRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseAuditRepository"/> class.
        /// </summary>
        /// <param name="whitelistRepository">The whitelist repository</param>
        /// <param name="complianceRepository">The compliance repository</param>
        /// <param name="logger">The logger instance</param>
        public EnterpriseAuditRepository(
            IWhitelistRepository whitelistRepository,
            IComplianceRepository complianceRepository,
            ILogger<EnterpriseAuditRepository> logger)
        {
            _whitelistRepository = whitelistRepository;
            _complianceRepository = complianceRepository;
            _logger = logger;
        }

        /// <summary>
        /// Gets unified audit log entries from all systems with comprehensive filtering
        /// </summary>
        public async Task<List<EnterpriseAuditLogEntry>> GetAuditLogAsync(GetEnterpriseAuditLogRequest request)
        {
            _logger.LogInformation("Retrieving enterprise audit logs with filters: AssetId={AssetId}, Network={Network}, Category={Category}", 
                request.AssetId, request.Network, request.Category);

            var allEntries = new List<EnterpriseAuditLogEntry>();

            // Get whitelist audit logs if category allows
            if (!request.Category.HasValue || 
                request.Category == AuditEventCategory.Whitelist || 
                request.Category == AuditEventCategory.TransferValidation)
            {
                var whitelistRequest = new GetWhitelistAuditLogRequest
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    Address = request.AffectedAddress,
                    PerformedBy = request.PerformedBy,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue // Get all for aggregation
                };

                var whitelistLogs = await _whitelistRepository.GetAuditLogAsync(whitelistRequest);
                allEntries.AddRange(whitelistLogs.Select(MapWhitelistEntry));
                _logger.LogDebug("Retrieved {Count} whitelist audit entries", whitelistLogs.Count);
            }

            // Get compliance audit logs if category allows
            if (!request.Category.HasValue || 
                request.Category == AuditEventCategory.Compliance || 
                request.Category == AuditEventCategory.Blacklist)
            {
                var complianceRequest = new GetComplianceAuditLogRequest
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    PerformedBy = request.PerformedBy,
                    Success = request.Success,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue // Get all for aggregation
                };

                var complianceLogs = await _complianceRepository.GetAuditLogAsync(complianceRequest);
                allEntries.AddRange(complianceLogs.Select(MapComplianceEntry));
                _logger.LogDebug("Retrieved {Count} compliance audit entries", complianceLogs.Count);
            }

            // Apply additional filters
            var filteredEntries = ApplyFilters(allEntries, request);

            // Order by most recent first
            var orderedEntries = filteredEntries.OrderByDescending(e => e.PerformedAt).ToList();

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var pagedEntries = orderedEntries.Skip(skip).Take(request.PageSize).ToList();

            _logger.LogInformation("Returning {Count} enterprise audit entries (page {Page} of {TotalPages})", 
                pagedEntries.Count, request.Page, (int)Math.Ceiling((double)orderedEntries.Count / request.PageSize));

            return pagedEntries;
        }

        /// <summary>
        /// Gets the total count of audit log entries matching the filter
        /// </summary>
        public async Task<int> GetAuditLogCountAsync(GetEnterpriseAuditLogRequest request)
        {
            _logger.LogInformation("Counting enterprise audit logs with filters");

            var allEntries = new List<EnterpriseAuditLogEntry>();

            // Get whitelist audit logs if category allows
            if (!request.Category.HasValue || 
                request.Category == AuditEventCategory.Whitelist || 
                request.Category == AuditEventCategory.TransferValidation)
            {
                var whitelistRequest = new GetWhitelistAuditLogRequest
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    Address = request.AffectedAddress,
                    PerformedBy = request.PerformedBy,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue
                };

                var whitelistLogs = await _whitelistRepository.GetAuditLogAsync(whitelistRequest);
                allEntries.AddRange(whitelistLogs.Select(MapWhitelistEntry));
            }

            // Get compliance audit logs if category allows
            if (!request.Category.HasValue || 
                request.Category == AuditEventCategory.Compliance || 
                request.Category == AuditEventCategory.Blacklist)
            {
                var complianceRequest = new GetComplianceAuditLogRequest
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    PerformedBy = request.PerformedBy,
                    Success = request.Success,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue
                };

                var complianceLogs = await _complianceRepository.GetAuditLogAsync(complianceRequest);
                allEntries.AddRange(complianceLogs.Select(MapComplianceEntry));
            }

            // Apply additional filters
            var filteredEntries = ApplyFilters(allEntries, request);

            return filteredEntries.Count;
        }

        /// <summary>
        /// Gets summary statistics for audit log entries matching the filter
        /// </summary>
        public async Task<AuditLogSummary> GetAuditLogSummaryAsync(GetEnterpriseAuditLogRequest request)
        {
            _logger.LogInformation("Generating enterprise audit log summary");

            var allEntries = new List<EnterpriseAuditLogEntry>();

            // Get all relevant audit logs (similar to GetAuditLogAsync but without pagination)
            if (!request.Category.HasValue || 
                request.Category == AuditEventCategory.Whitelist || 
                request.Category == AuditEventCategory.TransferValidation)
            {
                var whitelistRequest = new GetWhitelistAuditLogRequest
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    Address = request.AffectedAddress,
                    PerformedBy = request.PerformedBy,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue
                };

                var whitelistLogs = await _whitelistRepository.GetAuditLogAsync(whitelistRequest);
                allEntries.AddRange(whitelistLogs.Select(MapWhitelistEntry));
            }

            if (!request.Category.HasValue || 
                request.Category == AuditEventCategory.Compliance || 
                request.Category == AuditEventCategory.Blacklist)
            {
                var complianceRequest = new GetComplianceAuditLogRequest
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    PerformedBy = request.PerformedBy,
                    Success = request.Success,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue
                };

                var complianceLogs = await _complianceRepository.GetAuditLogAsync(complianceRequest);
                allEntries.AddRange(complianceLogs.Select(MapComplianceEntry));
            }

            // Apply additional filters
            var filteredEntries = ApplyFilters(allEntries, request);

            // Generate summary
            var summary = new AuditLogSummary
            {
                WhitelistEvents = filteredEntries.Count(e => e.Category == AuditEventCategory.Whitelist || e.Category == AuditEventCategory.TransferValidation),
                BlacklistEvents = filteredEntries.Count(e => e.Category == AuditEventCategory.Blacklist),
                ComplianceEvents = filteredEntries.Count(e => e.Category == AuditEventCategory.Compliance),
                SuccessfulOperations = filteredEntries.Count(e => e.Success),
                FailedOperations = filteredEntries.Count(e => !e.Success),
                Networks = filteredEntries.Where(e => !string.IsNullOrEmpty(e.Network))
                    .Select(e => e.Network!)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList(),
                Assets = filteredEntries.Where(e => e.AssetId.HasValue)
                    .Select(e => e.AssetId!.Value)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList(),
                DateRange = filteredEntries.Any() ? new AuditDateRange
                {
                    EarliestEvent = filteredEntries.Min(e => e.PerformedAt),
                    LatestEvent = filteredEntries.Max(e => e.PerformedAt)
                } : null
            };

            _logger.LogInformation("Summary: {WhitelistEvents} whitelist, {BlacklistEvents} blacklist, {ComplianceEvents} compliance events",
                summary.WhitelistEvents, summary.BlacklistEvents, summary.ComplianceEvents);

            return summary;
        }

        /// <summary>
        /// Maps a whitelist audit entry to an enterprise audit entry
        /// </summary>
        private EnterpriseAuditLogEntry MapWhitelistEntry(WhitelistAuditLogEntry entry)
        {
            var category = entry.ActionType == WhitelistActionType.TransferValidation 
                ? AuditEventCategory.TransferValidation 
                : AuditEventCategory.Whitelist;

            return new EnterpriseAuditLogEntry
            {
                Id = entry.Id,
                AssetId = entry.AssetId,
                Network = entry.Network,
                Category = category,
                ActionType = entry.ActionType.ToString(),
                PerformedBy = entry.PerformedBy,
                PerformedAt = entry.PerformedAt,
                Success = true, // Whitelist entries are always successful (failures aren't logged)
                AffectedAddress = entry.Address,
                OldStatus = entry.OldStatus?.ToString(),
                NewStatus = entry.NewStatus?.ToString(),
                Notes = entry.Notes,
                ToAddress = entry.ToAddress,
                TransferAllowed = entry.TransferAllowed,
                DenialReason = entry.DenialReason,
                Amount = entry.Amount,
                Role = entry.Role.ToString()
            };
        }

        /// <summary>
        /// Maps a compliance audit entry to an enterprise audit entry
        /// </summary>
        private EnterpriseAuditLogEntry MapComplianceEntry(ComplianceAuditLogEntry entry)
        {
            // Determine if this is a blacklist operation based on action and status
            var isBlacklistOperation = entry.ActionType == ComplianceActionType.Create && 
                (entry.Notes?.Contains("blacklist", StringComparison.OrdinalIgnoreCase) ?? false);

            var category = isBlacklistOperation ? AuditEventCategory.Blacklist : AuditEventCategory.Compliance;

            return new EnterpriseAuditLogEntry
            {
                Id = entry.Id,
                AssetId = entry.AssetId,
                Network = entry.Network,
                Category = category,
                ActionType = entry.ActionType.ToString(),
                PerformedBy = entry.PerformedBy,
                PerformedAt = entry.PerformedAt,
                Success = entry.Success,
                ErrorMessage = entry.ErrorMessage,
                OldStatus = GetComplianceStatusString(entry.OldComplianceStatus, entry.OldVerificationStatus),
                NewStatus = GetComplianceStatusString(entry.NewComplianceStatus, entry.NewVerificationStatus),
                Notes = entry.Notes,
                ItemCount = entry.ItemCount
            };
        }

        /// <summary>
        /// Gets a human-readable status string from compliance status and verification status
        /// </summary>
        private string? GetComplianceStatusString(ComplianceStatus? complianceStatus, VerificationStatus? verificationStatus)
        {
            if (!complianceStatus.HasValue && !verificationStatus.HasValue)
                return null;

            var parts = new List<string>();
            if (complianceStatus.HasValue)
                parts.Add($"Compliance: {complianceStatus}");
            if (verificationStatus.HasValue)
                parts.Add($"Verification: {verificationStatus}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Applies additional filters to the audit log entries
        /// </summary>
        private List<EnterpriseAuditLogEntry> ApplyFilters(List<EnterpriseAuditLogEntry> entries, GetEnterpriseAuditLogRequest request)
        {
            var query = entries.AsEnumerable();

            // Filter by category
            if (request.Category.HasValue)
            {
                query = query.Where(e => e.Category == request.Category.Value);
            }

            // Filter by action type
            if (!string.IsNullOrEmpty(request.ActionType))
            {
                query = query.Where(e => e.ActionType.Equals(request.ActionType, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by success (already applied in repository calls where possible)
            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            return query.ToList();
        }
    }
}

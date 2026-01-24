using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services.Interface;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing whitelisting rules operations
    /// </summary>
    public class WhitelistRulesService : IWhitelistRulesService
    {
        private readonly IWhitelistRulesRepository _rulesRepository;
        private readonly IWhitelistRepository _whitelistRepository;
        private readonly ILogger<WhitelistRulesService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistRulesService"/> class.
        /// </summary>
        /// <param name="rulesRepository">The rules repository</param>
        /// <param name="whitelistRepository">The whitelist repository</param>
        /// <param name="logger">The logger instance</param>
        public WhitelistRulesService(
            IWhitelistRulesRepository rulesRepository,
            IWhitelistRepository whitelistRepository,
            ILogger<WhitelistRulesService> logger)
        {
            _rulesRepository = rulesRepository;
            _whitelistRepository = whitelistRepository;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new whitelisting rule
        /// </summary>
        /// <param name="request">The create rule request</param>
        /// <param name="createdBy">The address of the user creating the rule</param>
        /// <returns>The rule response</returns>
        public async Task<WhitelistRuleResponse> CreateRuleAsync(CreateWhitelistRuleRequest request, string createdBy)
        {
            try
            {
                // Validate rule configuration if provided
                if (!string.IsNullOrEmpty(request.Configuration))
                {
                    var validationError = ValidateRuleConfiguration(request.RuleType, request.Configuration);
                    if (validationError != null)
                    {
                        return new WhitelistRuleResponse
                        {
                            Success = false,
                            ErrorMessage = validationError
                        };
                    }
                }

                var rule = new WhitelistRule
                {
                    AssetId = request.AssetId,
                    Name = request.Name,
                    Description = request.Description,
                    RuleType = request.RuleType,
                    IsActive = request.IsActive,
                    Priority = request.Priority,
                    Network = request.Network,
                    Configuration = request.Configuration,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                var added = await _rulesRepository.AddRuleAsync(rule);
                
                if (!added)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to add rule to repository"
                    };
                }

                // Add audit log entry
                await AddAuditLogEntryAsync(rule, RuleAuditActionType.Create, createdBy, null, rule);

                _logger.LogInformation("Created whitelisting rule {RuleId} for asset {AssetId} by {CreatedBy}",
                    rule.Id, rule.AssetId, createdBy);

                return new WhitelistRuleResponse
                {
                    Success = true,
                    Rule = rule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating whitelisting rule for asset {AssetId}", request.AssetId);
                return new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Updates an existing whitelisting rule
        /// </summary>
        /// <param name="request">The update rule request</param>
        /// <param name="updatedBy">The address of the user updating the rule</param>
        /// <returns>The rule response</returns>
        public async Task<WhitelistRuleResponse> UpdateRuleAsync(UpdateWhitelistRuleRequest request, string updatedBy)
        {
            try
            {
                var existingRule = await _rulesRepository.GetRuleAsync(request.RuleId);
                if (existingRule == null)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Rule with ID {request.RuleId} not found"
                    };
                }

                // Store old state for audit
                var oldRule = CloneRule(existingRule);

                // Update fields
                if (request.Name != null)
                {
                    existingRule.Name = request.Name;
                }

                if (request.Description != null)
                {
                    existingRule.Description = request.Description;
                }

                if (request.IsActive.HasValue)
                {
                    // Track activation/deactivation in audit log
                    if (existingRule.IsActive != request.IsActive.Value)
                    {
                        var actionType = request.IsActive.Value ? RuleAuditActionType.Activate : RuleAuditActionType.Deactivate;
                        await AddAuditLogEntryAsync(existingRule, actionType, updatedBy, oldRule, existingRule);
                    }
                    existingRule.IsActive = request.IsActive.Value;
                }

                if (request.Priority.HasValue)
                {
                    existingRule.Priority = request.Priority.Value;
                }

                if (request.Network != null)
                {
                    existingRule.Network = request.Network;
                }

                if (request.Configuration != null)
                {
                    // Validate configuration
                    var validationError = ValidateRuleConfiguration(existingRule.RuleType, request.Configuration);
                    if (validationError != null)
                    {
                        return new WhitelistRuleResponse
                        {
                            Success = false,
                            ErrorMessage = validationError
                        };
                    }
                    existingRule.Configuration = request.Configuration;
                }

                existingRule.UpdatedBy = updatedBy;
                existingRule.UpdatedAt = DateTime.UtcNow;

                var updated = await _rulesRepository.UpdateRuleAsync(existingRule);
                
                if (!updated)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to update rule in repository"
                    };
                }

                // Add audit log entry for update
                await AddAuditLogEntryAsync(existingRule, RuleAuditActionType.Update, updatedBy, oldRule, existingRule);

                _logger.LogInformation("Updated whitelisting rule {RuleId} for asset {AssetId} by {UpdatedBy}",
                    existingRule.Id, existingRule.AssetId, updatedBy);

                return new WhitelistRuleResponse
                {
                    Success = true,
                    Rule = existingRule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception updating whitelisting rule {RuleId}", request.RuleId);
                return new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Lists whitelisting rules for a specific asset
        /// </summary>
        /// <param name="request">The list rules request</param>
        /// <returns>The list response with rules</returns>
        public async Task<WhitelistRulesListResponse> ListRulesAsync(ListWhitelistRulesRequest request)
        {
            try
            {
                var allRules = await _rulesRepository.GetRulesByAssetIdAsync(
                    request.AssetId,
                    request.RuleType,
                    request.IsActive,
                    request.Network);

                var totalCount = allRules.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

                // Apply pagination
                var paginatedRules = allRules
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                return new WhitelistRulesListResponse
                {
                    Success = true,
                    Rules = paginatedRules,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing whitelisting rules for asset {AssetId}", request.AssetId);
                return new WhitelistRulesListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Applies a whitelisting rule to matching whitelist entries
        /// </summary>
        /// <param name="request">The apply rule request</param>
        /// <param name="appliedBy">The address of the user applying the rule</param>
        /// <returns>The application result</returns>
        public async Task<ApplyWhitelistRuleResponse> ApplyRuleAsync(ApplyWhitelistRuleRequest request, string appliedBy)
        {
            try
            {
                var rule = await _rulesRepository.GetRuleAsync(request.RuleId);
                if (rule == null)
                {
                    return new ApplyWhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Rule with ID {request.RuleId} not found"
                    };
                }

                if (!rule.IsActive)
                {
                    return new ApplyWhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Cannot apply inactive rule"
                    };
                }

                // Get whitelist entries to apply rule to
                var entries = await _whitelistRepository.GetEntriesByAssetIdAsync(rule.AssetId);
                
                // Filter by target addresses if specified
                if (request.TargetAddresses != null && request.TargetAddresses.Count > 0)
                {
                    var targetAddressesSet = new HashSet<string>(
                        request.TargetAddresses, 
                        StringComparer.OrdinalIgnoreCase);
                    entries = entries.Where(e => targetAddressesSet.Contains(e.Address)).ToList();
                }

                var result = new RuleApplicationResult
                {
                    Rule = rule,
                    Success = true,
                    AppliedAt = DateTime.UtcNow
                };

                // Apply rule based on type
                switch (rule.RuleType)
                {
                    case WhitelistRuleType.AutoRevokeExpired:
                        await ApplyAutoRevokeExpiredRule(entries, result, request.DryRun);
                        break;

                    case WhitelistRuleType.RequireKycForActive:
                        await ApplyRequireKycForActiveRule(entries, result, request.DryRun);
                        break;

                    case WhitelistRuleType.NetworkKycRequirement:
                        await ApplyNetworkKycRequirementRule(entries, rule, result, request.DryRun);
                        break;

                    case WhitelistRuleType.MinimumKycAge:
                        await ApplyMinimumKycAgeRule(entries, rule, result, request.DryRun);
                        break;

                    default:
                        result.Success = false;
                        result.ErrorMessage = $"Rule type {rule.RuleType} is not yet implemented";
                        break;
                }

                // Record application if not a dry run
                if (!request.DryRun && result.Success)
                {
                    await _rulesRepository.RecordRuleApplicationAsync(rule.Id, result.AppliedAt);
                    
                    // Add audit log entry
                    var auditLog = new WhitelistRuleAuditLog
                    {
                        AssetId = rule.AssetId,
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        ActionType = RuleAuditActionType.Apply,
                        PerformedBy = appliedBy,
                        PerformedAt = result.AppliedAt,
                        Notes = request.DryRun ? "Dry run - no changes made" : $"Applied to {result.AffectedEntriesCount} entries",
                        Network = rule.Network,
                        AffectedEntriesCount = result.AffectedEntriesCount
                    };
                    await _rulesRepository.AddAuditLogAsync(auditLog);
                }

                _logger.LogInformation(
                    "Applied rule {RuleId} ({RuleType}) to {Count} entries for asset {AssetId} (DryRun: {DryRun})",
                    rule.Id, rule.RuleType, result.AffectedEntriesCount, rule.AssetId, request.DryRun);

                return new ApplyWhitelistRuleResponse
                {
                    Success = true,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception applying whitelisting rule {RuleId}", request.RuleId);
                return new ApplyWhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deletes a whitelisting rule
        /// </summary>
        /// <param name="request">The delete rule request</param>
        /// <param name="deletedBy">The address of the user deleting the rule</param>
        /// <returns>The delete response</returns>
        public async Task<DeleteWhitelistRuleResponse> DeleteRuleAsync(DeleteWhitelistRuleRequest request, string deletedBy)
        {
            try
            {
                var rule = await _rulesRepository.GetRuleAsync(request.RuleId);
                if (rule == null)
                {
                    return new DeleteWhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Rule with ID {request.RuleId} not found"
                    };
                }

                // Add audit log entry before deletion
                await AddAuditLogEntryAsync(rule, RuleAuditActionType.Delete, deletedBy, rule, null);

                var deleted = await _rulesRepository.DeleteRuleAsync(request.RuleId);
                
                if (!deleted)
                {
                    return new DeleteWhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to delete rule from repository"
                    };
                }

                _logger.LogInformation("Deleted whitelisting rule {RuleId} for asset {AssetId} by {DeletedBy}",
                    request.RuleId, rule.AssetId, deletedBy);

                return new DeleteWhitelistRuleResponse
                {
                    Success = true,
                    RuleId = request.RuleId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting whitelisting rule {RuleId}", request.RuleId);
                return new DeleteWhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets audit log entries for rules
        /// </summary>
        public async Task<WhitelistRuleAuditLogResponse> GetAuditLogsAsync(
            ulong assetId,
            string? ruleId = null,
            RuleAuditActionType? actionType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 50)
        {
            try
            {
                var allLogs = await _rulesRepository.GetAuditLogsAsync(assetId, ruleId, actionType, fromDate, toDate);
                
                var totalCount = allLogs.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var paginatedLogs = allLogs
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new WhitelistRuleAuditLogResponse
                {
                    Success = true,
                    Entries = paginatedLogs,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting audit logs for asset {AssetId}", assetId);
                return new WhitelistRuleAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        // Private helper methods

        private async Task ApplyAutoRevokeExpiredRule(
            List<WhitelistEntry> entries, 
            RuleApplicationResult result,
            bool dryRun)
        {
            var now = DateTime.UtcNow;
            var expiredEntries = entries
                .Where(e => e.ExpirationDate.HasValue && e.ExpirationDate.Value <= now && e.Status != WhitelistStatus.Revoked)
                .ToList();

            foreach (var entry in expiredEntries)
            {
                result.AffectedEntriesCount++;
                result.AffectedAddresses.Add(entry.Address);
                result.Actions.Add($"Revoked expired entry for {entry.Address} (expired: {entry.ExpirationDate})");

                if (!dryRun)
                {
                    entry.Status = WhitelistStatus.Revoked;
                    entry.UpdatedAt = now;
                    await _whitelistRepository.UpdateEntryAsync(entry);
                }
            }
        }

        private async Task ApplyRequireKycForActiveRule(
            List<WhitelistEntry> entries,
            RuleApplicationResult result,
            bool dryRun)
        {
            var entriesWithoutKyc = entries
                .Where(e => e.Status == WhitelistStatus.Active && !e.KycVerified)
                .ToList();

            foreach (var entry in entriesWithoutKyc)
            {
                result.AffectedEntriesCount++;
                result.AffectedAddresses.Add(entry.Address);
                result.Actions.Add($"Deactivated entry for {entry.Address} (KYC not verified)");

                if (!dryRun)
                {
                    entry.Status = WhitelistStatus.Inactive;
                    entry.UpdatedAt = DateTime.UtcNow;
                    await _whitelistRepository.UpdateEntryAsync(entry);
                }
            }
        }

        private async Task ApplyNetworkKycRequirementRule(
            List<WhitelistEntry> entries,
            WhitelistRule rule,
            RuleApplicationResult result,
            bool dryRun)
        {
            // For Aramid network, KYC is mandatory for Active status
            var targetNetwork = rule.Network ?? "aramidmain-v1.0";
            
            var entriesOnNetwork = entries
                .Where(e => e.Network == targetNetwork && e.Status == WhitelistStatus.Active && !e.KycVerified)
                .ToList();

            foreach (var entry in entriesOnNetwork)
            {
                result.AffectedEntriesCount++;
                result.AffectedAddresses.Add(entry.Address);
                result.Actions.Add($"Deactivated entry for {entry.Address} on {targetNetwork} (KYC required for network)");

                if (!dryRun)
                {
                    entry.Status = WhitelistStatus.Inactive;
                    entry.UpdatedAt = DateTime.UtcNow;
                    await _whitelistRepository.UpdateEntryAsync(entry);
                }
            }
        }

        private async Task ApplyMinimumKycAgeRule(
            List<WhitelistEntry> entries,
            WhitelistRule rule,
            RuleApplicationResult result,
            bool dryRun)
        {
            // Parse configuration for minimum days
            int minimumDays = 30; // default
            if (!string.IsNullOrEmpty(rule.Configuration))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, int>>(rule.Configuration);
                    if (config != null && config.ContainsKey("minimumDays"))
                    {
                        minimumDays = config["minimumDays"];
                    }
                }
                catch
                {
                    _logger.LogWarning("Failed to parse configuration for rule {RuleId}", rule.Id);
                }
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-minimumDays);
            var recentKycEntries = entries
                .Where(e => e.Status == WhitelistStatus.Active && 
                           e.KycVerified && 
                           e.KycVerificationDate.HasValue &&
                           e.KycVerificationDate.Value > cutoffDate)
                .ToList();

            foreach (var entry in recentKycEntries)
            {
                result.AffectedEntriesCount++;
                result.AffectedAddresses.Add(entry.Address);
                result.Actions.Add($"Deactivated entry for {entry.Address} (KYC age {(DateTime.UtcNow - entry.KycVerificationDate.Value).Days} days < {minimumDays} days)");

                if (!dryRun)
                {
                    entry.Status = WhitelistStatus.Inactive;
                    entry.UpdatedAt = DateTime.UtcNow;
                    await _whitelistRepository.UpdateEntryAsync(entry);
                }
            }
        }

        private string? ValidateRuleConfiguration(WhitelistRuleType ruleType, string configuration)
        {
            // Validate JSON format
            try
            {
                JsonSerializer.Deserialize<Dictionary<string, object>>(configuration);
            }
            catch
            {
                return "Configuration must be valid JSON";
            }

            // Type-specific validation
            switch (ruleType)
            {
                case WhitelistRuleType.MinimumKycAge:
                    try
                    {
                        var config = JsonSerializer.Deserialize<Dictionary<string, int>>(configuration);
                        if (config == null || !config.ContainsKey("minimumDays"))
                        {
                            return "MinimumKycAge rule requires 'minimumDays' in configuration";
                        }
                        if (config["minimumDays"] < 1)
                        {
                            return "minimumDays must be at least 1";
                        }
                    }
                    catch
                    {
                        return "Invalid configuration for MinimumKycAge rule";
                    }
                    break;
            }

            return null;
        }

        private async Task AddAuditLogEntryAsync(
            WhitelistRule rule,
            RuleAuditActionType actionType,
            string performedBy,
            WhitelistRule? oldState,
            WhitelistRule? newState)
        {
            var auditLog = new WhitelistRuleAuditLog
            {
                AssetId = rule.AssetId,
                RuleId = rule.Id,
                RuleName = rule.Name,
                ActionType = actionType,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                OldState = oldState != null ? JsonSerializer.Serialize(oldState) : null,
                NewState = newState != null ? JsonSerializer.Serialize(newState) : null,
                Network = rule.Network
            };

            await _rulesRepository.AddAuditLogAsync(auditLog);
        }

        private WhitelistRule CloneRule(WhitelistRule rule)
        {
            return new WhitelistRule
            {
                Id = rule.Id,
                AssetId = rule.AssetId,
                Name = rule.Name,
                Description = rule.Description,
                RuleType = rule.RuleType,
                IsActive = rule.IsActive,
                Priority = rule.Priority,
                Network = rule.Network,
                Configuration = rule.Configuration,
                CreatedBy = rule.CreatedBy,
                CreatedAt = rule.CreatedAt,
                UpdatedBy = rule.UpdatedBy,
                UpdatedAt = rule.UpdatedAt,
                ApplicationCount = rule.ApplicationCount,
                LastAppliedAt = rule.LastAppliedAt
            };
        }
    }
}

using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services.Interface;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing whitelist rules with MICA compliance
    /// </summary>
    public class WhitelistRulesService : IWhitelistRulesService
    {
        private readonly IWhitelistRulesRepository _rulesRepository;
        private readonly IWhitelistRepository _whitelistRepository;
        private readonly ILogger<WhitelistRulesService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistRulesService"/> class.
        /// </summary>
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
        /// Creates a new whitelist rule
        /// </summary>
        public async Task<WhitelistRuleResponse> CreateRuleAsync(CreateWhitelistRuleRequest request, string createdBy)
        {
            try
            {
                // Validate configuration based on rule type
                var validationError = ValidateRuleConfiguration(request.RuleType, request.Configuration);
                if (validationError != null)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = validationError
                    };
                }

                var rule = new WhitelistRule
                {
                    AssetId = request.AssetId,
                    Name = request.Name,
                    Description = request.Description,
                    RuleType = request.RuleType,
                    Priority = request.Priority,
                    IsEnabled = request.IsEnabled,
                    Network = request.Network,
                    Configuration = request.Configuration,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                var createdRule = await _rulesRepository.CreateRuleAsync(rule);

                // Add audit log
                await _rulesRepository.AddAuditLogAsync(new WhitelistRuleAuditLog
                {
                    RuleId = createdRule.Id,
                    AssetId = createdRule.AssetId,
                    ActionType = "Create",
                    PerformedBy = createdBy,
                    NewState = JsonSerializer.Serialize(createdRule),
                    Notes = $"Created rule: {createdRule.Name}"
                });

                _logger.LogInformation("Created whitelist rule {RuleId} for asset {AssetId} by {CreatedBy}",
                    createdRule.Id, createdRule.AssetId, createdBy);

                return new WhitelistRuleResponse
                {
                    Success = true,
                    Rule = createdRule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating whitelist rule for asset {AssetId}", request.AssetId);
                return new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create rule: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Updates an existing whitelist rule
        /// </summary>
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

                var oldState = JsonSerializer.Serialize(existingRule);

                // Update fields if provided
                if (!string.IsNullOrEmpty(request.Name))
                {
                    existingRule.Name = request.Name;
                }

                if (request.Description != null)
                {
                    existingRule.Description = request.Description;
                }

                if (request.Priority.HasValue)
                {
                    existingRule.Priority = request.Priority.Value;
                }

                if (request.IsEnabled.HasValue)
                {
                    existingRule.IsEnabled = request.IsEnabled.Value;
                }

                if (request.Configuration != null)
                {
                    // Validate new configuration
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
                        ErrorMessage = "Failed to update rule"
                    };
                }

                // Add audit log
                await _rulesRepository.AddAuditLogAsync(new WhitelistRuleAuditLog
                {
                    RuleId = existingRule.Id,
                    AssetId = existingRule.AssetId,
                    ActionType = "Update",
                    PerformedBy = updatedBy,
                    OldState = oldState,
                    NewState = JsonSerializer.Serialize(existingRule),
                    Notes = $"Updated rule: {existingRule.Name}"
                });

                _logger.LogInformation("Updated whitelist rule {RuleId} by {UpdatedBy}",
                    existingRule.Id, updatedBy);

                return new WhitelistRuleResponse
                {
                    Success = true,
                    Rule = existingRule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating whitelist rule {RuleId}", request.RuleId);
                return new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to update rule: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets a rule by ID
        /// </summary>
        public async Task<WhitelistRuleResponse> GetRuleAsync(string ruleId)
        {
            try
            {
                var rule = await _rulesRepository.GetRuleAsync(ruleId);
                if (rule == null)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Rule with ID {ruleId} not found"
                    };
                }

                return new WhitelistRuleResponse
                {
                    Success = true,
                    Rule = rule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting whitelist rule {RuleId}", ruleId);
                return new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to get rule: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deletes a rule
        /// </summary>
        public async Task<WhitelistRuleResponse> DeleteRuleAsync(string ruleId, string deletedBy)
        {
            try
            {
                var existingRule = await _rulesRepository.GetRuleAsync(ruleId);
                if (existingRule == null)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Rule with ID {ruleId} not found"
                    };
                }

                var deleted = await _rulesRepository.DeleteRuleAsync(ruleId);
                if (!deleted)
                {
                    return new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to delete rule"
                    };
                }

                // Add audit log
                await _rulesRepository.AddAuditLogAsync(new WhitelistRuleAuditLog
                {
                    RuleId = ruleId,
                    AssetId = existingRule.AssetId,
                    ActionType = "Delete",
                    PerformedBy = deletedBy,
                    OldState = JsonSerializer.Serialize(existingRule),
                    Notes = $"Deleted rule: {existingRule.Name}"
                });

                _logger.LogInformation("Deleted whitelist rule {RuleId} by {DeletedBy}",
                    ruleId, deletedBy);

                return new WhitelistRuleResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting whitelist rule {RuleId}", ruleId);
                return new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to delete rule: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Lists rules for an asset
        /// </summary>
        public async Task<WhitelistRulesListResponse> ListRulesAsync(ListWhitelistRulesRequest request)
        {
            try
            {
                var allRules = await _rulesRepository.GetRulesForAssetAsync(
                    request.AssetId,
                    request.RuleType,
                    request.Network,
                    request.IsEnabled);

                var totalCount = allRules.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

                var pagedRules = allRules
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                return new WhitelistRulesListResponse
                {
                    Success = true,
                    Rules = pagedRules,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing whitelist rules for asset {AssetId}", request.AssetId);
                return new WhitelistRulesListResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to list rules: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Applies a rule to existing whitelist entries
        /// </summary>
        public async Task<ApplyRuleResponse> ApplyRuleAsync(ApplyWhitelistRuleRequest request, string performedBy)
        {
            try
            {
                var rule = await _rulesRepository.GetRuleAsync(request.RuleId);
                if (rule == null)
                {
                    return new ApplyRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Rule with ID {request.RuleId} not found"
                    };
                }

                if (!rule.IsEnabled)
                {
                    return new ApplyRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Cannot apply disabled rule"
                    };
                }

                var response = new ApplyRuleResponse { Success = true };

                if (request.ApplyToExisting)
                {
                    // Get all whitelist entries for the asset
                    var entries = await _whitelistRepository.GetEntriesByAssetIdAsync(rule.AssetId);

                    foreach (var entry in entries)
                    {
                        response.EntriesEvaluated++;

                        var validationError = await ValidateEntryAgainstRuleAsync(entry, rule);
                        if (validationError != null)
                        {
                            response.EntriesFailed++;
                            response.FailedAddresses.Add(entry.Address);
                            response.ValidationErrors.Add(validationError);

                            if (request.FailOnError)
                            {
                                response.Success = false;
                                response.ErrorMessage = $"Rule application failed due to validation errors";
                                break;
                            }
                        }
                        else
                        {
                            response.EntriesPassed++;
                        }
                    }
                }

                // Add audit log
                await _rulesRepository.AddAuditLogAsync(new WhitelistRuleAuditLog
                {
                    RuleId = request.RuleId,
                    AssetId = rule.AssetId,
                    ActionType = "Apply",
                    PerformedBy = performedBy,
                    Notes = $"Applied rule to {response.EntriesEvaluated} entries. Passed: {response.EntriesPassed}, Failed: {response.EntriesFailed}"
                });

                _logger.LogInformation("Applied rule {RuleId} to {Count} entries. Passed: {Passed}, Failed: {Failed}",
                    request.RuleId, response.EntriesEvaluated, response.EntriesPassed, response.EntriesFailed);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying whitelist rule {RuleId}", request.RuleId);
                return new ApplyRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to apply rule: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validates whitelist entries against rules
        /// </summary>
        public async Task<ValidateAgainstRulesResponse> ValidateAgainstRulesAsync(ValidateAgainstRulesRequest request)
        {
            try
            {
                var response = new ValidateAgainstRulesResponse { Success = true, IsValid = true };

                // Get rules to validate against
                List<WhitelistRule> rules;
                if (!string.IsNullOrEmpty(request.RuleId))
                {
                    var rule = await _rulesRepository.GetRuleAsync(request.RuleId);
                    rules = rule != null ? new List<WhitelistRule> { rule } : new List<WhitelistRule>();
                }
                else
                {
                    rules = await _rulesRepository.GetRulesForAssetAsync(request.AssetId, isEnabled: true);
                }

                // Get entries to validate
                List<WhitelistEntry> entries;
                if (!string.IsNullOrEmpty(request.Address))
                {
                    var entry = await _whitelistRepository.GetEntryAsync(request.AssetId, request.Address);
                    entries = entry != null ? new List<WhitelistEntry> { entry } : new List<WhitelistEntry>();
                }
                else
                {
                    entries = await _whitelistRepository.GetEntriesByAssetIdAsync(request.AssetId);
                }

                // Validate each entry against each rule
                foreach (var rule in rules)
                {
                    response.RulesEvaluated++;

                    bool rulePassedForAllEntries = true;
                    foreach (var entry in entries)
                    {
                        var validationError = await ValidateEntryAgainstRuleAsync(entry, rule);
                        if (validationError != null)
                        {
                            response.ValidationErrors.Add(validationError);
                            rulePassedForAllEntries = false;
                        }
                    }

                    if (rulePassedForAllEntries)
                    {
                        response.RulesPassed++;
                    }
                    else
                    {
                        response.RulesFailed++;
                    }
                }

                response.IsValid = response.RulesFailed == 0;
                response.Success = true;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating against rules for asset {AssetId}", request.AssetId);
                return new ValidateAgainstRulesResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to validate: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validates a single whitelist entry against a specific rule
        /// </summary>
        public Task<RuleValidationError?> ValidateEntryAgainstRuleAsync(WhitelistEntry entry, WhitelistRule rule)
        {
            // Check if rule applies to this entry's network
            if (!string.IsNullOrEmpty(rule.Network) && 
                !string.IsNullOrEmpty(entry.Network) &&
                !rule.Network.Equals(entry.Network, StringComparison.OrdinalIgnoreCase))
            {
                // Rule doesn't apply to this network
                return Task.FromResult<RuleValidationError?>(null);
            }

            switch (rule.RuleType)
            {
                case WhitelistRuleType.KycRequired:
                    return ValidateKycRule(entry, rule);

                case WhitelistRuleType.RoleBasedAccess:
                    return ValidateRoleRule(entry, rule);

                case WhitelistRuleType.NetworkSpecific:
                    return ValidateNetworkRule(entry, rule);

                case WhitelistRuleType.ExpirationRequired:
                    return ValidateExpirationRule(entry, rule);

                case WhitelistRuleType.StatusValidation:
                    return ValidateStatusRule(entry, rule);

                case WhitelistRuleType.Composite:
                    return ValidateCompositeRule(entry, rule);

                default:
                    return Task.FromResult<RuleValidationError?>(null);
            }
        }

        #region Private Validation Methods

        private Task<RuleValidationError?> ValidateKycRule(WhitelistEntry entry, WhitelistRule rule)
        {
            if (rule.Configuration.KycMandatory == true && !entry.KycVerified)
            {
                return Task.FromResult<RuleValidationError?>(new RuleValidationError
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Address = entry.Address,
                    ErrorMessage = rule.Configuration.ValidationMessage ?? "KYC verification is required",
                    FieldName = "KycVerified"
                });
            }

            if (rule.Configuration.ApprovedKycProviders?.Any() == true && 
                entry.KycVerified &&
                !string.IsNullOrEmpty(entry.KycProvider))
            {
                if (!rule.Configuration.ApprovedKycProviders.Contains(entry.KycProvider, StringComparer.OrdinalIgnoreCase))
                {
                    return Task.FromResult<RuleValidationError?>(new RuleValidationError
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Address = entry.Address,
                        ErrorMessage = rule.Configuration.ValidationMessage ?? 
                            $"KYC provider '{entry.KycProvider}' is not approved. Approved providers: {string.Join(", ", rule.Configuration.ApprovedKycProviders)}",
                        FieldName = "KycProvider"
                    });
                }
            }

            return Task.FromResult<RuleValidationError?>(null);
        }

        private Task<RuleValidationError?> ValidateRoleRule(WhitelistEntry entry, WhitelistRule rule)
        {
            if (rule.Configuration.MinimumRole.HasValue)
            {
                // Admin (0) > Operator (1) in terms of permissions
                if (entry.Role > rule.Configuration.MinimumRole.Value)
                {
                    return Task.FromResult<RuleValidationError?>(new RuleValidationError
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Address = entry.Address,
                        ErrorMessage = rule.Configuration.ValidationMessage ?? 
                            $"Minimum role required is {rule.Configuration.MinimumRole.Value}, but entry has {entry.Role}",
                        FieldName = "Role"
                    });
                }
            }

            return Task.FromResult<RuleValidationError?>(null);
        }

        private Task<RuleValidationError?> ValidateNetworkRule(WhitelistEntry entry, WhitelistRule rule)
        {
            if (!string.IsNullOrEmpty(rule.Configuration.NetworkRequirement))
            {
                if (string.IsNullOrEmpty(entry.Network) ||
                    !entry.Network.Equals(rule.Configuration.NetworkRequirement, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult<RuleValidationError?>(new RuleValidationError
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Address = entry.Address,
                        ErrorMessage = rule.Configuration.ValidationMessage ?? 
                            $"Network must be '{rule.Configuration.NetworkRequirement}'",
                        FieldName = "Network"
                    });
                }
            }

            return Task.FromResult<RuleValidationError?>(null);
        }

        private Task<RuleValidationError?> ValidateExpirationRule(WhitelistEntry entry, WhitelistRule rule)
        {
            if (rule.Configuration.ExpirationMandatory == true && !entry.ExpirationDate.HasValue)
            {
                return Task.FromResult<RuleValidationError?>(new RuleValidationError
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Address = entry.Address,
                    ErrorMessage = rule.Configuration.ValidationMessage ?? "Expiration date is required",
                    FieldName = "ExpirationDate"
                });
            }

            if (rule.Configuration.MaxValidityDays.HasValue && entry.ExpirationDate.HasValue)
            {
                var maxDate = entry.CreatedAt.AddDays(rule.Configuration.MaxValidityDays.Value);
                if (entry.ExpirationDate.Value > maxDate)
                {
                    return Task.FromResult<RuleValidationError?>(new RuleValidationError
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Address = entry.Address,
                        ErrorMessage = rule.Configuration.ValidationMessage ?? 
                            $"Maximum validity period is {rule.Configuration.MaxValidityDays.Value} days",
                        FieldName = "ExpirationDate"
                    });
                }
            }

            return Task.FromResult<RuleValidationError?>(null);
        }

        private Task<RuleValidationError?> ValidateStatusRule(WhitelistEntry entry, WhitelistRule rule)
        {
            if (rule.Configuration.RequiredStatus.HasValue && 
                entry.Status != rule.Configuration.RequiredStatus.Value)
            {
                return Task.FromResult<RuleValidationError?>(new RuleValidationError
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Address = entry.Address,
                    ErrorMessage = rule.Configuration.ValidationMessage ?? 
                        $"Status must be {rule.Configuration.RequiredStatus.Value}",
                    FieldName = "Status"
                });
            }

            return Task.FromResult<RuleValidationError?>(null);
        }

        private async Task<RuleValidationError?> ValidateCompositeRule(WhitelistEntry entry, WhitelistRule rule)
        {
            if (rule.Configuration.CompositeRuleIds?.Any() != true)
            {
                return null;
            }

            // Validate against all composite rules
            foreach (var compositeRuleId in rule.Configuration.CompositeRuleIds)
            {
                var compositeRule = await _rulesRepository.GetRuleAsync(compositeRuleId);
                if (compositeRule != null && compositeRule.IsEnabled)
                {
                    var error = await ValidateEntryAgainstRuleAsync(entry, compositeRule);
                    if (error != null)
                    {
                        return error;
                    }
                }
            }

            return null;
        }

        private string? ValidateRuleConfiguration(WhitelistRuleType ruleType, WhitelistRuleConfiguration config)
        {
            switch (ruleType)
            {
                case WhitelistRuleType.KycRequired:
                    // KYC rules should have at least KycMandatory or ApprovedKycProviders
                    if (config.KycMandatory != true && (config.ApprovedKycProviders == null || !config.ApprovedKycProviders.Any()))
                    {
                        return "KYC rule must specify either KycMandatory or ApprovedKycProviders";
                    }
                    break;

                case WhitelistRuleType.RoleBasedAccess:
                    if (!config.MinimumRole.HasValue)
                    {
                        return "Role-based rule must specify MinimumRole";
                    }
                    break;

                case WhitelistRuleType.NetworkSpecific:
                    if (string.IsNullOrEmpty(config.NetworkRequirement))
                    {
                        return "Network-specific rule must specify NetworkRequirement";
                    }
                    break;

                case WhitelistRuleType.ExpirationRequired:
                    if (config.ExpirationMandatory != true && !config.MaxValidityDays.HasValue)
                    {
                        return "Expiration rule must specify either ExpirationMandatory or MaxValidityDays";
                    }
                    break;

                case WhitelistRuleType.StatusValidation:
                    if (!config.RequiredStatus.HasValue)
                    {
                        return "Status validation rule must specify RequiredStatus";
                    }
                    break;

                case WhitelistRuleType.Composite:
                    if (config.CompositeRuleIds == null || !config.CompositeRuleIds.Any())
                    {
                        return "Composite rule must specify at least one CompositeRuleId";
                    }
                    break;
            }

            return null;
        }

        #endregion
    }
}

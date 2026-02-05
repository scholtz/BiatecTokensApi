using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service implementation for managing and evaluating jurisdiction rules
    /// </summary>
    public class JurisdictionRulesService : IJurisdictionRulesService
    {
        private readonly IJurisdictionRulesRepository _rulesRepository;
        private readonly IComplianceRepository _complianceRepository;
        private readonly ILogger<JurisdictionRulesService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="JurisdictionRulesService"/> class.
        /// </summary>
        /// <param name="rulesRepository">The jurisdiction rules repository</param>
        /// <param name="complianceRepository">The compliance repository</param>
        /// <param name="logger">The logger instance</param>
        public JurisdictionRulesService(
            IJurisdictionRulesRepository rulesRepository,
            IComplianceRepository complianceRepository,
            ILogger<JurisdictionRulesService> logger)
        {
            _rulesRepository = rulesRepository;
            _complianceRepository = complianceRepository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<JurisdictionRuleResponse> CreateRuleAsync(CreateJurisdictionRuleRequest request, string createdBy)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.JurisdictionCode))
                {
                    return new JurisdictionRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Jurisdiction code is required"
                    };
                }

                // Check if rule already exists for this jurisdiction
                var existingRule = await _rulesRepository.GetRuleByJurisdictionCodeAsync(request.JurisdictionCode);
                if (existingRule != null)
                {
                    return new JurisdictionRuleResponse
                    {
                        Success = false,
                        ErrorMessage = $"Jurisdiction rule already exists for {request.JurisdictionCode}"
                    };
                }

                var rule = new JurisdictionRule
                {
                    JurisdictionCode = request.JurisdictionCode,
                    JurisdictionName = request.JurisdictionName,
                    RegulatoryFramework = request.RegulatoryFramework,
                    IsActive = request.IsActive,
                    Priority = request.Priority,
                    Requirements = request.Requirements ?? new List<ComplianceRequirement>(),
                    CreatedBy = createdBy,
                    Notes = request.Notes
                };

                var createdRule = await _rulesRepository.CreateRuleAsync(rule);

                _logger.LogInformation("Created jurisdiction rule {RuleId} for {JurisdictionCode}",
                    LoggingHelper.SanitizeLogInput(createdRule.Id), 
                    LoggingHelper.SanitizeLogInput(createdRule.JurisdictionCode));

                return new JurisdictionRuleResponse
                {
                    Success = true,
                    Rule = createdRule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating jurisdiction rule");
                return new JurisdictionRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<JurisdictionRuleResponse> GetRuleByIdAsync(string ruleId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

            try
            {
                var rule = await _rulesRepository.GetRuleByIdAsync(ruleId);
                if (rule == null)
                {
                    return new JurisdictionRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Jurisdiction rule not found"
                    };
                }

                return new JurisdictionRuleResponse
                {
                    Success = true,
                    Rule = rule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));
                return new JurisdictionRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ListJurisdictionRulesResponse> ListRulesAsync(ListJurisdictionRulesRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // Validate pagination parameters
                request.Page = Math.Max(1, request.Page);
                request.PageSize = Math.Clamp(request.PageSize, 1, 100);

                var (rules, totalCount) = await _rulesRepository.ListRulesAsync(request);

                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

                _logger.LogInformation("Listed {Count} jurisdiction rules (page {Page}/{TotalPages})",
                    rules.Count, request.Page, totalPages);

                return new ListJurisdictionRulesResponse
                {
                    Success = true,
                    Rules = rules,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing jurisdiction rules");
                return new ListJurisdictionRulesResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<JurisdictionRuleResponse> UpdateRuleAsync(string ruleId, CreateJurisdictionRuleRequest request, string updatedBy)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

            try
            {
                var existingRule = await _rulesRepository.GetRuleByIdAsync(ruleId);
                if (existingRule == null)
                {
                    return new JurisdictionRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "Jurisdiction rule not found"
                    };
                }

                // Update rule properties
                existingRule.JurisdictionCode = request.JurisdictionCode;
                existingRule.JurisdictionName = request.JurisdictionName;
                existingRule.RegulatoryFramework = request.RegulatoryFramework;
                existingRule.IsActive = request.IsActive;
                existingRule.Priority = request.Priority;
                existingRule.Requirements = request.Requirements ?? new List<ComplianceRequirement>();
                existingRule.UpdatedBy = updatedBy;
                existingRule.Notes = request.Notes;

                var updatedRule = await _rulesRepository.UpdateRuleAsync(existingRule);

                _logger.LogInformation("Updated jurisdiction rule {RuleId} for {JurisdictionCode}",
                    LoggingHelper.SanitizeLogInput(updatedRule.Id), 
                    LoggingHelper.SanitizeLogInput(updatedRule.JurisdictionCode));

                return new JurisdictionRuleResponse
                {
                    Success = true,
                    Rule = updatedRule
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));
                return new JurisdictionRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<BaseResponse> DeleteRuleAsync(string ruleId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

            try
            {
                var deleted = await _rulesRepository.DeleteRuleAsync(ruleId);
                if (!deleted)
                {
                    return new BaseResponse
                    {
                        Success = false,
                        ErrorMessage = "Jurisdiction rule not found"
                    };
                }

                _logger.LogInformation("Deleted jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));

                return new BaseResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));
                return new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<JurisdictionEvaluationResult> EvaluateTokenComplianceAsync(ulong assetId, string network, string issuerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(network);
            ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);

            try
            {
                var result = new JurisdictionEvaluationResult
                {
                    AssetId = assetId,
                    Network = network,
                    EvaluatedAt = DateTime.UtcNow
                };

                // Get token's assigned jurisdictions
                var tokenJurisdictions = await _rulesRepository.GetTokenJurisdictionsAsync(assetId, network);
                
                // If no jurisdictions assigned, use global baseline
                if (tokenJurisdictions.Count == 0)
                {
                    tokenJurisdictions.Add(new TokenJurisdiction
                    {
                        AssetId = assetId,
                        Network = network,
                        JurisdictionCode = "GLOBAL",
                        IsPrimary = true,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = "System",
                        Notes = "Auto-assigned default jurisdiction"
                    });
                }

                result.ApplicableJurisdictions = tokenJurisdictions.Select(j => j.JurisdictionCode).ToList();

                // Get compliance metadata for the token
                var complianceMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(assetId);

                // Evaluate each applicable jurisdiction
                var allChecks = new List<JurisdictionComplianceCheck>();
                var passCount = 0;
                var failCount = 0;
                var totalMandatory = 0;

                foreach (var jurisdiction in tokenJurisdictions)
                {
                    var rule = await _rulesRepository.GetRuleByJurisdictionCodeAsync(jurisdiction.JurisdictionCode);
                    if (rule == null || !rule.IsActive)
                    {
                        result.Rationale.Add($"No active rule found for jurisdiction {jurisdiction.JurisdictionCode}");
                        continue;
                    }

                    // Evaluate each requirement
                    foreach (var requirement in rule.Requirements)
                    {
                        var checkResult = EvaluateRequirement(requirement, complianceMetadata, assetId, network);
                        checkResult.JurisdictionCode = jurisdiction.JurisdictionCode;
                        allChecks.Add(checkResult);

                        if (requirement.IsMandatory)
                        {
                            totalMandatory++;
                            if (checkResult.Status == "Pass")
                            {
                                passCount++;
                            }
                            else if (checkResult.Status == "Fail")
                            {
                                failCount++;
                                result.Rationale.Add($"Failed: {requirement.RequirementCode} - {requirement.Description}");
                            }
                        }
                    }
                }

                result.CheckResults = allChecks;

                // Determine overall compliance status
                if (totalMandatory == 0)
                {
                    result.ComplianceStatus = "Unknown";
                    result.Rationale.Add("No mandatory requirements found for evaluation");
                }
                else if (passCount == totalMandatory)
                {
                    result.ComplianceStatus = "Compliant";
                    result.Rationale.Add($"All {totalMandatory} mandatory requirements passed");
                }
                else if (passCount > 0)
                {
                    result.ComplianceStatus = "PartiallyCompliant";
                    result.Rationale.Add($"Passed {passCount} of {totalMandatory} mandatory requirements");
                }
                else
                {
                    result.ComplianceStatus = "NonCompliant";
                    result.Rationale.Add($"Failed all {totalMandatory} mandatory requirements");
                }

                _logger.LogInformation("Evaluated compliance for token {AssetId} on {Network}: {Status}",
                    assetId, LoggingHelper.SanitizeLogInput(network), result.ComplianceStatus);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating token compliance for {AssetId} on {Network}", assetId, LoggingHelper.SanitizeLogInput(network));
                
                return new JurisdictionEvaluationResult
                {
                    AssetId = assetId,
                    Network = network,
                    ComplianceStatus = "Unknown",
                    Rationale = new List<string> { $"Error during evaluation: {ex.Message}" },
                    EvaluatedAt = DateTime.UtcNow
                };
            }
        }

        /// <inheritdoc/>
        public async Task<BaseResponse> AssignTokenJurisdictionAsync(ulong assetId, string network, 
            string jurisdictionCode, bool isPrimary, string assignedBy, string? notes = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(network);
            ArgumentException.ThrowIfNullOrWhiteSpace(jurisdictionCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(assignedBy);

            try
            {
                // Validate that the jurisdiction rule exists
                var rule = await _rulesRepository.GetRuleByJurisdictionCodeAsync(jurisdictionCode);
                if (rule == null)
                {
                    return new BaseResponse
                    {
                        Success = false,
                        ErrorMessage = $"Jurisdiction rule not found for code {jurisdictionCode}"
                    };
                }

                var tokenJurisdiction = new TokenJurisdiction
                {
                    AssetId = assetId,
                    Network = network,
                    JurisdictionCode = jurisdictionCode,
                    IsPrimary = isPrimary,
                    AssignedBy = assignedBy,
                    Notes = notes
                };

                await _rulesRepository.AssignTokenJurisdictionAsync(tokenJurisdiction);

                _logger.LogInformation("Assigned jurisdiction {JurisdictionCode} to token {AssetId} on {Network}",
                    LoggingHelper.SanitizeLogInput(jurisdictionCode), assetId, LoggingHelper.SanitizeLogInput(network));

                return new BaseResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning jurisdiction to token {AssetId}", assetId);
                return new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<List<TokenJurisdiction>> GetTokenJurisdictionsAsync(ulong assetId, string network)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(network);

            try
            {
                return await _rulesRepository.GetTokenJurisdictionsAsync(assetId, network);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token jurisdictions for {AssetId}", assetId);
                return new List<TokenJurisdiction>();
            }
        }

        /// <inheritdoc/>
        public async Task<BaseResponse> RemoveTokenJurisdictionAsync(ulong assetId, string network, string jurisdictionCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(network);
            ArgumentException.ThrowIfNullOrWhiteSpace(jurisdictionCode);

            try
            {
                var removed = await _rulesRepository.RemoveTokenJurisdictionAsync(assetId, network, jurisdictionCode);
                if (!removed)
                {
                    return new BaseResponse
                    {
                        Success = false,
                        ErrorMessage = "Jurisdiction assignment not found"
                    };
                }

                _logger.LogInformation("Removed jurisdiction {JurisdictionCode} from token {AssetId} on {Network}",
                    LoggingHelper.SanitizeLogInput(jurisdictionCode), assetId, LoggingHelper.SanitizeLogInput(network));

                return new BaseResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing jurisdiction from token {AssetId}", assetId);
                return new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Evaluates a single compliance requirement
        /// </summary>
        private JurisdictionComplianceCheck EvaluateRequirement(ComplianceRequirement requirement, 
            ComplianceMetadata? metadata, ulong assetId, string network)
        {
            var result = new JurisdictionComplianceCheck
            {
                RequirementCode = requirement.RequirementCode,
                CheckedAt = DateTime.UtcNow
            };

            // Evaluate based on requirement code
            switch (requirement.RequirementCode)
            {
                case "MICA_ARTICLE_20":
                case "FATF_KYC":
                    // Check KYC verification status
                    if (metadata?.KycVerificationDate != null && metadata.VerificationStatus == VerificationStatus.Verified)
                    {
                        result.Status = "Pass";
                        result.Evidence = $"KYC verified on {metadata.KycVerificationDate:yyyy-MM-dd} by {metadata.KycProvider}";
                    }
                    else
                    {
                        result.Status = "Fail";
                        result.Evidence = "KYC verification not completed or expired";
                    }
                    break;

                case "MICA_ARTICLE_23":
                case "FATF_AML":
                    // Check AML procedures
                    if (!string.IsNullOrWhiteSpace(metadata?.KycProvider))
                    {
                        result.Status = "Pass";
                        result.Evidence = $"AML procedures in place via {metadata.KycProvider}";
                    }
                    else
                    {
                        result.Status = "Fail";
                        result.Evidence = "No AML provider configured";
                    }
                    break;

                case "MICA_ISSUER_PROFILE":
                    // Check issuer profile completeness (would need issuer profile service)
                    result.Status = "Partial";
                    result.Evidence = "Issuer profile evaluation requires additional data";
                    break;

                case "MICA_ARTICLE_17":
                case "MICA_ARTICLE_18":
                case "MICA_ARTICLE_30":
                    // These require external evidence
                    result.Status = "NotApplicable";
                    result.Evidence = "Manual review required - regulatory filing verification";
                    break;

                default:
                    result.Status = "NotApplicable";
                    result.Evidence = "Evaluation criteria not implemented";
                    break;
            }

            return result;
        }
    }
}

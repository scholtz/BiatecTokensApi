using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository implementation for jurisdiction rules
    /// </summary>
    public class JurisdictionRulesRepository : IJurisdictionRulesRepository
    {
        private readonly ConcurrentDictionary<string, JurisdictionRule> _rules = new();
        private readonly ConcurrentDictionary<string, List<TokenJurisdiction>> _tokenJurisdictions = new();
        private readonly ILogger<JurisdictionRulesRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="JurisdictionRulesRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public JurisdictionRulesRepository(ILogger<JurisdictionRulesRepository> logger)
        {
            _logger = logger;
            SeedDefaultRules();
        }

        /// <inheritdoc/>
        public Task<JurisdictionRule> CreateRuleAsync(JurisdictionRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);

            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                rule.Id = Guid.NewGuid().ToString();
            }

            rule.CreatedAt = DateTime.UtcNow;

            if (_rules.TryAdd(rule.Id, rule))
            {
                _logger.LogInformation("Created jurisdiction rule: {RuleId} for jurisdiction {JurisdictionCode}",
                    rule.Id, rule.JurisdictionCode);
                return Task.FromResult(rule);
            }

            throw new InvalidOperationException($"Rule with ID {rule.Id} already exists");
        }

        /// <inheritdoc/>
        public Task<JurisdictionRule?> GetRuleByIdAsync(string ruleId)
        {
            _rules.TryGetValue(ruleId, out var rule);
            return Task.FromResult(rule);
        }

        /// <inheritdoc/>
        public Task<JurisdictionRule?> GetRuleByJurisdictionCodeAsync(string jurisdictionCode)
        {
            var rule = _rules.Values.FirstOrDefault(r => 
                r.JurisdictionCode.Equals(jurisdictionCode, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(rule);
        }

        /// <inheritdoc/>
        public Task<(List<JurisdictionRule> Rules, int TotalCount)> ListRulesAsync(ListJurisdictionRulesRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = _rules.Values.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.JurisdictionCode))
            {
                query = query.Where(r => r.JurisdictionCode.Equals(request.JurisdictionCode, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.RegulatoryFramework))
            {
                query = query.Where(r => r.RegulatoryFramework.Equals(request.RegulatoryFramework, StringComparison.OrdinalIgnoreCase));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(r => r.IsActive == request.IsActive.Value);
            }

            var totalCount = query.Count();

            // Apply pagination
            var rules = query
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.JurisdictionCode)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(Math.Min(request.PageSize, 100))
                .ToList();

            return Task.FromResult((rules, totalCount));
        }

        /// <inheritdoc/>
        public Task<JurisdictionRule> UpdateRuleAsync(JurisdictionRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentException.ThrowIfNullOrWhiteSpace(rule.Id);

            if (!_rules.ContainsKey(rule.Id))
            {
                throw new InvalidOperationException($"Rule with ID {rule.Id} not found");
            }

            rule.UpdatedAt = DateTime.UtcNow;
            _rules[rule.Id] = rule;

            _logger.LogInformation("Updated jurisdiction rule: {RuleId} for jurisdiction {JurisdictionCode}",
                rule.Id, rule.JurisdictionCode);

            return Task.FromResult(rule);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteRuleAsync(string ruleId)
        {
            var removed = _rules.TryRemove(ruleId, out _);
            if (removed)
            {
                _logger.LogInformation("Deleted jurisdiction rule: {RuleId}", ruleId);
            }
            return Task.FromResult(removed);
        }

        /// <inheritdoc/>
        public Task<List<JurisdictionRule>> GetActiveRulesAsync()
        {
            var activeRules = _rules.Values
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.JurisdictionCode)
                .ToList();

            return Task.FromResult(activeRules);
        }

        /// <inheritdoc/>
        public Task<List<TokenJurisdiction>> GetTokenJurisdictionsAsync(ulong assetId, string network)
        {
            var key = GetTokenJurisdictionKey(assetId, network);
            _tokenJurisdictions.TryGetValue(key, out var jurisdictions);
            return Task.FromResult(jurisdictions ?? new List<TokenJurisdiction>());
        }

        /// <inheritdoc/>
        public Task<TokenJurisdiction> AssignTokenJurisdictionAsync(TokenJurisdiction tokenJurisdiction)
        {
            ArgumentNullException.ThrowIfNull(tokenJurisdiction);

            var key = GetTokenJurisdictionKey(tokenJurisdiction.AssetId, tokenJurisdiction.Network);
            var jurisdictions = _tokenJurisdictions.GetOrAdd(key, _ => new List<TokenJurisdiction>());

            // If this is marked as primary, unmark other primary jurisdictions
            if (tokenJurisdiction.IsPrimary)
            {
                foreach (var existing in jurisdictions)
                {
                    existing.IsPrimary = false;
                }
            }

            // Remove existing entry for this jurisdiction if present
            jurisdictions.RemoveAll(j => j.JurisdictionCode.Equals(tokenJurisdiction.JurisdictionCode, StringComparison.OrdinalIgnoreCase));
            
            tokenJurisdiction.AssignedAt = DateTime.UtcNow;
            jurisdictions.Add(tokenJurisdiction);

            _logger.LogInformation("Assigned jurisdiction {JurisdictionCode} to token {AssetId} on network {Network}",
                tokenJurisdiction.JurisdictionCode, tokenJurisdiction.AssetId, tokenJurisdiction.Network);

            return Task.FromResult(tokenJurisdiction);
        }

        /// <inheritdoc/>
        public Task<bool> RemoveTokenJurisdictionAsync(ulong assetId, string network, string jurisdictionCode)
        {
            var key = GetTokenJurisdictionKey(assetId, network);
            if (_tokenJurisdictions.TryGetValue(key, out var jurisdictions))
            {
                var removed = jurisdictions.RemoveAll(j => 
                    j.JurisdictionCode.Equals(jurisdictionCode, StringComparison.OrdinalIgnoreCase)) > 0;

                if (removed)
                {
                    _logger.LogInformation("Removed jurisdiction {JurisdictionCode} from token {AssetId} on network {Network}",
                        jurisdictionCode, assetId, network);
                }

                return Task.FromResult(removed);
            }

            return Task.FromResult(false);
        }

        private static string GetTokenJurisdictionKey(ulong assetId, string network)
        {
            return $"{network}:{assetId}";
        }

        /// <summary>
        /// Seeds default MICA jurisdiction rules for EU
        /// </summary>
        private void SeedDefaultRules()
        {
            try
            {
                // Seed EU MICA rules
                var euMicaRule = new JurisdictionRule
                {
                    Id = "eu-mica-baseline",
                    JurisdictionCode = "EU",
                    JurisdictionName = "European Union",
                    RegulatoryFramework = "MICA",
                    IsActive = true,
                    Priority = 100,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    Version = "1.0",
                    Notes = "Baseline MICA compliance requirements for EU token issuance",
                    Requirements = new List<ComplianceRequirement>
                    {
                        new ComplianceRequirement
                        {
                            RequirementCode = "MICA_ARTICLE_17",
                            Category = "Disclosure",
                            Description = "White paper publication and disclosure requirements",
                            IsMandatory = true,
                            Severity = RequirementSeverity.Critical,
                            RegulatoryReference = "MICA Article 17",
                            RemediationGuidance = "Publish comprehensive white paper with required disclosures"
                        },
                        new ComplianceRequirement
                        {
                            RequirementCode = "MICA_ARTICLE_18",
                            Category = "Marketing",
                            Description = "Marketing communications must be clear, fair, and not misleading",
                            IsMandatory = true,
                            Severity = RequirementSeverity.High,
                            RegulatoryReference = "MICA Article 18",
                            RemediationGuidance = "Review and update marketing materials to comply with MICA standards"
                        },
                        new ComplianceRequirement
                        {
                            RequirementCode = "MICA_ARTICLE_20",
                            Category = "KYC",
                            Description = "Know Your Customer (KYC) requirements for token holders",
                            IsMandatory = true,
                            Severity = RequirementSeverity.Critical,
                            RegulatoryReference = "MICA Article 20",
                            RemediationGuidance = "Implement KYC verification process for all token holders"
                        },
                        new ComplianceRequirement
                        {
                            RequirementCode = "MICA_ARTICLE_23",
                            Category = "AML",
                            Description = "Anti-Money Laundering (AML) procedures",
                            IsMandatory = true,
                            Severity = RequirementSeverity.Critical,
                            RegulatoryReference = "MICA Article 23",
                            RemediationGuidance = "Establish comprehensive AML procedures and monitoring"
                        },
                        new ComplianceRequirement
                        {
                            RequirementCode = "MICA_ARTICLE_30",
                            Category = "Licensing",
                            Description = "Notification or authorization requirements",
                            IsMandatory = true,
                            Severity = RequirementSeverity.Critical,
                            RegulatoryReference = "MICA Article 30",
                            RemediationGuidance = "Obtain necessary authorization from competent authority"
                        },
                        new ComplianceRequirement
                        {
                            RequirementCode = "MICA_ISSUER_PROFILE",
                            Category = "Disclosure",
                            Description = "Issuer profile completeness with legal entity details",
                            IsMandatory = true,
                            Severity = RequirementSeverity.High,
                            RegulatoryReference = "MICA General",
                            RemediationGuidance = "Complete issuer profile with all required legal entity information"
                        }
                    }
                };

                _rules.TryAdd(euMicaRule.Id, euMicaRule);
                _logger.LogInformation("Seeded default EU MICA jurisdiction rules");

                // Seed Global baseline (minimal requirements)
                var globalRule = new JurisdictionRule
                {
                    Id = "global-baseline",
                    JurisdictionCode = "GLOBAL",
                    JurisdictionName = "Global Baseline",
                    RegulatoryFramework = "FATF",
                    IsActive = true,
                    Priority = 50,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    Version = "1.0",
                    Notes = "Baseline FATF compliance requirements applicable globally",
                    Requirements = new List<ComplianceRequirement>
                    {
                        new ComplianceRequirement
                        {
                            RequirementCode = "FATF_KYC",
                            Category = "KYC",
                            Description = "Basic Know Your Customer verification",
                            IsMandatory = true,
                            Severity = RequirementSeverity.High,
                            RegulatoryReference = "FATF Recommendation 10",
                            RemediationGuidance = "Implement basic KYC verification process"
                        },
                        new ComplianceRequirement
                        {
                            RequirementCode = "FATF_AML",
                            Category = "AML",
                            Description = "Anti-Money Laundering monitoring",
                            IsMandatory = true,
                            Severity = RequirementSeverity.High,
                            RegulatoryReference = "FATF Recommendations",
                            RemediationGuidance = "Establish AML monitoring and reporting procedures"
                        }
                    }
                };

                _rules.TryAdd(globalRule.Id, globalRule);
                _logger.LogInformation("Seeded default Global baseline jurisdiction rules");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding default jurisdiction rules");
            }
        }
    }
}

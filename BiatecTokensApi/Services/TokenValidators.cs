using BiatecTokensApi.Models.Compliance;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Validator for Algorand Standard Assets (ASA)
    /// </summary>
    internal class AsaValidator : ITokenValidator
    {
        public List<RuleEvaluation> Validate(object metadata, ValidationContext context)
        {
            var evaluations = new List<RuleEvaluation>();

            // Convert metadata to dictionary for rule checks
            var metadataDict = ConvertToDictionary(metadata);

            // Rule 1: Asset name is required and must be <= 32 characters
            evaluations.Add(ValidateAssetName(metadataDict));

            // Rule 2: Unit name is required and must be <= 8 characters
            evaluations.Add(ValidateUnitName(metadataDict));

            // Rule 3: Total supply must be > 0
            evaluations.Add(ValidateTotalSupply(metadataDict));

            // Rule 4: Decimals must be between 0 and 19
            evaluations.Add(ValidateDecimals(metadataDict));

            // Rule 5: Network-specific validation
            evaluations.Add(ValidateNetwork(context.Network));

            // Rule 6: Metadata URL if provided must be valid
            evaluations.Add(ValidateMetadataUrl(metadataDict));

            return evaluations;
        }

        private RuleEvaluation ValidateAssetName(Dictionary<string, object?> metadata)
        {
            var hasName = metadata.TryGetValue("AssetName", out var nameObj) ||
                         metadata.TryGetValue("assetName", out nameObj) ||
                         metadata.TryGetValue("name", out nameObj);

            var name = nameObj?.ToString() ?? "";

            if (!hasName || string.IsNullOrWhiteSpace(name))
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-001",
                    RuleName = "Asset Name Required",
                    Description = "ASA tokens must have an asset name",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Asset name is required",
                    RemediationSteps = "Provide a non-empty asset name in the AssetName field"
                };
            }

            if (name.Length > 32)
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-001",
                    RuleName = "Asset Name Required",
                    Description = "ASA tokens must have an asset name",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = $"Asset name is too long ({name.Length} characters, max 32)",
                    RemediationSteps = "Shorten the asset name to 32 characters or less"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ASA-001",
                RuleName = "Asset Name Required",
                Description = "ASA tokens must have an asset name",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateUnitName(Dictionary<string, object?> metadata)
        {
            var hasUnit = metadata.TryGetValue("UnitName", out var unitObj) ||
                         metadata.TryGetValue("unitName", out unitObj) ||
                         metadata.TryGetValue("unit", out unitObj);

            var unit = unitObj?.ToString() ?? "";

            if (!hasUnit || string.IsNullOrWhiteSpace(unit))
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-002",
                    RuleName = "Unit Name Required",
                    Description = "ASA tokens must have a unit name",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Unit name is required",
                    RemediationSteps = "Provide a non-empty unit name in the UnitName field"
                };
            }

            if (unit.Length > 8)
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-002",
                    RuleName = "Unit Name Required",
                    Description = "ASA tokens must have a unit name",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = $"Unit name is too long ({unit.Length} characters, max 8)",
                    RemediationSteps = "Shorten the unit name to 8 characters or less"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ASA-002",
                RuleName = "Unit Name Required",
                Description = "ASA tokens must have a unit name",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateTotalSupply(Dictionary<string, object?> metadata)
        {
            var hasTotal = metadata.TryGetValue("Total", out var totalObj) ||
                          metadata.TryGetValue("total", out totalObj) ||
                          metadata.TryGetValue("totalSupply", out totalObj);

            if (!hasTotal || totalObj == null)
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-003",
                    RuleName = "Total Supply Required",
                    Description = "ASA tokens must specify total supply",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Total supply is required",
                    RemediationSteps = "Specify the total supply in the Total field (must be > 0)"
                };
            }

            try
            {
                var total = Convert.ToUInt64(totalObj);
                if (total == 0)
                {
                    return new RuleEvaluation
                    {
                        RuleId = "ASA-003",
                        RuleName = "Total Supply Required",
                        Description = "ASA tokens must specify total supply",
                        Passed = false,
                        Category = "Metadata",
                        Severity = ValidationSeverity.Error,
                        ErrorMessage = "Total supply must be greater than 0",
                        RemediationSteps = "Set the total supply to a value greater than 0"
                    };
                }
            }
            catch
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-003",
                    RuleName = "Total Supply Required",
                    Description = "ASA tokens must specify total supply",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Total supply must be a valid number",
                    RemediationSteps = "Provide a valid numeric value for total supply"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ASA-003",
                RuleName = "Total Supply Required",
                Description = "ASA tokens must specify total supply",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateDecimals(Dictionary<string, object?> metadata)
        {
            var hasDecimals = metadata.TryGetValue("Decimals", out var decimalsObj) ||
                             metadata.TryGetValue("decimals", out decimalsObj);

            if (!hasDecimals || decimalsObj == null)
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-004",
                    RuleName = "Decimals Specification",
                    Description = "ASA tokens should specify decimals (0-19)",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Warning,
                    ErrorMessage = "Decimals not specified (will default to 0)",
                    RemediationSteps = "Specify decimals field (0-19) to indicate fractional units"
                };
            }

            try
            {
                var decimals = Convert.ToUInt32(decimalsObj);
                if (decimals > 19)
                {
                    return new RuleEvaluation
                    {
                        RuleId = "ASA-004",
                        RuleName = "Decimals Specification",
                        Description = "ASA tokens should specify decimals (0-19)",
                        Passed = false,
                        Category = "Metadata",
                        Severity = ValidationSeverity.Error,
                        ErrorMessage = $"Decimals value {decimals} exceeds maximum of 19",
                        RemediationSteps = "Set decimals to a value between 0 and 19"
                    };
                }
            }
            catch
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-004",
                    RuleName = "Decimals Specification",
                    Description = "ASA tokens should specify decimals (0-19)",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Decimals must be a valid number",
                    RemediationSteps = "Provide a valid numeric value for decimals (0-19)"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ASA-004",
                RuleName = "Decimals Specification",
                Description = "ASA tokens should specify decimals (0-19)",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Warning
            };
        }

        private RuleEvaluation ValidateNetwork(string network)
        {
            var validNetworks = new[]
            {
                "mainnet", "testnet", "betanet",
                "voimain-v1.0", "voitest-v1", "sandbox-v1",
                "aramidmain-v1.0", "aramidtest-v1"
            };

            if (string.IsNullOrWhiteSpace(network))
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-005",
                    RuleName = "Network Specification",
                    Description = "Token deployment must specify a valid network",
                    Passed = false,
                    Category = "Network",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Network not specified",
                    RemediationSteps = $"Specify a valid network: {string.Join(", ", validNetworks)}"
                };
            }

            if (!validNetworks.Contains(network.ToLowerInvariant()))
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-005",
                    RuleName = "Network Specification",
                    Description = "Token deployment must specify a valid network",
                    Passed = false,
                    Category = "Network",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = $"Invalid network: {network}",
                    RemediationSteps = $"Use one of: {string.Join(", ", validNetworks)}"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ASA-005",
                RuleName = "Network Specification",
                Description = "Token deployment must specify a valid network",
                Passed = true,
                Category = "Network",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateMetadataUrl(Dictionary<string, object?> metadata)
        {
            var hasUrl = metadata.TryGetValue("URL", out var urlObj) ||
                        metadata.TryGetValue("url", out urlObj) ||
                        metadata.TryGetValue("metadataUrl", out urlObj);

            if (!hasUrl || urlObj == null || string.IsNullOrWhiteSpace(urlObj.ToString()))
            {
                // URL is optional, so skip this rule
                return new RuleEvaluation
                {
                    RuleId = "ASA-006",
                    RuleName = "Metadata URL Validation",
                    Description = "If provided, metadata URL should be valid",
                    Passed = true,
                    Skipped = true,
                    SkipReason = "Metadata URL not provided (optional)",
                    Category = "Metadata",
                    Severity = ValidationSeverity.Warning
                };
            }

            var url = urlObj.ToString() ?? "";

            // Basic URL validation
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-006",
                    RuleName = "Metadata URL Validation",
                    Description = "If provided, metadata URL should be valid",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Warning,
                    ErrorMessage = "Invalid URL format",
                    RemediationSteps = "Provide a valid absolute URL (e.g., https://example.com/metadata.json)"
                };
            }

            if (url.Length > 96)
            {
                return new RuleEvaluation
                {
                    RuleId = "ASA-006",
                    RuleName = "Metadata URL Validation",
                    Description = "If provided, metadata URL should be valid",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = $"URL is too long ({url.Length} characters, max 96)",
                    RemediationSteps = "Shorten the URL to 96 characters or less"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ASA-006",
                RuleName = "Metadata URL Validation",
                Description = "If provided, metadata URL should be valid",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Warning
            };
        }

        private Dictionary<string, object?> ConvertToDictionary(object metadata)
        {
            if (metadata is Dictionary<string, object?> dict)
            {
                return dict;
            }

            // Try to convert via JSON serialization
            try
            {
                var json = JsonSerializer.Serialize(metadata);
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }
    }

    /// <summary>
    /// Validator for ARC3 tokens (NFTs with IPFS metadata)
    /// </summary>
    internal class Arc3Validator : AsaValidator
    {
        public new List<RuleEvaluation> Validate(object metadata, ValidationContext context)
        {
            // Start with base ASA validation
            var evaluations = base.Validate(metadata, context);

            // Add ARC3-specific rules
            var metadataDict = ConvertToDictionary(metadata);

            // Rule: ARC3 requires metadata URL
            evaluations.Add(ValidateArc3MetadataRequired(metadataDict));

            return evaluations;
        }

        private RuleEvaluation ValidateArc3MetadataRequired(Dictionary<string, object?> metadata)
        {
            var hasUrl = metadata.TryGetValue("URL", out var urlObj) ||
                        metadata.TryGetValue("url", out urlObj) ||
                        metadata.TryGetValue("metadataUrl", out urlObj);

            if (!hasUrl || urlObj == null || string.IsNullOrWhiteSpace(urlObj.ToString()))
            {
                return new RuleEvaluation
                {
                    RuleId = "ARC3-001",
                    RuleName = "ARC3 Metadata URL Required",
                    Description = "ARC3 tokens require an IPFS metadata URL",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "ARC3 tokens must have a metadata URL pointing to IPFS",
                    RemediationSteps = "Provide an IPFS URL in the URL or metadataUrl field (e.g., ipfs://...)"
                };
            }

            var url = urlObj.ToString() ?? "";
            if (!url.StartsWith("ipfs://", StringComparison.OrdinalIgnoreCase))
            {
                return new RuleEvaluation
                {
                    RuleId = "ARC3-001",
                    RuleName = "ARC3 Metadata URL Required",
                    Description = "ARC3 tokens require an IPFS metadata URL",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Warning,
                    ErrorMessage = "ARC3 metadata URL should use ipfs:// scheme",
                    RemediationSteps = "Use an IPFS URL with ipfs:// scheme for ARC3 compliance"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ARC3-001",
                RuleName = "ARC3 Metadata URL Required",
                Description = "ARC3 tokens require an IPFS metadata URL",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private Dictionary<string, object?> ConvertToDictionary(object metadata)
        {
            if (metadata is Dictionary<string, object?> dict)
            {
                return dict;
            }

            try
            {
                var json = JsonSerializer.Serialize(metadata);
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }
    }

    /// <summary>
    /// Validator for ARC200 tokens (Smart contract tokens)
    /// </summary>
    internal class Arc200Validator : AsaValidator
    {
        public new List<RuleEvaluation> Validate(object metadata, ValidationContext context)
        {
            // Start with base ASA validation
            var evaluations = base.Validate(metadata, context);

            // Add ARC200-specific rules
            var metadataDict = ConvertToDictionary(metadata);

            // Rule: ARC200 requires smart contract application ID
            evaluations.Add(ValidateArc200AppId(metadataDict));

            return evaluations;
        }

        private RuleEvaluation ValidateArc200AppId(Dictionary<string, object?> metadata)
        {
            var hasAppId = metadata.TryGetValue("AppId", out var appIdObj) ||
                          metadata.TryGetValue("appId", out appIdObj) ||
                          metadata.TryGetValue("applicationId", out appIdObj);

            if (!hasAppId || appIdObj == null)
            {
                // For pre-deployment validation, app ID might not exist yet
                return new RuleEvaluation
                {
                    RuleId = "ARC200-001",
                    RuleName = "ARC200 Application ID",
                    Description = "ARC200 tokens are deployed as smart contracts",
                    Passed = true,
                    Skipped = true,
                    SkipReason = "Application ID will be assigned upon deployment",
                    Category = "Metadata",
                    Severity = ValidationSeverity.Info
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ARC200-001",
                RuleName = "ARC200 Application ID",
                Description = "ARC200 tokens are deployed as smart contracts",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Info
            };
        }

        private Dictionary<string, object?> ConvertToDictionary(object metadata)
        {
            if (metadata is Dictionary<string, object?> dict)
            {
                return dict;
            }

            try
            {
                var json = JsonSerializer.Serialize(metadata);
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }
    }

    /// <summary>
    /// Validator for ERC20 tokens (Ethereum-compatible tokens)
    /// </summary>
    internal class Erc20Validator : ITokenValidator
    {
        public List<RuleEvaluation> Validate(object metadata, ValidationContext context)
        {
            var evaluations = new List<RuleEvaluation>();
            var metadataDict = ConvertToDictionary(metadata);

            // Rule 1: Token name is required
            evaluations.Add(ValidateTokenName(metadataDict));

            // Rule 2: Token symbol is required
            evaluations.Add(ValidateTokenSymbol(metadataDict));

            // Rule 3: Total supply or max supply must be specified
            evaluations.Add(ValidateSupply(metadataDict));

            // Rule 4: Network must be a valid EVM chain
            evaluations.Add(ValidateEvmNetwork(context.Network));

            return evaluations;
        }

        private RuleEvaluation ValidateTokenName(Dictionary<string, object?> metadata)
        {
            var hasName = metadata.TryGetValue("Name", out var nameObj) ||
                         metadata.TryGetValue("name", out nameObj) ||
                         metadata.TryGetValue("tokenName", out nameObj);

            var name = nameObj?.ToString() ?? "";

            if (!hasName || string.IsNullOrWhiteSpace(name))
            {
                return new RuleEvaluation
                {
                    RuleId = "ERC20-001",
                    RuleName = "Token Name Required",
                    Description = "ERC20 tokens must have a name",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Token name is required",
                    RemediationSteps = "Provide a non-empty token name"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ERC20-001",
                RuleName = "Token Name Required",
                Description = "ERC20 tokens must have a name",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateTokenSymbol(Dictionary<string, object?> metadata)
        {
            var hasSymbol = metadata.TryGetValue("Symbol", out var symbolObj) ||
                           metadata.TryGetValue("symbol", out symbolObj) ||
                           metadata.TryGetValue("tokenSymbol", out symbolObj);

            var symbol = symbolObj?.ToString() ?? "";

            if (!hasSymbol || string.IsNullOrWhiteSpace(symbol))
            {
                return new RuleEvaluation
                {
                    RuleId = "ERC20-002",
                    RuleName = "Token Symbol Required",
                    Description = "ERC20 tokens must have a symbol",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Token symbol is required",
                    RemediationSteps = "Provide a non-empty token symbol (typically 3-5 characters)"
                };
            }

            if (symbol.Length > 11)
            {
                return new RuleEvaluation
                {
                    RuleId = "ERC20-002",
                    RuleName = "Token Symbol Required",
                    Description = "ERC20 tokens must have a symbol",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Warning,
                    ErrorMessage = $"Token symbol is unusually long ({symbol.Length} characters)",
                    RemediationSteps = "Consider using a shorter symbol (3-5 characters is typical)"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ERC20-002",
                RuleName = "Token Symbol Required",
                Description = "ERC20 tokens must have a symbol",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateSupply(Dictionary<string, object?> metadata)
        {
            var hasSupply = metadata.TryGetValue("TotalSupply", out var supplyObj) ||
                           metadata.TryGetValue("totalSupply", out supplyObj) ||
                           metadata.TryGetValue("MaxSupply", out supplyObj) ||
                           metadata.TryGetValue("maxSupply", out supplyObj);

            if (!hasSupply || supplyObj == null)
            {
                return new RuleEvaluation
                {
                    RuleId = "ERC20-003",
                    RuleName = "Supply Specification",
                    Description = "ERC20 tokens must specify total or max supply",
                    Passed = false,
                    Category = "Metadata",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Total supply or max supply is required",
                    RemediationSteps = "Specify totalSupply for preminted tokens or maxSupply for mintable tokens"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ERC20-003",
                RuleName = "Supply Specification",
                Description = "ERC20 tokens must specify total or max supply",
                Passed = true,
                Category = "Metadata",
                Severity = ValidationSeverity.Error
            };
        }

        private RuleEvaluation ValidateEvmNetwork(string network)
        {
            var validEvmNetworks = new[] { "base", "ethereum", "polygon", "arbitrum", "optimism" };

            if (string.IsNullOrWhiteSpace(network))
            {
                return new RuleEvaluation
                {
                    RuleId = "ERC20-004",
                    RuleName = "EVM Network Specification",
                    Description = "ERC20 tokens must specify a valid EVM network",
                    Passed = false,
                    Category = "Network",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = "Network not specified",
                    RemediationSteps = $"Specify a valid EVM network: {string.Join(", ", validEvmNetworks)}"
                };
            }

            if (!validEvmNetworks.Contains(network.ToLowerInvariant()))
            {
                return new RuleEvaluation
                {
                    RuleId = "ERC20-004",
                    RuleName = "EVM Network Specification",
                    Description = "ERC20 tokens must specify a valid EVM network",
                    Passed = false,
                    Category = "Network",
                    Severity = ValidationSeverity.Error,
                    ErrorMessage = $"Invalid EVM network: {network}",
                    RemediationSteps = $"Use one of: {string.Join(", ", validEvmNetworks)}"
                };
            }

            return new RuleEvaluation
            {
                RuleId = "ERC20-004",
                RuleName = "EVM Network Specification",
                Description = "ERC20 tokens must specify a valid EVM network",
                Passed = true,
                Category = "Network",
                Severity = ValidationSeverity.Error
            };
        }

        private Dictionary<string, object?> ConvertToDictionary(object metadata)
        {
            if (metadata is Dictionary<string, object?> dict)
            {
                return dict;
            }

            try
            {
                var json = JsonSerializer.Serialize(metadata);
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }
    }
}

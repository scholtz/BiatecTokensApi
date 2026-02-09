using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing and validating token metadata
    /// </summary>
    public class TokenMetadataService : ITokenMetadataService
    {
        private readonly ILogger<TokenMetadataService> _logger;
        private readonly ITokenRegistryService _registryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenMetadataService"/> class
        /// </summary>
        public TokenMetadataService(
            ILogger<TokenMetadataService> logger,
            ITokenRegistryService registryService)
        {
            _logger = logger;
            _registryService = registryService;
        }

        /// <inheritdoc/>
        public async Task<EnrichedTokenMetadata?> GetMetadataAsync(string tokenIdentifier, string chain, bool includeValidation = true)
        {
            try
            {
                _logger.LogInformation(
                    "Retrieving metadata for token {TokenId} on chain {Chain}",
                    LoggingHelper.SanitizeLogInput(tokenIdentifier),
                    LoggingHelper.SanitizeLogInput(chain)
                );

                // Try to get from registry service first
                var registryResponse = await _registryService.GetTokenAsync(tokenIdentifier, chain);
                if (registryResponse == null || !registryResponse.Found || registryResponse.Token == null)
                {
                    _logger.LogWarning(
                        "Token not found in registry: {TokenId} on {Chain}",
                        LoggingHelper.SanitizeLogInput(tokenIdentifier),
                        LoggingHelper.SanitizeLogInput(chain)
                    );
                    return null;
                }

                // Convert registry entry to metadata
                var metadata = ConvertRegistryEntryToMetadata(registryResponse.Token);

                // Apply validation if requested
                if (includeValidation)
                {
                    metadata = await ValidateMetadataAsync(metadata);
                }

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving metadata for token {TokenId} on chain {Chain}",
                    LoggingHelper.SanitizeLogInput(tokenIdentifier),
                    LoggingHelper.SanitizeLogInput(chain)
                );
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<EnrichedTokenMetadata> UpsertMetadataAsync(EnrichedTokenMetadata metadata)
        {
            try
            {
                _logger.LogInformation(
                    "Upserting metadata for token {TokenId} on chain {Chain}",
                    LoggingHelper.SanitizeLogInput(metadata.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(metadata.Chain)
                );

                // Validate before upserting
                metadata = await ValidateMetadataAsync(metadata);
                metadata.LastUpdatedAt = DateTime.UtcNow;

                // Generate explorer URL if not provided
                if (string.IsNullOrEmpty(metadata.ExplorerUrl))
                {
                    metadata.ExplorerUrl = GenerateExplorerUrl(metadata.TokenIdentifier, metadata.Chain);
                }

                // Note: In a real implementation, this would save to database
                // For now, we're just returning the validated metadata

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error upserting metadata for token {TokenId} on chain {Chain}",
                    LoggingHelper.SanitizeLogInput(metadata.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(metadata.Chain)
                );
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<EnrichedTokenMetadata> ValidateMetadataAsync(EnrichedTokenMetadata metadata)
        {
            await Task.CompletedTask; // Placeholder for async operations

            var issues = new List<TokenMetadataValidationIssue>();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(metadata.Name))
            {
                issues.Add(new TokenMetadataValidationIssue
                {
                    Code = "METADATA_001",
                    Field = "Name",
                    Message = "Token name is required",
                    Severity = TokenMetadataIssueSeverity.Error,
                    Remediation = "Provide a descriptive name for the token (1-100 characters)"
                });
            }

            if (string.IsNullOrWhiteSpace(metadata.Symbol))
            {
                issues.Add(new TokenMetadataValidationIssue
                {
                    Code = "METADATA_002",
                    Field = "Symbol",
                    Message = "Token symbol is required",
                    Severity = TokenMetadataIssueSeverity.Error,
                    Remediation = "Provide a token symbol/ticker (1-20 characters, typically 3-5 uppercase letters)"
                });
            }

            // Validate optional but recommended fields
            if (string.IsNullOrWhiteSpace(metadata.Description))
            {
                issues.Add(new TokenMetadataValidationIssue
                {
                    Code = "METADATA_003",
                    Field = "Description",
                    Message = "Token description is missing",
                    Severity = TokenMetadataIssueSeverity.Warning,
                    Remediation = "Add a clear description explaining the token's purpose and use case"
                });
            }

            if (string.IsNullOrWhiteSpace(metadata.ImageUrl))
            {
                issues.Add(new TokenMetadataValidationIssue
                {
                    Code = "METADATA_004",
                    Field = "ImageUrl",
                    Message = "Token logo is missing",
                    Severity = TokenMetadataIssueSeverity.Warning,
                    Remediation = "Provide a URL to a token logo image (PNG, JPG, or SVG format recommended)"
                });
            }

            if (string.IsNullOrWhiteSpace(metadata.WebsiteUrl))
            {
                issues.Add(new TokenMetadataValidationIssue
                {
                    Code = "METADATA_005",
                    Field = "WebsiteUrl",
                    Message = "Token website is missing",
                    Severity = TokenMetadataIssueSeverity.Warning,
                    Remediation = "Provide the official token website URL for user reference"
                });
            }

            if (string.IsNullOrWhiteSpace(metadata.ExplorerUrl))
            {
                // Try to generate it automatically
                var explorerUrl = GenerateExplorerUrl(metadata.TokenIdentifier, metadata.Chain);
                if (!string.IsNullOrEmpty(explorerUrl))
                {
                    metadata.ExplorerUrl = explorerUrl;
                    issues.Add(new TokenMetadataValidationIssue
                    {
                        Code = "METADATA_006",
                        Field = "ExplorerUrl",
                        Message = "Explorer URL was auto-generated",
                        Severity = TokenMetadataIssueSeverity.Info,
                        Remediation = "Verify that the auto-generated explorer URL is correct"
                    });
                }
                else
                {
                    issues.Add(new TokenMetadataValidationIssue
                    {
                        Code = "METADATA_007",
                        Field = "ExplorerUrl",
                        Message = "Blockchain explorer URL is missing",
                        Severity = TokenMetadataIssueSeverity.Warning,
                        Remediation = "Provide a direct link to view the token on a blockchain explorer"
                    });
                }
            }

            // Validate URL formats
            ValidateUrlField(metadata.ImageUrl, "ImageUrl", "METADATA_008", issues);
            ValidateUrlField(metadata.WebsiteUrl, "WebsiteUrl", "METADATA_009", issues);
            ValidateUrlField(metadata.ExplorerUrl, "ExplorerUrl", "METADATA_010", issues);
            ValidateUrlField(metadata.DocumentationUrl, "DocumentationUrl", "METADATA_011", issues);

            // Calculate completeness score
            metadata.CompletenessScore = CalculateCompletenessScore(metadata);

            // Set validation status
            if (issues.Any(i => i.Severity == TokenMetadataIssueSeverity.Error))
            {
                metadata.ValidationStatus = TokenMetadataValidationStatus.Invalid;
            }
            else if (issues.Any(i => i.Severity == TokenMetadataIssueSeverity.Warning))
            {
                metadata.ValidationStatus = TokenMetadataValidationStatus.ValidWithWarnings;
            }
            else
            {
                metadata.ValidationStatus = TokenMetadataValidationStatus.Valid;
            }

            metadata.ValidationIssues = issues;

            _logger.LogInformation(
                "Metadata validation complete for {TokenId}: Status={Status}, Score={Score}, Issues={IssueCount}",
                LoggingHelper.SanitizeLogInput(metadata.TokenIdentifier),
                metadata.ValidationStatus,
                metadata.CompletenessScore,
                issues.Count
            );

            return metadata;
        }

        /// <inheritdoc/>
        public int CalculateCompletenessScore(EnrichedTokenMetadata metadata)
        {
            int score = 0;
            int maxScore = 100;

            // Required fields (already present = base 40 points)
            score += 20; // Name
            score += 20; // Symbol

            // High-value optional fields (15 points each)
            if (!string.IsNullOrWhiteSpace(metadata.Description)) score += 15;
            if (!string.IsNullOrWhiteSpace(metadata.ImageUrl)) score += 15;

            // Medium-value optional fields (10 points each)
            if (!string.IsNullOrWhiteSpace(metadata.WebsiteUrl)) score += 10;
            if (!string.IsNullOrWhiteSpace(metadata.ExplorerUrl)) score += 10;

            // Lower-value optional fields (5 points each)
            if (!string.IsNullOrWhiteSpace(metadata.DocumentationUrl)) score += 5;
            if (metadata.Tags?.Any() == true) score += 5;

            // Normalize to 0-100 scale
            return Math.Min(Math.Max(score, 0), maxScore);
        }

        /// <inheritdoc/>
        public string? GenerateExplorerUrl(string tokenIdentifier, string chain)
        {
            try
            {
                var chainLower = chain.ToLowerInvariant();

                // Algorand networks - check for algorand-specific identifiers first
                if (chainLower.Contains("algorand"))
                {
                    if (chainLower.Contains("mainnet") || chainLower == "algorand")
                    {
                        return $"https://explorer.perawallet.app/asset/{tokenIdentifier}";
                    }
                    else if (chainLower.Contains("testnet"))
                    {
                        return $"https://testnet.explorer.perawallet.app/asset/{tokenIdentifier}";
                    }
                    else if (chainLower.Contains("betanet"))
                    {
                        return $"https://betanet.explorer.perawallet.app/asset/{tokenIdentifier}";
                    }
                }
                
                // Separate testnet/betanet checks for backward compatibility with simple chain names
                if (chainLower == "testnet")
                {
                    return $"https://testnet.explorer.perawallet.app/asset/{tokenIdentifier}";
                }
                else if (chainLower == "betanet")
                {
                    return $"https://betanet.explorer.perawallet.app/asset/{tokenIdentifier}";
                }

                // VOI network
                if (chainLower.Contains("voi"))
                {
                    return $"https://voi.observer/explorer/asset/{tokenIdentifier}";
                }

                // Aramid network
                if (chainLower.Contains("aramid"))
                {
                    return $"https://explorer.aramidmain.a-wallet.net/asset/{tokenIdentifier}";
                }

                // Base network (EVM)
                if (chainLower.Contains("base"))
                {
                    if (chainLower.Contains("mainnet") || chainLower == "base")
                    {
                        return $"https://basescan.org/token/{tokenIdentifier}";
                    }
                    else if (chainLower.Contains("sepolia") || chainLower.Contains("testnet"))
                    {
                        return $"https://sepolia.basescan.org/token/{tokenIdentifier}";
                    }
                }

                // Ethereum networks
                if (chainLower.Contains("ethereum"))
                {
                    if (chainLower.Contains("mainnet") || chainLower == "ethereum")
                    {
                        return $"https://etherscan.io/token/{tokenIdentifier}";
                    }
                    else if (chainLower.Contains("sepolia"))
                    {
                        return $"https://sepolia.etherscan.io/token/{tokenIdentifier}";
                    }
                    else if (chainLower.Contains("goerli"))
                    {
                        return $"https://goerli.etherscan.io/token/{tokenIdentifier}";
                    }
                }

                _logger.LogWarning(
                    "Could not generate explorer URL for unsupported chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(chain)
                );
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating explorer URL for token {TokenId} on chain {Chain}",
                    LoggingHelper.SanitizeLogInput(tokenIdentifier),
                    LoggingHelper.SanitizeLogInput(chain)
                );
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<EnrichedTokenMetadata> EnrichMetadataAsync(EnrichedTokenMetadata metadata)
        {
            await Task.CompletedTask; // Placeholder for future enrichment from external sources

            // Generate missing explorer URL
            if (string.IsNullOrEmpty(metadata.ExplorerUrl))
            {
                metadata.ExplorerUrl = GenerateExplorerUrl(metadata.TokenIdentifier, metadata.Chain);
            }

            return metadata;
        }

        /// <inheritdoc/>
        public EnrichedTokenMetadata ApplyFallbacks(EnrichedTokenMetadata metadata)
        {
            // Apply fallback for description
            if (string.IsNullOrWhiteSpace(metadata.Description))
            {
                metadata.Description = $"{metadata.Name} ({metadata.Symbol}) is a token on {metadata.Chain}";
            }

            // Generate explorer URL if missing
            if (string.IsNullOrEmpty(metadata.ExplorerUrl))
            {
                metadata.ExplorerUrl = GenerateExplorerUrl(metadata.TokenIdentifier, metadata.Chain);
            }

            // Apply fallback tags
            if (metadata.Tags == null || !metadata.Tags.Any())
            {
                metadata.Tags = new List<string> { "token" };
                
                // Add chain-specific tags
                if (metadata.Chain.Contains("algorand", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.Tags.Add("algorand");
                }
                else if (metadata.Chain.Contains("base", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.Tags.Add("base");
                    metadata.Tags.Add("evm");
                }
                else if (metadata.Chain.Contains("ethereum", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.Tags.Add("ethereum");
                    metadata.Tags.Add("evm");
                }
            }

            return metadata;
        }

        /// <summary>
        /// Validates a URL field and adds issues if invalid
        /// </summary>
        private void ValidateUrlField(string? url, string fieldName, string errorCode, List<TokenMetadataValidationIssue> issues)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    issues.Add(new TokenMetadataValidationIssue
                    {
                        Code = errorCode,
                        Field = fieldName,
                        Message = $"{fieldName} is not a valid URL",
                        Severity = TokenMetadataIssueSeverity.Warning,
                        Remediation = $"Provide a valid absolute URL starting with http:// or https://"
                    });
                }
                else if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    // Only allow HTTP(S) schemes for security
                    issues.Add(new TokenMetadataValidationIssue
                    {
                        Code = errorCode,
                        Field = fieldName,
                        Message = $"{fieldName} must use http:// or https:// scheme",
                        Severity = TokenMetadataIssueSeverity.Error,
                        Remediation = $"Update the URL to use http:// or https:// instead of {uri.Scheme}://"
                    });
                }
            }
        }

        /// <summary>
        /// Converts a TokenRegistryEntry to EnrichedTokenMetadata
        /// </summary>
        private EnrichedTokenMetadata ConvertRegistryEntryToMetadata(BiatecTokensApi.Models.TokenRegistry.TokenRegistryEntry entry)
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = entry.Name,
                Symbol = entry.Symbol,
                Decimals = entry.Decimals,
                Description = entry.Description,
                ImageUrl = entry.LogoUrl,
                WebsiteUrl = entry.Website,
                TokenIdentifier = entry.TokenIdentifier,
                Chain = entry.Chain,
                Standards = entry.SupportedStandards,
                TotalSupply = entry.TotalSupply,
                Tags = entry.Tags,
                CreatedAt = entry.CreatedAt,
                LastUpdatedAt = entry.UpdatedAt,
                DataSource = entry.DataSource
            };

            // Generate explorer URL
            metadata.ExplorerUrl = GenerateExplorerUrl(entry.TokenIdentifier, entry.Chain);

            // Set verification status
            metadata.IsVerified = entry.Readiness.HasValidMetadata;

            return metadata;
        }
    }
}

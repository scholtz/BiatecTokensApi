using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for token registry operations
    /// </summary>
    /// <remarks>
    /// Provides business logic for managing the token registry, including validation,
    /// filtering, and retrieval operations.
    /// </remarks>
    public class TokenRegistryService : ITokenRegistryService
    {
        private readonly ITokenRegistryRepository _repository;
        private readonly ILogger<TokenRegistryService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRegistryService"/> class
        /// </summary>
        /// <param name="repository">Token registry repository</param>
        /// <param name="logger">Logger instance</param>
        public TokenRegistryService(
            ITokenRegistryRepository repository,
            ILogger<TokenRegistryService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ListTokenRegistryResponse> ListTokensAsync(ListTokenRegistryRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Listing tokens with filters - Standard: {Standard}, Chain: {Chain}, ComplianceStatus: {ComplianceStatus}, Page: {Page}, PageSize: {PageSize}",
                    LoggingHelper.SanitizeLogInput(request.Standard ?? "all"),
                    LoggingHelper.SanitizeLogInput(request.Chain ?? "all"),
                    LoggingHelper.SanitizeLogInput(request.ComplianceStatus?.ToString() ?? "all"),
                    LoggingHelper.SanitizeLogInput(request.Page.ToString()),
                    LoggingHelper.SanitizeLogInput(request.PageSize.ToString())
                );

                return await _repository.ListTokensAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tokens");
                return new ListTokenRegistryResponse
                {
                    TotalCount = 0,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = 0
                };
            }
        }

        /// <inheritdoc/>
        public async Task<GetTokenRegistryResponse> GetTokenAsync(string identifier, string? chain = null)
        {
            try
            {
                _logger.LogInformation(
                    "Getting token details for identifier: {Identifier}, chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(identifier),
                    LoggingHelper.SanitizeLogInput(chain ?? "any")
                );

                var token = await _repository.GetTokenByIdAsync(identifier, chain);

                if (token == null)
                {
                    return new GetTokenRegistryResponse
                    {
                        Found = false,
                        ErrorMessage = $"Token not found: {identifier}"
                    };
                }

                return new GetTokenRegistryResponse
                {
                    Found = true,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token {Identifier}", LoggingHelper.SanitizeLogInput(identifier));
                return new GetTokenRegistryResponse
                {
                    Found = false,
                    ErrorMessage = "An error occurred while retrieving the token"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<UpsertTokenRegistryResponse> UpsertTokenAsync(UpsertTokenRegistryRequest request, string? createdBy = null)
        {
            try
            {
                // Validate the request
                var validationResult = await ValidateUpsertRequestAsync(request);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning(
                        "Token upsert validation failed for {Symbol}: {Errors}",
                        LoggingHelper.SanitizeLogInput(request.Symbol),
                        LoggingHelper.SanitizeLogInput(string.Join(", ", validationResult.Errors))
                    );

                    return new UpsertTokenRegistryResponse
                    {
                        Success = false,
                        ErrorMessage = string.Join("; ", validationResult.Errors)
                    };
                }

                // Normalize and sanitize data
                await NormalizeRequestDataAsync(request);

                // Upsert the token
                return await _repository.UpsertTokenAsync(request, createdBy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting token {Symbol}", LoggingHelper.SanitizeLogInput(request.Symbol));
                return new UpsertTokenRegistryResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while upserting the token"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<List<TokenRegistryEntry>> SearchTokensAsync(string searchTerm, int limit = 10)
        {
            try
            {
                _logger.LogInformation(
                    "Searching tokens with term: {SearchTerm}, limit: {Limit}",
                    LoggingHelper.SanitizeLogInput(searchTerm),
                    LoggingHelper.SanitizeLogInput(limit.ToString())
                );

                return await _repository.SearchTokensAsync(searchTerm, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tokens with term {SearchTerm}", LoggingHelper.SanitizeLogInput(searchTerm));
                return new List<TokenRegistryEntry>();
            }
        }

        /// <inheritdoc/>
        public Task<RegistryValidationResult> ValidateTokenAsync(TokenRegistryEntry entry)
        {
            var result = new RegistryValidationResult { IsValid = true };

            // Validate required fields
            if (string.IsNullOrWhiteSpace(entry.TokenIdentifier))
            {
                result.Errors.Add("Token identifier is required");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(entry.Chain))
            {
                result.Errors.Add("Chain is required");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                result.Errors.Add("Token name is required");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(entry.Symbol))
            {
                result.Errors.Add("Token symbol is required");
                result.IsValid = false;
            }

            // Validate chain format
            if (!string.IsNullOrWhiteSpace(entry.Chain))
            {
                var validChains = new[] { "algorand-mainnet", "algorand-testnet", "voi-mainnet", "aramid-mainnet", 
                                         "ethereum-mainnet", "base-mainnet", "arbitrum-mainnet", "polygon-mainnet" };
                if (!validChains.Any(c => entry.Chain.Equals(c, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Warnings.Add($"Chain '{entry.Chain}' is not in the standard format");
                }
            }

            // Validate token standards
            if (entry.SupportedStandards.Any())
            {
                var validStandards = new[] { "ASA", "ARC3", "ARC19", "ARC69", "ARC200", "ARC1400", "ERC20", "ERC721", "ERC1155" };
                var invalidStandards = entry.SupportedStandards
                    .Where(s => !validStandards.Contains(s, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (invalidStandards.Any())
                {
                    result.Warnings.Add($"Unknown token standards: {string.Join(", ", invalidStandards)}");
                }
            }

            // Validate compliance scoring
            if (entry.Compliance.Score.HasValue && (entry.Compliance.Score < 0 || entry.Compliance.Score > 100))
            {
                result.Errors.Add("Compliance score must be between 0 and 100");
                result.IsValid = false;
            }

            // Validate URLs
            if (!string.IsNullOrWhiteSpace(entry.Website) && !Uri.TryCreate(entry.Website, UriKind.Absolute, out _))
            {
                result.Warnings.Add("Website URL is not valid");
            }

            if (!string.IsNullOrWhiteSpace(entry.LogoUrl) && !Uri.TryCreate(entry.LogoUrl, UriKind.Absolute, out _))
            {
                result.Warnings.Add("Logo URL is not valid");
            }

            // Add info messages
            if (entry.Readiness.IsAudited && !entry.Readiness.AuditReports.Any())
            {
                result.Info.Add("Token is marked as audited but no audit reports are provided");
            }

            if (entry.Compliance.Status == ComplianceState.Compliant && entry.Compliance.RegulatoryFrameworks.Count == 0)
            {
                result.Info.Add("Token is marked as compliant but no regulatory frameworks are specified");
            }

            return Task.FromResult(result);
        }

        private async Task<RegistryValidationResult> ValidateUpsertRequestAsync(UpsertTokenRegistryRequest request)
        {
            var result = new RegistryValidationResult { IsValid = true };

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.TokenIdentifier))
            {
                result.Errors.Add("Token identifier is required");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(request.Chain))
            {
                result.Errors.Add("Chain is required");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                result.Errors.Add("Token name is required");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                result.Errors.Add("Token symbol is required");
                result.IsValid = false;
            }

            // Validate compliance scoring
            if (request.Compliance?.Score.HasValue == true && 
                (request.Compliance.Score < 0 || request.Compliance.Score > 100))
            {
                result.Errors.Add("Compliance score must be between 0 and 100");
                result.IsValid = false;
            }

            return await Task.FromResult(result);
        }

        private Task NormalizeRequestDataAsync(UpsertTokenRegistryRequest request)
        {
            // Normalize chain name to lowercase-hyphenated format
            request.Chain = request.Chain.ToLowerInvariant();

            // Normalize standards to uppercase
            if (request.SupportedStandards != null)
            {
                request.SupportedStandards = request.SupportedStandards
                    .Select(s => s.ToUpperInvariant())
                    .Distinct()
                    .ToList();
            }

            // Normalize primary standard
            if (!string.IsNullOrWhiteSpace(request.PrimaryStandard))
            {
                request.PrimaryStandard = request.PrimaryStandard.ToUpperInvariant();
            }

            // Normalize tags to lowercase
            if (request.Tags != null)
            {
                request.Tags = request.Tags
                    .Select(t => t.ToLowerInvariant())
                    .Distinct()
                    .ToList();
            }

            // Trim whitespace from string fields
            request.Name = request.Name.Trim();
            request.Symbol = request.Symbol.Trim();
            if (request.Description != null)
                request.Description = request.Description.Trim();

            return Task.CompletedTask;
        }
    }
}

using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository implementation for token registry
    /// </summary>
    /// <remarks>
    /// Uses thread-safe concurrent collections for production-grade concurrency.
    /// Can be replaced with a database-backed implementation without API changes.
    /// </remarks>
    public class TokenRegistryRepository : ITokenRegistryRepository
    {
        private readonly ConcurrentDictionary<string, TokenRegistryEntry> _entries = new();
        private readonly ILogger<TokenRegistryRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRegistryRepository"/> class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public TokenRegistryRepository(ILogger<TokenRegistryRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<ListTokenRegistryResponse> ListTokensAsync(ListTokenRegistryRequest request)
        {
            try
            {
                var query = _entries.Values.AsEnumerable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(request.Standard))
                {
                    query = query.Where(t => t.SupportedStandards.Contains(request.Standard, StringComparer.OrdinalIgnoreCase) ||
                                            (t.PrimaryStandard != null && t.PrimaryStandard.Equals(request.Standard, StringComparison.OrdinalIgnoreCase)));
                }

                if (request.ComplianceStatus.HasValue)
                {
                    query = query.Where(t => t.Compliance.Status == request.ComplianceStatus.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.Chain))
                {
                    query = query.Where(t => t.Chain.Equals(request.Chain, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.IssuerAddress))
                {
                    query = query.Where(t => t.Issuer != null && 
                                            t.Issuer.Address != null &&
                                            t.Issuer.Address.Equals(request.IssuerAddress, StringComparison.OrdinalIgnoreCase));
                }

                if (request.IsContractVerified.HasValue)
                {
                    query = query.Where(t => t.Readiness.IsContractVerified == request.IsContractVerified.Value);
                }

                if (request.IsAudited.HasValue)
                {
                    query = query.Where(t => t.Readiness.IsAudited == request.IsAudited.Value);
                }

                if (request.HasValidMetadata.HasValue)
                {
                    query = query.Where(t => t.Readiness.HasValidMetadata == request.HasValidMetadata.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var searchLower = request.Search.ToLowerInvariant();
                    query = query.Where(t => 
                        t.Name.ToLowerInvariant().Contains(searchLower) ||
                        t.Symbol.ToLowerInvariant().Contains(searchLower) ||
                        (t.Description != null && t.Description.ToLowerInvariant().Contains(searchLower)));
                }

                if (!string.IsNullOrWhiteSpace(request.Tags))
                {
                    var tags = request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    query = query.Where(t => tags.Any(tag => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
                }

                if (!string.IsNullOrWhiteSpace(request.DataSource))
                {
                    query = query.Where(t => t.DataSource.Equals(request.DataSource, StringComparison.OrdinalIgnoreCase));
                }

                // Get total count before pagination
                var totalCount = query.Count();

                // Apply sorting
                query = ApplySorting(query, request.SortBy, request.SortDirection);

                // Apply pagination
                var skip = (request.Page - 1) * request.PageSize;
                var tokens = query.Skip(skip).Take(request.PageSize).ToList();

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                var response = new ListTokenRegistryResponse
                {
                    Tokens = tokens,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    HasNextPage = request.Page < totalPages,
                    HasPreviousPage = request.Page > 1
                };

                _logger.LogInformation(
                    "Listed {TokenCount} tokens (page {Page} of {TotalPages}, total {TotalCount})",
                    LoggingHelper.SanitizeLogInput(tokens.Count.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Page.ToString()),
                    LoggingHelper.SanitizeLogInput(totalPages.ToString()),
                    LoggingHelper.SanitizeLogInput(totalCount.ToString())
                );

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tokens from registry");
                return Task.FromResult(new ListTokenRegistryResponse());
            }
        }

        /// <inheritdoc/>
        public Task<TokenRegistryEntry?> GetTokenByIdAsync(string id, string? chain = null)
        {
            try
            {
                // Try direct ID lookup first
                if (_entries.TryGetValue(id, out var entry))
                {
                    return Task.FromResult<TokenRegistryEntry?>(entry);
                }

                // Try by token identifier
                var byIdentifier = _entries.Values.FirstOrDefault(t => 
                    t.TokenIdentifier.Equals(id, StringComparison.OrdinalIgnoreCase) &&
                    (chain == null || t.Chain.Equals(chain, StringComparison.OrdinalIgnoreCase)));

                if (byIdentifier != null)
                {
                    return Task.FromResult<TokenRegistryEntry?>(byIdentifier);
                }

                // Try by symbol
                var bySymbol = _entries.Values.FirstOrDefault(t => 
                    t.Symbol.Equals(id, StringComparison.OrdinalIgnoreCase) &&
                    (chain == null || t.Chain.Equals(chain, StringComparison.OrdinalIgnoreCase)));

                return Task.FromResult(bySymbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token by ID {Id}", LoggingHelper.SanitizeLogInput(id));
                return Task.FromResult<TokenRegistryEntry?>(null);
            }
        }

        /// <inheritdoc/>
        public Task<TokenRegistryEntry?> GetTokenByIdentifierAsync(string tokenIdentifier, string chain)
        {
            try
            {
                var entry = _entries.Values.FirstOrDefault(t => 
                    t.TokenIdentifier.Equals(tokenIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    t.Chain.Equals(chain, StringComparison.OrdinalIgnoreCase));

                return Task.FromResult(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token by identifier {TokenIdentifier} on chain {Chain}", 
                    LoggingHelper.SanitizeLogInput(tokenIdentifier), 
                    LoggingHelper.SanitizeLogInput(chain));
                return Task.FromResult<TokenRegistryEntry?>(null);
            }
        }

        /// <inheritdoc/>
        public Task<UpsertTokenRegistryResponse> UpsertTokenAsync(UpsertTokenRegistryRequest request, string? createdBy = null)
        {
            try
            {
                // Find existing entry by token identifier and chain
                var existing = _entries.Values.FirstOrDefault(t =>
                    t.TokenIdentifier.Equals(request.TokenIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    t.Chain.Equals(request.Chain, StringComparison.OrdinalIgnoreCase));

                TokenRegistryEntry entry;
                bool created;

                if (existing != null)
                {
                    // Update existing entry
                    created = false;
                    entry = existing;
                    entry.Name = request.Name;
                    entry.Symbol = request.Symbol;
                    entry.Decimals = request.Decimals;
                    entry.TotalSupply = request.TotalSupply;
                    entry.UpdatedAt = DateTime.UtcNow;

                    // Update optional fields if provided
                    if (request.SupportedStandards != null)
                        entry.SupportedStandards = request.SupportedStandards;
                    if (request.PrimaryStandard != null)
                        entry.PrimaryStandard = request.PrimaryStandard;
                    if (request.Issuer != null)
                        entry.Issuer = request.Issuer;
                    if (request.Compliance != null)
                        entry.Compliance = request.Compliance;
                    if (request.Readiness != null)
                        entry.Readiness = request.Readiness;
                    if (request.Description != null)
                        entry.Description = request.Description;
                    if (request.Website != null)
                        entry.Website = request.Website;
                    if (request.LogoUrl != null)
                        entry.LogoUrl = request.LogoUrl;
                    if (request.ExternalRegistries != null)
                        entry.ExternalRegistries = request.ExternalRegistries;
                    if (request.Tags != null)
                        entry.Tags = request.Tags;
                    if (request.DeployedAt.HasValue)
                        entry.DeployedAt = request.DeployedAt;

                    entry.DataSource = request.DataSource;

                    _entries[entry.Id] = entry;
                }
                else
                {
                    // Create new entry
                    created = true;
                    entry = new TokenRegistryEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        TokenIdentifier = request.TokenIdentifier,
                        Chain = request.Chain,
                        Name = request.Name,
                        Symbol = request.Symbol,
                        Decimals = request.Decimals,
                        TotalSupply = request.TotalSupply,
                        SupportedStandards = request.SupportedStandards ?? new List<string>(),
                        PrimaryStandard = request.PrimaryStandard,
                        Issuer = request.Issuer,
                        Compliance = request.Compliance ?? new ComplianceScoring(),
                        Readiness = request.Readiness ?? new OperationalReadiness(),
                        Description = request.Description,
                        Website = request.Website,
                        LogoUrl = request.LogoUrl,
                        DataSource = request.DataSource,
                        ExternalRegistries = request.ExternalRegistries ?? new List<string>(),
                        Tags = request.Tags ?? new List<string>(),
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DeployedAt = request.DeployedAt
                    };

                    _entries[entry.Id] = entry;
                }

                _logger.LogInformation(
                    "{Action} token registry entry for {Symbol} ({TokenIdentifier}) on {Chain}",
                    created ? "Created" : "Updated",
                    LoggingHelper.SanitizeLogInput(entry.Symbol),
                    LoggingHelper.SanitizeLogInput(entry.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(entry.Chain)
                );

                return Task.FromResult(new UpsertTokenRegistryResponse
                {
                    Success = true,
                    RegistryId = entry.Id,
                    Created = created,
                    Token = entry
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting token {Symbol} ({TokenIdentifier}) on {Chain}", 
                    LoggingHelper.SanitizeLogInput(request.Symbol),
                    LoggingHelper.SanitizeLogInput(request.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(request.Chain));
                
                return Task.FromResult(new UpsertTokenRegistryResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to upsert token registry entry"
                });
            }
        }

        /// <inheritdoc/>
        public Task<bool> DeleteTokenAsync(string id)
        {
            try
            {
                var result = _entries.TryRemove(id, out _);
                if (result)
                {
                    _logger.LogInformation("Deleted token registry entry {Id}", LoggingHelper.SanitizeLogInput(id));
                }
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting token {Id}", LoggingHelper.SanitizeLogInput(id));
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<List<TokenRegistryEntry>> SearchTokensAsync(string searchTerm, int limit = 10)
        {
            try
            {
                var searchLower = searchTerm.ToLowerInvariant();
                var results = _entries.Values
                    .Where(t => t.Name.ToLowerInvariant().Contains(searchLower) ||
                               t.Symbol.ToLowerInvariant().Contains(searchLower) ||
                               t.TokenIdentifier.ToLowerInvariant().Contains(searchLower))
                    .OrderBy(t => t.Name)
                    .Take(limit)
                    .ToList();

                return Task.FromResult(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tokens with term {SearchTerm}", LoggingHelper.SanitizeLogInput(searchTerm));
                return Task.FromResult(new List<TokenRegistryEntry>());
            }
        }

        /// <inheritdoc/>
        public Task<int> GetTokenCountAsync(ListTokenRegistryRequest request)
        {
            try
            {
                var query = _entries.Values.AsEnumerable();

                // Apply same filters as ListTokensAsync
                if (!string.IsNullOrWhiteSpace(request.Standard))
                {
                    query = query.Where(t => t.SupportedStandards.Contains(request.Standard, StringComparer.OrdinalIgnoreCase) ||
                                            (t.PrimaryStandard != null && t.PrimaryStandard.Equals(request.Standard, StringComparison.OrdinalIgnoreCase)));
                }

                if (request.ComplianceStatus.HasValue)
                {
                    query = query.Where(t => t.Compliance.Status == request.ComplianceStatus.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.Chain))
                {
                    query = query.Where(t => t.Chain.Equals(request.Chain, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.IssuerAddress))
                {
                    query = query.Where(t => t.Issuer != null && 
                                            t.Issuer.Address != null &&
                                            t.Issuer.Address.Equals(request.IssuerAddress, StringComparison.OrdinalIgnoreCase));
                }

                if (request.IsContractVerified.HasValue)
                {
                    query = query.Where(t => t.Readiness.IsContractVerified == request.IsContractVerified.Value);
                }

                if (request.IsAudited.HasValue)
                {
                    query = query.Where(t => t.Readiness.IsAudited == request.IsAudited.Value);
                }

                if (request.HasValidMetadata.HasValue)
                {
                    query = query.Where(t => t.Readiness.HasValidMetadata == request.HasValidMetadata.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var searchLower = request.Search.ToLowerInvariant();
                    query = query.Where(t => 
                        t.Name.ToLowerInvariant().Contains(searchLower) ||
                        t.Symbol.ToLowerInvariant().Contains(searchLower) ||
                        (t.Description != null && t.Description.ToLowerInvariant().Contains(searchLower)));
                }

                if (!string.IsNullOrWhiteSpace(request.Tags))
                {
                    var tags = request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    query = query.Where(t => tags.Any(tag => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
                }

                if (!string.IsNullOrWhiteSpace(request.DataSource))
                {
                    query = query.Where(t => t.DataSource.Equals(request.DataSource, StringComparison.OrdinalIgnoreCase));
                }

                return Task.FromResult(query.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token count");
                return Task.FromResult(0);
            }
        }

        private IEnumerable<TokenRegistryEntry> ApplySorting(IEnumerable<TokenRegistryEntry> query, string sortBy, string sortDirection)
        {
            var ascending = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLowerInvariant() switch
            {
                "name" => ascending ? query.OrderBy(t => t.Name) : query.OrderByDescending(t => t.Name),
                "symbol" => ascending ? query.OrderBy(t => t.Symbol) : query.OrderByDescending(t => t.Symbol),
                "createdat" => ascending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt),
                "updatedat" => ascending ? query.OrderBy(t => t.UpdatedAt) : query.OrderByDescending(t => t.UpdatedAt),
                "compliancescore" => ascending ? query.OrderBy(t => t.Compliance.Score ?? 0) : query.OrderByDescending(t => t.Compliance.Score ?? 0),
                "deployedat" => ascending ? query.OrderBy(t => t.DeployedAt ?? DateTime.MinValue) : query.OrderByDescending(t => t.DeployedAt ?? DateTime.MinValue),
                _ => query.OrderByDescending(t => t.CreatedAt) // Default: newest first
            };
        }
    }
}

using Algorand;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing whitelist operations
    /// </summary>
    public class WhitelistService : IWhitelistService
    {
        private readonly IWhitelistRepository _repository;
        private readonly ILogger<WhitelistService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistService"/> class.
        /// </summary>
        /// <param name="repository">The whitelist repository</param>
        /// <param name="logger">The logger instance</param>
        public WhitelistService(IWhitelistRepository repository, ILogger<WhitelistService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Adds a single address to the whitelist
        /// </summary>
        /// <param name="request">The add whitelist entry request</param>
        /// <param name="createdBy">The address of the user creating the entry</param>
        /// <returns>The whitelist response</returns>
        public async Task<WhitelistResponse> AddEntryAsync(AddWhitelistEntryRequest request, string createdBy)
        {
            try
            {
                // Validate address format
                if (!IsValidAlgorandAddress(request.Address))
                {
                    _logger.LogWarning("Invalid Algorand address format: {Address}", request.Address);
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid Algorand address format: {request.Address}"
                    };
                }

                // Check if entry already exists
                var existingEntry = await _repository.GetEntryAsync(request.AssetId, request.Address);
                if (existingEntry != null)
                {
                    _logger.LogInformation("Whitelist entry already exists for address {Address} on asset {AssetId}, updating status", 
                        request.Address, request.AssetId);
                    
                    // Update existing entry
                    existingEntry.Status = request.Status;
                    existingEntry.UpdatedAt = DateTime.UtcNow;
                    existingEntry.UpdatedBy = createdBy;
                    
                    var updated = await _repository.UpdateEntryAsync(existingEntry);
                    
                    if (!updated)
                    {
                        return new WhitelistResponse
                        {
                            Success = false,
                            ErrorMessage = "Failed to update existing whitelist entry"
                        };
                    }
                    
                    return new WhitelistResponse
                    {
                        Success = true,
                        Entry = existingEntry
                    };
                }

                // Create new entry
                var entry = new WhitelistEntry
                {
                    AssetId = request.AssetId,
                    Address = request.Address.ToUpperInvariant(),
                    Status = request.Status,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                var added = await _repository.AddEntryAsync(entry);

                if (!added)
                {
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to add whitelist entry"
                    };
                }

                _logger.LogInformation("Successfully added whitelist entry for address {Address} on asset {AssetId} by {CreatedBy}", 
                    entry.Address, entry.AssetId, createdBy);

                return new WhitelistResponse
                {
                    Success = true,
                    Entry = entry
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding whitelist entry for address {Address} on asset {AssetId}", 
                    request.Address, request.AssetId);
                return new WhitelistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Removes an address from the whitelist
        /// </summary>
        /// <param name="request">The remove whitelist entry request</param>
        /// <returns>The whitelist response</returns>
        public async Task<WhitelistResponse> RemoveEntryAsync(RemoveWhitelistEntryRequest request)
        {
            try
            {
                // Validate address format
                if (!IsValidAlgorandAddress(request.Address))
                {
                    _logger.LogWarning("Invalid Algorand address format: {Address}", request.Address);
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid Algorand address format: {request.Address}"
                    };
                }

                var removed = await _repository.RemoveEntryAsync(request.AssetId, request.Address.ToUpperInvariant());

                if (!removed)
                {
                    _logger.LogWarning("Whitelist entry not found for address {Address} on asset {AssetId}", 
                        request.Address, request.AssetId);
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "Whitelist entry not found"
                    };
                }

                _logger.LogInformation("Successfully removed whitelist entry for address {Address} on asset {AssetId}", 
                    request.Address, request.AssetId);

                return new WhitelistResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing whitelist entry for address {Address} on asset {AssetId}", 
                    request.Address, request.AssetId);
                return new WhitelistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Bulk adds addresses to the whitelist
        /// </summary>
        /// <param name="request">The bulk add whitelist request</param>
        /// <param name="createdBy">The address of the user creating the entries</param>
        /// <returns>The bulk whitelist response</returns>
        public async Task<BulkWhitelistResponse> BulkAddEntriesAsync(BulkAddWhitelistRequest request, string createdBy)
        {
            var response = new BulkWhitelistResponse
            {
                Success = true
            };

            try
            {
                // Deduplicate addresses (case-insensitive)
                var uniqueAddresses = request.Addresses
                    .Select(a => a.ToUpperInvariant())
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Bulk adding {Count} unique addresses to whitelist for asset {AssetId}", 
                    uniqueAddresses.Count, request.AssetId);

                foreach (var address in uniqueAddresses)
                {
                    // Validate address format
                    if (!IsValidAlgorandAddress(address))
                    {
                        response.FailedAddresses.Add(address);
                        response.ValidationErrors.Add($"Invalid address format: {address}");
                        response.FailedCount++;
                        continue;
                    }

                    // Check if entry already exists
                    var existingEntry = await _repository.GetEntryAsync(request.AssetId, address);
                    if (existingEntry != null)
                    {
                        // Update existing entry
                        existingEntry.Status = request.Status;
                        existingEntry.UpdatedAt = DateTime.UtcNow;
                        existingEntry.UpdatedBy = createdBy;
                        
                        var updated = await _repository.UpdateEntryAsync(existingEntry);
                        if (updated)
                        {
                            response.SuccessfulEntries.Add(existingEntry);
                            response.SuccessCount++;
                        }
                        else
                        {
                            response.FailedAddresses.Add(address);
                            response.ValidationErrors.Add($"Failed to update existing entry: {address}");
                            response.FailedCount++;
                        }
                        continue;
                    }

                    // Create new entry
                    var entry = new WhitelistEntry
                    {
                        AssetId = request.AssetId,
                        Address = address,
                        Status = request.Status,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.UtcNow
                    };

                    var added = await _repository.AddEntryAsync(entry);

                    if (added)
                    {
                        response.SuccessfulEntries.Add(entry);
                        response.SuccessCount++;
                    }
                    else
                    {
                        response.FailedAddresses.Add(address);
                        response.ValidationErrors.Add($"Failed to add entry: {address}");
                        response.FailedCount++;
                    }
                }

                _logger.LogInformation("Bulk add completed: {SuccessCount} succeeded, {FailedCount} failed for asset {AssetId}", 
                    response.SuccessCount, response.FailedCount, request.AssetId);

                if (response.FailedCount > 0)
                {
                    response.Success = false;
                    response.ErrorMessage = $"Partially successful: {response.SuccessCount} added, {response.FailedCount} failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk add for asset {AssetId}", request.AssetId);
                response.Success = false;
                response.ErrorMessage = $"Internal error: {ex.Message}";
            }

            return response;
        }

        /// <summary>
        /// Lists whitelist entries for a token
        /// </summary>
        /// <param name="request">The list whitelist request</param>
        /// <returns>The whitelist list response</returns>
        public async Task<WhitelistListResponse> ListEntriesAsync(ListWhitelistRequest request)
        {
            try
            {
                var allEntries = await _repository.GetEntriesByAssetIdAsync(request.AssetId, request.Status);
                
                var totalCount = allEntries.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
                
                // Apply pagination
                var pagedEntries = allEntries
                    .OrderBy(e => e.CreatedAt)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                _logger.LogInformation("Listed {Count} whitelist entries for asset {AssetId} (page {Page} of {TotalPages})", 
                    pagedEntries.Count, request.AssetId, request.Page, totalPages);

                return new WhitelistListResponse
                {
                    Success = true,
                    Entries = pagedEntries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing whitelist entries for asset {AssetId}", request.AssetId);
                return new WhitelistListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validates an Algorand address format
        /// </summary>
        /// <param name="address">The address to validate</param>
        /// <returns>True if the address is valid</returns>
        public bool IsValidAlgorandAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            try
            {
                // Algorand addresses are 58 characters long
                if (address.Length != 58)
                {
                    return false;
                }

                // Try to parse the address - this will throw if invalid
                var _ = new Address(address);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

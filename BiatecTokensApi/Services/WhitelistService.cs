using Algorand;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Models.Webhook;
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
        private readonly ISubscriptionMeteringService _meteringService;
        private readonly ISubscriptionTierService _tierService;
        private readonly IWebhookService _webhookService;
        private const string NetworkNotAvailable = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistService"/> class.
        /// </summary>
        /// <param name="repository">The whitelist repository</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="meteringService">The subscription metering service</param>
        /// <param name="tierService">The subscription tier service</param>
        /// <param name="webhookService">The webhook service</param>
        public WhitelistService(
            IWhitelistRepository repository, 
            ILogger<WhitelistService> logger,
            ISubscriptionMeteringService meteringService,
            ISubscriptionTierService tierService,
            IWebhookService webhookService)
        {
            _repository = repository;
            _logger = logger;
            _meteringService = meteringService;
            _tierService = tierService;
            _webhookService = webhookService;
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
                    
                    var oldStatus = existingEntry.Status;
                    
                    // Update existing entry
                    existingEntry.Status = request.Status;
                    existingEntry.UpdatedAt = DateTime.UtcNow;
                    existingEntry.UpdatedBy = createdBy;
                    existingEntry.Reason = request.Reason ?? existingEntry.Reason;
                    existingEntry.ExpirationDate = request.ExpirationDate ?? existingEntry.ExpirationDate;
                    existingEntry.KycVerified = request.KycVerified;
                    existingEntry.KycVerificationDate = request.KycVerificationDate ?? existingEntry.KycVerificationDate;
                    existingEntry.KycProvider = request.KycProvider ?? existingEntry.KycProvider;
                    existingEntry.Network = request.Network ?? existingEntry.Network;
                    existingEntry.Role = request.Role;
                    
                    // Validate network-specific rules for update
                    var updateValidationError = ValidateNetworkRules(existingEntry);
                    if (updateValidationError != null)
                    {
                        _logger.LogWarning("Network validation failed for address {Address} on asset {AssetId}: {Error}",
                            request.Address, request.AssetId, updateValidationError);
                        return new WhitelistResponse
                        {
                            Success = false,
                            ErrorMessage = updateValidationError
                        };
                    }
                    
                    var updated = await _repository.UpdateEntryAsync(existingEntry);
                    
                    if (!updated)
                    {
                        return new WhitelistResponse
                        {
                            Success = false,
                            ErrorMessage = "Failed to update existing whitelist entry"
                        };
                    }
                    
                    // Record audit log for update
                    await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        Address = existingEntry.Address,
                        ActionType = WhitelistActionType.Update,
                        PerformedBy = createdBy,
                        PerformedAt = DateTime.UtcNow,
                        OldStatus = oldStatus,
                        NewStatus = request.Status,
                        Network = existingEntry.Network,
                        Role = existingEntry.Role
                    });
                    
                    // Emit metering event for billing analytics
                    _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                    {
                        Category = MeteringCategory.Whitelist,
                        OperationType = MeteringOperationType.Update,
                        AssetId = request.AssetId,
                        Network = existingEntry.Network ?? NetworkNotAvailable,
                        PerformedBy = createdBy,
                        ItemCount = 1
                    });
                    
                    return new WhitelistResponse
                    {
                        Success = true,
                        Entry = existingEntry
                    };
                }

                // Validate subscription tier before adding new entry
                // Note: GetEntriesCountAsync is called for each addition. For high-frequency operations,
                // consider implementing caching or batch validation to improve performance.
                var currentCount = await _repository.GetEntriesCountAsync(request.AssetId);
                var tierValidation = await _tierService.ValidateOperationAsync(createdBy, request.AssetId, currentCount, 1);
                
                if (!tierValidation.IsAllowed)
                {
                    _logger.LogWarning(
                        "Subscription tier limit exceeded for user {CreatedBy} on asset {AssetId}: {Reason}",
                        createdBy, request.AssetId, tierValidation.DenialReason);
                    
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = tierValidation.DenialReason ?? "Subscription tier limit exceeded"
                    };
                }

                // Create new entry
                var entry = new WhitelistEntry
                {
                    AssetId = request.AssetId,
                    Address = request.Address.ToUpperInvariant(),
                    Status = request.Status,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    Reason = request.Reason,
                    ExpirationDate = request.ExpirationDate,
                    KycVerified = request.KycVerified,
                    KycVerificationDate = request.KycVerificationDate,
                    KycProvider = request.KycProvider,
                    Network = request.Network,
                    Role = request.Role
                };

                // Validate network-specific rules
                var networkValidationError = ValidateNetworkRules(entry);
                if (networkValidationError != null)
                {
                    _logger.LogWarning("Network validation failed for address {Address} on asset {AssetId}: {Error}",
                        request.Address, request.AssetId, networkValidationError);
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = networkValidationError
                    };
                }

                var added = await _repository.AddEntryAsync(entry);

                if (!added)
                {
                    return new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to add whitelist entry"
                    };
                }

                // Record audit log for add
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = entry.AssetId,
                    Address = entry.Address,
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = createdBy,
                    PerformedAt = DateTime.UtcNow,
                    OldStatus = null,
                    NewStatus = entry.Status,
                    Network = entry.Network,
                    Role = entry.Role
                });

                // Emit metering event for billing analytics
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Whitelist,
                    OperationType = MeteringOperationType.Add,
                    AssetId = entry.AssetId,
                    Network = entry.Network ?? NetworkNotAvailable,
                    PerformedBy = createdBy,
                    ItemCount = 1
                });

                // Emit webhook event for whitelist add
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webhookService.EmitEventAsync(new WebhookEvent
                        {
                            EventType = WebhookEventType.WhitelistAdd,
                            AssetId = entry.AssetId,
                            Network = entry.Network,
                            Actor = createdBy,
                            AffectedAddress = entry.Address,
                            Timestamp = DateTime.UtcNow,
                            Data = new Dictionary<string, object>
                            {
                                { "status", entry.Status.ToString() },
                                { "role", entry.Role.ToString() },
                                { "kycVerified", entry.KycVerified }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to emit webhook event for whitelist add");
                    }
                });

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

                // Get the entry before removing to capture its status for audit log
                var existingEntry = await _repository.GetEntryAsync(request.AssetId, request.Address.ToUpperInvariant());
                
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

                // Record audit log for remove (use system if no user context available)
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = request.AssetId,
                    Address = request.Address.ToUpperInvariant(),
                    ActionType = WhitelistActionType.Remove,
                    PerformedBy = existingEntry?.UpdatedBy ?? existingEntry?.CreatedBy ?? "SYSTEM",
                    PerformedAt = DateTime.UtcNow,
                    OldStatus = existingEntry?.Status,
                    NewStatus = null
                });

                // Emit metering event for billing analytics
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Whitelist,
                    OperationType = MeteringOperationType.Remove,
                    AssetId = request.AssetId,
                    Network = NetworkNotAvailable,
                    PerformedBy = existingEntry?.UpdatedBy ?? existingEntry?.CreatedBy,
                    ItemCount = 1
                });

                // Emit webhook event for whitelist remove
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webhookService.EmitEventAsync(new WebhookEvent
                        {
                            EventType = WebhookEventType.WhitelistRemove,
                            AssetId = request.AssetId,
                            Network = existingEntry?.Network,
                            Actor = existingEntry?.UpdatedBy ?? existingEntry?.CreatedBy ?? "SYSTEM",
                            AffectedAddress = request.Address.ToUpperInvariant(),
                            Timestamp = DateTime.UtcNow,
                            Data = new Dictionary<string, object>
                            {
                                { "previousStatus", existingEntry?.Status.ToString() ?? "Unknown" }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to emit webhook event for whitelist remove");
                    }
                });

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
                // Check if bulk operations are enabled for this tier
                var isBulkEnabled = await _tierService.IsBulkOperationEnabledAsync(createdBy);
                if (!isBulkEnabled)
                {
                    _logger.LogWarning(
                        "Bulk operation denied for user {CreatedBy}: Not enabled for their subscription tier",
                        createdBy);
                    
                    return new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "Bulk operations are not available in your subscription tier. Please upgrade to Premium or Enterprise tier."
                    };
                }

                // Deduplicate addresses (case-insensitive)
                var uniqueAddresses = request.Addresses
                    .Select(a => a.ToUpperInvariant())
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Bulk adding {Count} unique addresses to whitelist for asset {AssetId}", 
                    uniqueAddresses.Count, request.AssetId);

                // Get current count to validate against tier limits
                var currentCount = await _repository.GetEntriesCountAsync(request.AssetId);
                
                // Count how many are new entries (not updates)
                // Note: This creates an N+1 query pattern. For production optimization,
                // consider adding a bulk exists check method to the repository.
                var newEntriesCount = 0;
                foreach (var address in uniqueAddresses)
                {
                    var exists = await _repository.GetEntryAsync(request.AssetId, address);
                    if (exists == null)
                    {
                        newEntriesCount++;
                    }
                }

                // Validate tier limits for new entries
                if (newEntriesCount > 0)
                {
                    var tierValidation = await _tierService.ValidateOperationAsync(
                        createdBy, request.AssetId, currentCount, newEntriesCount);
                    
                    if (!tierValidation.IsAllowed)
                    {
                        _logger.LogWarning(
                            "Bulk operation tier limit exceeded for user {CreatedBy} on asset {AssetId}: {Reason}",
                            createdBy, request.AssetId, tierValidation.DenialReason);
                        
                        return new BulkWhitelistResponse
                        {
                            Success = false,
                            ErrorMessage = tierValidation.DenialReason ?? "Subscription tier limit exceeded"
                        };
                    }
                }

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
                        var oldStatus = existingEntry.Status;
                        
                        // Update existing entry
                        existingEntry.Status = request.Status;
                        existingEntry.UpdatedAt = DateTime.UtcNow;
                        existingEntry.UpdatedBy = createdBy;
                        existingEntry.Reason = request.Reason ?? existingEntry.Reason;
                        existingEntry.ExpirationDate = request.ExpirationDate ?? existingEntry.ExpirationDate;
                        existingEntry.KycVerified = request.KycVerified;
                        existingEntry.KycVerificationDate = request.KycVerificationDate ?? existingEntry.KycVerificationDate;
                        existingEntry.KycProvider = request.KycProvider ?? existingEntry.KycProvider;
                        existingEntry.Network = request.Network ?? existingEntry.Network;
                        existingEntry.Role = request.Role;
                        
                        // Validate network-specific rules for update
                        var updateNetworkValidationError = ValidateNetworkRules(existingEntry);
                        if (updateNetworkValidationError != null)
                        {
                            response.FailedAddresses.Add(address);
                            response.ValidationErrors.Add($"Network validation failed: {updateNetworkValidationError}");
                            response.FailedCount++;
                            continue;
                        }
                        
                        var updated = await _repository.UpdateEntryAsync(existingEntry);
                        if (updated)
                        {
                            // Record audit log for update
                            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                            {
                                AssetId = request.AssetId,
                                Address = address,
                                ActionType = WhitelistActionType.Update,
                                PerformedBy = createdBy,
                                PerformedAt = DateTime.UtcNow,
                                OldStatus = oldStatus,
                                NewStatus = request.Status,
                                Network = existingEntry.Network,
                                Role = existingEntry.Role,
                                Notes = "Bulk operation"
                            });
                            
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
                        CreatedAt = DateTime.UtcNow,
                        Reason = request.Reason,
                        ExpirationDate = request.ExpirationDate,
                        KycVerified = request.KycVerified,
                        KycVerificationDate = request.KycVerificationDate,
                        KycProvider = request.KycProvider,
                        Network = request.Network,
                        Role = request.Role
                    };

                    // Validate network-specific rules
                    var addValidationError = ValidateNetworkRules(entry);
                    if (addValidationError != null)
                    {
                        response.FailedAddresses.Add(address);
                        response.ValidationErrors.Add($"Network validation failed: {addValidationError}");
                        response.FailedCount++;
                        continue;
                    }

                    var added = await _repository.AddEntryAsync(entry);

                    if (added)
                    {
                        // Record audit log for add
                        await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                        {
                            AssetId = entry.AssetId,
                            Address = entry.Address,
                            ActionType = WhitelistActionType.Add,
                            PerformedBy = createdBy,
                            PerformedAt = DateTime.UtcNow,
                            OldStatus = null,
                            NewStatus = entry.Status,
                            Network = entry.Network,
                            Role = entry.Role,
                            Notes = "Bulk operation"
                        });
                        
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

                // Emit metering event for billing analytics (only for successful operations)
                if (response.SuccessCount > 0)
                {
                    _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                    {
                        Category = MeteringCategory.Whitelist,
                        OperationType = MeteringOperationType.BulkAdd,
                        AssetId = request.AssetId,
                        Network = request.Network ?? NetworkNotAvailable,
                        PerformedBy = createdBy,
                        ItemCount = response.SuccessCount
                    });
                }

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
        /// Validates network-specific rules for whitelist entries
        /// </summary>
        /// <param name="entry">The whitelist entry to validate</param>
        /// <returns>Error message if validation fails, null if validation passes</returns>
        private string? ValidateNetworkRules(WhitelistEntry entry)
        {
            // If no network specified, validation passes
            if (string.IsNullOrEmpty(entry.Network))
            {
                return null;
            }

            // Normalize network name to lowercase for comparison
            var network = entry.Network.ToLowerInvariant();

            // VOI Network Rules (voimain-v1.0)
            if (network.StartsWith("voimain", StringComparison.Ordinal))
            {
                // Rule 1: For VOI network, KYC verification is strongly recommended for Active status
                if (entry.Status == WhitelistStatus.Active && !entry.KycVerified)
                {
                    _logger.LogWarning(
                        "VOI network whitelist entry for address {Address} on asset {AssetId} is Active but KYC not verified",
                        entry.Address, entry.AssetId);
                    // This is a warning, not a hard error - allow it to proceed
                }

                // Rule 2: Operators cannot revoke entries on VOI network (admin-only operation)
                if (entry.Role == WhitelistRole.Operator && entry.Status == WhitelistStatus.Revoked)
                {
                    return $"Operator role cannot revoke whitelist entries on VOI network. Admin privileges required.";
                }
            }

            // Aramid Network Rules (aramidmain-v1.0)
            if (network.StartsWith("aramidmain", StringComparison.Ordinal))
            {
                // Rule 1: For Aramid network, KYC verification is mandatory for Active status
                if (entry.Status == WhitelistStatus.Active && !entry.KycVerified)
                {
                    return $"Aramid network requires KYC verification for Active whitelist entries. Address: {entry.Address}";
                }

                // Rule 2: KYC provider must be specified when KYC is verified on Aramid
                if (entry.KycVerified && string.IsNullOrEmpty(entry.KycProvider))
                {
                    return $"Aramid network requires KYC provider to be specified when KYC is verified. Address: {entry.Address}";
                }

                // Rule 3: Operators have limited permissions on Aramid network
                if (entry.Role == WhitelistRole.Operator)
                {
                    // Operators can only set status to Active or Inactive, not Revoked
                    if (entry.Status == WhitelistStatus.Revoked)
                    {
                        return $"Operator role cannot revoke whitelist entries on Aramid network. Admin privileges required.";
                    }
                }
            }

            // Validation passed
            return null;
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

        /// <summary>
        /// Gets audit log entries for a token's whitelist
        /// </summary>
        /// <param name="request">The audit log request with filters and pagination</param>
        /// <returns>The audit log response with entries and pagination info</returns>
        /// <remarks>
        /// Note: For the in-memory implementation, the repository returns all filtered entries
        /// and pagination is applied at the service level. In a production database implementation,
        /// pagination should be moved to the repository layer for better performance.
        /// </remarks>
        public async Task<WhitelistAuditLogResponse> GetAuditLogAsync(GetWhitelistAuditLogRequest request)
        {
            try
            {
                // Validate pagination parameters
                if (request.Page < 1)
                {
                    request.Page = 1;
                }
                if (request.PageSize < 1)
                {
                    request.PageSize = 50;
                }
                if (request.PageSize > 100)
                {
                    request.PageSize = 100;
                }

                var allEntries = await _repository.GetAuditLogAsync(request);
                
                var totalCount = allEntries.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
                
                // Apply pagination
                var pagedEntries = allEntries
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var assetIdLog = request.AssetId.HasValue ? $"asset {request.AssetId.Value}" : "all assets";
                _logger.LogInformation("Retrieved {Count} audit log entries for {AssetLog} (page {Page} of {TotalPages})", 
                    pagedEntries.Count, assetIdLog, request.Page, totalPages);

                return new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = pagedEntries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    RetentionPolicy = new BiatecTokensApi.Models.Compliance.AuditRetentionPolicy
                    {
                        MinimumRetentionYears = 7,
                        RegulatoryFramework = "MICA",
                        ImmutableEntries = true,
                        Description = "Audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements. All entries are immutable and cannot be modified or deleted."
                    }
                };
            }
            catch (Exception ex)
            {
                var assetIdLog = request.AssetId.HasValue ? $"asset {request.AssetId.Value}" : "all assets";
                _logger.LogError(ex, "Error retrieving audit log for {AssetLog}", assetIdLog);
                return new WhitelistAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validates if a transfer between two addresses is allowed based on whitelist rules
        /// </summary>
        /// <param name="request">The transfer validation request</param>
        /// <param name="performedBy">The address of the user performing the validation (for audit logging)</param>
        /// <returns>The validation response indicating if the transfer is allowed</returns>
        public async Task<ValidateTransferResponse> ValidateTransferAsync(ValidateTransferRequest request, string performedBy)
        {
            try
            {
                // Validate address formats
                if (!IsValidAlgorandAddress(request.FromAddress))
                {
                    _logger.LogWarning("Invalid sender address format: {Address}", request.FromAddress);
                    
                    // Record audit log for invalid address format
                    await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        Address = request.FromAddress,
                        ActionType = WhitelistActionType.TransferValidation,
                        PerformedBy = performedBy,
                        PerformedAt = DateTime.UtcNow,
                        ToAddress = request.ToAddress,
                        TransferAllowed = false,
                        DenialReason = "Invalid sender address format",
                        Amount = request.Amount
                    });
                    
                    return new ValidateTransferResponse
                    {
                        Success = false,
                        IsAllowed = false,
                        ErrorMessage = $"Invalid sender address format: {request.FromAddress}",
                        DenialReason = "Invalid sender address format"
                    };
                }

                if (!IsValidAlgorandAddress(request.ToAddress))
                {
                    _logger.LogWarning("Invalid receiver address format: {Address}", request.ToAddress);
                    
                    // Record audit log for invalid address format
                    await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        Address = request.FromAddress,
                        ActionType = WhitelistActionType.TransferValidation,
                        PerformedBy = performedBy,
                        PerformedAt = DateTime.UtcNow,
                        ToAddress = request.ToAddress,
                        TransferAllowed = false,
                        DenialReason = "Invalid receiver address format",
                        Amount = request.Amount
                    });
                    
                    return new ValidateTransferResponse
                    {
                        Success = false,
                        IsAllowed = false,
                        ErrorMessage = $"Invalid receiver address format: {request.ToAddress}",
                        DenialReason = "Invalid receiver address format"
                    };
                }

                // Get whitelist entries for both addresses
                var senderEntry = await _repository.GetEntryAsync(request.AssetId, request.FromAddress);
                var receiverEntry = await _repository.GetEntryAsync(request.AssetId, request.ToAddress);

                var now = DateTime.UtcNow;

                // Build sender status
                // Note: IsExpired uses null-conditional operator - null ExpirationDate means "never expires"
                var senderStatus = new TransferParticipantStatus
                {
                    Address = request.FromAddress,
                    IsWhitelisted = senderEntry != null,
                    IsActive = senderEntry?.Status == WhitelistStatus.Active,
                    IsExpired = senderEntry?.ExpirationDate < now,
                    ExpirationDate = senderEntry?.ExpirationDate,
                    Status = senderEntry?.Status
                };

                // Build receiver status
                // Note: IsExpired uses null-conditional operator - null ExpirationDate means "never expires"
                var receiverStatus = new TransferParticipantStatus
                {
                    Address = request.ToAddress,
                    IsWhitelisted = receiverEntry != null,
                    IsActive = receiverEntry?.Status == WhitelistStatus.Active,
                    IsExpired = receiverEntry?.ExpirationDate < now,
                    ExpirationDate = receiverEntry?.ExpirationDate,
                    Status = receiverEntry?.Status
                };

                // Determine if transfer is allowed
                var isAllowed = true;
                var denialReasons = new List<string>();

                // Check sender
                // Note: Addresses are included in denial reasons for compliance audit purposes
                if (!senderStatus.IsWhitelisted)
                {
                    isAllowed = false;
                    denialReasons.Add($"Sender address {request.FromAddress} is not whitelisted for asset {request.AssetId}");
                }
                else if (!senderStatus.IsActive)
                {
                    isAllowed = false;
                    denialReasons.Add($"Sender address {request.FromAddress} whitelist status is {senderStatus.Status} (not Active)");
                }
                else if (senderStatus.IsExpired)
                {
                    isAllowed = false;
                    // IsExpired is only true when ExpirationDate has value and is in the past
                    denialReasons.Add($"Sender address {request.FromAddress} whitelist entry expired on {senderStatus.ExpirationDate!.Value:yyyy-MM-dd}");
                }

                // Check receiver
                if (!receiverStatus.IsWhitelisted)
                {
                    isAllowed = false;
                    denialReasons.Add($"Receiver address {request.ToAddress} is not whitelisted for asset {request.AssetId}");
                }
                else if (!receiverStatus.IsActive)
                {
                    isAllowed = false;
                    denialReasons.Add($"Receiver address {request.ToAddress} whitelist status is {receiverStatus.Status} (not Active)");
                }
                else if (receiverStatus.IsExpired)
                {
                    isAllowed = false;
                    // IsExpired is only true when ExpirationDate has value and is in the past
                    denialReasons.Add($"Receiver address {request.ToAddress} whitelist entry expired on {receiverStatus.ExpirationDate!.Value:yyyy-MM-dd}");
                }

                var denialReason = denialReasons.Any() ? string.Join("; ", denialReasons) : null;

                // Record audit log entry for this transfer validation attempt
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = request.AssetId,
                    Address = request.FromAddress,
                    ActionType = WhitelistActionType.TransferValidation,
                    PerformedBy = performedBy,
                    PerformedAt = DateTime.UtcNow,
                    ToAddress = request.ToAddress,
                    TransferAllowed = isAllowed,
                    DenialReason = denialReason,
                    Amount = request.Amount,
                    Notes = isAllowed ? "Transfer validation passed" : "Transfer validation failed"
                });

                _logger.LogInformation(
                    "Transfer validation for asset {AssetId} from {From} to {To}: {Result}",
                    request.AssetId, request.FromAddress, request.ToAddress,
                    isAllowed ? "ALLOWED" : $"DENIED - {denialReason}");

                // Emit metering event for billing analytics
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Whitelist,
                    OperationType = MeteringOperationType.TransferValidation,
                    AssetId = request.AssetId,
                    Network = NetworkNotAvailable,
                    PerformedBy = performedBy,
                    ItemCount = 1
                });

                // Emit webhook event for transfer deny (only if transfer was not allowed)
                if (!isAllowed)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _webhookService.EmitEventAsync(new WebhookEvent
                            {
                                EventType = WebhookEventType.TransferDeny,
                                AssetId = request.AssetId,
                                Network = null, // Network not available in ValidateTransferRequest
                                Actor = performedBy,
                                AffectedAddress = request.FromAddress,
                                Timestamp = DateTime.UtcNow,
                                Data = new Dictionary<string, object>
                                {
                                    { "fromAddress", request.FromAddress },
                                    { "toAddress", request.ToAddress },
                                    { "amount", request.Amount?.ToString() ?? "0" },
                                    { "denialReason", denialReason ?? "Unknown" },
                                    { "senderWhitelisted", senderEntry != null },
                                    { "receiverWhitelisted", receiverEntry != null }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to emit webhook event for transfer deny");
                        }
                    });
                }

                return new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = isAllowed,
                    DenialReason = denialReason,
                    SenderStatus = senderStatus,
                    ReceiverStatus = receiverStatus
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating transfer for asset {AssetId} from {From} to {To}",
                    request.AssetId, request.FromAddress, request.ToAddress);
                
                // Record audit log for error
                try
                {
                    await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        Address = request.FromAddress,
                        ActionType = WhitelistActionType.TransferValidation,
                        PerformedBy = performedBy,
                        PerformedAt = DateTime.UtcNow,
                        ToAddress = request.ToAddress,
                        TransferAllowed = false,
                        DenialReason = "Internal validation error",
                        Amount = request.Amount,
                        Notes = $"Error: {ex.Message}"
                    });
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Failed to record audit log entry for validation error");
                }
                
                return new ValidateTransferResponse
                {
                    Success = false,
                    IsAllowed = false,
                    ErrorMessage = "An error occurred while validating the transfer. Please try again or contact support.",
                    DenialReason = "Internal validation error"
                };
            }
        }

        /// <summary>
        /// Gets whitelist enforcement audit report (transfer validation events only) with summary statistics
        /// </summary>
        public async Task<WhitelistEnforcementReportResponse> GetEnforcementReportAsync(GetWhitelistEnforcementReportRequest request)
        {
            _logger.LogInformation("Retrieving whitelist enforcement report: AssetId={AssetId}, Network={Network}, Page={Page}",
                request.AssetId, request.Network, request.Page);

            try
            {
                // Validate and cap page size
                if (request.PageSize < 1)
                    request.PageSize = 50;
                if (request.PageSize > 100)
                    request.PageSize = 100;

                if (request.Page < 1)
                    request.Page = 1;

                // Get all entries with TransferValidation filter
                var allEntriesRequest = new GetWhitelistAuditLogRequest
                {
                    AssetId = request.AssetId,
                    ActionType = WhitelistActionType.TransferValidation,
                    PerformedBy = request.PerformedBy,
                    Network = request.Network,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Page = 1,
                    PageSize = int.MaxValue
                };

                // Get all entries to apply additional filters
                var allEntries = await _repository.GetAuditLogAsync(allEntriesRequest);

                // Apply enforcement-specific filters
                if (!string.IsNullOrEmpty(request.FromAddress))
                {
                    allEntries = allEntries.Where(e => !string.IsNullOrEmpty(e.Address) &&
                        e.Address.Equals(request.FromAddress, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(request.ToAddress))
                {
                    allEntries = allEntries.Where(e => !string.IsNullOrEmpty(e.ToAddress) &&
                        e.ToAddress.Equals(request.ToAddress, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (request.TransferAllowed.HasValue)
                {
                    allEntries = allEntries.Where(e => e.TransferAllowed == request.TransferAllowed.Value).ToList();
                }

                var totalCount = allEntries.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Apply pagination to filtered results
                var entries = allEntries
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                // Calculate summary statistics on all entries (not just current page)
                var summary = CalculateEnforcementSummary(allEntries);

                var response = new WhitelistEnforcementReportResponse
                {
                    Success = true,
                    Entries = entries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    Summary = summary,
                    RetentionPolicy = new Models.Compliance.AuditRetentionPolicy
                    {
                        MinimumRetentionYears = 7,
                        RegulatoryFramework = "MICA",
                        ImmutableEntries = true,
                        Description = "Audit logs are retained for a minimum of 7 years to comply with MICA (Markets in Crypto-Assets Regulation) and other regulatory requirements. All entries are immutable and cannot be modified or deleted."
                    }
                };

                _logger.LogInformation("Retrieved {Count} enforcement entries (page {Page} of {TotalPages}), {Allowed} allowed, {Denied} denied",
                    entries.Count, request.Page, totalPages, summary.AllowedTransfers, summary.DeniedTransfers);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enforcement report");
                return new WhitelistEnforcementReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Error retrieving enforcement report: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculates summary statistics for enforcement events
        /// </summary>
        private EnforcementSummaryStatistics CalculateEnforcementSummary(List<WhitelistAuditLogEntry> entries)
        {
            var summary = new EnforcementSummaryStatistics
            {
                TotalValidations = entries.Count,
                AllowedTransfers = entries.Count(e => e.TransferAllowed == true),
                DeniedTransfers = entries.Count(e => e.TransferAllowed == false)
            };

            if (summary.TotalValidations > 0)
            {
                summary.AllowedPercentage = Math.Round((double)summary.AllowedTransfers / summary.TotalValidations * 100, 2);
                summary.DeniedPercentage = Math.Round((double)summary.DeniedTransfers / summary.TotalValidations * 100, 2);
            }

            summary.UniqueAssets = entries.Select(e => e.AssetId).Distinct().ToList();
            summary.UniqueNetworks = entries.Where(e => !string.IsNullOrEmpty(e.Network))
                .Select(e => e.Network!).Distinct().ToList();

            if (entries.Any())
            {
                summary.DateRange = new EnforcementDateRange
                {
                    EarliestEvent = entries.Min(e => e.PerformedAt),
                    LatestEvent = entries.Max(e => e.PerformedAt)
                };
            }

            // Top denial reasons
            var denialReasons = entries.Where(e => !string.IsNullOrEmpty(e.DenialReason))
                .GroupBy(e => e.DenialReason!)
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var group in denialReasons)
            {
                summary.DenialReasons[group.Key] = group.Count();
            }

            return summary;
        }
    }
}

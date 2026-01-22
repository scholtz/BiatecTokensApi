using Algorand;
using BiatecTokensApi.Models.Metering;
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
        private readonly ISubscriptionMeteringService _meteringService;
        private const string NetworkNotAvailable = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistService"/> class.
        /// </summary>
        /// <param name="repository">The whitelist repository</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="meteringService">The subscription metering service</param>
        public WhitelistService(
            IWhitelistRepository repository, 
            ILogger<WhitelistService> logger,
            ISubscriptionMeteringService meteringService)
        {
            _repository = repository;
            _logger = logger;
            _meteringService = meteringService;
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
                        NewStatus = request.Status
                    });
                    
                    // Emit metering event for billing analytics
                    _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                    {
                        Category = MeteringCategory.Whitelist,
                        OperationType = MeteringOperationType.Update,
                        AssetId = request.AssetId,
                        Network = NetworkNotAvailable,
                        PerformedBy = createdBy,
                        ItemCount = 1
                    });
                    
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
                    CreatedAt = DateTime.UtcNow,
                    Reason = request.Reason,
                    ExpirationDate = request.ExpirationDate,
                    KycVerified = request.KycVerified,
                    KycVerificationDate = request.KycVerificationDate,
                    KycProvider = request.KycProvider
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

                // Record audit log for add
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = entry.AssetId,
                    Address = entry.Address,
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = createdBy,
                    PerformedAt = DateTime.UtcNow,
                    OldStatus = null,
                    NewStatus = entry.Status
                });

                // Emit metering event for billing analytics
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Whitelist,
                    OperationType = MeteringOperationType.Add,
                    AssetId = entry.AssetId,
                    Network = NetworkNotAvailable,
                    PerformedBy = createdBy,
                    ItemCount = 1
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
                        KycProvider = request.KycProvider
                    };

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
                        Network = NetworkNotAvailable,
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

                _logger.LogInformation("Retrieved {Count} audit log entries for asset {AssetId} (page {Page} of {TotalPages})", 
                    pagedEntries.Count, request.AssetId, request.Page, totalPages);

                return new WhitelistAuditLogResponse
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
                _logger.LogError(ex, "Error retrieving audit log for asset {AssetId}", request.AssetId);
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
        /// <returns>The validation response indicating if the transfer is allowed</returns>
        public async Task<ValidateTransferResponse> ValidateTransferAsync(ValidateTransferRequest request)
        {
            try
            {
                // Validate address formats
                if (!IsValidAlgorandAddress(request.FromAddress))
                {
                    _logger.LogWarning("Invalid sender address format: {Address}", request.FromAddress);
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
                var senderStatus = new TransferParticipantStatus
                {
                    Address = request.FromAddress,
                    IsWhitelisted = senderEntry != null,
                    IsActive = senderEntry?.Status == WhitelistStatus.Active,
                    IsExpired = senderEntry?.ExpirationDate.HasValue == true && senderEntry.ExpirationDate.Value < now,
                    ExpirationDate = senderEntry?.ExpirationDate,
                    Status = senderEntry?.Status
                };

                // Build receiver status
                var receiverStatus = new TransferParticipantStatus
                {
                    Address = request.ToAddress,
                    IsWhitelisted = receiverEntry != null,
                    IsActive = receiverEntry?.Status == WhitelistStatus.Active,
                    IsExpired = receiverEntry?.ExpirationDate.HasValue == true && receiverEntry.ExpirationDate.Value < now,
                    ExpirationDate = receiverEntry?.ExpirationDate,
                    Status = receiverEntry?.Status
                };

                // Determine if transfer is allowed
                var isAllowed = true;
                var denialReasons = new List<string>();

                // Check sender
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
                    denialReasons.Add($"Sender address {request.FromAddress} whitelist entry expired on {senderStatus.ExpirationDate:yyyy-MM-dd}");
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
                    denialReasons.Add($"Receiver address {request.ToAddress} whitelist entry expired on {receiverStatus.ExpirationDate:yyyy-MM-dd}");
                }

                var denialReason = denialReasons.Any() ? string.Join("; ", denialReasons) : null;

                _logger.LogInformation(
                    "Transfer validation for asset {AssetId} from {From} to {To}: {Result}",
                    request.AssetId, request.FromAddress, request.ToAddress,
                    isAllowed ? "ALLOWED" : $"DENIED - {denialReason}");

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
                return new ValidateTransferResponse
                {
                    Success = false,
                    IsAllowed = false,
                    ErrorMessage = $"Internal error: {ex.Message}",
                    DenialReason = "Internal validation error"
                };
            }
        }
    }
}

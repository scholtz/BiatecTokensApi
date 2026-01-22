using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing compliance metadata with network-specific validation
    /// </summary>
    public class ComplianceService : IComplianceService
    {
        private readonly IComplianceRepository _complianceRepository;
        private readonly ILogger<ComplianceService> _logger;
        private readonly ISubscriptionMeteringService _meteringService;
        
        /// <summary>
        /// Constant for system-generated audit entries when no user context is available
        /// </summary>
        private const string SystemActor = "System";

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceService"/> class.
        /// </summary>
        /// <param name="complianceRepository">The compliance repository</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="meteringService">The subscription metering service</param>
        public ComplianceService(
            IComplianceRepository complianceRepository,
            ILogger<ComplianceService> logger,
            ISubscriptionMeteringService meteringService)
        {
            _complianceRepository = complianceRepository;
            _logger = logger;
            _meteringService = meteringService;
        }

        /// <inheritdoc/>
        public async Task<ComplianceMetadataResponse> UpsertMetadataAsync(
            UpsertComplianceMetadataRequest request, 
            string createdBy)
        {
            try
            {
                // Validate network-specific rules
                var validationError = ValidateNetworkRules(request.Network, request);
                if (validationError != null)
                {
                    // Log failed validation attempt
                    await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        Network = request.Network,
                        ActionType = ComplianceActionType.Create,
                        PerformedBy = createdBy,
                        Success = false,
                        ErrorMessage = validationError
                    });

                    return new ComplianceMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = validationError
                    };
                }

                // Check if metadata already exists
                var existingMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(request.AssetId);
                var isUpdate = existingMetadata != null;

                var metadata = new ComplianceMetadata
                {
                    Id = existingMetadata?.Id ?? Guid.NewGuid().ToString(),
                    AssetId = request.AssetId,
                    KycProvider = request.KycProvider,
                    KycVerificationDate = request.KycVerificationDate,
                    VerificationStatus = request.VerificationStatus,
                    Jurisdiction = request.Jurisdiction,
                    RegulatoryFramework = request.RegulatoryFramework,
                    ComplianceStatus = request.ComplianceStatus,
                    LastComplianceReview = request.LastComplianceReview,
                    NextComplianceReview = request.NextComplianceReview,
                    AssetType = request.AssetType,
                    TransferRestrictions = request.TransferRestrictions,
                    MaxHolders = request.MaxHolders,
                    RequiresAccreditedInvestors = request.RequiresAccreditedInvestors,
                    Network = request.Network,
                    Notes = request.Notes,
                    CreatedBy = existingMetadata?.CreatedBy ?? createdBy,
                    CreatedAt = existingMetadata?.CreatedAt ?? DateTime.UtcNow,
                    UpdatedBy = existingMetadata != null ? createdBy : null,
                    UpdatedAt = existingMetadata != null ? DateTime.UtcNow : null
                };

                var success = await _complianceRepository.UpsertMetadataAsync(metadata);

                // Log the operation
                await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                {
                    AssetId = request.AssetId,
                    Network = request.Network,
                    ActionType = isUpdate ? ComplianceActionType.Update : ComplianceActionType.Create,
                    PerformedBy = createdBy,
                    Success = success,
                    OldComplianceStatus = existingMetadata?.ComplianceStatus,
                    NewComplianceStatus = request.ComplianceStatus,
                    OldVerificationStatus = existingMetadata?.VerificationStatus,
                    NewVerificationStatus = request.VerificationStatus,
                    Notes = isUpdate ? "Updated compliance metadata" : "Created compliance metadata"
                });

                if (success)
                {
                    _logger.LogInformation(
                        "Upserted compliance metadata for asset {AssetId} by {User}",
                        request.AssetId,
                        createdBy);

                    // Emit metering event for billing analytics
                    _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                    {
                        Category = MeteringCategory.Compliance,
                        OperationType = MeteringOperationType.Upsert,
                        AssetId = request.AssetId,
                        Network = request.Network,
                        PerformedBy = createdBy,
                        ItemCount = 1
                    });

                    return new ComplianceMetadataResponse
                    {
                        Success = true,
                        Metadata = metadata
                    };
                }
                else
                {
                    return new ComplianceMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to save compliance metadata"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting compliance metadata for asset {AssetId}", request.AssetId);

                // Log failed operation
                try
                {
                    await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        Network = request.Network,
                        ActionType = ComplianceActionType.Update,
                        PerformedBy = createdBy,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log audit entry for failed upsert");
                }

                return new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceMetadataResponse> GetMetadataAsync(ulong assetId)
        {
            try
            {
                var metadata = await _complianceRepository.GetMetadataByAssetIdAsync(assetId);

                // Log the read operation
                await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                {
                    AssetId = assetId,
                    Network = metadata?.Network,
                    ActionType = ComplianceActionType.Read,
                    PerformedBy = SystemActor, // Will be overridden by controller if user context available
                    Success = metadata != null,
                    ErrorMessage = metadata == null ? "Compliance metadata not found" : null,
                    Notes = "Retrieved compliance metadata"
                });

                if (metadata == null)
                {
                    return new ComplianceMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = "Compliance metadata not found for this asset"
                    };
                }

                return new ComplianceMetadataResponse
                {
                    Success = true,
                    Metadata = metadata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving compliance metadata for asset {AssetId}", assetId);

                // Log failed operation
                try
                {
                    await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                    {
                        AssetId = assetId,
                        ActionType = ComplianceActionType.Read,
                        PerformedBy = SystemActor,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log audit entry for failed read");
                }

                return new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceMetadataResponse> DeleteMetadataAsync(ulong assetId)
        {
            try
            {
                var success = await _complianceRepository.DeleteMetadataAsync(assetId);

                // Log the delete operation
                await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                {
                    AssetId = assetId,
                    ActionType = ComplianceActionType.Delete,
                    PerformedBy = SystemActor, // Will be overridden by controller if user context available
                    Success = success,
                    ErrorMessage = success ? null : "Compliance metadata not found",
                    Notes = success ? "Deleted compliance metadata" : "Delete failed - metadata not found"
                });

                if (success)
                {
                    _logger.LogInformation("Deleted compliance metadata for asset {AssetId}", assetId);
                    
                    // Emit metering event for billing analytics
                    _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                    {
                        Category = MeteringCategory.Compliance,
                        OperationType = MeteringOperationType.Delete,
                        AssetId = assetId,
                        Network = null, // Network info not available in delete context
                        PerformedBy = null, // User context not available in delete operation
                        ItemCount = 1
                    });
                    
                    return new ComplianceMetadataResponse
                    {
                        Success = true
                    };
                }
                else
                {
                    return new ComplianceMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = "Compliance metadata not found for this asset"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting compliance metadata for asset {AssetId}", assetId);

                // Log failed operation
                try
                {
                    await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                    {
                        AssetId = assetId,
                        ActionType = ComplianceActionType.Delete,
                        PerformedBy = SystemActor,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log audit entry for failed delete");
                }

                return new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceMetadataListResponse> ListMetadataAsync(ListComplianceMetadataRequest request)
        {
            try
            {
                var metadata = await _complianceRepository.ListMetadataAsync(request);
                var totalCount = await _complianceRepository.GetMetadataCountAsync(request);
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Log the list operation
                var filterCriteria = new List<string>();
                if (request.ComplianceStatus.HasValue)
                    filterCriteria.Add($"ComplianceStatus={request.ComplianceStatus.Value}");
                if (request.VerificationStatus.HasValue)
                    filterCriteria.Add($"VerificationStatus={request.VerificationStatus.Value}");
                if (!string.IsNullOrWhiteSpace(request.Network))
                    filterCriteria.Add($"Network={request.Network}");

                await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                {
                    ActionType = ComplianceActionType.List,
                    PerformedBy = SystemActor, // Will be overridden by controller if user context available
                    Success = true,
                    ItemCount = metadata.Count,
                    FilterCriteria = filterCriteria.Count > 0 ? string.Join(", ", filterCriteria) : "No filters",
                    Notes = $"Listed {metadata.Count} of {totalCount} total compliance metadata entries"
                });

                return new ComplianceMetadataListResponse
                {
                    Success = true,
                    Metadata = metadata,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing compliance metadata");

                // Log failed operation
                try
                {
                    await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                    {
                        ActionType = ComplianceActionType.List,
                        PerformedBy = SystemActor,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log audit entry for failed list");
                }

                return new ComplianceMetadataListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceAuditLogResponse> GetAuditLogAsync(GetComplianceAuditLogRequest request)
        {
            try
            {
                var entries = await _complianceRepository.GetAuditLogAsync(request);
                var totalCount = await _complianceRepository.GetAuditLogCountAsync(request);
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                return new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = entries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    RetentionPolicy = new AuditRetentionPolicy
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
                _logger.LogError(ex, "Error retrieving compliance audit log");
                return new ComplianceAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public string? ValidateNetworkRules(string? network, UpsertComplianceMetadataRequest metadata)
        {
            if (string.IsNullOrWhiteSpace(network))
            {
                // No network specified, no specific validation
                return null;
            }

            // Normalize network name
            var normalizedNetwork = network.ToLowerInvariant();

            // VOI network specific rules
            if (normalizedNetwork.Contains("voi"))
            {
                // VOI requires KYC verification for RWA tokens
                if (metadata.VerificationStatus != VerificationStatus.Verified && 
                    metadata.RequiresAccreditedInvestors)
                {
                    return "VOI network requires KYC verification (VerificationStatus=Verified) for tokens requiring accredited investors";
                }

                // VOI requires jurisdiction to be specified
                if (string.IsNullOrWhiteSpace(metadata.Jurisdiction))
                {
                    return "VOI network requires jurisdiction to be specified for compliance";
                }

                _logger.LogDebug("Validated VOI network rules for asset {AssetId}", metadata.AssetId);
            }

            // Aramid network specific rules
            if (normalizedNetwork.Contains("aramid"))
            {
                // Aramid requires regulatory framework for compliant status
                if (metadata.ComplianceStatus == ComplianceStatus.Compliant && 
                    string.IsNullOrWhiteSpace(metadata.RegulatoryFramework))
                {
                    return "Aramid network requires RegulatoryFramework to be specified when ComplianceStatus is Compliant";
                }

                // Aramid requires MaxHolders to be set for securities
                if (!string.IsNullOrWhiteSpace(metadata.AssetType) && 
                    metadata.AssetType.ToLowerInvariant().Contains("security") && 
                    !metadata.MaxHolders.HasValue)
                {
                    return "Aramid network requires MaxHolders to be specified for security tokens";
                }

                _logger.LogDebug("Validated Aramid network rules for asset {AssetId}", metadata.AssetId);
            }

            return null;
        }
    }
}

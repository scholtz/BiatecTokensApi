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
                    return new ComplianceMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = validationError
                    };
                }

                // Check if metadata already exists
                var existingMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(request.AssetId);

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
                return new ComplianceMetadataListResponse
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

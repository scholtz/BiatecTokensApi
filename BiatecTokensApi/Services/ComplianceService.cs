using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.IO.Compression;
using System.Security.Cryptography;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing compliance metadata with network-specific validation
    /// </summary>
    public class ComplianceService : IComplianceService
    {
        private readonly IComplianceRepository _complianceRepository;
        private readonly IWhitelistService _whitelistService;
        private readonly ILogger<ComplianceService> _logger;
        private readonly ISubscriptionMeteringService _meteringService;
        
        /// <summary>
        /// Constant for system-generated audit entries when no user context is available
        /// </summary>
        private const string SystemActor = "System";

        // Verification scoring weights
        private const int ScoreLegalName = 5;
        private const int ScoreCountry = 5;
        private const int ScoreAddress = 10;
        private const int ScoreContact = 10;
        private const int ScoreRegistration = 10;
        private const int ScoreKybVerified = 30;
        private const int ScoreKybInProgress = 15;
        private const int ScoreKybPending = 5;
        private const int ScoreMicaApproved = 30;
        private const int ScoreMicaUnderReview = 15;
        private const int ScoreMicaApplied = 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceService"/> class.
        /// </summary>
        /// <param name="complianceRepository">The compliance repository</param>
        /// <param name="whitelistService">The whitelist service</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="meteringService">The subscription metering service</param>
        public ComplianceService(
            IComplianceRepository complianceRepository,
            IWhitelistService whitelistService,
            ILogger<ComplianceService> logger,
            ISubscriptionMeteringService meteringService)
        {
            _complianceRepository = complianceRepository;
            _whitelistService = whitelistService;
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

        /// <inheritdoc/>
        public async Task<ValidateTokenPresetResponse> ValidateTokenPresetAsync(ValidateTokenPresetRequest request)
        {
            try
            {
                var errors = new List<ValidationIssue>();
                var warnings = new List<ValidationIssue>();

                // Validate MICA/RWA compliance requirements
                ValidateMICACompliance(request, errors, warnings);

                // Validate network-specific rules
                ValidateNetworkSpecificRules(request, errors, warnings);

                // Validate whitelist and issuer controls
                ValidateTokenControls(request, errors, warnings);

                // Determine if configuration is valid (no errors)
                var isValid = errors.Count == 0;

                // Generate summary
                var summary = GenerateValidationSummary(isValid, errors.Count, warnings.Count);

                // Filter warnings if not requested
                var finalWarnings = request.IncludeWarnings ? warnings : new List<ValidationIssue>();

                return await Task.FromResult(new ValidateTokenPresetResponse
                {
                    Success = true,
                    IsValid = isValid,
                    Errors = errors,
                    Warnings = finalWarnings,
                    Summary = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token preset");
                return new ValidateTokenPresetResponse
                {
                    Success = false,
                    IsValid = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validates MICA/RWA compliance requirements
        /// </summary>
        private void ValidateMICACompliance(
            ValidateTokenPresetRequest request,
            List<ValidationIssue> errors,
            List<ValidationIssue> warnings)
        {
            var isSecurityToken = IsSecurityToken(request.AssetType);

            // MICA requires KYC verification for security tokens
            if (isSecurityToken && request.VerificationStatus != VerificationStatus.Verified)
            {
                errors.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Field = "VerificationStatus",
                    Message = "Security tokens require KYC verification to be completed (VerificationStatus=Verified)",
                    Recommendation = "Complete KYC verification through your chosen provider before deploying the token",
                    RegulatoryContext = "MICA (Markets in Crypto-Assets Regulation)"
                });
            }

            // MICA requires jurisdiction specification for RWA tokens
            if (string.IsNullOrWhiteSpace(request.Jurisdiction))
            {
                if (isSecurityToken || request.RequiresAccreditedInvestors)
                {
                    errors.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Field = "Jurisdiction",
                        Message = "Jurisdiction must be specified for security tokens and tokens requiring accredited investors",
                        Recommendation = "Specify applicable jurisdiction(s) using ISO country codes (e.g., 'US', 'EU', 'US,EU')",
                        RegulatoryContext = "MICA"
                    });
                }
                else
                {
                    warnings.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Field = "Jurisdiction",
                        Message = "Jurisdiction is not specified. This may limit token distribution",
                        Recommendation = "Consider specifying jurisdiction(s) to clarify regulatory compliance",
                        RegulatoryContext = "MICA"
                    });
                }
            }

            // MICA requires regulatory framework for compliant tokens
            if (request.ComplianceStatus == ComplianceStatus.Compliant &&
                string.IsNullOrWhiteSpace(request.RegulatoryFramework))
            {
                errors.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Field = "RegulatoryFramework",
                    Message = "Regulatory framework must be specified when compliance status is 'Compliant'",
                    Recommendation = "Specify applicable regulatory framework (e.g., 'SEC Reg D', 'MiFID II', 'MICA')",
                    RegulatoryContext = "MICA"
                });
            }

            // Security tokens should have max holders specified
            if (isSecurityToken && !request.MaxHolders.HasValue)
            {
                warnings.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Field = "MaxHolders",
                    Message = "Maximum number of holders is not specified for security token",
                    Recommendation = "Consider setting a maximum holder limit to comply with securities regulations",
                    RegulatoryContext = "Securities Regulations"
                });
            }

            // Accredited investor tokens require whitelist
            if (request.RequiresAccreditedInvestors && !request.HasWhitelistControls)
            {
                errors.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Field = "HasWhitelistControls",
                    Message = "Tokens requiring accredited investors must have whitelist controls enabled",
                    Recommendation = "Enable whitelist controls to restrict token transfers to verified accredited investors",
                    RegulatoryContext = "Securities Act - Accredited Investor Requirements"
                });
            }
        }

        /// <summary>
        /// Validates network-specific compliance rules
        /// </summary>
        private void ValidateNetworkSpecificRules(
            ValidateTokenPresetRequest request,
            List<ValidationIssue> errors,
            List<ValidationIssue> warnings)
        {
            if (string.IsNullOrWhiteSpace(request.Network))
            {
                return;
            }

            var normalizedNetwork = request.Network.ToLowerInvariant();

            // VOI network specific rules
            if (normalizedNetwork.Contains("voi"))
            {
                // VOI requires KYC verification for tokens requiring accredited investors
                if (request.RequiresAccreditedInvestors && 
                    request.VerificationStatus != VerificationStatus.Verified)
                {
                    errors.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Field = "VerificationStatus",
                        Message = "VOI network requires KYC verification (VerificationStatus=Verified) for tokens requiring accredited investors",
                        Recommendation = "Complete KYC verification before deploying on VOI network",
                        RegulatoryContext = "VOI Network Policy"
                    });
                }

                // VOI requires jurisdiction to be specified
                if (string.IsNullOrWhiteSpace(request.Jurisdiction))
                {
                    errors.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Field = "Jurisdiction",
                        Message = "VOI network requires jurisdiction to be specified for compliance",
                        Recommendation = "Specify applicable jurisdiction(s) for your token",
                        RegulatoryContext = "VOI Network Policy"
                    });
                }

                _logger.LogDebug("Validated VOI network rules");
            }

            // Aramid network specific rules
            if (normalizedNetwork.Contains("aramid"))
            {
                // Aramid requires regulatory framework for compliant status
                if (request.ComplianceStatus == ComplianceStatus.Compliant &&
                    string.IsNullOrWhiteSpace(request.RegulatoryFramework))
                {
                    errors.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Field = "RegulatoryFramework",
                        Message = "Aramid network requires RegulatoryFramework to be specified when ComplianceStatus is Compliant",
                        Recommendation = "Specify the regulatory framework your token complies with",
                        RegulatoryContext = "Aramid Network Policy"
                    });
                }

                // Aramid requires MaxHolders to be set for securities
                var isSecurityToken = IsSecurityToken(request.AssetType);

                if (isSecurityToken && !request.MaxHolders.HasValue)
                {
                    errors.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Field = "MaxHolders",
                        Message = "Aramid network requires MaxHolders to be specified for security tokens",
                        Recommendation = "Set a maximum number of token holders",
                        RegulatoryContext = "Aramid Network Policy"
                    });
                }

                _logger.LogDebug("Validated Aramid network rules");
            }
        }

        /// <summary>
        /// Validates token controls (whitelist and issuer controls)
        /// </summary>
        private void ValidateTokenControls(
            ValidateTokenPresetRequest request,
            List<ValidationIssue> errors,
            List<ValidationIssue> warnings)
        {
            var isSecurityToken = IsSecurityToken(request.AssetType);

            // Security tokens should have whitelist controls
            if (isSecurityToken && !request.HasWhitelistControls)
            {
                warnings.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Field = "HasWhitelistControls",
                    Message = "Security tokens typically require whitelist controls to restrict transfers",
                    Recommendation = "Enable whitelist controls to ensure only verified investors can hold the token",
                    RegulatoryContext = "Securities Best Practices"
                });
            }

            // Security tokens or RWA tokens should have issuer controls
            // Only add one warning to avoid duplicates
            if (!request.HasIssuerControls && (isSecurityToken || request.RequiresAccreditedInvestors))
            {
                var message = isSecurityToken
                    ? "Security tokens typically require issuer controls (freeze, clawback) for regulatory compliance"
                    : "RWA tokens benefit from issuer controls for compliance and dispute resolution";
                
                var recommendation = isSecurityToken
                    ? "Enable issuer controls to allow freezing accounts and clawback in case of regulatory requirements or disputes"
                    : "Consider enabling freeze and clawback controls for regulatory compliance";

                warnings.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Field = "HasIssuerControls",
                    Message = message,
                    Recommendation = recommendation,
                    RegulatoryContext = isSecurityToken ? "Securities Best Practices" : "RWA Best Practices"
                });
            }

            // Warn if no controls are enabled for compliance-heavy tokens
            if (!request.HasWhitelistControls && !request.HasIssuerControls &&
                (isSecurityToken || request.RequiresAccreditedInvestors || 
                 request.ComplianceStatus == ComplianceStatus.Compliant))
            {
                warnings.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Field = "TokenControls",
                    Message = "No whitelist or issuer controls are enabled. This may limit compliance options",
                    Recommendation = "Consider enabling whitelist and/or issuer controls for better regulatory compliance",
                    RegulatoryContext = "General Compliance"
                });
            }
        }

        /// <summary>
        /// Determines if an asset type represents a security token
        /// </summary>
        private bool IsSecurityToken(string? assetType)
        {
            return !string.IsNullOrWhiteSpace(assetType) &&
                   assetType.ToLowerInvariant().Contains("security");
        }

        /// <summary>
        /// Generates a validation summary message
        /// </summary>
        private string GenerateValidationSummary(bool isValid, int errorCount, int warningCount)
        {
            if (isValid && warningCount == 0)
            {
                return "Token configuration is valid and compliant with MICA/RWA requirements";
            }
            else if (isValid && warningCount > 0)
            {
                return $"Token configuration is valid but has {warningCount} warning(s) that should be reviewed";
            }
            else
            {
                return $"Token configuration has {errorCount} error(s) that must be fixed before deployment";
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceAttestationResponse> CreateAttestationAsync(
            CreateComplianceAttestationRequest request,
            string createdBy)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.WalletAddress))
                {
                    return new ComplianceAttestationResponse
                    {
                        Success = false,
                        ErrorMessage = "Wallet address is required"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.IssuerAddress))
                {
                    return new ComplianceAttestationResponse
                    {
                        Success = false,
                        ErrorMessage = "Issuer address is required"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.ProofHash))
                {
                    return new ComplianceAttestationResponse
                    {
                        Success = false,
                        ErrorMessage = "Proof hash is required"
                    };
                }

                // Create attestation
                var attestation = new ComplianceAttestation
                {
                    Id = Guid.NewGuid().ToString(),
                    WalletAddress = request.WalletAddress,
                    AssetId = request.AssetId,
                    IssuerAddress = request.IssuerAddress,
                    ProofHash = request.ProofHash,
                    ProofType = request.ProofType,
                    AttestationType = request.AttestationType,
                    Network = request.Network,
                    Jurisdiction = request.Jurisdiction,
                    RegulatoryFramework = request.RegulatoryFramework,
                    ExpiresAt = request.ExpiresAt,
                    Notes = request.Notes,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    VerificationStatus = AttestationVerificationStatus.Pending
                };

                var success = await _complianceRepository.CreateAttestationAsync(attestation);

                if (success)
                {
                    // Emit metering event for billing analytics
                    _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                    {
                        Category = MeteringCategory.Compliance,
                        OperationType = MeteringOperationType.Add,
                        AssetId = request.AssetId,
                        Network = request.Network,
                        PerformedBy = createdBy,
                        ItemCount = 1,
                        Metadata = new Dictionary<string, string>
                        {
                            { "Operation", "CreateAttestation" },
                            { "WalletAddress", request.WalletAddress },
                            { "AttestationType", request.AttestationType ?? "General" }
                        }
                    });

                    _logger.LogInformation(
                        "Created attestation {Id} for wallet {WalletAddress} and asset {AssetId}",
                        attestation.Id,
                        attestation.WalletAddress,
                        attestation.AssetId);

                    return new ComplianceAttestationResponse
                    {
                        Success = true,
                        Attestation = attestation
                    };
                }
                else
                {
                    return new ComplianceAttestationResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to create attestation"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating attestation for wallet {WalletAddress}", request.WalletAddress);
                return new ComplianceAttestationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceAttestationResponse> GetAttestationAsync(string id)
        {
            try
            {
                var attestation = await _complianceRepository.GetAttestationByIdAsync(id);

                if (attestation != null)
                {
                    // Note: We modify the status for display purposes only, not persisted
                    // This provides real-time expiry checking without database updates
                    if (attestation.ExpiresAt.HasValue && 
                        attestation.ExpiresAt.Value < DateTime.UtcNow && 
                        attestation.VerificationStatus != AttestationVerificationStatus.Expired)
                    {
                        attestation.VerificationStatus = AttestationVerificationStatus.Expired;
                    }

                    return new ComplianceAttestationResponse
                    {
                        Success = true,
                        Attestation = attestation
                    };
                }
                else
                {
                    return new ComplianceAttestationResponse
                    {
                        Success = false,
                        ErrorMessage = "Attestation not found"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving attestation {Id}", id);
                return new ComplianceAttestationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceAttestationListResponse> ListAttestationsAsync(ListComplianceAttestationsRequest request)
        {
            try
            {
                var attestations = await _complianceRepository.ListAttestationsAsync(request);
                var totalCount = await _complianceRepository.GetAttestationCountAsync(request);
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Note: List operations typically don't emit metering events to avoid excessive billing
                // Only create/update/delete operations are metered
                _logger.LogInformation("Listed {Count} attestations (page {Page} of {TotalPages})", 
                    attestations.Count, request.Page, totalPages);

                return new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing attestations");
                return new ComplianceAttestationListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TokenComplianceReportResponse> GetComplianceReportAsync(
            GetTokenComplianceReportRequest request,
            string requestedBy)
        {
            try
            {
                _logger.LogInformation(
                    "Generating compliance report for network: {Network}, asset: {AssetId}, requested by: {RequestedBy}",
                    request.Network ?? "All", request.AssetId?.ToString() ?? "All", requestedBy);

                // Get compliance metadata with optional filtering
                var metadataListRequest = new ListComplianceMetadataRequest
                {
                    Network = request.Network,
                    Page = request.Page,
                    PageSize = request.PageSize
                };

                var metadataList = await _complianceRepository.ListMetadataAsync(metadataListRequest);
                var totalCount = await _complianceRepository.GetMetadataCountAsync(metadataListRequest);

                // If a specific asset ID is requested, filter to just that asset
                if (request.AssetId.HasValue)
                {
                    metadataList = metadataList.Where(m => m.AssetId == request.AssetId.Value).ToList();
                    totalCount = metadataList.Count;
                }

                var tokenStatuses = new List<TokenComplianceStatus>();

                foreach (var metadata in metadataList)
                {
                    var tokenStatus = await BuildTokenComplianceStatusAsync(metadata, request, requestedBy);
                    tokenStatuses.Add(tokenStatus);
                }

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Emit metering event for compliance report generation
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Network = request.Network,
                    AssetId = request.AssetId ?? 0,
                    OperationType = MeteringOperationType.Upsert, // Using Upsert as generic "operation"
                    Category = MeteringCategory.Compliance,
                    PerformedBy = requestedBy,
                    ItemCount = tokenStatuses.Count,
                    Metadata = new Dictionary<string, string>
                    {
                        { "ReportType", "ComplianceReport" },
                        { "IncludeWhitelist", request.IncludeWhitelistDetails.ToString() },
                        { "IncludeTransfers", request.IncludeTransferAudits.ToString() },
                        { "IncludeCompliance", request.IncludeComplianceAudits.ToString() }
                    }
                });

                _logger.LogInformation(
                    "Generated compliance report with {Count} tokens for {RequestedBy}",
                    tokenStatuses.Count, requestedBy);

                return new TokenComplianceReportResponse
                {
                    Success = true,
                    Tokens = tokenStatuses,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    GeneratedAt = DateTime.UtcNow,
                    NetworkFilter = request.Network,
                    SubscriptionInfo = new ReportSubscriptionInfo
                    {
                        TierName = "Enterprise", // Could be retrieved from subscription service
                        AuditLogEnabled = true,
                        MaxAssetsPerReport = 100,
                        DetailedReportsEnabled = true,
                        Metered = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception generating compliance report");
                return new TokenComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Builds a comprehensive compliance status for a single token
        /// </summary>
        private async Task<TokenComplianceStatus> BuildTokenComplianceStatusAsync(
            ComplianceMetadata metadata,
            GetTokenComplianceReportRequest request,
            string requestedBy)
        {
            var tokenStatus = new TokenComplianceStatus
            {
                AssetId = metadata.AssetId,
                Network = metadata.Network,
                ComplianceMetadata = metadata
            };

            // Get whitelist summary if requested
            if (request.IncludeWhitelistDetails)
            {
                tokenStatus.WhitelistSummary = await GetWhitelistSummaryAsync(
                    metadata.AssetId,
                    request.FromDate,
                    request.ToDate);
            }

            // Get compliance audit entries if requested
            if (request.IncludeComplianceAudits)
            {
                var complianceAuditRequest = new GetComplianceAuditLogRequest
                {
                    AssetId = metadata.AssetId,
                    Network = metadata.Network,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    PageSize = request.MaxAuditEntriesPerCategory
                };
                var complianceAuditResult = await _complianceRepository.GetAuditLogAsync(complianceAuditRequest);
                tokenStatus.ComplianceAuditEntries = complianceAuditResult;
            }

            // Get whitelist audit entries if requested
            if (request.IncludeWhitelistDetails || request.IncludeTransferAudits)
            {
                tokenStatus.WhitelistAuditEntries = await GetWhitelistAuditEntriesAsync(
                    metadata.AssetId,
                    metadata.Network,
                    request.FromDate,
                    request.ToDate,
                    request.MaxAuditEntriesPerCategory,
                    includeTransferValidations: false);

                if (request.IncludeTransferAudits)
                {
                    tokenStatus.TransferValidationEntries = await GetWhitelistAuditEntriesAsync(
                        metadata.AssetId,
                        metadata.Network,
                        request.FromDate,
                        request.ToDate,
                        request.MaxAuditEntriesPerCategory,
                        includeTransferValidations: true);
                }
            }

            // Calculate compliance health score
            tokenStatus.ComplianceHealthScore = CalculateComplianceHealthScore(tokenStatus);

            // Evaluate VOI/Aramid specific compliance
            tokenStatus.NetworkSpecificStatus = EvaluateNetworkSpecificCompliance(metadata);

            // Identify warnings
            tokenStatus.Warnings = IdentifyComplianceWarnings(tokenStatus);

            return tokenStatus;
        }

        /// <summary>
        /// Gets whitelist summary statistics for a token
        /// </summary>
        private async Task<WhitelistSummary> GetWhitelistSummaryAsync(
            ulong assetId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            // Note: This requires access to whitelist repository
            // For now, return a placeholder. In production, inject IWhitelistRepository
            // TODO: Integrate with IWhitelistRepository to fetch actual whitelist statistics
            _logger.LogWarning(
                "Returning placeholder whitelist summary for asset {AssetId}. Whitelist repository integration pending.",
                assetId);
            
            return new WhitelistSummary
            {
                TotalAddresses = 0,
                ActiveAddresses = 0,
                RevokedAddresses = 0,
                SuspendedAddresses = 0,
                KycVerifiedAddresses = 0,
                LastModified = null,
                TransferValidationsCount = 0,
                DeniedTransfersCount = 0
            };
        }

        /// <summary>
        /// Gets whitelist audit entries for a token
        /// </summary>
        private async Task<List<Models.Whitelist.WhitelistAuditLogEntry>> GetWhitelistAuditEntriesAsync(
            ulong assetId,
            string? network,
            DateTime? fromDate,
            DateTime? toDate,
            int maxEntries,
            bool includeTransferValidations)
        {
            // Note: This requires access to whitelist repository
            // For now, return empty list. In production, inject IWhitelistRepository
            // TODO: Integrate with IWhitelistRepository to fetch actual whitelist audit entries
            _logger.LogWarning(
                "Returning empty whitelist audit entries for asset {AssetId}, transferValidations: {IncludeTransfers}. Whitelist repository integration pending.",
                assetId, includeTransferValidations);
            
            return new List<Models.Whitelist.WhitelistAuditLogEntry>();
        }

        /// <summary>
        /// Calculates a compliance health score (0-100) based on multiple factors
        /// </summary>
        private int CalculateComplianceHealthScore(TokenComplianceStatus status)
        {
            int score = 0;

            if (status.ComplianceMetadata != null)
            {
                // Compliance status contribution (40 points)
                score += status.ComplianceMetadata.ComplianceStatus switch
                {
                    ComplianceStatus.Compliant => 40,
                    ComplianceStatus.UnderReview => 20,
                    ComplianceStatus.Exempt => 30,
                    ComplianceStatus.NonCompliant => 0,
                    ComplianceStatus.Suspended => 0,
                    _ => 0
                };

                // Verification status contribution (30 points)
                score += status.ComplianceMetadata.VerificationStatus switch
                {
                    VerificationStatus.Verified => 30,
                    VerificationStatus.InProgress => 15,
                    VerificationStatus.Pending => 10,
                    VerificationStatus.Failed => 0,
                    VerificationStatus.Expired => 0,
                    _ => 0
                };

                // Regulatory framework specified (10 points)
                if (!string.IsNullOrEmpty(status.ComplianceMetadata.RegulatoryFramework))
                {
                    score += 10;
                }

                // KYC provider specified (10 points)
                if (!string.IsNullOrEmpty(status.ComplianceMetadata.KycProvider))
                {
                    score += 10;
                }

                // Jurisdiction specified (10 points)
                if (!string.IsNullOrEmpty(status.ComplianceMetadata.Jurisdiction))
                {
                    score += 10;
                }
            }

            // Cap score at 100
            return Math.Min(100, score);
        }

        /// <summary>
        /// Evaluates network-specific compliance rules for VOI/Aramid
        /// </summary>
        private NetworkComplianceStatus EvaluateNetworkSpecificCompliance(ComplianceMetadata metadata)
        {
            var networkStatus = new NetworkComplianceStatus
            {
                MeetsNetworkRequirements = true
            };

            if (string.IsNullOrEmpty(metadata.Network))
            {
                return networkStatus;
            }

            // VOI Network Rules
            if (metadata.Network.StartsWith("voimain", StringComparison.Ordinal))
            {
                // Rule 1: KYC verification recommended for tokens requiring accredited investors
                if (metadata.RequiresAccreditedInvestors && metadata.VerificationStatus == VerificationStatus.Verified)
                {
                    networkStatus.SatisfiedRules.Add("VOI: KYC verification present for accredited investor tokens");
                }
                else if (metadata.RequiresAccreditedInvestors && metadata.VerificationStatus != VerificationStatus.Verified)
                {
                    networkStatus.ViolatedRules.Add("VOI: KYC verification recommended for tokens requiring accredited investors");
                    networkStatus.Recommendations.Add("Complete KYC verification to meet VOI network best practices");
                    networkStatus.MeetsNetworkRequirements = false;
                }

                // Rule 2: Jurisdiction should be specified
                if (!string.IsNullOrEmpty(metadata.Jurisdiction))
                {
                    networkStatus.SatisfiedRules.Add("VOI: Jurisdiction specified for compliance tracking");
                }
                else
                {
                    networkStatus.ViolatedRules.Add("VOI: Jurisdiction not specified");
                    networkStatus.Recommendations.Add("Specify jurisdiction to meet VOI network requirements");
                    networkStatus.MeetsNetworkRequirements = false;
                }
            }

            // Aramid Network Rules
            if (metadata.Network.StartsWith("aramidmain", StringComparison.Ordinal))
            {
                // Rule 1: Regulatory framework required for compliant status
                if (metadata.ComplianceStatus == ComplianceStatus.Compliant && !string.IsNullOrEmpty(metadata.RegulatoryFramework))
                {
                    networkStatus.SatisfiedRules.Add("Aramid: Regulatory framework specified for compliant tokens");
                }
                else if (metadata.ComplianceStatus == ComplianceStatus.Compliant && string.IsNullOrEmpty(metadata.RegulatoryFramework))
                {
                    networkStatus.ViolatedRules.Add("Aramid: Regulatory framework required when status is Compliant");
                    networkStatus.Recommendations.Add("Specify regulatory framework (e.g., MICA, SEC Reg D) for Aramid network");
                    networkStatus.MeetsNetworkRequirements = false;
                }

                // Rule 2: MaxHolders should be set for security tokens
                if (!string.IsNullOrEmpty(metadata.AssetType) && 
                    metadata.AssetType.Contains("Security", StringComparison.OrdinalIgnoreCase) &&
                    metadata.MaxHolders.HasValue)
                {
                    networkStatus.SatisfiedRules.Add("Aramid: MaxHolders specified for security tokens");
                }
                else if (!string.IsNullOrEmpty(metadata.AssetType) && 
                         metadata.AssetType.Contains("Security", StringComparison.OrdinalIgnoreCase) &&
                         !metadata.MaxHolders.HasValue)
                {
                    networkStatus.ViolatedRules.Add("Aramid: MaxHolders should be specified for security tokens");
                    networkStatus.Recommendations.Add("Set maximum holder count for Aramid security tokens");
                    networkStatus.MeetsNetworkRequirements = false;
                }
            }

            return networkStatus;
        }

        /// <summary>
        /// Identifies compliance warnings based on token status
        /// </summary>
        private List<string> IdentifyComplianceWarnings(TokenComplianceStatus status)
        {
            var warnings = new List<string>();

            if (status.ComplianceMetadata == null)
            {
                warnings.Add("No compliance metadata found for this token");
                return warnings;
            }

            var metadata = status.ComplianceMetadata;

            // Check verification status
            if (metadata.VerificationStatus == VerificationStatus.Expired)
            {
                warnings.Add("KYC verification has expired and needs renewal");
            }
            else if (metadata.VerificationStatus == VerificationStatus.Failed)
            {
                warnings.Add("KYC verification failed - compliance review required");
            }

            // Check compliance review dates
            if (metadata.NextComplianceReview.HasValue && metadata.NextComplianceReview.Value < DateTime.UtcNow)
            {
                warnings.Add("Compliance review is overdue");
            }

            // Check compliance status
            if (metadata.ComplianceStatus == ComplianceStatus.NonCompliant)
            {
                warnings.Add("Token is marked as non-compliant - immediate action required");
            }
            else if (metadata.ComplianceStatus == ComplianceStatus.Suspended)
            {
                warnings.Add("Token compliance is suspended - operations may be restricted");
            }

            // Check network-specific warnings
            if (status.NetworkSpecificStatus != null && !status.NetworkSpecificStatus.MeetsNetworkRequirements)
            {
                warnings.Add($"Token does not meet {metadata.Network} network requirements");
            }

            return warnings;
        }

        /// <inheritdoc/>
        public async Task<AttestationPackageResponse> GenerateAttestationPackageAsync(
            GenerateAttestationPackageRequest request, 
            string requestedBy)
        {
            try
            {
                // Validate date range
                if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate > request.ToDate)
                {
                    return new AttestationPackageResponse
                    {
                        Success = false,
                        ErrorMessage = "FromDate cannot be greater than ToDate"
                    };
                }

                // Validate format
                if (!string.Equals(request.Format, "json", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(request.Format, "pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return new AttestationPackageResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid format. Supported formats are 'json' and 'pdf'."
                    };
                }

                // Note: PDF format is validated here but not yet implemented in the controller
                // The controller will return 501 Not Implemented for PDF requests

                // Get compliance metadata for the token
                var metadata = await _complianceRepository.GetMetadataByAssetIdAsync(request.TokenId);
                
                // Get attestations for the token in the date range
                // Note: PageSize is set to 100. For tokens with more attestations,
                // consider implementing pagination or increasing the page size
                var attestationsRequest = new ListComplianceAttestationsRequest
                {
                    AssetId = request.TokenId,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    PageSize = 100
                };
                var attestations = await _complianceRepository.ListAttestationsAsync(attestationsRequest);

                // Build the attestation package
                var package = new AttestationPackage
                {
                    TokenId = request.TokenId,
                    GeneratedAt = DateTime.UtcNow,
                    IssuerAddress = requestedBy,
                    Network = metadata?.Network,
                    ComplianceMetadata = metadata,
                    Attestations = attestations,
                    DateRange = new DateRangeInfo
                    {
                        From = request.FromDate,
                        To = request.ToDate
                    }
                };

                // Get whitelist policy info
                // TODO: Integrate with WhitelistService for actual whitelist data
                // Currently using placeholder data - this is a known limitation
                package.WhitelistPolicy = new WhitelistPolicyInfo
                {
                    IsEnabled = false,
                    TotalWhitelisted = 0,
                    EnforcementType = "None"
                };

                // Get compliance status info
                if (metadata != null)
                {
                    package.ComplianceStatus = new ComplianceStatusInfo
                    {
                        Status = metadata.ComplianceStatus,
                        VerificationStatus = metadata.VerificationStatus,
                        LastReviewDate = metadata.LastComplianceReview,
                        NextReviewDate = metadata.NextComplianceReview
                    };
                }

                // Token metadata
                // TODO: Integrate with blockchain service to retrieve actual token metadata
                // Currently using placeholder with only AssetId - this is a known limitation
                // affecting audit completeness. Future enhancement should query Algorand/EVM
                // networks for creator, manager, reserve, freeze, and clawback addresses
                package.Token = new TokenMetadata
                {
                    AssetId = request.TokenId
                };

                // Generate deterministic hash of package content
                package.ContentHash = GeneratePackageHash(package);

                // Generate signature metadata
                // TODO: Implement actual cryptographic signature using private key
                // Currently providing structure only - this is a known limitation
                // For production use, integrate with key management system to sign
                // the ContentHash with the issuer's private key
                package.Signature = new SignatureMetadata
                {
                    Algorithm = "SHA256",
                    SignedAt = DateTime.UtcNow
                    // SignatureValue and PublicKey would be populated with actual signature
                };

                // Emit metering event for package generation
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Compliance,
                    OperationType = MeteringOperationType.Export,
                    AssetId = request.TokenId,
                    Network = metadata?.Network ?? "unknown",
                    PerformedBy = requestedBy,
                    ItemCount = package.Attestations.Count,
                    Metadata = new Dictionary<string, string>
                    {
                        ["exportFormat"] = request.Format,
                        ["exportType"] = "attestationPackage",
                        ["attestationCount"] = package.Attestations.Count.ToString(),
                        ["fromDate"] = request.FromDate?.ToString("O") ?? "none",
                        ["toDate"] = request.ToDate?.ToString("O") ?? "none",
                        ["hasComplianceMetadata"] = (metadata != null).ToString(),
                        ["contentHash"] = package.ContentHash
                    }
                });

                _logger.LogInformation(
                    "Generated attestation package for token {TokenId} by {RequestedBy}, format: {Format}, attestations: {Count}",
                    request.TokenId,
                    requestedBy,
                    request.Format,
                    package.Attestations.Count);

                return new AttestationPackageResponse
                {
                    Success = true,
                    Package = package,
                    Format = request.Format
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception generating attestation package for token {TokenId}", request.TokenId);
                return new AttestationPackageResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to generate attestation package: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Generates a deterministic SHA-256 hash of the package content for verification
        /// </summary>
        private string GeneratePackageHash(AttestationPackage package)
        {
            try
            {
                // Create a canonical JSON representation of the package content
                // Exclude the hash and signature fields themselves
                var contentForHashing = new
                {
                    package.TokenId,
                    package.GeneratedAt,
                    package.IssuerAddress,
                    package.Network,
                    ComplianceMetadata = package.ComplianceMetadata != null ? new
                    {
                        package.ComplianceMetadata.AssetId,
                        package.ComplianceMetadata.ComplianceStatus,
                        package.ComplianceMetadata.VerificationStatus,
                        package.ComplianceMetadata.RegulatoryFramework,
                        package.ComplianceMetadata.Jurisdiction
                    } : null,
                    AttestationCount = package.Attestations.Count,
                    AttestationIds = package.Attestations.Select(a => a.Id).ToList(),
                    package.DateRange
                };

                var jsonString = System.Text.Json.JsonSerializer.Serialize(contentForHashing, 
                    new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = false,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(jsonString));
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate package hash");
                return $"ERROR_{Guid.NewGuid()}";
            }
        }

        /// <inheritdoc/>
        public async Task<TokenComplianceIndicatorsResponse> GetComplianceIndicatorsAsync(ulong assetId)
        {
            try
            {
                // Get compliance metadata
                var complianceMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(assetId);

                // Get whitelist entries count (using small page size since we only need the count)
                var whitelistRequest = new ListWhitelistRequest
                {
                    AssetId = assetId,
                    Page = 1,
                    PageSize = 1  // Minimal page size - we only need TotalCount
                };
                
                var whitelistResponse = await _whitelistService.ListEntriesAsync(whitelistRequest);
                var whitelistCount = whitelistResponse.TotalCount;

                var hasComplianceMetadata = complianceMetadata != null;
                var hasWhitelisting = whitelistCount > 0;

                // Calculate MICA readiness
                var isMicaReady = hasComplianceMetadata &&
                    (complianceMetadata.ComplianceStatus == ComplianceStatus.Compliant ||
                     complianceMetadata.ComplianceStatus == ComplianceStatus.Exempt) &&
                    !string.IsNullOrWhiteSpace(complianceMetadata.RegulatoryFramework) &&
                    !string.IsNullOrWhiteSpace(complianceMetadata.Jurisdiction);

                // Calculate enterprise readiness score
                var enterpriseScore = 0;
                if (hasComplianceMetadata) enterpriseScore += 30;
                if (hasWhitelisting) enterpriseScore += 25;
                if (hasComplianceMetadata && complianceMetadata.VerificationStatus == VerificationStatus.Verified) enterpriseScore += 20;
                if (hasComplianceMetadata && !string.IsNullOrWhiteSpace(complianceMetadata.RegulatoryFramework)) enterpriseScore += 15;
                if (hasComplianceMetadata && !string.IsNullOrWhiteSpace(complianceMetadata.Jurisdiction)) enterpriseScore += 10;

                var indicators = new TokenComplianceIndicators
                {
                    AssetId = assetId,
                    IsMicaReady = isMicaReady,
                    WhitelistingEnabled = hasWhitelisting,
                    WhitelistedAddressCount = whitelistCount,
                    HasTransferRestrictions = hasComplianceMetadata && !string.IsNullOrWhiteSpace(complianceMetadata.TransferRestrictions),
                    TransferRestrictions = complianceMetadata?.TransferRestrictions,
                    RequiresAccreditedInvestors = complianceMetadata?.RequiresAccreditedInvestors ?? false,
                    ComplianceStatus = complianceMetadata?.ComplianceStatus.ToString(),
                    VerificationStatus = complianceMetadata?.VerificationStatus.ToString(),
                    RegulatoryFramework = complianceMetadata?.RegulatoryFramework,
                    Jurisdiction = complianceMetadata?.Jurisdiction,
                    MaxHolders = complianceMetadata?.MaxHolders,
                    EnterpriseReadinessScore = enterpriseScore,
                    Network = complianceMetadata?.Network,
                    HasComplianceMetadata = hasComplianceMetadata,
                    LastComplianceUpdate = complianceMetadata?.UpdatedAt ?? complianceMetadata?.CreatedAt
                };

                return new TokenComplianceIndicatorsResponse
                {
                    Success = true,
                    Indicators = indicators
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance indicators for asset {AssetId}", assetId);
                return new TokenComplianceIndicatorsResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to retrieve compliance indicators: {ex.Message}"
                };
            }
        }

        // Phase 2: Issuer Profile Management

        /// <inheritdoc/>
        public async Task<IssuerProfileResponse> UpsertIssuerProfileAsync(UpsertIssuerProfileRequest request, string issuerAddress)
        {
            try
            {
                var existingProfile = await _complianceRepository.GetIssuerProfileAsync(issuerAddress);
                var isUpdate = existingProfile != null;

                var profile = new IssuerProfile
                {
                    IssuerAddress = issuerAddress,
                    LegalName = request.LegalName,
                    DoingBusinessAs = request.DoingBusinessAs,
                    EntityType = request.EntityType,
                    CountryOfIncorporation = request.CountryOfIncorporation,
                    TaxIdentificationNumber = request.TaxIdentificationNumber,
                    RegistrationNumber = request.RegistrationNumber,
                    RegisteredAddress = request.RegisteredAddress,
                    OperationalAddress = request.OperationalAddress,
                    PrimaryContact = request.PrimaryContact,
                    ComplianceContact = request.ComplianceContact,
                    Website = request.Website,
                    KybProvider = request.KybProvider,
                    MicaLicenseNumber = request.MicaLicenseNumber,
                    MicaCompetentAuthority = request.MicaCompetentAuthority,
                    Notes = request.Notes,
                    CreatedBy = existingProfile?.CreatedBy ?? issuerAddress,
                    CreatedAt = existingProfile?.CreatedAt ?? DateTime.UtcNow,
                    UpdatedBy = isUpdate ? issuerAddress : null,
                    UpdatedAt = isUpdate ? DateTime.UtcNow : null,
                    KybStatus = existingProfile?.KybStatus ?? VerificationStatus.Pending,
                    MicaLicenseStatus = existingProfile?.MicaLicenseStatus ?? MicaLicenseStatus.None,
                    Status = existingProfile?.Status ?? IssuerProfileStatus.Draft
                };

                var success = await _complianceRepository.UpsertIssuerProfileAsync(profile);

                return new IssuerProfileResponse
                {
                    Success = success,
                    Profile = success ? profile : null,
                    ErrorMessage = success ? null : "Failed to save issuer profile"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting issuer profile for {IssuerAddress}", issuerAddress);
                return new IssuerProfileResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to save issuer profile: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<IssuerProfileResponse> GetIssuerProfileAsync(string issuerAddress)
        {
            try
            {
                var profile = await _complianceRepository.GetIssuerProfileAsync(issuerAddress);

                return new IssuerProfileResponse
                {
                    Success = profile != null,
                    Profile = profile,
                    ErrorMessage = profile == null ? "Issuer profile not found" : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting issuer profile for {IssuerAddress}", issuerAddress);
                return new IssuerProfileResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to retrieve issuer profile: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<IssuerVerificationResponse> GetIssuerVerificationAsync(string issuerAddress)
        {
            try
            {
                var profile = await _complianceRepository.GetIssuerProfileAsync(issuerAddress);

                if (profile == null)
                {
                    return new IssuerVerificationResponse
                    {
                        Success = false,
                        ErrorMessage = "Issuer profile not found"
                    };
                }

                // Calculate verification score
                int score = 0;
                var missingFields = new List<string>();

                // Profile completeness (40 points total)
                if (!string.IsNullOrEmpty(profile.LegalName)) score += ScoreLegalName; else missingFields.Add("LegalName");
                if (!string.IsNullOrEmpty(profile.CountryOfIncorporation)) score += ScoreCountry; else missingFields.Add("CountryOfIncorporation");
                if (profile.RegisteredAddress != null) score += ScoreAddress; else missingFields.Add("RegisteredAddress");
                if (profile.PrimaryContact != null) score += ScoreContact; else missingFields.Add("PrimaryContact");
                if (!string.IsNullOrEmpty(profile.RegistrationNumber)) score += ScoreRegistration; else missingFields.Add("RegistrationNumber");

                // KYB verification (30 points)
                if (profile.KybStatus == VerificationStatus.Verified)
                {
                    score += ScoreKybVerified;
                }
                else if (profile.KybStatus == VerificationStatus.InProgress)
                {
                    score += ScoreKybInProgress;
                }
                else if (profile.KybStatus == VerificationStatus.Pending)
                {
                    score += ScoreKybPending;
                    missingFields.Add("KYB Verification");
                }

                // MICA license (30 points)
                if (profile.MicaLicenseStatus == MicaLicenseStatus.Approved)
                {
                    score += ScoreMicaApproved;
                }
                else if (profile.MicaLicenseStatus == MicaLicenseStatus.UnderReview)
                {
                    score += ScoreMicaUnderReview;
                }
                else if (profile.MicaLicenseStatus == MicaLicenseStatus.Applied)
                {
                    score += ScoreMicaApplied;
                }
                else
                {
                    missingFields.Add("MICA License");
                }

                // Determine overall status
                IssuerVerificationStatus overallStatus;
                if (score >= 80) overallStatus = IssuerVerificationStatus.FullyVerified;
                else if (score >= 60) overallStatus = IssuerVerificationStatus.PartiallyVerified;
                else if (score >= 30) overallStatus = IssuerVerificationStatus.Pending;
                else if (profile.KybStatus == VerificationStatus.Expired) overallStatus = IssuerVerificationStatus.Expired;
                else overallStatus = IssuerVerificationStatus.Unverified;

                return new IssuerVerificationResponse
                {
                    Success = true,
                    IssuerAddress = issuerAddress,
                    OverallStatus = overallStatus,
                    KybStatus = profile.KybStatus,
                    MicaLicenseStatus = profile.MicaLicenseStatus,
                    ProfileStatus = profile.Status,
                    IsProfileComplete = missingFields.Count == 0,
                    MissingFields = missingFields,
                    VerificationScore = score
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting issuer verification for {IssuerAddress}", issuerAddress);
                return new IssuerVerificationResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to retrieve issuer verification: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<IssuerAssetsResponse> ListIssuerAssetsAsync(string issuerAddress, ListIssuerAssetsRequest request)
        {
            try
            {
                var assetIds = await _complianceRepository.ListIssuerAssetsAsync(issuerAddress, request);
                var totalCount = await _complianceRepository.GetIssuerAssetCountAsync(issuerAddress, request);

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                return new IssuerAssetsResponse
                {
                    Success = true,
                    IssuerAddress = issuerAddress,
                    AssetIds = assetIds,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing assets for issuer {IssuerAddress}", issuerAddress);
                return new IssuerAssetsResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to list issuer assets: {ex.Message}"
                };
            }
        }

        // Phase 3: Blacklist Management

        /// <inheritdoc/>
        public async Task<BlacklistResponse> AddBlacklistEntryAsync(AddBlacklistEntryRequest request, string createdBy)
        {
            try
            {
                var entry = new BlacklistEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Address = request.Address,
                    AssetId = request.AssetId,
                    Reason = request.Reason,
                    Category = request.Category,
                    Network = request.Network,
                    Jurisdiction = request.Jurisdiction,
                    Source = request.Source,
                    ReferenceId = request.ReferenceId,
                    EffectiveDate = request.EffectiveDate ?? DateTime.UtcNow,
                    ExpirationDate = request.ExpirationDate,
                    Status = BlacklistStatus.Active,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    Notes = request.Notes
                };

                var success = await _complianceRepository.CreateBlacklistEntryAsync(entry);

                return new BlacklistResponse
                {
                    Success = success,
                    Entry = success ? entry : null,
                    ErrorMessage = success ? null : "Failed to create blacklist entry"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating blacklist entry for address {Address}", request.Address);
                return new BlacklistResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create blacklist entry: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<BlacklistCheckResponse> CheckBlacklistAsync(CheckBlacklistRequest request)
        {
            try
            {
                var entries = await _complianceRepository.CheckBlacklistAsync(
                    request.Address,
                    request.AssetId,
                    request.Network);

                var globalEntries = entries.Where(e => e.AssetId == null).ToList();
                var assetEntries = entries.Where(e => e.AssetId.HasValue).ToList();

                return new BlacklistCheckResponse
                {
                    Success = true,
                    Address = request.Address,
                    IsBlacklisted = entries.Any(),
                    Entries = entries,
                    GlobalBlacklist = globalEntries.Any(),
                    AssetSpecificBlacklist = assetEntries.Any()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking blacklist for address {Address}", request.Address);
                return new BlacklistCheckResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to check blacklist: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<BlacklistListResponse> ListBlacklistEntriesAsync(ListBlacklistEntriesRequest request)
        {
            try
            {
                var entries = await _complianceRepository.ListBlacklistEntriesAsync(request);
                var totalCount = await _complianceRepository.GetBlacklistEntryCountAsync(request);

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                return new BlacklistListResponse
                {
                    Success = true,
                    Entries = entries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing blacklist entries");
                return new BlacklistListResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to list blacklist entries: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<BlacklistResponse> DeleteBlacklistEntryAsync(string id)
        {
            try
            {
                var success = await _complianceRepository.DeleteBlacklistEntryAsync(id);

                return new BlacklistResponse
                {
                    Success = success,
                    ErrorMessage = success ? null : "Blacklist entry not found"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blacklist entry {Id}", id);
                return new BlacklistResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to delete blacklist entry: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TransferValidationResponse> ValidateTransferAsync(ValidateComplianceTransferRequest request)
        {
            try
            {
                var validations = new List<ValidationCheck>();
                var violations = new List<string>();
                var warnings = new List<string>();

                // Check sender and receiver whitelist in single call
                var whitelistResponse = await _whitelistService.ListEntriesAsync(new ListWhitelistRequest 
                { 
                    AssetId = request.AssetId,
                    Status = WhitelistStatus.Active
                });
                
                var whitelistEntries = whitelistResponse.Success ? whitelistResponse.Entries : new List<WhitelistEntry>();
                
                // Check sender whitelist
                var senderWhitelisted = whitelistEntries.Any(e => e.Address == request.FromAddress);
                validations.Add(new ValidationCheck
                {
                    Rule = "SenderWhitelisted",
                    Passed = senderWhitelisted,
                    Message = senderWhitelisted ? "Sender is whitelisted" : "Sender is not whitelisted"
                });
                if (!senderWhitelisted)
                {
                    violations.Add("Sender address is not whitelisted for this token");
                }

                // Check receiver whitelist
                var receiverWhitelisted = whitelistEntries.Any(e => e.Address == request.ToAddress);
                validations.Add(new ValidationCheck
                {
                    Rule = "ReceiverWhitelisted",
                    Passed = receiverWhitelisted,
                    Message = receiverWhitelisted ? "Receiver is whitelisted" : "Receiver is not whitelisted"
                });
                if (!receiverWhitelisted)
                {
                    violations.Add("Receiver address is not whitelisted for this token");
                }

                // Check blacklist for sender
                var senderBlacklist = await _complianceRepository.CheckBlacklistAsync(request.FromAddress, request.AssetId, request.Network);
                validations.Add(new ValidationCheck
                {
                    Rule = "SenderNotBlacklisted",
                    Passed = !senderBlacklist.Any(),
                    Message = !senderBlacklist.Any() ? "Sender is not blacklisted" : "Sender is blacklisted"
                });
                if (senderBlacklist.Any())
                {
                    violations.Add($"Sender address is blacklisted: {senderBlacklist.First().Reason}");
                }

                // Check blacklist for receiver
                var receiverBlacklist = await _complianceRepository.CheckBlacklistAsync(request.ToAddress, request.AssetId, request.Network);
                validations.Add(new ValidationCheck
                {
                    Rule = "ReceiverNotBlacklisted",
                    Passed = !receiverBlacklist.Any(),
                    Message = !receiverBlacklist.Any() ? "Receiver is not blacklisted" : "Receiver is blacklisted"
                });
                if (receiverBlacklist.Any())
                {
                    violations.Add($"Receiver address is blacklisted: {receiverBlacklist.First().Reason}");
                }

                // Check compliance metadata
                var metadata = await _complianceRepository.GetMetadataByAssetIdAsync(request.AssetId);
                if (metadata != null)
                {
                    bool hasRestrictions = !string.IsNullOrEmpty(metadata.TransferRestrictions);
                    validations.Add(new ValidationCheck
                    {
                        Rule = "TransferRestrictions",
                        Passed = true,
                        Message = hasRestrictions ? $"Transfer restrictions apply: {metadata.TransferRestrictions}" : "No transfer restrictions"
                    });

                    if (hasRestrictions)
                    {
                        warnings.Add(metadata.TransferRestrictions!);
                    }
                }

                bool canTransfer = violations.Count == 0;

                return new TransferValidationResponse
                {
                    Success = true,
                    IsValid = canTransfer,
                    CanTransfer = canTransfer,
                    Validations = validations,
                    Violations = violations,
                    Warnings = warnings,
                    Recommendations = canTransfer ? new List<string>() : new List<string> { "Please resolve all violations before proceeding with the transfer" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating transfer for asset {AssetId}", request.AssetId);
                return new TransferValidationResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to validate transfer: {ex.Message}"
                };
            }
        }

        // Phase 4: MICA Checklist and Health

        /// <inheritdoc/>
        public async Task<MicaComplianceChecklistResponse> GetMicaComplianceChecklistAsync(ulong assetId)
        {
            try
            {
                var metadata = await _complianceRepository.GetMetadataByAssetIdAsync(assetId);
                var whitelistResponse = await _whitelistService.ListEntriesAsync(new ListWhitelistRequest { AssetId = assetId });
                var whitelistEntries = whitelistResponse.Success ? whitelistResponse.Entries : new List<WhitelistEntry>();

                var requirements = new List<MicaRequirement>();

                // MICA Art. 35: Issuer identification
                requirements.Add(new MicaRequirement
                {
                    Id = "MICA-ART35",
                    Category = "Issuer Identification",
                    Description = "Issuer must be identified and verified",
                    IsMet = metadata != null && !string.IsNullOrEmpty(metadata.CreatedBy),
                    Priority = MicaRequirementPriority.Critical,
                    Evidence = metadata != null ? $"Token created by {metadata.CreatedBy}" : null,
                    MetDate = metadata?.CreatedAt,
                    Recommendations = metadata == null ? "Create compliance metadata with issuer information" : null
                });

                // MICA Art. 36: White paper
                requirements.Add(new MicaRequirement
                {
                    Id = "MICA-ART36",
                    Category = "White Paper",
                    Description = "White paper must be published",
                    IsMet = metadata != null && !string.IsNullOrEmpty(metadata.Notes),
                    Priority = MicaRequirementPriority.Critical,
                    Evidence = metadata?.Notes != null ? "White paper reference in compliance notes" : null,
                    Recommendations = "Add white paper URL or reference in compliance metadata notes"
                });

                // MICA Art. 41: Prudential safeguards
                requirements.Add(new MicaRequirement
                {
                    Id = "MICA-ART41",
                    Category = "Prudential Safeguards",
                    Description = "Prudential safeguards must be in place",
                    IsMet = metadata?.ComplianceStatus == ComplianceStatus.Compliant,
                    Priority = MicaRequirementPriority.High,
                    Evidence = metadata != null ? $"Compliance status: {metadata.ComplianceStatus}" : null,
                    MetDate = metadata?.LastComplianceReview,
                    Recommendations = "Set compliance status to Compliant after review"
                });

                // MICA Art. 45: Transfer restrictions
                requirements.Add(new MicaRequirement
                {
                    Id = "MICA-ART45",
                    Category = "Transfer Restrictions",
                    Description = "Transfer restrictions must be documented",
                    IsMet = whitelistEntries?.Any() == true,
                    Priority = MicaRequirementPriority.High,
                    Evidence = whitelistEntries?.Any() == true ? $"{whitelistEntries.Count} addresses whitelisted" : null,
                    Recommendations = "Implement whitelist controls for transfer restrictions"
                });

                // MICA Art. 59: AML/CTF
                requirements.Add(new MicaRequirement
                {
                    Id = "MICA-ART59",
                    Category = "AML/CTF",
                    Description = "AML/CTF procedures must be implemented",
                    IsMet = metadata?.VerificationStatus == VerificationStatus.Verified,
                    Priority = MicaRequirementPriority.Critical,
                    Evidence = metadata?.VerificationStatus == VerificationStatus.Verified ? $"KYC verification by {metadata.KycProvider}" : null,
                    MetDate = metadata?.KycVerificationDate,
                    Recommendations = "Implement KYC attestation workflow"
                });

                // MICA Art. 60: Record keeping
                requirements.Add(new MicaRequirement
                {
                    Id = "MICA-ART60",
                    Category = "Record Keeping",
                    Description = "Records must be maintained (7 years minimum)",
                    IsMet = true, // Audit logs are always enabled
                    Priority = MicaRequirementPriority.High,
                    Evidence = "Audit logs enabled with 7-year retention",
                    MetDate = DateTime.UtcNow
                });

                // Calculate compliance percentage
                int metCount = requirements.Count(r => r.IsMet);
                int totalCount = requirements.Count;
                int percentage = (int)Math.Round((double)metCount / totalCount * 100);

                // Determine overall status
                MicaComplianceStatus overallStatus;
                if (percentage == 100) overallStatus = MicaComplianceStatus.FullyCompliant;
                else if (percentage >= 80) overallStatus = MicaComplianceStatus.NearlyCompliant;
                else if (percentage >= 40) overallStatus = MicaComplianceStatus.InProgress;
                else if (percentage > 0) overallStatus = MicaComplianceStatus.InProgress;
                else overallStatus = MicaComplianceStatus.NotStarted;

                // Find next critical unmet requirement
                var nextUnmet = requirements
                    .Where(r => !r.IsMet && r.Priority == MicaRequirementPriority.Critical)
                    .FirstOrDefault();

                var checklist = new MicaComplianceChecklist
                {
                    AssetId = assetId,
                    OverallStatus = overallStatus,
                    CompliancePercentage = percentage,
                    Requirements = requirements,
                    GeneratedAt = DateTime.UtcNow,
                    NextAction = nextUnmet != null ? nextUnmet.Recommendations : null,
                    EstimatedCompletionDate = overallStatus == MicaComplianceStatus.FullyCompliant ? null : DateTime.UtcNow.AddDays(30)
                };

                return new MicaComplianceChecklistResponse
                {
                    Success = true,
                    Checklist = checklist
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting MICA checklist for asset {AssetId}", assetId);
                return new MicaComplianceChecklistResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to retrieve MICA checklist: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceHealthResponse> GetComplianceHealthAsync(string issuerAddress, string? network)
        {
            try
            {
                // Get all assets for issuer
                var assetsRequest = new ListIssuerAssetsRequest
                {
                    Network = network,
                    Page = 1,
                    PageSize = 100
                };
                var assets = await _complianceRepository.ListIssuerAssetsAsync(issuerAddress, assetsRequest);
                var totalTokens = assets.Count;

                // Get metadata for each asset
                var metadataList = new List<ComplianceMetadata>();
                foreach (var assetId in assets)
                {
                    var metadata = await _complianceRepository.GetMetadataByAssetIdAsync(assetId);
                    if (metadata != null)
                    {
                        metadataList.Add(metadata);
                    }
                }

                // Calculate metrics
                int compliantTokens = metadataList.Count(m => m.ComplianceStatus == ComplianceStatus.Compliant);
                int underReviewTokens = metadataList.Count(m => m.ComplianceStatus == ComplianceStatus.UnderReview);
                int nonCompliantTokens = metadataList.Count(m => m.ComplianceStatus == ComplianceStatus.NonCompliant);

                // Count tokens with features
                int tokensWithWhitelisting = 0;
                int micaReadyTokens = 0;

                // Optimize: batch fetch whitelist data for all assets at once to avoid N+1 queries
                var assetWhitelistMap = new Dictionary<ulong, bool>();
                foreach (var metadata in metadataList)
                {
                    // Quick optimization: For now, make individual calls but cache results
                    // TODO: In production, implement batch whitelist lookup endpoint
                    if (!assetWhitelistMap.ContainsKey(metadata.AssetId))
                    {
                        var whitelistResponse = await _whitelistService.ListEntriesAsync(new ListWhitelistRequest { AssetId = metadata.AssetId });
                        var hasWhitelist = whitelistResponse.Success && whitelistResponse.Entries.Any();
                        assetWhitelistMap[metadata.AssetId] = hasWhitelist;
                        
                        if (hasWhitelist)
                        {
                            tokensWithWhitelisting++;
                        }
                    }

                    // Check MICA readiness
                    bool isMicaReady = metadata.ComplianceStatus == ComplianceStatus.Compliant &&
                                      !string.IsNullOrEmpty(metadata.RegulatoryFramework) &&
                                      !string.IsNullOrEmpty(metadata.Jurisdiction);
                    if (isMicaReady)
                    {
                        micaReadyTokens++;
                    }
                }

                // Check issuer verification
                var issuerProfile = await _complianceRepository.GetIssuerProfileAsync(issuerAddress);
                bool issuerVerified = issuerProfile?.KybStatus == VerificationStatus.Verified;

                // Generate alerts
                var alerts = new List<ComplianceAlert>();

                // Check for overdue reviews
                var overdueReviews = metadataList
                    .Where(m => m.NextComplianceReview.HasValue && m.NextComplianceReview.Value < DateTime.UtcNow.AddDays(30))
                    .ToList();
                if (overdueReviews.Any())
                {
                    alerts.Add(new ComplianceAlert
                    {
                        Severity = "Warning",
                        Message = $"{overdueReviews.Count} tokens have compliance review due within 30 days",
                        AffectedAssetIds = overdueReviews.Select(m => m.AssetId).ToList()
                    });
                }

                // Check for non-compliant tokens
                if (nonCompliantTokens > 0)
                {
                    alerts.Add(new ComplianceAlert
                    {
                        Severity = "Error",
                        Message = $"{nonCompliantTokens} tokens are non-compliant",
                        AffectedAssetIds = metadataList.Where(m => m.ComplianceStatus == ComplianceStatus.NonCompliant)
                            .Select(m => m.AssetId).ToList()
                    });
                }

                // Generate recommendations
                var recommendations = new List<string>();
                if (!issuerVerified)
                {
                    recommendations.Add("Complete KYB verification for issuer profile");
                }
                if (micaReadyTokens < totalTokens)
                {
                    recommendations.Add($"Complete MICA compliance checklist for {totalTokens - micaReadyTokens} tokens");
                }
                if (tokensWithWhitelisting < totalTokens)
                {
                    recommendations.Add($"Enable whitelist controls for {totalTokens - tokensWithWhitelisting} tokens");
                }

                // Calculate overall health score
                int healthScore = 0;
                if (totalTokens > 0)
                {
                    healthScore = (int)Math.Round(
                        (compliantTokens * 30.0 / totalTokens) +
                        (micaReadyTokens * 30.0 / totalTokens) +
                        (tokensWithWhitelisting * 20.0 / totalTokens) +
                        (issuerVerified ? 20 : 0)
                    );
                }

                return new ComplianceHealthResponse
                {
                    Success = true,
                    OverallHealthScore = healthScore,
                    TotalTokens = totalTokens,
                    CompliantTokens = compliantTokens,
                    UnderReviewTokens = underReviewTokens,
                    NonCompliantTokens = nonCompliantTokens,
                    MicaReadyTokens = micaReadyTokens,
                    TokensWithWhitelisting = tokensWithWhitelisting,
                    TokensWithAuditTrail = totalTokens, // All tokens have audit trail
                    IssuerVerified = issuerVerified,
                    Alerts = alerts,
                    Recommendations = recommendations
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance health for issuer {IssuerAddress}", issuerAddress);
                return new ComplianceHealthResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to retrieve compliance health: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ComplianceEvidenceBundleResponse> GenerateComplianceEvidenceBundleAsync(
            GenerateComplianceEvidenceBundleRequest request,
            string requestedBy)
        {
            try
            {
                _logger.LogInformation("Generating compliance evidence bundle for asset {AssetId} by {RequestedBy}", 
                    request.AssetId, requestedBy);

                // Generate unique bundle ID
                var bundleId = Guid.NewGuid().ToString("N");
                var generatedAt = DateTime.UtcNow;

                // Collect all evidence data
                var evidenceData = new Dictionary<string, (string content, string description, string format)>();
                var bundleSummary = new BundleSummary();
                DateTime? oldestDate = null;
                DateTime? newestDate = null;
                var includedCategories = new HashSet<string>();

                // 1. Collect Token Metadata
                if (request.IncludeTokenMetadata)
                {
                    var metadataResponse = await GetMetadataAsync(request.AssetId);
                    if (metadataResponse.Success && metadataResponse.Metadata != null)
                    {
                        var complianceMetadata = metadataResponse.Metadata;
                        var metadataJson = System.Text.Json.JsonSerializer.Serialize(complianceMetadata, 
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        evidenceData.Add("metadata/compliance_metadata.json", 
                            (metadataJson, "Token compliance metadata including KYC status, jurisdiction, and regulatory framework", "JSON"));
                        bundleSummary.HasComplianceMetadata = true;
                    }
                }

                // 2. Collect Whitelist History
                if (request.IncludeWhitelistHistory)
                {
                    // Get current whitelist entries
                    var whitelistRequest = new ListWhitelistRequest
                    {
                        AssetId = request.AssetId,
                        Page = 1,
                        PageSize = 10000
                    };
                    var whitelistResponse = await _whitelistService.ListEntriesAsync(whitelistRequest);
                    if (whitelistResponse.Success)
                    {
                        var whitelistJson = System.Text.Json.JsonSerializer.Serialize(whitelistResponse.Entries,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        evidenceData.Add("whitelist/current_entries.json",
                            (whitelistJson, "Current whitelist entries for the token", "JSON"));
                        bundleSummary.WhitelistEntriesCount = whitelistResponse.Entries.Count;
                    }

                    // Get whitelist audit log
                    var whitelistAuditRequest = new GetWhitelistAuditLogRequest
                    {
                        AssetId = request.AssetId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        Page = 1,
                        PageSize = 10000
                    };
                    var whitelistAuditResponse = await _whitelistService.GetAuditLogAsync(whitelistAuditRequest);
                    if (whitelistAuditResponse.Success)
                    {
                        var auditJson = System.Text.Json.JsonSerializer.Serialize(whitelistAuditResponse.Entries,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        evidenceData.Add("whitelist/audit_log.json",
                            (auditJson, "Complete audit log of whitelist operations (add/remove)", "JSON"));
                        bundleSummary.WhitelistRuleAuditCount = whitelistAuditResponse.Entries.Count;

                        // Update date ranges
                        foreach (var entry in whitelistAuditResponse.Entries)
                        {
                            if (oldestDate == null || entry.PerformedAt < oldestDate) oldestDate = entry.PerformedAt;
                            if (newestDate == null || entry.PerformedAt > newestDate) newestDate = entry.PerformedAt;
                        }
                    }
                }

                // 3. Collect Compliance Audit Logs
                if (request.IncludeAuditLogs)
                {
                    var auditRequest = new GetComplianceAuditLogRequest
                    {
                        AssetId = request.AssetId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        Page = 1,
                        PageSize = 10000
                    };
                    var auditResponse = await GetAuditLogAsync(auditRequest);
                    if (auditResponse.Success)
                    {
                        var auditJson = System.Text.Json.JsonSerializer.Serialize(auditResponse.Entries,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        evidenceData.Add("audit_logs/compliance_operations.json",
                            (auditJson, "Audit log of compliance metadata operations", "JSON"));
                        bundleSummary.AuditLogCount = auditResponse.Entries.Count;

                        // Update date ranges and categories
                        foreach (var entry in auditResponse.Entries)
                        {
                            if (oldestDate == null || entry.PerformedAt < oldestDate) oldestDate = entry.PerformedAt;
                            if (newestDate == null || entry.PerformedAt > newestDate) newestDate = entry.PerformedAt;
                            includedCategories.Add(entry.ActionType.ToString());
                        }
                    }
                }

                // 4. Collect Transfer Validation Records (if available through audit logs)
                // Note: Only collect if NOT already included in whitelist history to avoid duplicates
                if (request.IncludeTransferApprovals && !request.IncludeWhitelistHistory)
                {
                    // Transfer validations are logged in whitelist audit log with action type ValidateTransfer
                    var transferAuditRequest = new GetWhitelistAuditLogRequest
                    {
                        AssetId = request.AssetId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        Page = 1,
                        PageSize = 10000
                    };
                    var transferAuditResponse = await _whitelistService.GetAuditLogAsync(transferAuditRequest);
                    if (transferAuditResponse.Success)
                    {
                        // Filter to only transfer validation entries
                        var transferValidations = transferAuditResponse.Entries
                            .Where(e => e.ActionType == BiatecTokensApi.Models.Whitelist.WhitelistActionType.TransferValidation)
                            .ToList();
                        
                        if (transferValidations.Any())
                        {
                            var transferJson = System.Text.Json.JsonSerializer.Serialize(transferValidations,
                                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                            evidenceData.Add("audit_logs/transfer_validations.json",
                                (transferJson, "Audit log of transfer validation operations", "JSON"));
                            bundleSummary.TransferValidationCount = transferValidations.Count;

                            foreach (var entry in transferValidations)
                            {
                                if (oldestDate == null || entry.PerformedAt < oldestDate) oldestDate = entry.PerformedAt;
                                if (newestDate == null || entry.PerformedAt > newestDate) newestDate = entry.PerformedAt;
                            }
                            includedCategories.Add("TransferValidation");
                        }
                    }
                }
                else if (request.IncludeTransferApprovals && request.IncludeWhitelistHistory)
                {
                    // If whitelist history is included, transfer validations are already in whitelist/audit_log.json
                    // Just count them for the summary
                    var transferAuditRequest = new GetWhitelistAuditLogRequest
                    {
                        AssetId = request.AssetId,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        Page = 1,
                        PageSize = 10000
                    };
                    var transferAuditResponse = await _whitelistService.GetAuditLogAsync(transferAuditRequest);
                    if (transferAuditResponse.Success)
                    {
                        var transferValidationCount = transferAuditResponse.Entries
                            .Count(e => e.ActionType == BiatecTokensApi.Models.Whitelist.WhitelistActionType.TransferValidation);
                        bundleSummary.TransferValidationCount = transferValidationCount;
                        if (transferValidationCount > 0)
                        {
                            includedCategories.Add("TransferValidation");
                        }
                    }
                }

                // 5. Collect Policy Metadata
                if (request.IncludePolicyMetadata)
                {
                    var retentionPolicy = new
                    {
                        Framework = "MICA 2024",
                        MinimumRetentionPeriodYears = 7,
                        DataImmutability = "All audit entries are append-only and cannot be modified or deleted",
                        Scope = "Whitelist, blacklist, compliance metadata, and transfer validation events",
                        NetworkSupport = "All supported networks including VOI and Aramid mainnets"
                    };
                    var policyJson = System.Text.Json.JsonSerializer.Serialize(retentionPolicy,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    evidenceData.Add("policy/retention_policy.json",
                        (policyJson, "7-year MICA retention policy and data governance", "JSON"));
                }

                // Get network from metadata
                var metadataForNetwork = await GetMetadataAsync(request.AssetId);
                var network = metadataForNetwork.Metadata?.Network;

                // Update summary
                bundleSummary.OldestRecordDate = oldestDate;
                bundleSummary.NewestRecordDate = newestDate;
                bundleSummary.HasTokenMetadata = request.IncludeTokenMetadata;
                bundleSummary.IncludedCategories = includedCategories.ToList();

                // Create manifest with checksums
                var files = new List<BundleFile>();
                using var memoryStream = new System.IO.MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var (path, (content, description, format)) in evidenceData)
                    {
                        var entry = archive.CreateEntry(path);
                        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                        
                        // Write content to entry and explicitly dispose the stream
                        using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(bytes.AsMemory());
                        } // Stream is closed here before next iteration

                        // Calculate SHA256 checksum using HashData (more robust than Create())
                        var hash = SHA256.HashData(bytes);
                        var checksum = BitConverter.ToString(hash).Replace("-", "").ToLower();

                        files.Add(new BundleFile
                        {
                            Path = path,
                            Description = description,
                            Sha256 = checksum,
                            SizeBytes = bytes.Length,
                            Format = format
                        });
                    }

                    // Create manifest (without bundle checksum initially)
                    var manifest = new ComplianceEvidenceBundleMetadata
                    {
                        BundleId = bundleId,
                        AssetId = request.AssetId,
                        GeneratedAt = generatedAt,
                        GeneratedBy = requestedBy,
                        FromDate = request.FromDate,
                        ToDate = request.ToDate,
                        Network = network,
                        Files = files,
                        Summary = bundleSummary,
                        ComplianceFramework = "MICA 2024",
                        RetentionPeriodYears = 7,
                        BundleSha256 = "pending" // Will be calculated after ZIP is complete
                    };

                    var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    var manifestEntry = archive.CreateEntry("manifest.json");
                    var manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);
                    using (var manifestStream = manifestEntry.Open())
                    {
                        await manifestStream.WriteAsync(manifestBytes.AsMemory());
                    } // Stream is closed here

                    // Add README
                    var readme = GenerateReadme(manifest);
                    var readmeEntry = archive.CreateEntry("README.txt");
                    var readmeBytes = System.Text.Encoding.UTF8.GetBytes(readme);
                    using (var readmeStream = readmeEntry.Open())
                    {
                        await readmeStream.WriteAsync(readmeBytes.AsMemory());
                    } // Stream is closed here
                }

                // Calculate bundle checksum using HashData (more robust than Create())
                memoryStream.Position = 0;
                var bundleBytes = memoryStream.ToArray();
                var bundleHash = SHA256.HashData(bundleBytes);
                var bundleChecksum = BitConverter.ToString(bundleHash).Replace("-", "").ToLower();

                // Update metadata with bundle checksum
                var metadata = new ComplianceEvidenceBundleMetadata
                {
                    BundleId = bundleId,
                    AssetId = request.AssetId,
                    GeneratedAt = generatedAt,
                    GeneratedBy = requestedBy,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Network = network,
                    Files = files,
                    Summary = bundleSummary,
                    ComplianceFramework = "MICA 2024",
                    RetentionPeriodYears = 7,
                    BundleSha256 = bundleChecksum
                };

                // Log the export for audit trail
                await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                {
                    AssetId = request.AssetId,
                    Network = network,
                    ActionType = ComplianceActionType.Export,
                    PerformedBy = requestedBy,
                    Success = true
                });

                // Emit metering event for compliance export
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Network = network,
                    AssetId = request.AssetId,
                    OperationType = MeteringOperationType.Export,
                    Category = MeteringCategory.Compliance,
                    PerformedBy = requestedBy,
                    ItemCount = files.Count,
                    Metadata = new Dictionary<string, string>
                    {
                        { "bundle_id", bundleId },
                        { "file_count", files.Count.ToString() },
                        { "bundle_size_bytes", bundleBytes.Length.ToString() }
                    }
                });

                var fileName = $"compliance-evidence-{request.AssetId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";

                _logger.LogInformation("Successfully generated compliance evidence bundle {BundleId} for asset {AssetId}", 
                    bundleId, request.AssetId);

                return new ComplianceEvidenceBundleResponse
                {
                    Success = true,
                    BundleMetadata = metadata,
                    ZipContent = bundleBytes,
                    FileName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance evidence bundle for asset {AssetId}", request.AssetId);
                
                // Log failed export attempt
                try
                {
                    await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
                    {
                        AssetId = request.AssetId,
                        ActionType = ComplianceActionType.Export,
                        PerformedBy = requestedBy,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
                catch
                {
                    // Ignore audit log errors
                }

                return new ComplianceEvidenceBundleResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to generate compliance evidence bundle: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Generates a README file for the compliance evidence bundle
        /// </summary>
        private string GenerateReadme(ComplianceEvidenceBundleMetadata metadata)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("COMPLIANCE EVIDENCE BUNDLE");
            sb.AppendLine("==========================");
            sb.AppendLine();
            sb.AppendLine($"Bundle ID: {metadata.BundleId}");
            sb.AppendLine($"Asset ID: {metadata.AssetId}");
            sb.AppendLine($"Generated: {metadata.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Generated By: {metadata.GeneratedBy}");
            sb.AppendLine($"Network: {metadata.Network ?? "N/A"}");
            sb.AppendLine($"Compliance Framework: {metadata.ComplianceFramework}");
            sb.AppendLine($"Retention Period: {metadata.RetentionPeriodYears} years");
            sb.AppendLine();
            
            if (metadata.FromDate.HasValue || metadata.ToDate.HasValue)
            {
                sb.AppendLine("Date Range:");
                if (metadata.FromDate.HasValue)
                    sb.AppendLine($"  From: {metadata.FromDate.Value:yyyy-MM-dd}");
                if (metadata.ToDate.HasValue)
                    sb.AppendLine($"  To: {metadata.ToDate.Value:yyyy-MM-dd}");
                sb.AppendLine();
            }

            sb.AppendLine("BUNDLE CONTENTS:");
            sb.AppendLine("----------------");
            foreach (var file in metadata.Files)
            {
                sb.AppendLine($"- {file.Path}");
                sb.AppendLine($"  Description: {file.Description}");
                sb.AppendLine($"  Format: {file.Format}");
                sb.AppendLine($"  Size: {file.SizeBytes:N0} bytes");
                sb.AppendLine($"  SHA256: {file.Sha256}");
                sb.AppendLine();
            }

            sb.AppendLine("SUMMARY:");
            sb.AppendLine("--------");
            sb.AppendLine($"Audit Log Entries: {metadata.Summary.AuditLogCount}");
            sb.AppendLine($"Whitelist Entries: {metadata.Summary.WhitelistEntriesCount}");
            sb.AppendLine($"Whitelist Audit Records: {metadata.Summary.WhitelistRuleAuditCount}");
            sb.AppendLine($"Transfer Validations: {metadata.Summary.TransferValidationCount}");
            if (metadata.Summary.OldestRecordDate.HasValue)
                sb.AppendLine($"Oldest Record: {metadata.Summary.OldestRecordDate.Value:yyyy-MM-dd HH:mm:ss} UTC");
            if (metadata.Summary.NewestRecordDate.HasValue)
                sb.AppendLine($"Newest Record: {metadata.Summary.NewestRecordDate.Value:yyyy-MM-dd HH:mm:ss} UTC");
            if (metadata.Summary.IncludedCategories.Any())
                sb.AppendLine($"Event Categories: {string.Join(", ", metadata.Summary.IncludedCategories)}");
            sb.AppendLine();

            sb.AppendLine("VERIFICATION:");
            sb.AppendLine("-------------");
            sb.AppendLine($"Bundle SHA256 Checksum: {metadata.BundleSha256}");
            sb.AppendLine();
            sb.AppendLine("To verify the integrity of individual files, compare the SHA256 checksums");
            sb.AppendLine("listed above with those in the manifest.json file.");
            sb.AppendLine();
            sb.AppendLine("AUDIT TRAIL:");
            sb.AppendLine("------------");
            sb.AppendLine("This bundle was generated for MICA/RWA compliance audit purposes.");
            sb.AppendLine("All data is immutable and sourced from append-only audit logs.");
            sb.AppendLine($"Retention period: {metadata.RetentionPeriodYears} years minimum as per MICA requirements.");
            sb.AppendLine();
            sb.AppendLine("For questions or verification, please contact the compliance team.");

            return sb.ToString();
        }
    }
}

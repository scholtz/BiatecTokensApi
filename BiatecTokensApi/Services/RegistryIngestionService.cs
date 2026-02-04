using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Diagnostics;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for ingesting token data from internal and external sources
    /// </summary>
    /// <remarks>
    /// Handles data normalization, validation, and idempotent processing of token registry data.
    /// Designed to be run on a schedule or triggered manually via API.
    /// </remarks>
    public class RegistryIngestionService : IRegistryIngestionService
    {
        private readonly ITokenRegistryRepository _registryRepository;
        private readonly ITokenRegistryService _registryService;
        private readonly ILogger<RegistryIngestionService> _logger;
        private readonly TokenIssuanceRepository _tokenIssuanceRepository;
        private readonly ComplianceRepository _complianceRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryIngestionService"/> class
        /// </summary>
        public RegistryIngestionService(
            ITokenRegistryRepository registryRepository,
            ITokenRegistryService registryService,
            ILogger<RegistryIngestionService> logger,
            TokenIssuanceRepository tokenIssuanceRepository,
            ComplianceRepository complianceRepository)
        {
            _registryRepository = registryRepository;
            _registryService = registryService;
            _logger = logger;
            _tokenIssuanceRepository = tokenIssuanceRepository;
            _complianceRepository = complianceRepository;
        }

        /// <inheritdoc/>
        public async Task<IngestRegistryResponse> IngestAsync(IngestRegistryRequest request)
        {
            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            var response = new IngestRegistryResponse
            {
                StartedAt = startTime
            };

            try
            {
                _logger.LogInformation(
                    "Starting registry ingestion - Source: {Source}, Chain: {Chain}, Force: {Force}, Limit: {Limit}",
                    LoggingHelper.SanitizeLogInput(request.Source),
                    LoggingHelper.SanitizeLogInput(request.Chain ?? "all"),
                    LoggingHelper.SanitizeLogInput(request.Force.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Limit?.ToString() ?? "unlimited")
                );

                // Ingest from requested sources
                if (request.Source.Equals("all", StringComparison.OrdinalIgnoreCase) || 
                    request.Source.Equals("internal", StringComparison.OrdinalIgnoreCase))
                {
                    var internalCount = await IngestInternalTokensAsync(request.Chain, request.Limit);
                    response.ProcessedCount += internalCount;
                    response.CreatedCount += internalCount; // Simplified for now
                }

                // Additional external sources would be added here
                // e.g., IngestVestigeTokensAsync, IngestCoinMarketCapTokensAsync, etc.

                response.Success = true;
                response.CompletedAt = DateTime.UtcNow;
                response.Duration = stopwatch.Elapsed;

                _logger.LogInformation(
                    "Registry ingestion completed - Processed: {ProcessedCount}, Created: {CreatedCount}, Updated: {UpdatedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}, Duration: {Duration}",
                    LoggingHelper.SanitizeLogInput(response.ProcessedCount.ToString()),
                    LoggingHelper.SanitizeLogInput(response.CreatedCount.ToString()),
                    LoggingHelper.SanitizeLogInput(response.UpdatedCount.ToString()),
                    LoggingHelper.SanitizeLogInput(response.SkippedCount.ToString()),
                    LoggingHelper.SanitizeLogInput(response.ErrorCount.ToString()),
                    LoggingHelper.SanitizeLogInput(response.Duration.ToString())
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registry ingestion");
                response.Success = false;
                response.ErrorCount++;
                response.Errors.Add("Ingestion failed with exception");
                response.CompletedAt = DateTime.UtcNow;
                response.Duration = stopwatch.Elapsed;
            }

            return response;
        }

        /// <inheritdoc/>
        public async Task<int> IngestInternalTokensAsync(string? chain = null, int? limit = null)
        {
            try
            {
                _logger.LogInformation(
                    "Ingesting internal tokens - Chain: {Chain}, Limit: {Limit}",
                    LoggingHelper.SanitizeLogInput(chain ?? "all"),
                    LoggingHelper.SanitizeLogInput(limit?.ToString() ?? "unlimited")
                );

                // Get all token issuance records
                var request = new GetTokenIssuanceAuditLogRequest
                {
                    Network = chain,
                    Success = true, // Only successful deployments
                    PageSize = limit ?? 1000 // Default limit to avoid loading too much data
                };
                
                var issuanceRecords = await _tokenIssuanceRepository.GetAuditLogAsync(request);
                
                if (limit.HasValue && issuanceRecords.Count > limit.Value)
                {
                    issuanceRecords = issuanceRecords.Take(limit.Value).ToList();
                }

                int ingestedCount = 0;

                foreach (var record in issuanceRecords)
                {
                    try
                    {
                        // Check if token already exists in registry
                        if (record.AssetId == null || string.IsNullOrWhiteSpace(record.Network))
                            continue;

                        var tokenIdentifier = record.AssetId.ToString();
                        var existing = await _registryRepository.GetTokenByIdentifierAsync(tokenIdentifier, record.Network);

                        // Only create if it doesn't exist (idempotent)
                        if (existing == null)
                        {
                            // Get compliance metadata if available
                            var complianceMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(record.AssetId.Value);

                            var upsertRequest = new UpsertTokenRegistryRequest
                            {
                                TokenIdentifier = tokenIdentifier,
                                Chain = NormalizeChainName(record.Network),
                                Name = record.TokenName ?? "Unknown",
                                Symbol = record.TokenSymbol ?? "???",
                                Decimals = record.Decimals ?? 0,
                                TotalSupply = record.TotalSupply,
                                SupportedStandards = DetermineStandards(record),
                                PrimaryStandard = DeterminePrimaryStandard(record),
                                Issuer = CreateIssuerIdentity(record, complianceMetadata),
                                Compliance = CreateComplianceScoring(complianceMetadata),
                                Readiness = CreateOperationalReadiness(record),
                                DataSource = "internal",
                                Tags = CreateTags(record),
                                DeployedAt = record.DeployedAt
                            };

                            var result = await _registryRepository.UpsertTokenAsync(upsertRequest, record.DeployedBy);
                            if (result.Success)
                            {
                                ingestedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error ingesting token from issuance record {AssetId}", 
                            LoggingHelper.SanitizeLogInput(record.AssetId?.ToString() ?? "unknown"));
                    }
                }

                _logger.LogInformation("Ingested {Count} internal tokens", LoggingHelper.SanitizeLogInput(ingestedCount.ToString()));
                return ingestedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting internal tokens");
                return 0;
            }
        }

        /// <inheritdoc/>
        public Task<TokenRegistryEntry?> NormalizeTokenDataAsync(object rawData, string source)
        {
            // This would be implemented for external sources like Vestige, CoinMarketCap, etc.
            // For now, return null as we only have internal source
            _logger.LogInformation("Normalize token data called for source: {Source}", LoggingHelper.SanitizeLogInput(source));
            return Task.FromResult<TokenRegistryEntry?>(null);
        }

        /// <inheritdoc/>
        public async Task<List<string>> ValidateAndLogAnomaliesAsync(TokenRegistryEntry entry)
        {
            var anomalies = new List<string>();

            // Validate entry
            var validationResult = await _registryService.ValidateTokenAsync(entry);

            // Log warnings as anomalies
            foreach (var warning in validationResult.Warnings)
            {
                anomalies.Add($"Warning: {warning}");
                _logger.LogWarning(
                    "Anomaly detected for token {Symbol} ({TokenIdentifier}): {Warning}",
                    LoggingHelper.SanitizeLogInput(entry.Symbol),
                    LoggingHelper.SanitizeLogInput(entry.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(warning)
                );
            }

            // Log errors as critical anomalies
            foreach (var error in validationResult.Errors)
            {
                anomalies.Add($"Error: {error}");
                _logger.LogError(
                    "Critical anomaly for token {Symbol} ({TokenIdentifier}): {Error}",
                    LoggingHelper.SanitizeLogInput(entry.Symbol),
                    LoggingHelper.SanitizeLogInput(entry.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(error)
                );
            }

            return anomalies;
        }

        private string NormalizeChainName(string network)
        {
            // Normalize network names to standard format
            return network.ToLowerInvariant() switch
            {
                "mainnet" or "algorand" or "algorand-mainnet" => "algorand-mainnet",
                "testnet" or "algorand-testnet" => "algorand-testnet",
                "voimain" or "voi-mainnet" => "voi-mainnet",
                "aramidmain" or "aramid-mainnet" => "aramid-mainnet",
                "ethereum" or "ethereum-mainnet" => "ethereum-mainnet",
                "base" or "base-mainnet" => "base-mainnet",
                "arbitrum" or "arbitrum-mainnet" => "arbitrum-mainnet",
                _ => network.ToLowerInvariant()
            };
        }

        private List<string> DetermineStandards(TokenIssuanceAuditLogEntry record)
        {
            var standards = new List<string>();

            // Determine based on token type and network
            if (record.Network?.Contains("algorand", StringComparison.OrdinalIgnoreCase) == true ||
                record.Network?.Contains("voi", StringComparison.OrdinalIgnoreCase) == true ||
                record.Network?.Contains("aramid", StringComparison.OrdinalIgnoreCase) == true)
            {
                standards.Add("ASA");

                // Check for ARC standards based on token type or other properties
                if (record.TokenType?.Contains("ARC3", StringComparison.OrdinalIgnoreCase) == true)
                    standards.Add("ARC3");
                if (record.TokenType?.Contains("ARC200", StringComparison.OrdinalIgnoreCase) == true)
                    standards.Add("ARC200");
                if (record.TokenType?.Contains("ARC1400", StringComparison.OrdinalIgnoreCase) == true)
                    standards.Add("ARC1400");
            }
            else if (record.Network?.Contains("ethereum", StringComparison.OrdinalIgnoreCase) == true ||
                     record.Network?.Contains("base", StringComparison.OrdinalIgnoreCase) == true ||
                     record.Network?.Contains("arbitrum", StringComparison.OrdinalIgnoreCase) == true)
            {
                standards.Add("ERC20");
            }

            return standards;
        }

        private string? DeterminePrimaryStandard(TokenIssuanceAuditLogEntry record)
        {
            var standards = DetermineStandards(record);
            return standards.Count > 0 ? standards[0] : null;
        }

        private IssuerIdentity? CreateIssuerIdentity(TokenIssuanceAuditLogEntry record, Models.Compliance.ComplianceMetadata? complianceMetadata)
        {
            return new IssuerIdentity
            {
                Address = record.DeployedBy,
                Name = complianceMetadata?.IssuerName,
                IsVerified = complianceMetadata?.VerificationStatus == Models.Compliance.VerificationStatus.Verified,
                VerificationProvider = complianceMetadata?.KycProvider,
                VerificationDate = complianceMetadata?.KycVerificationDate
            };
        }

        private ComplianceScoring CreateComplianceScoring(Models.Compliance.ComplianceMetadata? complianceMetadata)
        {
            if (complianceMetadata == null)
            {
                return new ComplianceScoring
                {
                    Status = ComplianceState.Unknown
                };
            }

            return new ComplianceScoring
            {
                Status = MapComplianceStatus(complianceMetadata.ComplianceStatus),
                StatusReason = complianceMetadata.Notes,
                RegulatoryFrameworks = !string.IsNullOrWhiteSpace(complianceMetadata.RegulatoryFramework) 
                    ? complianceMetadata.RegulatoryFramework.Split(',', StringSplitOptions.TrimEntries).ToList()
                    : new List<string>(),
                Jurisdictions = !string.IsNullOrWhiteSpace(complianceMetadata.Jurisdiction)
                    ? complianceMetadata.Jurisdiction.Split(',', StringSplitOptions.TrimEntries).ToList()
                    : new List<string>(),
                LastReviewDate = complianceMetadata.LastComplianceReview,
                NextReviewDate = complianceMetadata.NextComplianceReview,
                RequiresKyc = complianceMetadata.KycProvider != null,
                AccreditedInvestorsOnly = complianceMetadata.RequiresAccreditedInvestors,
                TransferRestrictions = !string.IsNullOrWhiteSpace(complianceMetadata.TransferRestrictions)
                    ? complianceMetadata.TransferRestrictions.Split(',', StringSplitOptions.TrimEntries).ToList()
                    : new List<string>(),
                MaxHolders = complianceMetadata.MaxHolders
            };
        }

        private ComplianceState MapComplianceStatus(Models.Compliance.ComplianceStatus status)
        {
            return status switch
            {
                Models.Compliance.ComplianceStatus.Compliant => ComplianceState.Compliant,
                Models.Compliance.ComplianceStatus.NonCompliant => ComplianceState.NonCompliant,
                Models.Compliance.ComplianceStatus.UnderReview => ComplianceState.Pending,
                Models.Compliance.ComplianceStatus.Suspended => ComplianceState.Suspended,
                Models.Compliance.ComplianceStatus.Exempt => ComplianceState.Exempt,
                _ => ComplianceState.Unknown
            };
        }

        private OperationalReadiness CreateOperationalReadiness(TokenIssuanceAuditLogEntry record)
        {
            return new OperationalReadiness
            {
                IsContractVerified = false, // Would need to check block explorer
                IsAudited = false, // Would need audit information
                HasValidMetadata = !string.IsNullOrWhiteSpace(record.TokenName),
                HasLiquidity = false, // Would need DEX integration
                IsPausable = record.IsPausable ?? false,
                IsUpgradeable = false, // Would need to check smart contract
                HasMultisigControl = false, // Would need to check on-chain data
                SecurityFeatures = new List<string>()
            };
        }

        private List<string> CreateTags(TokenIssuanceAuditLogEntry record)
        {
            var tags = new List<string>();

            if (record.Network != null)
            {
                tags.Add(record.Network.ToLowerInvariant());
            }

            if (record.TokenType != null)
            {
                tags.Add(record.TokenType.ToLowerInvariant());
            }

            return tags;
        }
    }
}

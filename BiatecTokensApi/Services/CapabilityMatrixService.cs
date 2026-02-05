using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing compliance capability matrix
    /// </summary>
    public class CapabilityMatrixService : ICapabilityMatrixService
    {
        private readonly ILogger<CapabilityMatrixService> _logger;
        private readonly CapabilityMatrixConfig _config;
        private CapabilityMatrix? _cachedMatrix;
        private DateTime _lastLoadTime;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilityMatrixService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="config">The capability matrix configuration</param>
        public CapabilityMatrixService(
            ILogger<CapabilityMatrixService> logger,
            IOptions<CapabilityMatrixConfig> config)
        {
            _logger = logger;
            _config = config.Value;
            _lastLoadTime = DateTime.MinValue;

            // Load configuration at startup synchronously to avoid constructor async issues
            LoadConfigurationSync();
        }

        /// <summary>
        /// Gets the complete capability matrix with optional filtering
        /// </summary>
        /// <param name="request">Optional filters for jurisdiction, wallet type, token standard, and KYC tier</param>
        /// <returns>Response with the capability matrix data</returns>
        public async Task<CapabilityMatrixResponse> GetCapabilityMatrixAsync(GetCapabilityMatrixRequest? request = null)
        {
            try
            {
                // Load or use cached matrix
                var matrix = await GetMatrixAsync();

                if (matrix == null)
                {
                    _logger.LogError("Failed to load capability matrix configuration");
                    return new CapabilityMatrixResponse
                    {
                        Success = false,
                        ErrorMessage = "Capability matrix configuration not available"
                    };
                }

                // Apply filters if provided
                if (request != null && HasFilters(request))
                {
                    var filtered = ApplyFilters(matrix, request);
                    if (filtered == null)
                    {
                        return new CapabilityMatrixResponse
                        {
                            Success = false,
                            ErrorMessage = "No matching capabilities found for the specified filters",
                            ErrorDetails = new CapabilityErrorDetails
                            {
                                Error = "no_matching_capabilities",
                                Jurisdiction = request.Jurisdiction,
                                WalletType = request.WalletType,
                                TokenStandard = request.TokenStandard,
                                KycTier = request.KycTier
                            }
                        };
                    }
                    matrix = filtered;
                }

                // Log capability query for audit
                _logger.LogInformation("Capability matrix queried: Jurisdiction={Jurisdiction}, WalletType={WalletType}, TokenStandard={TokenStandard}, KycTier={KycTier}",
                    LoggingHelper.SanitizeLogInput(request?.Jurisdiction),
                    LoggingHelper.SanitizeLogInput(request?.WalletType),
                    LoggingHelper.SanitizeLogInput(request?.TokenStandard),
                    LoggingHelper.SanitizeLogInput(request?.KycTier));

                return new CapabilityMatrixResponse
                {
                    Success = true,
                    Data = matrix
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving capability matrix");
                return new CapabilityMatrixResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Checks if a specific action is allowed based on capability rules
        /// </summary>
        /// <param name="request">The capability check request</param>
        /// <returns>Response indicating if the action is allowed and required checks</returns>
        public async Task<CapabilityCheckResponse> CheckCapabilityAsync(CapabilityCheckRequest request)
        {
            try
            {
                var matrix = await GetMatrixAsync();

                if (matrix == null)
                {
                    _logger.LogError("Capability matrix not available for enforcement check");
                    return new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = "Capability matrix not available"
                    };
                }

                // Find matching jurisdiction
                var jurisdiction = matrix.Jurisdictions.FirstOrDefault(j =>
                    string.Equals(j.Code, request.Jurisdiction, StringComparison.OrdinalIgnoreCase));

                if (jurisdiction == null)
                {
                    var errorDetails = new CapabilityErrorDetails
                    {
                        Error = "capability_not_allowed",
                        Jurisdiction = request.Jurisdiction,
                        WalletType = request.WalletType,
                        TokenStandard = request.TokenStandard,
                        KycTier = request.KycTier,
                        Action = request.Action,
                        RuleId = "jurisdiction_not_found"
                    };

                    _logger.LogWarning("Capability check denied: event=capability_check, decision=deny, ruleId={RuleId}, context={Context}",
                        LoggingHelper.SanitizeLogInput(errorDetails.RuleId),
                        LoggingHelper.SanitizeLogInput($"jurisdiction={request.Jurisdiction}"));

                    return new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = $"Jurisdiction '{request.Jurisdiction}' not found in capability matrix",
                        ErrorDetails = errorDetails
                    };
                }

                // Find matching wallet type
                var walletType = jurisdiction.WalletTypes.FirstOrDefault(w =>
                    string.Equals(w.Type, request.WalletType, StringComparison.OrdinalIgnoreCase));

                if (walletType == null)
                {
                    var errorDetails = new CapabilityErrorDetails
                    {
                        Error = "capability_not_allowed",
                        Jurisdiction = request.Jurisdiction,
                        WalletType = request.WalletType,
                        TokenStandard = request.TokenStandard,
                        KycTier = request.KycTier,
                        Action = request.Action,
                        RuleId = "wallet_type_not_supported"
                    };

                    _logger.LogWarning("Capability check denied: event=capability_check, decision=deny, ruleId={RuleId}, context={Context}",
                        LoggingHelper.SanitizeLogInput(errorDetails.RuleId),
                        LoggingHelper.SanitizeLogInput($"jurisdiction={request.Jurisdiction},walletType={request.WalletType}"));

                    return new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = $"Wallet type '{request.WalletType}' not supported in jurisdiction '{request.Jurisdiction}'",
                        ErrorDetails = errorDetails
                    };
                }

                // Find matching KYC tier
                var kycTier = walletType.KycTiers.FirstOrDefault(k =>
                    string.Equals(k.Tier, request.KycTier, StringComparison.OrdinalIgnoreCase));

                if (kycTier == null)
                {
                    var errorDetails = new CapabilityErrorDetails
                    {
                        Error = "capability_not_allowed",
                        Jurisdiction = request.Jurisdiction,
                        WalletType = request.WalletType,
                        TokenStandard = request.TokenStandard,
                        KycTier = request.KycTier,
                        Action = request.Action,
                        RuleId = "kyc_tier_not_supported"
                    };

                    _logger.LogWarning("Capability check denied: event=capability_check, decision=deny, ruleId={RuleId}, context={Context}",
                        LoggingHelper.SanitizeLogInput(errorDetails.RuleId),
                        LoggingHelper.SanitizeLogInput($"jurisdiction={request.Jurisdiction},walletType={request.WalletType},kycTier={request.KycTier}"));

                    return new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = $"KYC tier '{request.KycTier}' not supported for wallet type '{request.WalletType}' in jurisdiction '{request.Jurisdiction}'",
                        ErrorDetails = errorDetails
                    };
                }

                // Find matching token standard
                var tokenStandard = kycTier.TokenStandards.FirstOrDefault(t =>
                    string.Equals(t.Standard, request.TokenStandard, StringComparison.OrdinalIgnoreCase));

                if (tokenStandard == null)
                {
                    var errorDetails = new CapabilityErrorDetails
                    {
                        Error = "capability_not_allowed",
                        Jurisdiction = request.Jurisdiction,
                        WalletType = request.WalletType,
                        TokenStandard = request.TokenStandard,
                        KycTier = request.KycTier,
                        Action = request.Action,
                        RuleId = "token_standard_not_supported"
                    };

                    _logger.LogWarning("Capability check denied: event=capability_check, decision=deny, ruleId={RuleId}, context={Context}",
                        LoggingHelper.SanitizeLogInput(errorDetails.RuleId),
                        LoggingHelper.SanitizeLogInput($"jurisdiction={request.Jurisdiction},walletType={request.WalletType},kycTier={request.KycTier},tokenStandard={request.TokenStandard}"));

                    return new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = $"Token standard '{request.TokenStandard}' not supported for KYC tier '{request.KycTier}' in jurisdiction '{request.Jurisdiction}'",
                        ErrorDetails = errorDetails
                    };
                }

                // Check if action is allowed
                var actionAllowed = tokenStandard.Actions.Any(a =>
                    string.Equals(a, request.Action, StringComparison.OrdinalIgnoreCase));

                if (!actionAllowed)
                {
                    var errorDetails = new CapabilityErrorDetails
                    {
                        Error = "capability_not_allowed",
                        Jurisdiction = request.Jurisdiction,
                        WalletType = request.WalletType,
                        TokenStandard = request.TokenStandard,
                        KycTier = request.KycTier,
                        Action = request.Action,
                        RuleId = "action_not_allowed"
                    };

                    _logger.LogWarning("Capability check denied: event=capability_check, decision=deny, ruleId={RuleId}, context={Context}",
                        LoggingHelper.SanitizeLogInput(errorDetails.RuleId),
                        LoggingHelper.SanitizeLogInput($"jurisdiction={request.Jurisdiction},walletType={request.WalletType},kycTier={request.KycTier},tokenStandard={request.TokenStandard},action={request.Action}"));

                    return new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = $"Action '{request.Action}' not allowed for token standard '{request.TokenStandard}' with KYC tier '{request.KycTier}' in jurisdiction '{request.Jurisdiction}'",
                        ErrorDetails = errorDetails
                    };
                }

                // Action is allowed
                _logger.LogInformation("Capability check allowed: event=capability_check, decision=allow, context={Context}",
                    LoggingHelper.SanitizeLogInput($"jurisdiction={request.Jurisdiction},walletType={request.WalletType},kycTier={request.KycTier},tokenStandard={request.TokenStandard},action={request.Action}"));

                return new CapabilityCheckResponse
                {
                    Allowed = true,
                    RequiredChecks = tokenStandard.Checks,
                    Notes = tokenStandard.Notes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking capability");
                return new CapabilityCheckResponse
                {
                    Allowed = false,
                    Reason = $"Internal error during capability check: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Reloads the capability matrix configuration from file
        /// </summary>
        /// <returns>True if reload was successful</returns>
        public async Task<bool> ReloadConfigurationAsync()
        {
            try
            {
                var matrix = await LoadConfigurationAsync();
                return matrix != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading capability matrix configuration");
                return false;
            }
        }

        /// <summary>
        /// Gets the current configuration version
        /// </summary>
        /// <returns>The version string</returns>
        public string GetVersion()
        {
            return _cachedMatrix?.Version ?? _config.Version;
        }

        /// <summary>
        /// Gets the capability matrix, using cache if enabled
        /// </summary>
        private async Task<CapabilityMatrix?> GetMatrixAsync()
        {
            if (_config.EnableCaching && _cachedMatrix != null)
            {
                var cacheAge = DateTime.UtcNow - _lastLoadTime;
                if (cacheAge.TotalSeconds < _config.CacheDurationSeconds)
                {
                    return _cachedMatrix;
                }
            }

            return await LoadConfigurationAsync();
        }

        /// <summary>
        /// Loads the capability matrix configuration from file asynchronously
        /// </summary>
        private async Task<CapabilityMatrix?> LoadConfigurationAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, _config.ConfigFilePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogError("Capability matrix configuration file not found: {FilePath}", LoggingHelper.SanitizeLogInput(filePath));
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var matrix = JsonSerializer.Deserialize<CapabilityMatrix>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (matrix == null)
                {
                    _logger.LogError("Failed to deserialize capability matrix configuration");
                    return null;
                }

                // Validate the configuration
                if (!ValidateConfiguration(matrix))
                {
                    _logger.LogError("Capability matrix configuration validation failed");
                    return null;
                }

                // Set generated timestamp
                matrix.GeneratedAt = DateTime.UtcNow;

                // Cache the matrix
                _cachedMatrix = matrix;
                _lastLoadTime = DateTime.UtcNow;

                _logger.LogInformation("Capability matrix configuration loaded successfully: Version={Version}, Jurisdictions={Count}",
                    LoggingHelper.SanitizeLogInput(matrix.Version),
                    matrix.Jurisdictions.Count);

                return matrix;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading capability matrix configuration");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Loads the capability matrix configuration from file synchronously (for constructor)
        /// </summary>
        private void LoadConfigurationSync()
        {
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, _config.ConfigFilePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogError("Capability matrix configuration file not found: {FilePath}", LoggingHelper.SanitizeLogInput(filePath));
                    return;
                }

                var json = File.ReadAllText(filePath);
                var matrix = JsonSerializer.Deserialize<CapabilityMatrix>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (matrix == null)
                {
                    _logger.LogError("Failed to deserialize capability matrix configuration");
                    return;
                }

                // Validate the configuration
                if (!ValidateConfiguration(matrix))
                {
                    _logger.LogError("Capability matrix configuration validation failed");
                    return;
                }

                // Set generated timestamp
                matrix.GeneratedAt = DateTime.UtcNow;

                // Cache the matrix
                _cachedMatrix = matrix;
                _lastLoadTime = DateTime.UtcNow;

                _logger.LogInformation("Capability matrix configuration loaded successfully: Version={Version}, Jurisdictions={Count}",
                    LoggingHelper.SanitizeLogInput(matrix.Version),
                    matrix.Jurisdictions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading capability matrix configuration");
            }
        }

        /// <summary>
        /// Validates the capability matrix configuration
        /// </summary>
        private bool ValidateConfiguration(CapabilityMatrix matrix)
        {
            if (string.IsNullOrWhiteSpace(matrix.Version))
            {
                _logger.LogError("Capability matrix version is missing");
                return false;
            }

            if (matrix.Jurisdictions == null || matrix.Jurisdictions.Count == 0)
            {
                _logger.LogError("Capability matrix has no jurisdictions defined");
                return false;
            }

            foreach (var jurisdiction in matrix.Jurisdictions)
            {
                if (string.IsNullOrWhiteSpace(jurisdiction.Code))
                {
                    _logger.LogError("Jurisdiction code is missing");
                    return false;
                }

                if (jurisdiction.WalletTypes == null || jurisdiction.WalletTypes.Count == 0)
                {
                    _logger.LogWarning("Jurisdiction {Code} has no wallet types defined", LoggingHelper.SanitizeLogInput(jurisdiction.Code));
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the request has any filters
        /// </summary>
        private bool HasFilters(GetCapabilityMatrixRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.Jurisdiction) ||
                   !string.IsNullOrWhiteSpace(request.WalletType) ||
                   !string.IsNullOrWhiteSpace(request.TokenStandard) ||
                   !string.IsNullOrWhiteSpace(request.KycTier);
        }

        /// <summary>
        /// Applies filters to the capability matrix
        /// </summary>
        private CapabilityMatrix? ApplyFilters(CapabilityMatrix matrix, GetCapabilityMatrixRequest request)
        {
            var filtered = new CapabilityMatrix
            {
                Version = matrix.Version,
                GeneratedAt = matrix.GeneratedAt,
                Jurisdictions = new List<JurisdictionCapability>()
            };

            foreach (var jurisdiction in matrix.Jurisdictions)
            {
                // Filter by jurisdiction
                if (!string.IsNullOrWhiteSpace(request.Jurisdiction) &&
                    !string.Equals(jurisdiction.Code, request.Jurisdiction, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var filteredJurisdiction = new JurisdictionCapability
                {
                    Code = jurisdiction.Code,
                    Name = jurisdiction.Name,
                    WalletTypes = new List<WalletTypeCapability>()
                };

                foreach (var walletType in jurisdiction.WalletTypes)
                {
                    // Filter by wallet type
                    if (!string.IsNullOrWhiteSpace(request.WalletType) &&
                        !string.Equals(walletType.Type, request.WalletType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var filteredWalletType = new WalletTypeCapability
                    {
                        Type = walletType.Type,
                        Description = walletType.Description,
                        KycTiers = new List<KycTierCapability>()
                    };

                    foreach (var kycTier in walletType.KycTiers)
                    {
                        // Filter by KYC tier
                        if (!string.IsNullOrWhiteSpace(request.KycTier) &&
                            !string.Equals(kycTier.Tier, request.KycTier, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var filteredKycTier = new KycTierCapability
                        {
                            Tier = kycTier.Tier,
                            Description = kycTier.Description,
                            TokenStandards = new List<TokenStandardCapability>()
                        };

                        foreach (var tokenStandard in kycTier.TokenStandards)
                        {
                            // Filter by token standard
                            if (!string.IsNullOrWhiteSpace(request.TokenStandard) &&
                                !string.Equals(tokenStandard.Standard, request.TokenStandard, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            filteredKycTier.TokenStandards.Add(tokenStandard);
                        }

                        if (filteredKycTier.TokenStandards.Count > 0)
                        {
                            filteredWalletType.KycTiers.Add(filteredKycTier);
                        }
                    }

                    if (filteredWalletType.KycTiers.Count > 0)
                    {
                        filteredJurisdiction.WalletTypes.Add(filteredWalletType);
                    }
                }

                if (filteredJurisdiction.WalletTypes.Count > 0)
                {
                    filtered.Jurisdictions.Add(filteredJurisdiction);
                }
            }

            return filtered.Jurisdictions.Count > 0 ? filtered : null;
        }
    }
}

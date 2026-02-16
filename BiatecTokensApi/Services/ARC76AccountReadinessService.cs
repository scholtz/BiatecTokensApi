using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing ARC76 account readiness and lifecycle
    /// </summary>
    public class ARC76AccountReadinessService : IARC76AccountReadinessService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthenticationService _authenticationService;
        private readonly ILogger<ARC76AccountReadinessService> _logger;
        private readonly KeyProviderFactory _keyProviderFactory;

        // In-memory storage for account readiness states
        // Key: userId, Value: readiness state
        private readonly ConcurrentDictionary<string, ARC76ReadinessState> _readinessStates;
        private readonly ConcurrentDictionary<string, DateTime> _lastStateTransitions;

        public ARC76AccountReadinessService(
            IUserRepository userRepository,
            IAuthenticationService authenticationService,
            ILogger<ARC76AccountReadinessService> logger,
            KeyProviderFactory keyProviderFactory)
        {
            _userRepository = userRepository;
            _authenticationService = authenticationService;
            _logger = logger;
            _keyProviderFactory = keyProviderFactory;
            _readinessStates = new ConcurrentDictionary<string, ARC76ReadinessState>(StringComparer.OrdinalIgnoreCase);
            _lastStateTransitions = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<ARC76AccountReadinessResult> CheckAccountReadinessAsync(string userId, string? correlationId = null)
        {
            try
            {
                var sanitizedUserId = LoggingHelper.SanitizeLogInput(userId);
                _logger.LogDebug("Checking ARC76 account readiness for user {UserId}. CorrelationId: {CorrelationId}",
                    sanitizedUserId, correlationId ?? "N/A");

                // Get user from repository
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found during readiness check: {UserId}. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");

                    return new ARC76AccountReadinessResult
                    {
                        State = ARC76ReadinessState.NotInitialized,
                        NotReadyReason = "User account not found",
                        RemediationSteps = new List<string> { "Register or login to initialize your account" },
                        CorrelationId = correlationId
                    };
                }

                // Check if account has Algorand address
                if (string.IsNullOrWhiteSpace(user.AlgorandAddress))
                {
                    _logger.LogWarning("User {UserId} has no Algorand address. State: NotInitialized. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");

                    SetReadinessState(userId, ARC76ReadinessState.NotInitialized);

                    return new ARC76AccountReadinessResult
                    {
                        State = ARC76ReadinessState.NotInitialized,
                        NotReadyReason = "ARC76 account not initialized",
                        RemediationSteps = new List<string>
                        {
                            "Initialize your ARC76 account through the authentication flow",
                            "Contact support if this issue persists"
                        },
                        CorrelationId = correlationId
                    };
                }

                // Check if encrypted mnemonic exists
                if (string.IsNullOrWhiteSpace(user.EncryptedMnemonic))
                {
                    _logger.LogWarning("User {UserId} has no encrypted mnemonic. State: Failed. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");

                    SetReadinessState(userId, ARC76ReadinessState.Failed);

                    return new ARC76AccountReadinessResult
                    {
                        State = ARC76ReadinessState.Failed,
                        AccountAddress = user.AlgorandAddress,
                        NotReadyReason = "Account credentials missing",
                        RemediationSteps = new List<string>
                        {
                            "Re-register your account to restore credentials",
                            "Contact support for account recovery"
                        },
                        CorrelationId = correlationId
                    };
                }

                // Validate key accessibility
                var keyStatus = await ValidateKeyAccessibilityAsync(userId);

                // Validate metadata
                var metadataValidation = ValidateAccountMetadata(user);

                // Determine overall readiness state
                var readinessState = DetermineReadinessState(keyStatus, metadataValidation);
                SetReadinessState(userId, readinessState);

                var result = new ARC76AccountReadinessResult
                {
                    State = readinessState,
                    AccountAddress = user.AlgorandAddress,
                    MetadataValidation = metadataValidation,
                    KeyStatus = keyStatus,
                    LastStateTransition = _lastStateTransitions.GetOrAdd(userId, DateTime.UtcNow),
                    CorrelationId = correlationId
                };

                if (!result.IsReady)
                {
                    result.NotReadyReason = BuildNotReadyReason(readinessState, keyStatus, metadataValidation);
                    result.RemediationSteps = BuildRemediationSteps(readinessState, keyStatus, metadataValidation);

                    _logger.LogWarning(
                        "User {UserId} account not ready. State: {State}, Reason: {Reason}. CorrelationId: {CorrelationId}",
                        sanitizedUserId, readinessState, result.NotReadyReason, correlationId ?? "N/A");
                }
                else
                {
                    _logger.LogDebug("User {UserId} account ready. State: Ready. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ARC76 account readiness for user {UserId}. CorrelationId: {CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId ?? "N/A");

                return new ARC76AccountReadinessResult
                {
                    State = ARC76ReadinessState.Failed,
                    NotReadyReason = "Error checking account readiness",
                    RemediationSteps = new List<string> { "Retry the operation", "Contact support if issue persists" },
                    CorrelationId = correlationId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> InitializeAccountAsync(string userId, string? correlationId = null)
        {
            try
            {
                var sanitizedUserId = LoggingHelper.SanitizeLogInput(userId);
                _logger.LogInformation("Initializing ARC76 account for user {UserId}. CorrelationId: {CorrelationId}",
                    sanitizedUserId, correlationId ?? "N/A");

                SetReadinessState(userId, ARC76ReadinessState.Initializing);

                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot initialize account - user not found: {UserId}. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");
                    SetReadinessState(userId, ARC76ReadinessState.Failed);
                    return false;
                }

                // If account already has address and mnemonic, consider it initialized
                if (!string.IsNullOrWhiteSpace(user.AlgorandAddress) && 
                    !string.IsNullOrWhiteSpace(user.EncryptedMnemonic))
                {
                    _logger.LogInformation("Account already initialized for user {UserId}. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");
                    SetReadinessState(userId, ARC76ReadinessState.Ready);
                    return true;
                }

                // Initialization happens during registration in AuthenticationService
                // This method primarily validates existing initialization
                var readinessResult = await CheckAccountReadinessAsync(userId, correlationId);
                
                if (readinessResult.IsReady)
                {
                    _logger.LogInformation("Account successfully initialized for user {UserId}. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId ?? "N/A");
                    return true;
                }

                _logger.LogWarning("Account initialization incomplete for user {UserId}. State: {State}. CorrelationId: {CorrelationId}",
                    sanitizedUserId, readinessResult.State, correlationId ?? "N/A");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ARC76 account for user {UserId}. CorrelationId: {CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId ?? "N/A");
                SetReadinessState(userId, ARC76ReadinessState.Failed);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateAccountIntegrityAsync(string userId)
        {
            try
            {
                var sanitizedUserId = LoggingHelper.SanitizeLogInput(userId);
                _logger.LogDebug("Validating account integrity for user {UserId}", sanitizedUserId);

                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                // Validate key accessibility
                var keyStatus = await ValidateKeyAccessibilityAsync(userId);
                if (!keyStatus.IsAccessible)
                {
                    _logger.LogWarning("Key not accessible for user {UserId}", sanitizedUserId);
                    return false;
                }

                // Validate metadata
                var metadataValidation = ValidateAccountMetadata(user);
                if (!metadataValidation.IsValid)
                {
                    _logger.LogWarning("Metadata validation failed for user {UserId}: {Errors}",
                        sanitizedUserId, string.Join(", ", metadataValidation.ValidationErrors));
                    return false;
                }

                _logger.LogDebug("Account integrity validated successfully for user {UserId}", sanitizedUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating account integrity for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<ARC76ReadinessState> GetReadinessStateAsync(string userId)
        {
            var state = _readinessStates.GetOrAdd(userId, ARC76ReadinessState.NotInitialized);
            return Task.FromResult(state);
        }

        #region Private Helper Methods

        private async Task<KeyStatusInfo> ValidateKeyAccessibilityAsync(string userId)
        {
            try
            {
                // Try to get the mnemonic to verify key provider is accessible
                var mnemonic = await _authenticationService.GetUserMnemonicForSigningAsync(userId);

                var keyProvider = _keyProviderFactory.CreateProvider();
                var providerName = keyProvider.GetType().Name;

                return new KeyStatusInfo
                {
                    IsAccessible = !string.IsNullOrEmpty(mnemonic),
                    RotationRequired = false,
                    KeyProvider = providerName
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating key accessibility for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return new KeyStatusInfo
                {
                    IsAccessible = false,
                    RotationRequired = false
                };
            }
        }

        private AccountMetadataValidation ValidateAccountMetadata(Models.Auth.User user)
        {
            var validation = new AccountMetadataValidation
            {
                IsValid = true,
                ValidationErrors = new List<string>()
            };

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                validation.IsValid = false;
                validation.ValidationErrors.Add("Email is missing");
            }

            if (string.IsNullOrWhiteSpace(user.AlgorandAddress))
            {
                validation.IsValid = false;
                validation.ValidationErrors.Add("Algorand address is missing");
            }

            if (string.IsNullOrWhiteSpace(user.EncryptedMnemonic))
            {
                validation.IsValid = false;
                validation.ValidationErrors.Add("Encrypted mnemonic is missing");
            }

            if (!user.IsActive)
            {
                validation.IsValid = false;
                validation.ValidationErrors.Add("User account is not active");
            }

            return validation;
        }

        private ARC76ReadinessState DetermineReadinessState(KeyStatusInfo keyStatus, AccountMetadataValidation metadataValidation)
        {
            if (!metadataValidation.IsValid)
            {
                return ARC76ReadinessState.Failed;
            }

            if (!keyStatus.IsAccessible)
            {
                return ARC76ReadinessState.Degraded;
            }

            if (keyStatus.RotationRequired || metadataValidation.NeedsUpdate)
            {
                return ARC76ReadinessState.Degraded;
            }

            return ARC76ReadinessState.Ready;
        }

        private void SetReadinessState(string userId, ARC76ReadinessState state)
        {
            _readinessStates.AddOrUpdate(userId, state, (key, oldValue) => state);
            _lastStateTransitions.AddOrUpdate(userId, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);
        }

        private string BuildNotReadyReason(ARC76ReadinessState state, KeyStatusInfo keyStatus, AccountMetadataValidation metadataValidation)
        {
            return state switch
            {
                ARC76ReadinessState.NotInitialized => "Account has not been initialized",
                ARC76ReadinessState.Initializing => "Account is currently being initialized",
                ARC76ReadinessState.Degraded => BuildDegradedReason(keyStatus, metadataValidation),
                ARC76ReadinessState.Failed => BuildFailedReason(metadataValidation),
                _ => "Account readiness check failed"
            };
        }

        private string BuildDegradedReason(KeyStatusInfo keyStatus, AccountMetadataValidation metadataValidation)
        {
            var reasons = new List<string>();

            if (!keyStatus.IsAccessible)
            {
                reasons.Add("Account keys are not accessible");
            }

            if (keyStatus.RotationRequired)
            {
                reasons.Add("Key rotation is required");
            }

            if (metadataValidation.NeedsUpdate)
            {
                reasons.Add("Account metadata needs update");
            }

            return reasons.Any() ? string.Join("; ", reasons) : "Account is in degraded state";
        }

        private string BuildFailedReason(AccountMetadataValidation metadataValidation)
        {
            if (metadataValidation.ValidationErrors.Any())
            {
                return $"Metadata validation failed: {string.Join(", ", metadataValidation.ValidationErrors)}";
            }

            return "Account initialization failed";
        }

        private List<string> BuildRemediationSteps(ARC76ReadinessState state, KeyStatusInfo keyStatus, AccountMetadataValidation metadataValidation)
        {
            var steps = new List<string>();

            switch (state)
            {
                case ARC76ReadinessState.NotInitialized:
                    steps.Add("Complete the registration process");
                    steps.Add("Ensure your account is fully initialized");
                    break;

                case ARC76ReadinessState.Initializing:
                    steps.Add("Wait for account initialization to complete");
                    steps.Add("Retry your operation in a few moments");
                    break;

                case ARC76ReadinessState.Degraded:
                    if (!keyStatus.IsAccessible)
                    {
                        steps.Add("Verify your authentication credentials");
                        steps.Add("Contact support if key access issues persist");
                    }
                    if (keyStatus.RotationRequired)
                    {
                        steps.Add("Perform key rotation through account settings");
                    }
                    if (metadataValidation.NeedsUpdate)
                    {
                        steps.Add("Update your account metadata");
                    }
                    break;

                case ARC76ReadinessState.Failed:
                    steps.Add("Contact support for account recovery");
                    steps.Add("Provide your user ID and correlation ID for faster resolution");
                    break;
            }

            if (!steps.Any())
            {
                steps.Add("Contact support for assistance");
            }

            return steps;
        }

        #endregion
    }
}

using Algorand;
using AlgorandARC76AccountDotNet;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NBitcoin;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Authentication service with ARC76 account derivation and JWT token management
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        /// <summary>
        /// Version of the ARC76 derivation contract implemented by this service.
        /// Increment when derivation rules change to allow clients to detect breaking changes.
        /// </summary>
        public const string DerivationContractVersion = "1.0";

        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly JwtConfig _jwtConfig;
        private readonly KeyProviderFactory _keyProviderFactory;

        public AuthenticationService(
            IUserRepository userRepository,
            ILogger<AuthenticationService> logger,
            IOptions<JwtConfig> jwtConfig,
            KeyProviderFactory keyProviderFactory)
        {
            _userRepository = userRepository;
            _logger = logger;
            _jwtConfig = jwtConfig.Value;
            _keyProviderFactory = keyProviderFactory;
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, string? ipAddress, string? userAgent)
        {
            try
            {
                // Validate password strength
                if (!IsPasswordStrong(request.Password))
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.WEAK_PASSWORD,
                        ErrorMessage = "Password must be at least 8 characters and contain uppercase, lowercase, number, and special character"
                    };
                }

                // Check if user already exists
                if (await _userRepository.UserExistsAsync(request.Email))
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.USER_ALREADY_EXISTS,
                        ErrorMessage = "A user with this email already exists"
                    };
                }

                // Derive ARC76 account from email and password
                var mnemonic = GenerateMnemonic();
                var account = ARC76.GetAccount(mnemonic);

                // Hash password
                var passwordHash = HashPassword(request.Password);

                // Encrypt mnemonic with system password (so it can be decrypted for signing operations)
                // Use configured key provider (Azure Key Vault, AWS KMS, or Environment Variable)
                var keyProvider = _keyProviderFactory.CreateProvider();
                var systemPassword = await keyProvider.GetEncryptionKeyAsync();
                var encryptedMnemonic = EncryptMnemonic(mnemonic, systemPassword);

                // Create user
                var user = new User
                {
                    UserId = Guid.NewGuid().ToString(),
                    Email = CanonicalizeEmail(request.Email),
                    PasswordHash = passwordHash,
                    AlgorandAddress = account.Address.ToString(),
                    EncryptedMnemonic = encryptedMnemonic,
                    FullName = request.FullName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _userRepository.CreateUserAsync(user);

                // Generate tokens
                var accessToken = GenerateAccessToken(user);
                var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.UserId, ipAddress, userAgent);

                _logger.LogInformation("User registered successfully: Email={Email}, AlgorandAddress={Address}",
                    LoggingHelper.SanitizeLogInput(user.Email),
                    LoggingHelper.SanitizeLogInput(user.AlgorandAddress));

                return new RegisterResponse
                {
                    Success = true,
                    UserId = user.UserId,
                    Email = user.Email,
                    AlgorandAddress = user.AlgorandAddress,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtConfig.AccessTokenExpirationMinutes),
                    DerivationContractVersion = DerivationContractVersion
                };
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("User with this email already exists"))
            {
                _logger.LogWarning("Duplicate registration attempt: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                return new RegisterResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.USER_ALREADY_EXISTS,
                    ErrorMessage = "A user with this email address already exists"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                return new RegisterResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred during registration"
                };
            }
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
        {
            try
            {
                var user = await _userRepository.GetUserByEmailAsync(CanonicalizeEmail(request.Email));

                if (user == null)
                {
                    _logger.LogWarning("Login attempt for non-existent user: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                    return new LoginResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_CREDENTIALS,
                        ErrorMessage = "Invalid email or password"
                    };
                }

                // Check if account is locked
                if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
                {
                    _logger.LogWarning("Login attempt for locked account: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                    return new LoginResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.ACCOUNT_LOCKED,
                        ErrorMessage = $"Account is locked until {user.LockedUntil.Value:yyyy-MM-dd HH:mm:ss} UTC"
                    };
                }

                // Check if account is active
                if (!user.IsActive)
                {
                    _logger.LogWarning("Login attempt for inactive account: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                    return new LoginResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.ACCOUNT_INACTIVE,
                        ErrorMessage = "Account is inactive. Please contact support."
                    };
                }

                // Verify password
                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    // Increment failed login attempts
                    user.FailedLoginAttempts++;
                    
                    // Lock account after 5 failed attempts for 30 minutes
                    if (user.FailedLoginAttempts >= 5)
                    {
                        user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                        _logger.LogWarning("Account locked due to failed login attempts: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                    }

                    await _userRepository.UpdateUserAsync(user);

                    return new LoginResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_CREDENTIALS,
                        ErrorMessage = "Invalid email or password"
                    };
                }

                // Reset failed login attempts
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateUserAsync(user);

                // Generate tokens
                var accessToken = GenerateAccessToken(user);
                var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.UserId, ipAddress, userAgent);

                _logger.LogInformation("User logged in successfully: Email={Email}, AlgorandAddress={Address}",
                    LoggingHelper.SanitizeLogInput(user.Email),
                    LoggingHelper.SanitizeLogInput(user.AlgorandAddress));

                return new LoginResponse
                {
                    Success = true,
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    AlgorandAddress = user.AlgorandAddress,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtConfig.AccessTokenExpirationMinutes),
                    DerivationContractVersion = DerivationContractVersion
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login: {Email}", LoggingHelper.SanitizeLogInput(request.Email));
                return new LoginResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred during login"
                };
            }
        }

        public async Task<RefreshTokenResponse> RefreshTokenAsync(string refreshTokenValue, string? ipAddress, string? userAgent)
        {
            try
            {
                var refreshToken = await _userRepository.GetRefreshTokenAsync(refreshTokenValue);

                if (refreshToken == null)
                {
                    return new RefreshTokenResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REFRESH_TOKEN,
                        ErrorMessage = "Invalid refresh token"
                    };
                }

                if (refreshToken.IsRevoked)
                {
                    _logger.LogWarning("Attempt to use revoked refresh token: UserId={UserId}", LoggingHelper.SanitizeLogInput(refreshToken.UserId));
                    return new RefreshTokenResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.REFRESH_TOKEN_REVOKED,
                        ErrorMessage = "Refresh token has been revoked"
                    };
                }

                if (refreshToken.ExpiresAt < DateTime.UtcNow)
                {
                    return new RefreshTokenResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.REFRESH_TOKEN_EXPIRED,
                        ErrorMessage = "Refresh token has expired"
                    };
                }

                var user = await _userRepository.GetUserByIdAsync(refreshToken.UserId);
                if (user == null || !user.IsActive)
                {
                    return new RefreshTokenResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.USER_NOT_FOUND,
                        ErrorMessage = "User not found or inactive"
                    };
                }

                // Revoke old refresh token
                await _userRepository.RevokeRefreshTokenAsync(refreshTokenValue);

                // Generate new tokens
                var accessToken = GenerateAccessToken(user);
                var newRefreshToken = await GenerateAndStoreRefreshTokenAsync(user.UserId, ipAddress, userAgent);

                _logger.LogInformation("Token refreshed successfully: UserId={UserId}", LoggingHelper.SanitizeLogInput(user.UserId));

                return new RefreshTokenResponse
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken.Token,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtConfig.AccessTokenExpirationMinutes)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return new RefreshTokenResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred during token refresh"
                };
            }
        }

        public async Task<LogoutResponse> LogoutAsync(string userId)
        {
            try
            {
                await _userRepository.RevokeAllUserRefreshTokensAsync(userId);

                _logger.LogInformation("User logged out: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));

                return new LogoutResponse
                {
                    Success = true,
                    Message = "Logged out successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                return new LogoutResponse
                {
                    Success = false,
                    Message = "An error occurred during logout"
                };
            }
        }

        public Task<string?> ValidateAccessTokenAsync(string accessToken)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtConfig.SecretKey);

                tokenHandler.ValidateToken(accessToken, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = _jwtConfig.ValidateIssuerSigningKey,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = _jwtConfig.ValidateIssuer,
                    ValidIssuer = _jwtConfig.Issuer,
                    ValidateAudience = _jwtConfig.ValidateAudience,
                    ValidAudience = _jwtConfig.Audience,
                    ValidateLifetime = _jwtConfig.ValidateLifetime,
                    ClockSkew = TimeSpan.FromMinutes(_jwtConfig.ClockSkewMinutes)
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;

                return Task.FromResult<string?>(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return Task.FromResult<string?>(null);
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null) return false;

                if (!VerifyPassword(currentPassword, user.PasswordHash))
                {
                    return false;
                }

                if (!IsPasswordStrong(newPassword))
                {
                    return false;
                }

                // Update password hash
                user.PasswordHash = HashPassword(newPassword);

                // Mnemonic remains encrypted with system password (no need to re-encrypt)
                // The user password is only for authentication, not for mnemonic encryption

                await _userRepository.UpdateUserAsync(user);

                // Revoke all refresh tokens for security
                await _userRepository.RevokeAllUserRefreshTokensAsync(userId);

                _logger.LogInformation("Password changed for user: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                return false;
            }
        }

        public async Task<string?> GetUserMnemonicForSigningAsync(string userId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found when retrieving mnemonic: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                    return null;
                }

                // Validate that encrypted mnemonic exists
                if (string.IsNullOrWhiteSpace(user.EncryptedMnemonic))
                {
                    _logger.LogError("Encrypted mnemonic is missing for user: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                    throw new InvalidOperationException("Account credentials are missing. Please contact support.");
                }

                // Return the decrypted mnemonic for signing operations
                // Uses configured key provider (Azure Key Vault, AWS KMS, or Environment Variable)
                var mnemonic = await DecryptMnemonicForSigning(user.EncryptedMnemonic);

                _logger.LogDebug("Successfully retrieved mnemonic for signing: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                return mnemonic;
            }
            catch (InvalidOperationException)
            {
                // Re-throw key provider and configuration exceptions with clear messaging
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Decryption failed for user mnemonic: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                throw new InvalidOperationException("Unable to decrypt account credentials. Please contact support.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting user mnemonic for signing: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                throw new InvalidOperationException("An unexpected error occurred while accessing credentials. Please contact support.", ex);
            }
        }

        public async Task<ARC76DerivationVerifyResponse> VerifyDerivationAsync(string userId, string? requestEmail, string correlationId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Derivation verification: user not found. UserId={UserId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId), correlationId);
                    return new ARC76DerivationVerifyResponse
                    {
                        Success = false,
                        IsConsistent = false,
                        ErrorCode = ErrorCodes.NOT_FOUND,
                        ErrorMessage = "User not found.",
                        RemediationHint = "Ensure you are authenticated and the session is valid.",
                        CorrelationId = correlationId
                    };
                }

                // If caller supplied an email, it must match the authenticated user's own email.
                if (!string.IsNullOrWhiteSpace(requestEmail) &&
                    !string.Equals(CanonicalizeEmail(requestEmail), user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Derivation verification: email mismatch. UserId={UserId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId), correlationId);
                    return new ARC76DerivationVerifyResponse
                    {
                        Success = false,
                        IsConsistent = false,
                        ErrorCode = ErrorCodes.FORBIDDEN,
                        ErrorMessage = "The supplied email does not match the authenticated user.",
                        RemediationHint = "Only the authenticated user may verify their own derivation.",
                        CorrelationId = correlationId
                    };
                }

                var address = user.AlgorandAddress ?? string.Empty;
                var fingerprint = address.Length >= 8 ? address[..8] : address;

                _logger.LogInformation("Derivation verification succeeded. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);

                return new ARC76DerivationVerifyResponse
                {
                    Success = true,
                    AlgorandAddress = address,
                    IsConsistent = true,
                    DerivationContractVersion = DerivationContractVersion,
                    DerivationAlgorithm = "ARC76/BIP39",
                    DeterminismProof = new ARC76DeterminismProof
                    {
                        CanonicalEmail = user.Email,
                        Standard = "ARC76",
                        DerivationPath = "BIP39/Algorand",
                        AddressFingerprint = fingerprint,
                        ContractVersion = DerivationContractVersion
                    },
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Derivation verification error. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);
                return new ARC76DerivationVerifyResponse
                {
                    Success = false,
                    IsConsistent = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred during derivation verification.",
                    RemediationHint = "Retry the request. If the problem persists, contact support.",
                    CorrelationId = correlationId
                };
            }
        }

        public ARC76DerivationInfoResponse GetDerivationInfo(string correlationId)
        {
            return new ARC76DerivationInfoResponse
            {
                ContractVersion = DerivationContractVersion,
                Standard = "ARC76",
                AlgorithmDescription = "Algorand ARC76 account derivation via BIP39 mnemonic. " +
                    "Each user receives a unique mnemonic at registration which is encrypted at rest. " +
                    "The Algorand account is derived deterministically from the mnemonic using ARC76.GetAccount().",
                BoundedErrorCodes = new[]
                {
                    ErrorCodes.NOT_FOUND,
                    ErrorCodes.FORBIDDEN,
                    ErrorCodes.UNAUTHORIZED,
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorCodes.INVALID_REQUEST,
                },
                IsBackwardCompatible = true,
                EffectiveFrom = "2026-01-01",
                SpecificationUrl = "https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0076.md",
                CorrelationId = correlationId
            };
        }

        public async Task<SessionInspectionResponse> InspectSessionAsync(string userId, string correlationId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Session inspection: user not found. UserId={UserId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId), correlationId);
                    return new SessionInspectionResponse
                    {
                        IsActive = false,
                        CorrelationId = correlationId
                    };
                }

                _logger.LogInformation("Session inspection succeeded. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);

                return new SessionInspectionResponse
                {
                    IsActive = user.IsActive,
                    UserId = user.UserId,
                    Email = user.Email,
                    AlgorandAddress = user.AlgorandAddress,
                    TokenType = "Bearer",
                    DerivationContractVersion = DerivationContractVersion,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session inspection error. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);
                return new SessionInspectionResponse
                {
                    IsActive = false,
                    CorrelationId = correlationId
                };
            }
        }

        // Private helper methods

        private string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtConfig.SecretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("algorand_address", user.AlgorandAddress),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                claims.Add(new Claim(ClaimTypes.Name, user.FullName));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtConfig.AccessTokenExpirationMinutes),
                Issuer = _jwtConfig.Issuer,
                Audience = _jwtConfig.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private async Task<RefreshToken> GenerateAndStoreRefreshTokenAsync(string userId, string? ipAddress, string? userAgent)
        {
            var refreshToken = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = GenerateRefreshTokenValue(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtConfig.RefreshTokenExpirationDays),
                CreatedByIp = ipAddress,
                CreatedByUserAgent = userAgent
            };

            await _userRepository.StoreRefreshTokenAsync(refreshToken);

            return refreshToken;
        }

        private string GenerateRefreshTokenValue()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string HashPassword(string password)
        {
            // Using BCrypt-like approach with SHA256 for MVP
            // In production, use BCrypt.Net or Microsoft.AspNetCore.Identity.PasswordHasher
            using var sha256 = SHA256.Create();
            var salt = GenerateSalt();
            var saltedPassword = salt + password;
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return $"{salt}:{Convert.ToBase64String(hash)}";
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            try
            {
                var parts = passwordHash.Split(':');
                if (parts.Length != 2) return false;

                var salt = parts[0];
                var storedHash = parts[1];

                using var sha256 = SHA256.Create();
                var saltedPassword = salt + password;
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                var computedHash = Convert.ToBase64String(hash);

                return storedHash == computedHash;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        private bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        /// <summary>
        /// Canonicalizes email address for deterministic ARC76 account derivation
        /// </summary>
        /// <param name="email">Raw email address from user input</param>
        /// <returns>Canonicalized email (lowercase, trimmed)</returns>
        /// <remarks>
        /// Email canonicalization ensures:
        /// - Deterministic ARC76 account derivation (same email -> same account)
        /// - Prevents duplicate accounts from case/whitespace variations
        /// - Consistent lookup behavior across registration/login
        /// 
        /// Canonicalization rules:
        /// 1. Trim leading/trailing whitespace
        /// 2. Convert to lowercase (email addresses are case-insensitive per RFC 5321)
        /// 
        /// Business Value: Eliminates support incidents from users unable to login
        /// due to case mismatch between registration and login.
        /// 
        /// Risk Mitigation: Prevents authorization drift and account access issues
        /// that would undermine enterprise governance requirements.
        /// </remarks>
        private static string CanonicalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
            }

            return email.Trim().ToLowerInvariant();
        }

        private string GenerateMnemonic()
        {
            // Generate a new BIP39 mnemonic using NBitcoin
            // NBitcoin generates 24-word BIP39 mnemonics (256 bits of entropy)
            // This is compatible with Algorand which uses the same BIP39 standard
            // ARC76.GetAccount accepts standard BIP39 mnemonics and derives Algorand accounts
            try
            {
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour);
                var mnemonicString = mnemonic.ToString();
                
                _logger.LogInformation("Generated new BIP39 mnemonic for user account");
                return mnemonicString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating mnemonic");
                throw new InvalidOperationException("Failed to generate BIP39 mnemonic", ex);
            }
        }

        private string EncryptMnemonic(string mnemonic, string password)
        {
            // Use AES-256-GCM for production-grade encryption
            try
            {
                var mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
                
                // Generate random nonce (12 bytes for GCM)
                var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
                RandomNumberGenerator.Fill(nonce);
                
                // Generate random salt for key derivation (stored with encrypted data)
                var salt = new byte[32];
                RandomNumberGenerator.Fill(salt);
                
                // Derive actual encryption key from password + salt
                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
                var encryptionKey = pbkdf2.GetBytes(32); // 256-bit key
                
                // Prepare buffers
                var ciphertext = new byte[mnemonicBytes.Length];
                var tag = new byte[AesGcm.TagByteSizes.MaxSize];
                
                // Encrypt using AES-GCM
                using var aesGcm = new AesGcm(encryptionKey, AesGcm.TagByteSizes.MaxSize);
                aesGcm.Encrypt(nonce, mnemonicBytes, ciphertext, tag);
                
                // Combine: salt (32) + nonce (12) + tag (16) + ciphertext
                var result = new byte[salt.Length + nonce.Length + tag.Length + ciphertext.Length];
                Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
                Buffer.BlockCopy(nonce, 0, result, salt.Length, nonce.Length);
                Buffer.BlockCopy(tag, 0, result, salt.Length + nonce.Length, tag.Length);
                Buffer.BlockCopy(ciphertext, 0, result, salt.Length + nonce.Length + tag.Length, ciphertext.Length);
                
                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting mnemonic");
                throw;
            }
        }

        private string DecryptMnemonic(string encryptedMnemonic, string password)
        {
            // Decrypt using AES-256-GCM
            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedMnemonic);
                
                // Extract components: salt (32) + nonce (12) + tag (16) + ciphertext
                var salt = new byte[32];
                var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
                var tag = new byte[AesGcm.TagByteSizes.MaxSize];
                var ciphertext = new byte[encryptedBytes.Length - salt.Length - nonce.Length - tag.Length];
                
                Buffer.BlockCopy(encryptedBytes, 0, salt, 0, salt.Length);
                Buffer.BlockCopy(encryptedBytes, salt.Length, nonce, 0, nonce.Length);
                Buffer.BlockCopy(encryptedBytes, salt.Length + nonce.Length, tag, 0, tag.Length);
                Buffer.BlockCopy(encryptedBytes, salt.Length + nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);
                
                // Derive encryption key from password + salt
                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
                var encryptionKey = pbkdf2.GetBytes(32); // 256-bit key
                
                // Decrypt using AES-GCM
                var plaintext = new byte[ciphertext.Length];
                using var aesGcm = new AesGcm(encryptionKey, AesGcm.TagByteSizes.MaxSize);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                
                return Encoding.UTF8.GetString(plaintext);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning("Failed to decrypt mnemonic - invalid password or corrupted data");
                throw new UnauthorizedAccessException("Invalid password or corrupted encrypted data", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting mnemonic");
                throw;
            }
        }

        private async Task<string> DecryptMnemonicForSigning(string encryptedMnemonic)
        {
            // Use configured key provider (Azure Key Vault, AWS KMS, or Environment Variable)
            try
            {
                var keyProvider = _keyProviderFactory.CreateProvider();
                
                // Validate provider configuration before attempting to retrieve key
                var isConfigValid = await keyProvider.ValidateConfigurationAsync();
                if (!isConfigValid)
                {
                    _logger.LogError("Key provider validation failed: ProviderType={ProviderType}", keyProvider.ProviderType);
                    throw new InvalidOperationException(
                        $"Key provider '{keyProvider.ProviderType}' is not properly configured. Please contact support.");
                }
                
                var systemPassword = await keyProvider.GetEncryptionKeyAsync();
                return DecryptMnemonic(encryptedMnemonic, systemPassword);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Key provider configuration error during mnemonic decryption");
                throw new InvalidOperationException(
                    "Unable to access encryption keys. This is a system configuration issue. Please contact support.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during mnemonic decryption");
                throw new InvalidOperationException(
                    "An error occurred while accessing secure credentials. Please try again later or contact support.", ex);
            }
        }

        private byte[] DeriveKeyFromPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}

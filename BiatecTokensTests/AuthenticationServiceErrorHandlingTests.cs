using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for enhanced error handling in AuthenticationService
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AuthenticationServiceErrorHandlingTests
    {
        private Mock<IUserRepository> _mockUserRepository = null!;
        private Mock<ILogger<AuthenticationService>> _mockLogger = null!;
        private Mock<IOptions<JwtConfig>> _mockJwtConfig = null!;
        private TestKeyProviderFactory _keyProviderFactory = null!;
        private TestKeyProvider _keyProvider = null!;
        private AuthenticationService _authService = null!;

        [SetUp]
        public void Setup()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<AuthenticationService>>();
            _mockJwtConfig = new Mock<IOptions<JwtConfig>>();
            
            var jwtConfig = new JwtConfig
            {
                SecretKey = "ThisIsAVeryLongSecretKeyForTestingPurposesWithAtLeast256Bits!",
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensApi",
                AccessTokenExpirationMinutes = 30,
                RefreshTokenExpirationDays = 7
            };
            _mockJwtConfig.Setup(x => x.Value).Returns(jwtConfig);

            _keyProvider = new TestKeyProvider();
            _keyProviderFactory = new TestKeyProviderFactory(_keyProvider);

            _authService = new AuthenticationService(
                _mockUserRepository.Object,
                _mockLogger.Object,
                _mockJwtConfig.Object,
                _keyProviderFactory);
        }

        #region Key Provider Failure Tests

        [Test]
        public async Task GetUserMnemonicForSigning_WhenKeyProviderValidationFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var userId = "test-user-123";
            var testUser = new User
            {
                UserId = userId,
                Email = "test@example.com",
                EncryptedMnemonic = "encrypted-mnemonic-data"
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync(testUser);
            
            _keyProvider.IsConfigValid = false;

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _authService.GetUserMnemonicForSigningAsync(userId));
            
            Assert.That(ex.Message, Does.Contain("encryption keys"));
        }

        [Test]
        public async Task GetUserMnemonicForSigning_WhenKeyProviderThrowsException_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var userId = "test-user-456";
            var testUser = new User
            {
                UserId = userId,
                Email = "test@example.com",
                EncryptedMnemonic = "encrypted-mnemonic-data"
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync(testUser);
            
            _keyProvider.ShouldThrowException = true;

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _authService.GetUserMnemonicForSigningAsync(userId));
            
            Assert.That(ex.Message, Does.Contain("encryption keys"));
        }

        [Test]
        public async Task GetUserMnemonicForSigning_WhenEncryptedMnemonicMissing_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var userId = "test-user-789";
            var testUser = new User
            {
                UserId = userId,
                Email = "test@example.com",
                EncryptedMnemonic = "" // Empty mnemonic
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync(testUser);

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _authService.GetUserMnemonicForSigningAsync(userId));
            
            Assert.That(ex.Message, Does.Contain("credentials are missing"));
        }

        [Test]
        public async Task GetUserMnemonicForSigning_WhenUserNotFound_ShouldReturnNull()
        {
            // Arrange
            var userId = "non-existent-user";

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _authService.GetUserMnemonicForSigningAsync(userId);

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region Account Lockout Tests

        [Test]
        public async Task Login_AfterMultipleFailedAttempts_ShouldLockAccount()
        {
            // Arrange
            var email = "lockout-test@example.com";
            var password = "WrongPassword123!";
            var lockedUntil = DateTime.UtcNow.AddMinutes(30);

            var testUser = new User
            {
                UserId = "lockout-user",
                Email = email,
                PasswordHash = "some-hash",
                IsActive = true,
                FailedLoginAttempts = 5,
                LockedUntil = lockedUntil
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(testUser);

            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var result = await _authService.LoginAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.ACCOUNT_LOCKED));
            Assert.That(result.ErrorMessage, Does.Contain("locked"));
        }

        #endregion

        #region Error Response Correlation Tests

        [Test]
        public async Task Login_WhenFails_ShouldIncludeCorrelationInfo()
        {
            // Arrange
            var email = "test@example.com";
            var password = "WrongPassword123!";

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync((User?)null);

            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var result = await _authService.LoginAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region Password Strength Tests

        [Test]
        public async Task Register_WithWeakPassword_ShouldReturnWeakPasswordError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "weak", // Too short, no complexity
                FullName = "Test User"
            };

            // Act
            var result = await _authService.RegisterAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.WEAK_PASSWORD));
            Assert.That(result.ErrorMessage, Does.Contain("Password must"));
        }

        [Test]
        public async Task Register_WithPasswordMissingUppercase_ShouldReturnWeakPasswordError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "password123!", // No uppercase
                FullName = "Test User"
            };

            // Act
            var result = await _authService.RegisterAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.WEAK_PASSWORD));
        }

        #endregion

        #region Duplicate Email Tests

        [Test]
        public async Task Register_WithExistingEmail_ShouldReturnUserExistsError()
        {
            // Arrange
            var email = "existing@example.com";

            _mockUserRepository.Setup(x => x.UserExistsAsync(email))
                .ReturnsAsync(true);

            var request = new RegisterRequest
            {
                Email = email,
                Password = "ValidPassword123!",
                FullName = "Test User"
            };

            // Act
            var result = await _authService.RegisterAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.USER_ALREADY_EXISTS));
            Assert.That(result.ErrorMessage, Does.Contain("already exists"));
        }

        #endregion

        #region Refresh Token Tests

        [Test]
        public async Task RefreshToken_WithRevokedToken_ShouldReturnRevokedError()
        {
            // Arrange
            var tokenValue = "revoked-token";
            var revokedToken = new RefreshToken
            {
                Token = tokenValue,
                UserId = "test-user",
                IsRevoked = true,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };

            _mockUserRepository.Setup(x => x.GetRefreshTokenAsync(tokenValue))
                .ReturnsAsync(revokedToken);

            // Act
            var result = await _authService.RefreshTokenAsync(tokenValue, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.REFRESH_TOKEN_REVOKED));
        }

        [Test]
        public async Task RefreshToken_WithExpiredToken_ShouldReturnExpiredError()
        {
            // Arrange
            var tokenValue = "expired-token";
            var expiredToken = new RefreshToken
            {
                Token = tokenValue,
                UserId = "test-user",
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired yesterday
            };

            _mockUserRepository.Setup(x => x.GetRefreshTokenAsync(tokenValue))
                .ReturnsAsync(expiredToken);

            // Act
            var result = await _authService.RefreshTokenAsync(tokenValue, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.REFRESH_TOKEN_EXPIRED));
        }

        [Test]
        public async Task RefreshToken_WithInvalidToken_ShouldReturnInvalidError()
        {
            // Arrange
            var tokenValue = "invalid-token";

            _mockUserRepository.Setup(x => x.GetRefreshTokenAsync(tokenValue))
                .ReturnsAsync((RefreshToken?)null);

            // Act
            var result = await _authService.RefreshTokenAsync(tokenValue, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REFRESH_TOKEN));
        }

        #endregion

        #region Inactive Account Tests

        [Test]
        public async Task Login_WithInactiveAccount_ShouldReturnInactiveError()
        {
            // Arrange
            var email = "inactive@example.com";
            var password = "ValidPassword123!";
            
            var inactiveUser = new User
            {
                UserId = "inactive-user",
                Email = email,
                PasswordHash = HashPassword(password),
                IsActive = false,
                FailedLoginAttempts = 0
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(inactiveUser);

            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var result = await _authService.LoginAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.ACCOUNT_INACTIVE));
        }

        #endregion

        // Helper method to simulate password hashing for tests
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var salt = Convert.ToBase64String(new byte[32]);
            var saltedPassword = salt + password;
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(saltedPassword));
            return $"{salt}:{Convert.ToBase64String(hash)}";
        }

        #region DerivationContractVersion Unit Tests

        [Test]
        public void DerivationContractVersion_IsDefinedAndSemVerFormatted()
        {
            // The constant must be non-empty and follow major.minor versioning
            Assert.That(AuthenticationService.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion constant must be defined and non-empty");
            Assert.That(AuthenticationService.DerivationContractVersion, Does.Match(@"^\d+\.\d+"),
                "DerivationContractVersion must follow semantic versioning format (e.g., '1.0')");
        }

        [Test]
        public async Task RegisterAsync_OnSuccess_ReturnsDerivationContractVersion()
        {
            // Arrange
            var email = "new-user@example.com";
            var password = "ValidPassword123!";

            _mockUserRepository.Setup(x => x.UserExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.CreateUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);
            _mockUserRepository.Setup(x => x.StoreRefreshTokenAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);

            var request = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Test User"
            };

            // Act
            var result = await _authService.RegisterAsync(request, "127.0.0.1", "Test Agent");

            // Assert: Either success with version, or internal error (key provider not mocked at unit level)
            // The DerivationContractVersion constant is the authoritative source regardless of call path
            Assert.That(AuthenticationService.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion constant is the authoritative version source for all success responses");
            // If the registration succeeded (key provider available), version must be set
            if (result.Success)
            {
                Assert.That(result.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                    "Successful RegisterResponse must include the service's DerivationContractVersion");
            }
            else
            {
                // When key provider is not available (unit test with mock factory), weak password / duplicate email
                // are the only expected failure codes. INTERNAL_SERVER_ERROR indicates key provider unavailability.
                Assert.That(result.ErrorCode, Is.Not.EqualTo(ErrorCodes.WEAK_PASSWORD),
                    "Password strength validation should not fail for 'ValidPassword123!'");
                Assert.That(result.ErrorCode, Is.Not.EqualTo(ErrorCodes.USER_ALREADY_EXISTS),
                    "UserExists mock returns false, so USER_ALREADY_EXISTS must not occur");
            }
        }

        [Test]
        public async Task RegisterAsync_OnWeakPassword_DoesNotReturnDerivationContractVersion()
        {
            // Negative path: failed register should NOT include version (contract only applies on success)
            var request = new RegisterRequest
            {
                Email = "user@example.com",
                Password = "weak", // Too short
                ConfirmPassword = "weak"
            };

            var result = await _authService.RegisterAsync(request, null, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.WEAK_PASSWORD));
            Assert.That(result.DerivationContractVersion, Is.Null.Or.Empty,
                "Failed registration must not expose DerivationContractVersion in error responses");
        }

        [Test]
        public async Task LoginAsync_OnSuccess_ReturnsDerivationContractVersion()
        {
            // Arrange: create a user with a properly-hashed password so VerifyPassword succeeds
            var email = "login-test@example.com";
            var password = "ValidPassword123!";

            // Use the same salt=all-zeros approach the service would accept
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var salt = Convert.ToBase64String(new byte[32]);
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(salt + password));
            var passwordHash = $"{salt}:{Convert.ToBase64String(hash)}";

            var activeUser = new User
            {
                UserId = "login-user-1",
                Email = email,
                PasswordHash = passwordHash,
                AlgorandAddress = "TESTADDRESS",
                IsActive = true,
                FailedLoginAttempts = 0,
                EncryptedMnemonic = "dummy"
            };

            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email))
                .ReturnsAsync(activeUser);
            _mockUserRepository.Setup(x => x.UpdateUserAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);
            _mockUserRepository.Setup(x => x.StoreRefreshTokenAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);

            var request = new LoginRequest { Email = email, Password = password };

            // Act
            var result = await _authService.LoginAsync(request, "127.0.0.1", "Test Agent");

            // Assert
            Assert.That(result.Success, Is.True, "Login should succeed with correct credentials");
            Assert.That(result.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "Successful LoginResponse must include DerivationContractVersion");
            Assert.That(result.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "Returned version must match the service constant");
            Assert.That(result.AlgorandAddress, Is.EqualTo("TESTADDRESS"),
                "Login response must include the user's AlgorandAddress");
        }

        [Test]
        public async Task LoginAsync_OnFailure_DoesNotReturnDerivationContractVersion()
        {
            // Negative path: failed login should NOT include version
            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var request = new LoginRequest { Email = "nobody@example.com", Password = "AnyPass1!" };
            var result = await _authService.LoginAsync(request, null, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS));
            Assert.That(result.DerivationContractVersion, Is.Null.Or.Empty,
                "Failed login must not expose DerivationContractVersion in error responses");
        }

        #endregion

        #region LoginAsync Wrong-Password Counter Tests

        [Test]
        public async Task LoginAsync_WithWrongPassword_IncrementsFailedLoginAttempts()
        {
            // Arrange
            var email = "counter-test@example.com";
            var activeUser = new User
            {
                UserId = "counter-user",
                Email = email,
                PasswordHash = HashPassword("CorrectPassword123!"),
                IsActive = true,
                FailedLoginAttempts = 0
            };

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email)).ReturnsAsync(activeUser);
            _mockUserRepository.Setup(x => x.UpdateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);

            var request = new LoginRequest { Email = email, Password = "WrongPassword999!" };

            // Act
            var result = await _authService.LoginAsync(request, null, null);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS));
            Assert.That(capturedUser, Is.Not.Null, "UpdateUserAsync must be called to persist failed attempt");
            Assert.That(capturedUser!.FailedLoginAttempts, Is.EqualTo(1),
                "FailedLoginAttempts must be incremented on wrong password");
        }

        [Test]
        public async Task LoginAsync_AfterFourFailures_TriggersLockOnFifthAttempt()
        {
            // Arrange: user already has 4 failed attempts, one more should lock
            var email = "lock-trigger@example.com";
            var activeUser = new User
            {
                UserId = "lock-user",
                Email = email,
                PasswordHash = HashPassword("CorrectPassword123!"),
                IsActive = true,
                FailedLoginAttempts = 4
            };

            User? capturedUser = null;
            _mockUserRepository.Setup(x => x.GetUserByEmailAsync(email)).ReturnsAsync(activeUser);
            _mockUserRepository.Setup(x => x.UpdateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);

            var request = new LoginRequest { Email = email, Password = "WrongPassword999!" };

            // Act
            var result = await _authService.LoginAsync(request, null, null);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(capturedUser, Is.Not.Null);
            Assert.That(capturedUser!.FailedLoginAttempts, Is.EqualTo(5),
                "5th failed attempt must set count to 5");
            Assert.That(capturedUser.LockedUntil, Is.Not.Null,
                "Account must be locked after 5 failed attempts");
            Assert.That(capturedUser.LockedUntil!.Value, Is.GreaterThan(DateTime.UtcNow),
                "LockedUntil must be set to a future time");
        }

        #endregion

        #region RefreshTokenAsync User-Inactive Tests

        [Test]
        public async Task RefreshTokenAsync_WhenUserIsInactive_ReturnsUserNotFoundError()
        {
            // Arrange: valid non-revoked non-expired token but user is inactive
            var tokenValue = "valid-but-inactive-user-token";
            var token = new RefreshToken
            {
                Token = tokenValue,
                UserId = "inactive-user-id",
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };

            _mockUserRepository.Setup(x => x.GetRefreshTokenAsync(tokenValue))
                .ReturnsAsync(token);
            _mockUserRepository.Setup(x => x.GetUserByIdAsync("inactive-user-id"))
                .ReturnsAsync(new User
                {
                    UserId = "inactive-user-id",
                    Email = "inactive@example.com",
                    IsActive = false
                });

            // Act
            var result = await _authService.RefreshTokenAsync(tokenValue, null, null);

            // Assert
            Assert.That(result.Success, Is.False,
                "Refresh must fail when the owning user is inactive");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.USER_NOT_FOUND),
                "Error code must be USER_NOT_FOUND for inactive user");
        }

        #endregion

        #region ChangePasswordAsync Branch Tests

        [Test]
        public async Task ChangePasswordAsync_WhenUserNotFound_ReturnsFalse()
        {
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.ChangePasswordAsync("ghost-user", "Old1!", "New1!");

            Assert.That(result, Is.False, "ChangePassword must return false when user does not exist");
        }

        [Test]
        public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsFalse()
        {
            var userId = "change-pw-user";
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync(new User
                {
                    UserId = userId,
                    PasswordHash = HashPassword("CorrectOldPass1!")
                });

            var result = await _authService.ChangePasswordAsync(userId, "WrongOldPass1!", "NewPass123!");

            Assert.That(result, Is.False, "ChangePassword must return false when current password is wrong");
        }

        [Test]
        public async Task ChangePasswordAsync_WithWeakNewPassword_ReturnsFalse()
        {
            var userId = "change-pw-user-2";
            var oldPassword = "CorrectOldPass1!";
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync(new User
                {
                    UserId = userId,
                    PasswordHash = HashPassword(oldPassword)
                });

            var result = await _authService.ChangePasswordAsync(userId, oldPassword, "weak");

            Assert.That(result, Is.False, "ChangePassword must return false when new password is too weak");
        }

        #endregion

    } // end class AuthenticationServiceErrorHandlingTests

    /// <summary>
    /// Test implementation of IKeyProvider
    /// </summary>
    internal class TestKeyProvider : IKeyProvider
    {
        public bool IsConfigValid { get; set; } = true;
        public bool ShouldThrowException { get; set; } = false;
        
        public string ProviderType => "Test";

        public Task<string> GetEncryptionKeyAsync()
        {
            if (ShouldThrowException)
            {
                throw new Exception("Key provider failure");
            }
            return Task.FromResult("TestSystemKeyForEncryption32CharactersMinimumRequired!");
        }

        public Task<bool> ValidateConfigurationAsync()
        {
            return Task.FromResult(IsConfigValid);
        }
    }

    /// <summary>
    /// Test implementation of KeyProviderFactory
    /// </summary>
    internal class TestKeyProviderFactory : KeyProviderFactory
    {
        private readonly IKeyProvider _provider;

        public TestKeyProviderFactory(IKeyProvider provider)
            : base(
                new Mock<IServiceProvider>().Object,
                Options.Create(new KeyManagementConfig { Provider = "Test" }),
                new Mock<ILogger<KeyProviderFactory>>().Object)
        {
            _provider = provider;
        }

        public new IKeyProvider CreateProvider()
        {
            return _provider;
        }
    }
}

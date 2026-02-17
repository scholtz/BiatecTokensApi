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
    }

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

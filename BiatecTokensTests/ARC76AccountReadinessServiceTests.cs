using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for ARC76AccountReadinessService
    /// </summary>
    [TestFixture]
    public class ARC76AccountReadinessServiceTests
    {
        private Mock<IUserRepository> _mockUserRepository = null!;
        private Mock<IAuthenticationService> _mockAuthService = null!;
        private Mock<ILogger<ARC76AccountReadinessService>> _mockLogger = null!;
        private KeyProviderFactory _keyProviderFactory = null!;
        private ARC76AccountReadinessService _service = null!;

        [SetUp]
        public void Setup()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockLogger = new Mock<ILogger<ARC76AccountReadinessService>>();

            // Create a real KeyProviderFactory with mocked dependencies
            var services = new ServiceCollection();
            services.AddSingleton<HardcodedKeyProvider>();
            services.AddSingleton(Options.Create(new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = "test-key-for-unit-tests-32-characters-minimum"
            }));
            services.AddSingleton<ILogger<HardcodedKeyProvider>>(Mock.Of<ILogger<HardcodedKeyProvider>>());
            var serviceProvider = services.BuildServiceProvider();

            var keyManagementConfig = Options.Create(new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = "test-key-for-unit-tests-32-characters-minimum"
            });
            var keyFactoryLogger = Mock.Of<ILogger<KeyProviderFactory>>();

            _keyProviderFactory = new KeyProviderFactory(serviceProvider, keyManagementConfig, keyFactoryLogger);

            _service = new ARC76AccountReadinessService(
                _mockUserRepository.Object,
                _mockAuthService.Object,
                _mockLogger.Object,
                _keyProviderFactory);
        }

        [Test]
        public async Task CheckAccountReadinessAsync_UserNotFound_ReturnsNotInitialized()
        {
            // Arrange
            var userId = "non-existent-user";
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.NotInitialized));
            Assert.That(result.IsReady, Is.False);
            Assert.That(result.NotReadyReason, Does.Contain("not found"));
            Assert.That(result.RemediationSteps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Is.EqualTo("test-correlation-id"));
        }

        [Test]
        public async Task CheckAccountReadinessAsync_UserWithoutAlgorandAddress_ReturnsNotInitialized()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "", // Missing
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.NotInitialized));
            Assert.That(result.IsReady, Is.False);
            Assert.That(result.NotReadyReason, Does.Contain("not initialized"));
            Assert.That(result.RemediationSteps, Contains.Item("Initialize your ARC76 account through the authentication flow"));
        }

        [Test]
        public async Task CheckAccountReadinessAsync_UserWithoutEncryptedMnemonic_ReturnsFailed()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "", // Missing
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.Failed));
            Assert.That(result.IsReady, Is.False);
            Assert.That(result.AccountAddress, Is.EqualTo("ALGORAND_ADDRESS_123"));
            Assert.That(result.NotReadyReason, Does.Contain("credentials missing"));
            Assert.That(result.RemediationSteps, Contains.Item("Contact support for account recovery"));
        }

        [Test]
        public async Task CheckAccountReadinessAsync_FullyInitializedUser_KeyAccessible_ReturnsReady()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockAuthService.Setup(x => x.GetUserMnemonicForSigningAsync(userId))
                .ReturnsAsync("mnemonic-value");

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.Ready));
            Assert.That(result.IsReady, Is.True);
            Assert.That(result.AccountAddress, Is.EqualTo("ALGORAND_ADDRESS_123"));
            Assert.That(result.MetadataValidation, Is.Not.Null);
            Assert.That(result.MetadataValidation!.IsValid, Is.True);
            Assert.That(result.KeyStatus, Is.Not.Null);
            Assert.That(result.KeyStatus!.IsAccessible, Is.True);
        }

        [Test]
        public async Task CheckAccountReadinessAsync_KeyNotAccessible_ReturnsDegraded()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockAuthService.Setup(x => x.GetUserMnemonicForSigningAsync(userId))
                .ReturnsAsync((string?)null); // Key not accessible

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.Degraded));
            Assert.That(result.IsReady, Is.False);
            Assert.That(result.KeyStatus!.IsAccessible, Is.False);
            Assert.That(result.RemediationSteps, Contains.Item("Verify your authentication credentials"));
        }

        [Test]
        public async Task CheckAccountReadinessAsync_InactiveUser_ReturnsFailed()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = false, // Inactive
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockAuthService.Setup(x => x.GetUserMnemonicForSigningAsync(userId))
                .ReturnsAsync("mnemonic-value");

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.Failed));
            Assert.That(result.IsReady, Is.False);
            Assert.That(result.MetadataValidation!.IsValid, Is.False);
            Assert.That(result.MetadataValidation.ValidationErrors, Contains.Item("User account is not active"));
        }

        [Test]
        public async Task InitializeAccountAsync_UserNotFound_ReturnsFalse()
        {
            // Arrange
            var userId = "non-existent-user";
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.InitializeAccountAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task InitializeAccountAsync_AlreadyInitialized_ReturnsTrue()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockAuthService.Setup(x => x.GetUserMnemonicForSigningAsync(userId))
                .ReturnsAsync("mnemonic-value");

            // Act
            var result = await _service.InitializeAccountAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task ValidateAccountIntegrityAsync_UserNotFound_ReturnsFalse()
        {
            // Arrange
            var userId = "non-existent-user";
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.ValidateAccountIntegrityAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateAccountIntegrityAsync_ValidUser_ReturnsTrue()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockAuthService.Setup(x => x.GetUserMnemonicForSigningAsync(userId))
                .ReturnsAsync("mnemonic-value");

            // Act
            var result = await _service.ValidateAccountIntegrityAsync(userId);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task ValidateAccountIntegrityAsync_KeyNotAccessible_ReturnsFalse()
        {
            // Arrange
            var userId = "test-user";
            var user = new User
            {
                UserId = userId,
                Email = "test@example.com",
                AlgorandAddress = "ALGORAND_ADDRESS_123",
                EncryptedMnemonic = "encrypted-mnemonic",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _mockAuthService.Setup(x => x.GetUserMnemonicForSigningAsync(userId))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _service.ValidateAccountIntegrityAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task GetReadinessStateAsync_NewUser_ReturnsNotInitialized()
        {
            // Arrange
            var userId = "new-user";

            // Act
            var state = await _service.GetReadinessStateAsync(userId);

            // Assert
            Assert.That(state, Is.EqualTo(ARC76ReadinessState.NotInitialized));
        }

        [Test]
        public async Task CheckAccountReadinessAsync_Exception_ReturnsFailed()
        {
            // Arrange
            var userId = "test-user";
            _mockUserRepository.Setup(x => x.GetUserByIdAsync(userId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.CheckAccountReadinessAsync(userId, "test-correlation-id");

            // Assert
            Assert.That(result.State, Is.EqualTo(ARC76ReadinessState.Failed));
            Assert.That(result.IsReady, Is.False);
            Assert.That(result.NotReadyReason, Does.Contain("Error checking account readiness"));
        }
    }
}

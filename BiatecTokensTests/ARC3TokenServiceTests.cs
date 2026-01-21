using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AlgorandAuthenticationV2;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ARC3TokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<ILogger<ARC3TokenService>> _loggerMock;
        private Mock<IIPFSRepository> _ipfsRepositoryMock;
        private Mock<IASATokenService> _asaTokenServiceMock;
        private AlgorandAuthenticationOptionsV2 _algoConfig;

        [SetUp]
        public void Setup()
        {
            _algoConfig = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new AllowedNetworks()
            };

            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _configMock.Setup(x => x.CurrentValue).Returns(_algoConfig);

            _loggerMock = new Mock<ILogger<ARC3TokenService>>();
            _ipfsRepositoryMock = new Mock<IIPFSRepository>();
            _asaTokenServiceMock = new Mock<IASATokenService>();
        }

        #region ARC3 FNFT Validation Tests

        [Test]
        public void ValidateARC3Request_ValidFractionalNFT_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = "Test Metadata"
                }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
        }

        [Test]
        public void ValidateARC3Request_NullRequest_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            ARC3FractionalNonFungibleTokenDeploymentRequest? request = null;

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object?[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ValidateARC3Request_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateARC3Request_EmptyUnitName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unit name is required"));
        }

        [Test]
        public void ValidateARC3Request_ZeroTotalSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 0,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Total supply must be greater than 0"));
        }

        [Test]
        public void ValidateARC3Request_DecimalsExceedsLimit_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 20,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Decimals cannot exceed 19"));
        }

        [Test]
        public void ValidateARC3Request_NullMetadata_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = null
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Metadata is required"));
        }

        [Test]
        public void ValidateARC3Request_MetadataNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = new string('A', 33) // 33 characters, max is 32
                }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Metadata name").And.Contain("32 characters"));
        }

        [Test]
        public void ValidateARC3Request_UnitNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TOOLONGNAME", // More than 8 characters
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unit name").And.Contain("8 characters"));
        }

        #endregion

        #region ARC3 FT Validation Tests

        [Test]
        public void ValidateARC3Request_ValidFungibleToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test Metadata" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FT }));
        }

        [Test]
        public void ValidateARC3Request_FT_NullMetadata_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0",
                Metadata = null
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Metadata is required"));
        }

        #endregion

        #region ARC3 NFT Validation Tests

        [Test]
        public void ValidateARC3Request_ValidNFT_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "TNFT",
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test Metadata" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_NFT }));
        }

        [Test]
        public void ValidateARC3Request_NFT_NullMetadata_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "TNFT",
                Network = "testnet-v1.0",
                Metadata = null
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ARC3_NFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Metadata is required"));
        }

        #endregion

        #region Metadata Validation Tests

        [Test]
        public void ValidateMetadata_ValidMetadata_ReturnsTrue()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Description = "Test Description"
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ErrorMessage, Is.Null);
        }

        [Test]
        public void ValidateMetadata_InvalidBackgroundColor_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                BackgroundColor = "GGGGGG" // Invalid hex
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Background color").And.Contain("hexadecimal"));
        }

        [Test]
        public void ValidateMetadata_ValidBackgroundColor_ReturnsTrue()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                BackgroundColor = "FF5733" // Valid hex
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateMetadata_InvalidImageMimeType_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                ImageMimetype = "application/pdf" // Should start with "image/"
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Image MIME type").And.Contain("image/"));
        }

        [Test]
        public void ValidateMetadata_ValidImageMimeType_ReturnsTrue()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                ImageMimetype = "image/png"
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void ValidateMetadata_LocalizationMissingUri_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = null,
                    Default = "en",
                    Locales = new List<string> { "en", "fr" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Localization URI is required"));
        }

        [Test]
        public void ValidateMetadata_LocalizationUriMissingPlaceholder_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = "https://example.com/metadata",
                    Default = "en",
                    Locales = new List<string> { "en", "fr" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Localization URI").And.Contain("{locale}"));
        }

        [Test]
        public void ValidateMetadata_LocalizationMissingDefault_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = "https://example.com/{locale}/metadata",
                    Default = null,
                    Locales = new List<string> { "en", "fr" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Default locale is required"));
        }

        [Test]
        public void ValidateMetadata_LocalizationMissingLocales_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = "https://example.com/{locale}/metadata",
                    Default = "en",
                    Locales = null
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("At least one locale"));
        }

        [Test]
        public void ValidateMetadata_ValidLocalization_ReturnsTrue()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = "https://example.com/{locale}/metadata",
                    Default = "en",
                    Locales = new List<string> { "en", "fr", "es" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Unsupported Token Type Tests

        [Test]
        public void ValidateARC3Request_UnsupportedTokenType_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ERC20_Mintable }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unsupported token type"));
        }

        [Test]
        public void CreateARC3TokenAsync_UnsupportedTokenType_ThrowsException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<Exception>(async () =>
                await service.CreateARC3TokenAsync(request, TokenType.ERC20_Mintable));
            
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateARC3Request_MaxDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 19, // Maximum
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
        }

        [Test]
        public void ValidateARC3Request_ZeroDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 0, // Minimum
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
        }

        [Test]
        public void ValidateARC3Request_MaxMetadataNameLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = new string('A', 32) // Exactly 32 characters
                }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
        }

        [Test]
        public void ValidateARC3Request_MaxUnitNameLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TESTNAME", // Exactly 8 characters
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
        }

        #endregion

        #region Helper Methods

        private ARC3TokenService CreateServiceWithoutNetworkValidation()
        {
            try
            {
                return new ARC3TokenService(
                    _configMock.Object,
                    _loggerMock.Object,
                    _ipfsRepositoryMock.Object,
                    _asaTokenServiceMock.Object
                );
            }
            catch
            {
                // Return null if constructor fails, tests will handle
                return null!;
            }
        }

        #endregion
    }
}

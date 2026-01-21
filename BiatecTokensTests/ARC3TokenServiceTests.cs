using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AlgorandAuthenticationV2;
using Algorand.Algod;

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

        #region Metadata Validation Tests

        [Test]
        public void ValidateMetadata_ValidMetadata_ReturnsTrue()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Description = "Test Description",
                Image = "ipfs://QmTest"
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
                BackgroundColor = "ZZZZZZ" // Invalid hex
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Background color"));
        }

        [Test]
        public void ValidateMetadata_BackgroundColorWithHash_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                BackgroundColor = "#FFFFFF" // Should not have #
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("hexadecimal without #"));
        }

        [Test]
        public void ValidateMetadata_InvalidImageMimeType_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                ImageMimetype = "text/plain" // Must start with "image/"
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("image/"));
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
                    Uri = "", // Required
                    Default = "en",
                    Locales = new List<string> { "en", "es" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("URI is required"));
        }

        [Test]
        public void ValidateMetadata_LocalizationMissingLocalePlaceholder_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = "ipfs://QmTest/metadata", // Missing {locale}
                    Default = "en",
                    Locales = new List<string> { "en", "es" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("{locale}"));
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
                    Uri = "ipfs://QmTest/{locale}",
                    Default = "", // Required
                    Locales = new List<string> { "en", "es" }
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Default locale is required"));
        }

        [Test]
        public void ValidateMetadata_LocalizationEmptyLocales_ReturnsFalse()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test Token",
                Localization = new ARC3TokenLocalization
                {
                    Uri = "ipfs://QmTest/{locale}",
                    Default = "en",
                    Locales = new List<string>() // At least one required
                }
            };

            // Act
            var result = service.ValidateMetadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("At least one locale"));
        }

        #endregion

        #region ARC3 Fractional NFT Validation Tests

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
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = "Test Token Metadata"
                }
            };

            // Act
            var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT }));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTEmptyName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTEmptyUnitName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "",
                TotalSupply = 1000,
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("Unit name"));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTZeroSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 0,
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("Total supply must be greater than 0"));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTDecimalsTooHigh_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 20, // Max is 19
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("19"));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTMissingMetadata_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = null! // Required
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("Metadata is required"));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTMetadataNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = new string('A', 33) // Max is 32
                }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("32 characters"));
        }

        [Test]
        public void ValidateARC3Request_FractionalNFTUnitNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TOOLONGNAME", // Max is 8
                TotalSupply = 1000,
                Decimals = 2,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("8 characters"));
        }

        #endregion

        #region ARC3 Fungible Token Validation Tests

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
                Metadata = new ARC3TokenMetadata
                {
                    Name = "Test Token Metadata"
                }
            };

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FT });
            });
        }

        [Test]
        public void ValidateARC3Request_FungibleTokenMissingMetadata_ThrowsArgumentException()
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
                Metadata = null!
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FT });
            });
            Assert.That(ex.Message, Does.Contain("Metadata is required"));
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
                UnitName = "NFT",
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata
                {
                    Name = "Test NFT Metadata"
                }
            };

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_NFT });
            });
        }

        [Test]
        public void ValidateARC3Request_NFTMissingMetadata_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "NFT",
                Network = "testnet-v1.0",
                Metadata = null!
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_NFT });
            });
            Assert.That(ex.Message, Does.Contain("Metadata is required"));
        }

        #endregion

        #region Invalid Request Type Tests

        [Test]
        public void ValidateARC3Request_WrongRequestType_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ARC3_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("Invalid request type"));
        }

        [Test]
        public void ValidateARC3Request_UnsupportedTokenType_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Network = "testnet-v1.0",
                Metadata = new ARC3TokenMetadata { Name = "Test" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var method = typeof(ARC3TokenService).GetMethod("ValidateARC3Request",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ERC20_Mintable });
            });
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
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
                    _asaTokenServiceMock.Object);
            }
            catch
            {
                return null!;
            }
        }

        #endregion
    }
}

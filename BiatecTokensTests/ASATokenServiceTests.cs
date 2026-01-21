using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AlgorandAuthenticationV2;
using Algorand.Algod;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ASATokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ARC3TokenService>> _loggerMock;
        private AlgorandAuthenticationOptionsV2 _algoConfig;
        private AppConfiguration _appConfig;

        [SetUp]
        public void Setup()
        {
            _algoConfig = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new AllowedNetworks()
            };

            _appConfig = new AppConfiguration
            {
                Account = "test-account"
            };

            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _configMock.Setup(x => x.CurrentValue).Returns(_algoConfig);

            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);

            _loggerMock = new Mock<ILogger<ARC3TokenService>>();
        }

        #region ASA Fungible Token Validation Tests

        [Test]
        public void ValidateASARequest_ValidFungibleToken_DoesNotThrow()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert - service creation will fail due to network connection, but validation happens before
            Assert.DoesNotThrow(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                // Access private ValidateASARequest method via reflection
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
        }

        [Test]
        public void ValidateASARequest_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateASARequest_NullName_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = null!,
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateASARequest_EmptyUnitName_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("Unit name"));
        }

        [Test]
        public void ValidateASARequest_UnitNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "VERYLONGNAME", // 12 characters
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("8 characters"));
        }

        [Test]
        public void ValidateASARequest_ZeroTotalSupply_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 0,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("Total supply must be greater than 0"));
        }

        [Test]
        public void ValidateASARequest_DecimalsTooHigh_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 20, // Max is 19
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("19"));
        }

        #endregion

        #region ASA Fractional NFT Validation Tests

        [Test]
        public void ValidateASARequest_ValidFractionalNFT_DoesNotThrow()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test FNFT",
                UnitName = "FNFT",
                TotalSupply = 100,
                Decimals = 2,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FNFT });
            });
        }

        [Test]
        public void ValidateASARequest_FractionalNFTEmptyName_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "FNFT",
                TotalSupply = 100,
                Decimals = 2,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FNFT });
            });
            Assert.That(ex.Message, Does.Contain("Token name is required"));
        }

        #endregion

        #region ASA NFT Validation Tests

        [Test]
        public void ValidateASARequest_ValidNFT_DoesNotThrow()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "NFT",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_NFT });
            });
        }

        [Test]
        public void ValidateASARequest_NFTEmptyName_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "NFT",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_NFT });
            });
            Assert.That(ex.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateASARequest_NFTUnitNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "TOOLONGNAME",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_NFT });
            });
            Assert.That(ex.Message, Does.Contain("8 characters"));
        }

        #endregion

        #region Invalid Request Type Tests

        [Test]
        public void ValidateASARequest_WrongRequestTypeForFungible_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT });
            });
            Assert.That(ex.Message, Does.Contain("Invalid request type"));
        }

        [Test]
        public void ValidateASARequest_UnsupportedTokenType_ThrowsArgumentException()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var service = CreateServiceWithoutNetworkValidation();
                var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                // Use an unsupported token type
                method?.Invoke(service, new object[] { request, TokenType.ERC20_Mintable });
            });
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region Helper Methods

        private ASATokenService CreateServiceWithoutNetworkValidation()
        {
            // Create a service that skips network validation by using empty config
            try
            {
                return new ASATokenService(_configMock.Object, _appConfigMock.Object, _loggerMock.Object);
            }
            catch
            {
                // If constructor fails due to network issues, return null
                // Tests will use reflection to call ValidateASARequest directly
                return null!;
            }
        }

        #endregion
    }
}

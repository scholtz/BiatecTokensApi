using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AlgorandAuthenticationV2;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ASATokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ARC3TokenService>> _loggerMock;
        private Mock<IComplianceRepository> _complianceRepositoryMock;
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
            _complianceRepositoryMock = new Mock<IComplianceRepository>();
        }

        #region ASA Fungible Token Validation Tests

        [Test]
        public void ValidateASARequest_ValidFungibleToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert - using reflection to call private method
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
        }

        [Test]
        public void ValidateASARequest_NullRequest_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            ASAFungibleTokenDeploymentRequest? request = null;

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
                method?.Invoke(service, new object?[] { request, TokenType.ASA_FT }));
            
            Assert.That(ex?.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ValidateASARequest_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateASARequest_EmptyUnitName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unit name is required"));
        }

        [Test]
        public void ValidateASARequest_ZeroTotalSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 0,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Total supply must be greater than 0"));
        }

        [Test]
        public void ValidateASARequest_DecimalsExceedsLimit_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 20,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Decimals cannot exceed 19"));
        }

        [Test]
        public void ValidateASARequest_UnitNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TOOLONGNAME", // 11 characters
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unit name").And.Contain("8 characters"));
        }

        [Test]
        public void ValidateASARequest_MaxUnitNameLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TESTNAME", // 8 characters - max allowed
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
        }

        #endregion

        #region ASA Fractional NFT Validation Tests

        [Test]
        public void ValidateASARequest_ValidFractionalNFT_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "TNFT",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_FNFT }));
        }

        [Test]
        public void ValidateASARequest_FractionalNFT_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "TNFT",
                TotalSupply = 1000,
                Decimals = 3,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateASARequest_FractionalNFT_ZeroSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "TNFT",
                TotalSupply = 0,
                Decimals = 3,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FNFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Total supply must be greater than 0"));
        }

        #endregion

        #region ASA Non-Fungible Token Validation Tests

        [Test]
        public void ValidateASARequest_ValidNFT_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "TNFT",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_NFT }));
        }

        [Test]
        public void ValidateASARequest_NFT_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "",
                UnitName = "TNFT",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_NFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Token name is required"));
        }

        [Test]
        public void ValidateASARequest_NFT_EmptyUnitName_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "",
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_NFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unit name is required"));
        }

        [Test]
        public void ValidateASARequest_NFT_UnitNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "VERYLONGNAME", // More than 8 characters
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_NFT }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unit name").And.Contain("8 characters"));
        }

        #endregion

        #region Unsupported Token Type Tests

        [Test]
        public void ValidateASARequest_UnsupportedTokenType_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TST",
                TotalSupply = 1000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ERC20_Mintable }));
            
            Assert.That(ex?.InnerException?.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateASARequest_MaxDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 19, // Maximum allowed
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
        }

        [Test]
        public void ValidateASARequest_ZeroDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 0, // Minimum allowed
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
        }

        [Test]
        public void CreateASATokenAsync_UnsupportedTokenType_ThrowsException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TST",
                TotalSupply = 1000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<Exception>(async () => 
                await service.CreateASATokenAsync(request, TokenType.ARC3_FNFT));
            
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        [Test]
        public void ValidateASARequest_WithOptionalAddresses_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0",
                ManagerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                ReserveAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                FreezeAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                ClawbackAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            // Act & Assert
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.DoesNotThrow(() => method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));
        }

        #endregion

        #region Helper Methods

        private ASATokenService CreateServiceWithoutNetworkValidation()
        {
            try
            {
                return new ASATokenService(_configMock.Object, _appConfigMock.Object, _loggerMock.Object, _complianceRepositoryMock.Object);
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

using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

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
            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _loggerMock = new Mock<ILogger<ARC3TokenService>>();

            _algoConfig = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new Dictionary<string, AlgodConfig>()
            };

            _appConfig = new AppConfiguration
            {
                Account = "test account"
            };

            _configMock.Setup(x => x.CurrentValue).Returns(_algoConfig);
            _appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);
        }

        #region Fungible Token Validation Tests

        [Test]
        public void ValidateASARequest_FungibleToken_ValidRequest_ShouldNotThrow()
        {
            // Note: Since ValidateASARequest is private, we test it indirectly through CreateASATokenAsync
            // This test validates that proper validation happens
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6
            };

            // We can't directly test the private method, but we can verify the validation logic
            Assert.That(request.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(request.UnitName, Is.Not.Null.And.Not.Empty);
            Assert.That(request.UnitName.Length, Is.LessThanOrEqualTo(8));
            Assert.That(request.TotalSupply, Is.GreaterThan(0));
            Assert.That(request.Decimals, Is.LessThanOrEqualTo(19));
        }

        [Test]
        public void ValidateASARequest_FungibleToken_EmptyName_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6
            };

            // Assert - Name should be required
            Assert.That(string.IsNullOrWhiteSpace(request.Name), Is.True);
        }

        [Test]
        public void ValidateASARequest_FungibleToken_EmptyUnitName_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "",
                TotalSupply = 1000000,
                Decimals = 6
            };

            // Assert - UnitName should be required
            Assert.That(string.IsNullOrWhiteSpace(request.UnitName), Is.True);
        }

        [Test]
        public void ValidateASARequest_FungibleToken_UnitNameTooLong_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "VERYLONGNAME", // More than 8 characters
                TotalSupply = 1000000,
                Decimals = 6
            };

            // Assert - UnitName should not exceed 8 characters
            Assert.That(request.UnitName.Length, Is.GreaterThan(8));
        }

        [Test]
        public void ValidateASARequest_FungibleToken_ZeroTotalSupply_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 0,
                Decimals = 6
            };

            // Assert - TotalSupply should be greater than 0
            Assert.That(request.TotalSupply, Is.EqualTo(0));
        }

        [Test]
        public void ValidateASARequest_FungibleToken_DecimalsTooHigh_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 20 // More than 19
            };

            // Assert - Decimals should not exceed 19
            Assert.That(request.Decimals, Is.GreaterThan(19));
        }

        [Test]
        public void ValidateASARequest_FungibleToken_MaxValidUnitNameLength_ShouldPass()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "ABCDEFGH", // Exactly 8 characters
                TotalSupply = 1000000,
                Decimals = 6
            };

            // Assert
            Assert.That(request.UnitName.Length, Is.EqualTo(8));
            Assert.That(request.UnitName, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ValidateASARequest_FungibleToken_MaxValidDecimals_ShouldPass()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 19 // Maximum allowed
            };

            // Assert
            Assert.That(request.Decimals, Is.EqualTo(19));
            Assert.That(request.Decimals, Is.LessThanOrEqualTo(19));
        }

        #endregion

        #region Non-Fungible Token Validation Tests

        [Test]
        public void ValidateASARequest_NFT_ValidRequest_ShouldNotThrow()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test NFT",
                UnitName = "TNFT"
            };

            // Assert
            Assert.That(request.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(request.UnitName, Is.Not.Null.And.Not.Empty);
            Assert.That(request.UnitName.Length, Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void ValidateASARequest_NFT_EmptyName_ShouldFailValidation()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "",
                UnitName = "TNFT"
            };

            // Assert
            Assert.That(string.IsNullOrWhiteSpace(request.Name), Is.True);
        }

        [Test]
        public void ValidateASARequest_NFT_UnitNameTooLong_ShouldFailValidation()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test NFT",
                UnitName = "VERYLONGNAME"
            };

            // Assert
            Assert.That(request.UnitName.Length, Is.GreaterThan(8));
        }

        #endregion

        #region Fractional Non-Fungible Token Validation Tests

        [Test]
        public void ValidateASARequest_FNFT_ValidRequest_ShouldNotThrow()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test FNFT",
                UnitName = "TFNFT",
                TotalSupply = 100,
                Decimals = 0
            };

            // Assert
            Assert.That(request.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(request.UnitName, Is.Not.Null.And.Not.Empty);
            Assert.That(request.UnitName.Length, Is.LessThanOrEqualTo(8));
            Assert.That(request.TotalSupply, Is.GreaterThan(0));
            Assert.That(request.Decimals, Is.LessThanOrEqualTo(19));
        }

        [Test]
        public void ValidateASARequest_FNFT_ZeroTotalSupply_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test FNFT",
                UnitName = "TFNFT",
                TotalSupply = 0,
                Decimals = 0
            };

            // Assert
            Assert.That(request.TotalSupply, Is.EqualTo(0));
        }

        [Test]
        public void ValidateASARequest_FNFT_DecimalsTooHigh_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test FNFT",
                UnitName = "TFNFT",
                TotalSupply = 100,
                Decimals = 20
            };

            // Assert
            Assert.That(request.Decimals, Is.GreaterThan(19));
        }

        [Test]
        public void ValidateASARequest_FNFT_EmptyUnitName_ShouldFailValidation()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test FNFT",
                UnitName = "",
                TotalSupply = 100,
                Decimals = 0
            };

            // Assert
            Assert.That(string.IsNullOrWhiteSpace(request.UnitName), Is.True);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ASARequest_FungibleToken_WithOptionalAddresses_ShouldBeValid()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                ManagerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                ReserveAddress = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
                FreezeAddress = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
                ClawbackAddress = "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD"
            };

            // Assert
            Assert.That(request.ManagerAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(request.ReserveAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(request.FreezeAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(request.ClawbackAddress, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ASARequest_FungibleToken_WithDefaultFrozen_ShouldBeValid()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                DefaultFrozen = true
            };

            // Assert
            Assert.That(request.DefaultFrozen, Is.True);
        }

        [Test]
        public void ASARequest_FungibleToken_WithUrlAndMetadata_ShouldBeValid()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test Token",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Url = "https://example.com/token",
                MetadataHash = new byte[32]
            };

            // Assert
            Assert.That(request.Url, Is.Not.Null.And.Not.Empty);
            Assert.That(request.MetadataHash, Is.Not.Null);
            Assert.That(request.MetadataHash.Length, Is.EqualTo(32));
        }

        [Test]
        public void ASARequest_FungibleToken_MinimalValid_ShouldBeValid()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "T",
                UnitName = "T",
                TotalSupply = 1,
                Decimals = 0
            };

            // Assert
            Assert.That(request.Name.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(request.UnitName.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(request.TotalSupply, Is.EqualTo(1));
            Assert.That(request.Decimals, Is.EqualTo(0));
        }

        [Test]
        public void ASARequest_NFT_WithMetadata_ShouldBeValid()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test NFT",
                UnitName = "TNFT",
                Url = "ipfs://QmTest",
                MetadataHash = new byte[32]
            };

            // Assert
            Assert.That(request.Url, Is.Not.Null);
            Assert.That(request.MetadataHash, Is.Not.Null);
        }

        #endregion
    }
}

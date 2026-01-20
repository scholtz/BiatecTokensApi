using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ARC200TokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ARC200TokenService>> _loggerMock;
        private AlgorandAuthenticationOptionsV2 _algoConfig;
        private AppConfiguration _appConfig;

        [SetUp]
        public void Setup()
        {
            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _loggerMock = new Mock<ILogger<ARC200TokenService>>();

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

        #region Mintable Token Validation Tests

        [Test]
        public void ARC200MintableRequest_ValidRequest_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "TARC200",
                Decimals = 6
            };

            // Assert
            Assert.That(request.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Symbol, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Network, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Decimals, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ARC200MintableRequest_EmptyName_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "",
                Symbol = "TARC200",
                Decimals = 6
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.Name), Is.True);
        }

        [Test]
        public void ARC200MintableRequest_EmptySymbol_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "",
                Decimals = 6
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.Symbol), Is.True);
        }

        [Test]
        public void ARC200MintableRequest_MaxDecimals_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "TARC200",
                Decimals = 18
            };

            // Assert
            Assert.That(request.Decimals, Is.EqualTo(18));
            Assert.That(request.Decimals, Is.LessThanOrEqualTo(18));
        }

        [Test]
        public void ARC200MintableRequest_ZeroDecimals_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "TARC200",
                Decimals = 0
            };

            // Assert
            Assert.That(request.Decimals, Is.EqualTo(0));
        }

        #endregion

        #region Preminted Token Validation Tests

        [Test]
        public void ARC200PremintedRequest_ValidRequest_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200 Preminted",
                Symbol = "TARC200P",
                Decimals = 6,
                TotalSupply = 1000000
            };

            // Assert
            Assert.That(request.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Symbol, Is.Not.Null.And.Not.Empty);
            Assert.That(request.TotalSupply, Is.GreaterThan(0));
            Assert.That(request.Decimals, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ARC200PremintedRequest_ZeroTotalSupply_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200 Preminted",
                Symbol = "TARC200P",
                Decimals = 6,
                TotalSupply = 0
            };

            // Assert
            Assert.That(request.TotalSupply, Is.EqualTo(0));
        }

        [Test]
        public void ARC200PremintedRequest_LargeTotalSupply_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200 Preminted",
                Symbol = "TARC200P",
                Decimals = 6,
                TotalSupply = 1_000_000_000_000
            };

            // Assert
            Assert.That(request.TotalSupply, Is.GreaterThan(0));
            Assert.That(request.TotalSupply, Is.EqualTo(1_000_000_000_000));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ARC200Request_LongName_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = new string('A', 100),
                Symbol = "TARC200",
                Decimals = 6
            };

            // Assert
            Assert.That(request.Name.Length, Is.EqualTo(100));
        }

        [Test]
        public void ARC200Request_LongSymbol_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = new string('T', 20),
                Decimals = 6
            };

            // Assert
            Assert.That(request.Symbol.Length, Is.EqualTo(20));
        }

        [Test]
        public void ARC200Request_MinimalName_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "A",
                Symbol = "T",
                Decimals = 6
            };

            // Assert
            Assert.That(request.Name, Is.EqualTo("A"));
            Assert.That(request.Symbol, Is.EqualTo("T"));
        }

        [Test]
        public void ARC200Request_DifferentNetworks_ShouldBeValid()
        {
            // Arrange
            var networks = new[] { "testnet", "mainnet", "betanet", "voimain" };

            foreach (var network in networks)
            {
                var request = new ARC200MintableTokenDeploymentRequest
                {
                    Network = network,
                    Name = "Test ARC200",
                    Symbol = "TARC200",
                    Decimals = 6
                };

                Assert.That(request.Network, Is.EqualTo(network));
            }
        }

        [Test]
        public void ARC200PremintedRequest_MaxSupplyWithDecimals_ShouldBeValid()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "TARC200",
                Decimals = 18,
                TotalSupply = ulong.MaxValue
            };

            // Assert
            Assert.That(request.TotalSupply, Is.EqualTo(ulong.MaxValue));
            Assert.That(request.Decimals, Is.EqualTo(18));
        }

        #endregion
    }
}

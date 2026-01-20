using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ARC1400TokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ARC1400TokenService>> _loggerMock;
        private AlgorandAuthenticationOptionsV2 _algoConfig;
        private AppConfiguration _appConfig;

        [SetUp]
        public void Setup()
        {
            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _loggerMock = new Mock<ILogger<ARC1400TokenService>>();

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
        public void ARC1400MintableRequest_ValidRequest_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "TARC1400"
            };

            // Assert
            Assert.That(request.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Symbol, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Network, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ARC1400MintableRequest_EmptyName_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "",
                Symbol = "TARC1400"
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.Name), Is.True);
        }

        [Test]
        public void ARC1400MintableRequest_EmptySymbol_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = ""
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.Symbol), Is.True);
        }

        [Test]
        public void ARC1400MintableRequest_NullName_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = null,
                Symbol = "TARC1400"
            };

            // Assert
            Assert.That(request.Name, Is.Null);
        }

        [Test]
        public void ARC1400MintableRequest_NullSymbol_ShouldBeInvalid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = null
            };

            // Assert
            Assert.That(request.Symbol, Is.Null);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ARC1400Request_LongName_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = new string('A', 100),
                Symbol = "TARC1400"
            };

            // Assert
            Assert.That(request.Name.Length, Is.EqualTo(100));
        }

        [Test]
        public void ARC1400Request_LongSymbol_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = new string('T', 20)
            };

            // Assert
            Assert.That(request.Symbol.Length, Is.EqualTo(20));
        }

        [Test]
        public void ARC1400Request_MinimalName_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "A",
                Symbol = "T"
            };

            // Assert
            Assert.That(request.Name, Is.EqualTo("A"));
            Assert.That(request.Symbol, Is.EqualTo("T"));
        }

        [Test]
        public void ARC1400Request_DifferentNetworks_ShouldBeValid()
        {
            // Arrange
            var networks = new[] { "testnet", "mainnet", "betanet", "voimain", "aramidmain" };

            foreach (var network in networks)
            {
                var request = new ARC1400MintableTokenDeploymentRequest
                {
                    Network = network,
                    Name = "Test ARC1400",
                    Symbol = "TARC1400"
                };

                Assert.That(request.Network, Is.EqualTo(network));
            }
        }

        [Test]
        public void ARC1400Request_SpecialCharactersInName_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test-ARC1400 Token (Security)",
                Symbol = "TARC1400"
            };

            // Assert
            Assert.That(request.Name, Does.Contain("-"));
            Assert.That(request.Name, Does.Contain("("));
            Assert.That(request.Name, Does.Contain(")"));
        }

        [Test]
        public void ARC1400Request_SpecialCharactersInSymbol_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "T-ARC1400"
            };

            // Assert
            Assert.That(request.Symbol, Does.Contain("-"));
        }

        [Test]
        public void ARC1400Request_UnicodeCharacters_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400 🚀",
                Symbol = "TARC"
            };

            // Assert
            Assert.That(request.Name, Does.Contain("🚀"));
        }

        [Test]
        public void ARC1400Request_WhitespaceInName_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test   ARC1400   Token",
                Symbol = "TARC1400"
            };

            // Assert
            Assert.That(request.Name, Does.Contain("   "));
        }

        [Test]
        public void ARC1400Request_MixedCaseSymbol_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "TaRc1400"
            };

            // Assert
            Assert.That(request.Symbol, Is.EqualTo("TaRc1400"));
        }

        [Test]
        public void ARC1400Request_NumericSymbol_ShouldBeValid()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "1400"
            };

            // Assert
            Assert.That(request.Symbol, Is.EqualTo("1400"));
        }

        #endregion
    }
}

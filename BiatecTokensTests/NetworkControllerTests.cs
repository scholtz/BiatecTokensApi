using BiatecTokensApi.Configuration;
using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using AlgorandAuthenticationV2;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for NetworkController
    /// </summary>
    [TestFixture]
    public class NetworkControllerTests
    {
        private Mock<IOptions<EVMChains>> _mockEvmChainsOptions = null!;
        private Mock<IOptions<AlgorandAuthenticationOptionsV2>> _mockAlgorandOptions = null!;
        private Mock<ILogger<NetworkController>> _mockLogger = null!;
        private NetworkController _controller = null!;

        [SetUp]
        public void Setup()
        {
            _mockEvmChainsOptions = new Mock<IOptions<EVMChains>>();
            _mockAlgorandOptions = new Mock<IOptions<AlgorandAuthenticationOptionsV2>>();
            _mockLogger = new Mock<ILogger<NetworkController>>();

            // Setup default configurations
            var evmChains = new EVMChains
            {
                Chains = new List<EVMBlockchainConfig>
                {
                    new EVMBlockchainConfig
                    {
                        RpcUrl = "https://mainnet.base.org",
                        ChainId = 8453,
                        GasLimit = 4500000
                    },
                    new EVMBlockchainConfig
                    {
                        RpcUrl = "https://sepolia.base.org",
                        ChainId = 84532,
                        GasLimit = 4500000
                    }
                }
            };

            var algorandOptions = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new AllowedNetworks()
            };

            // Add network configurations dynamically
            algorandOptions.AllowedNetworks["wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8="] = CreateNetworkConfig("https://mainnet-api.4160.nodely.dev");
            algorandOptions.AllowedNetworks["SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI="] = CreateNetworkConfig("https://testnet-api.4160.nodely.dev");

            _mockEvmChainsOptions.Setup(x => x.Value).Returns(evmChains);
            _mockAlgorandOptions.Setup(x => x.Value).Returns(algorandOptions);

            _controller = new NetworkController(
                _mockEvmChainsOptions.Object,
                _mockAlgorandOptions.Object,
                _mockLogger.Object);
        }

        [Test]
        public void GetNetworks_ShouldReturnNetworks()
        {
            // Act
            var result = _controller.GetNetworks();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.InstanceOf<NetworkMetadataResponse>());

            var response = (NetworkMetadataResponse)okResult.Value!;
            Assert.That(response.Success, Is.True);
            Assert.That(response.Networks, Is.Not.Empty);
        }

        [Test]
        public void GetNetworks_ShouldIncludeAlgorandMainnet()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            var algorandMainnet = response.Networks.FirstOrDefault(n => 
                n.NetworkId == "algorand-mainnet");
            
            Assert.That(algorandMainnet, Is.Not.Null);
            Assert.That(algorandMainnet!.BlockchainType, Is.EqualTo("algorand"));
            Assert.That(algorandMainnet.IsMainnet, Is.True);
            Assert.That(algorandMainnet.IsRecommended, Is.True);
            Assert.That(algorandMainnet.DisplayName, Is.EqualTo("Algorand Mainnet"));
        }

        [Test]
        public void GetNetworks_ShouldIncludeAlgorandTestnet()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            var algorandTestnet = response.Networks.FirstOrDefault(n => 
                n.NetworkId == "algorand-testnet");
            
            Assert.That(algorandTestnet, Is.Not.Null);
            Assert.That(algorandTestnet!.BlockchainType, Is.EqualTo("algorand"));
            Assert.That(algorandTestnet.IsMainnet, Is.False);
            Assert.That(algorandTestnet.IsRecommended, Is.False);
            Assert.That(algorandTestnet.DisplayName, Is.EqualTo("Algorand Testnet"));
        }

        [Test]
        public void GetNetworks_ShouldIncludeBaseMainnet()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            var baseMainnet = response.Networks.FirstOrDefault(n => 
                n.NetworkId == "base-mainnet");
            
            Assert.That(baseMainnet, Is.Not.Null);
            Assert.That(baseMainnet!.BlockchainType, Is.EqualTo("evm"));
            Assert.That(baseMainnet.IsMainnet, Is.True);
            Assert.That(baseMainnet.IsRecommended, Is.True);
            Assert.That(baseMainnet.ChainId, Is.EqualTo(8453));
            Assert.That(baseMainnet.DisplayName, Is.EqualTo("Base Mainnet"));
        }

        [Test]
        public void GetNetworks_ShouldIncludeBaseSepolia()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            var baseSepolia = response.Networks.FirstOrDefault(n => 
                n.NetworkId == "base-sepolia");
            
            Assert.That(baseSepolia, Is.Not.Null);
            Assert.That(baseSepolia!.BlockchainType, Is.EqualTo("evm"));
            Assert.That(baseSepolia.IsMainnet, Is.False);
            Assert.That(baseSepolia.IsRecommended, Is.False);
            Assert.That(baseSepolia.ChainId, Is.EqualTo(84532));
            Assert.That(baseSepolia.DisplayName, Is.EqualTo("Base Sepolia Testnet"));
        }

        [Test]
        public void GetNetworks_ShouldPrioritizeMainnets()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert - Recommended networks should be first
            var firstNetwork = response.Networks.First();
            Assert.That(firstNetwork.IsRecommended, Is.True);
            Assert.That(firstNetwork.IsMainnet, Is.True);
        }

        [Test]
        public void GetNetworks_ShouldPopulateRecommendedNetworks()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            Assert.That(response.RecommendedNetworks, Is.Not.Empty);
            Assert.That(response.RecommendedNetworks, Does.Contain("algorand-mainnet"));
            Assert.That(response.RecommendedNetworks, Does.Contain("base-mainnet"));
            Assert.That(response.RecommendedNetworks, Does.Not.Contain("algorand-testnet"));
            Assert.That(response.RecommendedNetworks, Does.Not.Contain("base-sepolia"));
        }

        [Test]
        public void GetNetworks_WithEmptyConfiguration_ShouldReturnEmptyOrConfiguredNetworks()
        {
            // Arrange
            var emptyEvmChains = new EVMChains { Chains = new List<EVMBlockchainConfig>() };
            var emptyAlgorandOptions = new AlgorandAuthenticationOptionsV2 { AllowedNetworks = null };
            
            _mockEvmChainsOptions.Setup(x => x.Value).Returns(emptyEvmChains);
            _mockAlgorandOptions.Setup(x => x.Value).Returns(emptyAlgorandOptions);
            
            _controller = new NetworkController(
                _mockEvmChainsOptions.Object,
                _mockAlgorandOptions.Object,
                _mockLogger.Object);

            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            Assert.That(response.Success, Is.True);
            // With null AllowedNetworks and empty chains, should be empty
            Assert.That(response.Networks, Is.Empty);
            Assert.That(response.RecommendedNetworks, Is.Empty);
        }

        private static AlgodConfig CreateNetworkConfig(string server)
        {
            return new AlgodConfig
            {
                Server = server,
                Token = "",
                Header = ""
            };
        }

        [Test]
        public void GetNetworks_EVMNetworkShouldIncludeGasLimit()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            var baseMainnet = response.Networks.First(n => n.NetworkId == "base-mainnet");
            Assert.That(baseMainnet.Properties, Is.Not.Null);
            Assert.That(baseMainnet.Properties!.ContainsKey("gasLimit"), Is.True);
            Assert.That(baseMainnet.Properties["gasLimit"], Is.EqualTo(4500000));
        }

        [Test]
        public void GetNetworks_AlgorandNetworkShouldIncludeGenesisHash()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            var algorandMainnet = response.Networks.First(n => n.NetworkId == "algorand-mainnet");
            Assert.That(algorandMainnet.GenesisHash, Is.Not.Null);
            Assert.That(algorandMainnet.GenesisHash, Is.EqualTo("wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8="));
        }

        [Test]
        public void GetNetworks_ShouldIncludeTimestamp()
        {
            // Act
            var result = _controller.GetNetworks();
            var okResult = (OkObjectResult)result;
            var response = (NetworkMetadataResponse)okResult.Value!;

            // Assert
            Assert.That(response.Timestamp, Is.Not.EqualTo(default(DateTime)));
            Assert.That(response.Timestamp, Is.LessThanOrEqualTo(DateTime.UtcNow));
        }
    }
}

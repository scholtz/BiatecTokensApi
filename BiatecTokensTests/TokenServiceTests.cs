using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenServiceTests
    {
        private ERC20TokenService _tokenService;
        private Mock<IOptions<EVMBlockchainConfig>> _blockchainConfigMock;
        private Mock<ILogger<ERC20TokenService>> _loggerMock;
        private EVMBlockchainConfig _blockchainConfig;
        private ERC20TokenDeploymentRequest _validRequest;

        [SetUp]
        public void Setup()
        {
            // Set up configuration
            _blockchainConfig = new EVMBlockchainConfig
            {
                BaseRpcUrl = "https://mainnet.base.org",
                ChainId = 8453,
                GasLimit = 4500000
            };

            _blockchainConfigMock = new Mock<IOptions<EVMBlockchainConfig>>();
            _blockchainConfigMock.Setup(x => x.Value).Returns(_blockchainConfig);

            _loggerMock = new Mock<ILogger<ERC20TokenService>>();

            _tokenService = new ERC20TokenService(_blockchainConfigMock.Object, _loggerMock.Object);

            // Create a valid deployment request for testing
            _validRequest = new ERC20TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                InitialSupplyReceiver = null, // Will default to deployer
                DeployerPrivateKey = "0xabc123def456abc123def456abc123def456abc123def456abc123def456abcd"
            };
        }
    }
}
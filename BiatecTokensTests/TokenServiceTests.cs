using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
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
        private Mock<IOptionsMonitor<EVMChains>> _blockchainConfigMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ERC20TokenService>> _loggerMock;
        private EVMChains _blockchainConfig;
        private AppConfiguration _appConfig;
        private ERC20MintableTokenDeploymentRequest _validRequest;

        [SetUp]
        public void Setup()
        {
            // Set up configuration
            _blockchainConfig = new EVMChains
            {
                Chains = new List<EVMBlockchainConfig>
                {
                    new EVMBlockchainConfig
                    {
                        RpcUrl = "http://127.0.0.1:8545",
                        ChainId = 31337,
                        GasLimit = 4500000
                    }
                }
            };

            _appConfig = new AppConfiguration
            {
                Account = "test-account"
            };

            _blockchainConfigMock = new Mock<IOptionsMonitor<EVMChains>>();
            _blockchainConfigMock.Setup(x => x.CurrentValue).Returns(_blockchainConfig);

            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);

            _loggerMock = new Mock<ILogger<ERC20TokenService>>();

            _tokenService = new ERC20TokenService(_blockchainConfigMock.Object, _appConfigMock.Object, _loggerMock.Object);

            // Create a valid deployment request for testing
            _validRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337, // Required property
                Cap = 10000000, // Required for mintable tokens
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79" // Sample address
            };
        }
    }
}
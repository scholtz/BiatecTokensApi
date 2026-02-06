using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Numerics;
using System.Text;

namespace BiatecTokensTests
{
    [TestFixture]
    [Category("LocalBlockchainRequired")]
    public class Erc20TokenTests
    {
        // Get default Ganache accounts
        private readonly (string Owner, string User) _accounts = TestHelper.GetDefaultGanachePrivateKeys();
        
        private EVMChains _blockchainConfig;
        private AppConfiguration _appConfig;
        private Mock<ILogger<ERC20TokenService>> _loggerMock;
        private Mock<ITokenIssuanceRepository> _tokenIssuanceRepositoryMock;
        private Mock<IComplianceRepository> _complianceRepositoryMock;
        private ERC20TokenService _tokenService;
        private string _tokenContractAddress;
        private Web3 _web3Owner;
        private Web3 _web3User;
        private Contract _tokenContract;
        private Account _ownerAccount;
        private Account _userAccount;

        [OneTimeSetUp]
        public async Task InitialSetup()
        {
            // Check if Ganache is running
            if (!await TestHelper.IsLocalBlockchainRunning())
            {
                Assert.Fail(TestHelper.GetGanacheSetupInstructions());
            }

            // Configure for local blockchain
            _blockchainConfig = new EVMChains
            {
                Chains = new List<EVMBlockchainConfig>
                {
                    new EVMBlockchainConfig
                    {
                        RpcUrl = TestHelper.LocalBlockchainUrl,
                        ChainId = 31337, // Hardhat/modern Ganache chain ID
                        GasLimit = 10000000 // Increased gas limit for local deployment
                    }
                }
            };

            _appConfig = new AppConfiguration
            {
                Account = "test-account"
            };

            var configMock = new Mock<IOptionsMonitor<EVMChains>>();
            configMock.Setup(x => x.CurrentValue).Returns(_blockchainConfig);
            
            var appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);

            _loggerMock = new Mock<ILogger<ERC20TokenService>>();
            _tokenIssuanceRepositoryMock = new Mock<ITokenIssuanceRepository>();
            _complianceRepositoryMock = new Mock<IComplianceRepository>();
            
            var deploymentStatusServiceMock = new Mock<IDeploymentStatusService>();
            deploymentStatusServiceMock.Setup(x => x.CreateDeploymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            deploymentStatusServiceMock.Setup(x => x.UpdateDeploymentStatusAsync(
                It.IsAny<string>(), It.IsAny<DeploymentStatus>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(true);
            
            // Add mock authentication service and user repository for new dependencies
            var mockAuthService = Moq.Mock.Of<IAuthenticationService>();
            var mockUserRepo = Moq.Mock.Of<IUserRepository>();
            
            _tokenService = new ERC20TokenService(configMock.Object, appConfigMock.Object, _loggerMock.Object, _tokenIssuanceRepositoryMock.Object, _complianceRepositoryMock.Object, deploymentStatusServiceMock.Object, mockAuthService, mockUserRepo);

            // Setup accounts
            _ownerAccount = new Account(_accounts.Owner, 31337);
            _userAccount = new Account(_accounts.User, 31337);
            _web3Owner = new Web3(_ownerAccount, TestHelper.LocalBlockchainUrl);
            _web3User = new Web3(_userAccount, TestHelper.LocalBlockchainUrl);
            
            // Make sure we have enough ETH in the accounts
            var ownerBalance = await _web3Owner.Eth.GetBalance.SendRequestAsync(_ownerAccount.Address);
            var userBalance = await _web3User.Eth.GetBalance.SendRequestAsync(_userAccount.Address);
            
            Console.WriteLine($"Owner account {_ownerAccount.Address} balance: {Web3.Convert.FromWei(ownerBalance)} ETH");
            Console.WriteLine($"User account {_userAccount.Address} balance: {Web3.Convert.FromWei(userBalance)} ETH");
            
            if (ownerBalance.Value <= 0 || userBalance.Value <= 0)
            {
                Assert.Fail("Test accounts don't have enough ETH. Please use Ganache with default accounts.");
            }
            
            // Deploy the token using the TokenService (which now uses BiatecToken)
            var deploymentRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test BiatecToken",
                Symbol = "TEST",
                InitialSupply = 1000000, // 1 million tokens
                Decimals = 18,
                ChainId = 31337, // Required property
                Cap = 10000000, // 10 million tokens cap
                InitialSupplyReceiver = _ownerAccount.Address // Set explicit receiver
            };
        }
    }
}
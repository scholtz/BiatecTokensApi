using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
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
        
        private BlockchainConfig _blockchainConfig;
        private Mock<ILogger<TokenService>> _loggerMock;
        private TokenService _tokenService;
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
            _blockchainConfig = new BlockchainConfig
            {
                BaseRpcUrl = TestHelper.LocalBlockchainUrl,
                ChainId = 1337, // Default Ganache chain ID
                GasLimit = 6721975 // Default Ganache gas limit
            };

            var configMock = new Mock<IOptions<BlockchainConfig>>();
            configMock.Setup(x => x.Value).Returns(_blockchainConfig);
            _loggerMock = new Mock<ILogger<TokenService>>();
            
            _tokenService = new TokenService(configMock.Object, _loggerMock.Object);

            // Setup accounts
            _ownerAccount = new Account(_accounts.Owner, _blockchainConfig.ChainId);
            _userAccount = new Account(_accounts.User, _blockchainConfig.ChainId);
            _web3Owner = new Web3(_ownerAccount, _blockchainConfig.BaseRpcUrl);
            _web3User = new Web3(_userAccount, _blockchainConfig.BaseRpcUrl);
            
            // Make sure we have enough ETH in the accounts
            var ownerBalance = await _web3Owner.Eth.GetBalance.SendRequestAsync(_ownerAccount.Address);
            var userBalance = await _web3Owner.Eth.GetBalance.SendRequestAsync(_userAccount.Address);
            
            Console.WriteLine($"Owner account {_ownerAccount.Address} balance: {Web3.Convert.FromWei(ownerBalance)} ETH");
            Console.WriteLine($"User account {_userAccount.Address} balance: {Web3.Convert.FromWei(userBalance)} ETH");
            
            if (ownerBalance.Value <= 0 || userBalance.Value <= 0)
            {
                Assert.Fail("Test accounts don't have enough ETH. Please use Ganache with default accounts.");
            }
            
            // Deploy the token
            var deploymentRequest = new TokenDeploymentRequest
            {
                Name = "Test ERC20 Token",
                Symbol = "TEST",
                InitialSupply = 1000000, // 1 million tokens
                Decimals = 18,
                DeployerPrivateKey = _accounts.Owner
            };
            
            var deploymentResult = await _tokenService.DeployTokenAsync(deploymentRequest);
            
            Assert.That(deploymentResult.Success, Is.True, "Token deployment failed");
            _tokenContractAddress = deploymentResult.ContractAddress!;
            Console.WriteLine($"Token deployed at {_tokenContractAddress}");
            
            // Get contract instance
            _tokenContract = _web3Owner.Eth.GetContract(GetERC20ABI(), _tokenContractAddress);
        }

        [Test, Order(1)]
        public async Task Token_ShouldHaveCorrectNameAndSymbol()
        {
            // Get token name
            var nameFunction = _tokenContract.GetFunction("name");
            var name = await nameFunction.CallAsync<string>();
            
            // Get token symbol
            var symbolFunction = _tokenContract.GetFunction("symbol");
            var symbol = await symbolFunction.CallAsync<string>();
            
            // Get decimals
            var decimalsFunction = _tokenContract.GetFunction("decimals");
            var decimals = await decimalsFunction.CallAsync<byte>();
            
            // Assert basic token properties
            Assert.That(name, Is.EqualTo("Test ERC20 Token"));
            Assert.That(symbol, Is.EqualTo("TEST"));
            Assert.That(decimals, Is.EqualTo(18));
            
            Console.WriteLine($"Token name: {name}");
            Console.WriteLine($"Token symbol: {symbol}");
            Console.WriteLine($"Token decimals: {decimals}");
        }

        [Test, Order(2)]
        public async Task Owner_ShouldHaveInitialTokenBalance()
        {
            // Get total supply
            var totalSupplyFunction = _tokenContract.GetFunction("totalSupply");
            var totalSupply = await totalSupplyFunction.CallAsync<BigInteger>();
            
            // Get owner balance
            var balanceOfFunction = _tokenContract.GetFunction("balanceOf");
            var ownerBalance = await balanceOfFunction.CallAsync<BigInteger>(_ownerAccount.Address);
            
            // Convert the balance from wei (smallest unit) to tokens
            var ownerBalanceInTokens = Web3.Convert.FromWei(ownerBalance);
            var totalSupplyInTokens = Web3.Convert.FromWei(totalSupply);
            
            // Assert that owner has full initial supply
            AssertBigIntegerEqual(totalSupply, ownerBalance, "Owner should have the full total supply");
            
            // Check if the total supply is approximately 1 million (allows for small rounding errors)
            var expectedSupply = 1000000m;
            Assert.That(totalSupplyInTokens, Is.InRange(expectedSupply - 0.01m, expectedSupply + 0.01m));
            
            Console.WriteLine($"Total supply: {totalSupplyInTokens} TEST");
            Console.WriteLine($"Owner balance: {ownerBalanceInTokens} TEST");
        }

        [Test, Order(3)]
        public async Task Owner_ShouldBeAbleToTransferTokens()
        {
            // Get initial balances
            var balanceOfFunction = _tokenContract.GetFunction("balanceOf");
            var ownerInitialBalance = await balanceOfFunction.CallAsync<BigInteger>(_ownerAccount.Address);
            var userInitialBalance = await balanceOfFunction.CallAsync<BigInteger>(_userAccount.Address);
            
            Console.WriteLine($"Owner initial balance: {Web3.Convert.FromWei(ownerInitialBalance)} TEST");
            Console.WriteLine($"User initial balance: {Web3.Convert.FromWei(userInitialBalance)} TEST");
            
            // Transfer amount (100 tokens with 18 decimals)
            var transferAmount = Web3.Convert.ToWei(100);
            
            // Transfer tokens
            var transferFunction = _tokenContract.GetFunction("transfer");
            var transactionReceipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
                _ownerAccount.Address,
                null, // Gas price
                new HexBigInteger(_blockchainConfig.GasLimit),
                null, // Value (ETH)
                _userAccount.Address, // To address
                transferAmount // Amount
            );
            
            Console.WriteLine($"Transfer transaction hash: {transactionReceipt.TransactionHash}");
            
            // Get updated balances
            var ownerFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(_ownerAccount.Address);
            var userFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(_userAccount.Address);
            
            Console.WriteLine($"Owner final balance: {Web3.Convert.FromWei(ownerFinalBalance)} TEST");
            Console.WriteLine($"User final balance: {Web3.Convert.FromWei(userFinalBalance)} TEST");
            
            // Assert transaction was successful (status = 1)
            Assert.IsTrue(transactionReceipt.Status.Value == 1, "Transaction failed");
            
            // Use custom helper to compare BigInteger values
            var expectedOwnerBalance = ownerInitialBalance - transferAmount;
            var expectedUserBalance = userInitialBalance + transferAmount;
            
            AssertBigIntegerEqual(expectedOwnerBalance, ownerFinalBalance, "Owner balance wasn't reduced correctly");
            AssertBigIntegerEqual(expectedUserBalance, userFinalBalance, "User didn't receive correct amount of tokens");
        }

        [Test, Order(4)]
        public async Task User_ShouldBeAbleToApproveAndTransferFrom()
        {
            // Approve the owner to spend tokens on behalf of user
            var approveAmount = Web3.Convert.ToWei(50); // 50 tokens
            
            // Get approve function
            var approveFunction = _tokenContract.GetFunction("approve");
            
            // User approves owner to spend tokens
            var approvalReceipt = await approveFunction.SendTransactionAndWaitForReceiptAsync(
                _userAccount.Address, // From address
                null, // Gas price
                new HexBigInteger(_blockchainConfig.GasLimit),
                null, // Value (ETH)
                _ownerAccount.Address, // Spender address
                approveAmount // Amount
            );
            
            // Check allowance
            var allowanceFunction = _tokenContract.GetFunction("allowance");
            var allowance = await allowanceFunction.CallAsync<BigInteger>(_userAccount.Address, _ownerAccount.Address);
            
            Console.WriteLine($"Approval transaction hash: {approvalReceipt.TransactionHash}");
            Console.WriteLine($"Owner allowance: {Web3.Convert.FromWei(allowance)} TEST");
            
            // Assert approval was successful (status = 1)
            Assert.IsTrue(approvalReceipt.Status.Value == 1, "Approval transaction failed");
            AssertBigIntegerEqual(approveAmount, allowance, "Allowance wasn't set correctly");
            
            // Get initial balances
            var balanceOfFunction = _tokenContract.GetFunction("balanceOf");
            var userInitialBalance = await balanceOfFunction.CallAsync<BigInteger>(_userAccount.Address);
            var thirdPartyAddress = "0x823D8A3c39f4110715e8352715a2C2707D6Ef22b"; // Random third party address
            var thirdPartyInitialBalance = await balanceOfFunction.CallAsync<BigInteger>(thirdPartyAddress);
            
            Console.WriteLine($"User initial balance: {Web3.Convert.FromWei(userInitialBalance)} TEST");
            Console.WriteLine($"Third party initial balance: {Web3.Convert.FromWei(thirdPartyInitialBalance)} TEST");
            
            // Amount to transfer (25 tokens)
            var transferAmount = Web3.Convert.ToWei(25);
            
            // Owner transfers from user to third party
            var transferFromFunction = _tokenContract.GetFunction("transferFrom");
            var transferFromReceipt = await transferFromFunction.SendTransactionAndWaitForReceiptAsync(
                _ownerAccount.Address, // From address (the owner)
                null, // Gas price
                new HexBigInteger(_blockchainConfig.GasLimit),
                null, // Value (ETH)
                _userAccount.Address, // From which address to take tokens
                thirdPartyAddress, // To which address to send tokens
                transferAmount // Amount
            );
            
            Console.WriteLine($"TransferFrom transaction hash: {transferFromReceipt.TransactionHash}");
            
            // Get updated balances and allowance
            var userFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(_userAccount.Address);
            var thirdPartyFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(thirdPartyAddress);
            var finalAllowance = await allowanceFunction.CallAsync<BigInteger>(_userAccount.Address, _ownerAccount.Address);
            
            Console.WriteLine($"User final balance: {Web3.Convert.FromWei(userFinalBalance)} TEST");
            Console.WriteLine($"Third party final balance: {Web3.Convert.FromWei(thirdPartyFinalBalance)} TEST");
            Console.WriteLine($"Final allowance: {Web3.Convert.FromWei(finalAllowance)} TEST");
            
            // Assert transferFrom was successful (status = 1)
            Assert.IsTrue(transferFromReceipt.Status.Value == 1, "TransferFrom transaction failed");
            
            // Calculate expected values
            var expectedUserBalance = userInitialBalance - transferAmount;
            var expectedThirdPartyBalance = thirdPartyInitialBalance + transferAmount;
            var expectedAllowance = approveAmount - transferAmount;
            
            // Use custom assertions for BigInteger
            AssertBigIntegerEqual(expectedUserBalance, userFinalBalance, "User balance wasn't reduced correctly");
            AssertBigIntegerEqual(expectedThirdPartyBalance, thirdPartyFinalBalance, "Third party didn't receive the correct amount");
            AssertBigIntegerEqual(expectedAllowance, finalAllowance, "Allowance wasn't reduced correctly");
        }
        
        [Test, Order(5)]
        public async Task IncreaseAndDecreaseAllowance_ShouldWorkCorrectly()
        {
            // Initial setup
            var initialApproveAmount = Web3.Convert.ToWei(100); // 100 tokens
            
            // Get allowance function
            var allowanceFunction = _tokenContract.GetFunction("allowance");
            
            // Get approve function and approve initial amount
            var approveFunction = _tokenContract.GetFunction("approve");
            await approveFunction.SendTransactionAndWaitForReceiptAsync(
                _ownerAccount.Address,
                null,
                new HexBigInteger(_blockchainConfig.GasLimit),
                null,
                _userAccount.Address,
                initialApproveAmount
            );
            
            // Verify initial allowance
            var initialAllowance = await allowanceFunction.CallAsync<BigInteger>(_ownerAccount.Address, _userAccount.Address);
            Console.WriteLine($"Initial allowance: {Web3.Convert.FromWei(initialAllowance)} TEST");
            AssertBigIntegerEqual(initialApproveAmount, initialAllowance, "Initial allowance not set correctly");
            
            // Increase allowance
            var increaseAmount = Web3.Convert.ToWei(50); // 50 more tokens
            var increaseAllowanceFunction = _tokenContract.GetFunction("increaseAllowance");
            var increaseReceipt = await increaseAllowanceFunction.SendTransactionAndWaitForReceiptAsync(
                _ownerAccount.Address,
                null,
                new HexBigInteger(_blockchainConfig.GasLimit),
                null,
                _userAccount.Address,
                increaseAmount
            );
            
            // Verify increased allowance
            var increasedAllowance = await allowanceFunction.CallAsync<BigInteger>(_ownerAccount.Address, _userAccount.Address);
            Console.WriteLine($"Increased allowance: {Web3.Convert.FromWei(increasedAllowance)} TEST");
            Console.WriteLine($"Increase transaction hash: {increaseReceipt.TransactionHash}");
            
            var expectedIncreasedAllowance = initialAllowance + increaseAmount;
            AssertBigIntegerEqual(expectedIncreasedAllowance, increasedAllowance, "Allowance not increased correctly");
            
            // Decrease allowance
            var decreaseAmount = Web3.Convert.ToWei(30); // Decrease by 30 tokens
            var decreaseAllowanceFunction = _tokenContract.GetFunction("decreaseAllowance");
            var decreaseReceipt = await decreaseAllowanceFunction.SendTransactionAndWaitForReceiptAsync(
                _ownerAccount.Address,
                null,
                new HexBigInteger(_blockchainConfig.GasLimit),
                null,
                _userAccount.Address,
                decreaseAmount
            );
            
            // Verify final allowance
            var finalAllowance = await allowanceFunction.CallAsync<BigInteger>(_ownerAccount.Address, _userAccount.Address);
            Console.WriteLine($"Final allowance: {Web3.Convert.FromWei(finalAllowance)} TEST");
            Console.WriteLine($"Decrease transaction hash: {decreaseReceipt.TransactionHash}");
            
            var expectedFinalAllowance = initialAllowance + increaseAmount - decreaseAmount;
            AssertBigIntegerEqual(expectedFinalAllowance, finalAllowance, "Allowance not decreased correctly");
        }

        private string GetERC20ABI()
        {
            // Return the standard ERC20 ABI from the TokenService
            return @"[{""inputs"":[{""internalType"":""string"",""name"":""name_"",""type"":""string""},{""internalType"":""string"",""name"":""symbol_"",""type"":""string""},{""internalType"":""uint256"",""name"":""initialSupply"",""type"":""uint256""},{""internalType"":""uint8"",""name"":""decimals_"",""type"":""uint8""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""owner"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""spender"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""inputs"":[{""internalType"":""address"",""name"":""owner"",""type"":""address""},{""internalType"":""address"",""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""decimals"",""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""subtractedValue"",""type"":""uint256""}],""name"":""decreaseAllowance"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""addedValue"",""type"":""uint256""}],""name"":""increaseAllowance"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""name"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""symbol"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""totalSupply"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""}]";
        }
        
        /// <summary>
        /// Helper method to assert BigInteger equality
        /// </summary>
        private void AssertBigIntegerEqual(BigInteger expected, BigInteger actual, string message = null)
        {
            if (expected != actual)
            {
                Assert.Fail($"{message ?? "BigIntegers not equal"}: Expected {expected}, Actual {actual}");
            }
        }
    }
}
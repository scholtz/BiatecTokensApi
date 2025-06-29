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
                ChainId = 31337, // Hardhat/modern Ganache chain ID
                GasLimit = 10000000 // Increased gas limit for local deployment
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
            var userBalance = await _web3User.Eth.GetBalance.SendRequestAsync(_userAccount.Address);
            
            Console.WriteLine($"Owner account {_ownerAccount.Address} balance: {Web3.Convert.FromWei(ownerBalance)} ETH");
            Console.WriteLine($"User account {_userAccount.Address} balance: {Web3.Convert.FromWei(userBalance)} ETH");
            
            if (ownerBalance.Value <= 0 || userBalance.Value <= 0)
            {
                Assert.Fail("Test accounts don't have enough ETH. Please use Ganache with default accounts.");
            }
            
            // Deploy the token using the TokenService (which now uses BiatecToken)
            var deploymentRequest = new TokenDeploymentRequest
            {
                Name = "Test BiatecToken",
                Symbol = "TEST",
                InitialSupply = 1000000, // 1 million tokens
                Decimals = 18,
                DeployerPrivateKey = _accounts.Owner
            };
            
            var deploymentResult = await _tokenService.DeployTokenAsync(deploymentRequest);
            
            Assert.That(deploymentResult.Success, Is.True, "Token deployment failed");
            _tokenContractAddress = deploymentResult.ContractAddress!;
            Console.WriteLine($"BiatecToken deployed at {_tokenContractAddress}");
            
            // Get contract instance with BiatecToken ABI
            _tokenContract = _web3Owner.Eth.GetContract(GetBiatecTokenABI(), _tokenContractAddress);
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
            Assert.That(name, Is.EqualTo("Test BiatecToken"));
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
            
            // Transfer tokens with explicit transaction input and gas limit
            var transferFunction = _tokenContract.GetFunction("transfer");
            var transactionInput = transferFunction.CreateTransactionInput(
                _ownerAccount.Address,
                new HexBigInteger(100000), // Gas limit
                null, // Gas price
                new HexBigInteger(0), // Value (ETH)
                _userAccount.Address, // To address
                transferAmount // Amount
            );
            var txHash = await _web3Owner.Eth.TransactionManager.SendTransactionAsync(transactionInput);
            Console.WriteLine($"Transfer transaction hash: {txHash}");
            var transactionReceipt = await _web3Owner.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txHash);
            
            // Get updated balances
            var ownerFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(_ownerAccount.Address);
            var userFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(_userAccount.Address);
            
            Console.WriteLine($"Owner final balance: {Web3.Convert.FromWei(ownerFinalBalance)} TEST");
            Console.WriteLine($"User final balance: {Web3.Convert.FromWei(userFinalBalance)} TEST");
            
            // Assert balances are updated correctly
            Assert.That(transactionReceipt.Status?.Value, Is.EqualTo(new Nethereum.Hex.HexTypes.HexBigInteger(1).Value), "Transaction failed");
            Assert.IsTrue(transactionReceipt.Status?.Value == 1, "Transaction failed");
            
            // Use custom helper to compare BigInteger values
            var expectedOwnerBalance = ownerInitialBalance - transferAmount;
            var expectedUserBalance = userInitialBalance + transferAmount;
            
            AssertBigIntegerEqual(expectedOwnerBalance, ownerFinalBalance, "Owner balance wasn't reduced correctly");
            AssertBigIntegerEqual(expectedUserBalance, userFinalBalance, "User didn't receive correct amount of tokens");
        }

        [Test, Order(4)]
        public async Task User_ShouldBeAbleToApproveAndTransferFrom()
        {
            // Ensure user has enough tokens by transferring from owner
            var initialTransferAmount = Web3.Convert.ToWei(100); // 100 tokens
            var transferFunction = _tokenContract.GetFunction("transfer");
            var transferTxInput = transferFunction.CreateTransactionInput(
                _ownerAccount.Address,
                new HexBigInteger(100000),
                null,
                new HexBigInteger(0),
                _userAccount.Address,
                initialTransferAmount
            );
            var transferTxHash = await _web3Owner.Eth.TransactionManager.SendTransactionAsync(transferTxInput);
            await _web3Owner.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(transferTxHash);

            // Approve the owner to spend tokens on behalf of user
            var approveAmount = Web3.Convert.ToWei(50); // 50 tokens
            var approveFunction = _tokenContract.GetFunction("approve");
            var approveTxInput = approveFunction.CreateTransactionInput(
                _userAccount.Address, // From address
                new HexBigInteger(100000), // Gas limit
                null, // Gas price
                new HexBigInteger(0), // Value (ETH)
                _ownerAccount.Address, // Spender address
                approveAmount // Amount
            );
            var approveTxHash = await _web3User.Eth.TransactionManager.SendTransactionAsync(approveTxInput);
            var approvalReceipt = await _web3User.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(approveTxHash);
            
            // Check allowance
            var allowanceFunction = _tokenContract.GetFunction("allowance");
            var allowance = await allowanceFunction.CallAsync<BigInteger>(_userAccount.Address, _ownerAccount.Address);
            
            Console.WriteLine($"Approval transaction hash: {approvalReceipt.TransactionHash}");
            Console.WriteLine($"Owner allowance: {Web3.Convert.FromWei(allowance)} TEST");
            
            // Assert approval was successful
            Assert.That(approvalReceipt.Status?.Value, Is.EqualTo(new Nethereum.Hex.HexTypes.HexBigInteger(1).Value), "Approval transaction failed");
            Assert.IsTrue(approvalReceipt.Status?.Value == 1, "Approval transaction failed");
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
            var transferFromTxInput = transferFromFunction.CreateTransactionInput(
                _ownerAccount.Address, // From address (the owner)
                new HexBigInteger(100000), // Gas limit
                null, // Gas price
                new HexBigInteger(0), // Value (ETH)
                _userAccount.Address, // From which address to take tokens
                thirdPartyAddress, // To which address to send tokens
                transferAmount // Amount
            );
            var transferFromTxHash = await _web3Owner.Eth.TransactionManager.SendTransactionAsync(transferFromTxInput);
            var transferFromReceipt = await _web3Owner.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(transferFromTxHash);
            
            Console.WriteLine($"TransferFrom transaction hash: {transferFromReceipt.TransactionHash}");
            
            // Get updated balances and allowance
            var userFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(_userAccount.Address);
            var thirdPartyFinalBalance = await balanceOfFunction.CallAsync<BigInteger>(thirdPartyAddress);
            var finalAllowance = await allowanceFunction.CallAsync<BigInteger>(_userAccount.Address, _ownerAccount.Address);
            
            Console.WriteLine($"User final balance: {Web3.Convert.FromWei(userFinalBalance)} TEST");
            Console.WriteLine($"Third party final balance: {Web3.Convert.FromWei(thirdPartyFinalBalance)} TEST");
            Console.WriteLine($"Final allowance: {Web3.Convert.FromWei(finalAllowance)} TEST");
            
            // Assert transferFrom was successful
            Assert.That(transferFromReceipt.Status?.Value, Is.EqualTo(new Nethereum.Hex.HexTypes.HexBigInteger(1).Value), "TransferFrom transaction failed");
            Assert.IsTrue(transferFromReceipt.Status?.Value == 1, "TransferFrom transaction failed");
            
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
        public async Task Owner_ShouldBeAbleToMintTokens()
        {
            // Test BiatecToken's minting functionality
            var mintAmount = Web3.Convert.ToWei(1000); // 1000 tokens
            var recipientAddress = _userAccount.Address;
            
            // Get initial balance
            var balanceOfFunction = _tokenContract.GetFunction("balanceOf");
            var initialBalance = await balanceOfFunction.CallAsync<BigInteger>(recipientAddress);
            
            // Get initial total supply
            var totalSupplyFunction = _tokenContract.GetFunction("totalSupply");
            var initialTotalSupply = await totalSupplyFunction.CallAsync<BigInteger>();
            
            Console.WriteLine($"Initial balance: {Web3.Convert.FromWei(initialBalance)} TEST");
            Console.WriteLine($"Initial total supply: {Web3.Convert.FromWei(initialTotalSupply)} TEST");
            
            // Mint tokens (owner is automatically a minter)
            var mintFunction = _tokenContract.GetFunction("mint");
            
            // Create a transaction input with explicit gas settings
            var transactionInput = mintFunction.CreateTransactionInput(
                _ownerAccount.Address,
                new HexBigInteger(100000), // Gas limit
                null, // Gas price (auto)
                new HexBigInteger(0), // Value (no ETH)
                recipientAddress, // to
                mintAmount // amount
            );
            
            // Send the transaction manually
            var txHash = await _web3Owner.Eth.TransactionManager.SendTransactionAsync(transactionInput);
            Console.WriteLine($"Mint transaction hash: {txHash}");
            
            // Wait for receipt
            var mintReceipt = await _web3Owner.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txHash);
            
            Console.WriteLine($"Gas used: {mintReceipt.GasUsed?.Value} / Gas limit: {transactionInput.Gas?.Value}");
            
            // Get updated balances
            var finalBalance = await balanceOfFunction.CallAsync<BigInteger>(recipientAddress);
            var finalTotalSupply = await totalSupplyFunction.CallAsync<BigInteger>();
            
            Console.WriteLine($"Final balance: {Web3.Convert.FromWei(finalBalance)} TEST");
            Console.WriteLine($"Final total supply: {Web3.Convert.FromWei(finalTotalSupply)} TEST");
            
            // Assert minting was successful
            Assert.That(mintReceipt.Status?.Value, Is.EqualTo(new Nethereum.Hex.HexTypes.HexBigInteger(1).Value), "Mint transaction failed");
            
            // Check balances
            var expectedBalance = initialBalance + mintAmount;
            var expectedTotalSupply = initialTotalSupply + mintAmount;
            
            AssertBigIntegerEqual(expectedBalance, finalBalance, "Recipient balance wasn't increased correctly");
            AssertBigIntegerEqual(expectedTotalSupply, finalTotalSupply, "Total supply wasn't increased correctly");
        }

        private string GetBiatecTokenABI()
        {
            // Return the standard ERC20 ABI with mint function from the TokenService (compatible with BiatecToken)
            return @"[{""inputs"":[{""internalType"":""string"",""name"":""name"",""type"":""string""},{""internalType"":""string"",""name"":""symbol"",""type"":""string""},{""internalType"":""uint8"",""name"":""decimals_"",""type"":""uint8""},{""internalType"":""uint256"",""name"":""premint"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""owner"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""spender"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""inputs"":[{""internalType"":""address"",""name"":""owner"",""type"":""address""},{""internalType"":""address"",""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""decimals"",""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""mint"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""name"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""symbol"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""totalSupply"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""}]";
        }
        
        /// <summary>
        /// Helper method to assert BigInteger equality with better comparison and error messages
        /// </summary>
        private void AssertBigIntegerEqual(BigInteger expected, BigInteger actual, string? message = null)
        {
            // Convert to string for better error messages
            if (expected != actual)
            {
                string errorMessage = message ?? "BigIntegers not equal";
                
                // Convert to decimal for more readable output if possible
                string expectedStr = Web3.Convert.FromWei(expected).ToString();
                string actualStr = Web3.Convert.FromWei(actual).ToString();
                
                // Include both wei values and token values in the error message
                string fullErrorMessage = $"{errorMessage}: Expected {expected} wei ({expectedStr} tokens), Actual {actual} wei ({actualStr} tokens)";
                
                Assert.Fail(fullErrorMessage);
            }
        }
    }
}
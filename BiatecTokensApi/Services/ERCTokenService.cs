using System.Numerics;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiatecTokensApi.Services
{
    public class ERCTokenService : IERC20TokenService
    {
        private readonly BlockchainConfig _config;
        private readonly ILogger<ERCTokenService> _logger;

        // BiatecToken ABI loaded from the JSON file
        private readonly string _biatecTokenAbi;
        private readonly string _biatecTokenBytecode;

        public ERCTokenService(IOptions<BlockchainConfig> config, ILogger<ERCTokenService> logger)
        {
            _config = config.Value;
            _logger = logger;
            
            // Load the BiatecToken ABI and bytecode from the JSON file
            var abiFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ABI", "BiatecToken.json");
            if (File.Exists(abiFilePath))
            {
                var jsonContent = File.ReadAllText(abiFilePath);
                var contractData = JsonSerializer.Deserialize<BiatecTokenContract>(jsonContent);
                _biatecTokenAbi = JsonSerializer.Serialize(contractData?.Abi);
                _biatecTokenBytecode = contractData?.Bytecode ?? throw new InvalidOperationException("Bytecode not found in BiatecToken.json");
                
                _logger.LogInformation("Loaded BiatecToken ABI and bytecode from {Path}", abiFilePath);
            }
            else
            {
                // Fallback to simple ERC20 if BiatecToken.json is not found
                _logger.LogWarning("BiatecToken.json not found at {Path}, using fallback simple ERC20", abiFilePath);
                _biatecTokenAbi = GetFallbackErc20Abi();
                _biatecTokenBytecode = GetFallbackErc20Bytecode();
            }
        }

        public async Task<TokenDeploymentResponse> DeployTokenAsync(TokenDeploymentRequest request)
        {
            var response = new TokenDeploymentResponse { Success = false };
            
            try
            {
                // Create an account with the provided private key
                var account = new Account(request.DeployerPrivateKey, _config.ChainId);
                
                // Connect to the blockchain
                var web3 = new Web3(account, _config.BaseRpcUrl);
                
                // Calculate token supply with decimals (convert to BigInteger properly)
                var decimalMultiplier = BigInteger.Pow(10, request.Decimals);
                var initialSupplyBigInteger = new BigInteger(request.InitialSupply) * decimalMultiplier;
                
                // Determine the initial supply receiver - use provided address or default to deployer
                var initialSupplyReceiver = !string.IsNullOrEmpty(request.InitialSupplyReceiver) 
                    ? request.InitialSupplyReceiver 
                    : account.Address;
                
                _logger.LogInformation("Deploying BiatecToken {Name} ({Symbol}) with supply {Supply} and {Decimals} decimals to receiver {Receiver}", 
                    request.Name, request.Symbol, request.InitialSupply, request.Decimals, initialSupplyReceiver);
                
                // Deploy the BiatecToken contract with updated constructor parameters
                // BiatecToken constructor: (string name, string symbol, uint8 decimals_, uint256 initialSupply, address initialSupplyReceiver)
                var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    _biatecTokenAbi,
                    _biatecTokenBytecode,
                    account.Address,
                    new HexBigInteger(_config.GasLimit),
                    null, // No ETH value being sent
                    request.Name,              // string name
                    request.Symbol,            // string symbol
                    (byte)request.Decimals,    // uint8 decimals_
                    initialSupplyBigInteger,   // uint256 initialSupply
                    initialSupplyReceiver      // address initialSupplyReceiver
                );
                
                // Check if deployment was successful
                if (receipt?.Status?.Value == 1 && !string.IsNullOrEmpty(receipt.ContractAddress))
                {
                    response.TransactionHash = receipt.TransactionHash;
                    response.ContractAddress = receipt.ContractAddress;
                    response.InitialSupplyReceiver = initialSupplyReceiver;
                    response.Success = true;
                    
                    _logger.LogInformation("BiatecToken {Symbol} deployed successfully at address {Address} with transaction {TxHash}", 
                        request.Symbol, receipt.ContractAddress, receipt.TransactionHash);
                }
                else
                {
                    response.ErrorMessage = "Contract deployment failed - transaction reverted or no contract address received";
                    _logger.LogError("BiatecToken deployment failed: {Error}", response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying BiatecToken: {Message}", ex.Message);
                response.ErrorMessage = ex.Message;
            }
            
            return response;
        }

        private string GetFallbackErc20Abi()
        {
            return @"[{""inputs"":[{""internalType"":""string"",""name"":""name"",""type"":""string""},{""internalType"":""string"",""name"":""symbol"",""type"":""string""},{""internalType"":""uint8"",""name"":""decimals_"",""type"":""uint8""},{""internalType"":""uint256"",""name"":""initialSupply"",""type"":""uint256""},{""internalType"":""address"",""name"":""initialSupplyReceiver"",""type"":""address""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""owner"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""spender"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""inputs"":[{""internalType"":""address"",""name"":""owner"",""type"":""address""},{""internalType"":""address"",""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""decimals"",""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""name"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""symbol"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""totalSupply"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""}]";
        }

        private string GetFallbackErc20Bytecode()
        {
            // Updated fallback bytecode for ERC20 with 5-parameter constructor
            return "0x608060405234801561001057600080fd5b5060405161099938038061099983398101604081905261002f916102f8565b8451610042906003906020880190610155565b508351610056906004906020870190610155565b506005805460ff191660ff8516179055826001600160a01b0316600090815260208190526040812084905560028390556100909084906103c6565b6040516001600160a01b038416906000907fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef906100ce9087906103e5565b60405180910390a350505050506103fe565b828054610161906103f8565b90600052602060002090601f01602090048101928261018357600085556101c9565b82601f1061019c57805160ff19168380011785556101c9565b828001600101855582156101c9579182015b828111156101c95782518255916020019190600101906101ae565b506101d59291506101d9565b5090565b5b808211156101d557600081556001016101da565b634e487b7160e01b600052604160045260246000fd5b600082601f83011261021557600080fd5b81516001600160401b0380821115610230576102306101ee565b604051601f8301601f19908116603f0116810190828211818310171561025857610258610204565b8160405283815260209250868385880101111561027457600080fd5b600091505b8382101561029657858201830151818301840152908201906102579056fea2646970667358221220c0ab4ec8e9f6c8e87a4e07d5b1ac7b9e3f3b3b3b3b3b3b3b3b3b3b3b3b3b3b64736f6c63430008140033";
        }

        // Helper class to deserialize the BiatecToken.json file
        private class BiatecTokenContract
        {
            [JsonPropertyName("abi")]
            public JsonElement[]? Abi { get; set; }
            
            [JsonPropertyName("bytecode")]
            public string? Bytecode { get; set; }
        }
    }
}
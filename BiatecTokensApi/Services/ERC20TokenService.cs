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
    public class ERC20TokenService : IERC20TokenService
    {
        private readonly BlockchainConfig _config;
        private readonly ILogger<ERC20TokenService> _logger;

        // BiatecToken ABI loaded from the JSON file
        private readonly string _biatecTokenAbi;
        private readonly string _biatecTokenBytecode;

        public ERC20TokenService(IOptions<BlockchainConfig> config, ILogger<ERC20TokenService> logger)
        {
            _config = config.Value;
            _logger = logger;

            // Load the BiatecToken ABI and bytecode from the JSON file
            var abiFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ABI", "BiatecToken.json");

            var jsonContent = File.ReadAllText(abiFilePath);
            var contractData = JsonSerializer.Deserialize<BiatecTokenContract>(jsonContent);
            _biatecTokenAbi = JsonSerializer.Serialize(contractData?.Abi);
            _biatecTokenBytecode = contractData?.Bytecode ?? throw new InvalidOperationException("Bytecode not found in BiatecToken.json");

            _logger.LogInformation("Loaded BiatecToken ABI and bytecode from {Path}", abiFilePath);

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
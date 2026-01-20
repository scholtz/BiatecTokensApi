using System.Numerics;
using BiatecTokensApi.Configuration;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Text.Json;
using System.Text.Json.Serialization;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Models.AVM;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ERC20.Response;
using BiatecTokensApi.Models;
using AlgorandARC76AccountDotNet;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Provides functionality for deploying and interacting with ERC-20 token contracts on blockchain networks.
    /// </summary>
    /// <remarks>The <see cref="ERC20TokenService"/> class is designed to facilitate the deployment of ERC-20
    /// token contracts and manage interactions with the BiatecToken smart contract. It loads the ABI and bytecode for
    /// the BiatecToken contract from a JSON file and uses this information to deploy contracts and perform related
    /// operations.  This service relies on blockchain configuration settings and application-specific settings provided
    /// via dependency injection. It also logs relevant information and errors during operations.  Ensure that the
    /// required ABI and bytecode file ("BiatecToken.json") is present in the "ABI" directory under the application's
    /// base directory.</remarks>
    public class ERC20TokenService : IERC20TokenService
    {
        private readonly IOptionsMonitor<EVMChains> _config;
        private readonly IOptionsMonitor<AppConfiguration> _appConfig;
        private readonly ILogger<ERC20TokenService> _logger;

        // BiatecToken ABI loaded from the JSON file
        private readonly string _biatecTokenMintableAbi;
        private readonly string _biatecTokenMintableBytecode;
        private readonly string _biatecTokenPremintedAbi;
        private readonly string _biatecTokenPremintedBytecode;
        /// <summary>
        /// Initializes a new instance of the <see cref="ERC20TokenService"/> class,  loading the ABI and bytecode for
        /// the BiatecToken contract and configuring the service.
        /// </summary>
        /// <remarks>This constructor reads the ABI and bytecode for the BiatecToken contract from a JSON
        /// file  located in the "ABI" directory under the application's base directory. The loaded ABI and  bytecode
        /// are used to interact with the BiatecToken smart contract. Ensure that the  "BiatecToken.json" file is
        /// present and correctly formatted in the expected location.</remarks>
        /// <param name="config">The configuration monitor for blockchain-related settings.</param>
        /// <param name="appConfig">The configuration monitor for application-specific settings.</param>
        /// <param name="logger">The logger used to log information and errors for this service.</param>
        /// <exception cref="InvalidOperationException">Thrown if the BiatecToken contract bytecode is not found in the ABI JSON file.</exception>
        public ERC20TokenService(
            IOptionsMonitor<EVMChains> config,
            IOptionsMonitor<AppConfiguration> appConfig,
            ILogger<ERC20TokenService> logger
            )
        {
            _config = config;
            _appConfig = appConfig;
            _logger = logger;

            // Load the BiatecToken ABI and bytecode from the JSON file
            var abiFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ABI", "BiatecTokenMintable.json");
            var jsonContent = File.ReadAllText(abiFilePath);
            var contractData = JsonSerializer.Deserialize<BiatecTokenContract>(jsonContent);
            _biatecTokenMintableAbi = JsonSerializer.Serialize(contractData?.Abi);
            _biatecTokenMintableBytecode = contractData?.Bytecode ?? throw new InvalidOperationException("Bytecode not found in BiatecToken.json");

            // Load the BiatecToken ABI and bytecode from the JSON file
            abiFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ABI", "BiatecTokenPreminted.json");
            jsonContent = File.ReadAllText(abiFilePath);
            contractData = JsonSerializer.Deserialize<BiatecTokenContract>(jsonContent);
            _biatecTokenPremintedAbi = JsonSerializer.Serialize(contractData?.Abi);
            _biatecTokenPremintedBytecode = contractData?.Bytecode ?? throw new InvalidOperationException("Bytecode not found in BiatecToken.json");

            _logger.LogInformation("Loaded BiatecToken ABI and bytecode from {Path}", abiFilePath);

        }


        private EVMBlockchainConfig GetBlockchainConfig(int chainId)
        {
            // Find the configuration for the specified chain ID
            var config = _config.CurrentValue.Chains.FirstOrDefault(c => c.ChainId == chainId);
            if (config == null)
            {
                throw new InvalidOperationException($"No configuration found for chain ID {chainId}");
            }
            return config;
        }
        /// <summary>
        /// Validates the deployment request for an ERC20 token based on the specified token type.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="tokenType"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void ValidateRequest(ERC20TokenDeploymentRequest request, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ERC20_Mintable:
                    var mintableRequest = request as ERC20MintableTokenDeploymentRequest;
                    if (mintableRequest == null)
                    {
                        throw new ArgumentException("Request must be of type ERC20MintableTokenDeploymentRequest for mintable tokens.");
                    }
                    if (mintableRequest.Symbol.Length > 10)
                    {
                        throw new ArgumentException("Symbol for ERC20 Mintable token must be 10 characters or less.");
                    }
                    if (mintableRequest.Name.Length > 50)
                    {
                        throw new ArgumentException("Name for ERC20 Mintable token must be 50 characters or less.");
                    }
                    if (mintableRequest.InitialSupply <= 0)
                    {
                        throw new ArgumentException("Initial supply for ERC20 Mintable token must be a positive value.");
                    }
                    if (mintableRequest.Decimals < 0 || mintableRequest.Decimals > 18)
                    {
                        throw new ArgumentException("Decimals for ERC20 Mintable token must be between 0 and 18.");
                    }
                    if (string.IsNullOrEmpty(mintableRequest.InitialSupplyReceiver))
                    {
                        throw new ArgumentException("Initial supply receiver address must be provided for ERC20 Mintable token deployment.");
                    }
                    if (mintableRequest.Cap < mintableRequest.InitialSupply)
                    {
                        throw new ArgumentException("Cap for ERC20 Mintable token must be at least the initial supply.");
                    }

                    break;
                case TokenType.ARC200_Preminted:
                    var premintedRequest = request as ERC20PremintedTokenDeploymentRequest;
                    if (premintedRequest == null)
                    {
                        throw new ArgumentException("Request must be of type ERC20PremintedTokenDeploymentRequest for preminted tokens.");
                    }
                    if (premintedRequest.Symbol.Length > 10)
                    {
                        throw new ArgumentException("Symbol for ERC20 Mintable token must be 10 characters or less.");
                    }
                    if (premintedRequest.Name.Length > 50)
                    {
                        throw new ArgumentException("Name for ERC20 Mintable token must be 50 characters or less.");
                    }
                    if (premintedRequest.InitialSupply <= 0)
                    {
                        throw new ArgumentException("Initial supply for ERC20 Mintable token must be a non-negative value.");
                    }
                    if (premintedRequest.Decimals < 0 || premintedRequest.Decimals > 18)
                    {
                        throw new ArgumentException("Decimals for ERC20 Mintable token must be between 0 and 18.");
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tokenType), $"Unsupported token type: {tokenType}");
            }
        }

        /// <summary>
        /// Deploys an ERC-20 token contract to the specified blockchain network.
        /// </summary>
        /// <remarks>This method deploys an ERC-20 token contract using the provided deployment
        /// parameters. The initial supply is allocated to the specified receiver address, or to the deployer's address
        /// if no receiver is provided. The method handles exceptions and logs relevant information about the deployment
        /// process.</remarks>
        /// <param name="request">The deployment request containing the token details, such as name, symbol, decimals, initial supply, and the
        /// blockchain configuration (e.g., chain ID and RPC URL).</param>
        /// <param name="tokenType">Token type</param>
        /// <returns>A <see cref="ERC20TokenDeploymentResponse"/> containing the deployment result, including the contract
        /// address, transaction hash, and success status. If the deployment fails, the response includes an error
        /// message.</returns>
        public async Task<ERC20TokenDeploymentResponse> DeployERC20TokenAsync(ERC20TokenDeploymentRequest request, TokenType tokenType)
        {
            ERC20TokenDeploymentResponse? response = null;

            try
            {
                ValidateRequest(request, tokenType);

                var acc = ARC76.GetEVMAccount(_appConfig.CurrentValue.Account, Convert.ToInt32(request.ChainId));

                var chainConfig = GetBlockchainConfig(Convert.ToInt32(request.ChainId));

                // Create an account with the provided private key
                var account = new Account(acc, request.ChainId);

                // Connect to the blockchain
                var web3 = new Web3(account, chainConfig.RpcUrl);

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

                var _biatecTokenAbi = tokenType == TokenType.ERC20_Mintable ? _biatecTokenMintableAbi : _biatecTokenPremintedAbi;
                var _biatecTokenBytecode = tokenType == TokenType.ERC20_Mintable ? _biatecTokenMintableBytecode : _biatecTokenPremintedBytecode;

                var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    _biatecTokenAbi,
                    _biatecTokenBytecode,
                    account.Address,
                    new HexBigInteger(chainConfig.GasLimit),
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
                    response = new ERC20TokenDeploymentResponse()
                    {
                        ContractAddress = receipt.ContractAddress,
                        Success = true,
                        TransactionHash = receipt.TransactionHash,
                    };

                    _logger.LogInformation("BiatecToken {Symbol} deployed successfully at address {Address} with transaction {TxHash}",
                        request.Symbol, receipt.ContractAddress, receipt.TransactionHash);
                }
                else
                {
                    response = new ERC20TokenDeploymentResponse()
                    {
                        Success = false,
                        TransactionHash = receipt?.TransactionHash ?? string.Empty,
                        ContractAddress = string.Empty,
                        ErrorMessage = "Contract deployment failed - transaction reverted or no contract address received"
                    };
                    _logger.LogError("BiatecToken deployment failed: {Error}", response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                response = new ERC20TokenDeploymentResponse()
                {
                    Success = false,
                    TransactionHash = string.Empty,
                    ContractAddress = string.Empty,
                    ErrorMessage = ex.Message
                };
                _logger.LogError(ex, "Error deploying BiatecToken: {Message}", ex.Message);
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
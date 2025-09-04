using Algorand;
using Algorand.Algod;
using AlgorandARC76AccountDotNet;
using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Models.ARC1400.Response;
using BiatecTokensApi.Models.ARC200.Response;
using BiatecTokensApi.Models.AVM;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Provides functionality for deploying and interacting with ERC-20 token contracts on blockchain networks.
    /// </summary>
    /// <remarks>The <see cref="ARC1400TokenService"/> class is designed to facilitate the deployment of ERC-20
    /// token contracts and manage interactions with the BiatecToken smart contract. It loads the ABI and bytecode for
    /// the BiatecToken contract from a JSON file and uses this information to deploy contracts and perform related
    /// operations.  This service relies on blockchain configuration settings and application-specific settings provided
    /// via dependency injection. It also logs relevant information and errors during operations.  Ensure that the
    /// required ABI and bytecode file ("BiatecToken.json") is present in the "ABI" directory under the application's
    /// base directory.</remarks>
    public class ARC1400TokenService : IARC1400TokenService
    {
        private readonly IOptionsMonitor<AlgorandAuthenticationOptionsV2> _config;
        private readonly IOptionsMonitor<AppConfiguration> _appConfig;
        private readonly ILogger<ARC1400TokenService> _logger;

        // BiatecToken ABI loaded from the JSON file
        private readonly string _biatecTokenMintableAbi;
        private readonly string _biatecTokenMintableBytecode;
        private readonly string _biatecTokenPremintedAbi;
        private readonly string _biatecTokenPremintedBytecode;
        private readonly Dictionary<string, string> _genesisId2GenesisHash = new();
        /// <summary>
        /// Initializes a new instance of the <see cref="ARC1400TokenService"/> class,  loading the ABI and bytecode for
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
        public ARC1400TokenService(
            IOptionsMonitor<AlgorandAuthenticationOptionsV2> config,
            IOptionsMonitor<AppConfiguration> appConfig,
            ILogger<ARC1400TokenService> logger
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

            foreach (var chain in _config.CurrentValue.AllowedNetworks)
            {
                _logger.LogInformation("Allowed network: {Network}", chain);

                using var httpClient = HttpClientConfigurator.ConfigureHttpClient(chain.Value.Server, chain.Value.Token, chain.Value.Header);
                DefaultApi algodApiInstance = new DefaultApi(httpClient);
                try
                {
                    // Test connection to the node
                    var status = algodApiInstance.TransactionParamsAsync().Result;
                    _logger.LogInformation("Connected to {GenesisId} node at {Server} with round {LastRound}", status.GenesisId, chain.Value.Server, status.LastRound);
                    _genesisId2GenesisHash[status.GenesisId] = chain.Key;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to Algorand node at {Url}", chain.Value.Server);
                    throw;
                }
            }
        }


        /// <summary>
        /// Validates the deployment request for an ARC1400 token based on the specified token type.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="tokenType"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void ValidateRequest(ARC1400TokenDeploymentRequest request, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ARC1400_Mintable:
                    var mintableRequest = request as ARC1400MintableTokenDeploymentRequest;
                    if (mintableRequest == null)
                    {
                        throw new ArgumentException("Request must be of type ARC1400MintableTokenDeploymentRequest for mintable tokens.");
                    }
                    if (mintableRequest.Symbol.Length > 10)
                    {
                        throw new ArgumentException("Symbol for ARC1400 Mintable token must be 10 characters or less.");
                    }
                    if (mintableRequest.Name.Length > 50)
                    {
                        throw new ArgumentException("Name for ARC1400 Mintable token must be 50 characters or less.");
                    }
                    if (mintableRequest.InitialSupply <= 0)
                    {
                        throw new ArgumentException("Initial supply for ARC1400 Mintable token must be a non-negative value.");
                    }
                    if (mintableRequest.Decimals < 0 || mintableRequest.Decimals > 18)
                    {
                        throw new ArgumentException("Decimals for ARC1400 Mintable token must be between 0 and 18.");
                    }
                    if (string.IsNullOrEmpty(mintableRequest.InitialSupplyReceiver))
                    {
                        throw new ArgumentException("Initial supply receiver address must be provided for ARC1400 Mintable token deployment.");
                    }
                    if (mintableRequest.Cap < mintableRequest.InitialSupply)
                    {
                        throw new ArgumentException("Cap for ARC1400 Mintable token must be at least the initial supply.");
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(tokenType), $"Unsupported token type: {tokenType}");
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="network"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private DefaultApi GetAlgod(string network)
        {
            if (!_genesisId2GenesisHash.TryGetValue(network, out var genesisHash))
            {
                throw new ArgumentException($"Unsupported network: {network}");
            }
            var chain = _config.CurrentValue.AllowedNetworks[genesisHash];
            using var httpClient = HttpClientConfigurator.ConfigureHttpClient(chain.Server, chain.Token, chain.Header);
            return new DefaultApi(httpClient);
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
        /// <returns>A <see cref="ARC1400TokenDeploymentResponse"/> containing the deployment result, including the contract
        /// address, transaction hash, and success status. If the deployment fails, the response includes an error
        /// message.</returns>
        public async Task<ARC1400TokenDeploymentResponse> CreateARC1400TokenAsync(ARC1400TokenDeploymentRequest request, TokenType tokenType)
        {
            ARC1400TokenDeploymentResponse? response = null;

            try
            {
                ValidateRequest(request, tokenType);

                var acc = ARC76.GetAccount(_appConfig.CurrentValue.Account);
                var algod = GetAlgod(request.Network);

                var client = new Generated.Arc1644Proxy(algod, 0);

                await client.CreateApplication(acc, 1000);
                AVM.ClientGenerator.ABI.ARC4.Types.UInt256 totalSupply = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256(request.InitialSupply);
                var txs = await client.Bootstrap_Transactions(Encoding.UTF8.GetBytes(request.Name), Encoding.UTF8.GetBytes(request.Symbol), (byte)Convert.ToByte(request.Decimals), totalSupply, _tx_sender: acc, _tx_fee: 1000);
                await client.Bootstrap(Encoding.UTF8.GetBytes(request.Name), Encoding.UTF8.GetBytes(request.Symbol), (byte)Convert.ToByte(request.Decimals), totalSupply, _tx_sender: acc, _tx_fee: 1000);

                var appInfo = await algod.GetApplicationByIDAsync(client.appId);

                return new ARC1400TokenDeploymentResponse()
                {
                    Success = true,
                    ErrorMessage = string.Empty,
                    AppId = client.appId,
                    AssetId = 0,
                    TransactionId = txs.First().TxID(),
                    ConfirmedRound = txs.First().FirstValid,
                    CreatorAddress = acc.Address.ToString()
                };
            }
            catch (Exception ex)
            {
                response = new ARC1400TokenDeploymentResponse()
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    AppId = 0,
                    AssetId = 0,
                    TransactionId = string.Empty,
                    ConfirmedRound = 0,
                    CreatorAddress = string.Empty
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
using Algorand;
using Algorand.Algod;
using Algorand.Algod.Model.Transactions;
using Algorand.Utils;
using AlgorandARC76AccountDotNet;
using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using Nethereum.Signer;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for creating and managing ARC3 Fungible Tokens on Algorand blockchain
    /// </summary>
    public class ASATokenService : IASATokenService
    {
        private readonly IOptionsMonitor<AlgorandAuthenticationOptionsV2> _config;
        private readonly ILogger<ARC3TokenService> _logger;
        private readonly Dictionary<string, string> _genesisId2GenesisHash = new();
        private readonly IOptionsMonitor<AppConfiguration> _appConfig;
        /// <summary>
        /// Initializes a new instance of the <see cref="ARC3TokenService"/> class, configuring it to interact
        /// with Algorand nodes and IPFS repositories based on the provided options.
        /// </summary>
        /// <remarks>During initialization, the service attempts to connect to each allowed Algorand
        /// network specified in the configuration. For each network, it validates the connection by retrieving
        /// transaction parameters and logs the connection status. If a connection to any network fails, an exception is
        /// thrown, and the service cannot be initialized.</remarks>
        /// <param name="config">A monitor for <see cref="AlgorandAuthenticationOptionsV2"/> that provides configuration settings, including
        /// allowed networks and authentication details for connecting to Algorand nodes.</param>
        /// <param name="appConfig"></param>
        /// <param name="logger">An <see cref="ILogger{TCategoryName}"/> instance used for logging information, warnings, and errors related
        /// to the service's operations.</param>
        public ASATokenService(
            IOptionsMonitor<AlgorandAuthenticationOptionsV2> config,
            IOptionsMonitor<AppConfiguration> appConfig,
            ILogger<ARC3TokenService> logger
            )
        {
            _config = config;
            _appConfig = appConfig;
            _logger = logger;

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
        /// Creates an ARC3 fungible token on Algorand blockchain
        /// </summary>
        public Task<ASATokenDeploymentResponse> CreateASATokenAsync(ASABaseTokenDeploymentRequest request, TokenType tokenType)
        {
            if (tokenType == TokenType.ASA_FNFT)
            {
                if (request is ASAFractionalNonFungibleTokenDeploymentRequest rfnftRequest)
                {
                    return CreateFNFTPublicAsync(rfnftRequest);
                }
            }
            if (tokenType == TokenType.ASA_FT)
            {
                if (request is ASAFungibleTokenDeploymentRequest ftRequest)
                {
                    return CreateFTPublicAsync(ftRequest);
                }
            }
            if (tokenType == TokenType.ASA_FT)
            {
                if (request is ASANonFungibleTokenDeploymentRequest nftRequest)
                {
                    return CreateNFTAsync(nftRequest);
                }
            }
            throw new Exception("Unsupported token type for ASA: " + tokenType);
        }


        /// <summary>
        /// Creates an ASA fractional nft token on Algorand blockchain
        /// </summary>
        public async Task<ASATokenDeploymentResponse> CreateFNFTPublicAsync(ASAFractionalNonFungibleTokenDeploymentRequest request)
        {
            ValidateASARequest(request, TokenType.ASA_FNFT);
            return await CreateFNFTAsync(request);
        }
        /// <summary>
        /// Creates an ASA fungible token on Algorand blockchain
        /// </summary>
        public async Task<ASATokenDeploymentResponse> CreateFTPublicAsync(ASAFungibleTokenDeploymentRequest request)
        {
            ValidateASARequest(request, TokenType.ASA_FT);
            return await CreateFTAsync(request);
        }

        /// <summary>
        /// Creates an ASA non fungible token on Algorand blockchain
        /// </summary>
        public async Task<ASATokenDeploymentResponse> CreateNFTPublicAsync(ASANonFungibleTokenDeploymentRequest request)
        {
            ValidateASARequest(request, TokenType.ASA_NFT);
            return await CreateNFTAsync(request);
        }


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


        private bool ValidateASARequest(ASABaseTokenDeploymentRequest? request, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ASA_FNFT:
                    if (true)
                    {
                        var r = request as ASAFractionalNonFungibleTokenDeploymentRequest;

                        if (r == null)
                        {
                            throw new ArgumentException("Invalid request type for ASA Fractional Non-Fungible Token");
                        }

                        if (string.IsNullOrWhiteSpace(r.Name))
                        {
                            throw new ArgumentException("Token name is required");
                        }

                        if (string.IsNullOrWhiteSpace(r.UnitName))
                        {
                            throw new ArgumentException("Unit name is required");
                        }

                        if (r.TotalSupply == 0)
                        {
                            throw new ArgumentException("Total supply must be greater than 0");
                        }

                        if (r.Decimals > 19)
                        {
                            throw new ArgumentException("Decimals cannot exceed 19");
                        }
                        if (r.UnitName == null || r.UnitName.Length > 8)
                        {
                            throw new ArgumentException("Unit name must be provided and cannot exceed 8 characters");
                        }
                    }
                    break;
                case TokenType.ASA_FT:
                    if (true)
                    {
                        var r = request as ASAFungibleTokenDeploymentRequest;

                        if (r == null)
                        {
                            throw new ArgumentException("Invalid request type for ASA Fungible Token");
                        }

                        if (string.IsNullOrWhiteSpace(r.Name))
                        {
                            throw new ArgumentException("Token name is required");
                        }

                        if (string.IsNullOrWhiteSpace(r.UnitName))
                        {
                            throw new ArgumentException("Unit name is required");
                        }

                        if (r.TotalSupply == 0)
                        {
                            throw new ArgumentException("Total supply must be greater than 0");
                        }

                        if (r.Decimals > 19)
                        {
                            throw new ArgumentException("Decimals cannot exceed 19");
                        }
                        if (r.UnitName == null || r.UnitName.Length > 8)
                        {
                            throw new ArgumentException("Unit name must be provided and cannot exceed 8 characters");
                        }
                    }
                    break;
                case TokenType.ASA_NFT:
                    if (true)
                    {
                        var r = request as ASANonFungibleTokenDeploymentRequest;

                        if (r == null)
                        {
                            throw new ArgumentException("Invalid request type for ASA Non Fungible Token");
                        }

                        if (string.IsNullOrWhiteSpace(r.Name))
                        {
                            throw new ArgumentException("Token name is required");
                        }

                        if (string.IsNullOrWhiteSpace(r.UnitName))
                        {
                            throw new ArgumentException("Unit name is required");
                        }

                        if (r.UnitName == null || r.UnitName.Length > 8)
                        {
                            throw new ArgumentException("Unit name must be provided and cannot exceed 8 characters");
                        }
                    }
                    break;
                default:
                    throw new ArgumentException($"Unsupported token type: {tokenType}");
            }

            return true;
        }
        /// <summary>
        /// Deploys a new Algorand Standard Asset (ASA) fungible token based on the provided deployment request.
        /// </summary>
        /// <remarks>This method creates a new fungible token on the specified Algorand network using the
        /// parameters provided in the request. It signs the transaction with the configured account and waits for the
        /// transaction to be confirmed.</remarks>
        /// <param name="request">The request containing parameters for the fungible token deployment, including name, unit name, total
        /// supply, and various addresses.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see
        /// cref="ASATokenDeploymentResponse"/> with details of the deployed token, including asset ID and transaction
        /// ID.</returns>
        /// <exception cref="Exception">Thrown if the asset creation transaction or asset index cannot be parsed after creation.</exception>
        private async Task<ASATokenDeploymentResponse> CreateFTAsync(
            ASAFungibleTokenDeploymentRequest request
            )
        {
            var assetCreateTx = new AssetCreateTransaction()
            {
                AssetParams = new Algorand.Algod.Model.AssetParams()
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    Total = request.TotalSupply,
                    Manager = request.ManagerAddress == null ? new Address(new byte[32]) : new Address(request.ManagerAddress),
                    Reserve = request.ReserveAddress == null ? new Address(new byte[32]) : new Address(request.ReserveAddress),
                    Freeze = request.FreezeAddress == null ? new Address(new byte[32]) : new Address(request.FreezeAddress),
                    Clawback = request.ClawbackAddress == null ? new Address(new byte[32]) : new Address(request.ClawbackAddress),
                    Decimals = request.Decimals,
                    DefaultFrozen = request.DefaultFrozen,
                    Url = request.Url,
                    MetadataHash = request.MetadataHash
                }
            };

            var apiInstance = GetAlgod(request.Network);
            var transParams = await apiInstance.TransactionParamsAsync();
            assetCreateTx.FillInParams(transParams);

            var acc = ARC76.GetAccount(_appConfig.CurrentValue.Account);

            var signedTx = assetCreateTx.Sign(acc);
            var response = await Utils.SubmitTransaction(apiInstance, signedTx);
            var result = await Utils.WaitTransactionToComplete(apiInstance, response.Txid);
            var assetResult = result as AssetCreateTransaction ?? throw new Exception("Unable to parse asset create transaction");
            var assetInfo = await apiInstance.GetAssetByIDAsync(assetResult.AssetIndex ?? throw new Exception("Unable to parse asset index after asset was created"));

            return new ASATokenDeploymentResponse
            {
                Success = true,
                AssetId = assetResult.AssetIndex,
                TransactionId = result.TxID(),
                CreatorAddress = acc.Address.EncodeAsString(),
                ConfirmedRound = assetResult.ConfirmedRound,
                TokenInfo = assetInfo
            };
        }
        /// <summary>
        /// Creates a non-fungible token (NFT) based on the specified deployment request.
        /// </summary>
        /// <remarks>This method configures the NFT with a total supply of 1 and 0 decimals, as required
        /// for non-fungible tokens. It uses the provided request details to set up the NFT's properties and delegates
        /// the creation to the <c>CreateFT</c> method.</remarks>
        /// <param name="request">The deployment request containing the parameters for the NFT, such as the manager address, metadata hash,
        /// and network details. The request must specify a total supply of 1 and 0 decimals to conform to NFT
        /// standards.</param>
        /// <returns>A task representing the asynchronous operation, with a result of type <see
        /// cref="ASATokenDeploymentResponse"/> that contains the details of the deployed NFT.</returns>
        private Task<ASATokenDeploymentResponse> CreateNFTAsync(
            ASANonFungibleTokenDeploymentRequest request
            )
        {
            var ftRequest = new ASAFungibleTokenDeploymentRequest()
            {
                ClawbackAddress = request.ClawbackAddress,
                Decimals = 0,// Non-fungible tokens have 0 decimals
                DefaultFrozen = request.DefaultFrozen,
                FreezeAddress = request.FreezeAddress,
                ManagerAddress = request.ManagerAddress,
                MetadataHash = request.MetadataHash,
                Name = request.Name,
                ReserveAddress = request.ReserveAddress,
                Network = request.Network,
                TotalSupply = 1, // Non-fungible tokens have a total supply of 1
                UnitName = request.UnitName,
                Url = request.Url
            };
            return CreateFTAsync(ftRequest);
        }
        /// <summary>
        /// Initiates the deployment of a fractional non-fungible token (FNFT) based on the specified request
        /// parameters.
        /// </summary>
        /// <remarks>This method constructs a fungible token deployment request from the provided
        /// fractional non-fungible token request and initiates the deployment process.</remarks>
        /// <param name="request">The request containing the parameters for deploying the fractional non-fungible token, including addresses,
        /// metadata, and supply details.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the response of the FNFT
        /// deployment, including deployment details and status.</returns>
        private Task<ASATokenDeploymentResponse> CreateFNFTAsync(
            ASAFractionalNonFungibleTokenDeploymentRequest request
            )
        {
            var ftRequest = new ASAFungibleTokenDeploymentRequest()
            {
                ClawbackAddress = request.ClawbackAddress,
                Decimals = request.Decimals,
                DefaultFrozen = request.DefaultFrozen,
                FreezeAddress = request.FreezeAddress,
                ManagerAddress = request.ManagerAddress,
                MetadataHash = request.MetadataHash,
                Name = request.Name,
                ReserveAddress = request.ReserveAddress,
                Network = request.Network,
                TotalSupply = request.TotalSupply,
                UnitName = request.UnitName,
                Url = request.Url
            };
            return CreateFTAsync(ftRequest);
        }
    }
}

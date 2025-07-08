using Algorand;
using Algorand.Algod;
using Algorand.Algod.Model.Transactions;
using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
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
    public class ARC3TokenService : IARC3TokenService
    {
        private readonly IOptionsMonitor<AlgorandAuthenticationOptionsV2> _config;
        private readonly ILogger<ARC3TokenService> _logger;
        private readonly Dictionary<string, string> _genesisId2GenesisHash = new();
        private readonly IIPFSRepository _ipfsRepository;
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
        /// <param name="logger">An <see cref="ILogger{TCategoryName}"/> instance used for logging information, warnings, and errors related
        /// to the service's operations.</param>
        /// <param name="ipfsRepository">An implementation of <see cref="IIPFSRepository"/> used to interact with IPFS for managing decentralized
        /// file storage.</param>
        public ARC3TokenService(
            IOptionsMonitor<AlgorandAuthenticationOptionsV2> config,
            ILogger<ARC3TokenService> logger,
            IIPFSRepository ipfsRepository)
        {
            _config = config;
            _logger = logger;
            _ipfsRepository = ipfsRepository;

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
        public async Task<ARC3TokenDeploymentResponse> CreateARC3TokenAsync(IARC3TokenDeploymentRequest request, TokenType tokenType)
        {
            var response = new ARC3TokenDeploymentResponse { Success = false };

            try
            {
                _logger.LogInformation("Creating ARC3 token {Name} ({Symbol}) on {Network}",
                    request.Name, request.UnitName, request.Network);

                // Validate request
                if (!ValidateRequest(request, tokenType, out string validationError))
                {
                    response.ErrorMessage = validationError;
                    return response;
                }

                // Validate metadata if provided
                if (request.Metadata != null)
                {
                    var (isValid, metadataError) = ValidateMetadata(request.Metadata);
                    if (!isValid)
                    {
                        response.ErrorMessage = $"Invalid metadata: {metadataError}";
                        return response;
                    }
                }

                // Get node URL for the specified network
                if (!_genesisId2GenesisHash.TryGetValue(request.Network, out var genesisHash))
                {
                    response.ErrorMessage = $"Unsupported network: {request.Network}";
                    return response;
                }
                var chain = _config.CurrentValue.AllowedNetworks[genesisHash];
                using var httpClient = HttpClientConfigurator.ConfigureHttpClient(chain.Server, chain.Token, chain.Header);
                DefaultApi algodApiInstance = new DefaultApi(httpClient);

                // Upload metadata if provided
                string? metadataUrl = null;
                string? metadataHash = null;
                string? sha256Hash = null;
                if (request.Metadata != null)
                {
                    (metadataUrl, metadataHash, sha256Hash) = await UploadMetadataAsync(request.Metadata);
                    if (string.IsNullOrEmpty(metadataUrl))
                    {
                        response.ErrorMessage = "Failed to upload metadata to IPFS";
                        return response;
                    }
                }

                // For now, create a placeholder response since we need the actual Algorand SDK
                response = await CreateToken(request, algodApiInstance, metadataUrl, sha256Hash);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token: {Message}", ex.Message);
                response.ErrorMessage = $"Failed to create token: {ex.Message}";
            }

            return response;
        }


        /// <summary>
        /// Creates an ARC3 fungible token on Algorand blockchain
        /// </summary>
        public async Task<ARC3TokenDeploymentResponse> CreateARC3TokenAsync(ARC3FractionalNonFungibleTokenDeploymentRequest request)
        {

            ValidateARC3Request(request, TokenType.ARC3_FNFT);

            var response = new ARC3TokenDeploymentResponse { Success = false };

            try
            {
                _logger.LogInformation("Creating ARC3 token {Name} ({Symbol}) on {Network}",
                    request.Name, request.UnitName, request.Network);

                // Validate metadata if provided
                if (request.Metadata != null)
                {
                    var (isValid, metadataError) = ValidateMetadata(request.Metadata);
                    if (!isValid)
                    {
                        response.ErrorMessage = $"Invalid metadata: {metadataError}";
                        return response;
                    }
                }
                var algodApiInstance = GetAlgod(request.Network);

                // Upload metadata if provided
                string? metadataUrl = null;
                string? metadataHash = null;
                string? sha256Hash = null;

                if (request.Metadata != null)
                {
                    (metadataUrl, metadataHash, sha256Hash) = await UploadMetadataAsync(request.Metadata);
                    if (string.IsNullOrEmpty(metadataUrl))
                    {
                        response.ErrorMessage = "Failed to upload metadata to IPFS";
                        return response;
                    }
                }

                // For now, create a placeholder response since we need the actual Algorand SDK
                response = await CreateToken(request, algodApiInstance, metadataUrl, sha256Hash);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token: {Message}", ex.Message);
                response.ErrorMessage = $"Failed to create token: {ex.Message}";
            }

            return response;
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


        ///// <summary>
        ///// Gets information about an existing ARC3 token
        ///// </summary>
        //public async Task<ARC3TokenInfo?> GetTokenInfoAsync(ulong assetId, string network)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Getting token info for asset {AssetId} on {Network}", assetId, network);

        //        if (!_genesisId2GenesisHash.TryGetValue(network, out var genesisHash))
        //        {
        //            _logger.LogError("Unsupported network: {Network}", network);
        //            return null;
        //        }

        //        var chain = _config.CurrentValue.AllowedNetworks[genesisHash];
        //        using var httpClient = HttpClientConfigurator.ConfigureHttpClient(chain.Server, chain.Token, chain.Header);
        //        DefaultApi algodApiInstance = new DefaultApi(httpClient);

        //        // Get asset information from Algorand
        //        try
        //        {
        //            var assetInfo = await algodApiInstance.GetAssetByIDAsync(assetId);
        //            if (assetInfo?.Params != null)
        //            {
        //                var tokenInfo = new ARC3TokenInfo
        //                {
        //                    Name = assetInfo.Params.Name ?? "",
        //                    UnitName = assetInfo.Params.UnitName ?? "",
        //                    TotalSupply = assetInfo.Params.Total ?? 0,
        //                    Decimals = assetInfo.Params.Decimals ?? 0,
        //                    Url = assetInfo.Params.Url,
        //                    DefaultFrozen = assetInfo.Params.DefaultFrozen ?? false,
        //                    ManagerAddress = new Algorand.Address(assetInfo.Params.Manager),
        //                    ReserveAddress = assetInfo.Params.Reserve,
        //                    FreezeAddress = assetInfo.Params.Freeze,
        //                    ClawbackAddress = assetInfo.Params.Clawback
        //                };

        //                // Fetch metadata if URL is present and it's an IPFS URL
        //                if (!string.IsNullOrEmpty(tokenInfo.Url))
        //                {
        //                    tokenInfo.Metadata = await FetchMetadataFromUrl(tokenInfo.Url);
        //                }

        //                return tokenInfo;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Failed to get asset info from Algorand for asset {AssetId}", assetId);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting token info for asset {AssetId}: {Message}", assetId, ex.Message);
        //    }

        //    return null;
        //}

        /// <summary>
        /// Transfers ARC3 tokens between accounts
        /// </summary>
        public async Task<string?> TransferTokenAsync(ulong assetId, string fromMnemonic, string toAddress, ulong amount, string network)
        {
            try
            {
                _logger.LogInformation("Transferring {Amount} of asset {AssetId} to {ToAddress} on {Network}",
                    amount, assetId, toAddress, network);

                if (!_genesisId2GenesisHash.TryGetValue(network, out var genesisHash))
                {
                    _logger.LogError("Unsupported network: {Network}", network);
                    return null;
                }

                var chain = _config.CurrentValue.AllowedNetworks[genesisHash];
                using var httpClient = HttpClientConfigurator.ConfigureHttpClient(chain.Server, chain.Token, chain.Header);
                DefaultApi algodApiInstance = new DefaultApi(httpClient);

                // TODO: Implement actual transfer logic using Algorand SDK
                // This would:
                // 1. Parse the mnemonic to get the account
                // 2. Create an asset transfer transaction
                // 3. Sign the transaction
                // 4. Submit it to the network
                // 5. Wait for confirmation

                // For now, return a placeholder response
                await Task.Delay(100); // Simulate API call

                // Generate a mock transaction ID for demonstration
                var mockTxId = GenerateMockTransactionId();

                _logger.LogInformation("Transfer completed with transaction ID: {TxId}", mockTxId);
                return mockTxId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring tokens: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Opts an account into receiving a specific ARC3 token
        /// </summary>
        public async Task<string?> OptInToTokenAsync(ulong assetId, string accountMnemonic, string network)
        {
            try
            {
                _logger.LogInformation("Opting in to asset {AssetId} on {Network}", assetId, network);

                if (!_genesisId2GenesisHash.TryGetValue(network, out var genesisHash))
                {
                    _logger.LogError("Unsupported network: {Network}", network);
                    return null;
                }

                var chain = _config.CurrentValue.AllowedNetworks[genesisHash];
                using var httpClient = HttpClientConfigurator.ConfigureHttpClient(chain.Server, chain.Token, chain.Header);
                DefaultApi algodApiInstance = new DefaultApi(httpClient);

                // TODO: Implement actual opt-in logic using Algorand SDK
                // This would:
                // 1. Parse the mnemonic to get the account
                // 2. Create an asset opt-in transaction (amount = 0)
                // 3. Sign the transaction
                // 4. Submit it to the network
                // 5. Wait for confirmation

                // For now, return a placeholder response
                await Task.Delay(100); // Simulate API call

                // Generate a mock transaction ID for demonstration
                var mockTxId = GenerateMockTransactionId();

                _logger.LogInformation("Opt-in completed with transaction ID: {TxId}", mockTxId);
                return mockTxId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opting in to token: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Uploads ARC3 metadata to IPFS and returns the URL and hash
        /// </summary>
        public async Task<(string? Url, string? Hash, string? Sha256Hash)> UploadMetadataAsync(ARC3TokenMetadata metadata)
        {
            try
            {
                _logger.LogInformation("Uploading ARC3 metadata to IPFS");

                // Use the IPFS repository to upload the metadata
                var uploadResult = await _ipfsRepository.UploadObjectAsync(metadata, "arc3-metadata.json");

                if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Hash))
                {
                    _logger.LogInformation("Metadata uploaded to IPFS: {Hash} at {Url}", uploadResult.Hash, uploadResult.GatewayUrl);
                    return (uploadResult.GatewayUrl, uploadResult.Hash, uploadResult.Sha256Hash);
                }
                else
                {
                    _logger.LogError("Failed to upload metadata to IPFS: {Error}", uploadResult.ErrorMessage);
                    return (null, null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading metadata: {Message}", ex.Message);
                return (null, null, null);
            }
        }

        /// <summary>
        /// Validates ARC3 metadata structure
        /// </summary>
        public (bool IsValid, string? ErrorMessage) ValidateMetadata(ARC3TokenMetadata metadata)
        {
            try
            {
                // Validate background color format if provided
                if (!string.IsNullOrEmpty(metadata.BackgroundColor))
                {
                    if (!Regex.IsMatch(metadata.BackgroundColor, @"^[0-9A-Fa-f]{6}$"))
                    {
                        return (false, "Background color must be a six-character hexadecimal without #");
                    }
                }

                // Validate image MIME type if provided
                if (!string.IsNullOrEmpty(metadata.ImageMimetype))
                {
                    if (!metadata.ImageMimetype.StartsWith("image/"))
                    {
                        return (false, "Image MIME type must be of the form 'image/*'");
                    }
                }

                // Validate localization if provided
                if (metadata.Localization != null)
                {
                    if (string.IsNullOrEmpty(metadata.Localization.Uri))
                    {
                        return (false, "Localization URI is required when localization is specified");
                    }

                    if (!metadata.Localization.Uri.Contains("{locale}"))
                    {
                        return (false, "Localization URI must contain the substring '{locale}'");
                    }

                    if (string.IsNullOrEmpty(metadata.Localization.Default))
                    {
                        return (false, "Default locale is required when localization is specified");
                    }

                    if (metadata.Localization.Locales == null || !metadata.Localization.Locales.Any())
                    {
                        return (false, "At least one locale must be specified when localization is used");
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Validation error: {ex.Message}");
            }
        }

        private bool ValidateARC3Request(IARC3TokenDeploymentRequest request, TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.ARC3_FNFT:
                    if (true)
                    {
                        var r = request as ARC3FractionalNonFungibleTokenDeploymentRequest;

                        if (r == null)
                        {
                            throw new ArgumentException("Invalid request type for ARC3 Fractional Non-Fungible Token");
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
                        if (r.Metadata == null)
                        {
                            throw new ArgumentException("Metadata is required for ARC3 Fractional Non-Fungible Token");
                        }
                        if (r.Metadata.Name == null || r.Metadata.Name.Length > 32)
                        {
                            throw new ArgumentException("Metadata name must be provided and cannot exceed 32 characters");
                        }
                        if (r.UnitName == null || r.UnitName.Length > 8)
                        {
                            throw new ArgumentException("Unit name must be provided and cannot exceed 8 characters");
                        }
                    }

                    break;
                case TokenType.ARC3_FT:
                    if (true)
                    {
                        var r = request as ARC3FungibleTokenDeploymentRequest;

                        if (r == null)
                        {
                            throw new ArgumentException("Invalid request type for ARC3 Fractional Fungible Token");
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
                        if (r.Metadata == null)
                        {
                            throw new ArgumentException("Metadata is required for ARC3 Fractional Non-Fungible Token");
                        }
                        if (r.Metadata.Name == null || r.Metadata.Name.Length > 32)
                        {
                            throw new ArgumentException("Metadata name must be provided and cannot exceed 32 characters");
                        }
                        if (r.UnitName == null || r.UnitName.Length > 8)
                        {
                            throw new ArgumentException("Unit name must be provided and cannot exceed 8 characters");
                        }


                    }
                    break;
                case TokenType.ARC3_NFT:
                    if (true)
                    {
                        var r = request as ARC3NonFungibleTokenDeploymentRequest;

                        if (r == null)
                        {
                            throw new ArgumentException("Invalid request type for ARC3 Non Fungible Token");
                        }

                        if (string.IsNullOrWhiteSpace(r.Name))
                        {
                            throw new ArgumentException("Token name is required");
                        }

                        if (string.IsNullOrWhiteSpace(r.UnitName))
                        {
                            throw new ArgumentException("Unit name is required");
                        }

                        if (r.Metadata == null)
                        {
                            throw new ArgumentException("Metadata is required for ARC3 Fractional Non-Fungible Token");
                        }
                        if (r.Metadata.Name == null || r.Metadata.Name.Length > 32)
                        {
                            throw new ArgumentException("Metadata name must be provided and cannot exceed 32 characters");
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


            //ulong qByDecimals = (ulong)Math.Pow(10, request.Decimals);

            //switch (tokenType)
            //{
            //    case TokenType.ERC20_Mintable:
            //        error = "ERC20 Mintable tokens are not supported in this service.";
            //        return false;
            //    case TokenType.ERC20_Preminted:
            //        error = "ERC20 Preminted tokens are not supported in this service.";
            //        return false;
            //    case TokenType.ASA_FT:
            //        if (request.Decimals == 0 && request.TotalSupply == 1)
            //        {
            //            error = "ASA_FT tokens must have total supply grater then 1";
            //            return false;
            //        }
            //        if (request.TotalSupply == qByDecimals)
            //        {
            //            error = "ASA_FT tokens must have a total supply different than 1";
            //            return false;
            //        }
            //        if (request.Metadata != null)
            //        {
            //            error = "ASA_FT tokens must NOT have metadata";
            //            return false;
            //        }
            //        break;
            //    case TokenType.ASA_NFT:
            //        if (request.Decimals != 0 || request.TotalSupply != 1)
            //        {
            //            error = "ASA_NFT tokens must have total supply equal to 1 and decimals equal to 0";
            //            return false;
            //        }
            //        if (request.Metadata != null)
            //        {
            //            error = "ASA_NFT tokens must NOT have metadata";
            //            return false;
            //        }
            //        break;
            //    case TokenType.ASA_FNFT:
            //        if (request.TotalSupply == qByDecimals)
            //        {
            //            error = "ASA_FNFT tokens must have total supply equal to 1";
            //            return false;
            //        }
            //        if (request.Metadata != null)
            //        {
            //            error = "ASA_FNFT tokens must NOT have metadata";
            //            return false;
            //        }
            //        break;
            //    case TokenType.ARC3_FT:
            //        if (request.Decimals == 0 && request.TotalSupply == 1)
            //        {
            //            error = "ARC3_FT tokens must have total supply greater than 1";
            //            return false;
            //        }
            //        if (request.TotalSupply == qByDecimals)
            //        {
            //            error = "ARC3_FT tokens must have a total supply different than 1";
            //            return false;
            //        }
            //        if (request.Metadata == null)
            //        {
            //            error = "ASA_FT tokens must have metadata";
            //            return false;
            //        }

            //        break;
            //    case TokenType.ARC3_NFT:
            //        if (request.Decimals != 0 || request.TotalSupply != 1)
            //        {
            //            error = "ARC3_NFT tokens must have total supply equal to 1 and decimals equal to 0";
            //            return false;
            //        }
            //        if (request.Metadata == null)
            //        {
            //            error = "ARC3_NFT tokens must have metadata";
            //            return false;
            //        }
            //        break;
            //    case TokenType.ARC3_FNFT:
            //        if (request.TotalSupply == qByDecimals)
            //        {
            //            error = "ARC3_FNFT tokens must have total supply equal to 1";
            //            return false;
            //        }
            //        if (request.Metadata == null)
            //        {
            //            error = "ARC3_FNFT tokens must have metadata";
            //            return false;
            //        }
            //        break;
            //    case TokenType.ARC200_Mintable:
            //    case TokenType.ARC200_Preminted:
            //        // ok
            //        break;
            //}

            return true;
        }

        private async Task<ARC3TokenMetadata?> FetchMetadataFromUrl(string url)
        {
            try
            {
                // Check if it's an IPFS URL and extract CID
                if (url.Contains("/ipfs/"))
                {
                    var cid = ExtractCidFromUrl(url);
                    if (!string.IsNullOrEmpty(cid))
                    {
                        return await _ipfsRepository.RetrieveObjectAsync<ARC3TokenMetadata>(cid);
                    }
                }

                // For non-IPFS URLs, we can't easily fetch without additional HTTP client
                _logger.LogWarning("Cannot fetch metadata from non-IPFS URL: {Url}", url);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch metadata from {Url}", url);
            }
            return null;
        }

        private static string? ExtractCidFromUrl(string url)
        {
            // Extract CID from IPFS URL like "https://ipfs.biatec.io/ipfs/QmHash"
            var match = Regex.Match(url, @"/ipfs/([a-zA-Z0-9]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async Task<ARC3TokenDeploymentResponse> CreateToken(
            ASABaseTokenDeploymentRequest request,
            DefaultApi algod,
            string? metadataUrl,
            string? metadataHash)
        {
            // This is a placeholder implementation that demonstrates the structure
            // In a real implementation, this would:
            // 1. Parse the mnemonic to get the account
            // 2. Create an asset creation transaction with metadata URL
            // 3. Sign the transaction
            // 4. Submit it to the network
            // 5. Wait for confirmation

            var assetCreateTx = new AssetCreateTransaction()
            {
                AssetParams = new Algorand.Algod.Model.AssetParams()
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    Total = request.TotalSupply,
                    Manager = new Algorand.Address(request.ManagerAddress),
                    Reserve = new Address(request.ReserveAddress),
                    Freeze = new Address(request.FreezeAddress),
                    Clawback = new Address(request.ClawbackAddress),
                    Decimals = request.Decimals,
                    DefaultFrozen = request.DefaultFrozen,
                    Url = metadataUrl,
                    MetadataHash = metadataHash != null ? Encoding.UTF8.GetBytes(metadataHash) : null

                }
            };

            // For demonstration, create a mock response
            var mockAssetId = (ulong)Random.Shared.NextInt64(1000000, 999999999);
            var mockTxId = GenerateMockTransactionId();

            return new ARC3TokenDeploymentResponse
            {
                Success = true,
                AssetId = mockAssetId,
                TransactionId = mockTxId,
                CreatorAddress = "PLACEHOLDER_ADDRESS", // Would be derived from mnemonic
                ConfirmedRound = (ulong)Random.Shared.NextInt64(1000000, 2000000),
                MetadataUrl = metadataUrl,
                MetadataHash = metadataHash,
                TokenInfo = new ARC3TokenInfo
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    TotalSupply = request.TotalSupply,
                    Decimals = request.Decimals,
                    Url = metadataUrl,
                    DefaultFrozen = request.DefaultFrozen,
                    ManagerAddress = request.ManagerAddress,
                    ReserveAddress = request.ReserveAddress,
                    FreezeAddress = request.FreezeAddress,
                    ClawbackAddress = request.ClawbackAddress,
                    Metadata = request.Metadata
                }
            };
        }

        private static string GenerateMockTransactionId()
        {
            // Generate a 32-byte mock transaction ID
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").ToUpperInvariant();
        }
    }
}

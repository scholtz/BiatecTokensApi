using Algorand;
using Algorand.Algod;
using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
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
    public class ARC3FungibleTokenService : IARC3FungibleTokenService
    {
        private readonly IOptionsMonitor<AlgorandAuthenticationOptionsV2> _config;
        private readonly ILogger<ARC3FungibleTokenService> _logger;
        private readonly Dictionary<string, string> _genesisId2GenesisHash = new();
        private readonly IIPFSRepository _ipfsRepository;

        public ARC3FungibleTokenService(
            IOptionsMonitor<AlgorandAuthenticationOptionsV2> config,
            ILogger<ARC3FungibleTokenService> logger,
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
        public async Task<ARC3TokenDeploymentResponse> CreateTokenAsync(ARC3FungibleTokenDeploymentRequest request)
        {
            var response = new ARC3TokenDeploymentResponse { Success = false };

            try
            {
                _logger.LogInformation("Creating ARC3 token {Name} ({Symbol}) on {Network}",
                    request.Name, request.UnitName, request.Network);

                // Validate request
                if (!ValidateRequest(request, out string validationError))
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
                if (request.Metadata != null)
                {
                    (metadataUrl, metadataHash) = await UploadMetadataAsync(request.Metadata);
                    if (string.IsNullOrEmpty(metadataUrl))
                    {
                        response.ErrorMessage = "Failed to upload metadata to IPFS";
                        return response;
                    }
                }

                // For now, create a placeholder response since we need the actual Algorand SDK
                response = await CreateToken(request, algodApiInstance, metadataUrl, metadataHash);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token: {Message}", ex.Message);
                response.ErrorMessage = $"Failed to create token: {ex.Message}";
            }

            return response;
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
        public async Task<(string? Url, string? Hash)> UploadMetadataAsync(ARC3TokenMetadata metadata)
        {
            try
            {
                _logger.LogInformation("Uploading ARC3 metadata to IPFS");

                // Use the IPFS repository to upload the metadata
                var uploadResult = await _ipfsRepository.UploadObjectAsync(metadata, "arc3-metadata.json");

                if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Hash))
                {
                    _logger.LogInformation("Metadata uploaded to IPFS: {Hash} at {Url}", uploadResult.Hash, uploadResult.GatewayUrl);
                    return (uploadResult.GatewayUrl, uploadResult.Hash);
                }
                else
                {
                    _logger.LogError("Failed to upload metadata to IPFS: {Error}", uploadResult.ErrorMessage);
                    return (null, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading metadata: {Message}", ex.Message);
                return (null, null);
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

        private bool ValidateRequest(ARC3FungibleTokenDeploymentRequest request, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                error = "Token name is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.UnitName))
            {
                error = "Unit name is required";
                return false;
            }

            if (request.TotalSupply == 0)
            {
                error = "Total supply must be greater than 0";
                return false;
            }

            if (request.Decimals > 19)
            {
                error = "Decimals cannot exceed 19";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.CreatorMnemonic))
            {
                error = "Creator mnemonic is required";
                return false;
            }

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
            ARC3FungibleTokenDeploymentRequest request,
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

            await Task.Delay(1000); // Simulate processing time

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

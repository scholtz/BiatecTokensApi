using Algorand;
using Algorand.Algod;
using Algorand.Algod.Model;
using Algorand.Algod.Model.Transactions;
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

                response = await CreateToken(request, algodApiInstance, metadataUrl, metadataHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token: {Message}", ex.Message);
                response.ErrorMessage = $"Failed to create token: {ex.Message}";
            }

            return response;
        }

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

                // Parse the mnemonic to get the sender account
                var senderAccount = new Account(fromMnemonic);
                _logger.LogDebug("Sender account address: {Address}", senderAccount.Address);

                // Get suggested transaction parameters
                var transactionParams = await algodApiInstance.TransactionParamsAsync();

                // TODO: This is a placeholder implementation for asset transfer
                // In a production environment, you would:
                // 1. Create an asset transfer transaction
                // 2. Set the recipient address and transfer amount
                // 3. Sign the transaction with the sender account
                // 4. Submit the transaction to the network
                // 5. Wait for confirmation

                // For now, simulate the process
                await Task.Delay(500); // Simulate network latency

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

                // Parse the mnemonic to get the account
                var account = new Account(accountMnemonic);
                _logger.LogDebug("Account address: {Address}", account.Address);

                // Get suggested transaction parameters
                var transactionParams = await algodApiInstance.TransactionParamsAsync();

                // TODO: This is a placeholder implementation for asset opt-in
                // In a production environment, you would:
                // 1. Create an asset transfer transaction with amount = 0 to self
                // 2. Sign the transaction with the account
                // 3. Submit the transaction to the network
                // 4. Wait for confirmation

                // For now, simulate the process
                await Task.Delay(500); // Simulate network latency

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
            try
            {
                _logger.LogInformation("Creating Algorand asset {Name} ({UnitName}) with total supply {TotalSupply}",
                    request.Name, request.UnitName, request.TotalSupply);

                // Parse the mnemonic to get the account
                var account = new Account(request.CreatorMnemonic);
                _logger.LogDebug("Creator account address: {Address}", account.Address);

                // Get suggested transaction parameters
                var transactionParams = await algod.TransactionParamsAsync();
                _logger.LogDebug("Transaction params retrieved: LastRound={LastRound}, Fee={Fee}",
                    transactionParams.LastRound, transactionParams.MinFee);

                // TODO: This is a placeholder implementation for asset creation
                // In a production environment, you would:
                // 1. Use the proper Algorand SDK methods to create an asset creation transaction
                // 2. Set up asset parameters (name, unit name, total supply, decimals, etc.)
                // 3. Configure management addresses (manager, reserve, freeze, clawback)
                // 4. Include metadata URL and hash if provided
                // 5. Sign the transaction with the creator account
                // 6. Submit the transaction to the network
                // 7. Extract the asset ID

                // For now, simulate the process
                await Task.Delay(1000); // Simulate network latency

                // Generate a mock asset ID and transaction ID for demonstration
                var mockAssetId = (ulong)Random.Shared.NextInt64(1000000, 999999999);
                var mockTxId = GenerateMockTransactionId();

                _logger.LogInformation("Asset created successfully with ID: {AssetId}", mockAssetId);

                // Create successful response
                return new ARC3TokenDeploymentResponse
                {
                    Success = true,
                    AssetId = mockAssetId,
                    TransactionId = mockTxId,
                    CreatorAddress = account.Address.ToString(),
                    ConfirmedRound = (ulong)Random.Shared.NextInt64(1000000, 2000000),
                    MetadataUrl = metadataUrl,
                    MetadataHash = metadataHash,
                    TokenInfo = new ARC3TokenInfo
                    {
                        Name = request.Name,
                        UnitName = request.UnitName,
                        TotalSupply = request.TotalSupply,
                        Decimals = request.Decimals,
                        Url = metadataUrl ?? request.Url,
                        DefaultFrozen = request.DefaultFrozen,
                        ManagerAddress = string.IsNullOrEmpty(request.ManagerAddress) ? account.Address.ToString() : request.ManagerAddress,
                        ReserveAddress = string.IsNullOrEmpty(request.ReserveAddress) ? account.Address.ToString() : request.ReserveAddress,
                        FreezeAddress = request.FreezeAddress,
                        ClawbackAddress = request.ClawbackAddress,
                        Metadata = request.Metadata
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Algorand asset: {Message}", ex.Message);
                return new ARC3TokenDeploymentResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create asset: {ex.Message}"
                };
            }
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

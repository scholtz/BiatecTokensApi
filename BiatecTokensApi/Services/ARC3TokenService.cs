using Algorand;
using Algorand.Algod;
using Algorand.Algod.Model.Transactions;
using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
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
        private const ulong NFT_TOTAL_SUPPLY = 1;
        private const uint NFT_DECIMALS = 0;

        private readonly IOptionsMonitor<AlgorandAuthenticationOptionsV2> _config;
        private readonly ILogger<ARC3TokenService> _logger;
        private readonly Dictionary<string, string> _genesisId2GenesisHash = new();
        private readonly IIPFSRepository _ipfsRepository;
        private readonly IASATokenService _asaTokenService;
        private readonly ITokenIssuanceRepository _tokenIssuanceRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
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
        /// <param name="asaTokenService">Token service to create ASAs</param>
        /// <param name="tokenIssuanceRepository">The token issuance audit repository</param>
        /// <param name="httpContextAccessor">HTTP context accessor for correlation ID extraction</param>
        public ARC3TokenService(
            IOptionsMonitor<AlgorandAuthenticationOptionsV2> config,
            ILogger<ARC3TokenService> logger,
            IIPFSRepository ipfsRepository,
            IASATokenService asaTokenService,
            ITokenIssuanceRepository tokenIssuanceRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _logger = logger;
            _ipfsRepository = ipfsRepository;
            _asaTokenService = asaTokenService;
            _tokenIssuanceRepository = tokenIssuanceRepository;
            _httpContextAccessor = httpContextAccessor;
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
        public Task<ARC3TokenDeploymentResponse> CreateARC3TokenAsync(IARC3TokenDeploymentRequest request, TokenType tokenType)
        {
            if (tokenType == TokenType.ARC3_FNFT)
            {
                if (request is ARC3FractionalNonFungibleTokenDeploymentRequest fnftRequest)
                {
                    return CreateARC3FNFTTokenAsync(fnftRequest);
                }
            }
            throw new Exception("Unsupported token type for ARC3: " + tokenType);
        }


        /// <summary>
        /// Creates an ARC3 fractional non fungible token on Algorand blockchain
        /// </summary>
        public async Task<ARC3TokenDeploymentResponse> CreateARC3FNFTTokenAsync(ARC3FractionalNonFungibleTokenDeploymentRequest request)
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

                var asaRequest = new ASAFractionalNonFungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    TotalSupply = request.TotalSupply,
                    Decimals = request.Decimals,
                    Url = metadataUrl ?? throw new Exception("Metadata URL is empty"),
                    MetadataHash = Convert.FromHexString(sha256Hash ?? throw new Exception("Hash is empty")),
                    Network = request.Network
                };

                var asaResponse = await _asaTokenService.CreateASATokenAsync(asaRequest, TokenType.ASA_FNFT);

                response.Success = asaResponse.Success;
                response.ErrorMessage = asaResponse.ErrorMessage;
                response.TransactionId = asaResponse.TransactionId;
                response.AssetId = asaResponse.AssetId;
                response.TokenInfo = asaResponse.TokenInfo;
                response.MetadataUrl = metadataUrl;
                response.MetadataHash = metadataHash;
                response.ConfirmedRound = asaResponse.ConfirmedRound;

                // Log audit entry
                await LogTokenIssuanceAudit(
                    request.Name,
                    request.UnitName,
                    request.TotalSupply,
                    request.Decimals,
                    TokenType.ARC3_FNFT,
                    asaResponse.AssetId,
                    asaResponse.TransactionId,
                    asaResponse.CreatorAddress ?? string.Empty,
                    request.Network,
                    asaResponse.ConfirmedRound,
                    asaResponse.Success,
                    asaResponse.ErrorMessage,
                    metadataUrl: metadataUrl);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token: {Message}", ex.Message);
                response.ErrorMessage = $"Failed to create token: {ex.Message}";

                // Log audit entry for failure
                await LogTokenIssuanceAudit(
                    request.Name,
                    request.UnitName,
                    request.TotalSupply,
                    request.Decimals,
                    TokenType.ARC3_FNFT,
                    null,
                    null,
                    string.Empty,
                    request.Network,
                    null,
                    false,
                    ex.Message);
            }
            return response;
        }

        /// <summary>
        /// Creates an ARC3 fungible token on Algorand blockchain
        /// </summary>
        public async Task<ARC3TokenDeploymentResponse> CreateARC3FTTokenAsync(ARC3FungibleTokenDeploymentRequest request)
        {

            ValidateARC3Request(request, TokenType.ARC3_FT);

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

                var asaRequest = new ASAFungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    TotalSupply = request.TotalSupply,
                    Decimals = request.Decimals,
                    Url = metadataUrl ?? throw new Exception("Metadata URL is empty"),
                    MetadataHash = Convert.FromHexString(sha256Hash ?? throw new Exception("Hash is empty")),
                    Network = request.Network
                };

                var asaResponse = await _asaTokenService.CreateASATokenAsync(asaRequest, TokenType.ASA_FT);

                response.Success = asaResponse.Success;
                response.ErrorMessage = asaResponse.ErrorMessage;
                response.TransactionId = asaResponse.TransactionId;
                response.AssetId = asaResponse.AssetId;
                response.TokenInfo = asaResponse.TokenInfo;
                response.MetadataUrl = metadataUrl;
                response.MetadataHash = metadataHash;
                response.ConfirmedRound = asaResponse.ConfirmedRound;

                // Log audit entry
                await LogTokenIssuanceAudit(
                    request.Name,
                    request.UnitName,
                    request.TotalSupply,
                    request.Decimals,
                    TokenType.ARC3_FT,
                    asaResponse.AssetId,
                    asaResponse.TransactionId,
                    asaResponse.CreatorAddress ?? string.Empty,
                    request.Network,
                    asaResponse.ConfirmedRound,
                    asaResponse.Success,
                    asaResponse.ErrorMessage,
                    metadataUrl: metadataUrl);

                return response;
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
        public async Task<ARC3TokenDeploymentResponse> CreateARC3NFTTokenAsync(ARC3NonFungibleTokenDeploymentRequest request)
        {

            ValidateARC3Request(request, TokenType.ARC3_NFT);

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

                var asaRequest = new ASANonFungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    Url = metadataUrl ?? throw new Exception("Metadata URL is empty"),
                    MetadataHash = Convert.FromHexString(sha256Hash ?? throw new Exception("Hash is empty")),
                    Network = request.Network
                };

                var asaResponse = await _asaTokenService.CreateASATokenAsync(asaRequest, TokenType.ASA_NFT);

                response.Success = asaResponse.Success;
                response.ErrorMessage = asaResponse.ErrorMessage;
                response.TransactionId = asaResponse.TransactionId;
                response.AssetId = asaResponse.AssetId;
                response.TokenInfo = asaResponse.TokenInfo;
                response.MetadataUrl = metadataUrl;
                response.MetadataHash = metadataHash;
                response.ConfirmedRound = asaResponse.ConfirmedRound;

                // Log audit entry (NFTs have total supply of 1 and 0 decimals)
                await LogTokenIssuanceAudit(
                    request.Name,
                    request.UnitName,
                    NFT_TOTAL_SUPPLY,
                    NFT_DECIMALS,
                    TokenType.ARC3_NFT,
                    asaResponse.AssetId,
                    asaResponse.TransactionId,
                    asaResponse.CreatorAddress ?? string.Empty,
                    request.Network,
                    asaResponse.ConfirmedRound,
                    asaResponse.Success,
                    asaResponse.ErrorMessage,
                    metadataUrl: metadataUrl);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token: {Message}", ex.Message);
                response.ErrorMessage = $"Failed to create token: {ex.Message}";

                // Log audit entry for failure (NFTs have total supply of 1 and 0 decimals)
                await LogTokenIssuanceAudit(
                    request.Name,
                    request.UnitName,
                    NFT_TOTAL_SUPPLY,
                    NFT_DECIMALS,
                    TokenType.ARC3_NFT,
                    null,
                    null,
                    string.Empty,
                    request.Network,
                    null,
                    false,
                    ex.Message);
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

        private bool ValidateARC3Request(IARC3TokenDeploymentRequest? request, TokenType tokenType)
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

        private async Task LogTokenIssuanceAudit(
            string? tokenName,
            string? tokenSymbol,
            ulong? totalSupply,
            uint decimals,
            TokenType tokenType,
            ulong? assetId,
            string? transactionId,
            string creatorAddress,
            string network,
            ulong? confirmedRound,
            bool success,
            string? errorMessage,
            string? managerAddress = null,
            string? reserveAddress = null,
            string? freezeAddress = null,
            string? clawbackAddress = null,
            string? metadataUrl = null)
        {
            try
            {
                var auditEntry = new TokenIssuanceAuditLogEntry
                {
                    AssetId = assetId,
                    AssetIdentifier = assetId?.ToString(),
                    Network = network,
                    TokenType = tokenType.ToString(),
                    TokenName = tokenName,
                    TokenSymbol = tokenSymbol,
                    TotalSupply = totalSupply?.ToString(),
                    Decimals = (int)decimals,
                    DeployedBy = creatorAddress,
                    DeployedAt = DateTime.UtcNow,
                    Success = success,
                    ErrorMessage = errorMessage,
                    TransactionHash = transactionId,
                    ConfirmedRound = confirmedRound,
                    ManagerAddress = managerAddress,
                    ReserveAddress = reserveAddress,
                    FreezeAddress = freezeAddress,
                    ClawbackAddress = clawbackAddress,
                    MetadataUrl = metadataUrl,
                    IsMintable = !string.IsNullOrEmpty(managerAddress),
                    IsBurnable = !string.IsNullOrEmpty(clawbackAddress),
                    CorrelationId = _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString()
                };

                await _tokenIssuanceRepository.AddAuditLogEntryAsync(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging token issuance audit entry");
            }
        }
    }
}

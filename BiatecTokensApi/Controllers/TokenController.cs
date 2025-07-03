using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/token")]
    public class TokenController : ControllerBase
    {
        private readonly IERC20TokenService _erc20TokenService;
        private readonly IARC3FungibleTokenService _arc3TokenService;
        private readonly ILogger<TokenController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenController"/> class.
        /// </summary>
        /// <param name="erc20TokenService">The service used to interact with ERC-20 tokens.</param>
        /// <param name="arc3TokenService">The service used to interact with ARC-3 fungible tokens.</param>
        /// <param name="logger">The logger instance used to log diagnostic and operational information.</param>
        public TokenController(
            IERC20TokenService erc20TokenService,
            IARC3FungibleTokenService arc3TokenService,
            ILogger<TokenController> logger)
        {
            _erc20TokenService = erc20TokenService;
            _arc3TokenService = arc3TokenService;
            _logger = logger;
        }

        /// <summary>
        /// Deploys a new BiatecToken on the Base blockchain.
        /// BiatecToken is an advanced ERC20 token with additional features:
        /// - Minting capabilities (owner and authorized minters)
        /// - Burning capabilities (burn and burnFrom)
        /// - Pausable functionality (owner can pause/unpause transfers)
        /// - Ownable (ownership transfer functionality)
        /// The deployer automatically becomes the owner and first minter.
        /// The initial token supply can be sent to a specified address or the deployer.
        /// </summary>
        /// <param name="request">Token deployment parameters including optional initial supply receiver</param>
        /// <returns>Deployment result with contract address and initial supply receiver</returns>
        [HttpPost("erc20/create")]
        [ProducesResponseType(typeof(TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeployToken([FromBody] TokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _erc20TokenService.DeployTokenAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("BiatecToken deployed successfully at address {Address} with transaction {TxHash}",
                        result.ContractAddress, result.TransactionHash);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("BiatecToken deployment failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying BiatecToken");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Creates a simple ASA (Algorand Standard Asset) without metadata on the specified network.
        /// ASA tokens are basic Algorand Standard Assets with essential properties:
        /// - Simple asset creation without ARC3 metadata compliance
        /// - Basic management features (freeze, clawback, reserve)
        /// - No IPFS metadata storage
        /// - Suitable for basic tokenization use cases
        /// The creator becomes the initial manager and can configure additional roles.
        /// </summary>
        /// <param name="request">ASA token creation parameters without metadata</param>
        /// <returns>Creation result with asset ID and transaction details</returns>
        [HttpPost("asa/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateASAToken([FromBody] ASATokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Convert ASA request to ARC3 request without metadata
                var arc3Request = new ARC3FungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    TotalSupply = request.TotalSupply,
                    Decimals = request.Decimals,
                    DefaultFrozen = request.DefaultFrozen,
                    ManagerAddress = request.ManagerAddress,
                    ReserveAddress = request.ReserveAddress,
                    FreezeAddress = request.FreezeAddress,
                    ClawbackAddress = request.ClawbackAddress,
                    CreatorMnemonic = request.CreatorMnemonic,
                    Network = request.Network,
                    Metadata = null // No metadata for simple ASA
                };

                var result = await _arc3TokenService.CreateTokenAsync(arc3Request);

                if (result.Success)
                {
                    _logger.LogInformation("ASA token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}",
                        result.AssetId, result.TransactionId, request.Network);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ASA token creation failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ASA token");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new ARC3 Fungible Token on the Algorand blockchain.
        /// ARC3 tokens are Algorand Standard Assets (ASAs) that comply with the ARC3 metadata standard:
        /// - Support for rich metadata including images, descriptions, and properties
        /// - IPFS-based metadata storage with integrity verification
        /// - Localization support for international use
        /// - Optional management features (freeze, clawback, reserve)
        /// The creator becomes the initial manager and can configure additional roles.
        /// </summary>
        /// <param name="request">ARC3 token creation parameters including metadata</param>
        /// <returns>Creation result with asset ID and transaction details</returns>
        [HttpPost("arc3-ft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3Token([FromBody] ARC3FungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _arc3TokenService.CreateTokenAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("ARC3 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}",
                        result.AssetId, result.TransactionId, request.Network);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC3 token creation failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 token");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Creates an ARC3-compliant non-fungible token (NFT) on the specified network.
        /// ARC3 NFTs are unique tokens with a total supply of exactly 1:
        /// - Comply with ARC3 metadata standard for rich content
        /// - IPFS-based metadata storage with integrity verification
        /// - Unique, non-divisible tokens (total supply = 1, decimals = 0)
        /// - Support for images, descriptions, and properties
        /// - Optional management features (freeze, clawback, reserve)
        /// The creator becomes the initial manager and owns the single token.
        /// </summary>
        /// <param name="request">ARC3 NFT creation parameters with required metadata</param>
        /// <returns>Creation result with asset ID and transaction details</returns>
        [HttpPost("arc3-nft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3NFT([FromBody] ARC3NFTDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Convert NFT request to ARC3 request with forced NFT properties
                var arc3Request = new ARC3FungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    TotalSupply = 1, // NFTs must have total supply of 1
                    Decimals = 0, // NFTs are not divisible
                    DefaultFrozen = request.DefaultFrozen,
                    ManagerAddress = request.ManagerAddress,
                    ReserveAddress = request.ReserveAddress,
                    FreezeAddress = request.FreezeAddress,
                    ClawbackAddress = request.ClawbackAddress,
                    CreatorMnemonic = request.CreatorMnemonic,
                    Network = request.Network,
                    Metadata = request.Metadata
                };

                var result = await _arc3TokenService.CreateTokenAsync(arc3Request);

                if (result.Success)
                {
                    _logger.LogInformation("ARC3 NFT created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}",
                        result.AssetId, result.TransactionId, request.Network);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC3 NFT creation failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 NFT");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Creates an ARC3 fractional non-fungible token (FNFT) with supply matching decimals precision.
        /// ARC3 FNFTs are divisible tokens that represent fractions of a whole asset:
        /// - Total supply is set to 10^decimals to enable fractional ownership
        /// - Comply with ARC3 metadata standard for rich content
        /// - IPFS-based metadata storage with integrity verification
        /// - Divisible tokens allowing fractional ownership
        /// - Support for images, descriptions, and properties
        /// The creator becomes the initial manager and owns all fractional units.
        /// </summary>
        /// <param name="request">ARC3 FNFT creation parameters with metadata</param>
        /// <returns>Creation result with asset ID and transaction details</returns>
        [HttpPost("arc3-fnft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3FractionalNFT([FromBody] ARC3FungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // For fractional NFTs, set total supply to 10^decimals
                var totalSupply = (ulong)Math.Pow(10, request.Decimals);
                
                // Create a new request with the calculated total supply
                var fnftRequest = new ARC3FungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.UnitName,
                    TotalSupply = totalSupply, // 10^decimals for fractional ownership
                    Decimals = request.Decimals,
                    DefaultFrozen = request.DefaultFrozen,
                    ManagerAddress = request.ManagerAddress,
                    ReserveAddress = request.ReserveAddress,
                    FreezeAddress = request.FreezeAddress,
                    ClawbackAddress = request.ClawbackAddress,
                    CreatorMnemonic = request.CreatorMnemonic,
                    Network = request.Network,
                    Metadata = request.Metadata
                };

                var result = await _arc3TokenService.CreateTokenAsync(fnftRequest);

                if (result.Success)
                {
                    _logger.LogInformation("ARC3 FNFT created successfully with asset ID {AssetId}, total supply {TotalSupply} (10^{Decimals}), and transaction {TxHash} on {Network}",
                        result.AssetId, totalSupply, request.Decimals, result.TransactionId, request.Network);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC3 FNFT creation failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC3 FNFT");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Creates an ARC200 token using ERC20-style parameters.
        /// ARC200 tokens are designed to be similar to ERC20 tokens but on Algorand:
        /// - Uses familiar ERC20-style parameters (name, symbol, initial supply, decimals)
        /// - Automatic total supply calculation based on initial supply and decimals
        /// - No metadata storage (simple token creation)
        /// - Compatible with ERC20 migration workflows
        /// The creator receives the initial token supply and becomes the manager.
        /// </summary>
        /// <param name="request">ARC200 token creation parameters using ERC20-style inputs</param>
        /// <returns>Creation result with asset ID and transaction details</returns>
        [HttpPost("arc200/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC200FractionalNFT([FromBody] ARC200DeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Calculate total supply: initial supply * 10^decimals
                var totalSupply = (ulong)(request.InitialSupply * (decimal)Math.Pow(10, request.Decimals));
                
                // Convert ARC200 request to ARC3 request (ERC20-style parameters)
                var arc3Request = new ARC3FungibleTokenDeploymentRequest
                {
                    Name = request.Name,
                    UnitName = request.Symbol, // Use Symbol as UnitName for ERC20 compatibility
                    TotalSupply = totalSupply,
                    Decimals = (uint)request.Decimals,
                    DefaultFrozen = false, // Standard behavior for ERC20-like tokens
                    ManagerAddress = request.InitialSupplyReceiver, // Manager gets the tokens
                    ReserveAddress = null,
                    FreezeAddress = null,
                    ClawbackAddress = null,
                    CreatorMnemonic = request.CreatorMnemonic,
                    Network = request.Network,
                    Metadata = null // No metadata for ERC20-style tokens
                };

                var result = await _arc3TokenService.CreateTokenAsync(arc3Request);

                if (result.Success)
                {
                    _logger.LogInformation("ARC200 token created successfully with asset ID {AssetId}, initial supply {InitialSupply}, total supply {TotalSupply}, and transaction {TxHash} on {Network}",
                        result.AssetId, request.InitialSupply, totalSupply, result.TransactionId, request.Network);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC200 token creation failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ARC200 token");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        //    /// <summary>
        //    /// Retrieves information about an existing ARC3 token including metadata.
        //    /// </summary>
        //    /// <param name="assetId">The asset ID of the ARC3 token</param>
        //    /// <param name="network">The Algorand network (mainnet, testnet, betanet)</param>
        //    /// <returns>Token information including metadata if available</returns>
        //    [HttpGet("arc3/{assetId}")]
        //    [ProducesResponseType(typeof(ARC3TokenInfo), StatusCodes.Status200OK)]
        //    [ProducesResponseType(StatusCodes.Status404NotFound)]
        //    [ProducesResponseType(StatusCodes.Status400BadRequest)]
        //    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        //    public async Task<IActionResult> GetARC3TokenInfo(ulong assetId, [FromQuery] string network = "testnet")
        //    {
        //        if (string.IsNullOrWhiteSpace(network))
        //        {
        //            return BadRequest("Network parameter is required");
        //        }

        //        try
        //        {
        //            var tokenInfo = await _arc3TokenService.GetTokenInfoAsync(assetId, network);

        //            if (tokenInfo == null)
        //            {
        //                _logger.LogWarning("ARC3 token with asset ID {AssetId} not found on {Network}", assetId, network);
        //                return NotFound($"Token with asset ID {assetId} not found on {network}");
        //            }

        //            _logger.LogInformation("Retrieved ARC3 token info for asset ID {AssetId} on {Network}", assetId, network);
        //            return Ok(tokenInfo);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error retrieving ARC3 token info for asset ID {AssetId}", assetId);
        //            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        //        }
        //    }


        //    /// <summary>
        //    /// Validates ARC3 metadata structure without creating a token.
        //    /// Useful for testing metadata before token creation.
        //    /// </summary>
        //    /// <param name="metadata">ARC3 metadata to validate</param>
        //    /// <returns>Validation result</returns>
        //    [HttpPost("arc3/validate-metadata")]
        //    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        //    [ProducesResponseType(StatusCodes.Status400BadRequest)]
        //    public IActionResult ValidateARC3Metadata([FromBody] ARC3TokenMetadata metadata)
        //    {
        //        if (!ModelState.IsValid)
        //        {
        //            return BadRequest(ModelState);
        //        }

        //        try
        //        {
        //            var (isValid, errorMessage) = _arc3TokenService.ValidateMetadata(metadata);

        //            _logger.LogInformation("ARC3 metadata validation result: {IsValid}", isValid);
        //            return Ok(new { isValid, errorMessage });
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error validating ARC3 metadata");
        //            return BadRequest(new { error = ex.Message });
        //        }
        //    }
    }
}


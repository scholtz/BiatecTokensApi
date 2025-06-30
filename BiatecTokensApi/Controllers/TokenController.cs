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
        /// Creates an ARC3 fungible token on the specified network.
        /// </summary>
        /// <remarks>This method processes a request to deploy an ARC3 fungible token using the provided
        /// deployment parameters. It validates the request model and interacts with the token service to create the
        /// token. If the token creation is successful, the method returns a response containing the asset ID and
        /// transaction details. If the creation fails, an error response is returned.</remarks>
        /// <param name="request">The deployment request containing the parameters required to create the ARC3 fungible token. This includes
        /// details such as the network, token properties, and other configuration settings.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the operation. Returns a 200 OK response with an
        /// <see cref="ARC3TokenDeploymentResponse"/> if the token is created successfully. Returns a 400 Bad Request
        /// response if the request model is invalid. Returns a 500 Internal Server Error response if an error occurs
        /// during token creation.</returns>
        [HttpPost("asa/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateASAToken([FromBody] ARC3FungibleTokenDeploymentRequest request)
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
        /// </summary>
        /// <remarks>This method accepts a deployment request containing the necessary parameters to
        /// create an ARC3 NFT. It validates the request model and interacts with the token service to perform the
        /// creation. If the operation is successful, the method returns the details of the created token, including the
        /// asset ID and transaction hash. In case of failure, it returns an appropriate error response.</remarks>
        /// <param name="request">The deployment request containing the parameters required to create the ARC3 NFT. This includes details such
        /// as the network, metadata, and other token-specific configurations.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the operation: <list type="bullet">
        /// <item><description>A 200 OK response with an <see cref="ARC3TokenDeploymentResponse"/> if the token is
        /// created successfully.</description></item> <item><description>A 400 Bad Request response if the request
        /// model is invalid.</description></item> <item><description>A 500 Internal Server Error response if an
        /// unexpected error occurs during token creation.</description></item> </list></returns>
        [HttpPost("arc3-nft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3NFT([FromBody] ARC3FungibleTokenDeploymentRequest request)
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
        /// Creates an ARC3 fractional non-fungible token (FNFT) based on the provided deployment request.
        /// </summary>
        /// <remarks>This method processes the deployment request for an ARC3 FNFT and returns the result
        /// of the operation. The request must include all required parameters for token creation, and the model state
        /// must be valid. If the operation succeeds, the response contains details about the created token, including
        /// the asset ID and transaction hash. If the operation fails, an appropriate error response is
        /// returned.</remarks>
        /// <param name="request">The deployment request containing the necessary parameters for creating the ARC3 fractional token. This must
        /// be a valid <see cref="ARC3FungibleTokenDeploymentRequest"/> object.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the operation: - A 200 OK response with an <see
        /// cref="ARC3TokenDeploymentResponse"/> if the token is successfully created. - A 400 Bad Request response if
        /// the model state is invalid. - A 500 Internal Server Error response if an error occurs during token creation.</returns>
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
        /// Creates a fractional ARC200 NFT based on the provided deployment request.
        /// </summary>
        /// <remarks>This method processes the deployment request for an ARC200 fractional NFT and returns
        /// the result of the operation. The request must be valid, as determined by model validation. If the operation
        /// succeeds, the response contains details about the created token, including its asset ID and transaction
        /// hash. If the operation fails, an error response is returned.</remarks>
        /// <param name="request">The deployment request containing the necessary parameters for creating the ARC200 fractional NFT. This
        /// includes details such as the network, token configuration, and other required metadata.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the operation: <list type="bullet">
        /// <item><description>A 200 OK response with the token creation details if successful.</description></item>
        /// <item><description>A 400 Bad Request response if the request is invalid.</description></item>
        /// <item><description>A 500 Internal Server Error response if an unexpected error occurs during
        /// processing.</description></item> </list></returns>
        [HttpPost("arc200/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC200FractionalNFT([FromBody] ARC3FungibleTokenDeploymentRequest request)
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


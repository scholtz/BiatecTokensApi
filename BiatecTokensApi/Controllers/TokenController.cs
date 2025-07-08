using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC200.Response;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.EVM;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for creating and managing various types of tokens, including ERC-20, ARC3 fungible tokens,
    /// ARC3 non-fungible tokens (NFTs), and ARC200 tokens.
    /// </summary>
    /// <remarks>This controller includes methods for deploying and managing tokens on different blockchain
    /// networks. It supports advanced token standards such as ERC-20 and ARC3, offering features like minting, burning,
    /// pausing, and metadata validation. Each endpoint validates the input request, interacts with the corresponding
    /// token service, and returns appropriate responses based on the operation's success or failure.</remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/token")]
    public class TokenController : ControllerBase
    {
        private readonly IERC20TokenService _erc20TokenService;
        private readonly IARC3TokenService _arc3TokenService;
        private readonly IASATokenService _asaTokenService;
        private readonly IARC200TokenService _arc200TokenService;
        private readonly ILogger<TokenController> _logger;
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenController"/> class.
        /// </summary>
        /// <param name="erc20TokenService">The service used to interact with ERC-20 tokens.</param>
        /// <param name="arc3TokenService">The service used to interact with ARC-3 tokens.</param>
        /// <param name="asaTokenService">The service used to interact with ASA tokens.</param>
        /// <param name="arc200TokenService">The service used to interact with ARC-200 tokens</param>
        /// <param name="logger">The logger instance used to log diagnostic and operational information.</param>
        public TokenController(
            IERC20TokenService erc20TokenService,
            IARC3TokenService arc3TokenService,
            IASATokenService asaTokenService,
            IARC200TokenService arc200TokenService,
            ILogger<TokenController> logger)
        {
            _erc20TokenService = erc20TokenService;
            _arc3TokenService = arc3TokenService;
            _asaTokenService = asaTokenService;
            _arc200TokenService = arc200TokenService;
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
        [HttpPost("erc20-mintable/create")]
        [ProducesResponseType(typeof(EVMTokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeployToken([FromBody] ERC20TokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _erc20TokenService.DeployERC20TokenAsync(request, TokenType.ERC20_Mintable);

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
        [HttpPost("asa-ft/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ASATokenDeploymentResponse>> CreateASAToken([FromBody] ARC3FungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_FT);

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
        /// Creates an ASA NFT (Algorand Standard Asset Non-Fungible Token) based on the provided deployment request. It creates basic ASA token with quantity of 1. If you want to serve also the picture for the NFT token, use the ARC3 NFT standard instead.
        /// </summary>
        /// <remarks>This method validates the input request and attempts to create an ASA NFT using the
        /// provided parameters.  If the operation is successful, the response includes details such as the asset ID and
        /// transaction hash.  In case of failure, appropriate error information is returned.</remarks>
        /// <param name="request">The deployment request containing the necessary parameters for creating the ASA NFT,  including network
        /// details and token-specific configurations. This parameter cannot be null.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the operation.  Returns a 200 OK response with an
        /// <see cref="ASATokenDeploymentResponse"/> if the token is created successfully.  Returns a 400 Bad Request
        /// response if the request is invalid.  Returns a 500 Internal Server Error response if an unexpected error
        /// occurs during the operation.</returns>
        [HttpPost("asa-nft/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ASATokenDeploymentResponse>> CreateASANFT([FromBody] ASANonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_NFT);

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
        /// Creates an ASA NFT (Algorand Standard Asset Non-Fungible Token) based on the provided deployment request. It creates basic ASA token with quantity of 1. If you want to serve also the picture for the NFT token, use the ARC3 NFT standard instead.
        /// </summary>
        /// <remarks>This method validates the input request and attempts to create an ASA NFT using the
        /// provided parameters.  If the operation is successful, the response includes details such as the asset ID and
        /// transaction hash.  In case of failure, appropriate error information is returned.</remarks>
        /// <param name="request">The deployment request containing the necessary parameters for creating the ASA NFT,  including network
        /// details and token-specific configurations. This parameter cannot be null.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the operation.  Returns a 200 OK response with an
        /// <see cref="ASATokenDeploymentResponse"/> if the token is created successfully.  Returns a 400 Bad Request
        /// response if the request is invalid.  Returns a 500 Internal Server Error response if an unexpected error
        /// occurs during the operation.</returns>
        [HttpPost("asa-fnft/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ASATokenDeploymentResponse>> CreateASAFNFT([FromBody] ASAFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_FNFT);

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
        public async Task<ActionResult<ARC3TokenDeploymentResponse>> CreateARC3FungibleToken([FromBody] ARC3FungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_FT);

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
        public async Task<ActionResult<ARC3TokenDeploymentResponse>> CreateARC3NFT([FromBody] ARC3NonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_NFT);

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
        /// be a valid <see cref="ARC3NonFungibleTokenDeploymentRequest"/> object.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the operation: - A 200 OK response with an <see
        /// cref="ARC3TokenDeploymentResponse"/> if the token is successfully created. - A 400 Bad Request response if
        /// the model state is invalid. - A 500 Internal Server Error response if an error occurs during token creation.</returns>
        [HttpPost("arc3-fnft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ARC3TokenDeploymentResponse>> CreateARC3FractionalNFT([FromBody] ARC3NonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_FNFT);

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
        /// Creates a new ARC200 mintable token based on the provided deployment request.
        /// </summary>
        /// <remarks>This method validates the input request and attempts to create an ARC200 mintable
        /// token using the provided details. If the operation succeeds, the response includes the asset ID and
        /// transaction details. In case of failure, an appropriate error response is returned.</remarks>
        /// <param name="request">The deployment request containing the configuration details for the ARC200 mintable token. This includes
        /// information such as the token name, symbol, initial supply, and network.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the token creation operation: - A 200 OK response
        /// with an <see cref="ARC200TokenDeploymentResponse"/> if the token is successfully created. - A 400 Bad Request
        /// response if the request is invalid. - A 500 Internal Server Error response if an unexpected error occurs
        /// during the operation.</returns>
        [HttpPost("arc200-mintable/create")]
        [ProducesResponseType(typeof(ARC200TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ARC200TokenDeploymentResponse>> ARC200MintableTokenDeploymentRequest([FromBody] ARC200MintableTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _arc200TokenService.CreateARC200TokenAsync(request, TokenType.ARC200_Mintable);

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
        /// Creates a new ARC200 preminted fungible token based on the provided deployment request.
        /// </summary>
        /// <remarks>This method validates the input request and attempts to create an ARC200 preminted
        /// token using the provided details. If the operation is successful, the response includes the asset ID and
        /// transaction details. If the operation fails, an appropriate error response is returned.</remarks>
        /// <param name="request">The deployment request containing the details required to create the ARC200 preminted token, including
        /// network information and token parameters. This parameter cannot be null and must pass model validation.</param>
        /// <returns>An <see cref="IActionResult"/> containing the result of the token creation operation. Returns a 200 OK
        /// response with an <see cref="ARC3TokenDeploymentResponse"/> if the token is created successfully, a 400 Bad
        /// Request response if the request is invalid, or a 500 Internal Server Error response if an unexpected error
        /// occurs.</returns>
        [HttpPost("arc200-preminted/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ARC200TokenDeploymentResponse>> CreateARC200Preminted([FromBody] ARC200PremintedTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _arc200TokenService.CreateARC200TokenAsync(request, TokenType.ARC200_Preminted);

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
    }
}
using BiatecTokensApi.Filters;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC200.Response;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.Compliance;
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
        private readonly IARC1400TokenService _arc1400TokenService;
        private readonly IComplianceService _complianceService;
        private readonly ILogger<TokenController> _logger;
        private readonly IHostEnvironment _env;
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenController"/> class.
        /// </summary>
        /// <param name="erc20TokenService">The service used to interact with ERC-20 tokens.</param>
        /// <param name="arc3TokenService">The service used to interact with ARC-3 tokens.</param>
        /// <param name="asaTokenService">The service used to interact with ASA tokens.</param>
        /// <param name="arc200TokenService">The service used to interact with ARC-200 tokens</param>
        /// <param name="arc1400TokenService">The service used to interact with ARC-1400 tokens</param>
        /// <param name="complianceService">The service used to interact with compliance metadata</param>
        /// <param name="logger">The logger instance used to log diagnostic and operational information.</param>
        /// <param name="env">The host environment for determining runtime environment.</param>
        public TokenController(
            IERC20TokenService erc20TokenService,
            IARC3TokenService arc3TokenService,
            IASATokenService asaTokenService,
            IARC200TokenService arc200TokenService,
            IARC1400TokenService arc1400TokenService,
            IComplianceService complianceService,
            ILogger<TokenController> logger,
            IHostEnvironment env)
        {
            _erc20TokenService = erc20TokenService;
            _arc3TokenService = arc3TokenService;
            _asaTokenService = asaTokenService;
            _arc200TokenService = arc200TokenService;
            _arc1400TokenService = arc1400TokenService;
            _complianceService = complianceService;
            _logger = logger;
            _env = env;
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
        /// <remarks>
        /// **Idempotency Support:**
        /// This endpoint supports idempotency via the Idempotency-Key header. Include a unique key in your request:
        /// ```
        /// Idempotency-Key: unique-deployment-id-12345
        /// ```
        /// If a request with the same key is received within 24 hours, the cached response will be returned.
        /// This prevents accidental duplicate deployments.
        /// </remarks>
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("erc20-mintable/create")]
        [ProducesResponseType(typeof(EVMTokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ERC20MintableTokenCreate([FromBody] ERC20MintableTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _erc20TokenService.DeployERC20TokenAsync(request, TokenType.ERC20_Mintable);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("BiatecToken deployed successfully at address {Address} with transaction {TxHash}. CorrelationId: {CorrelationId}",
                        result.ContractAddress, result.TransactionHash, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("BiatecToken deployment failed: {Error}. CorrelationId: {CorrelationId}", 
                        result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ERC20 mintable token deployment");
            }
        }

        /// <summary>
        /// Deploys a new ERC20 preminted token based on the provided deployment request.
        /// </summary>
        /// <remarks>This method logs the deployment status and any errors encountered during the
        /// process.
        /// 
        /// **Idempotency Support:**
        /// This endpoint supports idempotency via the Idempotency-Key header. Include a unique key in your request
        /// to prevent accidental duplicate deployments.
        /// </remarks>
        /// <param name="request">The <see cref="ERC20TokenDeploymentRequest"/> containing the parameters for the token deployment. Must be a
        /// valid model; otherwise, a 400 Bad Request response is returned.</param>
        /// <returns>An <see cref="IActionResult"/> representing the result of the token deployment operation. Returns a 200 OK
        /// response with an <see cref="EVMTokenDeploymentResponse"/> if the deployment is successful. Returns a 400 Bad
        /// Request response if the request model is invalid. Returns a 500 Internal Server Error response if an error
        /// occurs during deployment.</returns>
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("erc20-preminted/create")]
        [ProducesResponseType(typeof(EVMTokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ERC20PremnitedTokenCreate([FromBody] ERC20PremintedTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _erc20TokenService.DeployERC20TokenAsync(request, TokenType.ERC20_Preminted);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("BiatecToken deployed successfully at address {Address} with transaction {TxHash}. CorrelationId: {CorrelationId}",
                        result.ContractAddress, result.TransactionHash, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("BiatecToken deployment failed: {Error}. CorrelationId: {CorrelationId}", 
                        result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ERC20 preminted token deployment");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("asa-ft/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateASAToken([FromBody] ASAFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_FT);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ASA FT token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ASA FT token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ASA fungible token creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("asa-nft/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateASANFT([FromBody] ASANonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_NFT);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ASA token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ASA token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ASA NFT creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("asa-fnft/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateASAFNFT([FromBody] ASAFractionalNonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_FNFT);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ASA token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ASA token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ASA fractional NFT creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("arc3-ft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3FungibleToken([FromBody] ARC3FungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_FT);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ARC3 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC3 token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ARC3 fungible token creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("arc3-nft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3NFT([FromBody] ARC3NonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_NFT);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ARC3 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC3 token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ARC3 NFT creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("arc3-fnft/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC3FractionalNFT([FromBody] ARC3FractionalNonFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_FNFT);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ARC3 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC3 token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ARC3 fractional NFT creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("arc200-mintable/create")]
        [ProducesResponseType(typeof(ARC200TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ARC200MintableTokenDeploymentRequest([FromBody] ARC200MintableTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _arc200TokenService.CreateARC200TokenAsync(request, TokenType.ARC200_Mintable);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ARC200 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC200 token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ARC200 mintable token creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("arc200-preminted/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateARC200Preminted([FromBody] ARC200PremintedTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _arc200TokenService.CreateARC200TokenAsync(request, TokenType.ARC200_Preminted);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ARC200 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC200 token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ARC200 preminted token creation");
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
        [TokenDeploymentSubscription]
        [IdempotencyKey]
        [HttpPost("arc1400-mintable/create")]
        [ProducesResponseType(typeof(ARC200TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ARC1400MintableTokenDeploymentRequest([FromBody] ARC1400MintableTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                var result = await _arc1400TokenService.CreateARC1400TokenAsync(request, TokenType.ARC1400_Mintable);
                
                // Add correlation ID to response
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("ARC1400 token created successfully with asset ID {AssetId} and transaction {TxHash} on {Network}. CorrelationId: {CorrelationId}",
                        result.AssetId, result.TransactionId, request.Network, correlationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("ARC1400 token creation failed: {Error}. CorrelationId: {CorrelationId}", result.ErrorMessage, correlationId);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "ARC1400 mintable token creation");
            }
        }

        /// <summary>
        /// Gets compliance indicators for a token, providing enterprise readiness information
        /// </summary>
        /// <param name="assetId">The asset ID (token ID) to get compliance indicators for</param>
        /// <returns>Compliance indicators including MICA readiness, whitelisting status, and transfer restrictions</returns>
        /// <remarks>
        /// This endpoint exposes compliance-related flags that enable the frontend to display:
        /// - MICA regulatory readiness
        /// - Whitelisting enabled status and entry count
        /// - Transfer restrictions summary
        /// - Enterprise readiness score
        /// - KYC verification status
        /// - Regulatory framework and jurisdiction information
        /// 
        /// Use this endpoint to support subscription value features and enterprise readiness indicators in the UI.
        /// </remarks>
        [HttpGet("{assetId}/compliance-indicators")]
        [ProducesResponseType(typeof(TokenComplianceIndicatorsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComplianceIndicators([FromRoute] ulong assetId)
        {
            try
            {
                var result = await _complianceService.GetComplianceIndicatorsAsync(assetId);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved compliance indicators for asset {AssetId}", assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve compliance indicators for asset {AssetId}: {Error}", 
                        assetId, result.ErrorMessage);
                    
                    // Return the service response with proper error code if not set
                    if (string.IsNullOrEmpty(result.ErrorCode))
                    {
                        result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;
                    }
                    
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                return HandleTokenOperationException(ex, "compliance indicators retrieval");
            }
        }

        /// <summary>
        /// Simulates a token transfer with whitelist enforcement (demonstration endpoint)
        /// </summary>
        /// <param name="request">Transfer simulation request</param>
        /// <returns>Success if both addresses are whitelisted, HTTP 403 if not</returns>
        /// <remarks>
        /// This is a demonstration endpoint that shows how whitelist enforcement works.
        /// It validates that both sender and receiver are whitelisted for the asset,
        /// but does not actually execute a blockchain transaction.
        /// 
        /// Use this endpoint to test whitelist enforcement before implementing actual transfer logic.
        /// The WhitelistEnforcement attribute will automatically:
        /// - Validate both fromAddress and toAddress are whitelisted for the assetId
        /// - Return HTTP 403 Forbidden if any address is not whitelisted
        /// - Log audit entries for blocked operations
        /// - Allow the operation to proceed only if all validations pass
        /// </remarks>
        [HttpPost("transfer/simulate")]
        [WhitelistEnforcement(
            AssetIdParameter = "assetId",
            AddressParameters = new[] { "fromAddress", "toAddress" }
        )]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult SimulateTransfer([FromBody] SimulateTransferRequest request)
        {
            // If we reach here, the WhitelistEnforcement attribute has validated
            // that both addresses are whitelisted. In a real implementation,
            // this is where you would execute the actual blockchain transaction.
            
            _logger.LogInformation(
                "Transfer simulation passed whitelist validation for asset {AssetId} from {From} to {To}",
                request.AssetId, request.FromAddress, request.ToAddress);

            return Ok(new BaseResponse
            {
                Success = true,
                ErrorMessage = null
            });
        }

        /// <summary>
        /// Simulates token minting with whitelist enforcement (demonstration endpoint)
        /// </summary>
        /// <param name="request">Mint simulation request</param>
        /// <returns>Success if recipient address is whitelisted, HTTP 403 if not</returns>
        /// <remarks>
        /// This is a demonstration endpoint that shows how whitelist enforcement works for minting.
        /// It validates that the recipient is whitelisted for the asset,
        /// but does not actually execute a blockchain transaction.
        /// 
        /// The WhitelistEnforcement attribute will automatically:
        /// - Validate the toAddress is whitelisted for the assetId
        /// - Return HTTP 403 Forbidden if the address is not whitelisted
        /// - Log audit entries for blocked operations
        /// </remarks>
        [HttpPost("mint/simulate")]
        [WhitelistEnforcement(
            AssetIdParameter = "assetId",
            AddressParameters = new[] { "toAddress" }
        )]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult SimulateMint([FromBody] SimulateMintRequest request)
        {
            // If we reach here, the WhitelistEnforcement attribute has validated
            // that the recipient address is whitelisted.
            
            _logger.LogInformation(
                "Mint simulation passed whitelist validation for asset {AssetId} to {To}",
                request.AssetId, request.ToAddress);

            return Ok(new BaseResponse
            {
                Success = true,
                ErrorMessage = null
            });
        }

        /// <summary>
        /// Simulates token burning with whitelist enforcement (demonstration endpoint)
        /// </summary>
        /// <param name="request">Burn simulation request</param>
        /// <returns>Success if address is whitelisted, HTTP 403 if not</returns>
        /// <remarks>
        /// This is a demonstration endpoint that shows how whitelist enforcement works for burning.
        /// It validates that the token holder is whitelisted for the asset,
        /// but does not actually execute a blockchain transaction.
        /// 
        /// The WhitelistEnforcement attribute will automatically:
        /// - Validate the fromAddress is whitelisted for the assetId
        /// - Return HTTP 403 Forbidden if the address is not whitelisted
        /// - Log audit entries for blocked operations
        /// </remarks>
        [HttpPost("burn/simulate")]
        [WhitelistEnforcement(
            AssetIdParameter = "assetId",
            AddressParameters = new[] { "fromAddress" }
        )]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult SimulateBurn([FromBody] SimulateBurnRequest request)
        {
            // If we reach here, the WhitelistEnforcement attribute has validated
            // that the address is whitelisted.
            
            _logger.LogInformation(
                "Burn simulation passed whitelist validation for asset {AssetId} from {From}",
                request.AssetId, request.FromAddress);

            return Ok(new BaseResponse
            {
                Success = true,
                ErrorMessage = null
            });
        }

        /// <summary>
        /// Handles exceptions from token operations and returns appropriate error responses
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="operation">The operation that failed</param>
        /// <returns>Appropriate IActionResult based on exception type</returns>
        private IActionResult HandleTokenOperationException(Exception ex, string operation)
        {
            // Get correlation ID from current HTTP context
            var correlationId = HttpContext?.TraceIdentifier;
            
            // Sanitize operation for logging
            var sanitizedOperation = LoggingHelper.SanitizeLogInput(operation);
            
            // Log the exception with full details and correlation ID
            _logger.LogError(ex, "Error during {Operation}. CorrelationId: {CorrelationId}", 
                sanitizedOperation, correlationId);

            // Categorize exception and return appropriate response with correlation ID
            return ex switch
            {
                TimeoutException => AddCorrelationId(ErrorResponseBuilder.TimeoutError(operation), correlationId),
                HttpRequestException httpEx => AddCorrelationId(ErrorResponseBuilder.ExternalServiceError("blockchain network", 
                    _env.IsDevelopment() ? new Dictionary<string, object> { { "details", httpEx.Message }, { "operation", operation } } : null), correlationId),
                ArgumentException or ArgumentNullException => AddCorrelationId(ErrorResponseBuilder.ValidationError(
                    ex.Message,
                    _env.IsDevelopment() ? new Dictionary<string, object> { { "parameterName", (ex as ArgumentException)?.ParamName ?? "unknown" } } : null), correlationId),
                InvalidOperationException => AddCorrelationId(ErrorResponseBuilder.TransactionError(
                    ex.Message,
                    _env.IsDevelopment() ? new Dictionary<string, object> { { "details", ex.Message }, { "operation", operation } } : null), correlationId),
                _ => AddCorrelationId(ErrorResponseBuilder.InternalServerError(
                    $"An unexpected error occurred during {operation}",
                    _env.IsDevelopment(),
                    ex), correlationId)
            };
        }

        /// <summary>
        /// Adds correlation ID to error responses that support it
        /// </summary>
        /// <param name="result">The action result to enhance</param>
        /// <param name="correlationId">The correlation ID to add</param>
        /// <returns>The enhanced action result</returns>
        private IActionResult AddCorrelationId(IActionResult result, string? correlationId)
        {
            if (string.IsNullOrEmpty(correlationId))
                return result;

            // Extract the response object and add correlation ID
            if (result is ObjectResult objectResult && objectResult.Value is ApiErrorResponse errorResponse)
            {
                errorResponse.CorrelationId = correlationId;
            }

            return result;
        }
    }
}
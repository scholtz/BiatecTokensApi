using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.AlgorandApi;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides Algorand-specific endpoints for token deployment and account management.
    /// </summary>
    /// <remarks>
    /// All endpoints require JWT Bearer authentication. The user's Algorand account is derived
    /// deterministically from their email and password using the ARC76 standard — no wallet required.
    /// </remarks>
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ApiController]
    [Route("api/algorand")]
    public class AlgorandController : ControllerBase
    {
        private readonly IASATokenService _asaTokenService;
        private readonly IARC3TokenService _arc3TokenService;
        private readonly IARC200TokenService _arc200TokenService;
        private readonly IAccountService _accountService;
        private readonly IDeploymentStatusService _deploymentStatusService;
        private readonly ILogger<AlgorandController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorandController"/> class.
        /// </summary>
        public AlgorandController(
            IASATokenService asaTokenService,
            IARC3TokenService arc3TokenService,
            IARC200TokenService arc200TokenService,
            IAccountService accountService,
            IDeploymentStatusService deploymentStatusService,
            ILogger<AlgorandController> logger)
        {
            _asaTokenService = asaTokenService;
            _arc3TokenService = arc3TokenService;
            _arc200TokenService = arc200TokenService;
            _accountService = accountService;
            _deploymentStatusService = deploymentStatusService;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Account Info
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the ARC76-derived Algorand address and ALGO balance for the authenticated user.
        /// </summary>
        /// <returns>Account address and ALGO balance.</returns>
        /// <remarks>
        /// Returns the canonical Algorand address derived from the authenticated user's credentials
        /// via the ARC76 standard, together with the current ALGO balance on the specified network.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "derivationStandard": "ARC76",
        ///   "algoBalanceMicroAlgos": 1000000,
        ///   "algoBalance": 1.0,
        ///   "network": "testnet-v1.0"
        /// }
        /// ```
        /// </remarks>
        [HttpGet("account/info")]
        [ProducesResponseType(typeof(AlgorandAccountInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAccountInfo([FromQuery] string network = "testnet-v1.0")
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("AlgorandController.GetAccountInfo called without valid user ID. CorrelationId={CorrelationId}", correlationId);
                return Unauthorized(new AlgorandAccountInfoResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            var addressResponse = await _accountService.GetAddressAsync(userId, correlationId);
            if (!addressResponse.Success)
            {
                return NotFound(new AlgorandAccountInfoResponse
                {
                    Success = false,
                    ErrorMessage = addressResponse.ErrorMessage ?? "User account not found",
                    CorrelationId = correlationId
                });
            }

            // Attempt to retrieve balance; if it fails, return address with zero balance
            long algoBalanceMicroAlgos = 0;
            decimal algoBalance = 0;
            try
            {
                var balanceResponse = await _accountService.GetBalanceAsync(userId, network, correlationId);
                if (balanceResponse.Success)
                {
                    algoBalanceMicroAlgos = balanceResponse.AlgoBalanceMicroAlgos;
                    algoBalance = balanceResponse.AlgoBalance;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Balance retrieval failed for userId={UserId}; returning address with zero balance. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);
            }

            _logger.LogInformation("AlgorandController.GetAccountInfo: address returned for userId={UserId}. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId), correlationId);

            return Ok(new AlgorandAccountInfoResponse
            {
                Success = true,
                AlgorandAddress = addressResponse.AlgorandAddress,
                DerivationStandard = "ARC76",
                AlgoBalanceMicroAlgos = algoBalanceMicroAlgos,
                AlgoBalance = algoBalance,
                Network = network,
                CorrelationId = correlationId
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // ASA Token Creation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an ASA (Algorand Standard Asset) fungible token using the user's ARC76-derived account.
        /// </summary>
        /// <param name="request">Token deployment parameters (name, symbol, total supply, network, etc.)</param>
        /// <returns>Transaction details and the new ASA ID.</returns>
        /// <remarks>
        /// The backend derives the user's Algorand account via ARC76, signs the asset-creation
        /// transaction, submits it to the Algorand node, and returns the confirmed asset ID.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "assetId": 12345678,
        ///   "transactionId": "TX_HASH_HERE",
        ///   "creatorAddress": "ALGORAND_ADDRESS_HERE",
        ///   "confirmedRound": 987654
        /// }
        /// ```
        /// </remarks>
        [HttpPost("asa/create")]
        [ProducesResponseType(typeof(ASATokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateASAToken([FromBody] ASAFungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation("AlgorandController.CreateASAToken: userId={UserId}, network={Network}. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId ?? "anonymous"),
                LoggingHelper.SanitizeLogInput(request.Network),
                correlationId);

            try
            {
                var result = await _asaTokenService.CreateASATokenAsync(request, TokenType.ASA_FT, userId);
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("AlgorandController.CreateASAToken: success. AssetId={AssetId}, TxId={TxId}. CorrelationId={CorrelationId}",
                        result.AssetId, result.TransactionId, correlationId);
                    return Ok(result);
                }

                _logger.LogError("AlgorandController.CreateASAToken: failed. Error={Error}. CorrelationId={CorrelationId}",
                    result.ErrorMessage, correlationId);

                if (string.IsNullOrEmpty(result.ErrorCode))
                    result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;

                return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
            }
            catch (Exception ex)
            {
                return HandleAlgorandException(ex, "ASA token creation", correlationId);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ARC3 Asset Creation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an ARC3-compliant asset with on-chain metadata pinned to IPFS.
        /// </summary>
        /// <param name="request">ARC3 asset deployment parameters including metadata.</param>
        /// <returns>Transaction details and the new asset ID.</returns>
        /// <remarks>
        /// The backend pins ARC3 metadata to IPFS, then creates the ARC3 asset on Algorand using
        /// the user's ARC76-derived account. Returns the confirmed asset ID and IPFS metadata URL.
        /// </remarks>
        [HttpPost("arc3/create")]
        [ProducesResponseType(typeof(ARC3TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateARC3Token([FromBody] ARC3FungibleTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation("AlgorandController.CreateARC3Token: userId={UserId}, network={Network}. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId ?? "anonymous"),
                LoggingHelper.SanitizeLogInput(request.Network),
                correlationId);

            try
            {
                var result = await _arc3TokenService.CreateARC3TokenAsync(request, TokenType.ARC3_FT);
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("AlgorandController.CreateARC3Token: success. AssetId={AssetId}, TxId={TxId}. CorrelationId={CorrelationId}",
                        result.AssetId, result.TransactionId, correlationId);
                    return Ok(result);
                }

                _logger.LogError("AlgorandController.CreateARC3Token: failed. Error={Error}. CorrelationId={CorrelationId}",
                    result.ErrorMessage, correlationId);

                if (string.IsNullOrEmpty(result.ErrorCode))
                    result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;

                return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
            }
            catch (Exception ex)
            {
                return HandleAlgorandException(ex, "ARC3 token creation", correlationId);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ARC200 Smart Contract Deployment
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deploys an ARC200 smart contract token on Algorand.
        /// </summary>
        /// <param name="request">ARC200 token deployment parameters.</param>
        /// <returns>Transaction details and the new application ID.</returns>
        /// <remarks>
        /// The backend derives the user's Algorand account via ARC76, compiles the ARC200 contract,
        /// funds the contract with the minimum balance, and returns the confirmed application ID.
        /// </remarks>
        [HttpPost("arc200/deploy")]
        [ProducesResponseType(typeof(Models.ARC200.Response.ARC200TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeployARC200Token([FromBody] ARC200MintableTokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation("AlgorandController.DeployARC200Token: userId={UserId}, network={Network}. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId ?? "anonymous"),
                LoggingHelper.SanitizeLogInput(request.Network),
                correlationId);

            try
            {
                var result = await _arc200TokenService.CreateARC200TokenAsync(request, TokenType.ARC200_Mintable);
                result.CorrelationId = correlationId;

                if (result.Success)
                {
                    _logger.LogInformation("AlgorandController.DeployARC200Token: success. AppId={AppId}, TxId={TxId}. CorrelationId={CorrelationId}",
                        result.AssetId, result.TransactionId, correlationId);
                    return Ok(result);
                }

                _logger.LogError("AlgorandController.DeployARC200Token: failed. Error={Error}. CorrelationId={CorrelationId}",
                    result.ErrorMessage, correlationId);

                if (string.IsNullOrEmpty(result.ErrorCode))
                    result.ErrorCode = ErrorCodes.TRANSACTION_FAILED;

                return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
            }
            catch (Exception ex)
            {
                return HandleAlgorandException(ex, "ARC200 token deployment", correlationId);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Transaction Status
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the confirmed/pending/failed status for a given Algorand transaction ID.
        /// </summary>
        /// <param name="txId">The Algorand transaction ID (hash) to look up.</param>
        /// <returns>Transaction status with block confirmation details.</returns>
        /// <remarks>
        /// Looks up the transaction in the deployment tracking system by transaction hash or
        /// deployment ID. Returns Confirmed, Pending, Failed, or Cancelled based on the tracked
        /// deployment state. Returns 404 if the transaction is not tracked in this system.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "transactionId": "TX_HASH_HERE",
        ///   "status": "Confirmed",
        ///   "assetIdentifier": "12345678",
        ///   "network": "testnet-v1.0",
        ///   "tokenType": "ASA_FT"
        /// }
        /// ```
        /// </remarks>
        [HttpGet("transaction/{txId}/status")]
        [ProducesResponseType(typeof(AlgorandTransactionStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransactionStatus([FromRoute] string txId)
        {
            var correlationId = HttpContext.TraceIdentifier;

            if (string.IsNullOrWhiteSpace(txId))
            {
                return BadRequest(new AlgorandTransactionStatusResponse
                {
                    Success = false,
                    ErrorMessage = "Transaction ID is required",
                    CorrelationId = correlationId
                });
            }

            _logger.LogInformation("AlgorandController.GetTransactionStatus: txId={TxId}. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(txId), correlationId);

            try
            {
                // First try as deployment ID, then fall back to transaction hash lookup
                var deployment = await _deploymentStatusService.GetDeploymentAsync(txId)
                    ?? await _deploymentStatusService.GetDeploymentByTransactionHashAsync(txId);

                if (deployment == null)
                {
                    return NotFound(new AlgorandTransactionStatusResponse
                    {
                        Success = false,
                        TransactionId = txId,
                        Status = "Unknown",
                        ErrorMessage = "Transaction not found in deployment tracking system",
                        CorrelationId = correlationId
                    });
                }

                var status = deployment.CurrentStatus switch
                {
                    DeploymentStatus.Completed => "Confirmed",
                    DeploymentStatus.Failed => "Failed",
                    DeploymentStatus.Cancelled => "Cancelled",
                    _ => "Pending"
                };

                return Ok(new AlgorandTransactionStatusResponse
                {
                    Success = true,
                    TransactionId = deployment.TransactionHash ?? txId,
                    Status = status,
                    AssetIdentifier = deployment.AssetIdentifier,
                    Network = deployment.Network,
                    TokenType = deployment.TokenType,
                    CorrelationId = correlationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AlgorandController.GetTransactionStatus error: txId={TxId}. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(txId), correlationId);

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new AlgorandTransactionStatusResponse
                {
                    Success = false,
                    TransactionId = txId,
                    Status = "Unknown",
                    ErrorMessage = "Service temporarily unavailable. Please retry.",
                    CorrelationId = correlationId
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private IActionResult HandleAlgorandException(Exception ex, string operation, string correlationId)
        {
            _logger.LogError(ex, "AlgorandController {Operation} error. CorrelationId={CorrelationId}", operation, correlationId);

            // Network / node unreachable → 503 with retry guidance
            if (ex is HttpRequestException || ex is TaskCanceledException || ex is TimeoutException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Algorand network temporarily unavailable. Please retry in a few seconds. Operation: {operation}",
                    ErrorCode = ErrorCodes.NETWORK_ERROR,
                    CorrelationId = correlationId
                });
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new BaseResponse
            {
                Success = false,
                ErrorMessage = $"An unexpected error occurred during {operation}. Please retry.",
                ErrorCode = ErrorCodes.TRANSACTION_FAILED,
                CorrelationId = correlationId
            });
        }
    }
}

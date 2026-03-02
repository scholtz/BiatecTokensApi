using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for wallet connection state management, network mismatch detection, and reconnect guidance.
    /// Exposes the WalletConnectionService via a REST API for frontend wallet integration.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/wallet")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletConnectionService _walletConnectionService;
        private readonly IWalletRoutingService _walletRoutingService;
        private readonly ILogger<WalletController> _logger;

        /// <summary>
        /// Initializes a new instance of the WalletController
        /// </summary>
        /// <param name="walletConnectionService">Wallet connection service</param>
        /// <param name="walletRoutingService">Wallet routing service</param>
        /// <param name="logger">Logger instance</param>
        public WalletController(
            IWalletConnectionService walletConnectionService,
            IWalletRoutingService walletRoutingService,
            ILogger<WalletController> logger)
        {
            _walletConnectionService = walletConnectionService;
            _walletRoutingService = walletRoutingService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current connection state for a wallet address
        /// </summary>
        /// <param name="address">Wallet address to query</param>
        /// <param name="network">Expected network identifier (e.g., "algorand-mainnet", "base-mainnet")</param>
        /// <returns>Current wallet connection state</returns>
        [HttpGet("connection")]
        [ProducesResponseType(typeof(WalletConnectionState), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetConnectionState(
            [FromQuery] string address,
            [FromQuery] string network = "algorand-mainnet")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Wallet address is required"
                    });
                }

                _logger.LogInformation(
                    "Connection state query for wallet {Address} on {Network}",
                    LoggingHelper.SanitizeLogInput(address),
                    LoggingHelper.SanitizeLogInput(network));

                var state = _walletConnectionService.GetConnectionState(address, network);
                return Ok(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving wallet connection state");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving wallet connection state"
                });
            }
        }

        /// <summary>
        /// Records a wallet connection and returns updated state
        /// </summary>
        /// <param name="request">Connection request with wallet address and network</param>
        /// <returns>Updated wallet connection state</returns>
        [HttpPost("connect")]
        [ProducesResponseType(typeof(WalletConnectionState), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Connect([FromBody] WalletConnectRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Wallet address is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.ActualNetwork))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Actual network is required"
                    });
                }

                var expectedNetwork = string.IsNullOrWhiteSpace(request.ExpectedNetwork)
                    ? request.ActualNetwork
                    : request.ExpectedNetwork;

                _logger.LogInformation(
                    "Wallet connect: address={Address}, actualNetwork={ActualNetwork}, expectedNetwork={ExpectedNetwork}",
                    LoggingHelper.SanitizeLogInput(request.Address),
                    LoggingHelper.SanitizeLogInput(request.ActualNetwork),
                    LoggingHelper.SanitizeLogInput(expectedNetwork));

                var state = _walletConnectionService.Connect(request.Address, request.ActualNetwork, expectedNetwork);
                return Ok(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting wallet");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while connecting wallet"
                });
            }
        }

        /// <summary>
        /// Records a wallet disconnect event and returns the disconnected state
        /// </summary>
        /// <param name="request">Disconnect request with wallet address</param>
        /// <returns>Disconnected wallet state</returns>
        [HttpPost("disconnect")]
        [ProducesResponseType(typeof(WalletConnectionState), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Disconnect([FromBody] WalletDisconnectRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Wallet address is required"
                    });
                }

                _logger.LogInformation(
                    "Wallet disconnect: address={Address}",
                    LoggingHelper.SanitizeLogInput(request.Address));

                var state = _walletConnectionService.Disconnect(request.Address);
                return Ok(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting wallet");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while disconnecting wallet"
                });
            }
        }

        /// <summary>
        /// Attempts to reconnect a previously connected wallet and returns updated state
        /// </summary>
        /// <param name="request">Reconnect request with wallet address and network</param>
        /// <returns>Wallet state after reconnection attempt</returns>
        [HttpPost("reconnect")]
        [ProducesResponseType(typeof(WalletConnectionState), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Reconnect([FromBody] WalletReconnectRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Wallet address is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.ActualNetwork))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Actual network is required"
                    });
                }

                var expectedNetwork = string.IsNullOrWhiteSpace(request.ExpectedNetwork)
                    ? request.ActualNetwork
                    : request.ExpectedNetwork;

                _logger.LogInformation(
                    "Wallet reconnect: address={Address}, actualNetwork={ActualNetwork}, expectedNetwork={ExpectedNetwork}",
                    LoggingHelper.SanitizeLogInput(request.Address),
                    LoggingHelper.SanitizeLogInput(request.ActualNetwork),
                    LoggingHelper.SanitizeLogInput(expectedNetwork));

                var state = _walletConnectionService.Reconnect(request.Address, request.ActualNetwork, expectedNetwork);
                return Ok(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconnecting wallet");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while reconnecting wallet"
                });
            }
        }

        /// <summary>
        /// Gets reconnect guidance for a given failure reason
        /// </summary>
        /// <param name="reason">Reason the wallet needs to reconnect</param>
        /// <param name="actualNetwork">Network the wallet is currently on (optional)</param>
        /// <param name="expectedNetwork">Network required by the application (optional)</param>
        /// <returns>Step-by-step reconnect guidance</returns>
        [HttpGet("reconnect-guidance")]
        [ProducesResponseType(typeof(WalletReconnectGuidance), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetReconnectGuidance(
            [FromQuery] WalletReconnectReason reason = WalletReconnectReason.Unknown,
            [FromQuery] string? actualNetwork = null,
            [FromQuery] string? expectedNetwork = null)
        {
            try
            {
                _logger.LogInformation(
                    "Reconnect guidance requested for reason={Reason}",
                    LoggingHelper.SanitizeLogInput(reason.ToString()));

                var guidance = _walletConnectionService.GetReconnectGuidance(reason, actualNetwork, expectedNetwork);
                return Ok(guidance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reconnect guidance for reason={Reason}",
                    LoggingHelper.SanitizeLogInput(reason.ToString()));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving reconnect guidance"
                });
            }
        }

        /// <summary>
        /// Returns the list of supported blockchain networks
        /// </summary>
        /// <returns>List of supported network identifiers</returns>
        [HttpGet("networks")]
        [ProducesResponseType(typeof(SupportedNetworksResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetSupportedNetworks()
        {
            try
            {
                var networks = _walletConnectionService.GetSupportedNetworks();
                return Ok(new SupportedNetworksResponse { Networks = networks.ToList() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supported networks");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving supported networks"
                });
            }
        }

        /// <summary>
        /// Validates a wallet address format for the specified network
        /// </summary>
        /// <param name="request">Address validation request</param>
        /// <returns>Validation result indicating whether the address is structurally valid</returns>
        [HttpPost("validate-address")]
        [ProducesResponseType(typeof(WalletAddressValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ValidateAddress([FromBody] WalletAddressValidationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Wallet address is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Network is required"
                    });
                }

                _logger.LogInformation(
                    "Wallet address validation: network={Network}",
                    LoggingHelper.SanitizeLogInput(request.Network));

                var isValid = _walletConnectionService.ValidateWalletAddress(request.Address, request.Network);
                return Ok(new WalletAddressValidationResponse
                {
                    IsValid = isValid,
                    Address = request.Address,
                    Network = request.Network,
                    Message = isValid
                        ? "Address format is valid for the specified network"
                        : "Address format is invalid for the specified network"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating wallet address");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while validating wallet address"
                });
            }
        }

        /// <summary>
        /// Returns optimized routing options for a cross-network wallet operation
        /// </summary>
        /// <param name="request">Routing request specifying source/target network and operation type</param>
        /// <returns>Routing options with cost and time estimates ordered by recommendation</returns>
        /// <remarks>
        /// Helps users optimize wallet connections for cross-network token operations.
        /// Returns ordered routing options with step-by-step guidance, fee estimates,
        /// and time estimates to reduce friction for cross-chain scenarios.
        /// </remarks>
        [HttpPost("routing-options")]
        [ProducesResponseType(typeof(WalletRoutingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRoutingOptions([FromBody] WalletRoutingRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.SourceNetwork))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "SourceNetwork is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.TargetNetwork))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "TargetNetwork is required"
                    });
                }

                _logger.LogInformation(
                    "Wallet routing request: {Source} -> {Target}",
                    LoggingHelper.SanitizeLogInput(request.SourceNetwork),
                    LoggingHelper.SanitizeLogInput(request.TargetNetwork));

                var response = await _walletRoutingService.GetRoutingOptionsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing wallet routing options");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while computing routing options"
                });
            }
        }
    }

    /// <summary>
    /// Request to connect a wallet
    /// </summary>
    public class WalletConnectRequest
    {
        /// <summary>
        /// Wallet address to connect
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Network the wallet is currently connected to
        /// </summary>
        public string ActualNetwork { get; set; } = string.Empty;

        /// <summary>
        /// Network required by the application (defaults to ActualNetwork if omitted)
        /// </summary>
        public string? ExpectedNetwork { get; set; }
    }

    /// <summary>
    /// Request to disconnect a wallet
    /// </summary>
    public class WalletDisconnectRequest
    {
        /// <summary>
        /// Wallet address to disconnect
        /// </summary>
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to reconnect a wallet
    /// </summary>
    public class WalletReconnectRequest
    {
        /// <summary>
        /// Wallet address to reconnect
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Network the wallet is currently connected to
        /// </summary>
        public string ActualNetwork { get; set; } = string.Empty;

        /// <summary>
        /// Network required by the application (defaults to ActualNetwork if omitted)
        /// </summary>
        public string? ExpectedNetwork { get; set; }
    }

    /// <summary>
    /// Response containing list of supported blockchain networks
    /// </summary>
    public class SupportedNetworksResponse
    {
        /// <summary>
        /// List of supported network identifiers
        /// </summary>
        public List<string> Networks { get; set; } = new();

        /// <summary>
        /// Total number of supported networks
        /// </summary>
        public int TotalCount => Networks.Count;
    }

    /// <summary>
    /// Request to validate a wallet address format
    /// </summary>
    public class WalletAddressValidationRequest
    {
        /// <summary>
        /// Wallet address to validate
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Network to validate the address format against
        /// </summary>
        public string Network { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from wallet address validation
    /// </summary>
    public class WalletAddressValidationResponse
    {
        /// <summary>
        /// Whether the address format is valid for the specified network
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// The address that was validated
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// The network used for validation
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable validation result message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}

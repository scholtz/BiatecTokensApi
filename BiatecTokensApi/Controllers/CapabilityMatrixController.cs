using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for querying compliance capability matrix
    /// </summary>
    /// <remarks>
    /// This controller provides read-only access to the compliance capability matrix,
    /// which defines allowed token standards, compliance checks, and transaction types
    /// per jurisdiction, wallet type, and KYC tier.
    /// </remarks>
    [ApiController]
    [Route("api/v1/compliance/capabilities")]
    public class CapabilityMatrixController : ControllerBase
    {
        private readonly ICapabilityMatrixService _capabilityMatrixService;
        private readonly ILogger<CapabilityMatrixController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilityMatrixController"/> class.
        /// </summary>
        /// <param name="capabilityMatrixService">The capability matrix service</param>
        /// <param name="logger">The logger instance</param>
        public CapabilityMatrixController(
            ICapabilityMatrixService capabilityMatrixService,
            ILogger<CapabilityMatrixController> logger)
        {
            _capabilityMatrixService = capabilityMatrixService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the compliance capability matrix with optional filtering
        /// </summary>
        /// <param name="jurisdiction">Optional filter by jurisdiction code (e.g., "US", "CH", "EU")</param>
        /// <param name="walletType">Optional filter by wallet type (e.g., "custodial", "non-custodial")</param>
        /// <param name="tokenStandard">Optional filter by token standard (e.g., "ARC-3", "ARC-19", "ARC-200", "ERC-20")</param>
        /// <param name="kycTier">Optional filter by KYC tier (e.g., "1", "2", "3")</param>
        /// <returns>The capability matrix with allowed actions and compliance requirements</returns>
        /// <response code="200">Returns the capability matrix data</response>
        /// <response code="400">If the filter combination is invalid</response>
        /// <response code="404">If no matching capabilities are found</response>
        /// <response code="500">If an internal error occurs</response>
        [HttpGet]
        [ProducesResponseType(typeof(CapabilityMatrixResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CapabilityMatrixResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(CapabilityMatrixResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(CapabilityMatrixResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCapabilityMatrix(
            [FromQuery] string? jurisdiction = null,
            [FromQuery] string? walletType = null,
            [FromQuery] string? tokenStandard = null,
            [FromQuery] string? kycTier = null)
        {
            try
            {
                var request = new GetCapabilityMatrixRequest
                {
                    Jurisdiction = jurisdiction,
                    WalletType = walletType,
                    TokenStandard = tokenStandard,
                    KycTier = kycTier
                };

                var result = await _capabilityMatrixService.GetCapabilityMatrixAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Capability matrix retrieved successfully: Jurisdiction={Jurisdiction}, WalletType={WalletType}, TokenStandard={TokenStandard}, KycTier={KycTier}",
                        LoggingHelper.SanitizeLogInput(jurisdiction),
                        LoggingHelper.SanitizeLogInput(walletType),
                        LoggingHelper.SanitizeLogInput(tokenStandard),
                        LoggingHelper.SanitizeLogInput(kycTier));
                    return Ok(result);
                }
                else
                {
                    if (result.ErrorDetails != null && result.ErrorDetails.Error == "no_matching_capabilities")
                    {
                        _logger.LogWarning("No matching capabilities found: Jurisdiction={Jurisdiction}, WalletType={WalletType}, TokenStandard={TokenStandard}, KycTier={KycTier}",
                            LoggingHelper.SanitizeLogInput(jurisdiction),
                            LoggingHelper.SanitizeLogInput(walletType),
                            LoggingHelper.SanitizeLogInput(tokenStandard),
                            LoggingHelper.SanitizeLogInput(kycTier));
                        return NotFound(result);
                    }
                    else
                    {
                        _logger.LogError("Error retrieving capability matrix: {ErrorMessage}",
                            LoggingHelper.SanitizeLogInput(result.ErrorMessage));
                        return StatusCode(StatusCodes.Status500InternalServerError, result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in GetCapabilityMatrix endpoint");
                return StatusCode(StatusCodes.Status500InternalServerError, new CapabilityMatrixResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Checks if a specific action is allowed based on capability rules
        /// </summary>
        /// <param name="request">The capability check request containing jurisdiction, wallet type, token standard, KYC tier, and action</param>
        /// <returns>Response indicating if the action is allowed and required compliance checks</returns>
        /// <response code="200">Returns the capability check result</response>
        /// <response code="400">If the request is invalid</response>
        /// <response code="403">If the action is not allowed based on capability rules</response>
        /// <response code="500">If an internal error occurs</response>
        [HttpPost("check")]
        [ProducesResponseType(typeof(CapabilityCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CapabilityCheckResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(CapabilityCheckResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(CapabilityCheckResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckCapability([FromBody] CapabilityCheckRequest request)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Jurisdiction) ||
                    string.IsNullOrWhiteSpace(request.WalletType) ||
                    string.IsNullOrWhiteSpace(request.TokenStandard) ||
                    string.IsNullOrWhiteSpace(request.KycTier) ||
                    string.IsNullOrWhiteSpace(request.Action))
                {
                    _logger.LogWarning("Capability check request validation failed: missing required fields");
                    return BadRequest(new CapabilityCheckResponse
                    {
                        Allowed = false,
                        Reason = "All fields (Jurisdiction, WalletType, TokenStandard, KycTier, Action) are required"
                    });
                }

                var result = await _capabilityMatrixService.CheckCapabilityAsync(request);

                if (result.Allowed)
                {
                    _logger.LogInformation("Capability check passed: Jurisdiction={Jurisdiction}, WalletType={WalletType}, TokenStandard={TokenStandard}, KycTier={KycTier}, Action={Action}",
                        LoggingHelper.SanitizeLogInput(request.Jurisdiction),
                        LoggingHelper.SanitizeLogInput(request.WalletType),
                        LoggingHelper.SanitizeLogInput(request.TokenStandard),
                        LoggingHelper.SanitizeLogInput(request.KycTier),
                        LoggingHelper.SanitizeLogInput(request.Action));
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Capability check denied: Jurisdiction={Jurisdiction}, WalletType={WalletType}, TokenStandard={TokenStandard}, KycTier={KycTier}, Action={Action}, Reason={Reason}",
                        LoggingHelper.SanitizeLogInput(request.Jurisdiction),
                        LoggingHelper.SanitizeLogInput(request.WalletType),
                        LoggingHelper.SanitizeLogInput(request.TokenStandard),
                        LoggingHelper.SanitizeLogInput(request.KycTier),
                        LoggingHelper.SanitizeLogInput(request.Action),
                        LoggingHelper.SanitizeLogInput(result.Reason));
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CheckCapability endpoint");
                return StatusCode(StatusCodes.Status500InternalServerError, new CapabilityCheckResponse
                {
                    Allowed = false,
                    Reason = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the current capability matrix version
        /// </summary>
        /// <returns>The version string</returns>
        /// <response code="200">Returns the version information</response>
        [HttpGet("version")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetVersion()
        {
            try
            {
                var version = _capabilityMatrixService.GetVersion();
                _logger.LogInformation("Capability matrix version retrieved: {Version}", LoggingHelper.SanitizeLogInput(version));
                return Ok(new { version });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in GetVersion endpoint");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Internal error: {ex.Message}" });
            }
        }
    }
}

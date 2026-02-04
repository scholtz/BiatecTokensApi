using BiatecTokensApi.Filters;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenStandards;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for token standard compliance and validation endpoints
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/standards")]
    public class TokenStandardsController : ControllerBase
    {
        private readonly ITokenStandardRegistry _registry;
        private readonly ITokenStandardValidator _validator;
        private readonly ILogger<TokenStandardsController> _logger;

        /// <summary>
        /// Initializes a new instance of the TokenStandardsController
        /// </summary>
        /// <param name="registry">Token standard registry service</param>
        /// <param name="validator">Token standard validator service</param>
        /// <param name="logger">Logger instance</param>
        public TokenStandardsController(
            ITokenStandardRegistry registry,
            ITokenStandardValidator validator,
            ILogger<TokenStandardsController> logger)
        {
            _registry = registry;
            _validator = validator;
            _logger = logger;
        }

        /// <summary>
        /// Gets all supported token standards and their requirements
        /// </summary>
        /// <param name="request">Optional filters for standard retrieval</param>
        /// <returns>List of supported token standard profiles</returns>
        /// <remarks>
        /// This endpoint returns a comprehensive list of all supported token standards,
        /// including their version, required fields, optional fields, and validation rules.
        /// Use this endpoint to discover what standards are available and what fields are
        /// required for each standard when creating tokens.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(GetTokenStandardsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStandards([FromQuery] GetTokenStandardsRequest? request)
        {
            try
            {
                var correlationId = Guid.NewGuid().ToString();
                _logger.LogInformation(
                    "Standards discovery request received. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(correlationId));

                var activeOnly = request?.ActiveOnly ?? true;
                var standards = await _registry.GetAllStandardsAsync(activeOnly);

                if (request?.Standard.HasValue == true)
                {
                    standards = standards.Where(s => s.Standard == request.Standard.Value).ToList();
                }

                var response = new GetTokenStandardsResponse
                {
                    Standards = standards,
                    TotalCount = standards.Count
                };

                _logger.LogInformation(
                    "Standards discovery completed successfully. Count={Count}, CorrelationId={CorrelationId}",
                    standards.Count,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving token standards");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving token standards"
                });
            }
        }

        /// <summary>
        /// Gets details for a specific token standard
        /// </summary>
        /// <param name="standard">The token standard to retrieve</param>
        /// <returns>Token standard profile details</returns>
        /// <remarks>
        /// Returns detailed information about a specific token standard, including all
        /// required and optional fields, validation rules, and example metadata.
        /// </remarks>
        [HttpGet("{standard}")]
        [ProducesResponseType(typeof(TokenStandardProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStandard([FromRoute] TokenStandard standard)
        {
            try
            {
                var correlationId = Guid.NewGuid().ToString();
                _logger.LogInformation(
                    "Standard profile request for {Standard}. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(standard.ToString()),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var profile = await _registry.GetStandardProfileAsync(standard);
                if (profile == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.NOT_FOUND,
                        ErrorMessage = $"Token standard '{standard}' not found or not supported"
                    });
                }

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving token standard profile for {Standard}",
                    LoggingHelper.SanitizeLogInput(standard.ToString()));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving token standard profile"
                });
            }
        }

        /// <summary>
        /// Validates token metadata against a specified standard (preflight validation)
        /// </summary>
        /// <param name="request">Validation request with metadata and standard</param>
        /// <returns>Validation result with any errors or warnings</returns>
        /// <remarks>
        /// This endpoint performs preflight validation of token metadata against a specified
        /// standard without creating a token. Use this to validate metadata before submitting
        /// a full token creation request. The response includes detailed error messages and
        /// field-specific validation feedback.
        /// 
        /// **Performance:** Target p95 latency is under 200ms for typical payloads.
        /// </remarks>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ValidateTokenMetadataResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateMetadata([FromBody] ValidateTokenMetadataRequest request)
        {
            var correlationId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation(
                    "Metadata validation request for standard {Standard}. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.Standard.ToString()),
                    LoggingHelper.SanitizeLogInput(correlationId));

                // Check if standard is supported
                var isSupported = await _registry.IsStandardSupportedAsync(request.Standard);
                if (!isSupported)
                {
                    return BadRequest(new ValidateTokenMetadataResponse
                    {
                        IsValid = false,
                        ErrorCode = ErrorCodes.INVALID_TOKEN_STANDARD,
                        Message = $"Token standard '{request.Standard}' is not supported",
                        CorrelationId = correlationId
                    });
                }

                // Perform validation
                var validationResult = await _validator.ValidateAsync(
                    request.Standard,
                    request.Metadata,
                    request.Name,
                    request.Symbol,
                    request.Decimals);

                var response = new ValidateTokenMetadataResponse
                {
                    IsValid = validationResult.IsValid,
                    ValidationResult = validationResult,
                    Message = validationResult.Message,
                    CorrelationId = correlationId
                };

                if (!validationResult.IsValid && validationResult.Errors.Count > 0)
                {
                    response.ErrorCode = ErrorCodes.METADATA_VALIDATION_FAILED;
                }

                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation(
                    "Metadata validation completed. Standard={Standard}, IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}, Duration={Duration}ms, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.Standard.ToString()),
                    validationResult.IsValid,
                    validationResult.Errors.Count,
                    validationResult.Warnings.Count,
                    duration,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return Ok(response);
            }
            catch (Exception ex)
            {
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, 
                    "Error validating token metadata. Standard={Standard}, Duration={Duration}ms, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.Standard.ToString()),
                    duration,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return StatusCode(StatusCodes.Status500InternalServerError, new ValidateTokenMetadataResponse
                {
                    IsValid = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    Message = "An error occurred during metadata validation",
                    CorrelationId = correlationId
                });
            }
        }
    }
}

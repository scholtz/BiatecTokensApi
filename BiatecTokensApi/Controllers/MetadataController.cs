using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for token metadata operations
    /// </summary>
    /// <remarks>
    /// Provides endpoints for retrieving, updating, and validating token metadata.
    /// Token metadata includes essential information for wallet displays and user experience
    /// such as name, symbol, decimals, description, images, and links.
    /// </remarks>
    [ApiController]
    [Route("api/v1/metadata")]
    [Authorize]
    public class MetadataController : ControllerBase
    {
        private readonly ITokenMetadataService _metadataService;
        private readonly ILogger<MetadataController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataController"/> class
        /// </summary>
        public MetadataController(
            ITokenMetadataService metadataService,
            ILogger<MetadataController> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves comprehensive metadata for a specific token
        /// </summary>
        /// <param name="tokenIdentifier">Token identifier (asset ID for Algorand, contract address for EVM)</param>
        /// <param name="chain">Blockchain network identifier (e.g., "algorand-mainnet", "base-mainnet")</param>
        /// <param name="includeValidation">Whether to include validation details in the response</param>
        /// <returns>Token metadata with validation status and completeness score</returns>
        /// <remarks>
        /// Example request: GET /api/v1/metadata/token?tokenIdentifier=123456&amp;chain=algorand-mainnet&amp;includeValidation=true
        /// 
        /// The response includes:
        /// - Core token information (name, symbol, decimals)
        /// - Rich metadata (description, images, links)
        /// - Validation status and completeness score (0-100)
        /// - List of any validation issues with remediation guidance
        /// - Timestamps for when metadata was created and last updated
        /// 
        /// Completeness Score Calculation:
        /// - 100: All recommended fields present
        /// - 70-99: Most fields present, minor optional fields missing
        /// - 40-69: Core fields present, many optional fields missing
        /// - 0-39: Only required fields present
        /// 
        /// Validation Issues:
        /// - Error: Critical issues that should be fixed for proper functionality
        /// - Warning: Recommended fields missing or incorrectly formatted
        /// - Info: Informational messages about auto-generated or fallback values
        /// 
        /// This endpoint is publicly accessible to support wallet integrations without requiring authentication.
        /// </remarks>
        [AllowAnonymous]
        [HttpGet("token")]
        [SwaggerOperation(
            Summary = "Get token metadata",
            Description = "Retrieves comprehensive metadata for a token including validation status and completeness score"
        )]
        [SwaggerResponse(200, "Successfully retrieved token metadata", typeof(GetTokenMetadataResponse))]
        [SwaggerResponse(400, "Invalid request parameters", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(404, "Token not found", typeof(GetTokenMetadataResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> GetTokenMetadata(
            [FromQuery] string tokenIdentifier,
            [FromQuery] string chain,
            [FromQuery] bool includeValidation = true)
        {
            try
            {
                _logger.LogInformation(
                    "Get token metadata request from {User} - Token: {TokenId}, Chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(tokenIdentifier),
                    LoggingHelper.SanitizeLogInput(chain)
                );

                // Validate input
                if (string.IsNullOrWhiteSpace(tokenIdentifier))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.InvalidRequest,
                        ErrorMessage = "Token identifier is required",
                        Details = new Dictionary<string, object>
                        {
                            { "field", "tokenIdentifier" },
                            { "remediation", "Provide a valid token identifier (asset ID or contract address)" }
                        }
                    });
                }

                if (string.IsNullOrWhiteSpace(chain))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.InvalidRequest,
                        ErrorMessage = "Chain is required",
                        Details = new Dictionary<string, object>
                        {
                            { "field", "chain" },
                            { "remediation", "Provide a valid blockchain network identifier (e.g., 'algorand-mainnet', 'base-mainnet')" }
                        }
                    });
                }

                var metadata = await _metadataService.GetMetadataAsync(tokenIdentifier, chain, includeValidation);

                if (metadata == null)
                {
                    _logger.LogWarning(
                        "Token metadata not found for {TokenId} on {Chain}",
                        LoggingHelper.SanitizeLogInput(tokenIdentifier),
                        LoggingHelper.SanitizeLogInput(chain)
                    );

                    return NotFound(new GetTokenMetadataResponse
                    {
                        Success = false,
                        Found = false,
                        ErrorMessage = $"Token {tokenIdentifier} not found on {chain}. Ensure the token has been deployed and registered."
                    });
                }

                return Ok(new GetTokenMetadataResponse
                {
                    Success = true,
                    Found = true,
                    Metadata = metadata
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving token metadata for {TokenId} on {Chain}",
                    LoggingHelper.SanitizeLogInput(tokenIdentifier),
                    LoggingHelper.SanitizeLogInput(chain)
                );

                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while retrieving token metadata",
                    Details = new Dictionary<string, object>
                    {
                        { "tokenIdentifier", tokenIdentifier },
                        { "chain", chain }
                    }
                });
            }
        }

        /// <summary>
        /// Updates token metadata (description, images, links, tags)
        /// </summary>
        /// <param name="request">Metadata update request with new values</param>
        /// <returns>Updated metadata with validation results</returns>
        /// <remarks>
        /// Example request body:
        /// {
        ///   "tokenIdentifier": "123456",
        ///   "chain": "algorand-mainnet",
        ///   "description": "A comprehensive description of the token",
        ///   "imageUrl": "https://example.com/logo.png",
        ///   "websiteUrl": "https://example.com",
        ///   "documentationUrl": "https://docs.example.com",
        ///   "tags": ["DeFi", "RWA"],
        ///   "additionalLinks": {
        ///     "twitter": "https://twitter.com/example",
        ///     "github": "https://github.com/example",
        ///     "discord": "https://discord.gg/example"
        ///   }
        /// }
        /// 
        /// Core fields (name, symbol, decimals) cannot be updated through this endpoint
        /// as they are set during token deployment. This endpoint only updates
        /// supplementary metadata fields.
        /// 
        /// All URLs are validated for proper format (must be absolute URLs starting with http:// or https://).
        /// The response includes validation results and the updated completeness score.
        /// </remarks>
        [HttpPost("token/update")]
        [SwaggerOperation(
            Summary = "Update token metadata",
            Description = "Updates supplementary metadata fields for a token (description, images, links, tags)"
        )]
        [SwaggerResponse(200, "Successfully updated token metadata", typeof(UpdateTokenMetadataResponse))]
        [SwaggerResponse(400, "Invalid request parameters", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(404, "Token not found", typeof(UpdateTokenMetadataResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> UpdateTokenMetadata([FromBody] UpdateTokenMetadataRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Update token metadata request from {User} - Token: {TokenId}, Chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(request.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(request.Chain)
                );

                // Get existing metadata
                var existingMetadata = await _metadataService.GetMetadataAsync(
                    request.TokenIdentifier,
                    request.Chain,
                    includeValidation: false
                );

                if (existingMetadata == null)
                {
                    return NotFound(new UpdateTokenMetadataResponse
                    {
                        Success = false,
                        Updated = false,
                        ErrorMessage = $"Token {request.TokenIdentifier} not found on {request.Chain}"
                    });
                }

                // Update only the provided fields
                if (!string.IsNullOrWhiteSpace(request.Description))
                {
                    existingMetadata.Description = request.Description;
                }

                if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                {
                    existingMetadata.ImageUrl = request.ImageUrl;
                }

                if (!string.IsNullOrWhiteSpace(request.WebsiteUrl))
                {
                    existingMetadata.WebsiteUrl = request.WebsiteUrl;
                }

                if (!string.IsNullOrWhiteSpace(request.DocumentationUrl))
                {
                    existingMetadata.DocumentationUrl = request.DocumentationUrl;
                }

                if (request.Tags != null && request.Tags.Any())
                {
                    existingMetadata.Tags = request.Tags;
                }

                if (request.AdditionalLinks != null && request.AdditionalLinks.Any())
                {
                    existingMetadata.AdditionalLinks = request.AdditionalLinks;
                }

                // Upsert and validate
                var updatedMetadata = await _metadataService.UpsertMetadataAsync(existingMetadata);

                _logger.LogInformation(
                    "Token metadata updated for {TokenId}: CompletenessScore={Score}, ValidationStatus={Status}",
                    LoggingHelper.SanitizeLogInput(request.TokenIdentifier),
                    updatedMetadata.CompletenessScore,
                    updatedMetadata.ValidationStatus
                );

                return Ok(new UpdateTokenMetadataResponse
                {
                    Success = true,
                    Updated = true,
                    Metadata = updatedMetadata
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating token metadata for {TokenId} on {Chain}",
                    LoggingHelper.SanitizeLogInput(request.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(request.Chain)
                );

                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while updating token metadata",
                    Details = new Dictionary<string, object>
                    {
                        { "tokenIdentifier", request.TokenIdentifier },
                        { "chain", request.Chain }
                    }
                });
            }
        }

        /// <summary>
        /// Validates token metadata without persisting changes
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>Validation results with issues and completeness score</returns>
        /// <remarks>
        /// This endpoint allows you to validate metadata before creating or updating a token.
        /// It performs the same validation as the create/update endpoints but does not persist any changes.
        /// 
        /// Use this endpoint to:
        /// - Preview validation results before token deployment
        /// - Check metadata completeness score
        /// - Get remediation guidance for validation issues
        /// - Verify URL formats and field constraints
        /// 
        /// The response includes all validation issues with error codes, severity levels,
        /// and specific remediation guidance for each issue.
        /// </remarks>
        [HttpPost("token/validate")]
        [SwaggerOperation(
            Summary = "Validate token metadata",
            Description = "Validates token metadata and returns validation results without persisting changes"
        )]
        [SwaggerResponse(200, "Validation completed", typeof(EnrichedTokenMetadata))]
        [SwaggerResponse(400, "Invalid request", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> ValidateMetadata([FromBody] EnrichedTokenMetadata metadata)
        {
            try
            {
                _logger.LogInformation(
                    "Validate token metadata request from {User} - Token: {TokenId}, Chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(metadata.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(metadata.Chain)
                );

                var validatedMetadata = await _metadataService.ValidateMetadataAsync(metadata);

                _logger.LogInformation(
                    "Metadata validation complete for {TokenId}: Status={Status}, Score={Score}, Issues={IssueCount}",
                    LoggingHelper.SanitizeLogInput(metadata.TokenIdentifier),
                    validatedMetadata.ValidationStatus,
                    validatedMetadata.CompletenessScore,
                    validatedMetadata.ValidationIssues.Count
                );

                return Ok(validatedMetadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error validating token metadata for {TokenId} on {Chain}",
                    LoggingHelper.SanitizeLogInput(metadata.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(metadata.Chain)
                );

                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while validating token metadata"
                });
            }
        }
    }
}

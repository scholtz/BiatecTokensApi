using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for token metadata validation and evidence management
    /// </summary>
    /// <remarks>
    /// This controller manages validation operations for token issuances including
    /// pre-issuance validation, evidence retrieval, and validation history.
    /// All endpoints require ARC-0014 authentication.
    /// Supports deterministic validation with versioned rule sets and immutable audit trails.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance")]
    public class ValidationController : ControllerBase
    {
        private readonly IValidationService _validationService;
        private readonly ILogger<ValidationController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationController"/> class.
        /// </summary>
        /// <param name="validationService">The validation service</param>
        /// <param name="logger">The logger instance</param>
        public ValidationController(
            IValidationService validationService,
            ILogger<ValidationController> logger)
        {
            _validationService = validationService;
            _logger = logger;
        }

        /// <summary>
        /// Validates token metadata before issuance
        /// </summary>
        /// <param name="request">The validation request containing metadata and context</param>
        /// <returns>Validation result with evidence and rule evaluations</returns>
        /// <remarks>
        /// This endpoint performs deterministic validation of token metadata against versioned rule sets.
        /// 
        /// **Validation Process**:
        /// 1. Metadata is validated against token standard rules (ASA, ARC3, ARC200, ERC20)
        /// 2. Network-specific constraints are enforced
        /// 3. Jurisdiction-specific toggles are applied
        /// 4. Complete rule evaluations are generated with pass/fail/skip status
        /// 5. Evidence is stored with SHA256 checksum for tamper detection
        /// 
        /// **Dry-Run Mode**: Set `DryRun: true` to validate without persisting evidence.
        /// Useful for early validation during token configuration.
        /// 
        /// **Evidence Storage**: If `DryRun: false`, validation evidence is persisted
        /// with a unique evidence ID for future retrieval. Evidence includes complete
        /// rule evaluations, timestamps, validator versions, and checksums.
        /// 
        /// **Use Cases**:
        /// - Pre-issuance validation during token configuration
        /// - Compliance checking before submitting transactions
        /// - Testing metadata against multiple networks or jurisdictions
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ComplianceValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateTokenMetadata([FromBody] ComplianceValidationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userAddress = GetUserAddress();
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new ComplianceValidationResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _validationService.ValidateTokenMetadataAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Token metadata validation completed. Standard: {Standard}, Network: {Network}, Passed: {Passed}, DryRun: {DryRun}",
                        LoggingHelper.SanitizeLogInput(request.Context.TokenStandard),
                        LoggingHelper.SanitizeLogInput(request.Context.Network),
                        result.Passed,
                        request.DryRun);

                    return Ok(result);
                }
                else
                {
                    _logger.LogError(
                        "Validation failed: {Error}",
                        LoggingHelper.SanitizeLogInput(result.ErrorMessage));
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during token metadata validation");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceValidationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Retrieves validation evidence by evidence ID
        /// </summary>
        /// <param name="evidenceId">The unique evidence identifier</param>
        /// <returns>Validation evidence with complete rule evaluations</returns>
        /// <remarks>
        /// This endpoint retrieves immutable validation evidence by its unique identifier.
        /// Evidence includes:
        /// - Complete rule evaluations with pass/fail/skip status
        /// - Validation context (network, standard, flags, versions)
        /// - Timestamp and requester information
        /// - SHA256 checksum for tamper detection
        /// 
        /// **Use Cases**:
        /// - Retrieving evidence for compliance audits
        /// - Verifying validation results before issuance
        /// - Displaying validation history to users
        /// 
        /// Evidence is immutable and retained for at least 12 months.
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpGet("evidence/{evidenceId}")]
        [ProducesResponseType(typeof(GetValidationEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetValidationEvidence([FromRoute] string evidenceId)
        {
            try
            {
                var result = await _validationService.GetValidationEvidenceAsync(evidenceId);

                if (result.Success && result.Evidence != null)
                {
                    _logger.LogInformation(
                        "Retrieved validation evidence {EvidenceId}",
                        LoggingHelper.SanitizeLogInput(evidenceId));
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning(
                        "Validation evidence not found: {EvidenceId}",
                        LoggingHelper.SanitizeLogInput(evidenceId));
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving validation evidence {EvidenceId}", evidenceId);
                return StatusCode(StatusCodes.Status500InternalServerError, new GetValidationEvidenceResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists validation evidence for a token or pre-issuance identifier
        /// </summary>
        /// <param name="tokenId">Optional filter by token ID</param>
        /// <param name="preIssuanceId">Optional filter by pre-issuance identifier</param>
        /// <param name="passed">Optional filter by validation result (true/false)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of validation evidence entries with pagination</returns>
        /// <remarks>
        /// This endpoint lists validation evidence with optional filtering and pagination.
        /// 
        /// **Filtering Options**:
        /// - By token ID: List all validations for a deployed token
        /// - By pre-issuance ID: List all validations during token configuration
        /// - By result: Show only passing or failing validations
        /// - By date range: Filter validations within a time period
        /// 
        /// **Use Cases**:
        /// - Displaying validation history for a token
        /// - Tracking validation attempts during configuration
        /// - Compliance reporting and audit trails
        /// - Analyzing validation patterns and failures
        /// 
        /// Evidence is ordered by timestamp descending (most recent first).
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpGet("evidence")]
        [ProducesResponseType(typeof(ListValidationEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListValidationEvidence(
            [FromQuery] ulong? tokenId = null,
            [FromQuery] string? preIssuanceId = null,
            [FromQuery] bool? passed = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListValidationEvidenceRequest
                {
                    TokenId = tokenId,
                    PreIssuanceId = preIssuanceId,
                    Passed = passed,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _validationService.ListValidationEvidenceAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Listed {Count} validation evidence entries (page {Page} of {TotalPages})",
                        result.Evidence.Count,
                        result.Page,
                        result.TotalPages);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list validation evidence: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing validation evidence");
                return StatusCode(StatusCodes.Status500InternalServerError, new ListValidationEvidenceResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the user's address from the authentication context
        /// </summary>
        private string? GetUserAddress()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                   User.FindFirst("address")?.Value;
        }
    }
}

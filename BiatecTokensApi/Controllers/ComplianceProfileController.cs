using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceProfile;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing compliance profiles for wallet-free enterprise onboarding
    /// </summary>
    /// <remarks>
    /// This controller supports backend-first compliance metadata management for regulated token issuance.
    /// All endpoints use SaaS terminology (account-based) and avoid wallet language.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance-profile")]
    public class ComplianceProfileController : ControllerBase
    {
        private readonly IComplianceProfileService _service;
        private readonly ILogger<ComplianceProfileController> _logger;

        public ComplianceProfileController(
            IComplianceProfileService service,
            ILogger<ComplianceProfileController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Creates or updates the compliance profile for the authenticated account
        /// </summary>
        /// <param name="request">Compliance profile data</param>
        /// <returns>The created or updated compliance profile</returns>
        /// <remarks>
        /// This endpoint supports wallet-free enterprise onboarding by capturing regulatory metadata
        /// upfront. The compliance profile includes issuing entity details, jurisdiction, and issuance
        /// intent to support MICA readiness and compliance reporting.
        /// 
        /// All changes are logged in the audit trail with timestamp and actor information.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(ComplianceProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateOrUpdateProfile([FromBody] UpsertComplianceProfileRequest request)
        {
            // Get user ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Unauthorized compliance profile creation attempt - no user ID in claims");
                return Unauthorized(new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "User authentication required"
                });
            }

            // Get IP address and user agent for audit logging
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            _logger.LogInformation("Processing compliance profile upsert for user {UserId}",
                LoggingHelper.SanitizeLogInput(userId));

            var response = await _service.UpsertProfileAsync(request, userId, ipAddress, userAgent);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Gets the compliance profile for the authenticated account
        /// </summary>
        /// <returns>The compliance profile if it exists</returns>
        /// <remarks>
        /// Returns the current compliance profile including onboarding completion status.
        /// The profile includes all regulatory metadata and readiness indicators.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ComplianceProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProfile()
        {
            // Get user ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Unauthorized compliance profile access attempt - no user ID in claims");
                return Unauthorized(new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "User authentication required"
                });
            }

            _logger.LogInformation("Retrieving compliance profile for user {UserId}",
                LoggingHelper.SanitizeLogInput(userId));

            var response = await _service.GetProfileAsync(userId);

            if (!response.Success)
            {
                if (response.ErrorCode == ErrorCodes.COMPLIANCE_PROFILE_NOT_FOUND)
                {
                    return NotFound(response);
                }
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Gets the audit log for the authenticated account's compliance profile
        /// </summary>
        /// <param name="startDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="endDate">Optional end date filter (ISO 8601 format)</param>
        /// <returns>List of audit log entries</returns>
        /// <remarks>
        /// Returns all compliance profile changes with timestamp, actor, and changed fields.
        /// Supports date range filtering for compliance reporting and audit purposes.
        /// </remarks>
        [HttpGet("audit-log")]
        [ProducesResponseType(typeof(List<ComplianceProfileAuditEntry>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAuditLog(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            // Get user ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Unauthorized audit log access attempt - no user ID in claims");
                return Unauthorized(new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "User authentication required"
                });
            }

            _logger.LogInformation("Retrieving audit log for user {UserId}, StartDate={StartDate}, EndDate={EndDate}",
                LoggingHelper.SanitizeLogInput(userId),
                startDate?.ToString("O") ?? "null",
                endDate?.ToString("O") ?? "null");

            try
            {
                var entries = await _service.GetAuditLogAsync(userId, startDate, endDate);
                return Ok(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit log for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return StatusCode(500, new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving the audit log"
                });
            }
        }

        /// <summary>
        /// Exports the audit log as JSON for the authenticated account
        /// </summary>
        /// <param name="startDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="endDate">Optional end date filter (ISO 8601 format)</param>
        /// <returns>JSON export of audit log entries</returns>
        /// <remarks>
        /// Returns audit log entries in JSON format suitable for compliance reporting
        /// and data archival. Supports date range filtering.
        /// </remarks>
        [HttpGet("audit-log/export/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogJson(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            // Get user ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "User authentication required"
                });
            }

            _logger.LogInformation("Exporting audit log as JSON for user {UserId}",
                LoggingHelper.SanitizeLogInput(userId));

            try
            {
                var json = await _service.ExportAuditLogJsonAsync(userId, startDate, endDate);
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit log as JSON for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return StatusCode(500, new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while exporting the audit log"
                });
            }
        }

        /// <summary>
        /// Exports the audit log as CSV for the authenticated account
        /// </summary>
        /// <param name="startDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="endDate">Optional end date filter (ISO 8601 format)</param>
        /// <returns>CSV export of audit log entries</returns>
        /// <remarks>
        /// Returns audit log entries in CSV format suitable for spreadsheet analysis
        /// and enterprise reporting systems. Supports date range filtering.
        /// </remarks>
        [HttpGet("audit-log/export/csv")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogCsv(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            // Get user ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "User authentication required"
                });
            }

            _logger.LogInformation("Exporting audit log as CSV for user {UserId}",
                LoggingHelper.SanitizeLogInput(userId));

            try
            {
                var csv = await _service.ExportAuditLogCsvAsync(userId, startDate, endDate);
                return Content(csv, "text/csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit log as CSV for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return StatusCode(500, new BaseResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while exporting the audit log"
                });
            }
        }
    }
}

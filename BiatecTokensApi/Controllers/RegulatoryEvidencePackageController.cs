using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides regulator-facing evidence package APIs for compliance workflows.
    ///
    /// Evidence packages are authoritative, structured, audience-aware artifacts that enterprise
    /// compliance teams, legal reviewers, and regulators can rely on for governance sign-off,
    /// audit submissions, and internal review.
    ///
    /// Two retrieval modes are available:
    ///   - Summary: lightweight, UI-friendly snapshot of package status and manifest counts.
    ///   - Detail (canonical): full payload with manifest, KYC/AML summary, contradictions,
    ///     remediation items, approval history, posture transitions, and readiness rationale.
    ///
    /// The backend is the authoritative source for package composition.
    /// Readiness is fail-closed: any missing, stale, or contradicted evidence downgrades status.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/regulatory-evidence-packages")]
    [Produces("application/json")]
    public class RegulatoryEvidencePackageController : ControllerBase
    {
        private readonly IRegulatoryEvidencePackageService _service;
        private readonly ILogger<RegulatoryEvidencePackageController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="RegulatoryEvidencePackageController"/>.
        /// </summary>
        public RegulatoryEvidencePackageController(
            IRegulatoryEvidencePackageService service,
            ILogger<RegulatoryEvidencePackageController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Assembles a new regulatory evidence package for the specified subject and audience profile.
        /// </summary>
        /// <param name="request">
        /// Package creation request. Must include SubjectId and AudienceProfile.
        /// Supply an IdempotencyKey to enable safe retries without re-assembling the package.
        /// </param>
        /// <returns>
        /// Package creation response containing the assembled package summary.
        /// Use the returned PackageId to retrieve the summary or canonical detail.
        /// </returns>
        /// <remarks>
        /// Audience profiles affect summary framing only — the canonical record is never redacted.
        /// Readiness semantics are fail-closed: missing, stale, or contradicted evidence downgrades
        /// the readiness status explicitly rather than silently passing incomplete packages.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(CreateRegulatoryEvidencePackageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreatePackage([FromBody] CreateRegulatoryEvidencePackageRequest request)
        {
            var correlationId = GetCorrelationId();
            request.CorrelationId ??= correlationId;

            _logger.LogInformation(
                "CreatePackage. SubjectId={SubjectId} Audience={Audience} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.AudienceProfile,
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                if (string.IsNullOrWhiteSpace(request.SubjectId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "SubjectId is required.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var result = await _service.CreatePackageAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Package assembly failed.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating evidence package for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while assembling the evidence package.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Get summary ────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the lightweight summary of a regulatory evidence package.
        /// </summary>
        /// <param name="packageId">Stable package identifier returned from the create endpoint.</param>
        /// <returns>
        /// Package summary with readiness status, headline rationale, manifest counts,
        /// and open contradiction/remediation counts. Does not include full payload detail.
        /// </returns>
        /// <remarks>
        /// Use this endpoint for UI dashboards, approval workflow previews, and status checks.
        /// For canonical export-grade content use the detail endpoint.
        /// </remarks>
        [HttpGet("{packageId}/summary")]
        [ProducesResponseType(typeof(GetPackageSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPackageSummary(string packageId)
        {
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetPackageSummary. PackageId={PackageId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(packageId),
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "PackageId is required.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var result = await _service.GetPackageSummaryAsync(packageId, correlationId);

                if (!result.Success)
                {
                    if (result.ErrorCode == "NOT_FOUND")
                    {
                        return NotFound(new ApiErrorResponse
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.NOT_FOUND,
                            ErrorMessage = result.ErrorMessage ?? $"Package '{LoggingHelper.SanitizeLogInput(packageId)}' was not found.",
                            Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                        });
                    }

                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Failed to retrieve package summary.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving summary for package {PackageId}",
                    LoggingHelper.SanitizeLogInput(packageId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while retrieving the package summary.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Get canonical detail ───────────────────────────────────────────────

        /// <summary>
        /// Retrieves the canonical detail payload for a regulatory evidence package.
        /// </summary>
        /// <param name="packageId">Stable package identifier.</param>
        /// <returns>
        /// Full canonical package with manifest, KYC/AML decision summary, contradiction entries,
        /// remediation items, approval history, posture transitions, and authoritative readiness rationale.
        /// Use for export generation, archival, and regulator submission workflows.
        /// </returns>
        /// <remarks>
        /// The canonical payload is never redacted regardless of audience profile.
        /// Audience rules are recorded in the manifest for traceability.
        /// </remarks>
        [HttpGet("{packageId}")]
        [ProducesResponseType(typeof(GetPackageDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPackageDetail(string packageId)
        {
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetPackageDetail. PackageId={PackageId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(packageId),
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "PackageId is required.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var result = await _service.GetPackageDetailAsync(packageId, correlationId);

                if (!result.Success)
                {
                    if (result.ErrorCode == "NOT_FOUND")
                    {
                        return NotFound(new ApiErrorResponse
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.NOT_FOUND,
                            ErrorMessage = result.ErrorMessage ?? $"Package '{LoggingHelper.SanitizeLogInput(packageId)}' was not found.",
                            Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                        });
                    }

                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Failed to retrieve package detail.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving detail for package {PackageId}",
                    LoggingHelper.SanitizeLogInput(packageId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while retrieving the package detail.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── List packages ──────────────────────────────────────────────────────

        /// <summary>
        /// Lists regulatory evidence packages for a subject, most recently generated first.
        /// </summary>
        /// <param name="subjectId">Subject or issuer identifier.</param>
        /// <param name="limit">Maximum number of summaries to return (1–100, default 20).</param>
        /// <returns>Ordered list of package summaries for the subject.</returns>
        [HttpGet("subject/{subjectId}")]
        [ProducesResponseType(typeof(ListEvidencePackagesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListPackages(string subjectId, [FromQuery] int limit = 20)
        {
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "ListPackages. SubjectId={SubjectId} Limit={Limit} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                limit,
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                if (string.IsNullOrWhiteSpace(subjectId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "SubjectId is required.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var result = await _service.ListPackagesAsync(subjectId, limit, correlationId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error listing packages for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(subjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while listing evidence packages.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Health ─────────────────────────────────────────────────────────────

        /// <summary>Health check for the Regulatory Evidence Package API.</summary>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new ApiStatusResponse
            {
                Status = "Healthy",
                Version = "v1.0",
                Timestamp = DateTime.UtcNow
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetCorrelationId()
        {
            if (HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header) &&
                !string.IsNullOrWhiteSpace(header))
                return header.ToString();
            return Guid.NewGuid().ToString();
        }
    }
}

using System.Security.Claims;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TenantBranding;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Manages tenant branding and domain configuration for white-label issuance experiences.
    /// </summary>
    /// <remarks>
    /// Provides a governed draft/publish lifecycle for identity fields, constrained theme tokens,
    /// support metadata, and domain records. The published payload is available for unauthenticated
    /// frontend rendering and falls back to safe Biatec-branded defaults when no valid published
    /// configuration exists.
    ///
    /// Authorization is strict per-tenant: the authenticated caller's identity is used as the
    /// tenant identifier, ensuring cross-tenant access is impossible.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/tenant-branding")]
    [Produces("application/json")]
    public class TenantBrandingController : ControllerBase
    {
        private readonly ITenantBrandingService _service;
        private readonly ILogger<TenantBrandingController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="TenantBrandingController"/>.
        /// </summary>
        /// <param name="service">Tenant branding service.</param>
        /// <param name="logger">Logger instance.</param>
        public TenantBrandingController(ITenantBrandingService service, ILogger<TenantBrandingController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Returns the authenticated user's identity, used as the tenant identifier.
        /// </summary>
        private string CallerIdentity =>
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "unknown";

        // ── Draft Management ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the caller's current branding draft.
        /// </summary>
        /// <remarks>
        /// If no draft has been created yet, returns a response with
        /// <c>Status = NotConfigured</c>. Does not expose another tenant's configuration.
        /// </remarks>
        /// <returns>The current branding draft or a not-configured shell.</returns>
        [HttpGet("draft")]
        [ProducesResponseType(typeof(TenantBrandingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDraft()
        {
            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.GetDraftAsync(tenantId, tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving branding draft for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Creates or updates the caller's branding draft.
        /// </summary>
        /// <remarks>
        /// All request fields are optional — omitted fields preserve their existing values.
        /// Validation is run automatically; the response includes any current validation errors.
        /// The draft is not published until <c>POST /publish</c> is called explicitly.
        /// </remarks>
        /// <param name="request">Branding fields to set or update.</param>
        /// <returns>The updated branding draft including current validation state.</returns>
        [HttpPut("draft")]
        [ProducesResponseType(typeof(TenantBrandingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDraft([FromBody] UpdateTenantBrandingDraftRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new TenantBrandingResponse
                {
                    Success = false,
                    ErrorMessage = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage))
                });

            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.UpdateDraftAsync(request, tenantId, tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating branding draft for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Validation ────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the caller's current branding draft and returns actionable error details.
        /// </summary>
        /// <remarks>
        /// Returns <c>IsValid = true</c> when the draft is ready to publish.
        /// Does not modify the draft state.
        /// </remarks>
        /// <returns>Validation result with error list.</returns>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(TenantBrandingValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateDraft()
        {
            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.ValidateDraftAsync(tenantId, tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating branding draft for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingValidationResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Publish Lifecycle ─────────────────────────────────────────────────────

        /// <summary>
        /// Publishes the caller's current branding draft, making it the live configuration.
        /// </summary>
        /// <remarks>
        /// Publishing requires all validation checks to pass. Returns a <c>400 Bad Request</c>
        /// if the draft has validation errors, along with the error list for operator action.
        /// Publishing is an intentional lifecycle step — it does not happen implicitly on save.
        /// </remarks>
        /// <returns>The published branding configuration.</returns>
        [HttpPost("publish")]
        [ProducesResponseType(typeof(TenantBrandingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Publish()
        {
            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.PublishAsync(tenantId, tenantId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing branding for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Returns the published branding payload for the specified tenant identifier.
        /// </summary>
        /// <remarks>
        /// This endpoint is publicly accessible (no authentication required) so that frontend
        /// applications can load branded experience data at startup without requiring the user
        /// to authenticate first.
        ///
        /// When no valid published branding exists for the requested tenant, the response
        /// contains a safe fallback payload with <c>IsFallback = true</c>, indicating the
        /// frontend should render the default Biatec-branded experience.
        /// </remarks>
        /// <param name="tenantId">The tenant identifier to load published branding for.</param>
        /// <returns>Published branding payload or safe fallback.</returns>
        [HttpGet("published")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TenantBrandingPublishedResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPublished([FromQuery] string? tenantId = null)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                // Return global fallback when no tenant is specified
                return Ok(new TenantBrandingPublishedResponse
                {
                    Success = true,
                    Payload = new TenantBrandingPublishedPayload { TenantId = string.Empty, IsFallback = true, Version = 0 }
                });
            }

            try
            {
                var result = await _service.GetPublishedAsync(tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving published branding for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(tenantId));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingPublishedResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Status ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current lifecycle status of the caller's branding configuration.
        /// </summary>
        /// <remarks>
        /// Provides signals for frontend UX to distinguish between not-configured, draft,
        /// invalid (has validation errors), and published states.
        /// </remarks>
        /// <returns>Branding lifecycle status summary.</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(TenantBrandingStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.GetStatusAsync(tenantId, tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving branding status for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingStatusResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Domain Configuration ──────────────────────────────────────────────────

        /// <summary>
        /// Returns all domain records for the caller's tenant.
        /// </summary>
        /// <remarks>
        /// Domain records surface readiness and misconfiguration states so operators can
        /// understand which domains are verified, pending, or misconfigured.
        /// </remarks>
        /// <returns>List of domain records with readiness status.</returns>
        [HttpGet("domains")]
        [ProducesResponseType(typeof(TenantDomainListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDomains()
        {
            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.GetDomainsAsync(tenantId, tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving domains for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantDomainListResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Adds a new domain or updates an existing domain record for the caller's tenant.
        /// </summary>
        /// <remarks>
        /// New domains are created with <c>Pending</c> status and a generated verification token.
        /// The verification token must be published as a DNS TXT record or HTTP well-known path
        /// to confirm domain ownership. Updating an existing domain (same FQDN) only modifies the
        /// optional notes field; verification state is preserved.
        /// </remarks>
        /// <param name="request">Domain upsert request containing the FQDN and optional notes.</param>
        /// <returns>The created or updated domain record.</returns>
        [HttpPut("domains")]
        [ProducesResponseType(typeof(TenantDomainResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpsertDomain([FromBody] UpsertTenantDomainRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new TenantDomainResponse
                {
                    Success = false,
                    ErrorMessage = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage))
                });

            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.UpsertDomainAsync(request, tenantId, tenantId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting domain for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantDomainResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Audit History ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the branding change history for the caller's tenant.
        /// </summary>
        /// <remarks>
        /// History entries are ordered from most recent to oldest and include draft saves,
        /// publish events, and any reset operations. This provides basic auditability for
        /// enterprise operators.
        /// </remarks>
        /// <returns>Branding audit history ordered from most recent to oldest.</returns>
        [HttpGet("history")]
        [ProducesResponseType(typeof(TenantBrandingHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var tenantId = CallerIdentity;
                var result = await _service.GetHistoryAsync(tenantId, tenantId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving branding history for tenant {TenantId}",
                    LoggingHelper.SanitizeLogInput(CallerIdentity));
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new TenantBrandingHistoryResponse { Success = false, ErrorMessage = "An unexpected error occurred." });
            }
        }
    }
}

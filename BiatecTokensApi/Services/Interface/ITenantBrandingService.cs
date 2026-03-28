using BiatecTokensApi.Models.TenantBranding;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for managing tenant branding and domain configuration with
    /// draft/published lifecycle, server-side validation, audit history,
    /// and safe fallback semantics.
    /// </summary>
    public interface ITenantBrandingService
    {
        // ── Draft Management ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current branding draft for the specified tenant.
        /// If no draft exists, returns a response with <c>Success = true</c> and a
        /// <see cref="TenantBrandingConfig"/> whose status is <see cref="TenantBrandingLifecycleStatus.NotConfigured"/>.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor (for authorization checks).</param>
        Task<TenantBrandingResponse> GetDraftAsync(string tenantId, string actorId);

        /// <summary>
        /// Creates or updates the branding draft for the specified tenant.
        /// Increments the version counter and re-evaluates validation errors.
        /// Does not activate or publish the configuration.
        /// </summary>
        /// <param name="request">Fields to set or update.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        Task<TenantBrandingResponse> UpdateDraftAsync(UpdateTenantBrandingDraftRequest request, string tenantId, string actorId);

        // ── Validation ────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the current draft for the specified tenant and returns a detailed
        /// list of any validation errors without changing the draft state.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        Task<TenantBrandingValidationResponse> ValidateDraftAsync(string tenantId, string actorId);

        // ── Publish Lifecycle ─────────────────────────────────────────────────────

        /// <summary>
        /// Publishes the current draft for the specified tenant, making it the live
        /// branding configuration for frontend rendering.
        /// Returns an error response if the draft has validation errors.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the actor performing the publish.</param>
        Task<TenantBrandingResponse> PublishAsync(string tenantId, string actorId);

        /// <summary>
        /// Returns the published branding payload for the specified tenant.
        /// If no valid published configuration exists, returns a safe fallback payload
        /// with <see cref="TenantBrandingPublishedPayload.IsFallback"/> set to <c>true</c>.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        Task<TenantBrandingPublishedResponse> GetPublishedAsync(string tenantId);

        // ── Status ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a summary of the current lifecycle status of the tenant's branding
        /// configuration, including draft and published state signals for UX surfaces.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        Task<TenantBrandingStatusResponse> GetStatusAsync(string tenantId, string actorId);

        // ── Domain Configuration ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the list of domain records for the specified tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        Task<TenantDomainListResponse> GetDomainsAsync(string tenantId, string actorId);

        /// <summary>
        /// Adds a new domain record for the tenant, or updates an existing record
        /// if a record with the same domain name already exists for this tenant.
        /// New domains are created with <see cref="TenantDomainReadinessStatus.Pending"/> status
        /// and a generated verification token.
        /// </summary>
        /// <param name="request">Domain upsert request.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        Task<TenantDomainResponse> UpsertDomainAsync(UpsertTenantDomainRequest request, string tenantId, string actorId);

        // ── Audit History ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the branding change history for the specified tenant, ordered from
        /// most recent to oldest. Includes draft saves, publish events, and resets.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        Task<TenantBrandingHistoryResponse> GetHistoryAsync(string tenantId, string actorId);
    }
}

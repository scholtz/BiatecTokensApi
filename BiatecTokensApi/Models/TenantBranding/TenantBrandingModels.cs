using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.TenantBranding
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lifecycle status for tenant branding configuration.
    /// </summary>
    public enum TenantBrandingLifecycleStatus
    {
        /// <summary>Branding is being drafted and has not been validated or published.</summary>
        Draft,

        /// <summary>Branding has been validated and published for live frontend rendering.</summary>
        Published,

        /// <summary>Draft exists but contains validation errors that prevent publishing.</summary>
        Invalid,

        /// <summary>Publishing is blocked due to policy or compliance constraints.</summary>
        Blocked,

        /// <summary>No branding configuration exists; platform defaults will be used.</summary>
        NotConfigured
    }

    /// <summary>
    /// Readiness status for a domain record associated with a tenant's branding configuration.
    /// </summary>
    public enum TenantDomainReadinessStatus
    {
        /// <summary>Domain has been registered but not yet verified.</summary>
        Pending,

        /// <summary>Domain verification is confirmed and branding can be served on this domain.</summary>
        Verified,

        /// <summary>Domain record exists but DNS or verification token is missing or wrong.</summary>
        Misconfigured,

        /// <summary>Domain has been deactivated or blocked for policy reasons.</summary>
        Blocked,

        /// <summary>No domain record has been configured.</summary>
        Unconfigured
    }

    // ── Sub-models ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Constrained color token set for tenant branding.
    /// All values must be valid CSS hex color strings (e.g. <c>#1A2B3C</c> or <c>#FFF</c>).
    /// </summary>
    public class TenantThemeTokens
    {
        /// <summary>Primary brand color used for key interactive elements and headings.</summary>
        public string? PrimaryColor { get; set; }

        /// <summary>Secondary brand color used for supporting UI elements.</summary>
        public string? SecondaryColor { get; set; }

        /// <summary>Accent color used for calls-to-action and highlights.</summary>
        public string? AccentColor { get; set; }

        /// <summary>Background color for the primary application surface.</summary>
        public string? BackgroundColor { get; set; }

        /// <summary>Foreground / body text color.</summary>
        public string? TextColor { get; set; }
    }

    /// <summary>
    /// Customer-facing support and legal contact metadata for a tenant branding configuration.
    /// </summary>
    public class TenantSupportMetadata
    {
        /// <summary>Support email address shown to end users (must be a valid email if provided).</summary>
        public string? SupportEmail { get; set; }

        /// <summary>Support portal URL (must be a valid HTTPS URL if provided).</summary>
        public string? SupportUrl { get; set; }

        /// <summary>Legal contact email (must be a valid email if provided).</summary>
        public string? LegalContactEmail { get; set; }

        /// <summary>Legal contact URL (must be a valid HTTPS URL if provided).</summary>
        public string? LegalContactUrl { get; set; }
    }

    // ── Core Configuration Model ──────────────────────────────────────────────────

    /// <summary>
    /// Complete tenant branding configuration including identity fields, theme tokens,
    /// support metadata, and lifecycle state.
    /// </summary>
    public class TenantBrandingConfig
    {
        /// <summary>Unique identifier for this tenant (derived from the authenticated actor).</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Legal or display name of the tenant organisation.
        /// Required for publishing; maximum 100 characters.
        /// </summary>
        public string? OrganizationName { get; set; }

        /// <summary>
        /// Short customer-facing product label (e.g. "Acme Token Platform").
        /// Optional; maximum 60 characters.
        /// </summary>
        public string? ProductLabel { get; set; }

        /// <summary>HTTPS URL or content-addressable reference to the tenant logo asset.</summary>
        public string? LogoUrl { get; set; }

        /// <summary>HTTPS URL or content-addressable reference to the tenant favicon asset.</summary>
        public string? FaviconUrl { get; set; }

        /// <summary>Constrained color token set for frontend theme rendering.</summary>
        public TenantThemeTokens Theme { get; set; } = new();

        /// <summary>Support and legal contact metadata for customer-facing surfaces.</summary>
        public TenantSupportMetadata Support { get; set; } = new();

        /// <summary>Current lifecycle status of this branding configuration.</summary>
        public TenantBrandingLifecycleStatus Status { get; set; } = TenantBrandingLifecycleStatus.NotConfigured;

        /// <summary>Monotonically increasing version counter incremented on each draft save.</summary>
        public int Version { get; set; } = 0;

        /// <summary>Identity of the actor who created this branding record.</summary>
        public string? CreatedBy { get; set; }

        /// <summary>UTC timestamp when this branding record was first created.</summary>
        public DateTimeOffset? CreatedAt { get; set; }

        /// <summary>Identity of the actor who last updated the draft.</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>UTC timestamp when the draft was last updated.</summary>
        public DateTimeOffset? UpdatedAt { get; set; }

        /// <summary>Identity of the actor who last published this branding configuration.</summary>
        public string? PublishedBy { get; set; }

        /// <summary>UTC timestamp when this branding configuration was last published.</summary>
        public DateTimeOffset? PublishedAt { get; set; }

        /// <summary>
        /// Server-side validation errors present on the current draft.
        /// Empty when the draft is valid or published.
        /// </summary>
        public List<TenantBrandingValidationError> ValidationErrors { get; set; } = new();
    }

    // ── Domain Records ────────────────────────────────────────────────────────────

    /// <summary>
    /// A domain record associating an approved domain with a tenant's published branding.
    /// </summary>
    public class TenantDomainRecord
    {
        /// <summary>Unique identifier for this domain record.</summary>
        public string DomainId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The fully qualified domain name (e.g. <c>tokens.acme.com</c>).</summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>The tenant this domain record belongs to.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Current readiness/verification status for this domain.</summary>
        public TenantDomainReadinessStatus Status { get; set; } = TenantDomainReadinessStatus.Pending;

        /// <summary>Verification token that must be placed in DNS TXT or HTTP well-known path.</summary>
        public string VerificationToken { get; set; } = string.Empty;

        /// <summary>Optional operator notes about this domain entry.</summary>
        public string? Notes { get; set; }

        /// <summary>UTC timestamp when this domain record was created.</summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>UTC timestamp when domain verification was confirmed, if applicable.</summary>
        public DateTimeOffset? VerifiedAt { get; set; }

        /// <summary>Identity of the actor who last updated this record.</summary>
        public string? UpdatedBy { get; set; }
    }

    // ── Validation ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single server-side validation error for a tenant branding configuration field.
    /// </summary>
    public class TenantBrandingValidationError
    {
        /// <summary>Dot-separated JSON path of the field that failed validation (e.g. <c>Theme.PrimaryColor</c>).</summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>Human-readable description of the validation failure, suitable for operator display.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Short machine-readable error code (e.g. <c>INVALID_COLOR_FORMAT</c>).</summary>
        public string Code { get; set; } = string.Empty;
    }

    // ── Published Payload ─────────────────────────────────────────────────────────

    /// <summary>
    /// Stable, frontend-consumable published branding payload.
    /// Contains only safe public fields from the live published configuration.
    /// </summary>
    public class TenantBrandingPublishedPayload
    {
        /// <summary>Tenant identifier this payload belongs to.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Organisation display name for public-facing surfaces.</summary>
        public string? OrganizationName { get; set; }

        /// <summary>Short customer-facing product label.</summary>
        public string? ProductLabel { get; set; }

        /// <summary>HTTPS URL to the tenant logo asset.</summary>
        public string? LogoUrl { get; set; }

        /// <summary>HTTPS URL to the tenant favicon asset.</summary>
        public string? FaviconUrl { get; set; }

        /// <summary>Published color token set for frontend theme rendering.</summary>
        public TenantThemeTokens Theme { get; set; } = new();

        /// <summary>Published support metadata for customer-facing surfaces.</summary>
        public TenantSupportMetadata Support { get; set; } = new();

        /// <summary>Version of the published configuration this payload was derived from.</summary>
        public int Version { get; set; }

        /// <summary>UTC timestamp when this payload was published.</summary>
        public DateTimeOffset? PublishedAt { get; set; }

        /// <summary>
        /// When <c>true</c>, this payload represents safe platform defaults because the tenant
        /// has no valid published branding, and the frontend should render a Biatec-branded experience.
        /// </summary>
        public bool IsFallback { get; set; }
    }

    // ── Audit History ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A single entry in the tenant branding audit history.
    /// </summary>
    public class TenantBrandingHistoryEntry
    {
        /// <summary>Unique identifier for this history entry.</summary>
        public string EntryId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Version of the branding configuration this event applies to.</summary>
        public int Version { get; set; }

        /// <summary>Type of change: <c>DraftSaved</c>, <c>Published</c>, <c>DraftReset</c>.</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Identity of the actor who performed the change.</summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>UTC timestamp of this history entry.</summary>
        public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Human-readable description of the change.</summary>
        public string? Description { get; set; }
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to create or update a tenant branding draft.
    /// All fields are optional; omitted fields preserve their existing values.
    /// </summary>
    public class UpdateTenantBrandingDraftRequest
    {
        /// <summary>
        /// Legal or display name of the tenant organisation.
        /// Maximum 100 characters; required when publishing.
        /// </summary>
        [MaxLength(100)]
        public string? OrganizationName { get; set; }

        /// <summary>
        /// Short customer-facing product label (e.g. "Acme Token Platform").
        /// Maximum 60 characters; optional.
        /// </summary>
        [MaxLength(60)]
        public string? ProductLabel { get; set; }

        /// <summary>HTTPS URL or content-addressable reference to the tenant logo asset.</summary>
        public string? LogoUrl { get; set; }

        /// <summary>HTTPS URL or content-addressable reference to the tenant favicon asset.</summary>
        public string? FaviconUrl { get; set; }

        /// <summary>Color token set. Only provided tokens are updated; others are preserved.</summary>
        public TenantThemeTokens? Theme { get; set; }

        /// <summary>Support metadata. Only provided fields are updated; others are preserved.</summary>
        public TenantSupportMetadata? Support { get; set; }
    }

    /// <summary>
    /// Request to add or update a domain record for the authenticated tenant.
    /// </summary>
    public class UpsertTenantDomainRequest
    {
        /// <summary>
        /// The fully qualified domain name (e.g. <c>tokens.acme.com</c>).
        /// Required; maximum 253 characters.
        /// </summary>
        [Required]
        [MaxLength(253)]
        public string Domain { get; set; } = string.Empty;

        /// <summary>Optional operator notes about this domain entry.</summary>
        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Response wrapping a tenant branding configuration.
    /// </summary>
    public class TenantBrandingResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The branding configuration, populated on success.</summary>
        public TenantBrandingConfig? Branding { get; set; }

        /// <summary>Human-readable error message populated on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response from a draft validation request.
    /// </summary>
    public class TenantBrandingValidationResponse
    {
        /// <summary>Whether the validation request completed successfully (even if the draft is invalid).</summary>
        public bool Success { get; set; }

        /// <summary>Whether the draft is valid and ready to publish.</summary>
        public bool IsValid { get; set; }

        /// <summary>List of validation errors found. Empty when the draft is valid.</summary>
        public List<TenantBrandingValidationError> Errors { get; set; } = new();

        /// <summary>Human-readable error message populated on unexpected failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response wrapping the published branding payload for frontend rendering.
    /// </summary>
    public class TenantBrandingPublishedResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The published branding payload. Always populated on success (may be a fallback).</summary>
        public TenantBrandingPublishedPayload? Payload { get; set; }

        /// <summary>Human-readable error message populated on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response describing the current lifecycle status of a tenant branding configuration.
    /// </summary>
    public class TenantBrandingStatusResponse
    {
        /// <summary>Whether the status retrieval succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Current lifecycle status.</summary>
        public TenantBrandingLifecycleStatus Status { get; set; }

        /// <summary>Whether a draft currently exists.</summary>
        public bool HasDraft { get; set; }

        /// <summary>Whether a published configuration is live.</summary>
        public bool HasPublished { get; set; }

        /// <summary>Whether the draft has been validated and is ready to publish.</summary>
        public bool IsDraftValid { get; set; }

        /// <summary>Number of validation errors on the current draft.</summary>
        public int ValidationErrorCount { get; set; }

        /// <summary>Version of the live published configuration, or null if none.</summary>
        public int? PublishedVersion { get; set; }

        /// <summary>UTC timestamp of the last published configuration, or null if none.</summary>
        public DateTimeOffset? LastPublishedAt { get; set; }

        /// <summary>Human-readable description of the current status.</summary>
        public string? StatusDescription { get; set; }

        /// <summary>Human-readable error message populated on unexpected failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response wrapping a single tenant domain record.
    /// </summary>
    public class TenantDomainResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The domain record, populated on success.</summary>
        public TenantDomainRecord? Domain { get; set; }

        /// <summary>Human-readable error message populated on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response wrapping the list of domain records for a tenant.
    /// </summary>
    public class TenantDomainListResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The domain records for the tenant.</summary>
        public List<TenantDomainRecord> Domains { get; set; } = new();

        /// <summary>Human-readable error message populated on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response wrapping the branding audit history for a tenant.
    /// </summary>
    public class TenantBrandingHistoryResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Audit history entries, ordered from most recent to oldest.</summary>
        public List<TenantBrandingHistoryEntry> History { get; set; } = new();

        /// <summary>Human-readable error message populated on failure.</summary>
        public string? ErrorMessage { get; set; }
    }
}

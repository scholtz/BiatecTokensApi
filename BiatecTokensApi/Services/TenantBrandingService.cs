using System.Text.RegularExpressions;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TenantBranding;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory tenant branding service that manages draft/published lifecycle,
    /// server-side validation, domain configuration, and audit history.
    /// </summary>
    public class TenantBrandingService : ITenantBrandingService
    {
        // ── Per-tenant stores (keyed by tenantId) ─────────────────────────────────
        private readonly Dictionary<string, TenantBrandingConfig> _drafts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TenantBrandingConfig> _published = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TenantDomainRecord>> _domains = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TenantBrandingHistoryEntry>> _history = new(StringComparer.OrdinalIgnoreCase);
        private readonly Lock _lock = new();
        private readonly ILogger<TenantBrandingService> _logger;

        // ── Validation constants ──────────────────────────────────────────────────
        private const int MaxOrganizationNameLength = 100;
        private const int MaxProductLabelLength = 60;
        private const int MaxDomainLength = 253;

        /// <summary>Regex matching 3-character (#RGB) or 6-character (#RRGGBB) hex CSS color tokens.</summary>
        private static readonly Regex HexColorRegex = new(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled);

        /// <summary>Regex for basic email validation.</summary>
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of <see cref="TenantBrandingService"/>.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public TenantBrandingService(ILogger<TenantBrandingService> logger)
        {
            _logger = logger;
        }

        // ── Draft Management ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<TenantBrandingResponse> GetDraftAsync(string tenantId, string actorId)
        {
            lock (_lock)
            {
                if (_drafts.TryGetValue(tenantId, out var draft))
                {
                    return Task.FromResult(new TenantBrandingResponse { Success = true, Branding = Clone(draft) });
                }
            }

            // Return a NotConfigured shell so the caller can distinguish "no draft" from an error
            return Task.FromResult(new TenantBrandingResponse
            {
                Success = true,
                Branding = new TenantBrandingConfig
                {
                    TenantId = tenantId,
                    Status = TenantBrandingLifecycleStatus.NotConfigured
                }
            });
        }

        /// <inheritdoc/>
        public Task<TenantBrandingResponse> UpdateDraftAsync(UpdateTenantBrandingDraftRequest request, string tenantId, string actorId)
        {
            TenantBrandingConfig draft;
            int newVersion;

            lock (_lock)
            {
                if (!_drafts.TryGetValue(tenantId, out var existing))
                {
                    existing = new TenantBrandingConfig
                    {
                        TenantId = tenantId,
                        CreatedBy = actorId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        Version = 0
                    };
                }

                // Merge request fields into the existing draft
                if (request.OrganizationName != null)
                    existing.OrganizationName = request.OrganizationName;
                if (request.ProductLabel != null)
                    existing.ProductLabel = request.ProductLabel;
                if (request.LogoUrl != null)
                    existing.LogoUrl = request.LogoUrl;
                if (request.FaviconUrl != null)
                    existing.FaviconUrl = request.FaviconUrl;

                if (request.Theme != null)
                {
                    if (request.Theme.PrimaryColor != null)
                        existing.Theme.PrimaryColor = request.Theme.PrimaryColor;
                    if (request.Theme.SecondaryColor != null)
                        existing.Theme.SecondaryColor = request.Theme.SecondaryColor;
                    if (request.Theme.AccentColor != null)
                        existing.Theme.AccentColor = request.Theme.AccentColor;
                    if (request.Theme.BackgroundColor != null)
                        existing.Theme.BackgroundColor = request.Theme.BackgroundColor;
                    if (request.Theme.TextColor != null)
                        existing.Theme.TextColor = request.Theme.TextColor;
                }

                if (request.Support != null)
                {
                    if (request.Support.SupportEmail != null)
                        existing.Support.SupportEmail = request.Support.SupportEmail;
                    if (request.Support.SupportUrl != null)
                        existing.Support.SupportUrl = request.Support.SupportUrl;
                    if (request.Support.LegalContactEmail != null)
                        existing.Support.LegalContactEmail = request.Support.LegalContactEmail;
                    if (request.Support.LegalContactUrl != null)
                        existing.Support.LegalContactUrl = request.Support.LegalContactUrl;
                }

                // Bump version and metadata
                existing.Version += 1;
                existing.UpdatedBy = actorId;
                existing.UpdatedAt = DateTimeOffset.UtcNow;

                // Re-evaluate validation errors
                existing.ValidationErrors = ValidateConfig(existing);
                existing.Status = existing.ValidationErrors.Count == 0
                    ? TenantBrandingLifecycleStatus.Draft
                    : TenantBrandingLifecycleStatus.Invalid;

                newVersion = existing.Version;
                _drafts[tenantId] = existing;

                AppendHistory(tenantId, newVersion, "DraftSaved", actorId,
                    $"Draft v{newVersion} saved. ValidationErrors={existing.ValidationErrors.Count}");

                draft = Clone(existing);
            }

            _logger.LogInformation(
                "TenantBranding draft saved for tenant {TenantId} v{Version} by {Actor}",
                LoggingHelper.SanitizeLogInput(tenantId),
                newVersion,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new TenantBrandingResponse { Success = true, Branding = draft });
        }

        // ── Validation ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<TenantBrandingValidationResponse> ValidateDraftAsync(string tenantId, string actorId)
        {
            lock (_lock)
            {
                if (!_drafts.TryGetValue(tenantId, out var draft))
                {
                    return Task.FromResult(new TenantBrandingValidationResponse
                    {
                        Success = true,
                        IsValid = false,
                        Errors = new List<TenantBrandingValidationError>
                        {
                            new() { Field = string.Empty, Code = "NO_DRAFT", Message = "No branding draft exists. Create a draft first." }
                        }
                    });
                }

                var errors = ValidateConfig(draft);
                // Persist the updated error list and status
                draft.ValidationErrors = errors;
                draft.Status = errors.Count == 0 ? TenantBrandingLifecycleStatus.Draft : TenantBrandingLifecycleStatus.Invalid;

                return Task.FromResult(new TenantBrandingValidationResponse
                {
                    Success = true,
                    IsValid = errors.Count == 0,
                    Errors = new List<TenantBrandingValidationError>(errors)
                });
            }
        }

        // ── Publish Lifecycle ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<TenantBrandingResponse> PublishAsync(string tenantId, string actorId)
        {
            lock (_lock)
            {
                if (!_drafts.TryGetValue(tenantId, out var draft))
                {
                    return Task.FromResult(new TenantBrandingResponse
                    {
                        Success = false,
                        ErrorMessage = "No branding draft exists. Create a draft before publishing."
                    });
                }

                // Re-validate before publishing
                var errors = ValidateConfig(draft);
                if (errors.Count > 0)
                {
                    draft.ValidationErrors = errors;
                    draft.Status = TenantBrandingLifecycleStatus.Invalid;
                    return Task.FromResult(new TenantBrandingResponse
                    {
                        Success = false,
                        ErrorMessage = $"Draft has {errors.Count} validation error(s) and cannot be published.",
                        Branding = Clone(draft)
                    });
                }

                // Stamp publish metadata
                draft.PublishedBy = actorId;
                draft.PublishedAt = DateTimeOffset.UtcNow;
                draft.Status = TenantBrandingLifecycleStatus.Published;
                draft.ValidationErrors = new List<TenantBrandingValidationError>();

                // Promote draft → published (clone so both stores are independent)
                _published[tenantId] = Clone(draft);

                AppendHistory(tenantId, draft.Version, "Published", actorId,
                    $"Draft v{draft.Version} published by {actorId}.");

                _logger.LogInformation(
                    "TenantBranding published for tenant {TenantId} v{Version} by {Actor}",
                    LoggingHelper.SanitizeLogInput(tenantId),
                    draft.Version,
                    LoggingHelper.SanitizeLogInput(actorId));

                return Task.FromResult(new TenantBrandingResponse { Success = true, Branding = Clone(draft) });
            }
        }

        /// <inheritdoc/>
        public Task<TenantBrandingPublishedResponse> GetPublishedAsync(string tenantId)
        {
            lock (_lock)
            {
                if (_published.TryGetValue(tenantId, out var pub) && pub.Status == TenantBrandingLifecycleStatus.Published)
                {
                    return Task.FromResult(new TenantBrandingPublishedResponse
                    {
                        Success = true,
                        Payload = MapToPublishedPayload(pub, isFallback: false)
                    });
                }
            }

            // Safe fallback — platform defaults
            return Task.FromResult(new TenantBrandingPublishedResponse
            {
                Success = true,
                Payload = new TenantBrandingPublishedPayload
                {
                    TenantId = tenantId,
                    IsFallback = true,
                    Version = 0
                }
            });
        }

        // ── Status ────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<TenantBrandingStatusResponse> GetStatusAsync(string tenantId, string actorId)
        {
            lock (_lock)
            {
                _drafts.TryGetValue(tenantId, out var draft);
                _published.TryGetValue(tenantId, out var pub);

                bool hasDraft = draft != null;
                bool hasPublished = pub?.Status == TenantBrandingLifecycleStatus.Published;
                int errorCount = draft?.ValidationErrors.Count ?? 0;
                bool isDraftValid = hasDraft && errorCount == 0;

                TenantBrandingLifecycleStatus status;
                string description;

                if (!hasDraft && !hasPublished)
                {
                    status = TenantBrandingLifecycleStatus.NotConfigured;
                    description = "No branding configuration exists. Create a draft to get started.";
                }
                else if (hasDraft && draft!.Status == TenantBrandingLifecycleStatus.Invalid)
                {
                    status = TenantBrandingLifecycleStatus.Invalid;
                    description = $"Draft exists but has {errorCount} validation error(s). Fix errors before publishing.";
                }
                else if (hasPublished && (!hasDraft || draft!.Status == TenantBrandingLifecycleStatus.Published))
                {
                    status = TenantBrandingLifecycleStatus.Published;
                    description = $"Branding is live (published v{pub!.Version}).";
                }
                else
                {
                    status = TenantBrandingLifecycleStatus.Draft;
                    description = "Draft is valid and ready to publish.";
                }

                return Task.FromResult(new TenantBrandingStatusResponse
                {
                    Success = true,
                    Status = status,
                    HasDraft = hasDraft,
                    HasPublished = hasPublished,
                    IsDraftValid = isDraftValid,
                    ValidationErrorCount = errorCount,
                    PublishedVersion = hasPublished ? pub!.Version : null,
                    LastPublishedAt = hasPublished ? pub!.PublishedAt : null,
                    StatusDescription = description
                });
            }
        }

        // ── Domain Configuration ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<TenantDomainListResponse> GetDomainsAsync(string tenantId, string actorId)
        {
            lock (_lock)
            {
                _domains.TryGetValue(tenantId, out var list);
                return Task.FromResult(new TenantDomainListResponse
                {
                    Success = true,
                    Domains = list != null
                        ? new List<TenantDomainRecord>(list.Select(CloneDomain))
                        : new List<TenantDomainRecord>()
                });
            }
        }

        /// <inheritdoc/>
        public Task<TenantDomainResponse> UpsertDomainAsync(UpsertTenantDomainRequest request, string tenantId, string actorId)
        {
            if (string.IsNullOrWhiteSpace(request.Domain))
            {
                return Task.FromResult(new TenantDomainResponse
                {
                    Success = false,
                    ErrorMessage = "Domain name is required."
                });
            }

            if (request.Domain.Length > MaxDomainLength)
            {
                return Task.FromResult(new TenantDomainResponse
                {
                    Success = false,
                    ErrorMessage = $"Domain name exceeds maximum length of {MaxDomainLength} characters."
                });
            }

            // Basic domain format validation (no scheme, no path, no port)
            var normalizedDomain = request.Domain.Trim().ToLowerInvariant();
            if (!IsValidDomain(normalizedDomain))
            {
                return Task.FromResult(new TenantDomainResponse
                {
                    Success = false,
                    ErrorMessage = $"'{request.Domain}' is not a valid fully qualified domain name."
                });
            }

            lock (_lock)
            {
                if (!_domains.ContainsKey(tenantId))
                    _domains[tenantId] = new List<TenantDomainRecord>();

                var list = _domains[tenantId];
                var existing = list.FirstOrDefault(d => d.Domain.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Update notes only; preserve verification state
                    existing.Notes = request.Notes;
                    existing.UpdatedBy = actorId;

                    _logger.LogInformation(
                        "TenantDomain updated for tenant {TenantId} domain {Domain} by {Actor}",
                        LoggingHelper.SanitizeLogInput(tenantId),
                        LoggingHelper.SanitizeLogInput(normalizedDomain),
                        LoggingHelper.SanitizeLogInput(actorId));

                    return Task.FromResult(new TenantDomainResponse { Success = true, Domain = CloneDomain(existing) });
                }

                // Create new domain record
                var record = new TenantDomainRecord
                {
                    DomainId = Guid.NewGuid().ToString(),
                    Domain = normalizedDomain,
                    TenantId = tenantId,
                    Status = TenantDomainReadinessStatus.Pending,
                    VerificationToken = GenerateVerificationToken(tenantId, normalizedDomain),
                    Notes = request.Notes,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedBy = actorId
                };

                list.Add(record);

                _logger.LogInformation(
                    "TenantDomain created for tenant {TenantId} domain {Domain} by {Actor}",
                    LoggingHelper.SanitizeLogInput(tenantId),
                    LoggingHelper.SanitizeLogInput(normalizedDomain),
                    LoggingHelper.SanitizeLogInput(actorId));

                return Task.FromResult(new TenantDomainResponse { Success = true, Domain = CloneDomain(record) });
            }
        }

        // ── Audit History ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<TenantBrandingHistoryResponse> GetHistoryAsync(string tenantId, string actorId)
        {
            lock (_lock)
            {
                _history.TryGetValue(tenantId, out var list);
                return Task.FromResult(new TenantBrandingHistoryResponse
                {
                    Success = true,
                    History = list != null
                        ? new List<TenantBrandingHistoryEntry>(
                            list.OrderByDescending(e => e.OccurredAt))
                        : new List<TenantBrandingHistoryEntry>()
                });
            }
        }

        // ── Internal Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Validates a branding configuration and returns a list of errors.
        /// An empty list means the configuration is valid and publishable.
        /// </summary>
        private static List<TenantBrandingValidationError> ValidateConfig(TenantBrandingConfig config)
        {
            var errors = new List<TenantBrandingValidationError>();

            // OrganizationName is required for publishing
            if (string.IsNullOrWhiteSpace(config.OrganizationName))
            {
                errors.Add(new TenantBrandingValidationError
                {
                    Field = nameof(TenantBrandingConfig.OrganizationName),
                    Code = "REQUIRED_FIELD_MISSING",
                    Message = "OrganizationName is required. Provide the organisation's legal or display name."
                });
            }
            else if (config.OrganizationName.Length > MaxOrganizationNameLength)
            {
                errors.Add(new TenantBrandingValidationError
                {
                    Field = nameof(TenantBrandingConfig.OrganizationName),
                    Code = "FIELD_TOO_LONG",
                    Message = $"OrganizationName must not exceed {MaxOrganizationNameLength} characters."
                });
            }

            if (config.ProductLabel != null && config.ProductLabel.Length > MaxProductLabelLength)
            {
                errors.Add(new TenantBrandingValidationError
                {
                    Field = nameof(TenantBrandingConfig.ProductLabel),
                    Code = "FIELD_TOO_LONG",
                    Message = $"ProductLabel must not exceed {MaxProductLabelLength} characters."
                });
            }

            // URL fields must be well-formed HTTPS if provided
            ValidateUrl(config.LogoUrl, nameof(TenantBrandingConfig.LogoUrl), errors);
            ValidateUrl(config.FaviconUrl, nameof(TenantBrandingConfig.FaviconUrl), errors);

            // Color tokens must match CSS hex format
            ValidateColor(config.Theme.PrimaryColor, "Theme.PrimaryColor", errors);
            ValidateColor(config.Theme.SecondaryColor, "Theme.SecondaryColor", errors);
            ValidateColor(config.Theme.AccentColor, "Theme.AccentColor", errors);
            ValidateColor(config.Theme.BackgroundColor, "Theme.BackgroundColor", errors);
            ValidateColor(config.Theme.TextColor, "Theme.TextColor", errors);

            // Support metadata
            ValidateEmail(config.Support.SupportEmail, "Support.SupportEmail", errors);
            ValidateEmail(config.Support.LegalContactEmail, "Support.LegalContactEmail", errors);
            ValidateUrl(config.Support.SupportUrl, "Support.SupportUrl", errors);
            ValidateUrl(config.Support.LegalContactUrl, "Support.LegalContactUrl", errors);

            return errors;
        }

        private static void ValidateColor(string? value, string field, List<TenantBrandingValidationError> errors)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!HexColorRegex.IsMatch(value))
            {
                errors.Add(new TenantBrandingValidationError
                {
                    Field = field,
                    Code = "INVALID_COLOR_FORMAT",
                    Message = $"'{value}' is not a valid CSS hex color token. Use #RGB or #RRGGBB format."
                });
            }
        }

        private static void ValidateUrl(string? value, string field, List<TenantBrandingValidationError> errors)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
            {
                errors.Add(new TenantBrandingValidationError
                {
                    Field = field,
                    Code = "INVALID_URL_FORMAT",
                    Message = $"'{value}' is not a valid URL. Use a fully qualified HTTP or HTTPS URL."
                });
            }
        }

        private static void ValidateEmail(string? value, string field, List<TenantBrandingValidationError> errors)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!EmailRegex.IsMatch(value))
            {
                errors.Add(new TenantBrandingValidationError
                {
                    Field = field,
                    Code = "INVALID_EMAIL_FORMAT",
                    Message = $"'{value}' is not a valid email address."
                });
            }
        }

        private static bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return false;
            // Must not contain scheme, path, query, or spaces
            if (domain.Contains('/') || domain.Contains(' ') || domain.Contains(':')) return false;
            // Must have at least one dot (not starting or ending with it)
            if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.')) return false;
            return true;
        }

        private static string GenerateVerificationToken(string tenantId, string domain)
        {
            // Deterministic token based on tenant + domain + a fixed salt; stable across restarts
            var raw = $"biatec-verify-{tenantId}-{domain}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant()[..32];
        }

        private void AppendHistory(string tenantId, int version, string eventType, string actor, string? description)
        {
            if (!_history.ContainsKey(tenantId))
                _history[tenantId] = new List<TenantBrandingHistoryEntry>();

            _history[tenantId].Add(new TenantBrandingHistoryEntry
            {
                EntryId = Guid.NewGuid().ToString(),
                Version = version,
                EventType = eventType,
                Actor = actor,
                OccurredAt = DateTimeOffset.UtcNow,
                Description = description
            });
        }

        private static TenantBrandingPublishedPayload MapToPublishedPayload(TenantBrandingConfig source, bool isFallback)
        {
            return new TenantBrandingPublishedPayload
            {
                TenantId = source.TenantId,
                OrganizationName = source.OrganizationName,
                ProductLabel = source.ProductLabel,
                LogoUrl = source.LogoUrl,
                FaviconUrl = source.FaviconUrl,
                Theme = new TenantThemeTokens
                {
                    PrimaryColor = source.Theme.PrimaryColor,
                    SecondaryColor = source.Theme.SecondaryColor,
                    AccentColor = source.Theme.AccentColor,
                    BackgroundColor = source.Theme.BackgroundColor,
                    TextColor = source.Theme.TextColor
                },
                Support = new TenantSupportMetadata
                {
                    SupportEmail = source.Support.SupportEmail,
                    SupportUrl = source.Support.SupportUrl,
                    LegalContactEmail = source.Support.LegalContactEmail,
                    LegalContactUrl = source.Support.LegalContactUrl
                },
                Version = source.Version,
                PublishedAt = source.PublishedAt,
                IsFallback = isFallback
            };
        }

        /// <summary>Shallow-clone a branding config to prevent aliasing between stores.</summary>
        private static TenantBrandingConfig Clone(TenantBrandingConfig src)
        {
            return new TenantBrandingConfig
            {
                TenantId = src.TenantId,
                OrganizationName = src.OrganizationName,
                ProductLabel = src.ProductLabel,
                LogoUrl = src.LogoUrl,
                FaviconUrl = src.FaviconUrl,
                Theme = new TenantThemeTokens
                {
                    PrimaryColor = src.Theme.PrimaryColor,
                    SecondaryColor = src.Theme.SecondaryColor,
                    AccentColor = src.Theme.AccentColor,
                    BackgroundColor = src.Theme.BackgroundColor,
                    TextColor = src.Theme.TextColor
                },
                Support = new TenantSupportMetadata
                {
                    SupportEmail = src.Support.SupportEmail,
                    SupportUrl = src.Support.SupportUrl,
                    LegalContactEmail = src.Support.LegalContactEmail,
                    LegalContactUrl = src.Support.LegalContactUrl
                },
                Status = src.Status,
                Version = src.Version,
                CreatedBy = src.CreatedBy,
                CreatedAt = src.CreatedAt,
                UpdatedBy = src.UpdatedBy,
                UpdatedAt = src.UpdatedAt,
                PublishedBy = src.PublishedBy,
                PublishedAt = src.PublishedAt,
                ValidationErrors = new List<TenantBrandingValidationError>(src.ValidationErrors)
            };
        }

        private static TenantDomainRecord CloneDomain(TenantDomainRecord src)
        {
            return new TenantDomainRecord
            {
                DomainId = src.DomainId,
                Domain = src.Domain,
                TenantId = src.TenantId,
                Status = src.Status,
                VerificationToken = src.VerificationToken,
                Notes = src.Notes,
                CreatedAt = src.CreatedAt,
                VerifiedAt = src.VerifiedAt,
                UpdatedBy = src.UpdatedBy
            };
        }
    }
}

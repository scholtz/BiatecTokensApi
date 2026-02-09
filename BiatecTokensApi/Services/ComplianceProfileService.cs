using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceProfile;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing compliance profiles and audit logging
    /// </summary>
    public class ComplianceProfileService : IComplianceProfileService
    {
        private readonly IComplianceProfileRepository _repository;
        private readonly ILogger<ComplianceProfileService> _logger;
        private readonly ConcurrentBag<ComplianceProfileAuditEntry> _auditLog = new();

        // ISO 3166-1 alpha-2 country codes - commonly used jurisdictions
        private static readonly HashSet<string> _validJurisdictions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Major financial centers and RWA jurisdictions
            "US", "GB", "DE", "FR", "CH", "SG", "HK", "JP", "CA", "AU",
            "NL", "LU", "IE", "AT", "BE", "DK", "FI", "NO", "SE", "ES",
            "IT", "PT", "GR", "PL", "CZ", "HU", "RO", "BG", "HR", "SI",
            "SK", "EE", "LV", "LT", "MT", "CY", "IS", "LI", "MC", "SM",
            "VA", "AD", "GI", "IM", "JE", "GG", "FO", "AX", "PM", "GL",
            "BM", "KY", "VG", "TC", "AI", "MS", "BZ", "PA", "CR", "BS",
            "BB", "JM", "TT", "GD", "LC", "VC", "AG", "DM", "KN", "MX",
            "BR", "AR", "CL", "CO", "PE", "VE", "UY", "PY", "BO", "EC",
            "GY", "SR", "GF", "FK", "AE", "SA", "QA", "KW", "BH", "OM",
            "IL", "JO", "LB", "TR", "EG", "ZA", "KE", "NG", "GH", "MA",
            "TN", "DZ", "LY", "SD", "ET", "TZ", "UG", "ZW", "BW", "NA",
            "MZ", "ZM", "MW", "AO", "CD", "CG", "GA", "CM", "CI", "SN",
            "ML", "NE", "BF", "TG", "BJ", "GN", "SL", "LR", "GM", "GW",
            "MR", "ST", "CV", "KM", "SC", "MU", "MG", "RE", "YT", "MV",
            "IN", "PK", "BD", "LK", "NP", "BT", "AF", "IR", "IQ", "SY",
            "YE", "KZ", "UZ", "TM", "TJ", "KG", "MN", "CN", "KR", "TW",
            "MY", "TH", "VN", "PH", "ID", "LA", "KH", "MM", "BN", "NZ",
            "FJ", "PG", "NC", "PF", "WF", "WS", "TO", "VU", "SB", "TV",
            "NR", "KI", "FM", "MH", "PW", "MP", "GU", "AS", "NU", "CK",
            "TK", "PN", "SH", "TA", "AC"
        };

        // Valid issuance intent categories
        private static readonly HashSet<string> _validIssuanceIntents = new(StringComparer.OrdinalIgnoreCase)
        {
            "equity",
            "debt",
            "asset-backed",
            "real-estate",
            "commodity",
            "fund",
            "utility",
            "governance",
            "revenue-share",
            "hybrid",
            "other"
        };

        public ComplianceProfileService(
            IComplianceProfileRepository repository,
            ILogger<ComplianceProfileService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<ComplianceProfileResponse> UpsertProfileAsync(
            UpsertComplianceProfileRequest request,
            string userId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            // Validate jurisdiction
            if (!IsValidJurisdiction(request.Jurisdiction))
            {
                _logger.LogWarning("Invalid jurisdiction code provided: {Jurisdiction}",
                    LoggingHelper.SanitizeLogInput(request.Jurisdiction));

                return new ComplianceProfileResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_JURISDICTION,
                    ErrorMessage = $"Invalid jurisdiction code: {request.Jurisdiction}. Must be a valid ISO 3166-1 alpha-2 country code."
                };
            }

            // Validate issuance intent
            if (!IsValidIssuanceIntent(request.IssuanceIntent))
            {
                _logger.LogWarning("Invalid issuance intent provided: {IssuanceIntent}",
                    LoggingHelper.SanitizeLogInput(request.IssuanceIntent));

                return new ComplianceProfileResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_ISSUANCE_INTENT,
                    ErrorMessage = $"Invalid issuance intent: {request.IssuanceIntent}. Must be one of: {string.Join(", ", _validIssuanceIntents)}"
                };
            }

            try
            {
                var existingProfile = await _repository.GetProfileByUserIdAsync(userId);
                ComplianceProfile profile;
                string action;
                Dictionary<string, string> changedFields = new();
                Dictionary<string, string> previousValues = new();

                if (existingProfile == null)
                {
                    // Create new profile
                    profile = new ComplianceProfile
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        IssuingEntityName = request.IssuingEntityName,
                        Jurisdiction = request.Jurisdiction.ToUpperInvariant(),
                        IssuanceIntent = request.IssuanceIntent.ToLowerInvariant(),
                        ReadinessStatus = request.ReadinessStatus,
                        Metadata = request.Metadata ?? new Dictionary<string, string>(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId
                    };

                    await _repository.CreateProfileAsync(profile);
                    action = "Created";

                    // Record all fields for creation
                    changedFields["IssuingEntityName"] = profile.IssuingEntityName;
                    changedFields["Jurisdiction"] = profile.Jurisdiction;
                    changedFields["IssuanceIntent"] = profile.IssuanceIntent;
                    changedFields["ReadinessStatus"] = profile.ReadinessStatus.ToString();

                    _logger.LogInformation("Created compliance profile for user {UserId}", 
                        LoggingHelper.SanitizeLogInput(userId));
                }
                else
                {
                    // Update existing profile
                    action = "Updated";

                    // Track changes
                    if (existingProfile.IssuingEntityName != request.IssuingEntityName)
                    {
                        previousValues["IssuingEntityName"] = existingProfile.IssuingEntityName;
                        changedFields["IssuingEntityName"] = request.IssuingEntityName;
                        existingProfile.IssuingEntityName = request.IssuingEntityName;
                    }

                    var normalizedJurisdiction = request.Jurisdiction.ToUpperInvariant();
                    if (existingProfile.Jurisdiction != normalizedJurisdiction)
                    {
                        previousValues["Jurisdiction"] = existingProfile.Jurisdiction;
                        changedFields["Jurisdiction"] = normalizedJurisdiction;
                        existingProfile.Jurisdiction = normalizedJurisdiction;
                    }

                    var normalizedIntent = request.IssuanceIntent.ToLowerInvariant();
                    if (existingProfile.IssuanceIntent != normalizedIntent)
                    {
                        previousValues["IssuanceIntent"] = existingProfile.IssuanceIntent;
                        changedFields["IssuanceIntent"] = normalizedIntent;
                        existingProfile.IssuanceIntent = normalizedIntent;
                    }

                    if (existingProfile.ReadinessStatus != request.ReadinessStatus)
                    {
                        previousValues["ReadinessStatus"] = existingProfile.ReadinessStatus.ToString();
                        changedFields["ReadinessStatus"] = request.ReadinessStatus.ToString();
                        existingProfile.ReadinessStatus = request.ReadinessStatus;
                    }

                    if (request.Metadata != null)
                    {
                        existingProfile.Metadata = request.Metadata;
                    }

                    existingProfile.UpdatedBy = userId;

                    await _repository.UpdateProfileAsync(existingProfile);
                    profile = existingProfile;

                    _logger.LogInformation("Updated compliance profile for user {UserId}, Changes: {ChangeCount}",
                        LoggingHelper.SanitizeLogInput(userId),
                        changedFields.Count);
                }

                // Create audit log entry
                var auditEntry = new ComplianceProfileAuditEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    ProfileId = profile.Id,
                    UserId = userId,
                    Action = action,
                    Timestamp = DateTime.UtcNow,
                    PerformedBy = userId,
                    ChangedFields = changedFields,
                    PreviousValues = previousValues,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                _auditLog.Add(auditEntry);

                return new ComplianceProfileResponse
                {
                    Success = true,
                    Profile = profile,
                    IsOnboardingComplete = profile.IsOnboardingComplete()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting compliance profile for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return new ComplianceProfileResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while saving the compliance profile"
                };
            }
        }

        public async Task<ComplianceProfileResponse> GetProfileAsync(string userId)
        {
            try
            {
                var profile = await _repository.GetProfileByUserIdAsync(userId);

                if (profile == null)
                {
                    return new ComplianceProfileResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.COMPLIANCE_PROFILE_NOT_FOUND,
                        ErrorMessage = "Compliance profile not found for this account"
                    };
                }

                return new ComplianceProfileResponse
                {
                    Success = true,
                    Profile = profile,
                    IsOnboardingComplete = profile.IsOnboardingComplete()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving compliance profile for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                return new ComplianceProfileResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving the compliance profile"
                };
            }
        }

        public Task<List<ComplianceProfileAuditEntry>> GetAuditLogAsync(
            string userId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var entries = _auditLog
                .Where(e => e.UserId == userId)
                .Where(e => !startDate.HasValue || e.Timestamp >= startDate.Value)
                .Where(e => !endDate.HasValue || e.Timestamp <= endDate.Value)
                .OrderByDescending(e => e.Timestamp)
                .ToList();

            _logger.LogInformation("Retrieved {Count} audit log entries for user {UserId}",
                entries.Count,
                LoggingHelper.SanitizeLogInput(userId));

            return Task.FromResult(entries);
        }

        public async Task<string> ExportAuditLogJsonAsync(
            string userId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var entries = await GetAuditLogAsync(userId, startDate, endDate);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(entries, options);
        }

        public async Task<string> ExportAuditLogCsvAsync(
            string userId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var entries = await GetAuditLogAsync(userId, startDate, endDate);

            var csv = new StringBuilder();
            csv.AppendLine("Id,ProfileId,UserId,Action,Timestamp,PerformedBy,ChangedFields,PreviousValues,IpAddress,UserAgent");

            foreach (var entry in entries)
            {
                var changedFieldsJson = JsonSerializer.Serialize(entry.ChangedFields);
                var previousValuesJson = JsonSerializer.Serialize(entry.PreviousValues);

                csv.AppendLine($"\"{entry.Id}\",\"{entry.ProfileId}\",\"{entry.UserId}\",\"{entry.Action}\"," +
                             $"\"{entry.Timestamp:O}\",\"{entry.PerformedBy}\"," +
                             $"\"{changedFieldsJson.Replace("\"", "\"\"")}\",\"{previousValuesJson.Replace("\"", "\"\"")}\"," +
                             $"\"{entry.IpAddress ?? ""}\",\"{entry.UserAgent ?? ""}\"");
            }

            return csv.ToString();
        }

        public bool IsValidJurisdiction(string jurisdiction)
        {
            if (string.IsNullOrWhiteSpace(jurisdiction)) return false;
            return _validJurisdictions.Contains(jurisdiction.ToUpperInvariant());
        }

        public bool IsValidIssuanceIntent(string issuanceIntent)
        {
            if (string.IsNullOrWhiteSpace(issuanceIntent)) return false;
            return _validIssuanceIntents.Contains(issuanceIntent.ToLowerInvariant());
        }
    }
}

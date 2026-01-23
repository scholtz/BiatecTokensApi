namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Response for compliance metadata operations
    /// </summary>
    public class ComplianceMetadataResponse : BaseResponse
    {
        /// <summary>
        /// The compliance metadata that was created, retrieved, or modified
        /// </summary>
        public ComplianceMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Response for listing compliance metadata
    /// </summary>
    public class ComplianceMetadataListResponse : BaseResponse
    {
        /// <summary>
        /// List of compliance metadata entries
        /// </summary>
        public List<ComplianceMetadata> Metadata { get; set; } = new();

        /// <summary>
        /// Total number of entries matching the filter
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Response for token preset validation
    /// </summary>
    public class ValidateTokenPresetResponse : BaseResponse
    {
        /// <summary>
        /// Whether the token configuration is valid for MICA/RWA compliance
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors that must be fixed
        /// </summary>
        public List<ValidationIssue> Errors { get; set; } = new();

        /// <summary>
        /// List of validation warnings that should be reviewed
        /// </summary>
        public List<ValidationIssue> Warnings { get; set; } = new();

        /// <summary>
        /// Summary of validation results
        /// </summary>
        public string? Summary { get; set; }
    }

    /// <summary>
    /// Represents a validation issue (error or warning)
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>
        /// Severity of the issue
        /// </summary>
        public ValidationSeverity Severity { get; set; }

        /// <summary>
        /// Field or area that has the issue
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Suggested action to resolve the issue
        /// </summary>
        public string? Recommendation { get; set; }

        /// <summary>
        /// Applicable regulatory framework or standard
        /// </summary>
        public string? RegulatoryContext { get; set; }
    }

    /// <summary>
    /// Severity level for validation issues
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Critical error that must be fixed
        /// </summary>
        Error,

        /// <summary>
        /// Warning that should be reviewed
        /// </summary>
        Warning,

        /// <summary>
        /// Informational message
        /// </summary>
        Info
    }

    /// <summary>
    /// Response for issuer profile operations
    /// </summary>
    public class IssuerProfileResponse : BaseResponse
    {
        /// <summary>
        /// The issuer profile
        /// </summary>
        public IssuerProfile? Profile { get; set; }
    }

    /// <summary>
    /// Response for issuer verification status
    /// </summary>
    public class IssuerVerificationResponse : BaseResponse
    {
        /// <summary>
        /// Issuer address
        /// </summary>
        public string IssuerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Overall verification status
        /// </summary>
        public IssuerVerificationStatus OverallStatus { get; set; }

        /// <summary>
        /// KYB verification status
        /// </summary>
        public VerificationStatus KybStatus { get; set; }

        /// <summary>
        /// MICA license status
        /// </summary>
        public MicaLicenseStatus MicaLicenseStatus { get; set; }

        /// <summary>
        /// Profile status
        /// </summary>
        public IssuerProfileStatus ProfileStatus { get; set; }

        /// <summary>
        /// Whether profile is complete
        /// </summary>
        public bool IsProfileComplete { get; set; }

        /// <summary>
        /// List of missing required fields
        /// </summary>
        public List<string> MissingFields { get; set; } = new();

        /// <summary>
        /// Verification score (0-100)
        /// </summary>
        public int VerificationScore { get; set; }
    }

    /// <summary>
    /// Issuer verification status
    /// </summary>
    public enum IssuerVerificationStatus
    {
        /// <summary>
        /// Unverified
        /// </summary>
        Unverified,

        /// <summary>
        /// Pending verification
        /// </summary>
        Pending,

        /// <summary>
        /// Partially verified
        /// </summary>
        PartiallyVerified,

        /// <summary>
        /// Fully verified
        /// </summary>
        FullyVerified,

        /// <summary>
        /// Expired
        /// </summary>
        Expired
    }

    /// <summary>
    /// Response for issuer assets list
    /// </summary>
    public class IssuerAssetsResponse : BaseResponse
    {
        /// <summary>
        /// Issuer address
        /// </summary>
        public string IssuerAddress { get; set; } = string.Empty;

        /// <summary>
        /// List of asset IDs
        /// </summary>
        public List<ulong> AssetIds { get; set; } = new();

        /// <summary>
        /// Total count
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Response for blacklist operations
    /// </summary>
    public class BlacklistResponse : BaseResponse
    {
        /// <summary>
        /// The blacklist entry
        /// </summary>
        public BlacklistEntry? Entry { get; set; }
    }

    /// <summary>
    /// Response for blacklist check
    /// </summary>
    public class BlacklistCheckResponse : BaseResponse
    {
        /// <summary>
        /// Address checked
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Whether address is blacklisted
        /// </summary>
        public bool IsBlacklisted { get; set; }

        /// <summary>
        /// Blacklist entries for this address
        /// </summary>
        public List<BlacklistEntry> Entries { get; set; } = new();

        /// <summary>
        /// Whether on global blacklist
        /// </summary>
        public bool GlobalBlacklist { get; set; }

        /// <summary>
        /// Whether on asset-specific blacklist
        /// </summary>
        public bool AssetSpecificBlacklist { get; set; }
    }

    /// <summary>
    /// Response for blacklist list
    /// </summary>
    public class BlacklistListResponse : BaseResponse
    {
        /// <summary>
        /// List of blacklist entries
        /// </summary>
        public List<BlacklistEntry> Entries { get; set; } = new();

        /// <summary>
        /// Total count
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Response for transfer validation
    /// </summary>
    public class TransferValidationResponse : BaseResponse
    {
        /// <summary>
        /// Whether transfer is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether transfer can proceed
        /// </summary>
        public bool CanTransfer { get; set; }

        /// <summary>
        /// List of validation checks
        /// </summary>
        public List<ValidationCheck> Validations { get; set; } = new();

        /// <summary>
        /// List of violations
        /// </summary>
        public List<string> Violations { get; set; } = new();

        /// <summary>
        /// List of warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// List of recommendations
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Individual validation check
    /// </summary>
    public class ValidationCheck
    {
        /// <summary>
        /// Rule name
        /// </summary>
        public string Rule { get; set; } = string.Empty;

        /// <summary>
        /// Whether check passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Check message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for MICA compliance checklist
    /// </summary>
    public class MicaComplianceChecklistResponse : BaseResponse
    {
        /// <summary>
        /// The MICA compliance checklist
        /// </summary>
        public MicaComplianceChecklist? Checklist { get; set; }
    }

    /// <summary>
    /// Response for compliance health
    /// </summary>
    public class ComplianceHealthResponse : BaseResponse
    {
        /// <summary>
        /// Overall health score (0-100)
        /// </summary>
        public int OverallHealthScore { get; set; }

        /// <summary>
        /// Total tokens
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Compliant tokens count
        /// </summary>
        public int CompliantTokens { get; set; }

        /// <summary>
        /// Under review tokens count
        /// </summary>
        public int UnderReviewTokens { get; set; }

        /// <summary>
        /// Non-compliant tokens count
        /// </summary>
        public int NonCompliantTokens { get; set; }

        /// <summary>
        /// MICA-ready tokens count
        /// </summary>
        public int MicaReadyTokens { get; set; }

        /// <summary>
        /// Tokens with whitelisting
        /// </summary>
        public int TokensWithWhitelisting { get; set; }

        /// <summary>
        /// Tokens with audit trail
        /// </summary>
        public int TokensWithAuditTrail { get; set; }

        /// <summary>
        /// Whether issuer is verified
        /// </summary>
        public bool IssuerVerified { get; set; }

        /// <summary>
        /// List of alerts
        /// </summary>
        public List<ComplianceAlert> Alerts { get; set; } = new();

        /// <summary>
        /// List of recommendations
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Compliance alert
    /// </summary>
    public class ComplianceAlert
    {
        /// <summary>
        /// Alert severity
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Alert message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Affected asset IDs
        /// </summary>
        public List<ulong> AffectedAssetIds { get; set; } = new();
    }
}

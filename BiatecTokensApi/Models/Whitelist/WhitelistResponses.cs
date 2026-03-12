namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Compliance overview response for an asset's whitelist state, suitable for compliance
    /// monitoring dashboards. Provides a machine-readable summary of investor eligibility
    /// metrics, enforcement statistics, and MICA readiness indicators.
    /// </summary>
    public class WhitelistComplianceOverviewResponse : BaseResponse
    {
        /// <summary>
        /// The asset ID for which the compliance overview was generated
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Timestamp when this overview was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Investor eligibility summary
        /// </summary>
        public InvestorEligibilitySummary? InvestorEligibility { get; set; }

        /// <summary>
        /// Transfer enforcement summary
        /// </summary>
        public TransferEnforcementSummary? TransferEnforcement { get; set; }

        /// <summary>
        /// KYC verification summary
        /// </summary>
        public KycVerificationSummary? KycVerification { get; set; }

        /// <summary>
        /// Audit trail summary
        /// </summary>
        public AuditTrailSummary? AuditTrail { get; set; }

        /// <summary>
        /// MICA readiness indicators (populated when network is specified)
        /// </summary>
        public MicaReadinessIndicators? MicaReadiness { get; set; }
    }

    /// <summary>
    /// Summary of investor eligibility across the whitelist for an asset
    /// </summary>
    public class InvestorEligibilitySummary
    {
        /// <summary>
        /// Total number of whitelist entries
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Number of active (approved) entries
        /// </summary>
        public int ActiveEntries { get; set; }

        /// <summary>
        /// Number of inactive (pending) entries
        /// </summary>
        public int InactiveEntries { get; set; }

        /// <summary>
        /// Number of revoked entries
        /// </summary>
        public int RevokedEntries { get; set; }

        /// <summary>
        /// Number of expired entries
        /// </summary>
        public int ExpiredEntries { get; set; }

        /// <summary>
        /// Percentage of entries that are active
        /// </summary>
        public double ActivePercentage => TotalEntries > 0
            ? Math.Round(ActiveEntries * 100.0 / TotalEntries, 2)
            : 0.0;
    }

    /// <summary>
    /// Summary of transfer enforcement activity for an asset
    /// </summary>
    public class TransferEnforcementSummary
    {
        /// <summary>
        /// Total number of transfer validation events
        /// </summary>
        public int TotalValidations { get; set; }

        /// <summary>
        /// Number of allowed transfers
        /// </summary>
        public int AllowedTransfers { get; set; }

        /// <summary>
        /// Number of denied transfers
        /// </summary>
        public int DeniedTransfers { get; set; }

        /// <summary>
        /// Percentage of transfers that were allowed
        /// </summary>
        public double AllowedPercentage => TotalValidations > 0
            ? Math.Round(AllowedTransfers * 100.0 / TotalValidations, 2)
            : 0.0;

        /// <summary>
        /// Most recent validation timestamp
        /// </summary>
        public DateTime? LastValidationAt { get; set; }
    }

    /// <summary>
    /// Summary of KYC verification across whitelist entries
    /// </summary>
    public class KycVerificationSummary
    {
        /// <summary>
        /// Total number of entries with KYC verification
        /// </summary>
        public int KycVerifiedEntries { get; set; }

        /// <summary>
        /// Total number of active entries without KYC verification
        /// </summary>
        public int ActiveWithoutKyc { get; set; }

        /// <summary>
        /// Number of distinct KYC providers in use
        /// </summary>
        public int KycProviderCount { get; set; }

        /// <summary>
        /// List of KYC providers in use (for audit/reporting)
        /// </summary>
        public List<string> KycProviders { get; set; } = new();
    }

    /// <summary>
    /// Summary of audit trail completeness and retention compliance
    /// </summary>
    public class AuditTrailSummary
    {
        /// <summary>
        /// Total number of audit log entries
        /// </summary>
        public int TotalAuditEntries { get; set; }

        /// <summary>
        /// Most recent audit entry timestamp
        /// </summary>
        public DateTime? LastAuditAt { get; set; }

        /// <summary>
        /// Earliest audit entry timestamp
        /// </summary>
        public DateTime? EarliestAuditAt { get; set; }

        /// <summary>
        /// Minimum retention years for audit records (MICA requirement: 7 years)
        /// </summary>
        public int MinimumRetentionYears { get; set; } = 7;

        /// <summary>
        /// Whether audit entries are immutable
        /// </summary>
        public bool ImmutableEntries { get; set; } = true;
    }

    /// <summary>
    /// MICA readiness indicators for regulated network compliance
    /// </summary>
    public class MicaReadinessIndicators
    {
        /// <summary>
        /// Whether all active entries have KYC verification
        /// </summary>
        public bool AllActiveEntriesKycVerified { get; set; }

        /// <summary>
        /// Whether audit trail retention meets MICA requirements (7+ years)
        /// </summary>
        public bool AuditRetentionCompliant { get; set; }

        /// <summary>
        /// Whether immutable audit entries are in place
        /// </summary>
        public bool ImmutableAuditCompliant { get; set; }

        /// <summary>
        /// Network-specific MICA requirements
        /// </summary>
        public string? ApplicableNetwork { get; set; }

        /// <summary>
        /// Overall MICA readiness score (0-100)
        /// </summary>
        public int ReadinessScore { get; set; }
    }

    /// <summary>
    /// Response for whitelist operations
    /// </summary>
    public class WhitelistResponse : BaseResponse
    {
        /// <summary>
        /// The whitelist entry that was created or modified
        /// </summary>
        public WhitelistEntry? Entry { get; set; }

        /// <summary>
        /// Number of entries affected (for bulk operations)
        /// </summary>
        public int? AffectedCount { get; set; }
    }

    /// <summary>
    /// Response for listing whitelist entries
    /// </summary>
    public class WhitelistListResponse : BaseResponse
    {
        /// <summary>
        /// List of whitelist entries
        /// </summary>
        public List<WhitelistEntry> Entries { get; set; } = new();

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
    /// Response for bulk whitelist operations
    /// </summary>
    public class BulkWhitelistResponse : BaseResponse
    {
        /// <summary>
        /// List of successfully added/updated entries
        /// </summary>
        public List<WhitelistEntry> SuccessfulEntries { get; set; } = new();

        /// <summary>
        /// List of addresses that failed validation
        /// </summary>
        public List<string> FailedAddresses { get; set; } = new();

        /// <summary>
        /// Number of entries successfully processed
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of entries that failed
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new();
    }

    /// <summary>
    /// Response for transfer validation
    /// </summary>
    public class ValidateTransferResponse : BaseResponse
    {
        /// <summary>
        /// Whether the transfer is allowed based on whitelist rules
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Reason why the transfer is not allowed (if IsAllowed is false)
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// Details about the sender's whitelist status
        /// </summary>
        public TransferParticipantStatus? SenderStatus { get; set; }

        /// <summary>
        /// Details about the receiver's whitelist status
        /// </summary>
        public TransferParticipantStatus? ReceiverStatus { get; set; }
    }

    /// <summary>
    /// Status information for a transfer participant (sender or receiver)
    /// </summary>
    public class TransferParticipantStatus
    {
        /// <summary>
        /// The Algorand address
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Whether the address is whitelisted
        /// </summary>
        public bool IsWhitelisted { get; set; }

        /// <summary>
        /// Whether the whitelist entry is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether the whitelist entry has expired
        /// </summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// The expiration date if applicable
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Current whitelist status
        /// </summary>
        public WhitelistStatus? Status { get; set; }
    }

    /// <summary>
    /// Response for allowlist status verification
    /// </summary>
    public class VerifyAllowlistStatusResponse : BaseResponse
    {
        /// <summary>
        /// The asset ID (token ID) being verified
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Allowlist status for the sender
        /// </summary>
        public AllowlistParticipantStatus? SenderStatus { get; set; }

        /// <summary>
        /// Allowlist status for the recipient
        /// </summary>
        public AllowlistParticipantStatus? RecipientStatus { get; set; }

        /// <summary>
        /// Overall transfer allowance status
        /// </summary>
        public AllowlistTransferStatus TransferStatus { get; set; }

        /// <summary>
        /// MICA compliance disclosures for the verification
        /// </summary>
        public MicaComplianceDisclosure? MicaDisclosure { get; set; }

        /// <summary>
        /// Audit metadata for the verification request
        /// </summary>
        public AllowlistAuditMetadata? AuditMetadata { get; set; }

        /// <summary>
        /// Cache duration in seconds for this response
        /// </summary>
        public int CacheDurationSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Allowlist status information for a transfer participant (sender or recipient)
    /// </summary>
    public class AllowlistParticipantStatus
    {
        /// <summary>
        /// The Algorand address
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Allowlist status (Approved/Pending/Expired/Denied)
        /// </summary>
        public AllowlistStatus Status { get; set; }

        /// <summary>
        /// Whether the address is whitelisted
        /// </summary>
        public bool IsWhitelisted { get; set; }

        /// <summary>
        /// Date when the allowlist entry was created
        /// </summary>
        public DateTime? ApprovedDate { get; set; }

        /// <summary>
        /// Date when the allowlist entry expires (if applicable)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Whether KYC verification has been completed
        /// </summary>
        public bool KycVerified { get; set; }

        /// <summary>
        /// Name of the KYC provider
        /// </summary>
        public string? KycProvider { get; set; }

        /// <summary>
        /// Additional status notes or denial reasons
        /// </summary>
        public string? StatusNotes { get; set; }
    }

    /// <summary>
    /// Allowlist status enumeration for regulated transfers
    /// </summary>
    public enum AllowlistStatus
    {
        /// <summary>
        /// Address is approved and can participate in transfers
        /// </summary>
        Approved,

        /// <summary>
        /// Address approval is pending (e.g., awaiting KYC completion)
        /// </summary>
        Pending,

        /// <summary>
        /// Address approval has expired
        /// </summary>
        Expired,

        /// <summary>
        /// Address has been denied or revoked
        /// </summary>
        Denied
    }

    /// <summary>
    /// Overall transfer status based on allowlist verification
    /// </summary>
    public enum AllowlistTransferStatus
    {
        /// <summary>
        /// Transfer is allowed - both parties are approved
        /// </summary>
        Allowed,

        /// <summary>
        /// Transfer is blocked due to sender not being approved
        /// </summary>
        BlockedSender,

        /// <summary>
        /// Transfer is blocked due to recipient not being approved
        /// </summary>
        BlockedRecipient,

        /// <summary>
        /// Transfer is blocked due to both parties not being approved
        /// </summary>
        BlockedBoth
    }

    /// <summary>
    /// MICA compliance disclosure information
    /// </summary>
    public class MicaComplianceDisclosure
    {
        /// <summary>
        /// Whether this network requires MICA compliance
        /// </summary>
        public bool RequiresMicaCompliance { get; set; }

        /// <summary>
        /// Network identifier (e.g., voimain-v1.0, aramidmain-v1.0)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// MICA regulation articles applicable to this token
        /// </summary>
        public List<string> ApplicableRegulations { get; set; } = new();

        /// <summary>
        /// Compliance check performed timestamp
        /// </summary>
        public DateTime ComplianceCheckDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional compliance notes
        /// </summary>
        public string? ComplianceNotes { get; set; }
    }

    /// <summary>
    /// Audit metadata for allowlist verification
    /// </summary>
    public class AllowlistAuditMetadata
    {
        /// <summary>
        /// Unique identifier for this verification request
        /// </summary>
        public string VerificationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Address of the user who performed the verification
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the verification was performed
        /// </summary>
        public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Source of the verification request (API, UI, etc.)
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// IP address of the requester (if available)
        /// </summary>
        public string? RequestIpAddress { get; set; }
    }
}

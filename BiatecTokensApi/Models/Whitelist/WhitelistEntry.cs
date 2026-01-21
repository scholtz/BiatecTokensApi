namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Represents a whitelist entry for an RWA token
    /// </summary>
    public class WhitelistEntry
    {
        /// <summary>
        /// Unique identifier for the whitelist entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID (token ID) for which this whitelist entry applies
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// The Algorand address being whitelisted
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// The status of the whitelist entry (Active, Inactive, Revoked)
        /// </summary>
        public WhitelistStatus Status { get; set; }

        /// <summary>
        /// The address of the user who created this whitelist entry
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the whitelist entry was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the whitelist entry was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// The address of the user who last updated this whitelist entry
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Reason for whitelisting this address (e.g., "KYC verified", "Accredited investor")
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Date when the whitelist entry expires (optional)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Whether KYC verification has been completed for this address
        /// </summary>
        public bool KycVerified { get; set; }

        /// <summary>
        /// Date when KYC verification was completed
        /// </summary>
        public DateTime? KycVerificationDate { get; set; }

        /// <summary>
        /// Name of the KYC provider
        /// </summary>
        public string? KycProvider { get; set; }
    }

    /// <summary>
    /// Status of a whitelist entry
    /// </summary>
    public enum WhitelistStatus
    {
        /// <summary>
        /// The address is actively whitelisted
        /// </summary>
        Active,

        /// <summary>
        /// The address is temporarily inactive
        /// </summary>
        Inactive,

        /// <summary>
        /// The address has been permanently revoked
        /// </summary>
        Revoked
    }
}

namespace BiatecTokensApi.Models.Kyc
{
    /// <summary>
    /// Represents a KYC verification record
    /// </summary>
    public class KycRecord
    {
        /// <summary>
        /// Unique identifier for this KYC record
        /// </summary>
        public string KycId { get; set; } = string.Empty;

        /// <summary>
        /// User ID associated with this verification
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Current verification status
        /// </summary>
        public KycStatus Status { get; set; } = KycStatus.NotStarted;

        /// <summary>
        /// KYC provider used for this verification
        /// </summary>
        public KycProvider Provider { get; set; } = KycProvider.Mock;

        /// <summary>
        /// Provider-specific reference ID for this verification session
        /// </summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>
        /// When the verification was initiated
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the status was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the verification was completed (approved or rejected)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Expiration date for approved verifications (regulatory requirement)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Reason for rejection or additional context
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Encrypted sensitive verification data
        /// </summary>
        public string? EncryptedData { get; set; }

        /// <summary>
        /// Correlation ID for tracking across systems
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Additional metadata from provider
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}

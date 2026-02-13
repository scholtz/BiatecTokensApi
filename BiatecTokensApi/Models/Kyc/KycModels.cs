namespace BiatecTokensApi.Models.Kyc
{
    /// <summary>
    /// Request model for starting KYC verification
    /// </summary>
    public class StartKycVerificationRequest
    {
        /// <summary>
        /// User's full legal name
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Date of birth (YYYY-MM-DD format)
        /// </summary>
        public string? DateOfBirth { get; set; }

        /// <summary>
        /// Country of residence (ISO 3166-1 alpha-2 code)
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Optional: Document type (passport, drivers_license, etc.)
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Optional: Document number
        /// </summary>
        public string? DocumentNumber { get; set; }

        /// <summary>
        /// Additional metadata for verification
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Response from starting KYC verification
    /// </summary>
    public class StartKycVerificationResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// KYC record ID
        /// </summary>
        public string? KycId { get; set; }

        /// <summary>
        /// Provider reference ID
        /// </summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>
        /// Current status
        /// </summary>
        public KycStatus Status { get; set; }

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Error message if unsuccessful
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code if unsuccessful
        /// </summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Response for KYC status query
    /// </summary>
    public class KycStatusResponse
    {
        /// <summary>
        /// Whether the query was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// KYC record ID
        /// </summary>
        public string? KycId { get; set; }

        /// <summary>
        /// Current verification status
        /// </summary>
        public KycStatus Status { get; set; }

        /// <summary>
        /// Provider name
        /// </summary>
        public KycProvider Provider { get; set; }

        /// <summary>
        /// When verification was initiated
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// When status was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// When verification was completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Expiration date (for approved verifications)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Reason for rejection or additional context
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Error message if query unsuccessful
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code if query unsuccessful
        /// </summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Webhook payload from KYC provider
    /// </summary>
    public class KycWebhookPayload
    {
        /// <summary>
        /// Provider reference ID
        /// </summary>
        public string ProviderReferenceId { get; set; } = string.Empty;

        /// <summary>
        /// Event type
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// New status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the event
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional reason for status change
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Signature for verification
        /// </summary>
        public string? Signature { get; set; }

        /// <summary>
        /// Additional data from provider
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }
}

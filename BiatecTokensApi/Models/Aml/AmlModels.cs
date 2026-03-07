namespace BiatecTokensApi.Models.Aml
{
    /// <summary>
    /// Risk level for AML screening results
    /// </summary>
    public enum AmlRiskLevel
    {
        /// <summary>Low risk — no significant matches</summary>
        Low = 0,

        /// <summary>Medium risk — minor PEP or indirect match requiring monitoring</summary>
        Medium = 1,

        /// <summary>High risk — confirmed sanctions match or high-risk jurisdiction</summary>
        High = 2,

        /// <summary>Screening not yet performed</summary>
        Unknown = 3
    }

    /// <summary>
    /// Overall AML screening status for a subject
    /// </summary>
    public enum AmlScreeningStatus
    {
        /// <summary>Screening has not been initiated</summary>
        NotScreened = 0,

        /// <summary>Screening is in progress</summary>
        Pending = 1,

        /// <summary>Subject cleared — no sanctions or PEP match</summary>
        Cleared = 2,

        /// <summary>Subject matches a sanctions list — token creation blocked</summary>
        SanctionsMatch = 3,

        /// <summary>Subject is a PEP — requires manual review before proceeding</summary>
        PepMatch = 4,

        /// <summary>Screening requires manual compliance review</summary>
        NeedsReview = 5,

        /// <summary>Screening error — provider unavailable or malformed response</summary>
        Error = 6
    }

    /// <summary>
    /// Persisted AML screening record
    /// </summary>
    public class AmlRecord
    {
        /// <summary>Unique identifier for this AML record</summary>
        public string AmlId { get; set; } = string.Empty;

        /// <summary>Subject (user) identifier</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Current screening status</summary>
        public AmlScreeningStatus Status { get; set; } = AmlScreeningStatus.NotScreened;

        /// <summary>Computed AML risk level</summary>
        public AmlRiskLevel RiskLevel { get; set; } = AmlRiskLevel.Unknown;

        /// <summary>Provider reference ID for this screening session</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>Reason code from the screening provider</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Human-readable reason or notes</summary>
        public string? Notes { get; set; }

        /// <summary>Correlation ID for cross-system tracing</summary>
        public string? CorrelationId { get; set; }

        /// <summary>When the screening record was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the screening record was last updated</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the next scheduled re-screening is due</summary>
        public DateTime? NextScreeningDue { get; set; }

        /// <summary>Additional metadata from screening provider</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Request to trigger an AML screening (admin endpoint)
    /// </summary>
    public class AmlScreenRequest
    {
        /// <summary>Subject (user) ID to screen</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Optional additional metadata to pass to the provider</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Response from an AML screen request
    /// </summary>
    public class AmlScreenResponse
    {
        /// <summary>Whether the request was accepted</summary>
        public bool Success { get; set; }

        /// <summary>AML record ID</summary>
        public string? AmlId { get; set; }

        /// <summary>Provider reference ID</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>Current screening status</summary>
        public AmlScreeningStatus Status { get; set; }

        /// <summary>Risk level determined by screening</summary>
        public AmlRiskLevel RiskLevel { get; set; }

        /// <summary>Correlation ID for tracking</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Error message if unsuccessful</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if unsuccessful</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// AML status response for a specific user
    /// </summary>
    public class AmlStatusResponse
    {
        /// <summary>Whether the query succeeded</summary>
        public bool Success { get; set; }

        /// <summary>AML record ID</summary>
        public string? AmlId { get; set; }

        /// <summary>Current screening status</summary>
        public AmlScreeningStatus Status { get; set; }

        /// <summary>Risk level</summary>
        public AmlRiskLevel RiskLevel { get; set; }

        /// <summary>Provider reference ID</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>Reason code from provider</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Human-readable notes</summary>
        public string? Notes { get; set; }

        /// <summary>When the record was created</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>When the record was last updated</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>When the next re-screening is due</summary>
        public DateTime? NextScreeningDue { get; set; }

        /// <summary>Error message if query failed</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if query failed</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Webhook payload from AML continuous monitoring provider
    /// </summary>
    public class AmlWebhookPayload
    {
        /// <summary>Provider reference ID of the screening session</summary>
        public string ProviderReferenceId { get; set; } = string.Empty;

        /// <summary>Alert type (e.g., SANCTIONS_MATCH, PEP_MATCH, REVIEW_REQUIRED)</summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>New screening status</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Risk level reported by the provider</summary>
        public string RiskLevel { get; set; } = string.Empty;

        /// <summary>Timestamp of the event</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Reason code from provider</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Additional data from provider</summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// AML compliance report for a user
    /// </summary>
    public class AmlReportResponse
    {
        /// <summary>Whether the report was generated successfully</summary>
        public bool Success { get; set; }

        /// <summary>User ID this report covers</summary>
        public string? UserId { get; set; }

        /// <summary>Report generation timestamp</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>Most recent AML record</summary>
        public AmlRecord? LatestRecord { get; set; }

        /// <summary>All historical screening records</summary>
        public List<AmlRecord> ScreeningHistory { get; set; } = new();

        /// <summary>Overall compliance status narrative</summary>
        public string? ComplianceSummary { get; set; }

        /// <summary>Error message if report generation failed</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if report generation failed</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Request to initiate KYC and return an SDK token for the frontend widget
    /// </summary>
    public class InitiateKycRequest
    {
        /// <summary>User's full legal name</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Date of birth (YYYY-MM-DD)</summary>
        public string? DateOfBirth { get; set; }

        /// <summary>Country of residence (ISO 3166-1 alpha-2)</summary>
        public string? Country { get; set; }

        /// <summary>Document type for verification (passport, national_id, drivers_license)</summary>
        public string? DocumentType { get; set; }

        /// <summary>Additional metadata</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Response from KYC initiation — includes SDK token for frontend widget
    /// </summary>
    public class InitiateKycResponse
    {
        /// <summary>Whether the initiation was successful</summary>
        public bool Success { get; set; }

        /// <summary>KYC record ID</summary>
        public string? KycId { get; set; }

        /// <summary>
        /// SDK token for the frontend KYC verification widget (e.g., Sumsub SDK token).
        /// Pass this to the frontend to launch the verification widget.
        /// </summary>
        public string? SdkToken { get; set; }

        /// <summary>Provider reference ID</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>URL for the verification widget (alternative to SDK token)</summary>
        public string? VerificationUrl { get; set; }

        /// <summary>Estimated processing time (human-readable)</summary>
        public string EstimatedProcessingTime { get; set; } = "Typically 2-5 minutes";

        /// <summary>Correlation ID for tracking</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Error message if unsuccessful</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if unsuccessful</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Admin response for KYC record details
    /// </summary>
    public class KycAdminResponse
    {
        /// <summary>Whether the query was successful</summary>
        public bool Success { get; set; }

        /// <summary>KYC record ID</summary>
        public string? KycId { get; set; }

        /// <summary>User ID</summary>
        public string? UserId { get; set; }

        /// <summary>Current verification status</summary>
        public BiatecTokensApi.Models.Kyc.KycStatus Status { get; set; }

        /// <summary>KYC provider</summary>
        public BiatecTokensApi.Models.Kyc.KycProvider Provider { get; set; }

        /// <summary>Provider reference ID</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>Reason for rejection or admin notes</summary>
        public string? Reason { get; set; }

        /// <summary>When the record was created</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>When the record was last updated</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>When verification was completed</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Expiration date</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Correlation ID</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Additional metadata</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>Error message if query failed</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if query failed</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Admin request to override KYC status
    /// </summary>
    public class KycAdminOverrideRequest
    {
        /// <summary>New status to set</summary>
        public BiatecTokensApi.Models.Kyc.KycStatus NewStatus { get; set; }

        /// <summary>Reason for the override (required)</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Admin user performing the override</summary>
        public string AdminId { get; set; } = string.Empty;
    }

    /// <summary>
    /// GDPR erasure request
    /// </summary>
    public class GdprErasureRequest
    {
        /// <summary>User ID whose PII should be anonymized</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Reason for erasure request</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// GDPR erasure response
    /// </summary>
    public class GdprErasureResponse
    {
        /// <summary>Whether the erasure succeeded</summary>
        public bool Success { get; set; }

        /// <summary>
        /// Anonymization reference — retained for audit purposes after PII is erased.
        /// Regulatory data retention (AMLD5: 5 years) is preserved via this reference.
        /// </summary>
        public string? AnonymizationReference { get; set; }

        /// <summary>Number of KYC records anonymized</summary>
        public int KycRecordsAnonymized { get; set; }

        /// <summary>Number of AML records anonymized</summary>
        public int AmlRecordsAnonymized { get; set; }

        /// <summary>When the erasure was performed</summary>
        public DateTime ErasedAt { get; set; }

        /// <summary>Error message if unsuccessful</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if unsuccessful</summary>
        public string? ErrorCode { get; set; }
    }
}

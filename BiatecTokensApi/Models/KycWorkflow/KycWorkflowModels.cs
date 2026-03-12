using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.KycWorkflow
{
    /// <summary>
    /// States in the KYC verification workflow state machine
    /// </summary>
    public enum KycVerificationState
    {
        NotStarted = 0,
        Pending = 1,
        ManualReviewRequired = 2,
        Approved = 3,
        Rejected = 4,
        Expired = 5
    }

    /// <summary>
    /// Structured reason codes for KYC rejections
    /// </summary>
    public enum KycRejectionReason
    {
        None = 0,
        DocumentExpired = 1,
        DocumentInvalid = 2,
        DocumentMismatch = 3,
        FaceMatchFailed = 4,
        AddressUnverifiable = 5,
        SanctionsMatch = 6,
        InsufficientEvidence = 7,
        CountryRestricted = 8,
        IdentityConflict = 9,
        Other = 99
    }

    /// <summary>
    /// Types of evidence that can be submitted for KYC verification
    /// </summary>
    public enum KycEvidenceType
    {
        Passport = 0,
        DriversLicense = 1,
        NationalId = 2,
        UtilityBill = 3,
        BankStatement = 4,
        TaxDocument = 5,
        SelfieWithId = 6,
        VideoVerification = 7,
        Other = 99
    }

    /// <summary>
    /// Audit entry for tracking state changes in the KYC workflow
    /// </summary>
    public class KycAuditEntry
    {
        /// <summary>Unique identifier for this audit entry</summary>
        public string EntryId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>KYC record this entry belongs to</summary>
        public string KycId { get; set; } = string.Empty;

        /// <summary>State before the transition</summary>
        public KycVerificationState FromState { get; set; }

        /// <summary>State after the transition</summary>
        public KycVerificationState ToState { get; set; }

        /// <summary>Actor who caused the state change</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Type of actor: System, Admin, Reviewer, Provider</summary>
        public string ActorType { get; set; } = string.Empty;

        /// <summary>Optional review note from the actor</summary>
        public string? ReviewNote { get; set; }

        /// <summary>Free-form reason code string</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Structured rejection reason</summary>
        public KycRejectionReason RejectionReason { get; set; }

        /// <summary>When this audit entry was recorded</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Correlation ID for distributed tracing</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Additional metadata for this audit entry</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Evidence metadata for a submitted identity document
    /// </summary>
    public class KycEvidenceMetadata
    {
        /// <summary>Unique identifier for this evidence item</summary>
        public string EvidenceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>KYC record this evidence belongs to</summary>
        public string KycId { get; set; } = string.Empty;

        /// <summary>Type of evidence document</summary>
        public KycEvidenceType EvidenceType { get; set; }

        /// <summary>External reference to the stored document (not the document itself)</summary>
        public string? DocumentReference { get; set; }

        /// <summary>SHA256 hash of the document for integrity verification</summary>
        public string? ContentHash { get; set; }

        /// <summary>Country that issued the document</summary>
        public string? IssuingCountry { get; set; }

        /// <summary>When the document expires</summary>
        public DateTime? DocumentExpiresAt { get; set; }

        /// <summary>Whether this evidence has been verified</summary>
        public bool IsVerified { get; set; }

        /// <summary>Actor who verified this evidence</summary>
        public string? VerifiedBy { get; set; }

        /// <summary>When the evidence was uploaded</summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the evidence was verified</summary>
        public DateTime? VerifiedAt { get; set; }

        /// <summary>Additional metadata for this evidence item</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Comprehensive KYC workflow record including full audit history and evidence
    /// </summary>
    public class KycWorkflowRecord
    {
        /// <summary>Unique identifier for this KYC record</summary>
        public string KycId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>User/investor/participant identifier</summary>
        public string ParticipantId { get; set; } = string.Empty;

        /// <summary>Reference from the KYC provider</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Name of the KYC provider used</summary>
        public string? ProviderName { get; set; }

        /// <summary>Current state in the workflow</summary>
        public KycVerificationState State { get; set; } = KycVerificationState.NotStarted;

        /// <summary>Current review note</summary>
        public string? CurrentReviewNote { get; set; }

        /// <summary>Most recent rejection reason</summary>
        public KycRejectionReason LastRejectionReason { get; set; }

        /// <summary>Actor ID of the last reviewer</summary>
        public string? LastReviewerActorId { get; set; }

        /// <summary>When the record was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the record was last updated</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the KYC was approved</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>When the KYC approval expires</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>When the KYC process was completed (approved or rejected)</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Whether the approval has expired</summary>
        public bool IsExpired => State == KycVerificationState.Approved && ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

        /// <summary>Full audit history of state transitions</summary>
        public List<KycAuditEntry> AuditHistory { get; set; } = new();

        /// <summary>Evidence submitted for this verification</summary>
        public List<KycEvidenceMetadata> Evidence { get; set; } = new();

        /// <summary>Additional metadata</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>Correlation ID for distributed tracing</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Actor who created this record</summary>
        public string CreatedByActorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to create a new KYC verification workflow record
    /// </summary>
    public class CreateKycVerificationRequest
    {
        /// <summary>User/investor/participant identifier</summary>
        [Required]
        public string ParticipantId { get; set; } = string.Empty;

        /// <summary>Optional KYC provider name</summary>
        public string? ProviderName { get; set; }

        /// <summary>Optional external reference from the provider</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Optional initial note</summary>
        public string? InitialNote { get; set; }

        /// <summary>Days until KYC approval expires (default 365)</summary>
        public int ExpirationDays { get; set; } = 365;

        /// <summary>Additional metadata</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Request to update the status of a KYC verification
    /// </summary>
    public class UpdateKycVerificationStatusRequest
    {
        /// <summary>Target state to transition to</summary>
        [Required]
        public KycVerificationState NewState { get; set; }

        /// <summary>Optional review note</summary>
        public string? ReviewNote { get; set; }

        /// <summary>Structured rejection reason</summary>
        public KycRejectionReason RejectionReason { get; set; } = KycRejectionReason.None;

        /// <summary>Free-form reason code</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Override expiration days when approving</summary>
        public int? ExpirationDays { get; set; }

        /// <summary>Additional metadata</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Request to add evidence to a KYC verification
    /// </summary>
    public class AddKycEvidenceRequest
    {
        /// <summary>Type of evidence document</summary>
        [Required]
        public KycEvidenceType EvidenceType { get; set; }

        /// <summary>External reference to the stored document</summary>
        public string? DocumentReference { get; set; }

        /// <summary>SHA256 hash for integrity verification</summary>
        public string? ContentHash { get; set; }

        /// <summary>Country that issued the document</summary>
        public string? IssuingCountry { get; set; }

        /// <summary>When the document expires</summary>
        public DateTime? DocumentExpiresAt { get; set; }

        /// <summary>Additional metadata</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Response for KYC verification operations
    /// </summary>
    public class KycVerificationResponse
    {
        /// <summary>Whether the operation succeeded</summary>
        public bool Success { get; set; }

        /// <summary>The KYC workflow record</summary>
        public KycWorkflowRecord? Record { get; set; }

        /// <summary>Machine-readable error code</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response containing the audit history for a KYC record
    /// </summary>
    public class KycHistoryResponse
    {
        /// <summary>Whether the operation succeeded</summary>
        public bool Success { get; set; }

        /// <summary>KYC record identifier</summary>
        public string KycId { get; set; } = string.Empty;

        /// <summary>Participant identifier</summary>
        public string ParticipantId { get; set; } = string.Empty;

        /// <summary>Current state</summary>
        public KycVerificationState CurrentState { get; set; }

        /// <summary>Chronologically ordered audit history</summary>
        public List<KycAuditEntry> History { get; set; } = new();

        /// <summary>Machine-readable error code</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response containing evidence items for a KYC record
    /// </summary>
    public class KycEvidenceResponse
    {
        /// <summary>Whether the operation succeeded</summary>
        public bool Success { get; set; }

        /// <summary>KYC record identifier</summary>
        public string KycId { get; set; } = string.Empty;

        /// <summary>Evidence items submitted for this KYC record</summary>
        public List<KycEvidenceMetadata> Evidence { get; set; } = new();

        /// <summary>Machine-readable error code</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to evaluate KYC eligibility for a participant
    /// </summary>
    public class KycEligibilityRequest
    {
        /// <summary>Participant to evaluate</summary>
        [Required]
        public string ParticipantId { get; set; } = string.Empty;

        /// <summary>Optional offering/token for context</summary>
        public string? OfferingId { get; set; }

        /// <summary>Whether approved status is required (default true)</summary>
        public bool RequireApproved { get; set; } = true;

        /// <summary>Whether to check expiry (default true)</summary>
        public bool CheckExpiry { get; set; } = true;
    }

    /// <summary>
    /// Result of evaluating a participant's KYC eligibility
    /// </summary>
    public class KycEligibilityResult
    {
        /// <summary>Whether the participant is eligible</summary>
        public bool IsEligible { get; set; }

        /// <summary>Participant identifier</summary>
        public string ParticipantId { get; set; } = string.Empty;

        /// <summary>Optional offering/token for context</summary>
        public string? OfferingId { get; set; }

        /// <summary>Current KYC state for the participant</summary>
        public KycVerificationState CurrentState { get; set; }

        /// <summary>KYC record identifier if one exists</summary>
        public string? KycId { get; set; }

        /// <summary>Reason for ineligibility if not eligible</summary>
        public string? IneligibilityReason { get; set; }

        /// <summary>When the KYC approval expires</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Whether KYC is required for this offering</summary>
        public bool KycRequired { get; set; } = true;

        /// <summary>When eligibility was evaluated</summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Machine-readable error code</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of validating a state machine transition
    /// </summary>
    public class KycStateTransitionValidationResult
    {
        /// <summary>Whether the transition is valid</summary>
        public bool IsValid { get; set; }

        /// <summary>Source state</summary>
        public KycVerificationState FromState { get; set; }

        /// <summary>Target state</summary>
        public KycVerificationState ToState { get; set; }

        /// <summary>Error message if the transition is invalid</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Details of a transition error
    /// </summary>
    public class KycTransitionError
    {
        /// <summary>Always false for a transition error</summary>
        public bool IsValid => false;

        /// <summary>Machine-readable error code</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>Human-readable error message</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Source state</summary>
        public KycVerificationState FromState { get; set; }

        /// <summary>Target state</summary>
        public KycVerificationState ToState { get; set; }
    }
}

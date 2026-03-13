using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Whitelist
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Investor category classification for regulatory eligibility purposes
    /// </summary>
    public enum WhitelistPolicyInvestorCategory
    {
        /// <summary>Meets accredited investor threshold (e.g. SEC Rule 501)</summary>
        AccreditedInvestor,

        /// <summary>Meets qualified purchaser threshold (e.g. US Investment Company Act)</summary>
        QualifiedPurchaser,

        /// <summary>General retail investor without special classification</summary>
        RetailInvestor,

        /// <summary>Institutional investor (banks, funds, insurance companies)</summary>
        Institutional,

        /// <summary>Professional client under MiFID II</summary>
        Professional,

        /// <summary>Not eligible for investment in the current offering</summary>
        NonEligible
    }

    /// <summary>
    /// Lifecycle status for a whitelist policy
    /// </summary>
    public enum WhitelistPolicyStatus
    {
        /// <summary>Policy is being drafted; evaluation is fail-closed (always Deny)</summary>
        Draft,

        /// <summary>Policy is active and used for eligibility evaluation</summary>
        Active,

        /// <summary>Policy is archived and no longer used</summary>
        Archived
    }

    /// <summary>
    /// Outcome of an eligibility evaluation against a whitelist policy
    /// </summary>
    public enum WhitelistPolicyEligibilityOutcome
    {
        /// <summary>Participant is permitted to participate</summary>
        Allow,

        /// <summary>Participant is denied participation</summary>
        Deny,

        /// <summary>Requires manual compliance officer review</summary>
        ConditionalReview
    }

    // ── Core Policy Model ─────────────────────────────────────────────────────────

    /// <summary>
    /// A unified policy container that controls participant eligibility for an asset.
    /// Encodes allowlists, denylists, jurisdiction filters, and investor category requirements.
    /// </summary>
    public class WhitelistPolicy
    {
        /// <summary>Unique identifier for this policy</summary>
        public string PolicyId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Human-readable name for the policy</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Description of the policy's purpose</summary>
        public string? Description { get; set; }

        /// <summary>The asset (token) this policy applies to</summary>
        public ulong AssetId { get; set; }

        /// <summary>Current lifecycle status</summary>
        public WhitelistPolicyStatus Status { get; set; } = WhitelistPolicyStatus.Draft;

        /// <summary>
        /// Explicit allow list of Algorand addresses.
        /// When non-empty, only addresses in this list may be allowed.
        /// </summary>
        public List<string> AllowedAddresses { get; set; } = new();

        /// <summary>
        /// Explicit deny list of Algorand addresses.
        /// Addresses in this list are always denied regardless of other rules.
        /// </summary>
        public List<string> DeniedAddresses { get; set; } = new();

        /// <summary>
        /// ISO 3166-1 alpha-2 jurisdiction codes that are permitted.
        /// When non-empty, only addresses in these jurisdictions may be allowed.
        /// </summary>
        public List<string> AllowedJurisdictions { get; set; } = new();

        /// <summary>
        /// ISO 3166-1 alpha-2 jurisdiction codes that are blocked.
        /// Addresses from these jurisdictions are always denied.
        /// </summary>
        public List<string> BlockedJurisdictions { get; set; } = new();

        /// <summary>
        /// Investor categories that are required.
        /// When non-empty, only participants matching one of these categories may be allowed.
        /// </summary>
        public List<WhitelistPolicyInvestorCategory> RequiredInvestorCategories { get; set; } = new();

        /// <summary>Creator's address or identifier</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this policy was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Last updater's address or identifier</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>UTC timestamp of the last update</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Monotonically increasing version counter for optimistic concurrency</summary>
        public int Version { get; set; } = 1;

        /// <summary>Free-text notes for compliance officers</summary>
        public string? Notes { get; set; }
    }

    // ── Audit Enums ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Machine-readable reason codes for eligibility decisions
    /// </summary>
    public enum WhitelistEligibilityReasonCode
    {
        /// <summary>All policy criteria were satisfied; participant is allowed</summary>
        AllPolicyCriteriaSatisfied,

        /// <summary>Policy was not found</summary>
        PolicyNotFound,

        /// <summary>Policy is in Draft state; fail-closed</summary>
        PolicyInDraftState,

        /// <summary>Policy is archived and inactive</summary>
        PolicyIsArchived,

        /// <summary>Policy has no rules; fail-closed</summary>
        PolicyHasNoRules,

        /// <summary>Participant address is on the explicit deny list</summary>
        AddressOnDenyList,

        /// <summary>Participant's jurisdiction is blocked</summary>
        RestrictedJurisdiction,

        /// <summary>Participant's jurisdiction is not in the allowed list</summary>
        JurisdictionNotAllowed,

        /// <summary>No jurisdiction provided but policy requires one</summary>
        JurisdictionNotProvided,

        /// <summary>Participant's investor category is not permitted</summary>
        UnsupportedInvestorCategory,

        /// <summary>Participant address is not on the explicit allow list</summary>
        AddressNotOnAllowList,

        /// <summary>Policy is in an ambiguous or incomplete state</summary>
        PolicyIncomplete
    }

    /// <summary>
    /// Types of policy lifecycle events
    /// </summary>
    public enum WhitelistAuditEventType
    {
        /// <summary>A new whitelist policy was created</summary>
        PolicyCreated,

        /// <summary>A whitelist policy was updated</summary>
        PolicyUpdated,

        /// <summary>A whitelist policy was activated (set to Active status)</summary>
        PolicyActivated,

        /// <summary>A whitelist policy was archived</summary>
        PolicyArchived,

        /// <summary>A participant eligibility evaluation was performed</summary>
        EligibilityEvaluated
    }

    // ── Audit Models ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot of policy version metadata at the time of an evaluation or event
    /// </summary>
    public class WhitelistPolicyVersionMetadata
    {
        /// <summary>Policy unique identifier</summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>Human-readable policy name</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Monotonically increasing version number</summary>
        public int Version { get; set; }

        /// <summary>Lifecycle status at this version</summary>
        public WhitelistPolicyStatus Status { get; set; }

        /// <summary>UTC timestamp when this version was created or last updated</summary>
        public DateTime EffectiveAt { get; set; }

        /// <summary>Actor who created or last updated this version</summary>
        public string ActorIdentifier { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single immutable audit event recording a policy lifecycle action or evaluation decision
    /// </summary>
    public class WhitelistAuditEvent
    {
        /// <summary>Unique identifier for this audit event</summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The policy this event belongs to</summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>Type of event</summary>
        public WhitelistAuditEventType EventType { get; set; }

        /// <summary>Identifier of the actor who triggered this event</summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the event occurred</summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        /// <summary>Human-readable description of what happened</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Policy version number at the time of this event</summary>
        public int PolicyVersion { get; set; }

        /// <summary>Machine-readable reason codes associated with this event (populated for EligibilityEvaluated events)</summary>
        public List<WhitelistEligibilityReasonCode> ReasonCodes { get; set; } = new();

        /// <summary>Eligibility outcome (only for EligibilityEvaluated events)</summary>
        public WhitelistPolicyEligibilityOutcome? EligibilityOutcome { get; set; }

        /// <summary>Participant address evaluated (only for EligibilityEvaluated events)</summary>
        public string? ParticipantAddress { get; set; }

        /// <summary>Jurisdiction code evaluated (only for EligibilityEvaluated events)</summary>
        public string? JurisdictionCode { get; set; }

        /// <summary>Investor category evaluated (only for EligibilityEvaluated events)</summary>
        public WhitelistPolicyInvestorCategory? InvestorCategory { get; set; }

        /// <summary>Whether fail-closed semantics were applied (only for EligibilityEvaluated events)</summary>
        public bool? IsFailClosed { get; set; }
    }

    /// <summary>
    /// Request for paginated whitelist policy audit history
    /// </summary>
    public class WhitelistAuditHistoryRequest
    {
        /// <summary>Page number (1-based)</summary>
        public int Page { get; set; } = 1;

        /// <summary>Number of events per page (max 200)</summary>
        public int PageSize { get; set; } = 50;

        /// <summary>Optional filter to restrict results to a specific event type</summary>
        public WhitelistAuditEventType? EventTypeFilter { get; set; }
    }

    /// <summary>
    /// Paginated response containing whitelist policy audit history
    /// </summary>
    public class WhitelistAuditHistoryResponse : BaseResponse
    {
        /// <summary>The policy ID this history belongs to</summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>Audit events for the requested page (most recent first)</summary>
        public List<WhitelistAuditEvent> Events { get; set; } = new();

        /// <summary>Total number of events matching the filter</summary>
        public int TotalCount { get; set; }

        /// <summary>Current page number (1-based)</summary>
        public int Page { get; set; }

        /// <summary>Number of events per page</summary>
        public int PageSize { get; set; }

        /// <summary>Total number of pages</summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Summary statistics of eligibility evaluations for compliance reporting
    /// </summary>
    public class WhitelistEvaluationSummary
    {
        /// <summary>Total number of evaluations recorded</summary>
        public int TotalEvaluations { get; set; }

        /// <summary>Number of Allow outcomes</summary>
        public int AllowCount { get; set; }

        /// <summary>Number of Deny outcomes</summary>
        public int DenyCount { get; set; }

        /// <summary>Number of ConditionalReview outcomes</summary>
        public int ConditionalReviewCount { get; set; }

        /// <summary>Number of fail-closed denials</summary>
        public int FailClosedCount { get; set; }

        /// <summary>Most frequent deny reason codes</summary>
        public List<WhitelistEligibilityReasonCode> TopDenyReasonCodes { get; set; } = new();
    }

    /// <summary>
    /// Request for a compliance evidence report for a whitelist policy
    /// </summary>
    public class WhitelistComplianceEvidenceRequest
    {
        /// <summary>Whether to include the full evaluation history in the report</summary>
        public bool IncludeEvaluationHistory { get; set; } = true;

        /// <summary>Whether to include policy change history in the report</summary>
        public bool IncludePolicyChangeHistory { get; set; } = true;

        /// <summary>Optional start date for filtering events (UTC)</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Optional end date for filtering events (UTC)</summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Export-friendly compliance evidence report for a whitelist policy,
    /// suitable for audit export and regulatory review workflows.
    /// </summary>
    public class WhitelistComplianceEvidenceReport : BaseResponse
    {
        /// <summary>Policy identifier this report covers</summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this report was generated</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Identifier of the actor who requested this report</summary>
        public string GeneratedBy { get; set; } = string.Empty;

        /// <summary>Snapshot of the current policy version metadata</summary>
        public WhitelistPolicyVersionMetadata? PolicyVersionMetadata { get; set; }

        /// <summary>Summary statistics of eligibility evaluations</summary>
        public WhitelistEvaluationSummary EvaluationSummary { get; set; } = new();

        /// <summary>Full audit event log filtered by request parameters</summary>
        public List<WhitelistAuditEvent> AuditEvents { get; set; } = new();

        /// <summary>Policy change events only (Created, Updated, Activated, Archived)</summary>
        public List<WhitelistAuditEvent> PolicyChangeHistory { get; set; } = new();
    }

    // ── Eligibility Request / Result ─────────────────────────────────────────────

    /// <summary>
    /// Request to evaluate a participant's eligibility against a whitelist policy
    /// </summary>
    public class WhitelistPolicyEligibilityRequest
    {
        /// <summary>The policy to evaluate against</summary>
        [Required]
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>The Algorand address of the participant</summary>
        [Required]
        public string ParticipantAddress { get; set; } = string.Empty;

        /// <summary>ISO 3166-1 alpha-2 jurisdiction code of the participant</summary>
        public string? JurisdictionCode { get; set; }

        /// <summary>Investor category of the participant</summary>
        public WhitelistPolicyInvestorCategory InvestorCategory { get; set; } = WhitelistPolicyInvestorCategory.NonEligible;
    }

    /// <summary>
    /// Result of a participant eligibility evaluation
    /// </summary>
    public class WhitelistPolicyEligibilityResult : BaseResponse
    {
        /// <summary>Eligibility outcome</summary>
        public WhitelistPolicyEligibilityOutcome Outcome { get; set; }

        /// <summary>Human-readable reasons explaining the outcome</summary>
        public List<string> Reasons { get; set; } = new();

        /// <summary>Guidance for the operator or compliance officer</summary>
        public string? OperatorGuidance { get; set; }

        /// <summary>
        /// Indicates whether fail-closed semantics were applied
        /// (true when the policy was in Draft state or ambiguous)
        /// </summary>
        public bool IsFailClosed { get; set; }

        /// <summary>Unique audit identifier for this evaluation event</summary>
        public string AuditId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>UTC timestamp when the evaluation was performed</summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Machine-readable reason codes for the eligibility outcome</summary>
        public List<WhitelistEligibilityReasonCode> ReasonCodes { get; set; } = new();

        /// <summary>Policy version metadata at the time of this evaluation</summary>
        public WhitelistPolicyVersionMetadata? PolicyVersionMetadata { get; set; }
    }

    // ── Validation ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single validation issue found in a whitelist policy
    /// </summary>
    public class WhitelistPolicyValidationIssue
    {
        /// <summary>Machine-readable issue code</summary>
        public string IssueCode { get; set; } = string.Empty;

        /// <summary>Severity: Error, Warning, or Info</summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>Human-readable description of the issue</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Recommended remediation guidance</summary>
        public string? Guidance { get; set; }
    }

    /// <summary>
    /// Result of validating a whitelist policy for contradictions and completeness
    /// </summary>
    public class WhitelistPolicyValidationResult : BaseResponse
    {
        /// <summary>Whether the policy is free of blocking contradictions</summary>
        public bool IsValid { get; set; }

        /// <summary>List of issues found (may be empty)</summary>
        public List<WhitelistPolicyValidationIssue> Issues { get; set; } = new();
    }

    // ── CRUD Request / Response Models ────────────────────────────────────────────

    /// <summary>
    /// Request to create a new whitelist policy
    /// </summary>
    public class CreateWhitelistPolicyRequest
    {
        /// <summary>Human-readable name for the policy</summary>
        [Required]
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Description of the policy's purpose</summary>
        public string? Description { get; set; }

        /// <summary>The asset (token) this policy applies to</summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>Explicit allow list of Algorand addresses</summary>
        public List<string> AllowedAddresses { get; set; } = new();

        /// <summary>Explicit deny list of Algorand addresses</summary>
        public List<string> DeniedAddresses { get; set; } = new();

        /// <summary>ISO 3166-1 alpha-2 jurisdiction codes that are permitted</summary>
        public List<string> AllowedJurisdictions { get; set; } = new();

        /// <summary>ISO 3166-1 alpha-2 jurisdiction codes that are blocked</summary>
        public List<string> BlockedJurisdictions { get; set; } = new();

        /// <summary>Investor categories that are required</summary>
        public List<WhitelistPolicyInvestorCategory> RequiredInvestorCategories { get; set; } = new();

        /// <summary>Free-text notes for compliance officers</summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to update an existing whitelist policy
    /// </summary>
    public class UpdateWhitelistPolicyRequest
    {
        /// <summary>Human-readable name for the policy</summary>
        public string? PolicyName { get; set; }

        /// <summary>Description of the policy's purpose</summary>
        public string? Description { get; set; }

        /// <summary>Lifecycle status</summary>
        public WhitelistPolicyStatus? Status { get; set; }

        /// <summary>Explicit allow list of Algorand addresses</summary>
        public List<string>? AllowedAddresses { get; set; }

        /// <summary>Explicit deny list of Algorand addresses</summary>
        public List<string>? DeniedAddresses { get; set; }

        /// <summary>ISO 3166-1 alpha-2 jurisdiction codes that are permitted</summary>
        public List<string>? AllowedJurisdictions { get; set; }

        /// <summary>ISO 3166-1 alpha-2 jurisdiction codes that are blocked</summary>
        public List<string>? BlockedJurisdictions { get; set; }

        /// <summary>Investor categories that are required</summary>
        public List<WhitelistPolicyInvestorCategory>? RequiredInvestorCategories { get; set; }

        /// <summary>Free-text notes for compliance officers</summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Response containing a single whitelist policy
    /// </summary>
    public class WhitelistPolicyResponse : BaseResponse
    {
        /// <summary>The policy</summary>
        public WhitelistPolicy? Policy { get; set; }
    }

    /// <summary>
    /// Response containing a list of whitelist policies
    /// </summary>
    public class WhitelistPolicyListResponse : BaseResponse
    {
        /// <summary>The policies</summary>
        public List<WhitelistPolicy> Policies { get; set; } = new();

        /// <summary>Total number of policies matching the query</summary>
        public int TotalCount { get; set; }
    }
}

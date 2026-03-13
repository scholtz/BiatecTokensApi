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

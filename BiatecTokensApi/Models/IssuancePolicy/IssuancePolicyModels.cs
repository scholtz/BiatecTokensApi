namespace BiatecTokensApi.Models.IssuancePolicy
{
    /// <summary>
    /// Outcome of an issuance compliance policy evaluation
    /// </summary>
    public enum IssuancePolicyOutcome
    {
        /// <summary>Participant is eligible to participate in the issuance</summary>
        Allow,
        /// <summary>Participant is not eligible to participate in the issuance</summary>
        Deny,
        /// <summary>Participant may be eligible but requires additional review</summary>
        ConditionalReview
    }

    /// <summary>
    /// Compliance policy configuration for a token issuance
    /// </summary>
    public class IssuanceCompliancePolicy
    {
        /// <summary>Unique identifier for this policy (GUID)</summary>
        public string PolicyId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Address of the issuer who created this policy</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Token asset ID this policy applies to</summary>
        public ulong AssetId { get; set; }

        /// <summary>Human-readable policy name</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Optional description of the policy's intent</summary>
        public string? Description { get; set; }

        /// <summary>When true, participants must be on the whitelist to be eligible</summary>
        public bool WhitelistRequired { get; set; } = true;

        /// <summary>Allowed jurisdiction codes (ISO-3166 alpha-2). Empty list means all jurisdictions are allowed.</summary>
        public List<string> AllowedJurisdictions { get; set; } = new();

        /// <summary>Blocked jurisdiction codes. Empty list means none are blocked.</summary>
        public List<string> BlockedJurisdictions { get; set; } = new();

        /// <summary>When true, KYC verification is required for participation</summary>
        public bool KycRequired { get; set; } = false;

        /// <summary>Whether this policy is currently active</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>When the policy was created</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the policy was last updated</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Address of the user who created this policy</summary>
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to create a new issuance compliance policy
    /// </summary>
    public class CreateIssuancePolicyRequest
    {
        /// <summary>Token asset ID this policy applies to (must be > 0)</summary>
        public ulong AssetId { get; set; }

        /// <summary>Human-readable policy name (required)</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Optional description</summary>
        public string? Description { get; set; }

        /// <summary>When true, participants must be on the whitelist</summary>
        public bool WhitelistRequired { get; set; } = true;

        /// <summary>Allowed jurisdiction codes. Null or empty = all allowed.</summary>
        public List<string>? AllowedJurisdictions { get; set; }

        /// <summary>Blocked jurisdiction codes. Null or empty = none blocked.</summary>
        public List<string>? BlockedJurisdictions { get; set; }

        /// <summary>When true, KYC verification is required</summary>
        public bool KycRequired { get; set; } = false;

        /// <summary>Whether the policy is active immediately upon creation</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Request to update an existing issuance compliance policy (all fields optional)
    /// </summary>
    public class UpdateIssuancePolicyRequest
    {
        /// <summary>New policy name (if changing)</summary>
        public string? PolicyName { get; set; }

        /// <summary>New description (if changing)</summary>
        public string? Description { get; set; }

        /// <summary>New whitelist requirement setting (if changing)</summary>
        public bool? WhitelistRequired { get; set; }

        /// <summary>New allowed jurisdictions list (if changing)</summary>
        public List<string>? AllowedJurisdictions { get; set; }

        /// <summary>New blocked jurisdictions list (if changing)</summary>
        public List<string>? BlockedJurisdictions { get; set; }

        /// <summary>New KYC requirement setting (if changing)</summary>
        public bool? KycRequired { get; set; }

        /// <summary>New active status (if changing)</summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Request to evaluate a participant's eligibility against an issuance policy
    /// </summary>
    public class EvaluateParticipantRequest
    {
        /// <summary>Algorand or other blockchain address of the participant</summary>
        public string ParticipantAddress { get; set; } = string.Empty;

        /// <summary>Optional: participant's jurisdiction code (ISO-3166 alpha-2)</summary>
        public string? JurisdictionCode { get; set; }

        /// <summary>Optional: whether the participant has completed KYC</summary>
        public bool? KycVerified { get; set; }

        /// <summary>Optional: additional context notes for audit purposes</summary>
        public string? EvaluationContext { get; set; }
    }

    /// <summary>
    /// A matched rule detail included in the decision output
    /// </summary>
    public class MatchedPolicyRule
    {
        /// <summary>Unique rule identifier</summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>Human-readable rule name</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>The outcome this rule produced: "Allow", "Deny", or "Review"</summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Explanation of why this rule produced the outcome</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// The decision result returned after evaluating a participant against an issuance policy
    /// </summary>
    public class IssuancePolicyDecisionResult
    {
        /// <summary>Unique identifier for this decision (used for audit trail)</summary>
        public string DecisionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The policy that was evaluated</summary>
        public string PolicyId { get; set; } = string.Empty;

        /// <summary>Asset ID the policy applies to</summary>
        public ulong AssetId { get; set; }

        /// <summary>Address of the participant being evaluated</summary>
        public string ParticipantAddress { get; set; } = string.Empty;

        /// <summary>The final policy outcome</summary>
        public IssuancePolicyOutcome Outcome { get; set; }

        /// <summary>List of rules that matched during evaluation</summary>
        public List<MatchedPolicyRule> MatchedRules { get; set; } = new();

        /// <summary>Human-readable reasons for the decision</summary>
        public List<string> Reasons { get; set; } = new();

        /// <summary>Actions the participant must take to satisfy ConditionalReview requirements</summary>
        public List<string>? RequiredActions { get; set; }

        /// <summary>Version identifier for the policy at evaluation time</summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>When the evaluation was performed</summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Address of the entity that requested the evaluation</summary>
        public string EvaluatedBy { get; set; } = string.Empty;

        /// <summary>Whether the evaluation completed without errors</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error message if evaluation failed</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Standard response for issuance policy CRUD operations
    /// </summary>
    public class IssuancePolicyResponse
    {
        /// <summary>Whether the operation succeeded</summary>
        public bool Success { get; set; }

        /// <summary>The policy object (populated on success)</summary>
        public IssuanceCompliancePolicy? Policy { get; set; }

        /// <summary>Error message (populated on failure)</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response for listing issuance policies
    /// </summary>
    public class IssuancePolicyListResponse
    {
        /// <summary>Whether the operation succeeded</summary>
        public bool Success { get; set; }

        /// <summary>List of policies belonging to the requesting issuer</summary>
        public List<IssuanceCompliancePolicy> Policies { get; set; } = new();

        /// <summary>Total number of policies found</summary>
        public int TotalCount { get; set; }

        /// <summary>Error message (populated on failure)</summary>
        public string? ErrorMessage { get; set; }
    }
}

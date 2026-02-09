namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents a compliance decision for an onboarding step or token issuance operation
    /// </summary>
    /// <remarks>
    /// This model captures policy-driven compliance decisions for wallet-free enterprise onboarding.
    /// Each decision is immutable and forms an audit trail of who approved what, when, and why.
    /// Decisions are linked to specific policy rules that drove the approval or rejection.
    /// </remarks>
    public class ComplianceDecision
    {
        /// <summary>
        /// Unique identifier for the compliance decision
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Organization identifier for the entity being evaluated
        /// </summary>
        public string OrganizationId { get; set; } = string.Empty;

        /// <summary>
        /// Onboarding session identifier (unique per onboarding flow)
        /// </summary>
        public string? OnboardingSessionId { get; set; }

        /// <summary>
        /// The onboarding step this decision applies to
        /// </summary>
        public OnboardingStep Step { get; set; }

        /// <summary>
        /// The decision outcome
        /// </summary>
        public DecisionOutcome Outcome { get; set; }

        /// <summary>
        /// The policy rule identifiers that drove this decision
        /// </summary>
        public List<string> PolicyRuleIds { get; set; } = new();

        /// <summary>
        /// Address of the actor who made or approved this decision
        /// </summary>
        public string DecisionMaker { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the decision was made (UTC)
        /// </summary>
        public DateTime DecisionTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// References to evidence documents or data that support this decision
        /// </summary>
        public List<EvidenceReference> EvidenceReferences { get; set; } = new();

        /// <summary>
        /// Human-readable reason for the decision
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Policy version identifier to track which rules were used
        /// </summary>
        public string PolicyVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Correlation ID for linking related decisions
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Additional metadata about this decision
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Reference to the previous decision if this is an update
        /// </summary>
        public string? PreviousDecisionId { get; set; }

        /// <summary>
        /// Whether this decision has been superseded by a newer decision
        /// </summary>
        public bool IsSuperseded { get; set; }

        /// <summary>
        /// Timestamp when this decision was superseded (if applicable)
        /// </summary>
        public DateTime? SupersededAt { get; set; }

        /// <summary>
        /// ID of the decision that superseded this one (if applicable)
        /// </summary>
        public string? SupersededById { get; set; }

        /// <summary>
        /// Expiration timestamp for time-limited approvals (if applicable)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether this decision requires periodic review
        /// </summary>
        public bool RequiresReview { get; set; }

        /// <summary>
        /// Next review date if periodic review is required
        /// </summary>
        public DateTime? NextReviewDate { get; set; }
    }

    /// <summary>
    /// Onboarding steps that require compliance decisions
    /// </summary>
    public enum OnboardingStep
    {
        /// <summary>
        /// Organization identity verification
        /// </summary>
        OrganizationIdentityVerification,

        /// <summary>
        /// Business registration documentation
        /// </summary>
        BusinessRegistrationVerification,

        /// <summary>
        /// Beneficial ownership disclosure
        /// </summary>
        BeneficialOwnershipVerification,

        /// <summary>
        /// KYC/KYB compliance check
        /// </summary>
        KycKybVerification,

        /// <summary>
        /// AML screening and sanctions check
        /// </summary>
        AmlScreening,

        /// <summary>
        /// Jurisdictional compliance verification
        /// </summary>
        JurisdictionalCompliance,

        /// <summary>
        /// Token issuance authorization
        /// </summary>
        TokenIssuanceAuthorization,

        /// <summary>
        /// Wallet custody arrangement verification
        /// </summary>
        WalletCustodyVerification,

        /// <summary>
        /// Terms and conditions acceptance
        /// </summary>
        TermsAcceptance,

        /// <summary>
        /// Final onboarding approval
        /// </summary>
        FinalApproval
    }

    /// <summary>
    /// Outcome of a compliance decision
    /// </summary>
    public enum DecisionOutcome
    {
        /// <summary>
        /// Decision is pending evaluation
        /// </summary>
        Pending,

        /// <summary>
        /// Decision approved - step passed compliance
        /// </summary>
        Approved,

        /// <summary>
        /// Decision rejected - step failed compliance
        /// </summary>
        Rejected,

        /// <summary>
        /// Decision requires manual review
        /// </summary>
        RequiresManualReview,

        /// <summary>
        /// Decision approved with conditions
        /// </summary>
        ConditionalApproval,

        /// <summary>
        /// Decision expired and needs renewal
        /// </summary>
        Expired
    }

    /// <summary>
    /// Reference to evidence supporting a compliance decision
    /// </summary>
    public class EvidenceReference
    {
        /// <summary>
        /// Type of evidence (e.g., "ID_DOCUMENT", "BUSINESS_LICENSE", "BANK_STATEMENT")
        /// </summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>
        /// Reference identifier (e.g., file ID, URL, document number)
        /// </summary>
        public string ReferenceId { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when evidence was submitted (UTC)
        /// </summary>
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Verification status of this evidence
        /// </summary>
        public EvidenceVerificationStatus VerificationStatus { get; set; }

        /// <summary>
        /// Hash of the evidence data for integrity verification
        /// </summary>
        public string? DataHash { get; set; }

        /// <summary>
        /// Additional metadata about the evidence
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Verification status of evidence
    /// </summary>
    public enum EvidenceVerificationStatus
    {
        /// <summary>
        /// Evidence has been submitted but not yet verified
        /// </summary>
        Submitted,

        /// <summary>
        /// Evidence is currently being verified
        /// </summary>
        InReview,

        /// <summary>
        /// Evidence has been verified and accepted
        /// </summary>
        Verified,

        /// <summary>
        /// Evidence verification failed
        /// </summary>
        Rejected,

        /// <summary>
        /// Evidence has expired and needs renewal
        /// </summary>
        Expired
    }

    /// <summary>
    /// Request to create a new compliance decision
    /// </summary>
    public class CreateComplianceDecisionRequest
    {
        /// <summary>
        /// Organization identifier
        /// </summary>
        public string OrganizationId { get; set; } = string.Empty;

        /// <summary>
        /// Onboarding session identifier (optional, for session-specific decisions)
        /// </summary>
        public string? OnboardingSessionId { get; set; }

        /// <summary>
        /// The onboarding step being evaluated
        /// </summary>
        public OnboardingStep Step { get; set; }

        /// <summary>
        /// References to evidence documents supporting this decision
        /// </summary>
        public List<EvidenceReference> EvidenceReferences { get; set; } = new();

        /// <summary>
        /// Additional context for policy evaluation
        /// </summary>
        public Dictionary<string, object>? EvaluationContext { get; set; }

        /// <summary>
        /// Correlation ID for linking related operations
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Expiration duration in days for time-limited approvals (optional)
        /// </summary>
        public int? ExpirationDays { get; set; }

        /// <summary>
        /// Whether this decision requires periodic review
        /// </summary>
        public bool RequiresReview { get; set; }

        /// <summary>
        /// Review interval in days if periodic review is required
        /// </summary>
        public int? ReviewIntervalDays { get; set; }
    }

    /// <summary>
    /// Response after creating or updating a compliance decision
    /// </summary>
    public class ComplianceDecisionResponse : BaseResponse
    {
        /// <summary>
        /// The compliance decision that was created or updated
        /// </summary>
        public ComplianceDecision? Decision { get; set; }

        /// <summary>
        /// Detailed evaluation results from the policy engine
        /// </summary>
        public PolicyEvaluationResult? EvaluationResult { get; set; }
    }

    /// <summary>
    /// Result of policy evaluation
    /// </summary>
    public class PolicyEvaluationResult
    {
        /// <summary>
        /// Overall outcome of the evaluation
        /// </summary>
        public DecisionOutcome Outcome { get; set; }

        /// <summary>
        /// Policy rules that were evaluated
        /// </summary>
        public List<PolicyRuleEvaluation> RuleEvaluations { get; set; } = new();

        /// <summary>
        /// Overall reason for the decision
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Actions required if decision is rejected or conditional
        /// </summary>
        public List<string> RequiredActions { get; set; } = new();

        /// <summary>
        /// Estimated time to resolution if rejected
        /// </summary>
        public string? EstimatedResolutionTime { get; set; }
    }

    /// <summary>
    /// Evaluation result for a single policy rule
    /// </summary>
    public class PolicyRuleEvaluation
    {
        /// <summary>
        /// Policy rule identifier
        /// </summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable rule name
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this rule passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Detailed message explaining the evaluation result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level if rule failed
        /// </summary>
        public RuleSeverity? Severity { get; set; }

        /// <summary>
        /// Evidence used in this rule evaluation
        /// </summary>
        public List<string> EvidenceIds { get; set; } = new();
    }

    /// <summary>
    /// Severity level for policy rule failures
    /// </summary>
    public enum RuleSeverity
    {
        /// <summary>
        /// Informational - does not block progress
        /// </summary>
        Info,

        /// <summary>
        /// Warning - should be addressed but not blocking
        /// </summary>
        Warning,

        /// <summary>
        /// Error - blocks progress, must be resolved
        /// </summary>
        Error,

        /// <summary>
        /// Critical - immediate attention required
        /// </summary>
        Critical
    }

    /// <summary>
    /// Request to query compliance decisions
    /// </summary>
    public class QueryComplianceDecisionsRequest
    {
        /// <summary>
        /// Optional filter by organization ID
        /// </summary>
        public string? OrganizationId { get; set; }

        /// <summary>
        /// Optional filter by onboarding session ID
        /// </summary>
        public string? OnboardingSessionId { get; set; }

        /// <summary>
        /// Optional filter by onboarding step
        /// </summary>
        public OnboardingStep? Step { get; set; }

        /// <summary>
        /// Optional filter by decision outcome
        /// </summary>
        public DecisionOutcome? Outcome { get; set; }

        /// <summary>
        /// Optional filter by decision maker
        /// </summary>
        public string? DecisionMaker { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Include superseded decisions in results
        /// </summary>
        public bool IncludeSuperseded { get; set; } = false;

        /// <summary>
        /// Include expired decisions in results
        /// </summary>
        public bool IncludeExpired { get; set; } = false;

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response containing queried compliance decisions
    /// </summary>
    public class QueryComplianceDecisionsResponse : BaseResponse
    {
        /// <summary>
        /// List of compliance decisions
        /// </summary>
        public List<ComplianceDecision> Decisions { get; set; } = new();

        /// <summary>
        /// Total number of decisions matching the filter
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

        /// <summary>
        /// Summary statistics for the queried decisions
        /// </summary>
        public DecisionSummary? Summary { get; set; }
    }

    /// <summary>
    /// Summary statistics for compliance decisions
    /// </summary>
    public class DecisionSummary
    {
        /// <summary>
        /// Number of approved decisions
        /// </summary>
        public int ApprovedCount { get; set; }

        /// <summary>
        /// Number of rejected decisions
        /// </summary>
        public int RejectedCount { get; set; }

        /// <summary>
        /// Number of decisions requiring manual review
        /// </summary>
        public int RequiresReviewCount { get; set; }

        /// <summary>
        /// Number of pending decisions
        /// </summary>
        public int PendingCount { get; set; }

        /// <summary>
        /// Number of conditional approvals
        /// </summary>
        public int ConditionalApprovalCount { get; set; }

        /// <summary>
        /// Number of expired decisions
        /// </summary>
        public int ExpiredCount { get; set; }

        /// <summary>
        /// Average time to decision in hours
        /// </summary>
        public double? AverageDecisionTimeHours { get; set; }

        /// <summary>
        /// Most common rejection reasons
        /// </summary>
        public List<string> CommonRejectionReasons { get; set; } = new();
    }
}

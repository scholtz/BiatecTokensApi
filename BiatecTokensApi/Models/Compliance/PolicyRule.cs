namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents a policy rule used for compliance decision evaluation
    /// </summary>
    /// <remarks>
    /// Policy rules define the compliance requirements for onboarding steps.
    /// Rules can be code-based or configuration-based and support versioning.
    /// </remarks>
    public class PolicyRule
    {
        /// <summary>
        /// Unique identifier for the policy rule
        /// </summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable rule name
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what this rule checks
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The onboarding step this rule applies to
        /// </summary>
        public OnboardingStep ApplicableStep { get; set; }

        /// <summary>
        /// Category of the rule (e.g., "IDENTITY", "AML", "KYC", "JURISDICTION")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Severity level if this rule fails
        /// </summary>
        public RuleSeverity Severity { get; set; } = RuleSeverity.Error;

        /// <summary>
        /// Whether this rule is required or optional
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Version of this rule
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Whether this rule is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Effective date for this rule (UTC)
        /// </summary>
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Expiration date for this rule (UTC, null if no expiration)
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        /// <summary>
        /// Required evidence types for this rule
        /// </summary>
        public List<string> RequiredEvidenceTypes { get; set; } = new();

        /// <summary>
        /// Configuration parameters for this rule
        /// </summary>
        public Dictionary<string, object>? Configuration { get; set; }

        /// <summary>
        /// User-friendly message when rule passes
        /// </summary>
        public string PassMessage { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly message when rule fails
        /// </summary>
        public string FailMessage { get; set; } = string.Empty;

        /// <summary>
        /// Actions required to remediate if rule fails
        /// </summary>
        public List<string> RemediationActions { get; set; } = new();

        /// <summary>
        /// Estimated time to remediate in hours
        /// </summary>
        public int? EstimatedRemediationHours { get; set; }

        /// <summary>
        /// Tags for organizing and filtering rules
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Regulatory frameworks this rule helps satisfy (e.g., "MICA", "AML5", "FATF")
        /// </summary>
        public List<string> RegulatoryFrameworks { get; set; } = new();
    }

    /// <summary>
    /// Context information for policy evaluation
    /// </summary>
    public class PolicyEvaluationContext
    {
        /// <summary>
        /// Organization being evaluated
        /// </summary>
        public string OrganizationId { get; set; } = string.Empty;

        /// <summary>
        /// Onboarding session identifier
        /// </summary>
        public string? OnboardingSessionId { get; set; }

        /// <summary>
        /// Onboarding step being evaluated
        /// </summary>
        public OnboardingStep Step { get; set; }

        /// <summary>
        /// Evidence references provided for evaluation
        /// </summary>
        public List<EvidenceReference> Evidence { get; set; } = new();

        /// <summary>
        /// Additional contextual data for evaluation
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }

        /// <summary>
        /// User who initiated this evaluation
        /// </summary>
        public string Initiator { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction information (if applicable)
        /// </summary>
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Token type being issued (if step is TokenIssuanceAuthorization)
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Network for token deployment (if applicable)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Timestamp when evaluation was initiated (UTC)
        /// </summary>
        public DateTime EvaluationTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracking related operations
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Configuration for policy rules
    /// </summary>
    public class PolicyConfiguration
    {
        /// <summary>
        /// Version of the policy configuration
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// All policy rules organized by step
        /// </summary>
        public Dictionary<OnboardingStep, List<PolicyRule>> RulesByStep { get; set; } = new();

        /// <summary>
        /// Default expiration days for decisions
        /// </summary>
        public int DefaultExpirationDays { get; set; } = 365;

        /// <summary>
        /// Whether decisions require periodic review by default
        /// </summary>
        public bool DefaultRequiresReview { get; set; } = false;

        /// <summary>
        /// Default review interval in days
        /// </summary>
        public int DefaultReviewIntervalDays { get; set; } = 180;

        /// <summary>
        /// Whether to allow conditional approvals
        /// </summary>
        public bool AllowConditionalApprovals { get; set; } = true;

        /// <summary>
        /// Maximum number of evidence items per decision
        /// </summary>
        public int MaxEvidencePerDecision { get; set; } = 50;

        /// <summary>
        /// Whether to enable automatic policy rule matching
        /// </summary>
        public bool EnableAutomaticEvaluation { get; set; } = true;

        /// <summary>
        /// Metadata about this policy configuration
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Standard policy rule identifiers for common compliance checks
    /// </summary>
    public static class StandardPolicyRuleIds
    {
        // Organization Identity Verification
        public const string OrgIdentityDocumentRequired = "ORG_ID_DOC_001";
        public const string OrgIdentityVerificationComplete = "ORG_ID_VERIFY_001";
        
        // Business Registration
        public const string BusinessLicenseRequired = "BUS_REG_LIC_001";
        public const string BusinessRegistrationNumberValid = "BUS_REG_NUM_001";
        
        // Beneficial Ownership
        public const string BeneficialOwnerDisclosure = "BEN_OWN_DISC_001";
        public const string BeneficialOwnerVerification = "BEN_OWN_VERIFY_001";
        
        // KYC/KYB
        public const string KycDocumentationComplete = "KYC_DOC_001";
        public const string KybBusinessVerificationComplete = "KYB_BUS_001";
        
        // AML
        public const string AmlScreeningPassed = "AML_SCREEN_001";
        public const string SanctionsCheckPassed = "AML_SANCTION_001";
        public const string PepCheckPassed = "AML_PEP_001";
        
        // Jurisdictional
        public const string JurisdictionAllowed = "JUR_ALLOW_001";
        public const string JurisdictionCompliant = "JUR_COMPLY_001";
        
        // Token Issuance
        public const string TokenTypeAllowed = "TOKEN_TYPE_001";
        public const string TokenParametersValid = "TOKEN_PARAM_001";
        public const string TokenComplianceValid = "TOKEN_COMPLY_001";
        
        // Wallet Custody
        public const string CustodyArrangementVerified = "CUSTODY_VERIFY_001";
        public const string WalletSecurityAdequate = "WALLET_SEC_001";
        
        // Terms
        public const string TermsAccepted = "TERMS_ACCEPT_001";
        public const string PrivacyPolicyAccepted = "PRIVACY_ACCEPT_001";
        
        // Final Approval
        public const string AllStepsCompleted = "FINAL_STEPS_001";
        public const string ComplianceOfficerApproval = "FINAL_OFFICER_001";
    }

    /// <summary>
    /// Policy metrics for monitoring and reporting
    /// </summary>
    public class PolicyMetrics
    {
        /// <summary>
        /// Total number of evaluations
        /// </summary>
        public int TotalEvaluations { get; set; }

        /// <summary>
        /// Number of automatic approvals
        /// </summary>
        public int AutomaticApprovals { get; set; }

        /// <summary>
        /// Number of automatic rejections
        /// </summary>
        public int AutomaticRejections { get; set; }

        /// <summary>
        /// Number requiring manual review
        /// </summary>
        public int ManualReviewRequired { get; set; }

        /// <summary>
        /// Average evaluation time in milliseconds
        /// </summary>
        public double AverageEvaluationTimeMs { get; set; }

        /// <summary>
        /// Most failed rules
        /// </summary>
        public Dictionary<string, int> RuleFailureCounts { get; set; } = new();

        /// <summary>
        /// Policy version in use
        /// </summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>
        /// Last updated timestamp (UTC)
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

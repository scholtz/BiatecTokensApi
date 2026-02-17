namespace BiatecTokensApi.Models.LifecycleIntelligence
{
    /// <summary>
    /// Reference to evidence supporting an evaluation decision
    /// </summary>
    public class EvidenceReference
    {
        /// <summary>
        /// Unique evidence identifier
        /// </summary>
        public string EvidenceId { get; set; } = string.Empty;

        /// <summary>
        /// Type of evidence
        /// </summary>
        public EvidenceType Type { get; set; }

        /// <summary>
        /// Source system or service that generated the evidence
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// URI or path to retrieve the full evidence
        /// </summary>
        public string? ResourceUri { get; set; }

        /// <summary>
        /// Timestamp when evidence was collected
        /// </summary>
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when evidence was last validated
        /// </summary>
        public DateTime? ValidatedAt { get; set; }

        /// <summary>
        /// Hash of evidence data for integrity verification
        /// </summary>
        public string? DataHash { get; set; }

        /// <summary>
        /// Brief summary of the evidence content
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Metadata about evidence collection
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Expiration timestamp (if evidence has limited validity)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether this evidence is still valid
        /// </summary>
        public bool IsValid => !ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Type of evidence
    /// </summary>
    public enum EvidenceType
    {
        /// <summary>
        /// Entitlement or subscription check result
        /// </summary>
        EntitlementCheck,

        /// <summary>
        /// Account readiness assessment
        /// </summary>
        AccountReadiness,

        /// <summary>
        /// KYC/AML verification result
        /// </summary>
        KycVerification,

        /// <summary>
        /// Compliance decision or policy evaluation
        /// </summary>
        ComplianceDecision,

        /// <summary>
        /// Blockchain transaction or state proof
        /// </summary>
        BlockchainProof,

        /// <summary>
        /// Integration health check result
        /// </summary>
        IntegrationHealth,

        /// <summary>
        /// User action or attestation
        /// </summary>
        UserAction,

        /// <summary>
        /// System-generated audit log
        /// </summary>
        AuditLog,

        /// <summary>
        /// Third-party verification or certification
        /// </summary>
        ThirdPartyVerification,

        /// <summary>
        /// Risk assessment or scoring result
        /// </summary>
        RiskAssessment
    }

    /// <summary>
    /// Response containing evidence details
    /// </summary>
    public class EvidenceRetrievalResponse
    {
        /// <summary>
        /// Whether the evidence retrieval was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Evidence reference details
        /// </summary>
        public EvidenceReference? Evidence { get; set; }

        /// <summary>
        /// Full evidence content (if requested and available)
        /// </summary>
        public string? ContentJson { get; set; }

        /// <summary>
        /// Error message if retrieval failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Related evaluation ID
        /// </summary>
        public string? EvaluationId { get; set; }
    }
}

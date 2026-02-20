namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// Severity level for health and policy assessments
    /// </summary>
    public enum AssessmentSeverity
    {
        /// <summary>Healthy - no issues detected</summary>
        Healthy = 0,
        /// <summary>Low severity - informational issue</summary>
        Low = 1,
        /// <summary>Medium severity - action recommended</summary>
        Medium = 2,
        /// <summary>High severity - action required</summary>
        High = 3,
        /// <summary>Critical severity - immediate action required</summary>
        Critical = 4
    }

    /// <summary>
    /// Status of a policy dimension evaluation
    /// </summary>
    public enum PolicyStatus
    {
        /// <summary>Policy dimension passes all checks</summary>
        Pass,
        /// <summary>Policy dimension has warnings</summary>
        Warning,
        /// <summary>Policy dimension has failures</summary>
        Fail,
        /// <summary>Evaluation could not be completed due to data unavailability</summary>
        Degraded
    }

    /// <summary>
    /// Category of a token-affecting event
    /// </summary>
    public enum TokenEventCategory
    {
        /// <summary>Token minting operation</summary>
        Mint,
        /// <summary>Token burning operation</summary>
        Burn,
        /// <summary>Ownership or authority transfer</summary>
        OwnershipChange,
        /// <summary>Metadata update</summary>
        MetadataUpdate,
        /// <summary>Treasury movement</summary>
        TreasuryMovement,
        /// <summary>Deployment event</summary>
        Deployment,
        /// <summary>Governance action</summary>
        Governance,
        /// <summary>Compliance-related event</summary>
        Compliance,
        /// <summary>Security-related event</summary>
        Security,
        /// <summary>Other uncategorized event</summary>
        Other
    }

    /// <summary>
    /// Impact level of a token-affecting event
    /// </summary>
    public enum EventImpact
    {
        /// <summary>Informational - no operational impact</summary>
        Informational,
        /// <summary>Low impact</summary>
        Low,
        /// <summary>Moderate impact</summary>
        Moderate,
        /// <summary>High impact</summary>
        High,
        /// <summary>Critical impact</summary>
        Critical
    }

    /// <summary>
    /// Overall health status of a token
    /// </summary>
    public enum TokenHealthStatus
    {
        /// <summary>Token is healthy</summary>
        Healthy,
        /// <summary>Token has minor issues</summary>
        Degraded,
        /// <summary>Token has significant issues</summary>
        Unhealthy,
        /// <summary>Health could not be determined</summary>
        Unknown
    }
}

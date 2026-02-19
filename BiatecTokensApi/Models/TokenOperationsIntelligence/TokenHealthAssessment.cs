namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// Result of evaluating a single policy dimension
    /// </summary>
    public class PolicyAssuranceResult
    {
        /// <summary>
        /// Unique identifier for this policy dimension (e.g., "MintAuthority")
        /// </summary>
        public string DimensionId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the policy dimension
        /// </summary>
        public string DimensionName { get; set; } = string.Empty;

        /// <summary>
        /// Evaluation status for this dimension
        /// </summary>
        public PolicyStatus Status { get; set; }

        /// <summary>
        /// Severity when status is Warning or Fail
        /// </summary>
        public AssessmentSeverity Severity { get; set; }

        /// <summary>
        /// Human-readable description of the evaluation outcome
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Machine-readable code identifying the specific finding
        /// </summary>
        public string? FindingCode { get; set; }

        /// <summary>
        /// Human-readable remediation hint when status is not Pass
        /// </summary>
        public string? RemediationHint { get; set; }

        /// <summary>
        /// Specific details or evidence supporting this evaluation
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }

        /// <summary>
        /// UTC timestamp when this evaluation was performed
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this result came from cached data
        /// </summary>
        public bool FromCache { get; set; }
    }

    /// <summary>
    /// Aggregated token health assessment from all policy dimensions
    /// </summary>
    public class TokenHealthAssessment
    {
        /// <summary>
        /// Overall health status
        /// </summary>
        public TokenHealthStatus OverallStatus { get; set; }

        /// <summary>
        /// Composite health score (0.0 = critical, 1.0 = perfect)
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Individual policy dimension results
        /// </summary>
        public List<PolicyAssuranceResult> PolicyResults { get; set; } = new();

        /// <summary>
        /// Number of passing policy dimensions
        /// </summary>
        public int PassingDimensions => PolicyResults.Count(r => r.Status == PolicyStatus.Pass);

        /// <summary>
        /// Number of warning policy dimensions
        /// </summary>
        public int WarningDimensions => PolicyResults.Count(r => r.Status == PolicyStatus.Warning);

        /// <summary>
        /// Number of failing policy dimensions
        /// </summary>
        public int FailingDimensions => PolicyResults.Count(r => r.Status == PolicyStatus.Fail);

        /// <summary>
        /// Number of degraded (unavailable) policy dimensions
        /// </summary>
        public int DegradedDimensions => PolicyResults.Count(r => r.Status == PolicyStatus.Degraded);

        /// <summary>
        /// Whether health assessment is operating in degraded mode due to upstream failures
        /// </summary>
        public bool IsPartialResult { get; set; }

        /// <summary>
        /// Explanation of what data is missing when IsPartialResult is true
        /// </summary>
        public string? DegradedReason { get; set; }
    }
}

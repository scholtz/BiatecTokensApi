namespace BiatecTokensApi.Models.OperationalIntelligence
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enums
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalized compliance checkpoint state.
    /// Stable codes safe for frontend rendering and analytics.
    /// </summary>
    public enum ComplianceCheckpointState
    {
        /// <summary>Checkpoint has not yet been started</summary>
        Pending = 0,
        /// <summary>Checkpoint is under active review</summary>
        InReview = 1,
        /// <summary>Checkpoint has been fully satisfied</summary>
        Satisfied = 2,
        /// <summary>Checkpoint has failed and requires remediation</summary>
        Failed = 3,
        /// <summary>Checkpoint is blocked by an unresolved dependency</summary>
        Blocked = 4
    }

    /// <summary>
    /// Severity of an operation timeline entry.
    /// </summary>
    public enum OperationSeverity
    {
        /// <summary>Informational event, no action required</summary>
        Info = 0,
        /// <summary>Warning – monitoring recommended</summary>
        Warning = 1,
        /// <summary>Error – action needed</summary>
        Error = 2,
        /// <summary>Critical – immediate action required</summary>
        Critical = 3
    }

    /// <summary>
    /// Bounded set of operational risk categories.
    /// All domain errors map deterministically to one of these.
    /// </summary>
    public enum OperationalRiskCategory
    {
        /// <summary>No significant risk detected</summary>
        None = 0,
        /// <summary>Authentication or authorisation issue</summary>
        AuthorizationRisk = 1,
        /// <summary>Blockchain connectivity or network issue</summary>
        NetworkRisk = 2,
        /// <summary>Smart-contract execution failure</summary>
        ContractRisk = 3,
        /// <summary>Regulatory or KYC compliance gap</summary>
        ComplianceRisk = 4,
        /// <summary>Data integrity or schema validation failure</summary>
        DataIntegrityRisk = 5,
        /// <summary>Operational timeout or transient infrastructure failure</summary>
        InfrastructureRisk = 6,
        /// <summary>Security policy or access-control violation</summary>
        SecurityRisk = 7,
        /// <summary>Business-rule or policy conflict</summary>
        PolicyRisk = 8
    }

    /// <summary>
    /// Confidence level for a risk or intelligence assessment.
    /// </summary>
    public enum ConfidenceLevel
    {
        /// <summary>Less than 50 % confidence – insufficient data</summary>
        Low = 0,
        /// <summary>50–79 % confidence – partial evidence</summary>
        Medium = 1,
        /// <summary>80–94 % confidence – strong evidence</summary>
        High = 2,
        /// <summary>95 %+ confidence – definitive evidence</summary>
        Definitive = 3
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Timeline
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Single entry in a canonical operation timeline.
    /// </summary>
    public class OperationTimelineEntry
    {
        /// <summary>Unique entry identifier (stable across retries)</summary>
        public string EntryId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Correlation ID linking this entry to related backend evidence</summary>
        public string CorrelationId { get; set; } = string.Empty;
        /// <summary>UTC timestamp of the state transition</summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        /// <summary>Previous deployment/operation status (null for the initial entry)</summary>
        public string? FromState { get; set; }
        /// <summary>Resulting deployment/operation status</summary>
        public string ToState { get; set; } = string.Empty;
        /// <summary>Severity of this transition</summary>
        public OperationSeverity Severity { get; set; } = OperationSeverity.Info;
        /// <summary>Business-readable description of what happened</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>Recommended next action (if any)</summary>
        public string? RecommendedAction { get; set; }
        /// <summary>Stable error/event code (empty when no error)</summary>
        public string EventCode { get; set; } = string.Empty;
        /// <summary>Actor who triggered this transition (address or system name)</summary>
        public string Actor { get; set; } = string.Empty;
        /// <summary>Optional additional metadata (safe for external exposure)</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Request for an operation timeline.
    /// </summary>
    public class OperationTimelineRequest
    {
        /// <summary>Deployment ID to retrieve the timeline for</summary>
        public string DeploymentId { get; set; } = string.Empty;
        /// <summary>Optional cursor for idempotent pagination</summary>
        public string? AfterEntryId { get; set; }
        /// <summary>Maximum number of entries to return (1-200, default 50)</summary>
        public int Limit { get; set; } = 50;
    }

    /// <summary>
    /// Response containing an ordered operation timeline.
    /// </summary>
    public class OperationTimelineResponse
    {
        /// <summary>Whether the request was processed successfully</summary>
        public bool Success { get; set; }
        /// <summary>Deployment ID these entries belong to</summary>
        public string DeploymentId { get; set; } = string.Empty;
        /// <summary>Chronologically ordered timeline entries (oldest first)</summary>
        public List<OperationTimelineEntry> Entries { get; set; } = new();
        /// <summary>Total available entries (may exceed returned count)</summary>
        public int TotalEntries { get; set; }
        /// <summary>Whether additional entries are available</summary>
        public bool HasMore { get; set; }
        /// <summary>Cursor value for the next page (null when no more entries)</summary>
        public string? NextCursor { get; set; }
        /// <summary>UTC timestamp when this response was generated</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Correlation ID for this response</summary>
        public string CorrelationId { get; set; } = string.Empty;
        /// <summary>Error message (populated when Success = false)</summary>
        public string? ErrorMessage { get; set; }
        /// <summary>Stable error code (populated when Success = false)</summary>
        public string? ErrorCode { get; set; }
        /// <summary>Actionable remediation hint (populated when Success = false)</summary>
        public string? RemediationHint { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Compliance Checkpoint
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single compliance checkpoint with normalized state and guidance.
    /// </summary>
    public class ComplianceCheckpoint
    {
        /// <summary>Stable checkpoint identifier (safe for frontend and analytics)</summary>
        public string CheckpointId { get; set; } = string.Empty;
        /// <summary>Human-readable checkpoint name</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Normalized state of this checkpoint</summary>
        public ComplianceCheckpointState State { get; set; }
        /// <summary>Business-readable explanation of the current state</summary>
        public string Explanation { get; set; } = string.Empty;
        /// <summary>Recommended action to advance or resolve this checkpoint</summary>
        public string? RecommendedAction { get; set; }
        /// <summary>UTC timestamp of the last state change</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Whether this checkpoint blocks deployment progression</summary>
        public bool IsBlocking { get; set; }
        /// <summary>Stable category code for grouping in UI</summary>
        public string Category { get; set; } = string.Empty;
        /// <summary>Correlation ID linking this checkpoint to evidence</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for a compliance checkpoint summary.
    /// </summary>
    public class ComplianceCheckpointRequest
    {
        /// <summary>Deployment or asset identifier</summary>
        public string DeploymentId { get; set; } = string.Empty;
        /// <summary>Whether to include non-blocking checkpoints (default true)</summary>
        public bool IncludeNonBlocking { get; set; } = true;
    }

    /// <summary>
    /// Response containing normalized compliance checkpoint states.
    /// </summary>
    public class ComplianceCheckpointResponse
    {
        /// <summary>Whether the request was processed successfully</summary>
        public bool Success { get; set; }
        /// <summary>Deployment or asset identifier</summary>
        public string DeploymentId { get; set; } = string.Empty;
        /// <summary>List of compliance checkpoints</summary>
        public List<ComplianceCheckpoint> Checkpoints { get; set; } = new();
        /// <summary>Number of blocking checkpoints requiring action</summary>
        public int BlockingCount { get; set; }
        /// <summary>Number of satisfied checkpoints</summary>
        public int SatisfiedCount { get; set; }
        /// <summary>Overall compliance posture (derived from checkpoint states)</summary>
        public string OverallPosture { get; set; } = string.Empty;
        /// <summary>UTC timestamp when this summary was generated</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Correlation ID for this response</summary>
        public string CorrelationId { get; set; } = string.Empty;
        /// <summary>Error message (populated when Success = false)</summary>
        public string? ErrorMessage { get; set; }
        /// <summary>Stable error code (populated when Success = false)</summary>
        public string? ErrorCode { get; set; }
        /// <summary>Actionable remediation hint (populated when Success = false)</summary>
        public string? RemediationHint { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Risk Signal
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Operational risk signal with bounded category and stable severity/confidence fields.
    /// </summary>
    public class OperationalRiskSignal
    {
        /// <summary>Unique signal identifier (stable across retries)</summary>
        public string SignalId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Bounded operational risk category</summary>
        public OperationalRiskCategory Category { get; set; }
        /// <summary>Severity of this risk signal</summary>
        public OperationSeverity Severity { get; set; }
        /// <summary>Confidence in this assessment</summary>
        public ConfidenceLevel Confidence { get; set; }
        /// <summary>Stable signal code for machine processing</summary>
        public string SignalCode { get; set; } = string.Empty;
        /// <summary>Business-readable description (no internal details)</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>Actionable remediation hint</summary>
        public string RemediationHint { get; set; } = string.Empty;
        /// <summary>UTC timestamp of this assessment</summary>
        public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Correlation ID linking to backend evidence</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stakeholder Report
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Privacy-safe stakeholder report payload for non-technical summaries.
    /// </summary>
    public class StakeholderReportPayload
    {
        /// <summary>Deployment or asset identifier</summary>
        public string DeploymentId { get; set; } = string.Empty;
        /// <summary>Token name (safe for external publication)</summary>
        public string? TokenName { get; set; }
        /// <summary>Token symbol</summary>
        public string? TokenSymbol { get; set; }
        /// <summary>Issuance progress summary (e.g., "Deployment confirmed – metadata indexing")</summary>
        public string IssuanceProgress { get; set; } = string.Empty;
        /// <summary>Overall compliance posture (e.g., "All checkpoints satisfied")</summary>
        public string CompliancePosture { get; set; } = string.Empty;
        /// <summary>Number of unresolved blockers requiring stakeholder attention</summary>
        public int UnresolvedBlockers { get; set; }
        /// <summary>Highest-priority recommended action (null when no action needed)</summary>
        public string? PrimaryRecommendedAction { get; set; }
        /// <summary>Current deployment state (human-readable)</summary>
        public string CurrentState { get; set; } = string.Empty;
        /// <summary>UTC timestamp when this report was generated</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Correlation ID for audit tracing</summary>
        public string CorrelationId { get; set; } = string.Empty;
        /// <summary>Risk signals summarised for non-technical stakeholders</summary>
        public List<OperationalRiskSignal> RiskSignals { get; set; } = new();
    }

    /// <summary>
    /// Request for a stakeholder report.
    /// </summary>
    public class StakeholderReportRequest
    {
        /// <summary>Deployment ID to generate the report for</summary>
        public string DeploymentId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response wrapping a stakeholder report payload.
    /// </summary>
    public class StakeholderReportResponse
    {
        /// <summary>Whether the request was processed successfully</summary>
        public bool Success { get; set; }
        /// <summary>The stakeholder report payload (null when Success = false)</summary>
        public StakeholderReportPayload? Report { get; set; }
        /// <summary>Error message (populated when Success = false)</summary>
        public string? ErrorMessage { get; set; }
        /// <summary>Stable error code (populated when Success = false)</summary>
        public string? ErrorCode { get; set; }
        /// <summary>Actionable remediation hint (populated when Success = false)</summary>
        public string? RemediationHint { get; set; }
    }
}

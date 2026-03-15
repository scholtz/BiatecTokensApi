namespace BiatecTokensApi.Models.ComplianceCaseManagement
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    /// <summary>Lifecycle state of a compliance case.</summary>
    public enum ComplianceCaseState
    {
        /// <summary>Case has been created and is awaiting initial processing.</summary>
        Intake,
        /// <summary>Case is waiting for required evidence to be submitted.</summary>
        EvidencePending,
        /// <summary>Case is under active review.</summary>
        UnderReview,
        /// <summary>Case has been escalated due to a compliance concern.</summary>
        Escalated,
        /// <summary>Case has open remediation tasks that must be resolved.</summary>
        Remediating,
        /// <summary>Case has been approved — terminal state.</summary>
        Approved,
        /// <summary>Case has been rejected — terminal state.</summary>
        Rejected,
        /// <summary>Case evidence has expired and is no longer valid.</summary>
        Stale,
        /// <summary>Case is blocked pending manual intervention.</summary>
        Blocked
    }

    /// <summary>Priority level for a compliance case.</summary>
    public enum CasePriority { Low, Medium, High, Critical }

    /// <summary>Classification of what a compliance case is evaluating.</summary>
    public enum CaseType { InvestorEligibility, LaunchPackage, OngoingMonitoring }

    /// <summary>Status of an individual remediation task within a case.</summary>
    public enum RemediationTaskStatus { Open, InProgress, Resolved, Dismissed }

    /// <summary>Type of escalation raised against a case.</summary>
    public enum EscalationType
    {
        SanctionsHit,
        WatchlistMatch,
        JurisdictionConflict,
        AdverseMedia,
        ManualEscalation
    }

    /// <summary>Review status of a case escalation.</summary>
    public enum EscalationStatus { Open, UnderReview, Resolved, Escalated }

    /// <summary>Type of event recorded in the case timeline.</summary>
    public enum CaseTimelineEventType
    {
        CaseCreated,
        StateTransition,
        EvidenceAdded,
        EvidenceStale,
        EscalationRaised,
        EscalationResolved,
        RemediationTaskAdded,
        RemediationTaskResolved,
        ReviewerAssigned,
        ReviewerNoteAdded,
        ReadinessChanged,
        CaseExported
    }

    /// <summary>Validity status of a piece of evidence attached to a case.</summary>
    public enum CaseEvidenceStatus { Valid, Pending, Stale, Missing, Rejected }

    /// <summary>Severity level for a remediation task blocker.</summary>
    public enum EvidenceIssueSeverityLevel { Low, Medium, High, Critical }

    // ── Aggregates ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Root aggregate representing a compliance case tracking all evidence,
    /// escalations, remediation tasks and timeline for a subject.
    /// </summary>
    public class ComplianceCase
    {
        /// <summary>Unique identifier for this case.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Issuer scoping this case.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Identifier of the subject being evaluated (investor, token, etc.).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Classification of this case.</summary>
        public CaseType Type { get; set; }

        /// <summary>Current lifecycle state.</summary>
        public ComplianceCaseState State { get; set; }

        /// <summary>Priority assigned to this case.</summary>
        public CasePriority Priority { get; set; }

        /// <summary>User ID of the reviewer currently assigned.</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Actor who created this case.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when the case was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Timestamp when the case was last modified.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Jurisdiction code relevant to this case (e.g., "US", "EU").</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Links to ComplianceOrchestration decisionIds associated with this case.</summary>
        public List<string> LinkedDecisionIds { get; set; } = new();

        /// <summary>Human-readable reason the case was closed.</summary>
        public string? ClosureReason { get; set; }

        /// <summary>Timestamp when the case was closed (Approved or Rejected).</summary>
        public DateTimeOffset? ClosedAt { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional external reference (e.g., third-party case ID).</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Timestamp when the evidence bundle expires.</summary>
        public DateTimeOffset? EvidenceExpiresAt { get; set; }

        /// <summary>True when the evidence bundle has passed its expiry date.</summary>
        public bool IsEvidenceStale { get; set; }

        /// <summary>Evidence summaries attached to this case.</summary>
        public List<CaseEvidenceSummary> EvidenceSummaries { get; set; } = new();

        /// <summary>Remediation tasks open against this case.</summary>
        public List<RemediationTask> RemediationTasks { get; set; } = new();

        /// <summary>Escalations raised against this case.</summary>
        public List<CaseEscalation> Escalations { get; set; } = new();

        /// <summary>Immutable audit trail of events for this case.</summary>
        public List<CaseTimelineEntry> Timeline { get; set; } = new();
    }

    /// <summary>
    /// Normalized summary of a piece of evidence attached to a case.
    /// Does not contain raw provider payloads.
    /// </summary>
    public class CaseEvidenceSummary
    {
        /// <summary>Unique identifier for this evidence record.</summary>
        public string EvidenceId { get; set; } = string.Empty;

        /// <summary>Case this evidence belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Category of evidence: "KYC", "AML", "Jurisdiction", "Identity", etc.</summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>Validity status of this evidence.</summary>
        public CaseEvidenceStatus Status { get; set; }

        /// <summary>Name of the provider that supplied this evidence.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-side reference ID for this evidence record.</summary>
        public string? ProviderReference { get; set; }

        /// <summary>When the evidence was captured by the provider.</summary>
        public DateTimeOffset? CapturedAt { get; set; }

        /// <summary>When this evidence expires.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>True when the evidence has passed its expiry date.</summary>
        public bool IsExpired { get; set; }

        /// <summary>Human-readable summary of the evidence result.</summary>
        public string? Summary { get; set; }

        /// <summary>Normalized key-value attributes from the evidence (no raw payloads).</summary>
        public Dictionary<string, string> NormalizedAttributes { get; set; } = new();

        /// <summary>True when this evidence is blocking case readiness.</summary>
        public bool IsBlockingReadiness { get; set; }

        /// <summary>Explanation of why this evidence is blocking readiness.</summary>
        public string? BlockingReason { get; set; }

        /// <summary>Actor who added this evidence.</summary>
        public string AddedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when this evidence was added to the case.</summary>
        public DateTimeOffset AddedAt { get; set; }
    }

    /// <summary>An actionable remediation task that must be resolved before a case can progress.</summary>
    public class RemediationTask
    {
        /// <summary>Unique identifier for this task.</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>Case this task belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Short title of the remediation action required.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of what needs to be done.</summary>
        public string? Description { get; set; }

        /// <summary>User ID of the person responsible for resolving this task.</summary>
        public string? OwnerId { get; set; }

        /// <summary>Deadline for completing this task.</summary>
        public DateTimeOffset? DueAt { get; set; }

        /// <summary>Current status of this task.</summary>
        public RemediationTaskStatus Status { get; set; }

        /// <summary>True when this task must be resolved before the case can be approved.</summary>
        public bool IsBlockingCase { get; set; }

        /// <summary>Notes provided when the task was resolved or dismissed.</summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>Actor who created this task.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when this task was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Timestamp when this task was resolved or dismissed.</summary>
        public DateTimeOffset? ResolvedAt { get; set; }

        /// <summary>Severity level indicating urgency of the blocker.</summary>
        public EvidenceIssueSeverityLevel BlockerSeverity { get; set; }
    }

    /// <summary>An escalation raised against a case requiring compliance review.</summary>
    public class CaseEscalation
    {
        /// <summary>Unique identifier for this escalation.</summary>
        public string EscalationId { get; set; } = string.Empty;

        /// <summary>Case this escalation belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Type/reason for this escalation.</summary>
        public EscalationType Type { get; set; }

        /// <summary>Current review status of this escalation.</summary>
        public EscalationStatus Status { get; set; }

        /// <summary>Human-readable description of the escalation reason.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Name of the screening provider that surfaced this hit.</summary>
        public string? ScreeningSource { get; set; }

        /// <summary>Categories matched by the screening hit (e.g., "Sanctions", "PEP").</summary>
        public List<string> MatchedCategories { get; set; } = new();

        /// <summary>Provider-reported confidence score (0.0–1.0) for the match.</summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>Actor who raised this escalation.</summary>
        public string RaisedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when this escalation was raised.</summary>
        public DateTimeOffset RaisedAt { get; set; }

        /// <summary>Actor who reviewed or resolved this escalation.</summary>
        public string? ReviewedBy { get; set; }

        /// <summary>Timestamp when this escalation was reviewed.</summary>
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Notes provided when this escalation was resolved.</summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>True when this escalation requires a manual compliance review.</summary>
        public bool RequiresManualReview { get; set; }
    }

    /// <summary>An immutable audit trail entry recording a significant event in a case's lifecycle.</summary>
    public class CaseTimelineEntry
    {
        /// <summary>Unique identifier for this timeline entry.</summary>
        public string EntryId { get; set; } = string.Empty;

        /// <summary>Case this entry belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Type of event recorded.</summary>
        public CaseTimelineEventType EventType { get; set; }

        /// <summary>When the event occurred.</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Actor who caused this event.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Display name of the actor.</summary>
        public string? ActorDisplayName { get; set; }

        /// <summary>State before the transition (only for StateTransition events).</summary>
        public ComplianceCaseState? FromState { get; set; }

        /// <summary>State after the transition (only for StateTransition events).</summary>
        public ComplianceCaseState? ToState { get; set; }

        /// <summary>Human-readable description of this event.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Additional structured metadata about the event.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>Evaluated readiness summary for a compliance case (fail-closed semantics).</summary>
    public class CaseReadinessSummary
    {
        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>True when the case has no blocking issues and is ready to proceed.</summary>
        public bool IsReady { get; set; }

        /// <summary>True when required data is absent and readiness is failed-closed.</summary>
        public bool FailedClosed { get; set; }

        /// <summary>Issues that must be resolved before the case can be approved.</summary>
        public List<string> BlockingIssues { get; set; } = new();

        /// <summary>Non-blocking warnings that should be reviewed.</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Evidence types that are missing from the case.</summary>
        public List<string> MissingEvidence { get; set; } = new();

        /// <summary>Count of open remediation tasks on the case.</summary>
        public int OpenRemediationTasks { get; set; }

        /// <summary>Count of open escalations that require manual review.</summary>
        public int CriticalEscalations { get; set; }

        /// <summary>True when any evidence on the case has become stale.</summary>
        public bool HasStaleEvidence { get; set; }

        /// <summary>Human-readable explanation of the readiness evaluation result.</summary>
        public string? ReadinessExplanation { get; set; }

        /// <summary>Timestamp when this readiness summary was evaluated.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    // ── Request / Response types ───────────────────────────────────────────────

    /// <summary>Request to create a new compliance case.</summary>
    public class CreateComplianceCaseRequest
    {
        /// <summary>Issuer scoping this case.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Identifier of the subject being evaluated.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Classification of this case.</summary>
        public CaseType Type { get; set; }

        /// <summary>Initial priority for this case.</summary>
        public CasePriority Priority { get; set; } = CasePriority.Medium;

        /// <summary>Jurisdiction relevant to this case.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Optional external reference for cross-system linking.</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional links to existing compliance decision IDs.</summary>
        public List<string>? LinkedDecisionIds { get; set; }
    }

    /// <summary>Response returned when a compliance case is created.</summary>
    public class CreateComplianceCaseResponse
    {
        /// <summary>True when the case was successfully created or already existed (idempotent).</summary>
        public bool Success { get; set; }

        /// <summary>The created or existing case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>True when an existing active case was returned instead of creating a new one.</summary>
        public bool WasIdempotent { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to update mutable fields on a compliance case.</summary>
    public class UpdateComplianceCaseRequest
    {
        /// <summary>New priority for this case.</summary>
        public CasePriority? Priority { get; set; }

        /// <summary>Reviewer to assign to this case.</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Updated jurisdiction for this case.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Updated external reference.</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Additional decision IDs to link to this case.</summary>
        public List<string>? AdditionalLinkedDecisionIds { get; set; }
    }

    /// <summary>Response returned when a compliance case is updated or transitioned.</summary>
    public class UpdateComplianceCaseResponse
    {
        /// <summary>True when the update succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The updated case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to transition a case to a new lifecycle state.</summary>
    public class TransitionCaseStateRequest
    {
        /// <summary>The target state to transition the case to.</summary>
        public ComplianceCaseState NewState { get; set; }

        /// <summary>Optional reason for this state transition.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Filter and pagination parameters for listing compliance cases.</summary>
    public class ListComplianceCasesRequest
    {
        /// <summary>Filter by lifecycle state.</summary>
        public ComplianceCaseState? State { get; set; }

        /// <summary>Filter by priority.</summary>
        public CasePriority? Priority { get; set; }

        /// <summary>Filter by assigned reviewer.</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Filter by issuer.</summary>
        public string? IssuerId { get; set; }

        /// <summary>Filter by jurisdiction.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>When true, return only cases with stale evidence.</summary>
        public bool? HasStaleEvidence { get; set; }

        /// <summary>Filter by case type.</summary>
        public CaseType? Type { get; set; }

        /// <summary>1-based page number.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Number of results per page (max 100).</summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Paginated list of compliance cases.</summary>
    public class ListComplianceCasesResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Matched cases for the current page.</summary>
        public List<ComplianceCase> Cases { get; set; } = new();

        /// <summary>Total number of cases matching the filter.</summary>
        public int TotalCount { get; set; }

        /// <summary>Current page number.</summary>
        public int Page { get; set; }

        /// <summary>Page size used for this response.</summary>
        public int PageSize { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response returned when retrieving a single compliance case.</summary>
    public class GetComplianceCaseResponse
    {
        /// <summary>True when the case was found and returned.</summary>
        public bool Success { get; set; }

        /// <summary>The retrieved case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to add evidence to a compliance case.</summary>
    public class AddEvidenceRequest
    {
        /// <summary>Category of evidence being added (e.g., "KYC", "AML").</summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>Initial status of this evidence.</summary>
        public CaseEvidenceStatus Status { get; set; } = CaseEvidenceStatus.Valid;

        /// <summary>Name of the provider that supplied this evidence.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-side reference ID.</summary>
        public string? ProviderReference { get; set; }

        /// <summary>When the evidence was captured by the provider.</summary>
        public DateTimeOffset? CapturedAt { get; set; }

        /// <summary>When this evidence expires.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Human-readable summary of the evidence result.</summary>
        public string? Summary { get; set; }

        /// <summary>Normalized key-value attributes.</summary>
        public Dictionary<string, string>? NormalizedAttributes { get; set; }

        /// <summary>True when this evidence is blocking case readiness.</summary>
        public bool IsBlockingReadiness { get; set; }

        /// <summary>Explanation of why this evidence is blocking readiness.</summary>
        public string? BlockingReason { get; set; }
    }

    /// <summary>Request to add a remediation task to a compliance case.</summary>
    public class AddRemediationTaskRequest
    {
        /// <summary>Short title of the action required.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of what needs to be done.</summary>
        public string? Description { get; set; }

        /// <summary>User ID of the person responsible.</summary>
        public string? OwnerId { get; set; }

        /// <summary>Deadline for completing this task.</summary>
        public DateTimeOffset? DueAt { get; set; }

        /// <summary>True when this task must be resolved before the case can be approved.</summary>
        public bool IsBlockingCase { get; set; }

        /// <summary>Severity level of this blocker.</summary>
        public EvidenceIssueSeverityLevel BlockerSeverity { get; set; } = EvidenceIssueSeverityLevel.Medium;
    }

    /// <summary>Request to resolve or dismiss a remediation task.</summary>
    public class ResolveRemediationTaskRequest
    {
        /// <summary>Resolution status (Resolved or Dismissed).</summary>
        public RemediationTaskStatus Status { get; set; } = RemediationTaskStatus.Resolved;

        /// <summary>Notes about how the task was resolved.</summary>
        public string? ResolutionNotes { get; set; }
    }

    /// <summary>Request to raise an escalation on a compliance case.</summary>
    public class AddEscalationRequest
    {
        /// <summary>Type of escalation.</summary>
        public EscalationType Type { get; set; }

        /// <summary>Human-readable description of the escalation reason.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Name of the screening provider that surfaced this hit.</summary>
        public string? ScreeningSource { get; set; }

        /// <summary>Categories matched by the screening hit.</summary>
        public List<string>? MatchedCategories { get; set; }

        /// <summary>Provider-reported confidence score (0.0–1.0).</summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>True when this escalation requires manual compliance review.</summary>
        public bool RequiresManualReview { get; set; } = true;
    }

    /// <summary>Request to resolve an escalation on a compliance case.</summary>
    public class ResolveEscalationRequest
    {
        /// <summary>Notes about how the escalation was resolved.</summary>
        public string? ResolutionNotes { get; set; }
    }

    /// <summary>Chronological timeline of events for a compliance case.</summary>
    public class CaseTimelineResponse
    {
        /// <summary>True when the timeline was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Timeline entries in chronological order.</summary>
        public List<CaseTimelineEntry> Entries { get; set; } = new();

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response wrapping a <see cref="CaseReadinessSummary"/>.</summary>
    public class CaseReadinessSummaryResponse
    {
        /// <summary>True when the readiness summary was evaluated successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The evaluated readiness summary.</summary>
        public CaseReadinessSummary? Summary { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }
}

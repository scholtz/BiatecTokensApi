namespace BiatecTokensApi.Models.ScheduledReporting
{
    // ── Audience type ─────────────────────────────────────────────────────────

    /// <summary>
    /// Target audience for a compliance report. Governs framing, required evidence domains,
    /// and delivery routing preferences.
    /// </summary>
    public enum ReportingAudienceType
    {
        /// <summary>Full internal compliance operations report with remediation details.</summary>
        InternalCompliance,
        /// <summary>Executive-level summary focused on key status indicators and risk posture.</summary>
        ExecutiveSummary,
        /// <summary>Third-party auditor package with evidence lineage and review history.</summary>
        ExternalAuditor,
        /// <summary>Formal regulatory submission package with strict canonical evidence.</summary>
        RegulatorySubmission,
        /// <summary>Board-level governance presentation with strategic compliance posture.</summary>
        BoardPresentation
    }

    // ── Cadence ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reporting cadence defining how often a report should be generated.
    /// </summary>
    public enum ReportingCadence
    {
        /// <summary>Report is generated once per month.</summary>
        Monthly,
        /// <summary>Report is generated once per quarter.</summary>
        Quarterly,
        /// <summary>Report is generated twice per year.</summary>
        SemiAnnual,
        /// <summary>Report is generated once per year.</summary>
        Annual,
        /// <summary>Report is triggered by specific business or compliance events.</summary>
        EventDriven
    }

    // ── Evidence domain ───────────────────────────────────────────────────────

    /// <summary>
    /// Logical domain of compliance evidence that a report may require.
    /// </summary>
    public enum EvidenceDomainKind
    {
        /// <summary>KYC/AML decision records and screening outcomes.</summary>
        KycAml,
        /// <summary>Compliance case management records and remediation history.</summary>
        ComplianceCases,
        /// <summary>Approval workflow stage decisions and reviewer notes.</summary>
        ApprovalWorkflows,
        /// <summary>Enterprise audit export records and retention policy compliance.</summary>
        AuditExports,
        /// <summary>Ongoing monitoring task outcomes and subject reviews.</summary>
        OngoingMonitoring,
        /// <summary>Protected sign-off records and deployment governance.</summary>
        ProtectedSignOff,
        /// <summary>Regulatory evidence packages and readiness posture records.</summary>
        RegulatoryPackages
    }

    // ── Report run status ─────────────────────────────────────────────────────

    /// <summary>
    /// Lifecycle status of a report run from creation through delivery.
    /// </summary>
    public enum ReportRunStatus
    {
        /// <summary>Run record created; evidence evaluation not yet started.</summary>
        Draft,
        /// <summary>Run is queued for background evidence evaluation.</summary>
        Queued,
        /// <summary>Run cannot proceed because required evidence is missing or stale.</summary>
        Blocked,
        /// <summary>Evidence is assembled; run is awaiting manual reviewer sign-off.</summary>
        AwaitingReview,
        /// <summary>Run has been reviewed; awaiting formal approval before export.</summary>
        AwaitingApproval,
        /// <summary>Run has been approved and exported to a durable format.</summary>
        Exported,
        /// <summary>Run has been delivered to all configured destinations.</summary>
        Delivered,
        /// <summary>Run was delivered to some but not all configured destinations.</summary>
        PartiallyDelivered,
        /// <summary>Run encountered a terminal error that prevents completion.</summary>
        Failed
    }

    // ── Delivery destination type ──────────────────────────────────────────────

    /// <summary>
    /// Type of delivery destination for a completed report run.
    /// </summary>
    public enum DeliveryDestinationType
    {
        /// <summary>Report is archived internally within the platform.</summary>
        InternalArchive,
        /// <summary>Report is dispatched to configured executive recipients.</summary>
        ExecutiveInbox,
        /// <summary>Report is sent to external auditor inboxes.</summary>
        AuditorInbox,
        /// <summary>Report is exported to a regulator-facing channel or portal.</summary>
        RegulatorExport,
        /// <summary>Report event is emitted to registered webhook subscribers.</summary>
        WebhookSubscriber
    }

    // ── Delivery outcome status ────────────────────────────────────────────────

    /// <summary>
    /// Outcome status for a single report delivery attempt.
    /// </summary>
    public enum DeliveryOutcomeStatus
    {
        /// <summary>Delivery has not yet been attempted.</summary>
        Pending,
        /// <summary>Delivery completed successfully.</summary>
        Delivered,
        /// <summary>Delivery failed; diagnostic details are available.</summary>
        Failed,
        /// <summary>Delivery was intentionally skipped due to configuration or policy.</summary>
        Skipped
    }

    // ── Blocker severity ──────────────────────────────────────────────────────

    /// <summary>
    /// Severity classification for a report run blocker.
    /// </summary>
    public enum ReportBlockerSeverity
    {
        /// <summary>Informational gap; does not prevent report generation.</summary>
        Advisory,
        /// <summary>Non-critical gap; report may proceed but gap should be noted.</summary>
        Warning,
        /// <summary>Critical gap; report run is blocked until resolved.</summary>
        Critical
    }

    // ── Evidence freshness status ──────────────────────────────────────────────

    /// <summary>
    /// Freshness classification for an evidence domain included in a report run.
    /// </summary>
    /// <summary>
    /// Evidence freshness classification for scheduled reporting.
    /// Uses a distinct name from <c>BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision.EvidenceFreshnessStatus</c>
    /// to avoid Swashbuckle schema-ID conflicts.
    /// </summary>
    public enum ReportEvidenceFreshnessStatus
    {
        /// <summary>Evidence is current and within the required freshness window.</summary>
        Current,
        /// <summary>Evidence is approaching its freshness expiry.</summary>
        NearingExpiry,
        /// <summary>Evidence has exceeded its validity window and is stale.</summary>
        Stale,
        /// <summary>Required evidence was not found for this domain.</summary>
        Missing,
        /// <summary>Evidence source was unavailable at evaluation time.</summary>
        Unavailable
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reporting Template Models
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persisted reporting template that defines the parameters for recurring compliance reports.
    /// Templates are reusable recipes that operators configure once and trigger repeatedly.
    /// </summary>
    public class ReportingTemplate
    {
        /// <summary>Unique identifier for this template.</summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>Human-readable name for this reporting template.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of the template's purpose and scope.</summary>
        public string? Description { get; set; }

        /// <summary>Target audience that governs framing and delivery routing.</summary>
        public ReportingAudienceType AudienceType { get; set; }

        /// <summary>Reporting cadence for scheduled runs.</summary>
        public ReportingCadence Cadence { get; set; }

        /// <summary>Evidence domains that this template requires.</summary>
        public List<EvidenceDomainKind> RequiredEvidenceDomains { get; set; } = new();

        /// <summary>Configured delivery destinations for generated runs.</summary>
        public List<DeliveryDestinationConfig> DeliveryDestinations { get; set; } = new();

        /// <summary>Whether a manual reviewer must sign off before export.</summary>
        public bool ReviewRequired { get; set; }

        /// <summary>Whether formal approval is required before delivery.</summary>
        public bool ApprovalRequired { get; set; }

        /// <summary>Subject scope for this template (e.g., specific asset ID or issuer).</summary>
        public string? SubjectScope { get; set; }

        /// <summary>Whether this template is currently active for scheduling.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Whether this template has been archived.</summary>
        public bool IsArchived { get; set; }

        /// <summary>UTC timestamp when this template was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>User identity who created this template.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the last update to this template.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>User identity who last updated this template.</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>UTC timestamp when the next run should be triggered (for scheduled cadences).</summary>
        public DateTimeOffset? NextRunAt { get; set; }

        /// <summary>UTC timestamp of the most recent completed run.</summary>
        public DateTimeOffset? LastRunAt { get; set; }

        /// <summary>Status of the most recent run for UI display.</summary>
        public ReportRunStatus? LastRunStatus { get; set; }

        /// <summary>Current evidence freshness posture summary.</summary>
        public string? FreshnessPosture { get; set; }
    }

    /// <summary>
    /// Configuration for a delivery destination within a reporting template.
    /// Secret values (e.g., endpoint credentials) are not stored in this record.
    /// </summary>
    public class DeliveryDestinationConfig
    {
        /// <summary>Unique identifier for this destination within the template.</summary>
        public string DestinationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Type of delivery destination.</summary>
        public DeliveryDestinationType DestinationType { get; set; }

        /// <summary>Human-readable label for this destination.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Whether this destination is required for the run to be considered delivered.</summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>Non-secret routing metadata (e.g., masked email domain, webhook subscription ID).</summary>
        public string? RoutingHint { get; set; }
    }

    /// <summary>
    /// Request to create a new reporting template.
    /// </summary>
    public class CreateReportingTemplateRequest
    {
        /// <summary>Human-readable name for the template.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description.</summary>
        public string? Description { get; set; }

        /// <summary>Target audience type.</summary>
        public ReportingAudienceType AudienceType { get; set; }

        /// <summary>Reporting cadence.</summary>
        public ReportingCadence Cadence { get; set; }

        /// <summary>Evidence domains to include.</summary>
        public List<EvidenceDomainKind> RequiredEvidenceDomains { get; set; } = new();

        /// <summary>Delivery destinations.</summary>
        public List<DeliveryDestinationConfig> DeliveryDestinations { get; set; } = new();

        /// <summary>Whether reviewer sign-off is required.</summary>
        public bool ReviewRequired { get; set; }

        /// <summary>Whether formal approval is required.</summary>
        public bool ApprovalRequired { get; set; }

        /// <summary>Optional subject scope.</summary>
        public string? SubjectScope { get; set; }
    }

    /// <summary>
    /// Request to update an existing reporting template.
    /// Only provided fields are updated; null values are ignored.
    /// </summary>
    public class UpdateReportingTemplateRequest
    {
        /// <summary>Updated name, or null to leave unchanged.</summary>
        public string? Name { get; set; }

        /// <summary>Updated description, or null to leave unchanged.</summary>
        public string? Description { get; set; }

        /// <summary>Updated cadence, or null to leave unchanged.</summary>
        public ReportingCadence? Cadence { get; set; }

        /// <summary>Updated evidence domains, or null to leave unchanged.</summary>
        public List<EvidenceDomainKind>? RequiredEvidenceDomains { get; set; }

        /// <summary>Updated delivery destinations, or null to leave unchanged.</summary>
        public List<DeliveryDestinationConfig>? DeliveryDestinations { get; set; }

        /// <summary>Updated review-required flag, or null to leave unchanged.</summary>
        public bool? ReviewRequired { get; set; }

        /// <summary>Updated approval-required flag, or null to leave unchanged.</summary>
        public bool? ApprovalRequired { get; set; }

        /// <summary>Updated subject scope, or null to leave unchanged.</summary>
        public string? SubjectScope { get; set; }

        /// <summary>Updated active status, or null to leave unchanged.</summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Response for template create/update/get operations.
    /// </summary>
    public class ReportingTemplateResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The template record, present on success.</summary>
        public ReportingTemplate? Template { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response for listing reporting templates.
    /// </summary>
    public class ListReportingTemplatesResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Matching template records.</summary>
        public List<ReportingTemplate> Templates { get; set; } = new();

        /// <summary>Total count of matching templates (before pagination).</summary>
        public int TotalCount { get; set; }

        /// <summary>Error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Report Run Models
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Record of a single report run execution, including evidence lineage,
    /// approval metadata, delivery outcomes, and comparison to prior run.
    /// </summary>
    public class ReportRunRecord
    {
        /// <summary>Unique identifier for this report run.</summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>ID of the template that produced this run.</summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>Template name at the time of this run (snapshot).</summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>Audience type for this run.</summary>
        public ReportingAudienceType AudienceType { get; set; }

        /// <summary>Current lifecycle status of this run.</summary>
        public ReportRunStatus Status { get; set; }

        /// <summary>UTC timestamp when this run was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>User or system identity that triggered this run.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the last status transition.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>UTC timestamp when the run was exported, if applicable.</summary>
        public DateTimeOffset? ExportedAt { get; set; }

        /// <summary>UTC timestamp when the final delivery was confirmed, if applicable.</summary>
        public DateTimeOffset? DeliveredAt { get; set; }

        /// <summary>Evidence lineage entries for all domains evaluated in this run.</summary>
        public List<EvidenceLineageEntry> EvidenceLineage { get; set; } = new();

        /// <summary>Blockers that prevented or delayed this run.</summary>
        public List<ReportRunBlocker> Blockers { get; set; } = new();

        /// <summary>Delivery outcomes for each configured destination.</summary>
        public List<DeliveryOutcomeRecord> DeliveryOutcomes { get; set; } = new();

        /// <summary>Review and approval metadata for this run.</summary>
        public ReportRunApprovalMetadata? ApprovalMetadata { get; set; }

        /// <summary>Comparison summary relative to the previous run for the same template.</summary>
        public ReportRunComparison? ComparisonToPriorRun { get; set; }

        /// <summary>Count of critical blockers.</summary>
        public int CriticalBlockerCount { get; set; }

        /// <summary>Count of warning-level issues.</summary>
        public int WarningCount { get; set; }

        /// <summary>Count of successful delivery outcomes.</summary>
        public int DeliverySuccessCount { get; set; }

        /// <summary>Count of failed delivery outcomes.</summary>
        public int DeliveryFailureCount { get; set; }
    }

    /// <summary>
    /// Evidence lineage entry documenting the evaluation of a single evidence domain.
    /// </summary>
    public class EvidenceLineageEntry
    {
        /// <summary>Evidence domain evaluated.</summary>
        public EvidenceDomainKind Domain { get; set; }

        /// <summary>Freshness status determined at evaluation time.</summary>
        public ReportEvidenceFreshnessStatus FreshnessStatus { get; set; }

        /// <summary>UTC timestamp when the evidence was last updated.</summary>
        public DateTimeOffset? LastUpdatedAt { get; set; }

        /// <summary>UTC timestamp when the evidence will expire (if known).</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Source reference identifier (e.g., case ID, approval ID).</summary>
        public string? SourceReference { get; set; }

        /// <summary>Human-readable note about this evidence entry.</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// A blocker that prevents or delays a report run from progressing.
    /// </summary>
    public class ReportRunBlocker
    {
        /// <summary>Machine-readable blocker code.</summary>
        public string BlockerCode { get; set; } = string.Empty;

        /// <summary>Severity of this blocker.</summary>
        public ReportBlockerSeverity Severity { get; set; }

        /// <summary>Human-readable description of the blocker.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Actionable remediation hint for operators.</summary>
        public string? RemediationHint { get; set; }

        /// <summary>Evidence domain affected, if applicable.</summary>
        public EvidenceDomainKind? AffectedDomain { get; set; }

        /// <summary>UTC timestamp when this blocker was detected.</summary>
        public DateTimeOffset DetectedAt { get; set; }

        /// <summary>Whether this blocker has been resolved in a subsequent evaluation.</summary>
        public bool IsResolved { get; set; }
    }

    /// <summary>
    /// Delivery outcome for a single destination in a report run.
    /// Sensitive credentials or endpoint details are never included.
    /// </summary>
    public class DeliveryOutcomeRecord
    {
        /// <summary>Destination configuration ID from the template.</summary>
        public string DestinationId { get; set; } = string.Empty;

        /// <summary>Type of delivery destination.</summary>
        public DeliveryDestinationType DestinationType { get; set; }

        /// <summary>Human-readable label for this destination.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Outcome of the delivery attempt.</summary>
        public DeliveryOutcomeStatus Status { get; set; }

        /// <summary>UTC timestamp of the delivery attempt.</summary>
        public DateTimeOffset? AttemptedAt { get; set; }

        /// <summary>UTC timestamp of successful delivery confirmation.</summary>
        public DateTimeOffset? DeliveredAt { get; set; }

        /// <summary>Non-secret diagnostic detail on failure (e.g., HTTP status code, error category).</summary>
        public string? DiagnosticDetail { get; set; }

        /// <summary>Number of delivery attempts made.</summary>
        public int AttemptCount { get; set; }
    }

    /// <summary>
    /// Review and approval metadata captured for a report run.
    /// </summary>
    public class ReportRunApprovalMetadata
    {
        /// <summary>Whether this run required a reviewer sign-off.</summary>
        public bool ReviewRequired { get; set; }

        /// <summary>Whether this run required formal approval.</summary>
        public bool ApprovalRequired { get; set; }

        /// <summary>User who reviewed this run, if review has occurred.</summary>
        public string? ReviewedBy { get; set; }

        /// <summary>UTC timestamp of the review, if completed.</summary>
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Reviewer notes.</summary>
        public string? ReviewNotes { get; set; }

        /// <summary>User who approved this run, if approval has occurred.</summary>
        public string? ApprovedBy { get; set; }

        /// <summary>UTC timestamp of the approval, if completed.</summary>
        public DateTimeOffset? ApprovedAt { get; set; }

        /// <summary>Approver notes.</summary>
        public string? ApprovalNotes { get; set; }

        /// <summary>Whether the run was explicitly rejected.</summary>
        public bool IsRejected { get; set; }

        /// <summary>Rejection reason, if the run was rejected.</summary>
        public string? RejectionReason { get; set; }
    }

    /// <summary>
    /// Comparison summary between the current run and the previous run for the same template.
    /// Used to communicate what changed between reporting cycles.
    /// </summary>
    public class ReportRunComparison
    {
        /// <summary>ID of the prior run used as the comparison baseline.</summary>
        public string? PriorRunId { get; set; }

        /// <summary>UTC timestamp of the prior run.</summary>
        public DateTimeOffset? PriorRunAt { get; set; }

        /// <summary>Status of the prior run.</summary>
        public ReportRunStatus? PriorRunStatus { get; set; }

        /// <summary>New critical blockers that appeared since the prior run.</summary>
        public List<string> NewBlockers { get; set; } = new();

        /// <summary>Blockers that were present in the prior run but are now resolved.</summary>
        public List<string> ResolvedBlockers { get; set; } = new();

        /// <summary>Evidence domains whose freshness status changed.</summary>
        public List<string> EvidenceFreshnessChanges { get; set; } = new();

        /// <summary>Approval or review state changes since the prior run.</summary>
        public List<string> ApprovalChanges { get; set; } = new();

        /// <summary>Whether overall compliance posture improved since the prior run.</summary>
        public bool PostureImproved { get; set; }
    }

    /// <summary>
    /// Request to manually trigger a report run for a template.
    /// </summary>
    public class TriggerReportRunRequest
    {
        /// <summary>Optional correlation ID for tracing this trigger across systems.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional override for the actor identity triggering the run.</summary>
        public string? TriggeredBy { get; set; }

        /// <summary>Optional reason or context for this manual trigger.</summary>
        public string? TriggerReason { get; set; }
    }

    /// <summary>
    /// Response for triggering or retrieving a report run.
    /// </summary>
    public class ReportRunResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The report run record, present on success.</summary>
        public ReportRunRecord? Run { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response for listing report runs.
    /// </summary>
    public class ListReportRunsResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Report run records.</summary>
        public List<ReportRunRecord> Runs { get; set; } = new();

        /// <summary>Total count of matching runs.</summary>
        public int TotalCount { get; set; }

        /// <summary>Error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Schedule Management Models
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Schedule definition for a reporting template, controlling when recurring runs are triggered.
    /// </summary>
    public class ReportingScheduleDefinition
    {
        /// <summary>Unique identifier for this schedule.</summary>
        public string ScheduleId { get; set; } = string.Empty;

        /// <summary>Template this schedule belongs to.</summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>Cadence mode.</summary>
        public ReportingCadence Cadence { get; set; }

        /// <summary>For Monthly cadence: day of month (1-28). Null for other cadences.</summary>
        public int? DayOfMonth { get; set; }

        /// <summary>For Quarterly/Annual: calendar month of the year (1-12). Null for other cadences.</summary>
        public int? AnchorMonth { get; set; }

        /// <summary>Time of day (UTC) at which the scheduled run should trigger.</summary>
        public TimeSpan TriggerTimeUtc { get; set; }

        /// <summary>Whether this schedule is currently active.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>UTC timestamp of the next scheduled trigger.</summary>
        public DateTimeOffset? NextTriggerAt { get; set; }

        /// <summary>UTC timestamp of the last scheduled trigger.</summary>
        public DateTimeOffset? LastTriggeredAt { get; set; }

        /// <summary>UTC timestamp when this schedule was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>User who created this schedule.</summary>
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to create or update a schedule for a reporting template.
    /// </summary>
    public class UpsertScheduleRequest
    {
        /// <summary>Cadence mode for the schedule.</summary>
        public ReportingCadence Cadence { get; set; }

        /// <summary>Day of month for monthly cadence (1-28).</summary>
        public int? DayOfMonth { get; set; }

        /// <summary>Anchor month for quarterly or annual cadence (1-12).</summary>
        public int? AnchorMonth { get; set; }

        /// <summary>Time of day (UTC hours, 0-23) at which the run should trigger.</summary>
        public int TriggerHourUtc { get; set; }

        /// <summary>Whether this schedule should be active immediately.</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Response for schedule operations.
    /// </summary>
    public class ScheduleResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The schedule definition, present on success.</summary>
        public ReportingScheduleDefinition? Schedule { get; set; }

        /// <summary>Error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Approval and Review Models
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to record a review decision on a report run.
    /// </summary>
    public class ReviewReportRunRequest
    {
        /// <summary>Notes from the reviewer.</summary>
        public string? ReviewNotes { get; set; }

        /// <summary>Whether the reviewer approves or rejects this run.</summary>
        public bool Approve { get; set; }

        /// <summary>Rejection reason, required when Approve is false.</summary>
        public string? RejectionReason { get; set; }
    }

    /// <summary>
    /// Request to record a formal approval decision on a reviewed report run.
    /// </summary>
    public class ApproveReportRunRequest
    {
        /// <summary>Notes from the approver.</summary>
        public string? ApprovalNotes { get; set; }

        /// <summary>Whether the approver confirms or rejects this run.</summary>
        public bool Approve { get; set; }

        /// <summary>Rejection reason, required when Approve is false.</summary>
        public string? RejectionReason { get; set; }
    }
}

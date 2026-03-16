using BiatecTokensApi.Models.ComplianceCaseManagement;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service contract for compliance case management: creating, tracking, transitioning,
    /// and monitoring compliance cases through their full lifecycle.
    /// </summary>
    public interface IComplianceCaseManagementService
    {
        /// <summary>Creates a new compliance case or returns an existing active case (idempotent by issuerId+subjectId+type).</summary>
        Task<CreateComplianceCaseResponse> CreateCaseAsync(CreateComplianceCaseRequest request, string actorId);

        /// <summary>Retrieves a compliance case by ID. Checks evidence freshness and transitions to Stale if expired.</summary>
        Task<GetComplianceCaseResponse> GetCaseAsync(string caseId, string actorId);

        /// <summary>Lists compliance cases with optional filters and pagination.</summary>
        Task<ListComplianceCasesResponse> ListCasesAsync(ListComplianceCasesRequest request, string actorId);

        /// <summary>Updates mutable fields (priority, reviewer assignment, jurisdiction, external reference) on a case.</summary>
        Task<UpdateComplianceCaseResponse> UpdateCaseAsync(string caseId, UpdateComplianceCaseRequest request, string actorId);

        /// <summary>Transitions a case to a new lifecycle state following the allowed transition matrix.</summary>
        Task<UpdateComplianceCaseResponse> TransitionStateAsync(string caseId, TransitionCaseStateRequest request, string actorId);

        /// <summary>Adds a normalized evidence summary to the case and appends a timeline entry.</summary>
        Task<GetComplianceCaseResponse> AddEvidenceAsync(string caseId, AddEvidenceRequest request, string actorId);

        /// <summary>Adds a remediation task to the case and appends a timeline entry.</summary>
        Task<GetComplianceCaseResponse> AddRemediationTaskAsync(string caseId, AddRemediationTaskRequest request, string actorId);

        /// <summary>Resolves or dismisses a remediation task and appends a timeline entry.</summary>
        Task<GetComplianceCaseResponse> ResolveRemediationTaskAsync(string caseId, string taskId, ResolveRemediationTaskRequest request, string actorId);

        /// <summary>Raises an escalation on a case and appends a timeline entry.</summary>
        Task<GetComplianceCaseResponse> AddEscalationAsync(string caseId, AddEscalationRequest request, string actorId);

        /// <summary>Resolves an escalation on a case and appends a timeline entry.</summary>
        Task<GetComplianceCaseResponse> ResolveEscalationAsync(string caseId, string escalationId, ResolveEscalationRequest request, string actorId);

        /// <summary>Returns the chronological timeline of all events for a case.</summary>
        Task<CaseTimelineResponse> GetTimelineAsync(string caseId, string actorId);

        /// <summary>
        /// Evaluates whether a case is ready to proceed (fail-closed semantics).
        /// Automatically checks evidence freshness before evaluating.
        /// </summary>
        Task<CaseReadinessSummaryResponse> GetReadinessSummaryAsync(string caseId, string actorId);

        /// <summary>
        /// Scans all active cases for expired evidence and transitions them to <see cref="ComplianceCaseState.Stale"/>.
        /// Returns the number of cases that were transitioned.
        /// </summary>
        Task<int> RunEvidenceFreshnessCheckAsync();

        /// <summary>
        /// Configures or updates the monitoring schedule for an approved case, enrolling it
        /// in a periodic review program.
        /// </summary>
        Task<SetMonitoringScheduleResponse> SetMonitoringScheduleAsync(string caseId, SetMonitoringScheduleRequest request, string actorId);

        /// <summary>
        /// Records the outcome of a periodic monitoring review for a case.
        /// Updates the schedule's <c>LastReviewAt</c> and <c>NextReviewDueAt</c> fields and appends
        /// a timeline entry. If the outcome is <see cref="MonitoringReviewOutcome.EscalationRequired"/>
        /// and <c>CreateFollowUpCase</c> is true, creates a follow-up <see cref="CaseType.OngoingMonitoring"/> case.
        /// </summary>
        Task<RecordMonitoringReviewResponse> RecordMonitoringReviewAsync(string caseId, RecordMonitoringReviewRequest request, string actorId);

        /// <summary>
        /// Scans all cases with active monitoring schedules and marks overdue reviews.
        /// Returns a summary of cases inspected and those now flagged as overdue.
        /// </summary>
        Task<TriggerPeriodicReviewCheckResponse> TriggerPeriodicReviewCheckAsync(string actorId);

        /// <summary>
        /// Generates a regulator/audit-ready evidence bundle for the specified case,
        /// comprising the full case snapshot, chronological timeline, and export metadata
        /// (including a SHA-256 content hash).  A <see cref="Models.Webhook.WebhookEventType.ComplianceCaseExported"/>
        /// event is emitted on success.
        /// </summary>
        Task<ExportComplianceCaseResponse> ExportCaseAsync(string caseId, ExportComplianceCaseRequest request, string actorId);
    }
}

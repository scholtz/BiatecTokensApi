using BiatecTokensApi.Models.ScheduledReporting;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing scheduled compliance reporting templates, report runs,
    /// schedule definitions, delivery tracking, and approval workflows.
    ///
    /// Design principles:
    /// - Fail-closed: if required evidence is missing or stale, runs return blocked status.
    /// - Auditability: every template and run records who created it, who approved it, and what
    ///   evidence set was used.
    /// - Background-execution-ready: service is designed so report generation can be offloaded
    ///   to background workers without altering the API contract.
    /// </summary>
    public interface IScheduledReportingService
    {
        // ── Template CRUD ──────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new reporting template. Returns the persisted template on success.
        /// </summary>
        Task<ReportingTemplateResponse> CreateTemplateAsync(
            CreateReportingTemplateRequest request, string actorId);

        /// <summary>
        /// Retrieves a reporting template by its ID.
        /// </summary>
        Task<ReportingTemplateResponse> GetTemplateAsync(string templateId);

        /// <summary>
        /// Updates an existing reporting template. Only provided fields are modified.
        /// </summary>
        Task<ReportingTemplateResponse> UpdateTemplateAsync(
            string templateId, UpdateReportingTemplateRequest request, string actorId);

        /// <summary>
        /// Archives a reporting template. Archived templates are retained for audit purposes
        /// but will no longer trigger new scheduled runs.
        /// </summary>
        Task<ReportingTemplateResponse> ArchiveTemplateAsync(string templateId, string actorId);

        /// <summary>
        /// Lists reporting templates with optional filters. Supports pagination.
        /// </summary>
        Task<ListReportingTemplatesResponse> ListTemplatesAsync(
            bool includeArchived = false,
            ReportingAudienceType? audienceFilter = null,
            int page = 1,
            int pageSize = 20);

        // ── Report Run operations ──────────────────────────────────────────────

        /// <summary>
        /// Manually triggers a report run for the specified template. Evaluates evidence freshness
        /// fail-closed: if required evidence is missing or stale, the run is created with
        /// <see cref="ReportRunStatus.Blocked"/> status and actionable blocker details.
        /// </summary>
        Task<ReportRunResponse> TriggerRunAsync(
            string templateId, TriggerReportRunRequest request, string actorId);

        /// <summary>
        /// Retrieves a specific report run by its ID.
        /// </summary>
        Task<ReportRunResponse> GetRunAsync(string runId);

        /// <summary>
        /// Lists report runs for a given template, ordered by creation date descending.
        /// </summary>
        Task<ListReportRunsResponse> ListRunsAsync(
            string templateId,
            ReportRunStatus? statusFilter = null,
            int page = 1,
            int pageSize = 20);

        // ── Approval and review ────────────────────────────────────────────────

        /// <summary>
        /// Records a reviewer decision on a report run. Run must be in
        /// <see cref="ReportRunStatus.AwaitingReview"/> status.
        /// </summary>
        Task<ReportRunResponse> ReviewRunAsync(
            string runId, ReviewReportRunRequest request, string actorId);

        /// <summary>
        /// Records a formal approval decision on a reviewed report run. Run must be in
        /// <see cref="ReportRunStatus.AwaitingApproval"/> status.
        /// Approved runs are automatically transitioned to <see cref="ReportRunStatus.Exported"/>.
        /// </summary>
        Task<ReportRunResponse> ApproveRunAsync(
            string runId, ApproveReportRunRequest request, string actorId);

        // ── Schedule management ────────────────────────────────────────────────

        /// <summary>
        /// Creates or replaces the schedule definition for a reporting template.
        /// </summary>
        Task<ScheduleResponse> UpsertScheduleAsync(
            string templateId, UpsertScheduleRequest request, string actorId);

        /// <summary>
        /// Retrieves the schedule definition for a reporting template.
        /// </summary>
        Task<ScheduleResponse> GetScheduleAsync(string templateId);

        /// <summary>
        /// Deactivates the schedule for a reporting template without deleting the definition.
        /// </summary>
        Task<ScheduleResponse> DeactivateScheduleAsync(string templateId, string actorId);
    }
}

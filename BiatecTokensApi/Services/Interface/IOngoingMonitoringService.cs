using BiatecTokensApi.Models.OngoingMonitoring;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service contract for ongoing compliance monitoring tasks.
    /// Provides lifecycle operations to create, track, reassess, escalate, defer,
    /// and close monitoring tasks linked to compliance cases.
    /// All operations are fail-closed: missing required inputs produce explicit errors.
    /// </summary>
    public interface IOngoingMonitoringService
    {
        /// <summary>
        /// Creates a new monitoring task for the subject identified by the request.
        /// Validates that IssuerId, SubjectId, and CaseId are present.
        /// </summary>
        Task<CreateMonitoringTaskResponse> CreateTaskAsync(
            CreateMonitoringTaskRequest request, string actorId);

        /// <summary>
        /// Returns a paginated, filtered list of monitoring tasks.
        /// </summary>
        Task<ListMonitoringTasksResponse> ListTasksAsync(
            ListMonitoringTasksRequest request, string actorId);

        /// <summary>
        /// Retrieves a single monitoring task by ID.
        /// Returns a not-found error if the task does not exist.
        /// </summary>
        Task<GetMonitoringTaskResponse> GetTaskAsync(string taskId, string actorId);

        /// <summary>
        /// Starts a reassessment on the specified monitoring task.
        /// Transitions the task from Healthy, DueSoon, or Overdue to InProgress.
        /// Fails if the task is already in a terminal state (Resolved, Suspended, Restricted).
        /// </summary>
        Task<StartReassessmentResponse> StartReassessmentAsync(
            string taskId, StartReassessmentRequest request, string actorId);

        /// <summary>
        /// Defers a monitoring task to a future date with a recorded rationale.
        /// The task must not be in a terminal state.
        /// <c>DeferUntil</c> must be in the future; <c>Rationale</c> must be non-empty.
        /// </summary>
        Task<DeferMonitoringTaskResponse> DeferTaskAsync(
            string taskId, DeferMonitoringTaskRequest request, string actorId);

        /// <summary>
        /// Escalates a monitoring task for senior review.
        /// Can optionally raise the severity.
        /// <c>EscalationReason</c> must be non-empty.
        /// Fails if the task is already in a terminal state.
        /// </summary>
        Task<EscalateMonitoringTaskResponse> EscalateTaskAsync(
            string taskId, EscalateMonitoringTaskRequest request, string actorId);

        /// <summary>
        /// Closes a monitoring task with an auditable resolution outcome.
        /// <c>ResolutionNotes</c> must be non-empty.
        /// Terminal states (Resolved, Suspended, Restricted) cannot be re-closed.
        /// </summary>
        Task<CloseMonitoringTaskResponse> CloseTaskAsync(
            string taskId, CloseMonitoringTaskRequest request, string actorId);

        /// <summary>
        /// Scans all open monitoring tasks and marks those whose DueAt is in the past as Overdue.
        /// Also advances Deferred tasks back to DueSoon or Overdue when their DeferredUntil has elapsed.
        /// Returns a count of tasks updated.
        /// </summary>
        Task<int> RunDueDateCheckAsync();
    }
}

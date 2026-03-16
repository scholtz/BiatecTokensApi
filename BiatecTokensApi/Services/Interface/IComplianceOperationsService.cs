using BiatecTokensApi.Models.ComplianceOperations;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service contract for the compliance operations orchestration layer.
    ///
    /// Aggregates signals from scheduled reporting, approval workflows, compliance case
    /// management, and ongoing monitoring into a unified SLA-aware queue and overview.
    ///
    /// Design principles:
    /// - Fail-closed: missing evidence, failed delivery, or unresolved approvals always
    ///   produce explicit non-healthy states rather than silently appearing healthy.
    /// - Deterministic SLA classification: OnTrack / DueSoon / Overdue / Blocked with
    ///   numeric ordering so comparisons are stable.
    /// - Role-aware aggregation: ComplianceManager, OperationsLead, ExecutiveSummary
    ///   each receive tailored views without bespoke endpoints.
    /// - Webhook emission: overdue and blocked state transitions emit audit events for
    ///   enterprise subscriber consumption.
    /// </summary>
    public interface IComplianceOperationsService
    {
        /// <summary>
        /// Returns a cross-workflow compliance operations overview with aggregated counts,
        /// per-role summaries, health status, and blocker breakdowns.
        /// </summary>
        /// <param name="actorId">Identity of the requesting actor (for audit trail).</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<ComplianceOperationsOverviewResponse> GetOverviewAsync(
            string actorId, string correlationId);

        /// <summary>
        /// Returns a prioritised, filterable compliance operations queue.
        ///
        /// Supports role-based views (ComplianceManager, OperationsLead, ExecutiveSummary),
        /// workflow-type filtering, SLA status filtering, and fail-closed-only filtering.
        /// Items are ordered by PriorityScore descending (blocked &gt; overdue &gt; due-soon &gt; on-track).
        /// </summary>
        /// <param name="request">Filter and pagination parameters.</param>
        /// <param name="actorId">Identity of the requesting actor.</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<ComplianceOpsQueueResponse> GetQueueAsync(
            ComplianceOpsQueueRequest request, string actorId, string correlationId);

        /// <summary>
        /// Adds or updates a queue item in the in-memory store.
        /// Emits webhook events when the SLA status transitions to Overdue or Blocked.
        /// Designed to be called by other services when they record new workflow state.
        /// </summary>
        /// <param name="item">The queue item to register or update.</param>
        /// <param name="actorId">Identity of the triggering actor.</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task UpsertQueueItemAsync(
            ComplianceOpsQueueItem item, string actorId, string correlationId);

        /// <summary>
        /// Marks a queue item as resolved and removes it from the active queue.
        /// Emits a <see cref="BiatecTokensApi.Models.Webhook.WebhookEventType.ComplianceOpsItemResolved"/> event.
        /// </summary>
        /// <param name="itemId">Stable identifier of the item to resolve.</param>
        /// <param name="actorId">Identity of the resolving actor.</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<bool> ResolveQueueItemAsync(
            string itemId, string actorId, string correlationId);

        /// <summary>
        /// Classifies a due date and current time into a <see cref="ComplianceOpsSlaStatus"/>.
        ///
        /// Rules:
        /// - No deadline → OnTrack
        /// - Now &lt; DueAt − 3 days → OnTrack
        /// - DueAt − 3 days ≤ Now ≤ DueAt → DueSoon
        /// - Now &gt; DueAt → Overdue
        /// - Explicit blocker → Blocked (caller responsibility)
        /// </summary>
        ComplianceOpsSlaStatus ClassifySlaStatus(DateTime? dueAt, DateTime now);

        /// <summary>
        /// Computes a priority score for queue ordering.
        /// Blocked items always outrank Overdue, which outrank DueSoon, which outrank OnTrack.
        /// Within a tier, fail-closed items score higher than non-fail-closed items.
        /// Within that, older items (larger age) score higher to prevent starvation.
        /// </summary>
        int ComputePriorityScore(
            ComplianceOpsSlaStatus status, bool isFailClosed, DateTime createdAt, DateTime now);
    }
}

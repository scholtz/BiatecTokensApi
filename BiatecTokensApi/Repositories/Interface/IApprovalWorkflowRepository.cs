using BiatecTokensApi.Models.ApprovalWorkflow;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for durable approval workflow persistence.
    ///
    /// Stores stage decision records and audit events so that workflow history
    /// can be reconstructed after application restart or redeployment.
    ///
    /// Replace the in-memory implementation with a database-backed version by
    /// implementing this interface without changing any service code.
    /// </summary>
    public interface IApprovalWorkflowRepository
    {
        /// <summary>
        /// Persists a stage decision record. Each call appends a new immutable record.
        /// Multiple decisions for the same stage are allowed; the latest takes precedence.
        /// </summary>
        Task SaveStageDecisionAsync(PersistedApprovalStageDecision decision);

        /// <summary>
        /// Returns all stage decision records for the given release package,
        /// ordered by timestamp ascending.
        /// </summary>
        Task<List<PersistedApprovalStageDecision>> GetStageDecisionsForPackageAsync(string releasePackageId);

        /// <summary>
        /// Appends an audit event for the given release package.
        /// </summary>
        Task AppendAuditEventAsync(string releasePackageId, ApprovalAuditEvent ev);

        /// <summary>
        /// Returns audit events for the given release package, newest first, up to maxCount.
        /// </summary>
        Task<List<ApprovalAuditEvent>> GetAuditEventsAsync(string releasePackageId, int maxCount = 100);
    }
}

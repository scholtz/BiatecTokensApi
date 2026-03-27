using BiatecTokensApi.Models.ComplianceEvents;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Read-only service that normalises persisted compliance, onboarding, sign-off,
    /// and export milestones into a canonical operator-facing event stream.
    /// </summary>
    public interface IComplianceEventBackboneService
    {
        /// <summary>
        /// Retrieves a filtered, paginated set of canonical compliance events.
        /// </summary>
        /// <param name="request">Event filters and pagination options.</param>
        /// <param name="actorId">Authenticated actor requesting the timeline.</param>
        /// <returns>Paginated events plus the current delivery/degradation state summary.</returns>
        Task<ComplianceEventListResponse> GetEventsAsync(ComplianceEventQueryRequest request, string actorId);
    }
}

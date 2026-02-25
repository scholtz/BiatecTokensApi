using BiatecTokensApi.Models.OperationalIntelligence;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for deterministic operational intelligence and audit evidence APIs.
    /// </summary>
    public interface IOperationalIntelligenceService
    {
        /// <summary>
        /// Returns a deterministically ordered operation timeline for a deployment.
        /// Idempotent – repeated calls with the same parameters return the same result.
        /// </summary>
        Task<OperationTimelineResponse> GetOperationTimelineAsync(OperationTimelineRequest request);

        /// <summary>
        /// Returns a normalized compliance checkpoint summary for a deployment.
        /// Includes business-readable explanations and recommended actions.
        /// </summary>
        Task<ComplianceCheckpointResponse> GetComplianceCheckpointsAsync(ComplianceCheckpointRequest request);

        /// <summary>
        /// Returns a privacy-safe stakeholder report payload for a deployment.
        /// Suitable for non-technical stakeholders and customer-facing summaries.
        /// </summary>
        Task<StakeholderReportResponse> GetStakeholderReportAsync(StakeholderReportRequest request);

        /// <summary>
        /// Maps a domain error code to a bounded operational risk signal.
        /// Deterministic – same error code always maps to the same category.
        /// </summary>
        OperationalRiskSignal ClassifyRisk(string errorCode, string correlationId);
    }
}

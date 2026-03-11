using BiatecTokensApi.Models.ComplianceHardening;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Hardening service interface for enterprise compliance orchestration.
    /// </summary>
    /// <remarks>
    /// Aggregates jurisdiction constraint evaluation, sanctions/KYC readiness,
    /// launch gate enforcement, and provider status reporting into a single
    /// deterministic, auditable surface.  All methods emit structured log events
    /// carrying the supplied correlation identifier so that a complete audit trail
    /// can be reconstructed from logs.
    /// </remarks>
    public interface IComplianceOrchestrationHardeningService
    {
        /// <summary>
        /// Performs a full compliance hardening evaluation for a pending token launch.
        /// Aggregates jurisdiction constraints, sanctions readiness, and overall launch
        /// gate status into a single deterministic response.
        /// </summary>
        /// <param name="request">Hardening evaluation request.</param>
        /// <param name="correlationId">Correlation identifier for end-to-end tracing.</param>
        /// <returns>
        /// Comprehensive hardening response with launch gate status, jurisdiction result,
        /// sanctions result, remediation hints, and provider statuses.
        /// </returns>
        Task<ComplianceHardeningResponse> EvaluateLaunchReadinessAsync(
            ComplianceHardeningRequest request,
            string correlationId);

        /// <summary>
        /// Evaluates jurisdiction constraints for a subject in a specific jurisdiction.
        /// Returns an explicit <see cref="JurisdictionStatus.NotConfigured"/> outcome
        /// when the jurisdiction rules engine is not yet configured — never silently succeeds.
        /// </summary>
        /// <param name="request">Jurisdiction constraint request.</param>
        /// <param name="correlationId">Correlation identifier.</param>
        /// <returns>Jurisdiction constraint result.</returns>
        Task<JurisdictionConstraintResult> GetJurisdictionConstraintAsync(
            JurisdictionConstraintRequest request,
            string correlationId);

        /// <summary>
        /// Evaluates sanctions and KYC readiness for a subject.
        /// Returns an explicit <see cref="SanctionsStatus.NotConfigured"/> outcome
        /// when the provider is not integrated — never silently succeeds.
        /// </summary>
        /// <param name="request">Sanctions readiness request.</param>
        /// <param name="correlationId">Correlation identifier.</param>
        /// <returns>Sanctions readiness result.</returns>
        Task<SanctionsReadinessResult> GetSanctionsReadinessAsync(
            SanctionsReadinessRequest request,
            string correlationId);

        /// <summary>
        /// Enforces the launch gate for a specific token.
        /// Prevents launch execution when blocking compliance reasons exist.
        /// </summary>
        /// <param name="request">Launch gate enforcement request.</param>
        /// <param name="correlationId">Correlation identifier.</param>
        /// <returns>Launch gate response with permit/block decision and remediation hints.</returns>
        Task<LaunchGateResponse> EnforceLaunchGateAsync(
            LaunchGateRequest request,
            string correlationId);

        /// <summary>
        /// Returns the current integration and health status of all registered compliance providers.
        /// Provides observability into which providers are active, degraded, or not yet integrated.
        /// </summary>
        /// <param name="correlationId">Correlation identifier.</param>
        /// <returns>List of provider status reports.</returns>
        Task<ProviderStatusListResponse> GetProviderStatusAsync(string correlationId);
    }
}

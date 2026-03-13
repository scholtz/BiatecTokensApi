using BiatecTokensApi.Models.ProtectedSignOff;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service that delivers protected sign-off environment readiness, enterprise lifecycle
    /// verification, fixture provisioning, and operational diagnostics needed to run a
    /// credible, fail-closed protected sign-off against a live backend.
    /// </summary>
    public interface IProtectedSignOffEnvironmentService
    {
        /// <summary>
        /// Checks whether the protected sign-off environment is ready for a protected run.
        /// Verifies that all required backend services, configuration, and infrastructure
        /// are available and operational.
        /// </summary>
        /// <param name="request">Request specifying which checks to perform.</param>
        /// <returns>
        /// A <see cref="ProtectedSignOffEnvironmentResponse"/> describing the readiness status
        /// of each environment component and actionable guidance when remediation is required.
        /// </returns>
        Task<ProtectedSignOffEnvironmentResponse> CheckEnvironmentReadinessAsync(ProtectedSignOffEnvironmentRequest request);

        /// <summary>
        /// Executes a deterministic verification of every stage in the enterprise sign-off
        /// lifecycle journey (authentication → initiation → status polling → terminal state
        /// → validation). Each stage verifies that the backend API contract is stable and
        /// produces actionable evidence for protected sign-off.
        /// </summary>
        /// <param name="request">Request specifying the issuer, deployment, and lifecycle options.</param>
        /// <returns>
        /// A <see cref="EnterpriseSignOffLifecycleResponse"/> with per-stage results and a
        /// top-level flag indicating whether the full lifecycle is ready for protected evidence.
        /// </returns>
        Task<EnterpriseSignOffLifecycleResponse> ExecuteSignOffLifecycleAsync(EnterpriseSignOffLifecycleRequest request);

        /// <summary>
        /// Provisions deterministic enterprise fixtures (issuer team and sign-off user) so the
        /// protected sign-off journey can execute against realistic authorization state without
        /// relying on ad-hoc runtime mutation or permissive fallbacks.
        /// </summary>
        /// <param name="request">Request specifying the issuer ID, admin user, and reset options.</param>
        /// <returns>
        /// A <see cref="EnterpriseFixtureProvisionResponse"/> describing whether provisioning
        /// succeeded, whether fixtures already existed, and guidance when it failed.
        /// </returns>
        Task<EnterpriseFixtureProvisionResponse> ProvisionEnterpriseFixturesAsync(EnterpriseFixtureProvisionRequest request);

        /// <summary>
        /// Gathers operational diagnostics for the protected sign-off backend, distinguishing
        /// configuration failures, authorization failures, contract failures, and lifecycle
        /// failures so that operators can triage protected-run problems quickly.
        /// </summary>
        /// <param name="correlationId">Optional correlation ID for distributed tracing.</param>
        /// <returns>
        /// A <see cref="ProtectedSignOffDiagnosticsResponse"/> with structured failure-category
        /// information and per-service availability status.
        /// </returns>
        Task<ProtectedSignOffDiagnosticsResponse> GetDiagnosticsAsync(string? correlationId);
    }
}

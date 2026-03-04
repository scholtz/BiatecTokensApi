using BiatecTokensApi.Models.MVPHardening;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>Service for MVP backend hardening operations.</summary>
    public interface IMVPBackendHardeningService
    {
        /// <summary>Verifies that auth contract is deterministic for the given email.</summary>
        Task<AuthContractVerifyResponse> VerifyAuthContractAsync(AuthContractVerifyRequest request);

        /// <summary>Initiates a reliable deployment with idempotency support.</summary>
        Task<DeploymentReliabilityResponse> InitiateDeploymentAsync(DeploymentReliabilityRequest request);

        /// <summary>Returns the current status of a deployment, or null if not found.</summary>
        Task<DeploymentReliabilityResponse?> GetDeploymentStatusAsync(string deploymentId, string? correlationId);

        /// <summary>Transitions a deployment to the specified status.</summary>
        Task<DeploymentReliabilityResponse> TransitionDeploymentStatusAsync(DeploymentStatusTransitionRequest request);

        /// <summary>Runs a compliance check for the given asset and check type.</summary>
        Task<ComplianceCheckResponse> RunComplianceCheckAsync(ComplianceCheckRequest request);

        /// <summary>Creates an observability trace event.</summary>
        Task<ObservabilityTraceResponse> CreateTraceAsync(ObservabilityTraceRequest request);

        /// <summary>Returns audit events, optionally filtered by correlationId.</summary>
        IReadOnlyList<MVPHardeningAuditEvent> GetAuditEvents(string? correlationId = null);
    }
}

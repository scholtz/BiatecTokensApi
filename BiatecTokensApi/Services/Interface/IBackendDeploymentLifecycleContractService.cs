using BiatecTokensApi.Models.BackendDeploymentLifecycle;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service contract for deterministic backend deployment lifecycle with ARC76 hardening.
    ///
    /// Design guarantees:
    /// - Same email+password always derives the same ARC76 Algorand address (determinism).
    /// - Replayed requests with identical idempotency keys return cached results (idempotency).
    /// - Illegal lifecycle state transitions are blocked with structured error codes.
    /// - Every deployment emits compliance audit events with correlation IDs.
    /// - Errors are classified into a stable machine-readable taxonomy.
    /// </summary>
    public interface IBackendDeploymentLifecycleContractService
    {
        /// <summary>
        /// Initiates or resumes a deterministic backend deployment contract.
        /// Derives the deployer account via ARC76 (or uses an explicit address),
        /// validates inputs, enforces lifecycle state transitions, and emits audit events.
        /// Replayed requests with the same idempotency key return the cached result.
        /// </summary>
        /// <param name="request">Deployment parameters including credentials or explicit address.</param>
        /// <returns>Deployment contract response with lifecycle state, derivation evidence, and audit trail.</returns>
        Task<BackendDeploymentContractResponse> InitiateAsync(BackendDeploymentContractRequest request);

        /// <summary>
        /// Returns the current state of a deployment contract by its ID.
        /// </summary>
        /// <param name="deploymentId">Deployment contract ID.</param>
        /// <param name="correlationId">Optional correlation ID for tracing.</param>
        /// <returns>Current deployment contract response, or a not-found error response.</returns>
        Task<BackendDeploymentContractResponse> GetStatusAsync(
            string deploymentId,
            string? correlationId = null);

        /// <summary>
        /// Validates deployment inputs and ARC76 derivation without committing any state.
        /// Returns field-level validation results and derivation evidence.
        /// </summary>
        /// <param name="request">Validation request with credentials and deployment parameters.</param>
        /// <returns>Validation response with field results and derivation status.</returns>
        Task<BackendDeploymentContractValidationResponse> ValidateAsync(
            BackendDeploymentContractValidationRequest request);

        /// <summary>
        /// Returns the compliance audit trail for a deployment contract.
        /// Includes all audit events emitted across the deployment lifecycle.
        /// </summary>
        /// <param name="deploymentId">Deployment contract ID.</param>
        /// <param name="correlationId">Optional correlation ID for tracing.</param>
        /// <returns>Compliance audit trail with ordered events and final state.</returns>
        Task<BackendDeploymentAuditTrail> GetAuditTrailAsync(
            string deploymentId,
            string? correlationId = null);

        /// <summary>
        /// Derives the ARC76 Algorand address from email and password without deploying.
        /// Same credentials always produce the same address (determinism contract).
        /// </summary>
        /// <param name="email">Deployer email (canonicalized: lowercase + trim).</param>
        /// <param name="password">Deployer password (never stored or logged).</param>
        /// <returns>Derived Algorand address string.</returns>
        /// <exception cref="ArgumentException">Thrown when email or password is null/whitespace.</exception>
        string DeriveARC76Address(string email, string password);

        /// <summary>
        /// Returns whether a lifecycle state transition is valid.
        /// </summary>
        /// <param name="from">Current state.</param>
        /// <param name="to">Target state.</param>
        /// <returns>True when the transition is permitted.</returns>
        bool IsValidStateTransition(ContractLifecycleState from, ContractLifecycleState to);
    }
}

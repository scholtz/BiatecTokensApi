using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for validating state transitions with business rule invariants
    /// </summary>
    public interface IStateTransitionGuard
    {
        /// <summary>
        /// Validates if a deployment status transition is allowed
        /// </summary>
        /// <param name="currentStatus">Current deployment status</param>
        /// <param name="newStatus">Requested new status</param>
        /// <param name="deployment">The deployment being transitioned (for context-aware validation)</param>
        /// <returns>Validation result with allowed flag and reason code</returns>
        StateTransitionValidation ValidateTransition(
            DeploymentStatus currentStatus,
            DeploymentStatus newStatus,
            TokenDeployment? deployment = null);

        /// <summary>
        /// Gets the list of valid next states from a given status
        /// </summary>
        /// <param name="currentStatus">Current status</param>
        /// <returns>List of allowed next states</returns>
        List<DeploymentStatus> GetValidNextStates(DeploymentStatus currentStatus);

        /// <summary>
        /// Checks if a status represents a terminal state (no further transitions allowed)
        /// </summary>
        /// <param name="status">Status to check</param>
        /// <returns>True if terminal state, false otherwise</returns>
        bool IsTerminalState(DeploymentStatus status);

        /// <summary>
        /// Gets the reason code for a state transition
        /// </summary>
        /// <param name="fromStatus">Source status</param>
        /// <param name="toStatus">Target status</param>
        /// <returns>Standardized reason code</returns>
        string GetTransitionReasonCode(DeploymentStatus fromStatus, DeploymentStatus toStatus);
    }

    /// <summary>
    /// Result of a state transition validation
    /// </summary>
    public class StateTransitionValidation
    {
        /// <summary>
        /// Whether the transition is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Reason code for the validation result
        /// </summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable explanation
        /// </summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// List of valid alternative states if transition is not allowed
        /// </summary>
        public List<DeploymentStatus>? ValidAlternatives { get; set; }

        /// <summary>
        /// Invariants that were violated (if any)
        /// </summary>
        public List<string>? ViolatedInvariants { get; set; }
    }
}

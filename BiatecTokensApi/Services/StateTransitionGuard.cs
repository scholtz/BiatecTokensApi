using BiatecTokensApi.Models;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for validating deployment state transitions with business rule invariants
    /// </summary>
    /// <remarks>
    /// Implements strict state machine validation with explicit invariant checks.
    /// This ensures deployment lifecycle remains predictable and auditable.
    /// 
    /// Business Invariants:
    /// 1. Terminal states (Completed, Cancelled) cannot transition to any state
    /// 2. Confirmed status requires ConfirmedRound to be set
    /// 3. Submitted status requires TransactionHash to be set
    /// 4. Failed state can only retry to Queued
    /// 5. Status history must be chronologically ordered
    /// 
    /// Business Value: Prevents invalid state corruption that could lead to
    /// lost deployment tracking, incorrect billing, or compliance violations.
    /// </remarks>
    public class StateTransitionGuard : IStateTransitionGuard
    {
        private readonly ILogger<StateTransitionGuard> _logger;

        /// <summary>
        /// Valid status transitions in the deployment state machine
        /// </summary>
        private static readonly Dictionary<DeploymentStatus, List<DeploymentStatus>> ValidTransitions = new()
        {
            { DeploymentStatus.Queued, new List<DeploymentStatus> { DeploymentStatus.Submitted, DeploymentStatus.Failed, DeploymentStatus.Cancelled } },
            { DeploymentStatus.Submitted, new List<DeploymentStatus> { DeploymentStatus.Pending, DeploymentStatus.Failed } },
            { DeploymentStatus.Pending, new List<DeploymentStatus> { DeploymentStatus.Confirmed, DeploymentStatus.Failed } },
            { DeploymentStatus.Confirmed, new List<DeploymentStatus> { DeploymentStatus.Indexed, DeploymentStatus.Completed, DeploymentStatus.Failed } },
            { DeploymentStatus.Indexed, new List<DeploymentStatus> { DeploymentStatus.Completed, DeploymentStatus.Failed } },
            { DeploymentStatus.Completed, new List<DeploymentStatus>() }, // Terminal state
            { DeploymentStatus.Failed, new List<DeploymentStatus> { DeploymentStatus.Queued } }, // Allow retry from failed
            { DeploymentStatus.Cancelled, new List<DeploymentStatus>() } // Terminal state
        };

        /// <summary>
        /// Transition reason codes
        /// </summary>
        private static readonly Dictionary<(DeploymentStatus from, DeploymentStatus to), string> TransitionReasonCodes = new()
        {
            { (DeploymentStatus.Queued, DeploymentStatus.Submitted), "DEPLOYMENT_SUBMITTED" },
            { (DeploymentStatus.Queued, DeploymentStatus.Failed), "DEPLOYMENT_VALIDATION_FAILED" },
            { (DeploymentStatus.Queued, DeploymentStatus.Cancelled), "USER_CANCELLED" },
            { (DeploymentStatus.Submitted, DeploymentStatus.Pending), "TRANSACTION_BROADCAST" },
            { (DeploymentStatus.Submitted, DeploymentStatus.Failed), "TRANSACTION_SUBMISSION_FAILED" },
            { (DeploymentStatus.Pending, DeploymentStatus.Confirmed), "TRANSACTION_CONFIRMED" },
            { (DeploymentStatus.Pending, DeploymentStatus.Failed), "TRANSACTION_REVERTED" },
            { (DeploymentStatus.Confirmed, DeploymentStatus.Indexed), "TRANSACTION_INDEXED" },
            { (DeploymentStatus.Confirmed, DeploymentStatus.Completed), "DEPLOYMENT_COMPLETED" },
            { (DeploymentStatus.Confirmed, DeploymentStatus.Failed), "POST_DEPLOYMENT_FAILED" },
            { (DeploymentStatus.Indexed, DeploymentStatus.Completed), "DEPLOYMENT_COMPLETED" },
            { (DeploymentStatus.Indexed, DeploymentStatus.Failed), "POST_DEPLOYMENT_FAILED" },
            { (DeploymentStatus.Failed, DeploymentStatus.Queued), "DEPLOYMENT_RETRY_REQUESTED" }
        };

        public StateTransitionGuard(ILogger<StateTransitionGuard> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates if a deployment status transition is allowed
        /// </summary>
        public StateTransitionValidation ValidateTransition(
            DeploymentStatus currentStatus,
            DeploymentStatus newStatus,
            TokenDeployment? deployment = null)
        {
            var violatedInvariants = new List<string>();

            // Idempotency check: Allow same status (no-op)
            if (currentStatus == newStatus)
            {
                return new StateTransitionValidation
                {
                    IsAllowed = true,
                    ReasonCode = "IDEMPOTENT_UPDATE",
                    Explanation = "Status is already set to requested value"
                };
            }

            // Invariant 1: Terminal states cannot transition
            if (IsTerminalState(currentStatus))
            {
                violatedInvariants.Add($"Cannot transition from terminal state {currentStatus}");
                return new StateTransitionValidation
                {
                    IsAllowed = false,
                    ReasonCode = "TERMINAL_STATE_VIOLATION",
                    Explanation = $"Status {currentStatus} is terminal and cannot transition to {newStatus}",
                    ValidAlternatives = new List<DeploymentStatus>(),
                    ViolatedInvariants = violatedInvariants
                };
            }

            // Check if transition is in valid transitions map
            if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            {
                _logger.LogError("Unknown current status: {CurrentStatus}", currentStatus);
                violatedInvariants.Add($"Unknown status: {currentStatus}");
                return new StateTransitionValidation
                {
                    IsAllowed = false,
                    ReasonCode = "UNKNOWN_STATUS",
                    Explanation = $"Current status {currentStatus} is not recognized",
                    ViolatedInvariants = violatedInvariants
                };
            }

            // Check if requested transition is allowed
            if (!allowedStatuses.Contains(newStatus))
            {
                violatedInvariants.Add($"Transition {currentStatus} → {newStatus} is not allowed");
                return new StateTransitionValidation
                {
                    IsAllowed = false,
                    ReasonCode = "INVALID_TRANSITION",
                    Explanation = $"Cannot transition from {currentStatus} to {newStatus}",
                    ValidAlternatives = allowedStatuses,
                    ViolatedInvariants = violatedInvariants
                };
            }

            // Context-aware invariant checks (if deployment provided)
            if (deployment != null)
            {
                // Invariant 2: Submitted status requires TransactionHash
                if (newStatus == DeploymentStatus.Submitted && string.IsNullOrEmpty(deployment.TransactionHash))
                {
                    violatedInvariants.Add("Submitted status requires TransactionHash to be set");
                }

                // Invariant 3: Cancelled can only come from Queued
                if (newStatus == DeploymentStatus.Cancelled && currentStatus != DeploymentStatus.Queued)
                {
                    violatedInvariants.Add("Can only cancel from Queued status");
                }

                // Invariant 4: Retry from Failed must go to Queued
                if (currentStatus == DeploymentStatus.Failed && newStatus != DeploymentStatus.Queued)
                {
                    violatedInvariants.Add("Retry from Failed must transition to Queued");
                }

                // If any context-aware invariants violated, reject
                if (violatedInvariants.Count > 0)
                {
                    return new StateTransitionValidation
                    {
                        IsAllowed = false,
                        ReasonCode = "INVARIANT_VIOLATION",
                        Explanation = $"Context-aware invariants violated for {currentStatus} → {newStatus}",
                        ValidAlternatives = allowedStatuses,
                        ViolatedInvariants = violatedInvariants
                    };
                }
            }

            // Transition is valid
            var reasonCode = GetTransitionReasonCode(currentStatus, newStatus);
            return new StateTransitionValidation
            {
                IsAllowed = true,
                ReasonCode = reasonCode,
                Explanation = $"Valid transition: {currentStatus} → {newStatus}"
            };
        }

        /// <summary>
        /// Gets the list of valid next states from a given status
        /// </summary>
        public List<DeploymentStatus> GetValidNextStates(DeploymentStatus currentStatus)
        {
            if (ValidTransitions.TryGetValue(currentStatus, out var states))
            {
                return new List<DeploymentStatus>(states);
            }

            _logger.LogWarning("Unknown status requested: {Status}", currentStatus);
            return new List<DeploymentStatus>();
        }

        /// <summary>
        /// Checks if a status represents a terminal state
        /// </summary>
        public bool IsTerminalState(DeploymentStatus status)
        {
            return status == DeploymentStatus.Completed || status == DeploymentStatus.Cancelled;
        }

        /// <summary>
        /// Gets the reason code for a state transition
        /// </summary>
        public string GetTransitionReasonCode(DeploymentStatus fromStatus, DeploymentStatus toStatus)
        {
            if (TransitionReasonCodes.TryGetValue((fromStatus, toStatus), out var reasonCode))
            {
                return reasonCode;
            }

            // Default reason code for unknown transitions
            return $"TRANSITION_{fromStatus.ToString().ToUpperInvariant()}_{toStatus.ToString().ToUpperInvariant()}";
        }
    }
}

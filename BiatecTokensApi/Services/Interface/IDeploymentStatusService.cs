using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing deployment status tracking and state transitions
    /// </summary>
    /// <remarks>
    /// Provides business logic for deployment status tracking including state machine
    /// validation, idempotency guards, and webhook notifications.
    /// </remarks>
    public interface IDeploymentStatusService
    {
        /// <summary>
        /// Creates a new deployment tracking record
        /// </summary>
        /// <param name="tokenType">The type of token being deployed</param>
        /// <param name="network">The network where the token is being deployed</param>
        /// <param name="deployedBy">The address of the user deploying the token</param>
        /// <param name="tokenName">The name of the token</param>
        /// <param name="tokenSymbol">The symbol of the token</param>
        /// <param name="correlationId">Optional correlation ID for tracking related events</param>
        /// <returns>The deployment ID</returns>
        Task<string> CreateDeploymentAsync(
            string tokenType,
            string network,
            string deployedBy,
            string? tokenName,
            string? tokenSymbol,
            string? correlationId = null);

        /// <summary>
        /// Updates the status of a deployment with validation and state machine logic
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="newStatus">The new status to transition to</param>
        /// <param name="message">Optional message providing context</param>
        /// <param name="transactionHash">Optional transaction hash</param>
        /// <param name="confirmedRound">Optional confirmed block/round number</param>
        /// <param name="errorMessage">Optional error message for failed status</param>
        /// <param name="metadata">Optional additional metadata</param>
        /// <returns>True if the status was updated successfully, false otherwise</returns>
        Task<bool> UpdateDeploymentStatusAsync(
            string deploymentId,
            DeploymentStatus newStatus,
            string? message = null,
            string? transactionHash = null,
            ulong? confirmedRound = null,
            string? errorMessage = null,
            Dictionary<string, object>? metadata = null);

        /// <summary>
        /// Gets a deployment by its ID
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>The deployment if found, null otherwise</returns>
        Task<TokenDeployment?> GetDeploymentAsync(string deploymentId);

        /// <summary>
        /// Gets deployments with filtering and pagination
        /// </summary>
        /// <param name="request">The filter request</param>
        /// <returns>Response containing filtered deployments</returns>
        Task<ListDeploymentsResponse> GetDeploymentsAsync(ListDeploymentsRequest request);

        /// <summary>
        /// Gets the complete status history for a deployment
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>List of status entries ordered chronologically</returns>
        Task<List<DeploymentStatusEntry>> GetStatusHistoryAsync(string deploymentId);

        /// <summary>
        /// Validates if a status transition is allowed
        /// </summary>
        /// <param name="currentStatus">The current status</param>
        /// <param name="newStatus">The desired new status</param>
        /// <returns>True if the transition is valid, false otherwise</returns>
        bool IsValidStatusTransition(DeploymentStatus currentStatus, DeploymentStatus newStatus);

        /// <summary>
        /// Updates the asset identifier for a deployment after successful deployment
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="assetIdentifier">The asset ID or contract address</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task UpdateAssetIdentifierAsync(string deploymentId, string assetIdentifier);

        /// <summary>
        /// Marks a deployment as failed with retry capability check
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="errorMessage">The error message</param>
        /// <param name="isRetryable">Whether the failure is retryable</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task MarkDeploymentFailedAsync(string deploymentId, string errorMessage, bool isRetryable = false);
    }
}

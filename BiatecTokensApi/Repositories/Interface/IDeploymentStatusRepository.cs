using BiatecTokensApi.Models;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for deployment status tracking operations
    /// </summary>
    /// <remarks>
    /// Provides thread-safe operations for tracking token deployment status throughout
    /// the deployment lifecycle. Supports real-time status updates and audit trail queries.
    /// </remarks>
    public interface IDeploymentStatusRepository
    {
        /// <summary>
        /// Creates a new deployment record
        /// </summary>
        /// <param name="deployment">The deployment to create</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task CreateDeploymentAsync(TokenDeployment deployment);

        /// <summary>
        /// Updates an existing deployment record
        /// </summary>
        /// <param name="deployment">The deployment to update</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task UpdateDeploymentAsync(TokenDeployment deployment);

        /// <summary>
        /// Gets a deployment by its ID
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>The deployment if found, null otherwise</returns>
        Task<TokenDeployment?> GetDeploymentByIdAsync(string deploymentId);

        /// <summary>
        /// Gets deployments with filtering and pagination
        /// </summary>
        /// <param name="request">The filter request</param>
        /// <returns>List of deployments matching the filter</returns>
        Task<List<TokenDeployment>> GetDeploymentsAsync(ListDeploymentsRequest request);

        /// <summary>
        /// Gets the total count of deployments matching the filter
        /// </summary>
        /// <param name="request">The filter request</param>
        /// <returns>Total count of matching deployments</returns>
        Task<int> GetDeploymentsCountAsync(ListDeploymentsRequest request);

        /// <summary>
        /// Adds a status entry to a deployment's history
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="statusEntry">The status entry to add</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task AddStatusEntryAsync(string deploymentId, DeploymentStatusEntry statusEntry);

        /// <summary>
        /// Gets all status entries for a deployment ordered chronologically
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>List of status entries</returns>
        Task<List<DeploymentStatusEntry>> GetStatusHistoryAsync(string deploymentId);
    }
}

using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Repository implementation for deployment status tracking
    /// </summary>
    /// <remarks>
    /// Provides thread-safe, in-memory storage for deployment status tracking.
    /// Supports real-time status updates and comprehensive audit trail queries.
    /// </remarks>
    public class DeploymentStatusRepository : IDeploymentStatusRepository
    {
        private readonly ConcurrentDictionary<string, TokenDeployment> _deployments = new();
        private readonly ILogger<DeploymentStatusRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentStatusRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public DeploymentStatusRepository(ILogger<DeploymentStatusRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a new deployment record
        /// </summary>
        public Task CreateDeploymentAsync(TokenDeployment deployment)
        {
            if (deployment == null)
            {
                throw new ArgumentNullException(nameof(deployment));
            }

            if (string.IsNullOrWhiteSpace(deployment.DeploymentId))
            {
                throw new ArgumentException("DeploymentId cannot be empty", nameof(deployment));
            }

            if (!_deployments.TryAdd(deployment.DeploymentId, deployment))
            {
                _logger.LogWarning("Deployment with ID {DeploymentId} already exists", deployment.DeploymentId);
                throw new InvalidOperationException($"Deployment with ID {deployment.DeploymentId} already exists");
            }

            _logger.LogInformation("Created deployment: DeploymentId={DeploymentId}, TokenType={TokenType}, Network={Network}, DeployedBy={DeployedBy}",
                deployment.DeploymentId, deployment.TokenType, deployment.Network, deployment.DeployedBy);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates an existing deployment record
        /// </summary>
        public Task UpdateDeploymentAsync(TokenDeployment deployment)
        {
            if (deployment == null)
            {
                throw new ArgumentNullException(nameof(deployment));
            }

            if (string.IsNullOrWhiteSpace(deployment.DeploymentId))
            {
                throw new ArgumentException("DeploymentId cannot be empty", nameof(deployment));
            }

            deployment.UpdatedAt = DateTime.UtcNow;

            if (!_deployments.TryGetValue(deployment.DeploymentId, out _))
            {
                _logger.LogWarning("Deployment with ID {DeploymentId} not found for update", deployment.DeploymentId);
                throw new InvalidOperationException($"Deployment with ID {deployment.DeploymentId} not found");
            }

            _deployments[deployment.DeploymentId] = deployment;

            _logger.LogInformation("Updated deployment: DeploymentId={DeploymentId}, CurrentStatus={Status}",
                deployment.DeploymentId, deployment.CurrentStatus);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a deployment by its ID
        /// </summary>
        public Task<TokenDeployment?> GetDeploymentByIdAsync(string deploymentId)
        {
            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                throw new ArgumentException("DeploymentId cannot be empty", nameof(deploymentId));
            }

            _deployments.TryGetValue(deploymentId, out var deployment);

            if (deployment != null)
            {
                _logger.LogDebug("Retrieved deployment: DeploymentId={DeploymentId}, Status={Status}",
                    deploymentId, deployment.CurrentStatus);
            }
            else
            {
                _logger.LogDebug("Deployment not found: DeploymentId={DeploymentId}", deploymentId);
            }

            return Task.FromResult(deployment);
        }

        /// <summary>
        /// Gets deployments with filtering and pagination
        /// </summary>
        public Task<List<TokenDeployment>> GetDeploymentsAsync(ListDeploymentsRequest request)
        {
            _logger.LogInformation("Retrieving deployments: DeployedBy={DeployedBy}, Network={Network}, TokenType={TokenType}, Status={Status}",
                request.DeployedBy, request.Network, request.TokenType, request.Status);

            var query = _deployments.Values.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.DeployedBy))
                query = query.Where(d => d.DeployedBy.Equals(request.DeployedBy, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.Network))
                query = query.Where(d => d.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.TokenType))
                query = query.Where(d => d.TokenType.Equals(request.TokenType, StringComparison.OrdinalIgnoreCase));

            if (request.Status.HasValue)
                query = query.Where(d => d.CurrentStatus == request.Status.Value);

            if (request.FromDate.HasValue)
                query = query.Where(d => d.CreatedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(d => d.CreatedAt <= request.ToDate.Value);

            // Order by most recent first and apply pagination
            var orderedQuery = query.OrderByDescending(d => d.CreatedAt);
            var skip = (request.Page - 1) * request.PageSize;
            var result = orderedQuery.Skip(skip).Take(request.PageSize).ToList();

            _logger.LogInformation("Retrieved {Count} deployments", result.Count);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets the total count of deployments matching the filter
        /// </summary>
        public Task<int> GetDeploymentsCountAsync(ListDeploymentsRequest request)
        {
            var query = _deployments.Values.AsEnumerable();

            // Apply same filters as GetDeploymentsAsync
            if (!string.IsNullOrWhiteSpace(request.DeployedBy))
                query = query.Where(d => d.DeployedBy.Equals(request.DeployedBy, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.Network))
                query = query.Where(d => d.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.TokenType))
                query = query.Where(d => d.TokenType.Equals(request.TokenType, StringComparison.OrdinalIgnoreCase));

            if (request.Status.HasValue)
                query = query.Where(d => d.CurrentStatus == request.Status.Value);

            if (request.FromDate.HasValue)
                query = query.Where(d => d.CreatedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(d => d.CreatedAt <= request.ToDate.Value);

            var count = query.Count();
            _logger.LogDebug("Deployments count: {Count}", count);
            return Task.FromResult(count);
        }

        /// <summary>
        /// Adds a status entry to a deployment's history
        /// </summary>
        public Task AddStatusEntryAsync(string deploymentId, DeploymentStatusEntry statusEntry)
        {
            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                throw new ArgumentException("DeploymentId cannot be empty", nameof(deploymentId));
            }

            if (statusEntry == null)
            {
                throw new ArgumentNullException(nameof(statusEntry));
            }

            if (!_deployments.TryGetValue(deploymentId, out var deployment))
            {
                _logger.LogWarning("Deployment with ID {DeploymentId} not found for status update", deploymentId);
                throw new InvalidOperationException($"Deployment with ID {deploymentId} not found");
            }

            statusEntry.DeploymentId = deploymentId;
            statusEntry.Timestamp = DateTime.UtcNow;

            deployment.StatusHistory.Add(statusEntry);
            deployment.CurrentStatus = statusEntry.Status;
            deployment.UpdatedAt = DateTime.UtcNow;

            // Update transaction hash and error message if provided
            if (!string.IsNullOrWhiteSpace(statusEntry.TransactionHash))
            {
                deployment.TransactionHash = statusEntry.TransactionHash;
            }

            if (!string.IsNullOrWhiteSpace(statusEntry.ErrorMessage))
            {
                deployment.ErrorMessage = statusEntry.ErrorMessage;
            }

            _logger.LogInformation("Added status entry: DeploymentId={DeploymentId}, Status={Status}, Message={Message}",
                deploymentId, statusEntry.Status, statusEntry.Message);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets all status entries for a deployment ordered chronologically
        /// </summary>
        public Task<List<DeploymentStatusEntry>> GetStatusHistoryAsync(string deploymentId)
        {
            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                throw new ArgumentException("DeploymentId cannot be empty", nameof(deploymentId));
            }

            if (!_deployments.TryGetValue(deploymentId, out var deployment))
            {
                _logger.LogWarning("Deployment with ID {DeploymentId} not found", deploymentId);
                return Task.FromResult(new List<DeploymentStatusEntry>());
            }

            var history = deployment.StatusHistory.OrderBy(e => e.Timestamp).ToList();
            _logger.LogDebug("Retrieved {Count} status entries for deployment {DeploymentId}", history.Count, deploymentId);

            return Task.FromResult(history);
        }
    }
}

using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing deployment status tracking and state transitions
    /// </summary>
    /// <remarks>
    /// Implements business logic for deployment status tracking including:
    /// - State machine validation for status transitions
    /// - Idempotency guards to prevent duplicate status updates
    /// - Webhook notifications for status changes
    /// - Retry logic for transient failures
    /// </remarks>
    public class DeploymentStatusService : IDeploymentStatusService
    {
        private readonly IDeploymentStatusRepository _repository;
        private readonly IWebhookService _webhookService;
        private readonly ILogger<DeploymentStatusService> _logger;

        /// <summary>
        /// Valid status transitions in the deployment state machine
        /// </summary>
        private static readonly Dictionary<DeploymentStatus, List<DeploymentStatus>> ValidTransitions = new()
        {
            { DeploymentStatus.Queued, new List<DeploymentStatus> { DeploymentStatus.Submitted, DeploymentStatus.Failed } },
            { DeploymentStatus.Submitted, new List<DeploymentStatus> { DeploymentStatus.Pending, DeploymentStatus.Failed } },
            { DeploymentStatus.Pending, new List<DeploymentStatus> { DeploymentStatus.Confirmed, DeploymentStatus.Failed } },
            { DeploymentStatus.Confirmed, new List<DeploymentStatus> { DeploymentStatus.Completed, DeploymentStatus.Failed } },
            { DeploymentStatus.Completed, new List<DeploymentStatus>() }, // Terminal state
            { DeploymentStatus.Failed, new List<DeploymentStatus> { DeploymentStatus.Queued } } // Allow retry from failed
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentStatusService"/> class.
        /// </summary>
        /// <param name="repository">The deployment status repository</param>
        /// <param name="webhookService">The webhook service for notifications</param>
        /// <param name="logger">The logger instance</param>
        public DeploymentStatusService(
            IDeploymentStatusRepository repository,
            IWebhookService webhookService,
            ILogger<DeploymentStatusService> logger)
        {
            _repository = repository;
            _webhookService = webhookService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new deployment tracking record
        /// </summary>
        public async Task<string> CreateDeploymentAsync(
            string tokenType,
            string network,
            string deployedBy,
            string? tokenName,
            string? tokenSymbol,
            string? correlationId = null)
        {
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CurrentStatus = DeploymentStatus.Queued,
                TokenType = tokenType,
                Network = network,
                DeployedBy = deployedBy,
                TokenName = tokenName,
                TokenSymbol = tokenSymbol,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString()
            };

            // Add initial status entry
            deployment.StatusHistory.Add(new DeploymentStatusEntry
            {
                DeploymentId = deployment.DeploymentId,
                Status = DeploymentStatus.Queued,
                Message = "Deployment request queued for processing",
                Timestamp = DateTime.UtcNow
            });

            await _repository.CreateDeploymentAsync(deployment);

            _logger.LogInformation("Created deployment: DeploymentId={DeploymentId}, TokenType={TokenType}, Network={Network}",
                deployment.DeploymentId, tokenType, network);

            // Send webhook notification
            await SendDeploymentWebhookAsync(deployment, DeploymentStatus.Queued);

            return deployment.DeploymentId;
        }

        /// <summary>
        /// Updates the status of a deployment with validation and state machine logic
        /// </summary>
        public async Task<bool> UpdateDeploymentStatusAsync(
            string deploymentId,
            DeploymentStatus newStatus,
            string? message = null,
            string? transactionHash = null,
            ulong? confirmedRound = null,
            string? errorMessage = null,
            Dictionary<string, object>? metadata = null)
        {
            try
            {
                var deployment = await _repository.GetDeploymentByIdAsync(deploymentId);
                if (deployment == null)
                {
                    _logger.LogWarning("Deployment not found: DeploymentId={DeploymentId}", deploymentId);
                    return false;
                }

                // Validate state transition
                if (!IsValidStatusTransition(deployment.CurrentStatus, newStatus))
                {
                    _logger.LogWarning("Invalid status transition: DeploymentId={DeploymentId}, CurrentStatus={CurrentStatus}, NewStatus={NewStatus}",
                        deploymentId, deployment.CurrentStatus, newStatus);
                    return false;
                }

                // Check for duplicate status (idempotency guard)
                if (deployment.CurrentStatus == newStatus)
                {
                    _logger.LogDebug("Status already set: DeploymentId={DeploymentId}, Status={Status}",
                        deploymentId, newStatus);
                    return true; // Not an error, just a no-op
                }

                // Create status entry
                var statusEntry = new DeploymentStatusEntry
                {
                    DeploymentId = deploymentId,
                    Status = newStatus,
                    Message = message,
                    TransactionHash = transactionHash,
                    ConfirmedRound = confirmedRound,
                    ErrorMessage = errorMessage,
                    Metadata = metadata,
                    Timestamp = DateTime.UtcNow
                };

                // Add status entry to deployment
                await _repository.AddStatusEntryAsync(deploymentId, statusEntry);

                _logger.LogInformation("Updated deployment status: DeploymentId={DeploymentId}, Status={Status}, Message={Message}",
                    deploymentId, newStatus, message);

                // Get updated deployment for webhook
                deployment = await _repository.GetDeploymentByIdAsync(deploymentId);
                if (deployment != null)
                {
                    // Send webhook notification
                    await SendDeploymentWebhookAsync(deployment, newStatus);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating deployment status: DeploymentId={DeploymentId}, NewStatus={NewStatus}",
                    deploymentId, newStatus);
                return false;
            }
        }

        /// <summary>
        /// Gets a deployment by its ID
        /// </summary>
        public async Task<TokenDeployment?> GetDeploymentAsync(string deploymentId)
        {
            return await _repository.GetDeploymentByIdAsync(deploymentId);
        }

        /// <summary>
        /// Gets deployments with filtering and pagination
        /// </summary>
        public async Task<ListDeploymentsResponse> GetDeploymentsAsync(ListDeploymentsRequest request)
        {
            try
            {
                // Validate and sanitize pagination parameters
                if (request.Page < 1) request.Page = 1;
                if (request.PageSize < 1) request.PageSize = 50;
                if (request.PageSize > 100) request.PageSize = 100;

                var deployments = await _repository.GetDeploymentsAsync(request);
                var totalCount = await _repository.GetDeploymentsCountAsync(request);
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                return new ListDeploymentsResponse
                {
                    Success = true,
                    Deployments = deployments,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deployments");
                return new ListDeploymentsResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve deployments"
                };
            }
        }

        /// <summary>
        /// Gets the complete status history for a deployment
        /// </summary>
        public async Task<List<DeploymentStatusEntry>> GetStatusHistoryAsync(string deploymentId)
        {
            return await _repository.GetStatusHistoryAsync(deploymentId);
        }

        /// <summary>
        /// Validates if a status transition is allowed
        /// </summary>
        public bool IsValidStatusTransition(DeploymentStatus currentStatus, DeploymentStatus newStatus)
        {
            // Allow same status (idempotency)
            if (currentStatus == newStatus)
            {
                return true;
            }

            // Check if transition is in the valid transitions map
            if (ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            {
                return allowedStatuses.Contains(newStatus);
            }

            return false;
        }

        /// <summary>
        /// Updates the asset identifier for a deployment after successful deployment
        /// </summary>
        public async Task UpdateAssetIdentifierAsync(string deploymentId, string assetIdentifier)
        {
            var deployment = await _repository.GetDeploymentByIdAsync(deploymentId);
            if (deployment == null)
            {
                _logger.LogWarning("Deployment not found for asset identifier update: DeploymentId={DeploymentId}", deploymentId);
                return;
            }

            deployment.AssetIdentifier = assetIdentifier;
            await _repository.UpdateDeploymentAsync(deployment);

            _logger.LogInformation("Updated asset identifier: DeploymentId={DeploymentId}, AssetIdentifier={AssetIdentifier}",
                deploymentId, assetIdentifier);
        }

        /// <summary>
        /// Marks a deployment as failed with retry capability check
        /// </summary>
        public async Task MarkDeploymentFailedAsync(string deploymentId, string errorMessage, bool isRetryable = false)
        {
            var metadata = new Dictionary<string, object>
            {
                { "isRetryable", isRetryable }
            };

            await UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Failed,
                isRetryable ? "Deployment failed - retry possible" : "Deployment failed",
                errorMessage: errorMessage,
                metadata: metadata);

            _logger.LogWarning("Deployment failed: DeploymentId={DeploymentId}, IsRetryable={IsRetryable}, Error={Error}",
                deploymentId, isRetryable, errorMessage);
        }

        /// <summary>
        /// Sends a webhook notification for a deployment status change
        /// </summary>
        private async Task SendDeploymentWebhookAsync(TokenDeployment deployment, DeploymentStatus status)
        {
            try
            {
                // Determine webhook event type based on status
                WebhookEventType eventType = status switch
                {
                    DeploymentStatus.Queued => WebhookEventType.TokenDeploymentStarted,
                    DeploymentStatus.Submitted => WebhookEventType.TokenDeploymentStarted,
                    DeploymentStatus.Pending => WebhookEventType.TokenDeploymentConfirming,
                    DeploymentStatus.Confirmed => WebhookEventType.TokenDeploymentConfirming,
                    DeploymentStatus.Completed => WebhookEventType.TokenDeploymentCompleted,
                    DeploymentStatus.Failed => WebhookEventType.TokenDeploymentFailed,
                    _ => WebhookEventType.TokenDeploymentStarted
                };

                // Create webhook payload
                var payload = new
                {
                    deploymentId = deployment.DeploymentId,
                    status = status.ToString(),
                    tokenType = deployment.TokenType,
                    network = deployment.Network,
                    tokenName = deployment.TokenName,
                    tokenSymbol = deployment.TokenSymbol,
                    assetIdentifier = deployment.AssetIdentifier,
                    transactionHash = deployment.TransactionHash,
                    deployedBy = deployment.DeployedBy,
                    createdAt = deployment.CreatedAt,
                    updatedAt = deployment.UpdatedAt,
                    errorMessage = deployment.ErrorMessage,
                    correlationId = deployment.CorrelationId
                };

                await _webhookService.TriggerWebhookAsync(
                    eventType,
                    payload,
                    deployment.Network,
                    deployment.AssetIdentifier);

                _logger.LogDebug("Webhook triggered: EventType={EventType}, DeploymentId={DeploymentId}",
                    eventType, deployment.DeploymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending deployment webhook: DeploymentId={DeploymentId}", deployment.DeploymentId);
                // Don't throw - webhook failures shouldn't block deployment tracking
            }
        }
    }
}

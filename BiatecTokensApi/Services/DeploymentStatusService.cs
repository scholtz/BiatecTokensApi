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
        /// <remarks>
        /// State Machine Flow:
        /// Queued → Submitted → Pending → Confirmed → Indexed → Completed
        ///   ↓         ↓          ↓          ↓          ↓         ↓
        /// Failed ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← (from any non-terminal state)
        ///   ↓
        /// Queued (retry allowed)
        /// 
        /// Queued → Cancelled (user-initiated)
        /// </remarks>
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
        /// Marks a deployment as failed with structured error details
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="error">Structured error details</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task MarkDeploymentFailedAsync(string deploymentId, DeploymentError error)
        {
            var metadata = new Dictionary<string, object>
            {
                { "isRetryable", error.IsRetryable },
                { "errorCategory", error.Category.ToString() },
                { "errorCode", error.ErrorCode }
            };

            if (error.SuggestedRetryDelaySeconds.HasValue)
            {
                metadata["suggestedRetryDelaySeconds"] = error.SuggestedRetryDelaySeconds.Value;
            }

            await UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Failed,
                error.UserMessage,
                errorMessage: error.TechnicalMessage,
                metadata: metadata);

            _logger.LogWarning("Deployment failed: DeploymentId={DeploymentId}, Category={Category}, Code={Code}, Retryable={Retryable}",
                deploymentId, error.Category, error.ErrorCode, error.IsRetryable);
        }

        /// <summary>
        /// Cancels a deployment (only allowed from Queued state)
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="reason">Reason for cancellation</param>
        /// <returns>True if cancelled successfully, false otherwise</returns>
        public async Task<bool> CancelDeploymentAsync(string deploymentId, string reason)
        {
            var deployment = await _repository.GetDeploymentByIdAsync(deploymentId);
            if (deployment == null)
            {
                _logger.LogWarning("Deployment not found for cancellation: DeploymentId={DeploymentId}", deploymentId);
                return false;
            }

            // Can only cancel from Queued state
            if (deployment.CurrentStatus != DeploymentStatus.Queued)
            {
                _logger.LogWarning("Cannot cancel deployment in status {Status}: DeploymentId={DeploymentId}",
                    deployment.CurrentStatus, deploymentId);
                return false;
            }

            await UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Cancelled,
                $"Cancelled by user: {reason}",
                metadata: new Dictionary<string, object> { { "reason", reason } });

            _logger.LogInformation("Deployment cancelled: DeploymentId={DeploymentId}, Reason={Reason}",
                deploymentId, reason);

            return true;
        }

        /// <summary>
        /// Calculates deployment metrics for a given time period
        /// </summary>
        /// <param name="request">Metrics request with filters</param>
        /// <returns>Calculated metrics</returns>
        public async Task<DeploymentMetrics> GetDeploymentMetricsAsync(GetDeploymentMetricsRequest request)
        {
            // Set default date range if not provided (last 24 hours)
            var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
            var toDate = request.ToDate ?? DateTime.UtcNow;

            // Get all deployments in the period
            var listRequest = new ListDeploymentsRequest
            {
                Network = request.Network,
                TokenType = request.TokenType,
                DeployedBy = request.DeployedBy,
                FromDate = fromDate,
                ToDate = toDate,
                Page = 1,
                PageSize = 10000 // Get all for metrics
            };

            var deployments = await _repository.GetDeploymentsAsync(listRequest);

            // Calculate status counts
            var successful = deployments.Count(d => d.CurrentStatus == DeploymentStatus.Completed);
            var failed = deployments.Count(d => d.CurrentStatus == DeploymentStatus.Failed);
            var pending = deployments.Count(d => d.CurrentStatus != DeploymentStatus.Completed &&
                                                  d.CurrentStatus != DeploymentStatus.Failed &&
                                                  d.CurrentStatus != DeploymentStatus.Cancelled);
            var cancelled = deployments.Count(d => d.CurrentStatus == DeploymentStatus.Cancelled);
            var total = deployments.Count;

            // Calculate rates
            var successRate = total > 0 ? (double)successful / total * 100 : 0;
            var failureRate = total > 0 ? (double)failed / total * 100 : 0;

            // Calculate durations for completed deployments
            var completedDeployments = deployments
                .Where(d => d.CurrentStatus == DeploymentStatus.Completed)
                .ToList();

            var durations = new List<long>();
            var transitionDurations = new Dictionary<string, List<long>>();

            foreach (var deployment in completedDeployments)
            {
                var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);
                if (history.Count >= 2)
                {
                    var first = history.OrderBy(e => e.Timestamp).First();
                    var last = history.OrderBy(e => e.Timestamp).Last();
                    var duration = (long)(last.Timestamp - first.Timestamp).TotalMilliseconds;
                    durations.Add(duration);

                    // Calculate transition durations
                    for (int i = 1; i < history.Count; i++)
                    {
                        var prev = history[i - 1];
                        var curr = history[i];
                        var transitionKey = $"{prev.Status}->{curr.Status}";
                        var transitionDuration = (long)(curr.Timestamp - prev.Timestamp).TotalMilliseconds;

                        if (!transitionDurations.ContainsKey(transitionKey))
                        {
                            transitionDurations[transitionKey] = new List<long>();
                        }
                        transitionDurations[transitionKey].Add(transitionDuration);
                    }
                }
            }

            // Calculate duration statistics
            var avgDuration = durations.Any() ? (long)durations.Average() : 0;
            var medianDuration = CalculateMedian(durations);
            var p95Duration = CalculatePercentile(durations, 95);
            var fastestDuration = durations.Any() ? durations.Min() : 0;
            var slowestDuration = durations.Any() ? durations.Max() : 0;

            // Calculate average duration by transition
            var avgDurationByTransition = transitionDurations
                .ToDictionary(kvp => kvp.Key, kvp => (long)kvp.Value.Average());

            // Failure breakdown by category
            var failuresByCategory = new Dictionary<string, int>();
            var failedDeploymentsList = deployments.Where(d => d.CurrentStatus == DeploymentStatus.Failed).ToList();
            
            foreach (var deployment in failedDeploymentsList)
            {
                var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);
                var failedEntry = history.FirstOrDefault(e => e.Status == DeploymentStatus.Failed);
                
                if (failedEntry?.Metadata?.ContainsKey("errorCategory") == true)
                {
                    var category = failedEntry.Metadata["errorCategory"].ToString() ?? "Unknown";
                    if (!failuresByCategory.ContainsKey(category))
                    {
                        failuresByCategory[category] = 0;
                    }
                    failuresByCategory[category]++;
                }
            }

            // Count retries (deployments that went from Failed to Queued)
            var retriedCount = 0;
            foreach (var deployment in deployments)
            {
                var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);
                var hasRetry = history.Any(e => e.Status == DeploymentStatus.Queued && 
                                                history.Any(h => h.Status == DeploymentStatus.Failed && h.Timestamp < e.Timestamp));
                if (hasRetry) retriedCount++;
            }

            // Deployments by network
            var byNetwork = deployments
                .GroupBy(d => d.Network)
                .ToDictionary(g => g.Key, g => g.Count());

            // Deployments by token type
            var byTokenType = deployments
                .GroupBy(d => d.TokenType)
                .ToDictionary(g => g.Key, g => g.Count());

            return new DeploymentMetrics
            {
                TotalDeployments = total,
                SuccessfulDeployments = successful,
                FailedDeployments = failed,
                PendingDeployments = pending,
                CancelledDeployments = cancelled,
                SuccessRate = successRate,
                FailureRate = failureRate,
                AverageDurationMs = avgDuration,
                MedianDurationMs = medianDuration,
                P95DurationMs = p95Duration,
                FastestDurationMs = fastestDuration,
                SlowestDurationMs = slowestDuration,
                FailuresByCategory = failuresByCategory,
                DeploymentsByNetwork = byNetwork,
                DeploymentsByTokenType = byTokenType,
                AverageDurationByTransition = avgDurationByTransition,
                RetriedDeployments = retriedCount,
                PeriodStart = fromDate,
                PeriodEnd = toDate
            };
        }

        private long CalculateMedian(List<long> values)
        {
            if (!values.Any()) return 0;

            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;

            if (count % 2 == 0)
            {
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
            }
            else
            {
                return sorted[count / 2];
            }
        }

        private long CalculatePercentile(List<long> values, int percentile)
        {
            if (!values.Any()) return 0;

            var sorted = values.OrderBy(v => v).ToList();
            int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            index = Math.Max(0, Math.Min(index, sorted.Count - 1));

            return sorted[index];
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

                // Create webhook event
                var webhookEvent = new WebhookEvent
                {
                    EventType = eventType,
                    Actor = deployment.DeployedBy,
                    Network = deployment.Network,
                    Data = new Dictionary<string, object>
                    {
                        { "deploymentId", deployment.DeploymentId },
                        { "status", status.ToString() },
                        { "tokenType", deployment.TokenType },
                        { "tokenName", deployment.TokenName ?? string.Empty },
                        { "tokenSymbol", deployment.TokenSymbol ?? string.Empty },
                        { "assetIdentifier", deployment.AssetIdentifier ?? string.Empty },
                        { "transactionHash", deployment.TransactionHash ?? string.Empty },
                        { "createdAt", deployment.CreatedAt.ToString("o") },
                        { "updatedAt", deployment.UpdatedAt.ToString("o") },
                        { "errorMessage", deployment.ErrorMessage ?? string.Empty },
                        { "correlationId", deployment.CorrelationId ?? string.Empty }
                    }
                };

                // Try to parse asset ID if present
                if (!string.IsNullOrEmpty(deployment.AssetIdentifier) && 
                    ulong.TryParse(deployment.AssetIdentifier, out var assetId))
                {
                    webhookEvent.AssetId = assetId;
                }

                await _webhookService.EmitEventAsync(webhookEvent);

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

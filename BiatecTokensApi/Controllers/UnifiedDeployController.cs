using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.UnifiedDeploy;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Unified token deployment endpoints supporting multiple chains and token standards
    /// </summary>
    /// <remarks>
    /// All endpoints require JWT Bearer authentication. Deployments are queued for async processing
    /// and can be polled via the status endpoint.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/tokens")]
    public class UnifiedDeployController : ControllerBase
    {
        private readonly IDeploymentStatusService _deploymentStatusService;
        private readonly ILogger<UnifiedDeployController> _logger;

        /// <summary>
        /// Supported chain + standard combinations
        /// </summary>
        private static readonly HashSet<string> SupportedStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ERC20", "ERC721"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="UnifiedDeployController"/> class.
        /// </summary>
        public UnifiedDeployController(
            IDeploymentStatusService deploymentStatusService,
            ILogger<UnifiedDeployController> logger)
        {
            _deploymentStatusService = deploymentStatusService;
            _logger = logger;
        }

        /// <summary>
        /// Queues a new token deployment job for the authenticated user
        /// </summary>
        /// <param name="request">Deployment request with chain, standard, and token parameters</param>
        /// <returns>Response containing the job ID for polling</returns>
        /// <remarks>
        /// Accepts a chain + standard + parameters payload and enqueues the deployment for
        /// background processing. Returns a job ID immediately for status polling.
        ///
        /// **Supported Standards:** ASA, ARC3, ARC200, ERC20, ERC721
        ///
        /// **Sample Request (ASA):**
        /// ```json
        /// {
        ///   "chain": "algorand-mainnet",
        ///   "standard": "ASA",
        ///   "params": {
        ///     "name": "My Token",
        ///     "unitName": "MTK",
        ///     "total": 1000000,
        ///     "decimals": 6
        ///   }
        /// }
        /// ```
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "jobId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "status": "Queued",
        ///   "message": "Token deployment queued successfully"
        /// }
        /// ```
        /// </remarks>
        [HttpPost("deploy")]
        [ProducesResponseType(typeof(UnifiedDeployResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UnifiedDeployResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Deploy([FromBody] UnifiedDeployRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new UnifiedDeployResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new UnifiedDeployResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid request parameters",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.Chain))
            {
                return BadRequest(new UnifiedDeployResponse
                {
                    Success = false,
                    ErrorCode = "INVALID_CHAIN",
                    ErrorMessage = "Chain is required",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.Standard) || !SupportedStandards.Contains(request.Standard))
            {
                return BadRequest(new UnifiedDeployResponse
                {
                    Success = false,
                    ErrorCode = "INVALID_STANDARD",
                    ErrorMessage = $"Standard must be one of: {string.Join(", ", SupportedStandards)}",
                    CorrelationId = correlationId
                });
            }

            try
            {
                // Extract optional token name and symbol from params
                var tokenName = GetStringParam(request.Params, "name");
                var tokenSymbol = GetStringParam(request.Params, "unitName") ?? GetStringParam(request.Params, "symbol");

                // Create deployment record in Queued state
                var jobId = await _deploymentStatusService.CreateDeploymentAsync(
                    tokenType: request.Standard,
                    network: request.Chain,
                    deployedBy: userId,
                    tokenName: tokenName,
                    tokenSymbol: tokenSymbol,
                    correlationId: correlationId);

                _logger.LogInformation(
                    "Token deployment queued. UserId={UserId}, Standard={Standard}, Chain={Chain}, JobId={JobId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(request.Standard),
                    LoggingHelper.SanitizeLogInput(request.Chain),
                    jobId,
                    correlationId);

                return Ok(new UnifiedDeployResponse
                {
                    Success = true,
                    JobId = jobId,
                    Status = DeploymentStatus.Queued.ToString(),
                    Message = "Token deployment queued successfully",
                    CorrelationId = correlationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error queuing token deployment. UserId={UserId}, Standard={Standard}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(request.Standard),
                    correlationId);

                return StatusCode(StatusCodes.Status500InternalServerError, new UnifiedDeployResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while queuing the deployment",
                    CorrelationId = correlationId
                });
            }
        }

        /// <summary>
        /// Polls the status of a token deployment job
        /// </summary>
        /// <param name="jobId">The deployment job ID returned from POST /deploy</param>
        /// <returns>Current deployment status and history</returns>
        /// <remarks>
        /// Returns the current status and complete status history for a deployment job.
        /// Status transitions: Queued → Submitted → Pending → Confirmed → Completed
        /// (or Failed at any stage).
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "jobId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "status": "Confirmed",
        ///   "assetIdentifier": "12345678",
        ///   "statusHistory": [
        ///     { "status": "Queued", "timestamp": "..." },
        ///     { "status": "Submitted", "timestamp": "..." },
        ///     { "status": "Confirmed", "timestamp": "..." }
        ///   ]
        /// }
        /// ```
        /// </remarks>
        [HttpGet("deploy/{jobId}/status")]
        [ProducesResponseType(typeof(DeploymentStatusPollResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDeploymentStatus([FromRoute] string jobId)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new DeploymentStatusPollResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new DeploymentStatusPollResponse
                {
                    Success = false,
                    ErrorMessage = "JobId is required",
                    CorrelationId = correlationId
                });
            }

            try
            {
                var deployment = await _deploymentStatusService.GetDeploymentAsync(jobId);

                if (deployment == null)
                {
                    return NotFound(new DeploymentStatusPollResponse
                    {
                        Success = false,
                        JobId = jobId,
                        ErrorMessage = "Deployment not found",
                        CorrelationId = correlationId
                    });
                }

                var statusHistory = deployment.StatusHistory
                    .Select(e => new StatusHistoryEntry
                    {
                        Status = e.Status.ToString(),
                        Timestamp = e.Timestamp,
                        Message = e.Message
                    })
                    .ToList();

                return Ok(new DeploymentStatusPollResponse
                {
                    Success = true,
                    JobId = deployment.DeploymentId,
                    Status = deployment.CurrentStatus.ToString(),
                    TokenType = deployment.TokenType,
                    Network = deployment.Network,
                    TokenName = deployment.TokenName,
                    TokenSymbol = deployment.TokenSymbol,
                    AssetIdentifier = deployment.AssetIdentifier,
                    TransactionHash = deployment.TransactionHash,
                    StatusHistory = statusHistory,
                    ErrorMessage = deployment.StatusHistory
                        .LastOrDefault(e => !string.IsNullOrWhiteSpace(e.ErrorMessage))?.ErrorMessage,
                    CreatedAt = deployment.CreatedAt,
                    UpdatedAt = deployment.UpdatedAt,
                    CorrelationId = correlationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting deployment status. JobId={JobId}, CorrelationId={CorrelationId}",
                    jobId, correlationId);

                return StatusCode(StatusCodes.Status500InternalServerError, new DeploymentStatusPollResponse
                {
                    Success = false,
                    JobId = jobId,
                    ErrorMessage = "An error occurred while retrieving deployment status",
                    CorrelationId = correlationId
                });
            }
        }

        /// <summary>
        /// Returns all token deployments for the authenticated user
        /// </summary>
        /// <returns>List of user's token deployments</returns>
        /// <remarks>
        /// Returns the complete deployment history for the authenticated user across all
        /// chains and token standards.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "deployments": [
        ///     {
        ///       "jobId": "...",
        ///       "status": "Completed",
        ///       "tokenType": "ASA",
        ///       "network": "algorand-mainnet",
        ///       "tokenName": "My Token",
        ///       "assetIdentifier": "12345678"
        ///     }
        ///   ],
        ///   "totalCount": 1
        /// }
        /// ```
        /// </remarks>
        [HttpGet("deployments")]
        [ProducesResponseType(typeof(UserDeploymentsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDeployments()
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new UserDeploymentsResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            try
            {
                var result = await _deploymentStatusService.GetDeploymentsAsync(new ListDeploymentsRequest
                {
                    DeployedBy = userId,
                    Page = 1,
                    PageSize = 100
                });

                var summaries = result.Deployments.Select(d => new DeploymentSummary
                {
                    JobId = d.DeploymentId,
                    Status = d.CurrentStatus.ToString(),
                    TokenType = d.TokenType,
                    Network = d.Network,
                    TokenName = d.TokenName,
                    TokenSymbol = d.TokenSymbol,
                    AssetIdentifier = d.AssetIdentifier,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                }).ToList();

                return Ok(new UserDeploymentsResponse
                {
                    Success = true,
                    Deployments = summaries,
                    TotalCount = result.TotalCount,
                    CorrelationId = correlationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving deployments. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);

                return StatusCode(StatusCodes.Status500InternalServerError, new UserDeploymentsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving deployments",
                    CorrelationId = correlationId
                });
            }
        }

        /// <summary>
        /// Extracts a string value from the deployment params dictionary
        /// </summary>
        private static string? GetStringParam(Dictionary<string, object> @params, string key)
        {
            if (@params.TryGetValue(key, out var value))
            {
                return value?.ToString();
            }
            return null;
        }
    }
}

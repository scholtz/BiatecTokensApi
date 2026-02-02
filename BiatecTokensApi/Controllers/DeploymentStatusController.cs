using BiatecTokensApi.Filters;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for querying and tracking token deployment status
    /// </summary>
    /// <remarks>
    /// This controller enables real-time monitoring of token deployments with complete
    /// status history and audit trail. Supports filtering and pagination for deployment queries.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/token/deployments")]
    public class DeploymentStatusController : ControllerBase
    {
        private readonly IDeploymentStatusService _deploymentStatusService;
        private readonly ILogger<DeploymentStatusController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentStatusController"/> class.
        /// </summary>
        /// <param name="deploymentStatusService">The deployment status service</param>
        /// <param name="logger">The logger instance</param>
        public DeploymentStatusController(
            IDeploymentStatusService deploymentStatusService,
            ILogger<DeploymentStatusController> logger)
        {
            _deploymentStatusService = deploymentStatusService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current status and history of a specific deployment
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>Deployment status information including complete history</returns>
        /// <remarks>
        /// Returns comprehensive deployment information including:
        /// - Current deployment status
        /// - Complete status transition history
        /// - Token metadata (name, symbol, type)
        /// - Network and deployer information
        /// - Transaction hash and asset identifier
        /// - Error messages if deployment failed
        /// 
        /// **Use Cases:**
        /// - Real-time deployment progress monitoring
        /// - Debugging deployment failures
        /// - Compliance audit trail verification
        /// - Integration with frontend progress UI
        /// </remarks>
        [HttpGet("{deploymentId}")]
        [ProducesResponseType(typeof(DeploymentStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDeploymentStatus([FromRoute] string deploymentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deploymentId))
                {
                    return BadRequest(new DeploymentStatusResponse
                    {
                        Success = false,
                        ErrorMessage = "DeploymentId is required"
                    });
                }

                var deployment = await _deploymentStatusService.GetDeploymentAsync(deploymentId);

                if (deployment == null)
                {
                    _logger.LogWarning("Deployment not found: DeploymentId={DeploymentId}", LoggingHelper.SanitizeLogInput(deploymentId));
                    return NotFound(new DeploymentStatusResponse
                    {
                        Success = false,
                        ErrorMessage = $"Deployment with ID '{LoggingHelper.SanitizeLogInput(deploymentId)}' not found"
                    });
                }

                _logger.LogInformation("Retrieved deployment status: DeploymentId={DeploymentId}, Status={Status}",
                    LoggingHelper.SanitizeLogInput(deploymentId), deployment.CurrentStatus);

                return Ok(new DeploymentStatusResponse
                {
                    Success = true,
                    Deployment = deployment
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deployment status: DeploymentId={DeploymentId}", LoggingHelper.SanitizeLogInput(deploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new DeploymentStatusResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving deployment status"
                });
            }
        }

        /// <summary>
        /// Lists deployments with filtering and pagination
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>Paginated list of deployments</returns>
        /// <remarks>
        /// Returns a paginated list of deployments with optional filtering by:
        /// - Deployer address
        /// - Network
        /// - Token type
        /// - Current status
        /// - Date range
        /// 
        /// **Use Cases:**
        /// - Dashboard deployment history
        /// - Monitoring active deployments
        /// - Compliance reporting
        /// - User-specific deployment tracking
        /// 
        /// **Pagination:**
        /// - Default page size: 50
        /// - Maximum page size: 100
        /// - Pages are 1-indexed
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ListDeploymentsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListDeployments([FromQuery] ListDeploymentsRequest request)
        {
            try
            {
                // Validate pagination
                if (request.Page < 1)
                {
                    return BadRequest(new ListDeploymentsResponse
                    {
                        Success = false,
                        ErrorMessage = "Page number must be at least 1"
                    });
                }

                if (request.PageSize < 1 || request.PageSize > 100)
                {
                    return BadRequest(new ListDeploymentsResponse
                    {
                        Success = false,
                        ErrorMessage = "Page size must be between 1 and 100"
                    });
                }

                var response = await _deploymentStatusService.GetDeploymentsAsync(request);

                _logger.LogInformation("Retrieved deployments: Count={Count}, Page={Page}, TotalCount={TotalCount}",
                    response.Deployments.Count, response.Page, response.TotalCount);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deployments");
                return StatusCode(StatusCodes.Status500InternalServerError, new ListDeploymentsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving deployments"
                });
            }
        }

        /// <summary>
        /// Gets the complete status history for a deployment
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>Chronologically ordered list of status transitions</returns>
        /// <remarks>
        /// Returns an append-only audit trail of all status transitions for a deployment.
        /// Each entry includes timestamp, status, message, and relevant metadata.
        /// 
        /// **Use Cases:**
        /// - Compliance audit trail
        /// - Deployment timeline visualization
        /// - Debugging deployment issues
        /// - Performance analysis (time between status transitions)
        /// </remarks>
        [HttpGet("{deploymentId}/history")]
        [ProducesResponseType(typeof(List<DeploymentStatusEntry>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDeploymentHistory([FromRoute] string deploymentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deploymentId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        errorMessage = "DeploymentId is required"
                    });
                }

                var deployment = await _deploymentStatusService.GetDeploymentAsync(deploymentId);
                if (deployment == null)
                {
                    _logger.LogWarning("Deployment not found for history: DeploymentId={DeploymentId}", LoggingHelper.SanitizeLogInput(deploymentId));
                    return NotFound(new
                    {
                        success = false,
                        errorMessage = $"Deployment with ID '{LoggingHelper.SanitizeLogInput(deploymentId)}' not found"
                    });
                }

                var history = await _deploymentStatusService.GetStatusHistoryAsync(deploymentId);

                _logger.LogInformation("Retrieved deployment history: DeploymentId={DeploymentId}, EntryCount={Count}",
                    LoggingHelper.SanitizeLogInput(deploymentId), history.Count);

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deployment history: DeploymentId={DeploymentId}", LoggingHelper.SanitizeLogInput(deploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = "An error occurred while retrieving deployment history"
                });
            }
        }
    }
}

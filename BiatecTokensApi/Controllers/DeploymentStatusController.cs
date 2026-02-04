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
        private readonly IDeploymentAuditService _auditService;
        private readonly ILogger<DeploymentStatusController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentStatusController"/> class.
        /// </summary>
        /// <param name="deploymentStatusService">The deployment status service</param>
        /// <param name="auditService">The deployment audit service</param>
        /// <param name="logger">The logger instance</param>
        public DeploymentStatusController(
            IDeploymentStatusService deploymentStatusService,
            IDeploymentAuditService auditService,
            ILogger<DeploymentStatusController> logger)
        {
            _deploymentStatusService = deploymentStatusService;
            _auditService = auditService;
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

        /// <summary>
        /// Cancels a pending deployment
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="request">Cancellation request with reason</param>
        /// <returns>Cancellation result</returns>
        /// <remarks>
        /// Cancels a deployment that is in Queued status. Once a deployment has been
        /// submitted to the blockchain, it cannot be cancelled through this endpoint.
        /// 
        /// **Use Cases:**
        /// - User changes mind before transaction submission
        /// - User wants to modify parameters
        /// - User realizes incorrect configuration
        /// 
        /// **Restrictions:**
        /// - Only deployments in Queued status can be cancelled
        /// - Submitted transactions cannot be cancelled (blockchain immutability)
        /// </remarks>
        [HttpPost("{deploymentId}/cancel")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelDeployment(
            [FromRoute] string deploymentId,
            [FromBody] CancelDeploymentRequest request)
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

                if (string.IsNullOrWhiteSpace(request?.Reason))
                {
                    return BadRequest(new
                    {
                        success = false,
                        errorMessage = "Cancellation reason is required"
                    });
                }

                var result = await _deploymentStatusService.CancelDeploymentAsync(deploymentId, request.Reason);

                if (!result)
                {
                    return BadRequest(new
                    {
                        success = false,
                        errorMessage = "Deployment cannot be cancelled. It may not exist or may have already been submitted."
                    });
                }

                _logger.LogInformation("Deployment cancelled: DeploymentId={DeploymentId}, Reason={Reason}",
                    LoggingHelper.SanitizeLogInput(deploymentId), LoggingHelper.SanitizeLogInput(request.Reason));

                return Ok(new
                {
                    success = true,
                    message = "Deployment cancelled successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling deployment: DeploymentId={DeploymentId}", LoggingHelper.SanitizeLogInput(deploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = "An error occurred while cancelling the deployment"
                });
            }
        }

        /// <summary>
        /// Exports audit trail for a specific deployment
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <param name="format">Export format (json or csv)</param>
        /// <returns>Audit trail in requested format</returns>
        /// <remarks>
        /// Exports complete audit trail for a deployment including:
        /// - All status transitions with timestamps
        /// - Compliance checks performed
        /// - Error details if applicable
        /// - Transaction information
        /// - Duration metrics
        /// 
        /// **Use Cases:**
        /// - Regulatory compliance reporting
        /// - Incident investigation
        /// - Performance analysis
        /// - Customer support documentation
        /// 
        /// **Formats:**
        /// - JSON: Full structured data with nested objects
        /// - CSV: Flattened data suitable for spreadsheet analysis
        /// </remarks>
        [HttpGet("{deploymentId}/audit-trail")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/csv")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportAuditTrail(
            [FromRoute] string deploymentId,
            [FromQuery] string format = "json")
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

                // Validate format
                if (format.ToLower() != "json" && format.ToLower() != "csv")
                {
                    return BadRequest(new
                    {
                        success = false,
                        errorMessage = "Format must be 'json' or 'csv'"
                    });
                }

                string data;
                string contentType;
                string fileName;

                if (format.ToLower() == "json")
                {
                    data = await _auditService.ExportAuditTrailAsJsonAsync(deploymentId);
                    contentType = "application/json";
                    fileName = $"audit-trail-{deploymentId}.json";
                }
                else
                {
                    data = await _auditService.ExportAuditTrailAsCsvAsync(deploymentId);
                    contentType = "text/csv";
                    fileName = $"audit-trail-{deploymentId}.csv";
                }

                _logger.LogInformation("Exported audit trail: DeploymentId={DeploymentId}, Format={Format}, Size={Size}",
                    LoggingHelper.SanitizeLogInput(deploymentId), format, data.Length);

                return File(System.Text.Encoding.UTF8.GetBytes(data), contentType, fileName);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Deployment not found for audit trail: DeploymentId={DeploymentId}, Error={Error}",
                    LoggingHelper.SanitizeLogInput(deploymentId), ex.Message);
                return NotFound(new
                {
                    success = false,
                    errorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit trail: DeploymentId={DeploymentId}",
                    LoggingHelper.SanitizeLogInput(deploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = "An error occurred while exporting the audit trail"
                });
            }
        }

        /// <summary>
        /// Exports audit trails for multiple deployments
        /// </summary>
        /// <param name="request">Export request with filters</param>
        /// <returns>Audit trails in requested format</returns>
        /// <remarks>
        /// Exports audit trails for multiple deployments with filtering and pagination.
        /// Supports idempotency through the X-Idempotency-Key header for large exports.
        /// 
        /// **Use Cases:**
        /// - Bulk compliance reporting
        /// - Historical analysis
        /// - Data migration
        /// - Backup and archival
        /// 
        /// **Idempotency:**
        /// - Include X-Idempotency-Key header for large exports
        /// - Results are cached for 1 hour
        /// - Repeated requests with same key return cached results
        /// - Key must be unique per unique request parameters
        /// 
        /// **Pagination:**
        /// - Default: 100 records per page
        /// - Maximum: 1000 records per page
        /// - Use multiple requests for larger datasets
        /// </remarks>
        [HttpPost("audit-trail/export")]
        [ProducesResponseType(typeof(AuditExportResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportAuditTrails([FromBody] AuditExportRequest request)
        {
            try
            {
                // Get idempotency key from header
                var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();

                var result = await _auditService.ExportAuditTrailsAsync(request, idempotencyKey);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                _logger.LogInformation("Exported audit trails: Count={Count}, Format={Format}, Cached={Cached}",
                    result.RecordCount, request.Format, result.IsCached);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit trails");
                return StatusCode(StatusCodes.Status500InternalServerError, new AuditExportResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred while exporting audit trails"
                });
            }
        }

        /// <summary>
        /// Gets deployment metrics for monitoring and analytics
        /// </summary>
        /// <param name="request">Metrics request with filters</param>
        /// <returns>Deployment metrics</returns>
        /// <remarks>
        /// Returns comprehensive deployment metrics including:
        /// - Success/failure rates
        /// - Duration statistics (average, median, P95)
        /// - Failure breakdown by category
        /// - Deployment counts by network and token type
        /// - Average duration by status transition
        /// - Retry statistics
        /// 
        /// **Use Cases:**
        /// - Monitoring dashboard creation
        /// - SLA tracking and reporting
        /// - Performance optimization
        /// - Capacity planning
        /// - Customer success metrics
        /// 
        /// **Time Period:**
        /// - Default: Last 24 hours
        /// - Custom: Specify fromDate and toDate
        /// - Maximum recommended period: 30 days (for performance)
        /// 
        /// **Filtering:**
        /// - By network (e.g., "voimain-v1.0", "base-mainnet")
        /// - By token type (e.g., "ERC20_Mintable", "ARC200_Mintable")
        /// - By deployer address
        /// </remarks>
        [HttpGet("metrics")]
        [ProducesResponseType(typeof(DeploymentMetricsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDeploymentMetrics([FromQuery] GetDeploymentMetricsRequest request)
        {
            try
            {
                var metrics = await _deploymentStatusService.GetDeploymentMetricsAsync(request);

                _logger.LogInformation("Calculated deployment metrics: Period={FromDate} to {ToDate}, Total={Total}, Success={Success}, Failed={Failed}",
                    metrics.PeriodStart, metrics.PeriodEnd, metrics.TotalDeployments, 
                    metrics.SuccessfulDeployments, metrics.FailedDeployments);

                return Ok(new DeploymentMetricsResponse
                {
                    Success = true,
                    Metrics = metrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating deployment metrics");
                return StatusCode(StatusCodes.Status500InternalServerError, new DeploymentMetricsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while calculating deployment metrics"
                });
            }
        }
    }
}

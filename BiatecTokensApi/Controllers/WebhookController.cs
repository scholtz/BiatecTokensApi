using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing compliance webhook subscriptions
    /// </summary>
    /// <remarks>
    /// This controller manages webhook operations including subscription management,
    /// event delivery tracking, and audit history. All endpoints require ARC-0014 authentication.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/webhooks")]
    public class WebhookController : ControllerBase
    {
        private readonly IWebhookService _webhookService;
        private readonly ILogger<WebhookController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookController"/> class.
        /// </summary>
        /// <param name="webhookService">The webhook service</param>
        /// <param name="logger">The logger instance</param>
        public WebhookController(
            IWebhookService webhookService,
            ILogger<WebhookController> logger)
        {
            _webhookService = webhookService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new webhook subscription
        /// </summary>
        /// <param name="request">The webhook subscription creation request</param>
        /// <returns>The created webhook subscription with signing secret</returns>
        /// <remarks>
        /// Creates a webhook subscription for compliance events. The response includes a signing secret
        /// that should be used to verify webhook signatures. Supported event types:
        /// - WhitelistAdd: Triggered when an address is added to the whitelist
        /// - WhitelistRemove: Triggered when an address is removed from the whitelist
        /// - TransferDeny: Triggered when a transfer is denied by whitelist rules
        /// - AuditExportCreated: Triggered when an audit export is created
        /// - KycStatusChange: Triggered when KYC verification status changes
        /// - AmlStatusChange: Triggered when AML verification status changes
        /// - ComplianceBadgeUpdate: Triggered when compliance status or badge is updated
        /// 
        /// All webhook payloads include:
        /// - Event ID and type
        /// - Timestamp (UTC)
        /// - Actor (user who triggered the event)
        /// - Asset ID and network
        /// - Event-specific data
        /// 
        /// Webhook delivery includes exponential backoff retry on failure (1min, 5min, 15min).
        /// </remarks>
        [HttpPost("subscriptions")]
        [ProducesResponseType(typeof(WebhookSubscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateSubscription([FromBody] CreateWebhookSubscriptionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _webhookService.CreateSubscriptionAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Created webhook subscription for URL {Url} by {UserAddress}", 
                        request.Url, userAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to create webhook subscription for URL {Url}: {Error}", 
                        request.Url, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating webhook subscription for URL {Url}", request.Url);
                return StatusCode(StatusCodes.Status500InternalServerError, new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets a webhook subscription by ID
        /// </summary>
        /// <param name="subscriptionId">The webhook subscription ID</param>
        /// <returns>The webhook subscription details</returns>
        [HttpGet("subscriptions/{subscriptionId}")]
        [ProducesResponseType(typeof(WebhookSubscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSubscription([FromRoute] string subscriptionId)
        {
            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    return Unauthorized(new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _webhookService.GetSubscriptionAsync(subscriptionId, userAddress);

                if (result.Success)
                {
                    return Ok(result);
                }
                else if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(result);
                }
                else if (result.ErrorMessage?.Contains("permission") == true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting webhook subscription {SubscriptionId}", subscriptionId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists all webhook subscriptions for the authenticated user
        /// </summary>
        /// <returns>List of webhook subscriptions</returns>
        [HttpGet("subscriptions")]
        [ProducesResponseType(typeof(WebhookSubscriptionListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListSubscriptions()
        {
            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    return Unauthorized(new WebhookSubscriptionListResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _webhookService.ListSubscriptionsAsync(userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} webhook subscriptions for user {UserAddress}", 
                        result.Subscriptions.Count, userAddress);
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing webhook subscriptions");
                return StatusCode(StatusCodes.Status500InternalServerError, new WebhookSubscriptionListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Updates an existing webhook subscription
        /// </summary>
        /// <param name="request">The webhook subscription update request</param>
        /// <returns>The updated webhook subscription</returns>
        [HttpPut("subscriptions")]
        [ProducesResponseType(typeof(WebhookSubscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateSubscription([FromBody] UpdateWebhookSubscriptionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    return Unauthorized(new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _webhookService.UpdateSubscriptionAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Updated webhook subscription {SubscriptionId} by {UserAddress}", 
                        request.SubscriptionId, userAddress);
                    return Ok(result);
                }
                else if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(result);
                }
                else if (result.ErrorMessage?.Contains("permission") == true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception updating webhook subscription {SubscriptionId}", request.SubscriptionId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Deletes a webhook subscription
        /// </summary>
        /// <param name="subscriptionId">The webhook subscription ID to delete</param>
        /// <returns>Result of the deletion operation</returns>
        [HttpDelete("subscriptions/{subscriptionId}")]
        [ProducesResponseType(typeof(WebhookSubscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteSubscription([FromRoute] string subscriptionId)
        {
            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    return Unauthorized(new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _webhookService.DeleteSubscriptionAsync(subscriptionId, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Deleted webhook subscription {SubscriptionId} by {UserAddress}", 
                        subscriptionId, userAddress);
                    return Ok(result);
                }
                else if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(result);
                }
                else if (result.ErrorMessage?.Contains("permission") == true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting webhook subscription {SubscriptionId}", subscriptionId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets webhook delivery history with filtering
        /// </summary>
        /// <param name="subscriptionId">Optional filter by subscription ID</param>
        /// <param name="eventId">Optional filter by event ID</param>
        /// <param name="success">Optional filter by success status</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Webhook delivery history with statistics</returns>
        /// <remarks>
        /// This endpoint provides comprehensive webhook delivery tracking including:
        /// - Delivery attempts and outcomes
        /// - HTTP status codes and error messages
        /// - Retry status and next retry times
        /// - Success/failure statistics
        /// 
        /// This serves as the admin audit endpoint for webhook delivery failures.
        /// </remarks>
        [HttpGet("deliveries")]
        [ProducesResponseType(typeof(WebhookDeliveryHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDeliveryHistory(
            [FromQuery] string? subscriptionId = null,
            [FromQuery] string? eventId = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    return Unauthorized(new WebhookDeliveryHistoryResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var request = new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscriptionId,
                    EventId = eventId,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100)
                };

                var result = await _webhookService.GetDeliveryHistoryAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved webhook delivery history for user {UserAddress}: {Count} deliveries", 
                        userAddress, result.Deliveries.Count);
                    return Ok(result);
                }
                else if (result.ErrorMessage?.Contains("permission") == true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting webhook delivery history");
                return StatusCode(StatusCodes.Status500InternalServerError, new WebhookDeliveryHistoryResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        private string? GetUserAddress()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}

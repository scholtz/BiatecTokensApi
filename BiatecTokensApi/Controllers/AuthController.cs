using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for authentication diagnostics and ARC-0014 authentication flow support
    /// </summary>
    /// <remarks>
    /// This controller provides tools to debug authentication issues and understand the ARC-0014
    /// authentication flow. All endpoints require valid ARC-0014 authentication.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Verifies ARC-0014 authentication and returns user identity information
        /// </summary>
        /// <returns>Authentication status and user claims</returns>
        /// <remarks>
        /// This endpoint helps verify that ARC-0014 authentication is working correctly
        /// and provides visibility into the authenticated user's identity.
        /// 
        /// **Sample Request:**
        /// ```
        /// GET /api/v1/auth/verify
        /// Authorization: SigTx [base64-encoded-signed-transaction]
        /// ```
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "authenticated": true,
        ///   "userAddress": "ALGORAND_ADDRESS_HERE",
        ///   "authenticationMethod": "ARC-0014",
        ///   "claims": {
        ///     "sub": "ALGORAND_ADDRESS_HERE",
        ///     "nameidentifier": "ALGORAND_ADDRESS_HERE"
        ///   },
        ///   "correlationId": "abc123-def456"
        /// }
        /// ```
        /// 
        /// **Use Cases:**
        /// - Verify authentication is working
        /// - Debug authentication issues
        /// - Confirm user identity before operations
        /// - Integration testing authentication flow
        /// </remarks>
        [HttpGet("verify")]
        [ProducesResponseType(typeof(AuthVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult VerifyAuthentication()
        {
            var correlationId = HttpContext.TraceIdentifier;
            
            try
            {
                // Extract user address from claims
                var userAddress = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst("sub")?.Value 
                    ?? "UNKNOWN";

                // Get all claims for diagnostics
                var claims = User.Claims.ToDictionary(
                    c => c.Type.Split('/').Last(), // Use just the claim name, not full URI
                    c => c.Value
                );

                _logger.LogInformation(
                    "Authentication verified successfully. UserAddress={UserAddress}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userAddress),
                    correlationId
                );

                return Ok(new AuthVerificationResponse
                {
                    Success = true,
                    Authenticated = true,
                    UserAddress = userAddress,
                    AuthenticationMethod = "ARC-0014",
                    Claims = claims,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error verifying authentication. CorrelationId={CorrelationId}",
                    correlationId
                );

                return StatusCode(StatusCodes.Status500InternalServerError, new AuthVerificationResponse
                {
                    Success = false,
                    Authenticated = false,
                    ErrorMessage = "An error occurred while verifying authentication",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Returns information about the ARC-0014 authentication flow and requirements
        /// </summary>
        /// <returns>Authentication flow documentation and configuration</returns>
        /// <remarks>
        /// This endpoint provides detailed information about how to authenticate with the API
        /// using ARC-0014. It does NOT require authentication and can be used by clients
        /// to understand the authentication requirements.
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "authenticationMethod": "ARC-0014",
        ///   "realm": "BiatecTokens#ARC14",
        ///   "description": "Algorand ARC-0014 transaction-based authentication",
        ///   "headerFormat": "Authorization: SigTx [base64-encoded-signed-transaction]",
        ///   "documentation": "https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md",
        ///   "supportedNetworks": ["mainnet", "testnet"]
        /// }
        /// ```
        /// </remarks>
        [AllowAnonymous]
        [HttpGet("info")]
        [ProducesResponseType(typeof(AuthInfoResponse), StatusCodes.Status200OK)]
        public IActionResult GetAuthInfo()
        {
            var correlationId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "Authentication info requested. CorrelationId={CorrelationId}",
                correlationId
            );

            return Ok(new AuthInfoResponse
            {
                AuthenticationMethod = "ARC-0014",
                Realm = "BiatecTokens#ARC14",
                Description = "Algorand ARC-0014 transaction-based authentication. Users sign an Algorand transaction to prove ownership of their address.",
                HeaderFormat = "Authorization: SigTx [base64-encoded-signed-transaction]",
                Documentation = "https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md",
                SupportedNetworks = new List<string> { "algorand-mainnet", "algorand-testnet", "algorand-betanet", "voi-mainnet", "aramid-mainnet" },
                Requirements = new AuthRequirements
                {
                    TransactionFormat = "Signed Algorand transaction in base64 format",
                    ExpirationCheck = true,
                    NetworkValidation = true,
                    MinimumValidityRounds = 10
                },
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Response model for authentication verification
    /// </summary>
    public class AuthVerificationResponse
    {
        /// <summary>
        /// Indicates if the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Indicates if the user is authenticated
        /// </summary>
        public bool Authenticated { get; set; }

        /// <summary>
        /// The authenticated user's Algorand address
        /// </summary>
        public string? UserAddress { get; set; }

        /// <summary>
        /// The authentication method used (always "ARC-0014" for this API)
        /// </summary>
        public string? AuthenticationMethod { get; set; }

        /// <summary>
        /// User claims extracted from the authentication token
        /// </summary>
        public Dictionary<string, string>? Claims { get; set; }

        /// <summary>
        /// Error message if verification failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Timestamp when the verification was performed
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Response model for authentication information
    /// </summary>
    public class AuthInfoResponse
    {
        /// <summary>
        /// The authentication method supported by the API
        /// </summary>
        public string AuthenticationMethod { get; set; } = string.Empty;

        /// <summary>
        /// The ARC-0014 realm identifier
        /// </summary>
        public string Realm { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the authentication method
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Format of the Authorization header
        /// </summary>
        public string HeaderFormat { get; set; } = string.Empty;

        /// <summary>
        /// URL to ARC-0014 specification
        /// </summary>
        public string Documentation { get; set; } = string.Empty;

        /// <summary>
        /// List of supported Algorand networks
        /// </summary>
        public List<string> SupportedNetworks { get; set; } = new();

        /// <summary>
        /// Authentication requirements details
        /// </summary>
        public AuthRequirements? Requirements { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Timestamp when the information was retrieved
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Authentication requirements details
    /// </summary>
    public class AuthRequirements
    {
        /// <summary>
        /// Required format for authentication transactions
        /// </summary>
        public string TransactionFormat { get; set; } = string.Empty;

        /// <summary>
        /// Whether transaction expiration is checked
        /// </summary>
        public bool ExpirationCheck { get; set; }

        /// <summary>
        /// Whether network validation is performed
        /// </summary>
        public bool NetworkValidation { get; set; }

        /// <summary>
        /// Minimum validity period in rounds
        /// </summary>
        public int MinimumValidityRounds { get; set; }
    }
}

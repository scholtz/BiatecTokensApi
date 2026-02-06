using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Authentication controller for email/password based authentication with ARC76 account derivation
    /// </summary>
    /// <remarks>
    /// This controller provides email/password authentication endpoints that derive ARC76 accounts
    /// automatically. No wallet connection is required. The backend handles all blockchain signing
    /// operations on behalf of the user.
    /// </remarks>
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthV2Controller : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthV2Controller> _logger;

        public AuthV2Controller(
            IAuthenticationService authService,
            ILogger<AuthV2Controller> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new user with email and password
        /// </summary>
        /// <param name="request">Registration request containing email, password, and optional full name</param>
        /// <returns>Registration response with user details and authentication tokens</returns>
        /// <remarks>
        /// Creates a new user account with email/password credentials. Automatically derives an
        /// ARC76 Algorand account for the user. No wallet connection required.
        /// 
        /// **Password Requirements:**
        /// - Minimum 8 characters
        /// - Must contain at least one uppercase letter
        /// - Must contain at least one lowercase letter
        /// - Must contain at least one number
        /// - Must contain at least one special character
        /// 
        /// **Sample Request:**
        /// ```
        /// POST /api/v1/auth/register
        /// {
        ///   "email": "user@example.com",
        ///   "password": "SecurePass123!",
        ///   "confirmPassword": "SecurePass123!",
        ///   "fullName": "John Doe"
        /// }
        /// ```
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "userId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "email": "user@example.com",
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        ///   "refreshToken": "refresh_token_value",
        ///   "expiresAt": "2026-02-06T13:18:44.986Z"
        /// }
        /// ```
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid registration request. CorrelationId={CorrelationId}", correlationId);
                return BadRequest(ModelState);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var response = await _authService.RegisterAsync(request, ipAddress, userAgent);
            response.CorrelationId = correlationId;

            if (!response.Success)
            {
                _logger.LogWarning("Registration failed: {ErrorCode} - {ErrorMessage}. Email={Email}, CorrelationId={CorrelationId}",
                    response.ErrorCode, response.ErrorMessage, LoggingHelper.SanitizeLogInput(request.Email), correlationId);
                return BadRequest(response);
            }

            _logger.LogInformation("User registered successfully. Email={Email}, UserId={UserId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.Email), response.UserId, correlationId);

            return Ok(response);
        }

        /// <summary>
        /// Authenticates a user with email and password
        /// </summary>
        /// <param name="request">Login request containing email and password</param>
        /// <returns>Login response with user details and authentication tokens</returns>
        /// <remarks>
        /// Authenticates a user and returns JWT access token and refresh token. The backend
        /// manages the user's ARC76-derived Algorand account for all blockchain operations.
        /// 
        /// **Account Locking:**
        /// After 5 failed login attempts, the account will be locked for 30 minutes.
        /// 
        /// **Sample Request:**
        /// ```
        /// POST /api/v1/auth/login
        /// {
        ///   "email": "user@example.com",
        ///   "password": "SecurePass123!"
        /// }
        /// ```
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "userId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "email": "user@example.com",
        ///   "fullName": "John Doe",
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        ///   "refreshToken": "refresh_token_value",
        ///   "expiresAt": "2026-02-06T13:18:44.986Z"
        /// }
        /// ```
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid login request. CorrelationId={CorrelationId}", correlationId);
                return BadRequest(ModelState);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var response = await _authService.LoginAsync(request, ipAddress, userAgent);
            response.CorrelationId = correlationId;

            if (!response.Success)
            {
                _logger.LogWarning("Login failed: {ErrorCode} - {ErrorMessage}. Email={Email}, CorrelationId={CorrelationId}",
                    response.ErrorCode, response.ErrorMessage, LoggingHelper.SanitizeLogInput(request.Email), correlationId);
                
                return response.ErrorCode switch
                {
                    "INVALID_CREDENTIALS" => Unauthorized(response),
                    "ACCOUNT_LOCKED" => StatusCode(StatusCodes.Status423Locked, response),
                    "ACCOUNT_INACTIVE" => StatusCode(StatusCodes.Status403Forbidden, response),
                    _ => BadRequest(response)
                };
            }

            _logger.LogInformation("User logged in successfully. Email={Email}, UserId={UserId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.Email), response.UserId, correlationId);

            return Ok(response);
        }

        /// <summary>
        /// Refreshes an access token using a refresh token
        /// </summary>
        /// <param name="request">Refresh token request</param>
        /// <returns>New access token and refresh token</returns>
        /// <remarks>
        /// Exchanges a valid refresh token for a new access token and refresh token.
        /// The old refresh token is automatically revoked.
        /// 
        /// **Sample Request:**
        /// ```
        /// POST /api/v1/auth/refresh
        /// {
        ///   "refreshToken": "refresh_token_value"
        /// }
        /// ```
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        ///   "refreshToken": "new_refresh_token_value",
        ///   "expiresAt": "2026-02-06T13:18:44.986Z"
        /// }
        /// ```
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid refresh token request. CorrelationId={CorrelationId}", correlationId);
                return BadRequest(ModelState);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var response = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, userAgent);
            response.CorrelationId = correlationId;

            if (!response.Success)
            {
                _logger.LogWarning("Token refresh failed: {ErrorCode} - {ErrorMessage}. CorrelationId={CorrelationId}",
                    response.ErrorCode, response.ErrorMessage, correlationId);
                return Unauthorized(response);
            }

            _logger.LogInformation("Token refreshed successfully. CorrelationId={CorrelationId}", correlationId);

            return Ok(response);
        }

        /// <summary>
        /// Logs out the current user and revokes all refresh tokens
        /// </summary>
        /// <returns>Logout confirmation</returns>
        /// <remarks>
        /// Logs out the authenticated user and revokes all their refresh tokens.
        /// The access token should be discarded by the client.
        /// 
        /// **Sample Request:**
        /// ```
        /// POST /api/v1/auth/logout
        /// Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
        /// ```
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "message": "Logged out successfully"
        /// }
        /// ```
        /// </remarks>
        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpPost("logout")]
        [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var correlationId = HttpContext.TraceIdentifier;

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Logout attempt without valid user ID. CorrelationId={CorrelationId}", correlationId);
                return Unauthorized(new LogoutResponse
                {
                    Success = false,
                    Message = "Invalid user session",
                    CorrelationId = correlationId
                });
            }

            var response = await _authService.LogoutAsync(userId);
            response.CorrelationId = correlationId;

            _logger.LogInformation("User logged out. UserId={UserId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId), correlationId);

            return Ok(response);
        }

        /// <summary>
        /// Gets the current authenticated user's profile
        /// </summary>
        /// <returns>User profile information</returns>
        /// <remarks>
        /// Returns the current user's profile including their ARC76-derived Algorand address.
        /// This endpoint is useful for the frontend to display user information and verify authentication.
        /// 
        /// **Sample Request:**
        /// ```
        /// GET /api/v1/auth/profile
        /// Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
        /// ```
        /// 
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "userId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "email": "user@example.com",
        ///   "fullName": "John Doe",
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "createdAt": "2026-02-01T10:00:00Z",
        ///   "lastLoginAt": "2026-02-06T12:18:44.986Z"
        /// }
        /// ```
        /// </remarks>
        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpGet("profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetProfile()
        {
            var correlationId = HttpContext.TraceIdentifier;

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var fullName = User.FindFirst(ClaimTypes.Name)?.Value;
            var algorandAddress = User.FindFirst("algorand_address")?.Value;

            _logger.LogInformation("Profile requested. UserId={UserId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId ?? "unknown"), correlationId);

            return Ok(new
            {
                userId,
                email,
                fullName,
                algorandAddress,
                correlationId
            });
        }
    }
}

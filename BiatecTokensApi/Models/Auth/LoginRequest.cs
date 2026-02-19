using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Request model for user login
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// User's email address
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for successful login
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// Indicates if login was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// User ID
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// User's email address
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// User's full name
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// ARC76-derived Algorand address
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// JWT access token
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// Refresh token for obtaining new access tokens
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Token expiration timestamp
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Error message if login failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code for client handling
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Timestamp of the response
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Version of the ARC76 derivation contract used for this response.
        /// Clients can use this to detect contract changes and adapt accordingly.
        /// </summary>
        public string DerivationContractVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for token refresh
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// The refresh token
        /// </summary>
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for token refresh
    /// </summary>
    public class RefreshTokenResponse
    {
        /// <summary>
        /// Indicates if refresh was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// New JWT access token
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// New refresh token
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Token expiration timestamp
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Error message if refresh failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code for client handling
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Timestamp of the response
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response model for logout
    /// </summary>
    public class LogoutResponse
    {
        /// <summary>
        /// Indicates if logout was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Timestamp of the response
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

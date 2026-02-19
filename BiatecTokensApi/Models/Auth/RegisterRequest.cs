using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Request model for user registration
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// User's email address
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password (minimum 8 characters, must contain uppercase, lowercase, number, and special character)
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Password confirmation
        /// </summary>
        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
    }

    /// <summary>
    /// Response model for successful registration
    /// </summary>
    public class RegisterResponse
    {
        /// <summary>
        /// Indicates if registration was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// User ID of the newly created account
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Email address of the new user
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// ARC76-derived Algorand address for the user
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// JWT access token for immediate authentication
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
        /// Error message if registration failed
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
}

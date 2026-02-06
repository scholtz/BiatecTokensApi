namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Represents a refresh token for session management
    /// </summary>
    public class RefreshToken
    {
        /// <summary>
        /// Token identifier
        /// </summary>
        public string TokenId { get; set; } = string.Empty;

        /// <summary>
        /// The token value
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// User ID this token belongs to
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// When the token was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the token expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Whether the token has been revoked
        /// </summary>
        public bool IsRevoked { get; set; } = false;

        /// <summary>
        /// When the token was revoked (if applicable)
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// IP address where the token was created
        /// </summary>
        public string? CreatedByIp { get; set; }

        /// <summary>
        /// User agent where the token was created
        /// </summary>
        public string? CreatedByUserAgent { get; set; }
    }
}

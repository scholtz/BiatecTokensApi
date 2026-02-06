namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// JWT authentication configuration
    /// </summary>
    public class JwtConfig
    {
        /// <summary>
        /// Secret key for JWT token signing
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// JWT issuer
        /// </summary>
        public string Issuer { get; set; } = "BiatecTokensApi";

        /// <summary>
        /// JWT audience
        /// </summary>
        public string Audience { get; set; } = "BiatecTokensUsers";

        /// <summary>
        /// Access token expiration in minutes (default: 60 minutes = 1 hour)
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Refresh token expiration in days (default: 30 days)
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 30;

        /// <summary>
        /// Whether to validate token issuer
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Whether to validate token audience
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Whether to validate token lifetime
        /// </summary>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// Whether to validate issuer signing key
        /// </summary>
        public bool ValidateIssuerSigningKey { get; set; } = true;

        /// <summary>
        /// Clock skew in minutes (tolerance for time differences)
        /// </summary>
        public int ClockSkewMinutes { get; set; } = 5;
    }
}

namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Request model for POST /api/v1/auth/arc76/validate.
    /// Accepts email + password and returns the deterministic ARC76-derived Algorand address.
    /// </summary>
    public class ARC76ValidateRequest
    {
        /// <summary>
        /// User email address. Will be canonicalized (lowercase + trim) before derivation.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// User password. Case-sensitive. Never stored or logged.
        /// </summary>
        public string? Password { get; set; }
    }

    /// <summary>
    /// Response model for POST /api/v1/auth/arc76/validate.
    /// Returns the deterministic ARC76-derived Algorand address for the given credentials.
    /// Never returns private key material.
    /// </summary>
    public class ARC76ValidateResponse
    {
        /// <summary>
        /// The deterministic ARC76-derived Algorand address for the given credentials.
        /// Same email + password always produces the same address.
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// The Algorand Ed25519 public key in Base64 encoding.
        /// </summary>
        public string? PublicKeyBase64 { get; set; }

        /// <summary>
        /// Whether the derived address matches the stored account address for this user.
        /// True only if the user registered with credential-based ARC76 derivation.
        /// Null if the user does not exist (address is still returned for derivation proof).
        /// </summary>
        public bool? AddressMatchesStoredAccount { get; set; }

        /// <summary>
        /// Whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code for programmatic error handling.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Correlation ID for request tracing.
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response model for POST /api/v1/auth/arc76/verify-session.
    /// Returns the Algorand address bound to the current authenticated session.
    /// </summary>
    public class ARC76VerifySessionResponse
    {
        /// <summary>
        /// The ARC76-derived Algorand address bound to this session.
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// The authenticated user's ID.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing.
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}

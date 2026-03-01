namespace BiatecTokensApi.Models.ARC76
{
    /// <summary>
    /// Response model for the GET /api/v1/arc76/address endpoint.
    /// Returns the authenticated user's deterministic ARC76-derived Algorand address.
    /// </summary>
    public class ARC76AddressResponse
    {
        /// <summary>
        /// The ARC76-derived Algorand address for the authenticated user.
        /// Always deterministic: the same email/password always produces the same address.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request model for the POST /api/v1/arc76/verify endpoint.
    /// Provides an Algorand address to verify against the authenticated user's derived address.
    /// </summary>
    public class ARC76VerifyRequest
    {
        /// <summary>
        /// The Algorand address to verify against the authenticated user's ARC76-derived address.
        /// </summary>
        public string? Address { get; set; }
    }

    /// <summary>
    /// Response model for the POST /api/v1/arc76/verify endpoint.
    /// Indicates whether the provided address matches the authenticated user's ARC76-derived address.
    /// </summary>
    public class ARC76VerifyResponse
    {
        /// <summary>
        /// True if the provided address matches the authenticated user's ARC76-derived Algorand address.
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>
        /// Indicates if the operation itself was successful (distinct from the verification result)
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed (not set when Verified is false — only on errors)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}

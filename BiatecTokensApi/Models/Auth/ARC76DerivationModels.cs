namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Request model for ARC76 derivation verification.
    /// Allows callers to verify that a given session/user context maps deterministically
    /// to an expected ARC76-derived Algorand identity.
    /// </summary>
    public class ARC76DerivationVerifyRequest
    {
        /// <summary>
        /// The email address whose derivation should be verified.
        /// Must match the authenticated user's own email – cross-user verification is not permitted.
        /// </summary>
        public string? Email { get; set; }
    }

    /// <summary>
    /// Response model for ARC76 derivation verification.
    /// Provides stable, privacy-safe fields that frontend and enterprise consumers can rely on
    /// to confirm deterministic identity mapping.
    /// </summary>
    public class ARC76DerivationVerifyResponse
    {
        /// <summary>Whether the derivation verification succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The ARC76-derived Algorand address for the provided context.</summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// Indicates that the derived address is consistent across repeated calls with the same inputs.
        /// Always true for a successful verification, false when an inconsistency is detected.
        /// </summary>
        public bool IsConsistent { get; set; }

        /// <summary>
        /// Version of the ARC76 derivation contract used to compute this response.
        /// Consumers should monitor this field for breaking-change detection.
        /// </summary>
        public string DerivationContractVersion { get; set; } = string.Empty;

        /// <summary>The deterministic algorithm used for derivation (e.g. "ARC76/BIP39").</summary>
        public string DerivationAlgorithm { get; set; } = string.Empty;

        /// <summary>
        /// Lightweight proof metadata confirming the derivation path is deterministic.
        /// Does NOT contain secrets – only the derivation parameters and a stable hash.
        /// </summary>
        public ARC76DeterminismProof? DeterminismProof { get; set; }

        /// <summary>Error code if verification failed.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error description.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Actionable remediation hint for the caller.</summary>
        public string? RemediationHint { get; set; }

        /// <summary>Correlation ID linking this response to backend audit evidence.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp of this response.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Lightweight proof that the ARC76 derivation is deterministic for a given context.
    /// Contains only non-sensitive derivation metadata.
    /// </summary>
    public class ARC76DeterminismProof
    {
        /// <summary>The canonical (lower-cased, trimmed) email used as input.</summary>
        public string CanonicalEmail { get; set; } = string.Empty;

        /// <summary>Derivation standard applied.</summary>
        public string Standard { get; set; } = "ARC76";

        /// <summary>Key-derivation path or method label.</summary>
        public string DerivationPath { get; set; } = "BIP39/Algorand";

        /// <summary>
        /// Stable fingerprint (first 8 chars of the derived address) confirming the address
        /// is bound to the canonical email without exposing the full address unnecessarily.
        /// </summary>
        public string AddressFingerprint { get; set; } = string.Empty;

        /// <summary>Contract version at which this derivation was computed.</summary>
        public string ContractVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for ARC76 derivation contract information.
    /// Exposes the stable contract metadata that frontend and enterprise consumers rely on.
    /// </summary>
    public class ARC76DerivationInfoResponse
    {
        /// <summary>Current version of the ARC76 derivation contract.</summary>
        public string ContractVersion { get; set; } = string.Empty;

        /// <summary>Derivation standard.</summary>
        public string Standard { get; set; } = "ARC76";

        /// <summary>Human-readable description of the derivation algorithm.</summary>
        public string AlgorithmDescription { get; set; } = string.Empty;

        /// <summary>Bounded list of error codes this contract may return.</summary>
        public IReadOnlyList<string> BoundedErrorCodes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Indicates whether this contract version guarantees backward-compatible responses.
        /// </summary>
        public bool IsBackwardCompatible { get; set; }

        /// <summary>Date from which this contract version has been in production.</summary>
        public string EffectiveFrom { get; set; } = string.Empty;

        /// <summary>Link to the ARC76 specification.</summary>
        public string SpecificationUrl { get; set; } = string.Empty;

        /// <summary>Correlation ID for tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp of this response.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response model for session inspection.
    /// Exposes stable, derivation-linked session fields that frontend can assert on.
    /// </summary>
    public class SessionInspectionResponse
    {
        /// <summary>Whether a valid session is active for the caller.</summary>
        public bool IsActive { get; set; }

        /// <summary>User ID of the authenticated session.</summary>
        public string? UserId { get; set; }

        /// <summary>Canonicalized email address of the session owner.</summary>
        public string? Email { get; set; }

        /// <summary>The ARC76-derived Algorand address bound to this session.</summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>Token type (always "Bearer" for JWT sessions).</summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>UTC timestamp when the current access token was issued.</summary>
        public DateTime? IssuedAt { get; set; }

        /// <summary>UTC timestamp when the current access token expires.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Version of the ARC76 derivation contract that produced <see cref="AlgorandAddress"/>.
        /// </summary>
        public string DerivationContractVersion { get; set; } = string.Empty;

        /// <summary>Correlation ID for tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp of this response.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

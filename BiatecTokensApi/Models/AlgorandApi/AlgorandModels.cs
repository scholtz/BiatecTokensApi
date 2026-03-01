namespace BiatecTokensApi.Models.AlgorandApi
{
    /// <summary>
    /// Response model for the GET /api/algorand/account/info endpoint.
    /// Returns the ARC76-derived Algorand address and ALGO balance for the authenticated user.
    /// </summary>
    public class AlgorandAccountInfoResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The ARC76-derived Algorand address for the authenticated user
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// The derivation standard used (ARC76)
        /// </summary>
        public string DerivationStandard { get; set; } = "ARC76";

        /// <summary>
        /// ALGO balance in microAlgos (0 if balance could not be retrieved)
        /// </summary>
        public long AlgoBalanceMicroAlgos { get; set; }

        /// <summary>
        /// ALGO balance in whole ALGOs (for display)
        /// </summary>
        public decimal AlgoBalance { get; set; }

        /// <summary>
        /// Network used for balance query
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response model for the GET /api/algorand/transaction/{txId}/status endpoint.
    /// Returns the confirmed/pending/failed status for a given Algorand transaction.
    /// </summary>
    public class AlgorandTransactionStatusResponse
    {
        /// <summary>
        /// Indicates if the status lookup was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The transaction ID / hash that was looked up
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Human-readable status: Confirmed, Pending, Failed, or Unknown
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Asset ID if the transaction resulted in a created asset
        /// </summary>
        public string? AssetIdentifier { get; set; }

        /// <summary>
        /// Block round in which the transaction was confirmed (if confirmed)
        /// </summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>
        /// Network the transaction was submitted on
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Token type (e.g. ASA_FT, ARC3_FT, ARC200)
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Error message if the transaction failed or if lookup failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

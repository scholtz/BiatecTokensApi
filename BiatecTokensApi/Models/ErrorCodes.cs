namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Standardized error codes for API operations
    /// </summary>
    public static class ErrorCodes
    {
        // Validation errors (400)
        /// <summary>
        /// Invalid request parameters
        /// </summary>
        public const string INVALID_REQUEST = "INVALID_REQUEST";
        
        /// <summary>
        /// Required field is missing
        /// </summary>
        public const string MISSING_REQUIRED_FIELD = "MISSING_REQUIRED_FIELD";
        
        /// <summary>
        /// Invalid network specified
        /// </summary>
        public const string INVALID_NETWORK = "INVALID_NETWORK";
        
        /// <summary>
        /// Invalid token parameters
        /// </summary>
        public const string INVALID_TOKEN_PARAMETERS = "INVALID_TOKEN_PARAMETERS";

        // Authentication/Authorization errors (401, 403)
        /// <summary>
        /// Authentication required
        /// </summary>
        public const string UNAUTHORIZED = "UNAUTHORIZED";
        
        /// <summary>
        /// Insufficient permissions
        /// </summary>
        public const string FORBIDDEN = "FORBIDDEN";
        
        /// <summary>
        /// Invalid or expired authentication token
        /// </summary>
        public const string INVALID_AUTH_TOKEN = "INVALID_AUTH_TOKEN";

        // Resource errors (404, 409)
        /// <summary>
        /// Requested resource not found
        /// </summary>
        public const string NOT_FOUND = "NOT_FOUND";
        
        /// <summary>
        /// Resource already exists
        /// </summary>
        public const string ALREADY_EXISTS = "ALREADY_EXISTS";
        
        /// <summary>
        /// Resource conflict
        /// </summary>
        public const string CONFLICT = "CONFLICT";

        // External service errors (502, 503, 504)
        /// <summary>
        /// Blockchain network connection failed
        /// </summary>
        public const string BLOCKCHAIN_CONNECTION_ERROR = "BLOCKCHAIN_CONNECTION_ERROR";
        
        /// <summary>
        /// IPFS service unavailable
        /// </summary>
        public const string IPFS_SERVICE_ERROR = "IPFS_SERVICE_ERROR";
        
        /// <summary>
        /// External API call failed
        /// </summary>
        public const string EXTERNAL_SERVICE_ERROR = "EXTERNAL_SERVICE_ERROR";
        
        /// <summary>
        /// Request timeout
        /// </summary>
        public const string TIMEOUT = "TIMEOUT";
        
        /// <summary>
        /// Circuit breaker open - service temporarily unavailable
        /// </summary>
        public const string CIRCUIT_BREAKER_OPEN = "CIRCUIT_BREAKER_OPEN";

        // Blockchain-specific errors (422)
        /// <summary>
        /// Insufficient funds for transaction
        /// </summary>
        public const string INSUFFICIENT_FUNDS = "INSUFFICIENT_FUNDS";
        
        /// <summary>
        /// Transaction failed on blockchain
        /// </summary>
        public const string TRANSACTION_FAILED = "TRANSACTION_FAILED";
        
        /// <summary>
        /// Smart contract execution failed
        /// </summary>
        public const string CONTRACT_EXECUTION_FAILED = "CONTRACT_EXECUTION_FAILED";
        
        /// <summary>
        /// Gas estimation failed
        /// </summary>
        public const string GAS_ESTIMATION_FAILED = "GAS_ESTIMATION_FAILED";
        
        /// <summary>
        /// Transaction rejected by network
        /// </summary>
        public const string TRANSACTION_REJECTED = "TRANSACTION_REJECTED";

        // Server errors (500)
        /// <summary>
        /// Internal server error
        /// </summary>
        public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
        
        /// <summary>
        /// Configuration error
        /// </summary>
        public const string CONFIGURATION_ERROR = "CONFIGURATION_ERROR";
        
        /// <summary>
        /// Unexpected error occurred
        /// </summary>
        public const string UNEXPECTED_ERROR = "UNEXPECTED_ERROR";

        // Rate limiting (429)
        /// <summary>
        /// Rate limit exceeded
        /// </summary>
        public const string RATE_LIMIT_EXCEEDED = "RATE_LIMIT_EXCEEDED";
        
        /// <summary>
        /// Subscription limit reached
        /// </summary>
        public const string SUBSCRIPTION_LIMIT_REACHED = "SUBSCRIPTION_LIMIT_REACHED";
    }
}

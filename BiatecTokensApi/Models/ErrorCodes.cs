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

        // Security and Audit errors
        /// <summary>
        /// Audit export unavailable
        /// </summary>
        public const string AUDIT_EXPORT_UNAVAILABLE = "AUDIT_EXPORT_UNAVAILABLE";
        
        /// <summary>
        /// Invalid export format
        /// </summary>
        public const string INVALID_EXPORT_FORMAT = "INVALID_EXPORT_FORMAT";
        
        /// <summary>
        /// Export quota exceeded
        /// </summary>
        public const string EXPORT_QUOTA_EXCEEDED = "EXPORT_QUOTA_EXCEEDED";
        
        /// <summary>
        /// Recovery not available
        /// </summary>
        public const string RECOVERY_NOT_AVAILABLE = "RECOVERY_NOT_AVAILABLE";
        
        /// <summary>
        /// Recovery cooldown active
        /// </summary>
        public const string RECOVERY_COOLDOWN_ACTIVE = "RECOVERY_COOLDOWN_ACTIVE";

        // Token Standard Validation errors
        /// <summary>
        /// Token metadata validation failed
        /// </summary>
        public const string METADATA_VALIDATION_FAILED = "METADATA_VALIDATION_FAILED";
        
        /// <summary>
        /// Invalid token standard specified
        /// </summary>
        public const string INVALID_TOKEN_STANDARD = "INVALID_TOKEN_STANDARD";
        
        /// <summary>
        /// Required metadata field missing
        /// </summary>
        public const string REQUIRED_METADATA_FIELD_MISSING = "REQUIRED_METADATA_FIELD_MISSING";
        
        /// <summary>
        /// Metadata field type mismatch
        /// </summary>
        public const string METADATA_FIELD_TYPE_MISMATCH = "METADATA_FIELD_TYPE_MISMATCH";
        
        /// <summary>
        /// Metadata field validation failed
        /// </summary>
        public const string METADATA_FIELD_VALIDATION_FAILED = "METADATA_FIELD_VALIDATION_FAILED";
        
        /// <summary>
        /// Token standard not supported
        /// </summary>
        public const string TOKEN_STANDARD_NOT_SUPPORTED = "TOKEN_STANDARD_NOT_SUPPORTED";

        // Additional error codes for token registry
        /// <summary>
        /// Invalid request parameters
        /// </summary>
        public const string InvalidRequest = "INVALID_REQUEST";

        /// <summary>
        /// Validation failed
        /// </summary>
        public const string ValidationFailed = "VALIDATION_FAILED";

        /// <summary>
        /// Internal error occurred
        /// </summary>
        public const string InternalError = "INTERNAL_ERROR";

        /// <summary>
        /// Resource not found
        /// </summary>
        public const string NotFound = "NOT_FOUND";

        // Subscription and billing errors
        /// <summary>
        /// Subscription not found
        /// </summary>
        public const string SUBSCRIPTION_NOT_FOUND = "SUBSCRIPTION_NOT_FOUND";

        /// <summary>
        /// Subscription has expired
        /// </summary>
        public const string SUBSCRIPTION_EXPIRED = "SUBSCRIPTION_EXPIRED";

        /// <summary>
        /// Payment failed
        /// </summary>
        public const string PAYMENT_FAILED = "PAYMENT_FAILED";

        /// <summary>
        /// Payment method required
        /// </summary>
        public const string PAYMENT_METHOD_REQUIRED = "PAYMENT_METHOD_REQUIRED";

        /// <summary>
        /// Subscription is past due
        /// </summary>
        public const string SUBSCRIPTION_PAST_DUE = "SUBSCRIPTION_PAST_DUE";

        /// <summary>
        /// Subscription has an active dispute
        /// </summary>
        public const string SUBSCRIPTION_HAS_DISPUTE = "SUBSCRIPTION_HAS_DISPUTE";

        /// <summary>
        /// Feature not available in current subscription tier
        /// </summary>
        public const string FEATURE_NOT_AVAILABLE = "FEATURE_NOT_AVAILABLE";

        /// <summary>
        /// Subscription upgrade required
        /// </summary>
        public const string UPGRADE_REQUIRED = "UPGRADE_REQUIRED";

        /// <summary>
        /// Cannot purchase free tier
        /// </summary>
        public const string CANNOT_PURCHASE_FREE_TIER = "CANNOT_PURCHASE_FREE_TIER";

        /// <summary>
        /// Stripe service error
        /// </summary>
        public const string STRIPE_SERVICE_ERROR = "STRIPE_SERVICE_ERROR";

        /// <summary>
        /// Webhook signature validation failed
        /// </summary>
        public const string WEBHOOK_SIGNATURE_INVALID = "WEBHOOK_SIGNATURE_INVALID";

        /// <summary>
        /// Price ID not configured
        /// </summary>
        public const string PRICE_NOT_CONFIGURED = "PRICE_NOT_CONFIGURED";

        // Idempotency errors
        /// <summary>
        /// Idempotency key has been reused with different request parameters
        /// </summary>
        public const string IDEMPOTENCY_KEY_MISMATCH = "IDEMPOTENCY_KEY_MISMATCH";

        // Authentication errors
        /// <summary>
        /// Password does not meet strength requirements
        /// </summary>
        public const string WEAK_PASSWORD = "WEAK_PASSWORD";

        /// <summary>
        /// User with this email already exists
        /// </summary>
        public const string USER_ALREADY_EXISTS = "USER_ALREADY_EXISTS";

        /// <summary>
        /// Invalid email or password
        /// </summary>
        public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";

        /// <summary>
        /// Account is locked due to failed login attempts
        /// </summary>
        public const string ACCOUNT_LOCKED = "ACCOUNT_LOCKED";

        /// <summary>
        /// Account is inactive
        /// </summary>
        public const string ACCOUNT_INACTIVE = "ACCOUNT_INACTIVE";

        /// <summary>
        /// Invalid or expired refresh token
        /// </summary>
        public const string INVALID_REFRESH_TOKEN = "INVALID_REFRESH_TOKEN";

        /// <summary>
        /// Refresh token has been revoked
        /// </summary>
        public const string REFRESH_TOKEN_REVOKED = "REFRESH_TOKEN_REVOKED";

        /// <summary>
        /// Refresh token has expired
        /// </summary>
        public const string REFRESH_TOKEN_EXPIRED = "REFRESH_TOKEN_EXPIRED";

        /// <summary>
        /// User not found
        /// </summary>
        public const string USER_NOT_FOUND = "USER_NOT_FOUND";

        // Compliance Profile errors
        /// <summary>
        /// Compliance profile not found
        /// </summary>
        public const string COMPLIANCE_PROFILE_NOT_FOUND = "COMPLIANCE_PROFILE_NOT_FOUND";

        /// <summary>
        /// Invalid jurisdiction code
        /// </summary>
        public const string INVALID_JURISDICTION = "INVALID_JURISDICTION";

        /// <summary>
        /// Invalid issuance intent
        /// </summary>
        public const string INVALID_ISSUANCE_INTENT = "INVALID_ISSUANCE_INTENT";

        /// <summary>
        /// Compliance profile validation failed
        /// </summary>
        public const string COMPLIANCE_PROFILE_VALIDATION_FAILED = "COMPLIANCE_PROFILE_VALIDATION_FAILED";

        // KYC errors
        /// <summary>
        /// KYC verification not started
        /// </summary>
        public const string KYC_NOT_STARTED = "KYC_NOT_STARTED";

        /// <summary>
        /// KYC verification is pending
        /// </summary>
        public const string KYC_PENDING = "KYC_PENDING";

        /// <summary>
        /// KYC verification was rejected
        /// </summary>
        public const string KYC_REJECTED = "KYC_REJECTED";

        /// <summary>
        /// KYC verification has expired
        /// </summary>
        public const string KYC_EXPIRED = "KYC_EXPIRED";

        /// <summary>
        /// KYC verification requires manual review
        /// </summary>
        public const string KYC_NEEDS_REVIEW = "KYC_NEEDS_REVIEW";

        /// <summary>
        /// User is not KYC verified
        /// </summary>
        public const string KYC_NOT_VERIFIED = "KYC_NOT_VERIFIED";

        /// <summary>
        /// KYC verification already pending
        /// </summary>
        public const string KYC_VERIFICATION_ALREADY_PENDING = "KYC_VERIFICATION_ALREADY_PENDING";

        /// <summary>
        /// KYC provider error
        /// </summary>
        public const string KYC_PROVIDER_ERROR = "KYC_PROVIDER_ERROR";

        /// <summary>
        /// KYC is required for this operation
        /// </summary>
        public const string KYC_REQUIRED = "KYC_REQUIRED";

        // Entitlement and Policy errors
        /// <summary>
        /// Entitlement limit exceeded for subscription tier
        /// </summary>
        public const string ENTITLEMENT_LIMIT_EXCEEDED = "ENTITLEMENT_LIMIT_EXCEEDED";

        /// <summary>
        /// Feature not included in current subscription tier
        /// </summary>
        public const string FEATURE_NOT_INCLUDED = "FEATURE_NOT_INCLUDED";

        /// <summary>
        /// Operation requires a higher subscription tier
        /// </summary>
        public const string TIER_UPGRADE_REQUIRED = "TIER_UPGRADE_REQUIRED";

        /// <summary>
        /// Monthly quota exceeded
        /// </summary>
        public const string MONTHLY_QUOTA_EXCEEDED = "MONTHLY_QUOTA_EXCEEDED";

        /// <summary>
        /// Concurrent operation limit exceeded
        /// </summary>
        public const string CONCURRENT_LIMIT_EXCEEDED = "CONCURRENT_LIMIT_EXCEEDED";

        // ARC76 Account Readiness errors
        /// <summary>
        /// ARC76 account is not ready for operations
        /// </summary>
        public const string ACCOUNT_NOT_READY = "ACCOUNT_NOT_READY";

        /// <summary>
        /// ARC76 account is still initializing
        /// </summary>
        public const string ACCOUNT_INITIALIZING = "ACCOUNT_INITIALIZING";

        /// <summary>
        /// ARC76 account is in degraded state
        /// </summary>
        public const string ACCOUNT_DEGRADED = "ACCOUNT_DEGRADED";

        /// <summary>
        /// ARC76 account initialization failed
        /// </summary>
        public const string ACCOUNT_INITIALIZATION_FAILED = "ACCOUNT_INITIALIZATION_FAILED";

        /// <summary>
        /// ARC76 account key rotation required
        /// </summary>
        public const string ACCOUNT_KEY_ROTATION_REQUIRED = "ACCOUNT_KEY_ROTATION_REQUIRED";

        /// <summary>
        /// ARC76 account metadata invalid
        /// </summary>
        public const string ACCOUNT_METADATA_INVALID = "ACCOUNT_METADATA_INVALID";
    }
}

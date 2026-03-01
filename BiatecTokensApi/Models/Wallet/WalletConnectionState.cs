namespace BiatecTokensApi.Models.Wallet
{
    /// <summary>
    /// Represents the current wallet connection status
    /// </summary>
    public enum WalletConnectionStatus
    {
        /// <summary>
        /// No wallet is connected
        /// </summary>
        Disconnected,

        /// <summary>
        /// Wallet connection is being established
        /// </summary>
        Connecting,

        /// <summary>
        /// Wallet is connected and on the expected network
        /// </summary>
        Connected,

        /// <summary>
        /// A reconnection attempt is in progress
        /// </summary>
        Reconnecting,

        /// <summary>
        /// Wallet is connected but on a different network than required
        /// </summary>
        NetworkMismatch,

        /// <summary>
        /// Connection failed with an error; recovery guidance is available
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents the current state of a wallet connection, including network mismatch and error details
    /// </summary>
    public class WalletConnectionState
    {
        /// <summary>
        /// Wallet address (empty when Disconnected)
        /// </summary>
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>
        /// Current connection status
        /// </summary>
        public WalletConnectionStatus Status { get; set; } = WalletConnectionStatus.Disconnected;

        /// <summary>
        /// Network the wallet is currently connected to
        /// </summary>
        public string? ActualNetwork { get; set; }

        /// <summary>
        /// Network required by the application
        /// </summary>
        public string? ExpectedNetwork { get; set; }

        /// <summary>
        /// Whether this is the first time this wallet has connected
        /// </summary>
        public bool IsFirstConnection { get; set; }

        /// <summary>
        /// Whether reconnect guidance should be shown
        /// </summary>
        public bool HasReconnectGuidance { get; set; }

        /// <summary>
        /// Human-readable status message for the user
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// When the connection was established (null when not connected)
        /// </summary>
        public DateTime? ConnectedAt { get; set; }

        /// <summary>
        /// When the last connection attempt was made
        /// </summary>
        public DateTime LastAttemptAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Error code if Status is Error (null otherwise)
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// User-readable error description (null unless Status is Error)
        /// </summary>
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// Network mismatch details (null unless Status is NetworkMismatch)
        /// </summary>
        public NetworkMismatchInfo? NetworkMismatch { get; set; }
    }

    /// <summary>
    /// Describes a detected network mismatch between the connected wallet and the required network
    /// </summary>
    public class NetworkMismatchInfo
    {
        /// <summary>
        /// Network the wallet is currently on
        /// </summary>
        public string ActualNetwork { get; set; } = string.Empty;

        /// <summary>
        /// Network the application requires
        /// </summary>
        public string ExpectedNetwork { get; set; } = string.Empty;

        /// <summary>
        /// User-readable description of the mismatch
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Step-by-step instructions to resolve the mismatch
        /// </summary>
        public string ResolutionGuidance { get; set; } = string.Empty;
    }

    /// <summary>
    /// Guidance for reconnecting a wallet, including ordered recovery steps
    /// </summary>
    public class WalletReconnectGuidance
    {
        /// <summary>
        /// Root cause category for why reconnection is needed
        /// </summary>
        public WalletReconnectReason Reason { get; set; }

        /// <summary>
        /// User-readable explanation of why reconnection is needed
        /// </summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of steps the user should take to reconnect
        /// </summary>
        public List<WalletReconnectStep> Steps { get; set; } = new();

        /// <summary>
        /// Whether the issue can be auto-resolved without user action
        /// </summary>
        public bool CanAutoResolve { get; set; }

        /// <summary>
        /// Estimated time in seconds to resolve (null if unknown)
        /// </summary>
        public int? EstimatedResolutionSeconds { get; set; }
    }

    /// <summary>
    /// Reason categories for wallet reconnection
    /// </summary>
    public enum WalletReconnectReason
    {
        /// <summary>
        /// Session expired; user must re-authenticate
        /// </summary>
        SessionExpired,

        /// <summary>
        /// Connected to wrong network; user must switch
        /// </summary>
        NetworkMismatch,

        /// <summary>
        /// Transient network error; retry is safe
        /// </summary>
        NetworkTimeout,

        /// <summary>
        /// Wallet rejected or revoked authorization
        /// </summary>
        AuthorizationRevoked,

        /// <summary>
        /// Wallet application closed or disconnected
        /// </summary>
        WalletDisconnected,

        /// <summary>
        /// Unknown reason; general guidance is provided
        /// </summary>
        Unknown
    }

    /// <summary>
    /// A single ordered step in a wallet reconnect guidance flow
    /// </summary>
    public class WalletReconnectStep
    {
        /// <summary>
        /// Step number (1-based)
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Short title for this step
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed instruction for this step
        /// </summary>
        public string Instruction { get; set; } = string.Empty;

        /// <summary>
        /// Whether this step can be performed automatically
        /// </summary>
        public bool IsAutomated { get; set; }
    }
}

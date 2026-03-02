using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.Wallet
{
    /// <summary>
    /// Request for wallet routing options to optimize cross-network operations
    /// </summary>
    public class WalletRoutingRequest
    {
        /// <summary>
        /// Source network the wallet is currently on
        /// </summary>
        public string SourceNetwork { get; set; } = string.Empty;

        /// <summary>
        /// Target network for the operation
        /// </summary>
        public string TargetNetwork { get; set; } = string.Empty;

        /// <summary>
        /// Operation type being attempted
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WalletOperationType OperationType { get; set; } = WalletOperationType.TokenPurchase;

        /// <summary>
        /// Optional token identifier if routing for a specific token
        /// </summary>
        public string? TokenIdentifier { get; set; }

        /// <summary>
        /// Estimated transaction value in USD (used for fee optimization)
        /// </summary>
        public decimal? EstimatedValueUsd { get; set; }
    }

    /// <summary>
    /// Types of wallet operations that can be routed
    /// </summary>
    public enum WalletOperationType
    {
        /// <summary>
        /// Purchasing a token
        /// </summary>
        TokenPurchase,

        /// <summary>
        /// Deploying a new token
        /// </summary>
        TokenDeployment,

        /// <summary>
        /// Transferring tokens between wallets
        /// </summary>
        TokenTransfer,

        /// <summary>
        /// Bridging tokens cross-chain
        /// </summary>
        CrossChainBridge,

        /// <summary>
        /// Swapping tokens on a DEX
        /// </summary>
        TokenSwap
    }

    /// <summary>
    /// Response containing wallet routing options for optimized operations
    /// </summary>
    public class WalletRoutingResponse
    {
        /// <summary>
        /// Whether a direct route exists between source and target network
        /// </summary>
        public bool HasDirectRoute { get; set; }

        /// <summary>
        /// Recommended routing option for the best balance of cost and speed
        /// </summary>
        public WalletRoutingOption? RecommendedRoute { get; set; }

        /// <summary>
        /// All available routing options ordered by recommendation
        /// </summary>
        public List<WalletRoutingOption> AvailableRoutes { get; set; } = new();

        /// <summary>
        /// Whether the source and target are the same network (no routing needed)
        /// </summary>
        public bool IsSameNetwork { get; set; }

        /// <summary>
        /// User-readable routing summary
        /// </summary>
        public string RoutingSummary { get; set; } = string.Empty;

        /// <summary>
        /// When this routing analysis was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// A specific routing option between source and target network
    /// </summary>
    public class WalletRoutingOption
    {
        /// <summary>
        /// Unique route identifier
        /// </summary>
        public string RouteId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Route display name
        /// </summary>
        public string RouteName { get; set; } = string.Empty;

        /// <summary>
        /// Routing type
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WalletRouteType RouteType { get; set; }

        /// <summary>
        /// Estimated time to complete in seconds
        /// </summary>
        public int EstimatedTimeSeconds { get; set; }

        /// <summary>
        /// Estimated transaction fee in USD
        /// </summary>
        public decimal EstimatedFeeUsd { get; set; }

        /// <summary>
        /// Ordered steps the user must take to use this route
        /// </summary>
        public List<WalletRoutingStep> Steps { get; set; } = new();

        /// <summary>
        /// Confidence level for this route (0-100)
        /// </summary>
        public int ConfidenceScore { get; set; }

        /// <summary>
        /// Whether this route is recommended as the best option
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Risk warnings associated with this route
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Type of routing available between networks
    /// </summary>
    public enum WalletRouteType
    {
        /// <summary>
        /// Direct transfer on the same network (no bridging)
        /// </summary>
        Direct,

        /// <summary>
        /// Cross-chain bridge
        /// </summary>
        Bridge,

        /// <summary>
        /// CEX deposit/withdrawal route
        /// </summary>
        CentralizedExchange,

        /// <summary>
        /// DEX swap within the same chain ecosystem
        /// </summary>
        DecentralizedSwap,

        /// <summary>
        /// Multi-hop route through intermediate networks
        /// </summary>
        MultiHop
    }

    /// <summary>
    /// A single step in a wallet routing flow
    /// </summary>
    public class WalletRoutingStep
    {
        /// <summary>
        /// Step number (1-based)
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Short step title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed instruction for this step
        /// </summary>
        public string Instruction { get; set; } = string.Empty;

        /// <summary>
        /// Estimated time for this step in seconds
        /// </summary>
        public int EstimatedSeconds { get; set; }

        /// <summary>
        /// Whether this step can be automated
        /// </summary>
        public bool IsAutomatable { get; set; }
    }
}

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents compliance metadata for a blockchain network
    /// </summary>
    /// <remarks>
    /// This model provides per-network compliance flags and requirements that enable
    /// the frontend to display compliance indicators for different blockchain networks.
    /// </remarks>
    public class NetworkComplianceMetadata
    {
        /// <summary>
        /// Network identifier (e.g., "voimain-v1.0", "aramidmain-v1.0", "mainnet-v1.0", "testnet-v1.0")
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable network name (e.g., "VOI Mainnet", "Aramid Mainnet", "Algorand Mainnet")
        /// </summary>
        public string NetworkName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this network is ready for MICA (Markets in Crypto-Assets) compliance
        /// </summary>
        /// <remarks>
        /// MICA readiness indicates the network supports required compliance features
        /// such as jurisdiction tracking and regulatory framework specification.
        /// </remarks>
        public bool IsMicaReady { get; set; }

        /// <summary>
        /// Indicates if whitelisting is required for tokens on this network
        /// </summary>
        /// <remarks>
        /// Networks like VOI and Aramid may have stricter compliance requirements
        /// that mandate whitelisting for RWA tokens.
        /// </remarks>
        public bool RequiresWhitelisting { get; set; }

        /// <summary>
        /// Indicates if jurisdiction specification is required for this network
        /// </summary>
        public bool RequiresJurisdiction { get; set; }

        /// <summary>
        /// Indicates if regulatory framework specification is required for this network
        /// </summary>
        public bool RequiresRegulatoryFramework { get; set; }

        /// <summary>
        /// Description of network-specific compliance requirements
        /// </summary>
        public string? ComplianceRequirements { get; set; }

        /// <summary>
        /// Source of compliance metadata (e.g., "Network policy", "Regulatory guidance")
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Timestamp when this metadata was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response model for network compliance metadata endpoint
    /// </summary>
    public class NetworkComplianceMetadataResponse : BaseResponse
    {
        /// <summary>
        /// List of networks with their compliance metadata
        /// </summary>
        public List<NetworkComplianceMetadata> Networks { get; set; } = new List<NetworkComplianceMetadata>();

        /// <summary>
        /// Cache duration in seconds (recommended client-side cache time)
        /// </summary>
        public int CacheDurationSeconds { get; set; } = 3600; // 1 hour default
    }
}

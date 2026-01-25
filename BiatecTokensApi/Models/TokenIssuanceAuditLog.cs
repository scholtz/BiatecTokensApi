namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Audit log entry for token issuance/deployment events
    /// </summary>
    /// <remarks>
    /// Tracks all token creation events across different token standards (ERC20, ASA, ARC3, ARC200, ARC1400)
    /// for MICA compliance and regulatory reporting. Supports 7-year retention requirements.
    /// </remarks>
    public class TokenIssuanceAuditLogEntry
    {
        /// <summary>
        /// Unique identifier for the audit log entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID of the created token (for Algorand tokens) or contract address (for EVM tokens)
        /// </summary>
        public string? AssetIdentifier { get; set; }

        /// <summary>
        /// The numeric asset ID for Algorand tokens
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// The contract address for EVM tokens
        /// </summary>
        public string? ContractAddress { get; set; }

        /// <summary>
        /// Network on which the token was deployed (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, base-mainnet, etc.)
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Token standard type (ERC20_Mintable, ERC20_Preminted, ASA_FT, ASA_NFT, ARC3_FT, ARC3_NFT, ARC200_Mintable, etc.)
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Token symbol/unit name
        /// </summary>
        public string? TokenSymbol { get; set; }

        /// <summary>
        /// Total supply of the token
        /// </summary>
        public string? TotalSupply { get; set; }

        /// <summary>
        /// Number of decimal places for the token
        /// </summary>
        public int? Decimals { get; set; }

        /// <summary>
        /// The address of the user who deployed the token
        /// </summary>
        public string DeployedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the token was deployed (UTC)
        /// </summary>
        public DateTime DeployedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the deployment was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Transaction hash/ID for the deployment transaction
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Block number or round when the token was deployed
        /// </summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>
        /// Additional metadata or notes about the deployment
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Whether the token is mintable
        /// </summary>
        public bool? IsMintable { get; set; }

        /// <summary>
        /// Whether the token is pausable
        /// </summary>
        public bool? IsPausable { get; set; }

        /// <summary>
        /// Whether the token is burnable
        /// </summary>
        public bool? IsBurnable { get; set; }

        /// <summary>
        /// Manager address for the token (Algorand)
        /// </summary>
        public string? ManagerAddress { get; set; }

        /// <summary>
        /// Reserve address for the token (Algorand)
        /// </summary>
        public string? ReserveAddress { get; set; }

        /// <summary>
        /// Freeze address for the token (Algorand)
        /// </summary>
        public string? FreezeAddress { get; set; }

        /// <summary>
        /// Clawback address for the token (Algorand)
        /// </summary>
        public string? ClawbackAddress { get; set; }

        /// <summary>
        /// IPFS URL for metadata (ARC3 tokens)
        /// </summary>
        public string? MetadataUrl { get; set; }

        /// <summary>
        /// Source system that generated this audit entry
        /// </summary>
        public string SourceSystem { get; set; } = "BiatecTokensApi";

        /// <summary>
        /// Correlation ID for related events
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request to retrieve token issuance audit logs with filtering
    /// </summary>
    public class GetTokenIssuanceAuditLogRequest
    {
        /// <summary>
        /// Optional filter by asset ID (for Algorand tokens)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by contract address (for EVM tokens)
        /// </summary>
        public string? ContractAddress { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token type
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Optional filter by deployer address
        /// </summary>
        public string? DeployedBy { get; set; }

        /// <summary>
        /// Optional filter by operation result (success/failure)
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }
}

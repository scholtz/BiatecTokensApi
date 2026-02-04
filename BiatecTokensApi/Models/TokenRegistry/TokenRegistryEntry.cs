using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.TokenRegistry
{
    /// <summary>
    /// Represents a canonical token registry entry with identity, compliance, and operational metadata
    /// </summary>
    /// <remarks>
    /// This is the core registry model that aggregates token data from internal and external sources.
    /// It provides a consistent schema for discovery and compliance filtering.
    /// </remarks>
    public class TokenRegistryEntry
    {
        /// <summary>
        /// Unique identifier for the registry entry (auto-generated)
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Token identifier on the blockchain (asset ID for Algorand, contract address for EVM)
        /// </summary>
        [Required]
        public string TokenIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Blockchain network identifier (e.g., "algorand-mainnet", "base-mainnet", "ethereum-mainnet")
        /// </summary>
        [Required]
        public string Chain { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Token symbol (ticker)
        /// </summary>
        [Required]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Number of decimal places for the token
        /// </summary>
        public int Decimals { get; set; }

        /// <summary>
        /// Total supply of the token
        /// </summary>
        public string? TotalSupply { get; set; }

        /// <summary>
        /// Token standards supported (e.g., "ASA", "ARC3", "ARC19", "ARC69", "ARC200", "ERC20")
        /// </summary>
        public List<string> SupportedStandards { get; set; } = new();

        /// <summary>
        /// Primary token standard classification
        /// </summary>
        public string? PrimaryStandard { get; set; }

        /// <summary>
        /// Legal issuer identity information
        /// </summary>
        public IssuerIdentity? Issuer { get; set; }

        /// <summary>
        /// Compliance status and scoring information
        /// </summary>
        public ComplianceScoring Compliance { get; set; } = new();

        /// <summary>
        /// Operational readiness attributes
        /// </summary>
        public OperationalReadiness Readiness { get; set; } = new();

        /// <summary>
        /// Optional token description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Token website URL
        /// </summary>
        public string? Website { get; set; }

        /// <summary>
        /// Token logo URL
        /// </summary>
        public string? LogoUrl { get; set; }

        /// <summary>
        /// Source of this registry data (e.g., "internal", "vestige", "coinmarketcap")
        /// </summary>
        public string DataSource { get; set; } = "internal";

        /// <summary>
        /// External registry URLs where this token is listed
        /// </summary>
        public List<string> ExternalRegistries { get; set; } = new();

        /// <summary>
        /// Tags for categorization and search
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Date when the registry entry was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the registry entry was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Address of the user who created this entry
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Date when the token was deployed on the blockchain
        /// </summary>
        public DateTime? DeployedAt { get; set; }
    }

    /// <summary>
    /// Issuer identity information for a token
    /// </summary>
    public class IssuerIdentity
    {
        /// <summary>
        /// Legal name of the issuer
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Issuer blockchain address
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Issuer website
        /// </summary>
        public string? Website { get; set; }

        /// <summary>
        /// Issuer email contact
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Country of incorporation
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Legal entity type (e.g., "Corporation", "Foundation", "DAO")
        /// </summary>
        public string? EntityType { get; set; }

        /// <summary>
        /// Legal entity registration number
        /// </summary>
        public string? RegistrationNumber { get; set; }

        /// <summary>
        /// Whether the issuer identity has been verified
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// KYC/KYB verification provider
        /// </summary>
        public string? VerificationProvider { get; set; }

        /// <summary>
        /// Date when issuer verification was completed
        /// </summary>
        public DateTime? VerificationDate { get; set; }
    }

    /// <summary>
    /// Compliance status and scoring information
    /// </summary>
    public class ComplianceScoring
    {
        /// <summary>
        /// Overall compliance status
        /// </summary>
        public ComplianceState Status { get; set; } = ComplianceState.Unknown;

        /// <summary>
        /// Compliance score (0-100, higher is better)
        /// </summary>
        public int? Score { get; set; }

        /// <summary>
        /// Explanation of the compliance status
        /// </summary>
        public string? StatusReason { get; set; }

        /// <summary>
        /// Regulatory frameworks the token complies with (e.g., "MICA", "SEC Reg D")
        /// </summary>
        public List<string> RegulatoryFrameworks { get; set; } = new();

        /// <summary>
        /// Jurisdictions where the token is compliant (ISO country codes)
        /// </summary>
        public List<string> Jurisdictions { get; set; } = new();

        /// <summary>
        /// Date of last compliance review
        /// </summary>
        public DateTime? LastReviewDate { get; set; }

        /// <summary>
        /// Date of next compliance review
        /// </summary>
        public DateTime? NextReviewDate { get; set; }

        /// <summary>
        /// Audit sources that have reviewed this token
        /// </summary>
        public List<string> AuditSources { get; set; } = new();

        /// <summary>
        /// KYC/AML requirements for token holders
        /// </summary>
        public bool RequiresKyc { get; set; }

        /// <summary>
        /// Whether the token is restricted to accredited investors
        /// </summary>
        public bool AccreditedInvestorsOnly { get; set; }

        /// <summary>
        /// Transfer restrictions that apply to this token
        /// </summary>
        public List<string> TransferRestrictions { get; set; } = new();

        /// <summary>
        /// Maximum number of token holders allowed
        /// </summary>
        public int? MaxHolders { get; set; }
    }

    /// <summary>
    /// Operational readiness attributes for a token
    /// </summary>
    public class OperationalReadiness
    {
        /// <summary>
        /// Whether the token smart contract is verified on a block explorer
        /// </summary>
        public bool IsContractVerified { get; set; }

        /// <summary>
        /// Block explorer URL for contract verification
        /// </summary>
        public string? VerificationUrl { get; set; }

        /// <summary>
        /// Whether the token has been audited by a third party
        /// </summary>
        public bool IsAudited { get; set; }

        /// <summary>
        /// Audit report references
        /// </summary>
        public List<AuditReference> AuditReports { get; set; } = new();

        /// <summary>
        /// Whether the token metadata is available and valid
        /// </summary>
        public bool HasValidMetadata { get; set; }

        /// <summary>
        /// Metadata URL (for standards like ARC3, ARC19)
        /// </summary>
        public string? MetadataUrl { get; set; }

        /// <summary>
        /// Whether the token has liquidity on DEXs
        /// </summary>
        public bool HasLiquidity { get; set; }

        /// <summary>
        /// Whether the token contract is pausable
        /// </summary>
        public bool IsPausable { get; set; }

        /// <summary>
        /// Whether the token contract is upgradeable
        /// </summary>
        public bool IsUpgradeable { get; set; }

        /// <summary>
        /// Whether the token has a multisig controller
        /// </summary>
        public bool HasMultisigControl { get; set; }

        /// <summary>
        /// Security features enabled for the token
        /// </summary>
        public List<string> SecurityFeatures { get; set; } = new();

        /// <summary>
        /// Known vulnerabilities or security issues
        /// </summary>
        public List<string> SecurityIssues { get; set; } = new();
    }

    /// <summary>
    /// Reference to a third-party audit report
    /// </summary>
    public class AuditReference
    {
        /// <summary>
        /// Name of the auditing firm
        /// </summary>
        public string? Auditor { get; set; }

        /// <summary>
        /// Date when the audit was completed
        /// </summary>
        public DateTime? AuditDate { get; set; }

        /// <summary>
        /// URL to the audit report
        /// </summary>
        public string? ReportUrl { get; set; }

        /// <summary>
        /// Audit result summary (e.g., "Pass", "Pass with issues", "Fail")
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// Number of critical issues found
        /// </summary>
        public int? CriticalIssues { get; set; }

        /// <summary>
        /// Number of high severity issues found
        /// </summary>
        public int? HighIssues { get; set; }

        /// <summary>
        /// Number of medium severity issues found
        /// </summary>
        public int? MediumIssues { get; set; }
    }

    /// <summary>
    /// Compliance state taxonomy
    /// </summary>
    public enum ComplianceState
    {
        /// <summary>
        /// Compliance status is unknown or not yet evaluated
        /// </summary>
        Unknown,

        /// <summary>
        /// Compliance review is pending
        /// </summary>
        Pending,

        /// <summary>
        /// Token is compliant with relevant regulations
        /// </summary>
        Compliant,

        /// <summary>
        /// Token is non-compliant with relevant regulations
        /// </summary>
        NonCompliant,

        /// <summary>
        /// Compliance status is suspended pending review
        /// </summary>
        Suspended,

        /// <summary>
        /// Token is exempt from certain regulations
        /// </summary>
        Exempt
    }
}

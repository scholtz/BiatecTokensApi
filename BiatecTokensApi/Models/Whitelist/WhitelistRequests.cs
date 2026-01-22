using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Request to add a single address to a token's whitelist
    /// </summary>
    public class AddWhitelistEntryRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to add the whitelist entry
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// The Algorand address to whitelist
        /// </summary>
        [Required]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// The status of the whitelist entry (defaults to Active)
        /// </summary>
        public WhitelistStatus Status { get; set; } = WhitelistStatus.Active;

        /// <summary>
        /// Reason for whitelisting this address
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Date when the whitelist entry expires (optional)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Whether KYC verification has been completed
        /// </summary>
        public bool KycVerified { get; set; }

        /// <summary>
        /// Date when KYC verification was completed
        /// </summary>
        public DateTime? KycVerificationDate { get; set; }

        /// <summary>
        /// Name of the KYC provider
        /// </summary>
        public string? KycProvider { get; set; }

        /// <summary>
        /// Network on which the token is deployed (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, testnet-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Role of the user performing the operation (Admin or Operator)
        /// </summary>
        public WhitelistRole Role { get; set; } = WhitelistRole.Admin;
    }

    /// <summary>
    /// Request to remove an address from a token's whitelist
    /// </summary>
    public class RemoveWhitelistEntryRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to remove the whitelist entry
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// The Algorand address to remove from the whitelist
        /// </summary>
        [Required]
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to bulk upload whitelist entries for a token
    /// </summary>
    public class BulkAddWhitelistRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to add the whitelist entries
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// List of addresses to whitelist
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one address is required")]
        public List<string> Addresses { get; set; } = new();

        /// <summary>
        /// The status for all entries (defaults to Active)
        /// </summary>
        public WhitelistStatus Status { get; set; } = WhitelistStatus.Active;

        /// <summary>
        /// Reason for whitelisting these addresses (applies to all)
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Date when the whitelist entries expire (optional, applies to all)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Whether KYC verification has been completed for all addresses
        /// </summary>
        public bool KycVerified { get; set; }

        /// <summary>
        /// Date when KYC verification was completed (applies to all)
        /// </summary>
        public DateTime? KycVerificationDate { get; set; }

        /// <summary>
        /// Name of the KYC provider (applies to all)
        /// </summary>
        public string? KycProvider { get; set; }

        /// <summary>
        /// Network on which the token is deployed (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, testnet-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Role of the user performing the operation (Admin or Operator)
        /// </summary>
        public WhitelistRole Role { get; set; } = WhitelistRole.Admin;
    }

    /// <summary>
    /// Request to list whitelist entries for a token
    /// </summary>
    public class ListWhitelistRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to list whitelist entries
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Optional status filter
        /// </summary>
        public WhitelistStatus? Status { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request to validate a token transfer between two addresses
    /// </summary>
    public class ValidateTransferRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to validate the transfer
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// The sender's Algorand address
        /// </summary>
        [Required]
        public string FromAddress { get; set; } = string.Empty;

        /// <summary>
        /// The receiver's Algorand address
        /// </summary>
        [Required]
        public string ToAddress { get; set; } = string.Empty;

        /// <summary>
        /// Optional amount to transfer (for future use in amount-based restrictions)
        /// </summary>
        public ulong? Amount { get; set; }
    }
}

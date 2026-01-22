namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Response for whitelist operations
    /// </summary>
    public class WhitelistResponse : BaseResponse
    {
        /// <summary>
        /// The whitelist entry that was created or modified
        /// </summary>
        public WhitelistEntry? Entry { get; set; }

        /// <summary>
        /// Number of entries affected (for bulk operations)
        /// </summary>
        public int? AffectedCount { get; set; }
    }

    /// <summary>
    /// Response for listing whitelist entries
    /// </summary>
    public class WhitelistListResponse : BaseResponse
    {
        /// <summary>
        /// List of whitelist entries
        /// </summary>
        public List<WhitelistEntry> Entries { get; set; } = new();

        /// <summary>
        /// Total number of entries matching the filter
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Response for bulk whitelist operations
    /// </summary>
    public class BulkWhitelistResponse : BaseResponse
    {
        /// <summary>
        /// List of successfully added/updated entries
        /// </summary>
        public List<WhitelistEntry> SuccessfulEntries { get; set; } = new();

        /// <summary>
        /// List of addresses that failed validation
        /// </summary>
        public List<string> FailedAddresses { get; set; } = new();

        /// <summary>
        /// Number of entries successfully processed
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of entries that failed
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new();
    }

    /// <summary>
    /// Response for transfer validation
    /// </summary>
    public class ValidateTransferResponse : BaseResponse
    {
        /// <summary>
        /// Whether the transfer is allowed based on whitelist rules
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Reason why the transfer is not allowed (if IsAllowed is false)
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// Details about the sender's whitelist status
        /// </summary>
        public TransferParticipantStatus? SenderStatus { get; set; }

        /// <summary>
        /// Details about the receiver's whitelist status
        /// </summary>
        public TransferParticipantStatus? ReceiverStatus { get; set; }
    }

    /// <summary>
    /// Status information for a transfer participant (sender or receiver)
    /// </summary>
    public class TransferParticipantStatus
    {
        /// <summary>
        /// The Algorand address
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Whether the address is whitelisted
        /// </summary>
        public bool IsWhitelisted { get; set; }

        /// <summary>
        /// Whether the whitelist entry is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether the whitelist entry has expired
        /// </summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// The expiration date if applicable
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Current whitelist status
        /// </summary>
        public WhitelistStatus? Status { get; set; }
    }
}

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
}

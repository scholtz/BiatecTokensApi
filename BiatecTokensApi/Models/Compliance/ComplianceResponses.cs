namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Response for compliance metadata operations
    /// </summary>
    public class ComplianceMetadataResponse : BaseResponse
    {
        /// <summary>
        /// The compliance metadata that was created, retrieved, or modified
        /// </summary>
        public ComplianceMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Response for listing compliance metadata
    /// </summary>
    public class ComplianceMetadataListResponse : BaseResponse
    {
        /// <summary>
        /// List of compliance metadata entries
        /// </summary>
        public List<ComplianceMetadata> Metadata { get; set; } = new();

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
}

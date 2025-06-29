namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for IPFS repository operations
    /// </summary>
    public class IPFSConfig
    {
        /// <summary>
        /// IPFS API endpoint for storing data
        /// </summary>
        public string ApiUrl { get; set; } = "https://ipfs-api.biatec.io";

        /// <summary>
        /// IPFS gateway URL for fetching data
        /// </summary>
        public string GatewayUrl { get; set; } = "https://ipfs.biatec.io/ipfs";

        /// <summary>
        /// Username for basic authentication
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for basic authentication
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Timeout for HTTP requests in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum file size allowed for upload in bytes (default: 10MB)
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Whether to validate content hashes when retrieving data
        /// </summary>
        public bool ValidateContentHash { get; set; } = true;
    }
}
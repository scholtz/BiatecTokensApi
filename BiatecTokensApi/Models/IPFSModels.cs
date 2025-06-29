using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Response from IPFS API when uploading content
    /// </summary>
    public class IPFSUploadResponse
    {
        /// <summary>
        /// Content Identifier (CID) of the uploaded content
        /// </summary>
        public string? Hash { get; set; }

        /// <summary>
        /// Name of the uploaded file
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Size of the uploaded content in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Whether the upload was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if upload failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Full IPFS gateway URL to access the content
        /// </summary>
        public string? GatewayUrl { get; set; }
    }

    /// <summary>
    /// Request for uploading content to IPFS
    /// </summary>
    public class IPFSUploadRequest
    {
        /// <summary>
        /// Content to upload as bytes
        /// </summary>
        [Required]
        public required byte[] Content { get; set; }

        /// <summary>
        /// Optional filename for the content
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// MIME type of the content
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Whether to pin the content to ensure it stays available
        /// </summary>
        public bool Pin { get; set; } = true;
    }

    /// <summary>
    /// Response when retrieving content from IPFS
    /// </summary>
    public class IPFSRetrieveResponse
    {
        /// <summary>
        /// Retrieved content as bytes
        /// </summary>
        public byte[]? Content { get; set; }

        /// <summary>
        /// Content type of the retrieved data
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Size of the retrieved content
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Whether the retrieval was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if retrieval failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Hash integrity check result
        /// </summary>
        public bool HashVerified { get; set; }
    }

    /// <summary>
    /// IPFS content metadata
    /// </summary>
    public class IPFSContentInfo
    {
        /// <summary>
        /// Content Identifier (CID)
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Content size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Content type/MIME type
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// When the content was uploaded
        /// </summary>
        public DateTime? UploadedAt { get; set; }

        /// <summary>
        /// Whether the content is pinned
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Gateway URL to access the content
        /// </summary>
        public string? GatewayUrl { get; set; }
    }
}
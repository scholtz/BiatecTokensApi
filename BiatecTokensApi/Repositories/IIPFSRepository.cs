using BiatecTokensApi.Models;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Interface for IPFS repository operations
    /// </summary>
    public interface IIPFSRepository
    {
        /// <summary>
        /// Uploads content to IPFS
        /// </summary>
        /// <param name="request">Upload request with content and metadata</param>
        /// <returns>Upload response with CID and gateway URL</returns>
        Task<IPFSUploadResponse> UploadAsync(IPFSUploadRequest request);

        /// <summary>
        /// Uploads text content to IPFS
        /// </summary>
        /// <param name="content">Text content to upload</param>
        /// <param name="fileName">Optional filename</param>
        /// <param name="contentType">Content type (default: text/plain)</param>
        /// <returns>Upload response with CID and gateway URL</returns>
        Task<IPFSUploadResponse> UploadTextAsync(string content, string? fileName = null, string contentType = "text/plain");

        /// <summary>
        /// Uploads JSON content to IPFS
        /// </summary>
        /// <param name="jsonContent">JSON content as string</param>
        /// <param name="fileName">Optional filename</param>
        /// <returns>Upload response with CID and gateway URL</returns>
        Task<IPFSUploadResponse> UploadJsonAsync(string jsonContent, string? fileName = null);

        /// <summary>
        /// Uploads an object as JSON to IPFS
        /// </summary>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        /// <param name="obj">Object to serialize and upload</param>
        /// <param name="fileName">Optional filename</param>
        /// <returns>Upload response with CID and gateway URL</returns>
        Task<IPFSUploadResponse> UploadObjectAsync<T>(T obj, string? fileName = null);

        /// <summary>
        /// Retrieves content from IPFS by CID
        /// </summary>
        /// <param name="cid">Content Identifier</param>
        /// <returns>Retrieved content and metadata</returns>
        Task<IPFSRetrieveResponse> RetrieveAsync(string cid);

        /// <summary>
        /// Retrieves text content from IPFS by CID
        /// </summary>
        /// <param name="cid">Content Identifier</param>
        /// <returns>Text content as string</returns>
        Task<string?> RetrieveTextAsync(string cid);

        /// <summary>
        /// Retrieves and deserializes JSON content from IPFS
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="cid">Content Identifier</param>
        /// <returns>Deserialized object</returns>
        Task<T?> RetrieveObjectAsync<T>(string cid) where T : class;

        /// <summary>
        /// Gets information about content stored in IPFS
        /// </summary>
        /// <param name="cid">Content Identifier</param>
        /// <returns>Content information</returns>
        Task<IPFSContentInfo?> GetContentInfoAsync(string cid);

        /// <summary>
        /// Checks if content exists in IPFS
        /// </summary>
        /// <param name="cid">Content Identifier</param>
        /// <returns>True if content exists and is accessible</returns>
        Task<bool> ExistsAsync(string cid);

        /// <summary>
        /// Pins content in IPFS to ensure it stays available
        /// </summary>
        /// <param name="cid">Content Identifier</param>
        /// <returns>True if pinning was successful</returns>
        Task<bool> PinAsync(string cid);

        /// <summary>
        /// Unpins content from IPFS
        /// </summary>
        /// <param name="cid">Content Identifier</param>
        /// <returns>True if unpinning was successful</returns>
        Task<bool> UnpinAsync(string cid);
    }
}
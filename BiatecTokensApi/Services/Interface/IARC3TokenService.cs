using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC200.Response;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for ARC3 Fungible Token Service operations on Algorand blockchain
    /// </summary>
    public interface IARC3TokenService
    {
        /// <summary>
        /// Creates an ARC3 token on Algorand blockchain
        /// </summary>
        /// <param name="request">Token creation parameters</param>
        /// <param name="tokenType">Token type</param>
        /// <returns>Response with transaction details and asset ID</returns>
        Task<ARC3TokenDeploymentResponse> CreateARC3TokenAsync(IARC3TokenDeploymentRequest request, TokenType tokenType);

        /// <summary>
        /// Uploads ARC3 metadata to IPFS and returns the URL and hash
        /// </summary>
        /// <param name="metadata">ARC3 metadata to upload</param>
        /// <returns>Tuple containing the IPFS URL and content hash</returns>
        Task<(string? Url, string? Hash, string? Sha256Hash)> UploadMetadataAsync(ARC3TokenMetadata metadata);

        /// <summary>
        /// Validates ARC3 metadata structure
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>True if valid, otherwise false with error message</returns>
        (bool IsValid, string? ErrorMessage) ValidateMetadata(ARC3TokenMetadata metadata);
    }
}
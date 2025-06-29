using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Interface for ARC3 Fungible Token Service operations on Algorand blockchain
    /// </summary>
    public interface IARC3FungibleTokenService
    {
        /// <summary>
        /// Creates an ARC3 fungible token on Algorand blockchain
        /// </summary>
        /// <param name="request">Token creation parameters</param>
        /// <returns>Response with transaction details and asset ID</returns>
        Task<ARC3TokenDeploymentResponse> CreateTokenAsync(ARC3FungibleTokenDeploymentRequest request);

        /// <summary>
        /// Gets information about an existing ARC3 token
        /// </summary>
        /// <param name="assetId">Asset ID of the token</param>
        /// <param name="network">Network name (mainnet, testnet, betanet)</param>
        /// <returns>Token information</returns>
        Task<ARC3TokenInfo?> GetTokenInfoAsync(ulong assetId, string network);

        /// <summary>
        /// Transfers ARC3 tokens between accounts
        /// </summary>
        /// <param name="assetId">Asset ID of the token to transfer</param>
        /// <param name="fromMnemonic">Mnemonic of the sender account</param>
        /// <param name="toAddress">Recipient address</param>
        /// <param name="amount">Amount to transfer</param>
        /// <param name="network">Network name</param>
        /// <returns>Transaction ID if successful</returns>
        Task<string?> TransferTokenAsync(ulong assetId, string fromMnemonic, string toAddress, ulong amount, string network);

        /// <summary>
        /// Opts an account into receiving a specific ARC3 token
        /// </summary>
        /// <param name="assetId">Asset ID of the token</param>
        /// <param name="accountMnemonic">Mnemonic of the account to opt in</param>
        /// <param name="network">Network name</param>
        /// <returns>Transaction ID if successful</returns>
        Task<string?> OptInToTokenAsync(ulong assetId, string accountMnemonic, string network);

        /// <summary>
        /// Uploads ARC3 metadata to IPFS and returns the URL and hash
        /// </summary>
        /// <param name="metadata">ARC3 metadata to upload</param>
        /// <returns>Tuple containing the IPFS URL and content hash</returns>
        Task<(string? Url, string? Hash)> UploadMetadataAsync(ARC3TokenMetadata metadata);

        /// <summary>
        /// Validates ARC3 metadata structure
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>True if valid, otherwise false with error message</returns>
        (bool IsValid, string? ErrorMessage) ValidateMetadata(ARC3TokenMetadata metadata);
    }
}
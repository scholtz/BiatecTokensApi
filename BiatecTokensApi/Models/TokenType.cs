namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Represents the various types of tokens supported by the system.
    /// </summary>
    /// <remarks>This enumeration categorizes tokens based on their characteristics, such as quantity,
    /// decimals,  and metadata standards. It includes support for both Algorand Standard Assets (ASA) and 
    /// Ethereum-based ERC20 tokens, with further distinctions for fungibility and metadata.</remarks>
    public enum TokenType
    {
        /// <summary>
        /// ASA where sum of quantity is not equal to 1
        /// </summary>
        ASA_FT,
        /// <summary>
        /// ASA where quantity is equal to 1 and decimals is equal to 0
        /// </summary>
        ASA_NFT,
        /// <summary>
        /// ASA where sum of quantity is equal to 1, but decimals are not 0        
        /// </summary>
        ASA_FNFT,
        /// <summary>
        /// ASA_FT with ARC3 metadata pointing to json file with metadata
        /// </summary>
        ARC3_FT,
        /// <summary>
        /// ASA_NFT with ARC3 metadata pointing to json file with metadata
        /// </summary>
        ARC3_NFT,
        /// <summary>
        /// ASA_FNFT with ARC3 metadata pointing to json file with metadata
        /// </summary>
        ARC3_FNFT,
        /// <summary>
        /// AVM ARC200 token which supports minting functionality.
        /// </summary>
        ARC200_Mintable,
        /// <summary>
        /// AVM ARC200 token which is fully preminted.
        /// </summary>
        ARC200_Preminted,
        /// <summary>
        /// Represents an ERC20 token that supports minting functionality.
        /// </summary>
        ERC20_Mintable,
        /// <summary>
        /// Represents an ERC20 token which is fully preminted.
        /// </summary>
        ERC20_Preminted
    }
}

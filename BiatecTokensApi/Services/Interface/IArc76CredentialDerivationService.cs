namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for deterministic ARC76 credential-based Algorand account derivation.
    /// Implements the ARC-0076 specification: email + password → deterministic Algorand Ed25519 account.
    /// </summary>
    public interface IArc76CredentialDerivationService
    {
        /// <summary>
        /// Derives the Algorand address deterministically from email and password using ARC76.
        /// The same email + password always produces the same Algorand address.
        /// </summary>
        /// <param name="email">User email address (will be canonicalized to lowercase+trimmed)</param>
        /// <param name="password">User password (case-sensitive)</param>
        /// <returns>The deterministic Algorand address string</returns>
        string DeriveAddress(string email, string password);

        /// <summary>
        /// Derives the Algorand account (address + public key) from email and password using ARC76.
        /// The private key is zeroed after extracting the public key.
        /// </summary>
        /// <param name="email">User email address (will be canonicalized to lowercase+trimmed)</param>
        /// <param name="password">User password (case-sensitive)</param>
        /// <returns>Tuple of (algorandAddress, publicKeyBase64)</returns>
        (string Address, string PublicKeyBase64) DeriveAddressAndPublicKey(string email, string password);

        /// <summary>
        /// Derives the full Algorand account and returns the 25-word Algorand mnemonic
        /// for secure storage. The mnemonic encodes the private key in Algorand format.
        /// </summary>
        /// <param name="email">User email address (will be canonicalized to lowercase+trimmed)</param>
        /// <param name="password">User password</param>
        /// <returns>Tuple of (algorandAddress, mnemonic25Words)</returns>
        (string Address, string Mnemonic) DeriveAccountMnemonic(string email, string password);

        /// <summary>
        /// Canonicalizes the email address for consistent derivation.
        /// Applies: trim whitespace + convert to lowercase.
        /// </summary>
        string CanonicalizeEmail(string email);
    }
}

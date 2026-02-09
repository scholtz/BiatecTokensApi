namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for encryption key providers supporting various key management systems
    /// </summary>
    public interface IKeyProvider
    {
        /// <summary>
        /// Retrieves the encryption key for mnemonic encryption/decryption
        /// </summary>
        /// <returns>Encryption key as string</returns>
        /// <exception cref="InvalidOperationException">Thrown when key cannot be retrieved</exception>
        Task<string> GetEncryptionKeyAsync();

        /// <summary>
        /// Gets the provider type for logging and diagnostics
        /// </summary>
        string ProviderType { get; }

        /// <summary>
        /// Validates that the provider is properly configured
        /// </summary>
        /// <returns>True if provider is ready, false otherwise</returns>
        Task<bool> ValidateConfigurationAsync();
    }
}

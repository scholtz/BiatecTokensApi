using BiatecTokensApi.Models.TokenStandards;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for validating token metadata against standards
    /// </summary>
    public interface ITokenStandardValidator
    {
        /// <summary>
        /// Validates token metadata against a specified standard profile
        /// </summary>
        /// <param name="standard">The token standard to validate against</param>
        /// <param name="metadata">The metadata object to validate</param>
        /// <param name="tokenName">Optional token name for context</param>
        /// <param name="tokenSymbol">Optional token symbol for context</param>
        /// <param name="decimals">Optional decimals for context</param>
        /// <returns>Validation result with errors and warnings</returns>
        Task<TokenValidationResult> ValidateAsync(
            TokenStandard standard,
            object? metadata,
            string? tokenName = null,
            string? tokenSymbol = null,
            int? decimals = null);

        /// <summary>
        /// Validates that required fields are present
        /// </summary>
        /// <param name="profile">The standard profile to validate against</param>
        /// <param name="metadata">The metadata object to validate</param>
        /// <returns>List of validation errors for missing required fields</returns>
        Task<List<ValidationError>> ValidateRequiredFieldsAsync(
            TokenStandardProfile profile,
            object? metadata);

        /// <summary>
        /// Validates field types and constraints
        /// </summary>
        /// <param name="profile">The standard profile to validate against</param>
        /// <param name="metadata">The metadata object to validate</param>
        /// <returns>List of validation errors for field type mismatches</returns>
        Task<List<ValidationError>> ValidateFieldTypesAsync(
            TokenStandardProfile profile,
            object? metadata);

        /// <summary>
        /// Checks if the validator supports a given standard
        /// </summary>
        /// <param name="standard">The token standard to check</param>
        /// <returns>True if supported, false otherwise</returns>
        bool SupportsStandard(TokenStandard standard);
    }
}

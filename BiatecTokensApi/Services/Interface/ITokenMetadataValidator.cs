using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC200;
using BiatecTokensApi.Models.ERC20;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for validating token metadata across different standards
    /// </summary>
    public interface ITokenMetadataValidator
    {
        /// <summary>
        /// Validates ARC3 token metadata
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>Validation result with warnings and errors</returns>
        TokenMetadataValidationResult ValidateARC3Metadata(object metadata);

        /// <summary>
        /// Validates ARC200 token metadata
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>Validation result with warnings and errors</returns>
        TokenMetadataValidationResult ValidateARC200Metadata(object metadata);

        /// <summary>
        /// Validates ERC20 token metadata
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>Validation result with warnings and errors</returns>
        TokenMetadataValidationResult ValidateERC20Metadata(object metadata);

        /// <summary>
        /// Validates ERC721 token metadata
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>Validation result with warnings and errors</returns>
        TokenMetadataValidationResult ValidateERC721Metadata(object metadata);

        /// <summary>
        /// Normalizes metadata with deterministic defaults for missing fields
        /// </summary>
        /// <param name="metadata">Raw metadata</param>
        /// <param name="standard">Token standard</param>
        /// <returns>Normalized metadata with default values applied</returns>
        NormalizedMetadata NormalizeMetadata(object metadata, string standard);

        /// <summary>
        /// Validates decimal precision for a token amount
        /// </summary>
        /// <param name="amount">Amount to validate</param>
        /// <param name="decimals">Token decimals</param>
        /// <returns>Validation result</returns>
        DecimalValidationResult ValidateDecimalPrecision(decimal amount, int decimals);

        /// <summary>
        /// Converts raw token balance to display balance
        /// </summary>
        /// <param name="rawBalance">Raw balance in smallest unit</param>
        /// <param name="decimals">Token decimals</param>
        /// <returns>Display balance with proper decimal places</returns>
        decimal ConvertRawToDisplayBalance(string rawBalance, int decimals);

        /// <summary>
        /// Converts display balance to raw balance
        /// </summary>
        /// <param name="displayBalance">Display balance</param>
        /// <param name="decimals">Token decimals</param>
        /// <returns>Raw balance in smallest unit</returns>
        string ConvertDisplayToRawBalance(decimal displayBalance, int decimals);
    }

    /// <summary>
    /// Result of token metadata validation
    /// </summary>
    public class TokenMetadataValidationResult
    {
        /// <summary>
        /// Whether metadata is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<TokenValidationIssue> Errors { get; set; } = new();

        /// <summary>
        /// List of validation warnings
        /// </summary>
        public List<TokenValidationIssue> Warnings { get; set; } = new();

        /// <summary>
        /// Fields that were missing and assigned defaults
        /// </summary>
        public List<string> DefaultedFields { get; set; } = new();

        /// <summary>
        /// Fields that were malformed and corrected
        /// </summary>
        public List<string> CorrectedFields { get; set; } = new();

        /// <summary>
        /// Overall validation summary
        /// </summary>
        public string? Summary { get; set; }
    }

    /// <summary>
    /// Represents a validation issue (error or warning)
    /// </summary>
    public class TokenValidationIssue
    {
        /// <summary>
        /// Field name where issue occurred
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Issue description
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Issue severity
        /// </summary>
        public string Severity { get; set; } = "Error";

        /// <summary>
        /// Suggested fix
        /// </summary>
        public string? SuggestedFix { get; set; }

        /// <summary>
        /// Expected value or format
        /// </summary>
        public string? ExpectedFormat { get; set; }

        /// <summary>
        /// Actual value received
        /// </summary>
        public string? ActualValue { get; set; }
    }

    /// <summary>
    /// Normalized metadata with defaults applied
    /// </summary>
    public class NormalizedMetadata
    {
        /// <summary>
        /// Token standard
        /// </summary>
        public string Standard { get; set; } = string.Empty;

        /// <summary>
        /// Normalized metadata object
        /// </summary>
        public object Metadata { get; set; } = new();

        /// <summary>
        /// Whether any defaults were applied
        /// </summary>
        public bool HasDefaults { get; set; }

        /// <summary>
        /// List of fields that received default values
        /// </summary>
        public List<string> DefaultedFields { get; set; } = new();

        /// <summary>
        /// Warning signals for frontend display
        /// </summary>
        public List<string> WarningSignals { get; set; } = new();
    }

    /// <summary>
    /// Result of decimal precision validation
    /// </summary>
    public class DecimalValidationResult
    {
        /// <summary>
        /// Whether precision is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if invalid
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Maximum allowed precision
        /// </summary>
        public int MaxPrecision { get; set; }

        /// <summary>
        /// Actual precision used
        /// </summary>
        public int ActualPrecision { get; set; }

        /// <summary>
        /// Whether precision loss will occur
        /// </summary>
        public bool HasPrecisionLoss { get; set; }

        /// <summary>
        /// Recommended value with valid precision
        /// </summary>
        public decimal? RecommendedValue { get; set; }
    }
}

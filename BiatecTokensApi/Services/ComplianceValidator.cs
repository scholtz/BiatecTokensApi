using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for validating compliance metadata on token deployments
    /// </summary>
    /// <remarks>
    /// Validates that RWA tokens have required compliance fields,
    /// while allowing utility tokens to optionally include compliance metadata.
    /// </remarks>
    public class ComplianceValidator
    {
        /// <summary>
        /// Default compliance status for newly deployed tokens
        /// </summary>
        public const ComplianceStatus DefaultComplianceStatus = ComplianceStatus.UnderReview;

        /// <summary>
        /// Default verification status for newly deployed tokens
        /// </summary>
        public const VerificationStatus DefaultVerificationStatus = VerificationStatus.Pending;

        /// <summary>
        /// Validates compliance metadata for a token deployment based on asset type
        /// </summary>
        /// <param name="metadata">The compliance metadata to validate</param>
        /// <param name="isRwaToken">Whether this is an RWA token (requires compliance metadata)</param>
        /// <param name="errors">List of validation errors (output parameter)</param>
        /// <returns>True if validation passes, false otherwise</returns>
        public static bool ValidateComplianceMetadata(
            TokenDeploymentComplianceMetadata? metadata, 
            bool isRwaToken, 
            out List<string> errors)
        {
            errors = new List<string>();

            // If it's a utility token, compliance metadata is optional
            if (!isRwaToken)
            {
                return true;
            }

            // For RWA tokens, compliance metadata is required
            if (metadata == null)
            {
                errors.Add("Compliance metadata is required for RWA tokens. Please provide issuer details, jurisdiction, and regulatory framework.");
                return false;
            }

            // Validate required fields for RWA tokens
            if (string.IsNullOrWhiteSpace(metadata.IssuerName))
            {
                errors.Add("IssuerName is required for RWA tokens.");
            }

            if (string.IsNullOrWhiteSpace(metadata.Jurisdiction))
            {
                errors.Add("Jurisdiction is required for RWA tokens. Please specify the jurisdictions where the token is compliant (e.g., 'US,EU,GB').");
            }

            if (string.IsNullOrWhiteSpace(metadata.AssetType))
            {
                errors.Add("AssetType is required for RWA tokens. Please specify the type of asset being tokenized (e.g., 'Security Token', 'Real Estate').");
            }

            if (string.IsNullOrWhiteSpace(metadata.RegulatoryFramework))
            {
                errors.Add("RegulatoryFramework is required for RWA tokens. Please specify the regulatory framework(s) the token complies with (e.g., 'SEC Reg D', 'MiFID II', 'MICA').");
            }

            if (string.IsNullOrWhiteSpace(metadata.DisclosureUrl))
            {
                errors.Add("DisclosureUrl is required for RWA tokens. Please provide a URL to regulatory disclosure documents.");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Determines if a token is an RWA token based on the compliance metadata
        /// </summary>
        /// <param name="metadata">The compliance metadata</param>
        /// <returns>True if the token should be treated as RWA, false for utility tokens</returns>
        public static bool IsRwaToken(TokenDeploymentComplianceMetadata? metadata)
        {
            if (metadata == null)
            {
                return false; // No metadata means utility token
            }

            // If any RWA-specific fields are set, treat as RWA token
            return !string.IsNullOrWhiteSpace(metadata.IssuerName) ||
                   !string.IsNullOrWhiteSpace(metadata.RegulatoryFramework) ||
                   metadata.RequiresAccreditedInvestors ||
                   metadata.RequiresWhitelist ||
                   metadata.MaxHolders.HasValue;
        }
    }
}

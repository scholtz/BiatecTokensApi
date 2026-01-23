namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents simplified compliance indicators for a token, designed for frontend display
    /// </summary>
    /// <remarks>
    /// This model provides enterprise readiness indicators and compliance flags that enable
    /// the frontend to quickly assess a token's regulatory status and controls.
    /// </remarks>
    public class TokenComplianceIndicators
    {
        /// <summary>
        /// The asset ID (token ID) for which these indicators apply
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Indicates if the token meets MICA (Markets in Crypto-Assets) regulatory requirements
        /// </summary>
        /// <remarks>
        /// MICA readiness is determined by:
        /// - Having compliance metadata configured
        /// - ComplianceStatus being 'Compliant' or 'Exempt'
        /// - Having a specified regulatory framework
        /// - Having jurisdiction information
        /// </remarks>
        public bool IsMicaReady { get; set; }

        /// <summary>
        /// Indicates if whitelisting controls are enabled for this token
        /// </summary>
        /// <remarks>
        /// True if there are any whitelist entries configured for the token
        /// </remarks>
        public bool WhitelistingEnabled { get; set; }

        /// <summary>
        /// Number of addresses currently whitelisted for this token
        /// </summary>
        public int WhitelistedAddressCount { get; set; }

        /// <summary>
        /// Indicates if the token has transfer restrictions
        /// </summary>
        /// <remarks>
        /// True if transfer restrictions are specified in compliance metadata
        /// </remarks>
        public bool HasTransferRestrictions { get; set; }

        /// <summary>
        /// Description of transfer restrictions, if any
        /// </summary>
        public string? TransferRestrictions { get; set; }

        /// <summary>
        /// Indicates if the token requires accredited investors only
        /// </summary>
        public bool RequiresAccreditedInvestors { get; set; }

        /// <summary>
        /// Compliance status of the token
        /// </summary>
        public string? ComplianceStatus { get; set; }

        /// <summary>
        /// KYC verification status
        /// </summary>
        public string? VerificationStatus { get; set; }

        /// <summary>
        /// Regulatory framework(s) the token complies with
        /// </summary>
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// Jurisdiction(s) where the token is compliant
        /// </summary>
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Maximum number of token holders allowed
        /// </summary>
        public int? MaxHolders { get; set; }

        /// <summary>
        /// Overall enterprise readiness score (0-100)
        /// </summary>
        /// <remarks>
        /// Calculated based on:
        /// - Compliance metadata presence (30 points)
        /// - Whitelist controls (25 points)
        /// - KYC verification (20 points)
        /// - Regulatory framework specified (15 points)
        /// - Jurisdiction specified (10 points)
        /// </remarks>
        public int EnterpriseReadinessScore { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Indicates if compliance metadata exists for this token
        /// </summary>
        public bool HasComplianceMetadata { get; set; }

        /// <summary>
        /// Date when compliance metadata was last updated
        /// </summary>
        public DateTime? LastComplianceUpdate { get; set; }
    }

    /// <summary>
    /// Response model for token compliance indicators endpoint
    /// </summary>
    public class TokenComplianceIndicatorsResponse : BaseResponse
    {
        /// <summary>
        /// The compliance indicators for the token
        /// </summary>
        public TokenComplianceIndicators? Indicators { get; set; }
    }
}

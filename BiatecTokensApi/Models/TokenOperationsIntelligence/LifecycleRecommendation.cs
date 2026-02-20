namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// A lifecycle recommendation with reason code and rationale
    /// </summary>
    public class LifecycleRecommendation
    {
        /// <summary>
        /// Unique machine-readable reason code for this recommendation (e.g., "MINT_AUTHORITY_UNREVOKED")
        /// </summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>
        /// Short title of the recommendation
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// User-facing rationale explaining why this recommendation is relevant
        /// </summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>
        /// Suggested action to resolve or act on this recommendation
        /// </summary>
        public string SuggestedAction { get; set; } = string.Empty;

        /// <summary>
        /// Priority of this recommendation (higher = more urgent)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Severity associated with this recommendation
        /// </summary>
        public AssessmentSeverity Severity { get; set; }

        /// <summary>
        /// Related policy dimension (if applicable)
        /// </summary>
        public string? RelatedDimension { get; set; }

        /// <summary>
        /// Documentation URL for further reading (optional)
        /// </summary>
        public string? DocumentationUrl { get; set; }
    }
}

namespace BiatecTokensApi.Models.Entitlement
{
    /// <summary>
    /// Recommendation for subscription upgrade when operation is denied due to plan constraints
    /// </summary>
    public class UpgradeRecommendation
    {
        /// <summary>
        /// Current subscription tier
        /// </summary>
        public string CurrentTier { get; set; } = string.Empty;

        /// <summary>
        /// Recommended tier to upgrade to
        /// </summary>
        public string RecommendedTier { get; set; } = string.Empty;

        /// <summary>
        /// Features unlocked by upgrading
        /// </summary>
        public List<string> UnlockedFeatures { get; set; } = new();

        /// <summary>
        /// Limit increases by upgrading
        /// </summary>
        public Dictionary<string, string> LimitIncreases { get; set; } = new();

        /// <summary>
        /// Message explaining the upgrade benefit
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// URL to upgrade page (if available)
        /// </summary>
        public string? UpgradeUrl { get; set; }

        /// <summary>
        /// Estimated monthly cost increase (informational)
        /// </summary>
        public string? CostIncrease { get; set; }
    }
}

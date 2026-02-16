using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Subscription;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for evaluating user entitlements against subscription policies
    /// </summary>
    public interface IEntitlementEvaluationService
    {
        /// <summary>
        /// Checks if a user is entitled to perform a specific operation
        /// </summary>
        /// <param name="request">Entitlement check request</param>
        /// <returns>Entitlement check result with allow/deny decision and recommendations</returns>
        Task<EntitlementCheckResult> CheckEntitlementAsync(EntitlementCheckRequest request);

        /// <summary>
        /// Gets the current policy version being used
        /// </summary>
        /// <returns>Active policy version</returns>
        Task<EntitlementPolicyVersion> GetActivePolicyVersionAsync();

        /// <summary>
        /// Gets upgrade recommendation for a denied operation
        /// </summary>
        /// <param name="currentTier">User's current tier</param>
        /// <param name="operation">Operation that was denied</param>
        /// <returns>Upgrade recommendation</returns>
        Task<UpgradeRecommendation> GetUpgradeRecommendationAsync(SubscriptionTier currentTier, EntitlementOperation operation);

        /// <summary>
        /// Records an entitlement decision for audit purposes
        /// </summary>
        /// <param name="result">Entitlement check result</param>
        /// <param name="correlationId">Correlation ID for tracking</param>
        Task RecordEntitlementDecisionAsync(EntitlementCheckResult result, string? correlationId);
    }
}

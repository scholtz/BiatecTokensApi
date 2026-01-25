using BiatecTokensApi.Models.Billing;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for billing operations including usage tracking and plan limit enforcement
    /// </summary>
    public interface IBillingService
    {
        /// <summary>
        /// Gets usage summary for a tenant for the current billing period
        /// </summary>
        /// <param name="tenantAddress">The tenant's Algorand address</param>
        /// <returns>Usage summary with current plan limits</returns>
        Task<UsageSummary> GetUsageSummaryAsync(string tenantAddress);

        /// <summary>
        /// Checks if an operation is allowed based on current usage and plan limits (preflight check)
        /// </summary>
        /// <param name="tenantAddress">The tenant's Algorand address</param>
        /// <param name="request">The limit check request</param>
        /// <returns>Limit check result indicating if operation is allowed</returns>
        Task<LimitCheckResponse> CheckLimitAsync(string tenantAddress, LimitCheckRequest request);

        /// <summary>
        /// Updates plan limits for a tenant (admin only)
        /// </summary>
        /// <param name="request">The update request with new limits</param>
        /// <param name="performedBy">Address of the admin performing the update</param>
        /// <returns>Success status and updated limits</returns>
        Task<PlanLimitsResponse> UpdatePlanLimitsAsync(UpdatePlanLimitsRequest request, string performedBy);

        /// <summary>
        /// Gets current plan limits for a tenant
        /// </summary>
        /// <param name="tenantAddress">The tenant's Algorand address</param>
        /// <returns>Current plan limits</returns>
        Task<PlanLimits> GetPlanLimitsAsync(string tenantAddress);

        /// <summary>
        /// Checks if an address is an admin
        /// </summary>
        /// <param name="address">The address to check</param>
        /// <returns>True if the address is an admin</returns>
        bool IsAdmin(string address);

        /// <summary>
        /// Records usage for billing purposes (called when operations are performed)
        /// </summary>
        /// <param name="tenantAddress">The tenant's Algorand address</param>
        /// <param name="operationType">Type of operation performed</param>
        /// <param name="count">Number of operations performed</param>
        Task RecordUsageAsync(string tenantAddress, OperationType operationType, int count = 1);
    }
}

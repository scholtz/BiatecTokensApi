using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for evaluating compliance policies
    /// </summary>
    public interface IPolicyEvaluator
    {
        /// <summary>
        /// Evaluates compliance policies for a given context
        /// </summary>
        /// <param name="context">The evaluation context</param>
        /// <returns>Policy evaluation result</returns>
        Task<PolicyEvaluationResult> EvaluateAsync(PolicyEvaluationContext context);

        /// <summary>
        /// Gets the policy rules applicable to a specific step
        /// </summary>
        /// <param name="step">The onboarding step</param>
        /// <returns>List of applicable policy rules</returns>
        Task<List<PolicyRule>> GetApplicableRulesAsync(OnboardingStep step);

        /// <summary>
        /// Gets the current policy configuration
        /// </summary>
        /// <returns>Policy configuration</returns>
        Task<PolicyConfiguration> GetPolicyConfigurationAsync();

        /// <summary>
        /// Gets policy metrics for monitoring
        /// </summary>
        /// <returns>Policy metrics</returns>
        Task<PolicyMetrics> GetMetricsAsync();
    }
}

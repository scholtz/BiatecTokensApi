using BiatecTokensApi.Models.ComplianceOrchestration;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for AML (Anti-Money Laundering / sanctions screening) provider implementations.
    /// </summary>
    public interface IAmlProvider
    {
        /// <summary>
        /// Gets the human-readable name of this provider implementation.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Screens a subject for AML / sanctions list matches.
        /// </summary>
        /// <param name="subjectId">The subject identifier (e.g. user ID or wallet address).</param>
        /// <param name="subjectMetadata">Additional metadata about the subject.</param>
        /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>providerReferenceId</c> – opaque reference from the AML provider.</description></item>
        ///   <item><description><c>state</c> – normalized decision state.</description></item>
        ///   <item><description><c>reasonCode</c> – optional reason code returned by the provider.</description></item>
        ///   <item><description><c>errorMessage</c> – error detail when state is <see cref="ComplianceDecisionState.Error"/>.</description></item>
        /// </list>
        /// </returns>
        Task<(string providerReferenceId, ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
            ScreenSubjectAsync(
                string subjectId,
                Dictionary<string, string> subjectMetadata,
                string correlationId);

        /// <summary>
        /// Fetches the current screening status from the provider for a previously initiated check.
        /// </summary>
        /// <param name="providerReferenceId">The provider reference ID returned by <see cref="ScreenSubjectAsync"/>.</param>
        /// <returns>
        /// A tuple containing the current state, optional reason code, and optional error message.
        /// </returns>
        Task<(ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
            GetScreeningStatusAsync(string providerReferenceId);
    }
}

using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Mock AML provider for testing and development.
    /// Behaviour is controlled via the <c>subjectMetadata</c> dictionary:
    /// <list type="bullet">
    ///   <item><c>sanctions_flag=true</c>         → Rejected (reason: SANCTIONS_MATCH)</item>
    ///   <item><c>review_flag=true</c>             → NeedsReview (reason: REVIEW_REQUIRED)</item>
    ///   <item><c>simulate_timeout=true</c>        → Error / Timeout</item>
    ///   <item><c>simulate_unavailable=true</c>    → ProviderUnavailable (fail-closed)</item>
    ///   <item><c>simulate_malformed=true</c>      → Error / MalformedResponse</item>
    ///   <item><c>simulate_insufficient_data=true</c> → InsufficientData</item>
    ///   <item>(none of the above)                 → Approved</item>
    /// </list>
    /// </summary>
    public class MockAmlProvider : IAmlProvider
    {
        private readonly ILogger<MockAmlProvider> _logger;

        public MockAmlProvider(ILogger<MockAmlProvider> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public string ProviderName => "MockAmlProvider";

        /// <inheritdoc/>
        public Task<(string providerReferenceId, ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
            ScreenSubjectAsync(
                string subjectId,
                Dictionary<string, string> subjectMetadata,
                string correlationId)
        {
            var refId = $"AML-MOCK-{Guid.NewGuid():N}";

            _logger.LogInformation(
                "MockAmlProvider screening subject. SubjectId={SubjectId}, RefId={RefId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(refId),
                LoggingHelper.SanitizeLogInput(correlationId));

            (ComplianceDecisionState state, string? reasonCode, string? errorMessage) result;

            if (GetFlag(subjectMetadata, "simulate_timeout"))
            {
                _logger.LogWarning("MockAmlProvider simulating timeout for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
                result = (ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "Simulated provider timeout");
                // errorMessage carries the error detail; we still return a refId for traceability
                return Task.FromResult((refId, result.state, result.reasonCode, result.errorMessage));
            }

            if (GetFlag(subjectMetadata, "simulate_unavailable"))
            {
                _logger.LogWarning("MockAmlProvider simulating unavailability for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
                result = (ComplianceDecisionState.ProviderUnavailable, "PROVIDER_UNAVAILABLE", "Simulated provider unavailable");
                return Task.FromResult((refId, result.state, result.reasonCode, result.errorMessage));
            }

            if (GetFlag(subjectMetadata, "simulate_malformed"))
            {
                _logger.LogWarning("MockAmlProvider simulating malformed response for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
                result = (ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "Simulated malformed provider response");
                return Task.FromResult((refId, result.state, result.reasonCode, result.errorMessage));
            }

            if (GetFlag(subjectMetadata, "simulate_insufficient_data"))
            {
                _logger.LogWarning("MockAmlProvider simulating insufficient data for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
                result = (ComplianceDecisionState.InsufficientData, "INSUFFICIENT_SUBJECT_DATA", "Required subject metadata is missing or incomplete");
                return Task.FromResult((refId, result.state, result.reasonCode, result.errorMessage));
            }

            if (GetFlag(subjectMetadata, "sanctions_flag"))
            {
                _logger.LogInformation("MockAmlProvider: sanctions_flag set – returning Rejected for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
                return Task.FromResult((refId, ComplianceDecisionState.Rejected, (string?)"SANCTIONS_MATCH", (string?)null));
            }

            if (GetFlag(subjectMetadata, "review_flag"))
            {
                _logger.LogInformation("MockAmlProvider: review_flag set – returning NeedsReview for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
                return Task.FromResult((refId, ComplianceDecisionState.NeedsReview, (string?)"REVIEW_REQUIRED", (string?)null));
            }

            _logger.LogInformation("MockAmlProvider: returning Approved for SubjectId={SubjectId}", LoggingHelper.SanitizeLogInput(subjectId));
            return Task.FromResult((refId, ComplianceDecisionState.Approved, (string?)null, (string?)null));
        }

        /// <inheritdoc/>
        public Task<(ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
            GetScreeningStatusAsync(string providerReferenceId)
        {
            _logger.LogInformation(
                "MockAmlProvider fetching status for ProviderReferenceId={ProviderReferenceId}",
                LoggingHelper.SanitizeLogInput(providerReferenceId));

            // Mock: always return Approved for status polling
            return Task.FromResult((ComplianceDecisionState.Approved, (string?)null, (string?)null));
        }

        private static bool GetFlag(Dictionary<string, string> metadata, string key)
            => metadata.TryGetValue(key, out var val) && string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
    }
}

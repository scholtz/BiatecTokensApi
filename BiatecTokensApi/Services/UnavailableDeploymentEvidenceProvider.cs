using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// A fail-closed evidence provider that always returns <c>null</c>, simulating the
    /// scenario where a live blockchain node is unreachable or no authoritative evidence
    /// can be obtained.
    ///
    /// This provider is used in tests to verify that the
    /// <see cref="TokenDeploymentLifecycleService"/> transitions a deployment to
    /// <see cref="DeploymentStage.Failed"/> with
    /// <see cref="DeploymentOutcome.TerminalFailure"/> and error code
    /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c> when operating in
    /// <see cref="DeploymentExecutionMode.Authoritative"/> mode.
    ///
    /// In production, this would be replaced by a real network-aware provider (e.g.
    /// <c>AlgorandDeploymentEvidenceProvider</c>) that calls the Algorand indexer or algod
    /// node and returns <c>null</c> when the node is unreachable or the transaction has not
    /// been confirmed within the configured timeout.
    /// </summary>
    public sealed class UnavailableDeploymentEvidenceProvider : IDeploymentEvidenceProvider
    {
        /// <inheritdoc/>
        public bool IsSimulated => false;

        /// <inheritdoc/>
        public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
            string deploymentId,
            string tokenStandard,
            string network,
            CancellationToken cancellationToken = default)
        {
            // Always returns null: blockchain evidence is unavailable.
            // When the service is in Authoritative mode this causes a terminal failure.
            return Task.FromResult<BlockchainDeploymentEvidence?>(null);
        }
    }
}

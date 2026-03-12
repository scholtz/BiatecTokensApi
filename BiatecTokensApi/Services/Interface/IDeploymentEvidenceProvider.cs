using BiatecTokensApi.Models.TokenDeploymentLifecycle;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Provides blockchain deployment evidence for a completed token deployment transaction.
    ///
    /// This interface is the key abstraction that separates authoritative production deployments
    /// (where evidence is obtained from a live blockchain node) from simulated test deployments
    /// (where evidence is derived deterministically for contract-shape validation).
    ///
    /// Implementations:
    /// <list type="bullet">
    ///   <item><see cref="BiatecTokensApi.Services.SimulatedDeploymentEvidenceProvider"/> —
    ///     returns deterministic hash-derived evidence; sets <see cref="BlockchainDeploymentEvidence.IsSimulated"/> = <c>true</c>.
    ///     Suitable for development, testing, and non-production environments.</item>
    ///   <item><see cref="BiatecTokensApi.Services.AlgorandDeploymentEvidenceProvider"/> —
    ///     queries a live Algorand indexer node; sets <c>IsSimulated = false</c>.
    ///     Required for production regulated issuance sign-off on Algorand-family networks.
    ///     Enabled by setting <c>DeploymentEvidenceConfig:Provider = "Algorand"</c>.</item>
    /// </list>
    ///
    /// The <see cref="TokenDeploymentLifecycleService"/> calls this provider during the
    /// Submitting/Confirming lifecycle stages. If the provider returns <c>null</c> (meaning
    /// authoritative evidence is unavailable) and the request is in
    /// <see cref="DeploymentExecutionMode.Authoritative"/> mode, the service transitions
    /// the deployment to <see cref="DeploymentStage.Failed"/> with
    /// <see cref="DeploymentOutcome.TerminalFailure"/> and error code
    /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>.
    /// </summary>
    public interface IDeploymentEvidenceProvider
    {
        /// <summary>
        /// Obtains or derives blockchain evidence for the specified deployment.
        /// Returns <c>null</c> when authoritative evidence cannot be obtained (e.g. node
        /// unreachable, timeout, or this provider is intentionally fail-closed for testing).
        /// </summary>
        /// <param name="deploymentId">
        /// The deterministic deployment ID derived from the idempotency key and network.
        /// </param>
        /// <param name="tokenStandard">
        /// Token standard (e.g., "ASA", "ARC3", "ARC200", "ERC20") used to format
        /// standard-specific evidence fields.
        /// </param>
        /// <param name="network">
        /// Target network identifier (e.g., "algorand-testnet", "base-mainnet").
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <see cref="BlockchainDeploymentEvidence"/> when evidence is available;
        /// <c>null</c> when evidence cannot be obtained.
        /// </returns>
        Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
            string deploymentId,
            string tokenStandard,
            string network,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// <c>true</c> when this provider always returns simulated (non-authoritative) evidence.
        /// <c>false</c> when this provider attempts to obtain confirmed blockchain state.
        /// </summary>
        bool IsSimulated { get; }
    }
}

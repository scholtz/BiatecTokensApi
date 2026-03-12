namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for the deployment evidence provider used by
    /// <see cref="BiatecTokensApi.Services.TokenDeploymentLifecycleService"/>.
    ///
    /// <para>
    /// The <see cref="Provider"/> field selects the active implementation:
    /// <list type="bullet">
    ///   <item><term>Simulation</term> — deterministic hash-derived evidence; no blockchain
    ///     connectivity required. Suitable for development and non-production environments.
    ///     Sets <c>IsSimulatedEvidence = true</c> on all responses.</item>
    ///   <item><term>Algorand</term> — live authoritative evidence retrieved from an Algorand
    ///     indexer node. Requires <see cref="Algorand"/> configuration section. Suitable for
    ///     protected staging and production environments. Sets <c>IsSimulatedEvidence = false</c>
    ///     on successfully retrieved responses.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// In <c>Authoritative</c> execution mode, if the configured provider cannot obtain
    /// evidence (node unreachable, timeout, malformed response), the deployment lifecycle
    /// service will return a terminal failure with error code
    /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c> rather than silently degrading to simulation.
    /// </para>
    ///
    /// <para>
    /// Environment-based selection example (appsettings.Production.json):
    /// <code>
    /// "DeploymentEvidenceConfig": {
    ///   "Provider": "Algorand",
    ///   "Algorand": {
    ///     "IndexerUrl": "https://mainnet-idx.algonode.cloud",
    ///     "ApiToken": "",
    ///     "TimeoutSeconds": 15,
    ///     "MaxRetries": 2
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public class DeploymentEvidenceConfig
    {
        /// <summary>
        /// Name of the active evidence provider.
        /// Accepted values: <c>"Simulation"</c> (default), <c>"Algorand"</c>.
        /// </summary>
        public string Provider { get; set; } = "Simulation";

        /// <summary>
        /// Configuration for the Algorand live evidence provider.
        /// Required when <see cref="Provider"/> is <c>"Algorand"</c>.
        /// </summary>
        public AlgorandEvidenceProviderConfig Algorand { get; set; } = new();
    }

    /// <summary>
    /// Configuration for the Algorand live deployment evidence provider.
    ///
    /// <para>
    /// The provider queries an Algorand indexer node to search for an asset-configuration
    /// transaction whose note field starts with the deployment ID (base64-encoded). This
    /// requires that the actual token deployment transaction includes the deployment ID in
    /// its note field — which is the established contract between the deployment services
    /// and the evidence retrieval layer.
    /// </para>
    ///
    /// <para>
    /// Supported network identifiers (passed as the <c>network</c> argument to the provider):
    /// <c>algorand-mainnet</c>, <c>algorand-testnet</c>, <c>algorand-betanet</c>,
    /// <c>voi-mainnet</c>, <c>aramid-mainnet</c>.
    /// </para>
    ///
    /// <para>
    /// <strong>Business guarantees:</strong>
    /// <list type="bullet">
    ///   <item>Evidence obtained from a healthy indexer node represents confirmed on-chain
    ///     state; <c>IsSimulated</c> is always <c>false</c>.</item>
    ///   <item>If the indexer is unreachable, returns a non-HTTP-2xx status, or times out,
    ///     the provider returns <c>null</c> (fail-closed). In Authoritative execution mode
    ///     this causes a terminal failure with <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>.</item>
    ///   <item>If the indexer responds but the transaction has not yet been indexed, the
    ///     provider returns <c>null</c> (evidence pending); the caller should retry.</item>
    ///   <item>Malformed indexer responses are treated as unavailable evidence.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <strong>Operational requirements:</strong>
    /// <list type="bullet">
    ///   <item>Set <see cref="IndexerUrl"/> to the base URL of a reachable Algorand indexer node.
    ///     Public endpoints: testnet: <c>https://testnet-idx.algonode.cloud</c>;
    ///     mainnet: <c>https://mainnet-idx.algonode.cloud</c>.</item>
    ///   <item>For authenticated private nodes, set <see cref="ApiToken"/> to the
    ///     <c>X-Indexer-API-Token</c> value.</item>
    ///   <item>Tune <see cref="TimeoutSeconds"/> and <see cref="MaxRetries"/> based on
    ///     expected indexer latency. Defaults are conservative for public endpoints.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class AlgorandEvidenceProviderConfig
    {
        /// <summary>
        /// Base URL of the Algorand indexer node, without trailing slash.
        /// Example: <c>https://testnet-idx.algonode.cloud</c>
        /// </summary>
        public string IndexerUrl { get; set; } = string.Empty;

        /// <summary>
        /// API token for authenticated indexer nodes (optional).
        /// Sent as the <c>X-Indexer-API-Token</c> HTTP header when non-empty.
        /// </summary>
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Per-request HTTP timeout in seconds. Defaults to 15.
        /// The provider cancels the request and returns <c>null</c> on timeout.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Maximum number of retry attempts for transient HTTP failures. Defaults to 2.
        /// Each retry uses an exponential back-off starting at 500 ms.
        /// </summary>
        public int MaxRetries { get; set; } = 2;
    }
}

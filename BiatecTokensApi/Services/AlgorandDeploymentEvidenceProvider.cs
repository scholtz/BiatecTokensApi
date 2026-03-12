using System.Text;
using System.Text.Json;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Retrieves authoritative blockchain deployment evidence from an Algorand indexer node.
    ///
    /// <para>
    /// This provider queries the Algorand indexer REST API to search for an
    /// asset-configuration transaction (<c>tx-type=acfg</c>) whose <c>note</c> field begins
    /// with the deployment ID (UTF-8 bytes, base64-encoded). This requires the actual token
    /// deployment transaction to embed the deployment ID in its note — which is the
    /// established contract between the Algorand token services and the evidence layer.
    /// </para>
    ///
    /// <para>
    /// When evidence is found and the transaction has been confirmed on-chain,
    /// <see cref="BlockchainDeploymentEvidence.IsSimulated"/> is set to <c>false</c> and
    /// <see cref="BlockchainDeploymentEvidence.EvidenceSource"/> is set to
    /// <c>"algorand-indexer"</c>.
    /// </para>
    ///
    /// <para>
    /// Fail-closed semantics: the provider returns <c>null</c> — never simulated data — in
    /// all of the following scenarios:
    /// <list type="bullet">
    ///   <item>The indexer node is unreachable (<see cref="HttpRequestException"/>).</item>
    ///   <item>The request times out (controlled by
    ///     <see cref="AlgorandEvidenceProviderConfig.TimeoutSeconds"/>).</item>
    ///   <item>The indexer returns a non-HTTP-2xx status code.</item>
    ///   <item>The indexer response cannot be parsed as valid JSON.</item>
    ///   <item>The response contains no matching confirmed transaction.</item>
    ///   <item>The network is not an Algorand-family network (e.g., EVM chains).</item>
    ///   <item>The <see cref="AlgorandEvidenceProviderConfig.IndexerUrl"/> is not configured.</item>
    /// </list>
    /// In <see cref="DeploymentExecutionMode.Authoritative"/> mode, a <c>null</c> return
    /// from this provider causes the lifecycle service to transition the deployment to
    /// <see cref="DeploymentStage.Failed"/> with error code
    /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>.
    /// </para>
    ///
    /// <para>
    /// Supported Algorand-family networks: <c>algorand-mainnet</c>, <c>algorand-testnet</c>,
    /// <c>algorand-betanet</c>, <c>voi-mainnet</c>, <c>aramid-mainnet</c>.
    /// </para>
    /// </summary>
    public sealed class AlgorandDeploymentEvidenceProvider : IDeploymentEvidenceProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AlgorandEvidenceProviderConfig _config;
        private readonly ILogger<AlgorandDeploymentEvidenceProvider> _logger;

        private static readonly HashSet<string> AlgorandNetworks =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "algorand-mainnet",
                "algorand-testnet",
                "algorand-betanet",
                "voi-mainnet",
                "aramid-mainnet",
            };

        /// <summary>
        /// Initialises the provider with the configured HTTP client factory and options.
        /// </summary>
        public AlgorandDeploymentEvidenceProvider(
            IHttpClientFactory httpClientFactory,
            IOptions<DeploymentEvidenceConfig> options,
            ILogger<AlgorandDeploymentEvidenceProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = options.Value.Algorand;
            _logger = logger;
        }

        /// <summary>
        /// Internal constructor for unit testing — accepts a pre-built config directly,
        /// avoiding IOptions.
        /// </summary>
        internal AlgorandDeploymentEvidenceProvider(
            IHttpClientFactory httpClientFactory,
            AlgorandEvidenceProviderConfig config,
            ILogger<AlgorandDeploymentEvidenceProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Always <c>false</c> — this provider retrieves confirmed on-chain state,
        /// not deterministic simulations.
        /// </remarks>
        public bool IsSimulated => false;

        /// <inheritdoc/>
        public async Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
            string deploymentId,
            string tokenStandard,
            string network,
            CancellationToken cancellationToken = default)
        {
            if (!AlgorandNetworks.Contains(network))
            {
                _logger.LogWarning(
                    "AlgorandDeploymentEvidenceProvider: network {Network} is not an " +
                    "Algorand-family network; returning null (cannot retrieve Algorand evidence).",
                    LoggingHelper.SanitizeLogInput(network));
                return null;
            }

            if (string.IsNullOrWhiteSpace(_config.IndexerUrl))
            {
                _logger.LogWarning(
                    "AlgorandDeploymentEvidenceProvider: IndexerUrl is not configured; " +
                    "returning null. Set DeploymentEvidenceConfig:Algorand:IndexerUrl in " +
                    "the application configuration to enable authoritative evidence retrieval.");
                return null;
            }

            // The note-prefix query searches for transactions whose note starts with the
            // deployment ID bytes. Note that Algorand expects the note-prefix as a
            // base64-encoded byte string (standard encoding, no padding trimming needed).
            var noteBytes = Encoding.UTF8.GetBytes(deploymentId);
            var notePrefix = Convert.ToBase64String(noteBytes);
            var requestUrl = BuildIndexerUrl(notePrefix);

            var sanitisedDeploymentId = LoggingHelper.SanitizeLogInput(deploymentId);

            _logger.LogInformation(
                "AlgorandDeploymentEvidenceProvider: querying indexer for deployment " +
                "{DeploymentId} on network {Network}.",
                sanitisedDeploymentId,
                LoggingHelper.SanitizeLogInput(network));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds > 0
                ? _config.TimeoutSeconds : 15));

            int attempts = 0;
            int maxAttempts = Math.Max(1, _config.MaxRetries + 1);

            while (attempts < maxAttempts)
            {
                attempts++;
                try
                {
                    var client = _httpClientFactory.CreateClient("AlgorandIndexer");
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                    if (!string.IsNullOrWhiteSpace(_config.ApiToken))
                    {
                        request.Headers.Add("X-Indexer-API-Token", _config.ApiToken);
                    }

                    using var response = await client.SendAsync(request, cts.Token)
                        .ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "AlgorandDeploymentEvidenceProvider: indexer returned HTTP {StatusCode} " +
                            "for deployment {DeploymentId} (attempt {Attempt}/{Max}).",
                            (int)response.StatusCode,
                            sanitisedDeploymentId,
                            attempts,
                            maxAttempts);

                        if (attempts >= maxAttempts) return null;
                        await DelayBeforeRetry(attempts, cts.Token).ConfigureAwait(false);
                        continue;
                    }

                    var json = await response.Content
                        .ReadAsStringAsync(cts.Token)
                        .ConfigureAwait(false);

                    var evidence = ParseIndexerResponse(json, deploymentId);

                    if (evidence != null)
                    {
                        _logger.LogInformation(
                            "AlgorandDeploymentEvidenceProvider: authoritative evidence obtained " +
                            "for deployment {DeploymentId}: assetId={AssetId}, txId={TxId}, " +
                            "round={Round}.",
                            sanitisedDeploymentId,
                            evidence.AssetId,
                            LoggingHelper.SanitizeLogInput(evidence.TransactionId),
                            evidence.ConfirmedRound);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "AlgorandDeploymentEvidenceProvider: no confirmed transaction found " +
                            "for deployment {DeploymentId} (evidence pending or not yet indexed).",
                            sanitisedDeploymentId);
                    }

                    return evidence;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(
                        "AlgorandDeploymentEvidenceProvider: request timed out or was cancelled " +
                        "for deployment {DeploymentId} (attempt {Attempt}/{Max}). " +
                        "Increase DeploymentEvidenceConfig:Algorand:TimeoutSeconds if the node " +
                        "is reachable but slow.",
                        sanitisedDeploymentId,
                        attempts,
                        maxAttempts);
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(
                        "AlgorandDeploymentEvidenceProvider: HTTP request failed for deployment " +
                        "{DeploymentId} (attempt {Attempt}/{Max}): {Error}. Verify that " +
                        "DeploymentEvidenceConfig:Algorand:IndexerUrl is reachable from this host.",
                        sanitisedDeploymentId,
                        attempts,
                        maxAttempts,
                        LoggingHelper.SanitizeLogInput(ex.Message));

                    if (attempts >= maxAttempts) return null;
                    await DelayBeforeRetry(attempts, cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "AlgorandDeploymentEvidenceProvider: unexpected error for deployment " +
                        "{DeploymentId}: {Error}.",
                        sanitisedDeploymentId,
                        LoggingHelper.SanitizeLogInput(ex.Message));
                    return null;
                }
            }

            return null;
        }

        // ─── Private helpers ────────────────────────────────────────────────────────

        private string BuildIndexerUrl(string notePrefix)
        {
            var baseUrl = _config.IndexerUrl.TrimEnd('/');
            // note-prefix must be URL-encoded because it may contain +, /, = characters.
            return $"{baseUrl}/v2/transactions?note-prefix={Uri.EscapeDataString(notePrefix)}&tx-type=acfg";
        }

        /// <summary>
        /// Parses the Algorand indexer JSON response and returns the first confirmed
        /// asset-creation transaction found, or <c>null</c> if none is present.
        ///
        /// Expected response shape:
        /// <code>
        /// {
        ///   "current-round": 12345,
        ///   "transactions": [
        ///     {
        ///       "id": "TXID...",
        ///       "confirmed-round": 12345,
        ///       "created-asset-index": 123456,
        ///       "tx-type": "acfg",
        ///       "asset-config-transaction": { ... }
        ///     }
        ///   ]
        /// }
        /// </code>
        /// </summary>
        internal static BlockchainDeploymentEvidence? ParseIndexerResponse(
            string json,
            string deploymentId)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("transactions", out var transactions)) return null;

                foreach (var tx in transactions.EnumerateArray())
                {
                    // Transaction must be confirmed (has a confirmed-round).
                    if (!tx.TryGetProperty("confirmed-round", out var roundProp)) continue;
                    var confirmedRound = roundProp.ValueKind == JsonValueKind.Number
                        ? roundProp.GetUInt64()
                        : 0UL;
                    if (confirmedRound == 0) continue;

                    // Asset-creation transactions have created-asset-index > 0.
                    if (!tx.TryGetProperty("created-asset-index", out var assetProp)) continue;
                    var assetId = assetProp.ValueKind == JsonValueKind.Number
                        ? assetProp.GetUInt64()
                        : 0UL;
                    if (assetId == 0) continue;

                    // Transaction ID (base32 for Algorand).
                    if (!tx.TryGetProperty("id", out var idProp)) continue;
                    var txId = idProp.GetString();
                    if (string.IsNullOrEmpty(txId)) continue;

                    return new BlockchainDeploymentEvidence
                    {
                        TransactionId  = txId,
                        AssetId        = assetId,
                        ConfirmedRound = confirmedRound,
                        IsSimulated    = false,
                        EvidenceSource = "algorand-indexer",
                        ObtainedAt     = DateTimeOffset.UtcNow,
                    };
                }

                return null; // No confirmed asset-creation transaction found.
            }
            catch (JsonException)
            {
                // Malformed indexer response — treated as unavailable evidence.
                return null;
            }
        }

        private static Task DelayBeforeRetry(int attempt, CancellationToken ct)
        {
            // Exponential back-off: 500 ms, 1 000 ms, ...
            var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
            return Task.Delay(delay, ct);
        }
    }
}

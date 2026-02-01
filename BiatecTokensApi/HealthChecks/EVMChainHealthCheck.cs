using BiatecTokensApi.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.HealthChecks
{
    /// <summary>
    /// Health check for EVM blockchain RPC connectivity
    /// </summary>
    public class EVMChainHealthCheck : IHealthCheck
    {
        private readonly EVMChains _evmChains;
        private readonly ILogger<EVMChainHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EVMChainHealthCheck"/> class.
        /// </summary>
        /// <param name="evmChains">EVM chains configuration</param>
        /// <param name="logger">Logger instance</param>
        public EVMChainHealthCheck(
            IOptions<EVMChains> evmChains,
            ILogger<EVMChainHealthCheck> logger)
        {
            _evmChains = evmChains.Value;
            _logger = logger;
        }

        /// <summary>
        /// Checks the health of configured EVM chains
        /// </summary>
        /// <param name="context">Health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_evmChains?.Chains == null || !_evmChains.Chains.Any())
            {
                return HealthCheckResult.Degraded("No EVM chains configured");
            }

            var healthyChains = new List<string>();
            var unhealthyChains = new List<string>();
            var chainDetails = new Dictionary<string, object>();

            foreach (var chain in _evmChains.Chains)
            {
                try
                {
                    if (string.IsNullOrEmpty(chain.RpcUrl))
                    {
                        unhealthyChains.Add($"Chain {chain.ChainId} (no RPC configured)");
                        continue;
                    }

                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    // Send a simple JSON-RPC request to check if the node is responsive
                    var jsonRpcRequest = new
                    {
                        jsonrpc = "2.0",
                        method = "eth_blockNumber",
                        @params = new object[] { },
                        id = 1
                    };

                    var content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(jsonRpcRequest),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await httpClient.PostAsync(chain.RpcUrl, content, linkedCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
                        
                        // Check if response contains valid JSON-RPC structure
                        if (responseBody.Contains("\"result\"") || responseBody.Contains("\"error\""))
                        {
                            healthyChains.Add($"Chain {chain.ChainId}");
                            chainDetails[$"chain_{chain.ChainId}"] = new
                            {
                                status = "healthy",
                                rpcUrl = chain.RpcUrl,
                                chainId = chain.ChainId
                            };
                        }
                        else
                        {
                            unhealthyChains.Add($"Chain {chain.ChainId} (invalid response)");
                            chainDetails[$"chain_{chain.ChainId}"] = new
                            {
                                status = "degraded",
                                rpcUrl = chain.RpcUrl,
                                chainId = chain.ChainId,
                                reason = "Invalid JSON-RPC response"
                            };
                        }
                    }
                    else
                    {
                        unhealthyChains.Add($"Chain {chain.ChainId} ({response.StatusCode})");
                        chainDetails[$"chain_{chain.ChainId}"] = new
                        {
                            status = "degraded",
                            rpcUrl = chain.RpcUrl,
                            chainId = chain.ChainId,
                            statusCode = (int)response.StatusCode
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    unhealthyChains.Add($"Chain {chain.ChainId} (timeout)");
                    chainDetails[$"chain_{chain.ChainId}"] = new
                    {
                        status = "timeout",
                        rpcUrl = chain.RpcUrl ?? "not configured",
                        chainId = chain.ChainId
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health check failed for EVM chain {ChainId}", chain.ChainId);
                    unhealthyChains.Add($"Chain {chain.ChainId} (error)");
                    chainDetails[$"chain_{chain.ChainId}"] = new
                    {
                        status = "error",
                        rpcUrl = chain.RpcUrl ?? "not configured",
                        chainId = chain.ChainId,
                        error = ex.Message
                    };
                }
            }

            var data = new Dictionary<string, object>
            {
                { "totalChains", _evmChains.Chains.Count },
                { "healthyChains", healthyChains.Count },
                { "unhealthyChains", unhealthyChains.Count }
            };

            // Merge chain details
            foreach (var detail in chainDetails)
            {
                data[detail.Key] = detail.Value;
            }

            if (unhealthyChains.Count == _evmChains.Chains.Count)
            {
                return HealthCheckResult.Unhealthy(
                    $"All {unhealthyChains.Count} EVM chains are unreachable",
                    null,
                    data);
            }

            if (unhealthyChains.Any())
            {
                return HealthCheckResult.Degraded(
                    $"{healthyChains.Count}/{_evmChains.Chains.Count} EVM chains are healthy",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy(
                $"All {healthyChains.Count} EVM chains are healthy",
                data);
        }
    }
}

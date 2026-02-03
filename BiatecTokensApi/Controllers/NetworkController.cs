using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for querying supported blockchain networks and their configurations
    /// </summary>
    [ApiController]
    [Route("api/v1/networks")]
    public class NetworkController : ControllerBase
    {
        private readonly IOptions<EVMChains> _evmChainsOptions;
        private readonly IOptions<AlgorandAuthenticationOptionsV2> _algorandOptions;
        private readonly ILogger<NetworkController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkController"/> class.
        /// </summary>
        /// <param name="evmChainsOptions">EVM chains configuration</param>
        /// <param name="algorandOptions">Algorand authentication configuration containing allowed networks</param>
        /// <param name="logger">Logger instance</param>
        public NetworkController(
            IOptions<EVMChains> evmChainsOptions,
            IOptions<AlgorandAuthenticationOptionsV2> algorandOptions,
            ILogger<NetworkController> logger)
        {
            _evmChainsOptions = evmChainsOptions;
            _algorandOptions = algorandOptions;
            _logger = logger;
        }

        /// <summary>
        /// Gets metadata for all supported blockchain networks
        /// </summary>
        /// <returns>Network metadata including mainnet prioritization</returns>
        /// <remarks>
        /// This endpoint provides comprehensive information about supported blockchain networks,
        /// prioritizing mainnet networks for production deployments. It includes both Algorand
        /// and EVM-compatible chains with their respective configurations.
        /// 
        /// **Network Prioritization:**
        /// - Mainnet networks are marked as `isMainnet: true` and `isRecommended: true`
        /// - Testnet networks are available but not recommended for production
        /// - The `recommendedNetworks` array contains only production-ready mainnets
        /// 
        /// **Use Cases:**
        /// - Display available networks in deployment UI
        /// - Validate network selection before deployment
        /// - Filter networks by mainnet/testnet status
        /// - Provide network-specific configuration to wallet integrations
        /// 
        /// **Network Types:**
        /// - Algorand networks: Identified by genesis hash
        /// - EVM networks: Identified by chain ID
        /// 
        /// **Response Format:**
        /// Each network includes:
        /// - Network identifier and display name
        /// - Blockchain type (algorand/evm)
        /// - Mainnet and recommendation flags
        /// - Connection endpoints (RPC URL or API server)
        /// - Chain-specific identifiers (genesis hash or chain ID)
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(NetworkMetadataResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetNetworks()
        {
            try
            {
                var response = new NetworkMetadataResponse();

                // Add Algorand networks from authentication configuration
                if (_algorandOptions.Value.AllowedNetworks != null)
                {
                    foreach (var network in _algorandOptions.Value.AllowedNetworks)
                    {
                        var genesisHash = network.Key;
                        var config = network.Value;

                        // Determine network type from genesis hash or server URL
                        var isMainnet = genesisHash == "wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=" || 
                                       config.Server?.Contains("mainnet", StringComparison.OrdinalIgnoreCase) == true;
                        var isTestnet = genesisHash == "SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=" ||
                                       config.Server?.Contains("testnet", StringComparison.OrdinalIgnoreCase) == true;
                        var isBetanet = config.Server?.Contains("betanet", StringComparison.OrdinalIgnoreCase) == true;
                        var isVoiMain = config.Server?.Contains("voimain", StringComparison.OrdinalIgnoreCase) == true ||
                                       config.Server?.Contains("voi", StringComparison.OrdinalIgnoreCase) == true;
                        var isAramidMain = config.Server?.Contains("aramid", StringComparison.OrdinalIgnoreCase) == true;

                        string networkId, displayName;
                        if (isMainnet)
                        {
                            networkId = "algorand-mainnet";
                            displayName = "Algorand Mainnet";
                        }
                        else if (isTestnet)
                        {
                            networkId = "algorand-testnet";
                            displayName = "Algorand Testnet";
                        }
                        else if (isBetanet)
                        {
                            networkId = "algorand-betanet";
                            displayName = "Algorand Betanet";
                        }
                        else if (isVoiMain)
                        {
                            networkId = "voimain";
                            displayName = "VOI Network Mainnet";
                        }
                        else if (isAramidMain)
                        {
                            networkId = "aramidmain";
                            displayName = "Aramid Network Mainnet";
                        }
                        else
                        {
                            // Unknown network, use genesis hash as identifier
                            networkId = $"algorand-{genesisHash.Substring(0, 8)}";
                            displayName = $"Algorand Network ({genesisHash.Substring(0, 8)})";
                        }

                        var networkMetadata = new NetworkMetadata
                        {
                            NetworkId = networkId,
                            DisplayName = displayName,
                            BlockchainType = "algorand",
                            IsMainnet = isMainnet || isVoiMain || isAramidMain,
                            IsRecommended = isMainnet, // Only recommend official Algorand mainnet
                            EndpointUrl = config.Server,
                            GenesisHash = genesisHash,
                            Properties = new Dictionary<string, object>
                            {
                                { "hasToken", !string.IsNullOrEmpty(config.Token) },
                                { "hasHeader", !string.IsNullOrEmpty(config.Header) }
                            }
                        };

                        response.Networks.Add(networkMetadata);

                        if (networkMetadata.IsRecommended)
                        {
                            response.RecommendedNetworks.Add(networkId);
                        }
                    }
                }

                // Add EVM networks from configuration
                if (_evmChainsOptions.Value.Chains != null)
                {
                    foreach (var chain in _evmChainsOptions.Value.Chains)
                    {
                        if (chain.RpcUrl == null) continue;

                        // Determine if this is a mainnet based on chain ID
                        // Base mainnet: 8453, Ethereum mainnet: 1, etc.
                        var isMainnet = chain.ChainId == 8453 || chain.ChainId == 1;
                        var isTestnet = chain.ChainId == 84532 || chain.ChainId == 5 || chain.ChainId == 11155111;

                        string networkId, displayName;
                        if (chain.ChainId == 8453)
                        {
                            networkId = "base-mainnet";
                            displayName = "Base Mainnet";
                        }
                        else if (chain.ChainId == 84532)
                        {
                            networkId = "base-sepolia";
                            displayName = "Base Sepolia Testnet";
                        }
                        else if (chain.ChainId == 1)
                        {
                            networkId = "ethereum-mainnet";
                            displayName = "Ethereum Mainnet";
                        }
                        else if (chain.ChainId == 5)
                        {
                            networkId = "ethereum-goerli";
                            displayName = "Ethereum Goerli Testnet";
                        }
                        else if (chain.ChainId == 11155111)
                        {
                            networkId = "ethereum-sepolia";
                            displayName = "Ethereum Sepolia Testnet";
                        }
                        else
                        {
                            networkId = $"evm-{chain.ChainId}";
                            displayName = $"EVM Chain {chain.ChainId}";
                        }

                        var networkMetadata = new NetworkMetadata
                        {
                            NetworkId = networkId,
                            DisplayName = displayName,
                            BlockchainType = "evm",
                            IsMainnet = isMainnet,
                            IsRecommended = isMainnet,
                            EndpointUrl = chain.RpcUrl,
                            ChainId = chain.ChainId,
                            Properties = new Dictionary<string, object>
                            {
                                { "gasLimit", chain.GasLimit }
                            }
                        };

                        response.Networks.Add(networkMetadata);

                        if (networkMetadata.IsRecommended)
                        {
                            response.RecommendedNetworks.Add(networkId);
                        }
                    }
                }

                // Sort networks: recommended first, then by display name
                response.Networks = response.Networks
                    .OrderByDescending(n => n.IsRecommended)
                    .ThenBy(n => n.DisplayName)
                    .ToList();

                _logger.LogInformation("Returned {Count} networks, {RecommendedCount} recommended", 
                    response.Networks.Count, response.RecommendedNetworks.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving network metadata");
                return StatusCode(StatusCodes.Status500InternalServerError, new NetworkMetadataResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving network metadata",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
}

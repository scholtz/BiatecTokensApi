using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for computing wallet routing options to optimize cross-network operations.
    /// Provides ordered routing recommendations with cost and time estimates to reduce
    /// wallet connection friction and improve conversion rates for cross-chain token operations.
    /// </summary>
    public class WalletRoutingService : IWalletRoutingService
    {
        private readonly ILogger<WalletRoutingService> _logger;

        private static readonly IReadOnlySet<string> _algorandNetworks =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "algorand-mainnet", "algorand-testnet", "algorand-betanet",
                "voi-mainnet", "aramid-mainnet"
            };

        private static readonly IReadOnlySet<string> _evmNetworks =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "base-mainnet", "ethereum-mainnet"
            };

        /// <summary>
        /// Initializes a new instance of <see cref="WalletRoutingService"/>.
        /// </summary>
        public WalletRoutingService(ILogger<WalletRoutingService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<WalletRoutingResponse> GetRoutingOptionsAsync(WalletRoutingRequest request)
        {
            _logger.LogInformation(
                "Computing wallet routing: {SourceNetwork} -> {TargetNetwork}, Operation={Operation}",
                LoggingHelper.SanitizeLogInput(request.SourceNetwork),
                LoggingHelper.SanitizeLogInput(request.TargetNetwork),
                request.OperationType.ToString());

            var response = new WalletRoutingResponse
            {
                GeneratedAt = DateTime.UtcNow
            };

            bool sourceIsAlgorand = IsAlgorandEcosystem(request.SourceNetwork);
            bool targetIsAlgorand = IsAlgorandEcosystem(request.TargetNetwork);
            bool sourceIsEvm = IsEvmEcosystem(request.SourceNetwork);
            bool targetIsEvm = IsEvmEcosystem(request.TargetNetwork);

            // Same-network case
            bool isSameNetwork = string.Equals(
                request.SourceNetwork, request.TargetNetwork, StringComparison.OrdinalIgnoreCase);

            response.IsSameNetwork = isSameNetwork;

            if (isSameNetwork)
            {
                response.HasDirectRoute = true;
                response.RoutingSummary = "No routing needed - wallet is already on the target network.";
                response.RecommendedRoute = BuildDirectRoute(request);
                response.AvailableRoutes.Add(response.RecommendedRoute);
                return Task.FromResult(response);
            }

            var routes = new List<WalletRoutingOption>();

            // Within Algorand ecosystem (mainnet ↔ testnet ↔ voi etc.)
            if (sourceIsAlgorand && targetIsAlgorand)
            {
                routes.Add(BuildAlgorandNetworkSwitchRoute(request));
            }
            // Within EVM ecosystem (base ↔ ethereum)
            else if (sourceIsEvm && targetIsEvm)
            {
                routes.Add(BuildEvmBridgeRoute(request));
                routes.Add(BuildCexRoute(request));
            }
            // Cross-ecosystem (Algorand ↔ EVM)
            else if (sourceIsAlgorand && targetIsEvm)
            {
                routes.Add(BuildCexRoute(request));
                routes.Add(BuildMultiHopRoute(request, "Algorand → CEX → EVM"));
            }
            else if (sourceIsEvm && targetIsAlgorand)
            {
                routes.Add(BuildCexRoute(request));
                routes.Add(BuildMultiHopRoute(request, "EVM → CEX → Algorand"));
            }
            else
            {
                // Unknown combination - provide generic CEX route
                routes.Add(BuildCexRoute(request));
            }

            // Mark recommended route
            if (routes.Any())
            {
                routes[0].IsRecommended = true;
                response.RecommendedRoute = routes[0];
            }

            response.HasDirectRoute = routes.Any(r => r.RouteType == WalletRouteType.Direct);
            response.AvailableRoutes = routes;
            response.RoutingSummary = BuildRoutingSummary(request.SourceNetwork, request.TargetNetwork, routes);

            return Task.FromResult(response);
        }

        // ── Route builders ────────────────────────────────────────────────────────

        private static WalletRoutingOption BuildDirectRoute(WalletRoutingRequest request)
        {
            return new WalletRoutingOption
            {
                RouteName = "Direct (same network)",
                RouteType = WalletRouteType.Direct,
                EstimatedTimeSeconds = 5,
                EstimatedFeeUsd = 0.00m,
                ConfidenceScore = 100,
                IsRecommended = true,
                Steps = new List<WalletRoutingStep>
                {
                    new() { StepNumber = 1, Title = "Proceed with transaction",
                        Instruction = "Your wallet is already on the correct network. No routing needed.",
                        EstimatedSeconds = 5, IsAutomatable = true }
                }
            };
        }

        private static WalletRoutingOption BuildAlgorandNetworkSwitchRoute(WalletRoutingRequest request)
        {
            return new WalletRoutingOption
            {
                RouteName = $"Switch Algorand Network",
                RouteType = WalletRouteType.Direct,
                EstimatedTimeSeconds = 30,
                EstimatedFeeUsd = 0.001m,
                ConfidenceScore = 95,
                IsRecommended = true,
                Steps = new List<WalletRoutingStep>
                {
                    new() { StepNumber = 1, Title = "Open wallet settings",
                        Instruction = "Open your Algorand wallet and navigate to Network Settings.",
                        EstimatedSeconds = 15, IsAutomatable = false },
                    new() { StepNumber = 2, Title = $"Switch to {request.TargetNetwork}",
                        Instruction = $"Select '{request.TargetNetwork}' from the network list.",
                        EstimatedSeconds = 10, IsAutomatable = false },
                    new() { StepNumber = 3, Title = "Confirm connection",
                        Instruction = "Verify that your wallet now shows the correct network.",
                        EstimatedSeconds = 5, IsAutomatable = true }
                }
            };
        }

        private static WalletRoutingOption BuildEvmBridgeRoute(WalletRoutingRequest request)
        {
            return new WalletRoutingOption
            {
                RouteName = "EVM Bridge",
                RouteType = WalletRouteType.Bridge,
                EstimatedTimeSeconds = 300,
                EstimatedFeeUsd = 1.50m,
                ConfidenceScore = 85,
                Warnings = new List<string>
                {
                    "Bridge operations are irreversible - verify the destination address",
                    "Bridge fees vary with network congestion"
                },
                Steps = new List<WalletRoutingStep>
                {
                    new() { StepNumber = 1, Title = "Connect to bridge",
                        Instruction = $"Navigate to a bridge supporting {request.SourceNetwork} → {request.TargetNetwork}.",
                        EstimatedSeconds = 30, IsAutomatable = false },
                    new() { StepNumber = 2, Title = "Approve bridge contract",
                        Instruction = "Approve the bridge contract to access your tokens.",
                        EstimatedSeconds = 60, IsAutomatable = false },
                    new() { StepNumber = 3, Title = "Initiate bridge transfer",
                        Instruction = "Confirm the bridge transfer and wait for confirmation.",
                        EstimatedSeconds = 120, IsAutomatable = false },
                    new() { StepNumber = 4, Title = "Claim on target network",
                        Instruction = $"Once confirmed, claim your tokens on {request.TargetNetwork}.",
                        EstimatedSeconds = 90, IsAutomatable = false }
                }
            };
        }

        private static WalletRoutingOption BuildCexRoute(WalletRoutingRequest request)
        {
            return new WalletRoutingOption
            {
                RouteName = "Centralized Exchange",
                RouteType = WalletRouteType.CentralizedExchange,
                EstimatedTimeSeconds = 1800,
                EstimatedFeeUsd = 2.00m,
                ConfidenceScore = 70,
                Warnings = new List<string>
                {
                    "CEX withdrawals may require KYC verification",
                    "Processing times vary by exchange and network congestion"
                },
                Steps = new List<WalletRoutingStep>
                {
                    new() { StepNumber = 1, Title = "Deposit to CEX",
                        Instruction = $"Deposit your assets from {request.SourceNetwork} to a CEX (e.g., Coinbase, Binance).",
                        EstimatedSeconds = 600, IsAutomatable = false },
                    new() { StepNumber = 2, Title = "Exchange if needed",
                        Instruction = "Exchange to the native asset of the target network if required.",
                        EstimatedSeconds = 60, IsAutomatable = false },
                    new() { StepNumber = 3, Title = "Withdraw to target network",
                        Instruction = $"Withdraw to your wallet on {request.TargetNetwork}.",
                        EstimatedSeconds = 1200, IsAutomatable = false }
                }
            };
        }

        private static WalletRoutingOption BuildMultiHopRoute(WalletRoutingRequest request, string routeDescription)
        {
            return new WalletRoutingOption
            {
                RouteName = $"Multi-Hop: {routeDescription}",
                RouteType = WalletRouteType.MultiHop,
                EstimatedTimeSeconds = 2400,
                EstimatedFeeUsd = 3.00m,
                ConfidenceScore = 60,
                Warnings = new List<string>
                {
                    "Multi-hop routes have higher fees due to multiple transactions",
                    "Each hop adds latency and potential failure points"
                },
                Steps = new List<WalletRoutingStep>
                {
                    new() { StepNumber = 1, Title = "Send to intermediate network",
                        Instruction = "Transfer assets to an intermediate exchange or bridge.",
                        EstimatedSeconds = 900, IsAutomatable = false },
                    new() { StepNumber = 2, Title = "Convert to target asset",
                        Instruction = "Convert to the required asset for the target network.",
                        EstimatedSeconds = 300, IsAutomatable = false },
                    new() { StepNumber = 3, Title = "Transfer to target network",
                        Instruction = $"Complete the transfer to {request.TargetNetwork}.",
                        EstimatedSeconds = 1200, IsAutomatable = false }
                }
            };
        }

        private static string BuildRoutingSummary(string source, string target, List<WalletRoutingOption> routes)
        {
            if (!routes.Any())
            {
                return $"No routing options found from {source} to {target}.";
            }

            var best = routes.First();
            return $"Found {routes.Count} route(s) from {source} to {target}. " +
                   $"Recommended: {best.RouteName} (~{best.EstimatedTimeSeconds / 60} min, " +
                   $"~${best.EstimatedFeeUsd:F2} fee).";
        }

        private static bool IsAlgorandEcosystem(string? network) =>
            network != null && _algorandNetworks.Contains(network);

        private static bool IsEvmEcosystem(string? network) =>
            network != null && _evmNetworks.Contains(network);
    }
}

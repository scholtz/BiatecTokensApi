using System.Diagnostics;
using System.Text.RegularExpressions;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Portfolio;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Portfolio intelligence service: aggregates token metadata, wallet holdings context,
    /// and user-action affordances into an enriched portfolio response.
    ///
    /// All public operations are deterministic – identical inputs always produce identical outputs.
    /// Partial upstream failures produce a degraded-mode response (IsDegraded=true) rather than
    /// a hard error, so users always receive best-effort intelligence.
    /// </summary>
    public class PortfolioIntelligenceService : IPortfolioIntelligenceService
    {
        private readonly ILogger<PortfolioIntelligenceService> _logger;

        // Algorand address: 58 uppercase base32 chars
        private static readonly Regex AlgorandAddressPattern =
            new(@"^[A-Z2-7]{58}$", RegexOptions.Compiled);

        // EVM address: 0x followed by 40 hex chars
        private static readonly Regex EvmAddressPattern =
            new(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

        // Algorand-family networks
        private static readonly HashSet<string> AlgorandNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "algorand-mainnet", "algorand-testnet", "algorand-betanet",
            "voi-mainnet", "aramid-mainnet"
        };

        // EVM-family networks
        private static readonly HashSet<string> EvmNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "base-mainnet", "base-sepolia", "ethereum-mainnet", "ethereum-sepolia"
        };

        // Token standards by chain family
        private static readonly HashSet<string> AlgorandStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ARC1400"
        };
        private static readonly HashSet<string> EvmStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ERC20", "ERC721", "ERC1155"
        };

        /// <summary>
        /// Initializes a new instance of <see cref="PortfolioIntelligenceService"/>.
        /// </summary>
        public PortfolioIntelligenceService(ILogger<PortfolioIntelligenceService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<PortfolioIntelligenceResponse> GetPortfolioIntelligenceAsync(
            PortfolioIntelligenceRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                return BuildErrorResponse(string.Empty, string.Empty, correlationId,
                    "Request cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(request.WalletAddress))
            {
                return BuildErrorResponse(string.Empty, request.Network, correlationId,
                    "WalletAddress must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(request.Network))
            {
                return BuildErrorResponse(request.WalletAddress, string.Empty, correlationId,
                    "Network must not be empty.");
            }

            _logger.LogInformation(
                "Portfolio intelligence evaluation started: Wallet={Wallet}, Network={Network}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.WalletAddress),
                LoggingHelper.SanitizeLogInput(request.Network),
                correlationId);

            var (compatibilityStatus, compatibilityMessage) =
                EvaluateWalletCompatibility(request.WalletAddress, request.Network);

            // Build synthetic holdings for demonstration / domain logic validation.
            // In a real deployment this would call on-chain indexers; here we return
            // deterministic mock data so the domain rules are exercisable by tests.
            var holdings = BuildDeterministicHoldings(request);
            var degradedSources = new List<string>();
            bool isDegraded = false;

            // Simulate graceful degradation: if network is unrecognized, mark as degraded.
            if (!AlgorandNetworks.Contains(request.Network) && !EvmNetworks.Contains(request.Network))
            {
                degradedSources.Add("NetworkRegistry");
                isDegraded = true;
                _logger.LogWarning(
                    "Unrecognized network '{Network}'; portfolio intelligence running in degraded mode. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.Network),
                    correlationId);
            }

            var holdingRisks = holdings.Select(h => h.RiskLevel);
            var aggregateRisk = AggregateRisk(holdingRisks);
            var riskConfidence = ComputePortfolioConfidence(holdings);
            var actionReadiness = ComputeActionReadiness(compatibilityStatus, aggregateRisk);

            var summary = BuildSummary(holdings);

            List<PortfolioOpportunity> opportunities = new();
            if (request.IncludeOpportunities)
            {
                opportunities = DiscoverOpportunities(holdings);
                summary.WithOpportunitiesCount = opportunities
                    .GroupBy(o => o.AssetId)
                    .Count();
            }

            sw.Stop();
            _logger.LogInformation(
                "Portfolio intelligence evaluation complete: Wallet={Wallet}, AggregateRisk={Risk}, " +
                "Holdings={Count}, ElapsedMs={Elapsed}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.WalletAddress),
                aggregateRisk,
                holdings.Count,
                sw.ElapsedMilliseconds,
                correlationId);

            await Task.CompletedTask; // async boundary for future I/O expansion

            return new PortfolioIntelligenceResponse
            {
                WalletAddress = request.WalletAddress,
                Network = request.Network,
                IsDegraded = isDegraded,
                DegradedSources = degradedSources,
                AggregateRiskLevel = aggregateRisk,
                RiskConfidence = riskConfidence,
                WalletCompatibility = compatibilityStatus,
                WalletCompatibilityMessage = compatibilityMessage,
                ActionReadiness = actionReadiness,
                Summary = summary,
                Holdings = holdings,
                Opportunities = opportunities,
                CorrelationId = correlationId,
                EvaluatedAt = DateTimeOffset.UtcNow,
                SchemaVersion = "1.0.0"
            };
        }

        /// <inheritdoc/>
        public (WalletCompatibilityStatus Status, string Message) EvaluateWalletCompatibility(
            string walletAddress,
            string network,
            string? tokenStandard = null)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                return (WalletCompatibilityStatus.NotConnected, "Wallet address is required.");

            bool isAlgorandAddress = AlgorandAddressPattern.IsMatch(walletAddress);
            bool isEvmAddress = EvmAddressPattern.IsMatch(walletAddress);
            bool isAlgorandNetwork = AlgorandNetworks.Contains(network);
            bool isEvmNetwork = EvmNetworks.Contains(network);

            // Standard/network mismatch check
            if (!string.IsNullOrWhiteSpace(tokenStandard))
            {
                bool standardIsAlgorand = AlgorandStandards.Contains(tokenStandard);
                bool standardIsEvm = EvmStandards.Contains(tokenStandard);

                if (standardIsAlgorand && isEvmAddress)
                    return (WalletCompatibilityStatus.UnsupportedWalletType,
                        $"Token standard '{tokenStandard}' requires an Algorand wallet address, " +
                        "but an EVM address was provided.");

                if (standardIsEvm && isAlgorandAddress)
                    return (WalletCompatibilityStatus.UnsupportedWalletType,
                        $"Token standard '{tokenStandard}' requires an EVM wallet address, " +
                        "but an Algorand address was provided.");
            }

            // Address/network mismatch check
            if (isAlgorandAddress && isEvmNetwork)
                return (WalletCompatibilityStatus.NetworkMismatch,
                    $"Algorand wallet address is not compatible with EVM network '{network}'. " +
                    "Please switch to an Algorand-compatible network.");

            if (isEvmAddress && isAlgorandNetwork)
                return (WalletCompatibilityStatus.NetworkMismatch,
                    $"EVM wallet address is not compatible with Algorand network '{network}'. " +
                    "Please switch to a Base/EVM-compatible network.");

            if (isAlgorandAddress && isAlgorandNetwork)
                return (WalletCompatibilityStatus.Compatible,
                    $"Wallet is connected and compatible with {network}.");

            if (isEvmAddress && isEvmNetwork)
                return (WalletCompatibilityStatus.Compatible,
                    $"Wallet is connected and compatible with {network}.");

            // Unknown address format or unrecognized network
            if (!isAlgorandAddress && !isEvmAddress)
                return (WalletCompatibilityStatus.NotConnected,
                    "Wallet address format is not recognized. " +
                    "Expected an Algorand address (58 uppercase base32 chars) or an EVM address (0x...).");

            return (WalletCompatibilityStatus.NotConnected,
                $"Network '{network}' is not recognized. Unable to determine wallet compatibility.");
        }

        /// <inheritdoc/>
        public HoldingRiskLevel AggregateRisk(IEnumerable<HoldingRiskLevel> holdingRisks)
        {
            var risks = holdingRisks?.ToList() ?? new List<HoldingRiskLevel>();
            if (risks.Count == 0) return HoldingRiskLevel.Unknown;

            // Worst-case aggregation: any High → aggregate is High
            if (risks.Any(r => r == HoldingRiskLevel.High)) return HoldingRiskLevel.High;
            if (risks.Any(r => r == HoldingRiskLevel.Medium)) return HoldingRiskLevel.Medium;
            if (risks.All(r => r == HoldingRiskLevel.Unknown)) return HoldingRiskLevel.Unknown;
            return HoldingRiskLevel.Low;
        }

        /// <inheritdoc/>
        public (HoldingRiskLevel Risk, ConfidenceLevel Confidence, List<RiskSignal> Signals, List<ConfidenceIndicator> Indicators)
            EvaluateHoldingRisk(ulong assetId, string network, bool hasMintAuthority, bool metadataComplete, bool isVerified)
        {
            var signals = new List<RiskSignal>();
            var indicators = new List<ConfidenceIndicator>();

            // Risk signal: active mint authority raises risk
            if (hasMintAuthority)
            {
                signals.Add(new RiskSignal
                {
                    SignalCode = "MINT_AUTHORITY_ACTIVE",
                    Description = "Token mint authority is still active. Supply can be increased by the issuer.",
                    Severity = HoldingRiskLevel.Medium
                });
            }

            // Risk signal: incomplete metadata raises risk
            if (!metadataComplete)
            {
                signals.Add(new RiskSignal
                {
                    SignalCode = "METADATA_INCOMPLETE",
                    Description = "Token metadata is incomplete. This reduces transparency and trust.",
                    Severity = HoldingRiskLevel.Medium
                });
            }

            // Confidence indicators
            indicators.Add(new ConfidenceIndicator
            {
                Key = "METADATA_VERIFIED",
                IsPositive = metadataComplete,
                Description = metadataComplete
                    ? "Token metadata is complete and verifiable."
                    : "Token metadata is incomplete or missing."
            });

            indicators.Add(new ConfidenceIndicator
            {
                Key = "EXTERNALLY_VERIFIED",
                IsPositive = isVerified,
                Description = isVerified
                    ? "Token has been externally verified."
                    : "Token has not been externally verified."
            });

            indicators.Add(new ConfidenceIndicator
            {
                Key = "ONCHAIN_DATA_PRESENT",
                IsPositive = assetId > 0,
                Description = assetId > 0
                    ? "On-chain asset ID is present."
                    : "No on-chain asset ID provided."
            });

            // Determine risk and confidence
            HoldingRiskLevel risk;
            if (signals.Any(s => s.Severity == HoldingRiskLevel.High))
                risk = HoldingRiskLevel.High;
            else if (signals.Any(s => s.Severity == HoldingRiskLevel.Medium))
                risk = HoldingRiskLevel.Medium;
            else if (signals.Count == 0)
                risk = HoldingRiskLevel.Low;
            else
                risk = HoldingRiskLevel.Low;

            int positiveIndicators = indicators.Count(i => i.IsPositive);
            ConfidenceLevel confidence = positiveIndicators >= 3
                ? ConfidenceLevel.High
                : positiveIndicators == 2
                    ? ConfidenceLevel.Medium
                    : ConfidenceLevel.Low;

            return (risk, confidence, signals, indicators);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static PortfolioIntelligenceResponse BuildErrorResponse(
            string wallet, string network, string correlationId, string message)
        {
            return new PortfolioIntelligenceResponse
            {
                WalletAddress = wallet,
                Network = network,
                IsDegraded = true,
                DegradedSources = new List<string> { "Validation" },
                AggregateRiskLevel = HoldingRiskLevel.Unknown,
                RiskConfidence = ConfidenceLevel.Low,
                WalletCompatibility = WalletCompatibilityStatus.NotConnected,
                WalletCompatibilityMessage = message,
                ActionReadiness = ActionReadiness.NotReady,
                CorrelationId = correlationId,
                EvaluatedAt = DateTimeOffset.UtcNow,
                SchemaVersion = "1.0.0"
            };
        }

        private List<HoldingIntelligence> BuildDeterministicHoldings(PortfolioIntelligenceRequest request)
        {
            // Return deterministic synthetic holdings based on request parameters.
            // In production this would be replaced by an on-chain indexer call.
            var holdings = new List<HoldingIntelligence>();

            bool isAlgorandNetwork = AlgorandNetworks.Contains(request.Network);
            bool isEvmNetwork = EvmNetworks.Contains(request.Network);

            if (isAlgorandNetwork)
            {
                var (risk1, conf1, sigs1, inds1) = EvaluateHoldingRisk(1001, request.Network,
                    hasMintAuthority: false, metadataComplete: true, isVerified: true);
                holdings.Add(new HoldingIntelligence
                {
                    AssetId = 1001,
                    Name = "Biatec Token",
                    Symbol = "BIATEC",
                    Standard = "ASA",
                    RiskLevel = risk1,
                    RiskConfidence = conf1,
                    ActionReadiness = ActionReadiness.Ready,
                    RiskSignals = sigs1,
                    ConfidenceIndicators = inds1,
                    StatusSummary = "Well-configured ASA token with complete metadata.",
                    RecommendedAction = null
                });

                if (request.IncludeRiskDetails)
                {
                    var (risk2, conf2, sigs2, inds2) = EvaluateHoldingRisk(2002, request.Network,
                        hasMintAuthority: true, metadataComplete: false, isVerified: false);
                    holdings.Add(new HoldingIntelligence
                    {
                        AssetId = 2002,
                        Name = "Sample Token",
                        Symbol = "SAMP",
                        Standard = "ARC3",
                        RiskLevel = risk2,
                        RiskConfidence = conf2,
                        ActionReadiness = ActionReadiness.ConditionallyReady,
                        RiskSignals = sigs2,
                        ConfidenceIndicators = inds2,
                        StatusSummary = "Token has active mint authority and incomplete metadata.",
                        RecommendedAction = "Complete metadata and review mint authority posture."
                    });
                }
            }
            else if (isEvmNetwork)
            {
                var (risk1, conf1, sigs1, inds1) = EvaluateHoldingRisk(3003, request.Network,
                    hasMintAuthority: false, metadataComplete: true, isVerified: true);
                holdings.Add(new HoldingIntelligence
                {
                    AssetId = 3003,
                    Name = "Base Token",
                    Symbol = "BASE",
                    Standard = "ERC20",
                    RiskLevel = risk1,
                    RiskConfidence = conf1,
                    ActionReadiness = ActionReadiness.Ready,
                    RiskSignals = sigs1,
                    ConfidenceIndicators = inds1,
                    StatusSummary = "ERC20 token in good standing.",
                    RecommendedAction = null
                });
            }

            // Apply asset filter if provided
            if (request.AssetFilter != null && request.AssetFilter.Count > 0)
            {
                holdings = holdings
                    .Where(h => request.AssetFilter.Contains(h.AssetId))
                    .ToList();
            }

            return holdings;
        }

        private static PortfolioSummary BuildSummary(List<HoldingIntelligence> holdings)
        {
            return new PortfolioSummary
            {
                TotalHoldings = holdings.Count,
                HighRiskCount = holdings.Count(h => h.RiskLevel == HoldingRiskLevel.High),
                MediumRiskCount = holdings.Count(h => h.RiskLevel == HoldingRiskLevel.Medium),
                LowRiskCount = holdings.Count(h => h.RiskLevel == HoldingRiskLevel.Low),
                UnknownRiskCount = holdings.Count(h => h.RiskLevel == HoldingRiskLevel.Unknown),
                ActionReadyCount = holdings.Count(h => h.ActionReadiness == ActionReadiness.Ready)
            };
        }

        private static List<PortfolioOpportunity> DiscoverOpportunities(List<HoldingIntelligence> holdings)
        {
            var opportunities = new List<PortfolioOpportunity>();

            foreach (var holding in holdings)
            {
                if (!holding.MetadataComplete())
                {
                    opportunities.Add(new PortfolioOpportunity
                    {
                        Category = OpportunityCategory.MetadataImprovement,
                        AssetId = holding.AssetId,
                        Title = $"Complete metadata for {holding.Symbol}",
                        Description = $"Token '{holding.Name}' has incomplete metadata, reducing discoverability and buyer trust.",
                        CallToAction = "Update token metadata",
                        Priority = 80
                    });
                }

                if (holding.RiskSignals.Any(s => s.SignalCode == "MINT_AUTHORITY_ACTIVE"))
                {
                    opportunities.Add(new PortfolioOpportunity
                    {
                        Category = OpportunityCategory.ComplianceAction,
                        AssetId = holding.AssetId,
                        Title = $"Review mint authority for {holding.Symbol}",
                        Description = $"Token '{holding.Name}' has an active mint authority. " +
                                      "Consider revoking or clarifying this posture to improve investor confidence.",
                        CallToAction = "Review mint authority settings",
                        Priority = 70
                    });
                }
            }

            return opportunities.OrderByDescending(o => o.Priority).ToList();
        }

        private static ConfidenceLevel ComputePortfolioConfidence(List<HoldingIntelligence> holdings)
        {
            if (holdings.Count == 0) return ConfidenceLevel.Low;

            int highConfCount = holdings.Count(h => h.RiskConfidence == ConfidenceLevel.High);
            int lowConfCount = holdings.Count(h => h.RiskConfidence == ConfidenceLevel.Low);

            if (highConfCount == holdings.Count) return ConfidenceLevel.High;
            if (lowConfCount == holdings.Count) return ConfidenceLevel.Low;
            return ConfidenceLevel.Medium;
        }

        private static ActionReadiness ComputeActionReadiness(
            WalletCompatibilityStatus compatibility,
            HoldingRiskLevel aggregateRisk)
        {
            if (compatibility != WalletCompatibilityStatus.Compatible)
                return ActionReadiness.NotReady;

            return aggregateRisk switch
            {
                HoldingRiskLevel.High => ActionReadiness.NotReady,
                HoldingRiskLevel.Medium => ActionReadiness.ConditionallyReady,
                HoldingRiskLevel.Low => ActionReadiness.Ready,
                _ => ActionReadiness.NotReady
            };
        }
    }

    // Extension method to check metadata completeness from HoldingIntelligence
    internal static class HoldingIntelligenceExtensions
    {
        internal static bool MetadataComplete(this HoldingIntelligence h) =>
            !h.ConfidenceIndicators.Any(i => i.Key == "METADATA_VERIFIED" && !i.IsPositive);
    }
}

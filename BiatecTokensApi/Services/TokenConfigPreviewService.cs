using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for previewing token configurations before deployment and computing trust scores.
    /// Provides guided validation with completeness scoring, field-level guidance, cost estimates,
    /// and competitive signals to improve token launch success rates.
    /// </summary>
    public class TokenConfigPreviewService : ITokenConfigPreviewService
    {
        private readonly ILogger<TokenConfigPreviewService> _logger;

        // Score version for reproducibility
        private const string ScoreVersion = "2026.03.1";

        // Supported token types
        private static readonly IReadOnlySet<string> _supportedTokenTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ASA", "ARC3", "ARC200", "ARC1400", "ERC20"
            };

        // Supported networks
        private static readonly IReadOnlySet<string> _supportedNetworks =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "algorand-mainnet", "algorand-testnet", "algorand-betanet",
                "voi-mainnet", "aramid-mainnet", "base-mainnet"
            };

        /// <summary>
        /// Initializes a new instance of <see cref="TokenConfigPreviewService"/>.
        /// </summary>
        public TokenConfigPreviewService(ILogger<TokenConfigPreviewService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<TokenConfigPreviewResponse> PreviewConfigAsync(TokenConfigPreviewRequest request)
        {
            _logger.LogInformation(
                "Previewing token configuration: TokenType={TokenType}, Network={Network}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.TokenType),
                LoggingHelper.SanitizeLogInput(request.Network),
                LoggingHelper.SanitizeLogInput(request.CorrelationId));

            var response = new TokenConfigPreviewResponse
            {
                CorrelationId = request.CorrelationId
            };

            var fieldIssues = new List<TokenConfigFieldIssue>();
            var improvements = new List<TokenConfigImprovement>();
            int score = 0;
            bool isDeployable = true;

            // ── Core field validation ────────────────────────────────────────────

            // TokenType validation
            if (string.IsNullOrWhiteSpace(request.TokenType))
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "TokenType",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = "Token type is required",
                    SuggestedFix = $"Set TokenType to one of: {string.Join(", ", _supportedTokenTypes)}"
                });
                isDeployable = false;
            }
            else if (!_supportedTokenTypes.Contains(request.TokenType))
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "TokenType",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = $"Token type '{request.TokenType}' is not supported",
                    SuggestedFix = $"Use one of: {string.Join(", ", _supportedTokenTypes)}"
                });
                isDeployable = false;
            }
            else
            {
                score += 10;
            }

            // Network validation
            if (string.IsNullOrWhiteSpace(request.Network))
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Network",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = "Network is required",
                    SuggestedFix = "Set Network to a supported network"
                });
                isDeployable = false;
            }
            else if (!_supportedNetworks.Contains(request.Network))
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Network",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = $"Network '{request.Network}' is not supported",
                    SuggestedFix = $"Use one of: {string.Join(", ", _supportedNetworks)}"
                });
                isDeployable = false;
            }
            else
            {
                score += 10;
            }

            // Name validation
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Name",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = "Token name is required",
                    SuggestedFix = "Provide a descriptive name (max 32 chars for ASA)"
                });
                isDeployable = false;
                improvements.Add(new TokenConfigImprovement
                {
                    ScoreGain = 15,
                    Title = "Add Token Name",
                    Description = "A token name is mandatory for deployment",
                    IsRequired = true
                });
            }
            else if (IsAlgorandNetwork(request.Network) && request.Name.Length > 32)
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Name",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = $"Token name exceeds maximum length of 32 for Algorand (current: {request.Name.Length})",
                    SuggestedFix = "Shorten the name to 32 characters or fewer"
                });
                isDeployable = false;
            }
            else
            {
                score += 15;
            }

            // Symbol validation
            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Symbol",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = "Token symbol is required",
                    SuggestedFix = "Provide a ticker symbol (max 8 chars for ASA, e.g. 'USDC')"
                });
                isDeployable = false;
                improvements.Add(new TokenConfigImprovement
                {
                    ScoreGain = 15,
                    Title = "Add Token Symbol",
                    Description = "A ticker symbol is mandatory for deployment",
                    IsRequired = true
                });
            }
            else if (IsAlgorandNetwork(request.Network) && request.Symbol.Length > 8)
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Symbol",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = $"Symbol exceeds maximum length of 8 for Algorand (current: {request.Symbol.Length})",
                    SuggestedFix = "Shorten the symbol to 8 characters or fewer"
                });
                isDeployable = false;
            }
            else
            {
                score += 15;
            }

            // Decimals validation
            if (request.Decimals < 0 || request.Decimals > 19)
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "Decimals",
                    Severity = TokenConfigIssueSeverity.Error,
                    Message = $"Decimals must be between 0 and 19 (current: {request.Decimals})",
                    SuggestedFix = "Common values: 0 (NFT), 2 (cents-like), 6 (USDC-like), 8 (BTC-like)"
                });
                isDeployable = false;
            }
            else
            {
                score += 10;
            }

            // ── Optional but quality-enhancing fields ────────────────────────────

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                improvements.Add(new TokenConfigImprovement
                {
                    ScoreGain = 10,
                    Title = "Add Token Description",
                    Description = "A description improves buyer confidence and discoverability",
                    IsRequired = false
                });
            }
            else
            {
                score += 10;
            }

            if (string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                improvements.Add(new TokenConfigImprovement
                {
                    ScoreGain = 10,
                    Title = "Add Token Image",
                    Description = "A logo or image strongly improves buyer recognition and trust",
                    IsRequired = false
                });
            }
            else
            {
                score += 10;
            }

            // Supply validation
            if (request.TotalSupply == 0 && !request.IsMintable)
            {
                fieldIssues.Add(new TokenConfigFieldIssue
                {
                    FieldName = "TotalSupply",
                    Severity = TokenConfigIssueSeverity.Warning,
                    Message = "Total supply is 0 and token is not mintable; no tokens will exist",
                    SuggestedFix = "Set a non-zero TotalSupply or enable IsMintable"
                });
            }
            else
            {
                score += 10;
            }

            // Capped at 100
            score = Math.Min(score, 100);

            // ── Competitive signals ───────────────────────────────────────────────
            var trustFeatures = new List<string>();
            var missingFeatures = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.Description)) trustFeatures.Add("Token description present");
            else missingFeatures.Add("Add a description to improve discoverability");

            if (!string.IsNullOrWhiteSpace(request.ImageUrl)) trustFeatures.Add("Token image/logo present");
            else missingFeatures.Add("Add a logo to improve buyer recognition");

            if (request.IsMintable) trustFeatures.Add("Mintable supply (flexible)");
            if (request.IsFreezable) trustFeatures.Add("Freeze capability (compliance-ready)");
            if (request.IsClawbackEnabled) trustFeatures.Add("Clawback capability (compliance-ready)");

            string buyerConfidence;
            if (score >= 80) buyerConfidence = "High";
            else if (score >= 60) buyerConfidence = "Medium";
            else if (score >= 40) buyerConfidence = "Low";
            else buyerConfidence = "Minimal";

            // ── Cost estimate ─────────────────────────────────────────────────────
            var costEstimate = ComputeCostEstimate(request);

            // ── Build response ────────────────────────────────────────────────────
            response.CompletenessScore = score;
            response.IsDeployable = isDeployable;
            response.FieldIssues = fieldIssues;
            response.Improvements = improvements.OrderByDescending(i => i.IsRequired).ThenByDescending(i => i.ScoreGain).ToList();
            response.CostEstimate = costEstimate;
            response.CompetitiveSignals = new TokenCompetitiveSignals
            {
                ConfigurationQualityScore = score,
                TrustEnhancingFeatures = trustFeatures,
                MissingTrustFeatures = missingFeatures,
                BuyerConfidenceCategory = buyerConfidence
            };
            response.Summary = isDeployable
                ? $"Configuration is valid and deployable. Completeness: {score}/100 ({buyerConfidence} buyer confidence)."
                : $"Configuration has {fieldIssues.Count(i => i.Severity == TokenConfigIssueSeverity.Error)} error(s) that must be resolved before deployment.";

            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public Task<TokenTrustScoreResponse> ComputeTrustScoreAsync(string tokenIdentifier, string network)
        {
            _logger.LogInformation(
                "Computing trust score: TokenIdentifier={TokenIdentifier}, Network={Network}",
                LoggingHelper.SanitizeLogInput(tokenIdentifier),
                LoggingHelper.SanitizeLogInput(network));

            // Deterministic scoring based on token identifier characteristics
            // In production this would query on-chain data; here we use heuristic scoring
            var positiveSignals = new List<TrustSignal>();
            var riskSignals = new List<TrustSignal>();

            int metadataScore = 0;
            int complianceScore = 0;
            int deploymentQualityScore = 0;
            int creatorReputationScore = 0;

            // Token identifier length heuristic: longer IDs typically indicate richer metadata
            bool hasNumericId = ulong.TryParse(tokenIdentifier, out _);
            bool isKnownNetwork = _supportedNetworks.Contains(network ?? "");

            if (hasNumericId)
            {
                // Algorand ASA format
                metadataScore = 15;
                positiveSignals.Add(new TrustSignal
                {
                    Category = "Deployment",
                    Label = "On-chain Asset ID",
                    Description = "Token is deployed with a verifiable on-chain asset ID",
                    ScoreImpact = 15
                });
            }
            else if (tokenIdentifier.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // EVM contract address
                metadataScore = 15;
                positiveSignals.Add(new TrustSignal
                {
                    Category = "Deployment",
                    Label = "Smart Contract Address",
                    Description = "Token is deployed as a verifiable smart contract",
                    ScoreImpact = 15
                });
            }
            else
            {
                riskSignals.Add(new TrustSignal
                {
                    Category = "Deployment",
                    Label = "Unverifiable Identifier",
                    Description = "Token identifier format could not be verified",
                    ScoreImpact = -5
                });
            }

            if (isKnownNetwork)
            {
                deploymentQualityScore = 20;
                positiveSignals.Add(new TrustSignal
                {
                    Category = "Network",
                    Label = "Supported Network",
                    Description = "Token is deployed on a supported and monitored network",
                    ScoreImpact = 20
                });
            }
            else
            {
                riskSignals.Add(new TrustSignal
                {
                    Category = "Network",
                    Label = "Unmonitored Network",
                    Description = "Network is not in the supported monitoring list",
                    ScoreImpact = -10
                });
            }

            // Algorand-specific compliance signals
            if (IsAlgorandNetwork(network))
            {
                complianceScore = 15;
                creatorReputationScore = 10;
                positiveSignals.Add(new TrustSignal
                {
                    Category = "Compliance",
                    Label = "Algorand Network",
                    Description = "Algorand's native compliance architecture provides auditability",
                    ScoreImpact = 15
                });
            }

            // Risk signals for missing features
            riskSignals.Add(new TrustSignal
            {
                Category = "Metadata",
                Label = "Live Metadata Not Available",
                Description = "On-chain metadata could not be fetched; score uses available signals only",
                ScoreImpact = 0
            });

            int totalScore = Math.Clamp(
                metadataScore + complianceScore + deploymentQualityScore + creatorReputationScore,
                0, 100);

            var trustLevel = totalScore switch
            {
                >= 90 => TokenTrustLevel.Exceptional,
                >= 70 => TokenTrustLevel.High,
                >= 50 => TokenTrustLevel.Medium,
                >= 25 => TokenTrustLevel.Low,
                _ => TokenTrustLevel.Minimal
            };

            var trustSummary = trustLevel switch
            {
                TokenTrustLevel.Exceptional => "Exceptional trust posture with all compliance signals present.",
                TokenTrustLevel.High => "Strong trust signals present; suitable for most buyers.",
                TokenTrustLevel.Medium => "Reasonable trust posture; verify metadata before transacting.",
                TokenTrustLevel.Low => "Limited trust signals; conduct additional due diligence.",
                _ => "Minimal trust signals; exercise caution and verify independently."
            };

            var result = new TokenTrustScoreResponse
            {
                TokenIdentifier = tokenIdentifier,
                TrustScore = totalScore,
                TrustLevel = trustLevel,
                TrustSummary = trustSummary,
                Breakdown = new TokenTrustScoreBreakdown
                {
                    MetadataScore = metadataScore,
                    ComplianceScore = complianceScore,
                    DeploymentQualityScore = deploymentQualityScore,
                    CreatorReputationScore = creatorReputationScore
                },
                PositiveSignals = positiveSignals,
                RiskSignals = riskSignals,
                ScoreVersion = ScoreVersion
            };

            return Task.FromResult(result);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static bool IsAlgorandNetwork(string? network)
        {
            return network != null && (
                network.StartsWith("algorand", StringComparison.OrdinalIgnoreCase) ||
                network.StartsWith("voi", StringComparison.OrdinalIgnoreCase) ||
                network.StartsWith("aramid", StringComparison.OrdinalIgnoreCase));
        }

        private static TokenDeploymentCostEstimate ComputeCostEstimate(TokenConfigPreviewRequest request)
        {
            bool isAlgorand = IsAlgorandNetwork(request.Network);
            bool requiresIpfs = request.TokenType?.Equals("ARC3", StringComparison.OrdinalIgnoreCase) == true;

            if (isAlgorand)
            {
                // ASA minimum balance: 100_000 microAlgo base + 100_000 per asset created
                long minBalance = 200_000;
                if (requiresIpfs) minBalance += 100_000;

                return new TokenDeploymentCostEstimate
                {
                    EstimatedMinBalance = minBalance,
                    CostDescription = $"Minimum balance requirement: ~{minBalance / 1_000_000.0:F2} ALGO" +
                                      (requiresIpfs ? " plus IPFS storage fees" : ""),
                    CostUnit = "ALGO",
                    RequiresIpfsStorage = requiresIpfs
                };
            }
            else
            {
                // EVM: gas estimate in gwei equivalent
                long gasEstimateWei = 4_500_000L;

                return new TokenDeploymentCostEstimate
                {
                    EstimatedMinBalance = gasEstimateWei,
                    CostDescription = "Estimated gas: ~4,500,000 gas units; actual cost depends on network conditions",
                    CostUnit = "ETH (gas)",
                    RequiresIpfsStorage = false
                };
            }
        }
    }
}

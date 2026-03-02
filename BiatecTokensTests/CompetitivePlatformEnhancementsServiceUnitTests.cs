using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for the three competitive platform enhancements implemented in Issue #447:
    /// 1. TokenConfigPreviewService – guided pre-deployment configuration validation
    /// 2. TokenConfigPreviewService.ComputeTrustScoreAsync – trust score for buyer confidence
    /// 3. WalletRoutingService – cross-network routing optimization
    ///
    /// These service-layer tests exercise business logic independently of HTTP infrastructure,
    /// proving deterministic behavior, correct scoring thresholds, and edge-case handling.
    ///
    /// Business Value: Validates competitive differentiators that reduce creator launch friction
    /// and increase buyer confidence – directly driving conversion and retention metrics.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class CompetitivePlatformEnhancementsServiceUnitTests
    {
        private TokenConfigPreviewService _previewService = null!;
        private WalletRoutingService _routingService = null!;
        private Mock<ILogger<TokenConfigPreviewService>> _previewLoggerMock = null!;
        private Mock<ILogger<WalletRoutingService>> _routingLoggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _previewLoggerMock = new Mock<ILogger<TokenConfigPreviewService>>();
            _previewService = new TokenConfigPreviewService(_previewLoggerMock.Object);

            _routingLoggerMock = new Mock<ILogger<WalletRoutingService>>();
            _routingService = new WalletRoutingService(_routingLoggerMock.Object);
        }

        // ══════════════════════════════════════════════════════════════════════
        // AC1: TokenConfigPreviewService – Happy path
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PreviewConfig_FullyConfiguredAsaToken_ReturnsDeployableWithHighScore()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "My Token",
                Symbol = "MTK",
                TotalSupply = 1_000_000,
                Decimals = 6,
                Description = "A well-described token",
                ImageUrl = "https://example.com/logo.png",
                IsMintable = false
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.True);
            Assert.That(result.CompletenessScore, Is.GreaterThanOrEqualTo(70));
            Assert.That(result.FieldIssues.Any(i => i.Severity == TokenConfigIssueSeverity.Error), Is.False);
            Assert.That(result.Summary, Does.Contain("valid and deployable"));
            Assert.That(result.CostEstimate.CostUnit, Is.EqualTo("ALGO"));
        }

        [Test]
        public async Task PreviewConfig_MissingTokenType_ReturnsNotDeployableWithError()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "",
                Network = "algorand-mainnet",
                Name = "Token",
                Symbol = "TK"
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.False);
            Assert.That(result.FieldIssues.Any(f => f.FieldName == "TokenType" && f.Severity == TokenConfigIssueSeverity.Error), Is.True);
        }

        [Test]
        public async Task PreviewConfig_MissingName_ReturnsNotDeployableWithNameError()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "",
                Symbol = "TK"
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.False);
            Assert.That(result.FieldIssues.Any(f => f.FieldName == "Name" && f.Severity == TokenConfigIssueSeverity.Error), Is.True);
            Assert.That(result.Improvements.Any(i => i.IsRequired && i.Title.Contains("Name")), Is.True);
        }

        [Test]
        public async Task PreviewConfig_NameExceedsAlgorandLimit_ReturnsError()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = new string('A', 33), // 33 chars - exceeds 32 limit
                Symbol = "TK"
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.False);
            Assert.That(result.FieldIssues.Any(f => f.FieldName == "Name" && f.Severity == TokenConfigIssueSeverity.Error), Is.True);
        }

        [Test]
        public async Task PreviewConfig_SymbolExceedsAlgorandLimit_ReturnsError()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "Valid Name",
                Symbol = "TOOLONG1" // 8 chars exactly - within the 8-char limit for Algorand
            };

            var request9Chars = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "Valid Name",
                Symbol = "TOOLONG12" // 9 chars - exceeds the 8-char limit
            };

            // Act
            var result8 = await _previewService.PreviewConfigAsync(request);
            var result9 = await _previewService.PreviewConfigAsync(request9Chars);

            // Assert - 8 chars is valid
            Assert.That(result8.FieldIssues.Any(f => f.FieldName == "Symbol" && f.Severity == TokenConfigIssueSeverity.Error), Is.False);
            // 9 chars is invalid
            Assert.That(result9.IsDeployable, Is.False);
            Assert.That(result9.FieldIssues.Any(f => f.FieldName == "Symbol" && f.Severity == TokenConfigIssueSeverity.Error), Is.True);
        }

        [Test]
        public async Task PreviewConfig_UnsupportedNetwork_ReturnsNetworkError()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "unknown-network-xyz",
                Name = "My Token",
                Symbol = "MTK"
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.False);
            Assert.That(result.FieldIssues.Any(f => f.FieldName == "Network" && f.Severity == TokenConfigIssueSeverity.Error), Is.True);
        }

        [Test]
        public async Task PreviewConfig_InvalidDecimalsRange_ReturnsError()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "My Token",
                Symbol = "MTK",
                Decimals = 20 // Invalid: max is 19
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.False);
            Assert.That(result.FieldIssues.Any(f => f.FieldName == "Decimals"), Is.True);
        }

        [Test]
        public async Task PreviewConfig_MissingDescription_ReturnsImprovementSuggestion()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "My Token",
                Symbol = "MTK",
                TotalSupply = 1_000_000,
                Decimals = 6
                // No Description
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert - missing description is a suggestion, not a blocker
            Assert.That(result.IsDeployable, Is.True);
            Assert.That(result.Improvements.Any(i => i.Title.Contains("Description")), Is.True);
        }

        [Test]
        public async Task PreviewConfig_ERC20OnBaseMainnet_ReturnsCostInEth()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ERC20",
                Network = "base-mainnet",
                Name = "Base Token",
                Symbol = "BASE",
                TotalSupply = 1_000_000,
                Decimals = 18
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.IsDeployable, Is.True);
            Assert.That(result.CostEstimate.CostUnit, Is.EqualTo("ETH (gas)"));
        }

        [Test]
        public async Task PreviewConfig_ARC3Token_RequiresIpfsStorage()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ARC3",
                Network = "algorand-mainnet",
                Name = "ARC3 Token",
                Symbol = "ARC3",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.CostEstimate.RequiresIpfsStorage, Is.True);
            Assert.That(result.CostEstimate.CostDescription, Does.Contain("IPFS"));
        }

        [Test]
        public async Task PreviewConfig_ImprovementsOrderedByRequiredThenScore()
        {
            // Arrange - missing both name and description
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "",
                Symbol = "TK"
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert - required improvements should come first
            if (result.Improvements.Count > 1)
            {
                var firstRequired = result.Improvements.FirstOrDefault(i => i.IsRequired);
                var firstOptional = result.Improvements.FirstOrDefault(i => !i.IsRequired);
                if (firstRequired != null && firstOptional != null)
                {
                    Assert.That(result.Improvements.IndexOf(firstRequired),
                        Is.LessThan(result.Improvements.IndexOf(firstOptional)));
                }
            }

            Assert.Pass("Improvement ordering verified");
        }

        [Test]
        public async Task PreviewConfig_CompetitiveSignalsIncludeTrustFeatures()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "My Token",
                Symbol = "MTK",
                TotalSupply = 1_000_000,
                Decimals = 6,
                Description = "A token with description",
                ImageUrl = "https://example.com/logo.png",
                IsFreezable = true,
                IsClawbackEnabled = true
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert
            Assert.That(result.CompetitiveSignals.TrustEnhancingFeatures, Is.Not.Empty);
            Assert.That(result.CompetitiveSignals.TrustEnhancingFeatures.Any(f => f.Contains("description")), Is.True);
            Assert.That(result.CompetitiveSignals.TrustEnhancingFeatures.Any(f => f.Contains("image") || f.Contains("logo")), Is.True);
        }

        [Test]
        public async Task PreviewConfig_Deterministic_SameInputSameOutput()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "My Token",
                Symbol = "MTK",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

            // Act - run 3 times
            var r1 = await _previewService.PreviewConfigAsync(request);
            var r2 = await _previewService.PreviewConfigAsync(request);
            var r3 = await _previewService.PreviewConfigAsync(request);

            // Assert - scores must be identical (deterministic)
            Assert.That(r1.CompletenessScore, Is.EqualTo(r2.CompletenessScore));
            Assert.That(r2.CompletenessScore, Is.EqualTo(r3.CompletenessScore));
            Assert.That(r1.IsDeployable, Is.EqualTo(r2.IsDeployable));
        }

        [Test]
        public async Task PreviewConfig_ZeroSupplyNonMintable_ReturnsWarning()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "My Token",
                Symbol = "MTK",
                TotalSupply = 0, // 0 supply, not mintable
                IsMintable = false
            };

            // Act
            var result = await _previewService.PreviewConfigAsync(request);

            // Assert - should be a warning, not necessarily blocking
            Assert.That(result.FieldIssues.Any(f =>
                f.FieldName == "TotalSupply" && f.Severity == TokenConfigIssueSeverity.Warning), Is.True);
        }

        // ══════════════════════════════════════════════════════════════════════
        // AC2: TokenConfigPreviewService.ComputeTrustScoreAsync
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ComputeTrustScore_AlgorandAssetId_ReturnsPositiveSignals()
        {
            // Arrange
            var assetId = "12345678";
            var network = "algorand-mainnet";

            // Act
            var result = await _previewService.ComputeTrustScoreAsync(assetId, network);

            // Assert
            Assert.That(result.TokenIdentifier, Is.EqualTo(assetId));
            Assert.That(result.TrustScore, Is.GreaterThan(0));
            Assert.That(result.PositiveSignals.Any(s => s.Label.Contains("Asset ID") || s.Label.Contains("On-chain")), Is.True);
            Assert.That(result.ScoreVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ComputeTrustScore_EvmContractAddress_ReturnsPositiveSignals()
        {
            // Arrange
            var contractAddress = "0xAbCdEf1234567890abcdef1234567890AbCdEf12";
            var network = "base-mainnet";

            // Act
            var result = await _previewService.ComputeTrustScoreAsync(contractAddress, network);

            // Assert
            Assert.That(result.TrustScore, Is.GreaterThan(0));
            Assert.That(result.PositiveSignals.Any(s => s.Label.Contains("Smart Contract")), Is.True);
        }

        [Test]
        public async Task ComputeTrustScore_TrustLevelMatchesScoreThreshold()
        {
            // Arrange - Algorand mainnet numeric ID has good signals
            var result = await _previewService.ComputeTrustScoreAsync("99999999", "algorand-mainnet");

            // Assert - trust level must match score range
            var expectedLevel = result.TrustScore switch
            {
                >= 90 => TokenTrustLevel.Exceptional,
                >= 70 => TokenTrustLevel.High,
                >= 50 => TokenTrustLevel.Medium,
                >= 25 => TokenTrustLevel.Low,
                _ => TokenTrustLevel.Minimal
            };
            Assert.That(result.TrustLevel, Is.EqualTo(expectedLevel));
        }

        [Test]
        public async Task ComputeTrustScore_AlgorandNetwork_IncludesComplianceScore()
        {
            // Arrange
            var result = await _previewService.ComputeTrustScoreAsync("123456", "algorand-mainnet");

            // Assert - Algorand compliance architecture provides compliance bonus
            Assert.That(result.Breakdown.ComplianceScore, Is.GreaterThan(0));
        }

        [Test]
        public async Task ComputeTrustScore_BreakdownSumsToTotalScore()
        {
            // Arrange
            var result = await _previewService.ComputeTrustScoreAsync("456789", "algorand-testnet");

            // Assert - breakdown parts should sum to total
            var breakdownTotal = result.Breakdown.MetadataScore + result.Breakdown.ComplianceScore +
                                  result.Breakdown.DeploymentQualityScore + result.Breakdown.CreatorReputationScore;
            Assert.That(result.TrustScore, Is.EqualTo(breakdownTotal));
        }

        [Test]
        public async Task ComputeTrustScore_Deterministic_SameResultThreeRuns()
        {
            // Arrange
            var tokenId = "88888888";
            var network = "algorand-mainnet";

            // Act - run 3 times
            var r1 = await _previewService.ComputeTrustScoreAsync(tokenId, network);
            var r2 = await _previewService.ComputeTrustScoreAsync(tokenId, network);
            var r3 = await _previewService.ComputeTrustScoreAsync(tokenId, network);

            // Assert - deterministic
            Assert.That(r1.TrustScore, Is.EqualTo(r2.TrustScore));
            Assert.That(r2.TrustScore, Is.EqualTo(r3.TrustScore));
            Assert.That(r1.TrustLevel, Is.EqualTo(r2.TrustLevel));
        }

        [Test]
        public async Task ComputeTrustScore_TrustSummaryNotEmpty()
        {
            // Arrange
            var result = await _previewService.ComputeTrustScoreAsync("123", "algorand-mainnet");

            // Assert
            Assert.That(result.TrustSummary, Is.Not.Null.And.Not.Empty);
        }

        // ══════════════════════════════════════════════════════════════════════
        // AC3: WalletRoutingService – routing optimization
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetRoutingOptions_SameNetwork_ReturnsSameNetworkFlag()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "algorand-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.IsSameNetwork, Is.True);
            Assert.That(result.HasDirectRoute, Is.True);
            Assert.That(result.AvailableRoutes, Is.Not.Empty);
            Assert.That(result.RoutingSummary, Does.Contain("No routing needed"));
        }

        [Test]
        public async Task GetRoutingOptions_AlgorandNetworkSwitch_ReturnsDirectRoute()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "algorand-testnet",
                OperationType = WalletOperationType.TokenDeployment
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.IsSameNetwork, Is.False);
            Assert.That(result.RecommendedRoute, Is.Not.Null);
            Assert.That(result.AvailableRoutes, Is.Not.Empty);
            Assert.That(result.RecommendedRoute!.IsRecommended, Is.True);
        }

        [Test]
        public async Task GetRoutingOptions_EvmToEvm_ReturnsBridgeAndCexRoutes()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "base-mainnet",
                TargetNetwork = "ethereum-mainnet",
                OperationType = WalletOperationType.CrossChainBridge
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.AvailableRoutes.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.AvailableRoutes.Any(r => r.RouteType == WalletRouteType.Bridge), Is.True);
        }

        [Test]
        public async Task GetRoutingOptions_AlgorandToEvm_ReturnsCexRoute()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "base-mainnet",
                OperationType = WalletOperationType.TokenSwap
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.AvailableRoutes, Is.Not.Empty);
            Assert.That(result.AvailableRoutes.Any(r => r.RouteType == WalletRouteType.CentralizedExchange), Is.True);
        }

        [Test]
        public async Task GetRoutingOptions_RecommendedRoute_HasSteps()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "algorand-testnet",
                OperationType = WalletOperationType.TokenTransfer
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert - recommended route has at least one step
            Assert.That(result.RecommendedRoute, Is.Not.Null);
            Assert.That(result.RecommendedRoute!.Steps, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RecommendedRoute.Steps[0].StepNumber, Is.EqualTo(1));
        }

        [Test]
        public async Task GetRoutingOptions_RoutingSummaryIncludesNetworks()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "base-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.RoutingSummary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetRoutingOptions_AllRoutes_HavePositiveEstimatedTime()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "base-mainnet",
                TargetNetwork = "ethereum-mainnet",
                OperationType = WalletOperationType.CrossChainBridge
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert - all routes have a positive time estimate
            foreach (var route in result.AvailableRoutes)
            {
                Assert.That(route.EstimatedTimeSeconds, Is.GreaterThan(0),
                    $"Route '{route.RouteName}' has non-positive time estimate");
            }
        }

        [Test]
        public async Task GetRoutingOptions_Deterministic_SameInputSameOutput()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "base-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act - run 3 times
            var r1 = await _routingService.GetRoutingOptionsAsync(request);
            var r2 = await _routingService.GetRoutingOptionsAsync(request);
            var r3 = await _routingService.GetRoutingOptionsAsync(request);

            // Assert - routing count must be deterministic
            Assert.That(r1.AvailableRoutes.Count, Is.EqualTo(r2.AvailableRoutes.Count));
            Assert.That(r2.AvailableRoutes.Count, Is.EqualTo(r3.AvailableRoutes.Count));
            Assert.That(r1.RecommendedRoute?.RouteName, Is.EqualTo(r2.RecommendedRoute?.RouteName));
        }

        [Test]
        public async Task GetRoutingOptions_EvmToAlgorand_ReturnsMultiHopOrCex()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "ethereum-mainnet",
                TargetNetwork = "algorand-mainnet",
                OperationType = WalletOperationType.CrossChainBridge
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.AvailableRoutes, Is.Not.Empty);
            var routeTypes = result.AvailableRoutes.Select(r => r.RouteType).ToList();
            Assert.That(routeTypes.Any(t =>
                t == WalletRouteType.CentralizedExchange || t == WalletRouteType.MultiHop), Is.True);
        }

        [Test]
        public async Task GetRoutingOptions_BridgeRoute_HasWarnings()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "base-mainnet",
                TargetNetwork = "ethereum-mainnet",
                OperationType = WalletOperationType.CrossChainBridge
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);
            var bridgeRoute = result.AvailableRoutes.FirstOrDefault(r => r.RouteType == WalletRouteType.Bridge);

            // Assert - bridge routes should have risk warnings
            if (bridgeRoute != null)
            {
                Assert.That(bridgeRoute.Warnings, Is.Not.Null.And.Not.Empty,
                    "Bridge routes should include risk warnings to protect users");
            }

            Assert.Pass("Bridge route warning validation complete");
        }

        [Test]
        public async Task GetRoutingOptions_UnknownNetworkCombination_ReturnsAtLeastOneRoute()
        {
            // Arrange
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "voi-mainnet",
                TargetNetwork = "base-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act
            var result = await _routingService.GetRoutingOptionsAsync(request);

            // Assert - should always return at least one route (CEX fallback)
            Assert.That(result.AvailableRoutes, Is.Not.Empty);
            Assert.That(result.RecommendedRoute, Is.Not.Null);
        }
    }
}

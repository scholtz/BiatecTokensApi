using BiatecTokensApi.Models.Portfolio;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for Issue #466: Vision milestone – Portfolio intelligence
    /// and wallet experience advancement.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// PortfolioIntelligenceService business logic.
    ///
    /// AC1 - Portfolio intelligence with clear context and confidence indicators
    /// AC2 - Wallet flow communicates network and token compatibility before execution
    /// AC3 - Error, loading, and empty states are implemented and user-friendly
    /// AC4 - All changed logic has unit test coverage including negative paths
    /// AC5 - All integration boundaries have integration tests for success and failure
    /// AC6 - All changed user journeys have E2E tests covering critical paths
    /// AC7 - CI passes on all required checks with no skipped critical tests
    /// AC8 - PR description maps business goals to implementation and tests
    /// AC9 - Documentation is updated for operation, support, and release verification
    ///
    /// Business Value: Service-layer unit tests prove that portfolio intelligence domain
    /// rules are enforceable at the business-logic level independently of HTTP infrastructure,
    /// providing fast CI regression guards and explicit AC evidence for Issue #466 sign-off.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PortfolioIntelligenceIssue466ServiceUnitTests
    {
        private PortfolioIntelligenceService _service = null!;
        private Mock<ILogger<PortfolioIntelligenceService>> _loggerMock = null!;

        // Well-known Algorand test address (not a real funded account)
        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // Well-known EVM test address (Ethereum zero address – safe for tests)
        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<PortfolioIntelligenceService>>();
            _service = new PortfolioIntelligenceService(_loggerMock.Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: Portfolio intelligence with context and confidence indicators
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetPortfolioIntelligence_AlgorandWallet_ReturnsHoldings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true,
                IncludeOpportunities = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Holdings.Count, Is.GreaterThan(0));
            Assert.That(result.Summary.TotalHoldings, Is.EqualTo(result.Holdings.Count));
        }

        [Test]
        public async Task GetPortfolioIntelligence_AlgorandWallet_SetsAggregateRisk()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.AggregateRiskLevel,
                Is.AnyOf(HoldingRiskLevel.Low, HoldingRiskLevel.Medium,
                         HoldingRiskLevel.High, HoldingRiskLevel.Unknown));
        }

        [Test]
        public async Task GetPortfolioIntelligence_AlgorandWallet_SetsConfidenceLevel()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.RiskConfidence,
                Is.AnyOf(ConfidenceLevel.High, ConfidenceLevel.Medium, ConfidenceLevel.Low));
        }

        [Test]
        public async Task GetPortfolioIntelligence_EvmWallet_ReturnsHoldings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = EvmAddress,
                Network = "base-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.Holdings.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetPortfolioIntelligence_CorrelationIdPropagated()
        {
            const string correlationId = "test-correlation-466";
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                CorrelationId = correlationId
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task GetPortfolioIntelligence_NoCorrelationId_GeneratesOne()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                CorrelationId = null
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetPortfolioIntelligence_SchemaVersionPresent()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetPortfolioIntelligence_HoldingsHaveConfidenceIndicators()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            foreach (var holding in result.Holdings)
            {
                Assert.That(holding.ConfidenceIndicators.Count, Is.GreaterThan(0),
                    $"Holding {holding.AssetId} has no confidence indicators");
            }
        }

        [Test]
        public async Task GetPortfolioIntelligence_IncludeRiskDetailsFalse_StillReturnsHoldings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = false
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            // Should have at least the primary holding (BIATEC) even without risk details
            Assert.That(result.Holdings.Count, Is.GreaterThanOrEqualTo(1));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC2: Wallet compatibility – network / token standard checks
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void EvaluateWalletCompatibility_AlgorandAddress_AlgorandNetwork_Compatible()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "algorand-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.Compatible));
            Assert.That(message, Does.Contain("compatible"));
        }

        [Test]
        public void EvaluateWalletCompatibility_EvmAddress_EvmNetwork_Compatible()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                EvmAddress, "base-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.Compatible));
            Assert.That(message, Does.Contain("compatible"));
        }

        [Test]
        public void EvaluateWalletCompatibility_AlgorandAddress_EvmNetwork_NetworkMismatch()
        {
            var (status, _) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "base-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.NetworkMismatch));
        }

        [Test]
        public void EvaluateWalletCompatibility_EvmAddress_AlgorandNetwork_NetworkMismatch()
        {
            var (status, _) = _service.EvaluateWalletCompatibility(
                EvmAddress, "algorand-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.NetworkMismatch));
        }

        [Test]
        public void EvaluateWalletCompatibility_AlgorandAddress_EvmStandard_UnsupportedWalletType()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "base-mainnet", tokenStandard: "ERC20");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.UnsupportedWalletType));
            Assert.That(message, Does.Contain("ERC20"));
        }

        [Test]
        public void EvaluateWalletCompatibility_EvmAddress_AlgorandStandard_UnsupportedWalletType()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                EvmAddress, "algorand-mainnet", tokenStandard: "ASA");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.UnsupportedWalletType));
            Assert.That(message, Does.Contain("ASA"));
        }

        [Test]
        public void EvaluateWalletCompatibility_EmptyAddress_NotConnected()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(string.Empty, "algorand-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.NotConnected));
            Assert.That(message, Is.Not.Empty);
        }

        [Test]
        public void EvaluateWalletCompatibility_UnrecognizedAddress_NotConnected()
        {
            var (status, _) = _service.EvaluateWalletCompatibility("not_a_valid_address", "algorand-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.NotConnected));
        }

        [Test]
        public void EvaluateWalletCompatibility_NetworkMismatchMessage_IsActionable()
        {
            var (_, message) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "base-mainnet");

            // Message must tell user what to do
            Assert.That(message, Does.Contain("switch").IgnoreCase.Or.Contain("compatible").IgnoreCase);
        }

        [Test]
        public void EvaluateWalletCompatibility_AlgorandTestnet_Compatible()
        {
            var (status, _) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "algorand-testnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.Compatible));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1+AC4: Risk aggregation logic
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void AggregateRisk_AllLow_ReturnsLow()
        {
            var result = _service.AggregateRisk(new[]
            {
                HoldingRiskLevel.Low, HoldingRiskLevel.Low
            });

            Assert.That(result, Is.EqualTo(HoldingRiskLevel.Low));
        }

        [Test]
        public void AggregateRisk_AnyHigh_ReturnsHigh()
        {
            var result = _service.AggregateRisk(new[]
            {
                HoldingRiskLevel.Low, HoldingRiskLevel.High, HoldingRiskLevel.Medium
            });

            Assert.That(result, Is.EqualTo(HoldingRiskLevel.High));
        }

        [Test]
        public void AggregateRisk_MediumAndLow_ReturnsMedium()
        {
            var result = _service.AggregateRisk(new[]
            {
                HoldingRiskLevel.Low, HoldingRiskLevel.Medium
            });

            Assert.That(result, Is.EqualTo(HoldingRiskLevel.Medium));
        }

        [Test]
        public void AggregateRisk_AllUnknown_ReturnsUnknown()
        {
            var result = _service.AggregateRisk(new[]
            {
                HoldingRiskLevel.Unknown, HoldingRiskLevel.Unknown
            });

            Assert.That(result, Is.EqualTo(HoldingRiskLevel.Unknown));
        }

        [Test]
        public void AggregateRisk_EmptyList_ReturnsUnknown()
        {
            var result = _service.AggregateRisk(Enumerable.Empty<HoldingRiskLevel>());

            Assert.That(result, Is.EqualTo(HoldingRiskLevel.Unknown));
        }

        [Test]
        public void AggregateRisk_NullList_ReturnsUnknown()
        {
            var result = _service.AggregateRisk(null!);

            Assert.That(result, Is.EqualTo(HoldingRiskLevel.Unknown));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: Per-holding risk evaluation
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void EvaluateHoldingRisk_NoRiskFactors_ReturnsLowRisk()
        {
            var (risk, _, signals, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: true, isVerified: true);

            Assert.That(risk, Is.EqualTo(HoldingRiskLevel.Low));
            Assert.That(signals.Count, Is.EqualTo(0));
        }

        [Test]
        public void EvaluateHoldingRisk_MintAuthorityActive_AddsRiskSignal()
        {
            var (_, _, signals, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: true, metadataComplete: true, isVerified: true);

            Assert.That(signals.Any(s => s.SignalCode == "MINT_AUTHORITY_ACTIVE"), Is.True);
        }

        [Test]
        public void EvaluateHoldingRisk_IncompleteMetadata_AddsRiskSignal()
        {
            var (_, _, signals, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: false, isVerified: true);

            Assert.That(signals.Any(s => s.SignalCode == "METADATA_INCOMPLETE"), Is.True);
        }

        [Test]
        public void EvaluateHoldingRisk_AllRiskFactors_ElevatesRisk()
        {
            var (risk, _, _, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: true, metadataComplete: false, isVerified: false);

            Assert.That(risk, Is.AnyOf(HoldingRiskLevel.Medium, HoldingRiskLevel.High));
        }

        [Test]
        public void EvaluateHoldingRisk_AllPositiveFactors_HighConfidence()
        {
            var (_, confidence, _, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: true, isVerified: true);

            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.High));
        }

        [Test]
        public void EvaluateHoldingRisk_AllNegativeFactors_LowConfidence()
        {
            var (_, confidence, _, _) = _service.EvaluateHoldingRisk(
                0, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: false, isVerified: false);

            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.Low));
        }

        [Test]
        public void EvaluateHoldingRisk_ConfidenceIndicatorsAlwaysPresent()
        {
            var (_, _, _, indicators) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: true, isVerified: false);

            Assert.That(indicators.Count, Is.GreaterThan(0));
            Assert.That(indicators.All(i => !string.IsNullOrEmpty(i.Key)), Is.True);
            Assert.That(indicators.All(i => !string.IsNullOrEmpty(i.Description)), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC3: Error, loading, and empty states
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetPortfolioIntelligence_NullRequest_ReturnsDegradedResponse()
        {
            var result = await _service.GetPortfolioIntelligenceAsync(null!);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsDegraded, Is.True);
            Assert.That(result.DegradedSources, Is.Not.Empty);
        }

        [Test]
        public async Task GetPortfolioIntelligence_EmptyWalletAddress_ReturnsDegradedResponse()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = string.Empty,
                Network = "algorand-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.IsDegraded, Is.True);
            Assert.That(result.ActionReadiness, Is.EqualTo(ActionReadiness.NotReady));
        }

        [Test]
        public async Task GetPortfolioIntelligence_EmptyNetwork_ReturnsDegradedResponse()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = string.Empty
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.IsDegraded, Is.True);
        }

        [Test]
        public async Task GetPortfolioIntelligence_UnknownNetwork_ReturnsDegradedButNotEmpty()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "unknown-chain-xyz"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            // Must still return a response (degraded-mode) rather than throwing
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsDegraded, Is.True);
            Assert.That(result.DegradedSources, Contains.Item("NetworkRegistry"));
        }

        [Test]
        public async Task GetPortfolioIntelligence_AssetFilter_FiltersHoldings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                AssetFilter = new List<ulong> { 99999 } // non-existent asset
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.Holdings.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetPortfolioIntelligence_IncludeOpportunitiesFalse_NoOpportunities()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeOpportunities = false
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.Opportunities, Is.Empty);
        }

        [Test]
        public async Task GetPortfolioIntelligence_ResponseContainsEvaluatedAt()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.EvaluatedAt, Is.GreaterThan(before));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC4: Determinism – identical inputs produce identical outputs
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void EvaluateHoldingRisk_Deterministic_SameInputSameOutput()
        {
            var (risk1, conf1, sigs1, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet", true, false, true);
            var (risk2, conf2, sigs2, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet", true, false, true);

            Assert.That(risk1, Is.EqualTo(risk2));
            Assert.That(conf1, Is.EqualTo(conf2));
            Assert.That(sigs1.Count, Is.EqualTo(sigs2.Count));
        }

        [Test]
        public void AggregateRisk_Deterministic_SameInputSameOutput()
        {
            var input = new[] { HoldingRiskLevel.Low, HoldingRiskLevel.Medium };

            var result1 = _service.AggregateRisk(input);
            var result2 = _service.AggregateRisk(input);

            Assert.That(result1, Is.EqualTo(result2));
        }

        [Test]
        public void EvaluateWalletCompatibility_Deterministic_SameInputSameOutput()
        {
            var (s1, m1) = _service.EvaluateWalletCompatibility(AlgorandAddress, "algorand-mainnet");
            var (s2, m2) = _service.EvaluateWalletCompatibility(AlgorandAddress, "algorand-mainnet");

            Assert.That(s1, Is.EqualTo(s2));
            Assert.That(m1, Is.EqualTo(m2));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Summary field accuracy tests
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetPortfolioIntelligence_SummaryCounts_ConsistentWithHoldings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);
            var summary = result.Summary;

            int expectedTotal = result.Holdings.Count;
            int expectedHigh = result.Holdings.Count(h => h.RiskLevel == HoldingRiskLevel.High);
            int expectedMedium = result.Holdings.Count(h => h.RiskLevel == HoldingRiskLevel.Medium);
            int expectedLow = result.Holdings.Count(h => h.RiskLevel == HoldingRiskLevel.Low);

            Assert.That(summary.TotalHoldings, Is.EqualTo(expectedTotal));
            Assert.That(summary.HighRiskCount, Is.EqualTo(expectedHigh));
            Assert.That(summary.MediumRiskCount, Is.EqualTo(expectedMedium));
            Assert.That(summary.LowRiskCount, Is.EqualTo(expectedLow));
        }

        [Test]
        public async Task GetPortfolioIntelligence_ActionReadyCount_ConsistentWithHoldings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            int expectedActionReady = result.Holdings.Count(h => h.ActionReadiness == ActionReadiness.Ready);
            Assert.That(result.Summary.ActionReadyCount, Is.EqualTo(expectedActionReady));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Opportunity discovery tests
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetPortfolioIntelligence_OpportunitiesSortedByPriority()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true,
                IncludeOpportunities = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            if (result.Opportunities.Count > 1)
            {
                for (int i = 0; i < result.Opportunities.Count - 1; i++)
                {
                    Assert.That(result.Opportunities[i].Priority,
                        Is.GreaterThanOrEqualTo(result.Opportunities[i + 1].Priority),
                        "Opportunities must be sorted descending by priority");
                }
            }
        }

        [Test]
        public async Task GetPortfolioIntelligence_Opportunities_HaveRequiredFields()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeOpportunities = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            foreach (var opp in result.Opportunities)
            {
                Assert.That(opp.Title, Is.Not.Null.And.Not.Empty,
                    "Opportunity must have a title");
                Assert.That(opp.Description, Is.Not.Null.And.Not.Empty,
                    "Opportunity must have a description");
                Assert.That(opp.CallToAction, Is.Not.Null.And.Not.Empty,
                    "Opportunity must have a call-to-action");
            }
        }
    }
}

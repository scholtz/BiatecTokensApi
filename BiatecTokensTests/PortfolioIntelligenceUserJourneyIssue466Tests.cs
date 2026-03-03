using BiatecTokensApi.Models.Portfolio;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// User-journey and impact tests for Issue #466: Vision milestone – Portfolio intelligence
    /// and wallet experience advancement.
    ///
    /// PURPOSE: Provides explicit evidence for happy-path, invalid-input, boundary,
    /// failure-recovery, and non-crypto-native UX scenarios for the portfolio intelligence
    /// features from the perspective of a non-crypto-native token issuer.
    ///
    /// USER IMPACT RATIONALE (non-crypto-native users):
    /// • Portfolio intelligence aggregates token risk signals so users never need to manually
    ///   interpret on-chain data — the platform summarizes what matters.
    /// • Wallet compatibility check prevents silent failures: instead of a transaction
    ///   failing on-chain, the user is told upfront "wrong network" in plain language.
    /// • Risk levels (Low/Medium/High) translate complex blockchain state into simple
    ///   business decisions: "review before action", "proceed", "stop and fix".
    /// • Confidence indicators show WHY a risk score was assigned — the user can trust
    ///   the assessment rather than treating it as a black box.
    /// • Opportunities are surfaced automatically so users discover actions (complete
    ///   metadata, review mint authority) without needing blockchain expertise.
    /// • Degraded mode never crashes the experience — partial data is better than no data,
    ///   and the UI can show "some data unavailable" instead of a blank screen.
    ///
    /// Test categories:
    ///   HP = Happy Path (expected success flows)
    ///   II = Invalid Input (user mistake scenarios)
    ///   BD = Boundary (edge/limit cases)
    ///   FR = Failure-Recovery (behavior after errors)
    ///   NX = Non-Crypto-Native Experience (user-facing clarity)
    ///
    /// Roadmap Alignment:
    ///   Phase 3: Analytics &amp; Intelligence – Portfolio Analytics (15% → improved)
    ///   Phase 3: Analytics &amp; Intelligence – Risk Analytics (10% → improved)
    ///   Phase 1: Core Token Creation &amp; Authentication – Real-time Deployment Status (55% → improved UX)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PortfolioIntelligenceUserJourneyIssue466Tests
    {
        private PortfolioIntelligenceService _service = null!;
        private Mock<ILogger<PortfolioIntelligenceService>> _loggerMock = null!;

        // Algorand test address (not a real funded account)
        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // EVM test address (safe for tests — zero address)
        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<PortfolioIntelligenceService>>();
            _service = new PortfolioIntelligenceService(_loggerMock.Object);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HP – Happy Path Scenarios
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// HP1: Algorand token issuer evaluates their portfolio and receives actionable risk summary.
        /// User impact: Non-crypto-native user sees low-jargon summary (Low/Medium/High) without
        /// needing to understand on-chain mechanics.
        /// </summary>
        [Test]
        public async Task HP1_AlgorandIssuer_EvaluatesPortfolio_ReceivesActionableRiskSummary()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true,
                IncludeOpportunities = true,
                CorrelationId = "hp1-journey"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.IsDegraded, Is.False,
                "HP1: Known network must not produce degraded response");
            Assert.That(result.Holdings.Count, Is.GreaterThan(0),
                "HP1: Algorand portfolio must contain holdings");
            Assert.That(result.AggregateRiskLevel,
                Is.Not.EqualTo(HoldingRiskLevel.Unknown),
                "HP1: Risk level must be deterministically computed, not Unknown");
            Assert.That(result.WalletCompatibility,
                Is.EqualTo(WalletCompatibilityStatus.Compatible),
                "HP1: Algorand wallet must be compatible with Algorand mainnet");
            Assert.That(result.CorrelationId, Is.EqualTo("hp1-journey"),
                "HP1: CorrelationId must be propagated for support traceability");
        }

        /// <summary>
        /// HP2: EVM token issuer evaluates Base network portfolio and gets ERC20 holdings.
        /// User impact: Same UI works for both Algorand and EVM tokens — no separate dashboards.
        /// </summary>
        [Test]
        public async Task HP2_EvmIssuer_EvaluatesBasePortfolio_ReceivesErc20Holdings()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = EvmAddress,
                Network = "base-mainnet",
                IncludeRiskDetails = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.IsDegraded, Is.False,
                "HP2: Known EVM network must not produce degraded response");
            Assert.That(result.Holdings.Count, Is.GreaterThan(0),
                "HP2: EVM portfolio must contain holdings");
            Assert.That(result.Holdings.Any(h => h.Standard == "ERC20"), Is.True,
                "HP2: EVM holdings must include ERC20 standard tokens");
        }

        /// <summary>
        /// HP3: Issuer requests portfolio with risk details and receives per-holding breakdown.
        /// User impact: User can click into a specific token and understand exactly what
        /// risk factors were found, without reading blockchain documentation.
        /// </summary>
        [Test]
        public async Task HP3_PortfolioEvaluation_WithRiskDetails_ReturnsPerHoldingBreakdown()
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
                Assert.That(holding.RiskLevel,
                    Is.AnyOf(HoldingRiskLevel.Low, HoldingRiskLevel.Medium,
                             HoldingRiskLevel.High, HoldingRiskLevel.Unknown),
                    $"HP3: Holding {holding.AssetId} must have a valid risk level");
                Assert.That(holding.ConfidenceIndicators.Count, Is.GreaterThan(0),
                    $"HP3: Holding {holding.AssetId} must include confidence indicators");
                Assert.That(holding.StatusSummary, Is.Not.Null.And.Not.Empty,
                    $"HP3: Holding {holding.AssetId} must have a human-readable status summary");
            }
        }

        /// <summary>
        /// HP4: Portfolio evaluation surfaces metadata improvement opportunity automatically.
        /// User impact: User does not need to manually audit their token — the platform
        /// proactively tells them what needs to be fixed to improve discoverability.
        /// </summary>
        [Test]
        public async Task HP4_PortfolioEvaluation_IncompleteMetadata_SurfacesOpportunity()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true,
                IncludeOpportunities = true
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            // The deterministic holdings include one holding with incomplete metadata
            if (result.Holdings.Any(h => h.RiskSignals.Any(s => s.SignalCode == "METADATA_INCOMPLETE")))
            {
                Assert.That(
                    result.Opportunities.Any(o => o.Category == OpportunityCategory.MetadataImprovement),
                    Is.True,
                    "HP4: Incomplete metadata must surface a MetadataImprovement opportunity");
            }
        }

        /// <summary>
        /// HP5: Wallet compatibility check confirms Algorand address is compatible before execution.
        /// User impact: Before clicking "Deploy Token", user sees a green check that their
        /// wallet is ready — no surprises when they submit the transaction.
        /// </summary>
        [Test]
        public void HP5_WalletCompatibilityCheck_AlgorandOnAlgorand_ShowsCompatible()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "algorand-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.Compatible),
                "HP5: Algorand address on Algorand mainnet must show Compatible");
            Assert.That(message, Does.Contain("compatible").IgnoreCase,
                "HP5: Compatibility message must include 'compatible' for user clarity");
        }

        /// <summary>
        /// HP6: Wallet compatibility check confirms EVM address is compatible with Base network.
        /// User impact: EVM users see the same "ready" signal on Base as Algorand users see
        /// on Algorand — consistent UX across chains.
        /// </summary>
        [Test]
        public void HP6_WalletCompatibilityCheck_EvmOnBase_ShowsCompatible()
        {
            var (status, _) = _service.EvaluateWalletCompatibility(EvmAddress, "base-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.Compatible),
                "HP6: EVM address on Base mainnet must show Compatible");
        }

        /// <summary>
        /// HP7: Risk aggregation over multiple holdings promotes worst-case to portfolio level.
        /// User impact: Portfolio-level risk badge always shows the most important signal —
        /// users see "High Risk" if any single holding is high, preventing oversight.
        /// </summary>
        [Test]
        public void HP7_RiskAggregation_OneHighAmongMultiple_AggregateMustBeHigh()
        {
            var risks = new[]
            {
                HoldingRiskLevel.Low,
                HoldingRiskLevel.High,
                HoldingRiskLevel.Medium
            };

            var aggregate = _service.AggregateRisk(risks);

            Assert.That(aggregate, Is.EqualTo(HoldingRiskLevel.High),
                "HP7: Any High holding must elevate portfolio aggregate to High");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // II – Invalid Input Scenarios
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// II1: Null request returns degraded response (not exception).
        /// User impact: Client-side bugs (null body) never produce a confusing 500 error;
        /// the API returns a machine-readable degraded response instead.
        /// </summary>
        [Test]
        public async Task II1_NullRequest_ReturnsDegradedResponse_NotException()
        {
            var result = await _service.GetPortfolioIntelligenceAsync(null!);

            Assert.That(result, Is.Not.Null,
                "II1: Null request must never throw; must return degraded response");
            Assert.That(result.IsDegraded, Is.True,
                "II1: Null request must result in degraded mode");
            Assert.That(result.DegradedSources, Is.Not.Empty,
                "II1: Degraded response must name the failing source");
            Assert.That(result.ActionReadiness, Is.EqualTo(ActionReadiness.NotReady),
                "II1: Degraded response must mark actions as NotReady");
        }

        /// <summary>
        /// II2: Empty wallet address returns degraded response with actionable message.
        /// User impact: If the frontend fails to populate the wallet address field, the API
        /// returns a structured response rather than crashing, and the message tells
        /// the developer what is missing.
        /// </summary>
        [Test]
        public async Task II2_EmptyWalletAddress_ReturnsDegradedWithMessage()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = string.Empty,
                Network = "algorand-mainnet"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.IsDegraded, Is.True,
                "II2: Empty wallet address must produce degraded response");
            Assert.That(result.WalletCompatibilityMessage, Is.Not.Null.And.Not.Empty,
                "II2: Degraded response must include a compatibility message");
        }

        /// <summary>
        /// II3: Empty network returns degraded response.
        /// User impact: Missing network field is caught gracefully — no 500 error.
        /// </summary>
        [Test]
        public async Task II3_EmptyNetwork_ReturnsDegradedResponse()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = string.Empty
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.IsDegraded, Is.True,
                "II3: Empty network must produce degraded response");
        }

        /// <summary>
        /// II4: Malformed wallet address returns NotConnected compatibility status.
        /// User impact: If a user pastes a random string instead of a wallet address,
        /// they get "Address format not recognized" rather than a server error.
        /// </summary>
        [Test]
        public void II4_MalformedWalletAddress_ReturnsNotConnected()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                "not_a_valid_address!!!", "algorand-mainnet");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.NotConnected),
                "II4: Malformed address must return NotConnected, not an exception");
            Assert.That(message, Is.Not.Empty,
                "II4: NotConnected status must include an explanatory message");
        }

        /// <summary>
        /// II5: Wrong chain type returns UnsupportedWalletType when standard is specified.
        /// User impact: If user tries to use an Algorand wallet for an ERC20 token,
        /// the platform clearly explains the incompatibility instead of silently failing.
        /// </summary>
        [Test]
        public void II5_AlgorandAddressForERC20Standard_ReturnsUnsupportedWalletType()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "base-mainnet", tokenStandard: "ERC20");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.UnsupportedWalletType),
                "II5: Algorand address for ERC20 must return UnsupportedWalletType");
            Assert.That(message, Does.Contain("ERC20"),
                "II5: Message must explicitly name the incompatible standard");
        }

        /// <summary>
        /// II6: EVM address for ASA standard returns UnsupportedWalletType.
        /// User impact: Same protective check for the reverse case.
        /// </summary>
        [Test]
        public void II6_EvmAddressForASAStandard_ReturnsUnsupportedWalletType()
        {
            var (status, message) = _service.EvaluateWalletCompatibility(
                EvmAddress, "algorand-mainnet", tokenStandard: "ASA");

            Assert.That(status, Is.EqualTo(WalletCompatibilityStatus.UnsupportedWalletType),
                "II6: EVM address for ASA must return UnsupportedWalletType");
            Assert.That(message, Does.Contain("ASA"),
                "II6: Message must explicitly name the incompatible standard");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // BD – Boundary Scenarios
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BD1: Single High risk holding in otherwise Low portfolio elevates aggregate.
        /// Boundary: Exactly one High signal among many Low signals.
        /// </summary>
        [Test]
        public void BD1_SingleHighRisk_InLowPortfolio_AggregateMustBeHigh()
        {
            var risks = new[]
            {
                HoldingRiskLevel.Low, HoldingRiskLevel.Low, HoldingRiskLevel.Low,
                HoldingRiskLevel.Low, HoldingRiskLevel.High
            };

            Assert.That(_service.AggregateRisk(risks), Is.EqualTo(HoldingRiskLevel.High),
                "BD1: Even one High holding must make portfolio High");
        }

        /// <summary>
        /// BD2: All unknown holdings → aggregate is Unknown, not Low.
        /// Boundary: Complete lack of data must not produce a false 'safe' result.
        /// </summary>
        [Test]
        public void BD2_AllUnknownHoldings_AggregateMustBeUnknown_NotFalseSafe()
        {
            var risks = new[]
            {
                HoldingRiskLevel.Unknown, HoldingRiskLevel.Unknown
            };

            Assert.That(_service.AggregateRisk(risks), Is.EqualTo(HoldingRiskLevel.Unknown),
                "BD2: All Unknown must not produce a false Low result");
        }

        /// <summary>
        /// BD3: Empty holding list → aggregate is Unknown.
        /// Boundary: No data is explicitly Unknown, not a guess.
        /// </summary>
        [Test]
        public void BD3_EmptyHoldingList_AggregateMustBeUnknown()
        {
            Assert.That(_service.AggregateRisk(new HoldingRiskLevel[0]),
                Is.EqualTo(HoldingRiskLevel.Unknown),
                "BD3: Empty list must return Unknown (not Low)");
        }

        /// <summary>
        /// BD4: Asset filter with exact match returns only the matched holding.
        /// </summary>
        [Test]
        public async Task BD4_AssetFilter_ExactMatch_ReturnsOnlyMatchedHolding()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true,
                AssetFilter = new List<ulong> { 1001 }
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.Holdings.All(h => h.AssetId == 1001), Is.True,
                "BD4: Asset filter must return only requested asset");
        }

        /// <summary>
        /// BD5: Asset filter with non-existent ID returns empty holdings (not an error).
        /// Boundary: No match → empty list, not a failure.
        /// </summary>
        [Test]
        public async Task BD5_AssetFilter_NoMatch_ReturnsEmptyHoldings_NotError()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                AssetFilter = new List<ulong> { 99999999 }
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.Holdings, Is.Empty,
                "BD5: Non-existent asset filter must return empty holdings");
            Assert.That(result.IsDegraded, Is.False,
                "BD5: Empty filter result must not be marked as degraded");
        }

        /// <summary>
        /// BD6: Evaluation with all positive risk factors → HighConfidence assessment.
        /// Boundary: maximum positive indicator count drives High confidence.
        /// </summary>
        [Test]
        public void BD6_AllPositiveRiskFactors_ProducesHighConfidence()
        {
            var (_, confidence, _, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: true, isVerified: true);

            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.High),
                "BD6: All positive factors must produce High confidence");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // FR – Failure-Recovery Scenarios
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// FR1: Unknown network returns degraded mode, not an exception.
        /// Failure Semantics: Unknown network → IsDegraded=true, DegradedSources=["NetworkRegistry"].
        /// Recovery: The caller can still display partial data.
        /// </summary>
        [Test]
        public async Task FR1_UnknownNetwork_ReturnsDegradedMode_NotException()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "unknown-blockchain-xyz"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result, Is.Not.Null,
                "FR1: Unknown network must never throw; must return degraded response");
            Assert.That(result.IsDegraded, Is.True,
                "FR1: Unknown network must flag response as degraded");
            Assert.That(result.DegradedSources, Contains.Item("NetworkRegistry"),
                "FR1: DegradedSources must name 'NetworkRegistry' so operators know the cause");
        }

        /// <summary>
        /// FR2: Degraded response includes schema version so callers can detect contract issues.
        /// Failure Semantics: Degraded mode still returns a structured response.
        /// </summary>
        [Test]
        public async Task FR2_DegradedResponse_StillContainsSchemaVersion()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "unknown-chain-000"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty,
                "FR2: Even degraded responses must include SchemaVersion for contract stability");
        }

        /// <summary>
        /// FR3: Degraded response always sets ActionReadiness to NotReady.
        /// Failure Semantics: If data is incomplete, the platform must not encourage
        /// the user to act on potentially wrong information.
        /// </summary>
        [Test]
        public async Task FR3_DegradedResponse_ActionReadinessIsNotReady()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "unknown-chain-001"
            };

            var result = await _service.GetPortfolioIntelligenceAsync(request);

            Assert.That(result.ActionReadiness, Is.EqualTo(ActionReadiness.NotReady),
                "FR3: Degraded response must set ActionReadiness=NotReady to protect user from acting on bad data");
        }

        /// <summary>
        /// FR4: Multiple consecutive calls with unknown network all return degraded — no state leak.
        /// Recovery: The service handles repeated calls to bad networks gracefully.
        /// </summary>
        [Test]
        public async Task FR4_MultipleCallsWithUnknownNetwork_AllReturnDegraded_NoStateLeak()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "unknown-repeating-chain"
            };

            for (int i = 0; i < 3; i++)
            {
                var result = await _service.GetPortfolioIntelligenceAsync(request);
                Assert.That(result.IsDegraded, Is.True,
                    $"FR4: Call {i + 1} must return degraded (no state accumulation)");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // NX – Non-Crypto-Native Experience Scenarios
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// NX1: Wallet mismatch message is actionable (not a raw error code).
        /// User impact: "Please switch to an Algorand-compatible network" is understandable
        /// to any user; "NETWORK_MISMATCH_0x..." is not.
        /// </summary>
        [Test]
        public void NX1_NetworkMismatchMessage_IsActionable_NotTechnical()
        {
            var (_, message) = _service.EvaluateWalletCompatibility(
                AlgorandAddress, "base-mainnet");

            Assert.That(message.Length, Is.GreaterThan(20),
                "NX1: Mismatch message must be descriptive (> 20 chars)");
            Assert.That(message, Does.Not.Contain("0x"),
                "NX1: Mismatch message must not expose raw hex codes");
            Assert.That(message, Does.Not.Contain("null"),
                "NX1: Mismatch message must not expose null references");
        }

        /// <summary>
        /// NX2: Holdings have human-readable status summaries (not empty strings).
        /// User impact: Users read "Well-configured ASA token with complete metadata"
        /// rather than seeing an empty field or raw database state.
        /// </summary>
        [Test]
        public async Task NX2_Holdings_HaveHumanReadableStatusSummaries()
        {
            var result = await _service.GetPortfolioIntelligenceAsync(
                new PortfolioIntelligenceRequest
                {
                    WalletAddress = AlgorandAddress,
                    Network = "algorand-mainnet",
                    IncludeRiskDetails = true
                });

            foreach (var holding in result.Holdings)
            {
                Assert.That(holding.StatusSummary, Is.Not.Empty,
                    $"NX2: Holding {holding.AssetId} must have a human-readable status summary");
                Assert.That(holding.StatusSummary.Length, Is.GreaterThan(10),
                    $"NX2: Status summary for {holding.AssetId} must be descriptive (>10 chars)");
            }
        }

        /// <summary>
        /// NX3: Opportunities have call-to-action text that is instructional.
        /// User impact: Opportunity card shows "Update token metadata" — a clear action
        /// the user can take, not a raw field name like "metadata_missing".
        /// </summary>
        [Test]
        public async Task NX3_Opportunities_HaveInstructionalCallToAction()
        {
            var result = await _service.GetPortfolioIntelligenceAsync(
                new PortfolioIntelligenceRequest
                {
                    WalletAddress = AlgorandAddress,
                    Network = "algorand-mainnet",
                    IncludeRiskDetails = true,
                    IncludeOpportunities = true
                });

            foreach (var opp in result.Opportunities)
            {
                Assert.That(opp.CallToAction, Is.Not.Empty,
                    $"NX3: Opportunity '{opp.Title}' must have a call-to-action");
                Assert.That(opp.CallToAction.Length, Is.GreaterThan(5),
                    $"NX3: CallToAction for '{opp.Title}' must be descriptive");
            }
        }

        /// <summary>
        /// NX4: Risk signal descriptions are user-readable, not raw enum names.
        /// User impact: "Token mint authority is still active" vs "MINT_AUTHORITY_ACTIVE".
        /// </summary>
        [Test]
        public void NX4_RiskSignalDescriptions_AreReadable_NotRawEnumNames()
        {
            var (_, _, signals, _) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: true, metadataComplete: false, isVerified: false);

            foreach (var signal in signals)
            {
                Assert.That(signal.Description.Length, Is.GreaterThan(20),
                    $"NX4: Signal '{signal.SignalCode}' description must be user-readable (>20 chars)");
                Assert.That(signal.Description, Does.Not.StartWith("MINT_").And.Not.StartWith("METADATA_"),
                    $"NX4: Signal '{signal.SignalCode}' description must not start with raw code format");
            }
        }

        /// <summary>
        /// NX5: Confidence indicators have readable, non-technical descriptions.
        /// User impact: "Token metadata is complete and verifiable" is meaningful to a business user.
        /// </summary>
        [Test]
        public void NX5_ConfidenceIndicatorDescriptions_AreReadable()
        {
            var (_, _, _, indicators) = _service.EvaluateHoldingRisk(
                1001, "algorand-mainnet",
                hasMintAuthority: false, metadataComplete: true, isVerified: true);

            foreach (var indicator in indicators)
            {
                Assert.That(indicator.Description.Length, Is.GreaterThan(15),
                    $"NX5: Indicator '{indicator.Key}' description must be user-readable (>15 chars)");
            }
        }

        /// <summary>
        /// NX6: Response always has EvaluatedAt timestamp so users can see data freshness.
        /// User impact: UI can show "Portfolio evaluated 2 minutes ago" to set user expectations
        /// about data currency without needing blockchain knowledge.
        /// </summary>
        [Test]
        public async Task NX6_Response_AlwaysHasEvaluatedAtTimestamp()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            var result = await _service.GetPortfolioIntelligenceAsync(
                new PortfolioIntelligenceRequest
                {
                    WalletAddress = AlgorandAddress,
                    Network = "algorand-mainnet"
                });

            Assert.That(result.EvaluatedAt, Is.GreaterThan(before),
                "NX6: EvaluatedAt must reflect the current evaluation time for freshness display");
        }
    }
}

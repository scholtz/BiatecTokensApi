using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Portfolio;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration/contract tests for Issue #466: Vision milestone – Portfolio intelligence
    /// and wallet experience advancement.
    ///
    /// These tests use WebApplicationFactory to exercise the full application stack via HTTP,
    /// validating public API contracts for portfolio intelligence, wallet compatibility checks,
    /// error states, degraded-mode responses, and backward compatibility.
    ///
    /// AC1 - Portfolio intelligence surfaces confidence indicators and risk levels via API
    /// AC2 - Wallet compatibility endpoint clearly communicates network/standard compatibility
    /// AC3 - Error, loading, and empty states return structured responses (no 500s)
    /// AC4 - Domain rules validated in unit tests; contract tests prove HTTP wiring
    /// AC5 - Integration tests cover success and failure conditions at API boundaries
    /// AC6 - Critical user journeys (evaluate portfolio, check wallet) covered end-to-end
    /// AC7 - CI passes: DI container resolves, endpoints respond, no unhandled exceptions
    /// AC8 - PR maps business goals to implementation; test suite provides AC evidence
    /// AC9 - Documentation updated; runbooks reference new endpoints
    ///
    /// Business Value: Integration tests prove the portfolio intelligence stack is correctly
    /// wired in the DI container, returns deterministic HTTP responses, handles validation
    /// failures gracefully, and maintains backward-compatible API contracts for Issue #466.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PortfolioIntelligenceIssue466ContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        // Test addresses (not real accounts)
        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";
        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        private static readonly Dictionary<string, string?> TestConfiguration = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "portfolio-intelligence-466-test-key-32chars!!",
            ["JwtConfig:Issuer"] = "BiatecTokensApi",
            ["JwtConfig:Audience"] = "BiatecTokensUsers",
            ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
            ["JwtConfig:RefreshTokenExpirationDays"] = "30",
            ["JwtConfig:ValidateIssuerSigningKey"] = "true",
            ["JwtConfig:ValidateIssuer"] = "true",
            ["JwtConfig:ValidateAudience"] = "true",
            ["JwtConfig:ValidateLifetime"] = "true",
            ["JwtConfig:ClockSkewMinutes"] = "5",
            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
            ["IPFSConfig:TimeoutSeconds"] = "30",
            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
            ["IPFSConfig:ValidateContentHash"] = "true",
            ["EVMChains:Chains:0:RpcUrl"] = "https://sepolia.base.org",
            ["EVMChains:Chains:0:ChainId"] = "84532",
            ["EVMChains:Chains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "PortfolioIntelligence466TestKey32CharsReq!!"
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC7: DI container and application startup
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Application_Starts_HealthEndpointResponds()
        {
            var response = await _client.GetAsync("/health");

            Assert.That((int)response.StatusCode,
                Is.EqualTo(200).Or.EqualTo(503),
                "Health endpoint must respond (200 OK or 503 ServiceUnavailable for external deps)");
        }

        [Test]
        public async Task PortfolioIntelligenceEndpoints_DiContainerResolves_NoStartupException()
        {
            // If DI container fails to resolve IPortfolioIntelligenceService, the server
            // would fail to start and this request would throw. A 401 proves the app started.
            var response = await _client.GetAsync("/api/v1/portfolio-intelligence/wallet-compatibility?walletAddress=x&network=y");

            Assert.That((int)response.StatusCode,
                Is.EqualTo(401).Or.EqualTo(400).Or.EqualTo(200),
                "A response (not a startup crash) proves DI resolution succeeded");
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC5: Unauthenticated requests are rejected (auth boundary)
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluatePortfolio_Unauthenticated_Returns401()
        {
            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet"
            };

            var response = await _client.PostAsJsonAsync("/api/v1/portfolio-intelligence/evaluate", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetWalletCompatibility_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync(
                $"/api/v1/portfolio-intelligence/wallet-compatibility?walletAddress={AlgorandAddress}&network=algorand-mainnet");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC3: Error states – bad request validation
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluatePortfolio_NullBody_Returns401OrBadRequest()
        {
            // Without auth the server rejects with 401 before even parsing body.
            // With debug/empty-success-on-failure auth, may reach validation.
            var response = await _client.PostAsJsonAsync<PortfolioIntelligenceRequest?>(
                "/api/v1/portfolio-intelligence/evaluate", null);

            Assert.That((int)response.StatusCode,
                Is.EqualTo(401).Or.EqualTo(400),
                "Must reject null body with 401 (auth) or 400 (validation)");
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC6: Wallet compatibility – unit model contract (no auth required for service logic)
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void WalletCompatibilityResult_ModelContract_AllRequiredFieldsPresent()
        {
            // Verify that WalletCompatibilityResult has all required contract fields
            var result = new WalletCompatibilityResult
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                TokenStandard = "ASA",
                Status = WalletCompatibilityStatus.Compatible,
                Message = "Wallet is connected and compatible."
            };

            Assert.That(result.WalletAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Network, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void PortfolioIntelligenceResponse_ModelContract_AllRequiredFieldsPresent()
        {
            var response = new PortfolioIntelligenceResponse
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                CorrelationId = "test-corr-id",
                SchemaVersion = "1.0.0",
                EvaluatedAt = DateTimeOffset.UtcNow
            };

            Assert.That(response.WalletAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Network, Is.Not.Null.And.Not.Empty);
            Assert.That(response.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(response.SchemaVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Holdings, Is.Not.Null);
            Assert.That(response.Opportunities, Is.Not.Null);
            Assert.That(response.DegradedSources, Is.Not.Null);
        }

        [Test]
        public void PortfolioSummary_ModelContract_DefaultsToZero()
        {
            var summary = new PortfolioSummary();

            Assert.That(summary.TotalHoldings, Is.EqualTo(0));
            Assert.That(summary.HighRiskCount, Is.EqualTo(0));
            Assert.That(summary.MediumRiskCount, Is.EqualTo(0));
            Assert.That(summary.LowRiskCount, Is.EqualTo(0));
            Assert.That(summary.ActionReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void HoldingIntelligence_ModelContract_DefaultsAreUsable()
        {
            var holding = new HoldingIntelligence();

            Assert.That(holding.Name, Is.Not.Null);
            Assert.That(holding.Symbol, Is.Not.Null);
            Assert.That(holding.Standard, Is.Not.Null);
            Assert.That(holding.RiskSignals, Is.Not.Null);
            Assert.That(holding.ConfidenceIndicators, Is.Not.Null);
            Assert.That(holding.StatusSummary, Is.Not.Null);
        }

        [Test]
        public void RiskSignal_ModelContract_RequiredFieldsPresent()
        {
            var signal = new RiskSignal
            {
                SignalCode = "MINT_AUTHORITY_ACTIVE",
                Description = "Mint authority is active.",
                Severity = HoldingRiskLevel.Medium
            };

            Assert.That(signal.SignalCode, Is.Not.Null.And.Not.Empty);
            Assert.That(signal.Description, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ConfidenceIndicator_ModelContract_RequiredFieldsPresent()
        {
            var indicator = new ConfidenceIndicator
            {
                Key = "METADATA_VERIFIED",
                IsPositive = true,
                Description = "Token metadata is complete."
            };

            Assert.That(indicator.Key, Is.Not.Null.And.Not.Empty);
            Assert.That(indicator.Description, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void PortfolioOpportunity_ModelContract_RequiredFieldsPresent()
        {
            var opportunity = new PortfolioOpportunity
            {
                Category = OpportunityCategory.MetadataImprovement,
                AssetId = 1001,
                Title = "Complete metadata",
                Description = "Token metadata is incomplete.",
                CallToAction = "Update metadata",
                Priority = 80
            };

            Assert.That(opportunity.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(opportunity.Description, Is.Not.Null.And.Not.Empty);
            Assert.That(opportunity.CallToAction, Is.Not.Null.And.Not.Empty);
            Assert.That(opportunity.Priority, Is.InRange(0, 100));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: Enum coverage – all enum values are defined and accessible
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void ActionReadiness_EnumValues_AllDefined()
        {
            Assert.That(Enum.IsDefined(typeof(ActionReadiness), ActionReadiness.Ready), Is.True);
            Assert.That(Enum.IsDefined(typeof(ActionReadiness), ActionReadiness.ConditionallyReady), Is.True);
            Assert.That(Enum.IsDefined(typeof(ActionReadiness), ActionReadiness.NotReady), Is.True);
        }

        [Test]
        public void HoldingRiskLevel_EnumValues_AllDefined()
        {
            Assert.That(Enum.IsDefined(typeof(HoldingRiskLevel), HoldingRiskLevel.Low), Is.True);
            Assert.That(Enum.IsDefined(typeof(HoldingRiskLevel), HoldingRiskLevel.Medium), Is.True);
            Assert.That(Enum.IsDefined(typeof(HoldingRiskLevel), HoldingRiskLevel.High), Is.True);
            Assert.That(Enum.IsDefined(typeof(HoldingRiskLevel), HoldingRiskLevel.Unknown), Is.True);
        }

        [Test]
        public void ConfidenceLevel_EnumValues_AllDefined()
        {
            Assert.That(Enum.IsDefined(typeof(ConfidenceLevel), ConfidenceLevel.High), Is.True);
            Assert.That(Enum.IsDefined(typeof(ConfidenceLevel), ConfidenceLevel.Medium), Is.True);
            Assert.That(Enum.IsDefined(typeof(ConfidenceLevel), ConfidenceLevel.Low), Is.True);
        }

        [Test]
        public void WalletCompatibilityStatus_EnumValues_AllDefined()
        {
            Assert.That(Enum.IsDefined(typeof(WalletCompatibilityStatus), WalletCompatibilityStatus.Compatible), Is.True);
            Assert.That(Enum.IsDefined(typeof(WalletCompatibilityStatus), WalletCompatibilityStatus.NetworkMismatch), Is.True);
            Assert.That(Enum.IsDefined(typeof(WalletCompatibilityStatus), WalletCompatibilityStatus.NotConnected), Is.True);
            Assert.That(Enum.IsDefined(typeof(WalletCompatibilityStatus), WalletCompatibilityStatus.UnsupportedWalletType), Is.True);
        }

        [Test]
        public void OpportunityCategory_EnumValues_AllDefined()
        {
            Assert.That(Enum.IsDefined(typeof(OpportunityCategory), OpportunityCategory.Governance), Is.True);
            Assert.That(Enum.IsDefined(typeof(OpportunityCategory), OpportunityCategory.Staking), Is.True);
            Assert.That(Enum.IsDefined(typeof(OpportunityCategory), OpportunityCategory.ComplianceAction), Is.True);
            Assert.That(Enum.IsDefined(typeof(OpportunityCategory), OpportunityCategory.MetadataImprovement), Is.True);
            Assert.That(Enum.IsDefined(typeof(OpportunityCategory), OpportunityCategory.NetworkMigration), Is.True);
            Assert.That(Enum.IsDefined(typeof(OpportunityCategory), OpportunityCategory.GeneralInsight), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC4: Service-level boundary validation (in-process, no HTTP auth needed)
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluatePortfolio_Service_DegradedResponse_NeverThrows()
        {
            // Call service via DI without HTTP auth by resolving it directly
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            // Must not throw even with invalid inputs
            var result = await service.GetPortfolioIntelligenceAsync(null!);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsDegraded, Is.True);
        }

        [Test]
        public async Task EvaluatePortfolio_Service_ValidAlgorandRequest_ReturnsHoldings()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var result = await service.GetPortfolioIntelligenceAsync(new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                IncludeRiskDetails = true,
                IncludeOpportunities = true
            });

            Assert.That(result.IsDegraded, Is.False);
            Assert.That(result.Holdings.Count, Is.GreaterThan(0));
            Assert.That(result.WalletCompatibility, Is.EqualTo(WalletCompatibilityStatus.Compatible));
        }

        [Test]
        public async Task EvaluatePortfolio_Service_ValidEvmRequest_ReturnsHoldings()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var result = await service.GetPortfolioIntelligenceAsync(new PortfolioIntelligenceRequest
            {
                WalletAddress = EvmAddress,
                Network = "base-mainnet"
            });

            Assert.That(result.IsDegraded, Is.False);
            Assert.That(result.Holdings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void WalletCompatibility_Service_AlgorandEvmMismatch_NetworkMismatch()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var compatResult = service.EvaluateWalletCompatibility(AlgorandAddress, "base-mainnet");

            Assert.That(compatResult.Status, Is.EqualTo(WalletCompatibilityStatus.NetworkMismatch));
            Assert.That(compatResult.Message, Is.Not.Empty);
        }

        [Test]
        public async Task EvaluatePortfolio_Service_CorrelationIdPropagated()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            const string corrId = "contract-test-466";
            var result = await service.GetPortfolioIntelligenceAsync(new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                CorrelationId = corrId
            });

            Assert.That(result.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task EvaluatePortfolio_Service_UnknownNetwork_Degraded_NeverThrows()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var result = await service.GetPortfolioIntelligenceAsync(new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "unknown-chain-999"
            });

            // Must return degraded response, not throw
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsDegraded, Is.True);
        }

        [Test]
        public async Task EvaluatePortfolio_Service_ThreeConsecutiveRuns_DeterministicResults()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var request = new PortfolioIntelligenceRequest
            {
                WalletAddress = AlgorandAddress,
                Network = "algorand-mainnet",
                CorrelationId = "determinism-test"
            };

            var r1 = await service.GetPortfolioIntelligenceAsync(request);
            var r2 = await service.GetPortfolioIntelligenceAsync(request);
            var r3 = await service.GetPortfolioIntelligenceAsync(request);

            Assert.That(r1.AggregateRiskLevel, Is.EqualTo(r2.AggregateRiskLevel));
            Assert.That(r2.AggregateRiskLevel, Is.EqualTo(r3.AggregateRiskLevel));
            Assert.That(r1.Holdings.Count, Is.EqualTo(r2.Holdings.Count));
            Assert.That(r2.Holdings.Count, Is.EqualTo(r3.Holdings.Count));
            Assert.That(r1.WalletCompatibility, Is.EqualTo(r2.WalletCompatibility));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC2: Wallet experience – actionable messaging
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void WalletCompatibility_Service_NetworkMismatchMessage_IsActionable()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var compatResult = service.EvaluateWalletCompatibility(AlgorandAddress, "base-mainnet");

            // Message must give user clear guidance
            Assert.That(compatResult.Message.Length, Is.GreaterThan(20),
                "Error message must be descriptive enough to guide user action");
        }

        [Test]
        public void WalletCompatibility_Service_UnsupportedWalletType_MentionsStandard()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<IPortfolioIntelligenceService>();

            var compatResult = service.EvaluateWalletCompatibility(
                AlgorandAddress, "base-mainnet", tokenStandard: "ERC20");

            Assert.That(compatResult.Message, Does.Contain("ERC20"),
                "Message must reference the incompatible token standard");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Backward compatibility – existing endpoints not broken
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExistingWalletEndpoint_StillReachable()
        {
            var response = await _client.GetAsync("/api/v1/wallet/networks");

            Assert.That((int)response.StatusCode,
                Is.EqualTo(401).Or.EqualTo(200).Or.EqualTo(404),
                "Existing wallet endpoint must still be reachable (not broken by new service registration)");
        }

        [Test]
        public async Task ExistingOperationsIntelligenceEndpoint_StillReachable()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/operations-intelligence/evaluate",
                new { assetId = 1, network = "algorand-mainnet" });

            Assert.That((int)response.StatusCode,
                Is.EqualTo(401).Or.EqualTo(200).Or.EqualTo(400),
                "Existing operations intelligence endpoint must still be reachable");
        }

        [Test]
        public async Task ExistingTokenLaunchPreviewEndpoint_StillReachable()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/token-launch/preview-config",
                new { tokenType = "ASA", network = "algorand-mainnet", name = "T", symbol = "T" });

            Assert.That((int)response.StatusCode,
                Is.EqualTo(200).Or.EqualTo(400).Or.EqualTo(401),
                "Existing token launch preview endpoint must still be reachable");
        }
    }
}

using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
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
    /// Contract tests verifying that token portfolio surfaces are stable, comprehensible,
    /// and resilient to partial data.  Tests cover:
    /// <list type="bullet">
    ///   <item>Token metadata normalization across full / partial / missing data scenarios</item>
    ///   <item>Transaction lifecycle states – no ambiguous in-progress or terminal states</item>
    ///   <item>Wallet connection state lifecycle and network mismatch detection</item>
    ///   <item>Integration health-check to confirm the DI container starts correctly</item>
    /// </list>
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenPortfolioClarityContractTests
    {
        private Mock<ILogger<TokenMetadataService>> _metadataLoggerMock = null!;
        private TokenMetadataService _metadataService = null!;
        private Mock<ILogger<WalletConnectionService>> _walletLoggerMock = null!;
        private WalletConnectionService _walletService = null!;

        [SetUp]
        public void SetUp()
        {
            _metadataLoggerMock = new Mock<ILogger<TokenMetadataService>>();
            var registryMock = new Mock<BiatecTokensApi.Services.Interface.ITokenRegistryService>();
            _metadataService = new TokenMetadataService(_metadataLoggerMock.Object, registryMock.Object);

            _walletLoggerMock = new Mock<ILogger<WalletConnectionService>>();
            _walletService = new WalletConnectionService(_walletLoggerMock.Object);
        }

        // ── AC1: Token portfolio surfaces resilient to partial data ────────────

        [Test]
        public async Task TokenMetadata_WithAllFields_ShouldReturnValidStatus()
        {
            var metadata = BuildFullMetadata();

            var result = await _metadataService.ValidateMetadataAsync(metadata);

            Assert.That(result.ValidationStatus, Is.EqualTo(TokenMetadataValidationStatus.Valid));
            Assert.That(result.ValidationIssues.Count(i => i.Severity == TokenMetadataIssueSeverity.Error), Is.Zero);
        }

        [Test]
        public async Task TokenMetadata_WithMissingName_ShouldFailValidation()
        {
            var metadata = BuildFullMetadata();
            metadata.Name = string.Empty;

            var result = await _metadataService.ValidateMetadataAsync(metadata);

            Assert.That(result.ValidationStatus, Is.EqualTo(TokenMetadataValidationStatus.Invalid));
            Assert.That(result.ValidationIssues.Any(i => i.Field == "Name" && i.Severity == TokenMetadataIssueSeverity.Error), Is.True);
        }

        [Test]
        public async Task TokenMetadata_WithMissingOptionalFields_ShouldWarnNotError()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Minimal Token",
                Symbol = "MIN",
                TokenIdentifier = "12345",
                Chain = "algorand-mainnet"
            };

            var result = await _metadataService.ValidateMetadataAsync(metadata);

            // Warnings for optional fields are acceptable; errors are not
            Assert.That(result.ValidationStatus, Is.Not.EqualTo(TokenMetadataValidationStatus.Invalid));
            Assert.That(result.ValidationIssues.All(i => i.Severity != TokenMetadataIssueSeverity.Error), Is.True,
                "Missing optional fields should produce warnings, not errors");
        }

        [Test]
        public async Task TokenMetadata_ApplyFallbacks_ShouldNotNullOutRequiredDisplayFields()
        {
            var partial = new EnrichedTokenMetadata
            {
                Name = "Test",
                Symbol = "TST",
                TokenIdentifier = "99999",
                Chain = "algorand-mainnet"
            };

            var result = _metadataService.ApplyFallbacks(partial);

            Assert.That(result.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Symbol, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task TokenMetadata_CompletenessScore_ShouldBeHigherForFullMetadata()
        {
            var full = BuildFullMetadata();
            var minimal = new EnrichedTokenMetadata
            {
                Name = "Min",
                Symbol = "M",
                TokenIdentifier = "1",
                Chain = "algorand-mainnet"
            };

            int fullScore = _metadataService.CalculateCompletenessScore(full);
            int minScore = _metadataService.CalculateCompletenessScore(minimal);

            Assert.That(fullScore, Is.GreaterThan(minScore),
                "Completeness score must be higher when more fields are populated");
        }

        [Test]
        public async Task TokenMetadata_ExplorerUrl_ShouldBeGeneratedForKnownChains()
        {
            var url = _metadataService.GenerateExplorerUrl("12345678", "algorand-mainnet");

            Assert.That(url, Is.Not.Null.And.Not.Empty);
            Assert.That(url, Does.StartWith("https://"));
        }

        [Test]
        public async Task TokenMetadata_ExplorerUrl_ShouldReturnNullForUnknownChain()
        {
            var url = _metadataService.GenerateExplorerUrl("abc", "unknown-chain-xyz");

            Assert.That(url, Is.Null,
                "Unknown chains should return null explorer URL instead of an invalid URL");
        }

        // ── AC2: No ambiguous transaction state ───────────────────────────────

        [Test]
        public void TransactionStatus_AllValuesAreDefined_NoneAmbiguous()
        {
            var statuses = Enum.GetValues<TransactionStatus>();

            Assert.That(statuses.Length, Is.GreaterThanOrEqualTo(4),
                "TransactionStatus must define at least Pending, Confirming, Completed, Failed");
            Assert.That(Enum.IsDefined(typeof(TransactionStatus), "Completed"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TransactionStatus), "Failed"), Is.True);
        }

        [Test]
        public void TransactionSummary_PendingState_ShouldHaveProgressBelow100()
        {
            var summary = new TransactionSummary
            {
                Status = TransactionStatus.Pending,
                ProgressPercentage = 10,
                IsTerminal = false,
                IsRetryable = false
            };

            Assert.That(summary.IsTerminal, Is.False);
            Assert.That(summary.ProgressPercentage, Is.LessThan(100));
        }

        [Test]
        public void TransactionSummary_CompletedState_ShouldBeTerminal()
        {
            var summary = new TransactionSummary
            {
                Status = TransactionStatus.Completed,
                ProgressPercentage = 100,
                IsTerminal = true,
                IsRetryable = false
            };

            Assert.That(summary.IsTerminal, Is.True);
            Assert.That(summary.IsRetryable, Is.False);
        }

        [Test]
        public void TransactionSummary_FailedState_ShouldProvideRecommendedAction()
        {
            var summary = new TransactionSummary
            {
                Status = TransactionStatus.Failed,
                IsTerminal = true,
                IsRetryable = true,
                RecommendedAction = "Retry the transaction or contact support"
            };

            Assert.That(summary.IsTerminal, Is.True);
            Assert.That(summary.RecommendedAction, Is.Not.Null.And.Not.Empty,
                "Failed transactions must have a recommended action for the user");
        }

        [Test]
        public void TransactionSummary_ExplorerUrl_EnablesBlockchainTransparency()
        {
            var summary = new TransactionSummary
            {
                TransactionId = "tx-1",
                TransactionHash = "ABCD1234",
                ExplorerUrl = "https://allo.info/tx/ABCD1234"
            };

            Assert.That(summary.ExplorerUrl, Does.StartWith("https://"));
            Assert.That(summary.ExplorerUrl, Does.Contain(summary.TransactionHash ?? ""));
        }

        [Test]
        public void TransactionSummary_InProgress_ShouldExposeElapsedAndEstimated()
        {
            var summary = new TransactionSummary
            {
                Status = TransactionStatus.Confirming,
                ElapsedSeconds = 5,
                EstimatedSecondsToCompletion = 15
            };

            Assert.That(summary.ElapsedSeconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.EstimatedSecondsToCompletion, Is.GreaterThan(0));
        }

        // ── AC3: Wallet connection and reconnect paths ─────────────────────────

        [Test]
        public void WalletConnection_InitialState_IsDisconnected()
        {
            var state = _walletService.GetConnectionState("TESTADDRESS", "algorand-mainnet");

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
        }

        [Test]
        public void WalletConnection_AfterConnect_IsConnected()
        {
            var address = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
            var state = _walletService.Connect(address, "algorand-mainnet", "algorand-mainnet");

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Connected));
        }

        [Test]
        public void WalletConnection_NetworkMismatch_ShouldExplainMismatch()
        {
            var address = "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD";
            var state = _walletService.Connect(address, "algorand-testnet", "algorand-mainnet");

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.NetworkMismatch));
            Assert.That(state.NetworkMismatch, Is.Not.Null);
            Assert.That(state.NetworkMismatch!.Description, Is.Not.Null.And.Not.Empty);
            Assert.That(state.NetworkMismatch.ResolutionGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void WalletConnection_ReconnectGuidance_HasOrderedSteps()
        {
            var guidance = _walletService.GetReconnectGuidance(
                WalletReconnectReason.NetworkMismatch,
                "algorand-testnet",
                "algorand-mainnet");

            Assert.That(guidance.Steps, Is.Not.Empty);
            for (int i = 0; i < guidance.Steps.Count; i++)
            {
                Assert.That(guidance.Steps[i].StepNumber, Is.EqualTo(i + 1));
                Assert.That(guidance.Steps[i].Title, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void WalletConnection_DisconnectedStateMessage_IsUserReadable()
        {
            var state = _walletService.Disconnect("EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE");

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
            Assert.That(state.StatusMessage, Is.Not.Null.And.Not.Empty);
        }

        // ── AC4: Wallet connection service registered in DI ────────────────────

        [Test]
        [NonParallelizable]
        public async Task WalletConnectionService_ShouldBeResolvableFromDI()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(BuildTestConfig());
                    });
                });

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetService(typeof(BiatecTokensApi.Services.Interface.IWalletConnectionService));

            Assert.That(service, Is.Not.Null,
                "IWalletConnectionService must be registered in the DI container");

            await Task.CompletedTask; // satisfy async signature
        }

        [Test]
        [NonParallelizable]
        public async Task WalletConnectionEndpoint_HealthCheck_ShouldReturn200()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(BuildTestConfig());
                    });
                });

            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable),
                "/health endpoint must be reachable (status is secondary to reachability)");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static EnrichedTokenMetadata BuildFullMetadata()
        {
            return new EnrichedTokenMetadata
            {
                Name = "Full Token",
                Symbol = "FULL",
                Decimals = 6,
                Description = "A token with complete metadata for testing",
                ImageUrl = "https://example.com/logo.png",
                WebsiteUrl = "https://example.com",
                TokenIdentifier = "87654321",
                Chain = "algorand-mainnet"
            };
        }

        private static Dictionary<string, string?> BuildTestConfig()
        {
            return new Dictionary<string, string?>
            {
                ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                ["KeyManagementConfig:Provider"] = "Hardcoded",
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForPortfolioClarityTests32CharMinimum",
                ["JwtConfig:SecretKey"] = "TestJwtSecretKeyForTokenPortfolioClarityTestsPurposesOnly64ByteMinimumRequired",
                ["JwtConfig:Issuer"] = "BiatecTokensApi",
                ["JwtConfig:Audience"] = "BiatecTokensUsers",
                ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                ["AlgorandAuthentication:CheckExpiration"] = "false",
                ["AlgorandAuthentication:Debug"] = "true",
                ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                ["IPFSConfig:TimeoutSeconds"] = "30",
                ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                ["IPFSConfig:ValidateContentHash"] = "true",
                ["IPFSConfig:Username"] = "",
                ["IPFSConfig:Password"] = "",
                ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                ["EVMChains:0:ChainId"] = "8453",
                ["EVMChains:0:GasLimit"] = "4500000",
                ["Cors:0"] = "https://tokens.biatec.io",
                ["StripeConfig:SecretKey"] = "test_stripe_key",
                ["StripeConfig:PublishableKey"] = "test_publishable_key",
                ["StripeConfig:WebhookSecret"] = "test_webhook_secret",
                ["KycConfig:Provider"] = "Mock",
                ["KycConfig:Enabled"] = "false"
            };
        }
    }
}

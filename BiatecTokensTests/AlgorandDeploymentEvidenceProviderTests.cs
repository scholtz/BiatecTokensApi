using System.Net;
using System.Text;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for <see cref="AlgorandDeploymentEvidenceProvider"/> covering:
    ///
    /// <list type="bullet">
    ///   <item>Successful evidence retrieval (happy path)</item>
    ///   <item>Static JSON parsing via <c>ParseIndexerResponse</c></item>
    ///   <item>Timeout handling</item>
    ///   <item>HTTP non-2xx responses</item>
    ///   <item>Network / HttpRequestException</item>
    ///   <item>Malformed JSON responses</item>
    ///   <item>Unsupported (non-Algorand) networks</item>
    ///   <item>Missing IndexerUrl configuration</item>
    ///   <item>Partial evidence (confirmed-round = 0, created-asset-index = 0)</item>
    ///   <item>Empty transaction list</item>
    ///   <item>Multiple transactions — first confirmed wins</item>
    ///   <item>IsSimulated = false contract</item>
    ///   <item>EvidenceSource = "algorand-indexer" contract</item>
    ///   <item>Retry behaviour on transient HTTP failure</item>
    ///   <item>Integration with TokenDeploymentLifecycleService (Authoritative mode)</item>
    ///   <item>Integration with TokenDeploymentLifecycleService (Simulation fallback)</item>
    ///   <item>EvidenceProvenance propagation</item>
    ///   <item>DeploymentEvidenceConfig DI selection logic</item>
    /// </list>
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AlgorandDeploymentEvidenceProviderTests
    {
        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a provider backed by a fake <see cref="HttpMessageHandler"/> that
        /// returns the specified <paramref name="response"/> for every request.
        /// </summary>
        private static AlgorandDeploymentEvidenceProvider MakeProvider(
            HttpResponseMessage response,
            string indexerUrl = "https://testnet-idx.algonode.cloud",
            int timeoutSeconds = 30,
            int maxRetries = 0)
        {
            var handler = new FakeHttpMessageHandler(response);
            var factory = new FakeHttpClientFactory(handler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = indexerUrl,
                ApiToken       = string.Empty,
                TimeoutSeconds = timeoutSeconds,
                MaxRetries     = maxRetries,
            };
            return new AlgorandDeploymentEvidenceProvider(
                factory,
                config,
                NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);
        }

        /// <summary>
        /// Creates a valid Algorand indexer JSON response with one confirmed
        /// asset-creation transaction.
        /// </summary>
        private static string ValidIndexerJson(
            string txId    = "VALIDTXID123456789ABCDEF",
            ulong  assetId = 987654,
            ulong  round   = 22_000_000)
        {
            return $$"""
            {
              "current-round": {{round}},
              "transactions": [
                {
                  "id": "{{txId}}",
                  "tx-type": "acfg",
                  "confirmed-round": {{round}},
                  "created-asset-index": {{assetId}},
                  "asset-config-transaction": {
                    "asset-id": 0,
                    "params": {
                      "creator": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                      "decimals": 6,
                      "name": "Test Token",
                      "total": 1000000
                    }
                  }
                }
              ]
            }
            """;
        }

        private static HttpResponseMessage Ok(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static HttpResponseMessage Error(HttpStatusCode code)
            => new(code) { Content = new StringContent("error", Encoding.UTF8, "text/plain") };

        // ─── ParseIndexerResponse (static, no HTTP) ───────────────────────────────

        [Test]
        public void ParseIndexerResponse_ValidJson_ReturnsEvidence()
        {
            var evidence = AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(
                ValidIndexerJson("TXABC123", 111_222, 33_000_000),
                "deploy-test");

            Assert.That(evidence, Is.Not.Null, "Should parse valid indexer JSON");
            Assert.That(evidence!.TransactionId, Is.EqualTo("TXABC123"));
            Assert.That(evidence.AssetId, Is.EqualTo(111_222UL));
            Assert.That(evidence.ConfirmedRound, Is.EqualTo(33_000_000UL));
            Assert.That(evidence.IsSimulated, Is.False, "Algorand indexer evidence is never simulated");
            Assert.That(evidence.EvidenceSource, Is.EqualTo("algorand-indexer"));
        }

        [Test]
        public void ParseIndexerResponse_EmptyTransactions_ReturnsNull()
        {
            var json = """{"current-round":1,"transactions":[]}""";
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null,
                "Empty transactions list should return null");
        }

        [Test]
        public void ParseIndexerResponse_NoTransactionsKey_ReturnsNull()
        {
            var json = """{"current-round":1}""";
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null);
        }

        [Test]
        public void ParseIndexerResponse_MalformedJson_ReturnsNull()
        {
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse("{not json!!!", "d1"),
                Is.Null,
                "Malformed JSON should return null (fail-closed)");
        }

        [Test]
        public void ParseIndexerResponse_NullOrEmpty_ReturnsNull()
        {
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(null!, "d1"),
                Is.Null);
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(string.Empty, "d1"),
                Is.Null);
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse("   ", "d1"),
                Is.Null);
        }

        [Test]
        public void ParseIndexerResponse_ConfirmedRoundZero_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"TX1","tx-type":"acfg",
              "confirmed-round":0,
              "created-asset-index":555
            }]}
            """;
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null,
                "confirmed-round = 0 means not yet confirmed; should return null");
        }

        [Test]
        public void ParseIndexerResponse_NoConfirmedRound_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"TX1","tx-type":"acfg",
              "created-asset-index":555
            }]}
            """;
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null,
                "Missing confirmed-round property should return null");
        }

        [Test]
        public void ParseIndexerResponse_CreatedAssetIndexZero_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"TX1","tx-type":"acfg",
              "confirmed-round":9000000,
              "created-asset-index":0
            }]}
            """;
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null,
                "created-asset-index = 0 means no new asset was created; should return null");
        }

        [Test]
        public void ParseIndexerResponse_NoCreatedAssetIndex_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"TX1","tx-type":"acfg",
              "confirmed-round":9000000
            }]}
            """;
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null);
        }

        [Test]
        public void ParseIndexerResponse_EmptyTxId_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"",
              "tx-type":"acfg",
              "confirmed-round":9000000,
              "created-asset-index":555
            }]}
            """;
            Assert.That(
                AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1"),
                Is.Null,
                "Empty transaction ID should return null");
        }

        [Test]
        public void ParseIndexerResponse_MultipleTransactions_FirstConfirmedWins()
        {
            var json = """
            {"transactions":[
              {"id":"","tx-type":"acfg","confirmed-round":0,"created-asset-index":0},
              {"id":"SECOND_TX","tx-type":"acfg","confirmed-round":5000000,"created-asset-index":222},
              {"id":"THIRD_TX","tx-type":"acfg","confirmed-round":5000001,"created-asset-index":333}
            ]}
            """;
            var evidence = AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(json, "d1");
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence!.TransactionId, Is.EqualTo("SECOND_TX"),
                "First valid entry should be returned");
            Assert.That(evidence.AssetId, Is.EqualTo(222UL));
        }

        [Test]
        public void ParseIndexerResponse_ObtainedAt_IsSetToUtcNow()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            var evidence = AlgorandDeploymentEvidenceProvider.ParseIndexerResponse(
                ValidIndexerJson(), "d1");
            var after = DateTimeOffset.UtcNow.AddSeconds(1);

            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence!.ObtainedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(evidence.ObtainedAt, Is.LessThanOrEqualTo(after));
        }

        // ─── HTTP-level tests ─────────────────────────────────────────────────────

        [Test]
        public async Task ObtainEvidenceAsync_HappyPath_ReturnsEvidence()
        {
            var provider = MakeProvider(Ok(ValidIndexerJson("TX_HAPPY", 42_000, 15_000_000)));

            var evidence = await provider.ObtainEvidenceAsync(
                "deploy-happy", "ASA", "algorand-testnet");

            Assert.That(evidence, Is.Not.Null, "Happy path should return non-null evidence");
            Assert.That(evidence!.TransactionId, Is.EqualTo("TX_HAPPY"));
            Assert.That(evidence.AssetId, Is.EqualTo(42_000UL));
            Assert.That(evidence.ConfirmedRound, Is.EqualTo(15_000_000UL));
            Assert.That(evidence.IsSimulated, Is.False, "Authoritative evidence must not be simulated");
            Assert.That(evidence.EvidenceSource, Is.EqualTo("algorand-indexer"));
        }

        [Test]
        public async Task ObtainEvidenceAsync_IsSimulated_AlwaysFalse()
        {
            var provider = MakeProvider(Ok(ValidIndexerJson()));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-mainnet");

            Assert.That(provider.IsSimulated, Is.False,
                "Provider.IsSimulated must be false for live provider");
            Assert.That(evidence!.IsSimulated, Is.False,
                "Evidence.IsSimulated must be false for live provider");
        }

        [Test]
        public async Task ObtainEvidenceAsync_UnsupportedNetwork_ReturnsNull()
        {
            var provider = MakeProvider(Ok(ValidIndexerJson()));

            // EVM networks are not handled by the Algorand provider
            foreach (var network in new[] { "base-mainnet", "ethereum-mainnet", "unknown-net", "" })
            {
                var result = await provider.ObtainEvidenceAsync("dep", "ERC20", network);
                Assert.That(result, Is.Null,
                    $"Network '{network}' should return null from Algorand provider");
            }
        }

        [Test]
        public async Task ObtainEvidenceAsync_AllAlgorandNetworks_AreAccepted()
        {
            var networks = new[]
            {
                "algorand-mainnet",
                "algorand-testnet",
                "algorand-betanet",
                "voi-mainnet",
                "aramid-mainnet",
            };

            foreach (var network in networks)
            {
                var provider = MakeProvider(Ok(ValidIndexerJson("TX1", 100, 500_000)));
                var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", network);
                Assert.That(evidence, Is.Not.Null,
                    $"Algorand network '{network}' should be accepted");
            }
        }

        [Test]
        public async Task ObtainEvidenceAsync_EmptyIndexerUrl_ReturnsNull()
        {
            var handler = new FakeHttpMessageHandler(Ok(ValidIndexerJson()));
            var factory = new FakeHttpClientFactory(handler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = string.Empty,
                TimeoutSeconds = 5,
                MaxRetries     = 0,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null,
                "Missing IndexerUrl must cause provider to return null (cannot operate)");
        }

        [Test]
        public async Task ObtainEvidenceAsync_WhitespaceIndexerUrl_ReturnsNull()
        {
            var handler = new FakeHttpMessageHandler(Ok(ValidIndexerJson()));
            var factory = new FakeHttpClientFactory(handler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl = "   ",
                TimeoutSeconds = 5,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            var result = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task ObtainEvidenceAsync_Http404_ReturnsNull()
        {
            var provider = MakeProvider(Error(HttpStatusCode.NotFound));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "404 should cause fail-closed null return");
        }

        [Test]
        public async Task ObtainEvidenceAsync_Http500_ReturnsNull()
        {
            var provider = MakeProvider(Error(HttpStatusCode.InternalServerError));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "500 should cause fail-closed null return");
        }

        [Test]
        public async Task ObtainEvidenceAsync_Http401_ReturnsNull()
        {
            var provider = MakeProvider(Error(HttpStatusCode.Unauthorized));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "401 (bad API token) should cause fail-closed null return");
        }

        [Test]
        public async Task ObtainEvidenceAsync_NetworkException_ReturnsNull()
        {
            var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
            var factory = new FakeHttpClientFactory(handler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                TimeoutSeconds = 10,
                MaxRetries     = 0,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "Network exception must cause fail-closed null return");
        }

        [Test]
        public async Task ObtainEvidenceAsync_MalformedJson_ReturnsNull()
        {
            var provider = MakeProvider(Ok("{this is not valid json!!!}"));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "Malformed JSON must cause fail-closed null return");
        }

        [Test]
        public async Task ObtainEvidenceAsync_EmptyJsonObject_ReturnsNull()
        {
            var provider = MakeProvider(Ok("{}"));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "Empty JSON object (no 'transactions') should return null");
        }

        [Test]
        public async Task ObtainEvidenceAsync_EmptyTransactionList_ReturnsNull()
        {
            var provider = MakeProvider(Ok("""{"transactions":[]}"""));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "No transactions found should return null (evidence pending)");
        }

        [Test]
        public async Task ObtainEvidenceAsync_PartialEvidence_ConfirmedRoundZero_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"TXPARTIAL","tx-type":"acfg",
              "confirmed-round":0,
              "created-asset-index":12345
            }]}
            """;
            var provider = MakeProvider(Ok(json));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "Unconfirmed transaction must return null");
        }

        [Test]
        public async Task ObtainEvidenceAsync_PartialEvidence_NoAssetCreated_ReturnsNull()
        {
            var json = """
            {"transactions":[{
              "id":"TXPARTIAL","tx-type":"acfg",
              "confirmed-round":9999999,
              "created-asset-index":0
            }]}
            """;
            var provider = MakeProvider(Ok(json));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(evidence, Is.Null, "Transaction with no asset creation must return null");
        }

        [Test]
        public async Task ObtainEvidenceAsync_CancellationRequested_ReturnsNull()
        {
            // Handler that delays long enough to be cancelled
            var handler = new DelayingHttpMessageHandler(TimeSpan.FromSeconds(60), Ok(ValidIndexerJson()));
            var factory = new FakeHttpClientFactory(handler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                TimeoutSeconds = 30,
                MaxRetries     = 0,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            var evidence = await provider.ObtainEvidenceAsync(
                "dep", "ASA", "algorand-testnet", cts.Token);
            Assert.That(evidence, Is.Null, "Cancellation must return null (fail-closed)");
        }

        [Test]
        public async Task ObtainEvidenceAsync_ApiTokenSent_WhenConfigured()
        {
            string? capturedToken = null;
            var captureHandler = new CapturingHttpMessageHandler(
                req =>
                {
                    if (req.Headers.TryGetValues("X-Indexer-API-Token", out var vals))
                        capturedToken = vals.FirstOrDefault();
                },
                Ok(ValidIndexerJson()));

            var factory = new FakeHttpClientFactory(captureHandler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                ApiToken       = "my-secret-token",
                TimeoutSeconds = 10,
                MaxRetries     = 0,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");

            Assert.That(capturedToken, Is.EqualTo("my-secret-token"),
                "API token must be included in X-Indexer-API-Token header when configured");
        }

        [Test]
        public async Task ObtainEvidenceAsync_NoApiToken_NoAuthHeader()
        {
            bool headerPresent = false;
            var captureHandler = new CapturingHttpMessageHandler(
                req => { headerPresent = req.Headers.Contains("X-Indexer-API-Token"); },
                Ok(ValidIndexerJson()));

            var factory = new FakeHttpClientFactory(captureHandler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                ApiToken       = string.Empty,
                TimeoutSeconds = 10,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");
            Assert.That(headerPresent, Is.False, "No API token means no auth header");
        }

        [Test]
        public async Task ObtainEvidenceAsync_RequestUrlContainsNotePrefix_AndAcfgFilter()
        {
            string? capturedUrl = null;
            var captureHandler = new CapturingHttpMessageHandler(
                req => { capturedUrl = req.RequestUri?.ToString(); },
                Ok(ValidIndexerJson()));

            var factory = new FakeHttpClientFactory(captureHandler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                TimeoutSeconds = 10,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            const string deploymentId = "my-deploy-id-123";
            await provider.ObtainEvidenceAsync(deploymentId, "ASA", "algorand-testnet");

            Assert.That(capturedUrl, Is.Not.Null, "A request should have been made");
            Assert.That(capturedUrl, Does.Contain("note-prefix="), "URL must include note-prefix parameter");
            Assert.That(capturedUrl, Does.Contain("tx-type=acfg"), "URL must filter for asset-config transactions");
            Assert.That(capturedUrl, Does.Contain("testnet-idx.algonode.cloud"), "URL must use configured indexer");

            // The note-prefix should be URL-encoded base64 of the deploymentId
            var expectedB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(deploymentId));
            var expectedEncoded = Uri.EscapeDataString(expectedB64);
            Assert.That(capturedUrl, Does.Contain(expectedEncoded),
                "note-prefix must be URL-encoded base64 of the deployment ID");
        }

        [Test]
        public async Task ObtainEvidenceAsync_RetryOnTransientFailure_SucceedsOnSecondAttempt()
        {
            var callCount = 0;
            var retryHandler = new CountingHttpMessageHandler(req =>
            {
                callCount++;
                if (callCount == 1) return Task.FromResult(Error(HttpStatusCode.ServiceUnavailable));
                return Task.FromResult(Ok(ValidIndexerJson("TX_RETRY", 77_777, 10_000_000)));
            });

            var factory = new FakeHttpClientFactory(retryHandler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                TimeoutSeconds = 10,
                MaxRetries     = 1, // one retry
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");

            Assert.That(callCount, Is.EqualTo(2), "Should have made exactly 2 HTTP calls (1 initial + 1 retry)");
            Assert.That(evidence, Is.Not.Null, "Should succeed on retry");
            Assert.That(evidence!.TransactionId, Is.EqualTo("TX_RETRY"));
        }

        [Test]
        public async Task ObtainEvidenceAsync_ExhaustsRetries_ReturnsNull()
        {
            var callCount = 0;
            var retryHandler = new CountingHttpMessageHandler(_ =>
            {
                callCount++;
                return Task.FromResult(Error(HttpStatusCode.ServiceUnavailable));
            });

            var factory = new FakeHttpClientFactory(retryHandler);
            var config = new AlgorandEvidenceProviderConfig
            {
                IndexerUrl     = "https://testnet-idx.algonode.cloud",
                TimeoutSeconds = 10,
                MaxRetries     = 2,
            };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            var evidence = await provider.ObtainEvidenceAsync("dep", "ASA", "algorand-testnet");

            Assert.That(evidence, Is.Null, "Exhausting all retries should return null");
            Assert.That(callCount, Is.EqualTo(3), "Should have attempted 3 times (1 + 2 retries)");
        }

        // ─── IsSimulated contract ─────────────────────────────────────────────────

        [Test]
        public void IsSimulated_AlwaysFalse()
        {
            var handler = new FakeHttpMessageHandler(Ok(ValidIndexerJson()));
            var factory = new FakeHttpClientFactory(handler);
            var config = new AlgorandEvidenceProviderConfig { IndexerUrl = "https://x.y" };
            var provider = new AlgorandDeploymentEvidenceProvider(
                factory, config, NullLogger<AlgorandDeploymentEvidenceProvider>.Instance);

            Assert.That(provider.IsSimulated, Is.False,
                "AlgorandDeploymentEvidenceProvider.IsSimulated must always be false");
        }

        // ─── Integration with TokenDeploymentLifecycleService ────────────────────

        private static TokenDeploymentLifecycleService MakeLifecycleService(
            IDeploymentEvidenceProvider evidenceProvider)
        {
            var logger = NullLogger<TokenDeploymentLifecycleService>.Instance;
            return new TokenDeploymentLifecycleService(logger, evidenceProvider);
        }

        private static TokenDeploymentLifecycleRequest BuildRequest(
            DeploymentExecutionMode mode = DeploymentExecutionMode.Authoritative,
            string? network = null)
        {
            return new TokenDeploymentLifecycleRequest
            {
                IdempotencyKey  = Guid.NewGuid().ToString(),
                CorrelationId   = "integ-test-" + Guid.NewGuid().ToString("N")[..8],
                TokenStandard   = "ASA",
                TokenName       = "Integration Test Token",
                TokenSymbol     = "ITT",
                Network         = network ?? "algorand-testnet",
                TotalSupply     = 1_000_000,
                Decimals        = 6,
                CreatorAddress  = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                ExecutionMode   = mode,
            };
        }

        [Test]
        public async Task Service_WithAlgorandProvider_AuthoritativeMode_Success_SetsIsSimulatedFalse()
        {
            var provider = MakeProvider(Ok(ValidIndexerJson("LIVE_TX", 500_001, 20_000_000)));
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Authoritative));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
            Assert.That(result.IsSimulatedEvidence, Is.False,
                "Authoritative evidence from Algorand provider must set IsSimulatedEvidence = false");
            Assert.That(result.TransactionId, Is.EqualTo("LIVE_TX"));
            Assert.That(result.AssetId, Is.EqualTo(500_001UL));
        }

        [Test]
        public async Task Service_WithAlgorandProvider_EvidenceProvenance_IsPopulated()
        {
            var provider = MakeProvider(Ok(ValidIndexerJson()));
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Authoritative));

            Assert.That(result.EvidenceProvenance, Is.Not.Null.And.Not.Empty,
                "EvidenceProvenance must be populated for authoritative evidence");
            Assert.That(result.EvidenceProvenance, Does.Contain("algorand-indexer"),
                "EvidenceProvenance should reference the evidence source");
        }

        [Test]
        public async Task Service_WithAlgorandProvider_EvidenceUnavailable_AuthoritativeMode_Fails()
        {
            // Provider returns 404 → ObtainEvidenceAsync returns null
            var provider = MakeProvider(Error(HttpStatusCode.NotFound));
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Authoritative));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed),
                "Authoritative mode + null evidence must fail the deployment");
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
            Assert.That(result.Message, Does.Contain("BLOCKCHAIN_EVIDENCE_UNAVAILABLE").Or.Contain("unavailable"),
                "Failure message must reference evidence unavailability");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "Remediation hint must be provided on authoritative failure");
        }

        [Test]
        public async Task Service_WithAlgorandProvider_EvidenceUnavailable_SimulationMode_Succeeds()
        {
            // Even when the live provider cannot return evidence, Simulation mode falls back
            // to the internal SimulatedDeploymentEvidenceProvider
            var provider = MakeProvider(Error(HttpStatusCode.ServiceUnavailable));
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Simulation));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed),
                "Simulation mode must succeed even when the live provider is unavailable");
            Assert.That(result.IsSimulatedEvidence, Is.True,
                "Simulation fallback must set IsSimulatedEvidence = true");
        }

        [Test]
        public async Task Service_WithSimulatedProvider_AuthoritativeMode_Succeeds_IsSimulatedTrue()
        {
            // The SimulatedDeploymentEvidenceProvider returns non-null evidence even in
            // Authoritative mode — it's the default for dev/test environments.
            var provider = new SimulatedDeploymentEvidenceProvider();
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Authoritative));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.IsSimulatedEvidence, Is.True);
        }

        [Test]
        public async Task Service_WithUnavailableProvider_AuthoritativeMode_FailsClosed()
        {
            var provider = new UnavailableDeploymentEvidenceProvider();
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Authoritative));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task Service_EvidenceProvenance_PropagatedOnIdempotentReplay()
        {
            var provider = MakeProvider(Ok(ValidIndexerJson("TXREPLAY", 100_001, 6_000_000)));
            var service = MakeLifecycleService(provider);
            var request = BuildRequest(DeploymentExecutionMode.Authoritative);

            var first = await service.InitiateDeploymentAsync(request);
            Assert.That(first.EvidenceProvenance, Is.Not.Null.And.Not.Empty);

            var replay = await service.InitiateDeploymentAsync(request); // same idempotency key
            Assert.That(replay.IsIdempotentReplay, Is.True, "Second call must be an idempotent replay");
            Assert.That(replay.EvidenceProvenance, Is.EqualTo(first.EvidenceProvenance),
                "EvidenceProvenance must be preserved on idempotent replay");
        }

        // ─── DeploymentEvidenceConfig DI selection ────────────────────────────────

        [Test]
        public void DeploymentEvidenceConfig_DefaultProvider_IsSimulation()
        {
            var config = new DeploymentEvidenceConfig();
            Assert.That(config.Provider, Is.EqualTo("Simulation"),
                "Default provider should be 'Simulation' (safe for dev/test environments)");
        }

        [Test]
        public void DeploymentEvidenceConfig_AlgorandSection_DefaultTimeouts()
        {
            var config = new DeploymentEvidenceConfig();
            Assert.That(config.Algorand.TimeoutSeconds, Is.EqualTo(15),
                "Default timeout should be 15 seconds for public Algorand indexer endpoints");
            Assert.That(config.Algorand.MaxRetries, Is.EqualTo(2));
        }

        [Test]
        public void DeploymentEvidenceConfig_CanSetAlgorandProvider()
        {
            var config = new DeploymentEvidenceConfig
            {
                Provider = "Algorand",
                Algorand = new AlgorandEvidenceProviderConfig
                {
                    IndexerUrl     = "https://mainnet-idx.algonode.cloud",
                    ApiToken       = "secret",
                    TimeoutSeconds = 20,
                    MaxRetries     = 3,
                },
            };
            Assert.That(config.Provider, Is.EqualTo("Algorand"));
            Assert.That(config.Algorand.IndexerUrl, Is.EqualTo("https://mainnet-idx.algonode.cloud"));
        }

        // ─── New model field contracts ─────────────────────────────────────────────

        [Test]
        public void BlockchainDeploymentEvidence_HasEvidenceSourceField()
        {
            var evidence = new BlockchainDeploymentEvidence
            {
                TransactionId  = "TX1",
                AssetId        = 100,
                ConfirmedRound = 999,
                IsSimulated    = false,
                EvidenceSource = "algorand-indexer",
            };
            Assert.That(evidence.EvidenceSource, Is.EqualTo("algorand-indexer"));
        }

        [Test]
        public void BlockchainDeploymentEvidence_EvidenceSource_DefaultEmpty()
        {
            var evidence = new BlockchainDeploymentEvidence();
            Assert.That(evidence.EvidenceSource, Is.Not.Null);
            Assert.That(evidence.EvidenceSource, Is.EqualTo(string.Empty));
        }

        [Test]
        public void SimulatedProvider_SetsEvidenceSource_Simulation()
        {
            var provider = new SimulatedDeploymentEvidenceProvider();
            var evidence = provider.ObtainEvidenceAsync("test-id", "ASA", "algorand-testnet")
                .GetAwaiter().GetResult();

            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence!.EvidenceSource, Is.EqualTo("simulation"));
        }

        [Test]
        public void TokenDeploymentLifecycleResponse_HasEvidenceProvenanceField()
        {
            var resp = new TokenDeploymentLifecycleResponse
            {
                EvidenceProvenance = "algorand-indexer evidence obtained",
            };
            Assert.That(resp.EvidenceProvenance, Is.EqualTo("algorand-indexer evidence obtained"));
        }

        [Test]
        public void TokenDeploymentLifecycleResponse_EvidenceProvenance_DefaultNull()
        {
            var resp = new TokenDeploymentLifecycleResponse();
            Assert.That(resp.EvidenceProvenance, Is.Null);
        }

        [Test]
        public async Task Service_SimulatedProvider_EvidenceProvenance_MentionsSimulation()
        {
            var provider = new SimulatedDeploymentEvidenceProvider();
            var service = MakeLifecycleService(provider);

            var result = await service.InitiateDeploymentAsync(
                BuildRequest(DeploymentExecutionMode.Simulation));

            Assert.That(result.EvidenceProvenance, Is.Not.Null.And.Not.Empty,
                "EvidenceProvenance must be set even for simulated evidence");
            Assert.That(result.EvidenceProvenance,
                Does.Contain("imulat").IgnoreCase,
                "Simulation provenance should mention simulation");
        }

        // ─── Scenario: Algorand provider for ARC-family standards ─────────────────

        [Test]
        [TestCase("ASA")]
        [TestCase("ARC3")]
        [TestCase("ARC200")]
        [TestCase("ARC1400")]
        public async Task ObtainEvidenceAsync_AlgorandStandards_AllReturnEvidence(string standard)
        {
            var provider = MakeProvider(Ok(ValidIndexerJson("TX_STD", 50_000, 8_000_000)));
            var evidence = await provider.ObtainEvidenceAsync("deploy-std", standard, "algorand-testnet");

            Assert.That(evidence, Is.Not.Null,
                $"Standard '{standard}' on algorand-testnet should return evidence");
            Assert.That(evidence!.EvidenceSource, Is.EqualTo("algorand-indexer"));
        }

        [Test]
        public async Task ObtainEvidenceAsync_EvmStandard_OnAlgorandNetwork_ReturnsEvidence()
        {
            // The provider doesn't filter by token standard — only by network.
            // An ERC20 on algorand-testnet is semantically invalid, but the network check
            // is what matters for routing.
            var provider = MakeProvider(Ok(ValidIndexerJson("TX_EVM_ON_ALGO", 1, 1_000)));
            var evidence = await provider.ObtainEvidenceAsync("dep", "ERC20", "algorand-testnet");
            Assert.That(evidence, Is.Not.Null, "Provider is network-gated, not standard-gated");
        }
    }

    // ─── Test double HTTP infrastructure ─────────────────────────────────────────

    /// <summary>Always returns a fixed <see cref="HttpResponseMessage"/>.</summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_response);
        }
    }

    /// <summary>Always throws the given exception.</summary>
    internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _ex;
        public ThrowingHttpMessageHandler(Exception ex) => _ex = ex;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _ex;
    }

    /// <summary>Delays for the given duration before returning the response.</summary>
    internal sealed class DelayingHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        private readonly HttpResponseMessage _response;

        public DelayingHttpMessageHandler(TimeSpan delay, HttpResponseMessage response)
        {
            _delay    = delay;
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return _response;
        }
    }

    /// <summary>Invokes an action on each request before returning the response.</summary>
    internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Action<HttpRequestMessage> _capture;
        private readonly HttpResponseMessage _response;

        public CapturingHttpMessageHandler(
            Action<HttpRequestMessage> capture,
            HttpResponseMessage response)
        {
            _capture  = capture;
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _capture(request);
            return Task.FromResult(_response);
        }
    }

    /// <summary>Invokes a delegate per request, enabling per-attempt response control.</summary>
    internal sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
        public CountingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }

    /// <summary>Creates an <see cref="HttpClient"/> backed by the given handler.</summary>
    internal sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}

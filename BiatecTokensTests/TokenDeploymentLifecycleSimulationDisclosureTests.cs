using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests that explicitly validate the <c>IsSimulatedEvidence</c> flag on
    /// <see cref="BiatecTokensApi.Models.TokenDeploymentLifecycle.TokenDeploymentLifecycleResponse"/>.
    ///
    /// Scope and intent of this file
    /// ──────────────────────────────
    /// The <see cref="BiatecTokensApi.Services.TokenDeploymentLifecycleService"/> currently
    /// operates in **simulated mode**: blockchain-side evidence fields
    /// (<c>AssetId</c>, <c>TransactionId</c>, <c>ConfirmedRound</c>) are derived
    /// deterministically from a SHA-256 hash of the deployment ID rather than obtained
    /// from a live blockchain node.
    ///
    /// These tests prove:
    /// 1. The service explicitly discloses simulation via <c>IsSimulatedEvidence = true</c>.
    /// 2. The flag is stable and cannot be silently dropped across retries and replays.
    /// 3. A real (non-simulated) integration would set <c>IsSimulatedEvidence = false</c>;
    ///    contracts must check the flag before treating evidence as production-valid.
    /// 4. Failure paths set <c>IsSimulatedEvidence = false</c> because no evidence was
    ///    generated (fail-closed: no simulated success for a failed deployment).
    ///
    /// Limitations
    /// ────────────
    /// These tests do NOT prove that <c>AssetId</c> / <c>TransactionId</c> / <c>ConfirmedRound</c>
    /// are valid on any live blockchain network. Real blockchain integration and the associated
    /// sign-off evidence remain as a subsequent work item.
    /// </summary>
    [TestFixture]
    public class TokenDeploymentLifecycleSimulationDisclosureTests
    {
        // ── Unit-level service tests ───────────────────────────────────────────

        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;
        private TokenDeploymentLifecycleService _service = null!;

        private const string ValidAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();
            _service    = new TokenDeploymentLifecycleService(_loggerMock.Object);
        }

        // ── 1. Successful deployment explicitly discloses simulation ───────────

        [Test]
        public async Task SuccessfulDeployment_IsSimulatedEvidence_IsTrue()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Simulation Disclosure Token",
                TokenSymbol    = "SDT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed),
                "Precondition: deployment must complete for this test to be meaningful.");
            Assert.That(result.IsSimulatedEvidence, Is.True,
                "The service MUST disclose that AssetId/TransactionId/ConfirmedRound are " +
                "deterministic hash-derived values, not confirmed blockchain state.");
        }

        [Test]
        public async Task SuccessfulDeployment_MessageContainsSimulationDisclosure()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Disclosure Token",
                TokenSymbol    = "DSC",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Message, Does.Contain("simulated").IgnoreCase
                .Or.Contain("IsSimulatedEvidence").IgnoreCase,
                "The completion message MUST reference the simulation nature of the evidence " +
                "so that stakeholders reading the message know this is not real blockchain proof.");
        }

        [Test]
        public async Task SuccessfulDeployment_TelemetryContainsSimulationDisclosure()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Telemetry Disclosure Token",
                TokenSymbol    = "TDT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            var completionEvent = result.TelemetryEvents
                .FirstOrDefault(e => e.EventType == TelemetryEventType.CompletionSuccess);
            Assert.That(completionEvent, Is.Not.Null,
                "A completion telemetry event must exist.");
            // The event description or metadata should indicate simulated evidence
            var hasSimulationNote = completionEvent!.Description.Contains("simulated", StringComparison.OrdinalIgnoreCase)
                || (completionEvent.Metadata?.ContainsKey("isSimulatedEvidence") == true);
            Assert.That(hasSimulationNote, Is.True,
                "The completion telemetry event MUST contain a simulation disclosure so that " +
                "audit trails are honest about the evidence source.");
        }

        // ── 2. Simulation flag survives idempotency replay ─────────────────────

        [Test]
        public async Task IdempotentReplay_IsSimulatedEvidence_IsPreserved()
        {
            var key = Guid.NewGuid().ToString();
            var req = new TokenDeploymentLifecycleRequest
            {
                IdempotencyKey = key,
                TokenStandard  = "ASA",
                TokenName      = "Replay Token",
                TokenSymbol    = "RPT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var first  = await _service.InitiateDeploymentAsync(req);
            var replay = await _service.InitiateDeploymentAsync(req);

            Assert.That(first.IsSimulatedEvidence,  Is.True, "First call: IsSimulatedEvidence must be true.");
            Assert.That(replay.IsSimulatedEvidence, Is.True,
                "Idempotent replay: IsSimulatedEvidence must be preserved; " +
                "the simulation nature of the evidence cannot be silently dropped on replay.");
        }

        [Test]
        public async Task StatusQuery_IsSimulatedEvidence_IsPreserved()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Status Disclosure Token",
                TokenSymbol    = "STK",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var initiated = await _service.InitiateDeploymentAsync(req);
            var status    = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);

            Assert.That(status.IsSimulatedEvidence, Is.True,
                "Status query MUST preserve IsSimulatedEvidence from the original deployment; " +
                "polling consumers must always know the evidence type.");
        }

        // ── 3. Failure paths do NOT produce simulated blockchain evidence ──────

        [Test]
        public async Task FailedDeployment_IsSimulatedEvidence_IsFalse()
        {
            // A deployment that fails validation never reaches the blockchain submission step.
            // IsSimulatedEvidence must be false (fail-closed): no evidence was produced.
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = string.Empty,  // missing → validation fail
                TokenName      = "Failed Token",
                TokenSymbol    = "FLD",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed),
                "Precondition: deployment must fail for this test to be meaningful.");
            Assert.That(result.IsSimulatedEvidence, Is.False,
                "A failed deployment must NOT set IsSimulatedEvidence = true. " +
                "Fail-closed: no blockchain evidence (real or simulated) is produced for a failed deployment.");
        }

        [Test]
        public async Task FailedDeployment_AssetId_IsNull()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = "Failed Token",
                TokenSymbol    = "FLD",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.AssetId, Is.Null,
                "A failed deployment must NOT return a simulated AssetId; " +
                "returning a hash-derived AssetId for a failed deployment would be misleading.");
        }

        [Test]
        public async Task FailedDeployment_TransactionId_IsNull()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = "Failed Token",
                TokenSymbol    = "FLD",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.TransactionId, Is.Null,
                "A failed deployment must NOT return a simulated TransactionId.");
        }

        // ── 4. Contract consistency: simulated values are non-null when IsSimulatedEvidence = true ──

        [Test]
        public async Task SimulatedEvidence_AssetIdPresent_WhenFlagIsTrue()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Contract Token",
                TokenSymbol    = "CTK",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            if (result.IsSimulatedEvidence)
            {
                Assert.That(result.AssetId, Is.Not.Null,
                    "When IsSimulatedEvidence = true, AssetId must be present " +
                    "(even as a simulated value) to maintain a consistent response contract.");
                Assert.That(result.TransactionId, Is.Not.Null.And.Not.Empty,
                    "When IsSimulatedEvidence = true, TransactionId must be present.");
                Assert.That(result.ConfirmedRound, Is.Not.Null,
                    "When IsSimulatedEvidence = true, ConfirmedRound must be present.");
            }
        }

        // ── 5. Documentation contract: SchemaVersion reflects model version ────

        [Test]
        public async Task Response_SchemaVersion_PresentAndKnownVersion()
        {
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Schema Token",
                TokenSymbol    = "SKM",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty,
                "SchemaVersion must be present so consumers can detect breaking contract changes.");
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"),
                "SchemaVersion must be '1.0.0' for the current simulated implementation; " +
                "upgrade to '2.0.0' when real blockchain integration is delivered.");
        }

        // ── Integration test: HTTP endpoint exposes IsSimulatedEvidence ───────

        [Test]
        [NonParallelizable]
        public async Task HttpEndpoint_InitiateResponse_ContainsIsSimulatedEvidenceTrue()
        {
            await using var factory = new SimulationDisclosureWebApplicationFactory();
            var client = factory.CreateClient();

            // Register and login to get a JWT
            var email = $"sim-disclosure-{Guid.NewGuid():N}@biatec-test.example.com";
            var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Simulation Disclosure Test"
            });
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regBody?.AccessToken ?? string.Empty);

            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "HTTP Disclosure Token",
                TokenSymbol    = "HDT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
            };
            var resp = await client.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.IsSimulatedEvidence, Is.True,
                "The HTTP response MUST expose IsSimulatedEvidence = true so that frontend " +
                "consumers and sign-off tooling can distinguish simulated from real evidence.");
        }

        [Test]
        [NonParallelizable]
        public async Task HttpEndpoint_FailedDeployment_IsSimulatedEvidenceFalse()
        {
            await using var factory = new SimulationDisclosureWebApplicationFactory();
            var client = factory.CreateClient();

            var email = $"sim-failed-{Guid.NewGuid():N}@biatec-test.example.com";
            var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Simulation Failed Test"
            });
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regBody?.AccessToken ?? string.Empty);

            // Invalid request: no token standard → validation failure
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = "Failed HTTP Token",
                TokenSymbol    = "FHT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
            };
            var resp = await client.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(body.IsSimulatedEvidence, Is.False,
                "A failed deployment must NOT expose IsSimulatedEvidence = true via HTTP; " +
                "fail-closed semantics require that no evidence (real or simulated) is claimed.");
        }

        private sealed class SimulationDisclosureWebApplicationFactory
            : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "SimDisclosureTestKey32CharsMinimumReq!!",
                        ["JwtConfig:SecretKey"] = "SimDisclosureIntegrationTestKey32CharsRequired!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
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
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }
    }
}

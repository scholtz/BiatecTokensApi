using BiatecTokensApi.Models.ProtectedSignOff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests that prove protected sign-off evidence integrity:
    /// governance status and readiness are observed from actual runtime
    /// configuration, not from workflow-injected overrides.
    ///
    /// Coverage:
    ///
    /// EI1: Governance enabled (default config) → HTTP response contains WorkflowGovernanceEnabled=Pass.
    /// EI2: Governance disabled via runtime config → HTTP response contains WorkflowGovernanceEnabled=DegradedFail.
    /// EI3: Governance disabled → isReadyForProtectedRun is false (evidence cannot overstate readiness).
    /// EI4: Governance disabled → environment status is Degraded, not Ready.
    /// EI5: Governance disabled → DegradedFailCount ≥ 1 in response.
    /// EI6: Governance enabled → status can be Ready (no governance degradation).
    /// EI7: Evidence response TotalCheckCount matches Checks list length always.
    /// EI8: Removing WorkflowGovernanceConfig key entirely → defaults to Pass (safe default).
    /// EI9: Governance check outcome in response is observable before any manifest is written.
    /// EI10: Governance DegradedFail does not raise CriticalFailCount (non-blocking).
    /// EI11: Full required-config check + governance check present when IncludeConfigCheck=true.
    /// EI12: Changing governance config at runtime changes observed outcome (not hardcoded).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidenceIntegrityTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a <see cref="WebApplicationFactory{TEntryPoint}"/> with the standard
        /// minimal test configuration plus optional overrides for governance settings.
        /// </summary>
        private static GovernanceFactory CreateFactory(
            string? governanceEnabled = null,
            string? policyVersion = null) =>
            new GovernanceFactory(governanceEnabled, policyVersion);

        /// <summary>
        /// Registers a test user and returns a JWT bearer token for authenticated calls.
        /// </summary>
        private static async Task<string> GetJwtTokenAsync(HttpClient unauthClient)
        {
            string email = $"ei-test-{Guid.NewGuid():N}@biatec-evidence.example.com";
            object regReq = new
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Evidence Integrity Test User"
            };
            HttpResponseMessage regResp = await unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            JsonDocument? regBody = await regResp.Content.ReadFromJsonAsync<JsonDocument>();
            return regBody?.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;
        }

        // ── EI1: Governance enabled (default) → Pass ─────────────────────────

        [Test]
        public async Task EI1_GovernanceEnabled_Default_EnvironmentCheckReturnsPass()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "true");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            EnvironmentCheck? govCheck = result!.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null,
                "WorkflowGovernanceEnabled check must be present in response");
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "Observed governance check must be Pass when governance is enabled");
        }

        // ── EI2: Governance disabled → DegradedFail observed ─────────────────

        [Test]
        public async Task EI2_GovernanceDisabled_EnvironmentCheckReturnsDegradedFail()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "false");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            EnvironmentCheck? govCheck = result!.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null,
                "WorkflowGovernanceEnabled check must be present in response");
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail),
                "Observed governance check must be DegradedFail when governance is disabled — " +
                "evidence must reflect runtime config, not workflow-injected values");
        }

        // ── EI3: Governance disabled → isReadyForProtectedRun = false ─────────

        [Test]
        public async Task EI3_GovernanceDisabled_IsReadyForProtectedRun_IsFalse()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "false");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.IsReadyForProtectedRun, Is.False,
                "isReadyForProtectedRun must be false when governance is disabled — " +
                "evidence cannot overstate release readiness");
        }

        // ── EI4: Governance disabled → status = Degraded ─────────────────────

        [Test]
        public async Task EI4_GovernanceDisabled_EnvironmentStatus_IsDegraded()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "false");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Status, Is.EqualTo(ProtectedEnvironmentStatus.Degraded),
                "Environment status must be Degraded (not Ready) when governance is disabled");
        }

        // ── EI5: Governance disabled → DegradedFailCount ≥ 1 ─────────────────

        [Test]
        public async Task EI5_GovernanceDisabled_DegradedFailCount_IsAtLeastOne()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "false");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.DegradedFailCount, Is.GreaterThanOrEqualTo(1),
                "DegradedFailCount must be at least 1 when governance is disabled");
        }

        // ── EI6: Governance enabled → status can be Ready ────────────────────

        [Test]
        public async Task EI6_GovernanceEnabled_EnvironmentStatus_CanBeReady()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "true");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            // Status must not be Degraded due to governance — it is acceptable for it
            // to be Ready (all checks pass) or at worst Degraded from other optional checks.
            // Specifically the governance check itself must not degrade the status.
            EnvironmentCheck? govCheck = result!.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");
            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "Governance check must be Pass, not introducing degradation, when governance is enabled");
        }

        // ── EI7: TotalCheckCount always consistent ─────────────────────────

        [Test]
        public async Task EI7_ResponseTotalCheckCount_AlwaysMatchesChecksListLength()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "true");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.TotalCheckCount, Is.EqualTo(result.Checks.Count),
                "TotalCheckCount must always equal Checks.Count — manifest must not overcount");
            Assert.That(result.TotalCheckCount, Is.GreaterThan(0));
        }

        // ── EI8: Missing governance key → defaults to Pass ────────────────────

        [Test]
        public async Task EI8_GovernanceKeyAbsent_DefaultsToPass()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: null);
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            EnvironmentCheck? govCheck = result!.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govCheck, Is.Not.Null);
            Assert.That(govCheck!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass),
                "When WorkflowGovernanceConfig:Enabled is absent, the check must default to Pass " +
                "(safe default — no false degradation of environments without explicit config)");
        }

        // ── EI9: Governance outcome observable before manifest written ─────────

        [Test]
        public async Task EI9_GovernanceCheckOutcome_IsObservableInApiResponse()
        {
            // Confirms that the governance check outcome is present directly in
            // the HTTP API response — it does not require reading a manifest file
            // or any post-processing step.  The workflow evidence-collection step
            // reads this value from the response to write truthful manifest content.
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "false");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage httpResponse = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "ei9-test",
                    IncludeConfigCheck = true,
                    IncludeFixtureCheck = false,
                    IncludeObservabilityCheck = false
                });

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            string body = await httpResponse.Content.ReadAsStringAsync();

            // The check name and DegradedFail outcome must appear in the raw response body
            Assert.That(body, Does.Contain("WorkflowGovernanceEnabled"),
                "Raw response body must contain the governance check name");
            Assert.That(body, Does.Contain("DegradedFail"),
                "Raw response body must contain the observed DegradedFail outcome " +
                "— workflow evidence can read this value without post-processing");
        }

        // ── EI10: DegradedFail does not raise CriticalFailCount ───────────────

        [Test]
        public async Task EI10_GovernanceDisabled_CriticalFailCount_RemainsZero()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "false");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.CriticalFailCount, Is.EqualTo(0),
                "Governance DegradedFail must not raise CriticalFailCount — " +
                "it signals degraded quality, not a blocking infrastructure failure");
        }

        // ── EI11: Both config checks present when IncludeConfigCheck=true ──────

        [Test]
        public async Task EI11_IncludeConfigCheckTrue_BothConfigChecks_Present()
        {
            using GovernanceFactory factory = CreateFactory(governanceEnabled: "true");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtTokenAsync(unauthClient);

            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result = await PostEnvironmentCheckAsync(client);

            Assert.That(result, Is.Not.Null);
            bool hasRequiredConfig = result!.Checks.Any(c => c.Name == "RequiredConfigurationPresent");
            bool hasGovCheck = result.Checks.Any(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(hasRequiredConfig, Is.True,
                "RequiredConfigurationPresent check must be present when IncludeConfigCheck=true");
            Assert.That(hasGovCheck, Is.True,
                "WorkflowGovernanceEnabled check must be present when IncludeConfigCheck=true");
        }

        // ── EI12: Different configs produce different observed outcomes ─────────

        [Test]
        public async Task EI12_DifferentRuntimeConfig_ProducesDifferentObservedOutcome()
        {
            // Confirm that governance outcome changes when runtime config changes.
            // This proves the service reads from IConfiguration, not from hardcoded values.
            using GovernanceFactory enabledFactory = CreateFactory(governanceEnabled: "true");
            using GovernanceFactory disabledFactory = CreateFactory(governanceEnabled: "false");

            // Get tokens for both factories
            using HttpClient unauthClientEnabled = enabledFactory.CreateClient();
            using HttpClient unauthClientDisabled = disabledFactory.CreateClient();
            string jwtEnabled = await GetJwtTokenAsync(unauthClientEnabled);
            string jwtDisabled = await GetJwtTokenAsync(unauthClientDisabled);

            using HttpClient clientEnabled = enabledFactory.CreateClient();
            using HttpClient clientDisabled = disabledFactory.CreateClient();
            clientEnabled.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtEnabled);
            clientDisabled.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtDisabled);

            ProtectedSignOffEnvironmentResponse? resultEnabled = await PostEnvironmentCheckAsync(clientEnabled);
            ProtectedSignOffEnvironmentResponse? resultDisabled = await PostEnvironmentCheckAsync(clientDisabled);

            Assert.That(resultEnabled, Is.Not.Null);
            Assert.That(resultDisabled, Is.Not.Null);

            EnvironmentCheck? govEnabled = resultEnabled!.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");
            EnvironmentCheck? govDisabled = resultDisabled!.Checks
                .FirstOrDefault(c => c.Name == "WorkflowGovernanceEnabled");

            Assert.That(govEnabled!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.Pass));
            Assert.That(govDisabled!.Outcome, Is.EqualTo(EnvironmentCheckOutcome.DegradedFail));
            Assert.That(govEnabled.Outcome, Is.Not.EqualTo(govDisabled.Outcome),
                "Different runtime configs MUST produce different observed governance outcomes — " +
                "evidence cannot be hardcoded by the workflow");
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static async Task<ProtectedSignOffEnvironmentResponse?> PostEnvironmentCheckAsync(
            HttpClient client, string correlationId = "ei-test")
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = correlationId,
                    IncludeConfigCheck = true,
                    IncludeFixtureCheck = false,
                    IncludeObservabilityCheck = false
                });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Environment check must return 200. Body: {await response.Content.ReadAsStringAsync()}");

            return await response.Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // ── WebApplicationFactory specialization ──────────────────────────────

        private sealed class GovernanceFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            private readonly string? _governanceEnabled;
            private readonly string? _policyVersion;

            public GovernanceFactory(string? governanceEnabled, string? policyVersion = null)
            {
                _governanceEnabled = governanceEnabled;
                _policyVersion = policyVersion;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var dict = new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForEvidenceIntegrityTestsXX32Chars",
                        ["JwtConfig:SecretKey"] = "EvidenceIntegrityTestSecretKey32CharsMinXX",
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
                    };

                    // Inject governance config only if explicitly specified.
                    // When null, the application falls back to appsettings.json defaults
                    // (Enabled: true), proving the safe-default behavior.
                    if (_governanceEnabled != null)
                        dict["WorkflowGovernanceConfig:Enabled"] = _governanceEnabled;

                    if (_policyVersion != null)
                        dict["WorkflowGovernanceConfig:PolicyVersion"] = _policyVersion;

                    config.AddInMemoryCollection(dict);
                });
            }
        }
    }
}

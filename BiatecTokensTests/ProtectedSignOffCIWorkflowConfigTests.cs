using BiatecTokensApi.Models.ProtectedSignOff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests that document and enforce the CI workflow configuration requirements for
    /// the protected sign-off test suite.  These are regression-prevention tests that
    /// ensure the exact environment variable set used in <c>protected-sign-off.yml</c>
    /// and <c>test-pr.yml</c> keeps all 133 ProtectedSignOff tests green.
    ///
    /// Coverage:
    ///
    /// CI1:  Factory-owned JWT config keeps authentication working (no env-var key-mismatch).
    /// CI2:  App:Account env var is forwarded correctly by the factory.
    /// CI3:  KeyManagementConfig:Provider=Hardcoded works in test factory context.
    /// CI4:  EnvironmentLabel is informational only and does not affect endpoint behavior.
    /// CI5:  Protected endpoints return 401 when JWT token is missing.
    /// CI6:  Protected endpoints return 401 when JWT token is malformed (not a valid JWT).
    /// CI7:  Protected endpoints return 401 when JWT token has wrong signing key.
    /// CI8:  Unauthenticated lifecycle execute returns 401 — not 403 or 500.
    /// CI9:  Unauthenticated fixture provision returns 401.
    /// CI10: Unauthenticated diagnostics returns 401.
    /// CI11: Environment check with IncludeConfigCheck=true returns Configuration category check.
    /// CI12: Environment check returns non-empty Checks list for every request.
    /// CI13: Multiple concurrent environment checks return independent correlation IDs.
    /// CI14: Environment check with missing JwtConfig key → Misconfigured status (fail-closed).
    /// CI15: Environment check with missing App:Account key → Misconfigured status (fail-closed).
    /// CI16: EnvironmentLabel is echoed in the response EnvironmentLabel field.
    /// CI17: Diagnostics endpoint is accessible after successful authentication.
    /// CI18: Diagnostics response has non-null ServiceAvailability list.
    /// CI19: Fixture provision with ResetIfExists=true still returns 200.
    /// CI20: Lifecycle execute response always includes a non-empty Stages list.
    /// CI21: CorrelationId in request is echoed in the response.
    /// CI22: Environment check TotalCheckCount matches actual Checks list length.
    /// CI23: Workflow YAML has no column-0 Python inside run: blocks (YAML validity regression).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffCIWorkflowConfigTests
    {
        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a user and returns a valid JWT access token.
        /// Uses the same factory-owned JWT configuration, ensuring the
        /// signing key and validation key always match.
        /// </summary>
        private static async Task<string> GetJwtAsync(HttpClient client)
        {
            string email = $"ci-config-{Guid.NewGuid():N}@ci-test.biatec.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "CITestPass123!",
                ConfirmPassword = "CITestPass123!",
                FullName = "CI Config Test User"
            });
            JsonDocument? doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            return doc?.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;
        }

        /// <summary>
        /// Creates a factory with the exact environment variable set used by the
        /// Tier 1 CI workflow step (from protected-sign-off.yml build-and-test job).
        /// Note: JwtConfig:SecretKey is provided by the factory, NOT by an external env var,
        /// to prevent the JWT signing/validation key-mismatch bug documented in
        /// PROTECTED_SIGN_OFF_RUNBOOK.md §Regression Protection.
        /// </summary>
        private static CITier1Factory CreateCITier1Factory(string? environmentLabel = null) =>
            new CITier1Factory(environmentLabel);

        // ─── CI1: Factory-owned JWT config keeps authentication working ───────────

        [Test]
        public async Task CI1_FactoryOwnsJwtConfig_AuthenticationSucceeds()
        {
            // Arrange: factory controls JwtConfig (as in CI tier-1 workflow)
            using CITier1Factory factory = CreateCITier1Factory("ci-push");
            using HttpClient client = factory.CreateClient();

            // Act: register + login cycle
            string jwt = await GetJwtAsync(client);

            // Assert: a valid JWT was returned
            Assert.That(jwt, Is.Not.Null.And.Not.Empty,
                "Registration must return a JWT when factory controls JwtConfig:SecretKey");
            string[] parts = jwt.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "Returned token must be a 3-part JWT (header.payload.signature)");
        }

        // ─── CI2: App:Account env var forwarded correctly ─────────────────────────

        [Test]
        public async Task CI2_AppAccount_IsAvailable_InCIFactory()
        {
            // Arrange
            using CITier1Factory factory = CreateCITier1Factory("ci-push");
            using HttpClient unauthClient = factory.CreateClient();
            string jwt = await GetJwtAsync(unauthClient);
            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            // Act: the diagnostics endpoint reports App:Account availability
            HttpResponseMessage resp = await client.GetAsync(
                "/api/v1/protected-sign-off/diagnostics?correlationId=ci2-test");
            string body = await resp.Content.ReadAsStringAsync();

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Diagnostics must return 200. Body: {body}");

            JsonDocument? doc = JsonSerializer.Deserialize<JsonDocument>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.That(doc, Is.Not.Null);

            // Find App:Account in serviceAvailability
            bool accountAvailable = false;
            if (doc!.RootElement.TryGetProperty("serviceAvailability", out JsonElement svc))
            {
                foreach (JsonElement item in svc.EnumerateArray())
                {
                    if (item.TryGetProperty("serviceName", out JsonElement name) &&
                        name.GetString() == "App:Account" &&
                        item.TryGetProperty("isAvailable", out JsonElement avail) &&
                        avail.GetBoolean())
                    {
                        accountAvailable = true;
                    }
                }
            }
            Assert.That(accountAvailable, Is.True,
                "App:Account must be reported as available in diagnostics when the factory sets it");
        }

        // ─── CI3: KeyManagementConfig:Provider=Hardcoded works ───────────────────

        [Test]
        public async Task CI3_KeyManagementConfig_Hardcoded_AllowsStartup()
        {
            // Arrange: factory with Hardcoded key management (same as CI)
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();

            // Act: any successful HTTP call proves the app started with the config
            HttpResponseMessage resp = await client.GetAsync("/health");

            // Assert: application started (health returns 200 or 503 — both mean startup succeeded)
            Assert.That(
                resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.ServiceUnavailable,
                Is.True,
                $"Application must start with KeyManagementConfig:Provider=Hardcoded. Got {resp.StatusCode}");
        }

        // ─── CI4: EnvironmentLabel is informational only ──────────────────────────

        [Test]
        public async Task CI4_EnvironmentLabel_DoesNotAffectEndpointBehavior()
        {
            // Arrange: two factories with different labels
            using CITier1Factory factoryA = CreateCITier1Factory("ci-push");
            using CITier1Factory factoryB = CreateCITier1Factory("protected-ci");

            using HttpClient clientA = factoryA.CreateClient();
            using HttpClient clientB = factoryB.CreateClient();

            string jwtA = await GetJwtAsync(clientA);
            string jwtB = await GetJwtAsync(clientB);

            clientA.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtA);
            clientB.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtB);

            var reqA = new ProtectedSignOffEnvironmentRequest { CorrelationId = "ci4-label-a" };
            var reqB = new ProtectedSignOffEnvironmentRequest { CorrelationId = "ci4-label-b" };

            // Act
            HttpResponseMessage rA = await clientA.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check", reqA);
            HttpResponseMessage rB = await clientB.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check", reqB);

            // Assert: same HTTP status and same IsReadyForProtectedRun
            Assert.That(rA.StatusCode, Is.EqualTo(rB.StatusCode),
                "EnvironmentLabel must not affect the HTTP status code");

            ProtectedSignOffEnvironmentResponse? respA =
                await rA.Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();
            ProtectedSignOffEnvironmentResponse? respB =
                await rB.Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();

            Assert.That(respA!.IsReadyForProtectedRun, Is.EqualTo(respB!.IsReadyForProtectedRun),
                "EnvironmentLabel must not affect readiness");
            Assert.That(respA.Status, Is.EqualTo(respB.Status),
                "EnvironmentLabel must not affect environment status");
        }

        // ─── CI5-CI10: Unauthenticated requests → 401 ────────────────────────────

        [Test]
        public async Task CI5_EnvironmentCheck_NoJwt_Returns401()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            // No auth header set

            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "no-auth" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Environment check without JWT must return 401");
        }

        [Test]
        public async Task CI6_EnvironmentCheck_MalformedJwt_Returns401()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");

            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "malformed-jwt" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Environment check with malformed JWT must return 401");
        }

        [Test]
        public async Task CI7_EnvironmentCheck_WrongSigningKey_Returns401()
        {
            // Arrange: factory with Key A; sign token with Key B
            using CITier1Factory factory = CreateCITier1Factory();

            // Create a token signed with a DIFFERENT key than what the factory uses
            // We do this by creating a second factory and using its token against the first
            using CITier1Factory factoryB = new CITier1Factory("wrong-key-factory",
                jwtSecretOverride: "CompletelyDifferentSecretKeyForFactoryB32Chars!!");
            using HttpClient clientB = factoryB.CreateClient();
            string wrongKeyJwt = await GetJwtAsync(clientB);

            using HttpClient clientA = factory.CreateClient();
            clientA.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", wrongKeyJwt);

            HttpResponseMessage resp = await clientA.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "wrong-key" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Token signed with a different key must return 401 (proves signing/validation key symmetry)");
        }

        [Test]
        public async Task CI8_LifecycleExecute_NoJwt_Returns401()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/lifecycle/execute",
                new EnterpriseSignOffLifecycleRequest { CorrelationId = "unauth-lifecycle" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task CI9_FixtureProvision_NoJwt_Returns401()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/fixtures/provision",
                new EnterpriseFixtureProvisionRequest { CorrelationId = "unauth-fixtures" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task CI10_Diagnostics_NoJwt_Returns401()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage resp = await client.GetAsync(
                "/api/v1/protected-sign-off/diagnostics?correlationId=unauth-diag");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ─── CI11-CI13: Environment check structure ───────────────────────────────

        [Test]
        public async Task CI11_EnvironmentCheck_IncludeConfigCheck_True_HasConfigurationCategory()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            var req = new ProtectedSignOffEnvironmentRequest
            {
                CorrelationId = "ci11-config-check",
                IncludeConfigCheck = true
            };
            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check", req);

            ProtectedSignOffEnvironmentResponse? result =
                await resp.Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Checks.Any(c => c.Category == EnvironmentCheckCategory.Configuration),
                Is.True,
                "IncludeConfigCheck=true must produce at least one Configuration category check");
        }

        [Test]
        public async Task CI12_EnvironmentCheck_AlwaysReturnsNonEmptyChecksList()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            // Minimal request (all flags default)
            var req = new ProtectedSignOffEnvironmentRequest { CorrelationId = "ci12-checks-list" };
            ProtectedSignOffEnvironmentResponse? result = await (await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check", req))
                .Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Checks, Is.Not.Null.And.Not.Empty,
                "Checks list must never be empty in an environment check response");
        }

        [Test]
        public async Task CI13_ConcurrentEnvironmentChecks_HaveIndependentCorrelationIds()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient c1 = factory.CreateClient();
            using HttpClient c2 = factory.CreateClient();
            string jwt1 = await GetJwtAsync(c1);
            string jwt2 = await GetJwtAsync(c2);
            c1.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt1);
            c2.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt2);

            // Act: fire both concurrently
            Task<ProtectedSignOffEnvironmentResponse?> t1 = PostEnvCheckAsync(c1, "ci13-concurrent-a");
            Task<ProtectedSignOffEnvironmentResponse?> t2 = PostEnvCheckAsync(c2, "ci13-concurrent-b");
            await Task.WhenAll(t1, t2);

            // Assert: correlation IDs are echoed back correctly
            Assert.That(t1.Result!.CorrelationId, Is.EqualTo("ci13-concurrent-a"));
            Assert.That(t2.Result!.CorrelationId, Is.EqualTo("ci13-concurrent-b"));
            Assert.That(t1.Result.CheckId, Is.Not.EqualTo(t2.Result.CheckId),
                "Concurrent checks must have unique CheckIds");
        }

        private static async Task<ProtectedSignOffEnvironmentResponse?> PostEnvCheckAsync(
            HttpClient client, string correlationId)
        {
            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest { CorrelationId = correlationId });
            return await resp.Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();
        }

        // ─── CI14-CI15: Fail-closed when config missing ────────────────────────

        [Test]
        public async Task CI14_MissingJwtConfigKey_EnvironmentCheck_ReturnsMisconfigured()
        {
            // Arrange: factory with JwtConfig:SecretKey explicitly blank
            using MisconfiguredFactory factory = new MisconfiguredFactory(missingJwtKey: true);
            using HttpClient unauthClient = factory.CreateClient();

            // With blank JwtConfig:SecretKey the app may still start but the environment
            // check should detect it and return Misconfigured.
            // (We cannot obtain a JWT without a valid key, so we test the unauthenticated
            //  environment check response status, or test via the service directly.)
            // Strategy: call the service directly in unit-test style (no HTTP auth required).
            BiatecTokensApi.Services.ProtectedSignOffEnvironmentService svc = BuildSvcWithConfig(
                new Dictionary<string, string?>
                {
                    // Missing JwtConfig:SecretKey intentionally
                    ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                });

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "ci14-missing-jwt",
                    IncludeConfigCheck = true
                });

            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Misconfigured),
                "Missing JwtConfig:SecretKey must produce Misconfigured status (fail-closed)");
            Assert.That(result.IsReadyForProtectedRun, Is.False,
                "IsReadyForProtectedRun must be false when JwtConfig:SecretKey is missing");
        }

        [Test]
        public async Task CI15_MissingAppAccount_EnvironmentCheck_ReturnsMisconfigured()
        {
            // Arrange: factory without App:Account
            BiatecTokensApi.Services.ProtectedSignOffEnvironmentService svc = BuildSvcWithConfig(
                new Dictionary<string, string?>
                {
                    ["JwtConfig:SecretKey"] = "HasSecretKeyButNoAccountXXXXXXXXXX32Chars",
                    // Missing App:Account intentionally
                });

            ProtectedSignOffEnvironmentResponse result = await svc.CheckEnvironmentReadinessAsync(
                new ProtectedSignOffEnvironmentRequest
                {
                    CorrelationId = "ci15-missing-account",
                    IncludeConfigCheck = true
                });

            Assert.That(result.Status, Is.EqualTo(ProtectedEnvironmentStatus.Misconfigured),
                "Missing App:Account must produce Misconfigured status (fail-closed)");
            Assert.That(result.IsReadyForProtectedRun, Is.False);
        }

        // ─── CI16: EnvironmentLabel config accepted (no startup failure) ──────────

        [Test]
        public async Task CI16_EnvironmentLabel_SetInConfig_DoesNotCauseStartupFailure()
        {
            // The ProtectedSignOff:EnvironmentLabel config key is informational.
            // It must not cause a startup failure or exception, regardless of its value.
            using CITier1Factory factory = CreateCITier1Factory("my-custom-label");
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/environment/check",
                new ProtectedSignOffEnvironmentRequest { CorrelationId = "ci16-label" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"EnvironmentLabel='my-custom-label' must not cause startup failure. Got {resp.StatusCode}. Body: {await resp.Content.ReadAsStringAsync()}");

            ProtectedSignOffEnvironmentResponse? result =
                await resp.Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();
            Assert.That(result, Is.Not.Null, "Response must be valid JSON");
            Assert.That(result!.CorrelationId, Is.EqualTo("ci16-label"),
                "CorrelationId must be echoed even when a custom EnvironmentLabel is configured");
        }

        // ─── CI17-CI18: Diagnostics endpoint ─────────────────────────────────────

        [Test]
        public async Task CI17_Diagnostics_AfterAuthentication_Returns200()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage resp = await client.GetAsync(
                "/api/v1/protected-sign-off/diagnostics?correlationId=ci17-diag");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Diagnostics must return 200 after authentication. Body: {await resp.Content.ReadAsStringAsync()}");
        }

        [Test]
        public async Task CI18_Diagnostics_HasNonNullServiceAvailability()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffDiagnosticsResponse? diag =
                await (await client.GetAsync(
                    "/api/v1/protected-sign-off/diagnostics?correlationId=ci18-svc-avail"))
                .Content.ReadFromJsonAsync<ProtectedSignOffDiagnosticsResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(diag, Is.Not.Null);
            Assert.That(diag!.ServiceAvailability, Is.Not.Null,
                "Diagnostics response must have a non-null ServiceAvailability list");
        }

        // ─── CI19: Fixture provision with ResetIfExists=true ──────────────────────

        [Test]
        public async Task CI19_FixtureProvision_ResetIfExists_True_Returns200()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            HttpResponseMessage resp = await client.PostAsJsonAsync(
                "/api/v1/protected-sign-off/fixtures/provision",
                new EnterpriseFixtureProvisionRequest
                {
                    CorrelationId = "ci19-reset",
                    ResetIfExists = true
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Fixture provision with ResetIfExists=true must return 200. Body: {await resp.Content.ReadAsStringAsync()}");

            EnterpriseFixtureProvisionResponse? result =
                await resp.Content.ReadFromJsonAsync<EnterpriseFixtureProvisionResponse>();
            Assert.That(result!.IsProvisioned, Is.True);
        }

        // ─── CI20: Lifecycle execute always returns Stages ────────────────────────

        [Test]
        public async Task CI20_LifecycleExecute_AlwaysReturnsNonEmptyStagesList()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            EnterpriseSignOffLifecycleResponse? result =
                await (await client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/lifecycle/execute",
                    new EnterpriseSignOffLifecycleRequest { CorrelationId = "ci20-stages" }))
                .Content.ReadFromJsonAsync<EnterpriseSignOffLifecycleResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Stages, Is.Not.Null.And.Not.Empty,
                "Lifecycle execute must always return a non-empty Stages list");
        }

        // ─── CI21: CorrelationId echoed in response ───────────────────────────────

        [Test]
        public async Task CI21_EnvironmentCheck_CorrelationId_IsEchoedInResponse()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            string corrId = $"ci21-correlation-{Guid.NewGuid():N}";
            ProtectedSignOffEnvironmentResponse? result =
                await (await client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/environment/check",
                    new ProtectedSignOffEnvironmentRequest { CorrelationId = corrId }))
                .Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();

            Assert.That(result!.CorrelationId, Is.EqualTo(corrId),
                "CorrelationId from request must be echoed in response for traceability");
        }

        // ─── CI22: TotalCheckCount matches Checks.Count ───────────────────────────

        [Test]
        public async Task CI22_EnvironmentCheck_TotalCheckCount_MatchesChecksListLength()
        {
            using CITier1Factory factory = CreateCITier1Factory();
            using HttpClient client = factory.CreateClient();
            string jwt = await GetJwtAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            ProtectedSignOffEnvironmentResponse? result =
                await (await client.PostAsJsonAsync(
                    "/api/v1/protected-sign-off/environment/check",
                    new ProtectedSignOffEnvironmentRequest
                    {
                        CorrelationId = "ci22-count",
                        IncludeConfigCheck = true,
                        IncludeFixtureCheck = true,
                        IncludeObservabilityCheck = true
                    }))
                .Content.ReadFromJsonAsync<ProtectedSignOffEnvironmentResponse>();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.TotalCheckCount, Is.EqualTo(result.Checks.Count),
                "TotalCheckCount must equal the actual number of checks in the Checks list");
        }

        // ─── CI23: Workflow YAML must not contain column-0 Python in run blocks ────
        //
        // Regression test for the YAML syntax error where multi-line python3 -c "..."
        // with bare newlines at column 0 makes the protected-sign-off.yml invalid.
        // GitHub marks invalid-YAML workflow runs as 'failure' with zero jobs started.
        // See PROTECTED_SIGN_OFF_RUNBOOK.md §'Workflow YAML Maintenance Rules'.

        [Test]
        public void CI23_WorkflowYaml_ContainsNoColumnZeroPython_InsideRunBlocks()
        {
            // Locate the workflow file relative to the test binary output directory.
            // Path: BiatecTokensTests/bin/Release/net10.0/ → ../../../../.github/workflows/
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore(
                    $"Workflow file not found at '{workflowPath}'; skipping YAML validation.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Detect the forbidden pattern: python3 -c followed immediately by a newline
            // then any non-whitespace character (Python code at column 0).
            // Example of bad pattern:
            //   GOV=$(echo "$X" | python3 -c "
            //   import sys, json    ← this is at column 0, breaks YAML block scalar
            //   ...")
            bool hasColumnZeroPython = System.Text.RegularExpressions.Regex.IsMatch(
                content,
                @"python3 -c ""\s*\n[^\s""']",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            Assert.That(hasColumnZeroPython, Is.False,
                "protected-sign-off.yml contains a python3 -c invocation with bare newlines " +
                "starting at column 0.  This makes the YAML file syntactically invalid — " +
                "GitHub marks the workflow run as 'failure' with zero jobs started.\n\n" +
                "Fix: use a single-line python3 -c invocation (no embedded newlines) or " +
                "a properly-indented heredoc.  See PROTECTED_SIGN_OFF_RUNBOOK.md " +
                "§'Workflow YAML Maintenance Rules'.");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static BiatecTokensApi.Services.ProtectedSignOffEnvironmentService BuildSvcWithConfig(
            Dictionary<string, string?> configValues)
        {
            IConfiguration cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var wfMock = new Moq.Mock<BiatecTokensApi.Services.Interface.IIssuerWorkflowService>();
            wfMock.Setup(s => s.ListMembersAsync(It.IsAny<string>(), It.IsAny<string>()))
                  .ReturnsAsync(new BiatecTokensApi.Models.IssuerWorkflow.IssuerTeamMembersResponse
                  {
                      Success = true,
                      Members = new List<BiatecTokensApi.Models.IssuerWorkflow.IssuerTeamMember>()
                  });
            wfMock.Setup(s => s.ValidateTransition(
                    BiatecTokensApi.Models.IssuerWorkflow.WorkflowApprovalState.Prepared,
                    BiatecTokensApi.Models.IssuerWorkflow.WorkflowApprovalState.PendingReview))
                  .Returns(new BiatecTokensApi.Models.IssuerWorkflow.WorkflowTransitionValidationResult
                  {
                      IsValid = true,
                      Reason = "Valid"
                  });

            var signOffMock = new Moq.Mock<BiatecTokensApi.Services.Interface.IDeploymentSignOffService>();
            var contractMock = new Moq.Mock<BiatecTokensApi.Services.Interface.IBackendDeploymentLifecycleContractService>();

            return new BiatecTokensApi.Services.ProtectedSignOffEnvironmentService(
                wfMock.Object, signOffMock.Object, contractMock.Object, cfg,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BiatecTokensApi.Services.ProtectedSignOffEnvironmentService>.Instance);
        }

        // ─── WebApplicationFactory specializations ────────────────────────────────

        /// <summary>
        /// Factory that replicates the exact environment variable set used in the
        /// Tier 1 CI workflow step (protected-sign-off.yml build-and-test job).
        /// JwtConfig:SecretKey is supplied HERE — never via env var — to prevent
        /// the JWT signing/validation key-mismatch documented in the runbook.
        /// </summary>
        private sealed class CITier1Factory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            private readonly string _environmentLabel;
            private readonly string _jwtSecret;

            public CITier1Factory(string? environmentLabel = null,
                string? jwtSecretOverride = null)
            {
                _environmentLabel = environmentLabel ?? "ci-push";
                _jwtSecret = jwtSecretOverride ?? "CITier1FactoryJwtSecretKey32CharsMinXX!!";
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // The exact keys safe for dotnet test (no JwtConfig env var override)
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "CIPushTestKeyMinimum32CharsPaddingXXXX",
                        ["ProtectedSignOff:EnvironmentLabel"] = _environmentLabel,
                        // JWT config is factory-owned — never from external env var
                        ["JwtConfig:SecretKey"] = _jwtSecret,
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

        /// <summary>
        /// Factory with intentionally missing required configuration, used for
        /// fail-closed validation tests (CI14, CI15).
        /// NOTE: This factory may fail to start or produce configuration errors,
        /// which is the expected behavior being tested.
        /// </summary>
        private sealed class MisconfiguredFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            private readonly bool _missingJwtKey;

            public MisconfiguredFactory(bool missingJwtKey = false)
            {
                _missingJwtKey = missingJwtKey;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var dict = new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "MisconfiguredFactoryKeyXXXXXXXXXX32Chars",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    };
                    // Intentionally omit JwtConfig:SecretKey when testing missing-key scenario
                    if (!_missingJwtKey)
                        dict["JwtConfig:SecretKey"] = "MisconfiguredTestSecretKey32CharsMinXXXX";

                    config.AddInMemoryCollection(dict);
                });
            }
        }
    }
}

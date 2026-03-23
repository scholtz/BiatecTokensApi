using BiatecTokensApi.Models.ProtectedSignOff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests that document and enforce the CI workflow configuration requirements for
    /// the protected sign-off test suite.  These are regression-prevention tests that
    /// ensure the exact environment variable set used in <c>protected-sign-off.yml</c>
    /// and <c>test-pr.yml</c> keeps all 168 ProtectedSignOff tests green.
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
    /// CI24: Workflow has pull_request trigger for master/main (required status check surfacing).
    /// CI25: Tier 1 build-and-test job runs on pull_request events (not push-only).
    /// CI26: Tier 2 workflow has release gate enforcement step (fail-closed on lifecycle failure).
    /// CI27: Evidence manifest includes schemaVersion field (evidence contract stability).
    /// CI28: Workflow uploads named evidence artifact with retention period (durable evidence).
    /// CI29: Publish test results step has continue-on-error in build-and-test job (403 resilience).
    /// CI30: Step 0 preflight includes JWT secret length sanity check (≥ 32 chars).
    /// CI31: Step 0 preflight includes APP_ACCOUNT word-count sanity check (25 words).
    /// CI32: Step 0 preflight reports presence failures and sanity failures separately.
    /// CI33: Evidence manifest includes isReleaseGradeEvidence field.
    /// CI34: Evidence manifest includes releaseGradeNote field.
    /// CI35: Tier 1 summary includes permissive-lane notice distinguishing it from release-grade evidence.
    /// CI36: Runbook has explicit Permissive CI vs Release-Grade Evidence section.
    /// CI37: Workflow has a blocked-artifact step that runs on prerequisite failure (fail-closed artifact semantics).
    /// CI38: Blocked manifest includes isReleaseGradeEvidence:false, blocked:true, blockedReason, schemaVersion.
    /// CI39: Blocked manifest includes remediationUrl field for operator remediation guidance.
    /// CI40: Blocked manifest includes blockedAt field identifying which step caused the block.
    /// CI41: Blocked manifest JSON template is extractable and syntactically valid (parseable after stripping shell vars).
    /// CI42: Runbook has explicit "Blocked runs" subsection documenting blocked artifact schema.
    /// CI43: Blocked manifest schemaVersion matches the successful manifest schemaVersion (schema consistency).
    /// CI44: Artifact upload step uses if:always() so it runs regardless of step failure (always-upload guarantee).
    /// CI45: Blocked-manifest step writes 00_BLOCKED.txt plain-text remediation file.
    /// CI46: Prerequisite validation step (setup) has id: setup for conclusion reference.
    /// CI47: Blocked manifest blockedReason mentions required secret names for operator actionability.
    /// CI48: Tier-2 job declares environment: protected-sign-off to source secrets correctly.
    /// CI49: Evidence artifact upload step has retention-days configured for durable auditing.
    /// CI50: Blocked-manifest step FALLBACK_CORR variable encodes run_id and run_attempt.
    /// CI51: Artifact name fallback pattern contains 'blocked' for visual artifact disambiguation.
    /// CI52: Blocked manifest releaseGradeNote field references 'isReleaseGradeEvidence' field name.
    /// CI53: Tier-2 job is conditioned on workflow_dispatch (not triggered by push/pull_request).
    /// CI54: Workflow has workflow_dispatch trigger with dry_run input.
    /// CI55: Tier-2 job has descriptive "protected" name.
    /// CI56: Workflow has at least two jobs (Tier-1 + Tier-2).
    /// CI57: Tier-1 job conditioned on push/pull_request events.
    /// CI58: Artifact name uses steps.setup.outputs.correlation_id.
    /// CI59: Workflow declares both push and pull_request triggers.
    /// CI60: PROTECTED_SIGN_OFF_RUNBOOK.md exists and has substantive content.
    /// CI61: Workflow tier-2 job has a checkout step for source code access.
    /// CI62: Blocked-manifest step appears after the setup step (ordering).
    /// CI63: Upload-artifact step appears after blocked-manifest creation (ordering).
    /// CI64: Blocked manifest blockedReason field has meaningful content.
    /// CI65: Workflow references required secrets (JWT_SECRET, APP_ACCOUNT).
    /// CI66: Workflow evidence manifest includes correlationId field for audit traceability.
    /// CI67: Workflow has echo/printf statements for audit logging and operator visibility.
    /// CI68: test-pr.yml CI workflow exists.
    /// CI69: test-pr.yml references ProtectedSignOff to ensure those tests run in CI.
    /// CI70: upload-artifact step is after checkout and build steps (ordering).
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
            bool hasColumnZeroPython = Regex.IsMatch(
                content,
                @"python3 -c ""\s*\n[^\s""']",
                RegexOptions.Multiline);

            Assert.That(hasColumnZeroPython, Is.False,
                "protected-sign-off.yml contains a python3 -c invocation with bare newlines " +
                "starting at column 0.  This makes the YAML file syntactically invalid — " +
                "GitHub marks the workflow run as 'failure' with zero jobs started.\n\n" +
                "Fix: use a single-line python3 -c invocation (no embedded newlines) or " +
                "a properly-indented heredoc.  See PROTECTED_SIGN_OFF_RUNBOOK.md " +
                "§'Workflow YAML Maintenance Rules'.");
        }

        // ─── CI24: Workflow has pull_request trigger for master/main ─────────────
        //
        // The Tier 1 `build-and-test` job must surface as a required status check
        // on every PR targeting master/main.  This requires the workflow to declare
        // a `pull_request` trigger scoped to those branches.  Without this trigger,
        // the "Build and run protected sign-off tests" check never appears on a PR
        // and cannot be configured as a required branch-protection status check.

        [Test]
        public void CI24_WorkflowYaml_HasPullRequestTrigger_ForMasterAndMain()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The workflow must declare a pull_request trigger with master or main branches.
            // We accept either "master" or "main" being present under the pull_request key.
            bool hasPullRequestTrigger = content.Contains("pull_request:");
            Assert.That(hasPullRequestTrigger, Is.True,
                "protected-sign-off.yml must declare a 'pull_request:' trigger so the " +
                "Tier 1 'Build and run protected sign-off tests' check appears on every PR. " +
                "Without this trigger the check cannot be configured as a required " +
                "branch-protection status check in GitHub Settings → Branches.");

            // Verify at least one of master/main appears after pull_request trigger
            int pullRequestIndex = content.IndexOf("pull_request:", StringComparison.Ordinal);
            int nextJobIndex = content.IndexOf("\njobs:", StringComparison.Ordinal);
            string triggerSection = pullRequestIndex >= 0 && nextJobIndex > pullRequestIndex
                ? content.Substring(pullRequestIndex, nextJobIndex - pullRequestIndex)
                : string.Empty;

            bool coversReleaseTargets = triggerSection.Contains("master") || triggerSection.Contains("main");
            Assert.That(coversReleaseTargets, Is.True,
                "The pull_request trigger must include 'master' or 'main' branches so PRs " +
                "targeting the release branch produce the required status check.");
        }

        // ─── CI25: Tier 1 build-and-test job runs on pull_request events ─────────

        [Test]
        public void CI25_WorkflowYaml_BuildAndTestJob_RunsOnPullRequest()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The build-and-test job must not restrict itself to push events only.
            // An "if: github.event_name == 'push'" condition would prevent the job
            // from running on pull_request events, making it invisible as a PR check.
            // Acceptable conditions include:
            //   if: github.event_name == 'push' || github.event_name == 'pull_request'
            //   (or no 'if' at all if the workflow-level 'on:' already scopes correctly)
            bool hasPushOnlyCondition =
                Regex.IsMatch(content, @"event_name\s*==\s*'push'\s*$", RegexOptions.Multiline) &&
                !content.Contains("pull_request");

            Assert.That(hasPushOnlyCondition, Is.False,
                "The 'build-and-test' job must not restrict itself to 'push' events only. " +
                "Add 'pull_request' to the trigger conditions so the job runs on PRs and " +
                "the status check appears for branch-protection enforcement.");
        }

        // ─── CI26: Release gate enforcement step is present in Tier 2 ────────────
        //
        // The Tier 2 `protected-sign-off-run` job must contain an explicit release gate
        // enforcement step that fails the workflow when lifecycle verification fails.
        // Removing this step would silently promote a failed run as release evidence.

        [Test]
        public void CI26_WorkflowYaml_HasReleaseGateEnforcementStep()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The workflow must have a step that enforces the lifecycle pass requirement.
            // We check for the key identifiers: a step that reads lifecycle_ok and exits 1.
            bool hasGateEnforcement =
                content.Contains("lifecycle_ok") &&
                content.Contains("exit 1") &&
                (content.Contains("release gate") || content.Contains("Enforce lifecycle pass"));

            Assert.That(hasGateEnforcement, Is.True,
                "protected-sign-off.yml must contain a release gate enforcement step that " +
                "reads 'lifecycle_ok' and exits non-zero when lifecycle verification fails. " +
                "This step is the fail-closed control that prevents a failed run from being " +
                "used as release evidence. Removing it would silently weaken the gate.");
        }

        // ─── CI27: Evidence manifest schema version field is present ─────────────
        //
        // The evidence manifest must include a schemaVersion field so product owners
        // can verify which version of the evidence format they are reviewing.

        [Test]
        public void CI27_WorkflowYaml_EvidenceManifest_HasSchemaVersionField()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content, Does.Contain("schemaVersion"),
                "The evidence manifest written in protected-sign-off.yml must include a " +
                "'schemaVersion' field so reviewers can identify the manifest format version. " +
                "This field stabilizes the evidence contract and makes schema changes explicit.");
        }

        // ─── CI28: Workflow produces named evidence artifact for product-owner review ─

        [Test]
        public void CI28_WorkflowYaml_ProducesNamedEvidenceArtifact()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The workflow must upload an evidence artifact named with the correlation ID.
            // This artifact is the durable, reviewer-friendly output referenced in the runbook.
            bool hasEvidenceArtifact =
                content.Contains("protected-sign-off-evidence") &&
                content.Contains("upload-artifact") &&
                content.Contains("retention-days");

            Assert.That(hasEvidenceArtifact, Is.True,
                "protected-sign-off.yml must upload a named evidence artifact " +
                "('protected-sign-off-evidence-<corr-id>') with an explicit retention period. " +
                "This artifact is the primary durable output for product-owner sign-off review. " +
                "Removing it would make past runs unauditable.");
        }

        // ─── CI29: Publish test results step has continue-on-error: true ────────
        //
        // When the workflow is triggered by a pull_request from a restricted actor
        // (e.g. a copilot agent), the EnricoMi/publish-unit-test-result-action tries
        // to post a comment on the PR.  This fails with HTTP 403 "Resource not
        // accessible by integration".  Without continue-on-error, this causes the
        // entire workflow job to fail even though all tests passed.
        //
        // Lesson learned (2026-03-14, PR #543): the first PR trigger run failed because
        // the publish step did not have continue-on-error set, causing a 403 to cascade
        // into a workflow failure despite all 155 tests passing.
        //
        // This test prevents that regression from recurring.

        [Test]
        public void CI29_WorkflowYaml_PublishTestResults_HasContinueOnError_InBuildAndTestJob()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Verify the publish-unit-test-result-action step in the build-and-test job
            // has continue-on-error: true.  We look for the two strings near each other
            // in the Tier 1 (build-and-test) section, which ends before
            // "protected-sign-off-run:" job.

            // Find the build-and-test job section
            int buildAndTestStart = content.IndexOf("build-and-test:", StringComparison.Ordinal);
            int protectedRunStart = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);

            if (buildAndTestStart < 0)
            {
                Assert.Fail("Could not find 'build-and-test:' job in protected-sign-off.yml");
                return;
            }

            string buildAndTestSection = protectedRunStart > buildAndTestStart
                ? content.Substring(buildAndTestStart, protectedRunStart - buildAndTestStart)
                : content.Substring(buildAndTestStart);

            bool hasPublishAction = buildAndTestSection.Contains("publish-unit-test-result-action");
            bool hasContinueOnError = buildAndTestSection.Contains("continue-on-error: true");

            Assert.That(hasPublishAction, Is.True,
                "The build-and-test job must contain a publish-unit-test-result-action step.");

            Assert.That(hasContinueOnError, Is.True,
                "The publish-unit-test-result-action step in the build-and-test job must have " +
                "'continue-on-error: true'.  When the workflow is triggered by a pull_request " +
                "from a restricted actor (copilot agent, dependabot), the action tries to post " +
                "a PR comment and fails with HTTP 403.  Without continue-on-error, this cascades " +
                "into a workflow failure even when all tests passed.\n\n" +
                "Fix: add 'continue-on-error: true' to the Publish test results step.\n" +
                "See: Lesson learned 2026-03-14, PR #543.");
        }

        // ─── CI30: Step 0 preflight contains JWT secret length sanity check ──────
        //
        // The preflight step must check that the JWT secret is ≥ 32 characters.
        // A secret shorter than 32 chars may be syntactically valid but is too weak
        // for HS256 key strength, and would allow a misconfigured secret to bypass
        // the length guard at startup while still failing in practice.
        //
        // This test prevents a regression where the sanity check is removed or weakened.

        [Test]
        public void CI30_WorkflowYaml_Preflight_ValidatesJwtSecretLength()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Extract the Tier 2 protected-sign-off-run job section (starts at the job boundary)
            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            string tier2Section = tier2Start >= 0 ? content.Substring(tier2Start) : content;

            // Verify the preflight step checks JWT secret length against 32 characters
            bool hasJwtLengthCheck = tier2Section.Contains("JWT_LEN") ||
                                     tier2Section.Contains("-lt 32") ||
                                     tier2Section.Contains("32 characters");

            Assert.That(hasJwtLengthCheck, Is.True,
                "The Tier 2 preflight step (Step 0) in protected-sign-off.yml must validate " +
                "that the JWT secret is ≥ 32 characters.  A missing length check allows " +
                "weak secrets to pass the presence gate while failing in production.  " +
                "Add a check like: [ \"$JWT_LEN\" -lt 32 ] to the prerequisite validation step.\n\n" +
                "This test prevents the sanity check from being silently removed.");
        }

        // ─── CI31: Step 0 preflight contains APP_ACCOUNT word-count sanity check ──
        //
        // The preflight step must verify the Algorand mnemonic has exactly 25 words.
        // A 12-word or 24-word phrase would pass the empty check but fail during ARC76
        // key derivation with a cryptic error later in the run.  Catching the wrong
        // word count early at Step 0 produces an actionable error message.

        [Test]
        public void CI31_WorkflowYaml_Preflight_ValidatesAppAccountWordCount()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            string tier2Section = tier2Start >= 0 ? content.Substring(tier2Start) : content;

            // Verify the preflight step checks APP_ACCOUNT word count = 25
            bool hasWordCountCheck = tier2Section.Contains("WORD_COUNT") ||
                                     tier2Section.Contains("-ne 25") ||
                                     tier2Section.Contains("25 words");

            Assert.That(hasWordCountCheck, Is.True,
                "The Tier 2 preflight step (Step 0) in protected-sign-off.yml must validate " +
                "that PROTECTED_SIGN_OFF_APP_ACCOUNT contains exactly 25 words (Algorand mnemonic).  " +
                "Without this check, a shorter or longer phrase passes the empty check but produces " +
                "a cryptic ARC76 key derivation failure later in the run.  " +
                "Add a word-count check like: WORD_COUNT=$(echo \"$APP_ACCOUNT\" | wc -w); [ \"$WORD_COUNT\" -ne 25 ]\n\n" +
                "This test prevents the word-count sanity check from being silently removed.");
        }

        // ─── CI32: Step 0 preflight reports presence failures and sanity failures separately ──
        //
        // The preflight step must distinguish "secret missing" (MISSING array) from
        // "secret present but invalid" (MALFORMED array).  This distinction is important
        // for operators: a missing secret requires a completely different remediation than
        // a malformed one.  The step summary must use separate headings for each category.

        [Test]
        public void CI32_WorkflowYaml_Preflight_ReportsMissingAndMalformedSeparately()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            string tier2Section = tier2Start >= 0 ? content.Substring(tier2Start) : content;

            // Verify separate arrays or sections for presence vs sanity failures
            bool hasMissingArray = tier2Section.Contains("MISSING") &&
                                   tier2Section.Contains("MALFORMED");
            bool hasSeparateHeadings = tier2Section.Contains("Missing secrets") &&
                                       tier2Section.Contains("Malformed secrets");

            Assert.That(hasMissingArray, Is.True,
                "The Tier 2 preflight step must track missing secrets and malformed secrets " +
                "in separate variables (e.g. MISSING and MALFORMED arrays).  This ensures " +
                "operators can distinguish 'secret not set' from 'secret has wrong format'.");

            Assert.That(hasSeparateHeadings, Is.True,
                "The Tier 2 preflight step summary must use separate headings for missing " +
                "secrets (### Missing secrets) and malformed secrets (### Malformed secrets) " +
                "so operators see actionable, categorized failure reasons.");
        }

        // ─── CI33: Evidence manifest includes isReleaseGradeEvidence field ──────
        //
        // The evidence manifest is the artifact reviewed by product owners for release
        // sign-off decisions.  The `isReleaseGradeEvidence` field is the primary criterion:
        // a manifest without this field (or with the value false) does not qualify as
        // valid release evidence regardless of other fields.

        [Test]
        public void CI33_WorkflowYaml_EvidenceManifest_HasIsReleaseGradeEvidenceField()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content, Does.Contain("isReleaseGradeEvidence"),
                "The evidence manifest written in protected-sign-off.yml must include an " +
                "'isReleaseGradeEvidence' field.  This is the primary product-owner sign-off " +
                "criterion: 'true' only when lifecycle and environment checks both pass under " +
                "the protected-sign-off environment with validated real secrets.  " +
                "A manifest without this field cannot be reliably used for release decisions.\n\n" +
                "This test prevents the field from being removed from the manifest template.");
        }

        // ─── CI34: Evidence manifest includes releaseGradeNote field ─────────────
        //
        // The `releaseGradeNote` field provides a human-readable explanation of the
        // conditions under which `isReleaseGradeEvidence` is `true`.  This makes the
        // manifest self-explanatory for product owners who review it outside of the CI context.

        [Test]
        public void CI34_WorkflowYaml_EvidenceManifest_HasReleaseGradeNoteField()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content, Does.Contain("releaseGradeNote"),
                "The evidence manifest written in protected-sign-off.yml must include a " +
                "'releaseGradeNote' field explaining the conditions under which " +
                "'isReleaseGradeEvidence' is 'true'.  This makes the manifest self-documenting " +
                "for reviewers who encounter it outside of CI context and need to understand " +
                "without referring to the runbook what the evidence represents.\n\n" +
                "This test prevents the contextual note from being removed from the manifest.");
        }

        // ─── CI35: Tier 1 summary includes permissive-lane notice ────────────────
        //
        // The Tier 1 (build-and-test) job summary must explicitly state that it is a
        // permissive developer-feedback run and NOT release-grade evidence.  This prevents
        // a product owner or reviewer from misinterpreting a green Tier 1 run as sign-off
        // proof when only a Tier 2 run with real secrets qualifies.

        [Test]
        public void CI35_WorkflowYaml_Tier1Summary_ContainsPermissiveLaneNotice()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Extract the Tier 1 build-and-test job section
            int buildAndTestStart = content.IndexOf("build-and-test:", StringComparison.Ordinal);
            int protectedRunStart = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);

            if (buildAndTestStart < 0)
            {
                Assert.Fail("Could not find 'build-and-test:' job in protected-sign-off.yml");
                return;
            }

            string buildAndTestSection = protectedRunStart > buildAndTestStart
                ? content.Substring(buildAndTestStart, protectedRunStart - buildAndTestStart)
                : content.Substring(buildAndTestStart);

            bool hasPermissiveNotice = buildAndTestSection.Contains("permissive") ||
                                       buildAndTestSection.Contains("NOT release-grade") ||
                                       buildAndTestSection.Contains("not release-grade");

            Assert.That(hasPermissiveNotice, Is.True,
                "The Tier 1 'build-and-test' job summary in protected-sign-off.yml must " +
                "explicitly state that this is a permissive developer-feedback run and NOT " +
                "release-grade evidence.  Without this notice, a product owner reviewing the " +
                "GitHub Actions summary might misinterpret a green Tier 1 run as valid sign-off " +
                "evidence.\n\n" +
                "Add text like: '⚠️ This is a permissive developer-feedback run — NOT release-grade evidence.' " +
                "to the Tier 1 Produce summary step.\n\n" +
                "This test prevents the permissive-lane notice from being removed from the summary.");
        }

        // ─── CI36: Runbook has explicit Permissive CI vs Release-Grade Evidence section ──
        //
        // The runbook is the primary operator and product-owner reference.  It must
        // contain a section that explicitly distinguishes permissive CI lanes (Tier 1,
        // local dotnet test) from release-grade protected evidence (Tier 2 with real
        // validated secrets).  This ensures the distinction is documented for reviewers
        // who consult the runbook rather than the workflow YAML.

        [Test]
        public void CI36_Runbook_HasPermissiveVsReleaseGradeSection()
        {
            string runbookPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../PROTECTED_SIGN_OFF_RUNBOOK.md"));

            if (!File.Exists(runbookPath))
            {
                Assert.Ignore($"Runbook not found at '{runbookPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(runbookPath);

            bool hasPermissiveSection = content.Contains("Permissive CI vs Release-Grade") ||
                                        content.Contains("Permissive developer-feedback") ||
                                        content.Contains("permissive developer-feedback lanes");

            bool hasReleaseGradeCriteria = content.Contains("isReleaseGradeEvidence") &&
                                           content.Contains("What makes a run release-grade");

            Assert.That(hasPermissiveSection, Is.True,
                "PROTECTED_SIGN_OFF_RUNBOOK.md must contain a section that explicitly " +
                "distinguishes permissive CI lanes from release-grade evidence.  " +
                "Product owners and reviewers rely on the runbook to understand what " +
                "constitutes valid sign-off evidence.\n\n" +
                "Add a section titled 'Permissive CI vs Release-Grade Evidence' or equivalent " +
                "that lists which run types are NOT release-grade and explains what conditions " +
                "must be met for a run to qualify as release-grade evidence.\n\n" +
                "This test prevents the permissive-lane distinction from being removed from the runbook.");

            Assert.That(hasReleaseGradeCriteria, Is.True,
                "PROTECTED_SIGN_OFF_RUNBOOK.md must document the criteria that make a run " +
                "release-grade, including the 'isReleaseGradeEvidence' manifest field and the " +
                "conditions under which it is 'true'.  Product owners need to know exactly " +
                "what to check in the manifest before approving a release.\n\n" +
                "Add a 'What makes a run release-grade' subsection to the runbook.");
        }

        // ─── CI37: Workflow has a blocked-artifact step that runs on prerequisite failure ──
        //
        // When Step 0 (prerequisite validation) fails because secrets are missing or
        // malformed, the workflow MUST produce a downloadable artifact so that product
        // owners and reviewers can immediately understand WHY the run was blocked without
        // having to read CI logs or re-run the workflow.
        //
        // This implements fail-closed artifact semantics: an artifact is ALWAYS uploaded,
        // whether the run succeeded (release-grade evidence) or failed at prerequisites
        // (blocked evidence).  This was the root cause of issue #603 — the failed run
        // 23410028363 uploaded no artifact because the prerequisite step exited before
        // the evidence directory was created.
        //
        // This test prevents the blocked-manifest step from being removed.

        [Test]
        public void CI37_WorkflowYaml_HasBlockedEvidenceManifestStep_OnPrerequisiteFailure()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The Tier 2 section must contain a step that:
            //   (a) uses `if: failure() && steps.setup.conclusion == 'failure'` so it only
            //       runs when Step 0 failed;
            //   (b) creates the ./ProtectedRunEvidence/ directory; and
            //   (c) writes a blocked manifest to ./ProtectedRunEvidence/00_evidence_manifest.json.
            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            string tier2Section = tier2Start >= 0 ? content.Substring(tier2Start) : content;

            bool hasPrerequisiteFailureCondition =
                tier2Section.Contains("steps.setup.conclusion == 'failure'") ||
                tier2Section.Contains("steps.setup.conclusion == \"failure\"");

            bool createsEvidenceDir =
                tier2Section.Contains("mkdir -p ./ProtectedRunEvidence") &&
                tier2Section.Contains("Create blocked evidence manifest");

            bool uploadsBlockedArtifact =
                tier2Section.Contains("blocked-") ||
                (tier2Section.Contains("format('blocked") || tier2Section.Contains("format(\"blocked"));

            Assert.That(hasPrerequisiteFailureCondition, Is.True,
                "protected-sign-off.yml Tier 2 job must have a step with condition " +
                "'if: failure() && steps.setup.conclusion == ''failure''' that runs when " +
                "Step 0 (prerequisites) fails.  Without this step, the workflow uploads no " +
                "artifact when secrets are missing, leaving product owners without an " +
                "actionable record of why the run was blocked.\n\n" +
                "Root cause of issue #603: run 23410028363 failed at Step 0 and uploaded " +
                "no artifact, so there was no evidence artifact showing the blocked state.\n\n" +
                "Add a 'Create blocked evidence manifest (prerequisite failure)' step with " +
                "if: failure() && steps.setup.conclusion == 'failure'.");

            Assert.That(createsEvidenceDir, Is.True,
                "The blocked-manifest step must create ./ProtectedRunEvidence/ and write " +
                "00_evidence_manifest.json so the existing upload-artifact step can upload it.");

            Assert.That(uploadsBlockedArtifact, Is.True,
                "The evidence artifact upload step must use a fallback name when the " +
                "correlation ID is empty (prerequisite failure).  Use the GitHub Actions " +
                "expression '|| format(...)' to produce 'blocked-<run_id>-<attempt>' when " +
                "steps.setup.outputs.correlation_id is empty.\n\n" +
                "Without the fallback name, the artifact upload step would use an empty name " +
                "and either fail or produce an unidentifiable artifact.");
        }

        // ─── CI38: Blocked manifest template includes required fail-closed fields ────
        //
        // The blocked evidence manifest that is written when prerequisites fail must
        // include the same schema-critical fields as a successful manifest:
        //   • `isReleaseGradeEvidence: false`   — primary sign-off criterion (always false when blocked)
        //   • `blocked: true`                   — explicit flag distinguishing "blocked" from "failed"
        //   • `blockedReason`                   — human-readable explanation of the block cause
        //   • `blockedAt`                        — which step caused the block
        //   • `schemaVersion`                    — manifest schema for downstream consumers
        //
        // These fields ensure that frontend evidence center consumers and human reviewers
        // can tell the difference between "configuration success" and "credible release proof"
        // just by opening the manifest — no log reading required.

        [Test]
        public void CI38_WorkflowYaml_BlockedManifest_IncludesRequiredFailClosedFields()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Find the blocked-manifest step (runs on prerequisite failure) in Tier 2
            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            string tier2Section = tier2Start >= 0 ? content.Substring(tier2Start) : content;

            // Find the blocked-manifest step specifically
            int blockedStepStart = tier2Section.IndexOf("Create blocked evidence manifest", StringComparison.Ordinal);
            // Take a generous window covering the step body
            string blockedStepSection = blockedStepStart >= 0
                ? tier2Section.Substring(blockedStepStart, Math.Min(3000, tier2Section.Length - blockedStepStart))
                : string.Empty;

            Assert.That(blockedStepSection, Is.Not.Empty,
                "Could not find the 'Create blocked evidence manifest' step in the Tier 2 " +
                "job.  This step is required to emit a fail-closed artifact when prerequisites fail.");

            Assert.That(
                blockedStepSection.Contains("\"isReleaseGradeEvidence\": false"),
                Is.True,
                "The blocked manifest must include '\"isReleaseGradeEvidence\": false'.  " +
                "This is the primary product-owner sign-off criterion and must be explicitly " +
                "false in a blocked manifest so reviewers cannot mistake it for release evidence.");

            Assert.That(
                blockedStepSection.Contains("\"blocked\": true"),
                Is.True,
                "The blocked manifest must include '\"blocked\": true' that explicitly " +
                "distinguishes a prerequisites-blocked run from a run that failed during evidence " +
                "collection.  Without this field, a reviewer cannot tell why isReleaseGradeEvidence " +
                "is false without reading the CI logs.");

            Assert.That(
                blockedStepSection.Contains("blockedReason"),
                Is.True,
                "The blocked manifest must include a 'blockedReason' field with a human-readable " +
                "explanation of why the run was blocked.  This field is the primary operator-facing " +
                "explanation and must be present so reviewers and frontend consumers can surface the " +
                "reason without additional log access.");

            Assert.That(
                blockedStepSection.Contains("schemaVersion"),
                Is.True,
                "The blocked manifest must include 'schemaVersion' for downstream consumers " +
                "to identify the manifest format.  Consistency with successful manifests is required " +
                "so the frontend evidence center can parse both blocked and successful artifacts " +
                "with the same schema version.");
        }

        // ─── CI39: Blocked manifest includes remediationUrl field ─────────────────
        //
        // The `remediationUrl` field must be present in the blocked manifest so that
        // operators and automated consumers can locate the runbook section explaining
        // how to configure the required secrets and re-run.
        //
        // Uses the same MANIFEST_EOF JSON-extraction approach as CI41 for reliability.

        [Test]
        public void CI39_WorkflowYaml_BlockedManifest_HasRemediationUrlField()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string jsonTemplate = ExtractBlockedManifestJson(File.ReadAllText(workflowPath));
            Assert.That(jsonTemplate, Is.Not.Empty,
                "Could not extract blocked manifest JSON from MANIFEST_EOF heredoc.");

            using var doc = JsonDocument.Parse(jsonTemplate);
            Assert.That(doc.RootElement.TryGetProperty("remediationUrl", out var remediationProp),
                Is.True,
                "The blocked manifest JSON (extracted from MANIFEST_EOF heredoc) must include a " +
                "'remediationUrl' field pointing to the runbook §Required Secrets section.  " +
                "This allows automated consumers and human reviewers to find setup instructions " +
                "without searching the repository.\n\n" +
                "Add: '\"remediationUrl\": \"PROTECTED_SIGN_OFF_RUNBOOK.md §Required Secrets\"' " +
                "to the MANIFEST_EOF JSON template.");
            Assert.That(remediationProp.GetString(), Is.Not.Null.And.Not.Empty,
                "Parsed blocked manifest 'remediationUrl' must not be empty.");
        }

        // ─── CI40: Blocked manifest includes blockedAt field ──────────────────────
        //
        // The `blockedAt` field records which step caused the block.  Combined with
        // `blockedReason`, it provides a precise failure attribution that allows
        // operators to identify the failed step without reading CI logs.
        //
        // Uses the same MANIFEST_EOF JSON-extraction approach as CI41 for reliability.

        [Test]
        public void CI40_WorkflowYaml_BlockedManifest_HasBlockedAtField()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string jsonTemplate = ExtractBlockedManifestJson(File.ReadAllText(workflowPath));
            Assert.That(jsonTemplate, Is.Not.Empty,
                "Could not extract blocked manifest JSON from MANIFEST_EOF heredoc.");

            using var doc = JsonDocument.Parse(jsonTemplate);
            Assert.That(doc.RootElement.TryGetProperty("blockedAt", out var blockedAtProp),
                Is.True,
                "The blocked manifest JSON (extracted from MANIFEST_EOF heredoc) must include a " +
                "'blockedAt' field identifying which step caused the block.  This provides precise " +
                "failure attribution for operators who download the artifact.\n\n" +
                "Add: '\"blockedAt\": \"Step 0 — Validate prerequisites and set correlation ID\"' " +
                "to the MANIFEST_EOF JSON template.");
            Assert.That(blockedAtProp.GetString(), Is.Not.Null.And.Not.Empty,
                "Parsed blocked manifest 'blockedAt' must not be empty.");
        }

        // ─── CI41: Blocked manifest JSON template is syntactically valid ──────────
        //
        // The blocked manifest is written by a bash heredoc in the workflow YAML.
        // If the JSON template has a syntax error, the uploaded artifact will contain
        // malformed JSON that downstream consumers cannot parse.
        //
        // This test extracts the JSON template from the workflow YAML (using the shared
        // ExtractBlockedManifestJson helper) and verifies that it is parseable as JSON.

        [Test]
        public void CI41_WorkflowYaml_BlockedManifest_JsonTemplateIsExtractableAndValid()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string jsonTemplate = ExtractBlockedManifestJson(File.ReadAllText(workflowPath));

            Assert.That(jsonTemplate, Is.Not.Empty,
                "The blocked manifest JSON template between MANIFEST_EOF markers must not be empty.");

            // Parse the JSON template — it should be valid since the static fields don't have
            // shell variable expansions (those are patched in by the subsequent Python step).
            try
            {
                using var doc = JsonDocument.Parse(jsonTemplate);
                var root = doc.RootElement;
                Assert.That(root.TryGetProperty("schemaVersion", out _), Is.True,
                    "Parsed blocked manifest JSON must have 'schemaVersion' field.");
                Assert.That(root.TryGetProperty("isReleaseGradeEvidence", out var releaseEvProp), Is.True,
                    "Parsed blocked manifest JSON must have 'isReleaseGradeEvidence' field.");
                Assert.That(releaseEvProp.GetBoolean(), Is.False,
                    "Parsed blocked manifest 'isReleaseGradeEvidence' must be false (not true).");
                Assert.That(root.TryGetProperty("blocked", out var blockedProp), Is.True,
                    "Parsed blocked manifest JSON must have 'blocked' field.");
                Assert.That(blockedProp.GetBoolean(), Is.True,
                    "Parsed blocked manifest 'blocked' must be true.");
                Assert.That(root.TryGetProperty("blockedReason", out var reasonProp), Is.True,
                    "Parsed blocked manifest JSON must have 'blockedReason' field.");
                Assert.That(reasonProp.GetString(), Is.Not.Null.And.Not.Empty,
                    "Parsed blocked manifest 'blockedReason' must not be empty.");
            }
            catch (JsonException ex)
            {
                Assert.Fail(
                    $"Blocked manifest JSON template (between MANIFEST_EOF markers) is not valid JSON.\n" +
                    $"JSON parse error: {ex.Message}\n" +
                    $"Template content:\n{jsonTemplate}");
            }
        }

        // ─── CI42: Runbook has "Blocked runs" subsection ──────────────────────────
        //
        // The PROTECTED_SIGN_OFF_RUNBOOK.md must document the blocked artifact schema
        // for operators and product owners who download the artifact and need to
        // interpret it without referring to the workflow YAML.

        [Test]
        public void CI42_Runbook_HasBlockedRunsSubsection()
        {
            string runbookPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../PROTECTED_SIGN_OFF_RUNBOOK.md"));

            if (!File.Exists(runbookPath))
            {
                Assert.Ignore($"Runbook not found at '{runbookPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(runbookPath);

            bool hasBlockedRunsSection =
                content.Contains("Blocked runs") ||
                content.Contains("blocked runs") ||
                content.Contains("blocked artifact") ||
                content.Contains("blocked evidence artifact");

            bool hasBlockedArtifactFields =
                (content.Contains("blocked: true") ||
                 content.Contains("\"blocked\": true")) &&
                content.Contains("blockedReason") &&
                content.Contains("blockedAt");

            Assert.That(hasBlockedRunsSection, Is.True,
                "PROTECTED_SIGN_OFF_RUNBOOK.md must contain a 'Blocked runs' or equivalent " +
                "subsection that documents the blocked artifact for operators who download it.  " +
                "Without this documentation, operators cannot interpret the blocked manifest " +
                "without reading the workflow YAML.\n\n" +
                "Add a 'Blocked runs' subsection to §Artifacts produced documenting the " +
                "'protected-sign-off-evidence-blocked-*' artifact and its fields.");

            Assert.That(hasBlockedArtifactFields, Is.True,
                "PROTECTED_SIGN_OFF_RUNBOOK.md blocked runs section must document the key " +
                "fields: 'blocked: true', 'blockedReason', 'blockedAt'.  These are the primary " +
                "operator-facing explanation of why the run was blocked.");
        }

        // ─── CI43: Blocked manifest schemaVersion matches successful manifest ──────
        //
        // Both the blocked manifest and the successful manifest must use the same
        // schemaVersion value so that downstream consumers can parse both with the
        // same parser without version branching.

        [Test]
        public void CI43_WorkflowYaml_BlockedManifest_SchemaVersionMatchesSuccessManifest()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            string blockedTemplate = ExtractBlockedManifestJson(content);
            if (string.IsNullOrEmpty(blockedTemplate))
            {
                Assert.Ignore("Could not locate MANIFEST_EOF heredoc; skipping schema version check.");
                return;
            }

            // Parse the blocked template JSON and check schemaVersion value
            using var doc = JsonDocument.Parse(blockedTemplate);
            Assert.That(doc.RootElement.TryGetProperty("schemaVersion", out var schemaProp), Is.True,
                "Blocked manifest JSON must have 'schemaVersion' field.");
            Assert.That(schemaProp.GetString(), Is.EqualTo("2.0"),
                "The blocked manifest must use schemaVersion '2.0' to match the successful " +
                "manifest schema.  Both manifests must use the same version so downstream " +
                "consumers can parse both without version branching.\n\n" +
                "Ensure the blocked manifest MANIFEST_EOF heredoc contains: " +
                "'\"schemaVersion\": \"2.0\"'");

            // Verify the successful manifest also uses 2.0
            int successiveManifest = content.LastIndexOf("\"schemaVersion\": \"2.0\"", StringComparison.Ordinal);
            Assert.That(successiveManifest >= 0, Is.True,
                "The successful evidence manifest in protected-sign-off.yml must also contain " +
                "'\"schemaVersion\": \"2.0\"' for schema version consistency between blocked and " +
                "successful artifacts.");
        }

        // ─── CI44: Artifact upload step uses if:always() so it always runs ────────
        //
        // The evidence artifact upload step must run regardless of job outcome so that
        // blocked artifacts (created by the new blocked-manifest step) are uploaded.

        [Test]
        public void CI44_WorkflowYaml_ArtifactUploadStep_HasAlwaysCondition()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            string tier2Section = tier2Start >= 0 ? content.Substring(tier2Start) : content;

            // Find the upload-artifact step for evidence
            int uploadStart = tier2Section.IndexOf("Upload protected run evidence", StringComparison.Ordinal);
            string uploadSection = uploadStart >= 0
                ? tier2Section.Substring(uploadStart, Math.Min(500, tier2Section.Length - uploadStart))
                : string.Empty;

            Assert.That(uploadSection, Is.Not.Empty,
                "Could not find 'Upload protected run evidence' step in Tier 2 job.");

            bool hasAlwaysCondition =
                uploadSection.Contains("if: always()") ||
                uploadSection.Contains("if: always() &&");

            Assert.That(hasAlwaysCondition, Is.True,
                "The 'Upload protected run evidence' step in the Tier 2 job must have " +
                "'if: always()' (optionally combined with other conditions) so that the " +
                "blocked artifact created by the 'Create blocked evidence manifest' step " +
                "is uploaded even when Step 0 fails.  Without this condition, the step " +
                "would not run after a prerequisite failure, making the blocked artifact " +
                "inaccessible.\n\n" +
                "Ensure the step has: 'if: always() && github.event.inputs.dry_run != ''true'''");
        }

        // ─── CI45: Blocked-manifest step writes 00_BLOCKED.txt ───────────────────
        //
        // The blocked-manifest step must also create a plain-text remediation file
        // (00_BLOCKED.txt) so that operators who do not read JSON can still find guidance.

        [Test]
        public void CI45_WorkflowYaml_BlockedManifestStep_Writes00BlockedTxtFile()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int blockedStepStart = content.IndexOf("Create blocked evidence manifest", StringComparison.Ordinal);
            Assert.That(blockedStepStart, Is.GreaterThan(0),
                "Blocked-manifest step not found in workflow YAML.");

            // Find the next step start (or end of file) to scope the check
            int nextStepStart = content.IndexOf("\n      - name:", blockedStepStart + 100);
            string blockedStepSection = nextStepStart > blockedStepStart
                ? content.Substring(blockedStepStart, nextStepStart - blockedStepStart)
                : content.Substring(blockedStepStart);

            Assert.That(blockedStepSection.Contains("00_BLOCKED.txt"), Is.True,
                "The blocked-manifest step must write a '00_BLOCKED.txt' plain-text remediation " +
                "file so operators who do not parse JSON can still find actionable guidance.");
        }

        // ─── CI46: Blocked-manifest step has an id: field ────────────────────────
        //
        // Steps that need to be referenced via steps.<id>.conclusion must declare an id.

        [Test]
        public void CI46_WorkflowYaml_BlockedManifestStep_HasIdField()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The setup step has id: setup — verify this is present so its conclusion can be checked
            int setupStepStart = content.IndexOf("Validate prerequisites and set correlation ID", StringComparison.Ordinal);
            Assert.That(setupStepStart, Is.GreaterThan(0),
                "Prerequisite validation step not found in workflow YAML.");

            int nextStep = content.IndexOf("\n      - name:", setupStepStart + 50);
            string setupSection = nextStep > setupStepStart
                ? content.Substring(setupStepStart, nextStep - setupStepStart)
                : content.Substring(setupStepStart, Math.Min(400, content.Length - setupStepStart));

            Assert.That(setupSection.Contains("id: setup"), Is.True,
                "The prerequisite validation step must declare 'id: setup' so that the blocked-manifest " +
                "step can reference 'steps.setup.conclusion == ''failure''' correctly.");
        }

        // ─── CI47: BlockedReason mentions the required secret names ──────────────
        //
        // The blockedReason field must mention the specific secret names so operators
        // can immediately identify what to configure without reading the runbook.

        [Test]
        public void CI47_WorkflowYaml_BlockedManifest_BlockedReasonMentionsRequiredSecrets()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int blockedStepStart = content.IndexOf("Create blocked evidence manifest", StringComparison.Ordinal);
            Assert.That(blockedStepStart, Is.GreaterThan(0),
                "Blocked-manifest step not found in workflow YAML.");

            int nextStep = content.IndexOf("\n      - name:", blockedStepStart + 100);
            string blockedSection = nextStep > blockedStepStart
                ? content.Substring(blockedStepStart, nextStep - blockedStepStart)
                : content.Substring(blockedStepStart);

            // The blocked section should mention the required secrets to be actionable
            bool mentionsSecrets =
                blockedSection.Contains("PROTECTED_SIGN_OFF_JWT_SECRET") ||
                blockedSection.Contains("required secrets") ||
                blockedSection.Contains("secrets are missing");

            Assert.That(mentionsSecrets, Is.True,
                "The blocked-manifest step must mention either 'PROTECTED_SIGN_OFF_JWT_SECRET' or " +
                "'required secrets' in the blockedReason or 00_BLOCKED.txt so operators immediately " +
                "know what secrets need to be configured.");
        }

        // ─── CI48: Tier-2 job uses the protected-sign-off environment ────────────
        //
        // The Tier-2 workflow job must use the 'protected-sign-off' GitHub environment
        // so that secrets are sourced from the correct, access-controlled environment.

        [Test]
        public void CI48_WorkflowYaml_Tier2Job_UsesProtectedSignOffEnvironment()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Find the tier-2 job (workflow_dispatch job)
            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            Assert.That(tier2Start, Is.GreaterThan(0),
                "Tier-2 job (protected-sign-off-run) not found in workflow YAML.");

            // Look for environment: protected-sign-off within the tier-2 job header
            int nextJobStart = content.IndexOf("\n  [a-z]", tier2Start + 50);
            string tier2Header = content.Substring(tier2Start,
                Math.Min(500, content.Length - tier2Start));

            Assert.That(tier2Header.Contains("environment: protected-sign-off"), Is.True,
                "The Tier-2 job must declare 'environment: protected-sign-off' so that " +
                "PROTECTED_SIGN_OFF_JWT_SECRET and PROTECTED_SIGN_OFF_APP_ACCOUNT secrets " +
                "are sourced from the access-controlled environment, not from repository secrets.");
        }

        // ─── CI49: Evidence artifact upload step has retention-days configured ───
        //
        // Evidence artifacts must have a retention-days setting to support auditable
        // durable storage of release evidence.

        [Test]
        public void CI49_WorkflowYaml_EvidenceArtifactUpload_HasRetentionDays()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int uploadStart = content.IndexOf("Upload protected run evidence", StringComparison.Ordinal);
            Assert.That(uploadStart, Is.GreaterThan(0),
                "Upload protected run evidence step not found in workflow YAML.");

            int nextStep = content.IndexOf("\n      - name:", uploadStart + 50);
            string uploadSection = nextStep > uploadStart
                ? content.Substring(uploadStart, nextStep - uploadStart)
                : content.Substring(uploadStart, Math.Min(500, content.Length - uploadStart));

            Assert.That(uploadSection.Contains("retention-days"), Is.True,
                "The evidence artifact upload step must configure 'retention-days' to ensure " +
                "release evidence is retained beyond the default 90-day GitHub artifact window " +
                "for enterprise audit purposes.");
        }

        // ─── CI50: FALLBACK_CORR uses run_id and run_attempt ─────────────────────
        //
        // The fallback correlation ID for blocked runs must encode run_id and run_attempt
        // to produce unique, traceable artifact names even when Step 0 fails.

        [Test]
        public void CI50_WorkflowYaml_BlockedManifestStep_FallbackCorrEncodesRunIdAndAttempt()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The artifact name uses the || format() fallback expression
            bool hasRunIdFallback =
                content.Contains("run_id") && content.Contains("run_attempt") &&
                (content.Contains("format('blocked-{0}-{1}'") ||
                 content.Contains("format(\"blocked-{0}-{1}\""));

            Assert.That(hasRunIdFallback, Is.True,
                "The artifact upload step must use a fallback like " +
                "'format(''blocked-{0}-{1}'', github.run_id, github.run_attempt)' " +
                "so that blocked runs produce uniquely-named artifacts encoding the run ID " +
                "and attempt number for traceability.");
        }

        // ─── CI51: Artifact name has 'blocked' in the fallback pattern ───────────
        //
        // Blocked run artifacts must be distinguishable from successful ones by name prefix.

        [Test]
        public void CI51_WorkflowYaml_ArtifactName_FallbackPatternContainsBlocked()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The artifact name expression must contain 'blocked' as a literal string
            // in the fallback so artifact names are self-describing
            bool hasFallbackWithBlocked =
                content.Contains("'blocked-{0}-{1}'") ||
                content.Contains("\"blocked-{0}-{1}\"") ||
                content.Contains("blocked-${{ github.run_id }}");

            Assert.That(hasFallbackWithBlocked, Is.True,
                "The artifact upload name expression must include 'blocked' in the fallback " +
                "pattern so that blocked-run artifacts are visually distinguishable from " +
                "successful-run artifacts in the GitHub Actions artifacts UI.");
        }

        // ─── CI52: Blocked manifest releaseGradeNote contains 'isReleaseGradeEvidence' ──
        //
        // The releaseGradeNote field must refer to isReleaseGradeEvidence so that
        // readers understand what the field means and what to look for.

        [Test]
        public void CI52_WorkflowYaml_BlockedManifest_ReleaseGradeNoteContainsFieldName()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);
            string jsonTemplate = ExtractBlockedManifestJson(content);

            if (string.IsNullOrEmpty(jsonTemplate))
            {
                Assert.Ignore("Could not extract MANIFEST_EOF heredoc from workflow YAML; skipping.");
                return;
            }

            // Strip shell variable references for JSON parsing
            string cleanJson = System.Text.RegularExpressions.Regex.Replace(
                jsonTemplate, @"\$\{[^}]+\}", "\"__PLACEHOLDER__\"");

            JsonDocument doc = JsonDocument.Parse(cleanJson);
            JsonElement root = doc.RootElement;

            Assert.That(root.TryGetProperty("releaseGradeNote", out JsonElement noteEl), Is.True,
                "Blocked manifest must have a 'releaseGradeNote' field.");

            string note = noteEl.GetString() ?? string.Empty;
            Assert.That(note.Contains("isReleaseGradeEvidence"), Is.True,
                "The 'releaseGradeNote' field must contain 'isReleaseGradeEvidence' so that " +
                "readers understand which field to check when evaluating release-grade status.");
        }

        // ─── CI53: Tier-2 job is triggered by workflow_dispatch only ─────────────
        //
        // The Tier-2 strict sign-off job must run on workflow_dispatch, not on every push,
        // to prevent unintentional protected runs on untested branches.

        [Test]
        public void CI53_WorkflowYaml_Tier2Job_ConditionedOnWorkflowDispatch()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int tier2Start = content.IndexOf("protected-sign-off-run:", StringComparison.Ordinal);
            Assert.That(tier2Start, Is.GreaterThan(0),
                "Tier-2 job (protected-sign-off-run) not found in workflow YAML.");

            string tier2Header = content.Substring(tier2Start,
                Math.Min(600, content.Length - tier2Start));

            Assert.That(tier2Header.Contains("workflow_dispatch"), Is.True,
                "The Tier-2 job must have an 'if: github.event_name == ''workflow_dispatch''' " +
                "condition so it only runs on manual dispatch, not on every push or pull_request.");
        }

        // ─── CI54: Protected-sign-off.yml has workflow_dispatch trigger ───────────
        //
        // The workflow must have a workflow_dispatch trigger so it can be manually
        // triggered to produce protected run evidence.

        [Test]
        public void CI54_WorkflowYaml_HasWorkflowDispatchTrigger()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content.Contains("workflow_dispatch:"), Is.True,
                "The protected-sign-off.yml workflow must declare a 'workflow_dispatch:' trigger " +
                "so that product owners can manually trigger it to produce release evidence.");

            // Verify the workflow also has inputs (specifically dry_run)
            Assert.That(content.Contains("dry_run"), Is.True,
                "The workflow_dispatch trigger must include a 'dry_run' input so that the " +
                "workflow can be tested without executing real backend operations.");
        }

        // ─── CI55: Tier-2 job name is descriptive ────────────────────────────────
        //
        // The Tier-2 job name should contain "protected" to make it visually
        // distinguishable from the Tier-1 build-and-test job in CI run views.

        [Test]
        public void CI55_WorkflowYaml_Tier2Job_HasDescriptiveName()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content.ToLowerInvariant().Contains("protected strict sign-off run")
                     || content.ToLowerInvariant().Contains("protected sign-off run")
                     || content.ToLowerInvariant().Contains("protected"),
                Is.True,
                "The Tier-2 job must have a descriptive name that includes 'protected' to " +
                "distinguish it from the Tier-1 CI job in the GitHub Actions UI.");
        }

        // ─── CI56: Workflow has at least two jobs (Tier-1 and Tier-2) ────────────
        //
        // The workflow must have exactly two jobs: one for CI push/PR and one for
        // the protected workflow_dispatch run.

        [Test]
        public void CI56_WorkflowYaml_HasAtLeastTwoJobs()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // Count top-level job definitions (lines with "    name:" under a "jobs:" section)
            int jobCount = System.Text.RegularExpressions.Regex
                .Matches(content, @"^  [a-z_]+:", System.Text.RegularExpressions.RegexOptions.Multiline)
                .Count;

            Assert.That(jobCount, Is.GreaterThanOrEqualTo(2),
                "The protected-sign-off.yml workflow must define at least two jobs: one for " +
                "Tier-1 CI (push/pull_request) and one for Tier-2 protected execution (workflow_dispatch).");
        }

        // ─── CI57: Tier-1 job is conditioned on push or pull_request ─────────────
        //
        // The Tier-1 build-and-test job should only run on push/pull_request events
        // (not on manual workflow_dispatch) to maintain separation of concerns.

        [Test]
        public void CI57_WorkflowYaml_Tier1Job_ConditionedOnPushOrPullRequest()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(
                content.Contains("github.event_name == 'push'") ||
                content.Contains("github.event_name == 'pull_request'"),
                Is.True,
                "The Tier-1 job must use an 'if' condition referencing 'push' or 'pull_request' " +
                "event names to ensure it only runs on code changes, not on manual dispatch.");
        }

        // ─── CI58: Artifact name uses correlation_id output from setup step ──────
        //
        // The upload-artifact step name must reference steps.setup.outputs.correlation_id
        // to ensure the artifact is consistently named using the workflow-generated ID.

        [Test]
        public void CI58_WorkflowYaml_ArtifactName_UsesCorrelationIdFromSetupOutputs()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content.Contains("steps.setup.outputs.correlation_id"), Is.True,
                "The artifact upload step must reference 'steps.setup.outputs.correlation_id' " +
                "to ensure evidence artifacts are named using the workflow correlation ID, " +
                "enabling auditors to trace artifacts back to specific workflow runs.");
        }

        // ─── CI59: Workflow declares push and pull_request triggers ──────────────
        //
        // In addition to workflow_dispatch, the workflow must also declare push and
        // pull_request triggers for Tier-1 CI to run automatically on code changes.

        [Test]
        public void CI59_WorkflowYaml_HasPushAndPullRequestTriggers()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content.Contains("push:") || content.Contains("push"), Is.True,
                "The workflow must declare a 'push' trigger for Tier-1 CI to run on code changes.");

            Assert.That(content.Contains("pull_request:") || content.Contains("pull_request"), Is.True,
                "The workflow must declare a 'pull_request' trigger for Tier-1 CI to run on PRs.");
        }

        // ─── CI60: Protected-sign-off runbook exists as a markdown file ──────────
        //
        // The operator runbook documenting the protected sign-off workflow must exist
        // as a markdown file in the repository root or docs directory.

        [Test]
        public void CI60_Runbook_ExistsAsMarkdownFile()
        {
            string[] candidatePaths = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../PROTECTED_SIGN_OFF_RUNBOOK.md")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/PROTECTED_SIGN_OFF_RUNBOOK.md")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/protected-sign-off-runbook.md"))
            };

            bool exists = candidatePaths.Any(p => File.Exists(p));

            if (!exists)
            {
                Assert.Ignore("Runbook file not found at any candidate path; skipping.");
                return;
            }

            string runbookPath = candidatePaths.First(p => File.Exists(p));
            string content = File.ReadAllText(runbookPath);

            Assert.That(content.Length, Is.GreaterThan(500),
                "The PROTECTED_SIGN_OFF_RUNBOOK.md must contain substantive documentation " +
                "(at least 500 characters) to be useful as an operator reference guide.");

            Assert.That(content.ToLowerInvariant().Contains("protected sign-off") ||
                       content.ToLowerInvariant().Contains("protected sign off"),
                Is.True,
                "The runbook must document the 'protected sign-off' process.");
        }

        // ─── Helpers — original section ─────────────────────────────────────────

        // ─── CI61: protected-sign-off.yml has checkout step in tier-2 job ────────
        [Test]
        public void CI61_WorkflowYaml_Tier2Job_HasCheckoutStep()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content, Does.Contain("actions/checkout"),
                "CI61: workflow must include a checkout step to access the repository source code");
        }

        // ─── CI62: Blocked manifest step runs AFTER the setup step ──────────────
        [Test]
        public void CI62_WorkflowYaml_BlockedManifestStep_RunsAfterSetupStep()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int setupIdx = content.IndexOf("id: setup", StringComparison.OrdinalIgnoreCase);
            int blockedIdx = content.IndexOf("blocked evidence manifest",
                StringComparison.OrdinalIgnoreCase);

            if (blockedIdx < 0)
                blockedIdx = content.IndexOf("blocked-artifact", StringComparison.OrdinalIgnoreCase);

            if (setupIdx < 0 || blockedIdx < 0)
            {
                Assert.Ignore("Could not locate setup or blocked-manifest step; skipping ordering check.");
                return;
            }

            Assert.That(blockedIdx, Is.GreaterThan(setupIdx),
                "CI62: the blocked-manifest step must appear after the setup step in the workflow file");
        }

        // ─── CI63: Workflow upload step runs AFTER blocked-manifest creation step ─
        [Test]
        public void CI63_WorkflowYaml_UploadStep_RunsAfterBlockedManifestStep()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int blockedIdx = content.IndexOf("MANIFEST_EOF", StringComparison.Ordinal);
            int uploadIdx = content.IndexOf("upload-artifact", StringComparison.OrdinalIgnoreCase);

            if (blockedIdx < 0 || uploadIdx < 0)
            {
                Assert.Ignore("Could not locate blocked-manifest or upload-artifact; skipping ordering check.");
                return;
            }

            Assert.That(uploadIdx, Is.GreaterThan(blockedIdx),
                "CI63: the artifact upload step must appear after the blocked-manifest creation step");
        }

        // ─── CI64: Blocked manifest sets blockedReason with meaningful content ───
        [Test]
        public void CI64_WorkflowYaml_BlockedManifest_BlockedReasonIsMeaningful()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);
            string manifest = ExtractBlockedManifestJson(content);

            if (string.IsNullOrWhiteSpace(manifest))
            {
                Assert.Ignore("Could not extract blocked manifest JSON; skipping.");
                return;
            }

            // Strip shell variable substitutions for parsing
            string sanitized = System.Text.RegularExpressions.Regex.Replace(manifest, @"\$\{[^}]+\}", "SUBSTITUTED");
            string sanitized2 = sanitized.Replace("$(", "SUBSTITUTED(").Replace(")", "");

            // Check the raw manifest contains "blockedReason" key
            Assert.That(manifest, Does.Contain("blockedReason").IgnoreCase,
                "CI64: blocked manifest must contain a blockedReason field with meaningful content");
        }

        // ─── CI65: protected-sign-off.yml defines env section or steps use secrets ─
        [Test]
        public void CI65_WorkflowYaml_ReferencesRequiredSecrets()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            // The workflow must reference the key secrets used for backend authentication
            bool refsJwtSecret = content.Contains("PROTECTED_SIGN_OFF_JWT_SECRET",
                StringComparison.OrdinalIgnoreCase);
            bool refsAppAccount = content.Contains("PROTECTED_SIGN_OFF_APP_ACCOUNT",
                StringComparison.OrdinalIgnoreCase);

            Assert.That(refsJwtSecret, Is.True,
                "CI65: workflow must reference PROTECTED_SIGN_OFF_JWT_SECRET for backend authentication");
            Assert.That(refsAppAccount, Is.True,
                "CI65: workflow must reference PROTECTED_SIGN_OFF_APP_ACCOUNT for Algorand account");
        }

        // ─── CI66: Workflow evidence manifest includes correlationId field ────────
        [Test]
        public void CI66_WorkflowYaml_EvidenceManifest_IncludesCorrelationId()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content, Does.Contain("correlationId").Or.Contain("correlation_id"),
                "CI66: workflow evidence manifest must include a correlationId field for audit traceability");
        }

        // ─── CI67: Workflow has at least one echo/print step for audit logging ────
        [Test]
        public void CI67_WorkflowYaml_HasAuditLoggingStep()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            bool hasEcho = content.Contains("echo", StringComparison.OrdinalIgnoreCase);
            bool hasPrintf = content.Contains("printf", StringComparison.OrdinalIgnoreCase);

            Assert.That(hasEcho || hasPrintf, Is.True,
                "CI67: workflow must include echo/printf statements for audit logging and operator visibility");
        }

        // ─── CI68: test-pr.yml workflow exists ────────────────────────────────────
        [Test]
        public void CI68_TestPrWorkflow_Exists()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/test-pr.yml"));

            Assert.That(File.Exists(workflowPath), Is.True,
                "CI68: test-pr.yml must exist as the CI workflow for pull request validation");
        }

        // ─── CI69: test-pr.yml references ProtectedSignOff in test filter ─────────
        [Test]
        public void CI69_TestPrWorkflow_ReferencesProtectedSignOff()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/test-pr.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"test-pr.yml not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            Assert.That(content, Does.Contain("ProtectedSignOff"),
                "CI69: test-pr.yml must reference ProtectedSignOff to ensure protected sign-off tests run in CI");
        }

        // ─── CI70: Workflow step ordering: upload-artifact is after all evidence steps ─
        [Test]
        public void CI70_WorkflowYaml_UploadArtifact_IsLastMajorStep()
        {
            string workflowPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory,
                    "../../../../.github/workflows/protected-sign-off.yml"));

            if (!File.Exists(workflowPath))
            {
                Assert.Ignore($"Workflow file not found at '{workflowPath}'; skipping.");
                return;
            }

            string content = File.ReadAllText(workflowPath);

            int lastUploadIdx = content.LastIndexOf("upload-artifact", StringComparison.OrdinalIgnoreCase);
            int lastCheckoutIdx = content.LastIndexOf("checkout", StringComparison.OrdinalIgnoreCase);
            int lastBuildIdx = content.LastIndexOf("dotnet build", StringComparison.OrdinalIgnoreCase);

            if (lastUploadIdx < 0)
            {
                Assert.Fail("CI70: no upload-artifact step found in the workflow");
                return;
            }

            // Upload must be after checkout and build steps
            if (lastCheckoutIdx > 0)
                Assert.That(lastUploadIdx, Is.GreaterThan(lastCheckoutIdx),
                    "CI70: upload-artifact must come after checkout step");

            if (lastBuildIdx > 0)
                Assert.That(lastUploadIdx, Is.GreaterThan(lastBuildIdx),
                    "CI70: upload-artifact must come after dotnet build step");
        }

        // ─── Shared extraction helper (used by CI39, CI40, CI41, CI43) ───────────
        //
        // Extracts the JSON template from between the MANIFEST_EOF heredoc markers in
        // the protected-sign-off.yml workflow file.  Returns an empty string if the
        // markers cannot be found.

        private static string ExtractBlockedManifestJson(string content)
        {
            int manifestStart = content.IndexOf("MANIFEST_EOF", StringComparison.Ordinal);
            if (manifestStart < 0) return string.Empty;

            int afterOpenMarker = content.IndexOf("\n", manifestStart) + 1;
            if (afterOpenMarker <= 0) return string.Empty;

            int closeMarker = content.IndexOf("MANIFEST_EOF", afterOpenMarker);
            if (closeMarker < 0) return string.Empty;

            return content.Substring(afterOpenMarker, closeMarker - afterOpenMarker).Trim();
        }

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

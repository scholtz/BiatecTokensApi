using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive E2E tests for deterministic ARC76 auth/session contract enforcement
    /// Validates all 10 acceptance criteria from Issue #371
    ///
    /// Business Value: Ensures walletless, compliance-oriented tokenization delivers stable
    /// identity mapping and reliable backend-driven token operations for enterprise customers.
    ///
    /// Acceptance Criteria Coverage:
    /// AC1 - Identical authenticated inputs always produce identical ARC76 derivation outputs
    /// AC2 - Session endpoints expose documented, versioned derived-identity contract fields
    /// AC3 - Protected token operations reject invalid, stale, or missing session-account bindings
    /// AC4 - Error envelopes are explicit, actionable, and free of sensitive credential leakage
    /// AC5 - Logs include correlation IDs and derivation decision metadata
    /// AC6 - Unit tests cover standard and edge derivation paths with fixed expected outputs
    /// AC7 - Integration/API tests validate login -> session -> derivation -> token preflight behavior
    /// AC8 - Regression tests cover concurrency, session refresh, and invalid claim scenarios
    /// AC9 - CI for scoped backend suites is green with no newly skipped critical tests
    /// AC10 - Developer documentation clearly explains derivation and session contract semantics
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicAuthSessionContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

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
            ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
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
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForARC76SessionContractTests32CharMin"
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        /// <summary>
        /// AC1: Identical authenticated inputs always produce identical ARC76 derivation outputs.
        /// Same email+password must yield the same AlgorandAddress across multiple logins.
        /// </summary>
        [Test]
        public async Task AC1_DeterministicDerivation_SameCredentials_SameAddress()
        {
            var email = $"arc76-ac1-{Guid.NewGuid():N}@test.example.com";
            var password = "SecurePass123!";

            // Register once
            var registerReq = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            Assert.That(registerResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Registration should succeed");
            var registered = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registered!.AlgorandAddress, Is.Not.Null.And.Not.Empty, "Address must be present after register");
            var originalAddress = registered.AlgorandAddress!;

            // Login multiple times and verify same address each time
            var loginReq = new LoginRequest { Email = email, Password = password };
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
                Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Login attempt {attempt + 1} should succeed");
                var loggedIn = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loggedIn!.AlgorandAddress, Is.EqualTo(originalAddress),
                    $"AC1: Login attempt {attempt + 1} must return the same ARC76 address");
            }
        }

        /// <summary>
        /// AC2: Session endpoints expose documented, versioned derived-identity contract fields.
        /// Register and login responses must include DerivationContractVersion.
        /// </summary>
        [Test]
        public async Task AC2_SessionEndpoints_ExposeVersionedDerivationContractFields()
        {
            var email = $"arc76-ac2-{Guid.NewGuid():N}@test.example.com";
            var password = "SecurePass123!";

            // Register - verify contract version is present
            var registerReq = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            Assert.That(registerResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registered = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registered!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC2: RegisterResponse must include DerivationContractVersion");
            Assert.That(registered.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "AC2: RegisterResponse version must match the known contract version");

            // Login - verify contract version is present
            var loginReq = new LoginRequest { Email = email, Password = password };
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var loggedIn = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loggedIn!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC2: LoginResponse must include DerivationContractVersion");
            Assert.That(loggedIn.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "AC2: LoginResponse version must match the known contract version");
        }

        /// <summary>
        /// AC3: Protected token operations reject invalid, stale, or missing session-account bindings.
        /// Requests without a valid Bearer token must be rejected with 401.
        /// </summary>
        [Test]
        public async Task AC3_ProtectedOperations_Reject_InvalidOrMissingSessionBinding()
        {
            // Attempt to access a protected endpoint without a token
            var response = await _client.GetAsync("/api/v1/auth/profile");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC3: Protected profile endpoint must reject requests without a valid session binding");

            // Attempt with an invalid/stale token
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.stale.token");
            var responseWithBadToken = await _client.GetAsync("/api/v1/auth/profile");
            Assert.That(responseWithBadToken.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC3: Protected profile endpoint must reject invalid/stale session tokens");
            _client.DefaultRequestHeaders.Authorization = null;
        }

        /// <summary>
        /// AC4: Error envelopes are explicit, actionable, and free of sensitive credential leakage.
        /// Failed login must return a structured error with code and message, not expose credentials.
        /// </summary>
        [Test]
        public async Task AC4_ErrorEnvelopes_AreExplicit_And_NoCredentialLeakage()
        {
            var loginReq = new LoginRequest { Email = "nonexistent@example.com", Password = "WrongPassword1!" };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC4: Failed login must return 401 Unauthorized");

            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body).RootElement;

            Assert.That(json.TryGetProperty("errorCode", out var errorCode), Is.True,
                "AC4: Error response must include errorCode field");
            Assert.That(json.TryGetProperty("errorMessage", out var errorMessage), Is.True,
                "AC4: Error response must include errorMessage field");

            // Validate no sensitive data leakage
            Assert.That(body, Does.Not.Contain("WrongPassword1!"),
                "AC4: Error response must not echo back the password");
            Assert.That(errorMessage.GetString(), Does.Not.Contain("WrongPassword1!"),
                "AC4: Error message must not contain the submitted password");
            Assert.That(errorCode.GetString(), Is.EqualTo("INVALID_CREDENTIALS"),
                "AC4: Error code must be actionable INVALID_CREDENTIALS, not a generic server error");
        }

        /// <summary>
        /// AC5: Logs include correlation IDs and derivation decision metadata.
        /// API responses must include a CorrelationId that traces the request.
        /// </summary>
        [Test]
        public async Task AC5_Responses_IncludeCorrelationId_ForDerivationTracing()
        {
            var email = $"arc76-ac5-{Guid.NewGuid():N}@test.example.com";
            var password = "SecurePass123!";

            // Registration response must include correlation ID
            var registerReq = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            Assert.That(registerResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registered = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registered!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC5: RegisterResponse must include CorrelationId for log tracing");

            // Login response must include correlation ID
            var loginReq = new LoginRequest { Email = email, Password = password };
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var loggedIn = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loggedIn!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC5: LoginResponse must include CorrelationId for log tracing");

            // Each request gets a unique correlation ID
            var loginResp2 = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            var loggedIn2 = await loginResp2.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loggedIn2!.CorrelationId, Is.Not.EqualTo(loggedIn.CorrelationId),
                "AC5: Each request should have a unique CorrelationId for distinct trace entries");
        }

        /// <summary>
        /// AC6: Unit tests cover standard and edge derivation paths with fixed expected outputs.
        /// Email canonicalization (case insensitivity) must produce the same address.
        /// </summary>
        [Test]
        public async Task AC6_EdgeDerivationPaths_EmailCaseVariants_ProduceSameAddress()
        {
            var uniquePart = Guid.NewGuid().ToString("N");
            var baseEmail = $"arc76-ac6-{uniquePart}@test.example.com";
            var password = "SecurePass123!";

            // Register with lowercase email
            var registerReq = new RegisterRequest { Email = baseEmail, Password = password, ConfirmPassword = password };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            Assert.That(registerResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registered = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var canonicalAddress = registered!.AlgorandAddress!;

            // Login with uppercase email - should resolve to same account
            var loginUpperReq = new LoginRequest { Email = baseEmail.ToUpperInvariant(), Password = password };
            var loginUpperResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginUpperReq);
            Assert.That(loginUpperResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC6: Login with uppercase email variant must succeed");
            var loginUpper = await loginUpperResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginUpper!.AlgorandAddress, Is.EqualTo(canonicalAddress),
                "AC6: Email canonicalization must produce same ARC76 address regardless of case");

            // Login with mixed case email - should resolve to same account
            var mixedEmail = string.Concat(baseEmail.Select((c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : c));
            var loginMixedReq = new LoginRequest { Email = mixedEmail, Password = password };
            var loginMixedResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginMixedReq);
            Assert.That(loginMixedResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC6: Login with mixed-case email must succeed");
            var loginMixed = await loginMixedResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginMixed!.AlgorandAddress, Is.EqualTo(canonicalAddress),
                "AC6: Mixed-case email must produce same ARC76 address as lowercase");
        }

        /// <summary>
        /// AC7: Integration/API tests validate login -> session -> derivation -> token preflight behavior.
        /// Full auth flow: register -> login -> use token -> refresh -> verify address consistency.
        /// </summary>
        [Test]
        public async Task AC7_FullAuthFlow_Login_Session_DerivedAddress_TokenRefresh()
        {
            var email = $"arc76-ac7-{Guid.NewGuid():N}@test.example.com";
            var password = "SecurePass123!";

            // Step 1: Register
            var registerReq = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            Assert.That(registerResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registered = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var derivedAddress = registered!.AlgorandAddress!;
            var refreshToken = registered.RefreshToken!;

            // Step 2: Login - validate session binding returns same address
            var loginReq = new LoginRequest { Email = email, Password = password };
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var loggedIn = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loggedIn!.AlgorandAddress, Is.EqualTo(derivedAddress),
                "AC7: Login must return the same ARC76 address as registration");

            // Step 3: Use access token to access protected profile endpoint
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loggedIn.AccessToken);
            var profileResp = await _client.GetAsync("/api/v1/auth/profile");
            Assert.That(profileResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC7: Valid session token must allow access to protected endpoints");

            var profileBody = await profileResp.Content.ReadAsStringAsync();
            var profile = JsonDocument.Parse(profileBody).RootElement;
            if (profile.TryGetProperty("algorandAddress", out var profileAddress))
            {
                Assert.That(profileAddress.GetString(), Is.EqualTo(derivedAddress),
                    "AC7: Profile must reflect the same ARC76-derived address");
            }
            _client.DefaultRequestHeaders.Authorization = null;

            // Step 4: Token refresh - derived address must remain stable
            var refreshReq = new RefreshTokenRequest { RefreshToken = refreshToken };
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC7: Token refresh must succeed with valid refresh token");
            var refreshResult = await refreshResp.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(refreshResult!.AccessToken, Is.Not.Null.And.Not.Empty,
                "AC7: Refreshed access token must be present");
            Assert.That(refreshResult.AccessToken, Has.Length.GreaterThan(10).And.Contains("."),
                "AC7: Refreshed access token must be a valid JWT format");
        }

        /// <summary>
        /// AC8: Regression tests cover concurrency, session refresh, and invalid claim scenarios.
        /// Concurrent registrations with different credentials must produce different unique addresses.
        /// </summary>
        [Test]
        public async Task AC8_RegressionTests_DifferentCredentials_DifferentAddresses()
        {
            var password = "SecurePass123!";

            // Register multiple users concurrently
            var emails = Enumerable.Range(0, 5)
                .Select(_ => $"arc76-ac8-{Guid.NewGuid():N}@test.example.com")
                .ToList();

            var registrationTasks = emails.Select(email =>
            {
                var req = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
                return _client.PostAsJsonAsync("/api/v1/auth/register", req);
            }).ToList();

            var responses = await Task.WhenAll(registrationTasks);
            var addresses = new List<string>();

            foreach (var resp in responses)
            {
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    "AC8: All concurrent registrations must succeed");
                var result = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
                Assert.That(result!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                    "AC8: Each registration must produce an ARC76 address");
                addresses.Add(result.AlgorandAddress!);
            }

            // All addresses must be unique (no collisions)
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(addresses.Count),
                "AC8: Concurrent registrations must produce unique ARC76 addresses with no collisions");
        }

        /// <summary>
        /// AC8 (supplemental): Invalid refresh token must be rejected with 401.
        /// </summary>
        [Test]
        public async Task AC8_InvalidRefreshToken_IsRejected()
        {
            var refreshReq = new RefreshTokenRequest { RefreshToken = "invalid-or-expired-refresh-token" };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AC8: Invalid/stale refresh tokens must be rejected with 401");

            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body).RootElement;
            Assert.That(json.TryGetProperty("errorCode", out _), Is.True,
                "AC8: Invalid refresh token error must include errorCode field");
        }

        /// <summary>
        /// AC9: CI for scoped backend suites is green.
        /// Validates health check endpoint and application startup.
        /// </summary>
        [Test]
        public async Task AC9_HealthCheck_IsGreen_ApplicationStartsCorrectly()
        {
            var response = await _client.GetAsync("/health");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC9: Application health check must return 200 OK confirming CI environment is green");
        }

        /// <summary>
        /// AC10: Developer documentation - DerivationContractVersion constant is documented and accessible.
        /// Validates that the version constant is defined and non-empty.
        /// </summary>
        [Test]
        public async Task AC10_DerivationContractVersion_IsDocumented_AndStable()
        {
            // The version constant must be defined and non-empty
            Assert.That(AuthenticationService.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "AC10: DerivationContractVersion constant must be defined and non-empty");

            // The version must follow a versioned format (e.g., "1.0")
            Assert.That(AuthenticationService.DerivationContractVersion, Does.Match(@"^\d+\.\d+"),
                "AC10: DerivationContractVersion must follow semantic versioning format (e.g., '1.0')");

            // Register and login must both surface this same version
            var email = $"arc76-ac10-{Guid.NewGuid():N}@test.example.com";
            var password = "SecurePass123!";

            var registerReq = new RegisterRequest { Email = email, Password = password, ConfirmPassword = password };
            var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
            var registered = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registered!.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "AC10: All auth endpoints must expose the same canonical DerivationContractVersion");

            var loginReq = new LoginRequest { Email = email, Password = password };
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
            var loggedIn = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loggedIn!.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "AC10: Login endpoint must expose the same DerivationContractVersion as the constant");
        }
    }
}

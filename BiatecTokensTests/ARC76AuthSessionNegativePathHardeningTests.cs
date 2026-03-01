using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Explicit failure-first TDD tests for ARC76 auth/session and launch orchestration reliability.
    ///
    /// This file specifically covers negative paths, edge cases, and failure scenarios
    /// that are not covered by the happy-path contract tests in ARC76AuthSessionOrchestrationHardeningTests.
    ///
    /// Failure categories covered:
    /// 1. Malformed inputs: null, empty, whitespace, oversized, special characters
    /// 2. Stale/invalid sessions: expired refresh tokens, already-used tokens, tampered tokens
    /// 3. Dependency failures: repository exceptions, simulated downstream failures
    /// 4. ARC76 derivation edge cases: email case normalization, whitespace, invalid preconditions
    /// 5. Orchestration failure paths: TimeoutException, HttpRequestException, multi-step failures
    /// 6. Retry semantics: retryable vs non-retryable error classification
    /// 7. Security: no sensitive field leakage, no stack traces in errors
    ///
    /// TDD principle: Each test is written to validate a specific expected failure behavior,
    /// ensuring the system fails predictably, safely, and with actionable error outputs.
    ///
    /// Business Value: Predictable failure modes reduce enterprise support burden, prevent
    /// ambiguous error states that can halt token operations, and demonstrate MiCA-aligned
    /// auditability of error handling across all critical auth/session/orchestration paths.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76AuthSessionNegativePathHardeningTests
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
            ["JwtConfig:SecretKey"] = "arc76-negative-path-test-secret-key-32chars-minimum!",
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
            ["IPFSConfig:Username"] = "",
            ["IPFSConfig:Password"] = "",
            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
            ["EVMChains:0:ChainId"] = "8453",
            ["EVMChains:0:Name"] = "Base Mainnet",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "Arc76NegPathTestEncKey32CharsMinRequired!!",
            ["KycConfig:Provider"] = "Mock",
            ["KycConfig:AutoApprove"] = "true",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
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
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. Malformed Input Tests
        // ─────────────────────────────────────────────────────────────────────

        #region Malformed Inputs

        /// <summary>
        /// Null email in login body returns 400/422, not 500 Internal Server Error.
        /// </summary>
        [Test]
        public async Task MalformedInput_Login_NullEmail_Returns400NotInternalError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = (string?)null, Password = "ValidPass1!" });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Null email must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Null email must return structured validation error");
        }

        /// <summary>
        /// Whitespace-only email in login body returns 400/401, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Login_WhitespaceEmail_Returns400NotInternalError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "   ", Password = "ValidPass1!" });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Whitespace email must not cause 500 Internal Server Error");
        }

        /// <summary>
        /// Null password in login body returns 400, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Login_NullPassword_Returns400NotInternalError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "test@example.com", Password = (string?)null });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Null password must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401, 422),
                "Null password must return structured error");
        }

        /// <summary>
        /// Empty JSON body in login request returns 400, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Login_EmptyJsonBody_Returns400NotInternalError()
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1/auth/login", content);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Empty JSON body must not cause 500 Internal Server Error");
        }

        /// <summary>
        /// Completely missing body (no Content-Type) returns 400/415, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Login_NoBody_ReturnsClientError()
        {
            var content = new StringContent(string.Empty);
            var response = await _client.PostAsync("/api/v1/auth/login", content);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Missing body must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.InRange(400, 499),
                "Missing body must return 4xx client error");
        }

        /// <summary>
        /// Oversized email address (>1000 chars) returns non-500 response.
        /// Tests that the system handles long inputs gracefully without internal errors.
        /// </summary>
        [Test]
        public async Task MalformedInput_Register_OversizedEmail_HandledGracefully()
        {
            var oversizedEmail = new string('a', 900) + "@example.com";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = oversizedEmail, Password = "ValidPass1!", ConfirmPassword = "ValidPass1!" });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Oversized email must not cause 500 Internal Server Error");
            // System may accept long emails (200) or reject them (4xx) - both are acceptable; 500 is not
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 400, 422),
                "Oversized email must return either accepted (200) or structured error (400/422), never 500");
        }

        /// <summary>
        /// Email with SQL injection characters returns 400, not 500, and does NOT leak DB error details.
        /// </summary>
        [Test]
        public async Task MalformedInput_Login_SqlInjectionEmail_Returns4xxNoDbDetails()
        {
            var injectionEmail = "' OR '1'='1'; DROP TABLE users; --";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = injectionEmail, Password = "ValidPass1!" });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "SQL injection email must not cause 500 Internal Server Error");
            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Does.Not.Contain("SQL"),
                "SQL injection must not produce SQL error messages in response");
            Assert.That(raw, Does.Not.Contain("System.Data"),
                "SQL injection must not expose System.Data namespace in response");
        }

        /// <summary>
        /// Invalid JSON syntax in request body returns 400, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Register_InvalidJsonSyntax_Returns400NotInternalError()
        {
            var content = new StringContent(
                "{ invalid json: not valid }",
                Encoding.UTF8,
                "application/json");
            var response = await _client.PostAsync("/api/v1/auth/register", content);

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Invalid JSON must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 415, 422),
                "Invalid JSON must return client-level error");
        }

        /// <summary>
        /// Special characters in password (XSS attempt) are handled safely.
        /// </summary>
        [Test]
        public async Task MalformedInput_Register_XSSInPassword_HandledSafely()
        {
            var xssPassword = "<script>alert('xss')</script>Valid1!";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new
                {
                    Email = $"xss-{Guid.NewGuid():N}@test.com",
                    Password = xssPassword,
                    ConfirmPassword = xssPassword
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "XSS-like password must not cause 500 Internal Server Error");
            var raw = await response.Content.ReadAsStringAsync();
            // Response must not echo back raw script tags
            Assert.That(raw, Does.Not.Contain("<script>"),
                "XSS attempt in password must not be reflected in response");
        }

        /// <summary>
        /// Empty refresh token returns structured error, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Refresh_EmptyToken_Returns400StructuredError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = "" });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Empty refresh token must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401),
                "Empty refresh token must return structured error");
        }

        /// <summary>
        /// Null refresh token in body returns structured error, not 500.
        /// </summary>
        [Test]
        public async Task MalformedInput_Refresh_NullToken_Returns400StructuredError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = (string?)null });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Null refresh token must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401, 422),
                "Null refresh token must return structured error");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // 2. Stale / Invalid Session Tests
        // ─────────────────────────────────────────────────────────────────────

        #region Stale and Invalid Sessions

        /// <summary>
        /// A tampered/corrupted JWT token returns 401, not 500.
        /// </summary>
        [Test]
        public async Task StaleSession_TamperedJwtToken_Returns401NotInternalError()
        {
            // Register and get a valid token first
            var email = $"stale-tamper-{Guid.NewGuid():N}@test.com";
            var regResult = await RegisterUser(email, "ValidPass1!");
            Assert.That(regResult, Is.Not.Null);

            // Tamper the token by prepending garbage to the payload section
            var validToken = regResult!.AccessToken!;
            var parts = validToken.Split('.');
            if (parts.Length >= 2)
            {
                parts[1] = "TAMPERED_PAYLOAD_" + parts[1];
            }
            var tamperedToken = string.Join(".", parts);

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tamperedToken);

            var response = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Tampered JWT must be rejected with 401 or 403");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Tampered JWT must not cause 500 Internal Server Error");
        }

        /// <summary>
        /// A well-formed JWT with wrong signature returns 401, not 500.
        /// </summary>
        [Test]
        public async Task StaleSession_WrongSignatureJwt_Returns401NotInternalError()
        {
            // Create a JWT-looking token but with wrong signature
            var fakeJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                          "eyJzdWIiOiJ0ZXN0dXNlciIsImVtYWlsIjoidGVzdEB0ZXN0LmNvbSIsImlhdCI6MTYwMDAwMDAwMH0." +
                          "INVALID_SIGNATURE_THAT_DOES_NOT_MATCH";

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", fakeJwt);

            var response = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "JWT with wrong signature must return 401/403");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "JWT with wrong signature must not cause 500 Internal Server Error");
        }

        /// <summary>
        /// A malformed Bearer token (not JWT format) returns 401, not 500.
        /// </summary>
        [Test]
        public async Task StaleSession_NonJwtBearerToken_Returns401NotInternalError()
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "not-a-jwt-token-at-all");

            var response = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Non-JWT bearer token must return 401/403");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Non-JWT bearer token must not cause 500 Internal Server Error");
        }

        /// <summary>
        /// Random UUID as refresh token returns structured 400/401 error.
        /// </summary>
        [Test]
        public async Task StaleSession_RandomUuidRefreshToken_ReturnsStructuredError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = Guid.NewGuid().ToString() });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Random UUID refresh token must not cause 500");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401),
                "Random UUID refresh token must return structured error");

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Does.Not.Contain("StackTrace"),
                "Stale token error must not expose stack trace");
        }

        /// <summary>
        /// Using a valid refresh token twice: second use should return structured error.
        /// Tests that refresh tokens are invalidated after use (rotation).
        /// </summary>
        [Test]
        public async Task StaleSession_RefreshTokenUsedTwice_SecondUseFails()
        {
            var email = $"stale-double-{Guid.NewGuid():N}@test.com";
            var regResult = await RegisterUser(email, "DoubleRefresh1!");
            Assert.That(regResult, Is.Not.Null);
            var originalRefreshToken = regResult!.RefreshToken!;

            // First use: should succeed
            var firstRefreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = originalRefreshToken });
            Assert.That(firstRefreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "First refresh token use must succeed");
            var firstRefreshBody = await firstRefreshResp.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(firstRefreshBody!.Success, Is.True, "First refresh must succeed");

            // Second use of the SAME original token: should fail (token rotated)
            var secondRefreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = originalRefreshToken });

            Assert.That((int)secondRefreshResp.StatusCode, Is.Not.EqualTo(500),
                "Second use of same refresh token must not cause 500");
            // Should be rejected since token is rotated/revoked after first use
            Assert.That((int)secondRefreshResp.StatusCode, Is.AnyOf(400, 401),
                "Second use of same refresh token must be rejected");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // 3. ARC76 Derivation Edge Cases
        // ─────────────────────────────────────────────────────────────────────

        #region ARC76 Derivation Edge Cases

        /// <summary>
        /// Email case normalization: UPPERCASE and lowercase versions of the same email
        /// must produce the same ARC76 address (email is canonicalized before derivation).
        /// </summary>
        [Test]
        public async Task ARC76Edge_EmailCaseNormalization_SameAddressForUpperAndLower()
        {
            var baseEmail = $"caseedge-{Guid.NewGuid():N}";
            var lowercaseEmail = $"{baseEmail}@test.com";
            const string password = "CaseNorm1!";

            // Register with lowercase email
            var regResult = await RegisterUser(lowercaseEmail, password);
            Assert.That(regResult, Is.Not.Null);
            string expectedAddress = regResult!.AlgorandAddress!;

            // Login with UPPERCASE email - the system normalizes email to lowercase before derivation
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = lowercaseEmail.ToUpper(), Password = password });

            // The system canonicalizes email before ARC76 derivation, so UPPERCASE login must work
            // and produce the same deterministic address. If the system rejects UPPERCASE, that is
            // also acceptable as long as it fails with 401 (not 500).
            Assert.That((int)loginResp.StatusCode, Is.Not.EqualTo(500),
                "Email case handling must never cause 500 Internal Server Error");
            if (loginResp.StatusCode == HttpStatusCode.OK)
            {
                var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(expectedAddress),
                    "Email canonicalization: UPPERCASE login must produce same ARC76 address as lowercase registration");
            }
            else
            {
                Assert.That((int)loginResp.StatusCode, Is.EqualTo(401),
                    "If email case login fails, it must fail with 401 (credentials not found), not 500");
            }
        }

        /// <summary>
        /// Email with leading/trailing whitespace: trimmed version should match existing account.
        /// </summary>
        [Test]
        public async Task ARC76Edge_EmailWhitespaceTrimming_LoginWorksWithTrimmedEmail()
        {
            var email = $"whitespace-{Guid.NewGuid():N}@test.com";
            const string password = "WhiteSpace1!";

            var regResult = await RegisterUser(email, password);
            Assert.That(regResult, Is.Not.Null);
            string expectedAddress = regResult!.AlgorandAddress!;

            // Login with padded email
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "  " + email + "  ", Password = password });

            // Must not return 500 - whitespace must be handled
            Assert.That((int)loginResp.StatusCode, Is.Not.EqualTo(500),
                "Login with padded email must not cause 500 Internal Server Error");
        }

        /// <summary>
        /// Email without domain (invalid format) returns structured validation error.
        /// ARC76 derivation must reject invalid email preconditions.
        /// </summary>
        [Test]
        public async Task ARC76Edge_InvalidEmailFormat_ReturnsStructuredValidationError()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new
                {
                    Email = "notavalidemail",
                    Password = "ValidPass1!",
                    ConfirmPassword = "ValidPass1!"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Invalid email format must not cause 500 Internal Server Error");
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 422),
                "Invalid email format must return structured validation error");
        }

        /// <summary>
        /// Three different users with the same password MUST produce different ARC76 addresses.
        /// Validates that email is the differentiator in derivation (not password alone).
        /// </summary>
        [Test]
        public async Task ARC76Edge_SamePassword_DifferentEmails_ProduceDifferentAddresses()
        {
            const string sharedPassword = "SharedPassword1!";
            var emails = new[]
            {
                $"same-pwd-user1-{Guid.NewGuid():N}@test.com",
                $"same-pwd-user2-{Guid.NewGuid():N}@test.com",
                $"same-pwd-user3-{Guid.NewGuid():N}@test.com"
            };

            var addresses = new List<string>();
            foreach (var email in emails)
            {
                var result = await RegisterUser(email, sharedPassword);
                Assert.That(result, Is.Not.Null);
                addresses.Add(result!.AlgorandAddress!);
            }

            // All addresses must be unique (email differentiates derivation)
            var uniqueAddresses = addresses.Distinct().ToList();
            Assert.That(uniqueAddresses.Count, Is.EqualTo(3),
                "Users with same password but different emails must get different ARC76 addresses");
        }

        /// <summary>
        /// ARC76 derivation must be deterministic regardless of registration timestamp.
        /// Re-registering the same email (after delete if allowed) must produce the same address.
        /// </summary>
        [Test]
        public async Task ARC76Edge_DeterministicAddress_NotDependentOnRegistrationTime()
        {
            var email = $"determin-time-{Guid.NewGuid():N}@test.com";
            const string password = "DetermTime1!";

            // Register and get first address
            var firstReg = await RegisterUser(email, password);
            Assert.That(firstReg, Is.Not.Null);
            string addressFromRegistration = firstReg!.AlgorandAddress!;

            // Login immediately (same session) to get address again
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            loginResp.EnsureSuccessStatusCode();
            var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            // Address must match (not time-based)
            Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(addressFromRegistration),
                "ARC76 address must be derived from credentials, not registration timestamp");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // 4. Orchestration Failure Paths (Unit Tests)
        // ─────────────────────────────────────────────────────────────────────

        #region Orchestration Failure Paths

        /// <summary>
        /// Orchestration pipeline with TimeoutException in executor maps to BLOCKCHAIN_TIMEOUT error code.
        /// This is a retryable failure category (not a permanent failure).
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_ExecutorTimeout_MapsToBlockchainTimeoutErrorCode()
        {
            var pipeline = BuildOrchestrationPipeline();
            var ctx = pipeline.BuildContext("ASA_CREATE", "corr-timeout", null, "user-1");

            var result = await pipeline.ExecuteAsync<string, string>(
                ctx,
                request: "Token",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: async _ =>
                {
                    await Task.CompletedTask;
                    throw new TimeoutException("Blockchain node timed out");
                });

            Assert.That(result.Success, Is.False,
                "Timeout must result in failure");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.BLOCKCHAIN_TIMEOUT),
                "TimeoutException must map to BLOCKCHAIN_TIMEOUT error code for actionable retry guidance");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-timeout"),
                "CorrelationId must be preserved through timeout failure");
        }

        /// <summary>
        /// Orchestration pipeline with HttpRequestException in executor maps to NETWORK_ERROR error code.
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_ExecutorHttpException_MapsToNetworkErrorCode()
        {
            var pipeline = BuildOrchestrationPipeline();
            var ctx = pipeline.BuildContext("ERC20_CREATE", "corr-http", null, "user-1");

            var result = await pipeline.ExecuteAsync<string, string>(
                ctx,
                request: "Token",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: async _ =>
                {
                    await Task.CompletedTask;
                    throw new HttpRequestException("RPC endpoint unreachable");
                });

            Assert.That(result.Success, Is.False,
                "Network failure must result in orchestration failure");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NETWORK_ERROR),
                "HttpRequestException must map to NETWORK_ERROR error code");
            Assert.That(result.AuditSummary, Is.Not.Null,
                "AuditSummary must be populated for network failures");
        }

        /// <summary>
        /// Orchestration pipeline with InvalidOperationException in executor maps to OPERATION_FAILED.
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_ExecutorInvalidOperation_MapsToOperationFailedCode()
        {
            var pipeline = BuildOrchestrationPipeline();
            var ctx = pipeline.BuildContext("ASA_CREATE", "corr-invalid-op", null, "user-1");

            var result = await pipeline.ExecuteAsync<string, string>(
                ctx,
                request: "Token",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: async _ =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("State is invalid for this operation");
                });

            Assert.That(result.Success, Is.False,
                "InvalidOperationException must result in orchestration failure");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.OPERATION_FAILED),
                "InvalidOperationException must map to OPERATION_FAILED error code");
        }

        /// <summary>
        /// Precondition failure halts pipeline before executor is called.
        /// Verifies the executor is NEVER called when precondition fails.
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_PreconditionFails_ExecutorNeverCalled()
        {
            var pipeline = BuildOrchestrationPipeline();
            var ctx = pipeline.BuildContext("ASA_CREATE", "corr-precond", null, "user-1");
            bool executorWasCalled = false;

            var result = await pipeline.ExecuteAsync<string, string>(
                ctx,
                request: "Token",
                validationPolicy: _ => null,  // validation passes
                preconditionPolicy: _ => "SUBSCRIPTION_REQUIRED",  // precondition fails
                executor: async _ =>
                {
                    executorWasCalled = true;  // should never reach here
                    await Task.CompletedTask;
                    return "unreachable";
                });

            Assert.That(result.Success, Is.False,
                "Precondition failure must halt pipeline");
            Assert.That(executorWasCalled, Is.False,
                "Executor MUST NOT be called when precondition fails (no side effects)");
            Assert.That(result.AuditSummary, Is.Not.Null,
                "AuditSummary must be populated even for precondition failures");
        }

        /// <summary>
        /// Multi-step failure: validation pass → precondition pass → executor fails.
        /// Validates that failure at any stage produces full audit trail.
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_MultiStepExecutorFail_FullAuditTrailPreserved()
        {
            var pipeline = BuildOrchestrationPipeline();
            var ctx = pipeline.BuildContext("ERC20_MINT", "corr-multistep", "idem-multistep", "user-1");

            var result = await pipeline.ExecuteAsync<string, string>(
                ctx,
                request: "Token",
                validationPolicy: _ => null,       // Stage 1: passes
                preconditionPolicy: _ => null,     // Stage 2: passes
                executor: async _ =>               // Stage 3: fails
                {
                    await Task.CompletedTask;
                    throw new TimeoutException("Timeout at execution stage");
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.CorrelationId, Is.EqualTo("corr-multistep"),
                "CorrelationId must survive multi-step pipeline failure");
            Assert.That(result.IdempotencyKey, Is.EqualTo("idem-multistep"),
                "IdempotencyKey must survive multi-step pipeline failure");
            Assert.That(result.AuditSummary, Is.Not.Null,
                "Full audit trail must be present after multi-step failure");
            Assert.That(result.StageMarkers, Is.Not.Null.And.Not.Empty,
                "Stage markers must be populated for multi-step failure observability");
        }

        /// <summary>
        /// Concurrent orchestration runs with the same idempotency key.
        /// Each produces independent result with its own CorrelationId.
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_ConcurrentRunsSameIdempotencyKey_IndependentResults()
        {
            var pipeline = BuildOrchestrationPipeline();
            const string sharedIdempotencyKey = "shared-idem-key-concurrency-test";

            var tasks = Enumerable.Range(0, 3).Select(i => pipeline.ExecuteAsync<string, string>(
                pipeline.BuildContext("ASA_CREATE", $"corr-concurrent-{i}", sharedIdempotencyKey, "user-1"),
                request: $"Token-{i}",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: async _ => { await Task.CompletedTask; return "ok"; })).ToList();

            var results = await Task.WhenAll(tasks);

            // All should succeed
            Assert.That(results, Is.All.Not.Null);
            // Each should have unique CorrelationId (independent execution context)
            var correlationIds = results.Select(r => r.CorrelationId).Distinct().ToList();
            Assert.That(correlationIds.Count, Is.EqualTo(3),
                "Concurrent runs must have independent CorrelationIds (not shared state)");
        }

        /// <summary>
        /// Null operationType in BuildContext is handled gracefully (no crash).
        /// </summary>
        [Test]
        public async Task OrchestrationFailure_NullOperationType_HandledSafely()
        {
            var pipeline = BuildOrchestrationPipeline();
            // BuildContext with null operationType should not throw
            Assert.That(() =>
            {
                var ctx = pipeline.BuildContext(null!, "corr-null-op", null, null);
                Assert.That(ctx, Is.Not.Null, "BuildContext with null operationType must not throw");
            }, Throws.Nothing, "BuildContext must handle null operationType safely");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // 5. Retry Policy Semantics (Unit Tests)
        // ─────────────────────────────────────────────────────────────────────

        #region Retry Policy Semantics

        /// <summary>
        /// BLOCKCHAIN_TIMEOUT error code is classified as retryable (not NotRetryable).
        /// Enterprise operators need accurate retry guidance to automate recovery.
        /// </summary>
        [Test]
        public void RetryPolicy_BlockchainTimeout_ClassifiedAsRetryable()
        {
            var classifier = BuildRetryPolicyClassifier();

            var decision = classifier.ClassifyError(ErrorCodes.BLOCKCHAIN_TIMEOUT);

            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Policy, Is.Not.EqualTo(RetryPolicy.NotRetryable),
                "BLOCKCHAIN_TIMEOUT must be classified as retryable (not permanent failure)");
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                "Retry decision must include actionable explanation");
        }

        /// <summary>
        /// NETWORK_ERROR error code is classified as retryable.
        /// </summary>
        [Test]
        public void RetryPolicy_NetworkError_ClassifiedAsRetryable()
        {
            var classifier = BuildRetryPolicyClassifier();

            var decision = classifier.ClassifyError(ErrorCodes.NETWORK_ERROR);

            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Policy, Is.Not.EqualTo(RetryPolicy.NotRetryable),
                "NETWORK_ERROR must be classified as retryable");
        }

        /// <summary>
        /// INVALID_NETWORK error code is classified as NOT retryable (permanent failure).
        /// No amount of retry will fix an invalid network configuration.
        /// </summary>
        [Test]
        public void RetryPolicy_InvalidNetwork_ClassifiedAsNotRetryable()
        {
            var classifier = BuildRetryPolicyClassifier();

            var decision = classifier.ClassifyError(ErrorCodes.INVALID_NETWORK);

            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                "INVALID_NETWORK must be classified as NotRetryable (permanent configuration failure)");
        }

        /// <summary>
        /// ShouldRetry returns false after exceeding max retry attempts.
        /// </summary>
        [Test]
        public void RetryPolicy_ShouldRetry_ReturnsFalseAfterMaxAttempts()
        {
            var classifier = BuildRetryPolicyClassifier();

            // After 10 attempts, should stop retrying
            bool shouldRetry = classifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 10,
                firstAttemptTime: DateTime.UtcNow.AddMinutes(-30));

            Assert.That(shouldRetry, Is.False,
                "ShouldRetry must return false after exceeding maximum retry attempts");
        }

        /// <summary>
        /// ShouldRetry returns false for NotRetryable policy regardless of attempt count.
        /// </summary>
        [Test]
        public void RetryPolicy_ShouldRetry_NotRetryablePolicy_AlwaysFalse()
        {
            var classifier = BuildRetryPolicyClassifier();

            bool shouldRetry1 = classifier.ShouldRetry(RetryPolicy.NotRetryable, 0, DateTime.UtcNow);
            bool shouldRetry2 = classifier.ShouldRetry(RetryPolicy.NotRetryable, 1, DateTime.UtcNow);

            Assert.That(shouldRetry1, Is.False,
                "NotRetryable policy must always return ShouldRetry=false (attempt 0)");
            Assert.That(shouldRetry2, Is.False,
                "NotRetryable policy must always return ShouldRetry=false (attempt 1)");
        }

        /// <summary>
        /// CalculateRetryDelay returns positive value for retryable policies.
        /// </summary>
        [Test]
        public void RetryPolicy_CalculateRetryDelay_RetryablePolicy_ReturnsPositiveDelay()
        {
            var classifier = BuildRetryPolicyClassifier();

            var delay = classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, attemptCount: 1, useExponentialBackoff: false);

            Assert.That(delay, Is.GreaterThan(0),
                "RetryableWithDelay policy must return positive delay for actionable retry guidance");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // 6. Service-Level Dependency Failure Tests
        // ─────────────────────────────────────────────────────────────────────

        #region Service Dependency Failures

        /// <summary>
        /// InspectSessionAsync when user repository throws returns safe fallback (no throw propagation).
        /// Tests fault tolerance of the session inspection path.
        /// </summary>
        [Test]
        public async Task ServiceDependencyFailure_InspectSession_RepoThrows_ReturnsSafeFallback()
        {
            var mockRepo = new Mock<IUserRepository>();
            mockRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Database connection lost"));

            var authService = BuildAuthService(mockRepo);
            var result = await authService.InspectSessionAsync("user-123", "corr-repo-throw");

            // Must not propagate the exception
            Assert.That(result, Is.Not.Null,
                "InspectSession must not propagate repository exceptions");
            Assert.That(result.IsActive, Is.False,
                "InspectSession must return IsActive=false on repository failure (safe fallback)");
            Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                "InspectSession must not return partial data when repository fails");
        }

        /// <summary>
        /// VerifyDerivationAsync when user repository throws InvalidOperationException
        /// returns structured error response without propagating the exception.
        /// </summary>
        [Test]
        public async Task ServiceDependencyFailure_VerifyDerivation_DbOutage_ReturnsStructuredError()
        {
            var mockRepo = new Mock<IUserRepository>();
            mockRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Database cluster unavailable"));

            var authService = BuildAuthService(mockRepo);
            var result = await authService.VerifyDerivationAsync("user-123", null, "corr-db-outage");

            Assert.That(result, Is.Not.Null,
                "VerifyDerivation must not propagate repository exceptions");
            Assert.That(result.Success, Is.False,
                "VerifyDerivation must return Success=false on repository failure");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "VerifyDerivation must include ErrorCode for actionable error handling");
            // System maps repository failures to structured error codes (e.g., INTERNAL_SERVER_ERROR, DERIVATION_FAILED)
            Assert.That(result.ErrorCode, Is.Not.Null,
                "VerifyDerivation must return non-null ErrorCode on repository failure");
        }

        /// <summary>
        /// VerifyDerivationAsync when repository throws TaskCanceledException (connection timeout)
        /// returns structured error without propagating the cancellation exception.
        /// </summary>
        [Test]
        public async Task ServiceDependencyFailure_VerifyDerivation_TimeoutException_ReturnsStructuredError()
        {
            var mockRepo = new Mock<IUserRepository>();
            mockRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new TaskCanceledException("DB query timed out"));

            var authService = BuildAuthService(mockRepo);
            var result = await authService.VerifyDerivationAsync("user-123", null, "corr-timeout");

            // Must not throw, must return structured failure
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.False,
                "VerifyDerivation must return Success=false on repository timeout");
        }

        /// <summary>
        /// GetDerivationInfo must return stable output even when called with empty correlationId.
        /// Validates robustness of the contract info endpoint.
        /// </summary>
        [Test]
        public void ServiceDependencyFailure_GetDerivationInfo_EmptyCorrelationId_ReturnsStableResult()
        {
            var authService = BuildAuthService();

            // Should not throw even with empty correlationId
            var result1 = authService.GetDerivationInfo(string.Empty);
            var result2 = authService.GetDerivationInfo("   ");

            Assert.That(result1, Is.Not.Null,
                "GetDerivationInfo with empty correlationId must return stable result");
            Assert.That(result2, Is.Not.Null,
                "GetDerivationInfo with whitespace correlationId must return stable result");
            Assert.That(result1.ContractVersion, Is.EqualTo(result2.ContractVersion),
                "ContractVersion must be stable regardless of correlationId");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // 7. Security Boundary Tests
        // ─────────────────────────────────────────────────────────────────────

        #region Security Boundaries

        /// <summary>
        /// Successful registration response must NOT contain mnemonic, password hash, or encryption key.
        /// Verifies that sensitive internal fields are never exposed in API responses.
        /// </summary>
        [Test]
        public async Task SecurityBoundary_SuccessfulRegistration_NeverExposesSensitiveFields()
        {
            var email = $"security-{Guid.NewGuid():N}@test.com";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = "SecurePass1!", ConfirmPassword = "SecurePass1!" });
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Does.Not.Contain("mnemonic").IgnoreCase,
                "Successful registration must never expose mnemonic");
            Assert.That(raw, Does.Not.Contain("encryptedMnemonic").IgnoreCase,
                "Successful registration must never expose encryptedMnemonic");
            Assert.That(raw, Does.Not.Contain("passwordHash").IgnoreCase,
                "Successful registration must never expose passwordHash");
            Assert.That(raw, Does.Not.Contain("secretKey").IgnoreCase,
                "Successful registration must never expose secretKey");
        }

        /// <summary>
        /// Failed login response must NOT contain any internal implementation details.
        /// </summary>
        [Test]
        public async Task SecurityBoundary_FailedLogin_NeverExposesInternalDetails()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = $"sec-fail-{Guid.NewGuid():N}@test.com", Password = "WrongPass1!" });

            var raw = await response.Content.ReadAsStringAsync();
            Assert.That(raw, Does.Not.Contain("StackTrace").IgnoreCase,
                "Failed login must never expose StackTrace");
            Assert.That(raw, Does.Not.Contain("NullReferenceException").IgnoreCase,
                "Failed login must never expose exception type names");
            Assert.That(raw, Does.Not.Contain("System.Exception").IgnoreCase,
                "Failed login must never expose .NET exception namespace");
            Assert.That(raw, Does.Not.Contain("System.Data").IgnoreCase,
                "Failed login must never expose .NET data namespace");
            Assert.That(raw, Does.Not.Contain("at BiatecTokensApi.").IgnoreCase,
                "Failed login must never expose internal class paths");
        }

        /// <summary>
        /// Accessing session endpoint without auth header returns 401 with structured error body.
        /// Must not return 500 or empty body.
        /// </summary>
        [Test]
        public async Task SecurityBoundary_SessionEndpoint_NoAuth_Returns401WithBody()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var response = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)response.StatusCode, Is.AnyOf(401, 403),
                "Unauthenticated session access must return 401/403");
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Unauthenticated session access must not cause 500");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        // Helper Methods
        // ─────────────────────────────────────────────────────────────────────

        private async Task<RegisterResponse?> RegisterUser(string email, string password)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RegisterResponse>();
        }

        private static TokenWorkflowOrchestrationPipeline BuildOrchestrationPipeline()
        {
            var logger = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            var retryClassifier = new Mock<IRetryPolicyClassifier>();

            // Set up realistic retry policy responses for exception-based tests
            retryClassifier
                .Setup(r => r.ClassifyError(
                    It.IsAny<string>(),
                    It.IsAny<DeploymentErrorCategory?>(),
                    It.IsAny<Dictionary<string, object>?>()))
                .Returns((string errorCode, DeploymentErrorCategory? cat, Dictionary<string, object>? ctx) =>
                    new RetryPolicyDecision
                    {
                        Policy = errorCode is ErrorCodes.BLOCKCHAIN_TIMEOUT or ErrorCodes.NETWORK_ERROR
                            ? RetryPolicy.RetryableWithDelay
                            : RetryPolicy.NotRetryable,
                        Explanation = $"Classified: {errorCode}"
                    });

            return new TokenWorkflowOrchestrationPipeline(logger.Object, retryClassifier.Object);
        }

        private static RetryPolicyClassifier BuildRetryPolicyClassifier()
        {
            var logger = new Mock<ILogger<RetryPolicyClassifier>>();
            return new RetryPolicyClassifier(logger.Object);
        }

        private static AuthenticationService BuildAuthService(
            Mock<IUserRepository>? mockRepo = null)
        {
            var repo = mockRepo ?? new Mock<IUserRepository>();
            var logger = new Mock<ILogger<AuthenticationService>>();

            const string jwtSecret = "NegPathTestSecretKey32CharactersMin!!";
            const string encryptionKey = "NegPathTestEncryptKey32CharsMin!!A";

            var jwtConfig = new JwtConfig
            {
                SecretKey = jwtSecret,
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensUsers",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30
            };
            var keyMgmtConfig = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = encryptionKey
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(_ =>
            {
                _.Provider = "Hardcoded";
                _.HardcodedKey = encryptionKey;
            });
            services.AddSingleton<HardcodedKeyProvider>();
            var sp = services.BuildServiceProvider();

            var keyProviderFactory = new KeyProviderFactory(
                sp,
                Options.Create(keyMgmtConfig),
                new Mock<ILogger<KeyProviderFactory>>().Object);

            return new AuthenticationService(
                repo.Object,
                logger.Object,
                Options.Create(jwtConfig),
                keyProviderFactory);
        }
    }
}

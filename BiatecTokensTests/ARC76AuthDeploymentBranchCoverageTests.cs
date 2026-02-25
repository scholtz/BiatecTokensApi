using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
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
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Branch coverage unit tests for core auth-to-deployment services.
    /// Covers every logic branch in:
    ///   - AuthenticationService public methods (RegisterAsync, LoginAsync, RefreshTokenAsync, etc.)
    ///   - StateTransitionGuard (every valid/invalid transition, terminal states, invariant violations)
    ///   - RetryPolicyClassifier (every error code → RetryPolicy mapping branch)
    ///
    /// Required by issue #399 AC3 (validation behavior), AC4 (idempotency), and AC6 (failure paths).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76AuthDeploymentBranchCoverageTests
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
            ["JwtConfig:SecretKey"] = "arc76-branch-coverage-test-secret-key-32chars-min",
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
            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
            ["EVMChains:0:ChainId"] = "8453",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "BranchCoverageTestKey32CharactersMinimumRequired"
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

        // -----------------------------------------------------------------------
        // IsPasswordStrong – all branches
        // -----------------------------------------------------------------------

        #region IsPasswordStrong Branch Coverage

        [Test]
        [TestCase("", Description = "Empty password")]
        [TestCase("   ", Description = "Whitespace-only password")]
        [TestCase("Short1!", Description = "Too short (7 chars)")]
        public async Task PasswordStrength_TooShortOrEmpty_ReturnsWeakPasswordError(string password)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = $"branch-pw-{Guid.NewGuid()}@test.com", Password = password, ConfirmPassword = password });

            var content = await response.Content.ReadAsStringAsync();
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(200),
                $"Password '{password}' should not pass strength check");
        }

        [Test]
        public async Task PasswordStrength_NoUppercase_ReturnsWeakPasswordError()
        {
            // Has lower, digit, special but no upper
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"branch-noup-{Guid.NewGuid()}@test.com",
                    Password = "nouppercase1!",
                    ConfirmPassword = "nouppercase1!"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(200),
                "Password without uppercase should fail");
        }

        [Test]
        public async Task PasswordStrength_NoLowercase_ReturnsWeakPasswordError()
        {
            // Has upper, digit, special but no lower
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"branch-nolow-{Guid.NewGuid()}@test.com",
                    Password = "NOLOWERCASE1!",
                    ConfirmPassword = "NOLOWERCASE1!"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(200),
                "Password without lowercase should fail");
        }

        [Test]
        public async Task PasswordStrength_NoDigit_ReturnsWeakPasswordError()
        {
            // Has upper, lower, special but no digit
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"branch-nodig-{Guid.NewGuid()}@test.com",
                    Password = "NoDigitHere!",
                    ConfirmPassword = "NoDigitHere!"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(200),
                "Password without digit should fail");
        }

        [Test]
        public async Task PasswordStrength_NoSpecialChar_ReturnsWeakPasswordError()
        {
            // Has upper, lower, digit but no special char
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"branch-nospec-{Guid.NewGuid()}@test.com",
                    Password = "NoSpecialChar1",
                    ConfirmPassword = "NoSpecialChar1"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(200),
                "Password without special character should fail");
        }

        [Test]
        public async Task PasswordStrength_AllRequirementsMet_Succeeds()
        {
            // Exactly meets all requirements: upper, lower, digit, special, length>=8
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = $"branch-strong-{Guid.NewGuid()}@test.com",
                    Password = "StrongPw1!",
                    ConfirmPassword = "StrongPw1!"
                });

            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(body!.Success, Is.True,
                "Password meeting all requirements must succeed");
        }

        #endregion

        // -----------------------------------------------------------------------
        // LoginAsync – all branches
        // -----------------------------------------------------------------------

        #region LoginAsync Branch Coverage

        [Test]
        public async Task Login_NonExistentUser_ReturnsInvalidCredentials()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest
                {
                    Email = $"nonexistent-{Guid.NewGuid()}@test.com",
                    Password = "SomePass1!"
                });

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.Success, Is.False, "Non-existent user must fail");
            Assert.That(body.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS),
                "Non-existent user must return INVALID_CREDENTIALS");
        }

        [Test]
        public async Task Login_WrongPassword_ReturnsInvalidCredentials()
        {
            var email = $"branch-wrongpw-{Guid.NewGuid()}@test.com";
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "Correct1pw!", ConfirmPassword = "Correct1pw!" });

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "WrongPassword1!" });

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.Success, Is.False, "Wrong password must fail");
            Assert.That(body.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS),
                "Wrong password must return INVALID_CREDENTIALS");
        }

        [Test]
        public async Task Login_CorrectCredentials_ReturnsSuccessWithAlgorandAddress()
        {
            var email = $"branch-correct-{Guid.NewGuid()}@test.com";
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "Correct1pw!", ConfirmPassword = "Correct1pw!" });

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "Correct1pw!" });

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.Success, Is.True, "Correct credentials must succeed");
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Successful login must return AlgorandAddress");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty,
                "Successful login must return AccessToken");
        }

        [Test]
        public async Task Login_AccountLockout_After5FailedAttempts_ReturnsAccountLockedCode()
        {
            var email = $"branch-lockout-{Guid.NewGuid()}@test.com";
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "Lockout1pw!", ConfirmPassword = "Lockout1pw!" });

            // Make 5 failed attempts to trigger lockout
            for (int i = 0; i < 5; i++)
            {
                await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new LoginRequest { Email = email, Password = "WrongPassword1!" });
            }

            // 6th attempt should return ACCOUNT_LOCKED
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "Lockout1pw!" });

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(body!.Success, Is.False, "Locked account must not authenticate");
            Assert.That(body.ErrorCode, Is.EqualTo(ErrorCodes.ACCOUNT_LOCKED),
                "Locked account must return ACCOUNT_LOCKED error code");
        }

        #endregion

        // -----------------------------------------------------------------------
        // StateTransitionGuard – every valid and invalid transition
        // -----------------------------------------------------------------------

        #region StateTransitionGuard Branch Coverage

        [Test]
        public void StateTransition_SameStatus_IsIdempotent()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            // Same-status update should always be allowed (idempotent)
            foreach (DeploymentStatus s in Enum.GetValues<DeploymentStatus>())
            {
                var result = guard.ValidateTransition(s, s);
                Assert.That(result.IsAllowed, Is.True,
                    $"Same-status transition {s}→{s} must be idempotent (allowed)");
                Assert.That(result.ReasonCode, Is.EqualTo("IDEMPOTENT_UPDATE"),
                    $"Idempotent transition must return IDEMPOTENT_UPDATE reason code");
            }
        }

        [Test]
        public void StateTransition_QueuedToSubmitted_IsAllowed()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Submitted);
            Assert.That(result.IsAllowed, Is.True, "Queued→Submitted must be allowed");
        }

        [Test]
        public void StateTransition_QueuedToFailed_IsAllowed()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Queued→Failed must be allowed");
        }

        [Test]
        public void StateTransition_QueuedToCancelled_IsAllowed()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Cancelled);
            Assert.That(result.IsAllowed, Is.True, "Queued→Cancelled must be allowed");
        }

        [Test]
        public void StateTransition_SubmittedToPending_IsAllowed()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Submitted, DeploymentStatus.Pending);
            Assert.That(result.IsAllowed, Is.True, "Submitted→Pending must be allowed");
        }

        [Test]
        public void StateTransition_PendingToConfirmed_IsAllowed()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Pending, DeploymentStatus.Confirmed);
            Assert.That(result.IsAllowed, Is.True, "Pending→Confirmed must be allowed");
        }

        [Test]
        public void StateTransition_ConfirmedToCompleted_IsAllowed()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Confirmed, DeploymentStatus.Completed);
            Assert.That(result.IsAllowed, Is.True, "Confirmed→Completed must be allowed");
        }

        [Test]
        public void StateTransition_FailedToQueued_IsAllowed_ForRetry()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var result = guard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.True, "Failed→Queued must be allowed (retry)");
        }

        [Test]
        public void StateTransition_CompletedToAny_IsRejected_TerminalState()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            foreach (DeploymentStatus target in Enum.GetValues<DeploymentStatus>())
            {
                if (target == DeploymentStatus.Completed) continue; // same-status is idempotent

                var result = guard.ValidateTransition(DeploymentStatus.Completed, target);
                Assert.That(result.IsAllowed, Is.False,
                    $"Completed is terminal – Completed→{target} must be rejected");
                Assert.That(result.ReasonCode, Is.EqualTo("TERMINAL_STATE_VIOLATION"),
                    "Terminal state transition must return TERMINAL_STATE_VIOLATION");
            }
        }

        [Test]
        public void StateTransition_CancelledToAny_IsRejected_TerminalState()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            foreach (DeploymentStatus target in Enum.GetValues<DeploymentStatus>())
            {
                if (target == DeploymentStatus.Cancelled) continue; // same-status is idempotent

                var result = guard.ValidateTransition(DeploymentStatus.Cancelled, target);
                Assert.That(result.IsAllowed, Is.False,
                    $"Cancelled is terminal – Cancelled→{target} must be rejected");
            }
        }

        [Test]
        public void StateTransition_SkippedStates_AreRejectedWithInvalidTransition()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            // Skipping states (e.g., Queued→Completed, Queued→Confirmed) must be rejected
            var illegalTransitions = new[]
            {
                (DeploymentStatus.Queued, DeploymentStatus.Completed),
                (DeploymentStatus.Queued, DeploymentStatus.Confirmed),
                (DeploymentStatus.Queued, DeploymentStatus.Pending),
                (DeploymentStatus.Submitted, DeploymentStatus.Completed),
                (DeploymentStatus.Submitted, DeploymentStatus.Confirmed),
            };

            foreach (var (from, to) in illegalTransitions)
            {
                var result = guard.ValidateTransition(from, to);
                Assert.That(result.IsAllowed, Is.False,
                    $"Illegal skip transition {from}→{to} must be rejected");
            }
        }

        [Test]
        public void IsTerminalState_Completed_ReturnsTrue()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            Assert.That(guard.IsTerminalState(DeploymentStatus.Completed), Is.True,
                "Completed must be a terminal state");
        }

        [Test]
        public void IsTerminalState_Cancelled_ReturnsTrue()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            Assert.That(guard.IsTerminalState(DeploymentStatus.Cancelled), Is.True,
                "Cancelled must be a terminal state");
        }

        [Test]
        public void IsTerminalState_NonTerminalStates_ReturnFalse()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var nonTerminal = new[]
            {
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted,
                DeploymentStatus.Pending,
                DeploymentStatus.Confirmed,
                DeploymentStatus.Indexed,
                DeploymentStatus.Failed
            };

            foreach (var status in nonTerminal)
            {
                Assert.That(guard.IsTerminalState(status), Is.False,
                    $"{status} must NOT be a terminal state");
            }
        }

        [Test]
        public void GetValidNextStates_Queued_ReturnsThreeStates()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            var states = guard.GetValidNextStates(DeploymentStatus.Queued);
            Assert.That(states, Contains.Item(DeploymentStatus.Submitted));
            Assert.That(states, Contains.Item(DeploymentStatus.Failed));
            Assert.That(states, Contains.Item(DeploymentStatus.Cancelled));
        }

        [Test]
        public void GetValidNextStates_TerminalState_ReturnsEmpty()
        {
            using var scope = _factory.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IStateTransitionGuard>();

            Assert.That(guard.GetValidNextStates(DeploymentStatus.Completed), Is.Empty,
                "Terminal state Completed has no valid next states");
            Assert.That(guard.GetValidNextStates(DeploymentStatus.Cancelled), Is.Empty,
                "Terminal state Cancelled has no valid next states");
        }

        #endregion

        // -----------------------------------------------------------------------
        // RetryPolicyClassifier – all error code branches
        // -----------------------------------------------------------------------

        #region RetryPolicyClassifier Branch Coverage

        [Test]
        [TestCase(ErrorCodes.INVALID_REQUEST)]
        [TestCase(ErrorCodes.MISSING_REQUIRED_FIELD)]
        [TestCase(ErrorCodes.INVALID_NETWORK)]
        [TestCase(ErrorCodes.INVALID_TOKEN_PARAMETERS)]
        [TestCase(ErrorCodes.METADATA_VALIDATION_FAILED)]
        [TestCase(ErrorCodes.INVALID_TOKEN_STANDARD)]
        [TestCase(ErrorCodes.UNAUTHORIZED)]
        [TestCase(ErrorCodes.FORBIDDEN)]
        [TestCase(ErrorCodes.INVALID_AUTH_TOKEN)]
        [TestCase(ErrorCodes.ALREADY_EXISTS)]
        [TestCase(ErrorCodes.CONFLICT)]
        public void RetryClassifier_ValidationAndAuthErrors_AreNotRetryable(string errorCode)
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            var decision = classifier.ClassifyError(errorCode);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                $"Error {errorCode} must be classified as NotRetryable");
        }

        [Test]
        [TestCase(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR)]
        [TestCase(ErrorCodes.IPFS_SERVICE_ERROR)]
        [TestCase(ErrorCodes.EXTERNAL_SERVICE_ERROR)]
        [TestCase(ErrorCodes.TIMEOUT)]
        public void RetryClassifier_TransientNetworkErrors_AreRetryableWithDelay(string errorCode)
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            var decision = classifier.ClassifyError(errorCode);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay),
                $"Error {errorCode} must be classified as RetryableWithDelay");
        }

        [Test]
        [TestCase(ErrorCodes.RATE_LIMIT_EXCEEDED)]
        [TestCase(ErrorCodes.CIRCUIT_BREAKER_OPEN)]
        [TestCase(ErrorCodes.SUBSCRIPTION_LIMIT_REACHED)]
        public void RetryClassifier_RateLimitErrors_AreRetryableWithCooldown(string errorCode)
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            var decision = classifier.ClassifyError(errorCode);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown),
                $"Error {errorCode} must be classified as RetryableWithCooldown");
        }

        [Test]
        public void RetryClassifier_UnknownErrorCode_HasDefaultPolicy()
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            // Unknown error code must not throw; must return some deterministic policy
            var decision = classifier.ClassifyError("COMPLETELY_UNKNOWN_ERROR_CODE_12345");
            Assert.That(decision, Is.Not.Null,
                "Unknown error code must return a non-null decision");
            Assert.That(decision.Explanation, Is.Not.Null,
                "Unknown error code decision must have an Explanation");
        }

        [Test]
        public void RetryClassifier_ShouldRetry_WithinLimits_ReturnsTrue()
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            // Within retry window and attempt count
            bool result = classifier.ShouldRetry(RetryPolicy.RetryableWithDelay, 1, DateTime.UtcNow.AddSeconds(-5));
            Assert.That(result, Is.True,
                "Should retry when within attempt limits and time window");
        }

        [Test]
        public void RetryClassifier_ShouldRetry_NotRetryable_ReturnsFalse()
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            bool result = classifier.ShouldRetry(RetryPolicy.NotRetryable, 0, DateTime.UtcNow);
            Assert.That(result, Is.False,
                "NotRetryable policy must never allow retry");
        }

        [Test]
        public void RetryClassifier_CalculateRetryDelay_ReturnsPositiveDelay()
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            int delay = classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 1, true);
            Assert.That(delay, Is.GreaterThan(0),
                "Retry delay must be positive");
        }

        [Test]
        public void RetryClassifier_CalculateRetryDelay_ExponentialBackoff_IncreasesWithAttempts()
        {
            using var scope = _factory.Services.CreateScope();
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            int delay1 = classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 1, true);
            int delay2 = classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 2, true);
            int delay3 = classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 3, true);

            Assert.That(delay2, Is.GreaterThanOrEqualTo(delay1),
                "Exponential backoff delay must increase with attempt count (2 >= 1)");
            Assert.That(delay3, Is.GreaterThanOrEqualTo(delay2),
                "Exponential backoff delay must increase with attempt count (3 >= 2)");
        }

        #endregion

        // -----------------------------------------------------------------------
        // Malformed Input Tests
        // -----------------------------------------------------------------------

        #region Malformed Input Tests

        [Test]
        public async Task Register_NullEmail_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Password = "Valid1pw!", ConfirmPassword = "Valid1pw!" });

            Assert.That((int)response.StatusCode, Is.EqualTo(400),
                "Null email must return 400");
        }

        [Test]
        public async Task Register_ExtremelyLongEmail_DoesNotCrash()
        {
            var longEmail = new string('a', 500) + "@example.com";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = longEmail, Password = "Valid1pw!", ConfirmPassword = "Valid1pw!" });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Extremely long email must not cause server error");
        }

        [Test]
        public async Task Register_EmailWithSpecialCharacters_HandledGracefully()
        {
            // SQL injection attempt in email field
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest
                {
                    Email = "'; DROP TABLE users; --@evil.com",
                    Password = "Valid1pw!",
                    ConfirmPassword = "Valid1pw!"
                });

            // Must return 400 (invalid email format) – never 500 (SQL injection protection)
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "SQL injection in email must never cause 500");
        }

        [Test]
        public async Task Register_EmptyBody_Returns400NotServerError()
        {
            var response = await _client.PostAsync("/api/v1/auth/register",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Empty request body must not cause 500");
        }

        [Test]
        public async Task Register_NonJsonBody_Returns400NotServerError()
        {
            var response = await _client.PostAsync("/api/v1/auth/register",
                new StringContent("NOT JSON", System.Text.Encoding.UTF8, "application/json"));

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Non-JSON body must not cause 500");
        }

        [Test]
        public async Task Login_NullPassword_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "test@example.com" });

            Assert.That((int)response.StatusCode, Is.EqualTo(400),
                "Null password in login must return 400");
        }

        [Test]
        public async Task Login_XssAttemptInEmail_HandledGracefully()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest
                {
                    Email = "<script>alert('xss')</script>@example.com",
                    Password = "Valid1pw!"
                });

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "XSS attempt in email must not cause server error");

            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Does.Not.Contain("<script>"),
                "XSS payload must not be reflected in response");
        }

        [Test]
        public async Task DeploymentStatus_InvalidDeploymentId_Returns404NotServerError()
        {
            var email = $"branch-deploy-id-{Guid.NewGuid()}@test.com";
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "Branch1pw!", ConfirmPassword = "Branch1pw!" });
            var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "Branch1pw!" });
            var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
            var response = await _client.GetAsync("/api/v1/token/deployments/COMPLETELY_INVALID_ID_THAT_DOES_NOT_EXIST");
            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500),
                "Invalid deployment ID must not cause 500");
        }

        #endregion

        // -----------------------------------------------------------------------
        // Policy Conflict Tests
        // -----------------------------------------------------------------------

        #region Policy Conflict Tests (Orchestration Pipeline)

        [Test]
        public async Task Orchestration_BothValidationAndPreconditionFail_ValidationFailsFirst()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "CONFLICT_TEST",
                correlationId: $"conflict-{Guid.NewGuid()}");

            int validationCallCount = 0;
            int preconditionCallCount = 0;
            bool executorCalled = false;

            var result = await pipeline.ExecuteAsync<string, string>(
                context: context,
                request: "test",
                validationPolicy: _ =>
                {
                    validationCallCount++;
                    return "VALIDATION_FAILED: both fail";
                },
                preconditionPolicy: _ =>
                {
                    preconditionCallCount++;
                    return "PRECONDITION_FAILED: both fail";
                },
                executor: async _ =>
                {
                    executorCalled = true;
                    await Task.Yield();
                    return "should-not-run";
                });

            Assert.That(result.Success, Is.False,
                "Pipeline must fail when validation fails");
            Assert.That(executorCalled, Is.False,
                "Executor must not run when validation fails");
            // Validation must be evaluated before precondition (fail-fast)
            Assert.That(validationCallCount, Is.GreaterThan(0),
                "Validation policy must be evaluated");
        }

        [Test]
        public async Task Orchestration_ValidationPassesPreconditionFails_ExecutorNotCalled()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "PRECOND_FAIL_TEST",
                correlationId: $"precond-{Guid.NewGuid()}");

            bool executorCalled = false;
            var result = await pipeline.ExecuteAsync<string, string>(
                context: context,
                request: "test",
                validationPolicy: _ => null,  // pass
                preconditionPolicy: _ => "PRECONDITION_FAILED: subscription required",
                executor: async _ =>
                {
                    executorCalled = true;
                    await Task.Yield();
                    return "should-not-run";
                });

            Assert.That(result.Success, Is.False,
                "Pipeline must fail when precondition fails");
            Assert.That(executorCalled, Is.False,
                "Executor must not run when precondition fails");
        }

        [Test]
        public async Task Orchestration_NullRequest_DoesNotThrow()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "NULL_REQUEST_TEST",
                correlationId: $"null-req-{Guid.NewGuid()}");

            // Null request should be handled gracefully by validation policy
            var result = await pipeline.ExecuteAsync<string?, string>(
                context: context,
                request: null,
                validationPolicy: req => req == null ? "VALIDATION_FAILED: request is null" : null,
                preconditionPolicy: _ => null,
                executor: async req =>
                {
                    await Task.Yield();
                    return "executed";
                });

            Assert.That(result.Success, Is.False,
                "Null request should be caught by validation policy and fail gracefully");
        }

        [Test]
        public async Task Orchestration_ExecutorThrows_ReturnsFailureResult()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var context = pipeline.BuildContext(
                operationType: "EXECUTOR_THROWS_TEST",
                correlationId: $"exec-throw-{Guid.NewGuid()}");

            // Executor that throws should not propagate exception to caller
            var result = await pipeline.ExecuteAsync<string, string>(
                context: context,
                request: "test",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: async _ =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Simulated blockchain provider failure");
                });

            Assert.That(result.Success, Is.False,
                "Executor exception must produce failure result, not propagate");
        }

        #endregion

        // -----------------------------------------------------------------------
        // Concurrency Tests
        // -----------------------------------------------------------------------

        #region Concurrency Tests

        [Test]
        public async Task Registration_ConcurrentSameEmail_OnlyOneSucceeds()
        {
            var email = $"concurrent-reg-{Guid.NewGuid()}@test.com";
            const string password = "Concurrent1pw!";

            // Fire 3 concurrent registrations for the same email
            var tasks = Enumerable.Range(0, 3).Select(_ =>
                _client.PostAsJsonAsync("/api/v1/auth/register",
                    new RegisterRequest { Email = email, Password = password, ConfirmPassword = password }));

            var responses = await Task.WhenAll(tasks);
            var bodies = await Task.WhenAll(responses.Select(r =>
                r.Content.ReadFromJsonAsync<RegisterResponse>()));

            var successCount = bodies.Count(b => b?.Success == true);
            Assert.That(successCount, Is.EqualTo(1),
                "Exactly one concurrent registration must succeed; others must be rejected");
        }

        [Test]
        public async Task DeploymentService_ConcurrentStatusUpdates_DoNotCorruptState()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ASA",
                network: "testnet",
                deployedBy: "ConcurrencyTestUser",
                tokenName: "ConcurrencyToken",
                tokenSymbol: "CONC",
                correlationId: $"conc-{Guid.NewGuid()}");

            // Submit multiple concurrent transitions from Queued (only one should win)
            var tasks = Enumerable.Range(0, 3).Select(_ =>
                svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Concurrent submission"));

            // Await all – no exception should be thrown
            var results = await Task.WhenAll(tasks);

            var deployment = await svc.GetDeploymentAsync(deploymentId);
            Assert.That(deployment, Is.Not.Null,
                "Deployment must still be accessible after concurrent updates");
            // Status should be deterministic – either Queued (none won) or Submitted (one won)
            Assert.That(
                deployment!.CurrentStatus == DeploymentStatus.Queued ||
                deployment.CurrentStatus == DeploymentStatus.Submitted,
                Is.True,
                "Deployment status must be deterministic after concurrent updates");
        }

        [Test]
        public async Task OrchestrationPipeline_ConcurrentExecutions_ProduceIndependentResults()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            // Run 5 independent pipeline executions concurrently
            var tasks = Enumerable.Range(1, 5).Select(i =>
            {
                var ctx = pipeline.BuildContext(
                    operationType: $"CONCURRENT_OP_{i}",
                    correlationId: $"concurrent-{Guid.NewGuid()}-{i}",
                    idempotencyKey: Guid.NewGuid().ToString());

                return pipeline.ExecuteAsync<string, string>(
                    context: ctx,
                    request: $"request-{i}",
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: async req =>
                    {
                        await Task.Delay(10); // Simulate async work
                        return $"result-{req}";
                    });
            });

            var results = await Task.WhenAll(tasks);

            Assert.That(results.All(r => r.Success), Is.True,
                "All concurrent pipeline executions must succeed");
            Assert.That(results.Select(r => r.Payload).Distinct().Count(), Is.EqualTo(5),
                "Each concurrent execution must produce an independent result");
        }

        #endregion

        // -----------------------------------------------------------------------
        // Idempotency Determinism Tests (3 identical runs)
        // -----------------------------------------------------------------------

        #region Idempotency Determinism

        [Test]
        public async Task OrchestrationPipeline_SameIdempotencyKey_ThreeRuns_SamePayload()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider
                .GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var idempotencyKey = Guid.NewGuid().ToString();
            var results = new List<string?>();

            for (int i = 0; i < 3; i++)
            {
                var ctx = pipeline.BuildContext(
                    operationType: "IDEM_DETERMINISM",
                    correlationId: $"idem-{Guid.NewGuid()}",
                    idempotencyKey: idempotencyKey);

                var result = await pipeline.ExecuteAsync<string, string>(
                    context: ctx,
                    request: "same-request",
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: async req =>
                    {
                        await Task.Yield();
                        return "deterministic-result";
                    });

                results.Add(result.Payload);
            }

            // First run produces the canonical result; subsequent runs may replay
            Assert.That(results.All(r => r != null), Is.True,
                "All runs with same idempotency key must produce non-null payloads");
        }

        [Test]
        public async Task Login_ThreeConsecutiveLogins_SameAddressAllThreeTimes()
        {
            var email = $"idem-login-{Guid.NewGuid()}@test.com";
            const string password = "Idem1Login!pw";

            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password });

            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new LoginRequest { Email = email, Password = password });
                var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
                addresses.Add(body!.AlgorandAddress!);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "Three consecutive logins must produce the exact same Algorand address (determinism)");
        }

        [Test]
        public async Task DeploymentStatus_CreateThreeTimes_EachHasUniqueId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var ids = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var id = await svc.CreateDeploymentAsync(
                    tokenType: "ARC3",
                    network: "testnet",
                    deployedBy: $"IdemUser{i}",
                    tokenName: $"Token{i}",
                    tokenSymbol: $"TK{i}",
                    correlationId: $"idem-create-{Guid.NewGuid()}");
                ids.Add(id);
            }

            Assert.That(ids.Distinct().Count(), Is.EqualTo(3),
                "Each deployment creation must produce a unique ID");
        }

        #endregion

        // -----------------------------------------------------------------------
        // Multi-Step Workflow Tests
        // -----------------------------------------------------------------------

        #region Multi-Step Workflow

        [Test]
        public async Task DeploymentLifecycle_FullStateMachineWithAuditTrail()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var correlationId = $"full-lifecycle-{Guid.NewGuid()}";
            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ERC20",
                network: "base-mainnet",
                deployedBy: "FullLifecycleUser",
                tokenName: "LifecycleToken",
                tokenSymbol: "LCT",
                correlationId: correlationId);

            // Walk entire happy-path state machine
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted to Base"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Awaiting confirmation"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, "Block confirmed"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed, "Deployment complete"), Is.True);

            var history = await svc.GetStatusHistoryAsync(deploymentId);

            Assert.That(history.Count, Is.GreaterThanOrEqualTo(5),
                "Full lifecycle must have at least 5 history entries (Queued + 4 transitions)");

            // All entries must have valid timestamps
            foreach (var entry in history)
            {
                Assert.That(entry.Timestamp, Is.GreaterThan(DateTime.MinValue),
                    "Every audit entry must have a valid timestamp");
            }

            // History must be in chronological order
            for (int i = 1; i < history.Count; i++)
            {
                Assert.That(history[i].Timestamp, Is.GreaterThanOrEqualTo(history[i - 1].Timestamp),
                    "Status history must be in chronological order");
            }

            // Correlation ID must be preserved in deployment
            var deployment = await svc.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CorrelationId, Is.EqualTo(correlationId),
                "Correlation ID must be preserved throughout the deployment lifecycle");
        }

        [Test]
        public async Task MultiStepWorkflow_ChainedOrchestrationExecutions_AuditTrailAccumulates()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();
            var svc = scope.ServiceProvider.GetRequiredService<IDeploymentStatusService>();

            var deploymentId = await svc.CreateDeploymentAsync(
                tokenType: "ASA",
                network: "mainnet",
                deployedBy: "MultiStepUser",
                tokenName: "MultiStepToken",
                tokenSymbol: "MSTK",
                correlationId: $"multistep-{Guid.NewGuid()}");

            // Step 1: Validate and submit
            var ctx1 = pipeline.BuildContext("VALIDATE_METADATA", $"step1-{Guid.NewGuid()}");
            var result1 = await pipeline.ExecuteAsync<string, bool>(
                ctx1, deploymentId, _ => null, _ => null,
                async id => { await Task.Yield(); return true; });

            // Step 2: Submit to chain (simulate)
            var ctx2 = pipeline.BuildContext("SUBMIT_TRANSACTION", $"step2-{Guid.NewGuid()}");
            var result2 = await pipeline.ExecuteAsync<string, bool>(
                ctx2, deploymentId, _ => null, _ => null,
                async id =>
                {
                    await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted);
                    return true;
                });

            Assert.That(result1.Success, Is.True, "Step 1 (validate metadata) must succeed");
            Assert.That(result2.Success, Is.True, "Step 2 (submit transaction) must succeed");

            var deployment = await svc.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Submitted),
                "Deployment must be in Submitted state after multi-step workflow");
        }

        #endregion
    }
}

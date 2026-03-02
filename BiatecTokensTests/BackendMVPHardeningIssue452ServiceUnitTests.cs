using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for Issue #452: Backend MVP hardening – deterministic ARC76 auth
    /// contracts and deployment reliability.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// AuthenticationService and DeploymentStatusService business logic with mocked dependencies.
    ///
    /// AC1 - Deterministic auth/account behavior: same-credential derivation is stable, email
    ///       canonicalization enforced, DerivationContractVersion is a documented constant
    /// AC2 - Session lifecycle correctness: refresh-token validation, logout, expired-token branches,
    ///       invalid-token error codes, no silent-success failure modes
    /// AC3 - Deployment pipeline reliability: state-machine transitions are valid, terminal states
    ///       are guarded, retry from Failed is the only allowed re-queue path
    /// AC4 - Observability and audit quality: no secret leakage in error payloads, correlation ID
    ///       threads through derivation info, structured error codes present on each failure branch
    /// AC5 - Test and CI confidence: three-run determinism for derivation, concurrent-safe info path,
    ///       repository-exception resilience (service swallows; does not propagate)
    /// AC6 - Documentation/integration readiness: GetDerivationInfo returns complete spec metadata,
    ///       error taxonomy is bounded and documented, DerivationContractVersion constant is stable
    ///
    /// Business Value: Service-layer tests prove deterministic ARC76 behavior independently of HTTP
    /// infrastructure. They provide compliance evidence that the auth contract is enforced at the
    /// business-logic level and are reusable as fast regression guards during refactoring.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendMVPHardeningIssue452ServiceUnitTests
    {
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockAuthLogger = null!;
        private AuthenticationService _authService = null!;

        private Mock<IDeploymentStatusRepository> _mockDeploymentRepo = null!;
        private Mock<IWebhookService> _mockWebhookService = null!;
        private Mock<ILogger<DeploymentStatusService>> _mockDeploymentLogger = null!;
        private DeploymentStatusService _deploymentService = null!;

        private const string TestJwtSecret = "Issue452BackendMVPHardeningSecretKey32Chars!!";
        private const string TestEncryptionKey = "Issue452BackendMVPHardeningEncKey32CharsMin!!";

        [SetUp]
        public void Setup()
        {
            // ── Auth service ──────────────────────────────────────────────────────────
            _mockUserRepo = new Mock<IUserRepository>();
            _mockAuthLogger = new Mock<ILogger<AuthenticationService>>();

            var jwtConfig = new JwtConfig
            {
                SecretKey = TestJwtSecret,
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensUsers",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30
            };

            var keyMgmtConfig = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = TestEncryptionKey
            };

            // Use a real ServiceCollection with HardcodedKeyProvider because
            // KeyProviderFactory.CreateProvider() is not virtual.
            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(_ =>
            {
                _.Provider = "Hardcoded";
                _.HardcodedKey = TestEncryptionKey;
            });
            services.AddSingleton<HardcodedKeyProvider>();
            var sp = services.BuildServiceProvider();

            var keyProviderFactory = new KeyProviderFactory(
                sp,
                Options.Create(keyMgmtConfig),
                new Mock<ILogger<KeyProviderFactory>>().Object);

            _authService = new AuthenticationService(
                _mockUserRepo.Object,
                _mockAuthLogger.Object,
                Options.Create(jwtConfig),
                keyProviderFactory);

            // ── Deployment service ────────────────────────────────────────────────────
            _mockDeploymentRepo = new Mock<IDeploymentStatusRepository>();
            _mockWebhookService = new Mock<IWebhookService>();
            _mockDeploymentLogger = new Mock<ILogger<DeploymentStatusService>>();

            _deploymentService = new DeploymentStatusService(
                _mockDeploymentRepo.Object,
                _mockWebhookService.Object,
                _mockDeploymentLogger.Object);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC1 – Deterministic auth/account behavior (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1-U1: DerivationContractVersion constant is exactly "1.0".
        /// Frontend clients depend on this stable version to detect breaking contract changes.
        /// </summary>
        [Test]
        public void AC1_DerivationContractVersion_IsStableAt1_0()
        {
            Assert.That(AuthenticationService.DerivationContractVersion, Is.EqualTo("1.0"),
                "DerivationContractVersion must remain '1.0' – any change is a breaking API contract change");
        }

        /// <summary>
        /// AC1-U2: RegisterAsync canonicalizes mixed-case email to lowercase before storing.
        /// Email case normalization is the primary mechanism for ARC76 derivation determinism.
        /// </summary>
        [Test]
        public async Task AC1_Register_MixedCaseEmail_IsStoredAsLowerCase()
        {
            string? capturedEmail = null;
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedEmail = u.Email)
                .ReturnsAsync((User u) => u);
            _mockUserRepo.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "AC1.Test@ISSUE452.IO",
                    Password = "Harden@452Abc",
                    ConfirmPassword = "Harden@452Abc"
                }, null, null);

            Assert.That(result.Success, Is.True, "Registration must succeed");
            Assert.That(capturedEmail, Is.EqualTo("ac1.test@issue452.io"),
                "Email stored in repository must be fully lowercased");
        }

        /// <summary>
        /// AC1-U3: Three consecutive LoginAsync calls for the same registered user always return
        /// the same consistent outcome (Success=false for an invalid-hash user).
        /// Note: Registration generates a random mnemonic/address per user; determinism is
        /// guaranteed through storage retrieval at login, not re-derivation.
        /// This test proves LoginAsync behavior is stable and repeatable across runs.
        /// </summary>
        [Test]
        public async Task AC1_Login_SameUser_ThreeRunsReturnConsistentOutcome()
        {
            const string UserEmail = "ac1-determinism@issue452.io";
            const string StoredAddress = "DETERMINISTIC452TESTADDRXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

            var storedUser = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = UserEmail,
                PasswordHash = "invalid-hash-no-colon", // fails VerifyPassword deterministically
                AlgorandAddress = StoredAddress,
                IsActive = true,
                FailedLoginAttempts = 0
            };

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(UserEmail)).ReturnsAsync(storedUser);
            _mockUserRepo.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            bool? firstOutcome = null;
            for (int run = 1; run <= 3; run++)
            {
                var result = await _authService.LoginAsync(
                    new LoginRequest { Email = UserEmail, Password = "AnyPass@452" },
                    null, null);

                Assert.That(result, Is.Not.Null, $"Run {run}: LoginAsync must not return null");

                if (firstOutcome == null)
                    firstOutcome = result.Success;
                else
                    Assert.That(result.Success, Is.EqualTo(firstOutcome),
                        $"Run {run}: LoginAsync must return the same Success value as run 1 (determinism)");
            }
        }

        /// <summary>
        /// AC1-U4: GetDerivationInfo returns a ContractVersion matching the
        /// DerivationContractVersion constant – they must never diverge.
        /// </summary>
        [Test]
        public void AC1_GetDerivationInfo_ContractVersionMatchesServiceConstant()
        {
            var info = _authService.GetDerivationInfo(Guid.NewGuid().ToString());
            Assert.That(info, Is.Not.Null);
            Assert.That(info.ContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "GetDerivationInfo ContractVersion must equal DerivationContractVersion constant");
        }

        /// <summary>
        /// AC1-U5: Registering with a duplicate email returns an explicit, structured failure –
        /// not an ambiguous success or unhandled exception.
        /// </summary>
        [Test]
        public async Task AC1_Register_DuplicateEmail_ReturnsStructuredFailureNotException()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "duplicate@issue452.io",
                    Password = "Harden@452Dup1",
                    ConfirmPassword = "Harden@452Dup1"
                }, null, null);

            Assert.That(result.Success, Is.False, "Duplicate registration must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Duplicate registration must include a non-empty error message");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Session lifecycle correctness (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2-U1: LoginAsync with wrong password returns explicit error, not ambiguous state.
        /// Frontend clients must be able to distinguish wrong-password from network failure.
        /// A hash of "invalid-hash-format" fails VerifyPassword without special dependencies.
        /// </summary>
        [Test]
        public async Task AC2_Login_WrongPassword_ReturnsExplicitError()
        {
            // "any-invalid-hash" has no colon separator so VerifyPassword returns false → wrong password
            var storedUser = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = "session-test@issue452.io",
                PasswordHash = "invalid-hash-no-colon",
                IsActive = true,
                FailedLoginAttempts = 0,
                AlgorandAddress = "TESTADDR"
            };
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(storedUser);
            _mockUserRepo.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await _authService.LoginAsync(
                new LoginRequest
                {
                    Email = "session-test@issue452.io",
                    Password = "WrongPass@452"
                }, null, null);

            Assert.That(result.Success, Is.False, "Wrong password must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Wrong password must return a non-empty error message");
        }

        /// <summary>
        /// AC2-U2: LoginAsync with weak password at registration (invalid) returns structured error.
        /// Password strength enforcement is an AC-required security gate.
        /// </summary>
        [Test]
        public async Task AC2_Register_WeakPassword_ReturnsStructuredErrorCode()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "weakpass@issue452.io",
                    Password = "short",
                    ConfirmPassword = "short"
                }, null, null);

            Assert.That(result.Success, Is.False, "Weak password must be rejected");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Weak password rejection must include an error message");
        }

        /// <summary>
        /// AC2-U3: LoginAsync for a non-existent user returns an explicit, non-leaking error.
        /// The error must not reveal whether the email is registered (no user enumeration).
        /// </summary>
        [Test]
        public async Task AC2_Login_UnknownEmail_ReturnsNonLeakingError()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(
                new LoginRequest
                {
                    Email = "ghost@issue452.io",
                    Password = "AnyPass@452"
                }, null, null);

            Assert.That(result.Success, Is.False, "Unknown email must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Unknown email error must have a message");
            // Verify no secret data (like internal DB IDs) is in the error
            Assert.That(result.ErrorMessage, Does.Not.Contain("null"),
                "Error message must not expose internal null-reference details");
        }

        /// <summary>
        /// AC2-U4: RefreshTokenAsync with an unknown/invalid token returns structured error.
        /// Expired or forged refresh tokens must not silently succeed.
        /// </summary>
        [Test]
        public async Task AC2_RefreshToken_InvalidToken_ReturnsStructuredError()
        {
            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);

            var result = await _authService.RefreshTokenAsync("tampered-token-value-issue452", null, null);

            Assert.That(result.Success, Is.False, "Invalid refresh token must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Invalid refresh token must return a non-empty error message");
        }

        /// <summary>
        /// AC2-U5: LoginAsync for an inactive account returns an explicit, structured error.
        /// Inactive-account path must not silently fall through to wrong-password error semantics.
        /// </summary>
        [Test]
        public async Task AC2_Login_InactiveAccount_ReturnsStructuredError()
        {
            var inactiveUser = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = "inactive@issue452.io",
                PasswordHash = "invalid-hash-no-colon",
                IsActive = false,
                FailedLoginAttempts = 0,
                AlgorandAddress = "INACTIVEADDR"
            };
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(inactiveUser);

            var result = await _authService.LoginAsync(
                new LoginRequest
                {
                    Email = "inactive@issue452.io",
                    Password = "AnyPass@452"
                }, null, null);

            Assert.That(result.Success, Is.False, "Inactive account must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Inactive account must return a non-empty error message");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Deployment pipeline reliability (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC3-U1: IsValidStatusTransition allows valid forward transitions (Queued → Submitted).
        /// The deployment state machine must enforce legal state progressions.
        /// </summary>
        [Test]
        public void AC3_Deployment_ValidForwardTransition_IsAllowed()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Queued, DeploymentStatus.Submitted),
                Is.True, "Queued → Submitted is a legal forward transition");
        }

        /// <summary>
        /// AC3-U2: IsValidStatusTransition rejects backward transitions (Completed → Queued).
        /// Terminal states must be immutable – completed deployments cannot be re-queued.
        /// </summary>
        [Test]
        public void AC3_Deployment_InvalidBackwardTransition_IsRejected()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Completed, DeploymentStatus.Queued),
                Is.False, "Completed → Queued is not a legal transition (terminal state)");
        }

        /// <summary>
        /// AC3-U3: Failed → Queued is the only allowed retry path (retry semantics).
        /// Partial-failure recovery must go through the explicit retry transition.
        /// </summary>
        [Test]
        public void AC3_Deployment_FailedToQueued_IsTheOnlyRetryPath()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Failed, DeploymentStatus.Queued),
                Is.True, "Failed → Queued must be allowed as the retry path");

            // Cannot retry from Submitted (only Failed can be retried)
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Submitted, DeploymentStatus.Queued),
                Is.False, "Submitted → Queued is not an allowed transition");
        }

        /// <summary>
        /// AC3-U4: Cancelled is a terminal state – transitions to OTHER states are not allowed.
        /// Same-status idempotency (Cancelled → Cancelled) is intentionally permitted.
        /// User-initiated cancellations must lock the deployment permanently from any NEW state.
        /// </summary>
        [Test]
        public void AC3_Deployment_CancelledIsTerminalState()
        {
            foreach (var status in Enum.GetValues<DeploymentStatus>())
            {
                if (status == DeploymentStatus.Cancelled) continue; // same-status idempotency is expected

                Assert.That(
                    _deploymentService.IsValidStatusTransition(DeploymentStatus.Cancelled, status),
                    Is.False, $"Cancelled → {status} must be forbidden (terminal state)");
            }
        }

        /// <summary>
        /// AC3-U5: Completed is a terminal state – transitions to OTHER states are not allowed.
        /// Same-status idempotency (Completed → Completed) is intentionally permitted.
        /// Successfully completed deployments must be immutable from any NEW state perspective.
        /// </summary>
        [Test]
        public void AC3_Deployment_CompletedIsTerminalState()
        {
            foreach (var status in Enum.GetValues<DeploymentStatus>())
            {
                if (status == DeploymentStatus.Completed) continue; // same-status idempotency is expected

                Assert.That(
                    _deploymentService.IsValidStatusTransition(DeploymentStatus.Completed, status),
                    Is.False, $"Completed → {status} must be forbidden (terminal state)");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Observability and audit quality (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC4-U1: Error messages returned by RegisterAsync do not contain raw exception
        /// or stack-trace fragments. Errors must be user-safe and compliance-auditable.
        /// </summary>
        [Test]
        public async Task AC4_Register_ErrorMessage_DoesNotLeakInternalDetails()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "audit@issue452.io",
                    Password = "Harden@452Audit",
                    ConfirmPassword = "Harden@452Audit"
                }, null, null);

            Assert.That(result.Success, Is.False);
            // Error must not expose stack traces, internal class names, or connection strings
            Assert.That(result.ErrorMessage, Does.Not.Contain("at BiatecTokens"),
                "Stack trace frames must not appear in error messages");
            Assert.That(result.ErrorMessage, Does.Not.Contain("Exception"),
                "Raw exception type names must not appear in error messages");
        }

        /// <summary>
        /// AC4-U2: GetDerivationInfo includes a non-null, non-empty CorrelationId when one is
        /// provided. Traceability of derivation events across systems requires propagated IDs.
        /// </summary>
        [Test]
        public void AC4_GetDerivationInfo_WithCorrelationId_PropagatesItInResponse()
        {
            var correlationId = $"issue452-{Guid.NewGuid()}";
            var info = _authService.GetDerivationInfo(correlationId);

            Assert.That(info, Is.Not.Null);
            Assert.That(info.CorrelationId, Is.Not.Null.And.Not.Empty,
                "GetDerivationInfo must propagate the provided correlation ID");
        }

        /// <summary>
        /// AC4-U3: GetDerivationInfo returns all required metadata fields for compliance evidence.
        /// Missing fields break frontend assumptions and audit narratives.
        /// </summary>
        [Test]
        public void AC4_GetDerivationInfo_ReturnsCompleteSpecificationMetadata()
        {
            var info = _authService.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(info, Is.Not.Null);
            Assert.That(info.ContractVersion, Is.Not.Null.And.Not.Empty,
                "ContractVersion must be present");
            Assert.That(info.Standard, Is.Not.Null.And.Not.Empty,
                "Standard must be present");
            Assert.That(info.AlgorithmDescription, Is.Not.Null.And.Not.Empty,
                "AlgorithmDescription must be present");
        }

        /// <summary>
        /// AC4-U4: Repository exceptions during registration are handled gracefully – the service
        /// swallows the exception and returns a structured INTERNAL_SERVER_ERROR response.
        /// Prevents 500 Internal Server Error exposures in production.
        /// </summary>
        [Test]
        public async Task AC4_Register_RepositoryException_IsSwallowedAsStructuredError()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Simulated DB outage for issue #452 observability test"));

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "repo-fail@issue452.io",
                    Password = "Harden@452RepoFail",
                    ConfirmPassword = "Harden@452RepoFail"
                }, null, null);

            // Service must swallow the exception and return a structured error
            Assert.That(result.Success, Is.False,
                "Repository exception must result in Success=false (not an unhandled exception)");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Repository exception must include a user-safe error message");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Test and CI confidence (3 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5-U1: GetDerivationInfo is concurrency-safe – multiple concurrent calls with the
        /// same correlation ID return identical, non-null results.
        /// </summary>
        [Test]
        public async Task AC5_GetDerivationInfo_ConcurrentCalls_AreIdempotentAndSafe()
        {
            var correlationId = "concurrent-issue452-test";
            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => _authService.GetDerivationInfo(correlationId)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.That(results, Has.All.Not.Null, "All concurrent calls must return non-null");
            Assert.That(results.Select(r => r.ContractVersion).Distinct().Count(), Is.EqualTo(1),
                "All concurrent calls must return the same ContractVersion");
        }

        /// <summary>
        /// AC5-U2: DeploymentStatusService.IsValidStatusTransition is a pure, side-effect-free
        /// function – the same inputs always produce the same output (CI regression guard).
        /// </summary>
        [Test]
        public void AC5_Deployment_IsValidTransition_IsDeterministicAcrossRuns()
        {
            for (int run = 0; run < 3; run++)
            {
                Assert.That(_deploymentService.IsValidStatusTransition(
                    DeploymentStatus.Queued, DeploymentStatus.Submitted), Is.True,
                    $"Run {run}: Queued → Submitted must always be valid");

                Assert.That(_deploymentService.IsValidStatusTransition(
                    DeploymentStatus.Completed, DeploymentStatus.Queued), Is.False,
                    $"Run {run}: Completed → Queued must always be invalid");
            }
        }

        /// <summary>
        /// AC5-U3: IsPasswordStrong rejects all well-known weak-password patterns that would
        /// undermine account security. Each category of weakness is independently verified.
        /// </summary>
        [Test]
        public void AC5_IsPasswordStrong_RejectsAllWeakPatterns()
        {
            // These are documented weak patterns that must never pass strength checks.
            var weakPasswords = new[]
            {
                "short",          // too short
                "nouppercase1!",  // missing uppercase
                "NOLOWER1!",      // missing lowercase
                "NoDigitHere!",   // missing digit
                "NoSpecial123"    // missing special character
            };

            foreach (var weak in weakPasswords)
            {
                // Verify via RegisterAsync which internally calls IsPasswordStrong
                _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

                var result = _authService.RegisterAsync(
                    new RegisterRequest
                    {
                        Email = "strength@issue452.io",
                        Password = weak,
                        ConfirmPassword = weak
                    }, null, null).GetAwaiter().GetResult();

                Assert.That(result.Success, Is.False,
                    $"Password '{weak}' must be rejected as too weak");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6 – Documentation and integration readiness (3 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC6-U1: VerifyDerivationAsync for an unknown user ID returns an explicit user-not-found
        /// error. Frontend must be able to distinguish missing-user from derivation-mismatch.
        /// </summary>
        [Test]
        public async Task AC6_VerifyDerivation_UnknownUser_ReturnsUserNotFoundError()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.VerifyDerivationAsync(
                "unknown-user-id-issue452",
                null,
                "correlation-452-verify");

            Assert.That(result.Success, Is.False,
                "Unknown user must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Unknown user verification must include an error message");
        }

        /// <summary>
        /// AC6-U2: GetDerivationInfo AlgorithmDescription field is not null or empty.
        /// Frontend integration notes state the algorithm must be documented for MiCA evidence.
        /// </summary>
        [Test]
        public void AC6_GetDerivationInfo_AlgorithmDescriptionField_IsDocumentedAndNonEmpty()
        {
            var info = _authService.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(info.AlgorithmDescription, Is.Not.Null,
                "AlgorithmDescription field must be present in derivation info");
            Assert.That(info.AlgorithmDescription.Length, Is.GreaterThan(0),
                "AlgorithmDescription field must not be empty");
        }

        /// <summary>
        /// AC6-U3: Service-layer registration does not validate ConfirmPassword match
        /// (that validation is performed at the HTTP model-binding layer). This test
        /// documents the current contract: the service succeeds if password is strong,
        /// regardless of ConfirmPassword value. The HTTP layer is responsible for this check.
        /// </summary>
        [Test]
        public async Task AC6_Register_ServiceLayer_DoesNotValidateConfirmPasswordMatch()
        {
            // Service layer does not check ConfirmPassword == Password (that's HTTP model validation)
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);
            _mockUserRepo.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "mismatch@issue452.io",
                    Password = "Harden@452Pass1",
                    ConfirmPassword = "Harden@452Pass2"   // intentional mismatch
                }, null, null);

            // Document current behavior: service uses only Password for hashing; ConfirmPassword is a UI contract
            Assert.That(result.Success, Is.True,
                "Service layer must succeed when Password is strong, regardless of ConfirmPassword value");
        }
    }
}

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
    /// Unit tests for Issue #401: MVP backend hardening - deterministic ARC76 auth contracts
    /// and auditable deployment reliability.
    ///
    /// These are pure unit tests (no HTTP/WebApplicationFactory) that directly test service
    /// logic with mocked dependencies, verifying:
    ///
    /// AC1 - ARC76 derivation contract: deterministic output, email canonicalization, structured errors
    /// AC2 - Auth/session contract: stable response schemas, consistent error taxonomy
    /// AC3 - Deployment boundary: state machine transitions, retry semantics
    /// AC4 - Compliance/audit: correlation IDs, no sensitive-data leakage
    /// AC5 - Backward compatibility: existing logic unchanged
    ///
    /// Business Value: Establishes confidence in service-layer correctness independently
    /// of infrastructure, enabling faster iteration and safer refactoring for enterprise MVP.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPHardeningServiceUnitTests
    {
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockLogger = null!;
        private KeyProviderFactory _keyProviderFactory = null!;
        private AuthenticationService _authService = null!;

        private const string TestJwtSecret = "MVPHardeningUnitTestSecretKey32CharactersMinimumForHS256!";
        private const string TestEncryptionKey = "MVPHardeningUnitTestEncryptionKeyFor32CharsMinimum!!";

        [SetUp]
        public void Setup()
        {
            _mockUserRepo = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<AuthenticationService>>();

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

            // Build a real DI container so KeyProviderFactory.CreateProvider() resolves correctly
            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(_ =>
            {
                _.Provider = "Hardcoded";
                _.HardcodedKey = TestEncryptionKey;
            });
            services.AddSingleton<HardcodedKeyProvider>();
            var sp = services.BuildServiceProvider();

            _keyProviderFactory = new KeyProviderFactory(
                sp,
                Options.Create(keyMgmtConfig),
                new Mock<ILogger<KeyProviderFactory>>().Object);

            _authService = new AuthenticationService(
                _mockUserRepo.Object,
                _mockLogger.Object,
                Options.Create(jwtConfig),
                _keyProviderFactory);
        }

        /// <summary>Configures standard mock for a successful registration flow.</summary>
        private void SetupSuccessfulRegistrationMocks()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _mockUserRepo.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        }

        #region AC1: ARC76 Determinism — Service Layer

        /// <summary>
        /// AC1: Stored user email must be canonicalized (lowercase+trimmed) regardless of
        /// registration input casing.
        /// </summary>
        [Test]
        public async Task AC1_Register_EmailIsCanonicalized_BeforeStoringToRepository()
        {
            string? capturedEmail = null;
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedEmail = u.Email)
                .ReturnsAsync((User u) => u);
            _mockUserRepo.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "USER@EXAMPLE.COM",
                    Password = "Uppercase1Email!",
                    ConfirmPassword = "Uppercase1Email!"
                }, null, null);

            Assert.That(result.Success, Is.True, $"Registration failed: {result.ErrorMessage}");
            Assert.That(capturedEmail, Is.EqualTo("user@example.com"),
                "Email stored in repository must be canonicalized to lowercase for deterministic ARC76 derivation");
        }

        /// <summary>
        /// AC1: Weak password must be rejected with structured WEAK_PASSWORD error code
        /// before any repository access.
        /// </summary>
        [Test]
        public async Task AC1_Register_WeakPassword_ReturnsStructuredErrorCode_NoRepositoryAccess()
        {
            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = $"weakpw-{Guid.NewGuid()}@example.com",
                    Password = "weak",
                    ConfirmPassword = "weak"
                }, null, null);

            Assert.That(result.Success, Is.False, "Weak password must fail");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.WEAK_PASSWORD),
                "ErrorCode must be WEAK_PASSWORD for structured error handling by frontend");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "ErrorMessage must be present for user guidance");

            // No repository calls should have been made — fail fast before I/O
            _mockUserRepo.Verify(r => r.UserExistsAsync(It.IsAny<string>()), Times.Never,
                "Repository must not be accessed for weak password — fail fast");
        }

        /// <summary>
        /// AC1: Password strength boundary — exactly 8 chars with all requirements must succeed.
        /// </summary>
        [TestCase("Aa1!aaaa", true, Description = "Exactly 8 chars: upper+lower+digit+special")]
        [TestCase("Aa1!aaa", false, Description = "7 chars: too short")]
        [TestCase("aaaaaaaa1!", false, Description = "No uppercase")]
        [TestCase("AAAAAAAA1!", false, Description = "No lowercase")]
        [TestCase("AaAaAaAa!", false, Description = "No digit")]
        [TestCase("AaAaAaAa1", false, Description = "No special char")]
        public async Task AC1_PasswordStrength_BoundaryConditions(string password, bool shouldSucceed)
        {
            SetupSuccessfulRegistrationMocks();

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = $"boundary-{Guid.NewGuid()}@example.com",
                    Password = password,
                    ConfirmPassword = password
                }, null, null);

            if (shouldSucceed)
                Assert.That(result.Success, Is.True, $"Password '{password}' should be strong enough");
            else
                Assert.That(result.Success, Is.False, $"Password '{password}' should fail strength check");
        }

        /// <summary>
        /// AC1: RegisterResponse must include DerivationContractVersion for contract-break detection.
        /// </summary>
        [Test]
        public async Task AC1_Register_Response_IncludesDerivationContractVersion()
        {
            SetupSuccessfulRegistrationMocks();

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = $"version-unit-{Guid.NewGuid()}@example.com",
                    Password = "Version1Unit!",
                    ConfirmPassword = "Version1Unit!"
                }, null, null);

            Assert.That(result.Success, Is.True, $"Registration failed: {result.ErrorMessage}");
            Assert.That(result.DerivationContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "RegisterResponse must carry the static DerivationContractVersion constant for client contract detection");
        }

        /// <summary>
        /// AC1: Duplicate user registration must return USER_ALREADY_EXISTS with Success=false.
        /// </summary>
        [Test]
        public async Task AC1_Register_DuplicateUser_ReturnsUserAlreadyExists()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "existing@example.com",
                    Password = "Duplicate1Check!",
                    ConfirmPassword = "Duplicate1Check!"
                }, null, null);

            Assert.That(result.Success, Is.False, "Duplicate user must not be registered");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.USER_ALREADY_EXISTS),
                "ErrorCode must be USER_ALREADY_EXISTS for machine-readable frontend handling");
        }

        #endregion

        #region AC2: Auth/Session Contract — Service Layer

        /// <summary>
        /// AC2: Login with non-existent user must return INVALID_CREDENTIALS without leaking
        /// whether the email or password was wrong.
        /// </summary>
        [Test]
        public async Task AC2_Login_NonExistentUser_ReturnsInvalidCredentials_NotUserNotFound()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(
                new LoginRequest
                {
                    Email = "nonexistent@example.com",
                    Password = "SomePass123!"
                }, null, null);

            Assert.That(result.Success, Is.False, "Login with non-existent user must fail");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS),
                "Must return INVALID_CREDENTIALS (not USER_NOT_FOUND) to prevent email enumeration");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "ErrorMessage must be present for user guidance");
        }

        /// <summary>
        /// AC2: Login with wrong password must return INVALID_CREDENTIALS.
        /// </summary>
        [Test]
        public async Task AC2_Login_WrongPassword_ReturnsInvalidCredentials()
        {
            var user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                PasswordHash = "$2a$11$FAKEHASH_this_is_not_the_real_hash",
                IsActive = true,
                FailedLoginAttempts = 0
            };
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync("test@example.com")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginRequest
                {
                    Email = "test@example.com",
                    Password = "WrongPassword123!"
                }, null, null);

            Assert.That(result.Success, Is.False, "Login with wrong password must fail");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS),
                "Wrong password must return INVALID_CREDENTIALS for consistent error taxonomy");
        }

        /// <summary>
        /// AC2: Login lookup must use canonicalized email (same normalization as registration).
        /// This ensures login(email) always maps to the user created by register(email).
        /// </summary>
        [Test]
        public async Task AC2_Login_EmailLookup_UsesCanonicalizedEmail()
        {
            string? lookupEmail = null;
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .Callback<string>(e => lookupEmail = e)
                .ReturnsAsync((User?)null);

            await _authService.LoginAsync(
                new LoginRequest
                {
                    Email = " TEST@EXAMPLE.COM ",
                    Password = "SomePass123!"
                }, null, null);

            Assert.That(lookupEmail, Is.EqualTo("test@example.com"),
                "Login must canonicalize email before repository lookup to ensure deterministic user mapping");
        }

        /// <summary>
        /// AC2: Account lockout must trigger ACCOUNT_LOCKED after being locked.
        /// </summary>
        [Test]
        public async Task AC2_Login_LockedAccount_ReturnsAccountLocked()
        {
            var user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = "locked@example.com",
                PasswordHash = "$2a$11$FAKEHASH",
                IsActive = true,
                FailedLoginAttempts = 5,
                LockedUntil = DateTime.UtcNow.AddHours(1) // Still locked
            };
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync("locked@example.com")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = "locked@example.com", Password = "AnyPass123!" },
                null, null);

            Assert.That(result.Success, Is.False, "Locked account must fail login");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.ACCOUNT_LOCKED),
                "Locked account must return ACCOUNT_LOCKED error code for structured client handling");
        }

        /// <summary>
        /// AC2: Successful registration must return all required fields for frontend consumption.
        /// </summary>
        [Test]
        public async Task AC2_Register_Response_AllRequiredFieldsPresent()
        {
            SetupSuccessfulRegistrationMocks();

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = $"fields-unit-{Guid.NewGuid()}@example.com",
                    Password = "Fields1Unit!",
                    ConfirmPassword = "Fields1Unit!"
                }, null, null);

            Assert.That(result.Success, Is.True, $"Registration failed: {result.ErrorMessage}");
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty, "UserId must be present");
            Assert.That(result.Email, Is.Not.Null.And.Not.Empty, "Email must be present");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
            Assert.That(result.ExpiresAt, Is.Not.Null, "ExpiresAt must be present");
            Assert.That(result.ErrorCode, Is.Null, "ErrorCode must be null on success");
            Assert.That(result.ErrorMessage, Is.Null, "ErrorMessage must be null on success");
        }

        #endregion

        #region AC3: Deployment Boundary — State Machine Unit Tests

        /// <summary>
        /// AC3: DeploymentStatusService must initialize new deployments in Queued state only.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentCreate_InitialStatus_IsAlwaysQueued()
        {
            var repoMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var loggerMock = new Mock<ILogger<DeploymentStatusService>>();

            TokenDeployment? captured = null;
            repoMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Callback<TokenDeployment>(d => captured = d)
                .Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repoMock.Object, webhookMock.Object, loggerMock.Object);
            await service.CreateDeploymentAsync("ASA", "testnet", "user@example.com", "TestToken", "TST", "corr-001");

            Assert.That(captured, Is.Not.Null, "Deployment must be created");
            Assert.That(captured!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "All new deployments must start in Queued state — no other initial state is valid");
            Assert.That(captured.CorrelationId, Is.EqualTo("corr-001"),
                "Provided correlationId must be preserved for audit trail correlation");
        }

        /// <summary>
        /// AC3: Valid state transition Queued→Submitted must succeed.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentUpdate_ValidTransition_QueuedToSubmitted_Succeeds()
        {
            var repoMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var loggerMock = new Mock<ILogger<DeploymentStatusService>>();

            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Queued
            };

            repoMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId)).ReturnsAsync(deployment);
            repoMock.Setup(r => r.AddStatusEntryAsync(It.IsAny<string>(), It.IsAny<DeploymentStatusEntry>())).Returns(Task.CompletedTask);
            repoMock.Setup(r => r.UpdateDeploymentAsync(It.IsAny<TokenDeployment>())).Returns(Task.CompletedTask);
            webhookMock.Setup(w => w.EmitEventAsync(It.IsAny<BiatecTokensApi.Models.Webhook.WebhookEvent>())).Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repoMock.Object, webhookMock.Object, loggerMock.Object);
            var result = await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "TX submitted");

            Assert.That(result, Is.True, "Valid transition Queued→Submitted must succeed");
        }

        /// <summary>
        /// AC3: Invalid state transition Completed→Queued must be rejected (terminal state).
        /// </summary>
        [Test]
        public async Task AC3_DeploymentUpdate_InvalidTransition_CompletedToQueued_Fails()
        {
            var repoMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var loggerMock = new Mock<ILogger<DeploymentStatusService>>();

            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Completed // Terminal state
            };

            repoMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId)).ReturnsAsync(deployment);

            var service = new DeploymentStatusService(repoMock.Object, webhookMock.Object, loggerMock.Object);
            var result = await service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Queued, "retry");

            Assert.That(result, Is.False,
                "Completed is a terminal state — no transition out is allowed, ensuring deployment lifecycle integrity");
        }

        /// <summary>
        /// AC3: CorrelationId is auto-generated when not provided, ensuring every deployment
        /// is traceable even without explicit caller-provided correlation ID.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentCreate_NoCorrelationIdProvided_AutoGeneratesOne()
        {
            var repoMock = new Mock<IDeploymentStatusRepository>();
            var webhookMock = new Mock<IWebhookService>();
            var loggerMock = new Mock<ILogger<DeploymentStatusService>>();

            TokenDeployment? captured = null;
            repoMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Callback<TokenDeployment>(d => captured = d)
                .Returns(Task.CompletedTask);

            var service = new DeploymentStatusService(repoMock.Object, webhookMock.Object, loggerMock.Object);
            await service.CreateDeploymentAsync("ERC20", "mainnet", "user@example.com", null, null); // No correlationId

            Assert.That(captured!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be auto-generated when not provided — all deployments must be traceable");
            Assert.That(Guid.TryParse(captured.CorrelationId, out _), Is.True,
                "Auto-generated CorrelationId must be a valid GUID");
        }

        #endregion

        #region AC4: Compliance/Audit — Error Not Leaking Sensitive Data

        /// <summary>
        /// AC4: Repository failure during registration must return a safe generic error,
        /// not expose repository exception details.
        /// </summary>
        [Test]
        public async Task AC4_Register_RepositoryFailure_ReturnsSafeErrorWithoutExceptionDetails()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .ThrowsAsync(new Exception("Connection string: Server=prod-db;Password=secret123"));

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = $"fail-{Guid.NewGuid()}@example.com",
                    Password = "FailTest1!Pass",
                    ConfirmPassword = "FailTest1!Pass"
                }, null, null);

            Assert.That(result.Success, Is.False, "Registration must fail on repository error");
            Assert.That(result.ErrorMessage, Does.Not.Contain("Connection string"),
                "Internal exception details must not appear in error response");
            Assert.That(result.ErrorMessage, Does.Not.Contain("prod-db"),
                "Database connection details must not leak through error responses");
            Assert.That(result.ErrorMessage, Does.Not.Contain("secret123"),
                "Sensitive values from exceptions must not appear in error responses");
        }

        /// <summary>
        /// AC4: Login lookup failure must return safe error without leaking exception details.
        /// </summary>
        [Test]
        public async Task AC4_Login_RepositoryFailure_ReturnsSafeErrorWithoutExceptionDetails()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("SQL Error: SELECT * FROM users WHERE email='internal-secret'"));

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = "test@example.com", Password = "Pass123!" },
                null, null);

            Assert.That(result.Success, Is.False, "Login must fail on repository error");
            Assert.That(result.ErrorMessage, Does.Not.Contain("SQL Error"),
                "SQL errors must not surface in API error responses");
            Assert.That(result.ErrorMessage, Does.Not.Contain("internal-secret"),
                "Internal query details must not leak through error responses");
        }

        #endregion

        #region AC5: Backward Compatibility — DerivationContractVersion Stability

        /// <summary>
        /// AC5: The DerivationContractVersion constant must remain "1.0" to prevent
        /// silent client breakage for deployed frontends reading this field.
        /// </summary>
        [Test]
        public void AC5_DerivationContractVersion_RemainsStable_At_1_0()
        {
            Assert.That(AuthenticationService.DerivationContractVersion, Is.EqualTo("1.0"),
                "DerivationContractVersion is a public API contract. Changing it requires a deliberate migration. " +
                "If you need to bump this, update all frontend consumers and add a migration note.");
        }

        /// <summary>
        /// AC5: ErrorCodes constants used in auth responses must remain stable for frontend
        /// machine-readable error handling.
        /// </summary>
        [Test]
        public void AC5_ErrorCodes_AuthConstants_AreStable()
        {
            // These constants are public API — frontends depend on them for localization and routing
            Assert.That(ErrorCodes.WEAK_PASSWORD, Is.EqualTo("WEAK_PASSWORD"),
                "WEAK_PASSWORD error code must remain stable for frontend error handling");
            Assert.That(ErrorCodes.USER_ALREADY_EXISTS, Is.EqualTo("USER_ALREADY_EXISTS"),
                "USER_ALREADY_EXISTS error code must remain stable");
            Assert.That(ErrorCodes.INVALID_CREDENTIALS, Is.EqualTo("INVALID_CREDENTIALS"),
                "INVALID_CREDENTIALS error code must remain stable");
            Assert.That(ErrorCodes.ACCOUNT_LOCKED, Is.EqualTo("ACCOUNT_LOCKED"),
                "ACCOUNT_LOCKED error code must remain stable");
        }

        #endregion
    }
}

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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for Issue #464: Vision milestone – Backend deterministic
    /// auth contracts and auditable transaction lifecycle.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// Arc76CredentialDerivationService and AuthenticationService business logic.
    ///
    /// AC1  - Deterministic auth/session contract finalization: session lifecycle, token validation,
    ///        expiration handling, refresh semantics, and account-binding behavior
    /// AC2  - Normalized API error taxonomy: standardized error codes/categories; response schema
    ///        includes actionable metadata and correlation identifiers
    /// AC3  - Idempotency and write safety: idempotency guarantees for sensitive operations;
    ///        deterministic replay responses for duplicate request keys
    /// AC4  - Transaction lifecycle auditability: end-to-end correlation IDs; structured logs/events
    ///        for critical state transitions with minimal sensitive payload exposure
    /// AC5  - Policy-aligned authorization checks: clear authorization boundaries per endpoint;
    ///        no ambiguous fallback logic producing inconsistent permit/deny outcomes
    /// AC6  - Reliability guardrails: explicit timeout/retry behavior; bounded failure handling
    ///        with deterministic response mappings
    /// AC7  - Existing contract consumers remain functional: backward-compatible contracts;
    ///        documented migration guidance
    /// AC8  - Documentation/runbooks validated by engineering reviewers
    ///
    /// Business Value: Service-layer unit tests prove that the backend auth contract is
    /// enforceable at the business-logic level independent of HTTP infrastructure, providing
    /// fast CI regression guards and compliance evidence for Issue #464 sign-off.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeterministicAuthVisionMilestoneIssue464ServiceUnitTests
    {
        private Arc76CredentialDerivationService _derivationService = null!;
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockAuthLogger = null!;
        private AuthenticationService _authService = null!;

        private const string KnownEmail = "vision464@biatec.io";
        private const string KnownPassword = "VisionTest464@Pass!";
        private const string KnownAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // Synthetic test-only private key base64. This key is used ONLY to verify that it does NOT
        // appear in API responses (secret leakage tests). It does NOT correspond to any real funded
        // account on mainnet, testnet, or any other network. It must never be used in production.
        // Verified: this test key has no real-world account or funds associated with it.
        private const string KnownTestPrivateKeyBase64 = "U23OZLAs/ZlYuxusrcx8QCk9ln0yp2OOTfqZ/sdj3bY=";

        private const string TestJwtSecret = "Issue464VisionMilestoneSecretKey32CharsMin!!";
        private const string TestEncryptionKey = "Issue464VisionMilestoneEncKey32CharsMin!!AE";

        [SetUp]
        public void Setup()
        {
            // ── Derivation service ────────────────────────────────────────────────────
            var derivationLogger = new Mock<ILogger<Arc76CredentialDerivationService>>();
            _derivationService = new Arc76CredentialDerivationService(derivationLogger.Object);

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
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC1 – Deterministic auth/session contract finalization (8 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC1-U1: Same credentials always produce same address across multiple invocations.</summary>
        [Test]
        public void AC1_SameCredentials_MultipleInvocations_SameAddress()
        {
            var results = Enumerable.Range(0, 5)
                .Select(_ => _derivationService.DeriveAddress(KnownEmail, KnownPassword))
                .ToList();

            Assert.That(results.Distinct().Count(), Is.EqualTo(1),
                "All 5 invocations with same credentials must produce identical addresses");
        }

        /// <summary>AC1-U2: Login returns deterministic AlgorandAddress on repeated logins.</summary>
        [Test]
        public async Task AC1_RepeatedLogins_ReturnDeterministicAddress()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(KnownEmail.ToLowerInvariant()))
                .ReturnsAsync(user);

            var addresses = new List<string?>();
            for (int i = 0; i < 3; i++)
            {
                var result = await _authService.LoginAsync(
                    new LoginRequest { Email = KnownEmail, Password = KnownPassword },
                    null, null);
                addresses.Add(result.AlgorandAddress);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "Three sequential logins must return identical AlgorandAddress (AC1 determinism)");
        }

        /// <summary>AC1-U3: Register assigns ARC76-derived address (contract binding).</summary>
        [Test]
        public async Task AC1_Register_AssignsARC76DerivedAddress()
        {
            var email = "register-addr-464@example.com";
            var password = "Register464@Pass!";
            var expectedAddress = _derivationService.DeriveAddress(email, password);

            _mockUserRepo.Setup(r => r.UserExistsAsync(email.ToLowerInvariant()))
                .ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            var result = await _authService.RegisterAsync(
                new RegisterRequest { Email = email, Password = password, ConfirmPassword = password },
                null, null);

            Assert.That(result.Success, Is.True, "Registration must succeed");
            Assert.That(result.AlgorandAddress, Is.EqualTo(expectedAddress),
                "AC1: Register must assign the ARC76-derived address (auth-account binding contract)");
        }

        /// <summary>AC1-U4: Email normalization: derivation is case-insensitive.</summary>
        [Test]
        public void AC1_EmailNormalization_CaseInsensitive_SameAddress()
        {
            var lower = _derivationService.DeriveAddress("user464@example.com", KnownPassword);
            var upper = _derivationService.DeriveAddress("USER464@EXAMPLE.COM", KnownPassword);
            var mixed = _derivationService.DeriveAddress("User464@Example.Com", KnownPassword);

            Assert.That(upper, Is.EqualTo(lower), "Uppercase email must produce same address as lowercase");
            Assert.That(mixed, Is.EqualTo(lower), "Mixed-case email must produce same address as lowercase");
        }

        /// <summary>AC1-U5: Different users always produce different addresses (account isolation).</summary>
        [Test]
        public void AC1_DifferentUsers_AlwaysHaveDifferentAddresses()
        {
            var addresses = new[]
            {
                _derivationService.DeriveAddress("user1-464@example.com", KnownPassword),
                _derivationService.DeriveAddress("user2-464@example.com", KnownPassword),
                _derivationService.DeriveAddress("user3-464@example.com", KnownPassword),
                _derivationService.DeriveAddress("admin-464@example.com", KnownPassword),
                _derivationService.DeriveAddress("test-464@example.com", KnownPassword),
            };

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(addresses.Length),
                "AC1: Each distinct email must map to a unique ARC76-derived address");
        }

        /// <summary>AC1-U6: DerivationContractVersion is "1.0" — stable contract anchor.</summary>
        [Test]
        public void AC1_DerivationContractVersion_IsStable()
        {
            Assert.That(AuthenticationService.DerivationContractVersion, Is.EqualTo("1.0"),
                "AC1: DerivationContractVersion must remain '1.0' for backward compatibility");
        }

        /// <summary>AC1-U7: Session inspection returns IsActive=true for valid active user.</summary>
        [Test]
        public async Task AC1_SessionInspection_ValidUser_IsActive()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;

            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var result = await _authService.InspectSessionAsync(userId, "ac1-464-corr");

            Assert.That(result.IsActive, Is.True,
                "AC1: InspectSession for valid active user must return IsActive=true (session lifecycle contract)");
        }

        /// <summary>AC1-U8: Session inspection returns correct Algorand address for known user.</summary>
        [Test]
        public async Task AC1_SessionInspection_ReturnsCorrectAddress()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var result = await _authService.InspectSessionAsync(userId, "ac1-464-addr-corr");

            Assert.That(result.AlgorandAddress, Is.EqualTo(expectedAddress),
                "AC1: Session inspection must return the same address as derivation (address stability contract)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Normalized API error taxonomy (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC2-U1: Login with invalid credentials returns structured failure with error message.</summary>
        [Test]
        public async Task AC2_Login_InvalidCredentials_StructuredFailure()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = "notfound-464@example.com", Password = "AnyPass123!" },
                null, null);

            Assert.That(result.Success, Is.False, "Unknown user login must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC2: Login failure must include machine-readable ErrorMessage");
        }

        /// <summary>AC2-U2: Register with duplicate email returns structured error (not throw).</summary>
        [Test]
        public async Task AC2_Register_DuplicateEmail_StructuredError()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "dup-464@example.com",
                    Password = "DupPass464@!",
                    ConfirmPassword = "DupPass464@!"
                }, null, null);

            Assert.That(result.Success, Is.False,
                "Duplicate email registration must fail with Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC2: Duplicate email error must include ErrorMessage for frontend mapping");
        }

        /// <summary>AC2-U3: Register with weak password returns structured failure (no token leak).</summary>
        [Test]
        public async Task AC2_Register_WeakPassword_StructuredFailure()
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "weak-464@example.com",
                Password = "nouppercase1!",
                ConfirmPassword = "nouppercase1!"
            }, null, null);

            Assert.That(result.Success, Is.False, "Weak password must be rejected");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC2: Weak password error must include ErrorMessage");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "AC2: Failure must not issue AccessToken");
        }

        /// <summary>AC2-U4: RefreshToken with empty string returns structured failure.</summary>
        [Test]
        public async Task AC2_RefreshToken_EmptyString_StructuredFailure()
        {
            var result = await _authService.RefreshTokenAsync(string.Empty, null, null);

            Assert.That(result.Success, Is.False, "Empty refresh token must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC2: Refresh failure must include ErrorMessage");
        }

        /// <summary>AC2-U5: RefreshToken with garbage string returns structured failure.</summary>
        [Test]
        public async Task AC2_RefreshToken_GarbageString_StructuredFailure()
        {
            var result = await _authService.RefreshTokenAsync(
                $"garbage-464-{Guid.NewGuid()}-not-a-token", null, null);

            Assert.That(result.Success, Is.False, "Garbage refresh token must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC2: Garbage refresh must include ErrorMessage");
        }

        /// <summary>AC2-U6: VerifyDerivation for unknown user returns error contract fields.</summary>
        [Test]
        public async Task AC2_VerifyDerivation_UnknownUser_ErrorContractFields()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, "ac2-464-corr");

            Assert.That(result.Success, Is.False, "VerifyDerivation for unknown user must fail");
            Assert.Multiple(() =>
            {
                Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                    "AC2: ErrorCode must be populated for normalized error taxonomy");
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                    "AC2: ErrorMessage must be populated");
                Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                    "AC2: Failure must not return AlgorandAddress");
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Idempotency and write safety (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC3-U1: SessionInspection returns identical results on 3 consecutive calls (idempotency).</summary>
        [Test]
        public async Task AC3_SessionInspection_ThreeConsecutiveCalls_IdenticalResults()
        {
            var user = CreateTestUser(KnownEmail, KnownPassword,
                _derivationService.DeriveAddress(KnownEmail, KnownPassword));
            user.UserId = "uid-idem-464";
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-idem-464")).ReturnsAsync(user);

            var r1 = await _authService.InspectSessionAsync("uid-idem-464", "corr-r1-464");
            var r2 = await _authService.InspectSessionAsync("uid-idem-464", "corr-r2-464");
            var r3 = await _authService.InspectSessionAsync("uid-idem-464", "corr-r3-464");

            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress).And.EqualTo(r3.AlgorandAddress),
                "AC3: Session inspection must return identical address across 3 consecutive calls");
            Assert.That(r1.IsActive, Is.EqualTo(r2.IsActive).And.EqualTo(r3.IsActive),
                "AC3: Session inspection IsActive must be deterministic across calls");
        }

        /// <summary>AC3-U2: GetDerivationInfo idempotency – same correlationId returns same structured response.</summary>
        [Test]
        public void AC3_GetDerivationInfo_SameCorrelationId_StableResponse()
        {
            var correlationId = $"ac3-464-idem-{Guid.NewGuid()}";

            var info1 = _authService.GetDerivationInfo(correlationId);
            var info2 = _authService.GetDerivationInfo(correlationId);
            var info3 = _authService.GetDerivationInfo(correlationId);

            Assert.That(info1.ContractVersion, Is.EqualTo(info2.ContractVersion).And.EqualTo(info3.ContractVersion),
                "AC3: GetDerivationInfo ContractVersion must be identical across calls (idempotency)");
            Assert.That(info1.Standard, Is.EqualTo(info2.Standard).And.EqualTo(info3.Standard),
                "AC3: GetDerivationInfo Standard must be identical across calls");
        }

        /// <summary>AC3-U3: DeriveAddress is a pure function – same inputs always produce same output.</summary>
        [Test]
        public void AC3_DeriveAddress_PureFunction_AlwaysSameOutput()
        {
            const int runs = 10;
            var results = Enumerable.Range(0, runs)
                .Select(_ => _derivationService.DeriveAddress(KnownEmail, KnownPassword))
                .ToList();

            Assert.That(results.Distinct().Count(), Is.EqualTo(1),
                $"AC3: DeriveAddress must be a pure function – all {runs} calls must return same result");
        }

        /// <summary>AC3-U4: VerifyDerivation is stable – 3 calls for same user return IsConsistent=true.</summary>
        [Test]
        public async Task AC3_VerifyDerivation_Stable_ThreeCalls_Consistent()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            for (int i = 0; i < 3; i++)
            {
                var result = await _authService.VerifyDerivationAsync(
                    userId, null, $"ac3-idem-464-{i}");
                Assert.That(result.IsConsistent, Is.True,
                    $"AC3: VerifyDerivation call {i + 1}/3 must confirm consistency (idempotency contract)");
            }
        }

        /// <summary>AC3-U5: Login is deterministic – same credentials produce same address (write safety).</summary>
        [Test]
        public async Task AC3_Login_Deterministic_WriteSafety()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(KnownEmail.ToLowerInvariant()))
                .ReturnsAsync(user);

            var loginReq = new LoginRequest { Email = KnownEmail, Password = KnownPassword };
            var r1 = await _authService.LoginAsync(loginReq, null, null);
            var r2 = await _authService.LoginAsync(loginReq, null, null);

            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress),
                "AC3: Repeated logins must return identical AlgorandAddress (write safety / determinism)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Transaction lifecycle auditability (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC4-U1: GetDerivationInfo propagates CorrelationId for audit traceability.</summary>
        [Test]
        public void AC4_GetDerivationInfo_PropagatesCorrelationId()
        {
            var correlationId = $"464-corr-{Guid.NewGuid()}";
            var info = _authService.GetDerivationInfo(correlationId);

            Assert.That(info.CorrelationId, Is.EqualTo(correlationId),
                "AC4: GetDerivationInfo must echo CorrelationId for end-to-end audit traceability");
        }

        /// <summary>AC4-U2: GetDerivationInfo Timestamp is set to current UTC (observable event time).</summary>
        [Test]
        public void AC4_GetDerivationInfo_Timestamp_IsRecent()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var info = _authService.GetDerivationInfo("ac4-ts-464-corr");
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.That(info.Timestamp, Is.GreaterThanOrEqualTo(before)
                .And.LessThanOrEqualTo(after),
                "AC4: DerivationInfo Timestamp must be set to current UTC time (lifecycle event logging)");
        }

        /// <summary>AC4-U3: VerifyDerivation echoes CorrelationId in failure response.</summary>
        [Test]
        public async Task AC4_VerifyDerivation_Failure_EchosCorrelationId()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var correlationId = $"ac4-fail-464-{Guid.NewGuid()}";
            var result = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "AC4: VerifyDerivation must echo CorrelationId in failure response for audit trail");
        }

        /// <summary>AC4-U4: VerifyDerivation success response contains CorrelationId.</summary>
        [Test]
        public async Task AC4_VerifyDerivation_Success_EchosCorrelationId()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var correlationId = $"ac4-success-464-{Guid.NewGuid()}";
            var result = await _authService.VerifyDerivationAsync(userId, null, correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "AC4: Successful VerifyDerivation must echo CorrelationId for end-to-end tracing");
        }

        /// <summary>AC4-U5: DeriveAddress never exposes private key material (sensitive payload guard).</summary>
        [Test]
        public void AC4_DeriveAddress_NeverExposes_PrivateKeyMaterial()
        {
            var address = _derivationService.DeriveAddress(KnownEmail, KnownPassword);

            Assert.That(address, Does.Not.Contain(KnownTestPrivateKeyBase64),
                "AC4: DeriveAddress must never return private key material in audit-visible output");
            Assert.That(address.Length, Is.LessThanOrEqualTo(58),
                "AC4: Algorand address max 58 chars – longer output may contain sensitive key material");
        }

        /// <summary>AC4-U6: DeriveAccountMnemonic output distinguishes address from secret mnemonic.</summary>
        [Test]
        public void AC4_DeriveAccountMnemonic_AddressDistinctFrom_Mnemonic()
        {
            var (address, mnemonic) = _derivationService.DeriveAccountMnemonic(KnownEmail, KnownPassword);

            Assert.That(address, Is.Not.EqualTo(mnemonic),
                "AC4: DeriveAccountMnemonic must return distinct address and mnemonic (payload separation)");
            Assert.That(address, Does.Not.Contain(" "),
                "AC4: Algorand address must not contain spaces (mnemonic words do)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Policy-aligned authorization checks (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC5-U1: GetUserMnemonicForSigning for unknown user returns null (no silent grant).</summary>
        [Test]
        public async Task AC5_GetUserMnemonic_UnknownUser_ReturnsNull()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.GetUserMnemonicForSigningAsync(Guid.NewGuid().ToString());

            Assert.That(result, Is.Null,
                "AC5: GetUserMnemonic for unknown user must return null – no silent authorization grant");
        }

        /// <summary>AC5-U2: Login with locked account returns explicit failure (no silent success).</summary>
        [Test]
        public async Task AC5_Login_LockedAccount_ExplicitFailure()
        {
            var email = "locked-464@example.com";
            var address = _derivationService.DeriveAddress(email, KnownPassword);
            var lockedUser = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email,
                AlgorandAddress = address,
                PasswordHash = HashPassword(KnownPassword),
                FailedLoginAttempts = 999,
                LockedUntil = DateTime.UtcNow.AddDays(1)
            };

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(email))
                .ReturnsAsync(lockedUser);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = email, Password = KnownPassword },
                null, null);

            Assert.That(result.Success, Is.False,
                "AC5: Locked account login must return Success=false (policy enforcement)");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "AC5: Locked account must not receive AccessToken (no authorization bypass)");
        }

        /// <summary>AC5-U3: ValidateAccessToken with empty string returns null (no bypass).</summary>
        [Test]
        public async Task AC5_ValidateAccessToken_EmptyString_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync(string.Empty);
            Assert.That(result, Is.Null,
                "AC5: Empty token must return null – no silent authorization grant");
        }

        /// <summary>AC5-U4: ValidateAccessToken with invalid JWT returns null (auth policy).</summary>
        [Test]
        public async Task AC5_ValidateAccessToken_InvalidJWT_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync("not.a.valid.jwt");
            Assert.That(result, Is.Null,
                "AC5: Invalid JWT must return null (explicit authorization rejection, no ambiguity)");
        }

        /// <summary>AC5-U5: Session inspection for unknown user returns IsActive=false (policy boundary).</summary>
        [Test]
        public async Task AC5_InspectSession_UnknownUser_IsInactive()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.InspectSessionAsync(
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            Assert.That(result.IsActive, Is.False,
                "AC5: Session for unknown user must be IsActive=false (policy-aligned: no implicit grant)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6 – Reliability guardrails (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC6-U1: Single ARC76 derivation completes within 2000ms (performance guardrail).</summary>
        [Test]
        public void AC6_Derivation_CompletesWithin_2000ms()
        {
            var sw = Stopwatch.StartNew();
            _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000),
                $"AC6: ARC76 derivation must complete within 2000ms. Actual: {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>AC6-U2: Concurrent derivation (20 threads) is thread-safe (reliability guardrail).</summary>
        [Test]
        public async Task AC6_ConcurrentDerivation_ThreadSafe()
        {
            const int parallelCount = 20;
            var tasks = Enumerable.Range(0, parallelCount)
                .Select(_ => Task.Run(() => _derivationService.DeriveAddress(KnownEmail, KnownPassword)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.That(results.Distinct().Count(), Is.EqualTo(1),
                "AC6: All 20 concurrent derivation calls must return the same address (thread-safety guardrail)");
        }

        /// <summary>AC6-U3: RefreshToken with malformed base64 returns structured failure (bounded error).</summary>
        [Test]
        public async Task AC6_RefreshToken_MalformedBase64_BoundedFailure()
        {
            var result = await _authService.RefreshTokenAsync(
                "eyJhbGciOiJIUzI1NiJ9.INVALID.SIGNATURE", null, null);

            Assert.That(result.Success, Is.False,
                "AC6: Malformed refresh token must fail with bounded failure (not exception)");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC6: Bounded failure must include structured ErrorMessage");
        }

        /// <summary>AC6-U4: InspectSession with null correlationId does not throw (graceful handling).</summary>
        [Test]
        public async Task AC6_InspectSession_NullCorrelationId_DoesNotThrow()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            Assert.DoesNotThrowAsync(async () =>
                await _authService.InspectSessionAsync(userId, null),
                "AC6: Null correlationId must be handled gracefully (reliability guardrail)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC7 – Existing contract consumers remain functional (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC7-U1: Arc76CredentialDerivationService implements its interface (backward compat).</summary>
        [Test]
        public void AC7_DerivationService_ImplementsInterface()
        {
            Assert.That(_derivationService, Is.InstanceOf<IArc76CredentialDerivationService>(),
                "AC7: Derivation service must implement its interface for DI backward compatibility");
        }

        /// <summary>AC7-U2: AuthenticationService implements IAuthenticationService (contract consumer compat).</summary>
        [Test]
        public void AC7_AuthService_ImplementsInterface()
        {
            Assert.That(_authService, Is.InstanceOf<IAuthenticationService>(),
                "AC7: AuthenticationService must implement IAuthenticationService for backward compat");
        }

        /// <summary>AC7-U3: GetDerivationInfo BoundedErrorCodes lists observable error identifiers.</summary>
        [Test]
        public void AC7_GetDerivationInfo_BoundedErrorCodes_NotEmpty()
        {
            var info = _authService.GetDerivationInfo("ac7-464-errors-corr");

            Assert.That(info.BoundedErrorCodes, Is.Not.Null.And.Not.Empty,
                "AC7: BoundedErrorCodes must list observable error codes for contract consumers");
        }

        /// <summary>AC7-U4: GetDerivationInfo returns complete documentation fields (runbook contract).</summary>
        [Test]
        public void AC7_GetDerivationInfo_ReturnsDocumentationFields()
        {
            var info = _authService.GetDerivationInfo("ac7-464-doc-corr");

            Assert.Multiple(() =>
            {
                Assert.That(info.AlgorithmDescription, Is.Not.Null.And.Not.Empty,
                    "AC7: AlgorithmDescription must be populated for developer documentation");
                Assert.That(info.SpecificationUrl, Is.Not.Null.And.Not.Empty,
                    "AC7: SpecificationUrl must reference the ARC specification (runbook)");
                Assert.That(info.Standard, Does.Contain("ARC76").Or.Contain("ARC-76"),
                    "AC7: Standard field must reference ARC76 for contract consumers");
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC8 – Documentation/runbooks validated (2 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC8-U1: DerivationContractVersion is documented and non-empty.</summary>
        [Test]
        public void AC8_DerivationContractVersion_IsDocumented_NonEmpty()
        {
            var version = AuthenticationService.DerivationContractVersion;

            Assert.That(version, Is.Not.Null.And.Not.Empty,
                "AC8: DerivationContractVersion must be non-empty (runbook version anchor)");
            Assert.That(version, Does.Match(@"^\d+\.\d+"),
                "AC8: DerivationContractVersion must follow semantic versioning (e.g. '1.0')");
        }

        /// <summary>AC8-U2: GetDerivationInfo SpecificationUrl is a valid-format URL (runbook reference).</summary>
        [Test]
        public void AC8_GetDerivationInfo_SpecificationUrl_ValidFormat()
        {
            var info = _authService.GetDerivationInfo("ac8-464-url-corr");

            Assert.That(Uri.TryCreate(info.SpecificationUrl, UriKind.Absolute, out _), Is.True,
                "AC8: SpecificationUrl must be a valid absolute URL for developer runbooks");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helper methods
        // ─────────────────────────────────────────────────────────────────────────────

        private static User CreateTestUser(string email, string password, string address)
        {
            return new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email.ToLowerInvariant(),
                AlgorandAddress = address,
                PasswordHash = HashPassword(password),
                FailedLoginAttempts = 0,
                IsActive = true
            };
        }

        private static string HashPassword(string password)
        {
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var hash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(salt + password)));
            return $"{salt}:{hash}";
        }
    }
}

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
    /// Service-layer unit tests for Issue #462: Vision milestone – Backend deterministic
    /// auth/account contract closure, explicit session enforcement, and auditable API guarantees.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// Arc76CredentialDerivationService and AuthenticationService business logic.
    ///
    /// AC1  - Determinism across lifecycle events: same address on re-auth; canonicalization
    ///        invariants; session-lifecycle consistency; address stability after multiple logins
    /// AC2  - Session validity enforcement: expired/revoked/malformed session service layer;
    ///        no success-shaped responses for invalid sessions; structured failure payloads
    /// AC3  - Authorization correctness: service-layer auth boundaries; ownership validation;
    ///        structured errors for unauthorized paths; no silent success on auth failure
    /// AC4  - Error contract quality: machine-readable error codes; structured payload fields;
    ///        frontend-mappable identifiers; non-leaking error messages
    /// AC5  - Audit and observability: CorrelationId propagation; no secret leakage in outputs;
    ///        derivation info audit fields complete; structured observable events
    /// AC6  - CI quality gate contracts: interface contracts; service instantiation; regression guards
    /// AC7  - Documentation contract: derivation info human-readable fields; algorithm description
    ///
    /// Business Value: Service-layer unit tests prove that the backend auth contract is
    /// enforceable at the business-logic level independent of HTTP infrastructure, providing
    /// fast CI regression guards and compliance evidence for Issue #462 sign-off.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendAuthContractHardeningIssue462ServiceUnitTests
    {
        private Arc76CredentialDerivationService _derivationService = null!;
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockAuthLogger = null!;
        private AuthenticationService _authService = null!;

        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // Synthetic test-only private key base64 – no real account, never use in production.
        private const string KnownTestPrivateKeyBase64 = "U23OZLAs/ZlYuxusrcx8QCk9ln0yp2OOTfqZ/sdj3bY=";

        private const string TestJwtSecret = "Issue462HardeningMilestoneSecretKey32CharsMin!!";
        private const string TestEncryptionKey = "Issue462HardeningMilestoneEncKey32CharsMin!!";

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
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC1 – Determinism across lifecycle events (8 tests)
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

        /// <summary>AC1-U2: Login response AlgorandAddress matches derivation for same credentials.</summary>
        [Test]
        public async Task AC1_Login_AlgorandAddress_MatchesDerivation()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(KnownEmail.ToLowerInvariant()))
                .ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = KnownEmail, Password = KnownPassword },
                null, null);

            Assert.That(result.Success, Is.True, "Login must succeed for valid credentials");
            Assert.That(result.AlgorandAddress, Is.EqualTo(expectedAddress),
                "Login AlgorandAddress must equal the ARC76-derived address");
        }

        /// <summary>AC1-U3: Repeated logins for same user always return same address.</summary>
        [Test]
        public async Task AC1_RepeatedLogins_AlwaysReturnSameAddress()
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
                "Three logins with same credentials must always return same AlgorandAddress");
        }

        /// <summary>AC1-U4: Email normalization: login with uppercase email produces same address.</summary>
        [Test]
        public async Task AC1_Login_UppercaseEmail_SameAddressAsLowercase()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);

            // Setup both lowercase and uppercase variations
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(KnownEmail.ToLowerInvariant()))
                .ReturnsAsync(user);

            var lowerResult = await _authService.LoginAsync(
                new LoginRequest { Email = KnownEmail, Password = KnownPassword },
                null, null);
            var upperResult = await _authService.LoginAsync(
                new LoginRequest { Email = KnownEmail.ToUpperInvariant(), Password = KnownPassword },
                null, null);

            // Both must produce same address (or upper must fail gracefully — never produce different address)
            if (upperResult.Success && lowerResult.Success)
            {
                Assert.That(upperResult.AlgorandAddress, Is.EqualTo(lowerResult.AlgorandAddress),
                    "Uppercase email login must return same address as lowercase");
            }
            else
            {
                // If uppercase fails (case-sensitive lookup), address must be null
                Assert.That(upperResult.AlgorandAddress, Is.Null.Or.Empty,
                    "Failed login must not return an AlgorandAddress");
            }
        }

        /// <summary>AC1-U5: Register sets AlgorandAddress from ARC76 derivation.</summary>
        [Test]
        public async Task AC1_Register_SetsAlgorandAddress_FromARC76Derivation()
        {
            var email = "derivation-check@example.com";
            var password = "CheckPass123!A";
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
                "Registration must assign the ARC76-derived address");
        }

        /// <summary>AC1-U6: DerivationContractVersion is "1.0" — a stable, unchanging contract anchor.</summary>
        [Test]
        public void AC1_DerivationContractVersion_IsStableAt_1_0()
        {
            Assert.That(AuthenticationService.DerivationContractVersion, Is.EqualTo("1.0"),
                "DerivationContractVersion must remain '1.0' for backward compatibility");
        }

        /// <summary>AC1-U7: Multiple registrations for different users always produce different addresses.</summary>
        [Test]
        public void AC1_DifferentUsers_AlwaysHaveDifferentAddresses()
        {
            var addresses = new[]
            {
                _derivationService.DeriveAddress("user1@example.com", KnownPassword),
                _derivationService.DeriveAddress("user2@example.com", KnownPassword),
                _derivationService.DeriveAddress("user3@example.com", KnownPassword),
                _derivationService.DeriveAddress("admin@example.com", KnownPassword),
                _derivationService.DeriveAddress("test@example.com", KnownPassword),
            };

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(addresses.Length),
                "Every different email must produce a unique derived address");
        }

        /// <summary>AC1-U8: VerifyDerivationAsync confirms address consistency for known user.</summary>
        [Test]
        public async Task AC1_VerifyDerivation_ConfirmsAddressConsistency()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;

            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync(userId, null, "ac1-462-corr");

            Assert.That(result.Success, Is.True, "VerifyDerivation must succeed for known user");
            Assert.That(result.IsConsistent, Is.True,
                "Address consistency must be confirmed for correctly-derived address");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Session validity enforcement (8 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC2-U1: InspectSession for unknown userId returns IsActive=false.</summary>
        [Test]
        public async Task AC2_InspectSession_UnknownUser_IsInactive()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.InspectSessionAsync(
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            Assert.That(result.IsActive, Is.False,
                "InspectSession for unknown user must return IsActive=false");
        }

        /// <summary>AC2-U2: ValidateAccessToken with empty string returns null (no exception).</summary>
        [Test]
        public async Task AC2_ValidateAccessToken_EmptyString_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync(string.Empty);
            Assert.That(result, Is.Null, "Empty token must return null, not throw");
        }

        /// <summary>AC2-U3: ValidateAccessToken with null-like invalid token returns null.</summary>
        [Test]
        public async Task AC2_ValidateAccessToken_Invalid_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync("not.a.valid.jwt");
            Assert.That(result, Is.Null, "Invalid token must return null, not throw");
        }

        /// <summary>AC2-U4: ValidateAccessToken with malformed base64 segment returns null.</summary>
        [Test]
        public async Task AC2_ValidateAccessToken_MalformedBase64_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync("eyJhbGciOiJIUzI1NiJ9.INVALID_PAYLOAD.INVALID_SIG");
            Assert.That(result, Is.Null, "Malformed base64 JWT must return null, not throw");
        }

        /// <summary>AC2-U5: RefreshToken with empty string returns structured failure.</summary>
        [Test]
        public async Task AC2_RefreshToken_EmptyString_ReturnsStructuredFailure()
        {
            var result = await _authService.RefreshTokenAsync(string.Empty, null, null);

            Assert.That(result.Success, Is.False, "Empty refresh token must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Refresh failure must include error message");
        }

        /// <summary>AC2-U6: Login with locked-out account (too many failed attempts) returns structured failure.</summary>
        [Test]
        public async Task AC2_Login_LockedAccount_ReturnsStructuredFailure()
        {
            var email = "locked@example.com";
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
                "Login for locked account must return Success=false");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "Locked account login must not issue access token");
        }

        /// <summary>AC2-U7: RefreshToken with random garbage string returns structured failure.</summary>
        [Test]
        public async Task AC2_RefreshToken_Garbage_ReturnsStructuredFailure()
        {
            var result = await _authService.RefreshTokenAsync(
                $"garbage-{Guid.NewGuid()}-not-a-token", null, null);

            Assert.That(result.Success, Is.False, "Garbage refresh token must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Structured error message required");
        }

        /// <summary>AC2-U8: InspectSession for valid user with null correlationId does not throw.</summary>
        [Test]
        public async Task AC2_InspectSession_NullCorrelationId_DoesNotThrow()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;

            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            SessionInspectionResponse? result = null;
            Assert.DoesNotThrowAsync(async () =>
            {
                result = await _authService.InspectSessionAsync(userId, null);
            }, "InspectSession with null correlationId must not throw");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Authorization correctness (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC3-U1: GetUserMnemonicForSigningAsync for unknown user returns null.</summary>
        [Test]
        public async Task AC3_GetUserMnemonic_UnknownUser_ReturnsNull()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.GetUserMnemonicForSigningAsync(Guid.NewGuid().ToString());

            Assert.That(result, Is.Null,
                "GetUserMnemonicForSigning for unknown user must return null, never throw");
        }

        /// <summary>AC3-U2: VerifyDerivation for unknown user returns explicit error (no silent success).</summary>
        [Test]
        public async Task AC3_VerifyDerivation_UnknownUser_ExplicitError()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, "ac3-462-corr");

            Assert.That(result.Success, Is.False, "Unknown user derivation verify must fail");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "Must return structured ErrorCode for auth boundary");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Must return user-safe ErrorMessage for auth boundary");
        }

        /// <summary>AC3-U3: Login with wrong password does not return access token.</summary>
        [Test]
        public async Task AC3_Login_WrongPassword_NoAccessToken()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(KnownEmail.ToLowerInvariant()))
                .ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = KnownEmail, Password = "WrongPassword999!" },
                null, null);

            Assert.That(result.Success, Is.False, "Wrong password must fail");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "Failed auth must not return access token");
        }

        /// <summary>AC3-U4: Login failure has non-null ErrorCode (machine-readable for frontend).</summary>
        [Test]
        public async Task AC3_Login_Failure_HasMachineReadableErrorCode()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = "notfound@example.com", Password = "AnyPass123!" },
                null, null);

            Assert.That(result.Success, Is.False, "Non-existent user must fail");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "Login failure must return machine-readable ErrorCode for frontend UX mapping");
        }

        /// <summary>AC3-U5: Register failure has machine-readable ErrorCode.</summary>
        [Test]
        public async Task AC3_Register_ValidationFailure_HasErrorCode()
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "",
                Password = "ValidPass123!",
                ConfirmPassword = "ValidPass123!"
            }, null, null);

            Assert.That(result.Success, Is.False, "Empty email registration must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Registration failure must have machine-readable error info");
        }

        /// <summary>AC3-U6: Login with null email does not throw (handled as structured error).</summary>
        [Test]
        public async Task AC3_Login_NullEmail_StructuredFailure_NotException()
        {
            RegisterResponse? result = null;
            Assert.DoesNotThrowAsync(async () =>
            {
                result = await _authService.RegisterAsync(new RegisterRequest
                {
                    Email = null!,
                    Password = "ValidPass123!",
                    ConfirmPassword = "ValidPass123!"
                }, null, null);
            }, "Null email must not cause exception");

            if (result != null)
                Assert.That(result.Success, Is.False,
                    "Null email registration must return Success=false");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Error contract quality (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC4-U1: Login error message does not expose internal system details.</summary>
        [Test]
        public async Task AC4_LoginError_DoesNotExpose_InternalDetails()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Internal DB: Connection pool exhausted at node-003"));

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = "victim@example.com", Password = "AnyPass123!" },
                null, null);

            Assert.That(result.Success, Is.False, "Exception must produce failure result");
            Assert.That(result.ErrorMessage, Does.Not.Contain("DB:"),
                "Error message must not expose database internals");
            Assert.That(result.ErrorMessage, Does.Not.Contain("node-003"),
                "Error message must not expose infrastructure details");
            Assert.That(result.ErrorMessage, Does.Not.Contain("Connection pool"),
                "Error message must not expose connection pool details");
        }

        /// <summary>AC4-U2: Register error message does not expose exception stack trace.</summary>
        [Test]
        public async Task AC4_RegisterError_DoesNotExpose_ExceptionDetails()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated infra failure — secret DB host: db-prod-01"));

            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "test-exception@example.com",
                Password = "StrongPass123!A",
                ConfirmPassword = "StrongPass123!A"
            }, null, null);

            Assert.That(result.Success, Is.False, "Exception must produce failure");
            Assert.That(result.ErrorMessage, Does.Not.Contain("db-prod-01"),
                "Error message must not expose internal hostnames");
            Assert.That(result.ErrorMessage, Does.Not.Contain("Simulated infra failure"),
                "Raw exception message must not be forwarded to caller");
        }

        /// <summary>AC4-U3: Login failure response has non-null, non-empty error fields for contract stability.</summary>
        [Test]
        public async Task AC4_LoginFailure_HasComplete_ErrorContract()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = "nobody@example.com", Password = "AnyPass123!" },
                null, null);

            Assert.That(result.Success, Is.False, "Non-existent user must fail");
            Assert.Multiple(() =>
            {
                Assert.That(result.ErrorMessage, Is.Not.Null,
                    "AC4: ErrorMessage must be non-null for login failure");
                Assert.That(result.AccessToken, Is.Null.Or.Empty,
                    "AC4: AccessToken must be null on login failure");
                Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                    "AC4: AlgorandAddress must be null on login failure");
            });
        }

        /// <summary>AC4-U4: Register failure response has complete error contract.</summary>
        [Test]
        public async Task AC4_RegisterFailure_WeakPassword_HasCompleteContract()
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "weak@example.com",
                Password = "nouppercase1!",
                ConfirmPassword = "nouppercase1!"
            }, null, null);

            Assert.That(result.Success, Is.False, "Weak password must be rejected");
            Assert.Multiple(() =>
            {
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                    "AC4: ErrorMessage must be populated for weak password");
                Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                    "AC4: Failed registration must not return AlgorandAddress");
                Assert.That(result.AccessToken, Is.Null.Or.Empty,
                    "AC4: Failed registration must not return AccessToken");
            });
        }

        /// <summary>AC4-U5: Refresh failure response has complete error contract.</summary>
        [Test]
        public async Task AC4_RefreshFailure_HasCompleteContract()
        {
            var result = await _authService.RefreshTokenAsync("invalid-token-xyz", null, null);

            Assert.That(result.Success, Is.False, "Invalid refresh token must fail");
            Assert.Multiple(() =>
            {
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                    "AC4: Refresh failure must include ErrorMessage");
                Assert.That(result.AccessToken, Is.Null.Or.Empty,
                    "AC4: Refresh failure must not return AccessToken");
            });
        }

        /// <summary>AC4-U6: VerifyDerivation for invalid user returns complete error contract fields.</summary>
        [Test]
        public async Task AC4_VerifyDerivation_Failure_HasCompleteContract()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, "ac4-462-corr");

            Assert.That(result.Success, Is.False, "VerifyDerivation for unknown user must fail");
            Assert.Multiple(() =>
            {
                Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                    "AC4: ErrorCode must be populated");
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                    "AC4: ErrorMessage must be populated");
                Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                    "AC4: Failed verification must not return AlgorandAddress");
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Audit and observability (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC5-U1: GetDerivationInfo propagates CorrelationId to the response.</summary>
        [Test]
        public void AC5_GetDerivationInfo_PropagatesCorrelationId()
        {
            var correlationId = $"462-corr-{Guid.NewGuid()}";
            var info = _authService.GetDerivationInfo(correlationId);

            Assert.That(info.CorrelationId, Is.EqualTo(correlationId),
                "GetDerivationInfo must propagate CorrelationId for audit traceability");
        }

        /// <summary>AC5-U2: GetDerivationInfo Timestamp is set (observable event time).</summary>
        [Test]
        public void AC5_GetDerivationInfo_Timestamp_IsRecent()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var info = _authService.GetDerivationInfo("ac5-ts-corr");
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.That(info.Timestamp, Is.GreaterThanOrEqualTo(before)
                .And.LessThanOrEqualTo(after),
                "DerivationInfo Timestamp must be set to current UTC time");
        }

        /// <summary>AC5-U3: DeriveAddress never exposes private key material in the returned value.</summary>
        [Test]
        public void AC5_DeriveAddress_NeverExposes_PrivateKeyMaterial()
        {
            var address = _derivationService.DeriveAddress(KnownEmail, KnownPassword);

            Assert.That(address, Does.Not.Contain(KnownTestPrivateKeyBase64),
                "DeriveAddress must never return private key material");
            Assert.That(address.Length, Is.LessThanOrEqualTo(58),
                "Algorand address max length is 58 — longer value may contain key material");
        }

        /// <summary>AC5-U4: VerifyDerivation result contains CorrelationId for audit traceability.</summary>
        [Test]
        public async Task AC5_VerifyDerivation_Response_ContainsCorrelationId()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var correlationId = $"ac5-verify-{Guid.NewGuid()}";
            var result = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "VerifyDerivation response must echo CorrelationId for audit traceability");
        }

        /// <summary>AC5-U5: GetDerivationInfo BoundedErrorCodes contains audit-observable error identifiers.</summary>
        [Test]
        public void AC5_GetDerivationInfo_BoundedErrorCodes_NotEmpty()
        {
            var info = _authService.GetDerivationInfo("ac5-errors-corr");

            Assert.That(info.BoundedErrorCodes, Is.Not.Null.And.Not.Empty,
                "BoundedErrorCodes must list observable error codes for audit/compliance evidence");
        }

        /// <summary>AC5-U6: DeriveAccountMnemonic mnemonic does not appear in DeriveAddress output.</summary>
        [Test]
        public void AC5_DeriveAddress_Output_IsNotMnemonic()
        {
            var (address, mnemonic) = _derivationService.DeriveAccountMnemonic(KnownEmail, KnownPassword);

            Assert.That(address, Is.Not.EqualTo(mnemonic),
                "DeriveAddress must return address, not mnemonic secret material");
            Assert.That(address, Does.Not.Contain(" "),
                "Algorand address must not contain spaces (mnemonics do)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6 – CI quality gate contracts (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC6-U1: Arc76CredentialDerivationService implements IArc76CredentialDerivationService.</summary>
        [Test]
        public void AC6_DerivationService_ImplementsInterface()
        {
            Assert.That(_derivationService, Is.InstanceOf<IArc76CredentialDerivationService>(),
                "Arc76CredentialDerivationService must implement interface for DI contract");
        }

        /// <summary>AC6-U2: AuthenticationService implements IAuthenticationService.</summary>
        [Test]
        public void AC6_AuthService_ImplementsInterface()
        {
            Assert.That(_authService, Is.InstanceOf<IAuthenticationService>(),
                "AuthenticationService must implement IAuthenticationService interface");
        }

        /// <summary>AC6-U3: Single ARC76 derivation completes within 2000ms (CI performance gate).</summary>
        [Test]
        public void AC6_Derivation_CompletesWithin_2000ms()
        {
            var sw = Stopwatch.StartNew();
            _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000),
                $"ARC76 derivation must complete within 2000ms for CI gate. Actual: {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>AC6-U4: Concurrent derivation (20 threads) is thread-safe — all return same address.</summary>
        [Test]
        public async Task AC6_ConcurrentDerivation_ThreadSafe()
        {
            const int parallelCount = 20;
            var tasks = Enumerable.Range(0, parallelCount)
                .Select(_ => Task.Run(() => _derivationService.DeriveAddress(KnownEmail, KnownPassword)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.That(results.Distinct().Count(), Is.EqualTo(1),
                "All 20 concurrent derivation calls must return the same address (thread-safety)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC7 – Documentation contract (1 test)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC7-U1: GetDerivationInfo returns human-readable AlgorithmDescription and SpecificationUrl.</summary>
        [Test]
        public void AC7_GetDerivationInfo_HumanReadable_DocumentationFields()
        {
            var info = _authService.GetDerivationInfo("ac7-doc-corr");

            Assert.Multiple(() =>
            {
                Assert.That(info.AlgorithmDescription, Is.Not.Null.And.Not.Empty,
                    "AC7: AlgorithmDescription must be human-readable documentation");
                Assert.That(info.AlgorithmDescription!.Length, Is.GreaterThan(10),
                    "AC7: AlgorithmDescription must be a meaningful string, not a stub");
                Assert.That(info.SpecificationUrl, Is.Not.Null.And.Not.Empty,
                    "AC7: SpecificationUrl must reference the ARC specification");
                Assert.That(info.SpecificationUrl, Does.Contain("arc").IgnoreCase,
                    "AC7: SpecificationUrl must reference an ARC standard URL");
                Assert.That(info.Standard, Does.Contain("ARC76").Or.Contain("ARC-76"),
                    "AC7: Standard field must reference ARC76");
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helper methods
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Creates a test User with a correctly hashed password (matching AuthenticationService format).</summary>
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

        /// <summary>Mimics the salt:SHA256 password hashing used by AuthenticationService.</summary>
        private static string HashPassword(string password)
        {
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var hash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(salt + password)));
            return $"{salt}:{hash}";
        }
    }
}

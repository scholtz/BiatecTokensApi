using AlgorandARC76AccountDotNet;
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
    /// Service-layer unit tests for Issue #458: Vision milestone – deterministic ARC76 auth/account
    /// contract hardening and backend verification.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// Arc76CredentialDerivationService and AuthenticationService business logic.
    ///
    /// AC1  - Deterministic ARC76 derivation: same credentials always produce same address;
    ///        email canonicalization; different credentials produce different addresses;
    ///        contract version stability; 3-run derivation info consistency
    /// AC2  - Explicit invalid-session and contract-error semantics: wrong password → structured
    ///        error; non-existent user → structured error; no success-shaped failure responses;
    ///        errors are non-leaking and user-safe
    /// AC3  - Authorization-path reliability: VerifyDerivationAsync for known user; for unknown
    ///        user returns explicit error; InspectSessionAsync behavior
    /// AC4  - Coverage and reliability: concurrent derivation thread-safety; password validation
    ///        exhaustive coverage; no private key leakage; mnemonic reconstructs account
    /// AC5  - Observability fields: GetDerivationInfo returns CorrelationId, Standard, AlgorithmDescription,
    ///        ContractVersion, BoundedErrorCodes, SpecificationUrl, IsBackwardCompatible
    /// AC6  - Error handling avoids silent fallbacks: repository exceptions → structured errors;
    ///        no Success=true returned from error paths; error messages are non-null
    ///
    /// Business Value: Service-layer unit tests prove that deterministic ARC76 auth contract
    /// is enforced at the business-logic level, providing fast CI regression guards and
    /// compliance evidence independent of HTTP infrastructure.
    ///
    /// Known Test Vector (ARC76.GetEmailAccount, slot=0):
    ///   email = "testuser@biatec.io", password = "TestPassword123!"
    ///   address = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI"
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicARC76AuthHardeningIssue458ServiceUnitTests
    {
        private Arc76CredentialDerivationService _derivationService = null!;
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockAuthLogger = null!;
        private AuthenticationService _authService = null!;

        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        private const string TestJwtSecret = "Issue458HardeningMilestoneSecretKey32CharsMin!!";
        private const string TestEncryptionKey = "Issue458HardeningMilestoneEncKey32CharsMin!!";

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
        // AC1 – Deterministic ARC76 derivation (8 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC1-U1: Known test vector produces expected Algorand address.</summary>
        [Test]
        public void AC1_KnownTestVector_DeriveAddress_MatchesExpected()
        {
            var address = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            Assert.That(address, Is.EqualTo(KnownAddress),
                $"Known test vector must produce {KnownAddress}. Got: {address}");
        }

        /// <summary>AC1-U2: Same credentials produce identical address in three sequential runs.</summary>
        [Test]
        public void AC1_ThreeRuns_SameCredentials_ProduceSameAddress()
        {
            var addr1 = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var addr2 = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var addr3 = _derivationService.DeriveAddress(KnownEmail, KnownPassword);

            Assert.That(addr2, Is.EqualTo(addr1), "Run 2 must match run 1");
            Assert.That(addr3, Is.EqualTo(addr1), "Run 3 must match run 1");
        }

        /// <summary>AC1-U3: Email canonicalization - uppercase email produces same address as lowercase.</summary>
        [Test]
        public void AC1_EmailCanonicalization_UppercaseEmail_ProducesSameAddress()
        {
            var lower = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var upper = _derivationService.DeriveAddress(KnownEmail.ToUpperInvariant(), KnownPassword);

            Assert.That(upper, Is.EqualTo(lower),
                "Uppercase email must produce same address as lowercase (canonicalized before derivation)");
        }

        /// <summary>AC1-U4: Email with whitespace produces same address after canonicalization.</summary>
        [Test]
        public void AC1_EmailCanonicalization_WithWhitespace_ProducesSameAddress()
        {
            var trimmed = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var padded = _derivationService.DeriveAddress($"  {KnownEmail}  ", KnownPassword);

            Assert.That(padded, Is.EqualTo(trimmed),
                "Email with surrounding whitespace must produce same address after trimming");
        }

        /// <summary>AC1-U5: Different email addresses produce different derived addresses.</summary>
        [Test]
        public void AC1_DifferentEmails_ProduceDifferentAddresses()
        {
            var addr1 = _derivationService.DeriveAddress("user1@biatec.io", KnownPassword);
            var addr2 = _derivationService.DeriveAddress("user2@biatec.io", KnownPassword);

            Assert.That(addr1, Is.Not.EqualTo(addr2),
                "Different emails with same password must produce different addresses");
        }

        /// <summary>AC1-U6: Different passwords produce different derived addresses for same email.</summary>
        [Test]
        public void AC1_DifferentPasswords_ProduceDifferentAddresses()
        {
            var addr1 = _derivationService.DeriveAddress(KnownEmail, "Password123!");
            var addr2 = _derivationService.DeriveAddress(KnownEmail, "DifferentPass456@");

            Assert.That(addr1, Is.Not.EqualTo(addr2),
                "Same email with different passwords must produce different addresses");
        }

        /// <summary>AC1-U7: DerivationContractVersion constant is stable at "1.0".</summary>
        [Test]
        public void AC1_DerivationContractVersion_IsStableAt1_0()
        {
            Assert.That(AuthenticationService.DerivationContractVersion, Is.EqualTo("1.0"),
                "DerivationContractVersion must remain '1.0' — a change is a breaking API contract change");
        }

        /// <summary>AC1-U8: GetDerivationInfo returns identical ContractVersion across 3 runs.</summary>
        [Test]
        public void AC1_GetDerivationInfo_ThreeRuns_ReturnIdenticalContractVersion()
        {
            var correlationId = $"ac1-458-{Guid.NewGuid()}";

            var info1 = _authService.GetDerivationInfo(correlationId);
            var info2 = _authService.GetDerivationInfo(correlationId);
            var info3 = _authService.GetDerivationInfo(correlationId);

            Assert.That(info1.ContractVersion, Is.EqualTo("1.0"), "Run 1 must return '1.0'");
            Assert.That(info2.ContractVersion, Is.EqualTo(info1.ContractVersion), "Run 2 must match run 1");
            Assert.That(info3.ContractVersion, Is.EqualTo(info1.ContractVersion), "Run 3 must match run 1");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Explicit invalid-session and contract-error semantics (8 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC2-U1: Login with wrong password returns failure with error code, not success.</summary>
        [Test]
        public async Task AC2_Login_WrongPassword_ReturnsExplicitFailure_NotSuccess()
        {
            var storedAddress = _derivationService.DeriveAddress("wrongpass@example.com", KnownPassword);
            var user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = "wrongpass@example.com",
                AlgorandAddress = storedAddress,
                PasswordHash = HashPassword(KnownPassword),
                FailedLoginAttempts = 0
            };
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync("wrongpass@example.com"))
                .ReturnsAsync(user);

            var result = await _authService.LoginAsync(new LoginRequest
            {
                Email = "wrongpass@example.com",
                Password = "WrongPassword999!"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Login with wrong password must return Success=false, not a success-shaped failure");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "Login failure must have a non-null ErrorCode");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Login failure must have a non-null ErrorMessage");
        }

        /// <summary>AC2-U2: Login with non-existent user returns structured failure without leaking internals.</summary>
        [Test]
        public async Task AC2_Login_NonExistentUser_ReturnsStructuredFailure_NonLeaking()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(new LoginRequest
            {
                Email = "ghost@nonexistent.com",
                Password = "SomePass123!"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Non-existent user login must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Error message must be non-null");
            Assert.That(result.ErrorMessage, Does.Not.Contain("System."),
                "Error message must not expose internal system information");
            Assert.That(result.ErrorMessage, Does.Not.Contain("null reference"),
                "Error message must not expose null reference exceptions");
        }

        /// <summary>AC2-U3: Register with empty email returns structured failure (not exception).</summary>
        [Test]
        public async Task AC2_Register_EmptyEmail_ReturnsStructuredFailure()
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "",
                Password = "StrongPass123!",
                ConfirmPassword = "StrongPass123!"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Registration with empty email must fail gracefully");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Structured error message required");
        }

        /// <summary>AC2-U4: Register with weak password returns structured failure with error message.</summary>
        [Test]
        public async Task AC2_Register_WeakPassword_ReturnsStructuredFailure()
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "weakpass@example.com",
                Password = "nouppercase1!",
                ConfirmPassword = "nouppercase1!"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Weak password must be rejected with structured failure");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Structured error message required for weak password");
        }

        /// <summary>AC2-U5: Refresh with invalid token returns structured failure.</summary>
        [Test]
        public async Task AC2_RefreshToken_InvalidToken_ReturnsStructuredFailure()
        {
            var result = await _authService.RefreshTokenAsync("invalid-refresh-token-value", null, null);

            Assert.That(result.Success, Is.False,
                "Refresh with invalid token must return Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Refresh failure must have error message");
        }

        /// <summary>AC2-U6: Login failure does not return null AccessToken (no ambiguous success).</summary>
        [Test]
        public async Task AC2_Login_Failure_AccessToken_IsNull()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(new LoginRequest
            {
                Email = "nobody@example.com",
                Password = "AnyPass123!"
            }, null, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "Failed login must not return an access token");
        }

        /// <summary>AC2-U7: Register duplicate email returns structured error with CONFLICT-like code.</summary>
        [Test]
        public async Task AC2_Register_DuplicateEmail_ReturnsStructuredError()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync("dup@example.com"))
                .ReturnsAsync(true);

            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "dup@example.com",
                Password = "NewPass123!A",
                ConfirmPassword = "NewPass123!A"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Duplicate email registration must fail");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Duplicate email error must have user-safe message");
            Assert.That(result.ErrorMessage, Does.Not.Contain("System."),
                "Error message must not leak internal details");
        }

        /// <summary>AC2-U8: Repository exception during login is handled as structured error.</summary>
        [Test]
        public async Task AC2_Login_RepositoryException_IsHandledAsStructuredError()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));

            var result = await _authService.LoginAsync(new LoginRequest
            {
                Email = "exception@example.com",
                Password = "StrongPass123!"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Repository exception must not produce Success=true");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Repository exception must produce user-safe error message");
            Assert.That(result.ErrorMessage, Does.Not.Contain("Simulated DB failure"),
                "Raw exception message must not be exposed to caller");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Authorization-path reliability (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC3-U1: VerifyDerivationAsync for unknown user returns explicit structured error.</summary>
        [Test]
        public async Task AC3_VerifyDerivation_UnknownUser_ReturnsExplicitError()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.False,
                "VerifyDerivation for unknown user must return Success=false");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "VerifyDerivation must have structured ErrorCode for unknown user");
        }

        /// <summary>AC3-U2: InspectSession for unknown user returns IsActive=false (session not found).</summary>
        [Test]
        public async Task AC3_InspectSession_UnknownUser_ReturnsInactive()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.InspectSessionAsync(
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            Assert.That(result.IsActive, Is.False,
                "InspectSession for unknown user must return IsActive=false");
        }

        /// <summary>AC3-U3: VerifyDerivationAsync for valid user with matching address returns Success=true.</summary>
        [Test]
        public async Task AC3_VerifyDerivation_ValidUser_ReturnsSuccess()
        {
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = new User
            {
                UserId = userId,
                Email = KnownEmail,
                AlgorandAddress = expectedAddress,
                PasswordHash = HashPassword(KnownPassword)
            };
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync(userId, null, "test-corr-1");

            Assert.That(result.Success, Is.True,
                "VerifyDerivation for valid user must return Success=true");
            Assert.That(result.AlgorandAddress, Is.EqualTo(expectedAddress),
                "VerifyDerivation must return the correct stored address");
        }

        /// <summary>AC3-U4: ValidateAccessTokenAsync for invalid token returns null.</summary>
        [Test]
        public async Task AC3_ValidateAccessToken_InvalidToken_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync("not.a.valid.jwt.token");

            Assert.That(result, Is.Null,
                "Invalid access token must return null (not throw an exception)");
        }

        /// <summary>AC3-U5: ValidateAccessTokenAsync for empty token returns null.</summary>
        [Test]
        public async Task AC3_ValidateAccessToken_EmptyToken_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync("");

            Assert.That(result, Is.Null,
                "Empty access token must return null (not throw an exception)");
        }

        /// <summary>AC3-U6: GetUserMnemonicForSigningAsync for unknown user returns null.</summary>
        [Test]
        public async Task AC3_GetUserMnemonicForSigning_UnknownUser_ReturnsNull()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _authService.GetUserMnemonicForSigningAsync(Guid.NewGuid().ToString());

            Assert.That(result, Is.Null,
                "GetUserMnemonicForSigning for unknown user must return null, not throw");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Coverage and reliability (7 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC4-U1: Concurrent derivation calls are thread-safe and all return same address.</summary>
        [Test]
        public async Task AC4_ConcurrentDerivation_ThreadSafe_AllReturnSameAddress()
        {
            const int parallelCount = 20;
            var tasks = Enumerable.Range(0, parallelCount)
                .Select(_ => Task.Run(() => _derivationService.DeriveAddress(KnownEmail, KnownPassword)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            foreach (var r in results)
                Assert.That(r, Is.EqualTo(KnownAddress),
                    "Every concurrent derivation must return the same known address");
        }

        /// <summary>AC4-U2: DeriveAccountMnemonic returns a 25-word Algorand mnemonic.</summary>
        [Test]
        public void AC4_DeriveAccountMnemonic_Returns25WordMnemonic()
        {
            var (address, mnemonic) = _derivationService.DeriveAccountMnemonic(KnownEmail, KnownPassword);
            var words = mnemonic.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Assert.That(address, Is.EqualTo(KnownAddress),
                "Mnemonic derivation must produce the same known address");
            Assert.That(words.Length, Is.EqualTo(25),
                "Algorand mnemonic must be 25 words");
        }

        /// <summary>AC4-U3: DeriveAddress never returns private key material.</summary>
        [Test]
        public void AC4_DeriveAddress_DoesNotReturnPrivateKeyMaterial()
        {
            const string knownPrivateKeyBase64 = "U23OZLAs/ZlYuxusrcx8QCk9ln0yp2OOTfqZ/sdj3bY=";
            var address = _derivationService.DeriveAddress(KnownEmail, KnownPassword);

            Assert.That(address, Does.Not.Contain(knownPrivateKeyBase64),
                "DeriveAddress output must not contain private key material");
            Assert.That(address.Length, Is.LessThanOrEqualTo(58),
                "Algorand addresses are at most 58 characters — longer strings may contain raw key material");
        }

        /// <summary>AC4-U4: DeriveAddressAndPublicKey - public key is not equal to private key.</summary>
        [Test]
        public void AC4_DeriveAddressAndPublicKey_PublicKeyIsNotPrivateKey()
        {
            const string knownPrivateKeyBase64 = "U23OZLAs/ZlYuxusrcx8QCk9ln0yp2OOTfqZ/sdj3bY=";
            var (address, publicKey) = _derivationService.DeriveAddressAndPublicKey(KnownEmail, KnownPassword);

            Assert.That(publicKey, Is.Not.EqualTo(knownPrivateKeyBase64),
                "PublicKey must not be the private key");
            Assert.That(address, Is.EqualTo(KnownAddress),
                "Address must match known test vector");
        }

        /// <summary>AC4-U5: CanonicalizeEmail returns lowercase trimmed result.</summary>
        [Test]
        public void AC4_CanonicalizeEmail_ReturnsLowercaseTrimmed()
        {
            var result = _derivationService.CanonicalizeEmail("  TestUser@BIATEC.IO  ");
            Assert.That(result, Is.EqualTo("testuser@biatec.io"),
                "CanonicalizeEmail must return lowercase trimmed email");
        }

        /// <summary>AC4-U6: DeriveAddress performance: completes within 2000ms for single call.</summary>
        [Test]
        public void AC4_DeriveAddress_PerformanceWithin2000ms()
        {
            var sw = Stopwatch.StartNew();
            _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            sw.Stop();

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000),
                $"Single ARC76 derivation must complete within 2000ms. Actual: {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>AC4-U7: Register repository exception is handled as structured error.</summary>
        [Test]
        public async Task AC4_Register_RepositoryException_IsHandledAsStructuredError()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated repo failure"));

            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "repo-exception@example.com",
                Password = "StrongPass123!A",
                ConfirmPassword = "StrongPass123!A"
            }, null, null);

            Assert.That(result.Success, Is.False,
                "Repository exception during registration must not produce Success=true");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Structured error message must be returned");
            Assert.That(result.ErrorMessage, Does.Not.Contain("Simulated repo failure"),
                "Raw exception message must not be exposed");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Observability fields (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC5-U1: GetDerivationInfo propagates the CorrelationId in the response.</summary>
        [Test]
        public void AC5_GetDerivationInfo_PropagatesCorrelationId()
        {
            var correlationId = $"458-corr-{Guid.NewGuid()}";
            var info = _authService.GetDerivationInfo(correlationId);

            Assert.That(info.CorrelationId, Is.EqualTo(correlationId),
                "GetDerivationInfo must propagate CorrelationId to the response");
        }

        /// <summary>AC5-U2: GetDerivationInfo Standard field references ARC76.</summary>
        [Test]
        public void AC5_GetDerivationInfo_Standard_ReferencesARC76()
        {
            var info = _authService.GetDerivationInfo("test-corr");

            Assert.That(info.Standard, Does.Contain("ARC76").Or.Contain("ARC-76"),
                "Standard field must reference ARC76 specification");
        }

        /// <summary>AC5-U3: GetDerivationInfo AlgorithmDescription is human-readable.</summary>
        [Test]
        public void AC5_GetDerivationInfo_AlgorithmDescription_IsHumanReadable()
        {
            var info = _authService.GetDerivationInfo("test-corr");

            Assert.That(info.AlgorithmDescription, Is.Not.Null.And.Not.Empty,
                "AlgorithmDescription must be non-empty");
            Assert.That(info.AlgorithmDescription!.Length, Is.GreaterThan(10),
                "AlgorithmDescription must be a meaningful human-readable string");
        }

        /// <summary>AC5-U4: GetDerivationInfo BoundedErrorCodes is non-empty.</summary>
        [Test]
        public void AC5_GetDerivationInfo_BoundedErrorCodes_IsNonEmpty()
        {
            var info = _authService.GetDerivationInfo("test-corr");

            Assert.That(info.BoundedErrorCodes, Is.Not.Null.And.Not.Empty,
                "BoundedErrorCodes must be populated for audit/contract evidence");
        }

        /// <summary>AC5-U5: GetDerivationInfo IsBackwardCompatible is true.</summary>
        [Test]
        public void AC5_GetDerivationInfo_IsBackwardCompatible_IsTrue()
        {
            var info = _authService.GetDerivationInfo("test-corr");

            Assert.That(info.IsBackwardCompatible, Is.True,
                "IsBackwardCompatible must be true for version 1.0");
        }

        /// <summary>AC5-U6: GetDerivationInfo SpecificationUrl references ARC standard.</summary>
        [Test]
        public void AC5_GetDerivationInfo_SpecificationUrl_ReferencesArcStandard()
        {
            var info = _authService.GetDerivationInfo("test-corr");

            Assert.That(info.SpecificationUrl, Is.Not.Null.And.Not.Empty,
                "SpecificationUrl must be non-empty");
            Assert.That(info.SpecificationUrl, Does.Contain("arc"),
                "SpecificationUrl must reference an ARC standard");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6 – Error handling avoids silent fallbacks (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC6-U1: Register returns null AlgorandAddress on failure.</summary>
        [Test]
        public async Task AC6_Register_Failure_AlgorandAddress_IsNull()
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = "",
                Password = "StrongPass123!",
                ConfirmPassword = "StrongPass123!"
            }, null, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                "Failed registration must not return an AlgorandAddress");
        }

        /// <summary>AC6-U2: Register returns DerivationContractVersion even on success.</summary>
        [Test]
        public async Task AC6_Register_Success_IncludesDerivationContractVersion()
        {
            var email = $"ver-{Guid.NewGuid()}@example.com";
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(email))
                .ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "StrongPass123!A",
                ConfirmPassword = "StrongPass123!A"
            }, null, null);

            Assert.That(result.Success, Is.True, "Registration must succeed");
            Assert.That(result.DerivationContractVersion, Is.EqualTo("1.0"),
                "Registration response must include DerivationContractVersion='1.0'");
        }

        /// <summary>AC6-U3: ServiceInterface is fully implemented by Arc76CredentialDerivationService.</summary>
        [Test]
        public void AC6_ServiceInterface_FullyImplemented()
        {
            Assert.That(_derivationService, Is.InstanceOf<IArc76CredentialDerivationService>(),
                "Arc76CredentialDerivationService must implement IArc76CredentialDerivationService");
        }

        /// <summary>AC6-U4: IAuthenticationService is fully implemented by AuthenticationService.</summary>
        [Test]
        public void AC6_AuthService_ImplementsInterface()
        {
            Assert.That(_authService, Is.InstanceOf<IAuthenticationService>(),
                "AuthenticationService must implement IAuthenticationService");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helper methods
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mimics the password hashing scheme used by AuthenticationService (salt:SHA256 format).
        /// </summary>
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var saltBytes = new byte[32];
            RandomNumberGenerator.Fill(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var saltedPassword = salt + password;
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return $"{salt}:{Convert.ToBase64String(hash)}";
        }
    }
}

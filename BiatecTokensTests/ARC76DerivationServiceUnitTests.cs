using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Pure service-layer unit tests for the three new ARC76 derivation verification methods
    /// introduced in Issue #407:
    ///   - <see cref="AuthenticationService.VerifyDerivationAsync"/>
    ///   - <see cref="AuthenticationService.GetDerivationInfo"/>
    ///   - <see cref="AuthenticationService.InspectSessionAsync"/>
    ///
    /// These tests use only mocked dependencies (no HTTP, no WebApplicationFactory) and cover
    /// every code branch including happy paths, error paths, degraded dependency (exception)
    /// paths, and edge cases. They satisfy the PO requirement for TDD-quality unit coverage
    /// of all behavior-changing service methods.
    ///
    /// Coverage:
    /// - VerifyDerivationAsync: 8 branch variants
    /// - GetDerivationInfo: 3 field-stability variants
    /// - InspectSessionAsync: 5 branch variants
    /// - Degraded-path (exception handling): 3 variants
    /// - Determinism (equivalent inputs → equivalent outputs): 3 variants
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76DerivationServiceUnitTests
    {
        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockLogger = null!;
        private AuthenticationService _authService = null!;

        private const string TestJwtSecret = "ARC76DerivationUnitTestSecretKeyHs256MinLength32!";
        private const string TestEncryptionKey = "ARC76DerivationUnitTestEncryptionKey32CharsMin!!";

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
                _mockLogger.Object,
                Options.Create(jwtConfig),
                keyProviderFactory);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // VerifyDerivationAsync — Happy paths
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When the user exists and no email override is supplied, the method must return
        /// Success=true with IsConsistent=true and a populated DeterminismProof.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_UserExists_NoEmailOverride_ReturnsSuccess()
        {
            var user = MakeUser("verify1@example.com", "ABCDEFGHIJKLMNOP");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-1")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync("uid-1", null, "corr-1");

            Assert.That(result.Success, Is.True, "Must succeed when user exists");
            Assert.That(result.IsConsistent, Is.True, "IsConsistent must be true on success");
            Assert.That(result.AlgorandAddress, Is.EqualTo("ABCDEFGHIJKLMNOP"),
                "AlgorandAddress must match the stored user address");
            Assert.That(result.DerivationContractVersion,
                Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "Contract version must be the static constant");
            Assert.That(result.DerivationAlgorithm, Is.EqualTo("ARC76/BIP39"),
                "Algorithm label must be ARC76/BIP39");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-1"),
                "CorrelationId must be threaded through");
        }

        /// <summary>
        /// When the email override matches the user's stored (canonical) email, verification
        /// must succeed.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_EmailMatchesUser_ReturnsSuccess()
        {
            var user = MakeUser("match@example.com", "ADDRESS123");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-2")).ReturnsAsync(user);

            // Supply the same email as stored (case insensitive)
            var result = await _authService.VerifyDerivationAsync("uid-2", "MATCH@EXAMPLE.COM", "corr-2");

            Assert.That(result.Success, Is.True, "Email match (case-insensitive) must succeed");
            Assert.That(result.IsConsistent, Is.True);
        }

        /// <summary>
        /// DeterminismProof must be populated correctly with canonical email,
        /// standard = "ARC76", and AddressFingerprint = first 8 chars of address.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_DeterminismProof_FieldsPopulatedCorrectly()
        {
            var user = MakeUser("proof@example.com", "ABCDEFGHIJKLMNOP");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-3")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync("uid-3", null, "corr-3");

            Assert.That(result.DeterminismProof, Is.Not.Null, "DeterminismProof must not be null");
            Assert.That(result.DeterminismProof!.CanonicalEmail, Is.EqualTo("proof@example.com"),
                "CanonicalEmail must match stored email");
            Assert.That(result.DeterminismProof.Standard, Is.EqualTo("ARC76"));
            Assert.That(result.DeterminismProof.DerivationPath, Is.EqualTo("BIP39/Algorand"));
            Assert.That(result.DeterminismProof.AddressFingerprint, Is.EqualTo("ABCDEFGH"),
                "Fingerprint must be first 8 chars of AlgorandAddress");
            Assert.That(result.DeterminismProof.ContractVersion,
                Is.EqualTo(AuthenticationService.DerivationContractVersion));
        }

        /// <summary>
        /// When the AlgorandAddress is shorter than 8 characters, the fingerprint must be
        /// the whole address (no IndexOutOfRange).
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_ShortAddress_FingerprintIsWholeAddress()
        {
            var user = MakeUser("short@example.com", "AB");  // only 2 chars
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-4")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync("uid-4", null, "corr-4");

            Assert.That(result.Success, Is.True);
            Assert.That(result.DeterminismProof!.AddressFingerprint, Is.EqualTo("AB"),
                "Short address fingerprint must be the whole address, not throw");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // VerifyDerivationAsync — Error paths
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When the user is not found, must return Success=false with ErrorCode=NOT_FOUND
        /// and a non-empty RemediationHint.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_UserNotFound_ReturnsNotFound()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("missing")).ReturnsAsync((User?)null);

            var result = await _authService.VerifyDerivationAsync("missing", null, "corr-5");

            Assert.That(result.Success, Is.False, "Must fail when user not found");
            Assert.That(result.IsConsistent, Is.False, "IsConsistent must be false on failure");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NOT_FOUND),
                "ErrorCode must be NOT_FOUND");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "RemediationHint must be present for actionable error handling");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-5"));
        }

        /// <summary>
        /// When the supplied email does NOT match the user's stored email, must return
        /// Success=false with ErrorCode=FORBIDDEN.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_EmailMismatch_ReturnsForbidden()
        {
            var user = MakeUser("owner@example.com", "SOMEADDRESS");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-6")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync(
                "uid-6", "attacker@example.com", "corr-6");

            Assert.That(result.Success, Is.False, "Email mismatch must fail");
            Assert.That(result.IsConsistent, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.FORBIDDEN),
                "ErrorCode must be FORBIDDEN for cross-user verification attempt");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "RemediationHint must explain the restriction");
        }

        /// <summary>
        /// When the repository throws an unexpected exception, must return Success=false with
        /// ErrorCode=INTERNAL_SERVER_ERROR. No exception must propagate to the caller.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_RepositoryThrows_ReturnsInternalError_NoThrow()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated database failure"));

            Assert.DoesNotThrowAsync(
                async () => await _authService.VerifyDerivationAsync("uid-7", null, "corr-7"),
                "Exceptions from repository must not propagate to callers");

            var result = await _authService.VerifyDerivationAsync("uid-7", null, "corr-7");

            Assert.That(result.Success, Is.False, "Must return failure on repository exception");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INTERNAL_SERVER_ERROR),
                "ErrorCode must be INTERNAL_SERVER_ERROR");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "RemediationHint must guide the caller on recovery");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-7"),
                "CorrelationId must survive exception path for tracing");
        }

        /// <summary>
        /// Email mismatch check is case-insensitive: owner@example.com vs OWNER@EXAMPLE.COM
        /// must NOT trigger a FORBIDDEN error (they are the same canonical email).
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_EmailMatchCaseInsensitive_DoesNotTriggerForbidden()
        {
            var user = MakeUser("owner@example.com", "SOMEADDRESS");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-8")).ReturnsAsync(user);

            // Send email in ALL CAPS — same canonical email after lowercasing
            var result = await _authService.VerifyDerivationAsync(
                "uid-8", "OWNER@EXAMPLE.COM", "corr-8");

            Assert.That(result.Success, Is.True,
                "Case-insensitive match must not trigger FORBIDDEN");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GetDerivationInfo — Field stability and taxonomy completeness
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GetDerivationInfo must return the static contract metadata with all required fields.
        /// </summary>
        [Test]
        public void GetDerivationInfo_ReturnsAllRequiredFields()
        {
            var result = _authService.GetDerivationInfo("corr-info");

            Assert.That(result.ContractVersion,
                Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "ContractVersion must match the static constant");
            Assert.That(result.Standard, Is.EqualTo("ARC76"), "Standard must be ARC76");
            Assert.That(result.AlgorithmDescription, Is.Not.Null.And.Not.Empty,
                "AlgorithmDescription must be present");
            Assert.That(result.BoundedErrorCodes, Is.Not.Null,
                "BoundedErrorCodes must not be null");
            Assert.That(result.BoundedErrorCodes.Count, Is.GreaterThan(0),
                "At least one error code must be listed");
            Assert.That(result.IsBackwardCompatible, Is.True,
                "isBackwardCompatible must be true for current contract version");
            Assert.That(result.SpecificationUrl, Is.Not.Null.And.Not.Empty,
                "SpecificationUrl must be present");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-info"),
                "CorrelationId must be threaded through");
        }

        /// <summary>
        /// GetDerivationInfo must include critical bounded error codes that consumers
        /// depend on for machine-readable error handling.
        /// </summary>
        [Test]
        public void GetDerivationInfo_BoundedErrorCodes_IncludeCriticalCodes()
        {
            var result = _authService.GetDerivationInfo("corr-codes");

            Assert.That(result.BoundedErrorCodes, Does.Contain(ErrorCodes.NOT_FOUND),
                "NOT_FOUND must be in bounded error taxonomy");
            Assert.That(result.BoundedErrorCodes, Does.Contain(ErrorCodes.FORBIDDEN),
                "FORBIDDEN must be in bounded error taxonomy");
            Assert.That(result.BoundedErrorCodes, Does.Contain(ErrorCodes.UNAUTHORIZED),
                "UNAUTHORIZED must be in bounded error taxonomy");
            Assert.That(result.BoundedErrorCodes, Does.Contain(ErrorCodes.INTERNAL_SERVER_ERROR),
                "INTERNAL_SERVER_ERROR must be in bounded error taxonomy");
        }

        /// <summary>
        /// GetDerivationInfo must be deterministic — two calls with different correlation IDs
        /// return identical content except for the correlation ID.
        /// </summary>
        [Test]
        public void GetDerivationInfo_IsDeterministic_AcrossMultipleCalls()
        {
            var r1 = _authService.GetDerivationInfo("corr-A");
            var r2 = _authService.GetDerivationInfo("corr-B");
            var r3 = _authService.GetDerivationInfo("corr-C");

            Assert.That(r1.ContractVersion, Is.EqualTo(r2.ContractVersion),
                "ContractVersion must be identical across calls");
            Assert.That(r1.ContractVersion, Is.EqualTo(r3.ContractVersion));
            Assert.That(r1.Standard, Is.EqualTo(r2.Standard));
            Assert.That(r1.AlgorithmDescription, Is.EqualTo(r2.AlgorithmDescription));
            Assert.That(r1.IsBackwardCompatible, Is.EqualTo(r2.IsBackwardCompatible));

            // CorrelationId differs by design
            Assert.That(r1.CorrelationId, Is.EqualTo("corr-A"));
            Assert.That(r2.CorrelationId, Is.EqualTo("corr-B"));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // InspectSessionAsync — Happy paths
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When user is found and active, InspectSessionAsync must return IsActive=true
        /// with all identity fields populated.
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_UserFoundAndActive_ReturnsFullSession()
        {
            var user = MakeUser("session@example.com", "SESADDR123");
            user.IsActive = true;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-s1")).ReturnsAsync(user);

            var result = await _authService.InspectSessionAsync("uid-s1", "corr-s1");

            Assert.That(result.IsActive, Is.True, "Active user must return IsActive=true");
            Assert.That(result.UserId, Is.EqualTo(user.UserId), "UserId must match");
            Assert.That(result.Email, Is.EqualTo("session@example.com"), "Email must match");
            Assert.That(result.AlgorandAddress, Is.EqualTo("SESADDR123"),
                "AlgorandAddress must match stored address");
            Assert.That(result.TokenType, Is.EqualTo("Bearer"), "TokenType must be Bearer");
            Assert.That(result.DerivationContractVersion,
                Is.EqualTo(AuthenticationService.DerivationContractVersion),
                "DerivationContractVersion must match static constant");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-s1"),
                "CorrelationId must be threaded through");
        }

        /// <summary>
        /// When user is found but inactive (e.g., disabled account), IsActive must reflect
        /// the stored value (false).
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_UserFoundButInactive_ReturnsIsActiveFalse()
        {
            var user = MakeUser("inactive@example.com", "INACTADDR");
            user.IsActive = false;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-s2")).ReturnsAsync(user);

            var result = await _authService.InspectSessionAsync("uid-s2", "corr-s2");

            Assert.That(result.IsActive, Is.False,
                "Inactive user must return IsActive=false");
            Assert.That(result.UserId, Is.EqualTo(user.UserId),
                "Other identity fields must still be populated for inactive user");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // InspectSessionAsync — Error / degraded paths
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When the user is not found, InspectSessionAsync must return IsActive=false with
        /// the CorrelationId threaded through. No exception must propagate.
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_UserNotFound_ReturnsIsActiveFalse()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("missing-s")).ReturnsAsync((User?)null);

            var result = await _authService.InspectSessionAsync("missing-s", "corr-s3");

            Assert.That(result.IsActive, Is.False,
                "IsActive must be false when user is not found");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-s3"),
                "CorrelationId must survive not-found path");
        }

        /// <summary>
        /// When the repository throws an exception, InspectSessionAsync must return
        /// IsActive=false and NOT propagate the exception.
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_RepositoryThrows_ReturnsIsActiveFalse_NoThrow()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new TimeoutException("Simulated timeout"));

            Assert.DoesNotThrowAsync(
                async () => await _authService.InspectSessionAsync("uid-s4", "corr-s4"),
                "Repository exceptions must not propagate from InspectSessionAsync");

            var result = await _authService.InspectSessionAsync("uid-s4", "corr-s4");

            Assert.That(result.IsActive, Is.False,
                "IsActive must be false when repository throws");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-s4"),
                "CorrelationId must survive exception path");
        }

        /// <summary>
        /// Three consecutive calls for the same user must return identical results
        /// (idempotency / determinism check for the session inspection method).
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_ThreeConsecutiveCalls_ReturnIdenticalAddress()
        {
            var user = MakeUser("idempotent@example.com", "IDEMADDR12345");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-s5")).ReturnsAsync(user);

            var r1 = await _authService.InspectSessionAsync("uid-s5", "corr-r1");
            var r2 = await _authService.InspectSessionAsync("uid-s5", "corr-r2");
            var r3 = await _authService.InspectSessionAsync("uid-s5", "corr-r3");

            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress),
                "AlgorandAddress must be identical across calls (run 1 vs 2)");
            Assert.That(r1.AlgorandAddress, Is.EqualTo(r3.AlgorandAddress),
                "AlgorandAddress must be identical across calls (run 1 vs 3)");
            Assert.That(r1.DerivationContractVersion, Is.EqualTo(r2.DerivationContractVersion));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Cross-cutting: no sensitive data leakage
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VerifyDerivationAsync response must not expose mnemonic or encrypted mnemonic
        /// from the User object, even when the user has those fields populated.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_DoesNotLeakMnemonicOrEncryptedFields()
        {
            var user = MakeUser("leak-test@example.com", "LEAKADDRESS");
            user.EncryptedMnemonic = "SUPER_SECRET_ENCRYPTED_MNEMONIC";
            user.PasswordHash = "SUPER_SECRET_HASH";
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-leak")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync("uid-leak", null, "corr-leak");

            // Serialize the result to JSON and confirm no secret appears
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.That(json, Does.Not.Contain("SUPER_SECRET_ENCRYPTED_MNEMONIC"),
                "EncryptedMnemonic must never appear in the response");
            Assert.That(json, Does.Not.Contain("SUPER_SECRET_HASH"),
                "PasswordHash must never appear in the response");
        }

        /// <summary>
        /// InspectSessionAsync response must not expose mnemonic or encrypted mnemonic
        /// from the User object.
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_DoesNotLeakMnemonicOrEncryptedFields()
        {
            var user = MakeUser("leak-session@example.com", "LEAKSES");
            user.EncryptedMnemonic = "SESSION_SECRET_MNEMONIC";
            user.PasswordHash = "SESSION_SECRET_HASH";
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-lses")).ReturnsAsync(user);

            var result = await _authService.InspectSessionAsync("uid-lses", "corr-lses");

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.That(json, Does.Not.Contain("SESSION_SECRET_MNEMONIC"),
                "EncryptedMnemonic must never appear in session response");
            Assert.That(json, Does.Not.Contain("SESSION_SECRET_HASH"),
                "PasswordHash must never appear in session response");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Determinism: same inputs always produce same outputs
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Three VerifyDerivationAsync calls with the same user return identical
        /// AlgorandAddress and DeterminismProof fingerprint.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_ThreeConsecutiveCalls_ReturnIdenticalProof()
        {
            var user = MakeUser("det@example.com", "DETERMINISTIC01");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-det")).ReturnsAsync(user);

            var r1 = await _authService.VerifyDerivationAsync("uid-det", null, "c1");
            var r2 = await _authService.VerifyDerivationAsync("uid-det", null, "c2");
            var r3 = await _authService.VerifyDerivationAsync("uid-det", null, "c3");

            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress));
            Assert.That(r1.AlgorandAddress, Is.EqualTo(r3.AlgorandAddress));
            Assert.That(r1.DeterminismProof!.AddressFingerprint,
                Is.EqualTo(r2.DeterminismProof!.AddressFingerprint));
            Assert.That(r1.DeterminismProof.AddressFingerprint,
                Is.EqualTo(r3.DeterminismProof!.AddressFingerprint));
            Assert.That(r1.IsConsistent, Is.True);
            Assert.That(r2.IsConsistent, Is.True);
            Assert.That(r3.IsConsistent, Is.True);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static User MakeUser(string email, string algorandAddress) => new User
        {
            UserId = Guid.NewGuid().ToString(),
            Email = email,
            AlgorandAddress = algorandAddress,
            IsActive = true,
            PasswordHash = "hashed",
            EncryptedMnemonic = "encrypted",
            CreatedAt = DateTime.UtcNow
        };
    }
}

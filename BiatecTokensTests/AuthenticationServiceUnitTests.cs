using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="AuthenticationService"/>.
    ///
    /// These tests validate:
    /// - User registration (success, weak password, duplicate email)
    /// - Login (success, invalid credentials, non-existent user, locked account, inactive account)
    /// - Token refresh (success, invalid/revoked/expired token)
    /// - Logout (success)
    /// - JWT access token validation
    /// - Password change
    /// - ARC76 derivation verification
    /// - Derivation info (static contract)
    /// - Session inspection
    /// </summary>
    [TestFixture]
    public class AuthenticationServiceUnitTests
    {
        private Mock<IUserRepository> _userRepoMock = null!;
        private Mock<ILogger<AuthenticationService>> _loggerMock = null!;
        private IOptions<JwtConfig> _jwtOptions = null!;
        private KeyProviderFactory _keyProviderFactory = null!;
        private AuthenticationService _service = null!;

        private const string TestPassword = "SecurePass123!";
        private const string JwtSecretKey = "AuthUnitTestSecretKey32CharsRequired!!";
        private const string JwtIssuer = "BiatecTokensApi";
        private const string JwtAudience = "BiatecTokensUsers";

        [SetUp]
        public void SetUp()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _loggerMock = new Mock<ILogger<AuthenticationService>>();

            _jwtOptions = Options.Create(new JwtConfig
            {
                SecretKey = JwtSecretKey,
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            });

            // Build a minimal service container for KeyProviderFactory
            var services = new ServiceCollection();
            services.Configure<KeyManagementConfig>(c =>
            {
                c.Provider = "Hardcoded";
                c.HardcodedKey = "AuthUnitTestEncryptionKey32CharsReqd!";
            });
            services.AddLogging();
            services.AddSingleton<HardcodedKeyProvider>();
            services.AddSingleton<EnvironmentKeyProvider>();
            services.AddSingleton<KeyProviderFactory>();
            var sp = services.BuildServiceProvider();
            _keyProviderFactory = sp.GetRequiredService<KeyProviderFactory>();

            _service = new AuthenticationService(
                _userRepoMock.Object,
                _loggerMock.Object,
                _jwtOptions,
                _keyProviderFactory);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string UniqueEmail() => $"unit-{Guid.NewGuid():N}@biatec-test.example.com";

        private static User BuildUser(string? email = null, bool isActive = true, bool isLocked = false)
        {
            return new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email ?? "test@biatec-test.example.com",
                // Placeholder hash - does NOT match any real password.
                // Only use this helper in tests where password verification is NOT exercised
                // (e.g., locked-account tests, inactive-account tests, user-lookup tests).
                // For tests that require successful password verification, register via
                // _service.RegisterAsync() first and capture the real User from the mock callback.
                PasswordHash = "salt:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                AlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                EncryptedMnemonic = "encrypted_mnemonic_placeholder",
                IsActive = isActive,
                LockedUntil = isLocked ? DateTime.UtcNow.AddMinutes(30) : null,
                FailedLoginAttempts = 0
            };
        }

        private static RegisterRequest BuildRegisterRequest(string? email = null, string? password = null)
        {
            var pwd = password ?? TestPassword;
            return new RegisterRequest
            {
                Email = email ?? UniqueEmail(),
                Password = pwd,
                ConfirmPassword = pwd,
                FullName = "Test User"
            };
        }

        // ── Registration: success path ────────────────────────────────────────────

        [Test]
        public async Task RegisterAsync_ValidRequest_ReturnsSuccess()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.True, "Valid registration must succeed");
        }

        [Test]
        public async Task RegisterAsync_ValidRequest_ReturnsAlgorandAddress()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Algorand address must be returned on registration");
            Assert.That(result.AlgorandAddress!.Length, Is.EqualTo(58),
                "Algorand address must be 58 characters");
        }

        [Test]
        public async Task RegisterAsync_ValidRequest_ReturnsAccessToken()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty,
                "JWT access token must be returned on registration");
        }

        [Test]
        public async Task RegisterAsync_ValidRequest_ReturnsRefreshToken()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty,
                "Refresh token must be returned on registration");
        }

        [Test]
        public async Task RegisterAsync_ValidRequest_ReturnsUserId()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty, "UserId must be returned");
        }

        [Test]
        public async Task RegisterAsync_ValidRequest_ReturnsDerivationContractVersion()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be returned");
        }

        [Test]
        public async Task RegisterAsync_SameCredentials_AlwaysSameAlgorandAddress()
        {
            var email = "determinism@biatec-test.example.com";

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var req1 = BuildRegisterRequest(email, TestPassword);
            var req2 = BuildRegisterRequest(email, TestPassword);

            var result1 = await _service.RegisterAsync(req1, null, null);
            var result2 = await _service.RegisterAsync(req2, null, null);

            Assert.That(result1.AlgorandAddress, Is.EqualTo(result2.AlgorandAddress),
                "ARC76 derivation must be deterministic: same credentials must always produce same address");
        }

        // ── Registration: failure paths ───────────────────────────────────────────

        [Test]
        public async Task RegisterAsync_WeakPassword_ReturnsFailed()
        {
            var req = BuildRegisterRequest(password: "weak");

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Weak password must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.WEAK_PASSWORD));
        }

        [Test]
        public async Task RegisterAsync_PasswordTooShort_ReturnsFailed()
        {
            var req = BuildRegisterRequest(password: "short");

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Password shorter than 8 characters must be rejected");
        }

        [Test]
        public async Task RegisterAsync_PasswordNoUpperCase_ReturnsFailed()
        {
            var req = BuildRegisterRequest(password: "nouppercase123!");

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Password without uppercase must be rejected");
        }

        [Test]
        public async Task RegisterAsync_PasswordNoDigit_ReturnsFailed()
        {
            var req = BuildRegisterRequest(password: "NoDigitPass!");

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Password without digit must be rejected");
        }

        [Test]
        public async Task RegisterAsync_PasswordNoSpecial_ReturnsFailed()
        {
            var req = BuildRegisterRequest(password: "NoSpecial123");

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Password without special character must be rejected");
        }

        [Test]
        public async Task RegisterAsync_DuplicateEmail_ReturnsFailed()
        {
            var email = UniqueEmail();
            var req = BuildRegisterRequest(email: email);

            _userRepoMock.Setup(r => r.UserExistsAsync(It.Is<string>(e =>
                e == email.ToLowerInvariant()))).ReturnsAsync(true);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Duplicate email must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.USER_ALREADY_EXISTS));
        }

        [Test]
        public async Task RegisterAsync_RepositoryThrows_ReturnsFailed()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .ThrowsAsync(new InvalidOperationException("Repository failure"));

            var result = await _service.RegisterAsync(req, null, null);

            Assert.That(result.Success, Is.False, "Repository failure must return failed response");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INTERNAL_SERVER_ERROR));
        }

        // ── Login: success path ───────────────────────────────────────────────────

        [Test]
        public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
        {
            var email = UniqueEmail();

            // Register first to get a real password hash and encrypted mnemonic
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            User? registeredUser = null;
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);

            var regReq = BuildRegisterRequest(email: email);
            await _service.RegisterAsync(regReq, null, null);

            // Now login using the registered user's data
            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.Is<string>(e =>
                e.ToLowerInvariant() == email.ToLowerInvariant())))
                .ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var loginReq = new LoginRequest { Email = email, Password = TestPassword };
            var result = await _service.LoginAsync(loginReq, null, null);

            Assert.That(result.Success, Is.True, "Login with correct credentials must succeed");
        }

        [Test]
        public async Task LoginAsync_ValidCredentials_ReturnsAlgorandAddress()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var regReq = BuildRegisterRequest(email: email);
            await _service.RegisterAsync(regReq, null, null);

            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var loginReq = new LoginRequest { Email = email, Password = TestPassword };
            var result = await _service.LoginAsync(loginReq, null, null);

            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Algorand address must be returned on successful login");
        }

        [Test]
        public async Task LoginAsync_ValidCredentials_AddressMatchesRegistration()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var regReq = BuildRegisterRequest(email: email);
            var regResult = await _service.RegisterAsync(regReq, null, null);

            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var loginReq = new LoginRequest { Email = email, Password = TestPassword };
            var loginResult = await _service.LoginAsync(loginReq, null, null);

            Assert.That(loginResult.AlgorandAddress, Is.EqualTo(regResult.AlgorandAddress),
                "Address returned on login must match address returned on registration (ARC76 consistency)");
        }

        // ── Login: failure paths ──────────────────────────────────────────────────

        [Test]
        public async Task LoginAsync_NonExistentUser_ReturnsFailed()
        {
            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = "ghost@nobody.example.com", Password = TestPassword },
                null, null);

            Assert.That(result.Success, Is.False, "Non-existent user must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS));
        }

        [Test]
        public async Task LoginAsync_LockedAccount_ReturnsAccountLocked()
        {
            var user = BuildUser(isLocked: true);
            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = user.Email, Password = TestPassword },
                null, null);

            Assert.That(result.Success, Is.False, "Locked account must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.ACCOUNT_LOCKED));
        }

        [Test]
        public async Task LoginAsync_InactiveAccount_ReturnsAccountInactive()
        {
            var user = BuildUser(isActive: false);
            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = user.Email, Password = TestPassword },
                null, null);

            Assert.That(result.Success, Is.False, "Inactive account must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.ACCOUNT_INACTIVE));
        }

        [Test]
        public async Task LoginAsync_InvalidPassword_ReturnsInvalidCredentials()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            await _service.RegisterAsync(BuildRegisterRequest(email), null, null);

            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = email, Password = "WrongPassword999!" },
                null, null);

            Assert.That(result.Success, Is.False, "Invalid password must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_CREDENTIALS));
        }

        [Test]
        public async Task LoginAsync_FailedAttempts_IncrementFailedLoginAttempts()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            await _service.RegisterAsync(BuildRegisterRequest(email), null, null);

            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .Returns(Task.CompletedTask);

            // Make 1 failed attempt
            await _service.LoginAsync(
                new LoginRequest { Email = email, Password = "Wrong1!" }, null, null);

            Assert.That(registeredUser!.FailedLoginAttempts, Is.GreaterThan(0),
                "Failed login attempts must be incremented");
        }

        [Test]
        public async Task LoginAsync_FiveFailedAttempts_AccountGetsLocked()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            await _service.RegisterAsync(BuildRegisterRequest(email), null, null);

            _userRepoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .Returns(Task.CompletedTask);

            // Simulate 5 failed attempts
            for (var i = 0; i < 5; i++)
            {
                await _service.LoginAsync(
                    new LoginRequest { Email = email, Password = "WrongPass!" }, null, null);
            }

            Assert.That(registeredUser!.LockedUntil, Is.Not.Null,
                "Account must be locked after 5 failed login attempts");
            Assert.That(registeredUser.LockedUntil, Is.GreaterThan(DateTime.UtcNow),
                "Lock expiry must be in the future");
        }

        // ── Token refresh ─────────────────────────────────────────────────────────

        [Test]
        public async Task RefreshTokenAsync_InvalidToken_ReturnsFailed()
        {
            _userRepoMock.Setup(r => r.GetRefreshTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);

            var result = await _service.RefreshTokenAsync("invalid-token", null, null);

            Assert.That(result.Success, Is.False, "Invalid refresh token must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REFRESH_TOKEN));
        }

        [Test]
        public async Task RefreshTokenAsync_RevokedToken_ReturnsFailed()
        {
            var token = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "revoked-token",
                UserId = Guid.NewGuid().ToString(),
                IsRevoked = true,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            };
            _userRepoMock.Setup(r => r.GetRefreshTokenAsync(token.Token))
                .ReturnsAsync(token);

            var result = await _service.RefreshTokenAsync(token.Token, null, null);

            Assert.That(result.Success, Is.False, "Revoked refresh token must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.REFRESH_TOKEN_REVOKED));
        }

        [Test]
        public async Task RefreshTokenAsync_ExpiredToken_ReturnsFailed()
        {
            var token = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "expired-token",
                UserId = Guid.NewGuid().ToString(),
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(-1), // Already expired
                CreatedAt = DateTime.UtcNow.AddDays(-31)
            };
            _userRepoMock.Setup(r => r.GetRefreshTokenAsync(token.Token))
                .ReturnsAsync(token);

            var result = await _service.RefreshTokenAsync(token.Token, null, null);

            Assert.That(result.Success, Is.False, "Expired refresh token must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.REFRESH_TOKEN_EXPIRED));
        }

        [Test]
        public async Task RefreshTokenAsync_UserNotFound_ReturnsFailed()
        {
            var token = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "valid-token",
                UserId = Guid.NewGuid().ToString(),
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            };
            _userRepoMock.Setup(r => r.GetRefreshTokenAsync(token.Token))
                .ReturnsAsync(token);
            _userRepoMock.Setup(r => r.GetUserByIdAsync(token.UserId))
                .ReturnsAsync((User?)null);

            var result = await _service.RefreshTokenAsync(token.Token, null, null);

            Assert.That(result.Success, Is.False, "Missing user during refresh must be rejected");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.USER_NOT_FOUND));
        }

        [Test]
        public async Task RefreshTokenAsync_InactiveUser_ReturnsFailed()
        {
            var user = BuildUser(isActive: false);
            var token = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "valid-token",
                UserId = user.UserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            };
            _userRepoMock.Setup(r => r.GetRefreshTokenAsync(token.Token))
                .ReturnsAsync(token);
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId))
                .ReturnsAsync(user);

            var result = await _service.RefreshTokenAsync(token.Token, null, null);

            Assert.That(result.Success, Is.False, "Inactive user during refresh must be rejected");
        }

        [Test]
        public async Task RefreshTokenAsync_ValidToken_ReturnsNewAccessToken()
        {
            var user = BuildUser(isActive: true);
            var token = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "valid-token",
                UserId = user.UserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            };
            _userRepoMock.Setup(r => r.GetRefreshTokenAsync(token.Token)).ReturnsAsync(token);
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);
            _userRepoMock.Setup(r => r.RevokeRefreshTokenAsync(token.Token)).Returns(Task.CompletedTask);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RefreshTokenAsync(token.Token, null, null);

            Assert.That(result.Success, Is.True, "Valid refresh must succeed");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty, "New access token must be returned");
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty, "New refresh token must be returned");
        }

        // ── Logout ────────────────────────────────────────────────────────────────

        [Test]
        public async Task LogoutAsync_ValidUserId_ReturnsSuccess()
        {
            var userId = Guid.NewGuid().ToString();
            _userRepoMock.Setup(r => r.RevokeAllUserRefreshTokensAsync(userId))
                .Returns(Task.CompletedTask);

            var result = await _service.LogoutAsync(userId);

            Assert.That(result.Success, Is.True, "Valid logout must succeed");
        }

        [Test]
        public async Task LogoutAsync_ValidUserId_CallsRevokeAllTokens()
        {
            var userId = Guid.NewGuid().ToString();
            _userRepoMock.Setup(r => r.RevokeAllUserRefreshTokensAsync(userId))
                .Returns(Task.CompletedTask);

            await _service.LogoutAsync(userId);

            _userRepoMock.Verify(r => r.RevokeAllUserRefreshTokensAsync(userId), Times.Once,
                "RevokeAllUserRefreshTokensAsync must be called on logout");
        }

        [Test]
        public async Task LogoutAsync_RepositoryThrows_ReturnsFailed()
        {
            var userId = Guid.NewGuid().ToString();
            _userRepoMock.Setup(r => r.RevokeAllUserRefreshTokensAsync(userId))
                .ThrowsAsync(new InvalidOperationException("Repository failure"));

            var result = await _service.LogoutAsync(userId);

            Assert.That(result.Success, Is.False, "Repository failure during logout must return failed response");
        }

        // ── ValidateAccessTokenAsync ──────────────────────────────────────────────

        [Test]
        public async Task ValidateAccessTokenAsync_InvalidToken_ReturnsNull()
        {
            var result = await _service.ValidateAccessTokenAsync("invalid.token.value");

            Assert.That(result, Is.Null, "Invalid token must return null");
        }

        [Test]
        public async Task ValidateAccessTokenAsync_EmptyToken_ReturnsNull()
        {
            var result = await _service.ValidateAccessTokenAsync("");

            Assert.That(result, Is.Null, "Empty token must return null");
        }

        [Test]
        public async Task ValidateAccessTokenAsync_ValidToken_ReturnsUserId()
        {
            // Create a valid JWT directly using the same secret key and configuration
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(JwtSecretKey);
            var expectedUserId = Guid.NewGuid().ToString();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, expectedUserId),
                    new Claim(ClaimTypes.Email, "test@example.com")
                }),
                Expires = DateTime.UtcNow.AddMinutes(60),
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            var userId = await _service.ValidateAccessTokenAsync(tokenString);

            Assert.That(userId, Is.Not.Null, "Valid JWT must return a user ID");
            Assert.That(userId, Is.EqualTo(expectedUserId),
                "ValidateAccessTokenAsync must extract the correct user ID from the JWT");
        }

        // ── ChangePasswordAsync ───────────────────────────────────────────────────

        [Test]
        public async Task ChangePasswordAsync_UserNotFound_ReturnsFalse()
        {
            _userRepoMock.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _service.ChangePasswordAsync("unknown-user", TestPassword, "NewSecure123!");

            Assert.That(result, Is.False, "Password change for non-existent user must return false");
        }

        [Test]
        public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsFalse()
        {
            var user = BuildUser();
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.ChangePasswordAsync(user.UserId, "WrongPassword!", "NewSecure123!");

            Assert.That(result, Is.False, "Wrong current password must return false");
        }

        [Test]
        public async Task ChangePasswordAsync_WeakNewPassword_ReturnsFalse()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            await _service.RegisterAsync(BuildRegisterRequest(email), null, null);

            _userRepoMock.Setup(r => r.GetUserByIdAsync(registeredUser!.UserId)).ReturnsAsync(registeredUser);

            var result = await _service.ChangePasswordAsync(registeredUser!.UserId, TestPassword, "weak");

            Assert.That(result, Is.False, "Weak new password must be rejected");
        }

        [Test]
        public async Task ChangePasswordAsync_ValidChange_ReturnsTrue()
        {
            var email = UniqueEmail();
            User? registeredUser = null;

            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredUser = u)
                .ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            await _service.RegisterAsync(BuildRegisterRequest(email), null, null);

            _userRepoMock.Setup(r => r.GetUserByIdAsync(registeredUser!.UserId)).ReturnsAsync(registeredUser);
            _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _userRepoMock.Setup(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

            var result = await _service.ChangePasswordAsync(registeredUser!.UserId, TestPassword, "NewSecure456@");

            Assert.That(result, Is.True, "Valid password change must return true");
        }

        // ── VerifyDerivationAsync ─────────────────────────────────────────────────

        [Test]
        public async Task VerifyDerivationAsync_UserNotFound_ReturnsFailed()
        {
            _userRepoMock.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _service.VerifyDerivationAsync("unknown-user", null, Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.False, "Derivation verification for unknown user must fail");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NOT_FOUND));
        }

        [Test]
        public async Task VerifyDerivationAsync_EmailMismatch_ReturnsFailed()
        {
            var user = BuildUser(email: "real@example.com");
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.VerifyDerivationAsync(user.UserId, "different@example.com", Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.False, "Email mismatch must return failed derivation verification");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.FORBIDDEN));
        }

        [Test]
        public async Task VerifyDerivationAsync_ValidUser_ReturnsSuccess()
        {
            var user = BuildUser(email: "verify@example.com");
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.VerifyDerivationAsync(user.UserId, null, Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.True, "Valid derivation verification must succeed");
            Assert.That(result.IsConsistent, Is.True);
        }

        [Test]
        public async Task VerifyDerivationAsync_ValidUser_ReturnsAlgorandAddress()
        {
            var user = BuildUser();
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.VerifyDerivationAsync(user.UserId, null, Guid.NewGuid().ToString());

            Assert.That(result.AlgorandAddress, Is.EqualTo(user.AlgorandAddress),
                "Verified address must match user's stored address");
        }

        [Test]
        public async Task VerifyDerivationAsync_ValidUser_ReturnsDeterminismProof()
        {
            var user = BuildUser();
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.VerifyDerivationAsync(user.UserId, null, Guid.NewGuid().ToString());

            Assert.That(result.DeterminismProof, Is.Not.Null,
                "Determinism proof must be returned on successful verification");
            Assert.That(result.DeterminismProof!.Standard, Is.EqualTo("ARC76"));
        }

        [Test]
        public async Task VerifyDerivationAsync_MatchingEmail_ReturnsSuccess()
        {
            var user = BuildUser(email: "match@example.com");
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.VerifyDerivationAsync(
                user.UserId, "match@example.com", Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.True,
                "Verification with matching email must succeed");
        }

        [Test]
        public async Task VerifyDerivationAsync_CorrelationId_EchoedBack()
        {
            var correlationId = Guid.NewGuid().ToString();
            var user = BuildUser();
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.VerifyDerivationAsync(user.UserId, null, correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "CorrelationId must be echoed back in the response");
        }

        // ── GetDerivationInfo ─────────────────────────────────────────────────────

        [Test]
        public void GetDerivationInfo_ReturnsContractVersion()
        {
            var result = _service.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(result.ContractVersion, Is.EqualTo(AuthenticationService.DerivationContractVersion));
        }

        [Test]
        public void GetDerivationInfo_ReturnsAlgorithmDescription()
        {
            var result = _service.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(result.AlgorithmDescription, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetDerivationInfo_ReturnsStandardARC76()
        {
            var result = _service.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(result.Standard, Is.EqualTo("ARC76"));
        }

        [Test]
        public void GetDerivationInfo_ReturnsBackwardCompatibleTrue()
        {
            var result = _service.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(result.IsBackwardCompatible, Is.True);
        }

        [Test]
        public void GetDerivationInfo_CorrelationIdEchoedBack()
        {
            var correlationId = Guid.NewGuid().ToString();
            var result = _service.GetDerivationInfo(correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public void GetDerivationInfo_ReturnsBoundedErrorCodes()
        {
            var result = _service.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(result.BoundedErrorCodes, Is.Not.Null.And.Not.Empty,
                "Error taxonomy must be provided");
        }

        [Test]
        public void GetDerivationInfo_ReturnsSpecificationUrl()
        {
            var result = _service.GetDerivationInfo(Guid.NewGuid().ToString());

            Assert.That(result.SpecificationUrl, Is.Not.Null.And.Not.Empty,
                "Specification URL must be provided");
        }

        // ── InspectSessionAsync ───────────────────────────────────────────────────

        [Test]
        public async Task InspectSessionAsync_UserNotFound_ReturnsInactive()
        {
            _userRepoMock.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _service.InspectSessionAsync("unknown-user", Guid.NewGuid().ToString());

            Assert.That(result.IsActive, Is.False, "Session for non-existent user must be inactive");
        }

        [Test]
        public async Task InspectSessionAsync_ActiveUser_ReturnsActive()
        {
            var user = BuildUser(isActive: true);
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.InspectSessionAsync(user.UserId, Guid.NewGuid().ToString());

            Assert.That(result.IsActive, Is.True, "Active user must return IsActive=true");
        }

        [Test]
        public async Task InspectSessionAsync_ActiveUser_ReturnsAlgorandAddress()
        {
            var user = BuildUser(isActive: true);
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.InspectSessionAsync(user.UserId, Guid.NewGuid().ToString());

            Assert.That(result.AlgorandAddress, Is.EqualTo(user.AlgorandAddress),
                "Session inspection must return the user's Algorand address");
        }

        [Test]
        public async Task InspectSessionAsync_CorrelationId_EchoedBack()
        {
            var correlationId = Guid.NewGuid().ToString();
            var user = BuildUser();
            _userRepoMock.Setup(r => r.GetUserByIdAsync(user.UserId)).ReturnsAsync(user);

            var result = await _service.InspectSessionAsync(user.UserId, correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId));
        }

        // ── ARC76 determinism: 3-run repeatability via registration ───────────────

        [Test]
        public async Task RegisterAsync_SameEmailPassword_ThreeRuns_IdenticalAlgorandAddress()
        {
            var email = "three-run@biatec-test.example.com";
            var addresses = new List<string?>();

            for (var run = 0; run < 3; run++)
            {
                _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
                _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
                _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

                var req = BuildRegisterRequest(email: email, password: TestPassword);
                var result = await _service.RegisterAsync(req, null, null);
                addresses.Add(result.AlgorandAddress);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "ARC76 must produce identical address across 3 independent registration calls (determinism proof)");
        }

        // ── Schema contract ───────────────────────────────────────────────────────

        [Test]
        public async Task RegisterAsync_Success_SchemaContractNonNullFields()
        {
            var req = BuildRegisterRequest();
            _userRepoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _userRepoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(req, null, null);

            Assert.Multiple(() =>
            {
                Assert.That(result.UserId, Is.Not.Null, "UserId must not be null");
                Assert.That(result.Email, Is.Not.Null, "Email must not be null");
                Assert.That(result.AlgorandAddress, Is.Not.Null, "AlgorandAddress must not be null");
                Assert.That(result.AccessToken, Is.Not.Null, "AccessToken must not be null");
                Assert.That(result.RefreshToken, Is.Not.Null, "RefreshToken must not be null");
                Assert.That(result.ExpiresAt, Is.Not.Null, "ExpiresAt must not be null");
                Assert.That(result.DerivationContractVersion, Is.Not.Null, "DerivationContractVersion must not be null");
            });
        }
    }
}

using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NBitcoin;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests verifying NBitcoin 10.x compatibility and the mnemonic-related code paths
    /// in <see cref="AuthenticationService"/>.
    ///
    /// These tests exist to guarantee that upgrading NBitcoin (e.g. 9.x → 10.x) does not
    /// silently break:
    ///   1. The BIP39 <see cref="Mnemonic"/> / <see cref="Wordlist"/> / <see cref="WordCount"/> API
    ///      used inside <c>AuthenticationService.GenerateMnemonic()</c>.
    ///   2. The AES-256-GCM encrypt/decrypt cycle for the Algorand mnemonic stored at registration.
    ///   3. The full round-trip: register → retrieve encrypted mnemonic → decrypt → valid 25-word
    ///      Algorand phrase.
    ///
    /// Test IDs: NC01–NC20
    /// </summary>
    [TestFixture]
    public class NBitcoinMnemonicCompatibilityTests
    {
        // ── Test constants ────────────────────────────────────────────────────────

        private const string EncryptionPassword = "NBitcoinTestEncryptionKey32CharsReqd!";
        private const string JwtSecretKey = "NBitcoinTestJwtKey32CharsRequired!!";
        private const string TestPassword = "SecurePass123!";

        // ── Helpers: reproduce the AES-256-GCM encrypt/decrypt used by the service ──

        /// <summary>Replicates AuthenticationService.EncryptMnemonic.</summary>
        private static string EncryptMnemonic(string mnemonic, string password)
        {
            var mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            var salt = new byte[32];
            RandomNumberGenerator.Fill(nonce);
            RandomNumberGenerator.Fill(salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var key = pbkdf2.GetBytes(32);
            var ciphertext = new byte[mnemonicBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            aesGcm.Encrypt(nonce, mnemonicBytes, ciphertext, tag);
            var result = new byte[salt.Length + nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(nonce, 0, result, salt.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, salt.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, salt.Length + nonce.Length + tag.Length, ciphertext.Length);
            return Convert.ToBase64String(result);
        }

        /// <summary>Replicates AuthenticationService.DecryptMnemonic.</summary>
        private static string DecryptMnemonic(string encryptedMnemonic, string password)
        {
            var bytes = Convert.FromBase64String(encryptedMnemonic);
            var salt = new byte[32];
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            var ciphertext = new byte[bytes.Length - salt.Length - nonce.Length - tag.Length];
            Buffer.BlockCopy(bytes, 0, salt, 0, salt.Length);
            Buffer.BlockCopy(bytes, salt.Length, nonce, 0, nonce.Length);
            Buffer.BlockCopy(bytes, salt.Length + nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(bytes, salt.Length + nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var key = pbkdf2.GetBytes(32);
            var plaintext = new byte[ciphertext.Length];
            using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }

        // ── Build a minimal AuthenticationService ────────────────────────────────

        private static AuthenticationService BuildService(
            Mock<IUserRepository>? userRepoMock = null,
            string? encryptionKey = null)
        {
            var repo = userRepoMock ?? new Mock<IUserRepository>();
            var logger = new Mock<ILogger<AuthenticationService>>();
            var jwtOptions = Options.Create(new JwtConfig
            {
                SecretKey = JwtSecretKey,
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensUsers",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            });
            var services = new ServiceCollection();
            services.Configure<KeyManagementConfig>(c =>
            {
                c.Provider = "Hardcoded";
                c.HardcodedKey = encryptionKey ?? EncryptionPassword;
            });
            services.AddLogging();
            services.AddSingleton<HardcodedKeyProvider>();
            services.AddSingleton<EnvironmentKeyProvider>();
            services.AddSingleton<KeyProviderFactory>();
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<KeyProviderFactory>();
            return new AuthenticationService(repo.Object, logger.Object, jwtOptions, factory);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // NC01-NC07 — Direct NBitcoin 10.x API compatibility
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>NC01 — 24-word Mnemonic produces exactly 24 words.</summary>
        [Test]
        public void NC01_NBitcoin_Mnemonic24Words_ProducesCorrectWordCount()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour);

            Assert.That(mnemonic.Words.Length, Is.EqualTo(24),
                "NBitcoin TwentyFour wordcount must produce exactly 24 words");
        }

        /// <summary>NC02 — A freshly generated 24-word mnemonic passes BIP39 checksum.</summary>
        [Test]
        public void NC02_NBitcoin_Mnemonic24Words_IsValidChecksum()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour);

            Assert.That(mnemonic.IsValidChecksum, Is.True,
                "Generated 24-word mnemonic must have valid BIP39 checksum");
        }

        /// <summary>NC03 — Two consecutive calls produce different mnemonics (entropy is random).</summary>
        [Test]
        public void NC03_NBitcoin_TwoMnemonics_AreUnique()
        {
            var m1 = new Mnemonic(Wordlist.English, WordCount.TwentyFour);
            var m2 = new Mnemonic(Wordlist.English, WordCount.TwentyFour);

            Assert.That(m1.ToString(), Is.Not.EqualTo(m2.ToString()),
                "Two independently generated mnemonics must not be identical");
        }

        /// <summary>NC04 — A mnemonic string can be reconstructed into a valid Mnemonic object.</summary>
        [Test]
        public void NC04_NBitcoin_MnemonicFromString_ReconstructsSuccessfully()
        {
            var original = new Mnemonic(Wordlist.English, WordCount.TwentyFour);
            var phrase = original.ToString();

            var reconstructed = new Mnemonic(phrase);

            Assert.That(reconstructed.IsValidChecksum, Is.True, "Reconstructed mnemonic must be valid");
            Assert.That(reconstructed.Words, Is.EqualTo(original.Words),
                "Reconstructed mnemonic must have the same words as the original");
        }

        /// <summary>NC05 — 12-word Mnemonic produces exactly 12 words.</summary>
        [Test]
        public void NC05_NBitcoin_Mnemonic12Words_ProducesCorrectWordCount()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);

            Assert.That(mnemonic.Words.Length, Is.EqualTo(12),
                "NBitcoin Twelve wordcount must produce exactly 12 words");
        }

        /// <summary>NC06 — English wordlist contains exactly 2048 words.</summary>
        [Test]
        public void NC06_NBitcoin_EnglishWordlist_HasExpected2048Words()
        {
            Assert.That(Wordlist.English.WordCount, Is.EqualTo(2048),
                "BIP39 English wordlist must contain exactly 2048 words");
        }

        /// <summary>NC07 — All words in a generated mnemonic exist in the English wordlist.</summary>
        [Test]
        public void NC07_NBitcoin_GeneratedWords_ExistInWordlist()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour);

            foreach (var word in mnemonic.Words)
            {
                var found = Wordlist.English.WordExists(word, out int idx);
                Assert.That(found, Is.True,
                    $"Word '{word}' must exist in the BIP39 English wordlist");
                Assert.That(idx, Is.GreaterThanOrEqualTo(0),
                    $"Word '{word}' must have a non-negative index in the BIP39 English wordlist");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // NC08-NC11 — AES-256-GCM encrypt/decrypt round-trip
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>NC08 — Encrypt then Decrypt returns the original plaintext.</summary>
        [Test]
        public void NC08_EncryptDecrypt_Roundtrip_IsLossless()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour);
            var plaintext = mnemonic.ToString();

            var encrypted = EncryptMnemonic(plaintext, EncryptionPassword);
            var decrypted = DecryptMnemonic(encrypted, EncryptionPassword);

            Assert.That(decrypted, Is.EqualTo(plaintext),
                "Decrypting an encrypted mnemonic must return the original plaintext");
        }

        /// <summary>NC09 — Encrypting the same plaintext twice produces different ciphertexts (random IV).</summary>
        [Test]
        public void NC09_EncryptMnemonic_SamePlaintext_ProducesDifferentCiphertext()
        {
            var plaintext = new Mnemonic(Wordlist.English, WordCount.TwentyFour).ToString();

            var enc1 = EncryptMnemonic(plaintext, EncryptionPassword);
            var enc2 = EncryptMnemonic(plaintext, EncryptionPassword);

            Assert.That(enc1, Is.Not.EqualTo(enc2),
                "Different random nonces/salts must produce different ciphertexts for the same plaintext");
        }

        /// <summary>NC10 — Decrypting with the wrong password throws a CryptographicException.</summary>
        [Test]
        public void NC10_DecryptMnemonic_WrongPassword_ThrowsException()
        {
            var plaintext = new Mnemonic(Wordlist.English, WordCount.TwentyFour).ToString();
            var encrypted = EncryptMnemonic(plaintext, EncryptionPassword);

            Assert.That(() => DecryptMnemonic(encrypted, "WrongPassword123!"),
                Throws.InstanceOf<CryptographicException>(),
                "Decrypting with incorrect password must throw CryptographicException or a derived type (e.g. AuthenticationTagMismatchException)");
        }

        /// <summary>NC11 — Encrypted output is valid Base64.</summary>
        [Test]
        public void NC11_EncryptMnemonic_OutputIsValidBase64()
        {
            var plaintext = new Mnemonic(Wordlist.English, WordCount.TwentyFour).ToString();
            var encrypted = EncryptMnemonic(plaintext, EncryptionPassword);

            Assert.DoesNotThrow(() => Convert.FromBase64String(encrypted),
                "EncryptMnemonic output must be valid Base64");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // NC12-NC16 — AuthenticationService registration stores valid mnemonic
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>NC12 — After registration the stored EncryptedMnemonic is not null or empty.</summary>
        [Test]
        public async Task NC12_RegisterAsync_StoredMnemonic_IsNotNullOrEmpty()
        {
            User? capturedUser = null;
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                    .Callback<User>(u => capturedUser = u)
                    .ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc12@biatec-test.example.com",
                Password = TestPassword,
                ConfirmPassword = TestPassword,
                FullName = "NC12 User"
            }, null, null);

            Assert.That(capturedUser, Is.Not.Null, "CreateUserAsync must have been called");
            Assert.That(capturedUser!.EncryptedMnemonic, Is.Not.Null.And.Not.Empty,
                "Stored EncryptedMnemonic must not be null or empty after registration");
        }

        /// <summary>NC13 — The stored EncryptedMnemonic is valid Base64 (AES-GCM format).</summary>
        [Test]
        public async Task NC13_RegisterAsync_StoredMnemonic_IsValidBase64()
        {
            User? capturedUser = null;
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                    .Callback<User>(u => capturedUser = u)
                    .ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc13@biatec-test.example.com",
                Password = TestPassword,
                ConfirmPassword = TestPassword,
                FullName = "NC13 User"
            }, null, null);

            Assert.That(capturedUser, Is.Not.Null);
            Assert.DoesNotThrow(() => Convert.FromBase64String(capturedUser!.EncryptedMnemonic),
                "EncryptedMnemonic must be valid Base64");
        }

        /// <summary>
        /// NC14 — Decrypting the stored mnemonic (using the configured hardcoded key) returns
        /// a valid 25-word Algorand mnemonic (ARC76 derivation produces Algorand-native words).
        /// </summary>
        [Test]
        public async Task NC14_RegisterAsync_DecryptedMnemonic_IsValid25WordAlgorandPhrase()
        {
            User? capturedUser = null;
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                    .Callback<User>(u => capturedUser = u)
                    .ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc14@biatec-test.example.com",
                Password = TestPassword,
                ConfirmPassword = TestPassword,
                FullName = "NC14 User"
            }, null, null);

            Assert.That(capturedUser, Is.Not.Null);
            var plaintext = DecryptMnemonic(capturedUser!.EncryptedMnemonic, EncryptionPassword);

            // Algorand mnemonics are 25 words separated by single spaces
            var words = plaintext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(words.Length, Is.EqualTo(25),
                "Decrypted mnemonic must be a 25-word Algorand phrase (ARC76 derivation)");
        }

        /// <summary>NC15 — Same credentials on two registrations produce the same encrypted address.</summary>
        [Test]
        public async Task NC15_RegisterAsync_SameCredentials_ProduceSameAlgorandAddress()
        {
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            var email = "nc15@biatec-test.example.com";

            var r1 = await svc.RegisterAsync(new RegisterRequest
            {
                Email = email, Password = TestPassword, ConfirmPassword = TestPassword, FullName = "NC15 Run1"
            }, null, null);
            var r2 = await svc.RegisterAsync(new RegisterRequest
            {
                Email = email, Password = TestPassword, ConfirmPassword = TestPassword, FullName = "NC15 Run2"
            }, null, null);

            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress),
                "ARC76 derivation must be deterministic: identical credentials must produce identical Algorand address");
        }

        /// <summary>NC16 — Different credentials produce different Algorand addresses.</summary>
        [Test]
        public async Task NC16_RegisterAsync_DifferentCredentials_ProduceDifferentAddresses()
        {
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);

            var r1 = await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc16-a@biatec-test.example.com", Password = TestPassword, ConfirmPassword = TestPassword
            }, null, null);
            var r2 = await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc16-b@biatec-test.example.com", Password = TestPassword, ConfirmPassword = TestPassword
            }, null, null);

            Assert.That(r1.AlgorandAddress, Is.Not.EqualTo(r2.AlgorandAddress),
                "Different email addresses must produce different Algorand addresses (ARC76 determinism)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // NC17-NC20 — Full round-trip: register → GetUserMnemonicForSigningAsync
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>NC17 — GetUserMnemonicForSigningAsync returns the mnemonic stored at registration.</summary>
        [Test]
        public async Task NC17_GetUserMnemonicForSigningAsync_ReturnsDecryptedMnemonicStoredAtRegistration()
        {
            User? storedUser = null;
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                    .Callback<User>(u => storedUser = u)
                    .ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            var reg = await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc17@biatec-test.example.com",
                Password = TestPassword,
                ConfirmPassword = TestPassword,
                FullName = "NC17 User"
            }, null, null);

            Assert.That(storedUser, Is.Not.Null);

            // Now wire GetUserByIdAsync to return the stored user
            repoMock.Setup(r => r.GetUserByIdAsync(reg.UserId!)).ReturnsAsync(storedUser);

            var mnemonic = await svc.GetUserMnemonicForSigningAsync(reg.UserId!);

            Assert.That(mnemonic, Is.Not.Null.And.Not.Empty,
                "GetUserMnemonicForSigningAsync must return a non-empty mnemonic");
        }

        /// <summary>NC18 — Mnemonic returned by GetUserMnemonicForSigningAsync is a 25-word Algorand phrase.</summary>
        [Test]
        public async Task NC18_GetUserMnemonicForSigningAsync_Returns25WordAlgorandPhrase()
        {
            User? storedUser = null;
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                    .Callback<User>(u => storedUser = u)
                    .ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            var reg = await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc18@biatec-test.example.com",
                Password = TestPassword,
                ConfirmPassword = TestPassword
            }, null, null);

            repoMock.Setup(r => r.GetUserByIdAsync(reg.UserId!)).ReturnsAsync(storedUser);

            var mnemonic = await svc.GetUserMnemonicForSigningAsync(reg.UserId!);
            var words = mnemonic!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Assert.That(words.Length, Is.EqualTo(25),
                "Algorand ARC76 mnemonic must always be 25 words");
        }

        /// <summary>NC19 — Mnemonic round-trip is deterministic: two calls return the same phrase.</summary>
        [Test]
        public async Task NC19_GetUserMnemonicForSigningAsync_TwoCalls_ReturnIdenticalMnemonic()
        {
            User? storedUser = null;
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.UserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                    .Callback<User>(u => storedUser = u)
                    .ReturnsAsync((User u) => u);
            repoMock.Setup(r => r.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

            var svc = BuildService(repoMock);
            var reg = await svc.RegisterAsync(new RegisterRequest
            {
                Email = "nc19@biatec-test.example.com",
                Password = TestPassword,
                ConfirmPassword = TestPassword
            }, null, null);

            repoMock.Setup(r => r.GetUserByIdAsync(reg.UserId!)).ReturnsAsync(storedUser);

            var m1 = await svc.GetUserMnemonicForSigningAsync(reg.UserId!);
            var m2 = await svc.GetUserMnemonicForSigningAsync(reg.UserId!);

            Assert.That(m1, Is.EqualTo(m2),
                "Decrypting the same stored mnemonic twice must produce identical results");
        }

        /// <summary>NC20 — GetUserMnemonicForSigningAsync returns null for a non-existent user.</summary>
        [Test]
        public async Task NC20_GetUserMnemonicForSigningAsync_UnknownUserId_ReturnsNull()
        {
            var repoMock = new Mock<IUserRepository>();
            repoMock.Setup(r => r.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

            var svc = BuildService(repoMock);

            var result = await svc.GetUserMnemonicForSigningAsync("nonexistent-user-id");

            Assert.That(result, Is.Null,
                "GetUserMnemonicForSigningAsync must return null when the user does not exist");
        }
    }
}

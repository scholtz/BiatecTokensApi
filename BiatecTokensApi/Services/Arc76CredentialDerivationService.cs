using AlgorandARC76AccountDotNet;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implements deterministic ARC76 credential-based Algorand account derivation.
    ///
    /// Algorithm:
    /// - Email is canonicalized (lowercase + trim) before derivation
    /// - Uses <see cref="ARC76.GetEmailAccount(string, string, int)"/> which applies PBKDF2-based
    ///   derivation per the ARC-0076 specification
    /// - Slot 0 is always used (one account per credential set)
    /// - Returns a deterministic Algorand Ed25519 account — same email+password always produces the same address
    ///
    /// Security:
    /// - Private key material is never logged
    /// - The 25-word Algorand mnemonic (from ToMnemonic()) encodes the private key and must be
    ///   encrypted before storage
    /// - Public key is safe to return to callers
    ///
    /// Test Vector (documented in docs/arc76.md):
    ///   email = "testuser@biatec.io", password = "TestPassword123!", slot = 0
    ///   → address = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI"
    /// </summary>
    public class Arc76CredentialDerivationService : IArc76CredentialDerivationService
    {
        private readonly ILogger<Arc76CredentialDerivationService> _logger;

        /// <summary>
        /// Slot number used for ARC76 account derivation. Slot 0 is the primary account.
        /// </summary>
        private const int DerivationSlot = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Arc76CredentialDerivationService"/> class.
        /// </summary>
        public Arc76CredentialDerivationService(ILogger<Arc76CredentialDerivationService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public string DeriveAddress(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

            var canonicalEmail = CanonicalizeEmail(email);
            var account = ARC76.GetEmailAccount(canonicalEmail, password, DerivationSlot);
            var address = account.Address.ToString();

            _logger.LogInformation("ARC76 address derived. CanonicalEmail={CanonicalEmail}", canonicalEmail);
            return address;
        }

        /// <inheritdoc/>
        public (string Address, string PublicKeyBase64) DeriveAddressAndPublicKey(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

            var canonicalEmail = CanonicalizeEmail(email);
            var account = ARC76.GetEmailAccount(canonicalEmail, password, DerivationSlot);
            var address = account.Address.ToString();
            var publicKeyBase64 = Convert.ToBase64String(account.KeyPair.ClearTextPublicKey);

            _logger.LogInformation("ARC76 address+publicKey derived. CanonicalEmail={CanonicalEmail}", canonicalEmail);
            return (address, publicKeyBase64);
        }

        /// <inheritdoc/>
        public (string Address, string Mnemonic) DeriveAccountMnemonic(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

            var canonicalEmail = CanonicalizeEmail(email);
            var account = ARC76.GetEmailAccount(canonicalEmail, password, DerivationSlot);
            var address = account.Address.ToString();
            var mnemonic = account.ToMnemonic()
                ?? throw new InvalidOperationException("Failed to convert ARC76 account to mnemonic");

            _logger.LogInformation("ARC76 account mnemonic derived. CanonicalEmail={CanonicalEmail}", canonicalEmail);
            return (address, mnemonic);
        }

        /// <inheritdoc/>
        public string CanonicalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));

            return email.Trim().ToLowerInvariant();
        }
    }
}

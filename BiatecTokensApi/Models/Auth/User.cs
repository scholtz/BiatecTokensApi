namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Represents a user account in the system
    /// </summary>
    public class User
    {
        /// <summary>
        /// Unique identifier for the user
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// User's email address (used for login)
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Hashed password (never store plain text passwords)
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// ARC76-derived Algorand account address for this user
        /// </summary>
        public string AlgorandAddress { get; set; } = string.Empty;

        /// <summary>
        /// Encrypted mnemonic for ARC76 account (encrypted with password)
        /// </summary>
        public string EncryptedMnemonic { get; set; } = string.Empty;

        /// <summary>
        /// When the account was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last login timestamp
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Whether the account is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Number of failed login attempts
        /// </summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// When the account was locked (if applicable)
        /// </summary>
        public DateTime? LockedUntil { get; set; }

        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}

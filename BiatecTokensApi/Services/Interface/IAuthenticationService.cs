using BiatecTokensApi.Models.Auth;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for authentication service with ARC76 account derivation
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Registers a new user with email/password and derives ARC76 account
        /// </summary>
        Task<RegisterResponse> RegisterAsync(RegisterRequest request, string? ipAddress, string? userAgent);

        /// <summary>
        /// Authenticates a user with email/password
        /// </summary>
        Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent);

        /// <summary>
        /// Refreshes an access token using a refresh token
        /// </summary>
        Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken, string? ipAddress, string? userAgent);

        /// <summary>
        /// Logs out a user and revokes all their refresh tokens
        /// </summary>
        Task<LogoutResponse> LogoutAsync(string userId);

        /// <summary>
        /// Validates a JWT access token and returns user ID
        /// </summary>
        Task<string?> ValidateAccessTokenAsync(string accessToken);

        /// <summary>
        /// Changes a user's password
        /// </summary>
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

        /// <summary>
        /// Gets an Algorand account mnemonic for signing on behalf of the user
        /// </summary>
        /// <remarks>
        /// Returns the encrypted mnemonic that can be used with ARC76.GetAccount() to derive the account
        /// </remarks>
        Task<string?> GetUserMnemonicForSigningAsync(string userId);

        /// <summary>
        /// Verifies ARC76 derivation determinism for the user identified by <paramref name="userId"/>.
        /// Returns a structured response proving the derived Algorand address is stable.
        /// </summary>
        Task<ARC76DerivationVerifyResponse> VerifyDerivationAsync(string userId, string? requestEmail, string correlationId);

        /// <summary>
        /// Returns the static ARC76 derivation contract information (version, algorithm, error taxonomy).
        /// </summary>
        ARC76DerivationInfoResponse GetDerivationInfo(string correlationId);

        /// <summary>
        /// Returns a session inspection payload for the user identified by <paramref name="userId"/>.
        /// Provides stable, derivation-linked fields that frontend can assert on.
        /// </summary>
        Task<SessionInspectionResponse> InspectSessionAsync(string userId, string correlationId);
    }
}

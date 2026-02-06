using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for user management
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly ConcurrentDictionary<string, User> _users = new();
        private readonly ConcurrentDictionary<string, string> _emailToUserId = new();
        private readonly ConcurrentDictionary<string, string> _algorandAddressToUserId = new();
        private readonly ConcurrentDictionary<string, RefreshToken> _refreshTokens = new();
        private readonly ConcurrentDictionary<string, List<string>> _userRefreshTokens = new();
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(ILogger<UserRepository> logger)
        {
            _logger = logger;
        }

        public Task<User> CreateUserAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.UserId)) throw new ArgumentException("UserId cannot be empty");
            if (string.IsNullOrWhiteSpace(user.Email)) throw new ArgumentException("Email cannot be empty");

            var emailLower = user.Email.ToLowerInvariant();

            if (_emailToUserId.ContainsKey(emailLower))
            {
                throw new InvalidOperationException("User with this email already exists");
            }

            if (!_users.TryAdd(user.UserId, user))
            {
                throw new InvalidOperationException($"User with ID {user.UserId} already exists");
            }

            _emailToUserId[emailLower] = user.UserId;
            
            if (!string.IsNullOrWhiteSpace(user.AlgorandAddress))
            {
                _algorandAddressToUserId[user.AlgorandAddress] = user.UserId;
            }

            _logger.LogInformation("Created user: UserId={UserId}, Email={Email}, AlgorandAddress={AlgorandAddress}",
                LoggingHelper.SanitizeLogInput(user.UserId), 
                LoggingHelper.SanitizeLogInput(emailLower),
                LoggingHelper.SanitizeLogInput(user.AlgorandAddress));

            return Task.FromResult(user);
        }

        public Task<User?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return Task.FromResult<User?>(null);

            var emailLower = email.ToLowerInvariant();
            if (_emailToUserId.TryGetValue(emailLower, out var userId))
            {
                if (_users.TryGetValue(userId, out var user))
                {
                    return Task.FromResult<User?>(user);
                }
            }

            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetUserByIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult<User?>(null);

            _users.TryGetValue(userId, out var user);
            return Task.FromResult<User?>(user);
        }

        public Task<User?> GetUserByAlgorandAddressAsync(string algorandAddress)
        {
            if (string.IsNullOrWhiteSpace(algorandAddress)) return Task.FromResult<User?>(null);

            if (_algorandAddressToUserId.TryGetValue(algorandAddress, out var userId))
            {
                if (_users.TryGetValue(userId, out var user))
                {
                    return Task.FromResult<User?>(user);
                }
            }

            return Task.FromResult<User?>(null);
        }

        public Task UpdateUserAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.UserId)) throw new ArgumentException("UserId cannot be empty");

            if (!_users.ContainsKey(user.UserId))
            {
                throw new InvalidOperationException($"User with ID {user.UserId} not found");
            }

            _users[user.UserId] = user;

            _logger.LogInformation("Updated user: UserId={UserId}", LoggingHelper.SanitizeLogInput(user.UserId));

            return Task.CompletedTask;
        }

        public Task DeleteUserAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId cannot be empty");

            if (_users.TryRemove(userId, out var user))
            {
                _emailToUserId.TryRemove(user.Email.ToLowerInvariant(), out _);
                
                if (!string.IsNullOrWhiteSpace(user.AlgorandAddress))
                {
                    _algorandAddressToUserId.TryRemove(user.AlgorandAddress, out _);
                }

                // Revoke all refresh tokens
                if (_userRefreshTokens.TryGetValue(userId, out var tokenIds))
                {
                    foreach (var tokenId in tokenIds)
                    {
                        _refreshTokens.TryRemove(tokenId, out _);
                    }
                    _userRefreshTokens.TryRemove(userId, out _);
                }

                _logger.LogInformation("Deleted user: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
            }

            return Task.CompletedTask;
        }

        public Task<bool> UserExistsAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return Task.FromResult(false);

            var emailLower = email.ToLowerInvariant();
            return Task.FromResult(_emailToUserId.ContainsKey(emailLower));
        }

        public Task StoreRefreshTokenAsync(RefreshToken token)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (string.IsNullOrWhiteSpace(token.Token)) throw new ArgumentException("Token cannot be empty");

            _refreshTokens[token.Token] = token;

            // Track tokens by user
            _userRefreshTokens.AddOrUpdate(
                token.UserId,
                new List<string> { token.Token },
                (key, list) =>
                {
                    list.Add(token.Token);
                    return list;
                });

            _logger.LogInformation("Stored refresh token for user: UserId={UserId}", 
                LoggingHelper.SanitizeLogInput(token.UserId));

            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetRefreshTokenAsync(string tokenValue)
        {
            if (string.IsNullOrWhiteSpace(tokenValue)) return Task.FromResult<RefreshToken?>(null);

            _refreshTokens.TryGetValue(tokenValue, out var token);
            return Task.FromResult<RefreshToken?>(token);
        }

        public Task RevokeRefreshTokenAsync(string tokenValue)
        {
            if (string.IsNullOrWhiteSpace(tokenValue)) return Task.CompletedTask;

            if (_refreshTokens.TryGetValue(tokenValue, out var token))
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Revoked refresh token for user: UserId={UserId}", 
                    LoggingHelper.SanitizeLogInput(token.UserId));
            }

            return Task.CompletedTask;
        }

        public Task<List<RefreshToken>> GetUserRefreshTokensAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult(new List<RefreshToken>());

            if (_userRefreshTokens.TryGetValue(userId, out var tokenIds))
            {
                var tokens = tokenIds
                    .Select(id => _refreshTokens.TryGetValue(id, out var token) ? token : null)
                    .Where(t => t != null)
                    .Cast<RefreshToken>()
                    .ToList();

                return Task.FromResult(tokens);
            }

            return Task.FromResult(new List<RefreshToken>());
        }

        public Task RevokeAllUserRefreshTokensAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;

            if (_userRefreshTokens.TryGetValue(userId, out var tokenIds))
            {
                foreach (var tokenId in tokenIds)
                {
                    if (_refreshTokens.TryGetValue(tokenId, out var token))
                    {
                        token.IsRevoked = true;
                        token.RevokedAt = DateTime.UtcNow;
                    }
                }

                _logger.LogInformation("Revoked all refresh tokens for user: UserId={UserId}", 
                    LoggingHelper.SanitizeLogInput(userId));
            }

            return Task.CompletedTask;
        }
    }
}

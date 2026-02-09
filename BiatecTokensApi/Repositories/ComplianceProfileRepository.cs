using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceProfile;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for compliance profile management
    /// </summary>
    public class ComplianceProfileRepository : IComplianceProfileRepository
    {
        private readonly ConcurrentDictionary<string, ComplianceProfile> _profiles = new();
        private readonly ConcurrentDictionary<string, string> _userIdToProfileId = new();
        private readonly ILogger<ComplianceProfileRepository> _logger;

        public ComplianceProfileRepository(ILogger<ComplianceProfileRepository> logger)
        {
            _logger = logger;
        }

        public Task<ComplianceProfile> CreateProfileAsync(ComplianceProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.UserId)) throw new ArgumentException("UserId cannot be empty");
            if (string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("Profile Id cannot be empty");

            // Check if profile already exists for user
            if (_userIdToProfileId.ContainsKey(profile.UserId))
            {
                throw new InvalidOperationException($"Compliance profile already exists for user {profile.UserId}");
            }

            if (!_profiles.TryAdd(profile.Id, profile))
            {
                throw new InvalidOperationException($"Profile with ID {profile.Id} already exists");
            }

            _userIdToProfileId[profile.UserId] = profile.Id;

            _logger.LogInformation("Created compliance profile: ProfileId={ProfileId}, UserId={UserId}, Entity={Entity}, Jurisdiction={Jurisdiction}",
                LoggingHelper.SanitizeLogInput(profile.Id),
                LoggingHelper.SanitizeLogInput(profile.UserId),
                LoggingHelper.SanitizeLogInput(profile.IssuingEntityName),
                LoggingHelper.SanitizeLogInput(profile.Jurisdiction));

            return Task.FromResult(profile);
        }

        public Task<ComplianceProfile?> GetProfileByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult<ComplianceProfile?>(null);

            if (_userIdToProfileId.TryGetValue(userId, out var profileId))
            {
                if (_profiles.TryGetValue(profileId, out var profile))
                {
                    return Task.FromResult<ComplianceProfile?>(profile);
                }
            }

            return Task.FromResult<ComplianceProfile?>(null);
        }

        public Task<ComplianceProfile?> GetProfileByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Task.FromResult<ComplianceProfile?>(null);

            _profiles.TryGetValue(id, out var profile);
            return Task.FromResult<ComplianceProfile?>(profile);
        }

        public Task UpdateProfileAsync(ComplianceProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("Profile Id cannot be empty");

            if (!_profiles.ContainsKey(profile.Id))
            {
                throw new InvalidOperationException($"Profile with ID {profile.Id} does not exist");
            }

            profile.UpdatedAt = DateTime.UtcNow;
            _profiles[profile.Id] = profile;

            _logger.LogInformation("Updated compliance profile: ProfileId={ProfileId}, UserId={UserId}, Status={Status}",
                LoggingHelper.SanitizeLogInput(profile.Id),
                LoggingHelper.SanitizeLogInput(profile.UserId),
                LoggingHelper.SanitizeLogInput(profile.ReadinessStatus.ToString()));

            return Task.CompletedTask;
        }

        public Task DeleteProfileAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Task.CompletedTask;

            if (_profiles.TryRemove(id, out var profile))
            {
                _userIdToProfileId.TryRemove(profile.UserId, out _);

                _logger.LogInformation("Deleted compliance profile: ProfileId={ProfileId}, UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(id),
                    LoggingHelper.SanitizeLogInput(profile.UserId));
            }

            return Task.CompletedTask;
        }

        public Task<bool> ProfileExistsForUserAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult(false);

            return Task.FromResult(_userIdToProfileId.ContainsKey(userId));
        }
    }
}

using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository implementation for KYC records
    /// </summary>
    public class KycRepository : IKycRepository
    {
        private readonly ConcurrentDictionary<string, KycRecord> _kycRecords = new();
        private readonly ConcurrentDictionary<string, string> _userIdToKycId = new();
        private readonly ConcurrentDictionary<string, string> _providerRefToKycId = new();
        private readonly ILogger<KycRepository> _logger;

        public KycRepository(ILogger<KycRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<KycRecord> CreateKycRecordAsync(KycRecord record)
        {
            if (string.IsNullOrEmpty(record.KycId))
            {
                record.KycId = Guid.NewGuid().ToString();
            }

            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;

            if (!_kycRecords.TryAdd(record.KycId, record))
            {
                _logger.LogError("Failed to create KYC record with ID {KycId}", record.KycId);
                throw new InvalidOperationException($"KYC record with ID {record.KycId} already exists");
            }

            // Update lookup indexes
            _userIdToKycId[record.UserId] = record.KycId;
            if (!string.IsNullOrEmpty(record.ProviderReferenceId))
            {
                _providerRefToKycId[record.ProviderReferenceId] = record.KycId;
            }

            _logger.LogInformation("Created KYC record {KycId} for user {UserId}", record.KycId, record.UserId);
            return Task.FromResult(record);
        }

        /// <inheritdoc/>
        public Task<KycRecord?> GetKycRecordAsync(string kycId)
        {
            _kycRecords.TryGetValue(kycId, out var record);
            return Task.FromResult(record);
        }

        /// <inheritdoc/>
        public Task<KycRecord?> GetKycRecordByUserIdAsync(string userId)
        {
            if (_userIdToKycId.TryGetValue(userId, out var kycId))
            {
                _kycRecords.TryGetValue(kycId, out var record);
                return Task.FromResult(record);
            }
            return Task.FromResult<KycRecord?>(null);
        }

        /// <inheritdoc/>
        public Task<KycRecord?> GetKycRecordByProviderReferenceIdAsync(string providerReferenceId)
        {
            if (_providerRefToKycId.TryGetValue(providerReferenceId, out var kycId))
            {
                _kycRecords.TryGetValue(kycId, out var record);
                return Task.FromResult(record);
            }
            return Task.FromResult<KycRecord?>(null);
        }

        /// <inheritdoc/>
        public Task<KycRecord> UpdateKycRecordAsync(KycRecord record)
        {
            if (!_kycRecords.ContainsKey(record.KycId))
            {
                _logger.LogError("Cannot update non-existent KYC record {KycId}", record.KycId);
                throw new InvalidOperationException($"KYC record with ID {record.KycId} does not exist");
            }

            record.UpdatedAt = DateTime.UtcNow;
            _kycRecords[record.KycId] = record;

            // Update lookup indexes
            _userIdToKycId[record.UserId] = record.KycId;
            if (!string.IsNullOrEmpty(record.ProviderReferenceId))
            {
                _providerRefToKycId[record.ProviderReferenceId] = record.KycId;
            }

            _logger.LogInformation("Updated KYC record {KycId} with status {Status}", record.KycId, record.Status);
            return Task.FromResult(record);
        }

        /// <inheritdoc/>
        public Task<List<KycRecord>> GetKycRecordsByUserIdAsync(string userId)
        {
            var records = _kycRecords.Values
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
            return Task.FromResult(records);
        }

        /// <inheritdoc/>
        public Task<List<KycRecord>> GetKycRecordsByStatusAsync(KycStatus status)
        {
            var records = _kycRecords.Values
                .Where(r => r.Status == status)
                .OrderBy(r => r.CreatedAt)
                .ToList();
            return Task.FromResult(records);
        }
    }
}

using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository implementation for AML screening records
    /// </summary>
    public class AmlRepository : IAmlRepository
    {
        private readonly ConcurrentDictionary<string, AmlRecord> _records = new();
        private readonly ConcurrentDictionary<string, string> _userIdToAmlId = new();
        private readonly ConcurrentDictionary<string, string> _providerRefToAmlId = new();
        private readonly ILogger<AmlRepository> _logger;

        public AmlRepository(ILogger<AmlRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<AmlRecord> CreateAmlRecordAsync(AmlRecord record)
        {
            if (string.IsNullOrEmpty(record.AmlId))
            {
                record.AmlId = Guid.NewGuid().ToString();
            }

            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;

            if (!_records.TryAdd(record.AmlId, record))
            {
                _logger.LogError("Failed to create AML record with ID {AmlId}", record.AmlId);
                throw new InvalidOperationException($"AML record with ID {record.AmlId} already exists");
            }

            _userIdToAmlId[record.UserId] = record.AmlId;
            if (!string.IsNullOrEmpty(record.ProviderReferenceId))
            {
                _providerRefToAmlId[record.ProviderReferenceId] = record.AmlId;
            }

            _logger.LogInformation("Created AML record {AmlId} for user {UserId}", record.AmlId, record.UserId);
            return Task.FromResult(record);
        }

        /// <inheritdoc/>
        public Task<AmlRecord?> GetAmlRecordAsync(string amlId)
        {
            _records.TryGetValue(amlId, out var record);
            return Task.FromResult(record);
        }

        /// <inheritdoc/>
        public Task<AmlRecord?> GetAmlRecordByUserIdAsync(string userId)
        {
            if (_userIdToAmlId.TryGetValue(userId, out var amlId))
            {
                _records.TryGetValue(amlId, out var record);
                return Task.FromResult(record);
            }
            return Task.FromResult<AmlRecord?>(null);
        }

        /// <inheritdoc/>
        public Task<AmlRecord?> GetAmlRecordByProviderReferenceIdAsync(string providerReferenceId)
        {
            if (_providerRefToAmlId.TryGetValue(providerReferenceId, out var amlId))
            {
                _records.TryGetValue(amlId, out var record);
                return Task.FromResult(record);
            }
            return Task.FromResult<AmlRecord?>(null);
        }

        /// <inheritdoc/>
        public Task<AmlRecord> UpdateAmlRecordAsync(AmlRecord record)
        {
            record.UpdatedAt = DateTime.UtcNow;
            _records[record.AmlId] = record;

            // Update provider ref index if it changed
            if (!string.IsNullOrEmpty(record.ProviderReferenceId))
            {
                _providerRefToAmlId[record.ProviderReferenceId] = record.AmlId;
            }

            _logger.LogInformation("Updated AML record {AmlId} for user {UserId}", record.AmlId, record.UserId);
            return Task.FromResult(record);
        }

        /// <inheritdoc/>
        public Task<List<AmlRecord>> GetAmlRecordsByUserIdAsync(string userId)
        {
            var records = _records.Values
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
            return Task.FromResult(records);
        }

        /// <inheritdoc/>
        public Task<List<AmlRecord>> GetRecordsDueForRescreeningAsync(DateTime cutoff)
        {
            var records = _records.Values
                .Where(r => r.NextScreeningDue.HasValue && r.NextScreeningDue.Value <= cutoff
                    && r.Status == AmlScreeningStatus.Cleared)
                .ToList();
            return Task.FromResult(records);
        }
    }
}

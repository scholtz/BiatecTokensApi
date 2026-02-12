using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for compliance metadata
    /// </summary>
    /// <remarks>
    /// This implementation uses thread-safe concurrent collections for production-grade concurrency.
    /// Can be replaced with a database-backed implementation without API changes.
    /// </remarks>
    public class ComplianceRepository : IComplianceRepository
    {
        private readonly ConcurrentDictionary<ulong, ComplianceMetadata> _metadata = new();
        private readonly ConcurrentBag<ComplianceAuditLogEntry> _auditLog = new();
        private readonly ConcurrentDictionary<string, ComplianceAttestation> _attestations = new();
        private readonly ConcurrentDictionary<string, ValidationEvidence> _validationEvidence = new();
        private readonly ILogger<ComplianceRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public ComplianceRepository(ILogger<ComplianceRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<bool> UpsertMetadataAsync(ComplianceMetadata metadata)
        {
            try
            {
                _metadata.AddOrUpdate(
                    metadata.AssetId,
                    metadata,
                    (key, existing) =>
                    {
                        // Preserve creation info when updating
                        metadata.CreatedBy = existing.CreatedBy;
                        metadata.CreatedAt = existing.CreatedAt;
                        metadata.UpdatedAt = DateTime.UtcNow;
                        return metadata;
                    });

                _logger.LogDebug("Upserted compliance metadata for asset {AssetId}", metadata.AssetId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting compliance metadata for asset {AssetId}", metadata.AssetId);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<ComplianceMetadata?> GetMetadataByAssetIdAsync(ulong assetId)
        {
            _metadata.TryGetValue(assetId, out var metadata);
            return Task.FromResult(metadata);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteMetadataAsync(ulong assetId)
        {
            var result = _metadata.TryRemove(assetId, out _);
            if (result)
            {
                _logger.LogDebug("Deleted compliance metadata for asset {AssetId}", assetId);
            }
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<List<ComplianceMetadata>> ListMetadataAsync(ListComplianceMetadataRequest request)
        {
            var query = _metadata.Values.AsEnumerable();

            // Apply filters
            if (request.ComplianceStatus.HasValue)
            {
                query = query.Where(m => m.ComplianceStatus == request.ComplianceStatus.Value);
            }

            if (request.VerificationStatus.HasValue)
            {
                query = query.Where(m => m.VerificationStatus == request.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(m => 
                    !string.IsNullOrEmpty(m.Network) && 
                    m.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            // Order by creation date descending
            query = query.OrderByDescending(m => m.CreatedAt);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<int> GetMetadataCountAsync(ListComplianceMetadataRequest request)
        {
            var query = _metadata.Values.AsEnumerable();

            // Apply same filters as ListMetadataAsync
            if (request.ComplianceStatus.HasValue)
            {
                query = query.Where(m => m.ComplianceStatus == request.ComplianceStatus.Value);
            }

            if (request.VerificationStatus.HasValue)
            {
                query = query.Where(m => m.VerificationStatus == request.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(m => 
                    !string.IsNullOrEmpty(m.Network) && 
                    m.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(query.Count());
        }

        /// <inheritdoc/>
        public Task<bool> AddAuditLogEntryAsync(ComplianceAuditLogEntry entry)
        {
            try
            {
                _auditLog.Add(entry);
                _logger.LogDebug("Added audit log entry {Id} for action {ActionType}", entry.Id, entry.ActionType);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding audit log entry for action {ActionType}", entry.ActionType);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<List<ComplianceAuditLogEntry>> GetAuditLogAsync(GetComplianceAuditLogRequest request)
        {
            var query = _auditLog.AsEnumerable();

            // Apply filters
            if (request.AssetId.HasValue)
            {
                query = query.Where(e => e.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.Network) && 
                    e.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ActionType.HasValue)
            {
                query = query.Where(e => e.ActionType == request.ActionType.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PerformedBy))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.PerformedBy) && 
                    e.PerformedBy.Equals(request.PerformedBy, StringComparison.OrdinalIgnoreCase));
            }

            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt <= request.ToDate.Value);
            }

            // Order by timestamp descending (most recent first)
            query = query.OrderByDescending(e => e.PerformedAt);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<int> GetAuditLogCountAsync(GetComplianceAuditLogRequest request)
        {
            var query = _auditLog.AsEnumerable();

            // Apply same filters as GetAuditLogAsync
            if (request.AssetId.HasValue)
            {
                query = query.Where(e => e.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.Network) && 
                    e.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ActionType.HasValue)
            {
                query = query.Where(e => e.ActionType == request.ActionType.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PerformedBy))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.PerformedBy) && 
                    e.PerformedBy.Equals(request.PerformedBy, StringComparison.OrdinalIgnoreCase));
            }

            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt <= request.ToDate.Value);
            }

            return Task.FromResult(query.Count());
        }

        /// <inheritdoc/>
        public Task<bool> CreateAttestationAsync(ComplianceAttestation attestation)
        {
            try
            {
                if (_attestations.TryAdd(attestation.Id, attestation))
                {
                    _logger.LogDebug("Created attestation {Id} for wallet {WalletAddress} and asset {AssetId}", 
                        attestation.Id, attestation.WalletAddress, attestation.AssetId);
                    return Task.FromResult(true);
                }
                
                _logger.LogWarning("Attestation with ID {Id} already exists", attestation.Id);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attestation for wallet {WalletAddress}", attestation.WalletAddress);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<ComplianceAttestation?> GetAttestationByIdAsync(string id)
        {
            _attestations.TryGetValue(id, out var attestation);
            return Task.FromResult(attestation);
        }

        /// <inheritdoc/>
        public Task<List<ComplianceAttestation>> ListAttestationsAsync(ListComplianceAttestationsRequest request)
        {
            var query = _attestations.Values.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.WalletAddress))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.WalletAddress) && 
                    a.WalletAddress.Equals(request.WalletAddress, StringComparison.OrdinalIgnoreCase));
            }

            if (request.AssetId.HasValue)
            {
                query = query.Where(a => a.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.IssuerAddress))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.IssuerAddress) && 
                    a.IssuerAddress.Equals(request.IssuerAddress, StringComparison.OrdinalIgnoreCase));
            }

            if (request.VerificationStatus.HasValue)
            {
                query = query.Where(a => a.VerificationStatus == request.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.AttestationType))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.AttestationType) && 
                    a.AttestationType.Equals(request.AttestationType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.Network) && 
                    a.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ExcludeExpired.HasValue && request.ExcludeExpired.Value)
            {
                var now = DateTime.UtcNow;
                query = query.Where(a => !a.ExpiresAt.HasValue || a.ExpiresAt.Value > now);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(a => a.IssuedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(a => a.IssuedAt <= request.ToDate.Value);
            }

            // Order by creation date descending (most recent first)
            query = query.OrderByDescending(a => a.CreatedAt);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<int> GetAttestationCountAsync(ListComplianceAttestationsRequest request)
        {
            var query = _attestations.Values.AsEnumerable();

            // Apply same filters as ListAttestationsAsync
            if (!string.IsNullOrWhiteSpace(request.WalletAddress))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.WalletAddress) && 
                    a.WalletAddress.Equals(request.WalletAddress, StringComparison.OrdinalIgnoreCase));
            }

            if (request.AssetId.HasValue)
            {
                query = query.Where(a => a.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.IssuerAddress))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.IssuerAddress) && 
                    a.IssuerAddress.Equals(request.IssuerAddress, StringComparison.OrdinalIgnoreCase));
            }

            if (request.VerificationStatus.HasValue)
            {
                query = query.Where(a => a.VerificationStatus == request.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.AttestationType))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.AttestationType) && 
                    a.AttestationType.Equals(request.AttestationType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(a => 
                    !string.IsNullOrEmpty(a.Network) && 
                    a.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ExcludeExpired.HasValue && request.ExcludeExpired.Value)
            {
                var now = DateTime.UtcNow;
                query = query.Where(a => !a.ExpiresAt.HasValue || a.ExpiresAt.Value > now);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(a => a.IssuedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(a => a.IssuedAt <= request.ToDate.Value);
            }

            return Task.FromResult(query.Count());
        }

        // Phase 2: Issuer Profile Management

        private readonly ConcurrentDictionary<string, IssuerProfile> _issuerProfiles = new();

        /// <inheritdoc/>
        public Task<bool> UpsertIssuerProfileAsync(IssuerProfile profile)
        {
            try
            {
                _issuerProfiles.AddOrUpdate(
                    profile.IssuerAddress,
                    profile,
                    (key, existing) =>
                    {
                        // Preserve creation info when updating
                        profile.CreatedBy = existing.CreatedBy;
                        profile.CreatedAt = existing.CreatedAt;
                        profile.UpdatedAt = DateTime.UtcNow;
                        return profile;
                    });

                _logger.LogDebug("Upserted issuer profile for {IssuerAddress}", profile.IssuerAddress);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting issuer profile for {IssuerAddress}", profile.IssuerAddress);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<IssuerProfile?> GetIssuerProfileAsync(string issuerAddress)
        {
            _issuerProfiles.TryGetValue(issuerAddress, out var profile);
            return Task.FromResult(profile);
        }

        /// <inheritdoc/>
        public Task<List<ulong>> ListIssuerAssetsAsync(string issuerAddress, ListIssuerAssetsRequest request)
        {
            // Find all assets created by this issuer
            var query = _metadata.Values
                .Where(m => m.CreatedBy == issuerAddress)
                .AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(m => m.Network == request.Network);
            }

            if (request.ComplianceStatus.HasValue)
            {
                query = query.Where(m => m.ComplianceStatus == request.ComplianceStatus.Value);
            }

            // Get asset IDs
            var assetIds = query.Select(m => m.AssetId).ToList();

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            return Task.FromResult(assetIds.Skip(skip).Take(request.PageSize).ToList());
        }

        /// <inheritdoc/>
        public Task<int> GetIssuerAssetCountAsync(string issuerAddress, ListIssuerAssetsRequest request)
        {
            var query = _metadata.Values
                .Where(m => m.CreatedBy == issuerAddress)
                .AsEnumerable();

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(m => m.Network == request.Network);
            }

            if (request.ComplianceStatus.HasValue)
            {
                query = query.Where(m => m.ComplianceStatus == request.ComplianceStatus.Value);
            }

            return Task.FromResult(query.Count());
        }

        // Phase 3: Blacklist Management

        private readonly ConcurrentDictionary<string, BlacklistEntry> _blacklistEntries = new();

        /// <inheritdoc/>
        public Task<bool> CreateBlacklistEntryAsync(BlacklistEntry entry)
        {
            try
            {
                var added = _blacklistEntries.TryAdd(entry.Id, entry);
                if (added)
                {
                    _logger.LogDebug("Created blacklist entry {Id} for address {Address}", entry.Id, entry.Address);
                }
                return Task.FromResult(added);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating blacklist entry {Id}", entry.Id);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<BlacklistEntry?> GetBlacklistEntryAsync(string id)
        {
            _blacklistEntries.TryGetValue(id, out var entry);
            return Task.FromResult(entry);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateBlacklistEntryAsync(BlacklistEntry entry)
        {
            try
            {
                if (_blacklistEntries.ContainsKey(entry.Id))
                {
                    _blacklistEntries[entry.Id] = entry;
                    _logger.LogDebug("Updated blacklist entry {Id}", entry.Id);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating blacklist entry {Id}", entry.Id);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<bool> DeleteBlacklistEntryAsync(string id)
        {
            var result = _blacklistEntries.TryRemove(id, out _);
            if (result)
            {
                _logger.LogDebug("Deleted blacklist entry {Id}", id);
            }
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<List<BlacklistEntry>> ListBlacklistEntriesAsync(ListBlacklistEntriesRequest request)
        {
            var query = _blacklistEntries.Values.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.Address))
            {
                query = query.Where(e => e.Address == request.Address);
            }

            if (request.AssetId.HasValue)
            {
                query = query.Where(e => e.AssetId == request.AssetId.Value || e.AssetId == null);
            }

            if (request.Category.HasValue)
            {
                query = query.Where(e => e.Category == request.Category.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(e => e.Status == request.Status.Value);
            }

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(e => e.Network == request.Network || e.Network == null);
            }

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            return Task.FromResult(query.Skip(skip).Take(request.PageSize).ToList());
        }

        /// <inheritdoc/>
        public Task<int> GetBlacklistEntryCountAsync(ListBlacklistEntriesRequest request)
        {
            var query = _blacklistEntries.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(request.Address))
            {
                query = query.Where(e => e.Address == request.Address);
            }

            if (request.AssetId.HasValue)
            {
                query = query.Where(e => e.AssetId == request.AssetId.Value || e.AssetId == null);
            }

            if (request.Category.HasValue)
            {
                query = query.Where(e => e.Category == request.Category.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(e => e.Status == request.Status.Value);
            }

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(e => e.Network == request.Network || e.Network == null);
            }

            return Task.FromResult(query.Count());
        }

        /// <inheritdoc/>
        public Task<List<BlacklistEntry>> CheckBlacklistAsync(string address, ulong? assetId, string? network)
        {
            var now = DateTime.UtcNow;
            
            var query = _blacklistEntries.Values
                .Where(e => e.Address == address)
                .Where(e => e.Status == BlacklistStatus.Active)
                .Where(e => e.EffectiveDate <= now)
                .Where(e => !e.ExpirationDate.HasValue || e.ExpirationDate.Value > now)
                .AsEnumerable();

            // Check for global blacklist or asset-specific
            if (assetId.HasValue)
            {
                query = query.Where(e => e.AssetId == null || e.AssetId == assetId.Value);
            }
            else
            {
                query = query.Where(e => e.AssetId == null);
            }

            // Check network if specified
            if (!string.IsNullOrEmpty(network))
            {
                query = query.Where(e => e.Network == null || e.Network == network);
            }

            return Task.FromResult(query.ToList());
        }

        // Validation Evidence Management

        /// <inheritdoc/>
        public Task<bool> StoreValidationEvidenceAsync(ValidationEvidence evidence)
        {
            try
            {
                _validationEvidence.AddOrUpdate(evidence.EvidenceId, evidence, (key, existing) => evidence);
                _logger.LogDebug("Stored validation evidence {EvidenceId}", evidence.EvidenceId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing validation evidence {EvidenceId}", evidence.EvidenceId);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<ValidationEvidence?> GetValidationEvidenceByIdAsync(string evidenceId)
        {
            _validationEvidence.TryGetValue(evidenceId, out var evidence);
            return Task.FromResult(evidence);
        }

        /// <inheritdoc/>
        public Task<List<ValidationEvidence>> ListValidationEvidenceAsync(ListValidationEvidenceRequest request)
        {
            var query = _validationEvidence.Values.AsEnumerable();

            // Apply filters
            if (request.TokenId.HasValue)
            {
                query = query.Where(e => e.TokenId == request.TokenId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PreIssuanceId))
            {
                query = query.Where(e =>
                    !string.IsNullOrEmpty(e.PreIssuanceId) &&
                    e.PreIssuanceId.Equals(request.PreIssuanceId, StringComparison.OrdinalIgnoreCase));
            }

            if (request.Passed.HasValue)
            {
                query = query.Where(e => e.Passed == request.Passed.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.ValidationTimestamp >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.ValidationTimestamp <= request.ToDate.Value);
            }

            // Order by validation timestamp descending
            query = query.OrderByDescending(e => e.ValidationTimestamp);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var results = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(results);
        }

        /// <inheritdoc/>
        public Task<int> GetValidationEvidenceCountAsync(ListValidationEvidenceRequest request)
        {
            var query = _validationEvidence.Values.AsEnumerable();

            // Apply same filters as ListValidationEvidenceAsync
            if (request.TokenId.HasValue)
            {
                query = query.Where(e => e.TokenId == request.TokenId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PreIssuanceId))
            {
                query = query.Where(e =>
                    !string.IsNullOrEmpty(e.PreIssuanceId) &&
                    e.PreIssuanceId.Equals(request.PreIssuanceId, StringComparison.OrdinalIgnoreCase));
            }

            if (request.Passed.HasValue)
            {
                query = query.Where(e => e.Passed == request.Passed.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.ValidationTimestamp >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.ValidationTimestamp <= request.ToDate.Value);
            }

            return Task.FromResult(query.Count());
        }

        /// <inheritdoc/>
        public Task<ValidationEvidence?> GetMostRecentPassingValidationAsync(ulong? tokenId, string? preIssuanceId)
        {
            var query = _validationEvidence.Values
                .Where(e => e.Passed);

            if (tokenId.HasValue)
            {
                query = query.Where(e => e.TokenId == tokenId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(preIssuanceId))
            {
                query = query.Where(e =>
                    !string.IsNullOrEmpty(e.PreIssuanceId) &&
                    e.PreIssuanceId.Equals(preIssuanceId, StringComparison.OrdinalIgnoreCase));
            }

            var evidence = query
                .OrderByDescending(e => e.ValidationTimestamp)
                .FirstOrDefault();
            
            return Task.FromResult(evidence);
        }
    }
}

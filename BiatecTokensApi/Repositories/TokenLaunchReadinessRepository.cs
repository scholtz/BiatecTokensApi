using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Repositories.Interface;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for token launch readiness evidence
    /// </summary>
    /// <remarks>
    /// This implementation uses in-memory storage. In production, this should be replaced
    /// with a persistent storage backend (e.g., database, blob storage).
    /// </remarks>
    public class TokenLaunchReadinessRepository : ITokenLaunchReadinessRepository
    {
        private readonly ConcurrentDictionary<string, TokenLaunchReadinessEvidence> _evidenceByEvaluationId;
        private readonly ConcurrentDictionary<string, List<string>> _evidenceIdsByUserId;
        private readonly ConcurrentDictionary<string, List<string>> _evidenceIdsByTokenDeployment;
        private readonly ILogger<TokenLaunchReadinessRepository> _logger;

        public TokenLaunchReadinessRepository(ILogger<TokenLaunchReadinessRepository> logger)
        {
            _evidenceByEvaluationId = new ConcurrentDictionary<string, TokenLaunchReadinessEvidence>(StringComparer.OrdinalIgnoreCase);
            _evidenceIdsByUserId = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _evidenceIdsByTokenDeployment = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<TokenLaunchReadinessEvidence> StoreEvidenceAsync(TokenLaunchReadinessEvidence evidence)
        {
            // Generate data hash for integrity verification
            evidence.DataHash = GenerateDataHash(evidence);

            // Store evidence
            _evidenceByEvaluationId[evidence.EvaluationId] = evidence;

            // Index by user ID
            var userEvidenceIds = _evidenceIdsByUserId.GetOrAdd(evidence.UserId, _ => new List<string>());
            lock (userEvidenceIds)
            {
                userEvidenceIds.Add(evidence.EvaluationId);
            }

            // Index by token deployment if applicable
            if (!string.IsNullOrEmpty(evidence.TokenDeploymentId))
            {
                var deploymentEvidenceIds = _evidenceIdsByTokenDeployment.GetOrAdd(
                    evidence.TokenDeploymentId, 
                    _ => new List<string>());
                lock (deploymentEvidenceIds)
                {
                    deploymentEvidenceIds.Add(evidence.EvaluationId);
                }
            }

            _logger.LogInformation(
                "Stored readiness evidence: EvaluationId={EvaluationId}, UserId={UserId}",
                evidence.EvaluationId,
                evidence.UserId);

            return Task.FromResult(evidence);
        }

        /// <inheritdoc/>
        public Task<TokenLaunchReadinessEvidence?> GetEvidenceByEvaluationIdAsync(string evaluationId)
        {
            _evidenceByEvaluationId.TryGetValue(evaluationId, out var evidence);
            return Task.FromResult(evidence);
        }

        /// <inheritdoc/>
        public Task<List<TokenLaunchReadinessEvidence>> GetEvidenceHistoryAsync(
            string userId,
            int limit = 50,
            DateTime? fromDate = null)
        {
            var evidenceList = new List<TokenLaunchReadinessEvidence>();

            if (_evidenceIdsByUserId.TryGetValue(userId, out var evidenceIds))
            {
                List<string> safeEvidenceIds;
                lock (evidenceIds)
                {
                    safeEvidenceIds = evidenceIds.ToList();
                }

                foreach (var evidenceId in safeEvidenceIds)
                {
                    if (_evidenceByEvaluationId.TryGetValue(evidenceId, out var evidence))
                    {
                        if (!fromDate.HasValue || evidence.CreatedAt >= fromDate.Value)
                        {
                            evidenceList.Add(evidence);
                        }
                    }
                }
            }

            // Sort by creation date descending and limit
            evidenceList = evidenceList
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(evidenceList);
        }

        /// <inheritdoc/>
        public Task<List<TokenLaunchReadinessEvidence>> GetEvidenceByTokenDeploymentAsync(string tokenDeploymentId)
        {
            var evidenceList = new List<TokenLaunchReadinessEvidence>();

            if (_evidenceIdsByTokenDeployment.TryGetValue(tokenDeploymentId, out var evidenceIds))
            {
                List<string> safeEvidenceIds;
                lock (evidenceIds)
                {
                    safeEvidenceIds = evidenceIds.ToList();
                }

                foreach (var evidenceId in safeEvidenceIds)
                {
                    if (_evidenceByEvaluationId.TryGetValue(evidenceId, out var evidence))
                    {
                        evidenceList.Add(evidence);
                    }
                }
            }

            evidenceList = evidenceList
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            return Task.FromResult(evidenceList);
        }

        /// <inheritdoc/>
        public Task<TokenLaunchReadinessEvidence?> GetLatestEvaluationAsync(string userId)
        {
            TokenLaunchReadinessEvidence? latestEvidence = null;

            if (_evidenceIdsByUserId.TryGetValue(userId, out var evidenceIds))
            {
                List<string> safeEvidenceIds;
                lock (evidenceIds)
                {
                    safeEvidenceIds = evidenceIds.ToList();
                }

                foreach (var evidenceId in safeEvidenceIds)
                {
                    if (_evidenceByEvaluationId.TryGetValue(evidenceId, out var evidence))
                    {
                        if (latestEvidence == null || evidence.CreatedAt > latestEvidence.CreatedAt)
                        {
                            latestEvidence = evidence;
                        }
                    }
                }
            }

            return Task.FromResult(latestEvidence);
        }

        /// <summary>
        /// Generates SHA256 hash of evidence data for integrity verification
        /// </summary>
        private string GenerateDataHash(TokenLaunchReadinessEvidence evidence)
        {
            var dataToHash = $"{evidence.EvaluationId}|{evidence.UserId}|{evidence.RequestSnapshot}|{evidence.ResponseSnapshot}|{evidence.CreatedAt:O}";
            
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
            return Convert.ToBase64String(hashBytes);
        }
    }
}

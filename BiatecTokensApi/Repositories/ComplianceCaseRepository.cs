using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory singleton repository for compliance case persistence.
    ///
    /// Registered as a singleton so that cases, idempotency keys, and export logs
    /// survive individual HTTP requests and DI scope recycling within a single
    /// application process.
    ///
    /// For restart-durable storage, replace this implementation with a
    /// database-backed version (e.g., Entity Framework Core, Dapper) while
    /// keeping the same <see cref="IComplianceCaseRepository"/> interface contract.
    /// </summary>
    public class ComplianceCaseRepository : IComplianceCaseRepository
    {
        private readonly ILogger<ComplianceCaseRepository> _logger;

        // Primary store: caseId → ComplianceCase
        private readonly ConcurrentDictionary<string, ComplianceCase> _cases = new();

        // Idempotency index: "issuerId|subjectId|type" → caseId
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();

        // Export audit log: caseId → append-only list of CaseExportMetadata
        private readonly ConcurrentDictionary<string, List<CaseExportMetadata>> _exportLog = new();

        private const int MaxExportRecordsPerCase = 200;

        /// <summary>
        /// Initialises a new instance of <see cref="ComplianceCaseRepository"/>.
        /// </summary>
        public ComplianceCaseRepository(ILogger<ComplianceCaseRepository> logger)
        {
            _logger = logger;
        }

        // ── Case CRUD ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceCase?> GetCaseAsync(string caseId)
        {
            _cases.TryGetValue(caseId, out ComplianceCase? c);
            return Task.FromResult(c);
        }

        /// <inheritdoc/>
        public Task SaveCaseAsync(ComplianceCase complianceCase)
        {
            _cases[complianceCase.CaseId] = complianceCase;

            _logger.LogInformation(
                "ComplianceCaseRepository: saved case. CaseId={CaseId} State={State} IssuerId={IssuerId}",
                complianceCase.CaseId, complianceCase.State, complianceCase.IssuerId);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<List<ComplianceCase>> QueryCasesAsync(Func<ComplianceCase, bool>? predicate = null)
        {
            IEnumerable<ComplianceCase> all = _cases.Values;
            List<ComplianceCase> result = predicate == null
                ? all.ToList()
                : all.Where(predicate).ToList();
            return Task.FromResult(result);
        }

        // ── Idempotency index ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<string> AddOrGetIdempotencyKeyAsync(string key, string caseId)
        {
            // GetOrAdd is atomic in ConcurrentDictionary — safe for concurrent callers
            string stored = _idempotencyIndex.GetOrAdd(key, caseId);

            if (stored != caseId)
            {
                _logger.LogDebug(
                    "ComplianceCaseRepository: idempotency key '{Key}' already maps to '{ExistingId}'; ignoring new '{NewId}'.",
                    key, stored, caseId);
            }

            return Task.FromResult(stored);
        }

        /// <inheritdoc/>
        public Task RemoveIdempotencyKeyAsync(string key)
        {
            _idempotencyIndex.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        // ── Export log ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task AppendExportRecordAsync(string caseId, CaseExportMetadata metadata)
        {
            List<CaseExportMetadata> log =
                _exportLog.GetOrAdd(caseId, _ => new List<CaseExportMetadata>());

            lock (log)
            {
                log.Add(metadata);

                // Guard against unbounded growth — remove all excess in one operation
                if (log.Count > MaxExportRecordsPerCase)
                    log.RemoveRange(0, log.Count - MaxExportRecordsPerCase);
            }

            _logger.LogInformation(
                "ComplianceCaseRepository: recorded export. CaseId={CaseId} ExportId={ExportId} By={By}",
                caseId, metadata.ExportId, metadata.ExportedBy);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<List<CaseExportMetadata>> GetExportRecordsAsync(string caseId)
        {
            if (!_exportLog.TryGetValue(caseId, out List<CaseExportMetadata>? log))
                return Task.FromResult(new List<CaseExportMetadata>());

            List<CaseExportMetadata> snapshot;
            lock (log)
            {
                snapshot = log.OrderBy(m => m.ExportedAt).ToList();
            }

            return Task.FromResult(snapshot);
        }
    }
}

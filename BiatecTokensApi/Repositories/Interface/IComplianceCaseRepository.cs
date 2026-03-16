using BiatecTokensApi.Models.ComplianceCaseManagement;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository abstraction for durable compliance case persistence.
    ///
    /// The default implementation (<see cref="BiatecTokensApi.Repositories.ComplianceCaseRepository"/>)
    /// is an in-memory singleton that keeps state for the lifetime of the process.
    /// Replace with a database-backed implementation for full restart durability.
    /// </summary>
    public interface IComplianceCaseRepository
    {
        // ── Case CRUD ──────────────────────────────────────────────────────────

        /// <summary>Retrieve a case by its ID.  Returns null when not found.</summary>
        Task<ComplianceCase?> GetCaseAsync(string caseId);

        /// <summary>
        /// Persist (insert or overwrite) a compliance case.
        /// Callers are responsible for setting <see cref="ComplianceCase.UpdatedAt"/> before saving.
        /// </summary>
        Task SaveCaseAsync(ComplianceCase complianceCase);

        /// <summary>
        /// Return all cases that satisfy the supplied predicate.
        /// Pass <c>null</c> to return every stored case.
        /// </summary>
        Task<List<ComplianceCase>> QueryCasesAsync(Func<ComplianceCase, bool>? predicate = null);

        // ── Idempotency index ──────────────────────────────────────────────────

        /// <summary>
        /// Atomically register the idempotency key → caseId mapping.
        /// Returns the <em>caseId that was stored</em>:
        /// <list type="bullet">
        ///   <item>If the key was not present, stores the supplied <paramref name="caseId"/> and returns it.</item>
        ///   <item>If the key already exists, returns the previously stored caseId (no-op).</item>
        /// </list>
        /// </summary>
        Task<string> AddOrGetIdempotencyKeyAsync(string key, string caseId);

        /// <summary>Remove an idempotency key (called when a terminal-state case is superseded).</summary>
        Task RemoveIdempotencyKeyAsync(string key);

        // ── Export log ─────────────────────────────────────────────────────────

        /// <summary>Append an export metadata record so every export is auditable.</summary>
        Task AppendExportRecordAsync(string caseId, CaseExportMetadata metadata);

        /// <summary>Return all export records for the given case, ordered oldest-first.</summary>
        Task<List<CaseExportMetadata>> GetExportRecordsAsync(string caseId);
    }
}

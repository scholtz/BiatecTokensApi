using BiatecTokensApi.Models.KycAmlDecisionIngestion;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Provider-agnostic KYC/AML decision ingestion and evidence retention service.
    ///
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Normalise provider-specific payloads at the ingestion boundary.</item>
    ///   <item>Retain decisions and evidence artefacts with full provenance.</item>
    ///   <item>Enforce fail-closed readiness rules (missing / expired / contradictory evidence → not Ready).</item>
    ///   <item>Expose subject and cohort readiness summaries, timelines, and explicit blockers.</item>
    /// </list>
    /// </summary>
    public interface IKycAmlDecisionIngestionService
    {
        /// <summary>
        /// Ingests a normalised compliance decision from any provider or manual review flow.
        /// Idempotent: repeated calls with the same <see cref="IngestProviderDecisionRequest.IdempotencyKey"/>
        /// return the original record without creating a duplicate.
        /// </summary>
        /// <param name="request">The normalised decision to ingest.</param>
        /// <param name="actorId">Authenticated actor performing the ingestion.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns>
        /// <see cref="IngestProviderDecisionResponse"/> containing the created or replayed decision record.
        /// </returns>
        Task<IngestProviderDecisionResponse> IngestDecisionAsync(
            IngestProviderDecisionRequest request,
            string actorId,
            string correlationId);

        /// <summary>
        /// Retrieves a single ingested decision record by its stable <paramref name="decisionId"/>.
        /// </summary>
        /// <param name="decisionId">The decision identifier.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns>
        /// <see cref="GetIngestionDecisionResponse"/> with the record, or a failed response
        /// with error code <c>INGESTION_DECISION_NOT_FOUND</c>.
        /// </returns>
        Task<GetIngestionDecisionResponse> GetDecisionAsync(
            string decisionId,
            string correlationId);

        /// <summary>
        /// Lists all ingested decision records for a subject, most recent first.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns><see cref="ListSubjectDecisionsResponse"/> with all decisions for the subject.</returns>
        Task<ListSubjectDecisionsResponse> ListSubjectDecisionsAsync(
            string subjectId,
            string correlationId);

        /// <summary>
        /// Returns the merged timeline of events across all ingested decisions for a subject,
        /// ordered most-recent first.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns><see cref="GetSubjectTimelineResponse"/> with the merged event timeline.</returns>
        Task<GetSubjectTimelineResponse> GetSubjectTimelineAsync(
            string subjectId,
            string correlationId);

        /// <summary>
        /// Returns the current explicit blockers and advisories for a subject,
        /// derived by applying fail-closed readiness rules to their ingested decisions.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns><see cref="GetSubjectBlockersResponse"/> with hard blockers, advisories, and aggregate state.</returns>
        Task<GetSubjectBlockersResponse> GetSubjectBlockersAsync(
            string subjectId,
            string correlationId);

        /// <summary>
        /// Computes the aggregated launch readiness for a subject from their ingested decisions.
        /// Fail-closed: any missing, expired, contradictory, or unavailable evidence yields
        /// a non-Ready state.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns><see cref="GetSubjectReadinessResponse"/> with the computed readiness summary.</returns>
        Task<GetSubjectReadinessResponse> GetSubjectReadinessAsync(
            string subjectId,
            string correlationId);

        /// <summary>
        /// Creates or updates a cohort (group of subjects) and returns the resulting cohort ID.
        /// </summary>
        /// <param name="request">Cohort upsert request.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns><see cref="UpsertCohortResponse"/> confirming the upsert.</returns>
        Task<UpsertCohortResponse> UpsertCohortAsync(
            UpsertCohortRequest request,
            string correlationId);

        /// <summary>
        /// Computes cohort-level launch readiness by aggregating per-subject readiness
        /// for all subjects in the cohort.
        /// The overall state is the most severe state across all members (fail-closed).
        /// </summary>
        /// <param name="cohortId">The cohort identifier.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns>
        /// <see cref="GetCohortReadinessResponse"/> with per-subject and aggregate readiness,
        /// or a failed response with error code <c>INGESTION_COHORT_NOT_FOUND</c>.
        /// </returns>
        Task<GetCohortReadinessResponse> GetCohortReadinessAsync(
            string cohortId,
            string correlationId);

        /// <summary>
        /// Appends a reviewer note to an existing ingested decision record.
        /// </summary>
        /// <param name="decisionId">The decision to annotate.</param>
        /// <param name="request">The note content and optional evidence references.</param>
        /// <param name="actorId">Authenticated actor submitting the note.</param>
        /// <param name="correlationId">Correlation ID for distributed tracing.</param>
        /// <returns><see cref="AppendIngestionReviewerNoteResponse"/> with the created note on success.</returns>
        Task<AppendIngestionReviewerNoteResponse> AppendReviewerNoteAsync(
            string decisionId,
            AppendIngestionReviewerNoteRequest request,
            string actorId,
            string correlationId);
    }
}

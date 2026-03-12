using BiatecTokensApi.Models.KycWorkflow;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for the KYC verification workflow foundation layer.
    /// Provides production-grade compliance workflow capabilities including a
    /// deterministic state machine, audit history, evidence management, and eligibility evaluation.
    /// </summary>
    public interface IKycWorkflowService
    {
        // ── State machine ──────────────────────────────────────────────────────

        /// <summary>
        /// Validates whether a state transition is permitted by the workflow rules.
        /// </summary>
        KycStateTransitionValidationResult ValidateTransition(KycVerificationState from, KycVerificationState to);

        /// <summary>
        /// Returns the set of states that can be transitioned to from <paramref name="from"/>.
        /// </summary>
        IReadOnlySet<KycVerificationState> GetAllowedTransitions(KycVerificationState from);

        // ── CRUD ───────────────────────────────────────────────────────────────

        /// <summary>Creates a new KYC verification record in Pending state.</summary>
        Task<KycVerificationResponse> CreateVerificationAsync(CreateKycVerificationRequest request, string actorId, string correlationId);

        /// <summary>Gets a KYC verification record by its ID. Auto-expires if applicable.</summary>
        Task<KycVerificationResponse> GetVerificationAsync(string kycId);

        /// <summary>Gets the most recent active (Approved, Pending, or ManualReviewRequired) record for a participant.</summary>
        Task<KycVerificationResponse> GetActiveVerificationByParticipantAsync(string participantId);

        /// <summary>Transitions a KYC record to a new state, recording the audit trail.</summary>
        Task<KycVerificationResponse> UpdateStatusAsync(string kycId, UpdateKycVerificationStatusRequest request, string actorId, string correlationId);

        // ── History ────────────────────────────────────────────────────────────

        /// <summary>Returns the chronological audit history for a KYC record.</summary>
        Task<KycHistoryResponse> GetHistoryAsync(string kycId);

        // ── Evidence ───────────────────────────────────────────────────────────

        /// <summary>Adds an evidence item to a KYC record.</summary>
        Task<KycEvidenceResponse> AddEvidenceAsync(string kycId, AddKycEvidenceRequest request, string actorId);

        /// <summary>Returns all evidence items for a KYC record.</summary>
        Task<KycEvidenceResponse> GetEvidenceAsync(string kycId);

        // ── Eligibility ────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates whether a participant has an active, non-expired, approved KYC verification.
        /// </summary>
        Task<KycEligibilityResult> EvaluateEligibilityAsync(KycEligibilityRequest request);

        // ── Expiry ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Batch-processes all Approved records that have passed their expiry date,
        /// transitioning them to Expired state.
        /// </summary>
        /// <returns>The number of records that were expired.</returns>
        Task<int> ProcessExpiredVerificationsAsync();
    }
}

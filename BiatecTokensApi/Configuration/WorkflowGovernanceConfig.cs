namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for the policy-driven workflow governance core.
    /// </summary>
    /// <remarks>
    /// This configuration class provides feature-flag and phased-rollout controls
    /// for the orchestration pipeline. Operators can enable or disable specific
    /// governance capabilities without redeploying code.
    ///
    /// Rollout strategy:
    ///   1. Start with <see cref="Enabled"/> = false in staging to validate the pipeline in isolation.
    ///   2. Set <see cref="EnforceValidation"/> = true to activate schema/invariant checks.
    ///   3. Set <see cref="EnforcePreconditions"/> = true to activate KYC/subscription/compliance gates.
    ///   4. Set <see cref="EnforcePostCommitVerification"/> = true to activate on-chain verification.
    ///   5. Set <see cref="Enabled"/> = true in production to fully enable governance.
    ///
    /// Business value: Allows incremental, low-risk activation of governance policies in
    /// production without a big-bang deployment. Each flag gates a specific pipeline stage,
    /// enabling safe canary rollout and fast rollback if issues arise.
    /// </remarks>
    public class WorkflowGovernanceConfig
    {
        /// <summary>
        /// Master switch for the policy-driven orchestration pipeline.
        /// When false, token workflow operations bypass governance checks.
        /// Default: true (governance enabled).
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// When true, Stage 1 (Validate) enforces schema and invariant checks.
        /// When false, validation errors are logged but do not halt the pipeline.
        /// Default: true.
        /// </summary>
        public bool EnforceValidation { get; set; } = true;

        /// <summary>
        /// When true, Stage 2 (CheckPreconditions) enforces KYC, subscription, and compliance gates.
        /// When false, precondition failures are logged but do not halt the pipeline.
        /// Default: true.
        /// </summary>
        public bool EnforcePreconditions { get; set; } = true;

        /// <summary>
        /// When true, Stage 4 (VerifyPostCommit) enforces on-chain/storage verification.
        /// When false, post-commit verification failures are logged but do not fail the result.
        /// Default: true.
        /// </summary>
        public bool EnforcePostCommitVerification { get; set; } = true;

        /// <summary>
        /// Maximum number of retry attempts for transient failures.
        /// Used by retry consumers to cap retry loops.
        /// Default: 5.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 5;

        /// <summary>
        /// Policy version string for audit trail and change-management tracking.
        /// Should be updated with each policy rule change.
        /// </summary>
        public string PolicyVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Optional rollout percentage (0–100).
        /// When less than 100, the governance pipeline is only applied to that percentage of
        /// requests (deterministic hash-based routing for reproducible canary testing).
        /// Default: 100 (all requests governed).
        /// </summary>
        public int RolloutPercentage { get; set; } = 100;
    }
}

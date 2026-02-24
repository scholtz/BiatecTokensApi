namespace BiatecTokensApi.Models.Orchestration
{
    /// <summary>
    /// Represents the stages of the policy-driven token workflow orchestration pipeline
    /// </summary>
    /// <remarks>
    /// The pipeline executes stages in order: Validate → CheckPreconditions → Execute → VerifyPostCommit → EmitTelemetry.
    /// Each stage emits a structured log marker so operators can trace exactly where a workflow
    /// is in the lifecycle and diagnose failures quickly.
    /// </remarks>
    public enum OrchestrationStage
    {
        /// <summary>
        /// Pipeline not yet started
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// Stage 1: Validate request schema and invariant rules.
        /// Deterministic – same inputs produce the same validation outcome.
        /// </summary>
        Validate = 1,

        /// <summary>
        /// Stage 2: Check preconditions such as KYC status, subscription tier, and compliance policies.
        /// Guards execution from proceeding when platform requirements are unmet.
        /// </summary>
        CheckPreconditions = 2,

        /// <summary>
        /// Stage 3: Execute the core token workflow (deploy, mint, transfer, burn, etc.).
        /// Idempotency guarantees are enforced here using the idempotency key.
        /// </summary>
        Execute = 3,

        /// <summary>
        /// Stage 4: Post-commit verification – confirm the executed operation reached the expected state
        /// on the underlying network or storage layer.
        /// </summary>
        VerifyPostCommit = 4,

        /// <summary>
        /// Stage 5: Emit standardized lifecycle telemetry events including correlation ID,
        /// stage markers, policy decisions, and audit summary.
        /// </summary>
        EmitTelemetry = 5,

        /// <summary>
        /// Pipeline completed successfully through all stages
        /// </summary>
        Completed = 6,

        /// <summary>
        /// Pipeline terminated with a failure at one of the stages
        /// </summary>
        Failed = 7
    }
}

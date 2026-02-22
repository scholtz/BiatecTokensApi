using BiatecTokensApi.Models.Orchestration;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Orchestration pipeline for policy-driven token workflow execution.
    /// </summary>
    /// <remarks>
    /// Encapsulates the five-stage pipeline:
    ///   1. Validate   – schema and invariant checks
    ///   2. CheckPreconditions – KYC, subscription, compliance policy gates
    ///   3. Execute    – core token operation (idempotency-guarded)
    ///   4. VerifyPostCommit – confirm operation reached expected terminal state
    ///   5. EmitTelemetry – structured lifecycle events with correlation ID and stage markers
    ///
    /// Every run produces an <see cref="OrchestrationResult{TPayload}"/> that carries full
    /// stage markers, policy decisions, and an audit summary regardless of outcome.
    /// </remarks>
    public interface ITokenWorkflowOrchestrationPipeline
    {
        /// <summary>
        /// Executes a token workflow through the full five-stage pipeline.
        /// </summary>
        /// <typeparam name="TRequest">Type of the incoming request</typeparam>
        /// <typeparam name="TResult">Type of the operation result payload</typeparam>
        /// <param name="context">Orchestration context carrying correlation ID, idempotency key, and user identity</param>
        /// <param name="request">The validated request object</param>
        /// <param name="validationPolicy">
        ///   Delegate that validates the request at Stage 1.
        ///   Return null to indicate success; return a non-null error message to halt the pipeline.
        /// </param>
        /// <param name="preconditionPolicy">
        ///   Delegate that checks preconditions at Stage 2.
        ///   Return null to proceed; return a non-null error message to halt the pipeline.
        /// </param>
        /// <param name="executor">
        ///   Async delegate that executes the core operation at Stage 3.
        ///   Should throw on unrecoverable failures; transient failures are caught and classified.
        /// </param>
        /// <param name="postCommitVerifier">
        ///   Optional async delegate that verifies the result at Stage 4.
        ///   Return null to indicate verification passed; return a non-null message on failure.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for the pipeline execution</param>
        /// <returns>A fully populated <see cref="OrchestrationResult{TResult}"/></returns>
        Task<OrchestrationResult<TResult>> ExecuteAsync<TRequest, TResult>(
            OrchestrationContext context,
            TRequest request,
            Func<TRequest, string?> validationPolicy,
            Func<TRequest, string?> preconditionPolicy,
            Func<TRequest, Task<TResult>> executor,
            Func<TResult, Task<string?>>? postCommitVerifier = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds an <see cref="OrchestrationContext"/> from the provided values.
        /// Callers should pass the correlation ID obtained from <c>HttpContext.TraceIdentifier</c>
        /// and the idempotency key obtained from the <c>Idempotency-Key</c> request header.
        /// </summary>
        /// <param name="operationType">Logical operation type identifier (e.g. "ERC20_MINTABLE_CREATE")</param>
        /// <param name="correlationId">Correlation ID from the request trace identifier</param>
        /// <param name="idempotencyKey">Optional idempotency key extracted from the Idempotency-Key header</param>
        /// <param name="userId">Optional authenticated user identifier</param>
        /// <returns>A populated <see cref="OrchestrationContext"/></returns>
        OrchestrationContext BuildContext(
            string operationType,
            string correlationId,
            string? idempotencyKey = null,
            string? userId = null);
    }
}

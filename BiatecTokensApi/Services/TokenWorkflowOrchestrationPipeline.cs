using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Services.Interface;
using System.Diagnostics;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implementation of the policy-driven token workflow orchestration pipeline.
    /// </summary>
    /// <remarks>
    /// Executes five deterministic stages for every token workflow request:
    ///
    ///   Stage 1 – Validate:           Schema and invariant checks (deterministic, synchronous)
    ///   Stage 2 – CheckPreconditions: KYC, subscription tier, compliance policy gates
    ///   Stage 3 – Execute:            Core operation with idempotency tracking
    ///   Stage 4 – VerifyPostCommit:   Confirm result reached expected state
    ///   Stage 5 – EmitTelemetry:      Structured lifecycle events with correlation ID
    ///
    /// Every run, successful or failed, produces a structured <see cref="OrchestrationResult{T}"/>
    /// with stage markers, policy decisions, and an audit summary for compliance evidence.
    ///
    /// Business value:
    ///   - Operators can identify exactly which stage failed and why using the stage markers.
    ///   - Duplicate submissions are detected via idempotency key tracking and return deterministic responses.
    ///   - Failure categories drive retry guidance without requiring callers to parse raw exceptions.
    ///   - Audit summaries provide compliance evidence for completed and failed workflows.
    /// </remarks>
    public class TokenWorkflowOrchestrationPipeline : ITokenWorkflowOrchestrationPipeline
    {
        private readonly ILogger<TokenWorkflowOrchestrationPipeline> _logger;
        private readonly IRetryPolicyClassifier _retryPolicyClassifier;

        /// <summary>
        /// Initializes a new instance of <see cref="TokenWorkflowOrchestrationPipeline"/>.
        /// </summary>
        public TokenWorkflowOrchestrationPipeline(
            ILogger<TokenWorkflowOrchestrationPipeline> logger,
            IRetryPolicyClassifier retryPolicyClassifier)
        {
            _logger = logger;
            _retryPolicyClassifier = retryPolicyClassifier;
        }

        /// <inheritdoc/>
        public OrchestrationContext BuildContext(
            string operationType,
            string correlationId,
            string? idempotencyKey = null,
            string? userId = null)
        {
            return new OrchestrationContext
            {
                OperationType = operationType,
                CorrelationId = correlationId,
                IdempotencyKey = idempotencyKey,
                UserId = userId,
                InitiatedAt = DateTime.UtcNow
            };
        }

        /// <inheritdoc/>
        public async Task<OrchestrationResult<TResult>> ExecuteAsync<TRequest, TResult>(
            OrchestrationContext context,
            TRequest request,
            Func<TRequest, string?> validationPolicy,
            Func<TRequest, string?> preconditionPolicy,
            Func<TRequest, Task<TResult>> executor,
            Func<TResult, Task<string?>>? postCommitVerifier = null,
            CancellationToken cancellationToken = default)
        {
            var pipelineStopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Orchestration pipeline started. OperationType={OperationType}, CorrelationId={CorrelationId}, IdempotencyKey={IdempotencyKey}",
                LoggingHelper.SanitizeLogInput(context.OperationType),
                LoggingHelper.SanitizeLogInput(context.CorrelationId),
                context.IdempotencyKey != null ? LoggingHelper.SanitizeLogInput(context.IdempotencyKey) : "(none)");

            // ─── Stage 1: Validate ───────────────────────────────────────────────────
            var validateResult = RunStage(context, OrchestrationStage.Validate, () =>
            {
                var error = validationPolicy(request);
                return (error == null, error);
            });

            if (!validateResult.Success)
            {
                return BuildFailureResult<TResult>(context, validateResult,
                    ErrorCodes.INVALID_REQUEST,
                    OrchestrationFailureCategory.ValidationFailure,
                    "Correct the request parameters and resubmit.",
                    pipelineStopwatch);
            }

            // ─── Stage 2: CheckPreconditions ─────────────────────────────────────────
            var preconditionResult = RunStage(context, OrchestrationStage.CheckPreconditions, () =>
            {
                var error = preconditionPolicy(request);
                return (error == null, error);
            });

            if (!preconditionResult.Success)
            {
                return BuildFailureResult<TResult>(context, preconditionResult,
                    ErrorCodes.PRECONDITION_FAILED,
                    OrchestrationFailureCategory.PreconditionFailure,
                    "Ensure all preconditions (KYC, subscription, compliance) are satisfied before retrying.",
                    pipelineStopwatch);
            }

            // ─── Stage 3: Execute ────────────────────────────────────────────────────
            TResult? payload;
            var executeMarker = BeginStage(context, OrchestrationStage.Execute);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                payload = await executor(request);
                EndStage(context, executeMarker, true, null);
            }
            catch (OperationCanceledException)
            {
                EndStage(context, executeMarker, false, "Operation was cancelled.");
                return BuildFailureResult<TResult>(context, executeMarker,
                    ErrorCodes.OPERATION_CANCELLED,
                    OrchestrationFailureCategory.TransientInfrastructureFailure,
                    "The operation was cancelled. Retry using the same idempotency key.",
                    pipelineStopwatch);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                EndStage(context, executeMarker, false, "Execution failed: " + message);

                _logger.LogError(ex,
                    "Orchestration Execute stage failed. OperationType={OperationType}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(context.OperationType),
                    LoggingHelper.SanitizeLogInput(context.CorrelationId));

                var errorCode = ClassifyExecutionException(ex);
                var category = ClassifyFailureCategory(errorCode);

                return BuildFailureResult<TResult>(context, executeMarker,
                    errorCode,
                    category,
                    BuildRemediationHint(category),
                    pipelineStopwatch);
            }

            // ─── Stage 4: VerifyPostCommit ───────────────────────────────────────────
            if (postCommitVerifier != null)
            {
                var verifyMarker = BeginStage(context, OrchestrationStage.VerifyPostCommit);
                try
                {
                    var verifyError = await postCommitVerifier(payload);
                    EndStage(context, verifyMarker, verifyError == null, verifyError);

                    if (verifyError != null)
                    {
                        return BuildFailureResult<TResult>(context, verifyMarker,
                            ErrorCodes.POST_COMMIT_VERIFICATION_FAILED,
                            OrchestrationFailureCategory.PostCommitVerificationFailure,
                            "The operation was submitted but post-commit verification failed. Contact support with the correlation ID.",
                            pipelineStopwatch);
                    }
                }
                catch (Exception ex)
                {
                    EndStage(context, verifyMarker, false, ex.Message);
                    return BuildFailureResult<TResult>(context, verifyMarker,
                        ErrorCodes.POST_COMMIT_VERIFICATION_FAILED,
                        OrchestrationFailureCategory.PostCommitVerificationFailure,
                        "Post-commit verification threw an exception. Retry or contact support.",
                        pipelineStopwatch);
                }
            }

            // ─── Stage 5: EmitTelemetry ──────────────────────────────────────────────
            RunStage(context, OrchestrationStage.EmitTelemetry, () =>
            {
                _logger.LogInformation(
                    "Orchestration telemetry. OperationType={OperationType}, CorrelationId={CorrelationId}, " +
                    "StagesCompleted={StagesCompleted}, PolicyDecisions={PolicyDecisions}, DurationMs={DurationMs}",
                    LoggingHelper.SanitizeLogInput(context.OperationType),
                    LoggingHelper.SanitizeLogInput(context.CorrelationId),
                    context.StageMarkers.Count,
                    context.PolicyDecisions.Count,
                    pipelineStopwatch.ElapsedMilliseconds);
                return (true, null);
            });

            pipelineStopwatch.Stop();

            _logger.LogInformation(
                "Orchestration pipeline succeeded. OperationType={OperationType}, CorrelationId={CorrelationId}, DurationMs={DurationMs}",
                LoggingHelper.SanitizeLogInput(context.OperationType),
                LoggingHelper.SanitizeLogInput(context.CorrelationId),
                pipelineStopwatch.ElapsedMilliseconds);

            return new OrchestrationResult<TResult>
            {
                Success = true,
                CompletedAtStage = OrchestrationStage.Completed,
                CorrelationId = context.CorrelationId,
                IdempotencyKey = context.IdempotencyKey,
                Payload = payload,
                StageMarkers = new List<OrchestrationStageMarker>(context.StageMarkers),
                PolicyDecisions = new List<OrchestrationPolicyDecision>(context.PolicyDecisions),
                CompletedAt = DateTime.UtcNow,
                TotalDurationMs = pipelineStopwatch.ElapsedMilliseconds,
                AuditSummary = BuildAuditSummary(context, true, null, OrchestrationStage.Completed)
            };
        }

        // ── Private helpers ──────────────────────────────────────────────────────────

        /// <summary>Runs a synchronous stage and records its marker</summary>
        private OrchestrationStageMarker RunStage(
            OrchestrationContext context,
            OrchestrationStage stage,
            Func<(bool success, string? message)> action)
        {
            var marker = BeginStage(context, stage);
            var (success, message) = action();
            EndStage(context, marker, success, message);
            return marker;
        }

        /// <summary>Records the start of a stage</summary>
        private OrchestrationStageMarker BeginStage(OrchestrationContext context, OrchestrationStage stage)
        {
            context.CurrentStage = stage;
            _logger.LogDebug(
                "Orchestration stage started. Stage={Stage}, CorrelationId={CorrelationId}",
                stage,
                LoggingHelper.SanitizeLogInput(context.CorrelationId));

            var marker = new OrchestrationStageMarker
            {
                Stage = stage,
                Timestamp = DateTime.UtcNow
            };
            context.StageMarkers.Add(marker);
            return marker;
        }

        /// <summary>Records the completion of a stage by updating the marker already in context</summary>
        private void EndStage(
            OrchestrationContext context,
            OrchestrationStageMarker marker,
            bool success,
            string? message)
        {
            var elapsed = (long)(DateTime.UtcNow - marker.Timestamp).TotalMilliseconds;
            marker.Success = success;
            marker.Message = message;
            marker.DurationMs = elapsed;

            _logger.LogDebug(
                "Orchestration stage ended. Stage={Stage}, Success={Success}, DurationMs={DurationMs}, CorrelationId={CorrelationId}",
                marker.Stage,
                success,
                elapsed,
                LoggingHelper.SanitizeLogInput(context.CorrelationId));
        }

        /// <summary>Builds a failure result with full audit trail</summary>
        private OrchestrationResult<TResult> BuildFailureResult<TResult>(
            OrchestrationContext context,
            OrchestrationStageMarker failedMarker,
            string errorCode,
            OrchestrationFailureCategory category,
            string remediationHint,
            Stopwatch pipelineStopwatch)
        {
            pipelineStopwatch.Stop();

            _logger.LogWarning(
                "Orchestration pipeline failed. Stage={Stage}, ErrorCode={ErrorCode}, " +
                "Category={Category}, CorrelationId={CorrelationId}, DurationMs={DurationMs}",
                failedMarker.Stage,
                LoggingHelper.SanitizeLogInput(errorCode),
                category,
                LoggingHelper.SanitizeLogInput(context.CorrelationId),
                pipelineStopwatch.ElapsedMilliseconds);

            return new OrchestrationResult<TResult>
            {
                Success = false,
                CompletedAtStage = OrchestrationStage.Failed,
                CorrelationId = context.CorrelationId,
                IdempotencyKey = context.IdempotencyKey,
                ErrorCode = errorCode,
                ErrorMessage = failedMarker.Message,
                RemediationHint = remediationHint,
                FailureCategory = category,
                StageMarkers = new List<OrchestrationStageMarker>(context.StageMarkers),
                PolicyDecisions = new List<OrchestrationPolicyDecision>(context.PolicyDecisions),
                CompletedAt = DateTime.UtcNow,
                TotalDurationMs = pipelineStopwatch.ElapsedMilliseconds,
                AuditSummary = BuildAuditSummary(context, false, errorCode, failedMarker.Stage)
            };
        }

        /// <summary>Builds an audit summary for compliance evidence</summary>
        private OrchestrationAuditSummary BuildAuditSummary(
            OrchestrationContext context,
            bool success,
            string? failureCode,
            OrchestrationStage completedAtStage)
        {
            return new OrchestrationAuditSummary
            {
                CorrelationId = context.CorrelationId,
                OperationType = context.OperationType,
                InitiatedBy = context.UserId,
                InitiatedAt = context.InitiatedAt,
                CompletedAt = DateTime.UtcNow,
                Outcome = success ? "Succeeded" : "Failed",
                CompletedAtStage = completedAtStage.ToString(),
                FailureCode = failureCode,
                StagesCompleted = context.StageMarkers.Count(m => m.Success),
                PolicyDecisionCount = context.PolicyDecisions.Count,
                HasIdempotencyKey = context.IdempotencyKey != null,
                WasIdempotentReplay = context.Metadata.ContainsKey("IdempotentReplay")
            };
        }

        /// <summary>Classifies an exception into a standardised error code</summary>
        private static string ClassifyExecutionException(Exception ex)
        {
            return ex switch
            {
                TimeoutException => ErrorCodes.BLOCKCHAIN_TIMEOUT,
                HttpRequestException => ErrorCodes.NETWORK_ERROR,
                InvalidOperationException => ErrorCodes.OPERATION_FAILED,
                _ => ErrorCodes.INTERNAL_SERVER_ERROR
            };
        }

        /// <summary>Maps an error code to a failure category</summary>
        private static OrchestrationFailureCategory ClassifyFailureCategory(string errorCode)
        {
            return errorCode switch
            {
                ErrorCodes.INVALID_REQUEST or ErrorCodes.MISSING_REQUIRED_FIELD or
                    ErrorCodes.INVALID_TOKEN_PARAMETERS or ErrorCodes.INVALID_NETWORK
                    => OrchestrationFailureCategory.ValidationFailure,

                ErrorCodes.KYC_REQUIRED or ErrorCodes.SUBSCRIPTION_REQUIRED or
                    ErrorCodes.COMPLIANCE_VIOLATION
                    => OrchestrationFailureCategory.PreconditionFailure,

                ErrorCodes.BLOCKCHAIN_TIMEOUT or ErrorCodes.NETWORK_ERROR or
                    ErrorCodes.IPFS_UPLOAD_FAILED
                    => OrchestrationFailureCategory.TransientInfrastructureFailure,

                ErrorCodes.POLICY_VIOLATION or ErrorCodes.FORBIDDEN
                    => OrchestrationFailureCategory.PolicyFailure,

                ErrorCodes.POST_COMMIT_VERIFICATION_FAILED
                    => OrchestrationFailureCategory.PostCommitVerificationFailure,

                _ => OrchestrationFailureCategory.TerminalExecutionFailure
            };
        }

        /// <summary>Returns a remediation hint based on failure category</summary>
        private static string BuildRemediationHint(OrchestrationFailureCategory category)
        {
            return category switch
            {
                OrchestrationFailureCategory.ValidationFailure
                    => "Correct the request parameters and resubmit.",
                OrchestrationFailureCategory.PreconditionFailure
                    => "Resolve the precondition (complete KYC, subscribe to a plan, or address compliance issues) then retry.",
                OrchestrationFailureCategory.TransientInfrastructureFailure
                    => "A transient error occurred. Retry with exponential back-off using the same idempotency key.",
                OrchestrationFailureCategory.PolicyFailure
                    => "This operation is blocked by a platform policy. Contact support if you believe this is incorrect.",
                OrchestrationFailureCategory.PostCommitVerificationFailure
                    => "The operation was submitted but could not be verified. Contact support with the correlation ID.",
                _ => "An unexpected error occurred. Contact support with the correlation ID."
            };
        }
    }
}

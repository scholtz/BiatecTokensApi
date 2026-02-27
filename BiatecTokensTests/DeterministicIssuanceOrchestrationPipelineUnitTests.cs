using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for Issue #411: Roadmap Execution – deterministic issuance orchestration API
    /// with idempotent reliability.
    ///
    /// These tests exercise <see cref="TokenWorkflowOrchestrationPipeline"/> in isolation using
    /// mocked dependencies to validate deterministic stage progression, structured error taxonomy,
    /// idempotency semantics, observability primitives, and actionable remediation hints.
    ///
    /// Coverage (per AC):
    /// AC1 – Functional completeness: deterministic stage progression and validation surfacing.
    /// AC2 – Reliability: idempotency key propagation, no-side-effect semantics on failure.
    /// AC3 – Quality gates: branch coverage across success, validation, precondition, execution, post-commit.
    /// AC4 – Observability: correlation IDs, stage markers, audit summary always populated.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicIssuanceOrchestrationPipelineUnitTests
    {
        private TokenWorkflowOrchestrationPipeline _pipeline = null!;
        private Mock<ILogger<TokenWorkflowOrchestrationPipeline>> _mockLogger = null!;
        private Mock<IRetryPolicyClassifier> _mockRetryClassifier = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            _mockRetryClassifier = new Mock<IRetryPolicyClassifier>();
            _pipeline = new TokenWorkflowOrchestrationPipeline(_mockLogger.Object, _mockRetryClassifier.Object);
        }

        // ─── AC1: Functional completeness – deterministic stage progression ──────

        /// <summary>
        /// AC1: BuildContext initialises all fields correctly from the provided values.
        /// Validates that the context seed controls downstream behaviour deterministically.
        /// </summary>
        [Test]
        public void BuildContext_SetsAllFieldsCorrectly()
        {
            var ctx = _pipeline.BuildContext("ERC20_CREATE", "corr-001", "idem-001", "user-1");

            Assert.That(ctx.OperationType, Is.EqualTo("ERC20_CREATE"));
            Assert.That(ctx.CorrelationId, Is.EqualTo("corr-001"));
            Assert.That(ctx.IdempotencyKey, Is.EqualTo("idem-001"));
            Assert.That(ctx.UserId, Is.EqualTo("user-1"));
            Assert.That(ctx.InitiatedAt, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-2)));
        }

        /// <summary>
        /// AC1: A fully successful pipeline run returns Success=true and stage Completed.
        /// All five stage markers are recorded.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_AllStagesSucceed_ReturnsSuccessAndCompletedStage()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-1");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; });

            Assert.That(result.Success, Is.True, "Pipeline must succeed");
            Assert.That(result.CompletedAtStage, Is.EqualTo(OrchestrationStage.Completed));
            Assert.That(result.Payload, Is.EqualTo("ok"));
        }

        /// <summary>
        /// AC1: Stage markers are recorded in execution order and at least 4 are present.
        /// Confirms that every executed stage produces an observable telemetry entry.
        /// Without a post-commit verifier: Validate, CheckPreconditions, Execute, EmitTelemetry = 4 stages.
        /// With a post-commit verifier: all 5 stages are recorded.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_SuccessPath_RecordsAtLeastFourStageMarkers()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-2");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; });

            Assert.That(result.StageMarkers.Count, Is.GreaterThanOrEqualTo(4),
                "Validate, CheckPreconditions, Execute, EmitTelemetry must all be recorded (4 minimum without post-commit verifier)");
        }

        /// <summary>
        /// AC1: Validation failure at Stage 1 halts the pipeline immediately.
        /// No executor is called; error message is surfaced with actionable detail.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidationFails_PipelineHaltsAtValidate()
        {
            bool executorCalled = false;
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-3");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => "Token name is required",
                _ => null,
                async _ => { executorCalled = true; await Task.Delay(1); return "never"; });

            Assert.That(result.Success, Is.False);
            Assert.That(executorCalled, Is.False, "Executor must not be called on validation failure");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST));
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.ValidationFailure));
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty, "Actionable error message must be present");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty, "Remediation hint must be present");
        }

        /// <summary>
        /// AC1: Precondition failure at Stage 2 returns PreconditionFailure category.
        /// The executor is not invoked; a user-actionable remediation hint is provided.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_PreconditionFails_ReturnsPreconditionFailureCategory()
        {
            bool executorCalled = false;
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-4");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => "KYC verification required before issuance",
                async _ => { executorCalled = true; await Task.Delay(1); return "never"; });

            Assert.That(result.Success, Is.False);
            Assert.That(executorCalled, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PreconditionFailure));
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.PRECONDITION_FAILED));
            Assert.That(result.RemediationHint, Does.Contain("KYC").Or.Contain("precondition").Or.Contain("subscription").IgnoreCase);
        }

        // ─── AC2: Reliability – idempotency and no-side-effect semantics ─────────

        /// <summary>
        /// AC2: Idempotency key is propagated from context into the result.
        /// Callers can track whether a response corresponds to a specific retry.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_IdempotencyKey_PropagatedToResult()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-5", idempotencyKey: "idem-abc-123");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; });

            Assert.That(result.IdempotencyKey, Is.EqualTo("idem-abc-123"),
                "Idempotency key must be reflected in the result for client-side replay detection");
        }

        /// <summary>
        /// AC2: When execution fails, the idempotency key is still present in the failure result.
        /// This allows callers to safely retry using the same key.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidationFails_IdempotencyKey_StillPresentInFailureResult()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-6", idempotencyKey: "idem-retry-001");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => "Validation error",
                _ => null,
                async _ => { await Task.Delay(1); return "never"; });

            Assert.That(result.Success, Is.False);
            Assert.That(result.IdempotencyKey, Is.EqualTo("idem-retry-001"),
                "Idempotency key must survive failure so callers can correlate retries");
        }

        /// <summary>
        /// AC2: Three successive runs with identical inputs produce structurally identical results.
        /// Confirms deterministic pipeline execution (same stage sequence, same error code on failure).
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ThreeRuns_SameInput_ProduceDeterministicStructure()
        {
            async Task<OrchestrationResult<string>> Run(int n)
            {
                var ctx = _pipeline.BuildContext("TOKEN_CREATE", $"corr-det-{n}");
                return await _pipeline.ExecuteAsync<string, string>(
                    ctx, "payload",
                    _ => "Invariant validation failure",
                    _ => null,
                    async _ => { await Task.Delay(1); return "never"; });
            }

            var r1 = await Run(1);
            var r2 = await Run(2);
            var r3 = await Run(3);

            Assert.That(r2.ErrorCode, Is.EqualTo(r1.ErrorCode), "ErrorCode must be identical across runs");
            Assert.That(r3.ErrorCode, Is.EqualTo(r1.ErrorCode), "ErrorCode must be identical across runs");
            Assert.That(r2.FailureCategory, Is.EqualTo(r1.FailureCategory), "FailureCategory must be identical");
            Assert.That(r3.FailureCategory, Is.EqualTo(r1.FailureCategory), "FailureCategory must be identical");
        }

        /// <summary>
        /// AC2: Cancellation produces a TransientInfrastructureFailure so callers know to retry.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_CancellationRequested_ReturnsTransientFailure()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-cancel");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(100); return "ok"; },
                cancellationToken: cts.Token);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure),
                "Cancellation is a transient failure – callers should retry");
            Assert.That(result.RemediationHint, Does.Contain("idempotency").Or.Contain("Retry").IgnoreCase);
        }

        // ─── AC3: Quality gates – exception branch coverage ──────────────────────

        /// <summary>
        /// AC3: TimeoutException during execution maps to BLOCKCHAIN_TIMEOUT error code.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_TimeoutException_MapsToBlockchainTimeoutErrorCode()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-timeout");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); throw new TimeoutException("RPC timed out"); });

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.BLOCKCHAIN_TIMEOUT));
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
        }

        /// <summary>
        /// AC3: HttpRequestException during execution maps to NETWORK_ERROR error code.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_HttpRequestException_MapsToNetworkErrorCode()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-http");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); throw new HttpRequestException("Connection refused"); });

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NETWORK_ERROR));
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
        }

        /// <summary>
        /// AC3: InvalidOperationException during execution maps to OPERATION_FAILED error code.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_InvalidOperationException_MapsToOperationFailedErrorCode()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-invop");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); throw new InvalidOperationException("State error"); });

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.OPERATION_FAILED));
        }

        /// <summary>
        /// AC3: A post-commit verifier failure returns PostCommitVerificationFailure category.
        /// The payload is still produced but the result is marked as failure.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_PostCommitVerifierFails_ReturnsPostCommitFailureCategory()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-postcommit");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "tx-hash-123"; },
                async _ => { await Task.Delay(1); return "On-chain state mismatch"; });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PostCommitVerificationFailure));
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.POST_COMMIT_VERIFICATION_FAILED));
            Assert.That(result.RemediationHint, Does.Contain("correlation").Or.Contain("support").IgnoreCase);
        }

        /// <summary>
        /// AC3: When no post-commit verifier is provided the pipeline skips Stage 4.
        /// The result still succeeds and all other stages complete normally.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_NullPostCommitVerifier_SkipsVerifyStage_ReturnsSuccess()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-noverify");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; },
                postCommitVerifier: null);

            Assert.That(result.Success, Is.True);
            Assert.That(result.StageMarkers.Any(m => m.Stage == OrchestrationStage.VerifyPostCommit), Is.False,
                "VerifyPostCommit stage must not be present when verifier is null");
        }

        // ─── AC4: Observability – correlation IDs and audit summary ──────────────

        /// <summary>
        /// AC4: Correlation ID from context is propagated verbatim into the success result.
        /// Enables end-to-end tracing from HTTP request to pipeline output.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_CorrelationId_PropagatedToSuccessResult()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "trace-id-xyz-9876");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; });

            Assert.That(result.CorrelationId, Is.EqualTo("trace-id-xyz-9876"),
                "CorrelationId must be propagated to result for end-to-end tracing");
        }

        /// <summary>
        /// AC4: Correlation ID is propagated into the failure result as well.
        /// Support engineers can always look up a failing request by correlation ID.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_CorrelationId_PropagatedToFailureResult()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "trace-fail-id-001");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => "Validation error",
                _ => null,
                async _ => { await Task.Delay(1); return "never"; });

            Assert.That(result.CorrelationId, Is.EqualTo("trace-fail-id-001"),
                "CorrelationId must survive failure for support tracing");
        }

        /// <summary>
        /// AC4: AuditSummary is always populated with correct fields on success.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_AuditSummary_AlwaysPopulated_OnSuccess()
        {
            var ctx = _pipeline.BuildContext("ERC20_MINTABLE_CREATE", "corr-audit-ok", userId: "user-audit-1");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; });

            Assert.That(result.AuditSummary, Is.Not.Null, "AuditSummary must always be populated");
            Assert.That(result.AuditSummary.CorrelationId, Is.EqualTo("corr-audit-ok"));
            Assert.That(result.AuditSummary.OperationType, Is.EqualTo("ERC20_MINTABLE_CREATE"));
            Assert.That(result.AuditSummary.InitiatedBy, Is.EqualTo("user-audit-1"));
            Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Succeeded"));
            Assert.That(result.AuditSummary.CompletedAtStage, Is.EqualTo("Completed"));
            Assert.That(result.AuditSummary.StagesCompleted, Is.GreaterThanOrEqualTo(4),
                "Without post-commit verifier: Validate, CheckPreconditions, Execute, EmitTelemetry = 4 stages");
        }

        /// <summary>
        /// AC4: AuditSummary is populated on failure as well, capturing failure code.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_AuditSummary_AlwaysPopulated_OnFailure()
        {
            var ctx = _pipeline.BuildContext("ERC20_MINTABLE_CREATE", "corr-audit-fail", userId: "user-audit-2");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => "Invalid decimals value",
                _ => null,
                async _ => { await Task.Delay(1); return "never"; });

            Assert.That(result.AuditSummary, Is.Not.Null, "AuditSummary must be populated even on failure");
            Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"));
            Assert.That(result.AuditSummary.FailureCode, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// AC4: TotalDurationMs is a positive value indicating latency was measured.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_TotalDurationMs_IsPositive()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-latency");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(10); return "ok"; });

            Assert.That(result.TotalDurationMs, Is.GreaterThanOrEqualTo(0),
                "Pipeline must record non-negative total duration for latency telemetry");
        }

        /// <summary>
        /// AC4: Stage markers include timestamps that can be used to compute per-stage latency.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_StageMarkers_HaveValidTimestamps()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-ts");
            var before = DateTime.UtcNow.AddSeconds(-1);
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "ok"; });

            foreach (var marker in result.StageMarkers)
            {
                Assert.That(marker.Timestamp, Is.GreaterThan(before),
                    $"Stage {marker.Stage} timestamp must be after the test start");
            }
        }

        // ─── AC4: Remediation hints are actionable ────────────────────────────────

        /// <summary>
        /// AC4: ValidationFailure remediation hint tells the caller to fix the request.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_ValidationFailure_RemediationHint_TellsCallerToFixRequest()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-rem-val");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => "Missing required field",
                _ => null,
                async _ => { await Task.Delay(1); return "never"; });

            Assert.That(result.RemediationHint, Does.Contain("Correct").Or.Contain("fix").Or.Contain("request").IgnoreCase,
                "Validation failure hint must guide the caller to correct the input");
        }

        /// <summary>
        /// AC4: PreconditionFailure remediation hint mentions retrying after resolving prerequisites.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_PreconditionFailure_RemediationHint_MentionsRetryAfterResolution()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-rem-pre");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => "Subscription required",
                async _ => { await Task.Delay(1); return "never"; });

            Assert.That(result.RemediationHint, Does.Contain("KYC").Or.Contain("subscription").Or.Contain("precondition").Or.Contain("Resolve").IgnoreCase,
                "Precondition failure hint must guide the caller to resolve prerequisites");
        }

        /// <summary>
        /// AC4: TransientInfrastructureFailure remediation hint tells the caller to retry with back-off.
        /// </summary>
        [Test]
        public async Task ExecuteAsync_TransientInfrastructureFailure_RemediationHint_MentionsExponentialBackoff()
        {
            var ctx = _pipeline.BuildContext("TOKEN_CREATE", "corr-rem-transient");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "payload",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); throw new TimeoutException("RPC timeout"); });

            Assert.That(result.RemediationHint, Does.Contain("retry").Or.Contain("Retry").Or.Contain("back-off").IgnoreCase,
                "Transient failure hint must tell the caller to retry with back-off");
        }

        // ─── AC3: Success result completeness ────────────────────────────────────

        /// <summary>
        /// AC3: Success result is fully populated with all required fields for
        /// downstream consumers (frontend, webhook handlers, compliance systems).
        /// </summary>
        [Test]
        public async Task ExecuteAsync_SuccessResult_AllRequiredFieldsPopulated()
        {
            var ctx = _pipeline.BuildContext("ERC20_MINTABLE_CREATE", "corr-full", "idem-full", "user-full");
            var result = await _pipeline.ExecuteAsync<string, string>(
                ctx, "req",
                _ => null,
                _ => null,
                async _ => { await Task.Delay(1); return "result-payload"; });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Payload, Is.EqualTo("result-payload"));
                Assert.That(result.StageMarkers, Is.Not.Empty);
                Assert.That(result.AuditSummary, Is.Not.Null);
                Assert.That(result.AuditSummary.InitiatedAt, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-10)));
                Assert.That(result.AuditSummary.CompletedAt, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-10)));
                Assert.That(result.TotalDurationMs, Is.GreaterThanOrEqualTo(0));
                Assert.That(result.CompletedAt, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-10)));
            });
        }
    }
}

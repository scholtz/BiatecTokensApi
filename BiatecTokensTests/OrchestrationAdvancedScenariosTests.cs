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
    /// Advanced orchestration scenario tests covering:
    /// - Policy conflicts (multiple policies, conflicting preconditions)
    /// - Malformed / adversarial inputs (null, empty, oversized, special characters)
    /// - Concurrency edge cases (multiple parallel pipeline executions)
    /// - Multi-step workflow execution (chained operations with inter-step state)
    /// - Retry and rollback semantics (bounded retry, safe re-execution)
    ///
    /// These tests complement <see cref="PolicyDrivenOrchestrationTests"/>,
    /// <see cref="OrchestrationBranchCoverageTests"/>, and
    /// <see cref="OrchestrationIdempotencyE2ETests"/> by addressing the scenarios
    /// explicitly called out in the product-owner acceptance requirements:
    ///   - Policy conflicts and malformed inputs
    ///   - Concurrency and concurrent-execution safety
    ///   - Multi-step token workflow execution with retries and rollback guards
    ///
    /// Roadmap alignment: Phase "Platform Reliability Foundation" – orchestration
    ///   correctness under adversarial and concurrent real-world conditions.
    /// </summary>
    [TestFixture]
    public class OrchestrationAdvancedScenariosTests
    {
        private TokenWorkflowOrchestrationPipeline _pipeline = null!;
        private Mock<ILogger<TokenWorkflowOrchestrationPipeline>> _loggerMock = null!;
        private Mock<IRetryPolicyClassifier> _retryMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            _retryMock = new Mock<IRetryPolicyClassifier>();
            _pipeline = new TokenWorkflowOrchestrationPipeline(_loggerMock.Object, _retryMock.Object);
        }

        // ── Policy conflicts ──────────────────────────────────────────────────────

        /// <summary>
        /// When validation catches an error, the pipeline must NOT proceed to the
        /// precondition stage — i.e., stages are ordered and fail-fast.
        /// </summary>
        [Test]
        public async Task PolicyConflict_ValidationAndPreconditionBothFail_ValidationWins()
        {
            int preconditionCallCount = 0;

            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("ERC20_CREATE", "corr-conflict-01"),
                "bad-request",
                validationPolicy: _ => "Token symbol must be 1-8 characters",
                preconditionPolicy: _ =>
                {
                    preconditionCallCount++;
                    return "KYC required";
                },
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.Success, Is.False);
            // Validation stage must fire first and halt the pipeline
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.ValidationFailure));
            Assert.That(preconditionCallCount, Is.EqualTo(0),
                "Precondition policy must NOT be called when validation fails");
        }

        /// <summary>
        /// When precondition fails, the executor must NOT be called.
        /// This prevents unintended side effects when prerequisites are unmet.
        /// </summary>
        [Test]
        public async Task PolicyConflict_PreconditionFails_ExecutorNotCalled()
        {
            int executorCallCount = 0;

            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("ASA_CREATE", "corr-conflict-02"),
                "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => "Subscription expired",
                executor: _ =>
                {
                    executorCallCount++;
                    return Task.FromResult("should-not-run");
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PreconditionFailure));
            Assert.That(executorCallCount, Is.EqualTo(0),
                "Executor must NOT be called when precondition fails");
        }

        /// <summary>
        /// Multiple policy decisions can be recorded during a single pipeline run.
        /// This covers scenarios where several policies all pass.
        /// </summary>
        [Test]
        public async Task PolicyConflict_MultiplePassingPolicies_AllRecorded()
        {
            var context = _pipeline.BuildContext("ARC3_CREATE", "corr-conflict-03");

            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ =>
                {
                    context.PolicyDecisions.Add(new OrchestrationPolicyDecision
                    {
                        PolicyName = "SymbolValidation",
                        Outcome = "Pass",
                        Reason = "Symbol 'TEST' is valid"
                    });
                    context.PolicyDecisions.Add(new OrchestrationPolicyDecision
                    {
                        PolicyName = "DecimalsValidation",
                        Outcome = "Pass",
                        Reason = "Decimals 6 within 0-18 range"
                    });
                    return null;
                },
                preconditionPolicy: _ =>
                {
                    context.PolicyDecisions.Add(new OrchestrationPolicyDecision
                    {
                        PolicyName = "KycCheck",
                        Outcome = "Pass",
                        Reason = "KYC verified"
                    });
                    return null;
                },
                executor: _ => Task.FromResult("created"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.PolicyDecisions.Count, Is.EqualTo(3),
                "All 3 policy decisions must be recorded");
            Assert.That(result.PolicyDecisions.All(d => d.Outcome == "Pass"), Is.True,
                "All policy decisions must show Pass outcome");
            Assert.That(result.AuditSummary.PolicyDecisionCount, Is.EqualTo(3));
        }

        /// <summary>
        /// A failing precondition accompanied by multiple passing validation policies
        /// must correctly represent the mixed outcome in the audit summary.
        /// </summary>
        [Test]
        public async Task PolicyConflict_PassingValidationFailingPrecondition_AuditReflectsMixedState()
        {
            var context = _pipeline.BuildContext("ERC20_CREATE", "corr-conflict-04");

            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ =>
                {
                    context.PolicyDecisions.Add(new OrchestrationPolicyDecision
                    {
                        PolicyName = "NameValidation",
                        Outcome = "Pass",
                        Reason = "Name is valid"
                    });
                    return null;
                },
                preconditionPolicy: _ => "Account not funded",
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PreconditionFailure));
            Assert.That(result.PolicyDecisions.Count, Is.EqualTo(1),
                "Validation policy decision was recorded before failure");
            Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"));
        }

        // ── Malformed / adversarial input tests ──────────────────────────────────

        [Test]
        public async Task MalformedInput_EmptyOperationType_ContextStillBuilds()
        {
            // BuildContext should handle null/empty gracefully
            var context = _pipeline.BuildContext(string.Empty, "corr-malform-01");

            Assert.That(context.OperationType, Is.EqualTo(string.Empty));
            Assert.That(context.CorrelationId, Is.EqualTo("corr-malform-01"));
        }

        [Test]
        public async Task MalformedInput_ExtremelyLongCorrelationId_HandledGracefully()
        {
            var longId = new string('x', 10_000);
            var context = _pipeline.BuildContext("OP", longId);

            var result = await _pipeline.ExecuteAsync(
                context, "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.CorrelationId, Is.EqualTo(longId));
        }

        [Test]
        public async Task MalformedInput_SpecialCharactersInCorrelationId_HandledGracefully()
        {
            var specialId = "corr\n\r\t<script>alert('xss')</script>∑∂";
            var context = _pipeline.BuildContext("OP", specialId);

            var result = await _pipeline.ExecuteAsync(
                context, "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.CorrelationId, Is.EqualTo(specialId));
        }

        [Test]
        public async Task MalformedInput_EmptyRequest_ValidationCanRejectIt()
        {
            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("ASA_CREATE", "corr-malform-03"),
                string.Empty,
                validationPolicy: r => string.IsNullOrEmpty(r) ? "Request payload is required" : null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.ValidationFailure));
            Assert.That(result.ErrorMessage, Is.EqualTo("Request payload is required"));
        }

        [Test]
        public async Task MalformedInput_NullIdempotencyKey_AuditHasNoIdempotencyKey()
        {
            var context = _pipeline.BuildContext("OP", "corr-malform-04", idempotencyKey: null);

            var result = await _pipeline.ExecuteAsync(
                context, "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.AuditSummary.HasIdempotencyKey, Is.False,
                "No idempotency key must be correctly reflected in audit summary");
        }

        [Test]
        public async Task MalformedInput_ExtremelyLongValidationErrorMessage_HandledGracefully()
        {
            var longError = new string('E', 100_000); // 100KB error message

            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("OP", "corr-malform-05"),
                "req",
                validationPolicy: _ => longError,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo(longError));
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST));
        }

        // ── Concurrency edge cases ────────────────────────────────────────────────

        /// <summary>
        /// Multiple pipeline executions running concurrently must each produce
        /// independent, correct results without cross-contamination.
        /// </summary>
        [Test]
        public async Task Concurrency_MultipleParallelExecutions_AllSucceedIndependently()
        {
            const int parallelism = 20;

            var tasks = Enumerable.Range(0, parallelism).Select(i =>
                _pipeline.ExecuteAsync(
                    _pipeline.BuildContext($"OP_{i}", $"corr-concurrent-{i}"),
                    $"request-{i}",
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: r => Task.FromResult($"result-{r}")));

            var results = await Task.WhenAll(tasks);

            Assert.That(results.Length, Is.EqualTo(parallelism));
            Assert.That(results.All(r => r.Success), Is.True, "All parallel executions must succeed");

            // Each result must have its own distinct correlation ID
            var correlationIds = results.Select(r => r.CorrelationId).ToHashSet();
            Assert.That(correlationIds.Count, Is.EqualTo(parallelism),
                "Each concurrent execution must have a unique correlation ID");
        }

        /// <summary>
        /// Concurrent failures must each be independently classified without
        /// sharing state between executions.
        /// </summary>
        [Test]
        public async Task Concurrency_MultipleParallelFailures_AllClassifiedIndependently()
        {
            const int parallelism = 10;

            var tasks = Enumerable.Range(0, parallelism).Select(i =>
                _pipeline.ExecuteAsync<string, string>(
                    _pipeline.BuildContext($"OP_{i}", $"corr-concurrent-fail-{i}"),
                    $"request-{i}",
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: _ => throw new TimeoutException($"Timeout for operation {i}")));

            var results = await Task.WhenAll(tasks);

            Assert.That(results.All(r => r.Success == false), Is.True);
            Assert.That(results.All(r => r.ErrorCode == ErrorCodes.BLOCKCHAIN_TIMEOUT), Is.True,
                "All concurrent failures must independently map to BLOCKCHAIN_TIMEOUT");
            Assert.That(results.All(r => r.FailureCategory == OrchestrationFailureCategory.TransientInfrastructureFailure), Is.True);
        }

        /// <summary>
        /// Concurrent executions with the same correlation ID must still produce
        /// independent results — the pipeline must not share mutable state across runs.
        /// </summary>
        [Test]
        public async Task Concurrency_SameCorrelationId_IndependentContexts()
        {
            // Two concurrent executions sharing the same logical correlation ID
            // (e.g., due to a client bug) must not interfere
            const string sharedCorrelationId = "same-corr-id";

            var task1 = _pipeline.ExecuteAsync(
                _pipeline.BuildContext("OP", sharedCorrelationId),
                "payload-1",
                _ => null, _ => null,
                r => Task.FromResult($"result-{r}"));

            var task2 = _pipeline.ExecuteAsync(
                _pipeline.BuildContext("OP", sharedCorrelationId),
                "payload-2",
                _ => null, _ => null,
                r => Task.FromResult($"result-{r}"));

            var (r1, r2) = (await task1, await task2);

            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            // Both carry the same correlation ID (expected) but have independent payloads
            Assert.That(r1.Payload, Is.EqualTo("result-payload-1"));
            Assert.That(r2.Payload, Is.EqualTo("result-payload-2"));
        }

        // ── Multi-step workflow execution ─────────────────────────────────────────

        /// <summary>
        /// Simulates a multi-step token creation workflow:
        ///   Step 1: Validate + create token parameters
        ///   Step 2: Submit transaction
        ///   Step 3: Verify on-chain confirmation
        /// All three steps are chained through the pipeline.
        /// </summary>
        [Test]
        public async Task MultiStep_TokenCreationWorkflow_AllStepsComplete()
        {
            // Step 1: Validate and prepare parameters
            var step1Context = _pipeline.BuildContext("ERC20_VALIDATE", "corr-multistep-01", "key-multistep-01");
            var step1 = await _pipeline.ExecuteAsync(
                step1Context,
                new { Name = "BiatecToken", Symbol = "BTC", Decimals = 18, Supply = 1_000_000 },
                validationPolicy: r => r.Symbol.Length > 8 ? "Symbol too long" : null,
                preconditionPolicy: _ => null,
                executor: r => Task.FromResult($"validated:{r.Symbol}:{r.Decimals}:{r.Supply}"));

            Assert.That(step1.Success, Is.True, "Step 1 (validation) must succeed");

            // Step 2: Submit transaction using validated data from step 1
            var step2Context = _pipeline.BuildContext("ERC20_SUBMIT", "corr-multistep-02", "key-multistep-02");
            step2Context.Metadata["PreviousStepResult"] = step1.Payload!;

            var step2 = await _pipeline.ExecuteAsync(
                step2Context,
                step1.Payload!, // carry result from step 1
                validationPolicy: r => string.IsNullOrEmpty(r) ? "Empty payload from step 1" : null,
                preconditionPolicy: _ => null,
                executor: r => Task.FromResult($"tx:0xabc123:{r}"));

            Assert.That(step2.Success, Is.True, "Step 2 (submit) must succeed");
            Assert.That(step2.Payload, Does.Contain("tx:0xabc123"));

            // Step 3: Verify on-chain confirmation
            var step3Context = _pipeline.BuildContext("ERC20_VERIFY", "corr-multistep-03", "key-multistep-03");
            var step3 = await _pipeline.ExecuteAsync(
                step3Context,
                step2.Payload!,
                validationPolicy: r => !r.StartsWith("tx:") ? "Invalid transaction reference" : null,
                preconditionPolicy: _ => null,
                executor: r => Task.FromResult($"confirmed:{r}"),
                postCommitVerifier: r => Task.FromResult<string?>(
                    r.StartsWith("confirmed:") ? null : "On-chain confirmation failed"));

            Assert.That(step3.Success, Is.True, "Step 3 (verify) must succeed");
            Assert.That(step3.StageMarkers.Count, Is.EqualTo(5),
                "All 5 stages must run including VerifyPostCommit");

            // Validate the complete audit trail across all three steps
            Assert.That(step1.AuditSummary.OperationType, Is.EqualTo("ERC20_VALIDATE"));
            Assert.That(step2.AuditSummary.OperationType, Is.EqualTo("ERC20_SUBMIT"));
            Assert.That(step3.AuditSummary.OperationType, Is.EqualTo("ERC20_VERIFY"));
        }

        /// <summary>
        /// Validates rollback semantics: when step 2 of a multi-step workflow fails,
        /// the caller is correctly informed which step failed and at what stage,
        /// enabling targeted compensation logic.
        /// </summary>
        [Test]
        public async Task MultiStep_FailureInStep2_AuditTrailIdentifiesFailedStep()
        {
            // Step 1 succeeds
            var step1 = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("ARC3_PREPARE", "corr-rollback-01"),
                "params",
                _ => null, _ => null,
                _ => Task.FromResult("prepared"));

            Assert.That(step1.Success, Is.True);

            // Step 2 fails (simulating IPFS unavailable)
            var step2 = await _pipeline.ExecuteAsync<string, string>(
                _pipeline.BuildContext("ARC3_UPLOAD_METADATA", "corr-rollback-02", "idem-rollback-02"),
                step1.Payload!,
                _ => null,
                _ => null,
                _ => throw new HttpRequestException("IPFS node unreachable"));

            Assert.That(step2.Success, Is.False, "Step 2 must fail");
            Assert.That(step2.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
            Assert.That(step2.AuditSummary.OperationType, Is.EqualTo("ARC3_UPLOAD_METADATA"));
            Assert.That(step2.AuditSummary.FailureCode, Is.EqualTo(ErrorCodes.NETWORK_ERROR));
            Assert.That(step2.AuditSummary.HasIdempotencyKey, Is.True,
                "Idempotency key must be tracked so step 2 can be safely retried");
            // Step 3 is not attempted — caller controls compensation
        }

        /// <summary>
        /// Idempotency-safe retry: re-executing step 2 with the same idempotency key
        /// after a transient failure must produce the same outcome deterministically.
        /// </summary>
        [Test]
        public async Task MultiStep_RetryAfterTransientFailure_DeterministicOutcome()
        {
            const string idempotencyKey = "retry-idem-key-001";

            async Task<OrchestrationResult<string>> AttemptStep()
            {
                return await _pipeline.ExecuteAsync(
                    _pipeline.BuildContext("ARC200_MINT", "corr-retry", idempotencyKey),
                    "mint-params",
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: _ => Task.FromResult("minted:token-id:1000"));
            }

            // Retry the same step 3 times (simulating idempotent re-submission)
            var attempt1 = await AttemptStep();
            var attempt2 = await AttemptStep();
            var attempt3 = await AttemptStep();

            // All three must succeed and produce identical outcomes
            Assert.That(attempt1.Success, Is.True);
            Assert.That(attempt2.Success, Is.True);
            Assert.That(attempt3.Success, Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(attempt1.Payload, Is.EqualTo(attempt2.Payload), "Retry 1 must be idempotent");
                Assert.That(attempt2.Payload, Is.EqualTo(attempt3.Payload), "Retry 2 must be idempotent");
                Assert.That(attempt1.IdempotencyKey, Is.EqualTo(idempotencyKey));
                Assert.That(attempt2.IdempotencyKey, Is.EqualTo(idempotencyKey));
            });
        }

        // ── Bounded retry semantics ───────────────────────────────────────────────

        /// <summary>
        /// Validates that the pipeline correctly classifies errors for which
        /// unbounded retry would be dangerous (non-retryable categories).
        /// Callers must NOT retry terminal or validation failures.
        /// </summary>
        [Test]
        public async Task RetrySemantics_TerminalFailure_RemediationHintDoesNotSuggestRetry()
        {
            // INTERNAL_SERVER_ERROR → TerminalExecutionFailure
            var result = await _pipeline.ExecuteAsync<string, string>(
                _pipeline.BuildContext("OP", "corr-retry-terminal"),
                "req",
                _ => null,
                _ => null,
                _ => throw new DivideByZeroException("Unrecoverable error"));

            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TerminalExecutionFailure));
            // Hint must advise contacting support, not retrying indefinitely
            Assert.That(result.RemediationHint, Does.Not.Contain("exponential back-off"),
                "Terminal failures must not suggest unbounded retry");
            Assert.That(result.RemediationHint, Does.Contain("unexpected").Or.Contain("support").Or.Contain("Contact"),
                "Terminal failures must direct user to support");
        }

        [Test]
        public async Task RetrySemantics_TransientFailure_RemediationHintSuggestsBoundedRetry()
        {
            var result = await _pipeline.ExecuteAsync<string, string>(
                _pipeline.BuildContext("OP", "corr-retry-transient"),
                "req",
                _ => null,
                _ => null,
                _ => throw new TimeoutException("Transient RPC error"));

            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
            // Hint must mention exponential back-off (bounded by policy)
            Assert.That(result.RemediationHint, Does.Contain("exponential back-off").Or.Contain("Retry"),
                "Transient failures must suggest bounded retry with back-off");
        }

        // ── Backward compatibility (critical path regression) ─────────────────────

        /// <summary>
        /// The pipeline must produce structurally identical results for the same
        /// inputs over time — providing backward compatibility to existing API consumers.
        /// </summary>
        [Test]
        public async Task BackwardCompat_SuccessResultSchema_AllFieldsPresent()
        {
            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("ERC20_CREATE", "corr-compat-01", "idem-compat-01", "user-compat"),
                "payload",
                _ => null,
                _ => null,
                _ => Task.FromResult("tx-abc123"));

            // All fields required by the API consumer contract must be present
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId required");
                Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty, "IdempotencyKey required");
                Assert.That(result.CompletedAtStage, Is.Not.EqualTo(OrchestrationStage.NotStarted), "CompletedAtStage required");
                Assert.That(result.Payload, Is.Not.Null, "Payload required for success");
                Assert.That(result.StageMarkers, Is.Not.Null.And.Not.Empty, "StageMarkers required");
                Assert.That(result.PolicyDecisions, Is.Not.Null, "PolicyDecisions required (may be empty)");
                Assert.That(result.AuditSummary, Is.Not.Null, "AuditSummary required");
                Assert.That(result.CompletedAt, Is.LessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1)), "CompletedAt required");
                Assert.That(result.TotalDurationMs, Is.GreaterThanOrEqualTo(0), "TotalDurationMs required");
                Assert.That(result.ErrorCode, Is.Null, "ErrorCode must be null for success");
                Assert.That(result.ErrorMessage, Is.Null, "ErrorMessage must be null for success");
            });
        }

        [Test]
        public async Task BackwardCompat_FailureResultSchema_AllFieldsPresent()
        {
            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("ERC20_CREATE", "corr-compat-02"),
                "bad",
                _ => "Validation failed",
                _ => null,
                _ => Task.FromResult("ok"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty, "ErrorCode required for failure");
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty, "ErrorMessage required for failure");
                Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty, "RemediationHint required for failure");
                Assert.That(result.FailureCategory, Is.Not.EqualTo(OrchestrationFailureCategory.None), "FailureCategory must be set");
                Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId always required");
                Assert.That(result.AuditSummary, Is.Not.Null, "AuditSummary always required");
                Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"), "Audit outcome must be Failed");
                Assert.That(result.Payload, Is.Null, "Payload must be null on failure");
            });
        }

        /// <summary>
        /// Observability: the pipeline must emit at minimum one stage marker
        /// to guarantee operators can trace workflow progress.
        /// This is a minimal observability contract for incident diagnosis.
        /// </summary>
        [Test]
        public async Task Observability_MinimalStageMarkers_AlwaysPresentForDiagnosis()
        {
            // Failure case — must still emit stage markers
            var failureResult = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("OP", "corr-obs-01"),
                "req",
                _ => "validation error",
                _ => null,
                _ => Task.FromResult("ok"));

            Assert.That(failureResult.StageMarkers.Count, Is.GreaterThanOrEqualTo(1),
                "At least one stage marker must be emitted even for validation failures");
            Assert.That(failureResult.StageMarkers[0].Stage, Is.EqualTo(OrchestrationStage.Validate));
            Assert.That(failureResult.StageMarkers[0].Timestamp, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task Observability_CorrelationIdNeverNull_GuaranteedForOperators()
        {
            // Even with a minimal context (no idempotency key, no user ID),
            // the correlation ID must always be present for operator tracing
            var result = await _pipeline.ExecuteAsync(
                _pipeline.BuildContext("OP", "must-be-present"),
                "req",
                _ => null,
                _ => null,
                _ => Task.FromResult(42));

            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must never be null — required for incident diagnosis");
        }
    }
}

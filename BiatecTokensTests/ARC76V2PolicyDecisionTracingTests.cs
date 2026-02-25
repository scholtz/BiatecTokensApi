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
    /// Observable evidence tests for policy decision tracing in the orchestration pipeline.
    ///
    /// Addresses product owner requirement for "observable evidence for policy decision tracing"
    /// as part of the ARC76 Auth-to-Deployment Reliability Contract v2.
    ///
    /// Validates that:
    /// 1. Stage markers provide the observable trace record for every policy decision made
    /// 2. Policy decisions recorded in context propagate to the result artifact
    /// 3. Correlation IDs link all stage markers in a single pipeline execution
    /// 4. Partial pipeline execution (halt at Validate or CheckPreconditions) leaves correct trace
    /// 5. Failure categories provide machine-readable policy decision outcomes
    /// 6. AuditSummary captures policy decision count for compliance evidence
    ///
    /// Regression Prevention:
    /// - These tests BLOCK the introduction of any change that would remove stage markers
    ///   from the orchestration result — without stage markers there is no observable
    ///   policy trace, which would break compliance audit evidence.
    /// - Any change to OrchestrationResult.StageMarkers or OrchestrationAuditSummary.PolicyDecisionCount
    ///   will be caught immediately.
    /// </summary>
    [TestFixture]
    public class ARC76V2PolicyDecisionTracingTests
    {
        private TokenWorkflowOrchestrationPipeline _pipeline = null!;
        private Mock<ILogger<TokenWorkflowOrchestrationPipeline>> _loggerMock = null!;
        private Mock<IRetryPolicyClassifier> _retryMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenWorkflowOrchestrationPipeline>>();
            _retryMock = new Mock<IRetryPolicyClassifier>();
            _retryMock.Setup(r => r.ClassifyError(It.IsAny<string>(), It.IsAny<DeploymentErrorCategory?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(new RetryPolicyDecision
                {
                    Policy = RetryPolicy.NotRetryable,
                    Explanation = "Test: terminal error"
                });

            _pipeline = new TokenWorkflowOrchestrationPipeline(_loggerMock.Object, _retryMock.Object);
        }

        #region Stage Markers as Observable Policy Decision Evidence

        /// <summary>
        /// A successful pipeline execution produces a Validate stage marker as evidence
        /// that the validation policy was evaluated and allowed.
        /// </summary>
        [Test]
        public async Task PolicyTrace_SuccessfulPipeline_ValidateStageMarker_IsPresent()
        {
            // Arrange
            var context = _pipeline.BuildContext("ARC3_CREATE", "trace-corr-001");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            // Assert: Validate stage marker is present and shows policy allowed
            var validateMarker = result.StageMarkers.FirstOrDefault(m => m.Stage == OrchestrationStage.Validate);
            Assert.That(validateMarker, Is.Not.Null,
                "PolicyTrace: Validate stage marker must be present as observable policy decision evidence");
            Assert.That(validateMarker!.Success, Is.True,
                "PolicyTrace: Validate stage marker must show Success=true when validation policy allows");
        }

        /// <summary>
        /// A validation-denied pipeline produces a Validate stage marker showing failure —
        /// this is the observable evidence that the validation policy rejected the request.
        /// </summary>
        [Test]
        public async Task PolicyTrace_ValidationDenied_ValidateStageMarker_ShowsFailure()
        {
            // Arrange
            var context = _pipeline.BuildContext("ARC3_CREATE", "trace-corr-002");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => "Token name cannot be empty",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            // Assert: Validate stage marker shows denial
            var validateMarker = result.StageMarkers.FirstOrDefault(m => m.Stage == OrchestrationStage.Validate);
            Assert.That(validateMarker, Is.Not.Null,
                "PolicyTrace: Validate stage marker must appear even on policy denial");
            Assert.That(validateMarker!.Success, Is.False,
                "PolicyTrace: Validate stage marker Success must be false when validation policy denies");
            Assert.That(validateMarker.Message, Is.Not.Null.And.Not.Empty,
                "PolicyTrace: Validate stage marker message must contain the denial reason");
        }

        /// <summary>
        /// A precondition-denied pipeline produces:
        ///   - Validate stage marker showing Success=true (passed)
        ///   - CheckPreconditions stage marker showing Success=false (denied)
        /// This proves the pipeline evaluated both policies sequentially.
        /// </summary>
        [Test]
        public async Task PolicyTrace_PreconditionDenied_TwoStageMarkers_ShowSequentialEvaluation()
        {
            // Arrange
            var context = _pipeline.BuildContext("ARC200_MINT", "trace-corr-003");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,              // Pass
                preconditionPolicy: _ => "KYC required",  // Deny
                executor: _ => Task.FromResult("ok"));

            // Assert: two stage markers with correct outcomes
            var validateMarker = result.StageMarkers.FirstOrDefault(m => m.Stage == OrchestrationStage.Validate);
            var preconditionMarker = result.StageMarkers.FirstOrDefault(m => m.Stage == OrchestrationStage.CheckPreconditions);

            Assert.That(validateMarker, Is.Not.Null,
                "PolicyTrace: Validate marker must exist even when precondition is denied");
            Assert.That(validateMarker!.Success, Is.True,
                "PolicyTrace: Validate must pass before precondition is evaluated");

            Assert.That(preconditionMarker, Is.Not.Null,
                "PolicyTrace: CheckPreconditions marker must appear when precondition policy denies");
            Assert.That(preconditionMarker!.Success, Is.False,
                "PolicyTrace: CheckPreconditions Success must be false when policy denies");
            Assert.That(preconditionMarker.Message, Does.Contain("KYC"),
                "PolicyTrace: CheckPreconditions message must contain the denial reason for traceability");
        }

        /// <summary>
        /// A successful pipeline produces stage markers for all 4 expected stages
        /// (Validate, CheckPreconditions, Execute, EmitTelemetry) in execution order.
        /// </summary>
        [Test]
        public async Task PolicyTrace_SuccessfulPipeline_AllFourStageMarkers_InCorrectOrder()
        {
            // Arrange
            var context = _pipeline.BuildContext("ERC20_MINTABLE_CREATE", "trace-corr-004");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult(42));

            // Assert: all 4 stages present (no post-commit verifier → 4 markers)
            var stageOrder = result.StageMarkers.Select(m => m.Stage).ToList();
            Assert.That(stageOrder[0], Is.EqualTo(OrchestrationStage.Validate),
                "PolicyTrace: First stage must be Validate");
            Assert.That(stageOrder[1], Is.EqualTo(OrchestrationStage.CheckPreconditions),
                "PolicyTrace: Second stage must be CheckPreconditions");
            Assert.That(stageOrder[2], Is.EqualTo(OrchestrationStage.Execute),
                "PolicyTrace: Third stage must be Execute");
            Assert.That(stageOrder[3], Is.EqualTo(OrchestrationStage.EmitTelemetry),
                "PolicyTrace: Fourth stage must be EmitTelemetry");
        }

        #endregion

        #region Stage Marker Fields for Compliance Evidence

        /// <summary>
        /// Every stage marker must have a non-default Timestamp for chronological audit ordering.
        /// </summary>
        [Test]
        public async Task PolicyTrace_AllStageMarkers_HaveNonDefaultTimestamp()
        {
            // Arrange
            var context = _pipeline.BuildContext("ASA_CREATE", "trace-corr-005");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("done"));

            // Assert
            Assert.That(result.StageMarkers.All(m => m.Timestamp != default),
                Is.True, "PolicyTrace: All stage markers must have non-default Timestamp for chronological ordering");
        }

        /// <summary>
        /// Every stage marker must have a non-negative duration for performance observability.
        /// </summary>
        [Test]
        public async Task PolicyTrace_AllStageMarkers_HaveNonNegativeDuration()
        {
            // Arrange
            var context = _pipeline.BuildContext("ARC3_CREATE", "trace-corr-006");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("done"));

            // Assert
            Assert.That(result.StageMarkers.All(m => m.DurationMs >= 0),
                Is.True, "PolicyTrace: All stage markers must have non-negative DurationMs for performance observability");
        }

        /// <summary>
        /// AuditSummary.StagesCompleted must match the number of successful stage markers,
        /// providing a single numeric compliance signal for dashboards.
        /// </summary>
        [Test]
        public async Task PolicyTrace_AuditSummary_StagesCompleted_MatchesSuccessfulMarkers()
        {
            // Arrange
            var context = _pipeline.BuildContext("ARC200_CREATE", "trace-corr-007");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("done"));

            // Assert
            var successfulMarkers = result.StageMarkers.Count(m => m.Success);
            Assert.That(result.AuditSummary.StagesCompleted, Is.EqualTo(successfulMarkers),
                "PolicyTrace: AuditSummary.StagesCompleted must equal count of successful stage markers");
        }

        #endregion

        #region Policy Decisions List Propagation

        /// <summary>
        /// Policy decisions explicitly added to the context propagate to the result's PolicyDecisions list.
        /// This enables callers to inject named policy evaluations into the trace.
        /// </summary>
        [Test]
        public async Task PolicyTrace_ExplicitPolicyDecisions_PropagateToResult()
        {
            // Arrange
            var context = _pipeline.BuildContext("ERC20_CREATE", "trace-corr-008");
            context.PolicyDecisions.Add(new OrchestrationPolicyDecision
            {
                PolicyName = "KYC_VERIFIED",
                Outcome = "Pass",
                Reason = "User identity verified via KYC provider"
            });
            context.PolicyDecisions.Add(new OrchestrationPolicyDecision
            {
                PolicyName = "SUBSCRIPTION_ACTIVE",
                Outcome = "Pass",
                Reason = "Pro subscription is active and within limits"
            });

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("deployed"));

            // Assert: policy decisions propagate to result
            Assert.That(result.PolicyDecisions, Has.Count.EqualTo(2),
                "PolicyTrace: Explicitly recorded policy decisions must propagate to result");
            Assert.That(result.PolicyDecisions.Any(d => d.PolicyName == "KYC_VERIFIED"), Is.True,
                "PolicyTrace: KYC policy decision must be present in result");
            Assert.That(result.PolicyDecisions.Any(d => d.PolicyName == "SUBSCRIPTION_ACTIVE"), Is.True,
                "PolicyTrace: Subscription policy decision must be present in result");
        }

        /// <summary>
        /// AuditSummary.PolicyDecisionCount must match the number of explicit policy decisions.
        /// Enables compliance dashboards to query single-field for policy evaluation count.
        /// </summary>
        [Test]
        public async Task PolicyTrace_AuditSummary_PolicyDecisionCount_MatchesPolicyDecisionsList()
        {
            // Arrange
            var context = _pipeline.BuildContext("ASA_CREATE", "trace-corr-009");
            for (int i = 0; i < 3; i++)
            {
                context.PolicyDecisions.Add(new OrchestrationPolicyDecision
                {
                    PolicyName = $"POLICY_{i + 1}",
                    Outcome = "Pass",
                    Reason = $"Policy {i + 1} passed"
                });
            }

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("done"));

            // Assert
            Assert.That(result.AuditSummary.PolicyDecisionCount, Is.EqualTo(3),
                "PolicyTrace: AuditSummary.PolicyDecisionCount must equal explicit policy decision count");
        }

        /// <summary>
        /// Policy decisions added before pipeline start are preserved even when the pipeline fails.
        /// This ensures policy trace evidence is always emitted regardless of outcome.
        /// </summary>
        [Test]
        public async Task PolicyTrace_FailedPipeline_PolicyDecisions_ArePreservedInResult()
        {
            // Arrange
            var context = _pipeline.BuildContext("ARC3_CREATE", "trace-corr-010");
            context.PolicyDecisions.Add(new OrchestrationPolicyDecision
            {
                PolicyName = "WHITELIST_CHECK",
                Outcome = "Fail",
                Reason = "Address not in deployment whitelist"
            });

            // Act: pipeline fails at validation
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => "Address not whitelisted",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("deployed"));

            // Assert
            Assert.That(result.Success, Is.False,
                "PolicyTrace: Pipeline must fail");
            Assert.That(result.PolicyDecisions, Has.Count.EqualTo(1),
                "PolicyTrace: Policy decisions must be preserved in failure result");
            Assert.That(result.PolicyDecisions[0].PolicyName, Is.EqualTo("WHITELIST_CHECK"),
                "PolicyTrace: WHITELIST_CHECK decision must appear in failed result");
        }

        #endregion

        #region Correlation ID Linkage Across Trace

        /// <summary>
        /// The correlation ID must be the same in the result, audit summary, and context.
        /// This proves end-to-end ID propagation for log correlation.
        /// </summary>
        [Test]
        public async Task PolicyTrace_CorrelationId_IsSameInResultAndAuditSummary()
        {
            // Arrange
            var correlationId = $"v2-trace-{Guid.NewGuid():N}";
            var context = _pipeline.BuildContext("ARC3_CREATE", correlationId);

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            // Assert: correlation ID is the same in all trace artifacts
            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "PolicyTrace: result.CorrelationId must match the input correlation ID");
            Assert.That(result.AuditSummary.CorrelationId, Is.EqualTo(correlationId),
                "PolicyTrace: AuditSummary.CorrelationId must match for log correlation");
        }

        /// <summary>
        /// Two concurrent pipeline executions with different correlation IDs must not
        /// cross-contaminate their trace records.
        /// </summary>
        [Test]
        public async Task PolicyTrace_ConcurrentExecutions_CorrelationIds_AreIsolated()
        {
            // Arrange
            var id1 = $"v2-concurrent-a-{Guid.NewGuid():N}";
            var id2 = $"v2-concurrent-b-{Guid.NewGuid():N}";

            var ctx1 = _pipeline.BuildContext("ARC3_CREATE", id1);
            var ctx2 = _pipeline.BuildContext("ERC20_CREATE", id2);

            // Act: execute concurrently
            var task1 = _pipeline.ExecuteAsync(ctx1, "req1",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("r1"));
            var task2 = _pipeline.ExecuteAsync(ctx2, "req2",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("r2"));

            await Task.WhenAll(task1, task2);

            // Assert: each result has its own correlation ID
            Assert.That(task1.Result.CorrelationId, Is.EqualTo(id1),
                "PolicyTrace: Concurrent execution 1 must preserve its own correlation ID");
            Assert.That(task2.Result.CorrelationId, Is.EqualTo(id2),
                "PolicyTrace: Concurrent execution 2 must preserve its own correlation ID");
            Assert.That(task1.Result.CorrelationId, Is.Not.EqualTo(task2.Result.CorrelationId),
                "PolicyTrace: Concurrent executions must not share correlation IDs");
        }

        #endregion

        #region Idempotency Key in Policy Trace

        /// <summary>
        /// Idempotency key is present in the result artifact when provided.
        /// This enables deduplication evidence in audit trails.
        /// </summary>
        [Test]
        public async Task PolicyTrace_IdempotencyKey_IsPresentInResult()
        {
            // Arrange
            var idemKey = $"v2-idem-{Guid.NewGuid():N}";
            var context = _pipeline.BuildContext("ARC3_CREATE", "trace-idem-001", idemKey);

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            // Assert
            Assert.That(result.IdempotencyKey, Is.EqualTo(idemKey),
                "PolicyTrace: Idempotency key must be preserved in result for deduplication evidence");
            Assert.That(result.AuditSummary.HasIdempotencyKey, Is.True,
                "PolicyTrace: AuditSummary.HasIdempotencyKey must be true when key is provided");
        }

        /// <summary>
        /// AuditSummary.Outcome must be "Succeeded" for successful runs —
        /// machine-readable compliance signal for automated reporting.
        /// </summary>
        [Test]
        public async Task PolicyTrace_SuccessfulRun_AuditOutcome_IsSucceeded()
        {
            // Arrange
            var context = _pipeline.BuildContext("ERC20_CREATE", "trace-outcome-001");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("done"));

            // Assert
            Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Succeeded"),
                "PolicyTrace: AuditSummary.Outcome must be 'Succeeded' for compliance reporting");
        }

        /// <summary>
        /// AuditSummary.Outcome must be "Failed" for failed runs —
        /// machine-readable compliance signal distinguishing success from failure.
        /// </summary>
        [Test]
        public async Task PolicyTrace_FailedRun_AuditOutcome_IsFailed()
        {
            // Arrange
            var context = _pipeline.BuildContext("ERC20_CREATE", "trace-outcome-002");

            // Act
            var result = await _pipeline.ExecuteAsync(
                context, "request",
                validationPolicy: _ => "Token name missing",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("done"));

            // Assert
            Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"),
                "PolicyTrace: AuditSummary.Outcome must be 'Failed' for compliance reporting");
        }

        #endregion

        #region Regression Prevention Tests

        /// <summary>
        /// REGRESSION GUARD: StageMarkers list must never be null in any result.
        /// A null StageMarkers would silently break all observability consumers.
        /// </summary>
        [Test]
        public async Task PolicyTrace_RegressionGuard_StageMarkersNeverNull()
        {
            var contexts = new[]
            {
                _pipeline.BuildContext("ARC3_CREATE", "reg-1"),
                _pipeline.BuildContext("ERC20_CREATE", "reg-2"),
                _pipeline.BuildContext("ASA_CREATE", "reg-3")
            };

            foreach (var ctx in contexts)
            {
                var result = await _pipeline.ExecuteAsync(
                    ctx, "req",
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: _ => Task.FromResult("ok"));

                Assert.That(result.StageMarkers, Is.Not.Null,
                    "PolicyTrace REGRESSION: StageMarkers must never be null in any pipeline result");
            }
        }

        /// <summary>
        /// REGRESSION GUARD: AuditSummary must never be null in any result.
        /// A null AuditSummary would break compliance reporting silently.
        /// </summary>
        [Test]
        public async Task PolicyTrace_RegressionGuard_AuditSummaryNeverNull()
        {
            // Test both success and failure paths
            var ctxSuccess = _pipeline.BuildContext("ARC3_CREATE", "reg-audit-success");
            var ctxFailure = _pipeline.BuildContext("ARC3_CREATE", "reg-audit-failure");

            var successResult = await _pipeline.ExecuteAsync(
                ctxSuccess, "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            var failureResult = await _pipeline.ExecuteAsync(
                ctxFailure, "req",
                validationPolicy: _ => "error",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.That(successResult.AuditSummary, Is.Not.Null,
                "PolicyTrace REGRESSION: AuditSummary must never be null on success");
            Assert.That(failureResult.AuditSummary, Is.Not.Null,
                "PolicyTrace REGRESSION: AuditSummary must never be null on failure");
        }

        /// <summary>
        /// REGRESSION GUARD: FailureCategory must be PreconditionFailure when precondition denies.
        /// Any change to this would break policy gate observability for compliance consumers.
        /// </summary>
        [Test]
        public async Task PolicyTrace_RegressionGuard_PreconditionFailureCategory_IsCorrect()
        {
            var context = _pipeline.BuildContext("ERC20_MINTABLE", "reg-precond");

            var result = await _pipeline.ExecuteAsync(
                context, "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => "Compliance check failed",
                executor: _ => Task.FromResult("ok"));

            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PreconditionFailure),
                "PolicyTrace REGRESSION: Precondition denial must always produce PreconditionFailure category");
        }

        #endregion
    }
}

using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the policy-driven workflow governance core.
    ///
    /// Validates the four required integration paths (AC8):
    ///   Path 1 – Successful workflow execution end-to-end
    ///   Path 2 – Policy rejection (validation and precondition failures)
    ///   Path 3 – Transient failure with bounded retry guidance
    ///   Path 4 – Permanent (non-retryable) failure path
    ///
    /// Also validates:
    ///   AC7 – WorkflowGovernanceConfig is registered and rollout controls are respected
    ///   AC9 – Existing API endpoints remain stable
    ///   AC10 – All tests pass in CI
    ///
    /// Business value: Provides production-grade evidence that the governance pipeline
    /// behaves correctly under all four critical failure modes, making it safe to
    /// enable in production with staged rollout controls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenWorkflowGovernanceIntegrationTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private static readonly Dictionary<string, string?> TestConfig = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
            ["JwtConfig:Issuer"] = "BiatecTokensApi",
            ["JwtConfig:Audience"] = "BiatecTokensUsers",
            ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
            ["JwtConfig:RefreshTokenExpirationDays"] = "30",
            ["JwtConfig:ValidateIssuerSigningKey"] = "true",
            ["JwtConfig:ValidateIssuer"] = "true",
            ["JwtConfig:ValidateAudience"] = "true",
            ["JwtConfig:ValidateLifetime"] = "true",
            ["JwtConfig:ClockSkewMinutes"] = "5",
            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
            ["IPFSConfig:TimeoutSeconds"] = "30",
            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
            ["IPFSConfig:ValidateContentHash"] = "true",
            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
            ["EVMChains:0:ChainId"] = "8453",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForWorkflowGovernanceIntegrationTests32ChMin",
            ["WorkflowGovernanceConfig:Enabled"] = "true",
            ["WorkflowGovernanceConfig:EnforceValidation"] = "true",
            ["WorkflowGovernanceConfig:EnforcePreconditions"] = "true",
            ["WorkflowGovernanceConfig:EnforcePostCommitVerification"] = "true",
            ["WorkflowGovernanceConfig:MaxRetryAttempts"] = "5",
            ["WorkflowGovernanceConfig:PolicyVersion"] = "1.0.0",
            ["WorkflowGovernanceConfig:RolloutPercentage"] = "100",
            ["Cors:0"] = "https://tokens.biatec.io"
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfig);
                    });
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ── AC7: Feature flag / rollout controls ──────────────────────────────────

        [Test]
        public void WorkflowGovernanceConfig_IsRegisteredInDI_CanBeResolved()
        {
            // Verify the governance config is resolvable from the DI container
            using var scope = _factory.Services.CreateScope();
            var config = scope.ServiceProvider.GetService<IOptions<WorkflowGovernanceConfig>>();

            Assert.That(config, Is.Not.Null,
                "WorkflowGovernanceConfig must be registered in the DI container");
            Assert.That(config!.Value, Is.Not.Null,
                "WorkflowGovernanceConfig.Value must not be null");
        }

        [Test]
        public void WorkflowGovernanceConfig_DefaultsAreEnabled_AllStagesActive()
        {
            using var scope = _factory.Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<WorkflowGovernanceConfig>>();
            var config = options.Value;

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True, "Governance must be enabled by default");
                Assert.That(config.EnforceValidation, Is.True, "Validation enforcement must be enabled");
                Assert.That(config.EnforcePreconditions, Is.True, "Precondition enforcement must be enabled");
                Assert.That(config.EnforcePostCommitVerification, Is.True, "Post-commit verification must be enabled");
                Assert.That(config.MaxRetryAttempts, Is.GreaterThan(0), "Must allow at least one retry");
                Assert.That(config.RolloutPercentage, Is.EqualTo(100), "Full rollout by default");
                Assert.That(config.PolicyVersion, Is.Not.Null.And.Not.Empty, "Policy version must be set");
            });
        }

        [Test]
        public void WorkflowGovernanceConfig_MaxRetryAttempts_MatchesRetryClassifier()
        {
            // The governance config max retries should be >= the retry classifier's max
            using var scope = _factory.Services.CreateScope();
            var govConfig = scope.ServiceProvider.GetRequiredService<IOptions<WorkflowGovernanceConfig>>().Value;
            var classifier = scope.ServiceProvider.GetRequiredService<IRetryPolicyClassifier>();

            // For a transient error, the classifier should not exceed the governance max
            var decision = classifier.ClassifyError(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR);
            Assert.That(decision.MaxRetryAttempts, Is.LessThanOrEqualTo(govConfig.MaxRetryAttempts),
                "Classifier retry count must not exceed governance config maximum");
        }

        // ── AC8 Path 1: Successful workflow execution ─────────────────────────────

        [Test]
        public async Task GovernancePath_SuccessfulExecution_CompletesAllStagesAndProducesAuditSummary()
        {
            // Resolve the pipeline from the full application DI container
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            const string operationType = "ERC20_MINTABLE_CREATE";
            const string correlationId = "gov-integration-success-001";
            const string idempotencyKey = "idem-gov-success-001";
            const string userId = "user-governance-test";

            var context = pipeline.BuildContext(operationType, correlationId, idempotencyKey, userId);

            var result = await pipeline.ExecuteAsync(
                context,
                "valid-token-request",
                validationPolicy: r => r.Length > 0 ? null : "Request must not be empty",
                preconditionPolicy: _ => null,
                executor: r => Task.FromResult($"deployed-{r}"),
                postCommitVerifier: _ => Task.FromResult<string?>(null));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Successful path must return Success=true");
                Assert.That(result.CompletedAtStage, Is.EqualTo(OrchestrationStage.Completed));
                Assert.That(result.CorrelationId, Is.EqualTo(correlationId));
                Assert.That(result.IdempotencyKey, Is.EqualTo(idempotencyKey));
                Assert.That(result.Payload, Is.EqualTo("deployed-valid-token-request"));

                // Stage markers must be present (Validate, CheckPreconditions, Execute, VerifyPostCommit, EmitTelemetry)
                Assert.That(result.StageMarkers.Count, Is.EqualTo(5));
                Assert.That(result.StageMarkers.All(m => m.Success), Is.True);

                // Audit summary must be complete
                Assert.That(result.AuditSummary, Is.Not.Null);
                Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Succeeded"));
                Assert.That(result.AuditSummary.CorrelationId, Is.EqualTo(correlationId));
                Assert.That(result.AuditSummary.InitiatedBy, Is.EqualTo(userId));
                Assert.That(result.AuditSummary.FailureCode, Is.Null);
                Assert.That(result.AuditSummary.HasIdempotencyKey, Is.True);
                Assert.That(result.TotalDurationMs, Is.GreaterThanOrEqualTo(0));
            });
        }

        [Test]
        public async Task GovernancePath_SuccessfulExecution_NoRetryHintNeeded()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var result = await pipeline.ExecuteAsync(
                pipeline.BuildContext("ASA_CREATE", "gov-success-002"),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: r => Task.FromResult(42));

            Assert.That(result.Success, Is.True);
            Assert.That(result.RemediationHint, Is.Null, "Successful operations need no remediation hint");
            Assert.That(result.ErrorCode, Is.Null, "Successful operations produce no error code");
        }

        // ── AC8 Path 2: Policy rejection ──────────────────────────────────────────

        [Test]
        public async Task GovernancePath_ValidationRejection_ProducesStructuredFailureWithReason()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            const string rejectionReason = "Token symbol must be 1-8 characters (got 12)";
            var context = pipeline.BuildContext("ERC20_MINTABLE_CREATE", "gov-rejection-001");

            var result = await pipeline.ExecuteAsync(
                context,
                "bad-request",
                validationPolicy: _ => rejectionReason,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("should-not-execute"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False, "Policy-rejected workflow must return Success=false");
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST));
                Assert.That(result.ErrorMessage, Is.EqualTo(rejectionReason), "Rejection reason must be surfaced");
                Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.ValidationFailure));
                Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                    "Operator must receive actionable remediation hint");

                // Audit summary must record the failure
                Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"));
                Assert.That(result.AuditSummary.FailureCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST));
                Assert.That(result.AuditSummary.CompletedAtStage, Is.EqualTo(OrchestrationStage.Validate.ToString()));
            });
        }

        [Test]
        public async Task GovernancePath_PreconditionRejection_BlocksExecutionWithActionableGuidance()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            int executorCallCount = 0;
            const string preconditionFailure = "KYC verification required before token issuance";

            var result = await pipeline.ExecuteAsync(
                pipeline.BuildContext("ARC200_CREATE", "gov-rejection-002"),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => preconditionFailure,
                executor: _ =>
                {
                    executorCallCount++;
                    return Task.FromResult("should-not-run");
                });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.PRECONDITION_FAILED));
                Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PreconditionFailure));
                Assert.That(result.RemediationHint, Is.EqualTo("Ensure all preconditions (KYC, subscription, compliance) are satisfied before retrying."),
                    "Remediation hint must direct users to resolve preconditions");

                // Executor must NOT have been called (fail-fast semantics)
                Assert.That(executorCallCount, Is.EqualTo(0),
                    "Executor must not be called when precondition is rejected");
            });
        }

        [Test]
        public async Task GovernancePath_PolicyRejection_FailureIsNotRetryable()
        {
            // Validation failures should guide users to fix inputs, not retry blindly
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var result = await pipeline.ExecuteAsync(
                pipeline.BuildContext("ASA_CREATE", "gov-rejection-003"),
                "bad",
                validationPolicy: _ => "Token decimals out of range [0..18]",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult(0));

            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.ValidationFailure));
            Assert.That(result.RemediationHint, Is.EqualTo("Correct the request parameters and resubmit."),
                "Validation failure must prompt user to correct inputs, not retry");
        }

        // ── AC8 Path 3: Transient failure with bounded retry guidance ─────────────

        [Test]
        public async Task GovernancePath_TransientInfrastructureFailure_ClassifiedWithRetryGuidance()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var result = await pipeline.ExecuteAsync<string, string>(
                pipeline.BuildContext("ERC20_MINTABLE_CREATE", "gov-transient-001"),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => throw new TimeoutException("RPC node timed out after 30s"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.BLOCKCHAIN_TIMEOUT));
                Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
                Assert.That(result.RemediationHint, Is.EqualTo("A transient error occurred. Retry with exponential back-off using the same idempotency key."),
                    "Transient failures must include retry guidance");

                // Audit summary must record transient failure
                Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"));
                Assert.That(result.AuditSummary.FailureCode, Is.EqualTo(ErrorCodes.BLOCKCHAIN_TIMEOUT));
            });
        }

        [Test]
        public async Task GovernancePath_TransientNetworkFailure_ReturnsNetworkErrorCode()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var result = await pipeline.ExecuteAsync<string, string>(
                pipeline.BuildContext("ARC3_CREATE", "gov-transient-002"),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => throw new HttpRequestException("IPFS node unreachable"));

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NETWORK_ERROR));
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
        }

        [Test]
        public async Task GovernancePath_TransientFailure_IdempotencyKeyAllowsSafeRetry()
        {
            // When a transient failure occurs, the idempotency key must be preserved
            // so that callers can safely retry without duplicate side effects
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            const string idempotencyKey = "safe-retry-key-gov-003";

            var result = await pipeline.ExecuteAsync<string, string>(
                pipeline.BuildContext("ASA_CREATE", "gov-transient-003", idempotencyKey),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => throw new TimeoutException("Transient failure"));

            Assert.That(result.IdempotencyKey, Is.EqualTo(idempotencyKey),
                "Idempotency key must be preserved in failure result for safe retry tracking");
            Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.TransientInfrastructureFailure));
        }

        // ── AC8 Path 4: Permanent failure ─────────────────────────────────────────

        [Test]
        public async Task GovernancePath_PostCommitVerificationFailure_MarkedAsPermanentTerminalFailure()
        {
            // Post-commit verification failure is a terminal, non-retryable condition
            // because the operation was already submitted and the result is unknown
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var result = await pipeline.ExecuteAsync(
                pipeline.BuildContext("ERC20_MINTABLE_CREATE", "gov-permanent-001"),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("tx-hash-submitted"),
                postCommitVerifier: _ => Task.FromResult<string?>("Transaction hash not found on-chain after 60s"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.POST_COMMIT_VERIFICATION_FAILED));
                Assert.That(result.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.PostCommitVerificationFailure));
                Assert.That(result.RemediationHint, Is.EqualTo("The operation was submitted but post-commit verification failed. Contact support with the correlation ID."),
                    "Post-commit failures must direct operators to support with correlation ID");
            });
        }

        [Test]
        public async Task GovernancePath_OperationCancelled_ProducesTerminalCancelledResult()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await pipeline.ExecuteAsync(
                pipeline.BuildContext("ARC200_CREATE", "gov-cancelled-001", "idem-cancel-001"),
                "req",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"),
                cancellationToken: cts.Token);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.OPERATION_CANCELLED));
                Assert.That(result.RemediationHint, Is.EqualTo("The operation was cancelled. Retry using the same idempotency key."),
                    "Cancelled operations must suggest safe retry with same idempotency key");
            });
        }

        // ── AC9: Existing API stability ───────────────────────────────────────────

        [Test]
        public async Task ExistingApi_HealthEndpoint_ReturnsStableResponse()
        {
            // Verify that the governance changes did not break existing health endpoint
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "Health endpoint must not return 5xx after governance changes");
        }

        [Test]
        public async Task ExistingApi_StatusEndpoint_ReturnsStableResponse()
        {
            var response = await _client.GetAsync("/api/v1/status");
            Assert.That((int)response.StatusCode, Is.LessThan(500),
                "Status endpoint must remain stable after governance changes");
        }

        // ── Governance config: Rollout percentage boundary ────────────────────────

        [Test]
        public void WorkflowGovernanceConfig_RolloutPercentage_IsWithinValidRange()
        {
            using var scope = _factory.Services.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IOptions<WorkflowGovernanceConfig>>().Value;

            Assert.That(config.RolloutPercentage, Is.InRange(0, 100),
                "RolloutPercentage must be between 0 and 100");
        }

        [Test]
        public void WorkflowGovernanceConfig_PolicyVersion_IsSemanticVersionFormat()
        {
            using var scope = _factory.Services.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IOptions<WorkflowGovernanceConfig>>().Value;

            Assert.That(config.PolicyVersion, Does.Match(@"^\d+\.\d+\.\d+$"),
                "PolicyVersion must follow semantic versioning (MAJOR.MINOR.PATCH)");
        }

        // ── Pipeline + governance config integration ───────────────────────────────

        [Test]
        public void GovernancePipeline_PipelineAndConfigBothResolvable_FromSameScope()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetService<ITokenWorkflowOrchestrationPipeline>();
            var config = scope.ServiceProvider.GetService<IOptions<WorkflowGovernanceConfig>>();
            var stateGuard = scope.ServiceProvider.GetService<IStateTransitionGuard>();
            var retryClassifier = scope.ServiceProvider.GetService<IRetryPolicyClassifier>();

            Assert.Multiple(() =>
            {
                Assert.That(pipeline, Is.Not.Null, "ITokenWorkflowOrchestrationPipeline must be resolvable");
                Assert.That(config, Is.Not.Null, "WorkflowGovernanceConfig must be resolvable");
                Assert.That(stateGuard, Is.Not.Null, "IStateTransitionGuard must be resolvable");
                Assert.That(retryClassifier, Is.Not.Null, "IRetryPolicyClassifier must be resolvable");
            });
        }
    }
}

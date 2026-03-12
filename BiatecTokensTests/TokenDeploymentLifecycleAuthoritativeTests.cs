using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// TDD-driven tests proving authoritative deployment lifecycle behavior for regulated issuance.
    ///
    /// Core proposition
    /// ────────────────
    /// These tests prove that the <see cref="TokenDeploymentLifecycleService"/> is a backend
    /// that can serve as an authoritative source of deployment lifecycle truth for sign-off
    /// environments. Specifically:
    ///
    /// 1. **Fail-closed in Authoritative mode**: when the <see cref="IDeploymentEvidenceProvider"/>
    ///    cannot return confirmed blockchain evidence, the service transitions to
    ///    <see cref="DeploymentStage.Failed"/> with <see cref="DeploymentOutcome.TerminalFailure"/>
    ///    and error code <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>. It never silently returns a
    ///    simulated-success shape when running in <see cref="DeploymentExecutionMode.Authoritative"/>.
    ///
    /// 2. **Meaningful intermediate stage progression**: every deployment goes through
    ///    Initialising → Validating → Submitting → Confirming → Completed/Failed. Each stage
    ///    is recorded in the telemetry trail so frontend and sign-off tooling can track progress.
    ///
    /// 3. **Durable deployment identifiers**: every initiation returns a non-empty, stable
    ///    <c>DeploymentId</c> and <c>IdempotencyKey</c> that downstream lifecycle tracking
    ///    (status poll, telemetry query, retry) can use.
    ///
    /// 4. **Explicit failure semantics**: missing identifiers, unavailable blockchain evidence,
    ///    and validation errors all produce distinct, machine-readable error codes with
    ///    human-readable remediation hints.
    ///
    /// 5. **Simulation vs. Authoritative boundary**: the
    ///    <see cref="TokenDeploymentLifecycleResponse.IsSimulatedEvidence"/> flag and the
    ///    <see cref="DeploymentExecutionMode"/> enum together make the boundary between test
    ///    scaffolding and production-grade evidence machine-detectable by frontend consumers
    ///    and sign-off tooling.
    ///
    /// What is authoritative vs. simulated
    /// ─────────────────────────────────────
    /// **Authoritative** (production): the service is wired to a live blockchain node that
    /// returns confirmed <c>AssetId</c>, <c>TransactionId</c>, and <c>ConfirmedRound</c>.
    /// <c>IsSimulatedEvidence = false</c>. These values can be trusted as on-chain proof.
    ///
    /// **Simulated** (test/dev): the service uses <see cref="SimulatedDeploymentEvidenceProvider"/>.
    /// Evidence fields are derived from SHA-256 hashes of the deployment ID.
    /// <c>IsSimulatedEvidence = true</c>. These values are deterministic but NOT blockchain-confirmed.
    ///
    /// **Fail-closed** (no evidence available): the <see cref="UnavailableDeploymentEvidenceProvider"/>
    /// returns <c>null</c>. In <see cref="DeploymentExecutionMode.Authoritative"/> mode this causes
    /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c> terminal failure. This is the behavior that proves
    /// the service cannot be tricked into returning success when evidence is absent.
    /// </summary>
    [TestFixture]
    public class TokenDeploymentLifecycleAuthoritativeTests
    {
        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;

        private const string ValidAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();
        }

        private TokenDeploymentLifecycleService WithProvider(IDeploymentEvidenceProvider provider)
            => new(_loggerMock.Object, provider);

        private static TokenDeploymentLifecycleRequest BuildValidRequest(
            DeploymentExecutionMode mode = DeploymentExecutionMode.Authoritative,
            string? idempotencyKey = null)
        {
            return new TokenDeploymentLifecycleRequest
            {
                IdempotencyKey = idempotencyKey,
                TokenStandard  = "ASA",
                TokenName      = "Authoritative Token",
                TokenSymbol    = "ATK",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
                ExecutionMode  = mode,
            };
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 1: Fail-closed in Authoritative mode
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_FailsWithTerminalFailure()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Failed),
                "Authoritative mode with unavailable evidence must fail.");
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure),
                "Outcome must be TerminalFailure — not Success or Unknown.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_ErrorCodeIsBlockchainEvidenceUnavailable()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            var hasErrorCode = result.TelemetryEvents.Any(e =>
                e.Metadata.ContainsKey("errorCode") &&
                e.Metadata["errorCode"] == "BLOCKCHAIN_EVIDENCE_UNAVAILABLE");
            Assert.That(hasErrorCode, Is.True,
                "Telemetry must include the machine-readable error code BLOCKCHAIN_EVIDENCE_UNAVAILABLE " +
                "so that sign-off tooling can distinguish this failure from validation errors.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_AssetIdIsNull()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.AssetId, Is.Null,
                "Fail-closed: no AssetId must be returned when authoritative evidence is unavailable.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_TransactionIdIsNull()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.TransactionId, Is.Null,
                "Fail-closed: no TransactionId must be returned when authoritative evidence is unavailable.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_IsSimulatedEvidenceIsFalse()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.IsSimulatedEvidence, Is.False,
                "Fail-closed: IsSimulatedEvidence must be false when no evidence was obtained. " +
                "The service must not claim any evidence (real or simulated) for a failed deployment.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_MessageMentionsBlockchainEvidenceUnavailable()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.Message, Does.Contain("authoritative blockchain evidence").IgnoreCase
                .Or.Contain("BLOCKCHAIN_EVIDENCE_UNAVAILABLE").IgnoreCase,
                "The failure message must reference the specific reason — authoritative evidence unavailable.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_RemediationHintProvided()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "A remediation hint must be provided so operators know how to fix the issue.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_DeploymentIdStillPresent()
        {
            // Even on failure the deployment ID must be returned so lifecycle tracking can reference it.
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty,
                "DeploymentId must always be present for lifecycle tracking even when evidence fails.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_IdempotencyKeyPresent()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty,
                "IdempotencyKey must always be present for lifecycle tracking even on failure.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_DependencyFailureTelemetryEmitted()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            var hasDependencyFailure = result.TelemetryEvents
                .Any(e => e.EventType == TelemetryEventType.DependencyFailure);
            Assert.That(hasDependencyFailure, Is.True,
                "A DependencyFailure telemetry event must be emitted when the evidence provider returns null.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_TerminalFailureTelemetryEmitted()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            var hasTerminalFailure = result.TelemetryEvents
                .Any(e => e.EventType == TelemetryEventType.TerminalFailure);
            Assert.That(hasTerminalFailure, Is.True,
                "A TerminalFailure telemetry event must be emitted for audit trail completeness.");
        }

        [Test]
        public async Task AuthoritativeMode_EvidenceUnavailable_ProgressShowsFailed()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.Progress, Is.Not.Null);
            Assert.That(result.Progress.Summary, Does.Contain("fail").IgnoreCase,
                "Progress summary must reflect the failed state.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 2: Simulation mode succeeds with explicit disclosure
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task SimulationMode_WithSimulatedProvider_Succeeds()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task SimulationMode_IsSimulatedEvidenceTrue()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            Assert.That(result.IsSimulatedEvidence, Is.True,
                "Simulation mode must set IsSimulatedEvidence = true; consumers must not treat " +
                "these values as confirmed blockchain state.");
        }

        [Test]
        public async Task SimulationMode_AssetIdNonZero()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            Assert.That(result.AssetId, Is.GreaterThan(0UL),
                "Simulation mode must return a non-zero deterministic AssetId.");
        }

        [Test]
        public async Task SimulationMode_TransactionIdNonEmpty()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            Assert.That(result.TransactionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task SimulationMode_ConfirmedRoundNonZero()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            Assert.That(result.ConfirmedRound, Is.GreaterThan(0UL));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 3: Authoritative mode + non-null evidence succeeds
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AuthoritativeMode_WithSimulatedProvider_EvidenceNonNull_Succeeds()
        {
            // When a real evidence provider is wired (here simulated for testing purposes)
            // and it returns non-null evidence, the deployment completes in Authoritative mode.
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task AuthoritativeMode_WithSimulatedProvider_IsSimulatedEvidenceReflectsProvider()
        {
            // Authoritative mode does not force IsSimulatedEvidence = false.
            // The flag is driven by the provider's IsSimulated property.
            // When a real provider is available, it would set IsSimulated = false.
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            Assert.That(result.IsSimulatedEvidence, Is.EqualTo(new SimulatedDeploymentEvidenceProvider().IsSimulated),
                "IsSimulatedEvidence must match the provider's IsSimulated value; " +
                "when a real provider is wired IsSimulated = false and the evidence is authoritative.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 4: Meaningful intermediate state progression
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Lifecycle_TelemetryContainsInitialisingStage()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Initialising),
                "Telemetry must record the Initialising stage.");
        }

        [Test]
        public async Task Lifecycle_TelemetryContainsValidatingStage()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Validating),
                "Telemetry must record the Validating stage.");
        }

        [Test]
        public async Task Lifecycle_TelemetryContainsSubmittingStage()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Submitting),
                "Telemetry must record the Submitting stage.");
        }

        [Test]
        public async Task Lifecycle_TelemetryContainsConfirmingStage()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Confirming),
                "Telemetry must record the Confirming stage.");
        }

        [Test]
        public async Task Lifecycle_TelemetryContainsCompletedStage()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Completed),
                "Telemetry must record the Completed stage.");
        }

        [Test]
        public async Task Lifecycle_StageTransitionsAreMonotonicallyForward()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Simulation));

            var stageOrder = new[]
            {
                DeploymentStage.Initialising,
                DeploymentStage.Validating,
                DeploymentStage.Submitting,
                DeploymentStage.Confirming,
                DeploymentStage.Completed,
            };

            var transitions = result.TelemetryEvents
                .Where(e => e.EventType == TelemetryEventType.StageTransition)
                .Select(e => e.Stage)
                .ToList();

            Assert.That(transitions.Count, Is.GreaterThanOrEqualTo(4),
                "At least 4 stage transitions must be recorded.");

            for (int i = 1; i < transitions.Count; i++)
            {
                var prev = Array.IndexOf(stageOrder, transitions[i - 1]);
                var curr = Array.IndexOf(stageOrder, transitions[i]);
                Assert.That(curr, Is.GreaterThanOrEqualTo(prev),
                    $"Stage transitions must be monotonically forward: {transitions[i - 1]} → {transitions[i]}");
            }
        }

        [Test]
        public async Task Lifecycle_FailedPath_TelemetryContainsFailedStage()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Failed),
                "Failed deployments must record the Failed stage in telemetry.");
        }

        [Test]
        public async Task Lifecycle_FailedPath_TelemetryContainsInitialisingAndValidating()
        {
            // Even on failure, Initialising and Validating stages must be present
            // (the failure happens during Submitting, not before validation).
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));
            var stages = result.TelemetryEvents.Select(e => e.Stage).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Initialising));
            Assert.That(stages, Does.Contain(DeploymentStage.Validating));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 5: Durable deployment identifier
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiation_ReturnsNonEmptyDeploymentId()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest());
            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty,
                "DeploymentId must be returned for downstream lifecycle tracking.");
        }

        [Test]
        public async Task Initiation_DeploymentIdIsDeterministic()
        {
            // Each service instance has its own in-memory idempotency store. Two different
            // service instances that receive identical inputs must derive the SAME deployment ID
            // from the same idempotency key derivation function — proving the function is
            // deterministic, not a cache hit from shared state.
            var svc1 = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var svc2 = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var req = BuildValidRequest();

            var result1 = await svc1.InitiateDeploymentAsync(req);
            var result2 = await svc2.InitiateDeploymentAsync(req);

            Assert.That(result1.DeploymentId, Is.EqualTo(result2.DeploymentId),
                "DeploymentId must be deterministic: same inputs always produce the same ID " +
                "regardless of service instance (proven across two independent instances with " +
                "no shared state).");
        }

        [Test]
        public async Task Initiation_ReturnsNonEmptyIdempotencyKey()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest());
            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty,
                "IdempotencyKey must be returned for replay protection.");
        }

        [Test]
        public async Task Initiation_DeploymentIdCanBeUsedForStatusQuery()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var initiated = await svc.InitiateDeploymentAsync(BuildValidRequest());
            var status    = await svc.GetDeploymentStatusAsync(initiated.DeploymentId);
            Assert.That(status.DeploymentId, Is.EqualTo(initiated.DeploymentId),
                "GetDeploymentStatusAsync must return the same DeploymentId.");
            Assert.That(status.Stage,        Is.EqualTo(initiated.Stage),
                "Status query must return the same stage as the initiation response.");
        }

        [Test]
        public async Task Initiation_DeploymentIdCanBeUsedForTelemetryQuery()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var initiated = await svc.InitiateDeploymentAsync(BuildValidRequest());
            var events    = await svc.GetTelemetryEventsAsync(initiated.DeploymentId);
            Assert.That(events.Count, Is.GreaterThan(0),
                "GetTelemetryEventsAsync must return events for the deployment ID.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 6: Negative-path tests
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task NegativePath_MissingDeploymentId_StatusQueryFails()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.GetDeploymentStatusAsync(string.Empty);
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Failed),
                "An empty deployment ID must fail; the service must not silently return empty state.");
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task NegativePath_UnknownDeploymentId_StatusQueryReturnsNotFound()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.GetDeploymentStatusAsync("totally-unknown-deployment-id-xyz");
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Message, Does.Contain("not found").IgnoreCase,
                "Status query for unknown ID must return a descriptive not-found message.");
        }

        [Test]
        public async Task NegativePath_MissingDeploymentId_TelemetryQueryReturnsEmpty()
        {
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var events = await svc.GetTelemetryEventsAsync("no-such-deployment");
            Assert.That(events, Is.Empty,
                "Telemetry query for unknown deployment must return empty list, not throw.");
        }

        [Test]
        public async Task NegativePath_ValidationFail_NoBlockchainEvidenceReturned()
        {
            // Validation errors must short-circuit before the evidence provider is called.
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var req = BuildValidRequest(DeploymentExecutionMode.Authoritative);
            req.TokenStandard = string.Empty; // force validation failure

            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.AssetId, Is.Null,
                "Validation failure must not produce any blockchain evidence.");
            // Failure is due to validation, not BLOCKCHAIN_EVIDENCE_UNAVAILABLE
            var hasEvidenceUnavailable = result.TelemetryEvents.Any(e =>
                e.Metadata.ContainsKey("errorCode") &&
                e.Metadata["errorCode"] == "BLOCKCHAIN_EVIDENCE_UNAVAILABLE");
            Assert.That(hasEvidenceUnavailable, Is.False,
                "Validation failure must not emit BLOCKCHAIN_EVIDENCE_UNAVAILABLE — wrong error code.");
        }

        [Test]
        public async Task NegativePath_StalledLifecycle_RetryOfFailedDeployment_ReturnsInProgressState()
        {
            // Prove that a failed deployment (evidence unavailable) can be retried and
            // the service transitions to Submitting (in-progress), not silently ignored.
            // Terminal failure on retry-limit-exceeded is already proven in
            // TokenDeploymentLifecycleServiceTests.Retry_ExceedsMaxAttempts_ReturnsTerminalFailure.
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var key = Guid.NewGuid().ToString();

            // 1. Initiate → fails (evidence unavailable)
            var req = BuildValidRequest(DeploymentExecutionMode.Authoritative, key);
            var initial = await svc.InitiateDeploymentAsync(req);
            Assert.That(initial.Stage, Is.EqualTo(DeploymentStage.Failed),
                "Precondition: deployment must fail due to unavailable blockchain evidence.");
            Assert.That(initial.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));

            // 2. Retry the failed deployment
            var retried = await svc.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = key,
                CorrelationId  = Guid.NewGuid().ToString(),
                ForceRetry     = false,
            });

            // 3. Retry transitions to Submitting (in-progress retry state, not a silent success)
            Assert.That(retried.Stage, Is.EqualTo(DeploymentStage.Submitting),
                "Retrying a failed deployment must transition to Submitting (in-progress), " +
                "not silently return success or stay in Failed.");
            Assert.That(retried.RetryCount, Is.EqualTo(1),
                "First retry must increment RetryCount to 1.");
        }

        [Test]
        public async Task NegativePath_UnavailableEvidence_NullRequestNotCrashing()
        {
            // Prove that null request does not crash the service (defensive coding).
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(null!);
            Assert.That(result.Stage,              Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Outcome,            Is.EqualTo(DeploymentOutcome.TerminalFailure));
            Assert.That(result.AssetId,            Is.Null,
                "Null request must not return a simulated or real AssetId.");
            Assert.That(result.TransactionId,      Is.Null,
                "Null request must not return a TransactionId.");
            Assert.That(result.IsSimulatedEvidence, Is.False,
                "Null request must return IsSimulatedEvidence = false (no evidence claimed).");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 7: E2E authoritative lifecycle proof
        //   Proves validate → initiate → status → telemetry → explicit terminal outcome
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task E2E_Authoritative_LifecycleChain_WithUnavailableEvidence_Fails()
        {
            // Full lifecycle chain in Authoritative mode with no blockchain connection.
            // This is the "fail-closed" proof: the full lifecycle chain terminates cleanly.
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var req = BuildValidRequest(DeploymentExecutionMode.Authoritative);

            // Step 1: Validate inputs (dry-run, should succeed for valid request)
            var validation = await svc.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                CorrelationId  = "e2e-auth-corr-1",
                TokenStandard  = req.TokenStandard,
                TokenName      = req.TokenName,
                TokenSymbol    = req.TokenSymbol,
                Network        = req.Network,
                TotalSupply    = req.TotalSupply,
                Decimals       = req.Decimals,
                CreatorAddress = req.CreatorAddress,
            });
            Assert.That(validation.IsValid, Is.True,
                "Valid inputs must pass validation regardless of execution mode.");

            // Step 2: Initiate deployment (must fail loudly)
            req.CorrelationId = "e2e-auth-corr-1";
            var deployment = await svc.InitiateDeploymentAsync(req);
            Assert.That(deployment.Stage,   Is.EqualTo(DeploymentStage.Failed),
                "E2E: Authoritative + unavailable evidence must produce Failed stage.");
            Assert.That(deployment.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure),
                "E2E: Outcome must be TerminalFailure.");
            Assert.That(deployment.DeploymentId, Is.Not.Null.And.Not.Empty,
                "E2E: DeploymentId must be returned even on failure.");

            // Step 3: Status query returns the same failed state
            var status = await svc.GetDeploymentStatusAsync(deployment.DeploymentId, "e2e-auth-corr-2");
            Assert.That(status.Stage,   Is.EqualTo(DeploymentStage.Failed),
                "E2E: Status query must return Failed — state must not regress or change silently.");
            Assert.That(status.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));

            // Step 4: Telemetry events present and include the failure reason
            var events = await svc.GetTelemetryEventsAsync(deployment.DeploymentId);
            Assert.That(events.Count, Is.GreaterThan(0),
                "E2E: Telemetry must be recorded even for failed deployments.");
            var hasBlockchainFailure = events.Any(e =>
                e.Metadata.ContainsKey("errorCode") &&
                e.Metadata["errorCode"] == "BLOCKCHAIN_EVIDENCE_UNAVAILABLE");
            Assert.That(hasBlockchainFailure, Is.True,
                "E2E: Telemetry must contain the BLOCKCHAIN_EVIDENCE_UNAVAILABLE error code.");
        }

        [Test]
        public async Task E2E_Simulation_LifecycleChain_Completes()
        {
            // Full lifecycle chain in Simulation mode: validate → initiate → status → telemetry.
            var svc = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var key = Guid.NewGuid().ToString();
            var req = BuildValidRequest(DeploymentExecutionMode.Simulation, key);
            req.CorrelationId = "e2e-sim-corr-1";

            // Step 1: Validate
            var validation = await svc.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                CorrelationId  = "e2e-sim-corr-1",
                TokenStandard  = req.TokenStandard,
                TokenName      = req.TokenName,
                TokenSymbol    = req.TokenSymbol,
                Network        = req.Network,
                TotalSupply    = req.TotalSupply,
                Decimals       = req.Decimals,
                CreatorAddress = req.CreatorAddress,
            });
            Assert.That(validation.IsValid, Is.True);

            // Step 2: Initiate
            var deployment = await svc.InitiateDeploymentAsync(req);
            Assert.That(deployment.Stage,             Is.EqualTo(DeploymentStage.Completed));
            Assert.That(deployment.Outcome,           Is.EqualTo(DeploymentOutcome.Success));
            Assert.That(deployment.IsSimulatedEvidence, Is.True);
            Assert.That(deployment.AssetId,           Is.GreaterThan(0UL));
            Assert.That(deployment.TransactionId,     Is.Not.Null.And.Not.Empty);
            Assert.That(deployment.ConfirmedRound,    Is.GreaterThan(0UL));

            // Step 3: Status query returns consistent state
            var status = await svc.GetDeploymentStatusAsync(deployment.DeploymentId);
            Assert.That(status.Stage,             Is.EqualTo(DeploymentStage.Completed));
            Assert.That(status.IsSimulatedEvidence, Is.True,
                "Status query must preserve IsSimulatedEvidence from the original deployment.");

            // Step 4: Telemetry records all meaningful stages
            var events = await svc.GetTelemetryEventsAsync(deployment.DeploymentId);
            var stages = events.Select(e => e.Stage).Distinct().OrderBy(s => s).ToList();
            Assert.That(stages, Does.Contain(DeploymentStage.Initialising));
            Assert.That(stages, Does.Contain(DeploymentStage.Validating));
            Assert.That(stages, Does.Contain(DeploymentStage.Submitting));
            Assert.That(stages, Does.Contain(DeploymentStage.Confirming));
            Assert.That(stages, Does.Contain(DeploymentStage.Completed));

            // Step 5: Idempotency replay (same key, same result, 3 runs)
            var replay2 = await svc.InitiateDeploymentAsync(req);
            var replay3 = await svc.InitiateDeploymentAsync(req);
            Assert.That(replay2.AssetId,        Is.EqualTo(deployment.AssetId),   "Replay 2 AssetId");
            Assert.That(replay3.AssetId,        Is.EqualTo(deployment.AssetId),   "Replay 3 AssetId");
            Assert.That(replay2.TransactionId,  Is.EqualTo(deployment.TransactionId), "Replay 2 TxId");
            Assert.That(replay3.TransactionId,  Is.EqualTo(deployment.TransactionId), "Replay 3 TxId");
            Assert.That(replay2.IsSimulatedEvidence, Is.True, "Replay 2 preserves IsSimulatedEvidence");
            Assert.That(replay3.IsSimulatedEvidence, Is.True, "Replay 3 preserves IsSimulatedEvidence");
        }

        [Test]
        public async Task E2E_SchemaContract_AllRequiredFieldsPresentInAuthoritativeFailure()
        {
            // Schema contract assertion: even failure responses contain all required fields.
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BuildValidRequest(DeploymentExecutionMode.Authoritative));

            Assert.That(result.DeploymentId,       Is.Not.Null.And.Not.Empty, "DeploymentId");
            Assert.That(result.IdempotencyKey,     Is.Not.Null.And.Not.Empty, "IdempotencyKey");
            Assert.That(result.CorrelationId,      Is.Not.Null.And.Not.Empty, "CorrelationId");
            Assert.That(result.SchemaVersion,      Is.Not.Null.And.Not.Empty, "SchemaVersion");
            Assert.That(result.Message,            Is.Not.Null.And.Not.Empty, "Message");
            Assert.That(result.RemediationHint,    Is.Not.Null.And.Not.Empty, "RemediationHint");
            Assert.That(result.Stage,              Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Outcome,            Is.EqualTo(DeploymentOutcome.TerminalFailure));
            Assert.That(result.IsSimulatedEvidence, Is.False);
            Assert.That(result.TelemetryEvents,    Is.Not.Null, "TelemetryEvents");
            Assert.That(result.Progress,           Is.Not.Null, "Progress");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SECTION 8: HTTP endpoint exposes execution mode and fail-closed
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        [NonParallelizable]
        public async Task Http_AuthoritativeMode_EvidenceUnavailable_FailsLoudly()
        {
            await using var factory = new AuthoritativeTestWebApplicationFactory(
                typeof(UnavailableDeploymentEvidenceProvider));
            var client = factory.CreateClient();

            var email = $"auth-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Authoritative Test"
            });
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regBody?.AccessToken ?? string.Empty);

            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Authoritative Fail Token",
                TokenSymbol    = "AFT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                ExecutionMode  = DeploymentExecutionMode.Authoritative,
            };
            var resp = await client.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Stage,              Is.EqualTo(DeploymentStage.Failed),
                "HTTP: Authoritative mode with unavailable evidence must return Failed stage.");
            Assert.That(body.Outcome,             Is.EqualTo(DeploymentOutcome.TerminalFailure),
                "HTTP: Outcome must be TerminalFailure.");
            Assert.That(body.IsSimulatedEvidence, Is.False,
                "HTTP: IsSimulatedEvidence must be false on failure.");
        }

        [Test]
        [NonParallelizable]
        public async Task Http_SimulationMode_Succeeds_WithIsSimulatedEvidenceTrue()
        {
            await using var factory = new AuthoritativeTestWebApplicationFactory(
                typeof(SimulatedDeploymentEvidenceProvider));
            var client = factory.CreateClient();

            var email = $"sim-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Simulation Test"
            });
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regBody?.AccessToken ?? string.Empty);

            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Simulation Success Token",
                TokenSymbol    = "SST",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                ExecutionMode  = DeploymentExecutionMode.Simulation,
            };
            var resp = await client.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Stage,              Is.EqualTo(DeploymentStage.Completed));
            Assert.That(body.Outcome,             Is.EqualTo(DeploymentOutcome.Success));
            Assert.That(body.IsSimulatedEvidence, Is.True,
                "HTTP: Simulation mode must set IsSimulatedEvidence = true.");
        }

        // ── Test WebApplicationFactory with configurable evidence provider ────────

        private sealed class AuthoritativeTestWebApplicationFactory
            : WebApplicationFactory<BiatecTokensApi.Program>
        {
            private readonly Type _evidenceProviderType;

            public AuthoritativeTestWebApplicationFactory(Type evidenceProviderType)
            {
                _evidenceProviderType = evidenceProviderType;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "AuthoritativeTestKey32CharsMinimumRequired!!",
                        ["JwtConfig:SecretKey"] = "AuthoritativeIntegrationTestKey32CharsRequired!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Override the evidence provider with the test-specific implementation
                    var descriptor = services.FirstOrDefault(d =>
                        d.ServiceType == typeof(IDeploymentEvidenceProvider));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddSingleton(typeof(IDeploymentEvidenceProvider), _evidenceProviderType);
                });
            }
        }
    }
}

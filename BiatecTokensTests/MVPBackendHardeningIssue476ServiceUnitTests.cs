using BiatecTokensApi.Models.MVPHardening;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for MVPBackendHardeningService (Issue #476).
    /// All tests use direct service instantiation – no HTTP calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPBackendHardeningIssue476ServiceUnitTests
    {
        private MVPBackendHardeningService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<MVPBackendHardeningService>>();
            _service = new MVPBackendHardeningService(logger.Object);
        }

        // ── Auth contract verification ──────────────────────────────────────

        [Test]
        public async Task VerifyAuthContractAsync_ValidEmail_ReturnsSuccessWithDeterministicTrue()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "user@example.com" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsDeterministic, Is.True);
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task VerifyAuthContractAsync_NullEmail_ReturnsError()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = null });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_EMAIL"));
        }

        [Test]
        public async Task VerifyAuthContractAsync_InvalidEmailFormat_ReturnsError()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "not-an-email" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_EMAIL_FORMAT"));
        }

        [Test]
        public async Task VerifyAuthContractAsync_EmptyEmail_ReturnsError()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_EMAIL"));
        }

        [Test]
        public async Task VerifyAuthContractAsync_CorrelationIdPropagates()
        {
            var cid = Guid.NewGuid().ToString();
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "user@example.com", CorrelationId = cid });
            Assert.That(result.CorrelationId, Is.EqualTo(cid));
        }

        [Test]
        public async Task VerifyAuthContractAsync_SameEmail_ReturnsSameAddress()
        {
            var r1 = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "same@example.com" });
            var r2 = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "same@example.com" });
            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress));
        }

        // ── Deployment initiation ───────────────────────────────────────────

        [Test]
        public async Task InitiateDeploymentAsync_ValidRequest_CreatesPendingDeployment()
        {
            var result = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(DeploymentReliabilityStatus.Pending));
            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task InitiateDeploymentAsync_IdempotencyKeyReplay_ReturnsSameDeploymentWithReplayFlag()
        {
            var req = BuildDeploymentRequest("idem-key-1");
            var r1 = await _service.InitiateDeploymentAsync(req);
            var r2 = await _service.InitiateDeploymentAsync(req);
            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task InitiateDeploymentAsync_MissingTokenName_ReturnsError()
        {
            var req = BuildDeploymentRequest();
            req.TokenName = null;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task InitiateDeploymentAsync_MissingDeployerAddress_ReturnsError()
        {
            var req = BuildDeploymentRequest();
            req.DeployerAddress = null;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_DEPLOYER_ADDRESS"));
        }

        [Test]
        public async Task InitiateDeploymentAsync_MissingNetwork_ReturnsError()
        {
            var req = BuildDeploymentRequest();
            req.Network = null;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task InitiateDeploymentAsync_MultipleDeployments_GetUniqueIds()
        {
            var r1 = await _service.InitiateDeploymentAsync(BuildDeploymentRequest("key-a"));
            var r2 = await _service.InitiateDeploymentAsync(BuildDeploymentRequest("key-b"));
            Assert.That(r1.DeploymentId, Is.Not.EqualTo(r2.DeploymentId));
        }

        // ── Deployment status ───────────────────────────────────────────────

        [Test]
        public async Task GetDeploymentStatusAsync_ValidId_ReturnsCorrectStatus()
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            var status = await _service.GetDeploymentStatusAsync(r.DeploymentId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.Status, Is.EqualTo(DeploymentReliabilityStatus.Pending));
        }

        [Test]
        public async Task GetDeploymentStatusAsync_InvalidId_ReturnsNull()
        {
            var result = await _service.GetDeploymentStatusAsync("nonexistent-id", null);
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetDeploymentStatusAsync_CorrelationIdPropagates()
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            var cid = Guid.NewGuid().ToString();
            var status = await _service.GetDeploymentStatusAsync(r.DeploymentId!, cid);
            Assert.That(status!.CorrelationId, Is.EqualTo(cid));
        }

        // ── Status transitions ──────────────────────────────────────────────

        [Test]
        public async Task TransitionDeploymentStatusAsync_PendingToAccepted_Succeeds()
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Accepted });
            Assert.That(t.Success, Is.True);
            Assert.That(t.Status, Is.EqualTo(DeploymentReliabilityStatus.Accepted));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_AcceptedToQueued_Succeeds()
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Accepted });
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Queued });
            Assert.That(t.Success, Is.True);
            Assert.That(t.Status, Is.EqualTo(DeploymentReliabilityStatus.Queued));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_QueuedToProcessing_Succeeds()
        {
            var id = await CreateDeploymentAtStatus(DeploymentReliabilityStatus.Queued);
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = id, TargetStatus = DeploymentReliabilityStatus.Processing });
            Assert.That(t.Status, Is.EqualTo(DeploymentReliabilityStatus.Processing));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_ProcessingToCompleted_Succeeds()
        {
            var id = await CreateDeploymentAtStatus(DeploymentReliabilityStatus.Processing);
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = id, TargetStatus = DeploymentReliabilityStatus.Completed });
            Assert.That(t.Status, Is.EqualTo(DeploymentReliabilityStatus.Completed));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_InvalidTransition_ReturnsInvalidStateTransitionError()
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Completed });
            Assert.That(t.Success, Is.False);
            Assert.That(t.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_CompletedIsTerminal_BlocksTransition()
        {
            var id = await CreateDeploymentAtStatus(DeploymentReliabilityStatus.Completed);
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = id, TargetStatus = DeploymentReliabilityStatus.Processing });
            Assert.That(t.Success, Is.False);
            Assert.That(t.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_CancelledIsTerminal_BlocksTransition()
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Cancelled });
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Pending });
            Assert.That(t.Success, Is.False);
            Assert.That(t.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ── Compliance checks ───────────────────────────────────────────────

        [Test]
        public async Task RunComplianceCheckAsync_ValidRequest_ReturnsNormalizedResponse()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            { AssetId = "12345", CheckType = "kyc" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(ComplianceOutcome.Pass));
        }

        [Test]
        public async Task RunComplianceCheckAsync_MissingAssetId_ReturnsError()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { CheckType = "kyc" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ASSET_ID"));
        }

        [Test]
        public async Task RunComplianceCheckAsync_MissingCheckType_ReturnsError()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "12345" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_CHECK_TYPE"));
        }

        [Test]
        public async Task RunComplianceCheckAsync_UnknownCheckType_ReturnsWarningNotError()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            { AssetId = "12345", CheckType = "unknown-type" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(ComplianceOutcome.Warning));
        }

        [Test]
        public async Task RunComplianceCheckAsync_CorrelationIdPropagates()
        {
            var cid = Guid.NewGuid().ToString();
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            { AssetId = "12345", CheckType = "aml", CorrelationId = cid });
            Assert.That(result.CorrelationId, Is.EqualTo(cid));
        }

        // ── Observability traces ────────────────────────────────────────────

        [Test]
        public async Task CreateTraceAsync_CreatesTraceWithNonEmptyTraceId()
        {
            var result = await _service.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "test-op" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.TraceId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task CreateTraceAsync_CorrelationIdPropagates()
        {
            var cid = Guid.NewGuid().ToString();
            var result = await _service.CreateTraceAsync(new ObservabilityTraceRequest { CorrelationId = cid });
            Assert.That(result.CorrelationId, Is.EqualTo(cid));
        }

        [Test]
        public async Task CreateTraceAsync_OperationNameIsRecorded()
        {
            var result = await _service.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "my-operation" });
            Assert.That(result.OperationName, Is.EqualTo("my-operation"));
        }

        // ── Audit log ───────────────────────────────────────────────────────

        [Test]
        public async Task GetAuditEvents_RecordsEventsForEachOperation()
        {
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "audit@test.com" });
            var events = _service.GetAuditEvents();
            Assert.That(events.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetAuditEvents_FilterByCorrelationId_ReturnsOnlyMatchingEvents()
        {
            var cid = Guid.NewGuid().ToString();
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "filter@test.com", CorrelationId = cid });
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "other@test.com", CorrelationId = "other-cid" });
            var events = _service.GetAuditEvents(cid);
            Assert.That(events.All(e => e.CorrelationId == cid), Is.True);
            Assert.That(events.Count, Is.GreaterThan(0));
        }

        // ── Retry logic ─────────────────────────────────────────────────────

        [Test]
        public async Task TransitionDeploymentStatusAsync_FailedWithRetriesRemaining_AllowsRetrying()
        {
            var req = BuildDeploymentRequest();
            req.MaxRetries = 3;
            var r = await _service.InitiateDeploymentAsync(req);
            // Advance to Processing→Failed
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Accepted });
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Queued });
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Processing });
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Failed });
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Retrying });
            Assert.That(t.Status, Is.EqualTo(DeploymentReliabilityStatus.Retrying));
        }

        [Test]
        public async Task TransitionDeploymentStatusAsync_FailedWithZeroMaxRetries_TransitionToCancelledWhenRetryingRequested()
        {
            var req = BuildDeploymentRequest();
            req.MaxRetries = 0;
            var r = await _service.InitiateDeploymentAsync(req);
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Accepted });
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Queued });
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Processing });
            await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Failed });
            // With MaxRetries=0, requesting Retrying should produce Cancelled
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Retrying });
            Assert.That(t.Status, Is.EqualTo(DeploymentReliabilityStatus.Cancelled));
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static DeploymentReliabilityRequest BuildDeploymentRequest(string? idempotencyKey = null)
            => new()
            {
                TokenName = "TestToken",
                TokenStandard = "ASA",
                DeployerAddress = "DEPLOYER123",
                Network = "algorand-mainnet",
                IdempotencyKey = idempotencyKey,
                MaxRetries = 3
            };

        private async Task<string> CreateDeploymentAtStatus(DeploymentReliabilityStatus targetStatus)
        {
            var r = await _service.InitiateDeploymentAsync(BuildDeploymentRequest());
            var id = r.DeploymentId!;
            var path = new List<DeploymentReliabilityStatus>();

            switch (targetStatus)
            {
                case DeploymentReliabilityStatus.Accepted:
                    path.Add(DeploymentReliabilityStatus.Accepted);
                    break;
                case DeploymentReliabilityStatus.Queued:
                    path.AddRange(new[] { DeploymentReliabilityStatus.Accepted, DeploymentReliabilityStatus.Queued });
                    break;
                case DeploymentReliabilityStatus.Processing:
                    path.AddRange(new[] { DeploymentReliabilityStatus.Accepted, DeploymentReliabilityStatus.Queued, DeploymentReliabilityStatus.Processing });
                    break;
                case DeploymentReliabilityStatus.Completed:
                    path.AddRange(new[] { DeploymentReliabilityStatus.Accepted, DeploymentReliabilityStatus.Queued, DeploymentReliabilityStatus.Processing, DeploymentReliabilityStatus.Completed });
                    break;
                case DeploymentReliabilityStatus.Failed:
                    path.AddRange(new[] { DeploymentReliabilityStatus.Accepted, DeploymentReliabilityStatus.Queued, DeploymentReliabilityStatus.Processing, DeploymentReliabilityStatus.Failed });
                    break;
            }

            foreach (var s in path)
                await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest { DeploymentId = id, TargetStatus = s });

            return id;
        }
    }
}

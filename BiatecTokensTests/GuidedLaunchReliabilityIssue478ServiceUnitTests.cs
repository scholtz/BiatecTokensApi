using BiatecTokensApi.Models.GuidedLaunchReliability;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for GuidedLaunchReliabilityService (Issue #478).
    /// All tests use direct service instantiation – no HTTP calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class GuidedLaunchReliabilityIssue478ServiceUnitTests
    {
        private GuidedLaunchReliabilityService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<GuidedLaunchReliabilityService>>();
            _service = new GuidedLaunchReliabilityService(logger.Object);
        }

        // ── Initiate launch ────────────────────────────────────────────────

        [Test]
        public async Task InitiateLaunchAsync_ValidRequest_ReturnsSuccess()
        {
            var result = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.LaunchId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Stage, Is.EqualTo(LaunchStage.TokenDetails));
        }

        [Test]
        public async Task InitiateLaunchAsync_MissingTokenName_ReturnsError()
        {
            var req = BuildInitiateRequest();
            req.TokenName = null;
            var result = await _service.InitiateLaunchAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task InitiateLaunchAsync_EmptyTokenName_ReturnsError()
        {
            var req = BuildInitiateRequest();
            req.TokenName = "  ";
            var result = await _service.InitiateLaunchAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task InitiateLaunchAsync_MissingTokenStandard_ReturnsError()
        {
            var req = BuildInitiateRequest();
            req.TokenStandard = null;
            var result = await _service.InitiateLaunchAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task InitiateLaunchAsync_InvalidTokenStandard_ReturnsError()
        {
            var req = BuildInitiateRequest();
            req.TokenStandard = "UNKNOWN_STANDARD";
            var result = await _service.InitiateLaunchAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_TOKEN_STANDARD"));
        }

        [Test]
        public async Task InitiateLaunchAsync_MissingOwnerId_ReturnsError()
        {
            var req = BuildInitiateRequest();
            req.OwnerId = null;
            var result = await _service.InitiateLaunchAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task InitiateLaunchAsync_AllSupportedStandards_Succeed()
        {
            foreach (var std in new[] { "ASA", "ARC3", "ARC200", "ERC20", "ARC1400" })
            {
                var req = BuildInitiateRequest();
                req.TokenStandard = std;
                var result = await _service.InitiateLaunchAsync(req);
                Assert.That(result.Success, Is.True, $"Standard {std} should succeed");
            }
        }

        [Test]
        public async Task InitiateLaunchAsync_IdempotencyKeyReplay_ReturnsSameId()
        {
            var req = BuildInitiateRequest("idem-key-1");
            var r1 = await _service.InitiateLaunchAsync(req);
            var r2 = await _service.InitiateLaunchAsync(req);
            Assert.That(r2.LaunchId, Is.EqualTo(r1.LaunchId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task InitiateLaunchAsync_DifferentIdempotencyKeys_CreateDifferentLaunches()
        {
            var r1 = await _service.InitiateLaunchAsync(BuildInitiateRequest("key-A"));
            var r2 = await _service.InitiateLaunchAsync(BuildInitiateRequest("key-B"));
            Assert.That(r1.LaunchId, Is.Not.EqualTo(r2.LaunchId));
            Assert.That(r2.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task InitiateLaunchAsync_CorrelationIdPropagates()
        {
            var cid = Guid.NewGuid().ToString();
            var req = BuildInitiateRequest();
            req.CorrelationId = cid;
            var result = await _service.InitiateLaunchAsync(req);
            Assert.That(result.CorrelationId, Is.EqualTo(cid));
        }

        [Test]
        public async Task InitiateLaunchAsync_SchemaVersionPresent()
        {
            var result = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task InitiateLaunchAsync_NextActionPopulated()
        {
            var result = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(result.NextAction, Is.Not.Null.And.Not.Empty);
        }

        // ── Get status ─────────────────────────────────────────────────────

        [Test]
        public async Task GetLaunchStatusAsync_KnownId_ReturnsStatus()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var status = await _service.GetLaunchStatusAsync(r.LaunchId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.LaunchId, Is.EqualTo(r.LaunchId));
            Assert.That(status.Stage, Is.EqualTo(LaunchStage.TokenDetails));
        }

        [Test]
        public async Task GetLaunchStatusAsync_UnknownId_ReturnsNull()
        {
            var status = await _service.GetLaunchStatusAsync("non-existent-id", null);
            Assert.That(status, Is.Null);
        }

        [Test]
        public async Task GetLaunchStatusAsync_IncludesCompletedSteps()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = r.LaunchId });
            var status = await _service.GetLaunchStatusAsync(r.LaunchId!, null);
            Assert.That(status!.CompletedSteps, Is.Not.Empty);
        }

        // ── Advance launch ─────────────────────────────────────────────────

        [Test]
        public async Task AdvanceLaunchAsync_FromTokenDetails_AdvancesToComplianceSetup()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = r.LaunchId });
            Assert.That(adv.Success, Is.True);
            Assert.That(adv.PreviousStage, Is.EqualTo(LaunchStage.TokenDetails));
            Assert.That(adv.CurrentStage, Is.EqualTo(LaunchStage.ComplianceSetup));
        }

        [Test]
        public async Task AdvanceLaunchAsync_FullPath_ReachesSubmitted()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var launchId = r.LaunchId!;
            await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = launchId });
            await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = launchId });
            await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = launchId });
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = launchId });
            Assert.That(adv.CurrentStage, Is.EqualTo(LaunchStage.Submitted));
        }

        [Test]
        public async Task AdvanceLaunchAsync_MissingLaunchId_ReturnsError()
        {
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = null });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("MISSING_LAUNCH_ID"));
        }

        [Test]
        public async Task AdvanceLaunchAsync_UnknownLaunchId_ReturnsError()
        {
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = "ghost-id" });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("LAUNCH_NOT_FOUND"));
        }

        [Test]
        public async Task AdvanceLaunchAsync_AfterCancel_ReturnsTerminalError()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = r.LaunchId });
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = r.LaunchId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("LAUNCH_TERMINAL"));
        }

        [Test]
        public async Task AdvanceLaunchAsync_ComplianceMessages_PopulatedAtComplianceSetup()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = r.LaunchId });
            Assert.That(adv.ComplianceMessages, Is.Not.Empty);
        }

        [Test]
        public async Task AdvanceLaunchAsync_CorrelationIdPropagates()
        {
            var cid = Guid.NewGuid().ToString();
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest
                { LaunchId = r.LaunchId, CorrelationId = cid });
            Assert.That(adv.CorrelationId, Is.EqualTo(cid));
        }

        // ── Validate step ──────────────────────────────────────────────────

        [Test]
        public async Task ValidateStepAsync_ValidTokenDetails_IsValid()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "token-details",
                StepInputs = new Dictionary<string, string> { ["tokenName"] = "MyToken" }
            });
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public async Task ValidateStepAsync_TokenDetailsWithoutName_IsInvalid()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "token-details",
                StepInputs = new Dictionary<string, string>()
            });
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ValidationMessages, Has.Some.Matches<ComplianceUxMessage>(
                m => m.Code == "STEP_MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task ValidateStepAsync_ComplianceSetupWithJurisdiction_IsValid()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "compliance-setup",
                StepInputs = new Dictionary<string, string> { ["jurisdiction"] = "EU" }
            });
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public async Task ValidateStepAsync_ComplianceSetupWithoutJurisdiction_IsInvalid()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "compliance-setup",
                StepInputs = new Dictionary<string, string>()
            });
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ValidationMessages, Has.Some.Matches<ComplianceUxMessage>(
                m => m.Code == "STEP_MISSING_JURISDICTION"));
        }

        [Test]
        public async Task ValidateStepAsync_NetworkSelectionWithoutNetwork_IsInvalid()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "network-selection",
                StepInputs = new Dictionary<string, string>()
            });
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ValidationMessages, Has.Some.Matches<ComplianceUxMessage>(
                m => m.Code == "STEP_MISSING_NETWORK"));
        }

        [Test]
        public async Task ValidateStepAsync_MissingLaunchId_ReturnsError()
        {
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = null,
                StepName = "token-details"
            });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_LAUNCH_ID"));
        }

        [Test]
        public async Task ValidateStepAsync_InvalidStepName_ReturnsError()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "unknown-step"
            });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_STEP_NAME"));
        }

        [Test]
        public async Task ValidateStepAsync_UnknownLaunchId_ReturnsError()
        {
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = "ghost-id",
                StepName = "token-details"
            });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("LAUNCH_NOT_FOUND"));
        }

        [Test]
        public async Task ValidateStepAsync_ValidationMessages_HaveWhatWhyHow()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "compliance-setup",
                StepInputs = new Dictionary<string, string>()
            });
            foreach (var msg in result.ValidationMessages)
            {
                Assert.That(msg.What, Is.Not.Null.And.Not.Empty, "What must be populated");
                Assert.That(msg.Why, Is.Not.Null.And.Not.Empty, "Why must be populated");
                Assert.That(msg.How, Is.Not.Null.And.Not.Empty, "How must be populated");
            }
        }

        // ── Cancel launch ──────────────────────────────────────────────────

        [Test]
        public async Task CancelLaunchAsync_ActiveLaunch_Succeeds()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var cancel = await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = r.LaunchId });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.FinalStage, Is.EqualTo(LaunchStage.Cancelled));
        }

        [Test]
        public async Task CancelLaunchAsync_MissingLaunchId_ReturnsError()
        {
            var cancel = await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = null });
            Assert.That(cancel.Success, Is.False);
            Assert.That(cancel.ErrorCode, Is.EqualTo("MISSING_LAUNCH_ID"));
        }

        [Test]
        public async Task CancelLaunchAsync_UnknownLaunchId_ReturnsError()
        {
            var cancel = await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = "no-such-id" });
            Assert.That(cancel.Success, Is.False);
            Assert.That(cancel.ErrorCode, Is.EqualTo("LAUNCH_NOT_FOUND"));
        }

        [Test]
        public async Task CancelLaunchAsync_AlreadyCancelled_ReturnsTerminalError()
        {
            var r = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = r.LaunchId });
            var cancel2 = await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = r.LaunchId });
            Assert.That(cancel2.Success, Is.False);
            Assert.That(cancel2.ErrorCode, Is.EqualTo("LAUNCH_TERMINAL"));
        }

        // ── Audit log ──────────────────────────────────────────────────────

        [Test]
        public async Task GetAuditEvents_AfterInitiate_ContainsInitiateEvent()
        {
            var cid = Guid.NewGuid().ToString();
            var req = BuildInitiateRequest();
            req.CorrelationId = cid;
            await _service.InitiateLaunchAsync(req);
            var events = _service.GetAuditEvents(cid);
            Assert.That(events, Has.Some.Matches<GuidedLaunchAuditEvent>(e => e.OperationName == "InitiateLaunch"));
        }

        [Test]
        public async Task GetAuditEvents_NoCorrelationFilter_ReturnsAll()
        {
            await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var events = _service.GetAuditEvents();
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task GetAuditEvents_CorrelationFilter_ReturnsOnlyMatching()
        {
            var cid = Guid.NewGuid().ToString();
            var req = BuildInitiateRequest();
            req.CorrelationId = cid;
            await _service.InitiateLaunchAsync(req);
            await _service.InitiateLaunchAsync(BuildInitiateRequest()); // different correlation
            var events = _service.GetAuditEvents(cid);
            Assert.That(events.All(e => e.CorrelationId == cid), Is.True);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static GuidedLaunchInitiateRequest BuildInitiateRequest(string? idempotencyKey = null) =>
            new()
            {
                TokenName = "TestToken",
                TokenStandard = "ASA",
                OwnerId = "owner-001",
                IdempotencyKey = idempotencyKey
            };
    }
}

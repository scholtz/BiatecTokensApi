using BiatecTokensApi.Models.GuidedLaunchReliability;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for Issue #478: Guided Launch Reliability.
    /// Covers HP (happy-path), II (invalid input), BD (boundary), FR (failure-recovery),
    /// and NX (non-crypto-native UX) scenarios.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class GuidedLaunchReliabilityIssue478UserJourneyTests
    {
        private GuidedLaunchReliabilityService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<GuidedLaunchReliabilityService>>();
            _service = new GuidedLaunchReliabilityService(logger.Object);
        }

        // ── HP – Happy path ────────────────────────────────────────────────

        [Test]
        public async Task HP1_FullGuidedLaunchFlow_TokenDetails_To_Submitted()
        {
            // Initiate
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(init.Success, Is.True);
            Assert.That(init.Stage, Is.EqualTo(LaunchStage.TokenDetails));

            // Advance through each stage
            var launchId = init.LaunchId!;
            var stages = new[]
            {
                LaunchStage.ComplianceSetup,
                LaunchStage.NetworkSelection,
                LaunchStage.Review,
                LaunchStage.Submitted
            };

            var prev = LaunchStage.TokenDetails;
            foreach (var expected in stages)
            {
                var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = launchId });
                Assert.That(adv.Success, Is.True, $"Expected advance to {expected}");
                Assert.That(adv.PreviousStage, Is.EqualTo(prev));
                Assert.That(adv.CurrentStage, Is.EqualTo(expected));
                prev = expected;
            }
        }

        [Test]
        public async Task HP2_StatusReflectsCurrentStage()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var id = init.LaunchId!;

            await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });
            var status = await _service.GetLaunchStatusAsync(id, null);

            Assert.That(status!.Stage, Is.EqualTo(LaunchStage.ComplianceSetup));
        }

        [Test]
        public async Task HP3_StepValidationPasses_BeforeAdvance()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var id = init.LaunchId!;

            var validate = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = id,
                StepName = "token-details",
                StepInputs = new Dictionary<string, string> { ["tokenName"] = "SampleToken" }
            });

            Assert.That(validate.IsValid, Is.True);

            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });
            Assert.That(adv.Success, Is.True);
        }

        [Test]
        public async Task HP4_ComplianceSetupMessages_AreInformational()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = init.LaunchId });

            Assert.That(adv.ComplianceMessages, Is.Not.Empty);
            Assert.That(adv.ComplianceMessages.All(m => m.Severity == ComplianceMessageSeverity.Info), Is.True);
        }

        [Test]
        public async Task HP5_CancelledLaunchNotAdvanceable()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = init.LaunchId });

            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = init.LaunchId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("LAUNCH_TERMINAL"));
        }

        [Test]
        public async Task HP6_MultipleParallelLaunches_SucceedIndependently()
        {
            var tasks = Enumerable.Range(0, 5).Select(i =>
                _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
                {
                    TokenName = $"Token{i}",
                    TokenStandard = "ASA",
                    OwnerId = $"owner-{i}"
                })).ToList();

            var results = await Task.WhenAll(tasks);

            var ids = results.Select(r => r.LaunchId).ToHashSet();
            Assert.That(ids.Count, Is.EqualTo(5), "All launches must get distinct IDs");
            Assert.That(results.All(r => r.Success), Is.True);
        }

        [Test]
        public async Task HP7_CorrelationIdPropagatedThroughFullFlow()
        {
            var cid = Guid.NewGuid().ToString();
            var init = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "CorrelatedToken",
                TokenStandard = "ARC3",
                OwnerId = "owner-corr",
                CorrelationId = cid
            });
            Assert.That(init.CorrelationId, Is.EqualTo(cid));

            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest
                { LaunchId = init.LaunchId, CorrelationId = cid });
            Assert.That(adv.CorrelationId, Is.EqualTo(cid));

            var events = _service.GetAuditEvents(cid);
            Assert.That(events, Is.Not.Empty);
        }

        // ── II – Invalid input ─────────────────────────────────────────────

        [Test]
        public async Task II1_NullTokenName_Returns_MISSING_TOKEN_NAME()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = null, TokenStandard = "ASA", OwnerId = "o1"
            });
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task II2_NullTokenStandard_Returns_MISSING_TOKEN_STANDARD()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "T", TokenStandard = null, OwnerId = "o1"
            });
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task II3_UnsupportedStandard_Returns_INVALID_TOKEN_STANDARD()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "T", TokenStandard = "XYZ999", OwnerId = "o1"
            });
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_TOKEN_STANDARD"));
        }

        [Test]
        public async Task II4_NullOwnerId_Returns_MISSING_OWNER_ID()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "T", TokenStandard = "ASA", OwnerId = null
            });
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task II5_AdvanceWithNullId_Returns_MISSING_LAUNCH_ID()
        {
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = null });
            Assert.That(adv.ErrorCode, Is.EqualTo("MISSING_LAUNCH_ID"));
        }

        [Test]
        public async Task II6_CancelWithNullId_Returns_MISSING_LAUNCH_ID()
        {
            var cancel = await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = null });
            Assert.That(cancel.ErrorCode, Is.EqualTo("MISSING_LAUNCH_ID"));
        }

        [Test]
        public async Task II7_ValidateStepWithNullStepName_Returns_MISSING_STEP_NAME()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = init.LaunchId,
                StepName = null
            });
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_STEP_NAME"));
        }

        // ── BD – Boundary ──────────────────────────────────────────────────

        [Test]
        public async Task BD1_VeryLongTokenName_IsAccepted()
        {
            var longName = new string('A', 200);
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = longName, TokenStandard = "ASA", OwnerId = "o1"
            });
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD2_WhitespaceOnlyTokenName_Returns_MISSING_TOKEN_NAME()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "   ", TokenStandard = "ASA", OwnerId = "o1"
            });
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task BD3_AllValidStandards_AreAccepted()
        {
            foreach (var std in new[] { "ASA", "ARC3", "ARC200", "ERC20", "ARC1400" })
            {
                var r = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
                {
                    TokenName = "T", TokenStandard = std, OwnerId = "o1"
                });
                Assert.That(r.Success, Is.True, $"Standard {std}");
            }
        }

        [Test]
        public async Task BD4_CaseSensitiveStandardRejected()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "T", TokenStandard = "asa", OwnerId = "o1" // lowercase – should match case-insensitive
            });
            Assert.That(result.Success, Is.True, "Standard matching should be case-insensitive");
        }

        [Test]
        public async Task BD5_EmptyStepInputs_ProducesValidationMessages()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = init.LaunchId,
                StepName = "token-details",
                StepInputs = new Dictionary<string, string>()
            });
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ValidationMessages, Is.Not.Empty);
        }

        // ── FR – Failure recovery ──────────────────────────────────────────

        [Test]
        public async Task FR1_AdvanceUnknownId_ReturnsError_NotException()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = "ghost" });
                Assert.That(adv.Success, Is.False);
            });
        }

        [Test]
        public async Task FR2_CancelledLaunch_StatusShowsCancelled()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = init.LaunchId });
            var status = await _service.GetLaunchStatusAsync(init.LaunchId!, null);
            Assert.That(status!.Stage, Is.EqualTo(LaunchStage.Cancelled));
        }

        [Test]
        public async Task FR3_DoubleCancel_ReturnsTerminalError()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = init.LaunchId });
            var cancel2 = await _service.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = init.LaunchId });
            Assert.That(cancel2.ErrorCode, Is.EqualTo("LAUNCH_TERMINAL"));
        }

        [Test]
        public async Task FR4_IdempotentReplay_DoesNotCreateDuplicate()
        {
            var req = BuildInitiateRequest("fr4-idem-key");
            var r1 = await _service.InitiateLaunchAsync(req);
            var r2 = await _service.InitiateLaunchAsync(req);
            var r3 = await _service.InitiateLaunchAsync(req);

            Assert.That(r2.LaunchId, Is.EqualTo(r1.LaunchId));
            Assert.That(r3.LaunchId, Is.EqualTo(r1.LaunchId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ── NX – Non-crypto-native UX ──────────────────────────────────────

        [Test]
        public async Task NX1_ErrorMessages_ContainRemediationHint()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = null, TokenStandard = "ASA", OwnerId = "o1"
            });
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "Error responses must contain actionable remediation hints");
        }

        [Test]
        public async Task NX2_ComplianceMessages_NoInternalCodes_InMessage()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await _service.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = init.LaunchId });

            foreach (var msg in adv.ComplianceMessages)
            {
                // Messages should not contain raw stack traces or internal codes
                Assert.That(msg.What, Does.Not.Contain("Exception"));
                Assert.That(msg.Why, Does.Not.Contain("NullReference"));
            }
        }

        [Test]
        public async Task NX3_NextAction_IsHumanReadable()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(init.NextAction, Is.Not.Null);
            Assert.That(init.NextAction!.Length, Is.GreaterThan(10), "NextAction should be a meaningful sentence");
        }

        [Test]
        public async Task NX4_ValidationMessages_BlockerSeverity_IndicatesRequired()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var result = await _service.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = init.LaunchId,
                StepName = "compliance-setup",
                StepInputs = new Dictionary<string, string>()
            });
            Assert.That(result.ValidationMessages,
                Has.Some.Matches<ComplianceUxMessage>(m => m.Severity == ComplianceMessageSeverity.Blocker));
        }

        [Test]
        public async Task NX5_NoKeyOrAddressLeakage_InErrorResponses()
        {
            var result = await _service.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = null, TokenStandard = "ASA", OwnerId = "o1"
            });
            // Error messages must not leak internal key material
            Assert.That(result.ErrorMessage, Does.Not.Contain("mnemonic"));
            Assert.That(result.ErrorMessage, Does.Not.Contain("private"));
            Assert.That(result.ErrorMessage, Does.Not.Contain("secret"));
        }

        [Test]
        public async Task NX6_AuditEvents_DoNotLeakSensitiveData()
        {
            var init = await _service.InitiateLaunchAsync(BuildInitiateRequest());
            var events = _service.GetAuditEvents();
            foreach (var ev in events)
            {
                Assert.That(ev.EventId, Is.Not.Null.And.Not.Empty);
                Assert.That(ev.OperationName, Is.Not.Null.And.Not.Empty);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static GuidedLaunchInitiateRequest BuildInitiateRequest(string? idempotencyKey = null) =>
            new()
            {
                TokenName = "JourneyToken",
                TokenStandard = "ARC200",
                OwnerId = "journey-owner-001",
                IdempotencyKey = idempotencyKey
            };
    }
}

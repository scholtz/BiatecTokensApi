using BiatecTokensApi.Models.MVPHardening;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for Issue #476: MVP Backend Hardening.
    /// Pure service-layer tests – no HTTP calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPBackendHardeningIssue476UserJourneyTests
    {
        private MVPBackendHardeningService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<MVPBackendHardeningService>>();
            _service = new MVPBackendHardeningService(logger.Object);
        }

        // ── HP: Happy Path ───────────────────────────────────────────────────

        [Test]
        public async Task HP1_CompleteWorkflow_AuthDeploymentComplianceTrace()
        {
            var cid = Guid.NewGuid().ToString();
            var auth = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "hp1@test.com", CorrelationId = cid });
            Assert.That(auth.Success, Is.True);

            var dep = await _service.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "HP1Token", DeployerAddress = auth.AlgorandAddress, Network = "algorand-mainnet", CorrelationId = cid });
            Assert.That(dep.Success, Is.True);

            var comp = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "1001", CheckType = "kyc", CorrelationId = cid });
            Assert.That(comp.Success, Is.True);

            var trace = await _service.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "hp1-workflow", CorrelationId = cid });
            Assert.That(trace.Success, Is.True);
        }

        [Test]
        public async Task HP2_RepeatedAuthVerification_GivesSameResult()
        {
            var r1 = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "repeat@test.com" });
            var r2 = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "repeat@test.com" });
            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress));
        }

        [Test]
        public async Task HP3_DeploymentLifecycle_PendingToCompleted()
        {
            var r = await _service.InitiateDeploymentAsync(BuildRequest());
            Assert.That(r.Status, Is.EqualTo(DeploymentReliabilityStatus.Pending));
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Accepted);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Queued);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Processing);
            var final = await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Completed);
            Assert.That(final.Status, Is.EqualTo(DeploymentReliabilityStatus.Completed));
        }

        [Test]
        public async Task HP4_ComplianceCheck_ReturnsNormalizedResponse()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "5555", CheckType = "sanctions" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(result.Details, Is.Not.Null);
        }

        [Test]
        public async Task HP5_TraceCreatesValidTraceId()
        {
            var r = await _service.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "hp5" });
            Assert.That(r.TraceId, Is.Not.Null.And.Not.Empty);
            Assert.That(Guid.TryParse(r.TraceId, out _), Is.True);
        }

        [Test]
        public async Task HP6_MultipleDeployments_AreIndependent()
        {
            var r1 = await _service.InitiateDeploymentAsync(BuildRequest("hp6-key-a"));
            var r2 = await _service.InitiateDeploymentAsync(BuildRequest("hp6-key-b"));
            Assert.That(r1.DeploymentId, Is.Not.EqualTo(r2.DeploymentId));
            Assert.That(r1.Status, Is.EqualTo(DeploymentReliabilityStatus.Pending));
            Assert.That(r2.Status, Is.EqualTo(DeploymentReliabilityStatus.Pending));
        }

        [Test]
        public async Task HP7_AuditEventsContainRequiredFields()
        {
            var cid = Guid.NewGuid().ToString();
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "audit@hp7.com", CorrelationId = cid });
            var events = _service.GetAuditEvents(cid);
            Assert.That(events, Is.Not.Empty);
            foreach (var e in events)
            {
                Assert.That(e.EventId, Is.Not.Null.And.Not.Empty);
                Assert.That(e.OperationName, Is.Not.Null.And.Not.Empty);
                Assert.That(e.OccurredAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)));
            }
        }

        // ── II: Invalid Input ────────────────────────────────────────────────

        [Test]
        public async Task II1_NullEmailForAuth_ReturnsError()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = null });
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II2_NullRequestForDeployment_ReturnsError()
        {
            var result = await _service.InitiateDeploymentAsync(null!);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II3_EmptyTokenName_ReturnsError()
        {
            var req = BuildRequest();
            req.TokenName = "";
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task II4_EmptyNetwork_ReturnsError()
        {
            var req = BuildRequest();
            req.Network = "";
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task II5_NullComplianceRequest_ReturnsError()
        {
            var result = await _service.RunComplianceCheckAsync(null!);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II6_MissingAssetId_ReturnsError()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { CheckType = "kyc" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ASSET_ID"));
        }

        [Test]
        public async Task II7_MissingCheckType_ReturnsError()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "12345" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_CHECK_TYPE"));
        }

        // ── BD: Boundary ─────────────────────────────────────────────────────

        [Test]
        public async Task BD1_VeryLongEmail_DoesNotThrow()
        {
            var longEmail = new string('a', 500) + "@example.com";
            Assert.DoesNotThrowAsync(async () =>
                await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = longEmail }));
        }

        [Test]
        public async Task BD2_VeryLongTokenName_DoesNotThrow()
        {
            var req = BuildRequest();
            req.TokenName = new string('T', 500);
            Assert.DoesNotThrowAsync(async () => await _service.InitiateDeploymentAsync(req));
        }

        [Test]
        public async Task BD3_ZeroMaxRetries_HandledGracefully()
        {
            var req = BuildRequest();
            req.MaxRetries = 0;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Success, Is.True);
            Assert.That(result.MaxRetries, Is.EqualTo(0));
        }

        [Test]
        public async Task BD4_MaliciousEmailString_DoesNotThrow()
        {
            var injection = "' OR 1=1; DROP TABLE users; --@example.com";
            Assert.DoesNotThrowAsync(async () =>
                await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = injection }));
        }

        [Test]
        public async Task BD5_HtmlSpecialCharactersInTokenName_DoesNotThrow()
        {
            var xss = "<script>alert('xss')</script>";
            var req = BuildRequest();
            req.TokenName = xss;
            Assert.DoesNotThrowAsync(async () => await _service.InitiateDeploymentAsync(req));
        }

        // ── FR: Failure Recovery ─────────────────────────────────────────────

        [Test]
        public async Task FR1_DeploymentFails_ThenRetries()
        {
            var r = await _service.InitiateDeploymentAsync(BuildRequest());
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Accepted);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Queued);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Processing);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Failed);
            var retrying = await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Retrying);
            Assert.That(retrying.Status, Is.EqualTo(DeploymentReliabilityStatus.Retrying));
        }

        [Test]
        public async Task FR2_RetryWithIdempotencyKey_ReturnsSameDeployment()
        {
            var req = BuildRequest("fr2-idem");
            var r1 = await _service.InitiateDeploymentAsync(req);
            var r2 = await _service.InitiateDeploymentAsync(req);
            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task FR3_UnknownComplianceCheckType_ReturnsWarningNotError()
        {
            var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            { AssetId = "99", CheckType = "custom-unknown-type" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(ComplianceOutcome.Warning));
        }

        [Test]
        public async Task FR4_FailedDeploymentCanTransitionToRetrying()
        {
            var req = BuildRequest();
            req.MaxRetries = 5;
            var r = await _service.InitiateDeploymentAsync(req);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Accepted);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Queued);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Processing);
            await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Failed);
            var t = await Transition(r.DeploymentId!, DeploymentReliabilityStatus.Retrying);
            Assert.That(t.Success, Is.True);
        }

        // ── NX: Non-crypto-native UX ─────────────────────────────────────────

        [Test]
        public async Task NX1_ErrorMessages_AreUserFriendly()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = null });
            Assert.That(result.ErrorMessage, Does.Not.Contain("Exception"));
            Assert.That(result.ErrorMessage, Does.Not.Contain("StackTrace"));
        }

        [Test]
        public async Task NX2_NoPrivateKeyLeakage_InResponses()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "nox2@test.com" });
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.That(json.ToLower(), Does.Not.Contain("mnemonic"));
            Assert.That(json.ToLower(), Does.Not.Contain("privatekey"));
            Assert.That(json.ToLower(), Does.Not.Contain("secret"));
        }

        [Test]
        public async Task NX3_CorrelationIds_PresentInAllResponses()
        {
            var cid = Guid.NewGuid().ToString();
            var auth = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "nx3@test.com", CorrelationId = cid });
            var dep = await _service.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "NX3", DeployerAddress = "ADDR", Network = "algorand-mainnet", CorrelationId = cid });
            var comp = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "1", CheckType = "kyc", CorrelationId = cid });
            Assert.That(auth.CorrelationId, Is.EqualTo(cid));
            Assert.That(dep.CorrelationId, Is.EqualTo(cid));
            Assert.That(comp.CorrelationId, Is.EqualTo(cid));
        }

        [Test]
        public async Task NX4_RemediationHints_PresentOnFailures()
        {
            var result = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "bad-email" });
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task NX5_SchemaVersion_AlwaysPresent()
        {
            var auth = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "nx5@test.com" });
            var dep = await _service.InitiateDeploymentAsync(BuildRequest());
            var comp = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "1", CheckType = "kyc" });
            var trace = await _service.CreateTraceAsync(new ObservabilityTraceRequest());
            Assert.That(auth.SchemaVersion, Is.Not.Null);
            Assert.That(dep.SchemaVersion, Is.Not.Null);
            Assert.That(comp.SchemaVersion, Is.Not.Null);
            Assert.That(trace.SchemaVersion, Is.Not.Null);
        }

        [Test]
        public async Task NX6_ErrorCodes_UseStableTaxonomyNotExceptionTypeNames()
        {
            var auth = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = null });
            Assert.That(auth.ErrorCode, Does.Not.Contain("Exception"));
            Assert.That(auth.ErrorCode, Does.Not.Contain("Error"));
            // Stable enum-style code
            Assert.That(auth.ErrorCode, Is.EqualTo("MISSING_EMAIL").Or.EqualTo("INVALID_REQUEST"));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static DeploymentReliabilityRequest BuildRequest(string? key = null) => new()
        {
            TokenName = "JourneyToken",
            TokenStandard = "ASA",
            DeployerAddress = "DEPLOYER",
            Network = "algorand-mainnet",
            IdempotencyKey = key,
            MaxRetries = 3
        };

        private Task<DeploymentReliabilityResponse> Transition(string id, DeploymentReliabilityStatus status)
            => _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = id, TargetStatus = status });
    }
}

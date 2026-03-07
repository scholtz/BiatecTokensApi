using BiatecTokensApi.Models.ComplianceHardening;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// ~40 contract/schema tests for Issue #488: ComplianceOrchestrationHardeningService.
    /// Validates response shapes, field presence, enum validity, and non-null collections.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationHardeningIssue488ContractTests
    {
        private Mock<IComplianceOrchestrationService> _orchestrationMock = null!;
        private ComplianceOrchestrationHardeningService _svc = null!;
        private const string CorrelationId = "contract-corr-488";

        [SetUp]
        public void Setup()
        {
            _orchestrationMock = new Mock<IComplianceOrchestrationService>();
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Approved,
                    DecisionId = "test-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);
        }

        // ── ComplianceHardeningResponse schema ────────────────────────────────────

        [Test]
        public async Task HardeningResponse_EvaluationId_IsNonEmptyString()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s1", TokenId = "t1" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.EvaluationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HardeningResponse_EvaluatedAt_IsRecentUtc()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var req = new ComplianceHardeningRequest { SubjectId = "s2", TokenId = "t2" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.EvaluatedAt, Is.GreaterThan(before));
            Assert.That(result.EvaluatedAt.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public async Task HardeningResponse_CorrelationId_IsSet()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s3", TokenId = "t3" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.CorrelationId, Is.EqualTo(CorrelationId));
        }

        [Test]
        public async Task HardeningResponse_LaunchGate_IsValidEnumValue()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s4", TokenId = "t4" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(LaunchGateStatus), result.LaunchGate), Is.True);
        }

        [Test]
        public async Task HardeningResponse_ErrorCategory_IsValidEnumValue()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s5", TokenId = "t5" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(ComplianceErrorCategory), result.ErrorCategory), Is.True);
        }

        [Test]
        public async Task HardeningResponse_RemediationHints_NonNull()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s6", TokenId = "t6" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null);
        }

        [Test]
        public async Task HardeningResponse_ProviderStatuses_NonNull()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s7", TokenId = "t7" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ProviderStatuses, Is.Not.Null);
        }

        [Test]
        public async Task HardeningResponse_ProviderStatuses_Has4Entries()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s8", TokenId = "t8" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ProviderStatuses, Has.Count.EqualTo(4));
        }

        [Test]
        public async Task HardeningResponse_SchemaVersion_Is1_0()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s9", TokenId = "t9" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SchemaVersion, Is.EqualTo("1.0"));
        }

        [Test]
        public async Task HardeningResponse_Success_IsTrue_ForValidRequest()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "s10", TokenId = "t10" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.Success, Is.True);
        }

        // ── Blocked response includes non-empty RemediationHints ──────────────────

        [Test]
        public async Task HardeningResponse_BlockedJurisdiction_HasRemediationHints()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "s-blocked",
                TokenId = "t-blocked",
                JurisdictionCode = "KP"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Empty);
        }

        [Test]
        public async Task HardeningResponse_SanctionedSubject_HasRemediationHints()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Rejected,
                    DecisionId = "rej-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest { SubjectId = "s-sanc", TokenId = "t-sanc" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Empty);
        }

        // ── JurisdictionConstraintResult schema ───────────────────────────────────

        [Test]
        public async Task JurisdictionResult_ReasonCode_NonEmpty()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "s1", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task JurisdictionResult_Status_IsValidEnum()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "s1", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(JurisdictionStatus), result.Status), Is.True);
        }

        [Test]
        public async Task JurisdictionResult_Conditions_NonNull()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "s1", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Conditions, Is.Not.Null);
        }

        [Test]
        public async Task JurisdictionResult_RemediationHints_NonNull()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "s1", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null);
        }

        [Test]
        public async Task JurisdictionResult_EvaluatedAt_IsRecentUtc()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var req = new JurisdictionConstraintRequest { SubjectId = "s1", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.EvaluatedAt, Is.GreaterThan(before));
        }

        // ── SanctionsReadinessResult schema ──────────────────────────────────────

        [Test]
        public async Task SanctionsResult_Status_IsValidEnum()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "s-valid" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(SanctionsStatus), result.Status), Is.True);
        }

        [Test]
        public async Task SanctionsResult_ReasonCode_NonEmpty()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "s-valid" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task SanctionsResult_ProviderStatus_IsValidEnum()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "s-valid" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(ProviderIntegrationStatus), result.ProviderStatus), Is.True);
        }

        [Test]
        public async Task SanctionsResult_RemediationHints_NonNull()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "s-valid" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null);
        }

        [Test]
        public async Task SanctionsResult_EvaluatedAt_IsRecentUtc()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var req = new SanctionsReadinessRequest { SubjectId = "s-valid" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.EvaluatedAt, Is.GreaterThan(before));
        }

        // ── LaunchGateResponse schema ─────────────────────────────────────────────

        [Test]
        public async Task LaunchGateResponse_GateStatus_IsValidEnum()
        {
            var req = new LaunchGateRequest { TokenId = "t1", SubjectId = "s1" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(LaunchGateStatus), result.GateStatus), Is.True);
        }

        [Test]
        public async Task LaunchGateResponse_ErrorCategory_IsValidEnum()
        {
            var req = new LaunchGateRequest { TokenId = "t2", SubjectId = "s2" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(Enum.IsDefined(typeof(ComplianceErrorCategory), result.ErrorCategory), Is.True);
        }

        [Test]
        public async Task LaunchGateResponse_BlockingReasons_NonNull()
        {
            var req = new LaunchGateRequest { TokenId = "t3", SubjectId = "s3" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.BlockingReasons, Is.Not.Null);
        }

        [Test]
        public async Task LaunchGateResponse_RemediationHints_NonNull()
        {
            var req = new LaunchGateRequest { TokenId = "t4", SubjectId = "s4" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null);
        }

        [Test]
        public async Task LaunchGateResponse_EvaluatedAt_IsRecentUtc()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var req = new LaunchGateRequest { TokenId = "t5", SubjectId = "s5" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.EvaluatedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task LaunchGateResponse_ReasonCode_NonEmpty_ForSuccess()
        {
            var req = new LaunchGateRequest { TokenId = "t6", SubjectId = "s6" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        // ── ProviderStatusListResponse schema ─────────────────────────────────────

        [Test]
        public async Task ProviderStatusList_Has4Entries()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.Providers, Has.Count.EqualTo(4));
        }

        [Test]
        public async Task ProviderStatusList_AllProvidersHaveValidStatus()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.Providers.All(p =>
                Enum.IsDefined(typeof(ProviderIntegrationStatus), p.Status)), Is.True);
        }

        [Test]
        public async Task ProviderStatusList_AllProvidersHaveLastCheckedAt()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.Providers.All(p => p.LastCheckedAt > before), Is.True);
        }

        [Test]
        public async Task ProviderStatusList_CorrelationId_Propagated()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.CorrelationId, Is.EqualTo(CorrelationId));
        }

        [Test]
        public async Task ProviderStatusList_ReportedAt_IsRecent()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.ReportedAt, Is.GreaterThan(before));
        }
    }
}

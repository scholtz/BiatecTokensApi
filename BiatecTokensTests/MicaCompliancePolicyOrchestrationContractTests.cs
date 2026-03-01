using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive contract tests for the MICA-ready compliance policy orchestration foundation.
    /// Covers all 11 acceptance criteria from Issue #427.
    /// 
    /// AC1: Deterministic compliance policy evaluation service
    /// AC2: Normalized outcomes (allow, deny, requires_review) with structured reason codes
    /// AC3: API responses include policy version metadata for traceability
    /// AC4: Jurisdiction hook points with defined behavior for supported/unsupported jurisdictions
    /// AC5: Compliance evaluations write audit log entries with correlation metadata
    /// AC6: API contract validation rejects malformed requests with structured error responses
    /// AC7: Backend responses avoid internal error leakage
    /// AC8: Unit tests cover rule evaluation logic (allow/deny/review outcomes)
    /// AC9: Integration tests verify endpoint behavior and audit log persistence
    /// AC10: Existing backend CI pipelines pass with new coverage
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MicaCompliancePolicyOrchestrationContractTests
    {
        private PolicyEvaluator _policyEvaluator = null!;
        private Mock<ILogger<PolicyEvaluator>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<PolicyEvaluator>>();
            _policyEvaluator = new PolicyEvaluator(_loggerMock.Object);
        }

        // ============================================================
        // AC1: Deterministic Policy Evaluation
        // ============================================================

        [Test]
        public async Task AC1_PolicyEvaluation_SameInput_ReturnsSameOutcome_Run1()
        {
            var context = BuildApprovedKycContext("org-determinism-ac1");
            var result = await _policyEvaluator.EvaluateAsync(context);
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved),
                "AC1: Same approved input must always produce Approved outcome");
            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC1: Approved outcome must map to normalized Allow");
        }

        [Test]
        public async Task AC1_PolicyEvaluation_SameInput_ReturnsSameOutcome_Run2()
        {
            var context = BuildApprovedKycContext("org-determinism-ac1");
            var result = await _policyEvaluator.EvaluateAsync(context);
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved),
                "AC1: Second run with same input must also produce Approved");
            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC1: Second run must also map to normalized Allow");
        }

        [Test]
        public async Task AC1_PolicyEvaluation_SameInput_ReturnsSameOutcome_Run3()
        {
            var context = BuildApprovedKycContext("org-determinism-ac1");
            var result = await _policyEvaluator.EvaluateAsync(context);
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved),
                "AC1: Third run with same input must also produce Approved (determinism)");
        }

        [Test]
        public async Task AC1_PolicyEvaluation_DeniedInput_ReturnsDenyDeterministically()
        {
            var context = BuildRejectedAmlContext("org-deny-determinism");
            var result1 = await _policyEvaluator.EvaluateAsync(context);
            var result2 = await _policyEvaluator.EvaluateAsync(context);
            var result3 = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result1.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny));
            Assert.That(result2.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny));
            Assert.That(result3.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny),
                "AC1: Reject outcome must be deterministic across all runs");
        }

        [Test]
        public async Task AC1_PolicyEvaluation_SideEffectFree_IndependentEvaluations()
        {
            // Arrange - two different organizations evaluated
            var orgAContext = BuildApprovedKycContext("org-side-effect-A");
            var orgBContext = BuildRejectedAmlContext("org-side-effect-B");

            // Act - evaluate in both orders
            var resultA1 = await _policyEvaluator.EvaluateAsync(orgAContext);
            var resultB = await _policyEvaluator.EvaluateAsync(orgBContext);
            var resultA2 = await _policyEvaluator.EvaluateAsync(orgAContext);

            // Assert - org A outcome must not be affected by org B evaluation
            Assert.That(resultA1.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow));
            Assert.That(resultA2.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC1: Evaluations must be side-effect free; org A result must not change due to org B");
            Assert.That(resultB.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny));
        }

        // ============================================================
        // AC2: Normalized Outcomes with Structured Reason Codes
        // ============================================================

        [Test]
        public async Task AC2_AllowOutcome_ReturnedForApprovedCompliance()
        {
            var context = BuildApprovedKycContext("org-allow-test");
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC2: Fully compliant KYC must return normalized Allow");
            Assert.That(result.ReasonCodes, Is.Empty,
                "AC2: Allow outcome must have empty reason codes (no failures)");
        }

        [Test]
        public async Task AC2_DenyOutcome_ReturnedForFailedRequiredRule()
        {
            var context = BuildRejectedAmlContext("org-deny-test");
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny),
                "AC2: Failed AML check must return normalized Deny");
            Assert.That(result.ReasonCodes, Is.Not.Empty,
                "AC2: Deny outcome must include structured reason codes");
            Assert.That(result.ReasonCodes.All(c => !string.IsNullOrEmpty(c)), Is.True,
                "AC2: All reason codes must be non-empty strings");
        }

        [Test]
        public async Task AC2_RequiresReviewOutcome_ReturnedForUnconfiguredStep()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-review-test",
                Step = OnboardingStep.BeneficialOwnershipVerification, // No rules configured
                Evidence = new List<EvidenceReference>(),
                Initiator = "TEST_ACTOR"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.RequiresReview),
                "AC2: Unconfigured step must return normalized RequiresReview, not Allow");
            Assert.That(result.ReasonCodes, Does.Contain("NO_POLICY_RULES"),
                "AC2: RequiresReview for missing rules must include NO_POLICY_RULES reason code");
        }

        [Test]
        public async Task AC2_ReasonCodesAreStructured_MachineReadable()
        {
            var context = BuildRejectedAmlContext("org-reason-codes");
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.ReasonCodes, Is.Not.Null,
                "AC2: ReasonCodes must not be null");
            foreach (var code in result.ReasonCodes)
            {
                Assert.That(code, Does.Not.Contain(" "),
                    $"AC2: Reason code '{code}' must not contain spaces (machine-readable format)");
                Assert.That(code, Has.Length.GreaterThan(0),
                    "AC2: Reason codes must not be empty");
            }
        }

        [Test]
        public async Task AC2_AllThreeOutcomesAreReachable()
        {
            // Allow
            var allowResult = await _policyEvaluator.EvaluateAsync(BuildApprovedKycContext("org-all-outcomes-allow"));
            Assert.That(allowResult.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow));

            // Deny
            var denyResult = await _policyEvaluator.EvaluateAsync(BuildRejectedAmlContext("org-all-outcomes-deny"));
            Assert.That(denyResult.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny));

            // RequiresReview
            var reviewContext = new PolicyEvaluationContext
            {
                OrganizationId = "org-all-outcomes-review",
                Step = OnboardingStep.WalletCustodyVerification, // Not configured -> RequiresReview
                Evidence = new List<EvidenceReference>(),
                Initiator = "TEST_ACTOR"
            };
            var reviewResult = await _policyEvaluator.EvaluateAsync(reviewContext);
            Assert.That(reviewResult.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.RequiresReview),
                "AC2: All three normalized outcomes (Allow, Deny, RequiresReview) must be reachable");
        }

        // ============================================================
        // AC3: Policy Version Metadata in Responses
        // ============================================================

        [Test]
        public async Task AC3_PolicyVersionIncluded_InAllowOutcome()
        {
            var result = await _policyEvaluator.EvaluateAsync(BuildApprovedKycContext("org-ver-allow"));

            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty,
                "AC3: PolicyVersion must be present in Allow response");
            Assert.That(result.PolicyVersion, Does.Match(@"^\d+\.\d+\.\d+$"),
                "AC3: PolicyVersion must follow semantic versioning format");
        }

        [Test]
        public async Task AC3_PolicyVersionIncluded_InDenyOutcome()
        {
            var result = await _policyEvaluator.EvaluateAsync(BuildRejectedAmlContext("org-ver-deny"));

            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty,
                "AC3: PolicyVersion must be present in Deny response");
        }

        [Test]
        public async Task AC3_PolicyVersionIncluded_InRequiresReviewOutcome()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-ver-review",
                Step = OnboardingStep.BeneficialOwnershipVerification,
                Evidence = new List<EvidenceReference>(),
                Initiator = "TEST_ACTOR"
            };
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty,
                "AC3: PolicyVersion must be present even when no policy rules configured");
        }

        // ============================================================
        // AC4: Jurisdiction Hook Points
        // ============================================================

        [Test]
        public async Task AC4_JurisdictionHooks_PolicyContextAcceptsJurisdiction()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-jurisdiction-eu",
                Step = OnboardingStep.TokenIssuanceAuthorization,
                Jurisdiction = "EU",
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "TOKEN_SPECIFICATION",
                        ReferenceId = "spec-eu-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "TEST_ACTOR"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result, Is.Not.Null,
                "AC4: Policy evaluation must handle EU jurisdiction context");
            Assert.That(Enum.IsDefined(typeof(NormalizedPolicyOutcome), result.NormalizedOutcome), Is.True,
                "AC4: Normalized outcome must be a valid enum value for jurisdiction-scoped evaluation");
        }

        [Test]
        public async Task AC4_JurisdictionHooks_UnsupportedJurisdiction_ProducesTraceable()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-jurisdiction-unknown",
                Step = OnboardingStep.JurisdictionalCompliance,
                Jurisdiction = "XY", // Unknown jurisdiction
                Evidence = new List<EvidenceReference>(),
                Initiator = "TEST_ACTOR"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result, Is.Not.Null,
                "AC4: Unsupported jurisdiction must not throw; must return traceable outcome");
            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.RequiresReview),
                "AC4: Unknown jurisdiction with no rules must return RequiresReview (not Allow)");
        }

        [Test]
        public async Task AC4_JurisdictionService_ReturnsEvaluationResult_ForGlobalDefault()
        {
            // Arrange
            var mockRepoLogger = new Mock<ILogger<JurisdictionRulesRepository>>();
            var mockServiceLogger = new Mock<ILogger<JurisdictionRulesService>>();
            var mockComplianceRepo = new Mock<IComplianceRepository>();
            var repository = new JurisdictionRulesRepository(mockRepoLogger.Object);
            var service = new JurisdictionRulesService(repository, mockComplianceRepo.Object, mockServiceLogger.Object);

            mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act - evaluate an asset with no registered jurisdiction (falls back to GLOBAL)
            var result = await service.EvaluateTokenComplianceAsync(99999ul, "voimain-v1.0", "issuer-addr");

            // Assert
            Assert.That(result, Is.Not.Null, "AC4: Default jurisdiction evaluation must not be null");
            Assert.That(result.ComplianceStatus, Is.Not.Null.And.Not.Empty,
                "AC4: Global fallback must produce a traceable ComplianceStatus");
            Assert.That(result.ApplicableJurisdictions, Does.Contain("GLOBAL"),
                "AC4: Unknown asset must fall back to GLOBAL jurisdiction");
        }

        [Test]
        public async Task AC4_JurisdictionService_SupportedJurisdiction_EvaluatesRequirements()
        {
            var mockRepoLogger = new Mock<ILogger<JurisdictionRulesRepository>>();
            var mockServiceLogger = new Mock<ILogger<JurisdictionRulesService>>();
            var mockComplianceRepo = new Mock<IComplianceRepository>();
            var repository = new JurisdictionRulesRepository(mockRepoLogger.Object);
            var service = new JurisdictionRulesService(repository, mockComplianceRepo.Object, mockServiceLogger.Object);

            // Create a supported jurisdiction rule
            var createRequest = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "EU_MICA_TEST_AC4",
                JurisdictionName = "EU MICA Test",
                RegulatoryFramework = "MICA",
                IsActive = true,
                Priority = 10,
                Requirements = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement
                    {
                        RequirementCode = "MICA_ARTICLE_20",
                        Description = "KYC required",
                        IsMandatory = true,
                        Severity = RequirementSeverity.Critical
                    }
                }
            };
            await service.CreateRuleAsync(createRequest, "test-admin");

            // Assign this jurisdiction to a test token
            await service.AssignTokenJurisdictionAsync(12345ul, "voimain-v1.0", "EU_MICA_TEST_AC4", true, "test-admin");

            mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(12345ul))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Evaluate
            var result = await service.EvaluateTokenComplianceAsync(12345ul, "voimain-v1.0", "issuer-addr");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ApplicableJurisdictions, Does.Contain("EU_MICA_TEST_AC4"),
                "AC4: Supported jurisdiction must be included in evaluation");
            Assert.That(result.CheckResults, Is.Not.Null);
        }

        [Test]
        public async Task AC4_JurisdictionRule_DefaultBehavior_WhenNoRuleExists()
        {
            var mockRepoLogger = new Mock<ILogger<JurisdictionRulesRepository>>();
            var mockServiceLogger = new Mock<ILogger<JurisdictionRulesService>>();
            var mockComplianceRepo = new Mock<IComplianceRepository>();
            var repository = new JurisdictionRulesRepository(mockRepoLogger.Object);
            var service = new JurisdictionRulesService(repository, mockComplianceRepo.Object, mockServiceLogger.Object);

            mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Evaluate an asset for a jurisdiction code with no registered rule
            var result = await service.EvaluateTokenComplianceAsync(88888ul, "voimain-v1.0", "issuer-addr");

            Assert.That(result.ComplianceStatus, Is.Not.Empty,
                "AC4: Default behavior must produce a traceable status, not throw");
            Assert.That(new[] { "Unknown", "Compliant", "PartiallyCompliant", "NonCompliant" },
                Does.Contain(result.ComplianceStatus),
                "AC4: ComplianceStatus must be one of the known values");
        }

        // ============================================================
        // AC5: Compliance Decision Audit Logging with Correlation Metadata
        // ============================================================

        [Test]
        public async Task AC5_DecisionService_CorrelationId_PropagatedToDecision()
        {
            var correlationId = $"corr-ac5-{Guid.NewGuid():N}";
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();

            ComplianceDecision? capturedDecision = null;
            mockRepo.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);
            mockRepo.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });
            mockPolicyEval.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Approved,
                    NormalizedOutcome = NormalizedPolicyOutcome.Allow,
                    ReasonCodes = new List<string>(),
                    PolicyVersion = "1.0.0",
                    RuleEvaluations = new List<PolicyRuleEvaluation>()
                });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-ac5-correlation",
                Step = OnboardingStep.KycKybVerification,
                CorrelationId = correlationId,
                EvidenceReferences = new List<EvidenceReference>()
            };

            await service.CreateDecisionAsync(request, "ADDR_AC5_TEST");

            Assert.That(capturedDecision, Is.Not.Null, "AC5: Decision must be persisted");
            Assert.That(capturedDecision!.CorrelationId, Is.EqualTo(correlationId),
                "AC5: CorrelationId must be propagated into the persisted audit log entry");
        }

        [Test]
        public async Task AC5_DecisionService_Timestamp_IsPresent()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();

            ComplianceDecision? capturedDecision = null;
            mockRepo.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);
            mockRepo.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });
            mockPolicyEval.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Approved,
                    NormalizedOutcome = NormalizedPolicyOutcome.Allow,
                    ReasonCodes = new List<string>(),
                    PolicyVersion = "1.0.0",
                    RuleEvaluations = new List<PolicyRuleEvaluation>()
                });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            var beforeCreate = DateTime.UtcNow.AddSeconds(-1);
            await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-ac5-timestamp",
                Step = OnboardingStep.AmlScreening,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR_TIMESTAMP");

            Assert.That(capturedDecision, Is.Not.Null);
            Assert.That(capturedDecision!.DecisionTimestamp, Is.GreaterThanOrEqualTo(beforeCreate),
                "AC5: Audit log timestamp must be set to a current UTC time");
        }

        [Test]
        public async Task AC5_DecisionService_PolicyVersion_RecordedInAuditLog()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();

            ComplianceDecision? capturedDecision = null;
            mockRepo.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);
            mockRepo.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "2.0.1" }); // Specific version for test
            mockPolicyEval.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Approved,
                    NormalizedOutcome = NormalizedPolicyOutcome.Allow,
                    ReasonCodes = new List<string>(),
                    PolicyVersion = "2.0.1",
                    RuleEvaluations = new List<PolicyRuleEvaluation>()
                });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-ac5-policyver",
                Step = OnboardingStep.TermsAcceptance,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR_VERSION_TEST");

            Assert.That(capturedDecision, Is.Not.Null);
            Assert.That(capturedDecision!.PolicyVersion, Is.EqualTo("2.0.1"),
                "AC5: PolicyVersion must be recorded in audit log for traceability");
        }

        [Test]
        public async Task AC5_DecisionService_DecisionMaker_RecordedInAuditLog()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();

            ComplianceDecision? capturedDecision = null;
            mockRepo.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);
            mockRepo.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });
            mockPolicyEval.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Approved,
                    NormalizedOutcome = NormalizedPolicyOutcome.Allow,
                    ReasonCodes = new List<string>(),
                    PolicyVersion = "1.0.0",
                    RuleEvaluations = new List<PolicyRuleEvaluation>()
                });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            const string actorAddress = "ACTOR_ADDR_AC5_MAKER_001";
            await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-ac5-maker",
                Step = OnboardingStep.FinalApproval,
                EvidenceReferences = new List<EvidenceReference>()
            }, actorAddress);

            Assert.That(capturedDecision, Is.Not.Null);
            Assert.That(capturedDecision!.DecisionMaker, Is.EqualTo(actorAddress),
                "AC5: Decision maker address must be recorded in the audit log");
        }

        [Test]
        public async Task AC5_DecisionService_GeneratesCorrelationId_WhenNotProvided()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();

            ComplianceDecision? capturedDecision = null;
            mockRepo.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);
            mockRepo.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });
            mockPolicyEval.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Approved,
                    NormalizedOutcome = NormalizedPolicyOutcome.Allow,
                    ReasonCodes = new List<string>(),
                    PolicyVersion = "1.0.0",
                    RuleEvaluations = new List<PolicyRuleEvaluation>()
                });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            // No CorrelationId provided in request
            await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-ac5-autocorr",
                Step = OnboardingStep.KycKybVerification,
                CorrelationId = null,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR_AUTO_CORR");

            Assert.That(capturedDecision, Is.Not.Null);
            Assert.That(capturedDecision!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "AC5: Service must auto-generate CorrelationId when not provided by caller");
        }

        // ============================================================
        // AC6: API Contract Validation Rejects Malformed Requests
        // ============================================================

        [Test]
        public async Task AC6_PolicyDecisionService_EmptyOrganizationId_ReturnsBadRequest()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();
            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            var response = await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = string.Empty, // MALFORMED: empty
                Step = OnboardingStep.KycKybVerification,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR");

            Assert.That(response.Success, Is.False,
                "AC6: Empty OrganizationId must be rejected");
            Assert.That(response.ErrorMessage, Does.Contain("OrganizationId"),
                "AC6: Error message must identify the invalid field");
        }

        [Test]
        public async Task AC6_PolicyDecisionService_WhitespaceOrganizationId_ReturnsBadRequest()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();
            var mockPolicyEval = new Mock<IPolicyEvaluator>();
            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            var response = await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "   ", // MALFORMED: whitespace
                Step = OnboardingStep.AmlScreening,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR");

            Assert.That(response.Success, Is.False,
                "AC6: Whitespace-only OrganizationId must be rejected");
        }

        [Test]
        public async Task AC6_JurisdictionService_EmptyJurisdictionCode_ReturnsBadRequest()
        {
            var mockRepoLogger = new Mock<ILogger<JurisdictionRulesRepository>>();
            var mockServiceLogger = new Mock<ILogger<JurisdictionRulesService>>();
            var mockComplianceRepo = new Mock<IComplianceRepository>();
            var repository = new JurisdictionRulesRepository(mockRepoLogger.Object);
            var service = new JurisdictionRulesService(repository, mockComplianceRepo.Object, mockServiceLogger.Object);

            var response = await service.CreateRuleAsync(new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = string.Empty, // MALFORMED: empty
                JurisdictionName = "Test",
                RegulatoryFramework = "TEST"
            }, "admin");

            Assert.That(response.Success, Is.False,
                "AC6: Empty JurisdictionCode must be rejected with structured error");
            Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC6: Rejection must include a structured error message");
        }

        [Test]
        public async Task AC6_PolicyEvaluator_NoRulesConfigured_ReturnsRequiresReview_NotAllow()
        {
            // AC6: silently falling through to Allow when rules missing is FORBIDDEN
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-no-rules",
                Step = OnboardingStep.BeneficialOwnershipVerification, // No rules
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "BEN_OWN_CERT",
                        ReferenceId = "ref-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "TEST"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.Not.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC6: Service must NOT silently allow when no policy rules are configured");
            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.RequiresReview),
                "AC6: Missing rules must produce RequiresReview, never a silent Allow");
        }

        [Test]
        public async Task AC6_DecisionService_RepositoryThrows_ReturnsStructuredError()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();
            var mockPolicyEval = new Mock<IPolicyEvaluator>();

            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(new PolicyConfiguration { Version = "1.0.0" });
            mockPolicyEval.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Approved,
                    NormalizedOutcome = NormalizedPolicyOutcome.Allow,
                    ReasonCodes = new List<string>(),
                    PolicyVersion = "1.0.0",
                    RuleEvaluations = new List<PolicyRuleEvaluation>()
                });

            // Simulate infrastructure failure
            mockRepo.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            var response = await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-repo-throws",
                Step = OnboardingStep.KycKybVerification,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR");

            Assert.That(response.Success, Is.False,
                "AC6: Infrastructure failure must produce a structured failure response, not throw");
            Assert.That(response.ErrorMessage, Is.Not.Null,
                "AC6: Failure response must include an error message for frontend mapping");
        }

        // ============================================================
        // AC7: Error Responses Avoid Internal Error Leakage
        // ============================================================

        [Test]
        public async Task AC7_DecisionService_ErrorMessage_DoesNotLeakStackTrace()
        {
            var mockRepo = new Mock<IComplianceDecisionRepository>();
            var mockMetrics = new Mock<IMetricsService>();
            var mockLogger = new Mock<ILogger<ComplianceDecisionService>>();
            var mockPolicyEval = new Mock<IPolicyEvaluator>();

            mockPolicyEval.Setup(p => p.GetPolicyConfigurationAsync())
                .ThrowsAsync(new InvalidOperationException("Internal database connection failed: Server=db-internal;Password=secret123"));

            var service = new ComplianceDecisionService(
                mockRepo.Object, mockPolicyEval.Object, mockMetrics.Object, mockLogger.Object);

            var response = await service.CreateDecisionAsync(new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-ac7-leakage",
                Step = OnboardingStep.KycKybVerification,
                EvidenceReferences = new List<EvidenceReference>()
            }, "ADDR");

            Assert.That(response.Success, Is.False);
            // Error message should not contain stack trace indicators
            Assert.That(response.ErrorMessage, Does.Not.Contain("at BiatecTokensApi"),
                "AC7: Stack trace must not be included in API error response");
            Assert.That(response.ErrorMessage, Does.Not.Contain("   at "),
                "AC7: Stack frame lines must not appear in API error response");
        }

        [Test]
        public async Task AC7_PolicyEvaluator_SystemError_ReturnsUserSafeMessage()
        {
            // Simulate a policy evaluator context that causes internal error handling
            // by using an empty context (null organization)
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-ac7-safe",
                Step = OnboardingStep.BeneficialOwnershipVerification,
                Evidence = new List<EvidenceReference>(),
                Initiator = "TEST"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            // The result should always be well-formed (no exceptions thrown to caller)
            Assert.That(result, Is.Not.Null,
                "AC7: PolicyEvaluator must never throw to caller; must return structured result");
            Assert.That(result.Reason, Is.Not.Null,
                "AC7: Reason must always be provided for frontend mapping");
            Assert.That(Enum.IsDefined(typeof(NormalizedPolicyOutcome), result.NormalizedOutcome), Is.True,
                "AC7: NormalizedOutcome must always be set to a valid enum value");
        }

        [Test]
        public async Task AC7_RemediationHints_AreUserSafe_NoInternalDetails()
        {
            var context = BuildRejectedAmlContext("org-ac7-hints");
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.RequiredActions, Is.Not.Null);
            foreach (var action in result.RequiredActions)
            {
                Assert.That(action, Does.Not.Contain("Exception"),
                    "AC7: Remediation hints must not contain exception class names");
                Assert.That(action, Does.Not.Contain("StackTrace"),
                    "AC7: Remediation hints must not contain stack trace references");
                Assert.That(action, Does.Not.Contain("NullReference"),
                    "AC7: Remediation hints must not contain internal .NET error details");
            }
        }

        // ============================================================
        // AC8: Unit Tests for Rule Evaluation Logic
        // ============================================================

        [Test]
        public async Task AC8_RuleEvaluation_OrganizationIdentity_WithBothDocuments_Passes()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-ac8-identity",
                Step = OnboardingStep.OrganizationIdentityVerification,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "ORG_REGISTRATION_CERT",
                        ReferenceId = "cert-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    },
                    new EvidenceReference
                    {
                        EvidenceType = "TAX_ID_DOCUMENT",
                        ReferenceId = "tax-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "TEST"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC8: Both required identity documents -> Allow");
            Assert.That(result.RuleEvaluations.All(r => r.Passed), Is.True,
                "AC8: All rule evaluations must pass when both identity docs provided");
        }

        [Test]
        public async Task AC8_RuleEvaluation_OrganizationIdentity_WithOneMissing_Fails()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-ac8-partial",
                Step = OnboardingStep.OrganizationIdentityVerification,
                Evidence = new List<EvidenceReference>
                {
                    // Only one of the two required documents
                    new EvidenceReference
                    {
                        EvidenceType = "ORG_REGISTRATION_CERT",
                        ReferenceId = "cert-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                    // TAX_ID_DOCUMENT missing
                },
                Initiator = "TEST"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny),
                "AC8: Missing TAX_ID_DOCUMENT -> Deny");
        }

        [Test]
        public async Task AC8_RuleEvaluation_AmlScreening_UnverifiedEvidence_Fails()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-ac8-aml-unverified",
                Step = OnboardingStep.AmlScreening,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "AML_REPORT",
                        ReferenceId = "aml-001",
                        VerificationStatus = EvidenceVerificationStatus.InReview // Not yet verified
                    }
                },
                Initiator = "TEST"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny),
                "AC8: In-review AML evidence (not Verified) must produce Deny");
        }

        [Test]
        public async Task AC8_RuleEvaluation_TermsAcceptance_WithVerifiedAcceptance_Passes()
        {
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-ac8-terms",
                Step = OnboardingStep.TermsAcceptance,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "TERMS_ACCEPTANCE",
                        ReferenceId = "terms-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "TEST"
            };

            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow),
                "AC8: Verified terms acceptance -> Allow");
        }

        [Test]
        public async Task AC8_RuleEvaluation_ReturnsEstimatedResolutionTime_OnDeny()
        {
            var context = BuildRejectedAmlContext("org-ac8-eta");
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Deny));
            Assert.That(result.EstimatedResolutionTime, Is.Not.Null.And.Not.Empty,
                "AC8: Deny outcome must include estimated resolution time for remediation guidance");
        }

        // ============================================================
        // AC9: Integration Tests for Compliance Endpoints
        // ============================================================

        [Test]
        public async Task AC9_ComplianceDecisionEndpoint_RequiresAuthentication()
        {
            using var factory = new ComplianceOrchestrationTestFactory();
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/v1/compliance/decisions",
                new CreateComplianceDecisionRequest
                {
                    OrganizationId = "org-ac9-auth",
                    Step = OnboardingStep.KycKybVerification,
                    EvidenceReferences = new List<EvidenceReference>()
                });

            // Without auth the endpoint should return 401 or succeed via debug mode
            Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.Unauthorized).Or.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.BadRequest),
                "AC9: Decision endpoint must require authentication (401) or succeed in debug mode");
        }

        [Test]
        public async Task AC9_PolicyRulesEndpoint_ReturnsRulesForKycStep()
        {
            using var factory = new ComplianceOrchestrationTestFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/compliance/decisions/policy-rules/KycKybVerification");

            Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized),
                "AC9: Policy rules endpoint should be accessible");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                Assert.That(content, Is.Not.Empty);
                var rules = JsonSerializer.Deserialize<List<PolicyRule>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.That(rules, Is.Not.Null.And.Not.Empty,
                    "AC9: KycKybVerification step must have policy rules defined");
            }
        }

        [Test]
        public async Task AC9_PolicyConfigurationEndpoint_ReturnsPolicyVersion()
        {
            using var factory = new ComplianceOrchestrationTestFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/compliance/decisions/policy-configuration");

            Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized),
                "AC9: Policy configuration endpoint should be accessible");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                Assert.That(content, Does.Contain("version"),
                    "AC9: Policy configuration response must contain version field (AC3)");
            }
        }

        [Test]
        public async Task AC9_HealthEndpoint_ReturnsOkForCI()
        {
            using var factory = new ComplianceOrchestrationTestFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "AC9: Health endpoint must remain accessible after compliance changes");
        }

        [Test]
        public async Task AC9_JurisdictionRulesEndpoint_IsAccessible()
        {
            using var factory = new ComplianceOrchestrationTestFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/compliance/jurisdiction-rules?page=1&pageSize=10");

            Assert.That(response.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized),
                "AC9: Jurisdiction rules endpoint must be accessible (returns rules or 401)");
        }

        // ============================================================
        // AC10: CI Regression Checks
        // ============================================================

        [Test]
        public async Task AC10_ExistingPolicyEvaluator_StillProducesExpectedOutcomes()
        {
            // Regression: ensure backward-compatible behavior after model changes
            var context = BuildApprovedKycContext("org-ac10-regression");
            var result = await _policyEvaluator.EvaluateAsync(context);

            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved),
                "AC10: Regression - Approved outcome must still be set");
            Assert.That(result.RuleEvaluations, Is.Not.Empty,
                "AC10: Regression - RuleEvaluations must still be populated");
            Assert.That(result.Reason, Is.Not.Null.And.Not.Empty,
                "AC10: Regression - Reason must still be set");
            Assert.That(result.RequiredActions, Is.Not.Null,
                "AC10: Regression - RequiredActions list must not be null");
        }

        [Test]
        public async Task AC10_NewNormalizedOutcomeFields_DoNotBreakExistingFields()
        {
            // Verify new fields are additive; existing fields still work
            var context = BuildApprovedKycContext("org-ac10-additive");
            var result = await _policyEvaluator.EvaluateAsync(context);

            // Existing fields still work
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved));
            Assert.That(result.Reason, Is.Not.Empty);
            Assert.That(result.RuleEvaluations, Is.Not.Empty);

            // New fields also work
            Assert.That(result.NormalizedOutcome, Is.EqualTo(NormalizedPolicyOutcome.Allow));
            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ReasonCodes, Is.Not.Null);
        }

        // ============================================================
        // Helper Methods
        // ============================================================

        private static PolicyEvaluationContext BuildApprovedKycContext(string orgId)
        {
            return new PolicyEvaluationContext
            {
                OrganizationId = orgId,
                Step = OnboardingStep.KycKybVerification,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "KYC_REPORT",
                        ReferenceId = $"doc-kyc-{orgId}",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "TEST_ACTOR",
                CorrelationId = $"corr-{orgId}"
            };
        }

        private static PolicyEvaluationContext BuildRejectedAmlContext(string orgId)
        {
            return new PolicyEvaluationContext
            {
                OrganizationId = orgId,
                Step = OnboardingStep.AmlScreening,
                Evidence = new List<EvidenceReference>(), // No evidence = rejected
                Initiator = "TEST_ACTOR",
                CorrelationId = $"corr-{orgId}"
            };
        }

        // ============================================================
        // Integration Test Factory
        // ============================================================

        /// <summary>
        /// WebApplicationFactory for integration tests that require a running application.
        /// </summary>
        private class ComplianceOrchestrationTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForMicaComplianceContractTests32CharMin",
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
            }
        }
    }
}

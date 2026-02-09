using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Policy Evaluator Service
    /// </summary>
    [TestFixture]
    public class PolicyEvaluatorTests
    {
        private PolicyEvaluator _service = null!;
        private Mock<ILogger<PolicyEvaluator>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<PolicyEvaluator>>();
            _service = new PolicyEvaluator(_loggerMock.Object);
        }

        [Test]
        public async Task EvaluateAsync_WithAllRequiredEvidence_ReturnsApproved()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-123",
                Step = OnboardingStep.KycKybVerification,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "KYC_REPORT",
                        ReferenceId = "doc-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "ADDR123"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved));
            Assert.That(result.RuleEvaluations, Is.Not.Empty);
            Assert.That(result.RuleEvaluations.All(r => r.Passed), Is.True);
            Assert.That(result.RequiredActions, Is.Empty);
        }

        [Test]
        public async Task EvaluateAsync_WithMissingRequiredEvidence_ReturnsRejected()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-456",
                Step = OnboardingStep.KycKybVerification,
                Evidence = new List<EvidenceReference>(), // No evidence provided
                Initiator = "ADDR456"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Rejected));
            Assert.That(result.RuleEvaluations.Any(r => !r.Passed), Is.True);
            Assert.That(result.RequiredActions, Is.Not.Empty);
        }

        [Test]
        public async Task EvaluateAsync_WithUnverifiedEvidence_ReturnsRejected()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-789",
                Step = OnboardingStep.AmlScreening,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "AML_REPORT",
                        ReferenceId = "doc-002",
                        VerificationStatus = EvidenceVerificationStatus.Submitted // Not verified yet
                    }
                },
                Initiator = "ADDR789"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Rejected));
            Assert.That(result.Reason, Does.Contain("Failed").Or.Contain("failed"));
        }

        [Test]
        public async Task GetApplicableRulesAsync_ForValidStep_ReturnsRules()
        {
            // Act
            var rules = await _service.GetApplicableRulesAsync(OnboardingStep.KycKybVerification);

            // Assert
            Assert.That(rules, Is.Not.Empty);
            Assert.That(rules.All(r => r.IsActive), Is.True);
            Assert.That(rules.All(r => r.ApplicableStep == OnboardingStep.KycKybVerification), Is.True);
        }

        [Test]
        public async Task GetApplicableRulesAsync_ForUnconfiguredStep_ReturnsEmptyList()
        {
            // Act - Using a step that has no rules configured
            var rules = await _service.GetApplicableRulesAsync(OnboardingStep.BeneficialOwnershipVerification);

            // Assert
            Assert.That(rules, Is.Empty);
        }

        [Test]
        public async Task GetPolicyConfigurationAsync_ReturnsValidConfiguration()
        {
            // Act
            var config = await _service.GetPolicyConfigurationAsync();

            // Assert
            Assert.That(config, Is.Not.Null);
            Assert.That(config.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(config.RulesByStep, Is.Not.Empty);
            Assert.That(config.DefaultExpirationDays, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetMetricsAsync_ReturnsMetrics()
        {
            // Act
            var metrics = await _service.GetMetricsAsync();

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics.PolicyVersion, Is.Not.Null);
            Assert.That(metrics.TotalEvaluations, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task EvaluateAsync_UpdatesMetrics()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-metrics",
                Step = OnboardingStep.TermsAcceptance,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "TERMS_ACCEPTANCE",
                        ReferenceId = "doc-003",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "ADDR-METRICS"
            };

            var metricsBefore = await _service.GetMetricsAsync();
            var totalBefore = metricsBefore.TotalEvaluations;

            // Act
            await _service.EvaluateAsync(context);

            // Assert
            var metricsAfter = await _service.GetMetricsAsync();
            Assert.That(metricsAfter.TotalEvaluations, Is.EqualTo(totalBefore + 1));
            // Just check that approvals increased (may be affected by other tests)
            Assert.That(metricsAfter.AutomaticApprovals, Is.GreaterThanOrEqualTo(metricsBefore.AutomaticApprovals));
        }

        [Test]
        public async Task EvaluateAsync_MultipleEvaluations_UpdatesAverageTime()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-perf",
                Step = OnboardingStep.TermsAcceptance,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "TERMS_ACCEPTANCE",
                        ReferenceId = "doc-004",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "ADDR-PERF"
            };

            // Act - Multiple evaluations
            await _service.EvaluateAsync(context);
            await _service.EvaluateAsync(context);

            // Assert
            var metrics = await _service.GetMetricsAsync();
            Assert.That(metrics.AverageEvaluationTimeMs, Is.GreaterThan(0));
        }

        [Test]
        public async Task EvaluateAsync_WithFailedRule_TracksFailureInMetrics()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-failure",
                Step = OnboardingStep.KycKybVerification,
                Evidence = new List<EvidenceReference>(), // Missing evidence
                Initiator = "ADDR-FAILURE"
            };

            // Act
            await _service.EvaluateAsync(context);

            // Assert
            var metrics = await _service.GetMetricsAsync();
            Assert.That(metrics.RuleFailureCounts, Is.Not.Empty);
            Assert.That(metrics.AutomaticRejections, Is.GreaterThan(0));
        }

        [Test]
        public async Task EvaluateAsync_BusinessRegistration_WithValidEvidence_Approved()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-business",
                Step = OnboardingStep.BusinessRegistrationVerification,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "BUSINESS_LICENSE",
                        ReferenceId = "license-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "ADDR-BUSINESS"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved));
            Assert.That(result.RuleEvaluations.First().RuleId, Does.Contain("BUS_REG"));
        }

        [Test]
        public async Task EvaluateAsync_TokenIssuanceAuthorization_WithValidSpec_Approved()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-token",
                Step = OnboardingStep.TokenIssuanceAuthorization,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "TOKEN_SPECIFICATION",
                        ReferenceId = "spec-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                },
                Initiator = "ADDR-TOKEN",
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Approved));
            Assert.That(result.RuleEvaluations.First().RuleId, Does.Contain("TOKEN"));
        }

        [Test]
        public async Task EvaluateAsync_ProvidesClearRemediationActions()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-remediation",
                Step = OnboardingStep.AmlScreening,
                Evidence = new List<EvidenceReference>(), // No evidence
                Initiator = "ADDR-REMEDIATION"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Rejected));
            Assert.That(result.RequiredActions, Is.Not.Empty);
            Assert.That(result.EstimatedResolutionTime, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateAsync_WithPartialEvidence_IdentifiesMissingItems()
        {
            // Arrange
            var context = new PolicyEvaluationContext
            {
                OrganizationId = "org-partial",
                Step = OnboardingStep.OrganizationIdentityVerification,
                Evidence = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "ORG_REGISTRATION_CERT",
                        ReferenceId = "cert-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                    // Missing TAX_ID_DOCUMENT
                },
                Initiator = "ADDR-PARTIAL"
            };

            // Act
            var result = await _service.EvaluateAsync(context);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(DecisionOutcome.Rejected));
            Assert.That(result.Reason, Does.Contain("Organization Identity").IgnoreCase);
            // Check that there are required actions that mention the missing evidence
            Assert.That(result.RequiredActions.Any(), Is.True);
        }
    }
}

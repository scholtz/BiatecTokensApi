using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Compliance Decision Service
    /// </summary>
    [TestFixture]
    public class ComplianceDecisionServiceTests
    {
        private IComplianceDecisionService _service = null!;
        private Mock<IComplianceDecisionRepository> _repositoryMock = null!;
        private Mock<IPolicyEvaluator> _policyEvaluatorMock = null!;
        private Mock<IMetricsService> _metricsServiceMock = null!;
        private Mock<ILogger<ComplianceDecisionService>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceDecisionRepository>();
            _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
            _metricsServiceMock = new Mock<IMetricsService>();
            _loggerMock = new Mock<ILogger<ComplianceDecisionService>>();

            _service = new ComplianceDecisionService(
                _repositoryMock.Object,
                _policyEvaluatorMock.Object,
                _metricsServiceMock.Object,
                _loggerMock.Object);

            // Setup default policy configuration
            var policyConfig = new PolicyConfiguration
            {
                Version = "1.0.0",
                DefaultExpirationDays = 365
            };
            _policyEvaluatorMock.Setup(p => p.GetPolicyConfigurationAsync())
                .ReturnsAsync(policyConfig);
        }

        [Test]
        public async Task CreateDecisionAsync_WithValidRequest_CreatesDecision()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-123",
                Step = OnboardingStep.KycKybVerification,
                EvidenceReferences = new List<EvidenceReference>
                {
                    new EvidenceReference
                    {
                        EvidenceType = "KYC_REPORT",
                        ReferenceId = "doc-001",
                        VerificationStatus = EvidenceVerificationStatus.Verified
                    }
                }
            };

            var evaluationResult = new PolicyEvaluationResult
            {
                Outcome = DecisionOutcome.Approved,
                Reason = "All requirements met",
                RuleEvaluations = new List<PolicyRuleEvaluation>()
            };

            _policyEvaluatorMock.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            _repositoryMock.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            // Act
            var response = await _service.CreateDecisionAsync(request, "ADDR123");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Decision, Is.Not.Null);
            Assert.That(response.Decision.OrganizationId, Is.EqualTo("org-123"));
            Assert.That(response.Decision.Outcome, Is.EqualTo(DecisionOutcome.Approved));
            Assert.That(response.Decision.DecisionMaker, Is.EqualTo("ADDR123"));
            _repositoryMock.Verify(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()), Times.Once);
        }

        [Test]
        public async Task CreateDecisionAsync_WithDuplicate_ReturnsExistingDecision()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-456",
                Step = OnboardingStep.AmlScreening,
                EvidenceReferences = new List<EvidenceReference>
                {
                    new EvidenceReference { ReferenceId = "doc-002" }
                }
            };

            var existingDecision = new ComplianceDecision
            {
                Id = "existing-123",
                OrganizationId = "org-456",
                Step = OnboardingStep.AmlScreening
            };

            _repositoryMock.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync(existingDecision);

            // Act
            var response = await _service.CreateDecisionAsync(request, "ADDR456");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Decision, Is.EqualTo(existingDecision));
            Assert.That(response.ErrorMessage, Does.Contain("idempotent"));
            _repositoryMock.Verify(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()), Times.Never);
        }

        [Test]
        public async Task CreateDecisionAsync_WithMissingOrganizationId_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "", // Empty
                Step = OnboardingStep.KycKybVerification
            };

            // Act
            var response = await _service.CreateDecisionAsync(request, "ADDR789");

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("OrganizationId"));
        }

        [Test]
        public async Task CreateDecisionAsync_WithExpirationDays_SetsExpiration()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-expiry",
                Step = OnboardingStep.FinalApproval,
                ExpirationDays = 90,
                EvidenceReferences = new List<EvidenceReference>()
            };

            var evaluationResult = new PolicyEvaluationResult
            {
                Outcome = DecisionOutcome.Approved,
                RuleEvaluations = new List<PolicyRuleEvaluation>()
            };

            _policyEvaluatorMock.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            _repositoryMock.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            ComplianceDecision? capturedDecision = null;
            _repositoryMock.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);

            // Act
            var response = await _service.CreateDecisionAsync(request, "ADDR-EXPIRY");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(capturedDecision, Is.Not.Null);
            Assert.That(capturedDecision!.ExpiresAt, Is.Not.Null);
            Assert.That(capturedDecision.ExpiresAt!.Value, Is.GreaterThan(DateTime.UtcNow.AddDays(89)));
        }

        [Test]
        public async Task CreateDecisionAsync_WithReviewInterval_SetsNextReviewDate()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-review",
                Step = OnboardingStep.KycKybVerification,
                RequiresReview = true,
                ReviewIntervalDays = 180,
                EvidenceReferences = new List<EvidenceReference>()
            };

            var evaluationResult = new PolicyEvaluationResult
            {
                Outcome = DecisionOutcome.Approved,
                RuleEvaluations = new List<PolicyRuleEvaluation>()
            };

            _policyEvaluatorMock.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            _repositoryMock.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            ComplianceDecision? capturedDecision = null;
            _repositoryMock.Setup(r => r.CreateDecisionAsync(It.IsAny<ComplianceDecision>()))
                .Callback<ComplianceDecision>(d => capturedDecision = d)
                .Returns(Task.CompletedTask);

            // Act
            var response = await _service.CreateDecisionAsync(request, "ADDR-REVIEW");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(capturedDecision, Is.Not.Null);
            Assert.That(capturedDecision!.RequiresReview, Is.True);
            Assert.That(capturedDecision.NextReviewDate, Is.Not.Null);
            Assert.That(capturedDecision.NextReviewDate!.Value, Is.GreaterThan(DateTime.UtcNow.AddDays(179)));
        }

        [Test]
        public async Task CreateDecisionAsync_EmitsMetrics()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-metrics",
                Step = OnboardingStep.TermsAcceptance,
                EvidenceReferences = new List<EvidenceReference>()
            };

            var evaluationResult = new PolicyEvaluationResult
            {
                Outcome = DecisionOutcome.Approved,
                RuleEvaluations = new List<PolicyRuleEvaluation>()
            };

            _policyEvaluatorMock.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            _repositoryMock.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            // Act
            await _service.CreateDecisionAsync(request, "ADDR-METRICS");

            // Assert
            _metricsServiceMock.Verify(m => m.IncrementCounter(It.IsAny<string>(), It.IsAny<long>()), Times.AtLeastOnce);
            _metricsServiceMock.Verify(m => m.RecordHistogram(It.IsAny<string>(), It.IsAny<double>()), Times.Once);
        }

        [Test]
        public async Task GetDecisionByIdAsync_WithValidId_ReturnsDecision()
        {
            // Arrange
            var decision = new ComplianceDecision
            {
                Id = "dec-123",
                OrganizationId = "org-test"
            };

            _repositoryMock.Setup(r => r.GetDecisionByIdAsync("dec-123"))
                .ReturnsAsync(decision);

            // Act
            var result = await _service.GetDecisionByIdAsync("dec-123");

            // Assert
            Assert.That(result, Is.EqualTo(decision));
        }

        [Test]
        public async Task GetDecisionByIdAsync_WithInvalidId_ReturnsNull()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetDecisionByIdAsync("invalid"))
                .ReturnsAsync((ComplianceDecision?)null);

            // Act
            var result = await _service.GetDecisionByIdAsync("invalid");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task QueryDecisionsAsync_ReturnsMatchingDecisions()
        {
            // Arrange
            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-query",
                Step = OnboardingStep.KycKybVerification,
                Page = 1,
                PageSize = 10
            };

            var decisions = new List<ComplianceDecision>
            {
                new ComplianceDecision
                {
                    Id = "dec-1",
                    OrganizationId = "org-query",
                    Outcome = DecisionOutcome.Approved
                }
            };

            _repositoryMock.Setup(r => r.QueryDecisionsAsync(It.IsAny<QueryComplianceDecisionsRequest>()))
                .ReturnsAsync((decisions, 1));

            // Act
            var response = await _service.QueryDecisionsAsync(request);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Decisions, Has.Count.EqualTo(1));
            Assert.That(response.TotalCount, Is.EqualTo(1));
            Assert.That(response.Summary, Is.Not.Null);
            Assert.That(response.Summary.ApprovedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task QueryDecisionsAsync_CalculatesSummaryStatistics()
        {
            // Arrange
            var request = new QueryComplianceDecisionsRequest
            {
                OrganizationId = "org-stats"
            };

            var decisions = new List<ComplianceDecision>
            {
                new ComplianceDecision { Outcome = DecisionOutcome.Approved, DecisionTimestamp = DateTime.UtcNow.AddHours(-2) },
                new ComplianceDecision { Outcome = DecisionOutcome.Rejected, Reason = "Missing docs", DecisionTimestamp = DateTime.UtcNow.AddHours(-1) },
                new ComplianceDecision { Outcome = DecisionOutcome.RequiresManualReview, DecisionTimestamp = DateTime.UtcNow }
            };

            _repositoryMock.Setup(r => r.QueryDecisionsAsync(It.IsAny<QueryComplianceDecisionsRequest>()))
                .ReturnsAsync((decisions, 3));

            // Act
            var response = await _service.QueryDecisionsAsync(request);

            // Assert
            Assert.That(response.Summary, Is.Not.Null);
            Assert.That(response.Summary.ApprovedCount, Is.EqualTo(1));
            Assert.That(response.Summary.RejectedCount, Is.EqualTo(1));
            Assert.That(response.Summary.RequiresReviewCount, Is.EqualTo(1));
            Assert.That(response.Summary.CommonRejectionReasons, Is.Not.Empty);
        }

        [Test]
        public async Task UpdateDecisionAsync_WithValidPreviousId_CreatesNewDecision()
        {
            // Arrange
            var previousDecision = new ComplianceDecision
            {
                Id = "dec-old",
                OrganizationId = "org-update",
                Step = OnboardingStep.KycKybVerification
            };

            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-update",
                Step = OnboardingStep.KycKybVerification,
                EvidenceReferences = new List<EvidenceReference>()
            };

            var evaluationResult = new PolicyEvaluationResult
            {
                Outcome = DecisionOutcome.Approved,
                RuleEvaluations = new List<PolicyRuleEvaluation>()
            };

            _repositoryMock.Setup(r => r.GetDecisionByIdAsync("dec-old"))
                .ReturnsAsync(previousDecision);

            _policyEvaluatorMock.Setup(p => p.EvaluateAsync(It.IsAny<PolicyEvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            _repositoryMock.Setup(r => r.FindDuplicateDecisionAsync(
                    It.IsAny<string>(), It.IsAny<OnboardingStep>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync((ComplianceDecision?)null);

            // Act
            var response = await _service.UpdateDecisionAsync("dec-old", request, "ADDR-UPDATE");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Decision, Is.Not.Null);
            Assert.That(response.Decision.PreviousDecisionId, Is.EqualTo("dec-old"));
            _repositoryMock.Verify(r => r.SupersedeDecisionAsync("dec-old", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task UpdateDecisionAsync_WithInvalidPreviousId_ReturnsError()
        {
            // Arrange
            var request = new CreateComplianceDecisionRequest
            {
                OrganizationId = "org-fail",
                Step = OnboardingStep.KycKybVerification
            };

            _repositoryMock.Setup(r => r.GetDecisionByIdAsync("invalid"))
                .ReturnsAsync((ComplianceDecision?)null);

            // Act
            var response = await _service.UpdateDecisionAsync("invalid", request, "ADDR-FAIL");

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task GetActiveDecisionAsync_ReturnsLatestNonSuperseded()
        {
            // Arrange
            var activeDecision = new ComplianceDecision
            {
                Id = "dec-active",
                OrganizationId = "org-active",
                Step = OnboardingStep.KycKybVerification,
                IsSuperseded = false
            };

            _repositoryMock.Setup(r => r.GetActiveDecisionAsync("org-active", OnboardingStep.KycKybVerification))
                .ReturnsAsync(activeDecision);

            // Act
            var result = await _service.GetActiveDecisionAsync("org-active", OnboardingStep.KycKybVerification);

            // Assert
            Assert.That(result, Is.EqualTo(activeDecision));
            Assert.That(result.IsSuperseded, Is.False);
        }

        [Test]
        public async Task GetDecisionsRequiringReviewAsync_ReturnsOnlyReviewRequired()
        {
            // Arrange
            var decisions = new List<ComplianceDecision>
            {
                new ComplianceDecision
                {
                    RequiresReview = true,
                    NextReviewDate = DateTime.UtcNow.AddDays(-1)
                }
            };

            _repositoryMock.Setup(r => r.GetDecisionsRequiringReviewAsync(It.IsAny<DateTime?>()))
                .ReturnsAsync(decisions);

            // Act
            var result = await _service.GetDecisionsRequiringReviewAsync();

            // Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result.All(d => d.RequiresReview), Is.True);
        }

        [Test]
        public async Task GetExpiredDecisionsAsync_ReturnsOnlyExpired()
        {
            // Arrange
            var decisions = new List<ComplianceDecision>
            {
                new ComplianceDecision
                {
                    ExpiresAt = DateTime.UtcNow.AddDays(-1),
                    IsSuperseded = false
                }
            };

            _repositoryMock.Setup(r => r.GetExpiredDecisionsAsync())
                .ReturnsAsync(decisions);

            // Act
            var result = await _service.GetExpiredDecisionsAsync();

            // Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result.All(d => d.ExpiresAt < DateTime.UtcNow), Is.True);
        }
    }
}

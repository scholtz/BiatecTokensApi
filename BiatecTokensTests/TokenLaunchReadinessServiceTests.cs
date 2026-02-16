using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenLaunchReadinessServiceTests
    {
        private Mock<IEntitlementEvaluationService> _mockEntitlementService = null!;
        private Mock<IARC76AccountReadinessService> _mockArc76Service = null!;
        private Mock<IKycService> _mockKycService = null!;
        private Mock<ITokenLaunchReadinessRepository> _mockRepository = null!;
        private Mock<IMetricsService> _mockMetricsService = null!;
        private Mock<ILogger<TokenLaunchReadinessService>> _mockLogger = null!;
        private TokenLaunchReadinessService _service = null!;

        [SetUp]
        public void Setup()
        {
            _mockEntitlementService = new Mock<IEntitlementEvaluationService>();
            _mockArc76Service = new Mock<IARC76AccountReadinessService>();
            _mockKycService = new Mock<IKycService>();
            _mockRepository = new Mock<ITokenLaunchReadinessRepository>();
            _mockMetricsService = new Mock<IMetricsService>();
            _mockLogger = new Mock<ILogger<TokenLaunchReadinessService>>();

            _service = new TokenLaunchReadinessService(
                _mockEntitlementService.Object,
                _mockArc76Service.Object,
                _mockKycService.Object,
                _mockRepository.Object,
                _mockMetricsService.Object,
                _mockLogger.Object
            );
        }

        [Test]
        public async Task EvaluateReadinessAsync_AllChecksPassed_ReturnsReady()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3",
                Network = "mainnet"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult
                {
                    IsAllowed = true,
                    SubscriptionTier = "Premium",
                    PolicyVersion = "2026.02.15.1"
                });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult
                {
                    
                    State = ARC76ReadinessState.Ready,
                    AccountAddress = "ADDR123..."
                });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Approved
                });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Ready));
            Assert.That(result.CanProceed, Is.True);
            Assert.That(result.RemediationTasks, Is.Empty);
            Assert.That(result.Details.Entitlement.Passed, Is.True);
            Assert.That(result.Details.AccountReadiness.Passed, Is.True);
            Assert.That(result.Details.KycAml.Passed, Is.True);
        }

        [Test]
        public async Task EvaluateReadinessAsync_EntitlementFailed_ReturnsBlocked()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult
                {
                    IsAllowed = false,
                    SubscriptionTier = "Free",
                    DenialReason = "Deployment limit reached (3/3)",
                    DenialCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                    UpgradeRecommendation = new UpgradeRecommendation
                    {
                        CurrentTier = "Free",
                        RecommendedTier = "Basic"
                    }
                });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult
                {
                    
                    State = ARC76ReadinessState.Ready
                });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Approved
                });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Blocked));
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.RemediationTasks, Has.Count.EqualTo(1));
            Assert.That(result.RemediationTasks[0].Category, Is.EqualTo(BlockerCategory.Entitlement));
            Assert.That(result.RemediationTasks[0].Severity, Is.EqualTo(RemediationSeverity.Critical));
            Assert.That(result.Details.Entitlement.Passed, Is.False);
        }

        [Test]
        public async Task EvaluateReadinessAsync_AccountNotReady_ReturnsBlocked()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult
                {
                    IsAllowed = true,
                    SubscriptionTier = "Premium"
                });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult
                {
                    
                    State = ARC76ReadinessState.NotInitialized,
                    NotReadyReason = "ARC76 account not initialized",
                    RemediationSteps = new List<string>
                    {
                        "Initialize your ARC76 account through the authentication flow",
                        "Contact support if this issue persists"
                    }
                });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Approved
                });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Blocked));
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.RemediationTasks, Has.Count.EqualTo(1));
            Assert.That(result.RemediationTasks[0].Category, Is.EqualTo(BlockerCategory.AccountState));
            Assert.That(result.RemediationTasks[0].Severity, Is.EqualTo(RemediationSeverity.High));
            Assert.That(result.RemediationTasks[0].Actions, Has.Count.EqualTo(2));
            Assert.That(result.Details.AccountReadiness.Passed, Is.False);
        }

        [Test]
        public async Task EvaluateReadinessAsync_KycNotVerified_ReturnsWarning()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult
                {
                    IsAllowed = true,
                    SubscriptionTier = "Premium"
                });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult
                {
                    
                    State = ARC76ReadinessState.Ready
                });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Pending
                });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Warning));
            Assert.That(result.CanProceed, Is.True); // Can proceed despite warning
            Assert.That(result.RemediationTasks, Has.Count.EqualTo(1));
            Assert.That(result.RemediationTasks[0].Category, Is.EqualTo(BlockerCategory.KycAml));
            Assert.That(result.RemediationTasks[0].Severity, Is.EqualTo(RemediationSeverity.Medium));
            Assert.That(result.Details.KycAml.Passed, Is.False);
        }

        [Test]
        public async Task EvaluateReadinessAsync_MultipleBlockers_ReturnsOrderedRemediationTasks()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult
                {
                    IsAllowed = false,
                    SubscriptionTier = "Free",
                    DenialReason = "Limit reached",
                    DenialCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED
                });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult
                {
                    
                    State = ARC76ReadinessState.Failed,
                    NotReadyReason = "Account initialization failed"
                });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.NotStarted
                });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Blocked));
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.RemediationTasks, Has.Count.EqualTo(2)); // Entitlement + Account (KYC not included because other critical blockers exist)
            
            // Tasks should be ordered by severity (Critical first)
            Assert.That(result.RemediationTasks[0].Severity, Is.EqualTo(RemediationSeverity.Critical));
            Assert.That(result.RemediationTasks[1].Severity, Is.EqualTo(RemediationSeverity.High));
        }

        [Test]
        public async Task EvaluateReadinessAsync_StoresEvidence()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3",
                CorrelationId = "test-correlation-123"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult { IsAllowed = true, SubscriptionTier = "Premium" });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult { State = ARC76ReadinessState.Ready });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse { Success = true, Status = KycStatus.Approved });

            TokenLaunchReadinessEvidence? capturedEvidence = null;
            _mockRepository.Setup(x => x.StoreEvidenceAsync(It.IsAny<TokenLaunchReadinessEvidence>()))
                .Callback<TokenLaunchReadinessEvidence>(evidence => capturedEvidence = evidence)
                .ReturnsAsync((TokenLaunchReadinessEvidence e) => e);

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            _mockRepository.Verify(x => x.StoreEvidenceAsync(It.IsAny<TokenLaunchReadinessEvidence>()), Times.Once);
            Assert.That(capturedEvidence, Is.Not.Null);
            Assert.That(capturedEvidence!.UserId, Is.EqualTo("test-user"));
            Assert.That(capturedEvidence.CorrelationId, Is.EqualTo("test-correlation-123"));
            Assert.That(capturedEvidence.RequestSnapshot, Is.Not.Empty);
            Assert.That(capturedEvidence.ResponseSnapshot, Is.Not.Empty);
        }

        [Test]
        public async Task EvaluateReadinessAsync_EmitsMetrics()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult { IsAllowed = true, SubscriptionTier = "Premium" });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult { State = ARC76ReadinessState.Ready });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse { Success = true, Status = KycStatus.Approved });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            _mockMetricsService.Verify(x => x.IncrementCounter("token_launch_readiness_evaluation"), Times.Once);
            _mockMetricsService.Verify(x => x.IncrementCounter("token_launch_readiness_status_ready"), Times.Once);
            _mockMetricsService.Verify(x => x.RecordHistogram("token_launch_readiness_duration_ms", It.IsAny<double>()), Times.Once);
        }

        [Test]
        public async Task EvaluateReadinessAsync_ErrorDuringEvaluation_ReturnsBlockedWithSystemError()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ThrowsAsync(new Exception("Test exception"));
            
            // Mock other services to fail gracefully
            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult { State = ARC76ReadinessState.NotInitialized });
            
            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse { Success = false, Status = KycStatus.NotStarted });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Blocked));
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.RemediationTasks, Is.Not.Empty);
            // Should have remediation tasks since some evaluations did fail
            Assert.That(result.RemediationTasks.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetEvaluationAsync_ExistingEvaluation_ReturnsEvaluation()
        {
            // Arrange
            var evaluationId = "eval-123";
            var expectedResponse = new TokenLaunchReadinessResponse
            {
                EvaluationId = evaluationId,
                Status = ReadinessStatus.Ready,
                CanProceed = true
            };

            var evidence = new TokenLaunchReadinessEvidence
            {
                EvaluationId = evaluationId,
                ResponseSnapshot = System.Text.Json.JsonSerializer.Serialize(expectedResponse)
            };

            _mockRepository.Setup(x => x.GetEvidenceByEvaluationIdAsync(evaluationId))
                .ReturnsAsync(evidence);

            // Act
            var result = await _service.GetEvaluationAsync(evaluationId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.EvaluationId, Is.EqualTo(evaluationId));
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Ready));
        }

        [Test]
        public async Task GetEvaluationAsync_NonExistentEvaluation_ReturnsNull()
        {
            // Arrange
            var evaluationId = "nonexistent";
            _mockRepository.Setup(x => x.GetEvidenceByEvaluationIdAsync(evaluationId))
                .ReturnsAsync((TokenLaunchReadinessEvidence?)null);

            // Act
            var result = await _service.GetEvaluationAsync(evaluationId);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetEvaluationHistoryAsync_ReturnsHistoricalEvaluations()
        {
            // Arrange
            var userId = "test-user";
            var evidenceList = new List<TokenLaunchReadinessEvidence>
            {
                new TokenLaunchReadinessEvidence
                {
                    EvaluationId = "eval-1",
                    ResponseSnapshot = System.Text.Json.JsonSerializer.Serialize(new TokenLaunchReadinessResponse
                    {
                        EvaluationId = "eval-1",
                        Status = ReadinessStatus.Ready
                    })
                },
                new TokenLaunchReadinessEvidence
                {
                    EvaluationId = "eval-2",
                    ResponseSnapshot = System.Text.Json.JsonSerializer.Serialize(new TokenLaunchReadinessResponse
                    {
                        EvaluationId = "eval-2",
                        Status = ReadinessStatus.Blocked
                    })
                }
            };

            _mockRepository.Setup(x => x.GetEvidenceHistoryAsync(userId, 50, null))
                .ReturnsAsync(evidenceList);

            // Act
            var result = await _service.GetEvaluationHistoryAsync(userId);

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].EvaluationId, Is.EqualTo("eval-1"));
            Assert.That(result[1].EvaluationId, Is.EqualTo("eval-2"));
        }

        [Test]
        public async Task EvaluateReadinessAsync_IncludesCorrelationIdInAllComponents()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3",
                CorrelationId = "test-correlation-456"
            };

            _mockEntitlementService.Setup(x => x.CheckEntitlementAsync(It.IsAny<EntitlementCheckRequest>()))
                .ReturnsAsync(new EntitlementCheckResult { IsAllowed = true, SubscriptionTier = "Premium" });

            _mockArc76Service.Setup(x => x.CheckAccountReadinessAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ARC76AccountReadinessResult { State = ARC76ReadinessState.Ready });

            _mockKycService.Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new KycStatusResponse { Success = true, Status = KycStatus.Approved });

            // Act
            var result = await _service.EvaluateReadinessAsync(request);

            // Assert
            Assert.That(result.CorrelationId, Is.EqualTo("test-correlation-456"));
            
            // Verify correlation ID is passed to sub-services
            _mockEntitlementService.Verify(x => x.CheckEntitlementAsync(
                It.Is<EntitlementCheckRequest>(r => r.CorrelationId == "test-correlation-456")), Times.Once);
            
            _mockArc76Service.Verify(x => x.CheckAccountReadinessAsync(
                It.IsAny<string>(), "test-correlation-456"), Times.Once);
        }
    }
}

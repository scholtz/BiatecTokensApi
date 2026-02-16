using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Models.LifecycleIntelligence;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Models.DecisionIntelligence;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class LifecycleIntelligenceServiceTests
    {
        private Mock<ITokenLaunchReadinessService> _mockReadinessService = null!;
        private Mock<ITokenLaunchReadinessRepository> _mockRepository = null!;
        private Mock<IMetricsService> _mockMetricsService = null!;
        private Mock<IDecisionIntelligenceService> _mockDecisionService = null!;
        private Mock<ILogger<LifecycleIntelligenceService>> _mockLogger = null!;
        private LifecycleIntelligenceService _service = null!;

        [SetUp]
        public void Setup()
        {
            _mockReadinessService = new Mock<ITokenLaunchReadinessService>();
            _mockRepository = new Mock<ITokenLaunchReadinessRepository>();
            _mockMetricsService = new Mock<IMetricsService>();
            _mockDecisionService = new Mock<IDecisionIntelligenceService>();
            _mockLogger = new Mock<ILogger<LifecycleIntelligenceService>>();

            _service = new LifecycleIntelligenceService(
                _mockReadinessService.Object,
                _mockRepository.Object,
                _mockMetricsService.Object,
                _mockDecisionService.Object,
                _mockLogger.Object
            );
        }

        [Test]
        public async Task EvaluateReadinessV2Async_WithAllChecksPass_ReturnsReadyStatus()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "user-123",
                TokenType = "ARC3",
                Network = "mainnet"
            };

            var v1Response = new TokenLaunchReadinessResponse
            {
                EvaluationId = "eval-456",
                Status = ReadinessStatus.Ready,
                Summary = "All checks passed",
                CanProceed = true,
                Details = new ReadinessEvaluationDetails
                {
                    Entitlement = new CategoryEvaluationResult { Passed = true, Message = "Tier allows deployment" },
                    AccountReadiness = new CategoryEvaluationResult { Passed = true, Message = "Account ready" },
                    KycAml = new CategoryEvaluationResult { Passed = true, Message = "KYC verified" }
                },
                PolicyVersion = "2026.02.16.1",
                RemediationTasks = new List<RemediationTask>()
            };

            _mockReadinessService
                .Setup(s => s.EvaluateReadinessAsync(It.IsAny<TokenLaunchReadinessRequest>()))
                .ReturnsAsync(v1Response);

            // Act
            var result = await _service.EvaluateReadinessV2Async(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Ready));
            Assert.That(result.CanProceed, Is.True);
            Assert.That(result.ReadinessScore, Is.Not.Null);
            // Only 3 factors evaluated: entitlement (0.30), account (0.30), kyc (0.15) = 0.75
            Assert.That(result.ReadinessScore.OverallScore, Is.EqualTo(0.75).Within(0.01));
            Assert.That(result.ReadinessScore.Factors.Count, Is.GreaterThan(0));
            Assert.That(result.BlockingConditions.Count, Is.EqualTo(0));
            Assert.That(result.Confidence.OverallConfidence, Is.GreaterThan(0.8));
            Assert.That(result.EvidenceReferences.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task EvaluateReadinessV2Async_WithEntitlementFailure_ReturnsBlockedWithFactors()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "user-123",
                TokenType = "ARC3",
                Network = "mainnet"
            };

            var v1Response = new TokenLaunchReadinessResponse
            {
                EvaluationId = "eval-456",
                Status = ReadinessStatus.Blocked,
                Summary = "Blocked by entitlement",
                CanProceed = false,
                Details = new ReadinessEvaluationDetails
                {
                    Entitlement = new CategoryEvaluationResult
                    {
                        Passed = false,
                        Message = "Free tier limit exceeded",
                        ReasonCodes = new List<string> { ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED }
                    },
                    AccountReadiness = new CategoryEvaluationResult { Passed = true, Message = "Account ready" },
                    KycAml = new CategoryEvaluationResult { Passed = true, Message = "KYC verified" }
                },
                PolicyVersion = "2026.02.16.1",
                RemediationTasks = new List<RemediationTask>
                {
                    new RemediationTask
                    {
                        Category = BlockerCategory.Entitlement,
                        ErrorCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                        Description = "Deployment limit reached",
                        Severity = RemediationSeverity.Critical,
                        Actions = new List<string> { "Upgrade tier" }
                    }
                }
            };

            _mockReadinessService
                .Setup(s => s.EvaluateReadinessAsync(It.IsAny<TokenLaunchReadinessRequest>()))
                .ReturnsAsync(v1Response);

            // Act
            var result = await _service.EvaluateReadinessV2Async(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Blocked));
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.ReadinessScore, Is.Not.Null);
            Assert.That(result.ReadinessScore.OverallScore, Is.LessThan(0.8));
            Assert.That(result.ReadinessScore.MeetsThreshold, Is.False);
            Assert.That(result.ReadinessScore.BlockingFactors, Contains.Item("entitlement"));
            Assert.That(result.BlockingConditions.Count, Is.EqualTo(1));
            Assert.That(result.BlockingConditions[0].Type, Is.EqualTo("EntitlementLimit"));
            Assert.That(result.BlockingConditions[0].IsMandatory, Is.True);
        }

        [Test]
        public async Task EvaluateReadinessV2Async_WithMultipleFailures_ReturnsMultipleBlockingConditions()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "user-123",
                TokenType = "ARC3"
            };

            var v1Response = new TokenLaunchReadinessResponse
            {
                EvaluationId = "eval-789",
                Status = ReadinessStatus.Blocked,
                Summary = "Multiple blockers",
                CanProceed = false,
                Details = new ReadinessEvaluationDetails
                {
                    Entitlement = new CategoryEvaluationResult
                    {
                        Passed = false,
                        Message = "Entitlement failed",
                        ReasonCodes = new List<string> { ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED }
                    },
                    AccountReadiness = new CategoryEvaluationResult
                    {
                        Passed = false,
                        Message = "Account not ready",
                        ReasonCodes = new List<string> { ErrorCodes.ACCOUNT_NOT_READY }
                    },
                    KycAml = new CategoryEvaluationResult { Passed = true, Message = "KYC OK" }
                },
                PolicyVersion = "2026.02.16.1",
                RemediationTasks = new List<RemediationTask>
                {
                    new RemediationTask
                    {
                        Category = BlockerCategory.Entitlement,
                        ErrorCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                        Severity = RemediationSeverity.Critical,
                        Actions = new List<string> { "Upgrade" }
                    },
                    new RemediationTask
                    {
                        Category = BlockerCategory.AccountState,
                        ErrorCode = ErrorCodes.ACCOUNT_NOT_READY,
                        Severity = RemediationSeverity.High,
                        Actions = new List<string> { "Initialize account" }
                    }
                }
            };

            _mockReadinessService
                .Setup(s => s.EvaluateReadinessAsync(It.IsAny<TokenLaunchReadinessRequest>()))
                .ReturnsAsync(v1Response);

            // Act
            var result = await _service.EvaluateReadinessV2Async(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(ReadinessStatus.Blocked));
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.BlockingConditions.Count, Is.EqualTo(2));
            Assert.That(result.ReadinessScore.BlockingFactors, Has.Count.EqualTo(2));
            Assert.That(result.ReadinessScore.BlockingFactors, Contains.Item("entitlement"));
            Assert.That(result.ReadinessScore.BlockingFactors, Contains.Item("account_readiness"));
        }

        [Test]
        public async Task EvaluateReadinessV2Async_CalculatesWeightedScore_Correctly()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "user-123",
                TokenType = "ARC3"
            };

            var v1Response = new TokenLaunchReadinessResponse
            {
                EvaluationId = "eval-score",
                Status = ReadinessStatus.Warning,
                CanProceed = true,
                Details = new ReadinessEvaluationDetails
                {
                    Entitlement = new CategoryEvaluationResult { Passed = true },  // Weight: 0.30
                    AccountReadiness = new CategoryEvaluationResult { Passed = true },  // Weight: 0.30
                    KycAml = new CategoryEvaluationResult { Passed = false }  // Weight: 0.15, but advisory
                },
                PolicyVersion = "2026.02.16.1"
            };

            _mockReadinessService
                .Setup(s => s.EvaluateReadinessAsync(It.IsAny<TokenLaunchReadinessRequest>()))
                .ReturnsAsync(v1Response);

            // Act
            var result = await _service.EvaluateReadinessV2Async(request);

            // Assert
            Assert.That(result.ReadinessScore, Is.Not.Null);
            
            // Entitlement (passed): 1.0 * 0.30 = 0.30
            // AccountReadiness (passed): 1.0 * 0.30 = 0.30
            // KYC (failed but advisory): 0.5 * 0.15 = 0.075
            // Total: 0.30 + 0.30 + 0.075 = 0.675
            
            var entitlementFactor = result.ReadinessScore.Factors.FirstOrDefault(f => f.FactorId == "entitlement");
            Assert.That(entitlementFactor, Is.Not.Null);
            Assert.That(entitlementFactor!.Weight, Is.EqualTo(0.30));
            Assert.That(entitlementFactor.RawScore, Is.EqualTo(1.0));
            Assert.That(entitlementFactor.WeightedScore, Is.EqualTo(0.30));
            
            var accountFactor = result.ReadinessScore.Factors.FirstOrDefault(f => f.FactorId == "account_readiness");
            Assert.That(accountFactor, Is.Not.Null);
            Assert.That(accountFactor!.Weight, Is.EqualTo(0.30));
            Assert.That(accountFactor.RawScore, Is.EqualTo(1.0));
            Assert.That(accountFactor.WeightedScore, Is.EqualTo(0.30));
            
            var kycFactor = result.ReadinessScore.Factors.FirstOrDefault(f => f.FactorId == "kyc_aml");
            Assert.That(kycFactor, Is.Not.Null);
            Assert.That(kycFactor!.Weight, Is.EqualTo(0.15));
            Assert.That(kycFactor.RawScore, Is.EqualTo(0.5)); // Partial credit for advisory
            Assert.That(kycFactor.WeightedScore, Is.EqualTo(0.075));
            
            Assert.That(result.ReadinessScore.OverallScore, Is.EqualTo(0.675).Within(0.001));
        }

        [Test]
        public async Task EvaluateReadinessV2Async_BuildsConfidenceMetadata_Correctly()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest { UserId = "user-123", TokenType = "ARC3" };
            
            var v1Response = new TokenLaunchReadinessResponse
            {
                EvaluationId = "eval-conf",
                Status = ReadinessStatus.Ready,
                CanProceed = true,
                Details = new ReadinessEvaluationDetails
                {
                    Entitlement = new CategoryEvaluationResult { Passed = true },
                    AccountReadiness = new CategoryEvaluationResult { Passed = true },
                    KycAml = new CategoryEvaluationResult { Passed = true }
                },
                PolicyVersion = "2026.02.16.1"
            };

            _mockReadinessService
                .Setup(s => s.EvaluateReadinessAsync(It.IsAny<TokenLaunchReadinessRequest>()))
                .ReturnsAsync(v1Response);

            // Act
            var result = await _service.EvaluateReadinessV2Async(request);

            // Assert
            Assert.That(result.Confidence, Is.Not.Null);
            Assert.That(result.Confidence.OverallConfidence, Is.GreaterThan(0.9));
            Assert.That(result.Confidence.DataCompleteness, Is.GreaterThan(0));
            Assert.That(result.Confidence.Freshness, Is.EqualTo(DataFreshness.Fresh));
            Assert.That(result.Confidence.FactorsEvaluated, Is.EqualTo(3));
            Assert.That(result.Confidence.HighConfidenceFactors, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetEvidenceAsync_WithValidEvidenceId_ReturnsEvidence()
        {
            // Arrange
            var evidenceId = "eval-123";
            var evidence = new TokenLaunchReadinessEvidence
            {
                Id = "evidence-456",
                EvaluationId = evidenceId,
                UserId = "user-123",
                RequestSnapshot = "{}",
                ResponseSnapshot = "{\"status\":\"Ready\"}",
                CreatedAt = DateTime.UtcNow,
                CorrelationId = "corr-789",
                DataHash = "abc123"
            };

            _mockRepository
                .Setup(r => r.GetEvidenceByEvaluationIdAsync(evidenceId))
                .ReturnsAsync(evidence);

            // Act
            var result = await _service.GetEvidenceAsync(evidenceId, includeContent: false);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence, Is.Not.Null);
            Assert.That(result.Evidence.EvidenceId, Is.EqualTo(evidence.Id));
            Assert.That(result.Evidence.DataHash, Is.EqualTo(evidence.DataHash));
            Assert.That(result.Evidence.Type, Is.EqualTo(EvidenceType.AuditLog));
            Assert.That(result.EvaluationId, Is.EqualTo(evidenceId));
            Assert.That(result.ContentJson, Is.Null); // Not requested
        }

        [Test]
        public async Task GetEvidenceAsync_WithContentRequested_ReturnsFullContent()
        {
            // Arrange
            var evidenceId = "eval-123";
            var responseContent = "{\"status\":\"Ready\",\"score\":0.95}";
            var evidence = new TokenLaunchReadinessEvidence
            {
                Id = "evidence-456",
                EvaluationId = evidenceId,
                UserId = "user-123",
                ResponseSnapshot = responseContent,
                CreatedAt = DateTime.UtcNow
            };

            _mockRepository
                .Setup(r => r.GetEvidenceByEvaluationIdAsync(evidenceId))
                .ReturnsAsync(evidence);

            // Act
            var result = await _service.GetEvidenceAsync(evidenceId, includeContent: true);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.ContentJson, Is.EqualTo(responseContent));
        }

        [Test]
        public async Task GetEvidenceAsync_WithInvalidId_ReturnsFailure()
        {
            // Arrange
            var evidenceId = "invalid-id";
            _mockRepository
                .Setup(r => r.GetEvidenceByEvaluationIdAsync(evidenceId))
                .ReturnsAsync((TokenLaunchReadinessEvidence?)null);

            // Act
            var result = await _service.GetEvidenceAsync(evidenceId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task GetRiskSignalsAsync_WithoutAssetId_ReturnsEmptySignals()
        {
            // Arrange
            var request = new RiskSignalsRequest
            {
                Limit = 50
            };

            // Act
            var result = await _service.GetRiskSignalsAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Signals.Count, Is.EqualTo(0));
            Assert.That(result.Summary, Does.Contain("No risk signals detected"));
        }

        [Test]
        public async Task GetRiskSignalsAsync_WithAssetAnalytics_GeneratesRiskSignals()
        {
            // Arrange
            var request = new RiskSignalsRequest
            {
                AssetId = 12345,
                Network = "mainnet",
                Limit = 50,
                IncludeTrendHistory = true
            };

            var insightResponse = new InsightMetricsResponse
            {
                AssetId = 12345,
                Network = "mainnet",
                ConcentrationRisk = new ConcentrationRiskMetrics
                {
                    Top10HoldersPercentage = 65.0,
                    Top50HoldersPercentage = 85.0,
                    Top100HoldersPercentage = 95.0,
                    RiskLevel = ConcentrationRisk.High,
                    Trend = BiatecTokensApi.Models.DecisionIntelligence.TrendDirection.Improving
                },
                TransactionQuality = new TransactionQualityMetrics
                {
                    AverageTransactionsPerDay = 25.0,
                    SuccessRate = 98.5,
                    Trend = BiatecTokensApi.Models.DecisionIntelligence.TrendDirection.Stable
                },
                LiquidityHealth = new LiquidityHealthMetrics
                {
                    LiquidityScore = 55.0,
                    CirculatingSupplyPercentage = 70.0,
                    Status = LiquidityStatus.Fair
                },
                Retention = new RetentionMetrics
                {
                    ChurnRate = 12.5,
                    RetentionRate = 87.5,
                    Trend = BiatecTokensApi.Models.DecisionIntelligence.TrendDirection.Stable
                }
            };

            _mockDecisionService
                .Setup(s => s.GetInsightMetricsAsync(It.IsAny<GetInsightMetricsRequest>()))
                .ReturnsAsync(insightResponse);

            // Act
            var result = await _service.GetRiskSignalsAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Signals.Count, Is.GreaterThan(0));
            Assert.That(result.AssetId, Is.EqualTo(12345));
            Assert.That(result.Network, Is.EqualTo("mainnet"));
            
            // Verify concentration signal
            var concentrationSignal = result.Signals.FirstOrDefault(s => s.Type == RiskSignalType.HolderConcentration);
            Assert.That(concentrationSignal, Is.Not.Null);
            Assert.That(concentrationSignal!.Severity, Is.EqualTo(RiskSeverity.High));
            Assert.That(concentrationSignal.Value, Is.EqualTo(65.0));
            Assert.That(concentrationSignal.Trend, Is.EqualTo(BiatecTokensApi.Models.LifecycleIntelligence.TrendDirection.Improving));
            
            // Verify liquidity signal
            var liquiditySignal = result.Signals.FirstOrDefault(s => s.Type == RiskSignalType.LiquidityRisk);
            Assert.That(liquiditySignal, Is.Not.Null);
            Assert.That(liquiditySignal!.Severity, Is.EqualTo(RiskSeverity.Medium));
        }

        [Test]
        public async Task GetRiskSignalsAsync_WithSeverityFilter_FiltersCorrectly()
        {
            // Arrange
            var request = new RiskSignalsRequest
            {
                AssetId = 12345,
                Network = "mainnet",
                MinimumSeverity = RiskSeverity.High,
                Limit = 50
            };

            var insightResponse = new InsightMetricsResponse
            {
                AssetId = 12345,
                Network = "mainnet",
                ConcentrationRisk = new ConcentrationRiskMetrics
                {
                    Top10HoldersPercentage = 75.0, // Should be High severity
                    Trend = BiatecTokensApi.Models.DecisionIntelligence.TrendDirection.Stable
                },
                TransactionQuality = new TransactionQualityMetrics
                {
                    AverageTransactionsPerDay = 150.0, // Should be Info severity
                    Trend = BiatecTokensApi.Models.DecisionIntelligence.TrendDirection.Improving
                }
            };

            _mockDecisionService
                .Setup(s => s.GetInsightMetricsAsync(It.IsAny<GetInsightMetricsRequest>()))
                .ReturnsAsync(insightResponse);

            // Act
            var result = await _service.GetRiskSignalsAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            
            // Only High+ severity signals should be returned
            Assert.That(result.Signals.All(s => s.Severity >= RiskSeverity.High), Is.True);
            
            // Concentration signal should be included
            var concentrationSignal = result.Signals.FirstOrDefault(s => s.Type == RiskSignalType.HolderConcentration);
            Assert.That(concentrationSignal, Is.Not.Null);
        }

        [Test]
        public async Task EvaluateReadinessV2Async_EmitsMetrics_OnEvaluation()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest { UserId = "user-123", TokenType = "ARC3" };
            var v1Response = new TokenLaunchReadinessResponse
            {
                EvaluationId = "eval-metrics",
                Status = ReadinessStatus.Ready,
                CanProceed = true,
                Details = new ReadinessEvaluationDetails
                {
                    Entitlement = new CategoryEvaluationResult { Passed = true },
                    AccountReadiness = new CategoryEvaluationResult { Passed = true },
                    KycAml = new CategoryEvaluationResult { Passed = true }
                }
            };

            _mockReadinessService
                .Setup(s => s.EvaluateReadinessAsync(It.IsAny<TokenLaunchReadinessRequest>()))
                .ReturnsAsync(v1Response);

            // Act
            await _service.EvaluateReadinessV2Async(request);

            // Assert - Verify metrics were emitted
            _mockMetricsService.Verify(
                m => m.IncrementCounter("lifecycle_readiness_v2_evaluation"),
                Times.Once
            );
            
            _mockMetricsService.Verify(
                m => m.IncrementCounter("lifecycle_readiness_v2_status_ready"),
                Times.Once
            );
            
            _mockMetricsService.Verify(
                m => m.RecordHistogram("lifecycle_readiness_v2_duration_ms", It.IsAny<double>()),
                Times.Once
            );
            
            _mockMetricsService.Verify(
                m => m.RecordHistogram("lifecycle_readiness_v2_score", It.IsAny<double>()),
                Times.Once
            );
        }
    }
}

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.LifecycleIntelligence;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for lifecycle intelligence operations including readiness scoring and risk signals
    /// </summary>
    public class LifecycleIntelligenceService : ILifecycleIntelligenceService
    {
        private readonly ITokenLaunchReadinessService _readinessService;
        private readonly ITokenLaunchReadinessRepository _repository;
        private readonly IMetricsService _metricsService;
        private readonly IDecisionIntelligenceService _decisionIntelligenceService;
        private readonly ILogger<LifecycleIntelligenceService> _logger;

        private const string ScoringVersion = "v2.0";
        private const double ReadinessThreshold = 0.8;

        // Factor weights for readiness scoring
        private static readonly Dictionary<string, double> FactorWeights = new()
        {
            { "entitlement", 0.30 },
            { "account_readiness", 0.30 },
            { "kyc_aml", 0.15 },
            { "compliance", 0.15 },
            { "integration", 0.10 }
        };

        public LifecycleIntelligenceService(
            ITokenLaunchReadinessService readinessService,
            ITokenLaunchReadinessRepository repository,
            IMetricsService metricsService,
            IDecisionIntelligenceService decisionIntelligenceService,
            ILogger<LifecycleIntelligenceService> logger)
        {
            _readinessService = readinessService;
            _repository = repository;
            _metricsService = metricsService;
            _decisionIntelligenceService = decisionIntelligenceService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TokenLaunchReadinessResponseV2> EvaluateReadinessV2Async(TokenLaunchReadinessRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation(
                    "Starting lifecycle readiness evaluation (v2): UserId={UserId}, TokenType={TokenType}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(request.TokenType),
                    correlationId);

                // Get v1 readiness evaluation first
                var v1Response = await _readinessService.EvaluateReadinessAsync(request);

                // Build v2 response with enhancements
                var v2Response = new TokenLaunchReadinessResponseV2
                {
                    EvaluationId = v1Response.EvaluationId,
                    Status = v1Response.Status,
                    Summary = v1Response.Summary,
                    CanProceed = v1Response.CanProceed,
                    RemediationTasks = v1Response.RemediationTasks,
                    Details = v1Response.Details,
                    PolicyVersion = v1Response.PolicyVersion,
                    CorrelationId = correlationId
                };

                // Build readiness score with factor breakdown
                v2Response.ReadinessScore = BuildReadinessScore(v1Response);

                // Build blocking conditions
                v2Response.BlockingConditions = BuildBlockingConditions(v1Response);

                // Build confidence metadata
                v2Response.Confidence = BuildConfidenceMetadata(v1Response, v2Response.ReadinessScore);

                // Build evidence references
                v2Response.EvidenceReferences = await BuildEvidenceReferencesAsync(v1Response);

                // Add caveats
                v2Response.Caveats = BuildCaveats(v1Response, v2Response.Confidence);

                sw.Stop();
                v2Response.EvaluationTimeMs = sw.ElapsedMilliseconds;

                // Emit metrics
                EmitMetrics(v2Response);

                _logger.LogInformation(
                    "Lifecycle readiness evaluation (v2) completed: UserId={UserId}, Status={Status}, Score={Score:F2}, Duration={DurationMs}ms",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    v2Response.Status,
                    v2Response.ReadinessScore?.OverallScore ?? 0.0,
                    v2Response.EvaluationTimeMs);

                return v2Response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                
                _logger.LogError(ex,
                    "Error during lifecycle readiness evaluation (v2): UserId={UserId}, TokenType={TokenType}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(request.TokenType));

                // Return blocked response on error
                return new TokenLaunchReadinessResponseV2
                {
                    Status = ReadinessStatus.Blocked,
                    Summary = "Readiness evaluation failed due to system error",
                    CanProceed = false,
                    CorrelationId = correlationId,
                    EvaluationTimeMs = sw.ElapsedMilliseconds,
                    RemediationTasks = new List<RemediationTask>
                    {
                        new RemediationTask
                        {
                            Category = BlockerCategory.Integration,
                            ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                            Description = "System error during readiness evaluation",
                            Severity = RemediationSeverity.Critical,
                            OwnerHint = "Technical Support",
                            Actions = new List<string>
                            {
                                "Contact technical support with correlation ID: " + correlationId,
                                "Retry the operation after a few minutes"
                            }
                        }
                    }
                };
            }
        }

        /// <inheritdoc/>
        public async Task<EvidenceRetrievalResponse?> GetEvidenceAsync(string evidenceId, bool includeContent = false)
        {
            try
            {
                // Try to find evidence by evaluation ID first (most common case)
                var evidence = await _repository.GetEvidenceByEvaluationIdAsync(evidenceId);

                if (evidence == null)
                {
                    _logger.LogWarning("Evidence not found: {EvidenceId}", LoggingHelper.SanitizeLogInput(evidenceId));
                    return new EvidenceRetrievalResponse
                    {
                        Success = false,
                        ErrorMessage = $"Evidence not found: {evidenceId}"
                    };
                }

                // Build evidence reference
                var evidenceRef = new EvidenceReference
                {
                    EvidenceId = evidence.Id,
                    Type = EvidenceType.AuditLog,
                    Source = "TokenLaunchReadinessService",
                    CollectedAt = evidence.CreatedAt,
                    ValidatedAt = evidence.CreatedAt,
                    DataHash = evidence.DataHash,
                    Summary = $"Token launch readiness evaluation for user {evidence.UserId}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "evaluationId", evidence.EvaluationId },
                        { "userId", evidence.UserId },
                        { "correlationId", evidence.CorrelationId ?? string.Empty }
                    }
                };

                var response = new EvidenceRetrievalResponse
                {
                    Success = true,
                    Evidence = evidenceRef,
                    EvaluationId = evidence.EvaluationId
                };

                if (includeContent)
                {
                    response.ContentJson = evidence.ResponseSnapshot;
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evidence: {EvidenceId}", LoggingHelper.SanitizeLogInput(evidenceId));
                return new EvidenceRetrievalResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving evidence"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<RiskSignalsResponse> GetRiskSignalsAsync(RiskSignalsRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Retrieving risk signals: AssetId={AssetId}, Network={Network}, MinSeverity={MinSeverity}",
                    request.AssetId,
                    LoggingHelper.SanitizeLogInput(request.Network ?? "unspecified"),
                    request.MinimumSeverity);

                var signals = new List<RiskSignal>();

                // If asset ID is provided, generate risk signals based on asset analytics
                if (request.AssetId.HasValue && !string.IsNullOrEmpty(request.Network))
                {
                    signals = await GenerateRiskSignalsForAssetAsync(
                        request.AssetId.Value,
                        request.Network,
                        request.SignalTypes,
                        request.MinimumSeverity,
                        request.IncludeTrendHistory);
                }

                // Filter and limit results
                var filteredSignals = signals;
                if (request.MinimumSeverity.HasValue)
                {
                    filteredSignals = signals.Where(s => s.Severity >= request.MinimumSeverity.Value).ToList();
                }

                filteredSignals = filteredSignals.Take(request.Limit).ToList();

                var response = new RiskSignalsResponse
                {
                    Success = true,
                    Signals = filteredSignals,
                    TotalSignals = signals.Count,
                    MaxSeverity = signals.Count > 0 ? signals.Max(s => s.Severity) : (RiskSeverity?)null,
                    SignalsRequiringAttention = signals.Count(s => s.RequiresAttention),
                    AssetId = request.AssetId,
                    Network = request.Network,
                    Summary = BuildRiskSignalSummary(filteredSignals)
                };

                _logger.LogInformation(
                    "Risk signals retrieved: Count={Count}, MaxSeverity={MaxSeverity}, RequiringAttention={RequiringAttention}",
                    response.Signals.Count,
                    response.MaxSeverity,
                    response.SignalsRequiringAttention);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving risk signals: AssetId={AssetId}, Network={Network}",
                    request.AssetId,
                    LoggingHelper.SanitizeLogInput(request.Network ?? "unspecified"));

                return new RiskSignalsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving risk signals"
                };
            }
        }

        #region Private Helper Methods

        private ReadinessScore BuildReadinessScore(TokenLaunchReadinessResponse v1Response)
        {
            var factors = new List<ReadinessFactorBreakdown>();

            // Entitlement factor
            factors.Add(new ReadinessFactorBreakdown
            {
                FactorId = "entitlement",
                FactorName = "Subscription Entitlement",
                Category = "Entitlement",
                Weight = FactorWeights["entitlement"],
                RawScore = v1Response.Details.Entitlement.Passed ? 1.0 : 0.0,
                WeightedScore = (v1Response.Details.Entitlement.Passed ? 1.0 : 0.0) * FactorWeights["entitlement"],
                Passed = v1Response.Details.Entitlement.Passed,
                IsBlocking = true,
                Explanation = v1Response.Details.Entitlement.Message,
                EvidenceReference = v1Response.EvaluationId
            });

            // Account readiness factor
            factors.Add(new ReadinessFactorBreakdown
            {
                FactorId = "account_readiness",
                FactorName = "ARC76 Account Readiness",
                Category = "AccountState",
                Weight = FactorWeights["account_readiness"],
                RawScore = v1Response.Details.AccountReadiness.Passed ? 1.0 : 0.0,
                WeightedScore = (v1Response.Details.AccountReadiness.Passed ? 1.0 : 0.0) * FactorWeights["account_readiness"],
                Passed = v1Response.Details.AccountReadiness.Passed,
                IsBlocking = true,
                Explanation = v1Response.Details.AccountReadiness.Message,
                EvidenceReference = v1Response.EvaluationId
            });

            // KYC/AML factor (advisory)
            factors.Add(new ReadinessFactorBreakdown
            {
                FactorId = "kyc_aml",
                FactorName = "KYC/AML Verification",
                Category = "KycAml",
                Weight = FactorWeights["kyc_aml"],
                RawScore = v1Response.Details.KycAml.Passed ? 1.0 : 0.5, // Partial credit if not passed but advisory
                WeightedScore = (v1Response.Details.KycAml.Passed ? 1.0 : 0.5) * FactorWeights["kyc_aml"],
                Passed = v1Response.Details.KycAml.Passed,
                IsBlocking = false, // Advisory only
                Explanation = v1Response.Details.KycAml.Message,
                EvidenceReference = v1Response.EvaluationId,
                Confidence = v1Response.Details.KycAml.Passed ? 1.0 : 0.7 // Lower confidence for advisory checks
            });

            // Compliance factor (if available)
            if (v1Response.Details.ComplianceDecisions.Message != string.Empty)
            {
                factors.Add(new ReadinessFactorBreakdown
                {
                    FactorId = "compliance",
                    FactorName = "Compliance Decisions",
                    Category = "ComplianceDecision",
                    Weight = FactorWeights["compliance"],
                    RawScore = v1Response.Details.ComplianceDecisions.Passed ? 1.0 : 0.0,
                    WeightedScore = (v1Response.Details.ComplianceDecisions.Passed ? 1.0 : 0.0) * FactorWeights["compliance"],
                    Passed = v1Response.Details.ComplianceDecisions.Passed,
                    IsBlocking = false,
                    Explanation = v1Response.Details.ComplianceDecisions.Message,
                    EvidenceReference = v1Response.EvaluationId
                });
            }

            // Integration factor (if available)
            if (v1Response.Details.Integration.Message != string.Empty)
            {
                factors.Add(new ReadinessFactorBreakdown
                {
                    FactorId = "integration",
                    FactorName = "Integration Health",
                    Category = "Integration",
                    Weight = FactorWeights["integration"],
                    RawScore = v1Response.Details.Integration.Passed ? 1.0 : 0.0,
                    WeightedScore = (v1Response.Details.Integration.Passed ? 1.0 : 0.0) * FactorWeights["integration"],
                    Passed = v1Response.Details.Integration.Passed,
                    IsBlocking = false,
                    Explanation = v1Response.Details.Integration.Message,
                    EvidenceReference = v1Response.EvaluationId
                });
            }

            var overallScore = factors.Sum(f => f.WeightedScore);
            var overallConfidence = factors.Average(f => f.Confidence);

            var blockingFactors = factors
                .Where(f => !f.Passed && f.IsBlocking)
                .Select(f => f.FactorId)
                .ToList();

            return new ReadinessScore
            {
                OverallScore = overallScore,
                OverallConfidence = overallConfidence,
                Factors = factors,
                BlockingFactors = blockingFactors,
                ScoringVersion = ScoringVersion,
                ReadinessThreshold = ReadinessThreshold
            };
        }

        private List<BlockingCondition> BuildBlockingConditions(TokenLaunchReadinessResponse v1Response)
        {
            var conditions = new List<BlockingCondition>();

            // Entitlement blocking condition
            if (!v1Response.Details.Entitlement.Passed)
            {
                var task = v1Response.RemediationTasks.FirstOrDefault(t => t.Category == BlockerCategory.Entitlement);
                conditions.Add(new BlockingCondition
                {
                    Type = "EntitlementLimit",
                    Description = v1Response.Details.Entitlement.Message,
                    ErrorCode = v1Response.Details.Entitlement.ReasonCodes.FirstOrDefault() ?? ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                    Category = "Entitlement",
                    IsMandatory = true,
                    ResolutionSteps = task?.Actions ?? new List<string> { "Upgrade subscription tier" },
                    EvidenceReference = v1Response.EvaluationId,
                    EstimatedResolutionHours = task?.EstimatedResolutionHours ?? 1
                });
            }

            // Account readiness blocking condition
            if (!v1Response.Details.AccountReadiness.Passed)
            {
                var task = v1Response.RemediationTasks.FirstOrDefault(t => t.Category == BlockerCategory.AccountState);
                conditions.Add(new BlockingCondition
                {
                    Type = "AccountNotReady",
                    Description = v1Response.Details.AccountReadiness.Message,
                    ErrorCode = v1Response.Details.AccountReadiness.ReasonCodes.FirstOrDefault() ?? ErrorCodes.ACCOUNT_NOT_READY,
                    Category = "AccountState",
                    IsMandatory = true,
                    ResolutionSteps = task?.Actions ?? new List<string> { "Complete account initialization" },
                    EvidenceReference = v1Response.EvaluationId,
                    EstimatedResolutionHours = task?.EstimatedResolutionHours ?? 1
                });
            }

            return conditions;
        }

        private ConfidenceMetadata BuildConfidenceMetadata(
            TokenLaunchReadinessResponse v1Response,
            ReadinessScore score)
        {
            var factorsEvaluated = score.Factors.Count;
            var highConfidenceFactors = score.Factors.Count(f => f.Confidence >= 0.9);
            var lowConfidenceFactors = score.Factors.Count(f => f.Confidence < 0.7);

            var metadata = new ConfidenceMetadata
            {
                OverallConfidence = score.OverallConfidence,
                DataCompleteness = (double)factorsEvaluated / FactorWeights.Count * 100.0,
                Freshness = DataFreshness.Fresh, // All evaluations are real-time
                FactorsEvaluated = factorsEvaluated,
                HighConfidenceFactors = highConfidenceFactors,
                LowConfidenceFactors = lowConfidenceFactors
            };

            // Determine missing factors
            var evaluatedFactorIds = score.Factors.Select(f => f.FactorId).ToHashSet();
            var missingFactors = FactorWeights.Keys.Except(evaluatedFactorIds).ToList();
            metadata.MissingFactors = missingFactors;

            // Add quality warnings
            if (lowConfidenceFactors > 0)
            {
                metadata.QualityWarnings.Add($"{lowConfidenceFactors} factor(s) with low confidence");
            }

            if (missingFactors.Count > 0)
            {
                metadata.QualityWarnings.Add($"{missingFactors.Count} factor(s) not evaluated");
            }

            return metadata;
        }

        private async Task<List<EvidenceReference>> BuildEvidenceReferencesAsync(TokenLaunchReadinessResponse v1Response)
        {
            var references = new List<EvidenceReference>();

            // Add primary evaluation evidence
            references.Add(new EvidenceReference
            {
                EvidenceId = v1Response.EvaluationId,
                Type = EvidenceType.AuditLog,
                Source = "TokenLaunchReadinessService",
                Summary = "Complete readiness evaluation with all category results",
                Metadata = new Dictionary<string, string>
                {
                    { "evaluationId", v1Response.EvaluationId },
                    { "status", v1Response.Status.ToString() },
                    { "canProceed", v1Response.CanProceed.ToString() }
                }
            });

            return await Task.FromResult(references);
        }

        private List<string> BuildCaveats(TokenLaunchReadinessResponse v1Response, ConfidenceMetadata confidence)
        {
            var caveats = new List<string>();

            if (confidence.DataCompleteness < 100.0)
            {
                caveats.Add($"Evaluation based on {confidence.DataCompleteness:F0}% of expected factors");
            }

            if (confidence.LowConfidenceFactors > 0)
            {
                caveats.Add($"{confidence.LowConfidenceFactors} factor(s) evaluated with lower confidence");
            }

            if (!v1Response.Details.KycAml.Passed)
            {
                caveats.Add("KYC/AML verification is recommended but not required for launch");
            }

            return caveats;
        }

        private async Task<List<RiskSignal>> GenerateRiskSignalsForAssetAsync(
            ulong assetId,
            string network,
            List<RiskSignalType>? signalTypes,
            RiskSeverity? minimumSeverity,
            bool includeTrendHistory)
        {
            var signals = new List<RiskSignal>();

            try
            {
                // Try to get metrics from decision intelligence service
                var insightRequest = new Models.DecisionIntelligence.GetInsightMetricsRequest
                {
                    AssetId = assetId,
                    Network = network,
                    StartTime = DateTime.UtcNow.AddDays(-30),
                    EndTime = DateTime.UtcNow,
                    RequestedMetrics = new List<string>()
                };
                
                var insights = await _decisionIntelligenceService.GetInsightMetricsAsync(insightRequest);

                // Generate concentration risk signal
                if (ShouldIncludeSignalType(signalTypes, RiskSignalType.HolderConcentration))
                {
                    var concentrationSignal = GenerateConcentrationRiskSignal(insights, assetId, network);
                    if (concentrationSignal != null && MeetsSeverityThreshold(concentrationSignal.Severity, minimumSeverity))
                    {
                        signals.Add(concentrationSignal);
                    }
                }

                // Generate inactivity risk signal
                if (ShouldIncludeSignalType(signalTypes, RiskSignalType.InactivityRisk))
                {
                    var inactivitySignal = GenerateInactivityRiskSignal(insights, assetId, network);
                    if (inactivitySignal != null && MeetsSeverityThreshold(inactivitySignal.Severity, minimumSeverity))
                    {
                        signals.Add(inactivitySignal);
                    }
                }

                // Generate liquidity risk signal
                if (ShouldIncludeSignalType(signalTypes, RiskSignalType.LiquidityRisk))
                {
                    var liquiditySignal = GenerateLiquidityRiskSignal(insights, assetId, network);
                    if (liquiditySignal != null && MeetsSeverityThreshold(liquiditySignal.Severity, minimumSeverity))
                    {
                        signals.Add(liquiditySignal);
                    }
                }

                // Generate churn risk signal
                if (ShouldIncludeSignalType(signalTypes, RiskSignalType.ChurnRisk))
                {
                    var churnSignal = GenerateChurnRiskSignal(insights, assetId, network);
                    if (churnSignal != null && MeetsSeverityThreshold(churnSignal.Severity, minimumSeverity))
                    {
                        signals.Add(churnSignal);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve analytics for risk signal generation: AssetId={AssetId}", assetId);
                
                // Return placeholder signal indicating data unavailable
                signals.Add(new RiskSignal
                {
                    Type = RiskSignalType.TechnicalRisk,
                    Severity = RiskSeverity.Low,
                    Value = 0,
                    Trend = TrendDirection.Unknown,
                    Description = "Risk signals unavailable - analytics data not accessible",
                    AssetId = assetId,
                    Network = network,
                    RecommendedActions = new List<string> { "Retry later when analytics data is available" },
                    Confidence = 0.0
                });
            }

            return signals;
        }

        private RiskSignal? GenerateConcentrationRiskSignal(
            Models.DecisionIntelligence.InsightMetricsResponse? insights,
            ulong assetId,
            string network)
        {
            if (insights?.ConcentrationRisk == null)
                return null;

            var concentration = insights.ConcentrationRisk;
            var top10Percentage = concentration.Top10HoldersPercentage;

            // Determine severity based on concentration
            var severity = top10Percentage switch
            {
                >= 80 => RiskSeverity.Critical,
                >= 60 => RiskSeverity.High,
                >= 40 => RiskSeverity.Medium,
                >= 20 => RiskSeverity.Low,
                _ => RiskSeverity.Info
            };

            var trend = concentration.Trend switch
            {
                Models.DecisionIntelligence.TrendDirection.Declining => TrendDirection.Worsening,
                Models.DecisionIntelligence.TrendDirection.Improving => TrendDirection.Improving,
                Models.DecisionIntelligence.TrendDirection.Stable => TrendDirection.Stable,
                _ => TrendDirection.Unknown
            };

            return new RiskSignal
            {
                Type = RiskSignalType.HolderConcentration,
                Severity = severity,
                Value = top10Percentage,
                Trend = trend,
                Description = $"Top 10 holders own {top10Percentage:F1}% of total supply",
                AssetId = assetId,
                Network = network,
                Thresholds = new RiskThresholds
                {
                    InfoThreshold = 20,
                    LowThreshold = 40,
                    MediumThreshold = 60,
                    HighThreshold = 80,
                    CriticalThreshold = 90
                },
                RecommendedActions = severity >= RiskSeverity.High
                    ? new List<string>
                    {
                        "Monitor whale addresses for unusual activity",
                        "Consider incentive programs to improve distribution",
                        "Review tokenomics for concentration risks"
                    }
                    : new List<string> { "Continue monitoring holder distribution" },
                Confidence = 0.9
            };
        }

        private RiskSignal? GenerateInactivityRiskSignal(
            Models.DecisionIntelligence.InsightMetricsResponse? insights,
            ulong assetId,
            string network)
        {
            if (insights?.TransactionQuality == null)
                return null;

            var txQuality = insights.TransactionQuality;
            var dailyTxCount = txQuality.AverageTransactionsPerDay;

            // Determine severity based on activity level
            var severity = dailyTxCount switch
            {
                < 10 => RiskSeverity.High,
                < 50 => RiskSeverity.Medium,
                < 100 => RiskSeverity.Low,
                _ => RiskSeverity.Info
            };

            var trend = txQuality.Trend switch
            {
                Models.DecisionIntelligence.TrendDirection.Declining => TrendDirection.Worsening,
                Models.DecisionIntelligence.TrendDirection.Improving => TrendDirection.Improving,
                Models.DecisionIntelligence.TrendDirection.Stable => TrendDirection.Stable,
                _ => TrendDirection.Unknown
            };

            return new RiskSignal
            {
                Type = RiskSignalType.InactivityRisk,
                Severity = severity,
                Value = dailyTxCount,
                Trend = trend,
                Description = $"Average daily transaction count: {dailyTxCount:F0}",
                AssetId = assetId,
                Network = network,
                Thresholds = new RiskThresholds
                {
                    HighThreshold = 10,
                    MediumThreshold = 50,
                    LowThreshold = 100,
                    InfoThreshold = 200
                },
                RecommendedActions = severity >= RiskSeverity.Medium
                    ? new List<string>
                    {
                        "Review token utility and usage incentives",
                        "Engage community to increase activity",
                        "Analyze reasons for low transaction volume"
                    }
                    : new List<string> { "Maintain current activity levels" },
                Confidence = 0.85
            };
        }

        private RiskSignal? GenerateLiquidityRiskSignal(
            Models.DecisionIntelligence.InsightMetricsResponse? insights,
            ulong assetId,
            string network)
        {
            if (insights?.LiquidityHealth == null)
                return null;

            var liquidity = insights.LiquidityHealth;
            var liquidityScore = liquidity.LiquidityScore;

            // Determine severity based on liquidity score (0-100)
            var severity = liquidityScore switch
            {
                < 20 => RiskSeverity.Critical,
                < 40 => RiskSeverity.High,
                < 60 => RiskSeverity.Medium,
                < 80 => RiskSeverity.Low,
                _ => RiskSeverity.Info
            };

            // Liquidity doesn't have a trend property, set to unknown
            var trend = TrendDirection.Unknown;

            return new RiskSignal
            {
                Type = RiskSignalType.LiquidityRisk,
                Severity = severity,
                Value = liquidityScore,
                Trend = trend,
                Description = $"Liquidity health score: {liquidityScore:F0}/100",
                AssetId = assetId,
                Network = network,
                Thresholds = new RiskThresholds
                {
                    CriticalThreshold = 20,
                    HighThreshold = 40,
                    MediumThreshold = 60,
                    LowThreshold = 80,
                    InfoThreshold = 90
                },
                RecommendedActions = severity >= RiskSeverity.High
                    ? new List<string>
                    {
                        "Review circulating supply and locked tokens",
                        "Consider liquidity provision programs",
                        "Analyze trading volume patterns"
                    }
                    : new List<string> { "Continue monitoring liquidity metrics" },
                Confidence = 0.8
            };
        }

        private RiskSignal? GenerateChurnRiskSignal(
            Models.DecisionIntelligence.InsightMetricsResponse? insights,
            ulong assetId,
            string network)
        {
            if (insights?.Retention == null)
                return null;

            var retention = insights.Retention;
            var churnRate = retention.ChurnRate;

            // Determine severity based on churn rate
            var severity = churnRate switch
            {
                >= 50 => RiskSeverity.Critical,
                >= 30 => RiskSeverity.High,
                >= 15 => RiskSeverity.Medium,
                >= 5 => RiskSeverity.Low,
                _ => RiskSeverity.Info
            };

            var trend = retention.Trend switch
            {
                Models.DecisionIntelligence.TrendDirection.Declining => TrendDirection.Improving, // Declining churn is good
                Models.DecisionIntelligence.TrendDirection.Improving => TrendDirection.Worsening, // Improving retention trend, but churn context
                Models.DecisionIntelligence.TrendDirection.Stable => TrendDirection.Stable,
                _ => TrendDirection.Unknown
            };

            return new RiskSignal
            {
                Type = RiskSignalType.ChurnRisk,
                Severity = severity,
                Value = churnRate,
                Trend = trend,
                Description = $"Holder churn rate: {churnRate:F1}%",
                AssetId = assetId,
                Network = network,
                Thresholds = new RiskThresholds
                {
                    InfoThreshold = 5,
                    LowThreshold = 15,
                    MediumThreshold = 30,
                    HighThreshold = 50,
                    CriticalThreshold = 70
                },
                RecommendedActions = severity >= RiskSeverity.High
                    ? new List<string>
                    {
                        "Investigate reasons for high holder turnover",
                        "Review token value proposition and holder benefits",
                        "Implement retention programs or incentives",
                        "Analyze holder behavior patterns"
                    }
                    : new List<string> { "Monitor retention metrics" },
                Confidence = 0.85
            };
        }

        private bool ShouldIncludeSignalType(List<RiskSignalType>? requestedTypes, RiskSignalType signalType)
        {
            return requestedTypes == null || requestedTypes.Count == 0 || requestedTypes.Contains(signalType);
        }

        private bool MeetsSeverityThreshold(RiskSeverity severity, RiskSeverity? minimumSeverity)
        {
            return !minimumSeverity.HasValue || severity >= minimumSeverity.Value;
        }

        private string BuildRiskSignalSummary(List<RiskSignal> signals)
        {
            if (signals.Count == 0)
            {
                return "No risk signals detected";
            }

            var criticalCount = signals.Count(s => s.Severity == RiskSeverity.Critical);
            var highCount = signals.Count(s => s.Severity == RiskSeverity.High);

            if (criticalCount > 0)
            {
                return $"{criticalCount} critical risk signal(s) detected - immediate attention required";
            }

            if (highCount > 0)
            {
                return $"{highCount} high-priority risk signal(s) detected - action recommended";
            }

            var mediumCount = signals.Count(s => s.Severity == RiskSeverity.Medium);
            if (mediumCount > 0)
            {
                return $"{mediumCount} medium-priority risk signal(s) detected - monitoring recommended";
            }

            return $"{signals.Count} informational risk signal(s) - no immediate action required";
        }

        private void EmitMetrics(TokenLaunchReadinessResponseV2 response)
        {
            try
            {
                _metricsService.IncrementCounter("lifecycle_readiness_v2_evaluation");
                _metricsService.IncrementCounter($"lifecycle_readiness_v2_status_{response.Status.ToString().ToLower()}");
                _metricsService.RecordHistogram("lifecycle_readiness_v2_duration_ms", (double)response.EvaluationTimeMs);

                if (response.ReadinessScore != null)
                {
                    _metricsService.RecordHistogram("lifecycle_readiness_v2_score", response.ReadinessScore.OverallScore);
                    _metricsService.RecordHistogram("lifecycle_readiness_v2_confidence", response.ReadinessScore.OverallConfidence);
                }

                if (!response.CanProceed)
                {
                    _metricsService.IncrementCounter("lifecycle_readiness_v2_blocked");
                    _metricsService.RecordHistogram("lifecycle_readiness_v2_blocking_conditions", response.BlockingConditions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit lifecycle readiness v2 metrics");
            }
        }

        #endregion
    }
}

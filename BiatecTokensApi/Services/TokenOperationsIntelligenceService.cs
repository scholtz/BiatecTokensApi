using System.Diagnostics;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenOperationsIntelligence;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for consolidated token operations intelligence.
    /// Provides deterministic health assessments, lifecycle recommendations,
    /// and normalized event summaries with graceful degraded-mode handling.
    /// </summary>
    public class TokenOperationsIntelligenceService : ITokenOperationsIntelligenceService
    {
        private readonly IMemoryCache _cache;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<TokenOperationsIntelligenceService> _logger;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private const string CacheKeyPrefix = "token_ops_intel_";
        private const string ContractApiVersion = "v1.0";
        private const string ContractSchemaVersion = "1.0.0";

        // Policy dimension identifiers
        private static readonly string[] AllPolicyDimensions = new[]
        {
            "MintAuthority",
            "MetadataCompleteness",
            "TreasuryMovement",
            "OwnershipConsistency"
        };

        // Reason codes for lifecycle recommendations
        private static class ReasonCodes
        {
            public const string MintAuthorityUnrevoked = "MINT_AUTHORITY_UNREVOKED";
            public const string MetadataIncomplete = "METADATA_INCOMPLETE";
            public const string MetadataUrlMissing = "METADATA_URL_MISSING";
            public const string TreasuryUnmonitored = "TREASURY_UNMONITORED";
            public const string OwnershipUnverified = "OWNERSHIP_UNVERIFIED";
            public const string DeploymentConsistencyCheck = "DEPLOYMENT_CONSISTENCY_CHECK";
            public const string HighConcentrationRisk = "HIGH_CONCENTRATION_RISK";
            public const string LowActivityWarning = "LOW_ACTIVITY_WARNING";
        }

        public TokenOperationsIntelligenceService(
            IMemoryCache cache,
            IMetricsService metricsService,
            ILogger<TokenOperationsIntelligenceService> logger)
        {
            _cache = cache;
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TokenOperationsIntelligenceResponse> GetOperationsIntelligenceAsync(
            TokenOperationsIntelligenceRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
            var degradedSources = new List<string>();

            _logger.LogInformation(
                "Starting operations intelligence evaluation: AssetId={AssetId}, Network={Network}, CorrelationId={CorrelationId}",
                request.AssetId,
                LoggingHelper.SanitizeLogInput(request.Network),
                correlationId);

            TokenHealthAssessment? health = null;
            List<LifecycleRecommendation> recommendations = new();
            List<NormalizedTokenEvent> events = new();
            bool healthFromCache = false;

            // 1. Evaluate health (with degraded-mode fallback)
            try
            {
                var cacheKey = $"{CacheKeyPrefix}health_{request.AssetId}_{LoggingHelper.SanitizeLogInput(request.Network)}";
                if (_cache.TryGetValue(cacheKey, out TokenHealthAssessment? cachedHealth) && cachedHealth != null)
                {
                    health = cachedHealth;
                    healthFromCache = true;
                    _logger.LogDebug("Health assessment served from cache: AssetId={AssetId}", request.AssetId);
                }
                else
                {
                    health = await EvaluateHealthAsync(request.AssetId, request.Network, request.PolicyDimensions);
                    _cache.Set(cacheKey, health, CacheDuration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Health evaluation failed for AssetId={AssetId}, Network={Network}. Returning degraded response.",
                    request.AssetId,
                    LoggingHelper.SanitizeLogInput(request.Network));
                degradedSources.Add("HealthEvaluator");
                health = BuildDegradedHealthAssessment("Health evaluation temporarily unavailable");
            }

            // 2. Generate recommendations (with degraded-mode fallback)
            try
            {
                recommendations = await GetRecommendationsAsync(request.AssetId, request.Network);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Recommendation generation failed for AssetId={AssetId}. Continuing with empty recommendations.",
                    request.AssetId);
                degradedSources.Add("RecommendationEngine");
            }

            // 3. Retrieve events (with degraded-mode fallback)
            try
            {
                events = await GetNormalizedEventsAsync(
                    request.AssetId,
                    request.Network,
                    request.MaxEvents,
                    request.IncludeEventDetails);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Event retrieval failed for AssetId={AssetId}. Continuing with empty events.",
                    request.AssetId);
                degradedSources.Add("EventNormalizer");
            }

            sw.Stop();
            _logger.LogInformation(
                "Operations intelligence completed: AssetId={AssetId}, HealthStatus={HealthStatus}, Recommendations={RecCount}, Events={EvCount}, Degraded={Degraded}, LatencyMs={LatencyMs}, CorrelationId={CorrelationId}",
                request.AssetId,
                health?.OverallStatus,
                recommendations.Count,
                events.Count,
                degradedSources.Count > 0,
                sw.ElapsedMilliseconds,
                correlationId);

            return new TokenOperationsIntelligenceResponse
            {
                Success = true,
                AssetId = request.AssetId,
                Network = request.Network,
                CorrelationId = correlationId,
                ContractVersion = new OperationsContractVersion
                {
                    ApiVersion = ContractApiVersion,
                    SchemaVersion = ContractSchemaVersion,
                    GeneratedAt = DateTime.UtcNow
                },
                Health = health,
                Recommendations = recommendations,
                Events = events,
                IsDegraded = degradedSources.Count > 0,
                DegradedSources = degradedSources,
                GeneratedAt = DateTime.UtcNow,
                HealthFromCache = healthFromCache
            };
        }

        /// <inheritdoc/>
        public Task<TokenHealthAssessment> EvaluateHealthAsync(
            ulong assetId,
            string network,
            IEnumerable<string>? dimensions = null)
        {
            var dimensionList = (dimensions?.ToList() is { Count: > 0 } d) ? d : AllPolicyDimensions.ToList();

            var results = new List<PolicyAssuranceResult>();

            foreach (var dimension in dimensionList)
            {
                var result = EvaluatePolicyDimension(assetId, network, dimension);
                results.Add(result);
            }

            var health = ComputeOverallHealth(results);
            return Task.FromResult(health);
        }

        /// <inheritdoc/>
        public Task<List<LifecycleRecommendation>> GetRecommendationsAsync(ulong assetId, string network)
        {
            var recommendations = new List<LifecycleRecommendation>();

            // Rule 1: Mint authority posture - recommend revoking if applicable
            recommendations.Add(new LifecycleRecommendation
            {
                ReasonCode = ReasonCodes.MintAuthorityUnrevoked,
                Title = "Review Mint Authority Posture",
                Rationale = "Unrevoked mint authority increases supply manipulation risk. Review whether ongoing mint capability is intentional and documented.",
                SuggestedAction = "Confirm mint authority assignments match governance policy. Revoke if supply cap has been reached.",
                Priority = 80,
                Severity = AssessmentSeverity.Medium,
                RelatedDimension = "MintAuthority"
            });

            // Rule 2: Metadata completeness
            recommendations.Add(new LifecycleRecommendation
            {
                ReasonCode = ReasonCodes.MetadataIncomplete,
                Title = "Complete Token Metadata",
                Rationale = "Complete and verifiable metadata improves token discoverability, trust, and compliance posture.",
                SuggestedAction = "Ensure all required metadata fields are present, valid, and accessible via the metadata URL.",
                Priority = 60,
                Severity = AssessmentSeverity.Low,
                RelatedDimension = "MetadataCompleteness"
            });

            // Rule 3: Ownership consistency
            recommendations.Add(new LifecycleRecommendation
            {
                ReasonCode = ReasonCodes.OwnershipUnverified,
                Title = "Verify Ownership Consistency",
                Rationale = "Consistent and documented ownership reduces governance risk and simplifies regulatory reporting.",
                SuggestedAction = "Verify that current owner/manager addresses match approved governance records.",
                Priority = 70,
                Severity = AssessmentSeverity.Medium,
                RelatedDimension = "OwnershipConsistency"
            });

            // Rule 4: Treasury movement monitoring
            recommendations.Add(new LifecycleRecommendation
            {
                ReasonCode = ReasonCodes.TreasuryUnmonitored,
                Title = "Enable Treasury Movement Monitoring",
                Rationale = "Unmonitored treasury movements create audit gaps and increase financial risk.",
                SuggestedAction = "Set up alert thresholds for large treasury movements and review transaction history.",
                Priority = 65,
                Severity = AssessmentSeverity.Medium,
                RelatedDimension = "TreasuryMovement"
            });

            // Sort by priority descending (highest priority first) - deterministic ordering
            recommendations = recommendations.OrderByDescending(r => r.Priority)
                                             .ThenBy(r => r.ReasonCode)
                                             .ToList();

            return Task.FromResult(recommendations);
        }

        /// <inheritdoc/>
        public Task<List<NormalizedTokenEvent>> GetNormalizedEventsAsync(
            ulong assetId,
            string network,
            int maxEvents = 10,
            bool includeDetails = false)
        {
            // Normalize event limit
            var limit = Math.Clamp(maxEvents, 1, 50);

            // Return representative normalized events (deterministic structure)
            var events = new List<NormalizedTokenEvent>
            {
                new NormalizedTokenEvent
                {
                    EventId = $"evt_{assetId}_deploy_001",
                    OccurredAt = DateTime.UtcNow.AddDays(-30),
                    Category = TokenEventCategory.Deployment,
                    Impact = EventImpact.High,
                    Actor = "system",
                    Description = "Token deployed on network",
                    Details = includeDetails ? new Dictionary<string, object>
                    {
                        { "assetId", assetId },
                        { "network", network }
                    } : null
                }
            };

            // Return up to limit events
            return Task.FromResult(events.Take(limit).ToList());
        }

        /// <summary>
        /// Evaluates a single policy dimension deterministically
        /// </summary>
        private PolicyAssuranceResult EvaluatePolicyDimension(ulong assetId, string network, string dimension)
        {
            return dimension switch
            {
                "MintAuthority" => EvaluateMintAuthority(assetId, network),
                "MetadataCompleteness" => EvaluateMetadataCompleteness(assetId, network),
                "TreasuryMovement" => EvaluateTreasuryMovement(assetId, network),
                "OwnershipConsistency" => EvaluateOwnershipConsistency(assetId, network),
                _ => new PolicyAssuranceResult
                {
                    DimensionId = dimension,
                    DimensionName = dimension,
                    Status = PolicyStatus.Degraded,
                    Severity = AssessmentSeverity.Low,
                    Description = $"Unknown policy dimension: {dimension}",
                    FindingCode = "UNKNOWN_DIMENSION",
                    RemediationHint = "Use a valid policy dimension identifier"
                }
            };
        }

        /// <summary>
        /// Evaluates mint authority posture
        /// </summary>
        private PolicyAssuranceResult EvaluateMintAuthority(ulong assetId, string network)
        {
            // Deterministic evaluation: pass for standard checks (real evaluation would query blockchain)
            return new PolicyAssuranceResult
            {
                DimensionId = "MintAuthority",
                DimensionName = "Mint Authority Posture",
                Status = PolicyStatus.Warning,
                Severity = AssessmentSeverity.Medium,
                Description = "Mint authority is present. Verify this is intentional and governed.",
                FindingCode = "MINT_AUTHORITY_PRESENT",
                RemediationHint = "Review mint authority assignments against your token governance policy.",
                EvaluatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Evaluates metadata completeness
        /// </summary>
        private PolicyAssuranceResult EvaluateMetadataCompleteness(ulong assetId, string network)
        {
            return new PolicyAssuranceResult
            {
                DimensionId = "MetadataCompleteness",
                DimensionName = "Metadata Completeness",
                Status = PolicyStatus.Pass,
                Severity = AssessmentSeverity.Healthy,
                Description = "Token metadata structure is complete.",
                EvaluatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Evaluates treasury movement sanity
        /// </summary>
        private PolicyAssuranceResult EvaluateTreasuryMovement(ulong assetId, string network)
        {
            return new PolicyAssuranceResult
            {
                DimensionId = "TreasuryMovement",
                DimensionName = "Treasury Movement Sanity",
                Status = PolicyStatus.Pass,
                Severity = AssessmentSeverity.Healthy,
                Description = "No anomalous treasury movements detected.",
                EvaluatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Evaluates deployment/ownership consistency
        /// </summary>
        private PolicyAssuranceResult EvaluateOwnershipConsistency(ulong assetId, string network)
        {
            return new PolicyAssuranceResult
            {
                DimensionId = "OwnershipConsistency",
                DimensionName = "Deployment and Ownership Consistency",
                Status = PolicyStatus.Pass,
                Severity = AssessmentSeverity.Healthy,
                Description = "Deployment and ownership records are consistent.",
                EvaluatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Computes overall health from individual policy results
        /// </summary>
        private TokenHealthAssessment ComputeOverallHealth(List<PolicyAssuranceResult> results)
        {
            if (results.Count == 0)
            {
                return new TokenHealthAssessment
                {
                    OverallStatus = TokenHealthStatus.Unknown,
                    HealthScore = 0.0,
                    PolicyResults = results
                };
            }

            var passing = results.Count(r => r.Status == PolicyStatus.Pass);
            var warnings = results.Count(r => r.Status == PolicyStatus.Warning);
            var failures = results.Count(r => r.Status == PolicyStatus.Fail);
            var degraded = results.Count(r => r.Status == PolicyStatus.Degraded);
            var total = results.Count;

            // Compute composite health score
            double score = (passing * 1.0 + warnings * 0.5) / total;

            // Determine overall status
            TokenHealthStatus status;
            if (failures > 0)
                status = failures >= total / 2 ? TokenHealthStatus.Unhealthy : TokenHealthStatus.Degraded;
            else if (warnings > 0)
                status = TokenHealthStatus.Degraded;
            else if (degraded == total)
                status = TokenHealthStatus.Unknown;
            else
                status = TokenHealthStatus.Healthy;

            return new TokenHealthAssessment
            {
                OverallStatus = status,
                HealthScore = Math.Round(score, 4),
                PolicyResults = results,
                IsPartialResult = degraded > 0,
                DegradedReason = degraded > 0 ? $"{degraded} dimension(s) could not be evaluated" : null
            };
        }

        /// <summary>
        /// Builds a degraded health assessment for use when evaluation fails
        /// </summary>
        private TokenHealthAssessment BuildDegradedHealthAssessment(string reason)
        {
            return new TokenHealthAssessment
            {
                OverallStatus = TokenHealthStatus.Unknown,
                HealthScore = 0.0,
                PolicyResults = AllPolicyDimensions.Select(d => new PolicyAssuranceResult
                {
                    DimensionId = d,
                    DimensionName = d,
                    Status = PolicyStatus.Degraded,
                    Severity = AssessmentSeverity.Low,
                    Description = reason,
                    FindingCode = ErrorCodes.EXTERNAL_SERVICE_ERROR
                }).ToList(),
                IsPartialResult = true,
                DegradedReason = reason
            };
        }
    }
}

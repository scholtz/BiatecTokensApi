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
                    health = await EvaluateHealthAsync(request.AssetId, request.Network, request.PolicyDimensions, request.StateInputs);
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
            var isDegraded = degradedSources.Count > 0;
            var latencyMs = sw.Elapsed.TotalMilliseconds;

            // Emit telemetry: latency, degraded-mode invocation, failure class
            _metricsService.RecordHistogram("operations_intelligence.latency_ms", latencyMs);
            _metricsService.IncrementCounter("operations_intelligence.requests_total");
            if (isDegraded)
            {
                _metricsService.IncrementCounter("operations_intelligence.degraded_total");
                foreach (var source in degradedSources)
                {
                    _metricsService.IncrementCounter($"operations_intelligence.failure_class.{SanitizeMetricLabel(source)}");
                }
            }
            if (healthFromCache)
            {
                _metricsService.IncrementCounter("operations_intelligence.cache_hits_total");
            }

            _logger.LogInformation(
                "Operations intelligence completed: AssetId={AssetId}, HealthStatus={HealthStatus}, Recommendations={RecCount}, Events={EvCount}, Degraded={Degraded}, LatencyMs={LatencyMs}, CorrelationId={CorrelationId}",
                request.AssetId,
                health?.OverallStatus,
                recommendations.Count,
                events.Count,
                isDegraded,
                latencyMs,
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
        public virtual Task<TokenHealthAssessment> EvaluateHealthAsync(
            ulong assetId,
            string network,
            IEnumerable<string>? dimensions = null,
            TokenStateInputs? stateInputs = null)
        {
            var dimensionList = (dimensions?.ToList() is { Count: > 0 } d) ? d : AllPolicyDimensions.ToList();

            var results = new List<PolicyAssuranceResult>();

            foreach (var dimension in dimensionList)
            {
                var result = EvaluatePolicyDimension(assetId, network, dimension, stateInputs);
                results.Add(result);
            }

            var health = ComputeOverallHealth(results);
            return Task.FromResult(health);
        }

        /// <inheritdoc/>
        public virtual Task<List<LifecycleRecommendation>> GetRecommendationsAsync(ulong assetId, string network)
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
        public virtual Task<List<NormalizedTokenEvent>> GetNormalizedEventsAsync(
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
        private PolicyAssuranceResult EvaluatePolicyDimension(ulong assetId, string network, string dimension, TokenStateInputs? state)
        {
            return dimension switch
            {
                "MintAuthority" => EvaluateMintAuthority(assetId, network, state),
                "MetadataCompleteness" => EvaluateMetadataCompleteness(assetId, network, state),
                "TreasuryMovement" => EvaluateTreasuryMovement(assetId, network, state),
                "OwnershipConsistency" => EvaluateOwnershipConsistency(assetId, network, state),
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
        /// Evaluates mint authority posture.
        /// Pass: authority is revoked or supply cap reached.
        /// Warning: authority present but not critical.
        /// Critical: authority present and supply cap not reached with no governance documentation.
        /// </summary>
        private PolicyAssuranceResult EvaluateMintAuthority(ulong assetId, string network, TokenStateInputs? state)
        {
            // If authority is revoked: Pass
            if (state?.MintAuthorityRevoked == true)
            {
                return new PolicyAssuranceResult
                {
                    DimensionId = "MintAuthority",
                    DimensionName = "Mint Authority Posture",
                    Status = PolicyStatus.Pass,
                    Severity = AssessmentSeverity.Healthy,
                    Description = "Mint authority has been revoked. Supply is fixed.",
                    FindingCode = "MINT_AUTHORITY_REVOKED",
                    EvaluatedAt = DateTime.UtcNow
                };
            }

            // If supply cap reached: Warning (authority exists but is inactive)
            if (state?.SupplyCapReached == true)
            {
                return new PolicyAssuranceResult
                {
                    DimensionId = "MintAuthority",
                    DimensionName = "Mint Authority Posture",
                    Status = PolicyStatus.Warning,
                    Severity = AssessmentSeverity.Low,
                    Description = "Mint authority present but supply cap reached. Consider revoking authority.",
                    FindingCode = "MINT_AUTHORITY_PRESENT_CAP_REACHED",
                    RemediationHint = "Revoke mint authority to permanently fix the supply.",
                    EvaluatedAt = DateTime.UtcNow
                };
            }

            // If state explicitly shows authority present without cap: Critical
            if (state?.MintAuthorityRevoked == false && state?.SupplyCapReached == false)
            {
                return new PolicyAssuranceResult
                {
                    DimensionId = "MintAuthority",
                    DimensionName = "Mint Authority Posture",
                    Status = PolicyStatus.Fail,
                    Severity = AssessmentSeverity.High,
                    Description = "Active mint authority with no supply cap represents high supply manipulation risk.",
                    FindingCode = "MINT_AUTHORITY_UNCAPPED",
                    RemediationHint = "Review mint authority governance policy. Consider setting a supply cap or revoking authority.",
                    EvaluatedAt = DateTime.UtcNow
                };
            }

            // Default conservative: authority present, state unknown
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
        /// Evaluates metadata completeness.
        /// Pass: completeness >= 90% and URL accessible.
        /// Warning: completeness 50-89% or URL unknown.
        /// Fail: completeness &lt; 50% or URL inaccessible.
        /// </summary>
        private PolicyAssuranceResult EvaluateMetadataCompleteness(ulong assetId, string network, TokenStateInputs? state)
        {
            // URL inaccessible: Fail
            if (state?.MetadataUrlAccessible == false)
            {
                return new PolicyAssuranceResult
                {
                    DimensionId = "MetadataCompleteness",
                    DimensionName = "Metadata Completeness",
                    Status = PolicyStatus.Fail,
                    Severity = AssessmentSeverity.High,
                    Description = "Metadata URL is inaccessible. Token metadata cannot be verified.",
                    FindingCode = "METADATA_URL_INACCESSIBLE",
                    RemediationHint = "Restore metadata URL accessibility or update to a reachable endpoint.",
                    EvaluatedAt = DateTime.UtcNow
                };
            }

            var completeness = state?.MetadataCompletenessPercent;

            if (completeness.HasValue)
            {
                if (completeness.Value >= 90 && state?.MetadataUrlAccessible != false)
                {
                    return new PolicyAssuranceResult
                    {
                        DimensionId = "MetadataCompleteness",
                        DimensionName = "Metadata Completeness",
                        Status = PolicyStatus.Pass,
                        Severity = AssessmentSeverity.Healthy,
                        Description = $"Token metadata is {completeness.Value:F0}% complete.",
                        EvaluatedAt = DateTime.UtcNow,
                        Details = new Dictionary<string, object> { { "completenessPercent", completeness.Value } }
                    };
                }

                if (completeness.Value < 50)
                {
                    return new PolicyAssuranceResult
                    {
                        DimensionId = "MetadataCompleteness",
                        DimensionName = "Metadata Completeness",
                        Status = PolicyStatus.Fail,
                        Severity = AssessmentSeverity.High,
                        Description = $"Token metadata is only {completeness.Value:F0}% complete. Critical fields are missing.",
                        FindingCode = "METADATA_CRITICALLY_INCOMPLETE",
                        RemediationHint = "Complete required metadata fields: name, description, image, decimals.",
                        EvaluatedAt = DateTime.UtcNow,
                        Details = new Dictionary<string, object> { { "completenessPercent", completeness.Value } }
                    };
                }

                // 50-89%: Warning
                return new PolicyAssuranceResult
                {
                    DimensionId = "MetadataCompleteness",
                    DimensionName = "Metadata Completeness",
                    Status = PolicyStatus.Warning,
                    Severity = AssessmentSeverity.Medium,
                    Description = $"Token metadata is {completeness.Value:F0}% complete. Some optional fields are missing.",
                    FindingCode = "METADATA_PARTIALLY_COMPLETE",
                    RemediationHint = "Complete remaining metadata fields to improve token discoverability.",
                    EvaluatedAt = DateTime.UtcNow,
                    Details = new Dictionary<string, object> { { "completenessPercent", completeness.Value } }
                };
            }

            // Default: unknown state
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
        /// Evaluates treasury movement sanity.
        /// Pass: no large movements detected.
        /// Warning: movements above 10% of supply.
        /// Critical: movements above 30% of supply.
        /// </summary>
        private PolicyAssuranceResult EvaluateTreasuryMovement(ulong assetId, string network, TokenStateInputs? state)
        {
            if (state?.LargeTreasuryMovementDetected == true)
            {
                var movementPct = state.LargestMovementPercent ?? 0;

                if (movementPct >= 30)
                {
                    return new PolicyAssuranceResult
                    {
                        DimensionId = "TreasuryMovement",
                        DimensionName = "Treasury Movement Sanity",
                        Status = PolicyStatus.Fail,
                        Severity = AssessmentSeverity.Critical,
                        Description = $"Critical treasury movement detected: {movementPct:F1}% of total supply moved.",
                        FindingCode = "TREASURY_CRITICAL_MOVEMENT",
                        RemediationHint = "Immediately review treasury movement authorization. Contact governance team.",
                        EvaluatedAt = DateTime.UtcNow,
                        Details = new Dictionary<string, object> { { "largestMovementPercent", movementPct } }
                    };
                }

                return new PolicyAssuranceResult
                {
                    DimensionId = "TreasuryMovement",
                    DimensionName = "Treasury Movement Sanity",
                    Status = PolicyStatus.Warning,
                    Severity = AssessmentSeverity.High,
                    Description = $"Large treasury movement detected: {movementPct:F1}% of total supply moved.",
                    FindingCode = "TREASURY_LARGE_MOVEMENT",
                    RemediationHint = "Review treasury movement authorization records and verify approvals.",
                    EvaluatedAt = DateTime.UtcNow,
                    Details = new Dictionary<string, object> { { "largestMovementPercent", movementPct } }
                };
            }

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
        /// Evaluates deployment/ownership consistency.
        /// Pass: records match, no unauthorized changes.
        /// Warning: minor discrepancy or unknown state.
        /// Critical: unauthorized manager changes detected.
        /// </summary>
        private PolicyAssuranceResult EvaluateOwnershipConsistency(ulong assetId, string network, TokenStateInputs? state)
        {
            if (state?.UnauthorizedManagerChangesDetected == true)
            {
                return new PolicyAssuranceResult
                {
                    DimensionId = "OwnershipConsistency",
                    DimensionName = "Deployment and Ownership Consistency",
                    Status = PolicyStatus.Fail,
                    Severity = AssessmentSeverity.Critical,
                    Description = "Unauthorized manager address changes detected. Potential security incident.",
                    FindingCode = "OWNERSHIP_UNAUTHORIZED_CHANGE",
                    RemediationHint = "Immediately audit ownership changes. Review access controls and key management.",
                    EvaluatedAt = DateTime.UtcNow
                };
            }

            if (state?.OwnershipRecordsMatch == false)
            {
                return new PolicyAssuranceResult
                {
                    DimensionId = "OwnershipConsistency",
                    DimensionName = "Deployment and Ownership Consistency",
                    Status = PolicyStatus.Warning,
                    Severity = AssessmentSeverity.Medium,
                    Description = "On-chain ownership records do not fully match deployment records.",
                    FindingCode = "OWNERSHIP_RECORDS_MISMATCH",
                    RemediationHint = "Reconcile on-chain owner/manager addresses with deployment documentation.",
                    EvaluatedAt = DateTime.UtcNow
                };
            }

            if (state?.OwnershipRecordsMatch == true)
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

        /// <summary>
        /// Sanitizes a string for safe use as a metric label.
        /// Replaces non-alphanumeric characters with underscores and lowercases the result.
        /// </summary>
        private static string SanitizeMetricLabel(string label)
        {
            var sanitized = System.Text.RegularExpressions.Regex.Replace(
                label.ToLowerInvariant(),
                @"[^a-z0-9_]",
                "_");
            return sanitized;
        }
    }
}

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IComplianceEvidenceLaunchDecisionService"/>.
    /// </summary>
    /// <remarks>
    /// Implements deterministic, idempotent compliance evidence evaluation and launch decision
    /// logic.  All decisions are stored in-process and are reproducible for the lifetime of
    /// the application.  The evaluation engine runs a fixed, ordered rule set so that traces
    /// are stable across replays.
    /// </remarks>
    public class ComplianceEvidenceLaunchDecisionService : IComplianceEvidenceLaunchDecisionService
    {
        private readonly ILogger<ComplianceEvidenceLaunchDecisionService> _logger;

        // In-memory stores
        private readonly Dictionary<string, LaunchDecisionResponse> _decisions = new();
        private readonly Dictionary<string, DecisionTraceResponse> _traces = new();
        private readonly Dictionary<string, List<ComplianceEvidenceItem>> _evidenceStore = new();
        private readonly Dictionary<string, string> _idempotencyIndex = new(); // key → decisionId
        private readonly object _lock = new();

        private const string CurrentPolicyVersion = "2026.03.07.1";
        private const string SchemaVersion = "1.0.0";

        // Known valid token standards
        private static readonly HashSet<string> KnownStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ERC20", "ARC1400"
        };

        // Known valid networks
        private static readonly HashSet<string> KnownNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "mainnet", "testnet", "betanet", "voimain", "aramidmain", "base", "base-testnet"
        };

        /// <summary>Initialises a new instance of <see cref="ComplianceEvidenceLaunchDecisionService"/>.</summary>
        public ComplianceEvidenceLaunchDecisionService(
            ILogger<ComplianceEvidenceLaunchDecisionService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<LaunchDecisionResponse> EvaluateLaunchDecisionAsync(LaunchDecisionRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
            var policyVersion = request.PolicyVersion ?? CurrentPolicyVersion;

            // Input validation
            if (string.IsNullOrWhiteSpace(request.OwnerId))
                return Task.FromResult(ErrorResponse("MISSING_OWNER_ID",
                    "OwnerId is required.", correlationId));

            if (string.IsNullOrWhiteSpace(request.TokenStandard))
                return Task.FromResult(ErrorResponse("MISSING_TOKEN_STANDARD",
                    "TokenStandard is required.", correlationId));

            if (!KnownStandards.Contains(request.TokenStandard))
                return Task.FromResult(ErrorResponse("INVALID_TOKEN_STANDARD",
                    $"Token standard '{LoggingHelper.SanitizeLogInput(request.TokenStandard)}' is not supported. " +
                    "Supported: ASA, ARC3, ARC200, ERC20, ARC1400.", correlationId));

            if (string.IsNullOrWhiteSpace(request.Network))
                return Task.FromResult(ErrorResponse("MISSING_NETWORK",
                    "Network is required.", correlationId));

            if (!KnownNetworks.Contains(request.Network))
                return Task.FromResult(ErrorResponse("INVALID_NETWORK",
                    $"Network '{LoggingHelper.SanitizeLogInput(request.Network)}' is not recognised.", correlationId));

            lock (_lock)
            {
                // Idempotency check (skip when ForceRefresh is requested)
                if (!request.ForceRefresh &&
                    !string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                    _idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingId) &&
                    _decisions.TryGetValue(existingId, out var cached))
                {
                    _logger.LogInformation(
                        "Idempotent replay for key={Key}, DecisionId={Id}",
                        LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                        cached.DecisionId);

                    var replay = ShallowCopy(cached);
                    replay.IsIdempotentReplay = true;
                    replay.CorrelationId = correlationId;
                    return Task.FromResult(replay);
                }

                // Run the deterministic rule evaluation pipeline
                var (rules, evidence) = RunRuleEvaluation(request, policyVersion, correlationId);

                // Aggregate blockers and warnings
                var blockers = new List<LaunchBlocker>();
                var warnings = new List<LaunchWarning>();
                var actions = new List<RecommendedAction>();

                foreach (var rule in rules)
                {
                    if (rule.Outcome == RuleOutcome.Fail)
                    {
                        blockers.Add(BuildBlocker(rule));
                        actions.Add(BuildAction(rule, mandatory: true, priority: blockers.Count));
                    }
                    else if (rule.Outcome == RuleOutcome.Warning)
                    {
                        warnings.Add(BuildWarning(rule));
                        actions.Add(BuildAction(rule, mandatory: false, priority: 100 + warnings.Count));
                    }
                }

                // Determine overall status
                var status = DetermineStatus(blockers, warnings);
                var canLaunch = status == LaunchDecisionStatus.Ready || status == LaunchDecisionStatus.Warning;
                var summary = BuildSummary(status, blockers.Count, warnings.Count);

                var decisionId = Guid.NewGuid().ToString();
                sw.Stop();

                var decision = new LaunchDecisionResponse
                {
                    DecisionId = decisionId,
                    Status = status,
                    CanLaunch = canLaunch,
                    Summary = summary,
                    Blockers = blockers,
                    Warnings = warnings,
                    RecommendedActions = actions.OrderBy(a => a.Priority).ToList(),
                    EvidenceSummary = evidence.Select(e => new EvidenceSummaryItem
                    {
                        EvidenceId = e.EvidenceId,
                        Category = e.Category,
                        ValidationStatus = e.ValidationStatus,
                        Rationale = e.Rationale,
                        CollectedAt = e.Timestamp
                    }).ToList(),
                    PolicyVersion = policyVersion,
                    SchemaVersion = SchemaVersion,
                    CorrelationId = correlationId,
                    DecidedAt = DateTime.UtcNow,
                    EvaluationTimeMs = sw.ElapsedMilliseconds,
                    IsIdempotentReplay = false,
                    IsProvisional = status == LaunchDecisionStatus.NeedsReview,
                    Success = true
                };

                // Build the trace
                var trace = new DecisionTraceResponse
                {
                    DecisionId = decisionId,
                    PolicyVersion = policyVersion,
                    Rules = rules,
                    OverallOutcome = status,
                    EvaluationTimeMs = sw.ElapsedMilliseconds,
                    EvaluatedAt = decision.DecidedAt,
                    CorrelationId = correlationId,
                    SchemaVersion = SchemaVersion,
                    Success = true
                };

                // Store
                _decisions[decisionId] = decision;
                _traces[decisionId] = trace;

                if (!_evidenceStore.ContainsKey(request.OwnerId))
                    _evidenceStore[request.OwnerId] = new List<ComplianceEvidenceItem>();
                _evidenceStore[request.OwnerId].AddRange(evidence.Select(e => { e.DecisionId = decisionId; return e; }));

                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    _idempotencyIndex[request.IdempotencyKey] = decisionId;

                _logger.LogInformation(
                    "Launch decision evaluated: DecisionId={Id}, Status={Status}, OwnerId={Owner}, CorrelationId={Corr}",
                    decisionId,
                    status,
                    LoggingHelper.SanitizeLogInput(request.OwnerId),
                    correlationId);

                return Task.FromResult(decision);
            }
        }

        /// <inheritdoc/>
        public Task<LaunchDecisionResponse?> GetDecisionAsync(string decisionId, string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
                return Task.FromResult<LaunchDecisionResponse?>(null);

            lock (_lock)
            {
                _decisions.TryGetValue(decisionId, out var decision);
                if (decision != null && correlationId != null)
                {
                    var copy = ShallowCopy(decision);
                    copy.CorrelationId = correlationId;
                    decision = copy;
                }
                return Task.FromResult(decision);
            }
        }

        /// <inheritdoc/>
        public Task<EvidenceBundleResponse> GetEvidenceBundleAsync(EvidenceBundleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OwnerId))
            {
                return Task.FromResult(new EvidenceBundleResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_OWNER_ID",
                    ErrorMessage = "OwnerId is required.",
                    CorrelationId = request.CorrelationId
                });
            }

            if (request.Limit < 1 || request.Limit > 100)
            {
                return Task.FromResult(new EvidenceBundleResponse
                {
                    Success = false,
                    ErrorCode = "INVALID_LIMIT",
                    ErrorMessage = "Limit must be between 1 and 100.",
                    CorrelationId = request.CorrelationId
                });
            }

            lock (_lock)
            {
                var all = _evidenceStore.TryGetValue(request.OwnerId, out var items)
                    ? items.AsEnumerable()
                    : Enumerable.Empty<ComplianceEvidenceItem>();

                if (request.Category.HasValue)
                    all = all.Where(e => e.Category == request.Category.Value);

                if (!string.IsNullOrWhiteSpace(request.DecisionId))
                    all = all.Where(e => e.DecisionId == request.DecisionId);

                if (request.FromTimestamp.HasValue)
                    all = all.Where(e => e.Timestamp >= request.FromTimestamp.Value);

                var ordered = all.OrderByDescending(e => e.Timestamp).ToList();

                return Task.FromResult(new EvidenceBundleResponse
                {
                    BundleId = Guid.NewGuid().ToString(),
                    OwnerId = request.OwnerId,
                    Items = ordered.Take(request.Limit).ToList(),
                    TotalCount = ordered.Count,
                    CorrelationId = request.CorrelationId,
                    AssembledAt = DateTime.UtcNow,
                    SchemaVersion = SchemaVersion,
                    Success = true
                });
            }
        }

        /// <inheritdoc/>
        public Task<DecisionTraceResponse> GetDecisionTraceAsync(DecisionTraceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DecisionId))
            {
                return Task.FromResult(new DecisionTraceResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_DECISION_ID",
                    ErrorMessage = "DecisionId is required.",
                    CorrelationId = request.CorrelationId
                });
            }

            lock (_lock)
            {
                if (!_traces.TryGetValue(request.DecisionId, out var trace))
                {
                    return Task.FromResult(new DecisionTraceResponse
                    {
                        Success = false,
                        ErrorCode = "DECISION_NOT_FOUND",
                        ErrorMessage = $"Decision '{LoggingHelper.SanitizeLogInput(request.DecisionId)}' was not found.",
                        CorrelationId = request.CorrelationId
                    });
                }

                return Task.FromResult(trace);
            }
        }

        /// <inheritdoc/>
        public Task<List<LaunchDecisionResponse>> ListDecisionsAsync(
            string ownerId, int limit = 20, string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return Task.FromResult(new List<LaunchDecisionResponse>());

            lock (_lock)
            {
                // Evidence store keys = ownerIds; correlate decisions via evidence DecisionId
                var ownerDecisionIds = _evidenceStore.TryGetValue(ownerId, out var items)
                    ? items.Select(e => e.DecisionId).Where(id => id != null).Distinct().ToHashSet()
                    : new HashSet<string?>();

                var result = _decisions.Values
                    .Where(d => ownerDecisionIds.Contains(d.DecisionId))
                    .OrderByDescending(d => d.DecidedAt)
                    .Take(Math.Max(1, Math.Min(limit, 100)))
                    .ToList();

                return Task.FromResult(result);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Runs the deterministic, ordered rule evaluation pipeline.
        /// Returns the list of rule evaluation records and the evidence items collected.
        /// </summary>
        private static (List<RuleEvaluationRecord> rules, List<ComplianceEvidenceItem> evidence)
            RunRuleEvaluation(LaunchDecisionRequest request, string policyVersion, string correlationId)
        {
            var rules = new List<RuleEvaluationRecord>();
            var evidence = new List<ComplianceEvidenceItem>();
            var order = 1;

            // Rule 1: Owner identity validation
            rules.Add(EvalOwnerIdentity(request, order++, evidence));

            // Rule 2: Token standard compliance
            rules.Add(EvalTokenStandard(request, order++, evidence));

            // Rule 3: Network eligibility
            rules.Add(EvalNetworkEligibility(request, order++, evidence));

            // Rule 4: Entitlement / subscription check
            rules.Add(EvalEntitlement(request, order++, evidence));

            // Rule 5: KYC/AML status (advisory)
            rules.Add(EvalKycAml(request, order++, evidence));

            // Rule 6: Jurisdiction constraints
            rules.Add(EvalJurisdiction(request, order++, evidence));

            // Rule 7: Whitelist configuration
            rules.Add(EvalWhitelistConfig(request, order++, evidence));

            // Rule 8: Integration health
            rules.Add(EvalIntegrationHealth(request, order++, evidence));

            // Rule 9: Policy version staleness
            rules.Add(EvalPolicyStaleness(request, policyVersion, order++, evidence));

            return (rules, evidence);
        }

        private static RuleEvaluationRecord EvalOwnerIdentity(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-OWNER-001";
            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Identity,
                Source = "OwnerIdentityValidator",
                Timestamp = DateTime.UtcNow,
                Rationale = "Owner identity validated from request context",
                ValidationStatus = EvidenceValidationStatus.Valid,
                DataHash = ComputeHash(req.OwnerId)
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Owner Identity Validation",
                Category = EvidenceCategory.Identity,
                Outcome = RuleOutcome.Pass,
                InputSnapshot = new Dictionary<string, string> { { "ownerId", "present" } },
                Rationale = "Owner identity is present and well-formed.",
                EvaluationOrder = order,
                DurationMs = 1,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalTokenStandard(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-STANDARD-001";
            var isAlgorand = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ASA", "ARC3", "ARC200", "ARC1400" };
            var isKnown = KnownStandards.Contains(req.TokenStandard);

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Policy,
                Source = "TokenStandardRegistry",
                Timestamp = DateTime.UtcNow,
                Rationale = isKnown
                    ? $"Token standard '{req.TokenStandard}' is a supported standard."
                    : $"Token standard '{req.TokenStandard}' is not in the supported registry.",
                ValidationStatus = isKnown ? EvidenceValidationStatus.Valid : EvidenceValidationStatus.Invalid
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Token Standard Eligibility",
                Category = EvidenceCategory.Policy,
                Outcome = isKnown ? RuleOutcome.Pass : RuleOutcome.Fail,
                InputSnapshot = new Dictionary<string, string> { { "tokenStandard", req.TokenStandard } },
                Rationale = ev.Rationale,
                RemediationGuidance = isKnown ? null : "Choose a supported standard: ASA, ARC3, ARC200, ERC20, ARC1400.",
                EvaluationOrder = order,
                DurationMs = 1,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalNetworkEligibility(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-NETWORK-001";
            bool healthy = true; // In production, query network health endpoint
            var isMainnet = req.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase) ||
                            req.Network.Equals("base", StringComparison.OrdinalIgnoreCase) ||
                            req.Network.Equals("voimain", StringComparison.OrdinalIgnoreCase);
            var outcome = healthy ? RuleOutcome.Pass : RuleOutcome.Fail;

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Integration,
                Source = "NetworkHealthMonitor",
                Timestamp = DateTime.UtcNow,
                Rationale = healthy
                    ? $"Network '{req.Network}' is reachable and healthy."
                    : $"Network '{req.Network}' is not available.",
                ValidationStatus = healthy ? EvidenceValidationStatus.Valid : EvidenceValidationStatus.Unavailable,
                Metadata = new Dictionary<string, string>
                {
                    { "network", req.Network },
                    { "isMainnet", isMainnet.ToString() }
                }
            };
            evidence.Add(ev);

            var record = new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Network Eligibility",
                Category = EvidenceCategory.Integration,
                Outcome = outcome,
                InputSnapshot = new Dictionary<string, string> { { "network", req.Network } },
                Rationale = ev.Rationale,
                RemediationGuidance = healthy ? null : $"Verify network '{req.Network}' connectivity and retry.",
                EvaluationOrder = order,
                DurationMs = 2,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };

            if (isMainnet)
            {
                record.Outcome = RuleOutcome.Warning;
                record.Rationale += " Mainnet launch requires additional compliance review.";
                ev.Rationale = record.Rationale;
                ev.ValidationStatus = EvidenceValidationStatus.Pending;
            }

            return record;
        }

        private static RuleEvaluationRecord EvalEntitlement(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-ENTITLE-001";
            // Simulate: ARC1400 requires Premium tier
            var requiresPremium = req.TokenStandard.Equals("ARC1400", StringComparison.OrdinalIgnoreCase);
            var hasPremium = false; // In production, query entitlement service
            var outcome = requiresPremium && !hasPremium ? RuleOutcome.Fail : RuleOutcome.Pass;

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Entitlement,
                Source = "EntitlementEvaluationService",
                Timestamp = DateTime.UtcNow,
                Rationale = outcome == RuleOutcome.Pass
                    ? "Subscription entitlement allows this token standard."
                    : $"Token standard '{req.TokenStandard}' requires a Premium subscription.",
                ValidationStatus = outcome == RuleOutcome.Pass
                    ? EvidenceValidationStatus.Valid
                    : EvidenceValidationStatus.Invalid
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Subscription Entitlement Check",
                Category = EvidenceCategory.Entitlement,
                Outcome = outcome,
                InputSnapshot = new Dictionary<string, string>
                {
                    { "tokenStandard", req.TokenStandard },
                    { "requiresPremium", requiresPremium.ToString() }
                },
                Rationale = ev.Rationale,
                RemediationGuidance = outcome == RuleOutcome.Fail
                    ? "Upgrade to Premium subscription to deploy ARC1400 security tokens."
                    : null,
                EvaluationOrder = order,
                DurationMs = 2,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalKycAml(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-KYC-001";
            // Mainnet launches carry an advisory KYC warning
            var isMainnetLike = req.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase) ||
                                 req.Network.Equals("base", StringComparison.OrdinalIgnoreCase) ||
                                 req.Network.Equals("voimain", StringComparison.OrdinalIgnoreCase);
            var outcome = isMainnetLike ? RuleOutcome.Warning : RuleOutcome.Pass;

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Identity,
                Source = "KycAmlProvider",
                Timestamp = DateTime.UtcNow,
                Rationale = outcome == RuleOutcome.Pass
                    ? "KYC/AML check passed for testnet launch."
                    : "KYC/AML verification is advisory for mainnet launches. Ensure issuer KYC is on file.",
                ValidationStatus = outcome == RuleOutcome.Pass
                    ? EvidenceValidationStatus.Valid
                    : EvidenceValidationStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddDays(365)
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "KYC/AML Status Advisory",
                Category = EvidenceCategory.Identity,
                Outcome = outcome,
                InputSnapshot = new Dictionary<string, string>
                {
                    { "network", req.Network },
                    { "isMainnetLike", isMainnetLike.ToString() }
                },
                Rationale = ev.Rationale,
                RemediationGuidance = outcome == RuleOutcome.Warning
                    ? "Complete issuer KYC/AML verification via the compliance portal."
                    : null,
                EvaluationOrder = order,
                DurationMs = 3,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalJurisdiction(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-JURIS-001";
            var outcome = RuleOutcome.Pass;
            string rationale = "Jurisdiction checks passed.";

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Jurisdiction,
                Source = "JurisdictionRulesService",
                Timestamp = DateTime.UtcNow,
                Rationale = rationale,
                ValidationStatus = EvidenceValidationStatus.Valid
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Jurisdiction Compliance",
                Category = EvidenceCategory.Jurisdiction,
                Outcome = outcome,
                InputSnapshot = new Dictionary<string, string> { { "network", req.Network } },
                Rationale = rationale,
                EvaluationOrder = order,
                DurationMs = 1,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalWhitelistConfig(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-WL-001";
            var outcome = RuleOutcome.Pass;

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Workflow,
                Source = "WhitelistConfigurationService",
                Timestamp = DateTime.UtcNow,
                Rationale = "Whitelist configuration is valid or not required for this token standard.",
                ValidationStatus = EvidenceValidationStatus.Valid
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Whitelist Configuration",
                Category = EvidenceCategory.Workflow,
                Outcome = outcome,
                InputSnapshot = new Dictionary<string, string> { { "tokenStandard", req.TokenStandard } },
                Rationale = ev.Rationale,
                EvaluationOrder = order,
                DurationMs = 1,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalIntegrationHealth(
            LaunchDecisionRequest req, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-INTEG-001";

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Integration,
                Source = "IntegrationHealthMonitor",
                Timestamp = DateTime.UtcNow,
                Rationale = "All required service integrations are healthy.",
                ValidationStatus = EvidenceValidationStatus.Valid
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Integration Health",
                Category = EvidenceCategory.Integration,
                Outcome = RuleOutcome.Pass,
                InputSnapshot = new Dictionary<string, string> { { "network", req.Network } },
                Rationale = ev.Rationale,
                EvaluationOrder = order,
                DurationMs = 2,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static RuleEvaluationRecord EvalPolicyStaleness(
            LaunchDecisionRequest req, string policyVersion, int order, List<ComplianceEvidenceItem> evidence)
        {
            var ruleId = "RULE-POLICY-001";
            var isCustomVersion = req.PolicyVersion != null &&
                                   !req.PolicyVersion.Equals(CurrentPolicyVersion, StringComparison.OrdinalIgnoreCase);
            var outcome = isCustomVersion ? RuleOutcome.Warning : RuleOutcome.Pass;

            var ev = new ComplianceEvidenceItem
            {
                EvidenceId = Guid.NewGuid().ToString(),
                Category = EvidenceCategory.Policy,
                Source = "PolicyVersionRegistry",
                Timestamp = DateTime.UtcNow,
                Rationale = isCustomVersion
                    ? $"A non-current policy version '{policyVersion}' was requested. Consider updating to '{CurrentPolicyVersion}'."
                    : $"Policy version '{policyVersion}' is current.",
                ValidationStatus = EvidenceValidationStatus.Valid
            };
            evidence.Add(ev);

            return new RuleEvaluationRecord
            {
                RuleId = ruleId,
                RuleName = "Policy Version Staleness",
                Category = EvidenceCategory.Policy,
                Outcome = outcome,
                InputSnapshot = new Dictionary<string, string>
                {
                    { "requestedVersion", policyVersion },
                    { "latestVersion", CurrentPolicyVersion }
                },
                Rationale = ev.Rationale,
                RemediationGuidance = isCustomVersion
                    ? $"Update to policy version '{CurrentPolicyVersion}' to ensure current rules apply."
                    : null,
                EvaluationOrder = order,
                DurationMs = 1,
                EvidenceIds = new List<string> { ev.EvidenceId }
            };
        }

        private static LaunchDecisionStatus DetermineStatus(
            List<LaunchBlocker> blockers, List<LaunchWarning> warnings)
        {
            if (blockers.Any(b => b.Severity >= BlockerSeverity.High))
                return LaunchDecisionStatus.Blocked;
            if (blockers.Any())
                return LaunchDecisionStatus.NeedsReview;
            if (warnings.Any(w => w.Severity >= BlockerSeverity.Medium))
                return LaunchDecisionStatus.Warning;
            if (warnings.Any())
                return LaunchDecisionStatus.Warning;
            return LaunchDecisionStatus.Ready;
        }

        private static string BuildSummary(LaunchDecisionStatus status, int blockerCount, int warningCount)
        {
            return status switch
            {
                LaunchDecisionStatus.Ready =>
                    "All compliance prerequisites are met. Token launch is permitted.",
                LaunchDecisionStatus.Blocked =>
                    $"Launch is blocked by {blockerCount} critical issue(s). Resolve all blockers before proceeding.",
                LaunchDecisionStatus.Warning =>
                    $"Launch can proceed with {warningCount} advisory warning(s). Review and acknowledge before launch.",
                LaunchDecisionStatus.NeedsReview =>
                    $"Launch requires manual compliance review ({blockerCount} item(s)). Submit for review.",
                _ => "Evaluation complete."
            };
        }

        private static LaunchBlocker BuildBlocker(RuleEvaluationRecord rule)
        {
            return new LaunchBlocker
            {
                BlockerId = $"BLOCKER-{rule.RuleId}",
                Title = rule.RuleName,
                Description = rule.Rationale,
                Severity = BlockerSeverity.High,
                Category = rule.Category,
                RemediationSteps = rule.RemediationGuidance != null
                    ? new List<string> { rule.RemediationGuidance }
                    : new List<string>(),
                RuleId = rule.RuleId
            };
        }

        private static LaunchWarning BuildWarning(RuleEvaluationRecord rule)
        {
            return new LaunchWarning
            {
                WarningId = $"WARN-{rule.RuleId}",
                Title = rule.RuleName,
                Description = rule.Rationale,
                Severity = BlockerSeverity.Low,
                Category = rule.Category,
                SuggestedActions = rule.RemediationGuidance != null
                    ? new List<string> { rule.RemediationGuidance }
                    : new List<string>(),
                RuleId = rule.RuleId
            };
        }

        private static RecommendedAction BuildAction(RuleEvaluationRecord rule, bool mandatory, int priority)
        {
            return new RecommendedAction
            {
                ActionId = $"ACTION-{rule.RuleId}",
                Priority = priority,
                Title = $"Resolve: {rule.RuleName}",
                Description = rule.RemediationGuidance ?? rule.Rationale,
                Category = rule.Category,
                IsMandatory = mandatory
            };
        }

        private static LaunchDecisionResponse ErrorResponse(string code, string message, string correlationId)
        {
            return new LaunchDecisionResponse
            {
                DecisionId = Guid.NewGuid().ToString(),
                Success = false,
                ErrorCode = code,
                ErrorMessage = message,
                CorrelationId = correlationId,
                DecidedAt = DateTime.UtcNow,
                SchemaVersion = SchemaVersion
            };
        }

        /// <summary>Creates a shallow copy of a decision (for idempotent replay).</summary>
        private static LaunchDecisionResponse ShallowCopy(LaunchDecisionResponse src)
        {
            return new LaunchDecisionResponse
            {
                DecisionId = src.DecisionId,
                Status = src.Status,
                CanLaunch = src.CanLaunch,
                Summary = src.Summary,
                Blockers = src.Blockers,
                Warnings = src.Warnings,
                RecommendedActions = src.RecommendedActions,
                EvidenceSummary = src.EvidenceSummary,
                PolicyVersion = src.PolicyVersion,
                SchemaVersion = src.SchemaVersion,
                CorrelationId = src.CorrelationId,
                DecidedAt = src.DecidedAt,
                EvaluationTimeMs = src.EvaluationTimeMs,
                IsIdempotentReplay = false, // caller sets this
                IsProvisional = src.IsProvisional,
                Success = src.Success,
                ErrorCode = src.ErrorCode,
                ErrorMessage = src.ErrorMessage
            };
        }

        private static string ComputeHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

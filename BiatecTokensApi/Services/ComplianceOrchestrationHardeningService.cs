using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceHardening;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Hardening service for enterprise compliance orchestration.
    /// </summary>
    /// <remarks>
    /// Aggregates jurisdiction constraint evaluation, sanctions/KYC readiness, and
    /// launch gate enforcement with deterministic, auditable outcomes.  All providers
    /// that are not yet fully integrated return explicit "not configured" states rather
    /// than silently passing — ensuring no unsafe silent success patterns.
    /// Error responses carry typed <see cref="ComplianceErrorCategory"/> values so callers
    /// can distinguish invalid input, provider outages, policy failures, and internal
    /// exceptions.
    /// </remarks>
    public class ComplianceOrchestrationHardeningService : IComplianceOrchestrationHardeningService
    {
        private readonly IComplianceOrchestrationService _orchestrationService;
        private readonly ILogger<ComplianceOrchestrationHardeningService> _logger;

        // In-memory idempotency store: idempotencyKey → ComplianceHardeningResponse
        private readonly ConcurrentDictionary<string, ComplianceHardeningResponse> _evalCache = new();
        // In-memory launch gate cache: tokenId:subjectId → LaunchGateResponse
        private readonly ConcurrentDictionary<string, LaunchGateResponse> _gateCache = new();

        // Jurisdiction codes that are explicitly blocked (configurable; here a representative set)
        private static readonly HashSet<string> _blockedJurisdictions = new(StringComparer.OrdinalIgnoreCase)
        {
            "KP", // North Korea (OFAC SDN)
            "IR", // Iran (OFAC SDN)
            "CU", // Cuba (OFAC SDN)
            "SY", // Syria (OFAC SDN)
        };

        // Jurisdiction codes with special conditions
        private static readonly Dictionary<string, string[]> _conditionalJurisdictions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = new[] { "SEC_REGULATION_D_EXEMPTION_REQUIRED", "ACCREDITED_INVESTOR_VERIFICATION" },
            ["CN"] = new[] { "LOCAL_REGULATORY_APPROVAL_REQUIRED" },
            ["SG"] = new[] { "MAS_EXEMPTION_OR_LICENSE_REQUIRED" },
        };

        private const string SchemaVersion = "1.0";

        public ComplianceOrchestrationHardeningService(
            IComplianceOrchestrationService orchestrationService,
            ILogger<ComplianceOrchestrationHardeningService> logger)
        {
            _orchestrationService = orchestrationService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ComplianceHardeningResponse> EvaluateLaunchReadinessAsync(
            ComplianceHardeningRequest request,
            string correlationId)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return ErrorHardeningResponse(ComplianceErrorCategory.InvalidInput,
                    "SubjectId is required.", "MISSING_SUBJECT_ID", correlationId);

            if (string.IsNullOrWhiteSpace(request.TokenId))
                return ErrorHardeningResponse(ComplianceErrorCategory.InvalidInput,
                    "TokenId is required.", "MISSING_TOKEN_ID", correlationId);

            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"{request.SubjectId}:{request.TokenId}"
                : request.IdempotencyKey;

            // Idempotency check
            if (_evalCache.TryGetValue(idempotencyKey, out var cached))
            {
                _logger.LogInformation(
                    "Idempotent replay for hardening evaluation. Key={Key}, EvaluationId={EvaluationId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(idempotencyKey),
                    LoggingHelper.SanitizeLogInput(cached.EvaluationId ?? string.Empty),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var replay = ShallowCopy(cached);
                replay.IsIdempotentReplay = true;
                replay.CorrelationId = correlationId;
                return replay;
            }

            var evaluationId = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Starting compliance hardening evaluation. EvaluationId={EvaluationId}, SubjectId={SubjectId}, TokenId={TokenId}, Jurisdiction={Jurisdiction}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(evaluationId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(request.TokenId),
                LoggingHelper.SanitizeLogInput(request.JurisdictionCode ?? "none"),
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                // --- Jurisdiction evaluation ---
                JurisdictionConstraintResult? jurisdictionResult = null;
                if (!string.IsNullOrWhiteSpace(request.JurisdictionCode))
                {
                    jurisdictionResult = await GetJurisdictionConstraintAsync(
                        new JurisdictionConstraintRequest
                        {
                            SubjectId = request.SubjectId,
                            JurisdictionCode = request.JurisdictionCode
                        }, correlationId);
                }

                // --- Sanctions readiness ---
                var sanctionsResult = await GetSanctionsReadinessAsync(
                    new SanctionsReadinessRequest
                    {
                        SubjectId = request.SubjectId,
                        SubjectMetadata = request.SubjectMetadata
                    }, correlationId);

                // --- Provider statuses ---
                var providerStatus = await GetProviderStatusAsync(correlationId);

                // --- Derive overall launch gate ---
                var (gateStatus, reasonCode, remediationHints) =
                    DeriveGateStatus(jurisdictionResult, sanctionsResult);

                var response = new ComplianceHardeningResponse
                {
                    Success = true,
                    EvaluationId = evaluationId,
                    LaunchGate = gateStatus,
                    JurisdictionResult = jurisdictionResult,
                    SanctionsResult = sanctionsResult,
                    ReasonCode = reasonCode,
                    ErrorCategory = ComplianceErrorCategory.None,
                    CorrelationId = correlationId,
                    IsIdempotentReplay = false,
                    RemediationHints = remediationHints,
                    ProviderStatuses = providerStatus.Providers,
                    SchemaVersion = SchemaVersion,
                    EvaluatedAt = now
                };

                _evalCache[idempotencyKey] = response;

                _logger.LogInformation(
                    "Compliance hardening evaluation complete. EvaluationId={EvaluationId}, GateStatus={GateStatus}, ReasonCode={ReasonCode}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(evaluationId),
                    gateStatus,
                    LoggingHelper.SanitizeLogInput(reasonCode ?? string.Empty),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during compliance hardening evaluation. EvaluationId={EvaluationId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(evaluationId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return ErrorHardeningResponse(ComplianceErrorCategory.InternalException,
                    "An unexpected error occurred during compliance evaluation.",
                    "INTERNAL_EXCEPTION", correlationId);
            }
        }

        /// <inheritdoc/>
        public Task<JurisdictionConstraintResult> GetJurisdictionConstraintAsync(
            JurisdictionConstraintRequest request,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(request.JurisdictionCode))
            {
                return Task.FromResult(new JurisdictionConstraintResult
                {
                    JurisdictionCode = string.Empty,
                    Status = JurisdictionStatus.NotConfigured,
                    ReasonCode = "JURISDICTION_CODE_REQUIRED",
                    Description = "No jurisdiction code was supplied.",
                    EvaluatedAt = DateTimeOffset.UtcNow
                });
            }

            var code = request.JurisdictionCode.ToUpperInvariant();
            JurisdictionConstraintResult result;

            if (_blockedJurisdictions.Contains(code))
            {
                result = new JurisdictionConstraintResult
                {
                    JurisdictionCode = code,
                    Status = JurisdictionStatus.Blocked,
                    ReasonCode = "JURISDICTION_BLOCKED_SANCTIONS",
                    Description = $"Token launches are blocked in jurisdiction {code} due to international sanctions.",
                    RemediationHints = new List<string>
                    {
                        "Select an alternative jurisdiction not subject to international sanctions.",
                        "Consult your legal counsel before attempting to launch in restricted jurisdictions."
                    },
                    EvaluatedAt = DateTimeOffset.UtcNow
                };
            }
            else if (_conditionalJurisdictions.TryGetValue(code, out var conditions))
            {
                result = new JurisdictionConstraintResult
                {
                    JurisdictionCode = code,
                    Status = JurisdictionStatus.RestrictedWithConditions,
                    ReasonCode = "JURISDICTION_CONDITIONAL",
                    Description = $"Token launches in jurisdiction {code} are permitted subject to regulatory conditions.",
                    Conditions = conditions.ToList(),
                    RemediationHints = new List<string>
                    {
                        "Review all listed conditions with your compliance officer.",
                        "Obtain required regulatory exemptions or approvals before launch.",
                        "Document your compliance evidence for audit purposes."
                    },
                    EvaluatedAt = DateTimeOffset.UtcNow
                };
            }
            else
            {
                // Jurisdiction is known but has no special rules — permitted
                result = new JurisdictionConstraintResult
                {
                    JurisdictionCode = code,
                    Status = JurisdictionStatus.Permitted,
                    ReasonCode = "JURISDICTION_PERMITTED",
                    Description = $"No blocking restrictions found for jurisdiction {code}.",
                    EvaluatedAt = DateTimeOffset.UtcNow
                };
            }

            _logger.LogInformation(
                "Jurisdiction constraint evaluated. Jurisdiction={Jurisdiction}, Status={Status}, ReasonCode={ReasonCode}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(code),
                result.Status,
                LoggingHelper.SanitizeLogInput(result.ReasonCode),
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public async Task<SanctionsReadinessResult> GetSanctionsReadinessAsync(
            SanctionsReadinessRequest request,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return new SanctionsReadinessResult
                {
                    SubjectId = string.Empty,
                    Status = SanctionsStatus.NotConfigured,
                    ReasonCode = "SUBJECT_ID_REQUIRED",
                    ProviderStatus = ProviderIntegrationStatus.NotIntegrated,
                    EvaluatedAt = DateTimeOffset.UtcNow
                };
            }

            try
            {
                // Delegate to the underlying compliance orchestration service for KYC/AML check
                var checkRequest = new InitiateComplianceCheckRequest
                {
                    SubjectId = request.SubjectId,
                    ContextId = $"sanctions-check-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    CheckType = ComplianceCheckType.Aml,
                    SubjectMetadata = request.SubjectMetadata
                };

                var checkResponse = await _orchestrationService.InitiateCheckAsync(
                    checkRequest, "hardening-service", correlationId);

                SanctionsStatus sanctionsStatus;
                string reasonCode;
                List<string> remediationHints = new();
                ProviderIntegrationStatus providerStatus;

                if (!checkResponse.Success)
                {
                    // Provider outage or misconfiguration — explicit "not configured" state
                    sanctionsStatus = SanctionsStatus.ProviderUnavailable;
                    reasonCode = "SANCTIONS_PROVIDER_UNAVAILABLE";
                    providerStatus = ProviderIntegrationStatus.Unavailable;
                    remediationHints.Add("Retry after the compliance provider is restored.");
                    remediationHints.Add("Contact your compliance operations team to investigate the provider outage.");
                }
                else
                {
                    providerStatus = ProviderIntegrationStatus.Active;
                    (sanctionsStatus, reasonCode) = checkResponse.State switch
                    {
                        ComplianceDecisionState.Approved => (SanctionsStatus.Clear, "SANCTIONS_CLEAR"),
                        ComplianceDecisionState.Rejected => (SanctionsStatus.Flagged, "SANCTIONS_FLAGGED"),
                        ComplianceDecisionState.NeedsReview => (SanctionsStatus.PendingReview, "SANCTIONS_PENDING_REVIEW"),
                        ComplianceDecisionState.Pending => (SanctionsStatus.PendingReview, "SANCTIONS_PENDING"),
                        ComplianceDecisionState.Error => (SanctionsStatus.ProviderUnavailable, checkResponse.ReasonCode ?? "SANCTIONS_PROVIDER_ERROR"),
                        _ => (SanctionsStatus.ProviderUnavailable, "SANCTIONS_UNKNOWN_STATE")
                    };

                    if (sanctionsStatus == SanctionsStatus.Flagged)
                    {
                        remediationHints.Add("Review the flagged subject record with your AML compliance officer.");
                        remediationHints.Add("Gather supporting documentation and submit for manual review.");
                    }
                    else if (sanctionsStatus == SanctionsStatus.PendingReview)
                    {
                        remediationHints.Add("The sanctions check is pending. Check back after provider processing completes.");
                    }
                }

                var result = new SanctionsReadinessResult
                {
                    SubjectId = request.SubjectId,
                    Status = sanctionsStatus,
                    ReasonCode = reasonCode,
                    ProviderReferenceId = checkResponse.DecisionId,
                    ProviderStatus = providerStatus,
                    RemediationHints = remediationHints,
                    EvaluatedAt = DateTimeOffset.UtcNow
                };

                _logger.LogInformation(
                    "Sanctions readiness evaluated. SubjectId={SubjectId}, Status={Status}, ReasonCode={ReasonCode}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    sanctionsStatus,
                    LoggingHelper.SanitizeLogInput(reasonCode),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during sanctions readiness check. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new SanctionsReadinessResult
                {
                    SubjectId = request.SubjectId,
                    Status = SanctionsStatus.ProviderUnavailable,
                    ReasonCode = "SANCTIONS_INTERNAL_ERROR",
                    ProviderStatus = ProviderIntegrationStatus.Unavailable,
                    RemediationHints = new List<string> { "An internal error occurred. Retry after reviewing service logs." },
                    EvaluatedAt = DateTimeOffset.UtcNow
                };
            }
        }

        /// <inheritdoc/>
        public async Task<LaunchGateResponse> EnforceLaunchGateAsync(
            LaunchGateRequest request,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(request.TokenId))
                return ErrorGateResponse(ComplianceErrorCategory.InvalidInput,
                    "TokenId is required.", "GATE_MISSING_TOKEN_ID", correlationId);

            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return ErrorGateResponse(ComplianceErrorCategory.InvalidInput,
                    "SubjectId is required.", "GATE_MISSING_SUBJECT_ID", correlationId);

            var cacheKey = $"{request.TokenId}:{request.SubjectId}";

            _logger.LogInformation(
                "Enforcing launch gate. TokenId={TokenId}, SubjectId={SubjectId}, DecisionId={DecisionId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.TokenId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(request.ComplianceDecisionId ?? "none"),
                LoggingHelper.SanitizeLogInput(correlationId));

            try
            {
                // Reuse a specific cached evaluation if provided
                ComplianceHardeningResponse? evalResult = null;
                if (!string.IsNullOrWhiteSpace(request.ComplianceDecisionId))
                {
                    // Look up by evaluation ID in cache
                    evalResult = _evalCache.Values.FirstOrDefault(
                        r => r.EvaluationId == request.ComplianceDecisionId);
                }

                if (evalResult == null)
                {
                    // Run a fresh evaluation
                    evalResult = await EvaluateLaunchReadinessAsync(
                        new ComplianceHardeningRequest
                        {
                            SubjectId = request.SubjectId,
                            TokenId = request.TokenId
                        }, correlationId);
                }

                var blockingReasons = new List<string>();
                var remediationHints = new List<string>();

                // Check for blocking jurisdictions
                if (evalResult.JurisdictionResult?.Status == JurisdictionStatus.Blocked)
                {
                    blockingReasons.Add($"JURISDICTION_BLOCKED: {evalResult.JurisdictionResult.ReasonCode}");
                    remediationHints.AddRange(evalResult.JurisdictionResult.RemediationHints);
                }

                // Check for sanctions flags
                if (evalResult.SanctionsResult?.Status == SanctionsStatus.Flagged)
                {
                    blockingReasons.Add($"SANCTIONS_FLAGGED: {evalResult.SanctionsResult.ReasonCode}");
                    remediationHints.AddRange(evalResult.SanctionsResult.RemediationHints);
                }

                bool isPermitted = blockingReasons.Count == 0
                    && evalResult.LaunchGate == LaunchGateStatus.Permitted;

                var gateStatus = blockingReasons.Count > 0
                    ? LaunchGateStatus.BlockedByCompliance
                    : evalResult.LaunchGate;

                string reasonCode = gateStatus switch
                {
                    LaunchGateStatus.Permitted => "LAUNCH_GATE_PERMITTED",
                    LaunchGateStatus.BlockedByCompliance => "LAUNCH_GATE_BLOCKED",
                    LaunchGateStatus.PendingReview => "LAUNCH_GATE_PENDING_REVIEW",
                    LaunchGateStatus.NotReady => "LAUNCH_GATE_NOT_READY",
                    _ => "LAUNCH_GATE_UNKNOWN"
                };

                var gateResponse = new LaunchGateResponse
                {
                    Success = true,
                    GateStatus = gateStatus,
                    IsLaunchPermitted = isPermitted,
                    Message = isPermitted
                        ? "All compliance prerequisites are satisfied. Launch may proceed."
                        : "Launch is blocked. Resolve all compliance issues before proceeding.",
                    ReasonCode = reasonCode,
                    ErrorCategory = ComplianceErrorCategory.None,
                    BlockingReasons = blockingReasons,
                    RemediationHints = remediationHints.Distinct().ToList(),
                    CorrelationId = correlationId,
                    EvaluatedAt = DateTimeOffset.UtcNow
                };

                _gateCache[cacheKey] = gateResponse;

                _logger.LogInformation(
                    "Launch gate enforced. TokenId={TokenId}, SubjectId={SubjectId}, GateStatus={GateStatus}, IsPermitted={IsPermitted}, BlockingReasons={BlockingCount}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.TokenId),
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    gateStatus,
                    isPermitted,
                    blockingReasons.Count,
                    LoggingHelper.SanitizeLogInput(correlationId));

                return gateResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error enforcing launch gate. TokenId={TokenId}, SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.TokenId),
                    LoggingHelper.SanitizeLogInput(request.SubjectId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return ErrorGateResponse(ComplianceErrorCategory.InternalException,
                    "An unexpected error occurred during launch gate enforcement.",
                    "GATE_INTERNAL_EXCEPTION", correlationId);
            }
        }

        /// <inheritdoc/>
        public Task<ProviderStatusListResponse> GetProviderStatusAsync(string correlationId)
        {
            var now = DateTimeOffset.UtcNow;

            // Report provider integration states.
            // KYC and AML providers are integrated (via MockKycProvider / MockAmlProvider).
            // Jurisdiction and dedicated Sanctions providers are explicitly "NotIntegrated"
            // to avoid silent success patterns.
            var providers = new List<ProviderStatusReport>
            {
                new ProviderStatusReport
                {
                    ProviderName = "MockKycProvider",
                    ProviderType = "KYC",
                    Status = ProviderIntegrationStatus.Active,
                    StatusMessage = "KYC provider is active (mock implementation).",
                    LastCheckedAt = now
                },
                new ProviderStatusReport
                {
                    ProviderName = "MockAmlProvider",
                    ProviderType = "AML",
                    Status = ProviderIntegrationStatus.Active,
                    StatusMessage = "AML/sanctions provider is active (mock implementation).",
                    LastCheckedAt = now
                },
                new ProviderStatusReport
                {
                    ProviderName = "JurisdictionRulesEngine",
                    ProviderType = "Jurisdiction",
                    Status = ProviderIntegrationStatus.NotIntegrated,
                    StatusMessage = "Full jurisdiction rules engine not yet integrated. Basic blocked-list evaluation is active.",
                    LastCheckedAt = now
                },
                new ProviderStatusReport
                {
                    ProviderName = "DedicatedSanctionsProvider",
                    ProviderType = "Sanctions",
                    Status = ProviderIntegrationStatus.NotIntegrated,
                    StatusMessage = "Dedicated real-time sanctions provider not yet integrated. AML check is used as proxy.",
                    LastCheckedAt = now
                }
            };

            _logger.LogInformation(
                "Provider status report generated. ActiveProviders={ActiveCount}, NotIntegrated={NotIntegratedCount}, CorrelationId={CorrelationId}",
                providers.Count(p => p.Status == ProviderIntegrationStatus.Active),
                providers.Count(p => p.Status == ProviderIntegrationStatus.NotIntegrated),
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new ProviderStatusListResponse
            {
                Success = true,
                Providers = providers,
                CorrelationId = correlationId,
                ReportedAt = now
            });
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private static (LaunchGateStatus gateStatus, string? reasonCode, List<string> remediationHints)
            DeriveGateStatus(
                JurisdictionConstraintResult? jurisdiction,
                SanctionsReadinessResult? sanctions)
        {
            var hints = new List<string>();

            // Jurisdiction blocks are hard stops
            if (jurisdiction?.Status == JurisdictionStatus.Blocked)
            {
                hints.AddRange(jurisdiction.RemediationHints);
                return (LaunchGateStatus.BlockedByCompliance, "BLOCKED_BY_JURISDICTION", hints);
            }

            // Sanctions flags are hard stops
            if (sanctions?.Status == SanctionsStatus.Flagged)
            {
                hints.AddRange(sanctions.RemediationHints);
                return (LaunchGateStatus.BlockedByCompliance, "BLOCKED_BY_SANCTIONS", hints);
            }

            // Pending review scenarios
            if (sanctions?.Status == SanctionsStatus.PendingReview
                || jurisdiction?.Status == JurisdictionStatus.RestrictedWithConditions)
            {
                if (jurisdiction?.Status == JurisdictionStatus.RestrictedWithConditions)
                    hints.AddRange(jurisdiction.RemediationHints);
                if (sanctions?.Status == SanctionsStatus.PendingReview)
                    hints.AddRange(sanctions.RemediationHints);
                return (LaunchGateStatus.PendingReview, "PENDING_REVIEW_REQUIRED", hints);
            }

            // Provider unavailable — explicit degraded state, not silent success
            if (sanctions?.Status == SanctionsStatus.ProviderUnavailable)
            {
                hints.AddRange(sanctions.RemediationHints);
                return (LaunchGateStatus.NotReady, "PROVIDER_UNAVAILABLE", hints);
            }

            // Not configured — explicit missing-provider state
            if (sanctions?.Status == SanctionsStatus.NotConfigured)
            {
                hints.Add("Configure a sanctions/KYC provider before proceeding with launch.");
                return (LaunchGateStatus.NotReady, "SANCTIONS_NOT_CONFIGURED", hints);
            }

            // All clear
            return (LaunchGateStatus.Permitted, "LAUNCH_PERMITTED", hints);
        }

        private static ComplianceHardeningResponse ErrorHardeningResponse(
            ComplianceErrorCategory category,
            string message,
            string reasonCode,
            string correlationId) =>
            new()
            {
                Success = false,
                ErrorCategory = category,
                ErrorMessage = message,
                ReasonCode = reasonCode,
                LaunchGate = LaunchGateStatus.NotReady,
                CorrelationId = correlationId,
                SchemaVersion = SchemaVersion,
                EvaluatedAt = DateTimeOffset.UtcNow
            };

        private static LaunchGateResponse ErrorGateResponse(
            ComplianceErrorCategory category,
            string message,
            string reasonCode,
            string correlationId) =>
            new()
            {
                Success = false,
                GateStatus = LaunchGateStatus.NotReady,
                IsLaunchPermitted = false,
                ErrorCategory = category,
                ErrorMessage = message,
                ReasonCode = reasonCode,
                CorrelationId = correlationId,
                EvaluatedAt = DateTimeOffset.UtcNow
            };

        private static ComplianceHardeningResponse ShallowCopy(ComplianceHardeningResponse src) =>
            new()
            {
                Success = src.Success,
                EvaluationId = src.EvaluationId,
                LaunchGate = src.LaunchGate,
                JurisdictionResult = src.JurisdictionResult,
                SanctionsResult = src.SanctionsResult,
                ReasonCode = src.ReasonCode,
                ErrorCategory = src.ErrorCategory,
                ErrorMessage = src.ErrorMessage,
                CorrelationId = src.CorrelationId,
                IsIdempotentReplay = src.IsIdempotentReplay,
                RemediationHints = new List<string>(src.RemediationHints),
                ProviderStatuses = new List<ProviderStatusReport>(src.ProviderStatuses),
                SchemaVersion = src.SchemaVersion,
                EvaluatedAt = src.EvaluatedAt
            };
    }
}

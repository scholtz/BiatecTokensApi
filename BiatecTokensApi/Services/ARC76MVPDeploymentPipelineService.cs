using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ARC76MVPPipeline;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implements the ARC76 MVP deployment pipeline with ARC76 readiness enforcement,
    /// idempotency guarantees, compliance traceability, failure classification,
    /// and full audit-trail observability.
    /// </summary>
    public class ARC76MVPDeploymentPipelineService : IARC76MVPDeploymentPipelineService
    {
        private readonly ILogger<ARC76MVPDeploymentPipelineService> _logger;

        private readonly Dictionary<string, PipelineRecord> _store = new();
        private readonly Dictionary<string, string> _idempotencyIndex = new();
        private readonly List<PipelineAuditEntry> _auditLog = new();
        private readonly object _lock = new();

        // ── State machine ────────────────────────────────────────────────────────
        private static readonly Dictionary<PipelineStage, List<PipelineStage>> ValidTransitions = new()
        {
            { PipelineStage.PendingReadiness,    new() { PipelineStage.ReadinessVerified,   PipelineStage.Cancelled } },
            { PipelineStage.ReadinessVerified,   new() { PipelineStage.ValidationPending,   PipelineStage.Cancelled } },
            { PipelineStage.ValidationPending,   new() { PipelineStage.ValidationPassed,    PipelineStage.Failed,    PipelineStage.Cancelled } },
            { PipelineStage.ValidationPassed,    new() { PipelineStage.CompliancePending,   PipelineStage.Cancelled } },
            { PipelineStage.CompliancePending,   new() { PipelineStage.CompliancePassed,    PipelineStage.Failed,    PipelineStage.Cancelled } },
            { PipelineStage.CompliancePassed,    new() { PipelineStage.DeploymentQueued,    PipelineStage.Cancelled } },
            { PipelineStage.DeploymentQueued,    new() { PipelineStage.DeploymentActive,    PipelineStage.Cancelled } },
            { PipelineStage.DeploymentActive,    new() { PipelineStage.DeploymentConfirmed, PipelineStage.Failed,    PipelineStage.Retrying, PipelineStage.Cancelled } },
            { PipelineStage.DeploymentConfirmed, new() { PipelineStage.Completed } },
            { PipelineStage.Retrying,            new() { PipelineStage.DeploymentActive,    PipelineStage.Failed,    PipelineStage.Cancelled } },
            { PipelineStage.Completed,           new List<PipelineStage>() },
            { PipelineStage.Failed,              new List<PipelineStage>() },
            { PipelineStage.Cancelled,           new List<PipelineStage>() }
        };

        private static readonly HashSet<PipelineStage> TerminalStages = new()
        {
            PipelineStage.Completed,
            PipelineStage.Failed,
            PipelineStage.Cancelled
        };

        private static readonly HashSet<string> KnownStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ERC20", "ARC1400"
        };

        private static readonly HashSet<string> KnownNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "mainnet", "testnet", "betanet", "voimain", "base"
        };

        // ── Internal record ──────────────────────────────────────────────────────
        private sealed class PipelineRecord
        {
            public string PipelineId { get; init; } = string.Empty;
            public string? TokenName { get; init; }
            public string? TokenStandard { get; init; }
            public string? Network { get; init; }
            public string? DeployerAddress { get; init; }
            public string? DeployerEmail { get; init; }
            public string? IdempotencyKey { get; init; }
            public string? CorrelationId { get; init; }
            public int MaxRetries { get; init; }
            public int RetryCount { get; set; }
            public PipelineStage Stage { get; set; } = PipelineStage.PendingReadiness;
            public ARC76ReadinessStatus ReadinessStatus { get; set; } = ARC76ReadinessStatus.NotChecked;
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }

        /// <summary>Initialises a new instance of <see cref="ARC76MVPDeploymentPipelineService"/>.</summary>
        public ARC76MVPDeploymentPipelineService(ILogger<ARC76MVPDeploymentPipelineService> logger)
        {
            _logger = logger;
        }

        // ── InitiateAsync ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<PipelineInitiateResponse> InitiateAsync(PipelineInitiateRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                Audit("Initiate", correlationId, null, false, "INVALID_REQUEST", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("INVALID_REQUEST",
                    "Request cannot be null.",
                    "Provide a valid PipelineInitiateRequest.",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (string.IsNullOrWhiteSpace(request.TokenName))
            {
                Audit("Initiate", correlationId, null, false, "MISSING_TOKEN_NAME", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("MISSING_TOKEN_NAME",
                    "Token name is required.",
                    "Provide a non-empty token name.",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (string.IsNullOrWhiteSpace(request.TokenStandard))
            {
                Audit("Initiate", correlationId, null, false, "MISSING_TOKEN_STANDARD", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("MISSING_TOKEN_STANDARD",
                    "Token standard is required.",
                    "Provide a token standard such as ASA, ARC3, ARC200, ERC20, or ARC1400.",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (!KnownStandards.Contains(request.TokenStandard!))
            {
                Audit("Initiate", correlationId, null, false, "UNSUPPORTED_TOKEN_STANDARD", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("UNSUPPORTED_TOKEN_STANDARD",
                    $"Token standard '{request.TokenStandard}' is not supported.",
                    "Supported standards: ASA, ARC3, ARC200, ERC20, ARC1400.",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (string.IsNullOrWhiteSpace(request.Network))
            {
                Audit("Initiate", correlationId, null, false, "MISSING_NETWORK", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("MISSING_NETWORK",
                    "Network is required.",
                    "Specify the target network (e.g. mainnet, testnet, base).",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (!KnownNetworks.Contains(request.Network!))
            {
                Audit("Initiate", correlationId, null, false, "UNSUPPORTED_NETWORK", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("UNSUPPORTED_NETWORK",
                    $"Network '{request.Network}' is not supported.",
                    "Supported networks: mainnet, testnet, betanet, voimain, base.",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (string.IsNullOrWhiteSpace(request.DeployerAddress))
            {
                Audit("Initiate", correlationId, null, false, "MISSING_DEPLOYER_ADDRESS", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("MISSING_DEPLOYER_ADDRESS",
                    "Deployer address is required.",
                    "Provide the ARC76-derived wallet address that will sign the deployment transaction.",
                    correlationId, FailureCategory.UserCorrectable));
            }

            if (request.MaxRetries < 0)
            {
                Audit("Initiate", correlationId, null, false, "INVALID_MAX_RETRIES", null, FailureCategory.UserCorrectable);
                return Task.FromResult(ErrInit("INVALID_MAX_RETRIES",
                    "MaxRetries cannot be negative.",
                    "Set MaxRetries to a non-negative integer (default: 3).",
                    correlationId, FailureCategory.UserCorrectable));
            }

            lock (_lock)
            {
                // Idempotency check
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                    _idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingId) &&
                    _store.TryGetValue(existingId, out var existing))
                {
                    if (!string.Equals(existing.TokenName, request.TokenName, StringComparison.Ordinal) ||
                        !string.Equals(existing.Network, request.Network, StringComparison.Ordinal) ||
                        !string.Equals(existing.DeployerAddress, request.DeployerAddress, StringComparison.Ordinal) ||
                        !string.Equals(existing.TokenStandard, request.TokenStandard, StringComparison.Ordinal))
                    {
                        Audit("Initiate", correlationId, existingId, false, "IDEMPOTENCY_KEY_CONFLICT", null, FailureCategory.UserCorrectable);
                        return Task.FromResult(ErrInit("IDEMPOTENCY_KEY_CONFLICT",
                            "Idempotency key already used with different parameters.",
                            "Use a new idempotency key for a different pipeline.",
                            correlationId, FailureCategory.UserCorrectable));
                    }

                    _logger.LogInformation("IdempotentReplay pipelineId={Id} correlationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(existingId),
                        LoggingHelper.SanitizeLogInput(correlationId));
                    Audit("Initiate", correlationId, existingId, true, null, existing.Stage, FailureCategory.None);
                    var replay = MapToInitiateResponse(existing);
                    replay.IsIdempotentReplay = true;
                    replay.CorrelationId = correlationId;
                    return Task.FromResult(replay);
                }

                var pipelineId = Guid.NewGuid().ToString();
                var record = new PipelineRecord
                {
                    PipelineId = pipelineId,
                    TokenName = request.TokenName,
                    TokenStandard = request.TokenStandard,
                    Network = request.Network,
                    DeployerAddress = request.DeployerAddress,
                    DeployerEmail = request.DeployerEmail,
                    IdempotencyKey = request.IdempotencyKey,
                    CorrelationId = correlationId,
                    MaxRetries = request.MaxRetries,
                    Stage = PipelineStage.PendingReadiness,
                    ReadinessStatus = ARC76ReadinessStatus.NotChecked
                };

                _store[pipelineId] = record;
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    _idempotencyIndex[request.IdempotencyKey] = pipelineId;

                _logger.LogInformation(
                    "Pipeline initiated pipelineId={Id} tokenName={Name} network={Network} correlationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(pipelineId),
                    LoggingHelper.SanitizeLogInput(request.TokenName),
                    LoggingHelper.SanitizeLogInput(request.Network),
                    LoggingHelper.SanitizeLogInput(correlationId));

                Audit("Initiate", correlationId, pipelineId, true, null, PipelineStage.PendingReadiness, FailureCategory.None);
                var resp = MapToInitiateResponse(record);
                resp.NextAction = "Advance to ReadinessVerified to confirm ARC76 account readiness.";
                return Task.FromResult(resp);
            }
        }

        // ── GetStatusAsync ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<PipelineStatusResponse?> GetStatusAsync(string pipelineId, string? correlationId)
        {
            lock (_lock)
            {
                if (!_store.TryGetValue(pipelineId, out var record))
                    return Task.FromResult<PipelineStatusResponse?>(null);

                var resp = new PipelineStatusResponse
                {
                    Success = true,
                    PipelineId = record.PipelineId,
                    TokenName = record.TokenName,
                    TokenStandard = record.TokenStandard,
                    Network = record.Network,
                    DeployerAddress = record.DeployerAddress,
                    Stage = record.Stage,
                    ReadinessStatus = record.ReadinessStatus,
                    FailureCategory = FailureCategory.None,
                    RetryCount = record.RetryCount,
                    MaxRetries = record.MaxRetries,
                    NextAction = NextActionFor(record.Stage),
                    CorrelationId = correlationId ?? record.CorrelationId,
                    CreatedAt = record.CreatedAt,
                    UpdatedAt = record.UpdatedAt
                };
                return Task.FromResult<PipelineStatusResponse?>(resp);
            }
        }

        // ── AdvanceAsync ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<PipelineAdvanceResponse> AdvanceAsync(PipelineAdvanceRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request?.PipelineId))
            {
                Audit("Advance", correlationId, null, false, "MISSING_PIPELINE_ID", null, FailureCategory.UserCorrectable);
                return Task.FromResult(new PipelineAdvanceResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_PIPELINE_ID",
                    ErrorMessage = "PipelineId is required.",
                    RemediationHint = "Provide the pipeline ID returned when the pipeline was initiated.",
                    CorrelationId = correlationId
                });
            }

            lock (_lock)
            {
                if (!_store.TryGetValue(request.PipelineId!, out var record))
                {
                    Audit("Advance", correlationId, request.PipelineId, false, "PIPELINE_NOT_FOUND", null, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineAdvanceResponse
                    {
                        Success = false,
                        PipelineId = request.PipelineId,
                        ErrorCode = "PIPELINE_NOT_FOUND",
                        ErrorMessage = $"Pipeline '{request.PipelineId}' was not found.",
                        RemediationHint = "Check the pipeline ID and try again.",
                        CorrelationId = correlationId
                    });
                }

                var currentStage = record.Stage;

                if (TerminalStages.Contains(currentStage))
                {
                    Audit("Advance", correlationId, record.PipelineId, false, "TERMINAL_STAGE", currentStage, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineAdvanceResponse
                    {
                        Success = false,
                        PipelineId = record.PipelineId,
                        PreviousStage = currentStage,
                        CurrentStage = currentStage,
                        ErrorCode = "TERMINAL_STAGE",
                        ErrorMessage = $"Pipeline is in terminal stage '{currentStage}' and cannot be advanced.",
                        RemediationHint = "Terminal pipelines cannot be advanced. Start a new pipeline if needed.",
                        CorrelationId = correlationId
                    });
                }

                var allowed = ValidTransitions[currentStage];
                var nextStage = allowed.FirstOrDefault(s =>
                    s != PipelineStage.Cancelled &&
                    s != PipelineStage.Failed &&
                    s != PipelineStage.Retrying);

                if (nextStage == default)
                {
                    Audit("Advance", correlationId, record.PipelineId, false, "NO_FORWARD_TRANSITION", currentStage, FailureCategory.SystemCritical);
                    return Task.FromResult(new PipelineAdvanceResponse
                    {
                        Success = false,
                        PipelineId = record.PipelineId,
                        PreviousStage = currentStage,
                        CurrentStage = currentStage,
                        ErrorCode = "NO_FORWARD_TRANSITION",
                        ErrorMessage = $"No forward transition available from '{currentStage}'.",
                        RemediationHint = "Review the current stage before advancing.",
                        CorrelationId = correlationId
                    });
                }

                // When advancing from PendingReadiness to ReadinessVerified, mark ARC76 as ready
                if (currentStage == PipelineStage.PendingReadiness && nextStage == PipelineStage.ReadinessVerified)
                {
                    record.ReadinessStatus = ARC76ReadinessStatus.Ready;
                }

                record.Stage = nextStage;
                record.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Pipeline advanced pipelineId={Id} from={From} to={To} correlationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(record.PipelineId),
                    currentStage, nextStage,
                    LoggingHelper.SanitizeLogInput(correlationId));

                Audit("Advance", correlationId, record.PipelineId, true, null, nextStage, FailureCategory.None);

                return Task.FromResult(new PipelineAdvanceResponse
                {
                    Success = true,
                    PipelineId = record.PipelineId,
                    PreviousStage = currentStage,
                    CurrentStage = nextStage,
                    NextAction = NextActionFor(nextStage),
                    CorrelationId = correlationId
                });
            }
        }

        // ── CancelAsync ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<PipelineCancelResponse> CancelAsync(PipelineCancelRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request?.PipelineId))
            {
                Audit("Cancel", correlationId, null, false, "MISSING_PIPELINE_ID", null, FailureCategory.UserCorrectable);
                return Task.FromResult(new PipelineCancelResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_PIPELINE_ID",
                    ErrorMessage = "PipelineId is required.",
                    RemediationHint = "Provide the pipeline ID.",
                    CorrelationId = correlationId
                });
            }

            lock (_lock)
            {
                if (!_store.TryGetValue(request.PipelineId!, out var record))
                {
                    Audit("Cancel", correlationId, request.PipelineId, false, "PIPELINE_NOT_FOUND", null, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineCancelResponse
                    {
                        Success = false,
                        PipelineId = request.PipelineId,
                        ErrorCode = "PIPELINE_NOT_FOUND",
                        ErrorMessage = $"Pipeline '{request.PipelineId}' was not found.",
                        RemediationHint = "Check the pipeline ID.",
                        CorrelationId = correlationId
                    });
                }

                var currentStage = record.Stage;
                if (!ValidTransitions[currentStage].Contains(PipelineStage.Cancelled))
                {
                    Audit("Cancel", correlationId, record.PipelineId, false, "CANNOT_CANCEL", currentStage, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineCancelResponse
                    {
                        Success = false,
                        PipelineId = record.PipelineId,
                        PreviousStage = currentStage,
                        ErrorCode = "CANNOT_CANCEL",
                        ErrorMessage = $"Pipeline in stage '{currentStage}' cannot be cancelled.",
                        RemediationHint = "Only non-terminal pipelines can be cancelled.",
                        CorrelationId = correlationId
                    });
                }

                record.Stage = PipelineStage.Cancelled;
                record.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Pipeline cancelled pipelineId={Id} reason={Reason} correlationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(record.PipelineId),
                    LoggingHelper.SanitizeLogInput(request.Reason ?? "not specified"),
                    LoggingHelper.SanitizeLogInput(correlationId));

                Audit("Cancel", correlationId, record.PipelineId, true, null, PipelineStage.Cancelled, FailureCategory.None);

                return Task.FromResult(new PipelineCancelResponse
                {
                    Success = true,
                    PipelineId = record.PipelineId,
                    PreviousStage = currentStage,
                    CorrelationId = correlationId
                });
            }
        }

        // ── RetryAsync ───────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<PipelineRetryResponse> RetryAsync(PipelineRetryRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(request?.PipelineId))
            {
                Audit("Retry", correlationId, null, false, "MISSING_PIPELINE_ID", null, FailureCategory.UserCorrectable);
                return Task.FromResult(new PipelineRetryResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_PIPELINE_ID",
                    ErrorMessage = "PipelineId is required.",
                    RemediationHint = "Provide the pipeline ID.",
                    CorrelationId = correlationId
                });
            }

            lock (_lock)
            {
                if (!_store.TryGetValue(request.PipelineId!, out var record))
                {
                    Audit("Retry", correlationId, request.PipelineId, false, "PIPELINE_NOT_FOUND", null, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineRetryResponse
                    {
                        Success = false,
                        PipelineId = request.PipelineId,
                        ErrorCode = "PIPELINE_NOT_FOUND",
                        ErrorMessage = $"Pipeline '{request.PipelineId}' was not found.",
                        RemediationHint = "Check the pipeline ID.",
                        CorrelationId = correlationId
                    });
                }

                if (record.Stage != PipelineStage.Failed)
                {
                    Audit("Retry", correlationId, record.PipelineId, false, "NOT_IN_FAILED_STATE", record.Stage, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineRetryResponse
                    {
                        Success = false,
                        PipelineId = record.PipelineId,
                        Stage = record.Stage,
                        ErrorCode = "NOT_IN_FAILED_STATE",
                        ErrorMessage = $"Pipeline is in stage '{record.Stage}' and cannot be retried. Only Failed pipelines can be retried.",
                        RemediationHint = "Only pipelines in the Failed state can be retried.",
                        CorrelationId = correlationId
                    });
                }

                if (record.RetryCount >= record.MaxRetries)
                {
                    Audit("Retry", correlationId, record.PipelineId, false, "RETRY_LIMIT_EXCEEDED", record.Stage, FailureCategory.UserCorrectable);
                    return Task.FromResult(new PipelineRetryResponse
                    {
                        Success = false,
                        PipelineId = record.PipelineId,
                        Stage = record.Stage,
                        RetryCount = record.RetryCount,
                        ErrorCode = "RETRY_LIMIT_EXCEEDED",
                        ErrorMessage = $"Retry limit of {record.MaxRetries} has been reached.",
                        RemediationHint = "Start a new pipeline with a higher MaxRetries value or investigate the root cause.",
                        CorrelationId = correlationId
                    });
                }

                record.RetryCount++;
                record.Stage = PipelineStage.Retrying;
                record.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Pipeline retrying pipelineId={Id} retryCount={Count} correlationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(record.PipelineId),
                    record.RetryCount,
                    LoggingHelper.SanitizeLogInput(correlationId));

                Audit("Retry", correlationId, record.PipelineId, true, null, PipelineStage.Retrying, FailureCategory.None);

                return Task.FromResult(new PipelineRetryResponse
                {
                    Success = true,
                    PipelineId = record.PipelineId,
                    Stage = PipelineStage.Retrying,
                    RetryCount = record.RetryCount,
                    CorrelationId = correlationId
                });
            }
        }

        // ── GetAuditAsync ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<PipelineAuditResponse> GetAuditAsync(string pipelineId, string? correlationId)
        {
            lock (_lock)
            {
                var events = _auditLog
                    .Where(e => string.Equals(e.PipelineId, pipelineId, StringComparison.Ordinal))
                    .ToList();

                return Task.FromResult(new PipelineAuditResponse
                {
                    Success = true,
                    PipelineId = pipelineId,
                    Events = events,
                    CorrelationId = correlationId
                });
            }
        }

        // ── GetAuditEvents ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public IReadOnlyList<PipelineAuditEntry> GetAuditEvents(string? pipelineId = null, string? correlationId = null)
        {
            lock (_lock)
            {
                return _auditLog
                    .Where(e =>
                        (pipelineId == null || string.Equals(e.PipelineId, pipelineId, StringComparison.Ordinal)) &&
                        (correlationId == null || string.Equals(e.CorrelationId, correlationId, StringComparison.Ordinal)))
                    .ToList();
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private PipelineInitiateResponse MapToInitiateResponse(PipelineRecord r) => new()
        {
            Success = true,
            PipelineId = r.PipelineId,
            Stage = r.Stage,
            ReadinessStatus = r.ReadinessStatus,
            FailureCategory = FailureCategory.None,
            CorrelationId = r.CorrelationId,
            CreatedAt = r.CreatedAt,
            NextAction = NextActionFor(r.Stage)
        };

        private static PipelineInitiateResponse ErrInit(
            string code, string message, string hint, string? correlationId, FailureCategory category) =>
            new()
            {
                Success = false,
                ErrorCode = code,
                ErrorMessage = message,
                RemediationHint = hint,
                CorrelationId = correlationId,
                FailureCategory = category
            };

        private void Audit(
            string operation,
            string? correlationId,
            string? pipelineId,
            bool succeeded,
            string? errorCode,
            PipelineStage? stage,
            FailureCategory category)
        {
            _auditLog.Add(new PipelineAuditEntry
            {
                EventId = Guid.NewGuid().ToString(),
                PipelineId = pipelineId,
                CorrelationId = correlationId,
                Operation = operation,
                Succeeded = succeeded,
                ErrorCode = errorCode,
                Stage = stage,
                FailureCategory = category,
                Timestamp = DateTime.UtcNow
            });
        }

        private static string NextActionFor(PipelineStage stage) => stage switch
        {
            PipelineStage.PendingReadiness    => "Advance to ReadinessVerified to confirm ARC76 account readiness.",
            PipelineStage.ReadinessVerified   => "Advance to ValidationPending to begin input validation.",
            PipelineStage.ValidationPending   => "Advance to ValidationPassed after validating all inputs.",
            PipelineStage.ValidationPassed    => "Advance to CompliancePending to begin compliance checks.",
            PipelineStage.CompliancePending   => "Advance to CompliancePassed after compliance checks complete.",
            PipelineStage.CompliancePassed    => "Advance to DeploymentQueued to queue for blockchain deployment.",
            PipelineStage.DeploymentQueued    => "Advance to DeploymentActive when capacity is available.",
            PipelineStage.DeploymentActive    => "Monitor progress; advance to DeploymentConfirmed on success.",
            PipelineStage.DeploymentConfirmed => "Advance to Completed after post-deployment verification.",
            PipelineStage.Completed           => "Pipeline complete. No further actions required.",
            PipelineStage.Failed              => "Review audit trail and retry or start a new pipeline.",
            PipelineStage.Cancelled           => "Pipeline cancelled. Start a new pipeline if needed.",
            PipelineStage.Retrying            => "System will retry the deployment automatically.",
            _                                 => string.Empty
        };
    }
}

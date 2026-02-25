using BiatecTokensApi.Models;
using BiatecTokensApi.Models.OperationalIntelligence;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Deterministic operational intelligence and audit evidence service.
    ///
    /// All read operations are idempotent – repeated calls with the same inputs
    /// return semantically identical responses, supporting safe retries and
    /// stable contract behaviour for frontend and enterprise consumers.
    ///
    /// Internal implementation details and stack traces are never surfaced in
    /// public-facing payloads; only bounded error codes and remediation hints
    /// are exposed.
    /// </summary>
    public class OperationalIntelligenceService : IOperationalIntelligenceService
    {
        private readonly IDeploymentStatusService _deploymentStatusService;
        private readonly ILogger<OperationalIntelligenceService> _logger;

        // ─────────────────────────────────────────────────────────────────────
        // Deterministic risk classification table.
        // Maps stable error-code prefixes to bounded OperationalRiskCategory values.
        // Adding a new prefix here is the ONLY place the mapping needs to change.
        // ─────────────────────────────────────────────────────────────────────
        private static readonly (string Prefix, OperationalRiskCategory Category, OperationSeverity Severity, string Hint)[] RiskTable =
        [
            (ErrorCodes.UNAUTHORIZED,          OperationalRiskCategory.AuthorizationRisk, OperationSeverity.Error,    "Re-authenticate and retry."),
            (ErrorCodes.FORBIDDEN,             OperationalRiskCategory.AuthorizationRisk, OperationSeverity.Error,    "Verify permissions for the requested operation."),
            (ErrorCodes.INVALID_AUTH_TOKEN,    OperationalRiskCategory.AuthorizationRisk, OperationSeverity.Error,    "Refresh your authentication token and retry."),
            (ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR, OperationalRiskCategory.NetworkRisk, OperationSeverity.Warning,  "Check network connectivity; retry with exponential back-off."),
            (ErrorCodes.TIMEOUT,               OperationalRiskCategory.InfrastructureRisk, OperationSeverity.Warning, "Transient timeout – retry after a short delay."),
            (ErrorCodes.EXTERNAL_SERVICE_ERROR,OperationalRiskCategory.InfrastructureRisk, OperationSeverity.Warning, "Upstream service degraded – retry later."),
            (ErrorCodes.IPFS_SERVICE_ERROR,    OperationalRiskCategory.InfrastructureRisk, OperationSeverity.Warning, "IPFS service unavailable – retry after a short delay."),
            (ErrorCodes.INVALID_REQUEST,       OperationalRiskCategory.DataIntegrityRisk, OperationSeverity.Error,    "Correct the request parameters and resubmit."),
            (ErrorCodes.MISSING_REQUIRED_FIELD,OperationalRiskCategory.DataIntegrityRisk, OperationSeverity.Error,    "Supply all required fields and resubmit."),
            (ErrorCodes.INVALID_TOKEN_PARAMETERS, OperationalRiskCategory.DataIntegrityRisk, OperationSeverity.Error, "Correct the token parameters and resubmit."),
            (ErrorCodes.INVALID_NETWORK,       OperationalRiskCategory.NetworkRisk,       OperationSeverity.Error,    "Specify a supported network identifier."),
            (ErrorCodes.CONFLICT,              OperationalRiskCategory.PolicyRisk,        OperationSeverity.Warning,  "Resolve the conflicting operation before retrying."),
            (ErrorCodes.ALREADY_EXISTS,        OperationalRiskCategory.PolicyRisk,        OperationSeverity.Info,     "Resource already exists – idempotent re-submission is safe."),
            (ErrorCodes.NOT_FOUND,             OperationalRiskCategory.DataIntegrityRisk, OperationSeverity.Warning,  "Verify the resource identifier and retry."),
            (ErrorCodes.INTERNAL_SERVER_ERROR, OperationalRiskCategory.InfrastructureRisk, OperationSeverity.Error,   "Contact support if this error persists."),
        ];

        public OperationalIntelligenceService(
            IDeploymentStatusService deploymentStatusService,
            ILogger<OperationalIntelligenceService> logger)
        {
            _deploymentStatusService = deploymentStatusService;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Timeline
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<OperationTimelineResponse> GetOperationTimelineAsync(OperationTimelineRequest request)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            if (string.IsNullOrWhiteSpace(request.DeploymentId))
            {
                return FailTimeline(request.DeploymentId, correlationId,
                    ErrorCodes.MISSING_REQUIRED_FIELD,
                    "DeploymentId is required.",
                    "Provide a valid deployment identifier.");
            }

            var limit = Math.Clamp(request.Limit, 1, 200);

            try
            {
                // Check deployment existence first (history returns empty list for unknown IDs).
                var deployment = await _deploymentStatusService.GetDeploymentAsync(request.DeploymentId);
                if (deployment == null)
                {
                    return FailTimeline(request.DeploymentId, correlationId,
                        ErrorCodes.NOT_FOUND,
                        $"No deployment found with ID '{request.DeploymentId}'.",
                        "Verify the deployment ID and retry.");
                }

                var history = await _deploymentStatusService.GetStatusHistoryAsync(request.DeploymentId);

                // Convert to timeline entries (oldest first – deterministic ordering).
                var allEntries = history
                    .OrderBy(e => e.Timestamp)
                    .Select((e, i) => new OperationTimelineEntry
                    {
                        EntryId      = e.Id,
                        CorrelationId = correlationId,
                        OccurredAt   = e.Timestamp,
                        FromState    = i == 0 ? null : history.OrderBy(x => x.Timestamp).ElementAt(i - 1).Status.ToString(),
                        ToState      = e.Status.ToString(),
                        Severity     = e.Status == DeploymentStatus.Failed ? OperationSeverity.Error : OperationSeverity.Info,
                        Description  = BuildTransitionDescription(e.Status, e.Message),
                        RecommendedAction = e.Status == DeploymentStatus.Failed ? "Review the error details and retry or contact support." : null,
                        EventCode    = e.ReasonCode ?? e.Status.ToString().ToUpperInvariant(),
                        Actor        = e.ActorAddress ?? "system",
                        Metadata     = BuildSafeMetadata(e)
                    })
                    .ToList();

                // Cursor-based pagination.
                var startIndex = 0;
                if (!string.IsNullOrWhiteSpace(request.AfterEntryId))
                {
                    var idx = allEntries.FindIndex(e => e.EntryId == request.AfterEntryId);
                    if (idx >= 0) startIndex = idx + 1;
                }

                var page = allEntries.Skip(startIndex).Take(limit).ToList();
                var hasMore = startIndex + limit < allEntries.Count;

                return new OperationTimelineResponse
                {
                    Success      = true,
                    DeploymentId = request.DeploymentId,
                    Entries      = page,
                    TotalEntries = allEntries.Count,
                    HasMore      = hasMore,
                    NextCursor   = hasMore ? page.Last().EntryId : null,
                    GeneratedAt  = DateTime.UtcNow,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building operation timeline for deployment {DeploymentId}", request.DeploymentId);
                return FailTimeline(request.DeploymentId, correlationId,
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    "An unexpected error occurred while retrieving the operation timeline.",
                    "Retry the request. Contact support if the error persists.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Compliance Checkpoints
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ComplianceCheckpointResponse> GetComplianceCheckpointsAsync(ComplianceCheckpointRequest request)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            if (string.IsNullOrWhiteSpace(request.DeploymentId))
            {
                return FailCheckpoints(request.DeploymentId, correlationId,
                    ErrorCodes.MISSING_REQUIRED_FIELD,
                    "DeploymentId is required.",
                    "Provide a valid deployment identifier.");
            }

            try
            {
                var deployment = await _deploymentStatusService.GetDeploymentAsync(request.DeploymentId);

                if (deployment == null)
                {
                    return FailCheckpoints(request.DeploymentId, correlationId,
                        ErrorCodes.NOT_FOUND,
                        $"No deployment found with ID '{request.DeploymentId}'.",
                        "Verify the deployment ID and retry.");
                }

                var checkpoints = BuildComplianceCheckpoints(deployment, correlationId);

                if (!request.IncludeNonBlocking)
                {
                    checkpoints = checkpoints.Where(c => c.IsBlocking).ToList();
                }

                var blocking   = checkpoints.Count(c => c.IsBlocking && c.State != ComplianceCheckpointState.Satisfied);
                var satisfied  = checkpoints.Count(c => c.State == ComplianceCheckpointState.Satisfied);
                var posture    = DerivePosture(blocking, satisfied, checkpoints.Count);

                return new ComplianceCheckpointResponse
                {
                    Success       = true,
                    DeploymentId  = request.DeploymentId,
                    Checkpoints   = checkpoints,
                    BlockingCount = blocking,
                    SatisfiedCount = satisfied,
                    OverallPosture = posture,
                    GeneratedAt   = DateTime.UtcNow,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building compliance checkpoints for deployment {DeploymentId}", request.DeploymentId);
                return FailCheckpoints(request.DeploymentId, correlationId,
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    "An unexpected error occurred while retrieving compliance checkpoints.",
                    "Retry the request. Contact support if the error persists.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Stakeholder Report
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<StakeholderReportResponse> GetStakeholderReportAsync(StakeholderReportRequest request)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            if (string.IsNullOrWhiteSpace(request.DeploymentId))
            {
                return new StakeholderReportResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "DeploymentId is required.",
                    RemediationHint = "Provide a valid deployment identifier."
                };
            }

            try
            {
                var deployment = await _deploymentStatusService.GetDeploymentAsync(request.DeploymentId);

                if (deployment == null)
                {
                    return new StakeholderReportResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.NOT_FOUND,
                        ErrorMessage = $"No deployment found with ID '{request.DeploymentId}'.",
                        RemediationHint = "Verify the deployment ID and retry."
                    };
                }

                // Derive risk signals from current deployment state (no internal details).
                var riskSignals = new List<OperationalRiskSignal>();
                if (deployment.CurrentStatus == DeploymentStatus.Failed)
                {
                    riskSignals.Add(new OperationalRiskSignal
                    {
                        Category   = OperationalRiskCategory.InfrastructureRisk,
                        Severity   = OperationSeverity.Error,
                        Confidence = ConfidenceLevel.High,
                        SignalCode = "DEPLOYMENT_FAILED",
                        Description = "Deployment failed and requires attention.",
                        RemediationHint = "Review deployment details and retry or contact support.",
                        CorrelationId = correlationId
                    });
                }

                var posture = deployment.CurrentStatus == DeploymentStatus.Completed
                    ? "All checkpoints satisfied"
                    : deployment.CurrentStatus == DeploymentStatus.Failed
                        ? "Action required – deployment failed"
                        : "In progress";

                var progress = BuildProgressSummary(deployment.CurrentStatus);
                var primaryAction = riskSignals.Count > 0 ? riskSignals[0].RemediationHint : null;

                return new StakeholderReportResponse
                {
                    Success = true,
                    Report = new StakeholderReportPayload
                    {
                        DeploymentId    = request.DeploymentId,
                        TokenName       = deployment.TokenName,
                        TokenSymbol     = deployment.TokenSymbol,
                        IssuanceProgress = progress,
                        CompliancePosture = posture,
                        UnresolvedBlockers = riskSignals.Count(s => s.Severity >= OperationSeverity.Error),
                        PrimaryRecommendedAction = primaryAction,
                        CurrentState    = deployment.CurrentStatus.ToString(),
                        GeneratedAt     = DateTime.UtcNow,
                        CorrelationId   = correlationId,
                        RiskSignals     = riskSignals
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building stakeholder report for deployment {DeploymentId}", request.DeploymentId);
                return new StakeholderReportResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while generating the stakeholder report.",
                    RemediationHint = "Retry the request. Contact support if the error persists."
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Risk Classification
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public OperationalRiskSignal ClassifyRisk(string errorCode, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return new OperationalRiskSignal
                {
                    Category   = OperationalRiskCategory.None,
                    Severity   = OperationSeverity.Info,
                    Confidence = ConfidenceLevel.Definitive,
                    SignalCode = "NO_ERROR",
                    Description = "No error detected.",
                    RemediationHint = string.Empty,
                    CorrelationId = correlationId
                };
            }

            foreach (var (prefix, category, severity, hint) in RiskTable)
            {
                if (errorCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return new OperationalRiskSignal
                    {
                        Category   = category,
                        Severity   = severity,
                        Confidence = ConfidenceLevel.Definitive,
                        SignalCode = errorCode.ToUpperInvariant(),
                        Description = $"Operational risk detected: {category}.",
                        RemediationHint = hint,
                        CorrelationId = correlationId
                    };
                }
            }

            // Unknown error code – conservative classification.
            return new OperationalRiskSignal
            {
                Category   = OperationalRiskCategory.InfrastructureRisk,
                Severity   = OperationSeverity.Warning,
                Confidence = ConfidenceLevel.Low,
                SignalCode = errorCode.ToUpperInvariant(),
                Description = "Unclassified operational signal.",
                RemediationHint = "Contact support with the signal code for guidance.",
                CorrelationId = correlationId
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildTransitionDescription(DeploymentStatus status, string? message)
        {
            return status switch
            {
                DeploymentStatus.Queued     => "Deployment request received and queued for processing.",
                DeploymentStatus.Submitted  => "Deployment transaction submitted to the blockchain network.",
                DeploymentStatus.Pending    => "Transaction is pending confirmation on the blockchain.",
                DeploymentStatus.Confirmed  => "Transaction confirmed and included in a block.",
                DeploymentStatus.Indexed    => "Transaction indexed by blockchain explorers.",
                DeploymentStatus.Completed  => "Deployment completed successfully.",
                DeploymentStatus.Failed     => $"Deployment failed. {message ?? ""}".TrimEnd(),
                DeploymentStatus.Cancelled  => "Deployment was cancelled by the user.",
                _                           => message ?? status.ToString()
            };
        }

        private static Dictionary<string, string> BuildSafeMetadata(BiatecTokensApi.Models.DeploymentStatusEntry e)
        {
            var meta = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(e.TransactionHash))
                meta["transactionHash"] = e.TransactionHash;
            if (e.ConfirmedRound.HasValue)
                meta["confirmedRound"] = e.ConfirmedRound.Value.ToString();
            return meta;
        }

        private static List<ComplianceCheckpoint> BuildComplianceCheckpoints(
            BiatecTokensApi.Models.TokenDeployment deployment, string correlationId)
        {
            var checkpoints = new List<ComplianceCheckpoint>
            {
                new()
                {
                    CheckpointId = "deployment-initiated",
                    Name         = "Deployment Initiated",
                    Category     = "Lifecycle",
                    IsBlocking   = true,
                    CorrelationId = correlationId,
                    State        = ComplianceCheckpointState.Satisfied,
                    Explanation  = "Deployment record created and tracking initiated.",
                    LastUpdatedAt = deployment.CreatedAt
                },
                new()
                {
                    CheckpointId  = "network-submission",
                    Name          = "Network Submission",
                    Category      = "Blockchain",
                    IsBlocking    = true,
                    CorrelationId = correlationId,
                    State         = StateForStatus(deployment.CurrentStatus,
                        requiredStatuses: [DeploymentStatus.Submitted, DeploymentStatus.Pending, DeploymentStatus.Confirmed, DeploymentStatus.Indexed, DeploymentStatus.Completed]),
                    Explanation   = "Transaction submitted to the blockchain network.",
                    RecommendedAction = "Wait for submission or retry if stuck in Queued state.",
                    LastUpdatedAt = deployment.UpdatedAt
                },
                new()
                {
                    CheckpointId  = "blockchain-confirmation",
                    Name          = "Blockchain Confirmation",
                    Category      = "Blockchain",
                    IsBlocking    = true,
                    CorrelationId = correlationId,
                    State         = StateForStatus(deployment.CurrentStatus,
                        requiredStatuses: [DeploymentStatus.Confirmed, DeploymentStatus.Indexed, DeploymentStatus.Completed]),
                    Explanation   = "Transaction included in a confirmed block.",
                    RecommendedAction = "Wait for block confirmation.",
                    LastUpdatedAt = deployment.UpdatedAt
                },
                new()
                {
                    CheckpointId  = "deployment-complete",
                    Name          = "Deployment Complete",
                    Category      = "Lifecycle",
                    IsBlocking    = true,
                    CorrelationId = correlationId,
                    State         = StateForStatus(deployment.CurrentStatus,
                        requiredStatuses: [DeploymentStatus.Completed]),
                    Explanation   = "All post-deployment operations finished and token is live.",
                    RecommendedAction = deployment.CurrentStatus == DeploymentStatus.Failed
                        ? "Review error details and retry deployment."
                        : "Wait for completion.",
                    LastUpdatedAt = deployment.UpdatedAt
                }
            };

            // If deployment has failed, mark the relevant checkpoints.
            if (deployment.CurrentStatus == DeploymentStatus.Failed)
            {
                foreach (var cp in checkpoints.Where(c => c.State == ComplianceCheckpointState.Pending))
                {
                    cp.State = ComplianceCheckpointState.Failed;
                    cp.RecommendedAction = "Resolve the deployment failure and retry.";
                }
            }

            return checkpoints;
        }

        private static ComplianceCheckpointState StateForStatus(
            DeploymentStatus current, DeploymentStatus[] requiredStatuses)
        {
            if (requiredStatuses.Contains(current))
                return ComplianceCheckpointState.Satisfied;

            return current == DeploymentStatus.Failed
                ? ComplianceCheckpointState.Failed
                : ComplianceCheckpointState.Pending;
        }

        private static string DerivePosture(int blocking, int satisfied, int total)
        {
            if (blocking == 0 && satisfied == total) return "Fully compliant";
            if (blocking == 0) return "On track";
            if (blocking <= 1) return "Action required";
            return "Multiple blockers – immediate action required";
        }

        private static string BuildProgressSummary(DeploymentStatus status) => status switch
        {
            DeploymentStatus.Queued    => "Deployment queued – awaiting submission.",
            DeploymentStatus.Submitted => "Transaction submitted to the network.",
            DeploymentStatus.Pending   => "Awaiting blockchain confirmation.",
            DeploymentStatus.Confirmed => "Transaction confirmed on-chain.",
            DeploymentStatus.Indexed   => "Transaction indexed – finalising post-deployment steps.",
            DeploymentStatus.Completed => "Deployment complete – token is live.",
            DeploymentStatus.Failed    => "Deployment failed – action required.",
            DeploymentStatus.Cancelled => "Deployment cancelled by user.",
            _                          => status.ToString()
        };

        private static OperationTimelineResponse FailTimeline(
            string deploymentId, string correlationId,
            string errorCode, string message, string hint) =>
            new()
            {
                Success       = false,
                DeploymentId  = deploymentId,
                ErrorCode     = errorCode,
                ErrorMessage  = message,
                RemediationHint = hint,
                CorrelationId = correlationId,
                GeneratedAt   = DateTime.UtcNow
            };

        private static ComplianceCheckpointResponse FailCheckpoints(
            string deploymentId, string correlationId,
            string errorCode, string message, string hint) =>
            new()
            {
                Success       = false,
                DeploymentId  = deploymentId,
                ErrorCode     = errorCode,
                ErrorMessage  = message,
                RemediationHint = hint,
                CorrelationId = correlationId,
                GeneratedAt   = DateTime.UtcNow
            };
    }
}

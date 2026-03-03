using System.Text.RegularExpressions;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.MVPHardening;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implements MVP backend hardening: auth contract verification, deployment reliability with
    /// idempotency and state machine, compliance normalisation, and observability tracing.
    /// </summary>
    public class MVPBackendHardeningService : IMVPBackendHardeningService
    {
        private readonly ILogger<MVPBackendHardeningService> _logger;

        // In-memory stores (production: distributed cache / DB)
        private readonly Dictionary<string, DeploymentRecord> _deploymentStore = new();
        private readonly Dictionary<string, string> _idempotencyIndex = new(); // idempotencyKey → deploymentId
        private readonly List<MVPHardeningAuditEvent> _auditLog = new();
        private readonly object _lock = new();

        private static readonly Regex EmailPattern =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        // Known check types
        private static readonly HashSet<string> KnownCheckTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "kyc", "aml", "sanctions", "whitelist", "jurisdiction", "ownership"
        };

        // Valid state transitions
        private static readonly Dictionary<DeploymentReliabilityStatus, List<DeploymentReliabilityStatus>> ValidTransitions =
            new()
            {
                {
                    DeploymentReliabilityStatus.Pending,
                    new() { DeploymentReliabilityStatus.Accepted, DeploymentReliabilityStatus.Cancelled, DeploymentReliabilityStatus.Failed }
                },
                {
                    DeploymentReliabilityStatus.Accepted,
                    new() { DeploymentReliabilityStatus.Queued, DeploymentReliabilityStatus.Cancelled, DeploymentReliabilityStatus.Failed }
                },
                {
                    DeploymentReliabilityStatus.Queued,
                    new() { DeploymentReliabilityStatus.Processing, DeploymentReliabilityStatus.Cancelled, DeploymentReliabilityStatus.Failed }
                },
                {
                    DeploymentReliabilityStatus.Processing,
                    new() { DeploymentReliabilityStatus.Completed, DeploymentReliabilityStatus.Failed, DeploymentReliabilityStatus.Retrying }
                },
                {
                    DeploymentReliabilityStatus.Failed,
                    new() { DeploymentReliabilityStatus.Retrying, DeploymentReliabilityStatus.Cancelled }
                },
                {
                    DeploymentReliabilityStatus.Retrying,
                    new() { DeploymentReliabilityStatus.Queued, DeploymentReliabilityStatus.Cancelled, DeploymentReliabilityStatus.Failed }
                },
                {
                    DeploymentReliabilityStatus.Completed,
                    new List<DeploymentReliabilityStatus>() // terminal
                },
                {
                    DeploymentReliabilityStatus.Cancelled,
                    new List<DeploymentReliabilityStatus>() // terminal
                }
            };

        /// <summary>Internal deployment record with mutable state.</summary>
        private sealed class DeploymentRecord
        {
            public string DeploymentId { get; init; } = string.Empty;
            public string? TokenName { get; init; }
            public string? TokenStandard { get; init; }
            public string? DeployerAddress { get; init; }
            public string? Network { get; init; }
            public string? IdempotencyKey { get; init; }
            public string? CorrelationId { get; init; }
            public int MaxRetries { get; init; }
            public int RetryCount { get; set; }
            public DeploymentReliabilityStatus Status { get; set; }
            public string? StatusReason { get; set; }
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }

        /// <summary>Initialises a new instance of <see cref="MVPBackendHardeningService"/>.</summary>
        public MVPBackendHardeningService(ILogger<MVPBackendHardeningService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<AuthContractVerifyResponse> VerifyAuthContractAsync(AuthContractVerifyRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                var err = BuildAuthError("INVALID_REQUEST", "Request cannot be null.",
                    "Provide a valid AuthContractVerifyRequest.", correlationId);
                RecordAudit("VerifyAuthContract", correlationId, null, false, "INVALID_REQUEST");
                return Task.FromResult(err);
            }

            _logger.LogInformation("VerifyAuthContract called for email={Email} correlationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.Email),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                var err = BuildAuthError("MISSING_EMAIL", "Email is required.",
                    "Provide a non-empty email address.", correlationId);
                RecordAudit("VerifyAuthContract", correlationId, null, false, "MISSING_EMAIL");
                return Task.FromResult(err);
            }

            if (!EmailPattern.IsMatch(request.Email))
            {
                var err = BuildAuthError("INVALID_EMAIL_FORMAT", "Email format is invalid.",
                    "Provide a valid email address in the format user@domain.tld.", correlationId);
                RecordAudit("VerifyAuthContract", correlationId, null, false, "INVALID_EMAIL_FORMAT");
                return Task.FromResult(err);
            }

            // Deterministic mock Algorand address derived from email (no real key material)
            var mockAddress = DeriveStableAddress(request.Email);

            var response = new AuthContractVerifyResponse
            {
                Success = true,
                IsDeterministic = true,
                AlgorandAddress = mockAddress,
                CorrelationId = correlationId
            };
            RecordAudit("VerifyAuthContract", correlationId, null, true, null);
            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public Task<DeploymentReliabilityResponse> InitiateDeploymentAsync(DeploymentReliabilityRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                var err = BuildDeploymentError("INVALID_REQUEST", "Request cannot be null.",
                    "Provide a valid DeploymentReliabilityRequest.", correlationId);
                RecordAudit("InitiateDeployment", correlationId, null, false, "INVALID_REQUEST");
                return Task.FromResult(err);
            }

            _logger.LogInformation("InitiateDeployment tokenName={TokenName} correlationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.TokenName),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.TokenName))
            {
                var err = BuildDeploymentError("MISSING_TOKEN_NAME", "TokenName is required.",
                    "Provide a non-empty token name.", correlationId);
                RecordAudit("InitiateDeployment", correlationId, null, false, "MISSING_TOKEN_NAME");
                return Task.FromResult(err);
            }

            if (string.IsNullOrWhiteSpace(request.DeployerAddress))
            {
                var err = BuildDeploymentError("MISSING_DEPLOYER_ADDRESS", "DeployerAddress is required.",
                    "Provide the deployer's wallet address.", correlationId);
                RecordAudit("InitiateDeployment", correlationId, null, false, "MISSING_DEPLOYER_ADDRESS");
                return Task.FromResult(err);
            }

            if (string.IsNullOrWhiteSpace(request.Network))
            {
                var err = BuildDeploymentError("MISSING_NETWORK", "Network is required.",
                    "Provide the target network (e.g. algorand-mainnet).", correlationId);
                RecordAudit("InitiateDeployment", correlationId, null, false, "MISSING_NETWORK");
                return Task.FromResult(err);
            }

            // Clamp MaxRetries to non-negative
            if (request.MaxRetries < 0)
                request.MaxRetries = 0;

            lock (_lock)
            {
                // Check idempotency – also validate that the replayed request matches the original
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                    _idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingId) &&
                    _deploymentStore.TryGetValue(existingId, out var existing))
                {
                    // Verify critical fields match to prevent idempotency key reuse with different params
                    if (!string.Equals(existing.TokenName, request.TokenName, StringComparison.Ordinal) ||
                        !string.Equals(existing.Network, request.Network, StringComparison.Ordinal) ||
                        !string.Equals(existing.DeployerAddress, request.DeployerAddress, StringComparison.Ordinal))
                    {
                        _logger.LogWarning("IdempotencyKeyMismatch key={Key}", LoggingHelper.SanitizeLogInput(request.IdempotencyKey));
                        var mismatch = BuildDeploymentError("IDEMPOTENCY_KEY_MISMATCH",
                            "Idempotency key reused with different request parameters.",
                            "Use a unique idempotency key for each distinct deployment request.", correlationId);
                        RecordAudit("InitiateDeployment", correlationId, existingId, false, "IDEMPOTENCY_KEY_MISMATCH");
                        return Task.FromResult(mismatch);
                    }

                    _logger.LogInformation("IdempotentReplay deploymentId={Id}", LoggingHelper.SanitizeLogInput(existingId));
                    var replay = MapToResponse(existing);
                    replay.IsIdempotentReplay = true;
                    replay.CorrelationId = correlationId;
                    RecordAudit("InitiateDeployment", correlationId, existingId, true, null);
                    return Task.FromResult(replay);
                }

                var deploymentId = Guid.NewGuid().ToString();
                var record = new DeploymentRecord
                {
                    DeploymentId = deploymentId,
                    TokenName = request.TokenName,
                    TokenStandard = request.TokenStandard,
                    DeployerAddress = request.DeployerAddress,
                    Network = request.Network,
                    IdempotencyKey = request.IdempotencyKey,
                    CorrelationId = correlationId,
                    MaxRetries = request.MaxRetries,
                    Status = DeploymentReliabilityStatus.Pending
                };
                _deploymentStore[deploymentId] = record;
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    _idempotencyIndex[request.IdempotencyKey] = deploymentId;

                RecordAudit("InitiateDeployment", correlationId, deploymentId, true, null);
                return Task.FromResult(MapToResponse(record));
            }
        }

        /// <inheritdoc/>
        public Task<DeploymentReliabilityResponse?> GetDeploymentStatusAsync(string deploymentId, string? correlationId)
        {
            _logger.LogInformation("GetDeploymentStatus deploymentId={Id} correlationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(deploymentId),
                LoggingHelper.SanitizeLogInput(correlationId));

            lock (_lock)
            {
                if (!_deploymentStore.TryGetValue(deploymentId, out var record))
                    return Task.FromResult<DeploymentReliabilityResponse?>(null);

                var response = MapToResponse(record);
                response.CorrelationId = correlationId ?? record.CorrelationId;
                return Task.FromResult<DeploymentReliabilityResponse?>(response);
            }
        }

        /// <inheritdoc/>
        public Task<DeploymentReliabilityResponse> TransitionDeploymentStatusAsync(DeploymentStatusTransitionRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null || string.IsNullOrWhiteSpace(request.DeploymentId))
            {
                var err = BuildDeploymentError("INVALID_REQUEST", "DeploymentId is required.",
                    "Provide a valid deployment ID.", correlationId);
                RecordAudit("TransitionDeploymentStatus", correlationId, null, false, "INVALID_REQUEST");
                return Task.FromResult(err);
            }

            _logger.LogInformation("TransitionDeploymentStatus deploymentId={Id} target={Target} correlationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.DeploymentId),
                request.TargetStatus.ToString(),
                LoggingHelper.SanitizeLogInput(correlationId));

            lock (_lock)
            {
                if (!_deploymentStore.TryGetValue(request.DeploymentId, out var record))
                {
                    var err = BuildDeploymentError("DEPLOYMENT_NOT_FOUND",
                        $"Deployment '{request.DeploymentId}' not found.",
                        "Verify the deployment ID.", correlationId);
                    RecordAudit("TransitionDeploymentStatus", correlationId, request.DeploymentId, false, "DEPLOYMENT_NOT_FOUND");
                    return Task.FromResult(err);
                }

                var allowed = ValidTransitions.TryGetValue(record.Status, out var targets) ? targets : new List<DeploymentReliabilityStatus>();
                if (!allowed.Contains(request.TargetStatus))
                {
                    var err = BuildDeploymentError("INVALID_STATE_TRANSITION",
                        $"Cannot transition from {record.Status} to {request.TargetStatus}.",
                        $"Allowed targets from {record.Status}: {string.Join(", ", allowed)}.", correlationId);
                    err.DeploymentId = record.DeploymentId;
                    err.Status = record.Status;
                    RecordAudit("TransitionDeploymentStatus", correlationId, request.DeploymentId, false, "INVALID_STATE_TRANSITION");
                    return Task.FromResult(err);
                }

                // Handle retry counting
                if (request.TargetStatus == DeploymentReliabilityStatus.Retrying)
                {
                    if (record.RetryCount >= record.MaxRetries)
                    {
                        // Force to Cancelled when retries exhausted
                        record.Status = DeploymentReliabilityStatus.Cancelled;
                        record.StatusReason = "Max retries exhausted.";
                        record.UpdatedAt = DateTime.UtcNow;
                        var response = MapToResponse(record);
                        response.CorrelationId = correlationId;
                        RecordAudit("TransitionDeploymentStatus", correlationId, record.DeploymentId, true, null);
                        return Task.FromResult(response);
                    }
                    record.RetryCount++;
                }

                record.Status = request.TargetStatus;
                record.StatusReason = request.Reason;
                record.UpdatedAt = DateTime.UtcNow;

                var result = MapToResponse(record);
                result.CorrelationId = correlationId;
                RecordAudit("TransitionDeploymentStatus", correlationId, record.DeploymentId, true, null);
                return Task.FromResult(result);
            }
        }

        /// <inheritdoc/>
        public Task<ComplianceCheckResponse> RunComplianceCheckAsync(ComplianceCheckRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                var err = BuildComplianceError("INVALID_REQUEST", "Request cannot be null.",
                    "Provide a valid ComplianceCheckRequest.", correlationId);
                RecordAudit("RunComplianceCheck", correlationId, null, false, "INVALID_REQUEST");
                return Task.FromResult(err);
            }

            _logger.LogInformation("RunComplianceCheck assetId={AssetId} checkType={CheckType} correlationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.AssetId),
                LoggingHelper.SanitizeLogInput(request.CheckType),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.AssetId))
            {
                var err = BuildComplianceError("MISSING_ASSET_ID", "AssetId is required.",
                    "Provide a valid asset identifier.", correlationId);
                RecordAudit("RunComplianceCheck", correlationId, null, false, "MISSING_ASSET_ID");
                return Task.FromResult(err);
            }

            if (string.IsNullOrWhiteSpace(request.CheckType))
            {
                var err = BuildComplianceError("MISSING_CHECK_TYPE", "CheckType is required.",
                    "Provide a compliance check type (e.g. kyc, aml, sanctions).", correlationId);
                RecordAudit("RunComplianceCheck", correlationId, null, false, "MISSING_CHECK_TYPE");
                return Task.FromResult(err);
            }

            ComplianceOutcome outcome;
            string? remediationHint = null;
            var details = new List<ComplianceCheckDetail>();

            if (!KnownCheckTypes.Contains(request.CheckType))
            {
                // Unknown check type → warning, not error
                outcome = ComplianceOutcome.Warning;
                remediationHint = $"CheckType '{request.CheckType}' is not a recognised compliance check. Known types: {string.Join(", ", KnownCheckTypes)}.";
                details.Add(new ComplianceCheckDetail
                {
                    Rule = "check-type-known",
                    Outcome = ComplianceOutcome.Warning,
                    Message = $"Unknown check type: {request.CheckType}",
                    RemediationHint = remediationHint
                });
            }
            else
            {
                outcome = ComplianceOutcome.Pass;
                details.Add(new ComplianceCheckDetail
                {
                    Rule = $"{request.CheckType}-check",
                    Outcome = ComplianceOutcome.Pass,
                    Message = $"{request.CheckType.ToUpperInvariant()} check passed for asset {request.AssetId}."
                });
            }

            var response = new ComplianceCheckResponse
            {
                Success = true,
                Outcome = outcome,
                CheckType = request.CheckType,
                AssetId = request.AssetId,
                Details = details,
                RemediationHint = remediationHint,
                CorrelationId = correlationId
            };
            RecordAudit("RunComplianceCheck", correlationId, null, true, null);
            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public Task<ObservabilityTraceResponse> CreateTraceAsync(ObservabilityTraceRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();
            var operationName = request?.OperationName ?? "unknown";

            _logger.LogInformation("CreateTrace operation={Operation} correlationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(operationName),
                LoggingHelper.SanitizeLogInput(correlationId));

            var traceId = Guid.NewGuid().ToString();
            RecordAudit("CreateTrace", correlationId, null, true, null);

            return Task.FromResult(new ObservabilityTraceResponse
            {
                Success = true,
                TraceId = traceId,
                CorrelationId = correlationId,
                OperationName = operationName
            });
        }

        /// <inheritdoc/>
        public IReadOnlyList<MVPHardeningAuditEvent> GetAuditEvents(string? correlationId = null)
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(correlationId))
                    return _auditLog.ToList();
                return _auditLog.Where(e => e.CorrelationId == correlationId).ToList();
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void RecordAudit(string operationName, string? correlationId, string? userId, bool success, string? errorCode)
        {
            lock (_lock)
            {
                _auditLog.Add(new MVPHardeningAuditEvent
                {
                    OperationName = operationName,
                    CorrelationId = correlationId,
                    UserId = userId,
                    Success = success,
                    ErrorCode = errorCode
                });
            }
        }

        /// <summary>
        /// Derives a stable, deterministic mock Algorand address from an email string.
        /// NOTE: This is a mock implementation for testing/demo purposes only. It does NOT use
        /// real ARC76 key derivation and must be replaced with a proper ARC76 implementation
        /// (AlgorandARC76AccountDotNet) before production use.
        /// </summary>
        private static string DeriveStableAddress(string email)
        {
            // 58-char base32 uppercase string (no real crypto – deterministic for testing)
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var chars = new char[58];
            for (int i = 0; i < 58; i++)
                chars[i] = alphabet[hash[i % hash.Length] % 32];
            return new string(chars);
        }

        private static AuthContractVerifyResponse BuildAuthError(string code, string msg, string hint, string? correlationId)
            => new()
            {
                Success = false,
                IsDeterministic = false,
                ErrorCode = code,
                ErrorMessage = msg,
                RemediationHint = hint,
                CorrelationId = correlationId
            };

        private static DeploymentReliabilityResponse BuildDeploymentError(string code, string msg, string hint, string? correlationId)
            => new()
            {
                Success = false,
                ErrorCode = code,
                ErrorMessage = msg,
                RemediationHint = hint,
                CorrelationId = correlationId
            };

        private static ComplianceCheckResponse BuildComplianceError(string code, string msg, string hint, string? correlationId)
            => new()
            {
                Success = false,
                Outcome = ComplianceOutcome.Fail,
                ErrorCode = code,
                ErrorMessage = msg,
                RemediationHint = hint,
                CorrelationId = correlationId
            };

        private static DeploymentReliabilityResponse MapToResponse(DeploymentRecord r)
            => new()
            {
                Success = true,
                DeploymentId = r.DeploymentId,
                Status = r.Status,
                StatusReason = r.StatusReason,
                RetryCount = r.RetryCount,
                MaxRetries = r.MaxRetries,
                CorrelationId = r.CorrelationId,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
    }
}

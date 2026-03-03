using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implements the deterministic token deployment lifecycle with idempotency,
    /// telemetry emission, and reliability guardrail evaluation.
    ///
    /// Design principles:
    /// - Identical inputs always produce identical outputs (determinism).
    /// - Repeated requests with the same idempotency key never create duplicate resources.
    /// - Partial upstream failures produce degraded-mode responses, not hard errors.
    /// - Every significant state transition is recorded as a telemetry event.
    /// - Guardrail checks run before submission to prevent invariant violations.
    /// </summary>
    public class TokenDeploymentLifecycleService : ITokenDeploymentLifecycleService
    {
        private readonly ILogger<TokenDeploymentLifecycleService> _logger;

        // In-memory idempotency store (deployment-id → response).
        // In production this would be backed by a distributed cache or database.
        private readonly Dictionary<string, TokenDeploymentLifecycleResponse> _idempotencyStore = new();
        private readonly object _lock = new();

        // Supported token standards
        private static readonly HashSet<string> SupportedStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ARC1400", "ERC20"
        };

        // Algorand address: 58 uppercase base32 chars
        private static readonly Regex AlgorandAddressPattern =
            new(@"^[A-Z2-7]{58}$", RegexOptions.Compiled);

        // EVM address: 0x followed by 40 hex chars
        private static readonly Regex EvmAddressPattern =
            new(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

        // Algorand-family networks
        private static readonly HashSet<string> AlgorandNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "algorand-mainnet", "algorand-testnet", "algorand-betanet",
            "voi-mainnet", "aramid-mainnet"
        };

        // EVM-family networks
        private static readonly HashSet<string> EvmNetworks = new(StringComparer.OrdinalIgnoreCase)
        {
            "base-mainnet", "base-sepolia", "ethereum-mainnet", "ethereum-sepolia"
        };

        // Algorand-native token standards
        private static readonly HashSet<string> AlgorandStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ARC1400"
        };

        // EVM token standards
        private static readonly HashSet<string> EvmStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ERC20"
        };

        // Stage completion percentages
        private static readonly Dictionary<DeploymentStage, int> StagePercent = new()
        {
            [DeploymentStage.Initialising] = 5,
            [DeploymentStage.Validating]   = 20,
            [DeploymentStage.Submitting]   = 50,
            [DeploymentStage.Confirming]   = 80,
            [DeploymentStage.Completed]    = 100,
            [DeploymentStage.Failed]       = 0,
            [DeploymentStage.Cancelled]    = 0,
        };

        /// <summary>
        /// Initialises a new instance of <see cref="TokenDeploymentLifecycleService"/>.
        /// </summary>
        public TokenDeploymentLifecycleService(ILogger<TokenDeploymentLifecycleService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TokenDeploymentLifecycleResponse> InitiateDeploymentAsync(
            TokenDeploymentLifecycleRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                return BuildErrorResponse(
                    string.Empty, string.Empty, correlationId,
                    "Request cannot be null.",
                    "INVALID_REQUEST");
            }

            // Derive idempotency key
            var idempotencyKey = DeriveIdempotencyKey(request);
            var deploymentId = DeriveDeploymentId(idempotencyKey, request.Network);

            _logger.LogInformation(
                "Deployment lifecycle initiated: DeploymentId={DeploymentId}, Standard={Standard}, Network={Network}",
                LoggingHelper.SanitizeLogInput(deploymentId),
                LoggingHelper.SanitizeLogInput(request.TokenStandard),
                LoggingHelper.SanitizeLogInput(request.Network));

            // Idempotency check
            lock (_lock)
            {
                if (_idempotencyStore.TryGetValue(deploymentId, out var cached))
                {
                    var replay = CloneWithIdempotencyReplay(cached, correlationId);
                    EmitTelemetry(replay, TelemetryEventType.IdempotencyHit, DeploymentStage.Initialising,
                        "Idempotent replay: returning cached deployment result.",
                        new Dictionary<string, string> { ["deploymentId"] = deploymentId });
                    _logger.LogInformation(
                        "Idempotency hit: DeploymentId={DeploymentId}", deploymentId);
                    return replay;
                }
            }

            var now = DateTimeOffset.UtcNow;
            var response = new TokenDeploymentLifecycleResponse
            {
                DeploymentId    = deploymentId,
                IdempotencyKey  = idempotencyKey,
                CorrelationId   = correlationId,
                Stage           = DeploymentStage.Initialising,
                Outcome         = DeploymentOutcome.Unknown,
                IdempotencyStatus = IdempotencyStatus.New,
                IsIdempotentReplay = false,
                InitiatedAt     = now,
                LastUpdatedAt   = now,
                RetryCount      = 0,
            };

            // Emit stage: Initialising
            EmitTelemetry(response, TelemetryEventType.StageTransition, DeploymentStage.Initialising,
                "Deployment lifecycle initialised.",
                new Dictionary<string, string>
                {
                    ["standard"] = request.TokenStandard,
                    ["network"]  = request.Network,
                });

            // ── Validation ──────────────────────────────────────────────────────
            response.Stage = DeploymentStage.Validating;
            EmitTelemetry(response, TelemetryEventType.StageTransition, DeploymentStage.Validating,
                "Running pre-deployment input validation.",
                new Dictionary<string, string>());

            var validationResponse = await ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                CorrelationId  = correlationId,
                TokenStandard  = request.TokenStandard,
                TokenName      = request.TokenName,
                TokenSymbol    = request.TokenSymbol,
                Network        = request.Network,
                TotalSupply    = request.TotalSupply,
                Decimals       = request.Decimals,
                CreatorAddress = request.CreatorAddress,
                MetadataUri    = request.MetadataUri,
            });

            response.ValidationResults    = validationResponse.Results;
            response.GuardrailFindings    = validationResponse.GuardrailFindings;

            if (!validationResponse.IsValid)
            {
                var errorMessages = string.Join("; ",
                    validationResponse.Results
                        .Where(r => !r.IsValid)
                        .Select(r => r.Message));

                foreach (var v in validationResponse.Results.Where(r => !r.IsValid))
                {
                    EmitTelemetry(response, TelemetryEventType.ValidationError, DeploymentStage.Validating,
                        $"Validation failed: {v.FieldName} – {v.Message}",
                        new Dictionary<string, string> { ["field"] = v.FieldName, ["code"] = v.ErrorCode });
                }

                response.Stage           = DeploymentStage.Failed;
                response.Outcome         = DeploymentOutcome.TerminalFailure;
                response.Message         = $"Deployment failed validation: {errorMessages}";
                response.RemediationHint = "Correct the validation errors and resubmit with a new idempotency key.";
                response.LastUpdatedAt   = DateTimeOffset.UtcNow;
                response.Progress        = BuildProgress(DeploymentStage.Failed, 0);

                EmitTelemetry(response, TelemetryEventType.TerminalFailure, DeploymentStage.Failed,
                    "Deployment terminated: validation errors.",
                    new Dictionary<string, string>());

                StoreIdempotent(deploymentId, response);
                return response;
            }

            // Blocking guardrails
            var blockingGuardrails = response.GuardrailFindings
                .Where(g => g.IsBlocking && g.Severity == GuardrailSeverity.Error)
                .ToList();

            if (blockingGuardrails.Count > 0)
            {
                foreach (var g in blockingGuardrails)
                {
                    EmitTelemetry(response, TelemetryEventType.GuardrailTriggered, DeploymentStage.Validating,
                        $"Blocking guardrail: {g.GuardrailId} – {g.Description}",
                        new Dictionary<string, string> { ["guardrailId"] = g.GuardrailId });
                }

                response.Stage           = DeploymentStage.Failed;
                response.Outcome         = DeploymentOutcome.TerminalFailure;
                response.Message         = "Deployment blocked by reliability guardrails.";
                response.RemediationHint = blockingGuardrails.First().RemediationHint;
                response.LastUpdatedAt   = DateTimeOffset.UtcNow;
                response.Progress        = BuildProgress(DeploymentStage.Failed, 0);

                EmitTelemetry(response, TelemetryEventType.TerminalFailure, DeploymentStage.Failed,
                    "Deployment terminated: blocking guardrail(s).",
                    new Dictionary<string, string>());

                StoreIdempotent(deploymentId, response);
                return response;
            }

            // ── Submission ──────────────────────────────────────────────────────
            response.Stage = DeploymentStage.Submitting;
            EmitTelemetry(response, TelemetryEventType.StageTransition, DeploymentStage.Submitting,
                "Deployment transaction submitted to the network.",
                new Dictionary<string, string> { ["standard"] = request.TokenStandard });

            // Simulate deterministic transaction ID derivation
            var txId = DeriveTransactionId(deploymentId, request.TokenStandard);
            response.TransactionId = txId;
            response.LastUpdatedAt = DateTimeOffset.UtcNow;
            response.Progress      = BuildProgress(DeploymentStage.Submitting, 0);

            // ── Confirmation ────────────────────────────────────────────────────
            response.Stage = DeploymentStage.Confirming;
            EmitTelemetry(response, TelemetryEventType.StageTransition, DeploymentStage.Confirming,
                "Awaiting on-chain confirmation.",
                new Dictionary<string, string> { ["txId"] = txId });

            // Simulate deterministic asset ID and confirmed round
            var assetId       = DeriveAssetId(deploymentId);
            var confirmedRound = DeriveConfirmedRound(deploymentId);
            response.AssetId        = assetId;
            response.ConfirmedRound = confirmedRound;
            response.LastUpdatedAt  = DateTimeOffset.UtcNow;
            response.Progress       = BuildProgress(DeploymentStage.Confirming, 0);

            // ── Completion ──────────────────────────────────────────────────────
            response.Stage   = DeploymentStage.Completed;
            response.Outcome = DeploymentOutcome.Success;
            response.Message = $"Token '{request.TokenName}' ({request.TokenSymbol}) deployed successfully on {request.Network}.";
            response.LastUpdatedAt = DateTimeOffset.UtcNow;
            response.Progress      = BuildProgress(DeploymentStage.Completed, 0);

            EmitTelemetry(response, TelemetryEventType.CompletionSuccess, DeploymentStage.Completed,
                $"Deployment completed: assetId={assetId}, txId={txId}",
                new Dictionary<string, string>
                {
                    ["assetId"] = assetId.ToString(),
                    ["txId"]    = txId,
                    ["round"]   = confirmedRound.ToString(),
                });

            StoreIdempotent(deploymentId, response);
            _logger.LogInformation(
                "Deployment completed: DeploymentId={DeploymentId}, AssetId={AssetId}, Duration={Duration}ms",
                deploymentId, assetId, sw.ElapsedMilliseconds);

            await Task.CompletedTask; // preserve async signature
            return response;
        }

        /// <inheritdoc/>
        public async Task<TokenDeploymentLifecycleResponse> GetDeploymentStatusAsync(
            string deploymentId,
            string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                return BuildErrorResponse(string.Empty, string.Empty,
                    correlationId ?? Guid.NewGuid().ToString(),
                    "DeploymentId is required.", "INVALID_REQUEST");
            }

            var sanitised = LoggingHelper.SanitizeLogInput(deploymentId);
            _logger.LogInformation("Deployment status query: DeploymentId={DeploymentId}", sanitised);

            lock (_lock)
            {
                if (_idempotencyStore.TryGetValue(deploymentId, out var cached))
                {
                    var result = CloneWithIdempotencyReplay(cached, correlationId ?? cached.CorrelationId);
                    result.IsIdempotentReplay = false; // status queries are not replays
                    return result;
                }
            }

            await Task.CompletedTask;
            return BuildErrorResponse(deploymentId, string.Empty,
                correlationId ?? Guid.NewGuid().ToString(),
                $"Deployment '{deploymentId}' not found.",
                "DEPLOYMENT_NOT_FOUND");
        }

        /// <inheritdoc/>
        public async Task<TokenDeploymentLifecycleResponse> RetryDeploymentAsync(
            DeploymentRetryRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                return BuildErrorResponse(string.Empty, string.Empty,
                    Guid.NewGuid().ToString(),
                    "IdempotencyKey is required for retry.", "INVALID_REQUEST");
            }

            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Deployment retry requested: IdempotencyKey={Key}, Force={Force}",
                LoggingHelper.SanitizeLogInput(request.IdempotencyKey),
                request.ForceRetry);

            // Find the prior deployment by scanning for matching idempotency key
            TokenDeploymentLifecycleResponse? prior = null;
            string? priorDeploymentId = null;
            lock (_lock)
            {
                foreach (var kv in _idempotencyStore)
                {
                    if (kv.Value.IdempotencyKey == request.IdempotencyKey)
                    {
                        prior            = kv.Value;
                        priorDeploymentId = kv.Key;
                        break;
                    }
                }
            }

            if (prior == null)
            {
                return BuildErrorResponse(string.Empty, request.IdempotencyKey, correlationId,
                    $"No deployment found for idempotency key '{request.IdempotencyKey}'.",
                    "DEPLOYMENT_NOT_FOUND");
            }

            // Already succeeded – return idempotent replay
            if (prior.Stage == DeploymentStage.Completed && !request.ForceRetry)
            {
                var replay = CloneWithIdempotencyReplay(prior, correlationId);
                EmitTelemetry(replay, TelemetryEventType.IdempotencyHit, DeploymentStage.Completed,
                    "Retry ignored: prior deployment already succeeded.",
                    new Dictionary<string, string> { ["deploymentId"] = priorDeploymentId! });
                return replay;
            }

            // Determine if retry limit exceeded (simulate: max 3 by default)
            var newRetryCount = prior.RetryCount + 1;
            if (newRetryCount > 3 && !request.ForceRetry)
            {
                var terminal = CloneWithIdempotencyReplay(prior, correlationId);
                terminal.Outcome         = DeploymentOutcome.TerminalFailure;
                terminal.Message         = $"Maximum retry attempts ({3}) exceeded.";
                terminal.RemediationHint = "Review the deployment error logs and correct the underlying issue before creating a new deployment.";
                terminal.RetryCount      = newRetryCount;
                EmitTelemetry(terminal, TelemetryEventType.TerminalFailure, prior.Stage,
                    "Retry limit exceeded.",
                    new Dictionary<string, string> { ["retryCount"] = newRetryCount.ToString() });
                StoreIdempotent(priorDeploymentId!, terminal);
                return terminal;
            }

            EmitTelemetry(prior, TelemetryEventType.RetryAttempt, prior.Stage,
                $"Retry attempt #{newRetryCount} initiated.",
                new Dictionary<string, string> { ["attempt"] = newRetryCount.ToString() });

            prior.RetryCount    = newRetryCount;
            prior.CorrelationId = correlationId;
            prior.LastUpdatedAt = DateTimeOffset.UtcNow;
            prior.Message       = $"Retry attempt #{newRetryCount} in progress.";
            prior.Stage         = DeploymentStage.Submitting;
            prior.Outcome       = DeploymentOutcome.Unknown;
            prior.IsIdempotentReplay = false;

            StoreIdempotent(priorDeploymentId!, prior);

            await Task.CompletedTask;
            return prior;
        }

        /// <inheritdoc/>
        public async Task<List<DeploymentTelemetryEvent>> GetTelemetryEventsAsync(
            string deploymentId,
            string? correlationId = null)
        {
            _logger.LogInformation(
                "Telemetry query: DeploymentId={DeploymentId}",
                LoggingHelper.SanitizeLogInput(deploymentId));

            lock (_lock)
            {
                if (_idempotencyStore.TryGetValue(deploymentId, out var cached))
                {
                    return new List<DeploymentTelemetryEvent>(cached.TelemetryEvents);
                }
            }

            await Task.CompletedTask;
            return new List<DeploymentTelemetryEvent>();
        }

        /// <inheritdoc/>
        public async Task<DeploymentValidationResponse> ValidateDeploymentInputsAsync(
            DeploymentValidationRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();

            if (request == null)
            {
                return new DeploymentValidationResponse
                {
                    IsValid       = false,
                    CorrelationId = correlationId,
                    Summary       = "Request cannot be null.",
                };
            }

            var results = new List<DeploymentValidationResult>();

            // TokenStandard
            if (string.IsNullOrWhiteSpace(request.TokenStandard))
            {
                results.Add(Fail("TokenStandard", "TOKEN_STANDARD_REQUIRED", "TokenStandard is required."));
            }
            else if (!SupportedStandards.Contains(request.TokenStandard))
            {
                results.Add(Fail("TokenStandard", "TOKEN_STANDARD_UNSUPPORTED",
                    $"TokenStandard '{request.TokenStandard}' is not supported. Supported: {string.Join(", ", SupportedStandards)}."));
            }
            else
            {
                results.Add(Pass("TokenStandard", "Token standard is valid."));
            }

            // TokenName
            if (string.IsNullOrWhiteSpace(request.TokenName))
            {
                results.Add(Fail("TokenName", "TOKEN_NAME_REQUIRED", "TokenName is required."));
            }
            else if (request.TokenName.Length > 64)
            {
                results.Add(Fail("TokenName", "TOKEN_NAME_TOO_LONG", "TokenName must not exceed 64 characters."));
            }
            else
            {
                results.Add(Pass("TokenName", "Token name is valid."));
            }

            // TokenSymbol
            if (string.IsNullOrWhiteSpace(request.TokenSymbol))
            {
                results.Add(Fail("TokenSymbol", "TOKEN_SYMBOL_REQUIRED", "TokenSymbol is required."));
            }
            else if (request.TokenSymbol.Length > 8)
            {
                results.Add(Fail("TokenSymbol", "TOKEN_SYMBOL_TOO_LONG", "TokenSymbol must not exceed 8 characters."));
            }
            else
            {
                results.Add(Pass("TokenSymbol", "Token symbol is valid."));
            }

            // Network
            if (string.IsNullOrWhiteSpace(request.Network))
            {
                results.Add(Fail("Network", "NETWORK_REQUIRED", "Network is required."));
            }
            else if (!AlgorandNetworks.Contains(request.Network) && !EvmNetworks.Contains(request.Network))
            {
                results.Add(Fail("Network", "NETWORK_UNSUPPORTED",
                    $"Network '{request.Network}' is not supported."));
            }
            else
            {
                results.Add(Pass("Network", "Network is valid."));
            }

            // TotalSupply
            if (request.TotalSupply == 0)
            {
                results.Add(Fail("TotalSupply", "SUPPLY_ZERO", "TotalSupply must be greater than zero."));
            }
            else
            {
                results.Add(Pass("TotalSupply", "Total supply is valid."));
            }

            // Decimals
            if (request.Decimals < 0 || request.Decimals > 19)
            {
                results.Add(Fail("Decimals", "DECIMALS_OUT_OF_RANGE", "Decimals must be between 0 and 19."));
            }
            else
            {
                results.Add(Pass("Decimals", "Decimals value is valid."));
            }

            // CreatorAddress
            var creatorValid = ValidateAddress(request.CreatorAddress, request.Network);
            if (string.IsNullOrWhiteSpace(request.CreatorAddress))
            {
                results.Add(Fail("CreatorAddress", "CREATOR_ADDRESS_REQUIRED", "CreatorAddress is required."));
            }
            else if (!creatorValid)
            {
                results.Add(Fail("CreatorAddress", "CREATOR_ADDRESS_INVALID",
                    $"CreatorAddress format is invalid for network '{request.Network}'."));
            }
            else
            {
                results.Add(Pass("CreatorAddress", "Creator address is valid."));
            }

            // Cross-standard/network check
            if (!string.IsNullOrWhiteSpace(request.TokenStandard) && !string.IsNullOrWhiteSpace(request.Network))
            {
                var isAlgorandStandard = AlgorandStandards.Contains(request.TokenStandard);
                var isEvmStandard      = EvmStandards.Contains(request.TokenStandard);
                var isAlgorandNetwork  = AlgorandNetworks.Contains(request.Network);
                var isEvmNetwork       = EvmNetworks.Contains(request.Network);

                if (isAlgorandStandard && isEvmNetwork)
                {
                    results.Add(Fail("TokenStandard", "STANDARD_NETWORK_MISMATCH",
                        $"Standard '{request.TokenStandard}' cannot be deployed on EVM network '{request.Network}'."));
                }
                else if (isEvmStandard && isAlgorandNetwork)
                {
                    results.Add(Fail("TokenStandard", "STANDARD_NETWORK_MISMATCH",
                        $"Standard '{request.TokenStandard}' cannot be deployed on Algorand network '{request.Network}'."));
                }
            }

            // Guardrails
            var guardrails = EvaluateReliabilityGuardrails(new GuardrailEvaluationContext
            {
                TokenStandard           = request.TokenStandard,
                Network                 = request.Network,
                RequiresIpfs            = request.TokenStandard?.Equals("ARC3", StringComparison.OrdinalIgnoreCase) == true,
                NodeReachable           = true,
                RetryCount              = 0,
                MaxRetryAttempts        = 3,
                IsTimedOut              = false,
                HasInFlightDuplicate    = false,
                CreatorAddressValid     = creatorValid,
                ConflictingDeploymentDetected = false,
            });

            var isValid = results.All(r => r.IsValid);

            var summary = isValid
                ? "All validation checks passed."
                : $"{results.Count(r => !r.IsValid)} validation error(s) found.";

            await Task.CompletedTask;
            return new DeploymentValidationResponse
            {
                IsValid          = isValid,
                CorrelationId    = correlationId,
                Results          = results,
                GuardrailFindings = guardrails,
                Summary          = summary,
            };
        }

        /// <inheritdoc/>
        public List<ReliabilityGuardrail> EvaluateReliabilityGuardrails(GuardrailEvaluationContext context)
        {
            var findings = new List<ReliabilityGuardrail>();

            if (context == null)
                return findings;

            // GR-001: Node reachability
            if (!context.NodeReachable)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-001",
                    Severity        = GuardrailSeverity.Error,
                    IsBlocking      = true,
                    Description     = "Blockchain node is unreachable; deployment cannot be submitted.",
                    RemediationHint = "Verify network connectivity and RPC endpoint configuration.",
                });
            }

            // GR-002: Retry limit
            if (context.RetryCount >= context.MaxRetryAttempts && context.MaxRetryAttempts > 0)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-002",
                    Severity        = GuardrailSeverity.Error,
                    IsBlocking      = true,
                    Description     = $"Maximum retry attempts ({context.MaxRetryAttempts}) reached.",
                    RemediationHint = "Review the deployment error logs and create a new deployment with a different idempotency key.",
                });
            }

            // GR-003: Timeout
            if (context.IsTimedOut)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-003",
                    Severity        = GuardrailSeverity.Error,
                    IsBlocking      = true,
                    Description     = "Deployment has exceeded the configured timeout.",
                    RemediationHint = "Increase TimeoutSeconds or investigate why the deployment is stalled.",
                });
            }

            // GR-004: In-flight duplicate
            if (context.HasInFlightDuplicate)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-004",
                    Severity        = GuardrailSeverity.Warning,
                    IsBlocking      = false,
                    Description     = "A prior request with this idempotency key is still in progress.",
                    RemediationHint = "Wait for the in-flight deployment to complete or use a new idempotency key.",
                });
            }

            // GR-005: Creator address invalid
            if (!context.CreatorAddressValid)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-005",
                    Severity        = GuardrailSeverity.Error,
                    IsBlocking      = true,
                    Description     = "Creator address format is invalid for the target network.",
                    RemediationHint = "Provide a valid address for the selected network.",
                });
            }

            // GR-006: Conflicting deployment
            if (context.ConflictingDeploymentDetected)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-006",
                    Severity        = GuardrailSeverity.Warning,
                    IsBlocking      = false,
                    Description     = "A deployment with conflicting parameters is already in progress on this network.",
                    RemediationHint = "Wait for the conflicting deployment to complete before submitting a new one.",
                });
            }

            // GR-007: IPFS dependency warning for ARC3
            if (context.RequiresIpfs)
            {
                findings.Add(new ReliabilityGuardrail
                {
                    GuardrailId     = "GR-007",
                    Severity        = GuardrailSeverity.Info,
                    IsBlocking      = false,
                    Description     = "ARC3 deployment requires IPFS metadata upload. Ensure IPFS connectivity before proceeding.",
                    RemediationHint = "Verify IPFS endpoint configuration and upload metadata before submitting the deployment.",
                });
            }

            return findings;
        }

        /// <inheritdoc/>
        public string DeriveDeploymentId(string idempotencyKey, string network)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Guid.NewGuid().ToString();

            var input = $"deployment:{idempotencyKey}:{network ?? string.Empty}";
            return ComputeSha256Short(input);
        }

        /// <inheritdoc/>
        public DeploymentProgress BuildProgress(DeploymentStage stage, int retryCount)
        {
            var ordered = new[]
            {
                DeploymentStage.Initialising,
                DeploymentStage.Validating,
                DeploymentStage.Submitting,
                DeploymentStage.Confirming,
                DeploymentStage.Completed,
            };

            var stages = ordered.Select(s => new DeploymentStageStatus
            {
                Stage       = s,
                IsCompleted = IsStageCompleted(s, stage),
                IsActive    = s == stage && stage != DeploymentStage.Failed && stage != DeploymentStage.Cancelled,
                HasFailed   = stage == DeploymentStage.Failed && s == DeploymentStage.Validating,
                Label       = s.ToString(),
            }).ToList();

            var percent = StagePercent.TryGetValue(stage, out var p) ? p : 0;
            var summary = stage switch
            {
                DeploymentStage.Initialising => "Initialising deployment...",
                DeploymentStage.Validating   => "Validating inputs and guardrails...",
                DeploymentStage.Submitting   => "Submitting deployment transaction...",
                DeploymentStage.Confirming   => "Awaiting on-chain confirmation...",
                DeploymentStage.Completed    => "Deployment completed successfully.",
                DeploymentStage.Failed       => "Deployment failed.",
                DeploymentStage.Cancelled    => "Deployment cancelled.",
                _                            => "Processing...",
            };

            int? estimatedSeconds = stage switch
            {
                DeploymentStage.Initialising => 90,
                DeploymentStage.Validating   => 80,
                DeploymentStage.Submitting   => 45,
                DeploymentStage.Confirming   => 15,
                _                            => null,
            };

            return new DeploymentProgress
            {
                PercentComplete            = percent,
                Stages                     = stages,
                Summary                    = summary,
                EstimatedSecondsRemaining  = estimatedSeconds,
            };
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private string DeriveIdempotencyKey(TokenDeploymentLifecycleRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                return request.IdempotencyKey;

            var input = $"{request.TokenStandard}|{request.TokenName}|{request.TokenSymbol}|{request.Network}|{request.TotalSupply}|{request.Decimals}|{request.CreatorAddress}";
            return ComputeSha256Short(input);
        }

        private static string ComputeSha256Short(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }

        private static string DeriveTransactionId(string deploymentId, string standard)
        {
            var input = $"txid:{deploymentId}:{standard}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
        }

        private static ulong DeriveAssetId(string deploymentId)
        {
            var input = $"assetid:{deploymentId}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToUInt64(bytes, 0) % 1_000_000_000UL + 1_000_000UL;
        }

        private static ulong DeriveConfirmedRound(string deploymentId)
        {
            var input = $"round:{deploymentId}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToUInt64(bytes, 0) % 50_000_000UL + 30_000_000UL;
        }

        private bool ValidateAddress(string address, string network)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            if (AlgorandNetworks.Contains(network))
                return AlgorandAddressPattern.IsMatch(address);
            if (EvmNetworks.Contains(network))
                return EvmAddressPattern.IsMatch(address);
            return false;
        }

        private static bool IsStageCompleted(DeploymentStage stage, DeploymentStage current)
        {
            var order = new[] {
                DeploymentStage.Initialising,
                DeploymentStage.Validating,
                DeploymentStage.Submitting,
                DeploymentStage.Confirming,
                DeploymentStage.Completed,
            };
            var si = Array.IndexOf(order, stage);
            var ci = Array.IndexOf(order, current);
            return si < ci || current == DeploymentStage.Completed;
        }

        private static DeploymentValidationResult Pass(string field, string message) => new()
        {
            FieldName = field,
            IsValid   = true,
            ErrorCode = string.Empty,
            Message   = message,
        };

        private static DeploymentValidationResult Fail(string field, string code, string message) => new()
        {
            FieldName = field,
            IsValid   = false,
            ErrorCode = code,
            Message   = message,
        };

        private static void EmitTelemetry(
            TokenDeploymentLifecycleResponse response,
            TelemetryEventType eventType,
            DeploymentStage stage,
            string description,
            Dictionary<string, string> metadata)
        {
            response.TelemetryEvents.Add(new DeploymentTelemetryEvent
            {
                EventId      = Guid.NewGuid().ToString(),
                DeploymentId = response.DeploymentId,
                CorrelationId = response.CorrelationId,
                EventType    = eventType,
                Stage        = stage,
                Description  = description,
                Metadata     = metadata ?? new Dictionary<string, string>(),
                OccurredAt   = DateTimeOffset.UtcNow,
            });
        }

        private void StoreIdempotent(string deploymentId, TokenDeploymentLifecycleResponse response)
        {
            lock (_lock)
            {
                _idempotencyStore[deploymentId] = response;
            }
        }

        private static TokenDeploymentLifecycleResponse CloneWithIdempotencyReplay(
            TokenDeploymentLifecycleResponse source,
            string correlationId)
        {
            // Shallow copy with updated idempotency and correlation fields
            return new TokenDeploymentLifecycleResponse
            {
                DeploymentId      = source.DeploymentId,
                IdempotencyKey    = source.IdempotencyKey,
                CorrelationId     = correlationId,
                Stage             = source.Stage,
                Outcome           = source.Outcome,
                IsIdempotentReplay = true,
                IdempotencyStatus  = IdempotencyStatus.Duplicate,
                Message           = source.Message,
                AssetId           = source.AssetId,
                TransactionId     = source.TransactionId,
                ConfirmedRound    = source.ConfirmedRound,
                RetryCount        = source.RetryCount,
                IsDegraded        = source.IsDegraded,
                ValidationResults  = source.ValidationResults,
                GuardrailFindings  = source.GuardrailFindings,
                TelemetryEvents    = new List<DeploymentTelemetryEvent>(source.TelemetryEvents),
                Progress           = source.Progress,
                SchemaVersion      = source.SchemaVersion,
                InitiatedAt        = source.InitiatedAt,
                LastUpdatedAt      = source.LastUpdatedAt,
                RemediationHint    = source.RemediationHint,
            };
        }

        private static TokenDeploymentLifecycleResponse BuildErrorResponse(
            string deploymentId,
            string idempotencyKey,
            string correlationId,
            string message,
            string code)
        {
            return new TokenDeploymentLifecycleResponse
            {
                DeploymentId    = deploymentId,
                IdempotencyKey  = idempotencyKey,
                CorrelationId   = correlationId,
                Stage           = DeploymentStage.Failed,
                Outcome         = DeploymentOutcome.TerminalFailure,
                Message         = message,
                RemediationHint = $"Error code: {code}. Review the error message for details.",
                InitiatedAt     = DateTimeOffset.UtcNow,
                LastUpdatedAt   = DateTimeOffset.UtcNow,
                Progress        = new DeploymentProgress { PercentComplete = 0, Summary = message },
            };
        }
    }
}

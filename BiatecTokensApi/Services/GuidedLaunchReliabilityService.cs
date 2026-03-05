using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.GuidedLaunchReliability;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implements guided token launch reliability: stage-based state machine, idempotency,
    /// compliance UX with what/why/how messaging, and deterministic CI behaviour.
    /// </summary>
    public class GuidedLaunchReliabilityService : IGuidedLaunchReliabilityService
    {
        private readonly ILogger<GuidedLaunchReliabilityService> _logger;

        private readonly Dictionary<string, LaunchRecord> _launchStore = new();
        private readonly Dictionary<string, string> _idempotencyIndex = new(); // idempotencyKey → launchId
        private readonly List<GuidedLaunchAuditEvent> _auditLog = new();
        private readonly object _lock = new();

        // Valid stage transitions
        private static readonly Dictionary<LaunchStage, List<LaunchStage>> ValidTransitions =
            new()
            {
                { LaunchStage.NotStarted,       new() { LaunchStage.TokenDetails } },
                { LaunchStage.TokenDetails,     new() { LaunchStage.ComplianceSetup, LaunchStage.Cancelled } },
                { LaunchStage.ComplianceSetup,  new() { LaunchStage.NetworkSelection, LaunchStage.Cancelled } },
                { LaunchStage.NetworkSelection, new() { LaunchStage.Review, LaunchStage.Cancelled } },
                { LaunchStage.Review,           new() { LaunchStage.Submitted, LaunchStage.Cancelled } },
                { LaunchStage.Submitted,        new() { LaunchStage.Completed, LaunchStage.Failed, LaunchStage.Cancelled } },
                { LaunchStage.Completed,        new List<LaunchStage>() },
                { LaunchStage.Cancelled,        new List<LaunchStage>() },
                { LaunchStage.Failed,           new() { LaunchStage.TokenDetails, LaunchStage.Cancelled } }
            };

        // Known valid token standards
        private static readonly HashSet<string> KnownStandards = new(StringComparer.OrdinalIgnoreCase)
        {
            "ASA", "ARC3", "ARC200", "ERC20", "ARC1400"
        };

        // Known valid step names
        private static readonly HashSet<string> KnownStepNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "token-details", "compliance-setup", "network-selection", "review"
        };

        private sealed class LaunchRecord
        {
            public string LaunchId { get; init; } = string.Empty;
            public string? TokenName { get; init; }
            public string? TokenStandard { get; init; }
            public string? OwnerId { get; init; }
            public string? IdempotencyKey { get; init; }
            public string? CorrelationId { get; init; }
            public LaunchStage Stage { get; set; } = LaunchStage.NotStarted;
            public List<string> CompletedSteps { get; } = new();
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }

        /// <summary>Initialises a new instance of <see cref="GuidedLaunchReliabilityService"/>.</summary>
        public GuidedLaunchReliabilityService(ILogger<GuidedLaunchReliabilityService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<GuidedLaunchInitiateResponse> InitiateLaunchAsync(GuidedLaunchInitiateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TokenName))
                return Task.FromResult(Fail<GuidedLaunchInitiateResponse>("MISSING_TOKEN_NAME",
                    "Token name is required.",
                    "Please provide a token name before continuing.",
                    request.CorrelationId));

            if (string.IsNullOrWhiteSpace(request.TokenStandard))
                return Task.FromResult(Fail<GuidedLaunchInitiateResponse>("MISSING_TOKEN_STANDARD",
                    "Token standard is required.",
                    "Choose a token standard such as ASA, ARC3, ARC200, or ERC20.",
                    request.CorrelationId));

            if (!KnownStandards.Contains(request.TokenStandard))
                return Task.FromResult(Fail<GuidedLaunchInitiateResponse>("INVALID_TOKEN_STANDARD",
                    $"Token standard '{LoggingHelper.SanitizeLogInput(request.TokenStandard)}' is not supported.",
                    "Supported standards: ASA, ARC3, ARC200, ERC20, ARC1400.",
                    request.CorrelationId));

            if (string.IsNullOrWhiteSpace(request.OwnerId))
                return Task.FromResult(Fail<GuidedLaunchInitiateResponse>("MISSING_OWNER_ID",
                    "Owner ID is required.",
                    "Please provide a valid owner ID to initiate a launch.",
                    request.CorrelationId));

            lock (_lock)
            {
                // Idempotency check
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                    _idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingId) &&
                    _launchStore.TryGetValue(existingId, out var existingRecord))
                {
                    _logger.LogInformation("Guided launch idempotent replay for key {Key}", LoggingHelper.SanitizeLogInput(request.IdempotencyKey));
                    return Task.FromResult(new GuidedLaunchInitiateResponse
                    {
                        Success = true,
                        LaunchId = existingRecord.LaunchId,
                        Stage = existingRecord.Stage,
                        NextAction = GetNextAction(existingRecord.Stage),
                        IsIdempotentReplay = true,
                        CorrelationId = request.CorrelationId ?? existingRecord.CorrelationId,
                        SchemaVersion = "1.0.0"
                    });
                }

                var launchId = Guid.NewGuid().ToString();
                var record = new LaunchRecord
                {
                    LaunchId = launchId,
                    TokenName = request.TokenName,
                    TokenStandard = request.TokenStandard,
                    OwnerId = request.OwnerId,
                    IdempotencyKey = request.IdempotencyKey,
                    CorrelationId = request.CorrelationId,
                    Stage = LaunchStage.TokenDetails
                };

                _launchStore[launchId] = record;
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    _idempotencyIndex[request.IdempotencyKey] = launchId;

                Audit("InitiateLaunch", launchId, request.CorrelationId, LaunchStage.TokenDetails, true);
                _logger.LogInformation("Guided launch initiated: {LaunchId}", launchId);

                return Task.FromResult(new GuidedLaunchInitiateResponse
                {
                    Success = true,
                    LaunchId = launchId,
                    Stage = LaunchStage.TokenDetails,
                    NextAction = GetNextAction(LaunchStage.TokenDetails),
                    IsIdempotentReplay = false,
                    CorrelationId = request.CorrelationId,
                    SchemaVersion = "1.0.0"
                });
            }
        }

        /// <inheritdoc/>
        public Task<GuidedLaunchStatusResponse?> GetLaunchStatusAsync(string launchId, string? correlationId)
        {
            lock (_lock)
            {
                if (!_launchStore.TryGetValue(launchId, out var record))
                    return Task.FromResult<GuidedLaunchStatusResponse?>(null);

                return Task.FromResult<GuidedLaunchStatusResponse?>(new GuidedLaunchStatusResponse
                {
                    Success = true,
                    LaunchId = record.LaunchId,
                    TokenName = record.TokenName,
                    TokenStandard = record.TokenStandard,
                    OwnerId = record.OwnerId,
                    Stage = record.Stage,
                    NextAction = GetNextAction(record.Stage),
                    ComplianceMessages = GetComplianceMessages(record.Stage),
                    CompletedSteps = new List<string>(record.CompletedSteps),
                    CorrelationId = correlationId ?? record.CorrelationId,
                    SchemaVersion = "1.0.0",
                    CreatedAt = record.CreatedAt,
                    UpdatedAt = record.UpdatedAt
                });
            }
        }

        /// <inheritdoc/>
        public Task<GuidedLaunchAdvanceResponse> AdvanceLaunchAsync(GuidedLaunchAdvanceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LaunchId))
                return Task.FromResult(Fail<GuidedLaunchAdvanceResponse>("MISSING_LAUNCH_ID",
                    "Launch ID is required to advance the workflow.",
                    "Please provide the launch ID returned when you initiated the launch.",
                    request.CorrelationId));

            lock (_lock)
            {
                if (!_launchStore.TryGetValue(request.LaunchId, out var record))
                    return Task.FromResult(Fail<GuidedLaunchAdvanceResponse>("LAUNCH_NOT_FOUND",
                        "No guided launch was found with the provided ID.",
                        "Check the launch ID and try again, or start a new guided launch.",
                        request.CorrelationId));

                var prev = record.Stage;

                if (prev is LaunchStage.Completed or LaunchStage.Cancelled)
                    return Task.FromResult(Fail<GuidedLaunchAdvanceResponse>("LAUNCH_TERMINAL",
                        $"This launch is already in a terminal state: {prev}.",
                        "Start a new guided launch to create another token.",
                        request.CorrelationId));

                if (!ValidTransitions.TryGetValue(prev, out var allowed) || allowed.Count == 0)
                    return Task.FromResult(Fail<GuidedLaunchAdvanceResponse>("NO_VALID_TRANSITION",
                        "There are no valid stage transitions from the current state.",
                        "Check that the launch is in an active stage before advancing.",
                        request.CorrelationId));

                // Advance to first non-cancel transition target
                var next = allowed.First(s => s != LaunchStage.Cancelled);

                record.Stage = next;
                record.UpdatedAt = DateTime.UtcNow;
                if (!record.CompletedSteps.Contains(prev.ToString()))
                    record.CompletedSteps.Add(prev.ToString());

                Audit("AdvanceLaunch", request.LaunchId, request.CorrelationId, next, true);
                _logger.LogInformation("Guided launch {LaunchId} advanced {Prev} → {Next}", request.LaunchId, prev, next);

                return Task.FromResult(new GuidedLaunchAdvanceResponse
                {
                    Success = true,
                    LaunchId = request.LaunchId,
                    PreviousStage = prev,
                    CurrentStage = next,
                    NextAction = GetNextAction(next),
                    ComplianceMessages = GetComplianceMessages(next),
                    CorrelationId = request.CorrelationId ?? record.CorrelationId,
                    SchemaVersion = "1.0.0"
                });
            }
        }

        /// <inheritdoc/>
        public Task<GuidedLaunchValidateStepResponse> ValidateStepAsync(GuidedLaunchValidateStepRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LaunchId))
                return Task.FromResult(Fail<GuidedLaunchValidateStepResponse>("MISSING_LAUNCH_ID",
                    "Launch ID is required.",
                    "Provide the launch ID from the initiation response.",
                    request.CorrelationId));

            if (string.IsNullOrWhiteSpace(request.StepName))
                return Task.FromResult(Fail<GuidedLaunchValidateStepResponse>("MISSING_STEP_NAME",
                    "Step name is required.",
                    $"Valid step names are: {string.Join(", ", KnownStepNames)}.",
                    request.CorrelationId));

            if (!KnownStepNames.Contains(request.StepName))
                return Task.FromResult(Fail<GuidedLaunchValidateStepResponse>("INVALID_STEP_NAME",
                    $"Step '{LoggingHelper.SanitizeLogInput(request.StepName)}' is not recognised.",
                    $"Valid step names are: {string.Join(", ", KnownStepNames)}.",
                    request.CorrelationId));

            lock (_lock)
            {
                if (!_launchStore.ContainsKey(request.LaunchId))
                    return Task.FromResult(Fail<GuidedLaunchValidateStepResponse>("LAUNCH_NOT_FOUND",
                        "No guided launch was found with the provided ID.",
                        "Check the launch ID and try again.",
                        request.CorrelationId));
            }

            var messages = new List<ComplianceUxMessage>();
            var isValid = true;

            // Step-specific validation
            if (request.StepName.Equals("token-details", StringComparison.OrdinalIgnoreCase))
            {
                if (request.StepInputs == null ||
                    !request.StepInputs.TryGetValue("tokenName", out var tn) ||
                    string.IsNullOrWhiteSpace(tn))
                {
                    isValid = false;
                    messages.Add(new ComplianceUxMessage
                    {
                        Severity = ComplianceMessageSeverity.Error,
                        What = "Token name is missing.",
                        Why = "Every token must have a unique, human-readable name.",
                        How = "Enter a name for your token in the Token Name field.",
                        Code = "STEP_MISSING_TOKEN_NAME"
                    });
                }
            }

            if (request.StepName.Equals("compliance-setup", StringComparison.OrdinalIgnoreCase))
            {
                if (request.StepInputs == null ||
                    !request.StepInputs.TryGetValue("jurisdiction", out var jur) ||
                    string.IsNullOrWhiteSpace(jur))
                {
                    isValid = false;
                    messages.Add(new ComplianceUxMessage
                    {
                        Severity = ComplianceMessageSeverity.Blocker,
                        What = "Jurisdiction is required.",
                        Why = "Biatec must know where your token will be offered to ensure regulatory compliance.",
                        How = "Select your primary operating jurisdiction from the dropdown.",
                        Code = "STEP_MISSING_JURISDICTION"
                    });
                }
            }

            if (request.StepName.Equals("network-selection", StringComparison.OrdinalIgnoreCase))
            {
                if (request.StepInputs == null ||
                    !request.StepInputs.TryGetValue("network", out var net) ||
                    string.IsNullOrWhiteSpace(net))
                {
                    isValid = false;
                    messages.Add(new ComplianceUxMessage
                    {
                        Severity = ComplianceMessageSeverity.Error,
                        What = "Network is required.",
                        Why = "Your token will be deployed on the selected blockchain network.",
                        How = "Choose a network: Algorand Mainnet, Algorand Testnet, or Base.",
                        Code = "STEP_MISSING_NETWORK"
                    });
                }
            }

            Audit("ValidateStep", request.LaunchId, request.CorrelationId, LaunchStage.NotStarted, isValid);

            return Task.FromResult(new GuidedLaunchValidateStepResponse
            {
                Success = true,
                LaunchId = request.LaunchId,
                StepName = request.StepName,
                IsValid = isValid,
                ValidationMessages = messages,
                CorrelationId = request.CorrelationId,
                SchemaVersion = "1.0.0"
            });
        }

        /// <inheritdoc/>
        public Task<GuidedLaunchCancelResponse> CancelLaunchAsync(GuidedLaunchCancelRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LaunchId))
                return Task.FromResult(Fail<GuidedLaunchCancelResponse>("MISSING_LAUNCH_ID",
                    "Launch ID is required.",
                    "Provide the launch ID from the initiation response.",
                    request.CorrelationId));

            lock (_lock)
            {
                if (!_launchStore.TryGetValue(request.LaunchId, out var record))
                    return Task.FromResult(Fail<GuidedLaunchCancelResponse>("LAUNCH_NOT_FOUND",
                        "No guided launch was found with the provided ID.",
                        "Check the launch ID and try again.",
                        request.CorrelationId));

                if (record.Stage is LaunchStage.Completed or LaunchStage.Cancelled)
                    return Task.FromResult(Fail<GuidedLaunchCancelResponse>("LAUNCH_TERMINAL",
                        $"Launch is already in terminal state: {record.Stage}.",
                        "A completed or already-cancelled launch cannot be cancelled again.",
                        request.CorrelationId));

                record.Stage = LaunchStage.Cancelled;
                record.UpdatedAt = DateTime.UtcNow;

                Audit("CancelLaunch", request.LaunchId, request.CorrelationId, LaunchStage.Cancelled, true);
                _logger.LogInformation("Guided launch {LaunchId} cancelled", request.LaunchId);

                return Task.FromResult(new GuidedLaunchCancelResponse
                {
                    Success = true,
                    LaunchId = request.LaunchId,
                    FinalStage = LaunchStage.Cancelled,
                    CorrelationId = request.CorrelationId ?? record.CorrelationId,
                    SchemaVersion = "1.0.0"
                });
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<GuidedLaunchAuditEvent> GetAuditEvents(string? correlationId = null)
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(correlationId))
                    return _auditLog.AsReadOnly();
                return _auditLog.Where(e => e.CorrelationId == correlationId).ToList().AsReadOnly();
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static string GetNextAction(LaunchStage stage) => stage switch
        {
            LaunchStage.NotStarted       => "Initiate the guided launch.",
            LaunchStage.TokenDetails     => "Complete token details and advance to compliance setup.",
            LaunchStage.ComplianceSetup  => "Configure compliance settings and advance to network selection.",
            LaunchStage.NetworkSelection => "Select the deployment network and advance to review.",
            LaunchStage.Review           => "Review all settings and submit your token launch.",
            LaunchStage.Submitted        => "Waiting for blockchain confirmation.",
            LaunchStage.Completed        => "Your token has been successfully launched.",
            LaunchStage.Cancelled        => "This launch has been cancelled. Start a new launch if needed.",
            LaunchStage.Failed           => "The launch failed. Correct the issues and retry from Token Details.",
            _                            => "Unknown stage."
        };

        private static List<ComplianceUxMessage> GetComplianceMessages(LaunchStage stage) => stage switch
        {
            LaunchStage.ComplianceSetup => new List<ComplianceUxMessage>
            {
                new()
                {
                    Severity = ComplianceMessageSeverity.Info,
                    What = "Compliance information is required.",
                    Why = "Biatec operates in regulated markets and must verify that your token meets applicable rules.",
                    How = "Fill in jurisdiction, KYC status, and any applicable AML flags before continuing.",
                    Code = "COMPLIANCE_REQUIRED"
                }
            },
            LaunchStage.Review => new List<ComplianceUxMessage>
            {
                new()
                {
                    Severity = ComplianceMessageSeverity.Info,
                    What = "Please review all settings carefully.",
                    Why = "Once submitted, token parameters cannot be changed without redeployment.",
                    How = "Confirm all details are correct, then click Submit to deploy.",
                    Code = "REVIEW_BEFORE_SUBMIT"
                }
            },
            LaunchStage.Failed => new List<ComplianceUxMessage>
            {
                new()
                {
                    Severity = ComplianceMessageSeverity.Error,
                    What = "The token launch failed.",
                    Why = "An error occurred during submission or on-chain processing.",
                    How = "Review the error details, correct the issue, and restart from the Token Details step.",
                    Code = "LAUNCH_FAILED"
                }
            },
            _ => new List<ComplianceUxMessage>()
        };

        private static T Fail<T>(string errorCode, string errorMessage, string remediationHint, string? correlationId)
            where T : new()
        {
            var obj = new T();
            SetProperty(obj, "Success", false);
            SetProperty(obj, "ErrorCode", errorCode);
            SetProperty(obj, "ErrorMessage", errorMessage);
            SetProperty(obj, "RemediationHint", remediationHint);
            SetProperty(obj, "CorrelationId", correlationId);
            SetProperty(obj, "SchemaVersion", "1.0.0");
            return obj;
        }

        private static void SetProperty(object obj, string name, object? value)
        {
            var prop = obj.GetType().GetProperty(name);
            prop?.SetValue(obj, value);
        }

        private void Audit(string op, string? launchId, string? correlationId, LaunchStage stage, bool success,
            string? errorCode = null)
        {
            _auditLog.Add(new GuidedLaunchAuditEvent
            {
                OperationName = op,
                LaunchId = launchId,
                CorrelationId = correlationId,
                Stage = stage,
                Success = success,
                ErrorCode = errorCode
            });
        }
    }
}

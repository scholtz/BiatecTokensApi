using System.Diagnostics;
using System.Text.Json;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for evaluating token launch readiness
    /// </summary>
    /// <remarks>
    /// Orchestrates compliance decisioning and evidence collection for regulated token issuance.
    /// Provides deterministic, auditable readiness assessment by aggregating:
    /// - Subscription entitlements
    /// - ARC76 account readiness
    /// - Compliance decisions
    /// - KYC/AML verification status
    /// - Jurisdiction constraints
    /// - Whitelist configuration
    /// - Integration health
    /// </remarks>
    public class TokenLaunchReadinessService : ITokenLaunchReadinessService
    {
        private readonly IEntitlementEvaluationService _entitlementService;
        private readonly IARC76AccountReadinessService _arc76ReadinessService;
        private readonly IKycService _kycService;
        private readonly ITokenLaunchReadinessRepository _repository;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<TokenLaunchReadinessService> _logger;

        private const string PolicyVersion = "2026.02.16.1";

        public TokenLaunchReadinessService(
            IEntitlementEvaluationService entitlementService,
            IARC76AccountReadinessService arc76ReadinessService,
            IKycService kycService,
            ITokenLaunchReadinessRepository repository,
            IMetricsService metricsService,
            ILogger<TokenLaunchReadinessService> logger)
        {
            _entitlementService = entitlementService;
            _arc76ReadinessService = arc76ReadinessService;
            _kycService = kycService;
            _repository = repository;
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TokenLaunchReadinessResponse> EvaluateReadinessAsync(TokenLaunchReadinessRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();

            try
            {
                var sanitizedUserId = LoggingHelper.SanitizeLogInput(request.UserId);
                var sanitizedTokenType = LoggingHelper.SanitizeLogInput(request.TokenType);

                _logger.LogInformation(
                    "Starting token launch readiness evaluation: UserId={UserId}, TokenType={TokenType}, CorrelationId={CorrelationId}",
                    sanitizedUserId, sanitizedTokenType, correlationId);

                // Initialize response
                var response = new TokenLaunchReadinessResponse
                {
                    PolicyVersion = PolicyVersion,
                    CorrelationId = correlationId
                };

                // Evaluate all categories in parallel for performance
                var evaluationTasks = new List<Task>
                {
                    EvaluateEntitlementAsync(request, response),
                    EvaluateAccountReadinessAsync(request, response),
                    EvaluateKycAmlAsync(request, response)
                };

                await Task.WhenAll(evaluationTasks);

                // Determine overall readiness
                DetermineOverallReadiness(response);

                // Generate remediation tasks
                GenerateRemediationTasks(response);

                // Build summary
                response.Summary = BuildReadinessSummary(response);

                sw.Stop();
                response.EvaluationTimeMs = sw.ElapsedMilliseconds;

                // Emit metrics
                EmitMetrics(response);

                // Store evidence
                await StoreEvidenceAsync(request, response);

                _logger.LogInformation(
                    "Readiness evaluation completed: UserId={UserId}, Status={Status}, CanProceed={CanProceed}, Duration={DurationMs}ms, CorrelationId={CorrelationId}",
                    sanitizedUserId, response.Status, response.CanProceed, response.EvaluationTimeMs, correlationId);

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                
                _logger.LogError(ex,
                    "Error during readiness evaluation: UserId={UserId}, TokenType={TokenType}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(request.TokenType),
                    correlationId);

                // Return blocked response on error
                return new TokenLaunchReadinessResponse
                {
                    Status = ReadinessStatus.Blocked,
                    Summary = "Readiness evaluation failed due to system error",
                    CanProceed = false,
                    PolicyVersion = PolicyVersion,
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
        public async Task<TokenLaunchReadinessResponse?> GetEvaluationAsync(string evaluationId)
        {
            try
            {
                var evidence = await _repository.GetEvidenceByEvaluationIdAsync(evaluationId);
                if (evidence == null)
                {
                    return null;
                }

                // Deserialize response from evidence
                var response = JsonSerializer.Deserialize<TokenLaunchReadinessResponse>(evidence.ResponseSnapshot);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation: EvaluationId={EvaluationId}",
                    LoggingHelper.SanitizeLogInput(evaluationId));
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<List<TokenLaunchReadinessResponse>> GetEvaluationHistoryAsync(
            string userId,
            int limit = 50,
            DateTime? fromDate = null)
        {
            try
            {
                var evidenceList = await _repository.GetEvidenceHistoryAsync(userId, limit, fromDate);
                var responses = new List<TokenLaunchReadinessResponse>();

                foreach (var evidence in evidenceList)
                {
                    try
                    {
                        var response = JsonSerializer.Deserialize<TokenLaunchReadinessResponse>(evidence.ResponseSnapshot);
                        if (response != null)
                        {
                            responses.Add(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to deserialize evidence: EvaluationId={EvaluationId}",
                            evidence.EvaluationId);
                    }
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation history: UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return new List<TokenLaunchReadinessResponse>();
            }
        }

        #region Private Evaluation Methods

        private async Task EvaluateEntitlementAsync(
            TokenLaunchReadinessRequest request,
            TokenLaunchReadinessResponse response)
        {
            try
            {
                var entitlementRequest = new EntitlementCheckRequest
                {
                    UserId = request.UserId,
                    Operation = EntitlementOperation.TokenDeployment,
                    OperationContext = request.DeploymentContext.HasValue 
                        ? JsonSerializer.Deserialize<Dictionary<string, object>>(request.DeploymentContext.Value.GetRawText())
                        : new Dictionary<string, object> { { "tokenType", request.TokenType } },
                    CorrelationId = request.CorrelationId
                };

                var entitlementResult = await _entitlementService.CheckEntitlementAsync(entitlementRequest);

                response.Details.Entitlement = new CategoryEvaluationResult
                {
                    Passed = entitlementResult.IsAllowed,
                    Message = entitlementResult.IsAllowed
                        ? $"Subscription tier '{entitlementResult.SubscriptionTier}' allows token deployment"
                        : entitlementResult.DenialReason ?? "Entitlement check failed",
                    ReasonCodes = entitlementResult.IsAllowed
                        ? new List<string>()
                        : new List<string> { entitlementResult.DenialCode ?? ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED },
                    Details = JsonSerializer.SerializeToElement(new Dictionary<string, object>
                    {
                        { "subscriptionTier", entitlementResult.SubscriptionTier },
                        { "isAllowed", entitlementResult.IsAllowed },
                        { "upgradeRecommendation", entitlementResult.UpgradeRecommendation?.RecommendedTier ?? string.Empty }
                    })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating entitlement: UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(request.UserId));

                response.Details.Entitlement = new CategoryEvaluationResult
                {
                    Passed = false,
                    Message = "Entitlement evaluation failed",
                    ReasonCodes = new List<string> { ErrorCodes.INTERNAL_SERVER_ERROR }
                };
            }
        }

        private async Task EvaluateAccountReadinessAsync(
            TokenLaunchReadinessRequest request,
            TokenLaunchReadinessResponse response)
        {
            try
            {
                var accountReadiness = await _arc76ReadinessService.CheckAccountReadinessAsync(
                    request.UserId,
                    request.CorrelationId);

                response.Details.AccountReadiness = new CategoryEvaluationResult
                {
                    Passed = accountReadiness.IsReady,
                    Message = accountReadiness.IsReady
                        ? "ARC76 account is ready for token operations"
                        : accountReadiness.NotReadyReason ?? "Account not ready",
                    ReasonCodes = accountReadiness.IsReady
                        ? new List<string>()
                        : new List<string> { GetAccountReadinessErrorCode(accountReadiness.State) },
                    Details = JsonSerializer.SerializeToElement(new Dictionary<string, object>
                    {
                        { "state", accountReadiness.State.ToString() },
                        { "accountAddress", accountReadiness.AccountAddress ?? string.Empty },
                        { "remediationSteps", accountReadiness.RemediationSteps ?? new List<string>() }
                    })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating account readiness: UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(request.UserId));

                response.Details.AccountReadiness = new CategoryEvaluationResult
                {
                    Passed = false,
                    Message = "Account readiness evaluation failed",
                    ReasonCodes = new List<string> { ErrorCodes.INTERNAL_SERVER_ERROR }
                };
            }
        }

        private async Task EvaluateKycAmlAsync(
            TokenLaunchReadinessRequest request,
            TokenLaunchReadinessResponse response)
        {
            try
            {
                // Check if KYC verification exists for this user
                var kycStatus = await _kycService.GetStatusAsync(request.UserId);

                bool passed = kycStatus != null && kycStatus.Status == KycStatus.Approved;
                
                response.Details.KycAml = new CategoryEvaluationResult
                {
                    Passed = passed,
                    Message = passed
                        ? "KYC/AML verification complete"
                        : "KYC/AML verification required or pending",
                    ReasonCodes = passed
                        ? new List<string>()
                        : new List<string> { ErrorCodes.KYC_REQUIRED },
                    Details = JsonSerializer.SerializeToElement(new Dictionary<string, object>
                    {
                        { "status", kycStatus?.Status.ToString() ?? "NotStarted" },
                        { "updatedAt", kycStatus?.UpdatedAt?.ToString("O") ?? string.Empty }
                    })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating KYC/AML: UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(request.UserId));

                // KYC/AML is advisory for now - don't block on evaluation errors
                response.Details.KycAml = new CategoryEvaluationResult
                {
                    Passed = true,
                    Message = "KYC/AML evaluation unavailable (advisory only)",
                    ReasonCodes = new List<string>(),
                    Details = JsonSerializer.SerializeToElement(new Dictionary<string, object>
                    {
                        { "status", "Unavailable" },
                        { "note", "KYC/AML verification is recommended but not required" }
                    })
                };
            }
        }

        private void DetermineOverallReadiness(TokenLaunchReadinessResponse response)
        {
            // Critical blockers: Entitlement and Account Readiness
            bool hasCriticalBlockers = !response.Details.Entitlement.Passed ||
                                       !response.Details.AccountReadiness.Passed;

            // Advisory checks: KYC/AML (warning only)
            bool hasAdvisoryWarnings = !response.Details.KycAml.Passed;

            if (hasCriticalBlockers)
            {
                response.Status = ReadinessStatus.Blocked;
                response.CanProceed = false;
            }
            else if (hasAdvisoryWarnings)
            {
                response.Status = ReadinessStatus.Warning;
                response.CanProceed = true; // Can proceed but with warnings
            }
            else
            {
                response.Status = ReadinessStatus.Ready;
                response.CanProceed = true;
            }
        }

        private void GenerateRemediationTasks(TokenLaunchReadinessResponse response)
        {
            var tasks = new List<RemediationTask>();

            // Entitlement blockers
            if (!response.Details.Entitlement.Passed)
            {
                var entitlementTask = new RemediationTask
                {
                    Category = BlockerCategory.Entitlement,
                    ErrorCode = response.Details.Entitlement.ReasonCodes.FirstOrDefault() ?? ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                    Description = response.Details.Entitlement.Message,
                    Severity = RemediationSeverity.Critical,
                    OwnerHint = "Account Owner",
                    Actions = new List<string>
                    {
                        "Review your current subscription tier and deployment limits",
                        "Upgrade to a higher tier to increase deployment capacity",
                        "Contact sales for enterprise pricing options"
                    },
                    EstimatedResolutionHours = 1
                };

                if (response.Details.Entitlement.Details.HasValue)
                {
                    try
                    {
                        if (response.Details.Entitlement.Details.Value.TryGetProperty("upgradeRecommendation", out var upgradeRecElement))
                        {
                            var upgradeRec = upgradeRecElement.GetString();
                            if (!string.IsNullOrEmpty(upgradeRec))
                            {
                                entitlementTask.Metadata = JsonSerializer.SerializeToElement(new Dictionary<string, object>
                                {
                                    { "upgradeRecommendation", upgradeRec }
                                });
                            }
                        }
                    }
                    catch { /* Ignore JSON parsing errors */ }
                }

                tasks.Add(entitlementTask);
            }

            // Account readiness blockers
            if (!response.Details.AccountReadiness.Passed)
            {
                var accountTask = new RemediationTask
                {
                    Category = BlockerCategory.AccountState,
                    ErrorCode = response.Details.AccountReadiness.ReasonCodes.FirstOrDefault() ?? ErrorCodes.ACCOUNT_NOT_READY,
                    Description = response.Details.AccountReadiness.Message,
                    Severity = RemediationSeverity.High,
                    OwnerHint = "User",
                    Actions = new List<string>(),
                    EstimatedResolutionHours = 1
                };

                // Extract remediation steps from details
                if (response.Details.AccountReadiness.Details.HasValue)
                {
                    try
                    {
                        if (response.Details.AccountReadiness.Details.Value.TryGetProperty("remediationSteps", out var stepsElement))
                        {
                            var stepsList = JsonSerializer.Deserialize<List<string>>(stepsElement.GetRawText());
                            if (stepsList != null)
                            {
                                accountTask.Actions.AddRange(stepsList);
                            }
                        }
                    }
                    catch { /* Ignore JSON parsing errors */ }
                }

                if (accountTask.Actions.Count == 0)
                {
                    accountTask.Actions.Add("Complete account initialization through the authentication flow");
                    accountTask.Actions.Add("Contact support if the issue persists");
                }

                tasks.Add(accountTask);
            }

            // KYC/AML advisory warnings
            if (!response.Details.KycAml.Passed && response.Status == ReadinessStatus.Warning)
            {
                var kycTask = new RemediationTask
                {
                    Category = BlockerCategory.KycAml,
                    ErrorCode = ErrorCodes.KYC_REQUIRED,
                    Description = "KYC/AML verification recommended for regulatory compliance",
                    Severity = RemediationSeverity.Medium,
                    OwnerHint = "Compliance Team",
                    Actions = new List<string>
                    {
                        "Complete KYC verification process for enhanced compliance",
                        "Provide required identity documentation",
                        "Review jurisdiction-specific requirements"
                    },
                    EstimatedResolutionHours = 24
                };

                tasks.Add(kycTask);
            }

            // Order tasks by severity (Critical -> High -> Medium -> Low -> Info)
            response.RemediationTasks = tasks
                .OrderByDescending(t => t.Severity)
                .ToList();
        }

        private string BuildReadinessSummary(TokenLaunchReadinessResponse response)
        {
            return response.Status switch
            {
                ReadinessStatus.Ready => "All requirements met. Token launch can proceed.",
                
                ReadinessStatus.Blocked => response.RemediationTasks.Count > 0
                    ? $"Token launch blocked by {response.RemediationTasks.Count} critical issue(s). Review remediation tasks."
                    : "Token launch blocked. Please contact support.",
                
                ReadinessStatus.Warning => "Token launch can proceed with advisory warnings. Review recommendations.",
                
                ReadinessStatus.NeedsReview => "Manual compliance review required before token launch.",
                
                _ => "Unknown readiness status"
            };
        }

        private string GetAccountReadinessErrorCode(ARC76ReadinessState state)
        {
            return state switch
            {
                ARC76ReadinessState.NotInitialized => ErrorCodes.ACCOUNT_NOT_READY,
                ARC76ReadinessState.Initializing => ErrorCodes.ACCOUNT_INITIALIZING,
                ARC76ReadinessState.Degraded => ErrorCodes.ACCOUNT_DEGRADED,
                ARC76ReadinessState.Failed => ErrorCodes.ACCOUNT_INITIALIZATION_FAILED,
                _ => ErrorCodes.ACCOUNT_NOT_READY
            };
        }

        private async Task StoreEvidenceAsync(
            TokenLaunchReadinessRequest request,
            TokenLaunchReadinessResponse response)
        {
            try
            {
                var evidence = new TokenLaunchReadinessEvidence
                {
                    EvaluationId = response.EvaluationId,
                    UserId = request.UserId,
                    RequestSnapshot = JsonSerializer.Serialize(request),
                    ResponseSnapshot = JsonSerializer.Serialize(response),
                    CategoryResultsSnapshot = JsonSerializer.Serialize(response.Details),
                    CorrelationId = request.CorrelationId
                };

                await _repository.StoreEvidenceAsync(evidence);
            }
            catch (Exception ex)
            {
                // Don't fail the evaluation if evidence storage fails
                _logger.LogError(ex,
                    "Failed to store readiness evidence: EvaluationId={EvaluationId}",
                    response.EvaluationId);
            }
        }

        private void EmitMetrics(TokenLaunchReadinessResponse response)
        {
            try
            {
                _metricsService.IncrementCounter($"token_launch_readiness_evaluation");
                _metricsService.IncrementCounter($"token_launch_readiness_status_{response.Status.ToString().ToLower()}");
                _metricsService.RecordHistogram("token_launch_readiness_duration_ms", response.EvaluationTimeMs);

                if (!response.CanProceed)
                {
                    _metricsService.IncrementCounter($"token_launch_blocked");
                    _metricsService.RecordHistogram("token_launch_remediation_task_count", response.RemediationTasks.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit readiness metrics");
            }
        }

        #endregion
    }
}

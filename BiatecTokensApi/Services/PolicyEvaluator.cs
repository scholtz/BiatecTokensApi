using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for evaluating compliance policies against onboarding steps
    /// </summary>
    /// <remarks>
    /// Implements policy-driven compliance decision logic for wallet-free enterprise onboarding.
    /// Evaluates policy rules and determines approval/rejection based on evidence and context.
    /// </remarks>
    public class PolicyEvaluator : IPolicyEvaluator
    {
        private readonly ILogger<PolicyEvaluator> _logger;
        private readonly PolicyConfiguration _policyConfiguration;
        private readonly PolicyMetrics _metrics;
        private readonly object _metricsLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="PolicyEvaluator"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public PolicyEvaluator(ILogger<PolicyEvaluator> logger)
        {
            _logger = logger;
            _policyConfiguration = InitializePolicyConfiguration();
            _metrics = new PolicyMetrics
            {
                PolicyVersion = _policyConfiguration.Version
            };
        }

        /// <summary>
        /// Evaluates compliance policies for a given context
        /// </summary>
        public async Task<PolicyEvaluationResult> EvaluateAsync(PolicyEvaluationContext context)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation(
                    "Starting policy evaluation: OrganizationId={OrganizationId}, Step={Step}, SessionId={SessionId}",
                    context.OrganizationId,
                    context.Step,
                    context.OnboardingSessionId ?? "N/A"
                );

                // Get applicable rules for the step
                var rules = await GetApplicableRulesAsync(context.Step);
                
                if (rules.Count == 0)
                {
                    _logger.LogWarning(
                        "No policy rules found for step: {Step}",
                        context.Step
                    );
                    
                    return new PolicyEvaluationResult
                    {
                        Outcome = DecisionOutcome.RequiresManualReview,
                        Reason = $"No policy rules configured for step: {context.Step}",
                        RequiredActions = new List<string> { "Contact compliance team for manual review" }
                    };
                }

                // Evaluate each rule
                var ruleEvaluations = new List<PolicyRuleEvaluation>();
                var failedRequiredRules = new List<PolicyRuleEvaluation>();
                var warnings = new List<PolicyRuleEvaluation>();

                foreach (var rule in rules)
                {
                    var evaluation = await EvaluateRuleAsync(rule, context);
                    ruleEvaluations.Add(evaluation);

                    if (!evaluation.Passed)
                    {
                        if (rule.IsRequired && rule.Severity >= RuleSeverity.Error)
                        {
                            failedRequiredRules.Add(evaluation);
                        }
                        else if (rule.Severity <= RuleSeverity.Warning)
                        {
                            warnings.Add(evaluation);
                        }
                    }
                }

                // Determine overall outcome
                var result = DetermineOutcome(ruleEvaluations, failedRequiredRules, warnings, context);
                
                // Update metrics
                UpdateMetrics(result, startTime);

                _logger.LogInformation(
                    "Policy evaluation completed: OrganizationId={OrganizationId}, Step={Step}, Outcome={Outcome}",
                    context.OrganizationId,
                    context.Step,
                    result.Outcome
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during policy evaluation: OrganizationId={OrganizationId}, Step={Step}",
                    context.OrganizationId,
                    context.Step
                );

                return new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.RequiresManualReview,
                    Reason = "Policy evaluation failed due to system error. Manual review required.",
                    RequiredActions = new List<string> { "Contact support for assistance" }
                };
            }
        }

        /// <summary>
        /// Gets the policy rules applicable to a specific step
        /// </summary>
        public Task<List<PolicyRule>> GetApplicableRulesAsync(OnboardingStep step)
        {
            if (_policyConfiguration.RulesByStep.TryGetValue(step, out var rules))
            {
                // Filter to only active rules
                var activeRules = rules
                    .Where(r => r.IsActive && 
                               r.EffectiveFrom <= DateTime.UtcNow &&
                               (r.EffectiveTo == null || r.EffectiveTo > DateTime.UtcNow))
                    .ToList();
                
                return Task.FromResult(activeRules);
            }

            return Task.FromResult(new List<PolicyRule>());
        }

        /// <summary>
        /// Gets the current policy configuration
        /// </summary>
        public Task<PolicyConfiguration> GetPolicyConfigurationAsync()
        {
            return Task.FromResult(_policyConfiguration);
        }

        /// <summary>
        /// Gets policy metrics for monitoring
        /// </summary>
        public Task<PolicyMetrics> GetMetricsAsync()
        {
            lock (_metricsLock)
            {
                return Task.FromResult(_metrics);
            }
        }

        /// <summary>
        /// Evaluates a single policy rule
        /// </summary>
        private async Task<PolicyRuleEvaluation> EvaluateRuleAsync(PolicyRule rule, PolicyEvaluationContext context)
        {
            try
            {
                // Check if required evidence is provided
                var missingEvidence = new List<string>();
                foreach (var requiredType in rule.RequiredEvidenceTypes)
                {
                    var hasEvidence = context.Evidence.Any(e => 
                        e.EvidenceType.Equals(requiredType, StringComparison.OrdinalIgnoreCase) &&
                        e.VerificationStatus == EvidenceVerificationStatus.Verified
                    );

                    if (!hasEvidence)
                    {
                        missingEvidence.Add(requiredType);
                    }
                }

                if (missingEvidence.Any())
                {
                    return new PolicyRuleEvaluation
                    {
                        RuleId = rule.RuleId,
                        RuleName = rule.RuleName,
                        Passed = false,
                        Message = $"{rule.FailMessage}. Missing required evidence: {string.Join(", ", missingEvidence)}",
                        Severity = rule.Severity,
                        EvidenceIds = context.Evidence.Select(e => e.ReferenceId).ToList()
                    };
                }

                // Apply specific rule logic based on rule ID
                var passed = await ApplyRuleLogicAsync(rule, context);

                return new PolicyRuleEvaluation
                {
                    RuleId = rule.RuleId,
                    RuleName = rule.RuleName,
                    Passed = passed,
                    Message = passed ? rule.PassMessage : rule.FailMessage,
                    Severity = passed ? null : rule.Severity,
                    EvidenceIds = context.Evidence.Select(e => e.ReferenceId).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule: {RuleId}", rule.RuleId);
                
                return new PolicyRuleEvaluation
                {
                    RuleId = rule.RuleId,
                    RuleName = rule.RuleName,
                    Passed = false,
                    Message = "Rule evaluation failed due to system error",
                    Severity = RuleSeverity.Error
                };
            }
        }

        /// <summary>
        /// Applies specific business logic for a policy rule
        /// </summary>
        private Task<bool> ApplyRuleLogicAsync(PolicyRule rule, PolicyEvaluationContext context)
        {
            // This is where custom rule logic would be implemented
            // For MVP, we check evidence presence and verification status
            
            // All required evidence types must be present and verified
            foreach (var requiredType in rule.RequiredEvidenceTypes)
            {
                var evidence = context.Evidence.FirstOrDefault(e => 
                    e.EvidenceType.Equals(requiredType, StringComparison.OrdinalIgnoreCase)
                );

                if (evidence == null || evidence.VerificationStatus != EvidenceVerificationStatus.Verified)
                {
                    return Task.FromResult(false);
                }
            }

            // Additional rule-specific logic can be added here based on rule.Configuration
            return Task.FromResult(true);
        }

        /// <summary>
        /// Determines the overall outcome based on rule evaluations
        /// </summary>
        private PolicyEvaluationResult DetermineOutcome(
            List<PolicyRuleEvaluation> allEvaluations,
            List<PolicyRuleEvaluation> failedRequiredRules,
            List<PolicyRuleEvaluation> warnings,
            PolicyEvaluationContext context)
        {
            // If any required rules failed, reject
            if (failedRequiredRules.Any())
            {
                var failedRuleNames = failedRequiredRules.Select(r => r.RuleName).ToList();
                var requiredActions = failedRequiredRules
                    .SelectMany(r => GetRemediationActions(r.RuleId))
                    .Distinct()
                    .ToList();

                return new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.Rejected,
                    RuleEvaluations = allEvaluations,
                    Reason = $"Failed required compliance checks: {string.Join(", ", failedRuleNames)}",
                    RequiredActions = requiredActions,
                    EstimatedResolutionTime = EstimateResolutionTime(failedRequiredRules)
                };
            }

            // If there are warnings but no critical failures, conditional approval
            if (warnings.Any())
            {
                var warningRuleNames = warnings.Select(r => r.RuleName).ToList();
                var requiredActions = warnings
                    .SelectMany(r => GetRemediationActions(r.RuleId))
                    .Distinct()
                    .ToList();

                return new PolicyEvaluationResult
                {
                    Outcome = DecisionOutcome.ConditionalApproval,
                    RuleEvaluations = allEvaluations,
                    Reason = $"Approved with conditions. Address warnings: {string.Join(", ", warningRuleNames)}",
                    RequiredActions = requiredActions,
                    EstimatedResolutionTime = EstimateResolutionTime(warnings)
                };
            }

            // All rules passed
            return new PolicyEvaluationResult
            {
                Outcome = DecisionOutcome.Approved,
                RuleEvaluations = allEvaluations,
                Reason = $"All compliance requirements met for {context.Step}",
                RequiredActions = new List<string>()
            };
        }

        /// <summary>
        /// Gets remediation actions for a failed rule
        /// </summary>
        private List<string> GetRemediationActions(string ruleId)
        {
            // Find the rule in configuration
            foreach (var stepRules in _policyConfiguration.RulesByStep.Values)
            {
                var rule = stepRules.FirstOrDefault(r => r.RuleId == ruleId);
                if (rule != null)
                {
                    return rule.RemediationActions;
                }
            }

            return new List<string> { "Contact compliance team for guidance" };
        }

        /// <summary>
        /// Estimates resolution time based on failed rules
        /// </summary>
        private string EstimateResolutionTime(List<PolicyRuleEvaluation> failedRules)
        {
            // Get the maximum estimated remediation time
            var maxHours = 0;
            foreach (var evaluation in failedRules)
            {
                foreach (var stepRules in _policyConfiguration.RulesByStep.Values)
                {
                    var rule = stepRules.FirstOrDefault(r => r.RuleId == evaluation.RuleId);
                    if (rule?.EstimatedRemediationHours != null && rule.EstimatedRemediationHours > maxHours)
                    {
                        maxHours = rule.EstimatedRemediationHours.Value;
                    }
                }
            }

            if (maxHours == 0) return "Contact compliance team for estimate";
            if (maxHours < 24) return $"{maxHours} hours";
            if (maxHours < 168) return $"{maxHours / 24} days";
            return $"{maxHours / 168} weeks";
        }

        /// <summary>
        /// Updates policy metrics
        /// </summary>
        private void UpdateMetrics(PolicyEvaluationResult result, DateTime startTime)
        {
            lock (_metricsLock)
            {
                _metrics.TotalEvaluations++;
                var evaluationTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                // Update average evaluation time
                var totalTime = _metrics.AverageEvaluationTimeMs * (_metrics.TotalEvaluations - 1) + evaluationTimeMs;
                _metrics.AverageEvaluationTimeMs = totalTime / _metrics.TotalEvaluations;

                // Update outcome counts
                switch (result.Outcome)
                {
                    case DecisionOutcome.Approved:
                        _metrics.AutomaticApprovals++;
                        break;
                    case DecisionOutcome.Rejected:
                        _metrics.AutomaticRejections++;
                        break;
                    case DecisionOutcome.RequiresManualReview:
                    case DecisionOutcome.ConditionalApproval:
                        _metrics.ManualReviewRequired++;
                        break;
                }

                // Update rule failure counts
                foreach (var evaluation in result.RuleEvaluations.Where(e => !e.Passed))
                {
                    if (!_metrics.RuleFailureCounts.ContainsKey(evaluation.RuleId))
                    {
                        _metrics.RuleFailureCounts[evaluation.RuleId] = 0;
                    }
                    _metrics.RuleFailureCounts[evaluation.RuleId]++;
                }

                _metrics.LastUpdated = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Initializes the policy configuration with default rules
        /// </summary>
        private PolicyConfiguration InitializePolicyConfiguration()
        {
            var config = new PolicyConfiguration
            {
                Version = "1.0.0",
                DefaultExpirationDays = 365,
                DefaultRequiresReview = true,
                DefaultReviewIntervalDays = 180,
                AllowConditionalApprovals = true,
                MaxEvidencePerDecision = 50,
                EnableAutomaticEvaluation = true
            };

            // Organization Identity Verification rules
            config.RulesByStep[OnboardingStep.OrganizationIdentityVerification] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.OrgIdentityDocumentRequired,
                    RuleName = "Organization Identity Document",
                    Description = "Organization must provide valid identity documentation",
                    ApplicableStep = OnboardingStep.OrganizationIdentityVerification,
                    Category = "IDENTITY",
                    Severity = RuleSeverity.Error,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "ORG_REGISTRATION_CERT", "TAX_ID_DOCUMENT" },
                    PassMessage = "Organization identity documentation verified",
                    FailMessage = "Missing or invalid organization identity documentation",
                    RemediationActions = new List<string>
                    {
                        "Upload organization registration certificate",
                        "Upload tax identification document"
                    },
                    EstimatedRemediationHours = 2,
                    RegulatoryFrameworks = new List<string> { "MICA", "AML5" }
                }
            };

            // Business Registration rules
            config.RulesByStep[OnboardingStep.BusinessRegistrationVerification] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.BusinessLicenseRequired,
                    RuleName = "Business License Verification",
                    Description = "Valid business license must be provided",
                    ApplicableStep = OnboardingStep.BusinessRegistrationVerification,
                    Category = "BUSINESS",
                    Severity = RuleSeverity.Error,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "BUSINESS_LICENSE" },
                    PassMessage = "Business license verified",
                    FailMessage = "Missing or invalid business license",
                    RemediationActions = new List<string> { "Upload valid business license" },
                    EstimatedRemediationHours = 24,
                    RegulatoryFrameworks = new List<string> { "MICA" }
                }
            };

            // KYC/KYB rules
            config.RulesByStep[OnboardingStep.KycKybVerification] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.KycDocumentationComplete,
                    RuleName = "KYC Documentation Complete",
                    Description = "Know Your Customer documentation must be complete",
                    ApplicableStep = OnboardingStep.KycKybVerification,
                    Category = "KYC",
                    Severity = RuleSeverity.Error,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "KYC_REPORT" },
                    PassMessage = "KYC documentation complete and verified",
                    FailMessage = "Incomplete or invalid KYC documentation",
                    RemediationActions = new List<string> { "Complete KYC verification process" },
                    EstimatedRemediationHours = 48,
                    RegulatoryFrameworks = new List<string> { "MICA", "AML5", "FATF" }
                }
            };

            // AML Screening rules
            config.RulesByStep[OnboardingStep.AmlScreening] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.AmlScreeningPassed,
                    RuleName = "AML Screening Passed",
                    Description = "Anti-Money Laundering screening must pass",
                    ApplicableStep = OnboardingStep.AmlScreening,
                    Category = "AML",
                    Severity = RuleSeverity.Critical,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "AML_REPORT" },
                    PassMessage = "AML screening passed successfully",
                    FailMessage = "AML screening failed or flagged concerns",
                    RemediationActions = new List<string> { "Contact compliance team for AML review" },
                    EstimatedRemediationHours = 72,
                    RegulatoryFrameworks = new List<string> { "MICA", "AML5", "FATF" }
                }
            };

            // Token Issuance rules
            config.RulesByStep[OnboardingStep.TokenIssuanceAuthorization] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.TokenTypeAllowed,
                    RuleName = "Token Type Allowed",
                    Description = "Token type must be allowed for this organization",
                    ApplicableStep = OnboardingStep.TokenIssuanceAuthorization,
                    Category = "TOKEN",
                    Severity = RuleSeverity.Error,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "TOKEN_SPECIFICATION" },
                    PassMessage = "Token type is approved for issuance",
                    FailMessage = "Token type is not allowed or requires additional approval",
                    RemediationActions = new List<string> { "Submit token specification for review" },
                    EstimatedRemediationHours = 24,
                    RegulatoryFrameworks = new List<string> { "MICA" }
                }
            };

            // Terms Acceptance rules
            config.RulesByStep[OnboardingStep.TermsAcceptance] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.TermsAccepted,
                    RuleName = "Terms and Conditions Accepted",
                    Description = "Terms and conditions must be accepted",
                    ApplicableStep = OnboardingStep.TermsAcceptance,
                    Category = "LEGAL",
                    Severity = RuleSeverity.Error,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "TERMS_ACCEPTANCE" },
                    PassMessage = "Terms and conditions accepted",
                    FailMessage = "Terms and conditions not accepted",
                    RemediationActions = new List<string> { "Review and accept terms and conditions" },
                    EstimatedRemediationHours = 1,
                    RegulatoryFrameworks = new List<string> { "GDPR", "MICA" }
                }
            };

            // Final Approval rules
            config.RulesByStep[OnboardingStep.FinalApproval] = new List<PolicyRule>
            {
                new PolicyRule
                {
                    RuleId = StandardPolicyRuleIds.AllStepsCompleted,
                    RuleName = "All Steps Completed",
                    Description = "All onboarding steps must be completed",
                    ApplicableStep = OnboardingStep.FinalApproval,
                    Category = "ONBOARDING",
                    Severity = RuleSeverity.Error,
                    IsRequired = true,
                    RequiredEvidenceTypes = new List<string> { "ONBOARDING_COMPLETION" },
                    PassMessage = "All onboarding steps completed successfully",
                    FailMessage = "Not all onboarding steps are complete",
                    RemediationActions = new List<string> { "Complete all pending onboarding steps" },
                    EstimatedRemediationHours = 0,
                    RegulatoryFrameworks = new List<string> { "MICA" }
                }
            };

            return config;
        }
    }
}

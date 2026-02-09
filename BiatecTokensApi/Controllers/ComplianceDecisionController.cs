using BiatecTokensApi.Filters;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing compliance decisions in wallet-free enterprise onboarding
    /// </summary>
    /// <remarks>
    /// This controller implements the policy-driven compliance audit trail and decision API.
    /// It enables creating, querying, and managing compliance decisions with full audit logging.
    /// All endpoints require ARC-0014 authentication to ensure decision maker identity is tracked.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance/decisions")]
    public class ComplianceDecisionController : ControllerBase
    {
        private readonly IComplianceDecisionService _decisionService;
        private readonly IPolicyEvaluator _policyEvaluator;
        private readonly ILogger<ComplianceDecisionController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceDecisionController"/> class.
        /// </summary>
        /// <param name="decisionService">The compliance decision service</param>
        /// <param name="policyEvaluator">The policy evaluator service</param>
        /// <param name="logger">The logger instance</param>
        public ComplianceDecisionController(
            IComplianceDecisionService decisionService,
            IPolicyEvaluator policyEvaluator,
            ILogger<ComplianceDecisionController> logger)
        {
            _decisionService = decisionService;
            _policyEvaluator = policyEvaluator;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new compliance decision for an onboarding step
        /// </summary>
        /// <param name="request">The decision creation request</param>
        /// <returns>The created decision with policy evaluation results</returns>
        /// <remarks>
        /// Creates a compliance decision after evaluating the provided evidence against policy rules.
        /// 
        /// **Decision Process:**
        /// 1. Validates required evidence is provided
        /// 2. Evaluates evidence against policy rules for the specified step
        /// 3. Determines outcome (Approved, Rejected, RequiresManualReview, ConditionalApproval)
        /// 4. Creates immutable audit record with decision details
        /// 5. Returns decision with evaluation results and required actions
        /// 
        /// **Idempotency:**
        /// Creating the same decision within 1 hour (same organization, step, policy version, and evidence)
        /// will return the existing decision instead of creating a duplicate.
        /// 
        /// **Authorization:**
        /// Requires ARC-0014 authentication. The authenticated address is recorded as the decision maker.
        /// 
        /// **Example Request:**
        /// ```json
        /// {
        ///   "organizationId": "org-123",
        ///   "onboardingSessionId": "session-456",
        ///   "step": "KycKybVerification",
        ///   "evidenceReferences": [
        ///     {
        ///       "evidenceType": "KYC_REPORT",
        ///       "referenceId": "doc-789",
        ///       "verificationStatus": "Verified",
        ///       "dataHash": "sha256:abc123..."
        ///     }
        ///   ],
        ///   "expirationDays": 365,
        ///   "requiresReview": true,
        ///   "reviewIntervalDays": 180
        /// }
        /// ```
        /// 
        /// **Example Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "message": "Compliance decision created successfully",
        ///   "decision": {
        ///     "id": "dec-001",
        ///     "organizationId": "org-123",
        ///     "step": "KycKybVerification",
        ///     "outcome": "Approved",
        ///     "policyRuleIds": ["KYC_DOC_001"],
        ///     "decisionMaker": "ADDR123...",
        ///     "decisionTimestamp": "2026-02-09T20:00:00Z",
        ///     "reason": "All compliance requirements met for KycKybVerification"
        ///   },
        ///   "evaluationResult": {
        ///     "outcome": "Approved",
        ///     "ruleEvaluations": [
        ///       {
        ///         "ruleId": "KYC_DOC_001",
        ///         "ruleName": "KYC Documentation Complete",
        ///         "passed": true,
        ///         "message": "KYC documentation complete and verified"
        ///       }
        ///     ],
        ///     "reason": "All compliance requirements met for KycKybVerification",
        ///     "requiredActions": []
        ///   }
        /// }
        /// ```
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(ComplianceDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ComplianceDecisionResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateDecision([FromBody] CreateComplianceDecisionRequest request)
        {
            try
            {
                // Get authenticated user address from claims
                var actorAddress = User.FindFirst("Address")?.Value;
                if (string.IsNullOrWhiteSpace(actorAddress))
                {
                    _logger.LogWarning("Failed to extract actor address from authentication claims");
                    return Unauthorized(new ComplianceDecisionResponse
                    {
                        Success = false,
                        ErrorMessage = "Unable to identify authenticated user"
                    });
                }

                _logger.LogInformation(
                    "Creating compliance decision: OrganizationId={OrganizationId}, Step={Step}, Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    request.Step,
                    LoggingHelper.SanitizeLogInput(actorAddress)
                );

                var response = await _decisionService.CreateDecisionAsync(request, actorAddress);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Failed to create compliance decision: {ErrorMessage}",
                        LoggingHelper.SanitizeLogInput(response.ErrorMessage)
                    );
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating compliance decision");
                return StatusCode(500, new ComplianceDecisionResponse
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred while creating the compliance decision"
                });
            }
        }

        /// <summary>
        /// Gets a compliance decision by ID
        /// </summary>
        /// <param name="decisionId">The decision ID</param>
        /// <returns>The compliance decision</returns>
        /// <remarks>
        /// Retrieves a specific compliance decision by its unique identifier.
        /// Returns the complete decision record including all evidence references,
        /// policy rules evaluated, and decision metadata.
        /// 
        /// **Example Response:**
        /// ```json
        /// {
        ///   "id": "dec-001",
        ///   "organizationId": "org-123",
        ///   "onboardingSessionId": "session-456",
        ///   "step": "KycKybVerification",
        ///   "outcome": "Approved",
        ///   "policyRuleIds": ["KYC_DOC_001"],
        ///   "decisionMaker": "ADDR123...",
        ///   "decisionTimestamp": "2026-02-09T20:00:00Z",
        ///   "evidenceReferences": [...],
        ///   "reason": "All compliance requirements met",
        ///   "policyVersion": "1.0.0",
        ///   "expiresAt": "2027-02-09T20:00:00Z"
        /// }
        /// ```
        /// </remarks>
        [HttpGet("{decisionId}")]
        [ProducesResponseType(typeof(ComplianceDecision), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDecisionById([FromRoute] string decisionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(decisionId))
                {
                    return BadRequest(new { error = "Decision ID is required" });
                }

                var decision = await _decisionService.GetDecisionByIdAsync(decisionId);
                
                if (decision == null)
                {
                    _logger.LogWarning("Decision not found: {DecisionId}", LoggingHelper.SanitizeLogInput(decisionId));
                    return NotFound(new { error = $"Decision not found: {decisionId}" });
                }

                return Ok(decision);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving decision: {DecisionId}", LoggingHelper.SanitizeLogInput(decisionId));
                return StatusCode(500, new { error = "An error occurred while retrieving the decision" });
            }
        }

        /// <summary>
        /// Queries compliance decisions with filtering and pagination
        /// </summary>
        /// <param name="request">The query parameters</param>
        /// <returns>List of matching decisions with pagination</returns>
        /// <remarks>
        /// Queries compliance decisions with comprehensive filtering options and pagination.
        /// 
        /// **Filtering Options:**
        /// - **organizationId**: Filter by organization
        /// - **onboardingSessionId**: Filter by onboarding session
        /// - **step**: Filter by onboarding step
        /// - **outcome**: Filter by decision outcome
        /// - **decisionMaker**: Filter by who made the decision
        /// - **fromDate/toDate**: Filter by date range
        /// - **includeSuperseded**: Include decisions that have been updated (default: false)
        /// - **includeExpired**: Include expired decisions (default: false)
        /// 
        /// **Pagination:**
        /// - **page**: Page number (1-based, default: 1)
        /// - **pageSize**: Items per page (default: 50, max: 100)
        /// 
        /// **Response includes:**
        /// - List of decisions matching filters
        /// - Total count of matching records
        /// - Pagination metadata
        /// - Summary statistics (counts by outcome, average decision time, common rejection reasons)
        /// 
        /// **Example Request:**
        /// ```
        /// GET /api/v1/compliance/decisions/query?organizationId=org-123&step=KycKybVerification&page=1&pageSize=20
        /// ```
        /// 
        /// **Example Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "decisions": [...],
        ///   "totalCount": 45,
        ///   "page": 1,
        ///   "pageSize": 20,
        ///   "totalPages": 3,
        ///   "summary": {
        ///     "approvedCount": 38,
        ///     "rejectedCount": 5,
        ///     "requiresReviewCount": 2,
        ///     "pendingCount": 0,
        ///     "conditionalApprovalCount": 0,
        ///     "expiredCount": 0,
        ///     "averageDecisionTimeHours": 12.5,
        ///     "commonRejectionReasons": ["Missing KYC documentation", "Incomplete AML screening"]
        ///   }
        /// }
        /// ```
        /// </remarks>
        [HttpGet("query")]
        [ProducesResponseType(typeof(QueryComplianceDecisionsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> QueryDecisions([FromQuery] QueryComplianceDecisionsRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Querying compliance decisions: OrganizationId={OrganizationId}, Step={Step}, Page={Page}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    request.Step?.ToString() ?? "All",
                    request.Page
                );

                var response = await _decisionService.QueryDecisionsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying compliance decisions");
                return StatusCode(500, new QueryComplianceDecisionsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while querying compliance decisions"
                });
            }
        }

        /// <summary>
        /// Gets the most recent active decision for an organization and step
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <param name="step">The onboarding step</param>
        /// <returns>The most recent active decision</returns>
        /// <remarks>
        /// Retrieves the latest active (not superseded, not expired) compliance decision
        /// for a specific organization and onboarding step.
        /// 
        /// **Use Cases:**
        /// - Check current compliance status for an organization
        /// - Verify if a step has been approved
        /// - Determine if re-evaluation is needed
        /// 
        /// **Example Request:**
        /// ```
        /// GET /api/v1/compliance/decisions/active/org-123/KycKybVerification
        /// ```
        /// 
        /// Returns 404 if no active decision exists for the specified organization and step.
        /// </remarks>
        [HttpGet("active/{organizationId}/{step}")]
        [ProducesResponseType(typeof(ComplianceDecision), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetActiveDecision(
            [FromRoute] string organizationId,
            [FromRoute] OnboardingStep step)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(organizationId))
                {
                    return BadRequest(new { error = "Organization ID is required" });
                }

                var decision = await _decisionService.GetActiveDecisionAsync(organizationId, step);
                
                if (decision == null)
                {
                    return NotFound(new 
                    { 
                        error = $"No active decision found for organization {organizationId} and step {step}" 
                    });
                }

                return Ok(decision);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving active decision: OrganizationId={OrganizationId}, Step={Step}",
                    LoggingHelper.SanitizeLogInput(organizationId),
                    step
                );
                return StatusCode(500, new { error = "An error occurred while retrieving the active decision" });
            }
        }

        /// <summary>
        /// Updates an existing compliance decision by creating a new one
        /// </summary>
        /// <param name="previousDecisionId">ID of the decision to update</param>
        /// <param name="request">The new decision request</param>
        /// <returns>The new decision that supersedes the previous one</returns>
        /// <remarks>
        /// Updates a compliance decision by creating a new immutable record that supersedes the previous one.
        /// The previous decision is marked as superseded and linked to the new decision.
        /// 
        /// **Immutability:**
        /// Decisions are immutable - updates create a new decision record rather than modifying the existing one.
        /// This maintains a complete audit trail showing the history of decisions and changes.
        /// 
        /// **Use Cases:**
        /// - Re-evaluation after new evidence is submitted
        /// - Correction of erroneous decisions
        /// - Periodic review updates
        /// - Policy version upgrades
        /// 
        /// **Example Request:**
        /// ```
        /// PUT /api/v1/compliance/decisions/dec-001
        /// {
        ///   "organizationId": "org-123",
        ///   "step": "KycKybVerification",
        ///   "evidenceReferences": [...]
        /// }
        /// ```
        /// 
        /// The new decision will have `previousDecisionId` set to "dec-001",
        /// and the old decision will be marked with `isSuperseded: true`.
        /// </remarks>
        [HttpPut("{previousDecisionId}")]
        [ProducesResponseType(typeof(ComplianceDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ComplianceDecisionResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateDecision(
            [FromRoute] string previousDecisionId,
            [FromBody] CreateComplianceDecisionRequest request)
        {
            try
            {
                // Get authenticated user address
                var actorAddress = User.FindFirst("Address")?.Value;
                if (string.IsNullOrWhiteSpace(actorAddress))
                {
                    return Unauthorized(new ComplianceDecisionResponse
                    {
                        Success = false,
                        ErrorMessage = "Unable to identify authenticated user"
                    });
                }

                if (string.IsNullOrWhiteSpace(previousDecisionId))
                {
                    return BadRequest(new ComplianceDecisionResponse
                    {
                        Success = false,
                        ErrorMessage = "Previous decision ID is required"
                    });
                }

                _logger.LogInformation(
                    "Updating compliance decision: PreviousDecisionId={PreviousDecisionId}, Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(previousDecisionId),
                    LoggingHelper.SanitizeLogInput(actorAddress)
                );

                var response = await _decisionService.UpdateDecisionAsync(previousDecisionId, request, actorAddress);

                if (!response.Success)
                {
                    if (response.ErrorMessage?.Contains("not found") == true)
                    {
                        return NotFound(response);
                    }
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating compliance decision: {PreviousDecisionId}", 
                    LoggingHelper.SanitizeLogInput(previousDecisionId));
                return StatusCode(500, new ComplianceDecisionResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while updating the compliance decision"
                });
            }
        }

        /// <summary>
        /// Gets compliance decisions requiring periodic review
        /// </summary>
        /// <param name="beforeDate">Optional date filter (ISO 8601). Returns decisions with review date before this date. Defaults to current time.</param>
        /// <returns>List of decisions requiring review</returns>
        /// <remarks>
        /// Retrieves compliance decisions that are flagged for periodic review
        /// and have a review date that has passed or is approaching.
        /// 
        /// **Use Cases:**
        /// - Compliance team dashboard showing pending reviews
        /// - Automated notifications for upcoming reviews
        /// - Risk management monitoring
        /// 
        /// **Example Request:**
        /// ```
        /// GET /api/v1/compliance/decisions/review-required?beforeDate=2026-03-01T00:00:00Z
        /// ```
        /// </remarks>
        [HttpGet("review-required")]
        [ProducesResponseType(typeof(List<ComplianceDecision>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDecisionsRequiringReview([FromQuery] DateTime? beforeDate = null)
        {
            try
            {
                var decisions = await _decisionService.GetDecisionsRequiringReviewAsync(beforeDate);
                return Ok(decisions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving decisions requiring review");
                return StatusCode(500, new { error = "An error occurred while retrieving decisions requiring review" });
            }
        }

        /// <summary>
        /// Gets expired compliance decisions
        /// </summary>
        /// <returns>List of expired decisions</returns>
        /// <remarks>
        /// Retrieves compliance decisions that have expired and may need renewal.
        /// 
        /// **Use Cases:**
        /// - Identify organizations requiring re-evaluation
        /// - Compliance monitoring and alerts
        /// - Automated renewal workflows
        /// 
        /// **Example Response:**
        /// Returns decisions where `expiresAt` is in the past and `isSuperseded` is false.
        /// </remarks>
        [HttpGet("expired")]
        [ProducesResponseType(typeof(List<ComplianceDecision>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetExpiredDecisions()
        {
            try
            {
                var decisions = await _decisionService.GetExpiredDecisionsAsync();
                return Ok(decisions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expired decisions");
                return StatusCode(500, new { error = "An error occurred while retrieving expired decisions" });
            }
        }

        /// <summary>
        /// Gets policy rules applicable to a specific onboarding step
        /// </summary>
        /// <param name="step">The onboarding step</param>
        /// <returns>List of policy rules</returns>
        /// <remarks>
        /// Retrieves the policy rules that will be evaluated for a specific onboarding step.
        /// Useful for understanding requirements before submitting evidence.
        /// 
        /// **Use Cases:**
        /// - Display requirements to users before they submit evidence
        /// - Frontend validation and guidance
        /// - Compliance documentation and training
        /// 
        /// **Example Response:**
        /// ```json
        /// [
        ///   {
        ///     "ruleId": "KYC_DOC_001",
        ///     "ruleName": "KYC Documentation Complete",
        ///     "description": "Know Your Customer documentation must be complete",
        ///     "requiredEvidenceTypes": ["KYC_REPORT"],
        ///     "severity": "Error",
        ///     "isRequired": true,
        ///     "remediationActions": ["Complete KYC verification process"],
        ///     "estimatedRemediationHours": 48
        ///   }
        /// ]
        /// ```
        /// </remarks>
        [HttpGet("policy-rules/{step}")]
        [ProducesResponseType(typeof(List<PolicyRule>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPolicyRules([FromRoute] OnboardingStep step)
        {
            try
            {
                var rules = await _policyEvaluator.GetApplicableRulesAsync(step);
                return Ok(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving policy rules for step: {Step}", step);
                return StatusCode(500, new { error = "An error occurred while retrieving policy rules" });
            }
        }

        /// <summary>
        /// Gets the current policy configuration
        /// </summary>
        /// <returns>Policy configuration including all rules and settings</returns>
        /// <remarks>
        /// Retrieves the complete policy configuration including all rules, defaults, and metadata.
        /// 
        /// **Contains:**
        /// - Policy version
        /// - Rules organized by onboarding step
        /// - Default expiration and review settings
        /// - Configuration metadata
        /// 
        /// **Use Cases:**
        /// - Administrative oversight of compliance policies
        /// - Documentation generation
        /// - Policy audit and review
        /// </remarks>
        [HttpGet("policy-configuration")]
        [ProducesResponseType(typeof(PolicyConfiguration), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPolicyConfiguration()
        {
            try
            {
                var config = await _policyEvaluator.GetPolicyConfigurationAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving policy configuration");
                return StatusCode(500, new { error = "An error occurred while retrieving policy configuration" });
            }
        }

        /// <summary>
        /// Gets policy evaluation metrics
        /// </summary>
        /// <returns>Policy metrics including evaluation counts and performance data</returns>
        /// <remarks>
        /// Retrieves metrics about policy evaluations for monitoring and reporting.
        /// 
        /// **Metrics Include:**
        /// - Total evaluations performed
        /// - Automatic approval/rejection counts
        /// - Manual review requirements
        /// - Average evaluation time
        /// - Most frequently failed rules
        /// 
        /// **Use Cases:**
        /// - Operational dashboards
        /// - Performance monitoring
        /// - Policy effectiveness analysis
        /// - Identifying common failure points
        /// </remarks>
        [HttpGet("policy-metrics")]
        [ProducesResponseType(typeof(PolicyMetrics), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPolicyMetrics()
        {
            try
            {
                var metrics = await _policyEvaluator.GetMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving policy metrics");
                return StatusCode(500, new { error = "An error occurred while retrieving policy metrics" });
            }
        }
    }
}

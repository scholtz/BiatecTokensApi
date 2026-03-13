using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory whitelist policy service with fail-closed evaluation semantics.
    /// </summary>
    public class WhitelistPolicyService : IWhitelistPolicyService
    {
        private readonly Dictionary<string, WhitelistPolicy> _store = new();
        private readonly Lock _lock = new();
        private readonly ILogger<WhitelistPolicyService> _logger;
        private readonly List<WhitelistAuditEvent> _auditLog = new();

        /// <summary>
        /// Initializes a new instance of <see cref="WhitelistPolicyService"/>
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public WhitelistPolicyService(ILogger<WhitelistPolicyService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyResponse> CreatePolicyAsync(CreateWhitelistPolicyRequest request, string createdBy)
        {
            var policy = new WhitelistPolicy
            {
                PolicyId = Guid.NewGuid().ToString(),
                PolicyName = request.PolicyName,
                Description = request.Description,
                AssetId = request.AssetId,
                Status = WhitelistPolicyStatus.Draft,
                AllowedAddresses = NormalizeAddresses(request.AllowedAddresses),
                DeniedAddresses = NormalizeAddresses(request.DeniedAddresses),
                AllowedJurisdictions = NormalizeJurisdictions(request.AllowedJurisdictions),
                BlockedJurisdictions = NormalizeJurisdictions(request.BlockedJurisdictions),
                RequiredInvestorCategories = new List<WhitelistPolicyInvestorCategory>(request.RequiredInvestorCategories),
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                Notes = request.Notes,
                Version = 1
            };

            lock (_lock)
            {
                _store[policy.PolicyId] = policy;
                _auditLog.Add(new WhitelistAuditEvent
                {
                    PolicyId = policy.PolicyId,
                    EventType = WhitelistAuditEventType.PolicyCreated,
                    Actor = createdBy,
                    Description = $"Policy '{policy.PolicyName}' created for asset {policy.AssetId}.",
                    PolicyVersion = policy.Version
                });
            }

            _logger.LogInformation("WhitelistPolicy created: {PolicyId} for asset {AssetId} by {CreatedBy}",
                LoggingHelper.SanitizeLogInput(policy.PolicyId),
                policy.AssetId,
                LoggingHelper.SanitizeLogInput(createdBy));

            return Task.FromResult(new WhitelistPolicyResponse { Success = true, Policy = policy });
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyResponse> GetPolicyAsync(string policyId)
        {
            lock (_lock)
            {
                if (_store.TryGetValue(policyId, out var policy))
                    return Task.FromResult(new WhitelistPolicyResponse { Success = true, Policy = policy });
            }

            return Task.FromResult(new WhitelistPolicyResponse
            {
                Success = false,
                ErrorMessage = $"Policy '{policyId}' not found.",
                ErrorCode = "POLICY_NOT_FOUND"
            });
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyListResponse> GetPoliciesAsync(ulong? assetId = null)
        {
            List<WhitelistPolicy> results;
            lock (_lock)
            {
                results = assetId.HasValue
                    ? _store.Values.Where(p => p.AssetId == assetId.Value).ToList()
                    : _store.Values.ToList();
            }

            return Task.FromResult(new WhitelistPolicyListResponse
            {
                Success = true,
                Policies = results,
                TotalCount = results.Count
            });
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyResponse> UpdatePolicyAsync(string policyId, UpdateWhitelistPolicyRequest request, string updatedBy)
        {
            lock (_lock)
            {
                if (!_store.TryGetValue(policyId, out var policy))
                    return Task.FromResult(new WhitelistPolicyResponse
                    {
                        Success = false,
                        ErrorMessage = $"Policy '{policyId}' not found.",
                        ErrorCode = "POLICY_NOT_FOUND"
                    });

                if (policy.Status == WhitelistPolicyStatus.Archived)
                    return Task.FromResult(new WhitelistPolicyResponse
                    {
                        Success = false,
                        ErrorMessage = "Archived policies cannot be updated.",
                        ErrorCode = "POLICY_ARCHIVED"
                    });

                if (request.PolicyName is not null) policy.PolicyName = request.PolicyName;
                if (request.Description is not null) policy.Description = request.Description;
                if (request.Status.HasValue) policy.Status = request.Status.Value;
                if (request.AllowedAddresses is not null) policy.AllowedAddresses = NormalizeAddresses(request.AllowedAddresses);
                if (request.DeniedAddresses is not null) policy.DeniedAddresses = NormalizeAddresses(request.DeniedAddresses);
                if (request.AllowedJurisdictions is not null) policy.AllowedJurisdictions = NormalizeJurisdictions(request.AllowedJurisdictions);
                if (request.BlockedJurisdictions is not null) policy.BlockedJurisdictions = NormalizeJurisdictions(request.BlockedJurisdictions);
                if (request.RequiredInvestorCategories is not null) policy.RequiredInvestorCategories = new List<WhitelistPolicyInvestorCategory>(request.RequiredInvestorCategories);
                if (request.Notes is not null) policy.Notes = request.Notes;

                policy.UpdatedBy = updatedBy;
                policy.UpdatedAt = DateTime.UtcNow;
                policy.Version++;

                var auditEventType = request.Status == WhitelistPolicyStatus.Active
                    ? WhitelistAuditEventType.PolicyActivated
                    : WhitelistAuditEventType.PolicyUpdated;

                _auditLog.Add(new WhitelistAuditEvent
                {
                    PolicyId = policyId,
                    EventType = auditEventType,
                    Actor = updatedBy,
                    Description = $"Policy updated to version {policy.Version}.",
                    PolicyVersion = policy.Version
                });

                _logger.LogInformation("WhitelistPolicy updated: {PolicyId} by {UpdatedBy}",
                    LoggingHelper.SanitizeLogInput(policyId),
                    LoggingHelper.SanitizeLogInput(updatedBy));

                return Task.FromResult(new WhitelistPolicyResponse { Success = true, Policy = policy });
            }
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyResponse> ArchivePolicyAsync(string policyId, string archivedBy)
        {
            lock (_lock)
            {
                if (!_store.TryGetValue(policyId, out var policy))
                    return Task.FromResult(new WhitelistPolicyResponse
                    {
                        Success = false,
                        ErrorMessage = $"Policy '{policyId}' not found.",
                        ErrorCode = "POLICY_NOT_FOUND"
                    });

                policy.Status = WhitelistPolicyStatus.Archived;
                policy.UpdatedBy = archivedBy;
                policy.UpdatedAt = DateTime.UtcNow;
                policy.Version++;

                _auditLog.Add(new WhitelistAuditEvent
                {
                    PolicyId = policyId,
                    EventType = WhitelistAuditEventType.PolicyArchived,
                    Actor = archivedBy,
                    Description = $"Policy archived at version {policy.Version}.",
                    PolicyVersion = policy.Version
                });

                _logger.LogInformation("WhitelistPolicy archived: {PolicyId} by {ArchivedBy}",
                    LoggingHelper.SanitizeLogInput(policyId),
                    LoggingHelper.SanitizeLogInput(archivedBy));

                return Task.FromResult(new WhitelistPolicyResponse { Success = true, Policy = policy });
            }
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyValidationResult> ValidatePolicyAsync(string policyId)
        {
            WhitelistPolicy? policy;
            lock (_lock)
            {
                _store.TryGetValue(policyId, out policy);
            }

            if (policy is null)
                return Task.FromResult(new WhitelistPolicyValidationResult
                {
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found.",
                    ErrorCode = "POLICY_NOT_FOUND"
                });

            var issues = new List<WhitelistPolicyValidationIssue>();

            // Contradiction: address in both allowlist and denylist
            var addressConflicts = policy.AllowedAddresses.Intersect(policy.DeniedAddresses, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var addr in addressConflicts)
            {
                issues.Add(new WhitelistPolicyValidationIssue
                {
                    IssueCode = "ADDR_ALLOW_DENY_CONFLICT",
                    Severity = "Error",
                    Description = $"Address '{addr}' appears in both AllowedAddresses and DeniedAddresses.",
                    Guidance = "Remove the address from one of the lists. DeniedAddresses takes precedence during evaluation."
                });
            }

            // Contradiction: jurisdiction in both allowed and blocked
            var jurisdictionConflicts = policy.AllowedJurisdictions.Intersect(policy.BlockedJurisdictions, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var jur in jurisdictionConflicts)
            {
                issues.Add(new WhitelistPolicyValidationIssue
                {
                    IssueCode = "JURISDICTION_ALLOW_BLOCK_CONFLICT",
                    Severity = "Error",
                    Description = $"Jurisdiction '{jur}' appears in both AllowedJurisdictions and BlockedJurisdictions.",
                    Guidance = "Remove the jurisdiction from one of the lists. BlockedJurisdictions takes precedence during evaluation."
                });
            }

            // Warning: completely empty policy — every evaluation is fail-closed
            if (IsPolicyEmpty(policy))
            {
                issues.Add(new WhitelistPolicyValidationIssue
                {
                    IssueCode = "POLICY_EMPTY",
                    Severity = "Warning",
                    Description = "The policy has no rules defined. All evaluations will be denied (fail-closed).",
                    Guidance = "Add at least one AllowedAddress, AllowedJurisdiction, or RequiredInvestorCategory to permit participants."
                });
            }

            // Warning: no name
            if (string.IsNullOrWhiteSpace(policy.PolicyName))
            {
                issues.Add(new WhitelistPolicyValidationIssue
                {
                    IssueCode = "POLICY_NO_NAME",
                    Severity = "Warning",
                    Description = "The policy has no name.",
                    Guidance = "Provide a descriptive PolicyName to aid compliance officers."
                });
            }

            var hasErrors = issues.Any(i => i.Severity == "Error");

            return Task.FromResult(new WhitelistPolicyValidationResult
            {
                Success = true,
                IsValid = !hasErrors,
                Issues = issues
            });
        }

        /// <inheritdoc/>
        public Task<WhitelistPolicyEligibilityResult> EvaluateEligibilityAsync(WhitelistPolicyEligibilityRequest request, string? evaluatedBy = null)
        {
            WhitelistPolicy? policy;
            lock (_lock)
            {
                _store.TryGetValue(request.PolicyId, out policy);
            }

            if (policy is null)
            {
                return Task.FromResult(new WhitelistPolicyEligibilityResult
                {
                    Success = false,
                    ErrorMessage = $"Policy '{request.PolicyId}' not found.",
                    ErrorCode = "POLICY_NOT_FOUND",
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy not found." },
                    IsFailClosed = true,
                    OperatorGuidance = "Ensure the policy exists and the policyId is correct.",
                    ReasonCodes = new List<WhitelistEligibilityReasonCode> { WhitelistEligibilityReasonCode.PolicyNotFound }
                });
            }

            var address = (request.ParticipantAddress ?? string.Empty).Trim();
            var jurisdiction = (request.JurisdictionCode ?? string.Empty).Trim().ToUpperInvariant();
            var versionMeta = BuildVersionMetadata(policy);

            // FAIL-CLOSED: Draft policies always deny
            if (policy.Status == WhitelistPolicyStatus.Draft)
            {
                _logger.LogInformation("WhitelistPolicy evaluation DENY (Draft) policyId={PolicyId} address={Address}",
                    LoggingHelper.SanitizeLogInput(request.PolicyId),
                    LoggingHelper.SanitizeLogInput(address));

                var draftResult = new WhitelistPolicyEligibilityResult
                {
                    Success = true,
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy is in Draft state. Fail-closed: all evaluations are denied until the policy is activated." },
                    IsFailClosed = true,
                    OperatorGuidance = "Activate the policy before evaluating participant eligibility.",
                    ReasonCodes = new List<WhitelistEligibilityReasonCode> { WhitelistEligibilityReasonCode.PolicyInDraftState },
                    PolicyVersionMetadata = versionMeta
                };
                RecordEvaluationEvent(request, policy, draftResult, evaluatedBy);
                return Task.FromResult(draftResult);
            }

            // Archived policies also deny
            if (policy.Status == WhitelistPolicyStatus.Archived)
            {
                var archivedResult = new WhitelistPolicyEligibilityResult
                {
                    Success = true,
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy is Archived and no longer active." },
                    IsFailClosed = true,
                    OperatorGuidance = "Create or activate a new policy for this asset.",
                    ReasonCodes = new List<WhitelistEligibilityReasonCode> { WhitelistEligibilityReasonCode.PolicyIsArchived },
                    PolicyVersionMetadata = versionMeta
                };
                RecordEvaluationEvent(request, policy, archivedResult, evaluatedBy);
                return Task.FromResult(archivedResult);
            }

            // FAIL-CLOSED: completely empty active policy — no rules defined → deny
            if (IsPolicyEmpty(policy))
            {
                var emptyResult = new WhitelistPolicyEligibilityResult
                {
                    Success = true,
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy has no rules defined. Fail-closed: all evaluations are denied." },
                    IsFailClosed = true,
                    OperatorGuidance = "Add at least one allow rule (AllowedAddresses, AllowedJurisdictions, or RequiredInvestorCategories).",
                    ReasonCodes = new List<WhitelistEligibilityReasonCode> { WhitelistEligibilityReasonCode.PolicyHasNoRules },
                    PolicyVersionMetadata = versionMeta
                };
                RecordEvaluationEvent(request, policy, emptyResult, evaluatedBy);
                return Task.FromResult(emptyResult);
            }

            var reasons = new List<string>();
            var reasonCodes = new List<WhitelistEligibilityReasonCode>();

            // Hard deny: address in denylist
            if (policy.DeniedAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("Participant address is on the explicit deny list.");
                reasonCodes.Add(WhitelistEligibilityReasonCode.AddressOnDenyList);
                var denyResult = DenyResult(reasons, isFailClosed: false,
                    guidance: "Remove the address from DeniedAddresses to allow participation.",
                    reasonCodes: reasonCodes, versionMeta: versionMeta);
                RecordEvaluationEvent(request, policy, denyResult, evaluatedBy);
                return Task.FromResult(denyResult);
            }

            // Hard deny: jurisdiction is blocked
            if (!string.IsNullOrEmpty(jurisdiction) && policy.BlockedJurisdictions.Contains(jurisdiction, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add($"Jurisdiction '{jurisdiction}' is blocked by this policy.");
                reasonCodes.Add(WhitelistEligibilityReasonCode.RestrictedJurisdiction);
                var denyResult = DenyResult(reasons, isFailClosed: false,
                    guidance: "Participation from this jurisdiction is not permitted.",
                    reasonCodes: reasonCodes, versionMeta: versionMeta);
                RecordEvaluationEvent(request, policy, denyResult, evaluatedBy);
                return Task.FromResult(denyResult);
            }

            // Jurisdiction allow check: when AllowedJurisdictions is non-empty, participant must be in it
            if (policy.AllowedJurisdictions.Count > 0)
            {
                if (string.IsNullOrEmpty(jurisdiction) || !policy.AllowedJurisdictions.Contains(jurisdiction, StringComparer.OrdinalIgnoreCase))
                {
                    var code = string.IsNullOrEmpty(jurisdiction)
                        ? WhitelistEligibilityReasonCode.JurisdictionNotProvided
                        : WhitelistEligibilityReasonCode.JurisdictionNotAllowed;
                    reasons.Add(string.IsNullOrEmpty(jurisdiction)
                        ? "No jurisdiction provided and policy requires an allowed jurisdiction."
                        : $"Jurisdiction '{jurisdiction}' is not in the permitted jurisdictions list.");
                    reasonCodes.Add(code);
                    var denyResult = DenyResult(reasons, isFailClosed: false,
                        guidance: "Participant must be from an allowed jurisdiction to participate.",
                        reasonCodes: reasonCodes, versionMeta: versionMeta);
                    RecordEvaluationEvent(request, policy, denyResult, evaluatedBy);
                    return Task.FromResult(denyResult);
                }
            }

            // Investor category check: when RequiredInvestorCategories is non-empty, participant must match one
            if (policy.RequiredInvestorCategories.Count > 0)
            {
                if (!policy.RequiredInvestorCategories.Contains(request.InvestorCategory))
                {
                    reasons.Add($"Investor category '{request.InvestorCategory}' is not in the required categories for this policy.");
                    reasonCodes.Add(WhitelistEligibilityReasonCode.UnsupportedInvestorCategory);
                    var denyResult = DenyResult(reasons, isFailClosed: false,
                        guidance: "Participant must meet one of the required investor category thresholds.",
                        reasonCodes: reasonCodes, versionMeta: versionMeta);
                    RecordEvaluationEvent(request, policy, denyResult, evaluatedBy);
                    return Task.FromResult(denyResult);
                }
            }

            // Address allowlist check: when AllowedAddresses is non-empty, participant must be in it
            if (policy.AllowedAddresses.Count > 0)
            {
                if (!policy.AllowedAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
                {
                    reasons.Add("Participant address is not on the explicit allow list.");
                    reasonCodes.Add(WhitelistEligibilityReasonCode.AddressNotOnAllowList);
                    var denyResult = DenyResult(reasons, isFailClosed: false,
                        guidance: "Add the participant's address to AllowedAddresses.",
                        reasonCodes: reasonCodes, versionMeta: versionMeta);
                    RecordEvaluationEvent(request, policy, denyResult, evaluatedBy);
                    return Task.FromResult(denyResult);
                }
            }

            // All checks passed → Allow
            _logger.LogInformation("WhitelistPolicy evaluation ALLOW policyId={PolicyId} address={Address}",
                LoggingHelper.SanitizeLogInput(request.PolicyId),
                LoggingHelper.SanitizeLogInput(address));

            var allowResult = new WhitelistPolicyEligibilityResult
            {
                Success = true,
                Outcome = WhitelistPolicyEligibilityOutcome.Allow,
                Reasons = new List<string> { "All policy criteria satisfied." },
                IsFailClosed = false,
                OperatorGuidance = null,
                ReasonCodes = new List<WhitelistEligibilityReasonCode> { WhitelistEligibilityReasonCode.AllPolicyCriteriaSatisfied },
                PolicyVersionMetadata = versionMeta
            };
            RecordEvaluationEvent(request, policy, allowResult, evaluatedBy);
            return Task.FromResult(allowResult);
        }

        /// <inheritdoc/>
        public Task<WhitelistAuditHistoryResponse> GetAuditHistoryAsync(string policyId, WhitelistAuditHistoryRequest request)
        {
            bool exists;
            lock (_lock)
            {
                exists = _store.ContainsKey(policyId);
            }

            if (!exists)
                return Task.FromResult(new WhitelistAuditHistoryResponse
                {
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found.",
                    ErrorCode = "POLICY_NOT_FOUND",
                    PolicyId = policyId
                });

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, MaxAuditPageSize);

            List<WhitelistAuditEvent> filtered;
            lock (_lock)
            {
                filtered = _auditLog
                    .Where(e => e.PolicyId == policyId
                        && (request.EventTypeFilter == null || e.EventType == request.EventTypeFilter.Value))
                    .OrderByDescending(e => e.OccurredAt)
                    .ToList();
            }

            var total = filtered.Count;
            var totalPages = (int)Math.Ceiling((double)total / pageSize);
            var events = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult(new WhitelistAuditHistoryResponse
            {
                Success = true,
                PolicyId = policyId,
                Events = events,
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        /// <inheritdoc/>
        public Task<WhitelistComplianceEvidenceReport> GetComplianceEvidenceAsync(
            string policyId,
            WhitelistComplianceEvidenceRequest request,
            string requestedBy)
        {
            WhitelistPolicy? policy;
            lock (_lock)
            {
                _store.TryGetValue(policyId, out policy);
            }

            if (policy is null)
                return Task.FromResult(new WhitelistComplianceEvidenceReport
                {
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found.",
                    ErrorCode = "POLICY_NOT_FOUND",
                    PolicyId = policyId,
                    GeneratedBy = requestedBy
                });

            List<WhitelistAuditEvent> allEvents;
            lock (_lock)
            {
                allEvents = _auditLog
                    .Where(e => e.PolicyId == policyId)
                    .Where(e => request.FromDate == null || e.OccurredAt >= request.FromDate.Value)
                    .Where(e => request.ToDate == null || e.OccurredAt <= request.ToDate.Value)
                    .OrderByDescending(e => e.OccurredAt)
                    .ToList();
            }

            var evaluationEvents = allEvents.Where(e => e.EventType == WhitelistAuditEventType.EligibilityEvaluated).ToList();
            var changeEvents = allEvents.Where(e => e.EventType != WhitelistAuditEventType.EligibilityEvaluated).ToList();

            var allowCount = evaluationEvents.Count(e => e.EligibilityOutcome == WhitelistPolicyEligibilityOutcome.Allow);
            var denyCount = evaluationEvents.Count(e => e.EligibilityOutcome == WhitelistPolicyEligibilityOutcome.Deny);
            var conditionalCount = evaluationEvents.Count(e => e.EligibilityOutcome == WhitelistPolicyEligibilityOutcome.ConditionalReview);
            var failClosedCount = evaluationEvents.Count(e => e.IsFailClosed == true);

            var topDenyCodes = evaluationEvents
                .Where(e => e.EligibilityOutcome == WhitelistPolicyEligibilityOutcome.Deny)
                .SelectMany(e => e.ReasonCodes)
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            var summary = new WhitelistEvaluationSummary
            {
                TotalEvaluations = evaluationEvents.Count,
                AllowCount = allowCount,
                DenyCount = denyCount,
                ConditionalReviewCount = conditionalCount,
                FailClosedCount = failClosedCount,
                TopDenyReasonCodes = topDenyCodes
            };

            var report = new WhitelistComplianceEvidenceReport
            {
                Success = true,
                PolicyId = policyId,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = requestedBy,
                PolicyVersionMetadata = BuildVersionMetadata(policy),
                EvaluationSummary = summary,
                AuditEvents = request.IncludeEvaluationHistory ? allEvents : changeEvents,
                PolicyChangeHistory = changeEvents
            };

            _logger.LogInformation("Compliance evidence report generated for policy {PolicyId} by {RequestedBy}",
                LoggingHelper.SanitizeLogInput(policyId),
                LoggingHelper.SanitizeLogInput(requestedBy));

            return Task.FromResult(report);
        }

        private const int MaxAuditPageSize = 200;

        private static bool IsPolicyEmpty(WhitelistPolicy policy)
            => policy.AllowedAddresses.Count == 0
            && policy.DeniedAddresses.Count == 0
            && policy.AllowedJurisdictions.Count == 0
            && policy.BlockedJurisdictions.Count == 0
            && policy.RequiredInvestorCategories.Count == 0;

        private static WhitelistPolicyVersionMetadata BuildVersionMetadata(WhitelistPolicy policy)
            => new WhitelistPolicyVersionMetadata
            {
                PolicyId = policy.PolicyId,
                PolicyName = policy.PolicyName,
                Version = policy.Version,
                Status = policy.Status,
                EffectiveAt = policy.UpdatedAt ?? policy.CreatedAt,
                ActorIdentifier = policy.UpdatedBy ?? policy.CreatedBy
            };

        private static WhitelistPolicyEligibilityResult DenyResult(
            List<string> reasons,
            bool isFailClosed,
            string? guidance,
            List<WhitelistEligibilityReasonCode>? reasonCodes = null,
            WhitelistPolicyVersionMetadata? versionMeta = null)
        {
            return new WhitelistPolicyEligibilityResult
            {
                Success = true,
                Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                Reasons = reasons,
                IsFailClosed = isFailClosed,
                OperatorGuidance = guidance,
                ReasonCodes = reasonCodes ?? new List<WhitelistEligibilityReasonCode>(),
                PolicyVersionMetadata = versionMeta
            };
        }

        private void RecordEvaluationEvent(WhitelistPolicyEligibilityRequest request, WhitelistPolicy policy, WhitelistPolicyEligibilityResult result, string? actor = null)
        {
            lock (_lock)
            {
                _auditLog.Add(new WhitelistAuditEvent
                {
                    PolicyId = request.PolicyId,
                    EventType = WhitelistAuditEventType.EligibilityEvaluated,
                    Actor = actor ?? "system",
                    Description = $"Eligibility evaluation: {result.Outcome}. {string.Join("; ", result.Reasons)}",
                    PolicyVersion = policy.Version,
                    ReasonCodes = new List<WhitelistEligibilityReasonCode>(result.ReasonCodes),
                    EligibilityOutcome = result.Outcome,
                    ParticipantAddress = LoggingHelper.SanitizeLogInput(request.ParticipantAddress ?? string.Empty),
                    JurisdictionCode = request.JurisdictionCode,
                    InvestorCategory = request.InvestorCategory,
                    IsFailClosed = result.IsFailClosed
                });
            }
        }

        private static List<string> NormalizeAddresses(List<string> addresses)
            => addresses.Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();

        private static List<string> NormalizeJurisdictions(List<string> jurisdictions)
            => jurisdictions.Select(j => j.Trim().ToUpperInvariant()).Where(j => !string.IsNullOrEmpty(j)).ToList();
    }
}


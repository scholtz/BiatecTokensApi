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
            bool hasAnyRule = policy.AllowedAddresses.Count > 0
                || policy.DeniedAddresses.Count > 0
                || policy.AllowedJurisdictions.Count > 0
                || policy.BlockedJurisdictions.Count > 0
                || policy.RequiredInvestorCategories.Count > 0;

            if (!hasAnyRule)
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
        public Task<WhitelistPolicyEligibilityResult> EvaluateEligibilityAsync(WhitelistPolicyEligibilityRequest request)
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
                    OperatorGuidance = "Ensure the policy exists and the policyId is correct."
                });
            }

            var reasons = new List<string>();
            var address = (request.ParticipantAddress ?? string.Empty).Trim();
            var jurisdiction = (request.JurisdictionCode ?? string.Empty).Trim().ToUpperInvariant();

            // FAIL-CLOSED: Draft policies always deny
            if (policy.Status == WhitelistPolicyStatus.Draft)
            {
                _logger.LogInformation("WhitelistPolicy evaluation DENY (Draft) policyId={PolicyId} address={Address}",
                    LoggingHelper.SanitizeLogInput(request.PolicyId),
                    LoggingHelper.SanitizeLogInput(address));

                return Task.FromResult(new WhitelistPolicyEligibilityResult
                {
                    Success = true,
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy is in Draft state. Fail-closed: all evaluations are denied until the policy is activated." },
                    IsFailClosed = true,
                    OperatorGuidance = "Activate the policy before evaluating participant eligibility."
                });
            }

            // Archived policies also deny
            if (policy.Status == WhitelistPolicyStatus.Archived)
            {
                return Task.FromResult(new WhitelistPolicyEligibilityResult
                {
                    Success = true,
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy is Archived and no longer active." },
                    IsFailClosed = true,
                    OperatorGuidance = "Create or activate a new policy for this asset."
                });
            }

            // FAIL-CLOSED: completely empty active policy — no rules defined → deny
            bool hasAnyAllowRule = policy.AllowedAddresses.Count > 0
                || policy.AllowedJurisdictions.Count > 0
                || policy.RequiredInvestorCategories.Count > 0;

            bool hasDenyRules = policy.DeniedAddresses.Count > 0 || policy.BlockedJurisdictions.Count > 0;

            if (!hasAnyAllowRule && !hasDenyRules)
            {
                return Task.FromResult(new WhitelistPolicyEligibilityResult
                {
                    Success = true,
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    Reasons = new List<string> { "Policy has no rules defined. Fail-closed: all evaluations are denied." },
                    IsFailClosed = true,
                    OperatorGuidance = "Add at least one allow rule (AllowedAddresses, AllowedJurisdictions, or RequiredInvestorCategories)."
                });
            }

            // Hard deny: address in denylist
            if (policy.DeniedAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("Participant address is on the explicit deny list.");
                return Task.FromResult(DenyResult(reasons, isFailClosed: false,
                    guidance: "Remove the address from DeniedAddresses to allow participation."));
            }

            // Hard deny: jurisdiction is blocked
            if (!string.IsNullOrEmpty(jurisdiction) && policy.BlockedJurisdictions.Contains(jurisdiction, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add($"Jurisdiction '{jurisdiction}' is blocked by this policy.");
                return Task.FromResult(DenyResult(reasons, isFailClosed: false,
                    guidance: "Participation from this jurisdiction is not permitted."));
            }

            // Jurisdiction allow check: when AllowedJurisdictions is non-empty, participant must be in it
            if (policy.AllowedJurisdictions.Count > 0)
            {
                if (string.IsNullOrEmpty(jurisdiction) || !policy.AllowedJurisdictions.Contains(jurisdiction, StringComparer.OrdinalIgnoreCase))
                {
                    reasons.Add(string.IsNullOrEmpty(jurisdiction)
                        ? "No jurisdiction provided and policy requires an allowed jurisdiction."
                        : $"Jurisdiction '{jurisdiction}' is not in the permitted jurisdictions list.");
                    return Task.FromResult(DenyResult(reasons, isFailClosed: false,
                        guidance: "Participant must be from an allowed jurisdiction to participate."));
                }
            }

            // Investor category check: when RequiredInvestorCategories is non-empty, participant must match one
            if (policy.RequiredInvestorCategories.Count > 0)
            {
                if (!policy.RequiredInvestorCategories.Contains(request.InvestorCategory))
                {
                    reasons.Add($"Investor category '{request.InvestorCategory}' is not in the required categories for this policy.");
                    return Task.FromResult(DenyResult(reasons, isFailClosed: false,
                        guidance: "Participant must meet one of the required investor category thresholds."));
                }
            }

            // Address allowlist check: when AllowedAddresses is non-empty, participant must be in it
            if (policy.AllowedAddresses.Count > 0)
            {
                if (!policy.AllowedAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
                {
                    reasons.Add("Participant address is not on the explicit allow list.");
                    return Task.FromResult(DenyResult(reasons, isFailClosed: false,
                        guidance: "Add the participant's address to AllowedAddresses."));
                }
            }

            // All checks passed → Allow
            _logger.LogInformation("WhitelistPolicy evaluation ALLOW policyId={PolicyId} address={Address}",
                LoggingHelper.SanitizeLogInput(request.PolicyId),
                LoggingHelper.SanitizeLogInput(address));

            return Task.FromResult(new WhitelistPolicyEligibilityResult
            {
                Success = true,
                Outcome = WhitelistPolicyEligibilityOutcome.Allow,
                Reasons = new List<string> { "All policy criteria satisfied." },
                IsFailClosed = false,
                OperatorGuidance = null
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static WhitelistPolicyEligibilityResult DenyResult(List<string> reasons, bool isFailClosed, string? guidance)
        {
            return new WhitelistPolicyEligibilityResult
            {
                Success = true,
                Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                Reasons = reasons,
                IsFailClosed = isFailClosed,
                OperatorGuidance = guidance
            };
        }

        private static List<string> NormalizeAddresses(List<string> addresses)
            => addresses.Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();

        private static List<string> NormalizeJurisdictions(List<string> jurisdictions)
            => jurisdictions.Select(j => j.Trim().ToUpperInvariant()).Where(j => !string.IsNullOrEmpty(j)).ToList();
    }
}

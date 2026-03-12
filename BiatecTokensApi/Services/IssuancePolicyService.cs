using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.IssuancePolicy;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory service for managing issuance compliance policies and evaluating participant eligibility.
    /// Policies are stored per-issuer with strict tenant isolation.
    /// </summary>
    public class IssuancePolicyService : IIssuancePolicyService
    {
        private readonly IWhitelistService _whitelistService;
        private readonly ILogger<IssuancePolicyService> _logger;

        // In-memory store keyed by PolicyId
        private readonly ConcurrentDictionary<string, IssuanceCompliancePolicy> _policies = new();

        /// <summary>
        /// Initializes a new instance of <see cref="IssuancePolicyService"/>
        /// </summary>
        public IssuancePolicyService(
            IWhitelistService whitelistService,
            ILogger<IssuancePolicyService> logger)
        {
            _whitelistService = whitelistService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<IssuancePolicyResponse> CreatePolicyAsync(CreateIssuancePolicyRequest request, string issuerId)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);

            if (string.IsNullOrWhiteSpace(request.PolicyName))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = "PolicyName is required"
                });
            }

            if (request.AssetId == 0)
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = "AssetId must be greater than 0"
                });
            }

            var allowed = NormalizeList(request.AllowedJurisdictions);
            var blocked = NormalizeList(request.BlockedJurisdictions);
            var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
            var conflict = blocked.Where(b => allowedSet.Contains(b)).ToList();
            if (conflict.Count > 0)
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = $"Jurisdiction(s) cannot appear in both AllowedJurisdictions and BlockedJurisdictions: {string.Join(", ", conflict)}"
                });
            }

            var policy = new IssuanceCompliancePolicy
            {
                PolicyId = Guid.NewGuid().ToString(),
                IssuerId = issuerId,
                AssetId = request.AssetId,
                PolicyName = request.PolicyName.Trim(),
                Description = request.Description?.Trim(),
                WhitelistRequired = request.WhitelistRequired,
                AllowedJurisdictions = allowed,
                BlockedJurisdictions = blocked,
                KycRequired = request.KycRequired,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = issuerId
            };

            _policies[policy.PolicyId] = policy;

            _logger.LogInformation(
                "Issuance policy created: PolicyId={PolicyId}, AssetId={AssetId}, IssuerId={IssuerId}",
                policy.PolicyId,
                policy.AssetId,
                LoggingHelper.SanitizeLogInput(issuerId));

            return Task.FromResult(new IssuancePolicyResponse { Success = true, Policy = policy });
        }

        /// <inheritdoc/>
        public Task<IssuancePolicyResponse> GetPolicyAsync(string policyId, string requesterId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
            ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

            if (!_policies.TryGetValue(policyId, out var policy))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found"
                });
            }

            if (!string.Equals(policy.IssuerId, requesterId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = "Not authorized to access this policy"
                });
            }

            return Task.FromResult(new IssuancePolicyResponse { Success = true, Policy = policy });
        }

        /// <inheritdoc/>
        public Task<IssuancePolicyResponse> GetPolicyByAssetAsync(ulong assetId, string requesterId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

            var policy = _policies.Values
                .FirstOrDefault(p =>
                    p.AssetId == assetId &&
                    string.Equals(p.IssuerId, requesterId, StringComparison.OrdinalIgnoreCase));

            if (policy == null)
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = $"No policy found for asset {assetId}"
                });
            }

            return Task.FromResult(new IssuancePolicyResponse { Success = true, Policy = policy });
        }

        /// <inheritdoc/>
        public Task<IssuancePolicyResponse> UpdatePolicyAsync(string policyId, UpdateIssuancePolicyRequest request, string requesterId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

            if (!_policies.TryGetValue(policyId, out var policy))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found"
                });
            }

            if (!string.Equals(policy.IssuerId, requesterId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = "Not authorized to update this policy"
                });
            }

            if (request.PolicyName != null && string.IsNullOrWhiteSpace(request.PolicyName))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = "PolicyName cannot be set to empty"
                });
            }

            // Compute effective jurisdiction lists for conflict check
            var effectiveAllowed = request.AllowedJurisdictions != null
                ? NormalizeList(request.AllowedJurisdictions)
                : policy.AllowedJurisdictions;
            var effectiveBlocked = request.BlockedJurisdictions != null
                ? NormalizeList(request.BlockedJurisdictions)
                : policy.BlockedJurisdictions;

            var effectiveAllowedSet = new HashSet<string>(effectiveAllowed, StringComparer.OrdinalIgnoreCase);
            var conflict = effectiveBlocked.Where(b => effectiveAllowedSet.Contains(b)).ToList();
            if (conflict.Count > 0)
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = $"Jurisdiction(s) cannot appear in both AllowedJurisdictions and BlockedJurisdictions: {string.Join(", ", conflict)}"
                });
            }

            if (request.PolicyName != null) policy.PolicyName = request.PolicyName.Trim();
            if (request.Description != null) policy.Description = request.Description.Trim();
            if (request.WhitelistRequired.HasValue) policy.WhitelistRequired = request.WhitelistRequired.Value;
            if (request.AllowedJurisdictions != null) policy.AllowedJurisdictions = effectiveAllowed;
            if (request.BlockedJurisdictions != null) policy.BlockedJurisdictions = effectiveBlocked;
            if (request.KycRequired.HasValue) policy.KycRequired = request.KycRequired.Value;
            if (request.IsActive.HasValue) policy.IsActive = request.IsActive.Value;
            policy.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Issuance policy updated: PolicyId={PolicyId}, UpdatedBy={UpdatedBy}",
                policyId,
                LoggingHelper.SanitizeLogInput(requesterId));

            return Task.FromResult(new IssuancePolicyResponse { Success = true, Policy = policy });
        }

        /// <inheritdoc/>
        public Task<IssuancePolicyResponse> DeletePolicyAsync(string policyId, string requesterId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
            ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);

            if (!_policies.TryGetValue(policyId, out var policy))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found"
                });
            }

            if (!string.Equals(policy.IssuerId, requesterId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new IssuancePolicyResponse
                {
                    Success = false,
                    ErrorMessage = "Not authorized to delete this policy"
                });
            }

            _policies.TryRemove(policyId, out _);

            _logger.LogInformation(
                "Issuance policy deleted: PolicyId={PolicyId}, DeletedBy={DeletedBy}",
                policyId,
                LoggingHelper.SanitizeLogInput(requesterId));

            return Task.FromResult(new IssuancePolicyResponse { Success = true });
        }

        /// <inheritdoc/>
        public Task<IssuancePolicyListResponse> ListPoliciesAsync(string issuerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);

            var issuerPolicies = _policies.Values
                .Where(p => string.Equals(p.IssuerId, issuerId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.CreatedAt)
                .ToList();

            return Task.FromResult(new IssuancePolicyListResponse
            {
                Success = true,
                Policies = issuerPolicies,
                TotalCount = issuerPolicies.Count
            });
        }

        /// <inheritdoc/>
        public async Task<IssuancePolicyDecisionResult> EvaluateParticipantAsync(
            string policyId,
            EvaluateParticipantRequest request,
            string evaluatorId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(evaluatorId);

            var decisionId = Guid.NewGuid().ToString();

            if (!_policies.TryGetValue(policyId, out var policy))
            {
                return new IssuancePolicyDecisionResult
                {
                    DecisionId = decisionId,
                    PolicyId = policyId,
                    ParticipantAddress = request.ParticipantAddress ?? string.Empty,
                    Outcome = IssuancePolicyOutcome.Deny,
                    Reasons = new List<string> { $"Policy '{policyId}' not found" },
                    MatchedRules = new List<MatchedPolicyRule>(),
                    EvaluatedAt = DateTime.UtcNow,
                    EvaluatedBy = evaluatorId,
                    PolicyVersion = string.Empty,
                    Success = false,
                    ErrorMessage = $"Policy '{policyId}' not found"
                };
            }

            var matchedRules = new List<MatchedPolicyRule>();
            var reasons = new List<string>();
            var requiredActions = new List<string>();
            bool hasDeny = false;
            bool hasReview = false;

            // Rule 1: Whitelist check
            if (policy.WhitelistRequired)
            {
                bool isWhitelisted = await IsParticipantWhitelistedAsync(policy.AssetId, request.ParticipantAddress);
                if (isWhitelisted)
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "WHITELIST_CHECK",
                        RuleName = "Whitelist Membership",
                        Outcome = "Allow",
                        Reason = "Participant is on the active whitelist for this asset"
                    });
                    reasons.Add("Participant is on the active whitelist for this asset");
                }
                else
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "WHITELIST_CHECK",
                        RuleName = "Whitelist Membership",
                        Outcome = "Deny",
                        Reason = "Participant is not on the whitelist for this asset (whitelist required)"
                    });
                    reasons.Add("Participant is not on the whitelist for this asset");
                    hasDeny = true;
                }
            }

            // Rule 2: Allowed jurisdictions check
            if (policy.AllowedJurisdictions.Count > 0)
            {
                var participantJurisdiction = request.JurisdictionCode;
                if (string.IsNullOrWhiteSpace(participantJurisdiction))
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "ALLOWED_JURISDICTION_CHECK",
                        RuleName = "Allowed Jurisdiction",
                        Outcome = "Review",
                        Reason = "Jurisdiction not provided; required for allowed-jurisdiction policy"
                    });
                    reasons.Add("Participant jurisdiction not provided; required for allowed-jurisdiction policy");
                    requiredActions.Add("Provide jurisdiction code");
                    hasReview = true;
                }
                else
                {
                    bool inAllowed = policy.AllowedJurisdictions.Contains(participantJurisdiction, StringComparer.OrdinalIgnoreCase);
                    if (inAllowed)
                    {
                        matchedRules.Add(new MatchedPolicyRule
                        {
                            RuleId = "ALLOWED_JURISDICTION_CHECK",
                            RuleName = "Allowed Jurisdiction",
                            Outcome = "Allow",
                            Reason = $"Jurisdiction '{participantJurisdiction}' is in the allowed jurisdictions list"
                        });
                        reasons.Add($"Jurisdiction '{participantJurisdiction}' is allowed");
                    }
                    else
                    {
                        matchedRules.Add(new MatchedPolicyRule
                        {
                            RuleId = "ALLOWED_JURISDICTION_CHECK",
                            RuleName = "Allowed Jurisdiction",
                            Outcome = "Deny",
                            Reason = $"Jurisdiction '{participantJurisdiction}' is not in the allowed jurisdictions list"
                        });
                        reasons.Add($"Jurisdiction '{participantJurisdiction}' is not permitted for this issuance");
                        hasDeny = true;
                    }
                }
            }

            // Rule 3: Blocked jurisdictions check
            if (policy.BlockedJurisdictions.Count > 0 && !string.IsNullOrWhiteSpace(request.JurisdictionCode))
            {
                bool inBlocked = policy.BlockedJurisdictions.Contains(request.JurisdictionCode, StringComparer.OrdinalIgnoreCase);
                if (inBlocked)
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "BLOCKED_JURISDICTION_CHECK",
                        RuleName = "Blocked Jurisdiction",
                        Outcome = "Deny",
                        Reason = $"Jurisdiction '{request.JurisdictionCode}' is explicitly blocked for this issuance"
                    });
                    reasons.Add($"Jurisdiction '{request.JurisdictionCode}' is blocked for this issuance");
                    hasDeny = true;
                }
                else
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "BLOCKED_JURISDICTION_CHECK",
                        RuleName = "Blocked Jurisdiction",
                        Outcome = "Allow",
                        Reason = $"Jurisdiction '{request.JurisdictionCode}' is not in the blocked list"
                    });
                }
            }

            // Rule 4: KYC check
            if (policy.KycRequired)
            {
                bool kycVerified = request.KycVerified == true;
                if (kycVerified)
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "KYC_CHECK",
                        RuleName = "KYC Verification",
                        Outcome = "Allow",
                        Reason = "KYC verification confirmed"
                    });
                    reasons.Add("KYC verification confirmed");
                }
                else if (request.KycVerified == null)
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "KYC_CHECK",
                        RuleName = "KYC Verification",
                        Outcome = "Review",
                        Reason = "KYC status not provided; required for this policy"
                    });
                    reasons.Add("KYC status not provided; required for this policy");
                    requiredActions.Add("Complete KYC verification");
                    hasReview = true;
                }
                else
                {
                    matchedRules.Add(new MatchedPolicyRule
                    {
                        RuleId = "KYC_CHECK",
                        RuleName = "KYC Verification",
                        Outcome = "Deny",
                        Reason = "KYC verification not completed"
                    });
                    reasons.Add("KYC verification not completed (required by policy)");
                    hasDeny = true;
                }
            }

            // Determine final outcome
            IssuancePolicyOutcome outcome;
            if (hasDeny)
                outcome = IssuancePolicyOutcome.Deny;
            else if (hasReview)
                outcome = IssuancePolicyOutcome.ConditionalReview;
            else
                outcome = IssuancePolicyOutcome.Allow;

            if (outcome == IssuancePolicyOutcome.Allow && reasons.Count == 0)
                reasons.Add("All policy checks passed");

            _logger.LogInformation(
                "Issuance policy evaluated: PolicyId={PolicyId}, Participant={Participant}, Outcome={Outcome}, EvaluatedBy={EvaluatedBy}",
                policyId,
                LoggingHelper.SanitizeLogInput(request.ParticipantAddress),
                outcome,
                LoggingHelper.SanitizeLogInput(evaluatorId));

            return new IssuancePolicyDecisionResult
            {
                DecisionId = decisionId,
                PolicyId = policyId,
                AssetId = policy.AssetId,
                ParticipantAddress = request.ParticipantAddress ?? string.Empty,
                Outcome = outcome,
                MatchedRules = matchedRules,
                Reasons = reasons,
                RequiredActions = requiredActions.Count > 0 ? requiredActions : null,
                PolicyVersion = policy.UpdatedAt.ToString("O"),
                EvaluatedAt = DateTime.UtcNow,
                EvaluatedBy = evaluatorId,
                Success = true
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private async Task<bool> IsParticipantWhitelistedAsync(ulong assetId, string? participantAddress)
        {
            if (string.IsNullOrWhiteSpace(participantAddress))
                return false;

            try
            {
                const int pageSize = 100;
                int page = 1;

                while (true)
                {
                    var listResult = await _whitelistService.ListEntriesAsync(new ListWhitelistRequest
                    {
                        AssetId = assetId,
                        PageSize = pageSize,
                        Page = page
                    });

                    if (!listResult.Success || listResult.Entries == null || listResult.Entries.Count == 0)
                        break;

                    if (listResult.Entries.Any(e =>
                            string.Equals(e.Address, participantAddress, StringComparison.OrdinalIgnoreCase) &&
                            e.Status == WhitelistStatus.Active))
                        return true;

                    // Stop if we've fetched fewer entries than a full page (no more pages)
                    if (listResult.Entries.Count < pageSize)
                        break;

                    page++;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Whitelist check failed for asset {AssetId}", assetId);
                return false;
            }
        }

        private static List<string> NormalizeList(List<string>? input)
        {
            if (input == null) return new List<string>();
            return input
                .Where(j => !string.IsNullOrWhiteSpace(j))
                .Select(j => j.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
        }
    }
}

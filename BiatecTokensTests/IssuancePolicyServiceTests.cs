using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.IssuancePolicy;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive test suite for the issuance compliance policy engine.
    /// Covers unit tests (service logic), integration tests (HTTP API), and contract tests (response shape).
    /// </summary>

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. UNIT TESTS — direct service instantiation, no HTTP overhead
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    [NonParallelizable]
    public class IssuancePolicyUnitTests
    {
        private IIssuancePolicyService _service = null!;
        private const string IssuerA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string IssuerB = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string Participant = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        [SetUp]
        public void SetUp()
        {
            var whitelistService = new WhitelistService(
                new BiatecTokensApi.Repositories.WhitelistRepository(NullLogger<BiatecTokensApi.Repositories.WhitelistRepository>.Instance),
                NullLogger<WhitelistService>.Instance,
                null!, null!, null!);
            _service = new IssuancePolicyService(whitelistService, NullLogger<IssuancePolicyService>.Instance);
        }

        // ── Create ────────────────────────────────────────────────────────────────

        [Test]
        public async Task CreatePolicy_ValidRequest_ReturnsSuccess()
        {
            var req = new CreateIssuancePolicyRequest
            {
                AssetId = 100001,
                PolicyName = "EU Retail Policy",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "DE", "FR" }
            };

            var result = await _service.CreatePolicyAsync(req, IssuerA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Policy, Is.Not.Null);
            Assert.That(result.Policy!.PolicyId, Is.Not.Empty);
            Assert.That(result.Policy.PolicyName, Is.EqualTo("EU Retail Policy"));
            Assert.That(result.Policy.AssetId, Is.EqualTo(100001UL));
            Assert.That(result.Policy.IssuerId, Is.EqualTo(IssuerA));
        }

        [Test]
        public async Task CreatePolicy_EmptyName_ReturnsBadRequest()
        {
            var req = new CreateIssuancePolicyRequest { AssetId = 200001, PolicyName = "" };
            var result = await _service.CreatePolicyAsync(req, IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("PolicyName"));
        }

        [Test]
        public async Task CreatePolicy_ZeroAssetId_ReturnsBadRequest()
        {
            var req = new CreateIssuancePolicyRequest { AssetId = 0, PolicyName = "Test" };
            var result = await _service.CreatePolicyAsync(req, IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("AssetId"));
        }

        [Test]
        public async Task CreatePolicy_JurisdictionInBothAllowedAndBlocked_ReturnsBadRequest()
        {
            var req = new CreateIssuancePolicyRequest
            {
                AssetId = 300001,
                PolicyName = "Bad Policy",
                AllowedJurisdictions = new List<string> { "DE", "FR" },
                BlockedJurisdictions = new List<string> { "FR", "US" }
            };

            var result = await _service.CreatePolicyAsync(req, IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("FR"));
        }

        [Test]
        public async Task CreatePolicy_DefaultsAreCorrect()
        {
            var req = new CreateIssuancePolicyRequest { AssetId = 400001, PolicyName = "Defaults Test" };
            var result = await _service.CreatePolicyAsync(req, IssuerA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Policy!.WhitelistRequired, Is.True);
            Assert.That(result.Policy.KycRequired, Is.False);
            Assert.That(result.Policy.IsActive, Is.True);
            Assert.That(result.Policy.AllowedJurisdictions, Is.Empty);
            Assert.That(result.Policy.BlockedJurisdictions, Is.Empty);
        }

        [Test]
        public async Task CreatePolicy_NormalizesJurisdictionCodes()
        {
            var req = new CreateIssuancePolicyRequest
            {
                AssetId = 500001,
                PolicyName = "Normalize Test",
                AllowedJurisdictions = new List<string> { "de", "fr", "DE" }  // duplicates + lowercase
            };

            var result = await _service.CreatePolicyAsync(req, IssuerA);

            Assert.That(result.Success, Is.True);
            // After normalization: should contain "DE" and "FR" only (uppercased, de-duplicated)
            Assert.That(result.Policy!.AllowedJurisdictions, Does.Contain("DE"));
            Assert.That(result.Policy!.AllowedJurisdictions, Does.Contain("FR"));
            Assert.That(result.Policy!.AllowedJurisdictions.Count, Is.EqualTo(2));
        }

        // ── Get ───────────────────────────────────────────────────────────────────

        [Test]
        public async Task GetPolicy_ByOwner_ReturnsPolicy()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 600001, PolicyName = "Get Test" }, IssuerA);
            var policyId = created.Policy!.PolicyId;

            var result = await _service.GetPolicyAsync(policyId, IssuerA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Policy!.PolicyId, Is.EqualTo(policyId));
        }

        [Test]
        public async Task GetPolicy_ByNonOwner_ReturnsError()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 700001, PolicyName = "Auth Test" }, IssuerA);
            var policyId = created.Policy!.PolicyId;

            var result = await _service.GetPolicyAsync(policyId, IssuerB);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Not authorized"));
        }

        [Test]
        public async Task GetPolicy_NonExistentId_ReturnsError()
        {
            var result = await _service.GetPolicyAsync("nonexistent-id", IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task GetPolicyByAsset_ReturnsMatchingPolicy()
        {
            var assetId = 800001UL;
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetId, PolicyName = "Asset Lookup Test" }, IssuerA);

            var result = await _service.GetPolicyByAssetAsync(assetId, IssuerA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Policy!.AssetId, Is.EqualTo(assetId));
        }

        [Test]
        public async Task GetPolicyByAsset_WrongIssuer_ReturnsError()
        {
            var assetId = 900001UL;
            await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetId, PolicyName = "Isolation Test" }, IssuerA);

            var result = await _service.GetPolicyByAssetAsync(assetId, IssuerB);

            Assert.That(result.Success, Is.False);
        }

        // ── List ──────────────────────────────────────────────────────────────────

        [Test]
        public async Task ListPolicies_ReturnsOnlyIssuerPolicies()
        {
            var assetBase = 1100000UL + (ulong)(DateTime.UtcNow.Ticks % 100000);
            await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetBase + 1, PolicyName = "A-Policy-1" }, IssuerA);
            await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetBase + 2, PolicyName = "A-Policy-2" }, IssuerA);
            await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetBase + 3, PolicyName = "B-Policy-1" }, IssuerB);

            var resultA = await _service.ListPoliciesAsync(IssuerA);
            var resultB = await _service.ListPoliciesAsync(IssuerB);

            Assert.That(resultA.Success, Is.True);
            Assert.That(resultA.Policies.All(p => p.IssuerId == IssuerA), Is.True, "IssuerA should only see own policies");

            Assert.That(resultB.Success, Is.True);
            Assert.That(resultB.Policies.All(p => p.IssuerId == IssuerB), Is.True, "IssuerB should only see own policies");
        }

        [Test]
        public async Task ListPolicies_TotalCountMatchesPolicies()
        {
            var assetBase = 1200000UL + (ulong)(DateTime.UtcNow.Ticks % 100000);
            var issuer = IssuerA[..20] + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetBase + 1, PolicyName = "Policy X" }, issuer);
            await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = assetBase + 2, PolicyName = "Policy Y" }, issuer);

            var result = await _service.ListPoliciesAsync(issuer);

            Assert.That(result.TotalCount, Is.EqualTo(result.Policies.Count));
            Assert.That(result.TotalCount, Is.EqualTo(2));
        }

        // ── Update ────────────────────────────────────────────────────────────────

        [Test]
        public async Task UpdatePolicy_ByOwner_ReturnsUpdatedPolicy()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 1300001, PolicyName = "Before Update" }, IssuerA);
            var policyId = created.Policy!.PolicyId;

            var updateReq = new UpdateIssuancePolicyRequest
            {
                PolicyName = "After Update",
                KycRequired = true,
                IsActive = false
            };

            var result = await _service.UpdatePolicyAsync(policyId, updateReq, IssuerA);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Policy!.PolicyName, Is.EqualTo("After Update"));
            Assert.That(result.Policy.KycRequired, Is.True);
            Assert.That(result.Policy.IsActive, Is.False);
        }

        [Test]
        public async Task UpdatePolicy_ByNonOwner_ReturnsError()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 1400001, PolicyName = "Auth Update Test" }, IssuerA);
            var policyId = created.Policy!.PolicyId;

            var result = await _service.UpdatePolicyAsync(policyId, new UpdateIssuancePolicyRequest { PolicyName = "Hijack" }, IssuerB);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Not authorized"));
        }

        [Test]
        public async Task UpdatePolicy_EmptyName_ReturnsError()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 1500001, PolicyName = "Valid Name" }, IssuerA);

            var result = await _service.UpdatePolicyAsync(
                created.Policy!.PolicyId,
                new UpdateIssuancePolicyRequest { PolicyName = "  " },
                IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("PolicyName"));
        }

        [Test]
        public async Task UpdatePolicy_ConflictingJurisdictions_ReturnsError()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 1600001, PolicyName = "Conflict Test" }, IssuerA);

            var result = await _service.UpdatePolicyAsync(
                created.Policy!.PolicyId,
                new UpdateIssuancePolicyRequest
                {
                    AllowedJurisdictions = new List<string> { "US" },
                    BlockedJurisdictions = new List<string> { "US" }
                },
                IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("US"));
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        [Test]
        public async Task DeletePolicy_ByOwner_Succeeds()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 1700001, PolicyName = "To Delete" }, IssuerA);
            var policyId = created.Policy!.PolicyId;

            var deleteResult = await _service.DeletePolicyAsync(policyId, IssuerA);
            Assert.That(deleteResult.Success, Is.True);

            // Policy should no longer be retrievable
            var getResult = await _service.GetPolicyAsync(policyId, IssuerA);
            Assert.That(getResult.Success, Is.False);
        }

        [Test]
        public async Task DeletePolicy_ByNonOwner_ReturnsError()
        {
            var created = await _service.CreatePolicyAsync(
                new CreateIssuancePolicyRequest { AssetId = 1800001, PolicyName = "Delete Auth Test" }, IssuerA);

            var result = await _service.DeletePolicyAsync(created.Policy!.PolicyId, IssuerB);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Not authorized"));
        }

        [Test]
        public async Task DeletePolicy_NonExistent_ReturnsError()
        {
            var result = await _service.DeletePolicyAsync("ghost-policy-id", IssuerA);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        // ── Evaluate ──────────────────────────────────────────────────────────────

        [Test]
        public async Task Evaluate_AllowedJurisdiction_AllowOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2000001,
                PolicyName = "EU-only Policy",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "DE", "FR" }
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = "DE" },
                IssuerA);

            Assert.That(decision.Success, Is.True);
            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Allow));
            Assert.That(decision.Reasons, Is.Not.Empty);
        }

        [Test]
        public async Task Evaluate_NotInAllowedJurisdiction_DenyOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2100001,
                PolicyName = "EU-only Policy 2",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "DE", "FR" }
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = "US" },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Deny));
            Assert.That(decision.Reasons.Any(r => r.Contains("US")), Is.True);
        }

        [Test]
        public async Task Evaluate_BlockedJurisdiction_DenyOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2200001,
                PolicyName = "Sanctions Policy",
                WhitelistRequired = false,
                BlockedJurisdictions = new List<string> { "RU", "KP" }
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = "RU" },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Deny));
            Assert.That(decision.MatchedRules.Any(r => r.RuleId == "BLOCKED_JURISDICTION_CHECK"), Is.True);
        }

        [Test]
        public async Task Evaluate_NotInBlockedJurisdiction_AllowOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2300001,
                PolicyName = "Partial Block Policy",
                WhitelistRequired = false,
                BlockedJurisdictions = new List<string> { "RU" }
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = "DE" },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Allow));
        }

        [Test]
        public async Task Evaluate_KycRequiredAndVerified_AllowOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2400001,
                PolicyName = "KYC Policy",
                WhitelistRequired = false,
                KycRequired = true
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, KycVerified = true },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Allow));
            Assert.That(decision.MatchedRules.Any(r => r.RuleId == "KYC_CHECK" && r.Outcome == "Allow"), Is.True);
        }

        [Test]
        public async Task Evaluate_KycRequiredAndNotVerified_DenyOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2500001,
                PolicyName = "KYC Strict Policy",
                WhitelistRequired = false,
                KycRequired = true
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, KycVerified = false },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Deny));
        }

        [Test]
        public async Task Evaluate_KycRequiredAndStatusUnknown_ConditionalReviewOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2600001,
                PolicyName = "KYC Conditional Policy",
                WhitelistRequired = false,
                KycRequired = true
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, KycVerified = null },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.ConditionalReview));
            Assert.That(decision.RequiredActions, Is.Not.Null);
            Assert.That(decision.RequiredActions!, Does.Contain("Complete KYC verification"));
        }

        [Test]
        public async Task Evaluate_AllowedJurisdictionNotProvided_ConditionalReview()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2700001,
                PolicyName = "Jurisdiction Required Policy",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "US" }
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = null },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.ConditionalReview));
        }

        [Test]
        public async Task Evaluate_NonExistentPolicy_ReturnsErrorResult()
        {
            var decision = await _service.EvaluateParticipantAsync(
                "nonexistent-policy",
                new EvaluateParticipantRequest { ParticipantAddress = Participant },
                IssuerA);

            Assert.That(decision.Success, Is.False);
            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Deny));
            Assert.That(decision.ErrorMessage, Is.Not.Null);
        }

        [Test]
        public async Task Evaluate_DecisionContainsRequiredAuditFields()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 2900001,
                PolicyName = "Audit Fields Test",
                WhitelistRequired = false
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant },
                IssuerA);

            Assert.That(decision.DecisionId, Is.Not.Empty);
            Assert.That(decision.PolicyId, Is.EqualTo(created.Policy.PolicyId));
            Assert.That(decision.ParticipantAddress, Is.EqualTo(Participant));
            Assert.That(decision.EvaluatedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
            Assert.That(decision.EvaluatedBy, Is.EqualTo(IssuerA));
            Assert.That(decision.PolicyVersion, Is.Not.Empty);
        }

        [Test]
        public async Task Evaluate_MatchedRulesReflectChecksPerformed()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 3000001,
                PolicyName = "Multi-Rule Policy",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "DE" },
                KycRequired = true
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = "DE", KycVerified = true },
                IssuerA);

            Assert.That(decision.MatchedRules.Any(r => r.RuleId == "ALLOWED_JURISDICTION_CHECK"), Is.True);
            Assert.That(decision.MatchedRules.Any(r => r.RuleId == "KYC_CHECK"), Is.True);
        }

        [Test]
        public async Task Evaluate_NoChecksRequired_AllowOutcome()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 3100001,
                PolicyName = "Open Policy",
                WhitelistRequired = false,
                KycRequired = false
            }, IssuerA);

            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Allow));
        }

        [Test]
        public async Task Evaluate_EachDecisionHasUniqueDecisionId()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 3200001,
                PolicyName = "Uniqueness Test",
                WhitelistRequired = false
            }, IssuerA);

            var d1 = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant },
                IssuerA);
            var d2 = await _service.EvaluateParticipantAsync(
                created.Policy.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant },
                IssuerA);

            Assert.That(d1.DecisionId, Is.Not.EqualTo(d2.DecisionId));
        }

        [Test]
        public async Task Evaluate_CaseInsensitiveJurisdictionMatch()
        {
            var created = await _service.CreatePolicyAsync(new CreateIssuancePolicyRequest
            {
                AssetId = 3300001,
                PolicyName = "Case Test",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "DE" }
            }, IssuerA);

            // Pass lowercase jurisdiction code - should still match
            var decision = await _service.EvaluateParticipantAsync(
                created.Policy!.PolicyId,
                new EvaluateParticipantRequest { ParticipantAddress = Participant, JurisdictionCode = "de" },
                IssuerA);

            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Allow));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. INTEGRATION TESTS — HTTP via WebApplicationFactory
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    [NonParallelizable]
    public class IssuancePolicyIntegrationTests
    {
        private IssuancePolicyTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new IssuancePolicyTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var email = $"ip-test-{Guid.NewGuid():N}@biatec.io";
            var reg = new RegisterRequest
            {
                Email = email,
                Password = "IssuancePolicyTest123!",
                ConfirmPassword = "IssuancePolicyTest123!"
            };

            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", reg);
            if (regResp.IsSuccessStatusCode)
            {
                var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
                _accessToken = regBody?.AccessToken;
            }

            _authClient = _factory.CreateClient();
            if (_accessToken != null)
            {
                _authClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _authClient?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        // ── Authorization ─────────────────────────────────────────────────────────

        [Test]
        public async Task CreatePolicy_Unauthenticated_Returns401()
        {
            var body = new { assetId = 11111, policyName = "Test" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ListPolicies_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/compliance/issuance-policies");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetPolicy_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/compliance/issuance-policies/some-id");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Evaluate_Unauthenticated_Returns401()
        {
            var body = new { participantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies/some-id/evaluate", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── CRUD ──────────────────────────────────────────────────────────────────

        [Test]
        public async Task CreatePolicy_ValidRequest_Returns200()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var body = new CreateIssuancePolicyRequest
            {
                AssetId = 4000001UL,
                PolicyName = "Integration Test Policy",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "US", "DE" },
                KycRequired = false
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Policy, Is.Not.Null);
            Assert.That(result.Policy!.PolicyName, Is.EqualTo("Integration Test Policy"));
        }

        [Test]
        public async Task CreatePolicy_EmptyName_Returns400()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var body = new { assetId = 4100001, policyName = "" };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task CreatePolicy_ConflictingJurisdictions_Returns400()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var body = new CreateIssuancePolicyRequest
            {
                AssetId = 4200001UL,
                PolicyName = "Bad Policy",
                AllowedJurisdictions = new List<string> { "US" },
                BlockedJurisdictions = new List<string> { "US" }
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.False);
        }

        [Test]
        public async Task ListPolicies_Authenticated_Returns200WithList()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var resp = await _authClient.GetAsync("/api/v1/compliance/issuance-policies");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyListResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Policies, Is.Not.Null);
        }

        [Test]
        public async Task GetPolicy_AfterCreate_Returns200()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            // Create
            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 4300001UL,
                PolicyName = "Get By ID Test"
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            var policyId = created!.Policy!.PolicyId;

            // Get
            var getResp = await _authClient.GetAsync($"/api/v1/compliance/issuance-policies/{policyId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await getResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Policy!.PolicyId, Is.EqualTo(policyId));
        }

        [Test]
        public async Task GetPolicyByAsset_AfterCreate_Returns200()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var assetId = 4400001UL;
            var createBody = new CreateIssuancePolicyRequest { AssetId = assetId, PolicyName = "Asset Lookup Integration" };
            await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);

            var resp = await _authClient.GetAsync($"/api/v1/compliance/issuance-policies/asset/{assetId}");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Policy!.AssetId, Is.EqualTo(assetId));
        }

        [Test]
        public async Task UpdatePolicy_ValidRequest_Returns200WithUpdatedPolicy()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            // Create
            var createBody = new CreateIssuancePolicyRequest { AssetId = 4500001UL, PolicyName = "Before" };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            var policyId = created!.Policy!.PolicyId;

            // Update
            var updateBody = new UpdateIssuancePolicyRequest { PolicyName = "After", KycRequired = true };
            var updateResp = await _authClient.PutAsJsonAsync($"/api/v1/compliance/issuance-policies/{policyId}", updateBody);
            Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await updateResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Policy!.PolicyName, Is.EqualTo("After"));
            Assert.That(result.Policy.KycRequired, Is.True);
        }

        [Test]
        public async Task DeletePolicy_AfterCreate_Returns200()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest { AssetId = 4600001UL, PolicyName = "To Delete Integration" };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            var policyId = created!.Policy!.PolicyId;

            var deleteResp = await _authClient.DeleteAsync($"/api/v1/compliance/issuance-policies/{policyId}");
            Assert.That(deleteResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await deleteResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True);
        }

        [Test]
        public async Task DeletePolicy_ThenGet_Returns400()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest { AssetId = 4700001UL, PolicyName = "Delete Then Get" };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            var policyId = created!.Policy!.PolicyId;

            await _authClient.DeleteAsync($"/api/v1/compliance/issuance-policies/{policyId}");

            var getResp = await _authClient.GetAsync($"/api/v1/compliance/issuance-policies/{policyId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // ── Evaluate ──────────────────────────────────────────────────────────────

        [Test]
        public async Task Evaluate_AllowedJurisdiction_Returns200WithAllowOutcome()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 5000001UL,
                PolicyName = "EU Integration Eval",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "DE", "FR" }
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                JurisdictionCode = "DE"
            };

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}/evaluate",
                evalBody);

            Assert.That(evalResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var decision = await evalResp.Content.ReadFromJsonAsync<IssuancePolicyDecisionResult>();
            Assert.That(decision!.Success, Is.True);
            Assert.That(decision.Outcome, Is.EqualTo(IssuancePolicyOutcome.Allow));
        }

        [Test]
        public async Task Evaluate_BlockedJurisdiction_Returns200WithDenyOutcome()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 5100001UL,
                PolicyName = "Sanctions Integration Eval",
                WhitelistRequired = false,
                BlockedJurisdictions = new List<string> { "RU" }
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                JurisdictionCode = "RU"
            };

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}/evaluate",
                evalBody);

            Assert.That(evalResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var decision = await evalResp.Content.ReadFromJsonAsync<IssuancePolicyDecisionResult>();
            Assert.That(decision!.Outcome, Is.EqualTo(IssuancePolicyOutcome.Deny));
        }

        [Test]
        public async Task Evaluate_NonExistentPolicy_Returns400()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            var resp = await _authClient.PostAsJsonAsync(
                "/api/v1/compliance/issuance-policies/nonexistent-policy/evaluate",
                evalBody);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Evaluate_KycRequiredAndNotVerified_DenyOutcome_Integration()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 5300001UL,
                PolicyName = "KYC Integration Eval",
                WhitelistRequired = false,
                KycRequired = true
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                KycVerified = false
            };

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}/evaluate",
                evalBody);

            var decision = await evalResp.Content.ReadFromJsonAsync<IssuancePolicyDecisionResult>();
            Assert.That(decision!.Outcome, Is.EqualTo(IssuancePolicyOutcome.Deny));
        }

        [Test]
        public async Task CreateThenListThenDelete_PoliciesCountChanges()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            // Count before
            var listBefore = await _authClient.GetAsync("/api/v1/compliance/issuance-policies");
            var before = await listBefore.Content.ReadFromJsonAsync<IssuancePolicyListResponse>();
            int countBefore = before!.TotalCount;

            // Create
            var createBody = new CreateIssuancePolicyRequest { AssetId = 5400001UL, PolicyName = "Count Test" };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            // Count after create
            var listAfter = await _authClient.GetAsync("/api/v1/compliance/issuance-policies");
            var after = await listAfter.Content.ReadFromJsonAsync<IssuancePolicyListResponse>();
            Assert.That(after!.TotalCount, Is.EqualTo(countBefore + 1));

            // Delete
            await _authClient.DeleteAsync($"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}");

            // Count after delete
            var listFinal = await _authClient.GetAsync("/api/v1/compliance/issuance-policies");
            var final = await listFinal.Content.ReadFromJsonAsync<IssuancePolicyListResponse>();
            Assert.That(final!.TotalCount, Is.EqualTo(countBefore));
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────────

        private sealed class IssuancePolicyTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "IssuancePolicyIntegrationTestKey32MinCharsRequired!!",
                        ["JwtConfig:SecretKey"] = "IssuancePolicyControllerIntegrationTestSecretKey32CharMin!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. CONTRACT TESTS — verify response shape and required fields
    // ─────────────────────────────────────────────────────────────────────────────

    [TestFixture]
    [NonParallelizable]
    public class IssuancePolicyContractTests
    {
        private ContractTestFactory _factory = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new ContractTestFactory();
            var unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var email = $"ip-contract-{Guid.NewGuid():N}@biatec.io";
            var reg = new RegisterRequest
            {
                Email = email,
                Password = "ContractTest123!",
                ConfirmPassword = "ContractTest123!"
            };

            var regResp = await unauthClient.PostAsJsonAsync("/api/v1/auth/register", reg);
            if (regResp.IsSuccessStatusCode)
            {
                var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
                _accessToken = regBody?.AccessToken;
            }

            _authClient = _factory.CreateClient();
            if (_accessToken != null)
            {
                _authClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _authClient?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task CreatePolicyResponse_HasAllRequiredFields()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var body = new CreateIssuancePolicyRequest { AssetId = 6000001UL, PolicyName = "Contract Test Policy" };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", body);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Response must have 'success' field");
            Assert.That(root.TryGetProperty("policy", out var policyEl), Is.True, "Response must have 'policy' field");

            Assert.That(policyEl.TryGetProperty("policyId", out _), Is.True, "'policy' must have 'policyId'");
            Assert.That(policyEl.TryGetProperty("issuerId", out _), Is.True, "'policy' must have 'issuerId'");
            Assert.That(policyEl.TryGetProperty("assetId", out _), Is.True, "'policy' must have 'assetId'");
            Assert.That(policyEl.TryGetProperty("policyName", out _), Is.True, "'policy' must have 'policyName'");
            Assert.That(policyEl.TryGetProperty("whitelistRequired", out _), Is.True, "'policy' must have 'whitelistRequired'");
            Assert.That(policyEl.TryGetProperty("allowedJurisdictions", out _), Is.True, "'policy' must have 'allowedJurisdictions'");
            Assert.That(policyEl.TryGetProperty("blockedJurisdictions", out _), Is.True, "'policy' must have 'blockedJurisdictions'");
            Assert.That(policyEl.TryGetProperty("kycRequired", out _), Is.True, "'policy' must have 'kycRequired'");
            Assert.That(policyEl.TryGetProperty("isActive", out _), Is.True, "'policy' must have 'isActive'");
            Assert.That(policyEl.TryGetProperty("createdAt", out _), Is.True, "'policy' must have 'createdAt'");
            Assert.That(policyEl.TryGetProperty("updatedAt", out _), Is.True, "'policy' must have 'updatedAt'");
            Assert.That(policyEl.TryGetProperty("createdBy", out _), Is.True, "'policy' must have 'createdBy'");
        }

        [Test]
        public async Task ListPoliciesResponse_HasAllRequiredFields()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var resp = await _authClient.GetAsync("/api/v1/compliance/issuance-policies");
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Response must have 'success' field");
            Assert.That(root.TryGetProperty("policies", out _), Is.True, "Response must have 'policies' field");
            Assert.That(root.TryGetProperty("totalCount", out _), Is.True, "Response must have 'totalCount' field");
        }

        [Test]
        public async Task DecisionResult_HasAllRequiredAuditFields()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 6100001UL,
                PolicyName = "Decision Schema Test",
                WhitelistRequired = false
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                JurisdictionCode = "US"
            };

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}/evaluate",
                evalBody);

            var json = await evalResp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("decisionId", out var decisionId), Is.True, "Must have 'decisionId'");
            Assert.That(decisionId.GetString(), Is.Not.Empty, "'decisionId' must be non-empty");

            Assert.That(root.TryGetProperty("policyId", out _), Is.True, "Must have 'policyId'");
            Assert.That(root.TryGetProperty("assetId", out _), Is.True, "Must have 'assetId'");
            Assert.That(root.TryGetProperty("participantAddress", out _), Is.True, "Must have 'participantAddress'");
            Assert.That(root.TryGetProperty("outcome", out _), Is.True, "Must have 'outcome'");
            Assert.That(root.TryGetProperty("matchedRules", out _), Is.True, "Must have 'matchedRules'");
            Assert.That(root.TryGetProperty("reasons", out _), Is.True, "Must have 'reasons'");
            Assert.That(root.TryGetProperty("policyVersion", out _), Is.True, "Must have 'policyVersion'");
            Assert.That(root.TryGetProperty("evaluatedAt", out _), Is.True, "Must have 'evaluatedAt'");
            Assert.That(root.TryGetProperty("evaluatedBy", out var evaluatedBy), Is.True, "Must have 'evaluatedBy'");
            Assert.That(evaluatedBy.GetString(), Is.Not.Empty, "'evaluatedBy' must be non-empty");
            Assert.That(root.TryGetProperty("success", out _), Is.True, "Must have 'success'");
        }

        [Test]
        public async Task DecisionResult_OutcomeIsValidEnumValue()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 6200001UL,
                PolicyName = "Outcome Enum Test",
                WhitelistRequired = false
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}/evaluate",
                evalBody);

            var decision = await evalResp.Content.ReadFromJsonAsync<IssuancePolicyDecisionResult>();
            Assert.That(
                Enum.IsDefined(typeof(IssuancePolicyOutcome), decision!.Outcome),
                Is.True,
                "Outcome must be a valid IssuancePolicyOutcome enum value");
        }

        [Test]
        public async Task DecisionResult_MatchedRulesHaveRequiredFields()
        {
            if (_accessToken == null) Assert.Ignore("No auth token available");

            var createBody = new CreateIssuancePolicyRequest
            {
                AssetId = 6300001UL,
                PolicyName = "Matched Rules Schema",
                WhitelistRequired = false,
                AllowedJurisdictions = new List<string> { "US" }
            };
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/issuance-policies", createBody);
            var created = await createResp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();

            var evalBody = new EvaluateParticipantRequest
            {
                ParticipantAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                JurisdictionCode = "US"
            };

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/compliance/issuance-policies/{created!.Policy!.PolicyId}/evaluate",
                evalBody);

            var json = await evalResp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var rules = doc.RootElement.GetProperty("matchedRules");

            Assert.That(rules.GetArrayLength(), Is.GreaterThan(0), "matchedRules must contain at least one rule");

            foreach (var rule in rules.EnumerateArray())
            {
                Assert.That(rule.TryGetProperty("ruleId", out _), Is.True, "Each rule must have 'ruleId'");
                Assert.That(rule.TryGetProperty("ruleName", out _), Is.True, "Each rule must have 'ruleName'");
                Assert.That(rule.TryGetProperty("outcome", out _), Is.True, "Each rule must have 'outcome'");
                Assert.That(rule.TryGetProperty("reason", out _), Is.True, "Each rule must have 'reason'");
            }
        }

        [Test]
        public async Task SwaggerSpec_IssuancePolicyEndpoints_Reachable()
        {
            // Verify the Swagger spec is accessible and includes our new endpoints
            var resp = await _authClient.GetAsync("/swagger/v1/swagger.json");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Swagger spec should be accessible (new types shouldn't cause schema conflicts)");

            var json = await resp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("issuance-policies"), "Swagger spec should contain issuance-policies routes");
        }

        private sealed class ContractTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "IssuancePolicyContractTestKey32MinCharsRequired!!!!",
                        ["JwtConfig:SecretKey"] = "IssuancePolicyContractTestSecretKey32CharMin!!!!!!!!!!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }
    }
}

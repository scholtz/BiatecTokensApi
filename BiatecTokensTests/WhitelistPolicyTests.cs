using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for the WhitelistPolicy engine: CRUD, validation, eligibility evaluation (fail-closed semantics),
    /// and HTTP API contract (auth, routes, status codes).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WhitelistPolicyTests
    {
        // ── Algorand test addresses ───────────────────────────────────────────────
        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // ── Unit-level service factory ────────────────────────────────────────────

        private static IWhitelistPolicyService CreateService()
            => new WhitelistPolicyService(NullLogger<WhitelistPolicyService>.Instance);

        // ── HTTP test factory ─────────────────────────────────────────────────────

        private PolicyTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new PolicyTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var email = $"policy-{Guid.NewGuid():N}@biatec.io";
            var reg = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "PolicyTest123!", ConfirmPassword = "PolicyTest123!" });

            Assert.That(reg.IsSuccessStatusCode,
                $"Registration failed with status {reg.StatusCode}. Authenticated tests will not work.");

            var body = await reg.Content.ReadFromJsonAsync<RegisterResponse>();
            var token = body?.AccessToken;
            Assert.That(token, Is.Not.Null,
                "Access token was null after successful registration. Authenticated tests will not work.");

            _authClient = _factory.CreateClient();
            _authClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _authClient?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        private sealed class PolicyTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "PolicyTestKey32MinCharsRequired!!!!!!!!!",
                        ["JwtConfig:SecretKey"] = "PolicyTestSecretKey32CharMinRequired!!!!!!",
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

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT TESTS — WhitelistPolicyService directly
        // ═══════════════════════════════════════════════════════════════════════

        // ── CreatePolicy ──────────────────────────────────────────────────────────

        [Test]
        public async Task CreatePolicy_ValidRequest_ReturnsDraftPolicy()
        {
            var svc = CreateService();
            var result = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Test Policy",
                AssetId = 1001
            }, "creator@test");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Policy, Is.Not.Null);
            Assert.That(result.Policy!.Status, Is.EqualTo(WhitelistPolicyStatus.Draft));
            Assert.That(result.Policy.PolicyName, Is.EqualTo("Test Policy"));
            Assert.That(result.Policy.AssetId, Is.EqualTo(1001UL));
            Assert.That(result.Policy.CreatedBy, Is.EqualTo("creator@test"));
            Assert.That(result.Policy.Version, Is.EqualTo(1));
        }

        [Test]
        public async Task CreatePolicy_AssignsPolicyId_NonEmpty()
        {
            var svc = CreateService();
            var r = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 1 }, "u");
            Assert.That(r.Policy!.PolicyId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task CreatePolicy_WithAllowedAddresses_StoresThem()
        {
            var svc = CreateService();
            var r = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "P", AssetId = 1,
                AllowedAddresses = new List<string> { AlgoAddr1, AlgoAddr2 }
            }, "u");

            Assert.That(r.Policy!.AllowedAddresses, Has.Count.EqualTo(2));
        }

        // ── GetPolicy ─────────────────────────────────────────────────────────────

        [Test]
        public async Task GetPolicy_UnknownId_ReturnsFailure()
        {
            var svc = CreateService();
            var r = await svc.GetPolicyAsync("does-not-exist");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("POLICY_NOT_FOUND"));
        }

        [Test]
        public async Task GetPolicy_KnownId_ReturnsPolicy()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 2 }, "u");
            var r = await svc.GetPolicyAsync(created.Policy!.PolicyId);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Policy!.PolicyId, Is.EqualTo(created.Policy.PolicyId));
        }

        // ── GetPolicies ───────────────────────────────────────────────────────────

        [Test]
        public async Task GetPolicies_NoFilter_ReturnsAll()
        {
            var svc = CreateService();
            await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P1", AssetId = 100 }, "u");
            await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P2", AssetId = 200 }, "u");
            var r = await svc.GetPoliciesAsync();
            Assert.That(r.Policies.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task GetPolicies_FilterByAssetId_ReturnsOnlyMatching()
        {
            var svc = CreateService();
            await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P1", AssetId = 9901 }, "u");
            await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P2", AssetId = 9902 }, "u");

            var r = await svc.GetPoliciesAsync(9901);
            Assert.That(r.Policies.All(p => p.AssetId == 9901), Is.True);
        }

        // ── UpdatePolicy ──────────────────────────────────────────────────────────

        [Test]
        public async Task UpdatePolicy_ActivatesPolicy_IncreasesVersion()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 3 }, "u");
            var r = await svc.UpdatePolicyAsync(c.Policy!.PolicyId, new UpdateWhitelistPolicyRequest
            {
                Status = WhitelistPolicyStatus.Active
            }, "updater");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Policy!.Status, Is.EqualTo(WhitelistPolicyStatus.Active));
            Assert.That(r.Policy.Version, Is.EqualTo(2));
            Assert.That(r.Policy.UpdatedBy, Is.EqualTo("updater"));
        }

        [Test]
        public async Task UpdatePolicy_ArchivedPolicy_ReturnsBadRequest()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 4 }, "u");
            await svc.ArchivePolicyAsync(c.Policy!.PolicyId, "u");
            var r = await svc.UpdatePolicyAsync(c.Policy.PolicyId, new UpdateWhitelistPolicyRequest { PolicyName = "New" }, "u");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("POLICY_ARCHIVED"));
        }

        [Test]
        public async Task UpdatePolicy_UnknownId_ReturnsPolicyNotFound()
        {
            var svc = CreateService();
            var r = await svc.UpdatePolicyAsync("ghost", new UpdateWhitelistPolicyRequest { PolicyName = "X" }, "u");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("POLICY_NOT_FOUND"));
        }

        // ── ArchivePolicy ─────────────────────────────────────────────────────────

        [Test]
        public async Task ArchivePolicy_ExistingPolicy_SetsArchived()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 5 }, "u");
            var r = await svc.ArchivePolicyAsync(c.Policy!.PolicyId, "archiver");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Policy!.Status, Is.EqualTo(WhitelistPolicyStatus.Archived));
        }

        [Test]
        public async Task ArchivePolicy_UnknownId_ReturnsFailure()
        {
            var svc = CreateService();
            var r = await svc.ArchivePolicyAsync("no-such", "u");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("POLICY_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT TESTS — ValidatePolicyAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ValidatePolicy_EmptyRules_ReturnsWarning()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 6 }, "u");
            var r = await svc.ValidatePolicyAsync(c.Policy!.PolicyId);

            Assert.That(r.Success, Is.True);
            Assert.That(r.IsValid, Is.True, "Warnings alone should not make IsValid=false");
            Assert.That(r.Issues.Any(i => i.IssueCode == "POLICY_EMPTY"), Is.True);
        }

        [Test]
        public async Task ValidatePolicy_AddressBothAllowAndDeny_ReturnsError()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "P", AssetId = 7,
                AllowedAddresses = new List<string> { AlgoAddr1 },
                DeniedAddresses = new List<string> { AlgoAddr1 }
            }, "u");

            var r = await svc.ValidatePolicyAsync(c.Policy!.PolicyId);
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.Issues.Any(i => i.IssueCode == "ADDR_ALLOW_DENY_CONFLICT"), Is.True);
            Assert.That(r.Issues.Any(i => i.Severity == "Error"), Is.True);
        }

        [Test]
        public async Task ValidatePolicy_JurisdictionBothAllowedAndBlocked_ReturnsError()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "P", AssetId = 8,
                AllowedJurisdictions = new List<string> { "US" },
                BlockedJurisdictions = new List<string> { "US" }
            }, "u");

            var r = await svc.ValidatePolicyAsync(c.Policy!.PolicyId);
            Assert.That(r.IsValid, Is.False);
            Assert.That(r.Issues.Any(i => i.IssueCode == "JURISDICTION_ALLOW_BLOCK_CONFLICT"), Is.True);
        }

        [Test]
        public async Task ValidatePolicy_ValidPolicy_NoErrors()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Good Policy", AssetId = 9,
                AllowedAddresses = new List<string> { AlgoAddr1 },
                AllowedJurisdictions = new List<string> { "US", "DE" }
            }, "u");

            var r = await svc.ValidatePolicyAsync(c.Policy!.PolicyId);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Issues.Any(i => i.Severity == "Error"), Is.False);
        }

        [Test]
        public async Task ValidatePolicy_UnknownId_ReturnsNotFound()
        {
            var svc = CreateService();
            var r = await svc.ValidatePolicyAsync("ghost");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("POLICY_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT TESTS — EvaluateEligibilityAsync (core fail-closed logic)
        // ═══════════════════════════════════════════════════════════════════════

        private async Task<string> CreateActivePolicy(
            IWhitelistPolicyService svc,
            ulong assetId,
            List<string>? allowedAddr = null,
            List<string>? deniedAddr = null,
            List<string>? allowedJur = null,
            List<string>? blockedJur = null,
            List<WhitelistPolicyInvestorCategory>? requiredCats = null)
        {
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "TestPolicy",
                AssetId = assetId,
                AllowedAddresses = allowedAddr ?? new(),
                DeniedAddresses = deniedAddr ?? new(),
                AllowedJurisdictions = allowedJur ?? new(),
                BlockedJurisdictions = blockedJur ?? new(),
                RequiredInvestorCategories = requiredCats ?? new()
            }, "system");

            await svc.UpdatePolicyAsync(c.Policy!.PolicyId, new UpdateWhitelistPolicyRequest
            {
                Status = WhitelistPolicyStatus.Active
            }, "system");

            return c.Policy!.PolicyId;
        }

        // ── Fail-closed: Draft policy ─────────────────────────────────────────────

        [Test]
        public async Task Evaluate_DraftPolicy_AlwaysDenies()
        {
            var svc = CreateService();
            var c = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Draft", AssetId = 100,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "u");

            // Policy stays in Draft — do NOT activate
            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = c.Policy!.PolicyId,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "US",
                InvestorCategory = WhitelistPolicyInvestorCategory.AccreditedInvestor
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.IsFailClosed, Is.True);
        }

        // ── Fail-closed: Archived policy ─────────────────────────────────────────

        [Test]
        public async Task Evaluate_ArchivedPolicy_AlwaysDenies()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 101, allowedAddr: new List<string> { AlgoAddr1 });
            await svc.ArchivePolicyAsync(id, "u");

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.IsFailClosed, Is.True);
        }

        // ── Fail-closed: empty active policy ─────────────────────────────────────

        [Test]
        public async Task Evaluate_EmptyActivePolicy_DeniesFailClosed()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 102);

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.IsFailClosed, Is.True);
        }

        // ── Fail-closed: unknown policy ───────────────────────────────────────────

        [Test]
        public async Task Evaluate_UnknownPolicy_DeniesFailClosed()
        {
            var svc = CreateService();
            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = "nonexistent",
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.IsFailClosed, Is.True);
        }

        // ── Denylist overrides allowlist ──────────────────────────────────────────

        [Test]
        public async Task Evaluate_AddressOnDenylist_DeniesRegardlessOfAllowlist()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 103,
                allowedAddr: new List<string> { AlgoAddr1 },
                deniedAddr: new List<string> { AlgoAddr1 });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.IsFailClosed, Is.False, "Hard deny, not fail-closed");
        }

        // ── Blocked jurisdiction ──────────────────────────────────────────────────

        [Test]
        public async Task Evaluate_BlockedJurisdiction_Denies()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 104,
                allowedAddr: new List<string> { AlgoAddr1 },
                blockedJur: new List<string> { "KP" });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "KP"
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.Reasons.Any(reason => reason.Contains("KP")), Is.True);
        }

        // ── Jurisdiction not in allowed list ──────────────────────────────────────

        [Test]
        public async Task Evaluate_JurisdictionNotInAllowedList_Denies()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 105,
                allowedAddr: new List<string> { AlgoAddr1 },
                allowedJur: new List<string> { "DE", "FR" });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "US"   // Not in allowed list
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
        }

        // ── Missing jurisdiction when required ────────────────────────────────────

        [Test]
        public async Task Evaluate_NoJurisdictionProvidedWhenRequired_Denies()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 106,
                allowedAddr: new List<string> { AlgoAddr1 },
                allowedJur: new List<string> { "DE" });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = null   // Missing
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
        }

        // ── Wrong investor category ───────────────────────────────────────────────

        [Test]
        public async Task Evaluate_WrongInvestorCategory_Denies()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 107,
                allowedAddr: new List<string> { AlgoAddr1 },
                requiredCats: new List<WhitelistPolicyInvestorCategory> { WhitelistPolicyInvestorCategory.AccreditedInvestor });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                InvestorCategory = WhitelistPolicyInvestorCategory.RetailInvestor
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(r.Reasons.Any(reason => reason.Contains("RetailInvestor")), Is.True);
        }

        // ── Address not on allowlist when allowlist is non-empty ──────────────────

        [Test]
        public async Task Evaluate_AddressNotOnAllowlist_Denies()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 108,
                allowedAddr: new List<string> { AlgoAddr1 });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr2   // Not on allowlist
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
        }

        // ── Happy path: all checks pass → Allow ───────────────────────────────────

        [Test]
        public async Task Evaluate_AllCriteriaSatisfied_Allows()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 109,
                allowedAddr: new List<string> { AlgoAddr1 },
                allowedJur: new List<string> { "US" },
                requiredCats: new List<WhitelistPolicyInvestorCategory> { WhitelistPolicyInvestorCategory.AccreditedInvestor });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "US",
                InvestorCategory = WhitelistPolicyInvestorCategory.AccreditedInvestor
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Allow));
            Assert.That(r.IsFailClosed, Is.False);
        }

        // ── Happy path: only denylist/blocked rules, participant not on them → Allow

        [Test]
        public async Task Evaluate_OnlyDenyRules_ParticipantClean_Allows()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 110,
                deniedAddr: new List<string> { AlgoAddr2 },
                blockedJur: new List<string> { "KP" });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "US"
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Allow));
        }

        // ── Jurisdiction comparison is case-insensitive ───────────────────────────

        [Test]
        public async Task Evaluate_JurisdictionCaseInsensitive_Allows()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 111,
                allowedAddr: new List<string> { AlgoAddr1 },
                allowedJur: new List<string> { "US" });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "us"   // Lowercase
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Allow));
        }

        // ── Multiple required categories: participant matching any one is allowed ──

        [Test]
        public async Task Evaluate_MultipleRequiredCategories_MatchesOne_Allows()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 112,
                allowedAddr: new List<string> { AlgoAddr1 },
                requiredCats: new List<WhitelistPolicyInvestorCategory>
                {
                    WhitelistPolicyInvestorCategory.AccreditedInvestor,
                    WhitelistPolicyInvestorCategory.Institutional
                });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1,
                InvestorCategory = WhitelistPolicyInvestorCategory.Institutional
            });

            Assert.That(r.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Allow));
        }

        // ── Result contains audit ID and timestamp ────────────────────────────────

        [Test]
        public async Task Evaluate_ResultContainsAuditIdAndTimestamp()
        {
            var svc = CreateService();
            var id = await CreateActivePolicy(svc, 113,
                allowedAddr: new List<string> { AlgoAddr1 });

            var r = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = id,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(r.AuditId, Is.Not.Null.And.Not.Empty);
            Assert.That(r.EvaluatedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HTTP API CONTRACT TESTS
        // ═══════════════════════════════════════════════════════════════════════

        // ── Auth guard ────────────────────────────────────────────────────────────

        [Test]
        public async Task Http_GetPolicies_Unauthenticated_Returns401()
        {
            var r = await _unauthClient.GetAsync("/api/v1/whitelist/policies");
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Http_CreatePolicy_Unauthenticated_Returns401()
        {
            var r = await _unauthClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest { PolicyName = "P", AssetId = 1 });
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Http_EvaluateEligibility_Unauthenticated_Returns401()
        {
            var r = await _unauthClient.PostAsJsonAsync("/api/v1/whitelist/policies/someid/evaluate",
                new WhitelistPolicyEligibilityRequest { PolicyId = "someid", ParticipantAddress = AlgoAddr1 });
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── CRUD via HTTP ─────────────────────────────────────────────────────────

        [Test]
        public async Task Http_CreatePolicy_Returns200WithDraftPolicy()
        {
            if (_authClient.DefaultRequestHeaders.Authorization == null) { Assert.Ignore("No auth token"); return; }

            var r = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest { PolicyName = "HTTP Test Policy", AssetId = 50001 });

            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await r.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Policy!.Status, Is.EqualTo(WhitelistPolicyStatus.Draft));
        }

        [Test]
        public async Task Http_GetPolicies_ReturnsListWithSuccess()
        {
            if (_authClient.DefaultRequestHeaders.Authorization == null) { Assert.Ignore("No auth token"); return; }

            var r = await _authClient.GetAsync("/api/v1/whitelist/policies");
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await r.Content.ReadFromJsonAsync<WhitelistPolicyListResponse>();
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Policies, Is.Not.Null);
        }

        [Test]
        public async Task Http_GetPolicyById_NotFound_Returns404()
        {
            if (_authClient.DefaultRequestHeaders.Authorization == null) { Assert.Ignore("No auth token"); return; }

            var r = await _authClient.GetAsync("/api/v1/whitelist/policies/nonexistent-id");
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Http_FullCrudLifecycle_WorksEndToEnd()
        {
            if (_authClient.DefaultRequestHeaders.Authorization == null) { Assert.Ignore("No auth token"); return; }

            // CREATE
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest
                {
                    PolicyName = "E2E Lifecycle Policy",
                    AssetId = 60001,
                    AllowedAddresses = new List<string> { AlgoAddr1 },
                    AllowedJurisdictions = new List<string> { "DE" }
                });
            Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var created = await createResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            var pid = created!.Policy!.PolicyId;
            Assert.That(pid, Is.Not.Null.And.Not.Empty);

            // GET BY ID
            var getResp = await _authClient.GetAsync($"/api/v1/whitelist/policies/{pid}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var fetched = await getResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            Assert.That(fetched!.Policy!.PolicyName, Is.EqualTo("E2E Lifecycle Policy"));

            // UPDATE → Activate
            var updateResp = await _authClient.PutAsJsonAsync($"/api/v1/whitelist/policies/{pid}",
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active });
            Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var updated = await updateResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            Assert.That(updated!.Policy!.Status, Is.EqualTo(WhitelistPolicyStatus.Active));

            // VALIDATE
            var validateResp = await _authClient.PostAsync($"/api/v1/whitelist/policies/{pid}/validate", null);
            Assert.That(validateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var validated = await validateResp.Content.ReadFromJsonAsync<WhitelistPolicyValidationResult>();
            Assert.That(validated!.Success, Is.True);

            // EVALUATE — matching participant → Allow
            var evalResp = await _authClient.PostAsJsonAsync($"/api/v1/whitelist/policies/{pid}/evaluate",
                new WhitelistPolicyEligibilityRequest
                {
                    PolicyId = pid,
                    ParticipantAddress = AlgoAddr1,
                    JurisdictionCode = "DE"
                });
            Assert.That(evalResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var evalBody = await evalResp.Content.ReadFromJsonAsync<WhitelistPolicyEligibilityResult>();
            Assert.That(evalBody!.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Allow));

            // EVALUATE — non-matching participant → Deny
            var evalDenyResp = await _authClient.PostAsJsonAsync($"/api/v1/whitelist/policies/{pid}/evaluate",
                new WhitelistPolicyEligibilityRequest
                {
                    PolicyId = pid,
                    ParticipantAddress = AlgoAddr2,   // Not on allowlist
                    JurisdictionCode = "DE"
                });
            var evalDenyBody = await evalDenyResp.Content.ReadFromJsonAsync<WhitelistPolicyEligibilityResult>();
            Assert.That(evalDenyBody!.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));

            // ARCHIVE
            var archiveResp = await _authClient.DeleteAsync($"/api/v1/whitelist/policies/{pid}");
            Assert.That(archiveResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var archived = await archiveResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            Assert.That(archived!.Policy!.Status, Is.EqualTo(WhitelistPolicyStatus.Archived));
        }

        [Test]
        public async Task Http_EvaluateDraftPolicy_Returns200WithDeny()
        {
            if (_authClient.DefaultRequestHeaders.Authorization == null) { Assert.Ignore("No auth token"); return; }

            // Create but don't activate
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest
                {
                    PolicyName = "Draft Fail-Closed Test",
                    AssetId = 70001,
                    AllowedAddresses = new List<string> { AlgoAddr1 }
                });
            var created = await createResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            var pid = created!.Policy!.PolicyId;

            var evalResp = await _authClient.PostAsJsonAsync($"/api/v1/whitelist/policies/{pid}/evaluate",
                new WhitelistPolicyEligibilityRequest
                {
                    PolicyId = pid,
                    ParticipantAddress = AlgoAddr1
                });

            Assert.That(evalResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await evalResp.Content.ReadFromJsonAsync<WhitelistPolicyEligibilityResult>();
            Assert.That(body!.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(body.IsFailClosed, Is.True);
        }

        [Test]
        public async Task Http_GetPoliciesByAssetId_FiltersCorrectly()
        {
            if (_authClient.DefaultRequestHeaders.Authorization == null) { Assert.Ignore("No auth token"); return; }

            ulong uniqueAsset = 80001;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest { PolicyName = "Asset Filter Test", AssetId = uniqueAsset });

            var r = await _authClient.GetAsync($"/api/v1/whitelist/policies?assetId={uniqueAsset}");
            var body = await r.Content.ReadFromJsonAsync<WhitelistPolicyListResponse>();
            Assert.That(body!.Policies.All(p => p.AssetId == uniqueAsset), Is.True);
            Assert.That(body.Policies.Count, Is.GreaterThanOrEqualTo(1));
        }
    }
}

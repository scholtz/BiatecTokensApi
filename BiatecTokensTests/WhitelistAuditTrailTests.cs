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
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for the whitelist decision audit trail and compliance evidence APIs.
    /// Covers reason codes, version metadata, audit history, compliance reports, and HTTP contract.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WhitelistAuditTrailTests
    {
        // ── Algorand test addresses ───────────────────────────────────────────────
        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // ── Unit-level service factory ────────────────────────────────────────────

        private static IWhitelistPolicyService CreateService()
            => new WhitelistPolicyService(NullLogger<WhitelistPolicyService>.Instance);

        // ── HTTP test factory ─────────────────────────────────────────────────────

        private AuditTrailTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new AuditTrailTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var email = $"audit-{Guid.NewGuid():N}@biatec.io";
            var reg = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest { Email = email, Password = "AuditTest123!", ConfirmPassword = "AuditTest123!" });

            Assert.That(reg.IsSuccessStatusCode,
                $"Registration failed with status {reg.StatusCode}. Authenticated tests will not work.");

            var body = await reg.Content.ReadFromJsonAsync<RegisterResponse>();
            var token = body?.AccessToken;
            Assert.That(token, Is.Not.Null,
                "Access token was null after successful registration.");

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

        private sealed class AuditTrailTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "AuditTrailTestKey32MinCharsRequired!!!!!",
                        ["JwtConfig:SecretKey"] = "AuditTrailSecretKey32CharMinRequired!!!!!",
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
        // SECTION 1: Reason codes on evaluation results
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateEligibility_PolicyNotFound_ReturnsReasonCode_PolicyNotFound()
        {
            var svc = CreateService();
            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = "non-existent-policy-id",
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyNotFound));
        }

        [Test]
        public async Task EvaluateEligibility_DraftPolicy_ReturnsReasonCode_PolicyInDraftState()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Draft Policy",
                AssetId = 100001,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy!.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyInDraftState));
            Assert.That(result.IsFailClosed, Is.True);
        }

        [Test]
        public async Task EvaluateEligibility_ArchivedPolicy_ReturnsReasonCode_PolicyIsArchived()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "To Archive Policy",
                AssetId = 100002,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.ArchivePolicyAsync(created.Policy!.PolicyId, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyIsArchived));
            Assert.That(result.IsFailClosed, Is.True);
        }

        [Test]
        public async Task EvaluateEligibility_EmptyPolicy_ReturnsReasonCode_PolicyHasNoRules()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Empty Policy",
                AssetId = 100003
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyHasNoRules));
            Assert.That(result.IsFailClosed, Is.True);
        }

        [Test]
        public async Task EvaluateEligibility_AddressOnDenyList_ReturnsReasonCode_AddressOnDenyList()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Deny List Policy",
                AssetId = 100004,
                AllowedAddresses = new List<string> { AlgoAddr2 },
                DeniedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.AddressOnDenyList));
        }

        [Test]
        public async Task EvaluateEligibility_BlockedJurisdiction_ReturnsReasonCode_RestrictedJurisdiction()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Jurisdiction Block Policy",
                AssetId = 100005,
                AllowedAddresses = new List<string> { AlgoAddr1 },
                BlockedJurisdictions = new List<string> { "CN" }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "CN"
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.RestrictedJurisdiction));
        }

        [Test]
        public async Task EvaluateEligibility_JurisdictionNotProvided_ReturnsReasonCode_JurisdictionNotProvided()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "US Qualified Investors Policy",
                AssetId = 100006,
                AllowedAddresses = new List<string> { AlgoAddr1 },
                AllowedJurisdictions = new List<string> { "US" }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = null
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.JurisdictionNotProvided));
        }

        [Test]
        public async Task EvaluateEligibility_JurisdictionNotAllowed_ReturnsReasonCode_JurisdictionNotAllowed()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "European Retail Investors Policy",
                AssetId = 100007,
                AllowedAddresses = new List<string> { AlgoAddr1 },
                AllowedJurisdictions = new List<string> { "DE", "GB", "FR" }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1,
                JurisdictionCode = "US"
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.JurisdictionNotAllowed));
        }

        [Test]
        public async Task EvaluateEligibility_InvestorCategoryNotAllowed_ReturnsReasonCode_UnsupportedInvestorCategory()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Accredited Only Policy",
                AssetId = 100008,
                AllowedAddresses = new List<string> { AlgoAddr1 },
                RequiredInvestorCategories = new List<WhitelistPolicyInvestorCategory>
                    { WhitelistPolicyInvestorCategory.AccreditedInvestor }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1,
                InvestorCategory = WhitelistPolicyInvestorCategory.RetailInvestor
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.UnsupportedInvestorCategory));
        }

        [Test]
        public async Task EvaluateEligibility_AddressNotOnAllowList_ReturnsReasonCode_AddressNotOnAllowList()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Allowlist Policy",
                AssetId = 100009,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr2
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Deny));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.AddressNotOnAllowList));
        }

        [Test]
        public async Task EvaluateEligibility_AllCriteriaSatisfied_ReturnsReasonCode_AllPolicyCriteriaSatisfied()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "All Satisfied Policy",
                AssetId = 100010,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.Outcome, Is.EqualTo(WhitelistPolicyEligibilityOutcome.Allow));
            Assert.That(result.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.AllPolicyCriteriaSatisfied));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SECTION 2: Policy version metadata in evaluation results
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateEligibility_ActivePolicy_ContainsPolicyVersionMetadata()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Version Metadata Policy",
                AssetId = 200001,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.PolicyVersionMetadata, Is.Not.Null);
            Assert.That(result.PolicyVersionMetadata!.PolicyId, Is.EqualTo(created.Policy.PolicyId));
            Assert.That(result.PolicyVersionMetadata.PolicyName, Is.EqualTo("Version Metadata Policy"));
        }

        [Test]
        public async Task EvaluateEligibility_VersionMetadata_ReflectsCorrectVersion()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Version Check Policy",
                AssetId = 200002,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            // version 1 created + version 2 after update
            Assert.That(result.PolicyVersionMetadata!.Version, Is.EqualTo(2));
        }

        [Test]
        public async Task EvaluateEligibility_AfterPolicyUpdate_VersionMetadataReflectsNewVersion()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Version Evolution Policy",
                AssetId = 200003,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { PolicyName = "Version Evolution Policy v2" }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            Assert.That(result.PolicyVersionMetadata!.Version, Is.EqualTo(3));
            Assert.That(result.PolicyVersionMetadata.PolicyName, Is.EqualTo("Version Evolution Policy v2"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SECTION 3: Audit history retrieval
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetAuditHistory_AfterCreate_ContainsPolicyCreatedEvent()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Audit Create Policy",
                AssetId = 300001
            }, "creator@test");

            var history = await svc.GetAuditHistoryAsync(created.Policy!.PolicyId,
                new WhitelistAuditHistoryRequest { Page = 1, PageSize = 50 });

            Assert.That(history.Success, Is.True);
            Assert.That(history.Events.Any(e => e.EventType == WhitelistAuditEventType.PolicyCreated), Is.True);
        }

        [Test]
        public async Task GetAuditHistory_AfterEvaluation_ContainsEligibilityEvaluatedEvent()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Audit Eval Policy",
                AssetId = 300002,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var history = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest { Page = 1, PageSize = 50 });

            Assert.That(history.Events.Any(e => e.EventType == WhitelistAuditEventType.EligibilityEvaluated), Is.True);
        }

        [Test]
        public async Task GetAuditHistory_AfterArchive_ContainsPolicyArchivedEvent()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Audit Archive Policy",
                AssetId = 300003
            }, "creator@test");
            await svc.ArchivePolicyAsync(created.Policy!.PolicyId, "admin@test");

            var history = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest { Page = 1, PageSize = 50 });

            Assert.That(history.Events.Any(e => e.EventType == WhitelistAuditEventType.PolicyArchived), Is.True);
        }

        [Test]
        public async Task GetAuditHistory_Pagination_ReturnsCorrectPage()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Pagination Policy",
                AssetId = 300004,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            // Produce multiple evaluation events
            for (int i = 0; i < 5; i++)
            {
                await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                {
                    PolicyId = created.Policy.PolicyId,
                    ParticipantAddress = AlgoAddr1
                });
            }

            var page1 = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest { Page = 1, PageSize = 3 });
            var page2 = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest { Page = 2, PageSize = 3 });

            Assert.That(page1.Events.Count, Is.EqualTo(3));
            Assert.That(page1.Page, Is.EqualTo(1));
            Assert.That(page1.TotalCount, Is.GreaterThanOrEqualTo(7)); // create + update + 5 evals
            Assert.That(page2.Page, Is.EqualTo(2));
            Assert.That(page2.Events.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetAuditHistory_FilterByEventType_ReturnsOnlyMatchingEvents()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Filter Test Policy",
                AssetId = 300005,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var filtered = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest
                {
                    Page = 1,
                    PageSize = 50,
                    EventTypeFilter = WhitelistAuditEventType.EligibilityEvaluated
                });

            Assert.That(filtered.Events.All(e => e.EventType == WhitelistAuditEventType.EligibilityEvaluated), Is.True);
            Assert.That(filtered.Events, Is.Not.Empty);
        }

        [Test]
        public async Task GetAuditHistory_UnknownPolicy_ReturnsFailure()
        {
            var svc = CreateService();
            var history = await svc.GetAuditHistoryAsync("unknown-policy-id",
                new WhitelistAuditHistoryRequest { Page = 1, PageSize = 50 });

            Assert.That(history.Success, Is.False);
            Assert.That(history.ErrorCode, Is.EqualTo("POLICY_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SECTION 4: Compliance evidence report
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetComplianceEvidence_ActivePolicy_ReturnsReport()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Evidence Policy",
                AssetId = 400001,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            Assert.That(report.Success, Is.True);
            Assert.That(report.PolicyId, Is.EqualTo(created.Policy.PolicyId));
            Assert.That(report.GeneratedBy, Is.EqualTo("compliance@test"));
        }

        [Test]
        public async Task GetComplianceEvidence_EvaluationSummary_CountsCorrectly()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Summary Count Policy",
                AssetId = 400002,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            // 2 allows
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr1 });
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr1 });
            // 1 deny (not on allowlist)
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr2 });

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            Assert.That(report.EvaluationSummary.TotalEvaluations, Is.EqualTo(3));
            Assert.That(report.EvaluationSummary.AllowCount, Is.EqualTo(2));
            Assert.That(report.EvaluationSummary.DenyCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetComplianceEvidence_IncludeEvaluationHistory_True_IncludesAllEvents()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Include History Policy",
                AssetId = 400003,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr1 });

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest { IncludeEvaluationHistory = true }, "compliance@test");

            Assert.That(report.AuditEvents.Any(e => e.EventType == WhitelistAuditEventType.EligibilityEvaluated), Is.True);
        }

        [Test]
        public async Task GetComplianceEvidence_IncludeEvaluationHistory_False_ExcludesEvaluationEvents()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Exclude History Policy",
                AssetId = 400004,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr1 });

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest { IncludeEvaluationHistory = false }, "compliance@test");

            Assert.That(report.AuditEvents.All(e => e.EventType != WhitelistAuditEventType.EligibilityEvaluated), Is.True);
        }

        [Test]
        public async Task GetComplianceEvidence_UnknownPolicy_ReturnsFailure()
        {
            var svc = CreateService();
            var report = await svc.GetComplianceEvidenceAsync("unknown-policy-id",
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            Assert.That(report.Success, Is.False);
            Assert.That(report.ErrorCode, Is.EqualTo("POLICY_NOT_FOUND"));
        }

        [Test]
        public async Task GetComplianceEvidence_ContainsPolicyVersionMetadata()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Version Meta Evidence Policy",
                AssetId = 400005,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            Assert.That(report.PolicyVersionMetadata, Is.Not.Null);
            Assert.That(report.PolicyVersionMetadata!.PolicyId, Is.EqualTo(created.Policy.PolicyId));
            Assert.That(report.PolicyVersionMetadata.Version, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetComplianceEvidence_DateFilter_LimitsEvents()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Date Filter Policy",
                AssetId = 400006,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var future = DateTime.UtcNow.AddDays(1);

            // Filter to future date range — should return no events
            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest
                {
                    FromDate = future,
                    ToDate = future.AddDays(1)
                }, "compliance@test");

            Assert.That(report.AuditEvents, Is.Empty);
        }

        [Test]
        public async Task GetComplianceEvidence_PolicyChangeHistory_OnlyNonEvaluationEvents()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Change History Policy",
                AssetId = 400007,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");
            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr1 });

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            Assert.That(report.PolicyChangeHistory.All(e => e.EventType != WhitelistAuditEventType.EligibilityEvaluated), Is.True);
            Assert.That(report.PolicyChangeHistory, Is.Not.Empty);
        }

        [Test]
        public async Task GetComplianceEvidence_TopDenyReasonCodes_ReflectsMostFrequent()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Deny Frequency Policy",
                AssetId = 400008,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            // 3 denies for AddressNotOnAllowList
            for (int i = 0; i < 3; i++)
                await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
                    { PolicyId = created.Policy.PolicyId, ParticipantAddress = AlgoAddr2 });

            var report = await svc.GetComplianceEvidenceAsync(created.Policy.PolicyId,
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            Assert.That(report.EvaluationSummary.TopDenyReasonCodes,
                Contains.Item(WhitelistEligibilityReasonCode.AddressNotOnAllowList));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SECTION 5: Fail-closed behavior audit recording
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FailClosed_DraftPolicy_AuditEventRecorded()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Fail Closed Draft Policy",
                AssetId = 500001,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");

            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy!.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var history = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest
                {
                    EventTypeFilter = WhitelistAuditEventType.EligibilityEvaluated
                });

            var evalEvent = history.Events.FirstOrDefault();
            Assert.That(evalEvent, Is.Not.Null);
            Assert.That(evalEvent!.IsFailClosed, Is.True);
            Assert.That(evalEvent.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyInDraftState));
        }

        [Test]
        public async Task FailClosed_EmptyPolicy_AuditEventRecorded()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Fail Closed Empty Policy",
                AssetId = 500002
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var history = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest
                {
                    EventTypeFilter = WhitelistAuditEventType.EligibilityEvaluated
                });

            var evalEvent = history.Events.FirstOrDefault();
            Assert.That(evalEvent, Is.Not.Null);
            Assert.That(evalEvent!.IsFailClosed, Is.True);
            Assert.That(evalEvent.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyHasNoRules));
        }

        [Test]
        public async Task FailClosed_ArchivedPolicy_AuditEventRecorded()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Fail Closed Archived Policy",
                AssetId = 500003,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.ArchivePolicyAsync(created.Policy!.PolicyId, "admin@test");

            await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var history = await svc.GetAuditHistoryAsync(created.Policy.PolicyId,
                new WhitelistAuditHistoryRequest
                {
                    EventTypeFilter = WhitelistAuditEventType.EligibilityEvaluated
                });

            var evalEvent = history.Events.FirstOrDefault();
            Assert.That(evalEvent, Is.Not.Null);
            Assert.That(evalEvent!.IsFailClosed, Is.True);
            Assert.That(evalEvent.ReasonCodes, Contains.Item(WhitelistEligibilityReasonCode.PolicyIsArchived));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SECTION 6: Serialization shape tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluationResult_SerializationShape_ContainsReasonCodes()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Shape Test Policy",
                AssetId = 600001,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.That(json, Does.Contain("reasonCodes"));
        }

        [Test]
        public async Task EvaluationResult_SerializationShape_ContainsPolicyVersionMetadata()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Shape Meta Policy",
                AssetId = 600002,
                AllowedAddresses = new List<string> { AlgoAddr1 }
            }, "creator@test");
            await svc.UpdatePolicyAsync(created.Policy!.PolicyId,
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active }, "admin@test");

            var result = await svc.EvaluateEligibilityAsync(new WhitelistPolicyEligibilityRequest
            {
                PolicyId = created.Policy.PolicyId,
                ParticipantAddress = AlgoAddr1
            });

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.That(json, Does.Contain("policyVersionMetadata"));
        }

        [Test]
        public void AuditEvent_SerializationShape_ContainsExpectedFields()
        {
            var evt = new WhitelistAuditEvent
            {
                PolicyId = "test-policy-id",
                EventType = WhitelistAuditEventType.PolicyCreated,
                Actor = "creator@test",
                Description = "Policy created",
                PolicyVersion = 1,
                ReasonCodes = new List<WhitelistEligibilityReasonCode>(),
                OccurredAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            Assert.That(json, Does.Contain("eventId"));
            Assert.That(json, Does.Contain("policyId"));
            Assert.That(json, Does.Contain("eventType"));
            Assert.That(json, Does.Contain("actor"));
            Assert.That(json, Does.Contain("occurredAt"));
            Assert.That(json, Does.Contain("description"));
            Assert.That(json, Does.Contain("policyVersion"));
            Assert.That(json, Does.Contain("reasonCodes"));
        }

        [Test]
        public async Task ComplianceEvidenceReport_SerializationShape_ContainsExpectedFields()
        {
            var svc = CreateService();
            var created = await svc.CreatePolicyAsync(new CreateWhitelistPolicyRequest
            {
                PolicyName = "Report Shape Policy",
                AssetId = 600003
            }, "creator@test");

            var report = await svc.GetComplianceEvidenceAsync(created.Policy!.PolicyId,
                new WhitelistComplianceEvidenceRequest(), "compliance@test");

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            Assert.That(json, Does.Contain("policyId"));
            Assert.That(json, Does.Contain("generatedAt"));
            Assert.That(json, Does.Contain("generatedBy"));
            Assert.That(json, Does.Contain("evaluationSummary"));
            Assert.That(json, Does.Contain("auditEvents"));
            Assert.That(json, Does.Contain("policyChangeHistory"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SECTION 7: HTTP controller tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Http_GetAuditHistory_Unauthenticated_Returns401()
        {
            var response = await _unauthClient.GetAsync("/api/v1/whitelist/policies/some-policy-id/audit-history");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Http_GetAuditHistory_UnknownPolicy_Returns404()
        {
            var response = await _authClient.GetAsync("/api/v1/whitelist/policies/unknown-policy-xyz/audit-history");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Http_GetAuditHistory_AfterEvaluation_Returns200()
        {
            // Create policy via HTTP
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest
                {
                    PolicyName = "HTTP Audit History Policy",
                    AssetId = 700001,
                    AllowedAddresses = new List<string> { AlgoAddr1 }
                });
            Assert.That(createResp.IsSuccessStatusCode, Is.True, $"Create failed: {createResp.StatusCode}");
            var createBody = await createResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            var policyId = createBody!.Policy!.PolicyId;

            // Activate via HTTP
            await _authClient.PutAsJsonAsync($"/api/v1/whitelist/policies/{policyId}",
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active });

            // Evaluate via HTTP
            await _authClient.PostAsJsonAsync($"/api/v1/whitelist/policies/{policyId}/evaluate",
                new WhitelistPolicyEligibilityRequest { PolicyId = policyId, ParticipantAddress = AlgoAddr1 });

            // Get audit history
            var histResp = await _authClient.GetAsync($"/api/v1/whitelist/policies/{policyId}/audit-history");
            Assert.That(histResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var histBody = await histResp.Content.ReadFromJsonAsync<WhitelistAuditHistoryResponse>();
            Assert.That(histBody!.Success, Is.True);
            Assert.That(histBody.Events, Is.Not.Empty);
        }

        [Test]
        public async Task Http_GetComplianceEvidence_Unauthenticated_Returns401()
        {
            var response = await _unauthClient.PostAsJsonAsync(
                "/api/v1/whitelist/policies/some-policy-id/compliance-evidence",
                new WhitelistComplianceEvidenceRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Http_GetComplianceEvidence_UnknownPolicy_Returns404()
        {
            var response = await _authClient.PostAsJsonAsync(
                "/api/v1/whitelist/policies/unknown-policy-xyz/compliance-evidence",
                new WhitelistComplianceEvidenceRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Http_GetComplianceEvidence_ValidPolicy_Returns200()
        {
            // Create policy
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest
                {
                    PolicyName = "HTTP Evidence Policy",
                    AssetId = 700002,
                    AllowedAddresses = new List<string> { AlgoAddr1 }
                });
            Assert.That(createResp.IsSuccessStatusCode, Is.True);
            var createBody = await createResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            var policyId = createBody!.Policy!.PolicyId;

            var evidResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/whitelist/policies/{policyId}/compliance-evidence",
                new WhitelistComplianceEvidenceRequest());

            Assert.That(evidResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var evidBody = await evidResp.Content.ReadFromJsonAsync<WhitelistComplianceEvidenceReport>();
            Assert.That(evidBody!.Success, Is.True);
            Assert.That(evidBody.PolicyId, Is.EqualTo(policyId));
        }

        [Test]
        public async Task Http_Evaluate_ReasonCodesInResponse()
        {
            // Create and activate policy
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest
                {
                    PolicyName = "HTTP Reason Codes Policy",
                    AssetId = 700003,
                    AllowedAddresses = new List<string> { AlgoAddr1 }
                });
            Assert.That(createResp.IsSuccessStatusCode, Is.True);
            var createBody = await createResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            var policyId = createBody!.Policy!.PolicyId;
            await _authClient.PutAsJsonAsync($"/api/v1/whitelist/policies/{policyId}",
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active });

            // Evaluate
            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/whitelist/policies/{policyId}/evaluate",
                new WhitelistPolicyEligibilityRequest { PolicyId = policyId, ParticipantAddress = AlgoAddr1 });
            Assert.That(evalResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await evalResp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("reasonCodes"), "Response JSON must contain reasonCodes field");
        }

        [Test]
        public async Task Http_Evaluate_PolicyVersionMetadataInResponse()
        {
            // Create and activate policy
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/policies",
                new CreateWhitelistPolicyRequest
                {
                    PolicyName = "HTTP Version Meta Policy",
                    AssetId = 700004,
                    AllowedAddresses = new List<string> { AlgoAddr1 }
                });
            Assert.That(createResp.IsSuccessStatusCode, Is.True);
            var createBody = await createResp.Content.ReadFromJsonAsync<WhitelistPolicyResponse>();
            var policyId = createBody!.Policy!.PolicyId;
            await _authClient.PutAsJsonAsync($"/api/v1/whitelist/policies/{policyId}",
                new UpdateWhitelistPolicyRequest { Status = WhitelistPolicyStatus.Active });

            var evalResp = await _authClient.PostAsJsonAsync(
                $"/api/v1/whitelist/policies/{policyId}/evaluate",
                new WhitelistPolicyEligibilityRequest { PolicyId = policyId, ParticipantAddress = AlgoAddr1 });
            Assert.That(evalResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = await evalResp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("policyVersionMetadata"), "Response JSON must contain policyVersionMetadata field");
        }
    }
}

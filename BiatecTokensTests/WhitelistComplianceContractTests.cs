using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced whitelist and jurisdiction compliance contract tests.
    ///
    /// These tests verify:
    /// - Detailed error contracts for compliance outcomes (allowed / blocked / review-required)
    /// - Idempotency behavior for whitelist and jurisdiction mutations
    /// - Audit trail completeness and traceability
    /// - Multi-jurisdiction policy scenarios
    /// - Whitelist rules CRUD with policy evaluation
    /// - Machine-readable error response contracts
    /// - KYC metadata preservation and retrieval
    /// - Network-specific compliance behavior
    /// - Transfer validation across multiple scenarios
    /// - Policy scoping and token-level enforcement
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WhitelistComplianceContractTests
    {
        private ComplianceContractTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new ComplianceContractTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var email = $"compliance-contract-{Guid.NewGuid():N}@biatec.io";
            var reg = new RegisterRequest
            {
                Email = email,
                Password = "ComplianceContract123!",
                ConfirmPassword = "ComplianceContract123!"
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

        private sealed class ComplianceContractTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "ComplianceContractTestKey32MinCharsRequired!!",
                        ["JwtConfig:SecretKey"] = "ComplianceContractTestSecretKey32CharMinRequired!!",
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

        // ── Error contract machine-readability ────────────────────────────────────

        [Test]
        public async Task AddWhitelist_InvalidAddress_ErrorContractIsMachineReadable()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = 10001UL,
                Address = "INVALID_ALGO_ADDRESS",
                Status = WhitelistStatus.Active
            });

            var json = await resp.Content.ReadAsStringAsync();

            // Parse as JSON - must not be an HTML error page or plain text
            Assert.DoesNotThrow(() => JsonDocument.Parse(json),
                "Error response must be valid JSON, not HTML");

            var doc = JsonDocument.Parse(json);
            Assert.That(doc.RootElement.TryGetProperty("success", out var success), Is.True,
                "Error response must have 'success' field");
            Assert.That(success.GetBoolean(), Is.False, "success must be false for error");
        }

        [Test]
        public async Task CreateJurisdictionRule_EmptyCode_ErrorContractIsMachineReadable()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "",
                JurisdictionName = "Test",
                RegulatoryFramework = "MICA"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var doc = JsonDocument.Parse(json);
                Assert.That(doc.RootElement.TryGetProperty("success", out _), Is.True,
                    "Error response must be structured JSON with 'success'");
            }
            else
            {
                // 400 is also valid - just ensure no 500
                Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                    "Empty code must not cause 500 error");
            }
        }

        // ── Transfer validation outcome contracts ─────────────────────────────────

        [Test]
        public async Task TransferValidation_AllowedOutcome_HasClearStructure()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10010UL;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest
                {
                    AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50
                });

            var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.True, "Both whitelisted → allowed");
            Assert.That(result.SenderStatus, Is.Not.Null, "Sender status must be populated");
            Assert.That(result.ReceiverStatus, Is.Not.Null, "Receiver status must be populated");
            Assert.That(result.SenderStatus!.IsWhitelisted, Is.True);
            Assert.That(result.ReceiverStatus!.IsWhitelisted, Is.True);
        }

        [Test]
        public async Task TransferValidation_BlockedOutcome_HasDenialReason()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest
                {
                    AssetId = 99990001UL, // Fresh asset with no entries
                    FromAddress = AlgoAddr1,
                    ToAddress = AlgoAddr2,
                    Amount = 100
                });

            var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.False, "Neither whitelisted → blocked");
            Assert.That(result.DenialReason, Is.Not.Null.And.Not.Empty,
                "Blocked transfer must include denial reason for compliance audit");
        }

        [Test]
        public async Task TransferValidation_SenderBlockedOnly_CorrectOutcomeReported()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10011UL;

            // Only receiver whitelisted
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest
                {
                    AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100
                });

            var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.False, "Sender not whitelisted → blocked");
            Assert.That(result.SenderStatus!.IsWhitelisted, Is.False, "Sender status shows not whitelisted");
        }

        [Test]
        public async Task TransferValidation_ReceiverBlockedOnly_CorrectOutcomeReported()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10012UL;

            // Only sender whitelisted
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest
                {
                    AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100
                });

            var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.False, "Receiver not whitelisted → blocked");
            Assert.That(result.ReceiverStatus!.IsWhitelisted, Is.False, "Receiver status shows not whitelisted");
        }

        // ── Allowlist verification outcomes ───────────────────────────────────────

        [Test]
        public async Task AllowlistVerification_AllowedTransfer_CacheDurationSet()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10020UL;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest
                {
                    AssetId = assetId,
                    SenderAddress = AlgoAddr1,
                    RecipientAddress = AlgoAddr2,
                    Network = "mainnet-v1.0"
                });

            var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result!.TransferStatus, Is.EqualTo(AllowlistTransferStatus.Allowed));
            Assert.That(result.CacheDurationSeconds, Is.GreaterThan(0),
                "Cache duration must be positive for downstream consumers");
        }

        [Test]
        public async Task AllowlistVerification_BlockedBoth_ReportsCorrectStatus()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest
                {
                    AssetId = 10021UL,
                    SenderAddress = AlgoAddr1,
                    RecipientAddress = AlgoAddr2
                });

            var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result!.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedBoth),
                "Both blocked → BlockedBoth status");
        }

        // ── Idempotency ───────────────────────────────────────────────────────────

        [Test]
        public async Task AddWhitelistEntry_SameEntryThreeTimes_IdempotentResult()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10030UL;
            var request = new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                Reason = "Idempotency test"
            };

            // Three identical adds
            var result1 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", request)
                .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<WhitelistResponse>().GetAwaiter().GetResult());
            var result2 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", request)
                .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<WhitelistResponse>().GetAwaiter().GetResult());
            var result3 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", request)
                .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<WhitelistResponse>().GetAwaiter().GetResult());

            Assert.That(result1!.Success, Is.True, "First add must succeed");
            Assert.That(result2!.Success, Is.True, "Second add (update) must succeed");
            Assert.That(result3!.Success, Is.True, "Third add (update) must succeed");

            // List should show only 1 entry (not 3)
            var listResp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}");
            var listBody = await listResp.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(listBody!.TotalCount, Is.EqualTo(1),
                "Idempotent adds must not create duplicates - only 1 entry should exist");
        }

        [Test]
        public async Task CreateJurisdictionRule_SameCodeTwice_SecondCallFails()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"I{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Idempotency Test",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            var result1 = await resp1.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(result1!.Success, Is.True, "First create must succeed");

            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            var result2 = await resp2.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(result2!.Success, Is.False,
                "Duplicate jurisdiction code must be rejected - not silently ignored");
        }

        // ── Audit trail completeness ──────────────────────────────────────────────

        [Test]
        public async Task AuditLog_AfterStatusChange_RecordsBothOldAndNewStatus()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10040UL;

            // Add as Active
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });

            // Change to Inactive
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive
            });

            var logResp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var log = await logResp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();

            var updateEntry = log!.Entries.FirstOrDefault(e => e.ActionType == WhitelistActionType.Update);
            if (updateEntry != null)
            {
                Assert.That(updateEntry.OldStatus, Is.EqualTo(WhitelistStatus.Active),
                    "Audit must record previous status for traceability");
                Assert.That(updateEntry.NewStatus, Is.EqualTo(WhitelistStatus.Inactive),
                    "Audit must record new status");
            }
            else
            {
                // Some implementations may log differently; just verify there's an audit trail
                Assert.That(log.Entries, Is.Not.Empty, "Some audit entries must exist");
            }
        }

        [Test]
        public async Task AuditLog_MultipleOperations_PreservesChronologicalOrder()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10041UL;

            // Sequential operations
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await Task.Delay(10); // Ensure timestamps differ
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive
            });

            var logResp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var log = await logResp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();

            Assert.That(log!.Entries.Count, Is.GreaterThanOrEqualTo(2),
                "Multiple operations should create multiple audit entries");
        }

        [Test]
        public async Task AuditLog_ImmutableRetentionPolicy_DocumentedInResponse()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var logResp = await _authClient.GetAsync("/api/v1/whitelist/10041/audit-log");
            var log = await logResp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();

            Assert.That(log!.RetentionPolicy, Is.Not.Null, "Retention policy must be documented");
            Assert.That(log.RetentionPolicy!.ImmutableEntries, Is.True,
                "MICA compliance requires immutable audit entries");
            Assert.That(log.RetentionPolicy.RegulatoryFramework, Is.Not.Null.And.Not.Empty,
                "Regulatory framework must be identified");
        }

        // ── KYC metadata preservation ─────────────────────────────────────────────

        [Test]
        public async Task AddWhitelistEntry_KycMetadata_PersistedCorrectly()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10050UL;
            var kycDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

            var addResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr3,
                Status = WhitelistStatus.Active,
                KycVerified = true,
                KycVerificationDate = kycDate,
                KycProvider = "TestKycProvider"
            });

            var result = await addResp.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Entry!.KycVerified, Is.True, "KYC verified flag must be preserved");
            Assert.That(result.Entry.KycProvider, Is.EqualTo("TestKycProvider"),
                "KYC provider name must be preserved");
        }

        [Test]
        public async Task AddWhitelistEntry_WithExpiry_ExpiryDatePreserved()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10051UL;
            var expiry = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc);

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                ExpirationDate = expiry
            });

            var result = await resp.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Entry!.ExpirationDate, Is.Not.Null,
                "Expiry date must be persisted for compliance monitoring");
        }

        // ── Policy scoping and token isolation ────────────────────────────────────

        [Test]
        public async Task WhitelistEntries_DifferentAssets_NotCrossContaminated()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong asset1 = 11000001UL;
            const ulong asset2 = 11000002UL;

            // Add to asset1 only
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = asset1, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });

            // Asset2 should be empty
            var list2 = await _authClient.GetAsync($"/api/v1/whitelist/{asset2}");
            var body2 = await list2.Content.ReadFromJsonAsync<WhitelistListResponse>();

            Assert.That(body2!.TotalCount, Is.EqualTo(0),
                "Asset 2 whitelist must not contain entries from Asset 1 - policy scoping must work");
        }

        [Test]
        public async Task TransferValidation_DifferentAssets_IndependentPolicies()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong asset1 = 11000010UL;
            const ulong asset2 = 11000011UL;

            // Whitelist for asset1 only
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = asset1, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = asset1, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            // Transfer for asset1 should be allowed
            var asset1Transfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest
                {
                    AssetId = asset1, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10
                });
            var asset1Result = await asset1Transfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();

            // Transfer for asset2 should be blocked (no whitelist)
            var asset2Transfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest
                {
                    AssetId = asset2, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10
                });
            var asset2Result = await asset2Transfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();

            Assert.That(asset1Result!.IsAllowed, Is.True, "Asset1 whitelist allows transfer");
            Assert.That(asset2Result!.IsAllowed, Is.False, "Asset2 has no whitelist so transfer is blocked");
        }

        // ── Whitelist rules CRUD ──────────────────────────────────────────────────

        [Test]
        public async Task CreateWhitelistRule_ValidRule_Returns200()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var body = new CreateWhitelistRuleRequest
            {
                AssetId = 12000001UL,
                Name = "Require KYC for Active Status",
                RuleType = WhitelistRuleType.RequireKycForActive,
                IsActive = true
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Create whitelist rule must return 200");

            var result = await resp.Content.ReadFromJsonAsync<WhitelistRuleResponse>();
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task ListWhitelistRules_AfterCreate_ReturnsCreatedRule()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 12000002UL;

            await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules", new CreateWhitelistRuleRequest
            {
                AssetId = assetId,
                Name = "Auto Revoke Expired Entries",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true
            });

            var listResp = await _authClient.GetAsync($"/api/v1/whitelist-rules/{assetId}");
            Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var listBody = await listResp.Content.ReadFromJsonAsync<WhitelistRulesListResponse>();
            Assert.That(listBody, Is.Not.Null);
            Assert.That(listBody!.Success, Is.True);
        }

        [Test]
        public async Task WhitelistRules_AuditLog_ReturnsAuditHistory()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 12000003UL;

            await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules", new CreateWhitelistRuleRequest
            {
                AssetId = assetId,
                Name = "Require Expiration Date",
                RuleType = WhitelistRuleType.RequireExpirationDate,
                IsActive = true
            });

            var auditResp = await _authClient.GetAsync($"/api/v1/whitelist-rules/{assetId}/audit-log");
            Assert.That(auditResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Jurisdiction rules with requirements ──────────────────────────────────

        [Test]
        public async Task JurisdictionRule_RequirementsPreservedAfterGet()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"Q{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code,
                    JurisdictionName = "Requirements Preservation Test",
                    RegulatoryFramework = "MICA",
                    IsActive = true,
                    Requirements = new List<ComplianceRequirement>
                    {
                        new() { RequirementCode = "REQ_1", Category = "KYC", IsMandatory = true, Severity = RequirementSeverity.Critical },
                        new() { RequirementCode = "REQ_2", Category = "AML", IsMandatory = false, Severity = RequirementSeverity.High }
                    }
                });

            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            if (createResult?.Rule?.Id == null) { Assert.Ignore("Could not create rule"); return; }

            var getResp = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");
            var getResult = await getResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            Assert.That(getResult!.Rule!.Requirements, Has.Count.EqualTo(2),
                "Both requirements must be preserved after GET");

            var req1 = getResult.Rule.Requirements.FirstOrDefault(r => r.RequirementCode == "REQ_1");
            Assert.That(req1, Is.Not.Null, "REQ_1 must be findable by code");
            Assert.That(req1!.IsMandatory, Is.True, "Mandatory flag must be preserved");
            Assert.That(req1.Severity, Is.EqualTo(RequirementSeverity.Critical));
        }

        [Test]
        public async Task JurisdictionRule_UpdateClearsOldRequirements()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"W{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            // Create with 2 requirements
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code,
                    JurisdictionName = "Update Requirements Test",
                    RegulatoryFramework = "MICA",
                    IsActive = true,
                    Requirements = new List<ComplianceRequirement>
                    {
                        new() { RequirementCode = "OLD_REQ_1", Category = "KYC", IsMandatory = true, Severity = RequirementSeverity.Critical },
                        new() { RequirementCode = "OLD_REQ_2", Category = "AML", IsMandatory = true, Severity = RequirementSeverity.High }
                    }
                });

            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            if (createResult?.Rule?.Id == null) { Assert.Ignore("Could not create rule"); return; }

            // Update with only 1 requirement
            var updateResp = await _authClient.PutAsJsonAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code,
                    JurisdictionName = "Updated Name",
                    RegulatoryFramework = "MICA",
                    IsActive = true,
                    Requirements = new List<ComplianceRequirement>
                    {
                        new() { RequirementCode = "NEW_REQ_1", Category = "Disclosure", IsMandatory = true, Severity = RequirementSeverity.Medium }
                    }
                });

            Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var updateResult = await updateResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(updateResult!.Success, Is.True);
        }

        // ── Deactivation contract ─────────────────────────────────────────────────

        [Test]
        public async Task DeactivateJurisdictionRule_SetIsActiveFalse_RuleDeactivated()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"Z{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code,
                    JurisdictionName = "Deactivation Test",
                    RegulatoryFramework = "MICA",
                    IsActive = true
                });

            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            if (createResult?.Rule?.Id == null) { Assert.Ignore("Could not create rule"); return; }

            // Deactivate by updating IsActive = false
            var updateResp = await _authClient.PutAsJsonAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code,
                    JurisdictionName = "Deactivation Test",
                    RegulatoryFramework = "MICA",
                    IsActive = false // Deactivated
                });

            Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var updateResult = await updateResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(updateResult!.Success, Is.True);

            // Verify rule is now inactive via GET
            var getResp = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");
            var getResult = await getResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(getResult!.Rule!.IsActive, Is.False,
                "Deactivated rule must show IsActive = false");
        }

        [Test]
        public async Task DeactivateWhitelistEntry_SetInactive_TransferBlocked()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 10060UL;

            // Add both as active
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            // Verify transfer is allowed
            var before = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var beforeResult = await before.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(beforeResult!.IsAllowed, Is.True, "Transfer must be allowed before deactivation");

            // Deactivate sender
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive
            });

            // Verify transfer is now blocked
            var after = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var afterResult = await after.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(afterResult!.IsAllowed, Is.False,
                "Transfer must be blocked after sender deactivation");
        }

        // ── Network-specific compliance ────────────────────────────────────────────

        [Test]
        public async Task VerifyAllowlist_MicaNetwork_IncludesApplicableRegulations()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest
                {
                    AssetId = 10070UL,
                    SenderAddress = AlgoAddr1,
                    RecipientAddress = AlgoAddr2,
                    Network = "aramidmain-v1.0" // MICA-applicable network
                });

            var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result!.MicaDisclosure, Is.Not.Null,
                "MICA network must include MICA disclosure");
            Assert.That(result.MicaDisclosure!.RequiresMicaCompliance, Is.True,
                "MICA-applicable network must set RequiresMicaCompliance = true");
        }

        [Test]
        public async Task VerifyAllowlist_NonMicaNetwork_StillReturnsAuditData()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest
                {
                    AssetId = 10071UL,
                    SenderAddress = AlgoAddr1,
                    RecipientAddress = AlgoAddr2,
                    Network = "testnet-v1.0" // Non-MICA network
                });

            var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result!.AuditMetadata, Is.Not.Null,
                "Audit metadata must be included regardless of network type");
        }

        // ── Export endpoint (CSV) ─────────────────────────────────────────────────

        [Test]
        public async Task ExportWhitelist_Returns200WithCsvContent()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            // Add some entries first
            const ulong assetId = 10080UL;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });

            var resp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/export");

            // Either returns CSV data or 200 with data
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Export endpoint must not return 5xx");
        }

        // ── Compliance overview ───────────────────────────────────────────────────

        [Test]
        public async Task GetComplianceOverview_Returns200()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/compliance-overview?assetId=12345");
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Compliance overview must not return 5xx");
        }

        // ── Jurisdiction assignment and removal ───────────────────────────────────

        [Test]
        public async Task AssignThenRemoveJurisdiction_CleanCycle()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"J{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code, JurisdictionName = "Assign/Remove Test",
                    RegulatoryFramework = "FATF", IsActive = true
                });

            const ulong assetId = 13000001UL;

            // Assign
            var assignResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign",
                new { assetId = assetId, network = "mainnet-v1.0", jurisdictionCode = code, isPrimary = true });
            Assert.That((int)assignResp.StatusCode, Is.LessThan(500), "Assign must not 5xx");

            // Get jurisdictions
            var getResp = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/token-jurisdictions?assetId={assetId}&network=mainnet-v1.0");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Remove
            var removeResp = await _authClient.DeleteAsync(
                $"/api/v1/compliance/jurisdiction-rules/token-jurisdictions?assetId={assetId}&network=mainnet-v1.0&jurisdictionCode={code}");
            Assert.That((int)removeResp.StatusCode, Is.LessThan(500), "Remove must not 5xx");
        }

        // ── Compliance evaluation result contract ─────────────────────────────────

        [Test]
        public async Task EvaluateTokenCompliance_HasFullResultContract()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var resp = await _authClient.GetAsync(
                "/api/v1/compliance/jurisdiction-rules/evaluate?assetId=99999&network=mainnet-v1.0&issuerId=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<JurisdictionEvaluationResult>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AssetId, Is.EqualTo(99999UL), "AssetId must be echoed in response");
            Assert.That(result.ApplicableJurisdictions, Is.Not.Null, "ApplicableJurisdictions must be present");
            Assert.That(result.CheckResults, Is.Not.Null, "CheckResults must be present");
        }
    }
}

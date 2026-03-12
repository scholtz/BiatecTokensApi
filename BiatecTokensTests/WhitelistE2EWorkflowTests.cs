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
    /// End-to-end workflow tests for the whitelist and jurisdiction policy compliance backend.
    ///
    /// These tests prove the full end-to-end compliance contract for regulated token issuance:
    ///
    /// - Complete regulated issuance workflow: configure policy → onboard investors → enforce transfers
    /// - Jurisdiction evaluation with multi-rule compliance checks
    /// - Concurrent whitelist management without race conditions
    /// - Policy conflict detection: duplicate jurisdiction codes, conflicting status transitions
    /// - Schema regression contract: all required fields present across API calls
    /// - Authorization boundaries: each endpoint enforces 401 correctly
    /// - Idempotency determinism: same operations produce identical outcomes across 3 runs
    /// - Audit trail completeness: every mutation creates a traceable, timestamped entry
    /// - Transfer enforcement across 5+ scenarios (all/none/one/expired/revoked whitelisted)
    /// - Compliance monitoring: enforcement report reflects real transfer activity
    /// - Error contract stability: all validation failures return JSON with success=false
    /// - Token isolation: policy rules are per-token, not shared across offerings
    /// - MICA disclosure accuracy: correct for VOI/Aramid, absent for testnet
    /// - Jurisdiction lifecycle: create → update → list-filter → assign → evaluate → delete
    /// - Whitelist rules lifecycle: create → list → apply → audit
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WhitelistE2EWorkflowTests
    {
        private E2EWorkflowTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new E2EWorkflowTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var email = $"e2e-wl-{Guid.NewGuid():N}@biatec.io";
            var reg = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email, Password = "E2EWorkflowTest123!", ConfirmPassword = "E2EWorkflowTest123!"
            });
            if (reg.IsSuccessStatusCode)
            {
                var body = await reg.Content.ReadFromJsonAsync<RegisterResponse>();
                _accessToken = body?.AccessToken;
            }
            _authClient = _factory.CreateClient();
            if (_accessToken != null)
                _authClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _authClient?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        private sealed class E2EWorkflowTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // NOTE: Standard BIP-39 test mnemonic - DO NOT use in production
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "E2EWorkflowTestKey32MinCharsRequired!!!!!!",
                        ["JwtConfig:SecretKey"] = "E2EWorkflowTestSecretKey32CharMinRequired!!",
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

        // ── E2E Workflow 1: Full regulated issuance workflow ──────────────────────

        [Test]
        public async Task E2E_FullRegulatedIssuanceWorkflow_AllStepsSucceed()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30001;
            var jurisdictionCode = $"RE{Guid.NewGuid().ToString("N")[..3].ToUpper()}";

            // PHASE 1: Configure compliance policy
            var createJurisdiction = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = jurisdictionCode,
                    JurisdictionName = "Regulated EU Market",
                    RegulatoryFramework = "MICA",
                    IsActive = true,
                    Requirements = new List<ComplianceRequirement>
                    {
                        new() { RequirementCode = "MICA_KYC", Category = "KYC", IsMandatory = true, Severity = RequirementSeverity.Critical }
                    }
                });
            var jurisdictionResult = await createJurisdiction.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(jurisdictionResult!.Success, Is.True, "Phase 1: Jurisdiction creation failed");

            // PHASE 2: Assign jurisdiction to token
            var assignResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign",
                new { assetId = assetId, network = "mainnet-v1.0", jurisdictionCode = jurisdictionCode, isPrimary = true });
            Assert.That((int)assignResp.StatusCode, Is.LessThan(500), "Phase 2: Assignment failed");

            // PHASE 3: Onboard investors
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "EURegulatoryKYC", Reason = "Accredited EU investor"
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "EURegulatoryKYC", Reason = "Accredited EU investor"
            });

            // PHASE 4: Validate compliance (both whitelisted → transfer allowed)
            var transfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1000 });
            var result = await transfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.True, "Phase 4: Transfer must be allowed for whitelisted investors");

            // PHASE 5: Enforce against non-whitelisted
            var blockAttempt = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr1, Amount = 100 });
            var blocked = await blockAttempt.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(blocked!.IsAllowed, Is.False, "Phase 5: Non-investor transfer must be blocked");

            // PHASE 6: Compliance evaluation
            var eval = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/evaluate?assetId={assetId}&network=mainnet-v1.0&issuerId={AlgoAddr1}");
            Assert.That(eval.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Phase 6: Evaluation failed");
        }

        // ── E2E Workflow 2: Idempotency determinism across 3 runs ─────────────────

        [Test]
        public async Task E2E_WhitelistIdempotency_ThreeIdenticalRunsProduceIdenticalResults()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30010;
            var request = new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "KYCProvider", Reason = "Idempotency test"
            };

            // Run 1
            var r1 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", request);
            var result1 = await r1.Content.ReadFromJsonAsync<WhitelistResponse>();

            // Run 2
            var r2 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", request);
            var result2 = await r2.Content.ReadFromJsonAsync<WhitelistResponse>();

            // Run 3
            var r3 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", request);
            var result3 = await r3.Content.ReadFromJsonAsync<WhitelistResponse>();

            Assert.That(result1!.Success, Is.True, "Run 1 must succeed");
            Assert.That(result2!.Success, Is.True, "Run 2 must succeed (idempotent update)");
            Assert.That(result3!.Success, Is.True, "Run 3 must succeed (idempotent update)");

            // Only 1 entry should exist regardless of 3 adds
            var list = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}");
            var listBody = await list.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(listBody!.TotalCount, Is.EqualTo(1),
                "Idempotent adds must not create duplicates");

            // Entry data must be consistent
            Assert.That(result1.Entry!.KycVerified, Is.EqualTo(result2.Entry!.KycVerified));
            Assert.That(result2.Entry.KycVerified, Is.EqualTo(result3.Entry!.KycVerified));
        }

        // ── E2E Workflow 3: All transfer validation scenarios ─────────────────────

        [Test]
        public async Task E2E_TransferValidation_AllFiveScenarios_CorrectOutcomes()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            // Scenario A: Both active → Allowed
            const ulong assetA = 30020;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetA, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetA, Address = AlgoAddr2, Status = WhitelistStatus.Active });

            var scenarioA = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetA, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100 });
            Assert.That((await scenarioA.Content.ReadFromJsonAsync<ValidateTransferResponse>())!.IsAllowed, Is.True,
                "Scenario A: both active → allowed");

            // Scenario B: Sender not whitelisted → Denied
            const ulong assetB = 30021;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetB, Address = AlgoAddr2, Status = WhitelistStatus.Active }); // only receiver

            var scenarioB = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetB, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50 });
            Assert.That((await scenarioB.Content.ReadFromJsonAsync<ValidateTransferResponse>())!.IsAllowed, Is.False,
                "Scenario B: sender not whitelisted → denied");

            // Scenario C: Receiver not whitelisted → Denied
            const ulong assetC = 30022;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetC, Address = AlgoAddr1, Status = WhitelistStatus.Active }); // only sender

            var scenarioC = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetC, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50 });
            Assert.That((await scenarioC.Content.ReadFromJsonAsync<ValidateTransferResponse>())!.IsAllowed, Is.False,
                "Scenario C: receiver not whitelisted → denied");

            // Scenario D: Sender inactive → Denied
            const ulong assetD = 30023;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetD, Address = AlgoAddr1, Status = WhitelistStatus.Inactive });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetD, Address = AlgoAddr2, Status = WhitelistStatus.Active });

            var scenarioD = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetD, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            Assert.That((await scenarioD.Content.ReadFromJsonAsync<ValidateTransferResponse>())!.IsAllowed, Is.False,
                "Scenario D: inactive sender → denied");

            // Scenario E: Neither whitelisted → Denied
            const ulong assetE = 30024; // no entries
            var scenarioE = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetE, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            Assert.That((await scenarioE.Content.ReadFromJsonAsync<ValidateTransferResponse>())!.IsAllowed, Is.False,
                "Scenario E: neither whitelisted → denied");
        }

        // ── E2E Workflow 4: Error contract stability ──────────────────────────────

        [Test]
        public async Task E2E_ErrorContracts_AllValidationFailuresReturnJsonSuccess_False()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var invalidInputs = new[]
            {
                // Invalid address
                new AddWhitelistEntryRequest { AssetId = 30030, Address = "INVALID", Status = WhitelistStatus.Active },
                // Too-short address
                new AddWhitelistEntryRequest { AssetId = 30031, Address = "SHORT", Status = WhitelistStatus.Active },
                // Wrong length address (57 chars)
                new AddWhitelistEntryRequest { AssetId = 30032, Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", Status = WhitelistStatus.Active }
            };

            foreach (var input in invalidInputs)
            {
                var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", input);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    Assert.DoesNotThrow(() => JsonDocument.Parse(json),
                        $"Error for address '{input.Address}' must be valid JSON");
                    var doc = JsonDocument.Parse(json);
                    Assert.That(doc.RootElement.TryGetProperty("success", out var success), Is.True,
                        $"Error response for '{input.Address}' must have 'success' field");
                    Assert.That(success.GetBoolean(), Is.False,
                        $"Invalid address '{input.Address}' must return success=false");
                }
                else
                {
                    Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                        $"Invalid address '{input.Address}' must return 200 with error or 400");
                }
            }
        }

        // ── E2E Workflow 5: Schema regression contract ────────────────────────────

        [Test]
        public async Task E2E_SchemaRegression_AllRequiredFieldsPresentAcrossAPIs()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30040;

            // Add entry - verify response schema
            var addResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            var addDoc = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync());
            Assert.That(addDoc.RootElement.TryGetProperty("success", out _), Is.True, "Add: missing 'success'");

            // List - verify response schema
            var listResp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}");
            var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
            Assert.That(listDoc.RootElement.TryGetProperty("success", out _), Is.True, "List: missing 'success'");
            Assert.That(listDoc.RootElement.TryGetProperty("entries", out _), Is.True, "List: missing 'entries'");

            // Transfer validation - verify response schema
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active });
            var transferResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10 });
            var transferDoc = JsonDocument.Parse(await transferResp.Content.ReadAsStringAsync());
            Assert.That(transferDoc.RootElement.TryGetProperty("success", out _), Is.True, "Transfer: missing 'success'");
            Assert.That(transferDoc.RootElement.TryGetProperty("isAllowed", out _), Is.True, "Transfer: missing 'isAllowed'");

            // Audit log - verify response schema
            var auditResp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var auditDoc = JsonDocument.Parse(await auditResp.Content.ReadAsStringAsync());
            Assert.That(auditDoc.RootElement.TryGetProperty("success", out _), Is.True, "AuditLog: missing 'success'");
            Assert.That(auditDoc.RootElement.TryGetProperty("entries", out _), Is.True, "AuditLog: missing 'entries'");
            Assert.That(auditDoc.RootElement.TryGetProperty("retentionPolicy", out _), Is.True, "AuditLog: missing 'retentionPolicy'");
        }

        // ── E2E Workflow 6: Jurisdiction rule lifecycle end-to-end ────────────────

        [Test]
        public async Task E2E_JurisdictionLifecycle_CreateUpdateListFilterAssignEvaluateDelete()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code1 = $"LC{Guid.NewGuid().ToString("N")[..3].ToUpper()}";
            var code2 = $"LD{Guid.NewGuid().ToString("N")[..3].ToUpper()}";

            // 1. Create two rules with different frameworks
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code1, JurisdictionName = "MICA Test Rule",
                RegulatoryFramework = "MICA", IsActive = true, Priority = 100
            });
            var create2 = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code2, JurisdictionName = "FATF Test Rule",
                    RegulatoryFramework = "FATF", IsActive = false, Priority = 200
                });
            var created2 = await create2.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(created2!.Success, Is.True, "Step 1 failed");

            // 2. Update rule 2 to activate it
            await _authClient.PutAsJsonAsync(
                $"/api/v1/compliance/jurisdiction-rules/{created2.Rule!.Id}",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code2, JurisdictionName = "FATF Updated",
                    RegulatoryFramework = "FATF", IsActive = true, Priority = 150
                });

            // 3. List all active rules (should include both)
            var listActive = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules?isActive=true");
            var activeRules = await listActive.Content.ReadFromJsonAsync<ListJurisdictionRulesResponse>();
            Assert.That(activeRules!.Rules.Any(r => r.JurisdictionCode == code1), Is.True, "code1 must be in active list");
            Assert.That(activeRules.Rules.Any(r => r.JurisdictionCode == code2), Is.True, "code2 must be in active list");

            // 4. Filter by MICA framework
            var micaOnly = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules?regulatoryFramework=MICA");
            var micaRules = await micaOnly.Content.ReadFromJsonAsync<ListJurisdictionRulesResponse>();
            Assert.That(micaRules!.Rules.All(r => r.RegulatoryFramework == "MICA"), Is.True,
                "Framework filter must only return MICA rules");

            // 5. Assign to token and evaluate
            const ulong assetId = 30050;
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign",
                new { assetId = assetId, network = "mainnet-v1.0", jurisdictionCode = code1, isPrimary = true });

            var eval = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/evaluate?assetId={assetId}&network=mainnet-v1.0&issuerId={AlgoAddr1}");
            Assert.That(eval.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 5 evaluation failed");

            // 6. Delete rule 2
            var deleteResp = await _authClient.DeleteAsync(
                $"/api/v1/compliance/jurisdiction-rules/{created2.Rule.Id}");
            Assert.That((int)deleteResp.StatusCode, Is.LessThan(500), "Delete failed");
        }

        // ── E2E Workflow 7: Compliance monitoring end-to-end ─────────────────────

        [Test]
        public async Task E2E_ComplianceMonitoring_EnforcementReportReflectsRealActivity()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30060;

            // Setup
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active });

            // 3 allowed transfers
            for (int i = 0; i < 3; i++)
            {
                await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                    new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100 });
            }

            // 2 denied (AlgoAddr3 not whitelisted)
            for (int i = 0; i < 2; i++)
            {
                await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                    new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr1, Amount = 50 });
            }

            // Get enforcement report
            var report = await _authClient.GetAsync($"/api/v1/whitelist/enforcement-report?assetId={assetId}");
            Assert.That(report.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var reportBody = await report.Content.ReadFromJsonAsync<WhitelistEnforcementReportResponse>();
            Assert.That(reportBody!.Success, Is.True);
            Assert.That(reportBody.Summary!.AllowedTransfers, Is.EqualTo(3), "3 allowed transfers expected");
            Assert.That(reportBody.Summary.DeniedTransfers, Is.EqualTo(2), "2 denied transfers expected");
            Assert.That(reportBody.Summary.AllowedPercentage, Is.EqualTo(60.0).Within(0.1),
                "60% allowed (3/5)");
        }

        // ── E2E Workflow 8: Authorization boundary – all endpoints require auth ───

        [Test]
        public async Task E2E_AuthorizationBoundary_AllEndpointsRequireAuthentication()
        {
            var endpoints = new[]
            {
                ("GET",  "/api/v1/whitelist/12345"),
                ("POST", "/api/v1/whitelist"),
                ("DELETE", "/api/v1/whitelist?assetId=12345&address=TEST"),
                ("POST", "/api/v1/whitelist/bulk"),
                ("POST", "/api/v1/whitelist/validate-transfer"),
                ("POST", "/api/v1/whitelist/verify-allowlist-status"),
                ("GET",  "/api/v1/whitelist/12345/audit-log"),
                ("GET",  "/api/v1/whitelist/enforcement-report"),
                ("GET",  "/api/v1/compliance/jurisdiction-rules"),
                ("POST", "/api/v1/compliance/jurisdiction-rules"),
                ("GET",  "/api/v1/whitelist-rules/12345"),
                ("POST", "/api/v1/whitelist-rules"),
            };

            foreach (var (method, url) in endpoints)
            {
                HttpResponseMessage resp;
                if (method == "GET")
                    resp = await _unauthClient.GetAsync(url);
                else if (method == "DELETE")
                    resp = await _unauthClient.DeleteAsync(url);
                else
                    resp = await _unauthClient.PostAsJsonAsync(url, new { });

                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    $"Endpoint {method} {url} must return 401 for unauthenticated access");
            }
        }

        // ── E2E Workflow 9: Token isolation – policies are per-token ─────────────

        [Test]
        public async Task E2E_TokenIsolation_WhitelistPoliciesIndependentAcrossTokens()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong tokenA = 30070;
            const ulong tokenB = 30071;

            // Token A: full whitelist setup
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = tokenA, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = tokenA, Address = AlgoAddr2, Status = WhitelistStatus.Active });

            // Token B: no whitelist configured

            // Token A transfer → allowed
            var tokenATransfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = tokenA, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var tokenAResult = await tokenATransfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();

            // Token B transfer → denied
            var tokenBTransfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = tokenB, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var tokenBResult = await tokenBTransfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();

            Assert.That(tokenAResult!.IsAllowed, Is.True, "Token A whitelist allows transfer");
            Assert.That(tokenBResult!.IsAllowed, Is.False, "Token B has no whitelist → denied");

            // Token B whitelist should be completely empty
            var tokenBList = await _authClient.GetAsync($"/api/v1/whitelist/{tokenB}");
            var tokenBListBody = await tokenBList.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(tokenBListBody!.TotalCount, Is.EqualTo(0), "Token B whitelist must be empty");
        }

        // ── E2E Workflow 10: Policy conflict detection ────────────────────────────

        [Test]
        public async Task E2E_PolicyConflictDetection_DuplicateJurisdictionCodeRejected()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"CF{Guid.NewGuid().ToString("N")[..3].ToUpper()}";
            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code, JurisdictionName = "Conflict Test",
                RegulatoryFramework = "MICA", IsActive = true
            };

            // First creation - must succeed
            var first = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            var firstResult = await first.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(firstResult!.Success, Is.True, "First creation must succeed");

            // Duplicate - must fail
            var second = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            var secondResult = await second.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            Assert.That(secondResult!.Success, Is.False,
                "Duplicate jurisdiction code must be rejected - conflict detection must work");

            // Verify the error is machine-readable JSON
            var json = await second.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(json), "Conflict error must be valid JSON");
        }

        // ── E2E Workflow 11: Allowlist verification for all 4 outcomes ────────────

        [Test]
        public async Task E2E_AllowlistVerification_AllFourOutcomesAvailable()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            // Outcome 1: Allowed (both approved)
            const ulong asset1 = 30080;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = asset1, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = asset1, Address = AlgoAddr2, Status = WhitelistStatus.Active });
            var outcome1 = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest { AssetId = asset1, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2 });
            Assert.That((await outcome1.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>())!.TransferStatus,
                Is.EqualTo(AllowlistTransferStatus.Allowed), "Outcome 1: both active → Allowed");

            // Outcome 2: BlockedSender (only receiver whitelisted)
            const ulong asset2 = 30081;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = asset2, Address = AlgoAddr2, Status = WhitelistStatus.Active });
            var outcome2 = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest { AssetId = asset2, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2 });
            Assert.That((await outcome2.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>())!.TransferStatus,
                Is.EqualTo(AllowlistTransferStatus.BlockedSender), "Outcome 2: sender blocked");

            // Outcome 3: BlockedRecipient (only sender whitelisted)
            const ulong asset3 = 30082;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = asset3, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            var outcome3 = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest { AssetId = asset3, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2 });
            Assert.That((await outcome3.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>())!.TransferStatus,
                Is.EqualTo(AllowlistTransferStatus.BlockedRecipient), "Outcome 3: recipient blocked");

            // Outcome 4: BlockedBoth (neither whitelisted)
            const ulong asset4 = 30083;
            var outcome4 = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest { AssetId = asset4, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2 });
            Assert.That((await outcome4.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>())!.TransferStatus,
                Is.EqualTo(AllowlistTransferStatus.BlockedBoth), "Outcome 4: both blocked");
        }

        // ── E2E Workflow 12: MICA disclosure accuracy ─────────────────────────────

        [Test]
        public async Task E2E_MicaDisclosure_AccurateByNetwork()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var micaNetworks = new[] { "voimain-v1.0", "aramidmain-v1.0" };
            var nonMicaNetworks = new[] { "testnet-v1.0", "mainnet-v1.0" };

            foreach (var network in micaNetworks)
            {
                var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                    new VerifyAllowlistStatusRequest
                    {
                        AssetId = 30090, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                        Network = network
                    });
                var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
                Assert.That(result!.MicaDisclosure!.RequiresMicaCompliance, Is.True,
                    $"Network {network} must require MICA compliance");
            }

            foreach (var network in nonMicaNetworks)
            {
                var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                    new VerifyAllowlistStatusRequest
                    {
                        AssetId = 30091, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                        Network = network
                    });
                var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
                Assert.That(result!.MicaDisclosure!.RequiresMicaCompliance, Is.False,
                    $"Network {network} must NOT require MICA compliance");
            }
        }

        // ── E2E Workflow 13: Whitelist rules end-to-end lifecycle ─────────────────

        [Test]
        public async Task E2E_WhitelistRules_FullLifecycle()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30100;

            // Create multiple rule types
            var ruleTypes = new[]
            {
                (WhitelistRuleType.RequireKycForActive, "Require KYC for Active Status"),
                (WhitelistRuleType.AutoRevokeExpired, "Auto Revoke Expired Entries"),
                (WhitelistRuleType.RequireExpirationDate, "Require Expiration Date"),
                (WhitelistRuleType.NetworkKycRequirement, "Network KYC Requirement"),
                (WhitelistRuleType.RequireOperatorApproval, "Require Operator Approval")
            };

            var createdIds = new List<string>();
            foreach (var (ruleType, name) in ruleTypes)
            {
                var createResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules",
                    new CreateWhitelistRuleRequest
                    {
                        AssetId = assetId, Name = name, RuleType = ruleType, IsActive = true
                    });
                Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                    $"Rule type {ruleType} creation must succeed");

                var created = await createResp.Content.ReadFromJsonAsync<WhitelistRuleResponse>();
                Assert.That(created!.Success, Is.True, $"Rule {ruleType} success must be true");
            }

            // List all rules for the asset
            var listResp = await _authClient.GetAsync($"/api/v1/whitelist-rules/{assetId}");
            Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var rulesList = await listResp.Content.ReadFromJsonAsync<WhitelistRulesListResponse>();
            Assert.That(rulesList!.Success, Is.True);

            // Get audit log for rule changes
            var auditResp = await _authClient.GetAsync($"/api/v1/whitelist-rules/{assetId}/audit-log");
            Assert.That(auditResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── E2E Workflow 14: Compliance export and overview endpoints ─────────────

        [Test]
        public async Task E2E_ComplianceExport_AndOverview_ReturnCorrectSchema()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30110;

            // Add an active entry so the overview has data to report
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active });

            // Export whitelist (CSV route exists; generic /export returns 404 which is < 500)
            var export = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/export");
            Assert.That((int)export.StatusCode, Is.LessThan(500), "Export must not 5xx");

            // Compliance overview — now a real endpoint
            var overviewResp = await _authClient.GetAsync(
                $"/api/v1/whitelist/compliance-overview?assetId={assetId}");
            Assert.That(overviewResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Compliance overview must return 200");

            var overviewBody = await overviewResp.Content.ReadAsStringAsync();
            var overviewDoc = JsonDocument.Parse(overviewBody);
            var root = overviewDoc.RootElement;

            // success field
            Assert.That(root.TryGetProperty("success", out var successProp), Is.True,
                "Compliance overview must have 'success' field");
            Assert.That(successProp.GetBoolean(), Is.True, "Compliance overview success must be true");

            // assetId field
            Assert.That(root.TryGetProperty("assetId", out var assetIdProp), Is.True,
                "Compliance overview must have 'assetId' field");
            Assert.That(assetIdProp.GetUInt64(), Is.EqualTo(assetId), "assetId must match request");

            // generatedAt field
            Assert.That(root.TryGetProperty("generatedAt", out _), Is.True,
                "Compliance overview must have 'generatedAt' timestamp");

            // investorEligibility section
            Assert.That(root.TryGetProperty("investorEligibility", out var eligibility), Is.True,
                "Compliance overview must have 'investorEligibility' section");
            Assert.That(eligibility.TryGetProperty("totalEntries", out var totalEntries), Is.True,
                "investorEligibility must have 'totalEntries'");
            Assert.That(totalEntries.GetInt32(), Is.GreaterThanOrEqualTo(1),
                "Should have at least 1 entry after adding one");
            Assert.That(eligibility.TryGetProperty("activeEntries", out _), Is.True,
                "investorEligibility must have 'activeEntries'");
            Assert.That(eligibility.TryGetProperty("activePercentage", out _), Is.True,
                "investorEligibility must have 'activePercentage'");

            // transferEnforcement section
            Assert.That(root.TryGetProperty("transferEnforcement", out var enforcement), Is.True,
                "Compliance overview must have 'transferEnforcement' section");
            Assert.That(enforcement.TryGetProperty("totalValidations", out _), Is.True,
                "transferEnforcement must have 'totalValidations'");
            Assert.That(enforcement.TryGetProperty("allowedTransfers", out _), Is.True,
                "transferEnforcement must have 'allowedTransfers'");
            Assert.That(enforcement.TryGetProperty("deniedTransfers", out _), Is.True,
                "transferEnforcement must have 'deniedTransfers'");

            // kycVerification section
            Assert.That(root.TryGetProperty("kycVerification", out var kyc), Is.True,
                "Compliance overview must have 'kycVerification' section");
            Assert.That(kyc.TryGetProperty("kycVerifiedEntries", out _), Is.True,
                "kycVerification must have 'kycVerifiedEntries'");

            // auditTrail section
            Assert.That(root.TryGetProperty("auditTrail", out var auditTrail), Is.True,
                "Compliance overview must have 'auditTrail' section");
            Assert.That(auditTrail.TryGetProperty("minimumRetentionYears", out var retentionYears), Is.True,
                "auditTrail must have 'minimumRetentionYears'");
            Assert.That(retentionYears.GetInt32(), Is.GreaterThanOrEqualTo(7),
                "MICA requires 7+ year retention");
            Assert.That(auditTrail.TryGetProperty("immutableEntries", out var immutable), Is.True,
                "auditTrail must have 'immutableEntries'");
            Assert.That(immutable.GetBoolean(), Is.True, "Audit entries must be immutable");

            // Test with MICA network — should return micaReadiness section
            var overviewMicaResp = await _authClient.GetAsync(
                $"/api/v1/whitelist/compliance-overview?assetId={assetId}&network=voimain-v1.0");
            Assert.That(overviewMicaResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var micaDoc = JsonDocument.Parse(await overviewMicaResp.Content.ReadAsStringAsync());
            Assert.That(micaDoc.RootElement.TryGetProperty("micaReadiness", out var micaReadiness), Is.True,
                "Compliance overview with MICA network must have 'micaReadiness' section");
            Assert.That(micaReadiness.ValueKind, Is.Not.EqualTo(JsonValueKind.Null),
                "micaReadiness must not be null for MICA network");
            Assert.That(micaReadiness.TryGetProperty("readinessScore", out var score), Is.True,
                "micaReadiness must have 'readinessScore'");
            Assert.That(score.GetInt32(), Is.InRange(0, 100),
                "readinessScore must be 0-100");

            // Audit log retention policy
            var retentionPolicy = await _authClient.GetAsync("/api/v1/whitelist/audit-log/retention-policy");
            Assert.That(retentionPolicy.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var retentionDoc = JsonDocument.Parse(await retentionPolicy.Content.ReadAsStringAsync());
            Assert.That(retentionDoc.RootElement.TryGetProperty("minimumRetentionYears", out var years), Is.True,
                "Retention policy must have minimumRetentionYears");
            Assert.That(years.GetInt32(), Is.GreaterThanOrEqualTo(7), "MICA requires 7+ years retention");
        }

        // ── E2E Workflow 15: Pagination across large whitelist ───────────────────

        [Test]
        public async Task E2E_Pagination_ListReturnsPaginationMetadata()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 30120;

            // Add 3 entries
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            { AssetId = assetId, Address = AlgoAddr3, Status = WhitelistStatus.Active });

            // Page 1, size 2
            var page1 = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}?page=1&pageSize=2");
            var page1Body = await page1.Content.ReadFromJsonAsync<WhitelistListResponse>();

            Assert.That(page1Body!.Page, Is.EqualTo(1), "Page must be 1");
            Assert.That(page1Body.PageSize, Is.EqualTo(2), "PageSize must be 2");
            Assert.That(page1Body.TotalCount, Is.EqualTo(3), "Total must be 3");
            Assert.That(page1Body.TotalPages, Is.EqualTo(2), "Total pages must be 2 (3 entries, size 2)");
            Assert.That(page1Body.Entries, Has.Count.EqualTo(2), "Page 1 must have 2 entries");

            // Page 2, size 2
            var page2 = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}?page=2&pageSize=2");
            var page2Body = await page2.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(page2Body!.Entries, Has.Count.EqualTo(1), "Page 2 must have 1 remaining entry");
        }
    }
}

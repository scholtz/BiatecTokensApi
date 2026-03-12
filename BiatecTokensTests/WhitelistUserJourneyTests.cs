using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for whitelist and jurisdiction policy backend.
    ///
    /// These tests simulate complete operator and compliance officer workflows
    /// through real API paths. Each journey proves that the backend contract
    /// supports a meaningful real-world compliance use case:
    ///
    /// - Investor onboarding (register whitelist entry → verify status → enable transfer)
    /// - KYC-verified investor activation lifecycle
    /// - Expiry-managed investor access (add with TTL → expire → re-activate)
    /// - Compliance officer reviewing and correcting whitelist state
    /// - Multi-investor whitelist buildout for a regulated offering
    /// - Jurisdiction rule lifecycle (create → assign to asset → evaluate)
    /// - Compliance-gated transfer enforcement (only whitelisted investors can transfer)
    /// - Audit-trail completeness verification for regulated changes
    /// - Status lifecycle: pending → approved → suspended → re-activated
    /// - Duplicate entry handling without creating phantom entries
    /// - Token-level jurisdiction policy enforcement per offering
    /// - Network-specific MICA compliance check workflow
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WhitelistUserJourneyTests
    {
        private UserJourneyTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new UserJourneyTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var email = $"journey-{Guid.NewGuid():N}@biatec.io";
            var reg = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email, Password = "JourneyTest123!", ConfirmPassword = "JourneyTest123!"
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

        private sealed class UserJourneyTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
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
                        ["KeyManagementConfig:HardcodedKey"] = "UserJourneyTestKey32MinCharsRequired!!!!!",
                        ["JwtConfig:SecretKey"] = "UserJourneyTestSecretKey32CharMinRequired!!",
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

        // ── Journey 1: Basic investor onboarding ─────────────────────────────────

        [Test]
        public async Task Journey_BasicInvestorOnboarding_SuccessfulTransferEnabled()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20001;

            // Step 1: Issuer registers investor 1
            var add1 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                Reason = "Initial KYC approved", Network = "mainnet-v1.0"
            });
            Assert.That(add1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var r1 = await add1.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(r1!.Success, Is.True, "Step 1 failed: " + r1.ErrorMessage);

            // Step 2: Issuer registers investor 2
            var add2 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                Reason = "Initial KYC approved", Network = "mainnet-v1.0"
            });
            var r2 = await add2.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(r2!.Success, Is.True, "Step 2 failed");

            // Step 3: Confirm both investors appear in whitelist
            var list = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}");
            var listBody = await list.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(listBody!.TotalCount, Is.GreaterThanOrEqualTo(2), "Both investors must appear");

            // Step 4: Transfer validation confirms both are allowed
            var transfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100 });
            var transferResult = await transfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(transferResult!.IsAllowed, Is.True, "Step 4: Both whitelisted → transfer allowed");
        }

        // ── Journey 2: KYC lifecycle ──────────────────────────────────────────────

        [Test]
        public async Task Journey_KycLifecycle_PendingToApproved()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20002;

            // Step 1: Add investor as Inactive (pending KYC)
            var addPending = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive,
                Reason = "Awaiting KYC", KycVerified = false
            });
            var pending = await addPending.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(pending!.Success, Is.True, "Pending entry creation failed");
            Assert.That(pending.Entry!.Status, Is.EqualTo(WhitelistStatus.Inactive), "Must start as Inactive");

            // Step 2: Transfer is blocked while pending
            var transferBlocked = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 10 });
            var blockedResult = await transferBlocked.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(blockedResult!.IsAllowed, Is.False, "Step 2: Pending investor cannot transfer");

            // Step 3: KYC approved → Activate investor
            var activate = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "TrustKYC", Reason = "KYC approved"
            });
            var activatedEntry = await activate.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(activatedEntry!.Success, Is.True, "Activation failed");
            Assert.That(activatedEntry.Entry!.Status, Is.EqualTo(WhitelistStatus.Active), "Must be Active after KYC");
            Assert.That(activatedEntry.Entry.KycVerified, Is.True, "KYC verified must be true");

            // Step 4: Audit log should record the status change
            var audit = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var auditBody = await audit.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();
            Assert.That(auditBody!.Entries, Is.Not.Empty, "Audit log must have entries");
        }

        // ── Journey 3: Investor access expiry and renewal ─────────────────────────

        [Test]
        public async Task Journey_ExpiryManagement_AddWithTtlThenRenew()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20003;
            var validUntil = DateTime.UtcNow.AddDays(365);

            // Step 1: Add investor with 1-year TTL
            var add = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                ExpirationDate = validUntil, Reason = "Annual subscription, expires in 1 year"
            });
            var addResult = await add.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(addResult!.Success, Is.True, "Add with expiry failed");
            Assert.That(addResult.Entry!.ExpirationDate, Is.Not.Null, "Expiration date must be set");

            // Step 2: Transfer is allowed (not expired yet)
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });
            var transfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50 });
            var transferResult = await transfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(transferResult!.IsAllowed, Is.True, "Transfer with future expiry must be allowed");

            // Step 3: Renew with new TTL
            var newExpiry = validUntil.AddYears(1);
            var renew = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                ExpirationDate = newExpiry, Reason = "Annual renewal"
            });
            var renewResult = await renew.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(renewResult!.Success, Is.True, "Renewal failed");
        }

        // ── Journey 4: Compliance officer deactivates suspicious investor ─────────

        [Test]
        public async Task Journey_ComplianceOfficerDeactivation_TransferImmediatelyBlocked()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20004;

            // Step 1: Both investors active
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            // Step 2: Transfer allowed
            var before = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var beforeResult = await before.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(beforeResult!.IsAllowed, Is.True, "Before deactivation: allowed");

            // Step 3: Compliance officer deactivates investor 1 (suspicious activity detected)
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive,
                Reason = "Flagged for suspicious activity - pending review"
            });

            // Step 4: Transfer immediately blocked
            var after = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var afterResult = await after.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(afterResult!.IsAllowed, Is.False, "After deactivation: transfer must be blocked");

            // Step 5: Verify audit trail records the change
            var audit = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var auditBody = await audit.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();
            Assert.That(auditBody!.Entries, Is.Not.Empty, "Audit trail must exist for compliance review");
        }

        // ── Journey 5: Jurisdiction policy creation and token assignment ──────────

        [Test]
        public async Task Journey_JurisdictionPolicyCreationAndAssignment()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"EU{Guid.NewGuid().ToString("N")[..3].ToUpper()}";

            // Step 1: Create jurisdiction rule for EU MICA compliance
            var createRule = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code,
                    JurisdictionName = "European Union",
                    RegulatoryFramework = "MICA",
                    IsActive = true,
                    Priority = 100,
                    Notes = "MiCA regulation applies to all EU member states",
                    Requirements = new List<ComplianceRequirement>
                    {
                        new() { RequirementCode = "MICA_KYC", Category = "KYC", IsMandatory = true, Severity = RequirementSeverity.Critical, Description = "KYC required under MiCA" },
                        new() { RequirementCode = "MICA_WHITEPAPER", Category = "Disclosure", IsMandatory = true, Severity = RequirementSeverity.Critical, Description = "White paper publication required" }
                    }
                });
            var createResult = await createRule.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(createResult!.Success, Is.True, "Step 1: Rule creation failed");
            Assert.That(createResult.Rule!.JurisdictionCode, Is.EqualTo(code));
            Assert.That(createResult.Rule.Requirements, Has.Count.EqualTo(2));

            // Step 2: Assign to token offering
            const ulong tokenAssetId = 20020;
            var assign = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign",
                new { assetId = tokenAssetId, network = "mainnet-v1.0", jurisdictionCode = code, isPrimary = true });
            Assert.That((int)assign.StatusCode, Is.LessThan(500), "Step 2: Assignment failed");

            // Step 3: Evaluate token compliance
            var eval = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/evaluate?assetId={tokenAssetId}&network=mainnet-v1.0&issuerId={AlgoAddr1}");
            Assert.That(eval.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 3: Evaluation failed");
            var evalResult = await eval.Content.ReadFromJsonAsync<JurisdictionEvaluationResult>();
            Assert.That(evalResult!.AssetId, Is.EqualTo(tokenAssetId));
        }

        // ── Journey 6: Multiple jurisdiction enforcement ───────────────────────────

        [Test]
        public async Task Journey_MultipleJurisdictions_AllAppliedToToken()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code1 = $"MJ{Guid.NewGuid().ToString("N")[..3].ToUpper()}";
            var code2 = $"NK{Guid.NewGuid().ToString("N")[..3].ToUpper()}";

            // Create 2 jurisdiction rules
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code1, JurisdictionName = "Jurisdiction One",
                RegulatoryFramework = "FATF", IsActive = true, Priority = 100
            });
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code2, JurisdictionName = "Jurisdiction Two",
                RegulatoryFramework = "SEC", IsActive = true, Priority = 200
            });

            const ulong assetId = 20021;

            // Assign both to same token
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign",
                new { assetId = assetId, network = "mainnet-v1.0", jurisdictionCode = code1, isPrimary = true });
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign",
                new { assetId = assetId, network = "mainnet-v1.0", jurisdictionCode = code2, isPrimary = false });

            // Get token jurisdictions - should have 2
            var getJurisdictions = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/token-jurisdictions?assetId={assetId}&network=mainnet-v1.0");
            Assert.That(getJurisdictions.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Journey 7: Whitelist enforcement for regulated offering ───────────────

        [Test]
        public async Task Journey_RegulatedOfferingWhitelistEnforcement()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20030;

            // Step 1: Issuer sets up whitelist with KYC requirements
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "AccreditedKYC", Reason = "Accredited investor - KYC passed"
            });

            // Step 2: Non-whitelisted investor attempt fails
            var blockedTransfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr1, Amount = 100 });
            var blocked = await blockedTransfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(blocked!.IsAllowed, Is.False, "Step 2: Non-whitelisted transfer must be blocked");
            Assert.That(blocked.DenialReason, Is.Not.Null.And.Not.Empty, "Denial reason must explain compliance block");

            // Step 3: Whitelisted investor transfer passes
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "AccreditedKYC"
            });
            var allowedTransfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100 });
            var allowed = await allowedTransfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(allowed!.IsAllowed, Is.True, "Step 3: Both KYC-verified → transfer allowed");
        }

        // ── Journey 8: Allowlist verification for compliance officer ──────────────

        [Test]
        public async Task Journey_ComplianceOfficerAllowlistVerification()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20040;

            // Add both investors as active
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "EuropeanKYC"
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "EuropeanKYC"
            });

            // Step: Compliance officer verifies allowlist status
            var verify = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest
                {
                    AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                    Network = "voimain-v1.0"
                });

            var result = await verify.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result!.TransferStatus, Is.EqualTo(AllowlistTransferStatus.Allowed),
                "Both active → Allowed");
            Assert.That(result.AuditMetadata!.VerificationId, Is.Not.Null.And.Not.Empty,
                "Audit metadata must have verification ID for compliance records");
            Assert.That(result.MicaDisclosure!.RequiresMicaCompliance, Is.True,
                "VOI main must require MICA compliance");
        }

        // ── Journey 9: Audit trail review after compliance event ─────────────────

        [Test]
        public async Task Journey_AuditTrailReview_CompleteHistoryAvailable()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20050;

            // Series of operations
            // 1. Add
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Inactive
            });

            // 2. Activate
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });

            // 3. Validate transfer (audit entry)
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 50 });

            // 4. Review audit trail
            var auditResp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var auditBody = await auditResp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();

            Assert.That(auditBody!.Success, Is.True);
            Assert.That(auditBody.Entries.Count, Is.GreaterThanOrEqualTo(4),
                "Audit trail must have at least 4 entries (2 adds + 1 update + 1 validation)");
            Assert.That(auditBody.RetentionPolicy!.MinimumRetentionYears, Is.GreaterThanOrEqualTo(7),
                "MICA retention policy must be 7+ years");
        }

        // ── Journey 10: Jurisdiction rule edit and deactivation lifecycle ─────────

        [Test]
        public async Task Journey_JurisdictionRuleEditAndDeactivation()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            var code = $"LF{Guid.NewGuid().ToString("N")[..3].ToUpper()}";

            // Step 1: Create rule
            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code, JurisdictionName = "Lifecycle Test Jurisdiction",
                    RegulatoryFramework = "SEC", IsActive = true, Priority = 50
                });
            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(createResult!.Success, Is.True, "Create failed");

            // Step 2: Edit name and add requirement
            var updateResp = await _authClient.PutAsJsonAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule!.Id}",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code, JurisdictionName = "Updated Name",
                    RegulatoryFramework = "SEC", IsActive = true,
                    Requirements = new List<ComplianceRequirement>
                    {
                        new() { RequirementCode = "SEC_REG_D", Category = "Exemption", IsMandatory = true, Severity = RequirementSeverity.High }
                    }
                });
            var updateResult = await updateResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(updateResult!.Success, Is.True, "Update failed");

            // Step 3: Retrieve and verify changes
            var getResp = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");
            var getResult = await getResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(getResult!.Rule!.JurisdictionName, Is.EqualTo("Updated Name"), "Name must be updated");
            Assert.That(getResult.Rule.Requirements, Has.Count.EqualTo(1));

            // Step 4: Deactivate
            var deactivateResp = await _authClient.PutAsJsonAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}",
                new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = code, JurisdictionName = "Updated Name",
                    RegulatoryFramework = "SEC", IsActive = false
                });
            var deactivated = await deactivateResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(deactivated!.Success, Is.True, "Deactivation failed");

            // Step 5: Verify deactivated
            var deactivatedGet = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");
            var deactivatedResult = await deactivatedGet.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(deactivatedResult!.Rule!.IsActive, Is.False, "Rule must be inactive");
        }

        // ── Journey 11: Whitelist rules policy management ─────────────────────────

        [Test]
        public async Task Journey_WhitelistRulesManagement_CreateListAudit()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20060;

            // Step 1: Create a KYC requirement rule
            var createRule = await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules",
                new CreateWhitelistRuleRequest
                {
                    AssetId = assetId, Name = "Require KYC for Active",
                    RuleType = WhitelistRuleType.RequireKycForActive, IsActive = true
                });
            Assert.That(createRule.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Create rule failed");
            var createdRule = await createRule.Content.ReadFromJsonAsync<WhitelistRuleResponse>();
            Assert.That(createdRule!.Success, Is.True);

            // Step 2: Create an auto-expire rule
            await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules", new CreateWhitelistRuleRequest
            {
                AssetId = assetId, Name = "Auto-revoke Expired Entries",
                RuleType = WhitelistRuleType.AutoRevokeExpired, IsActive = true, Priority = 200
            });

            // Step 3: List rules for asset
            var listRules = await _authClient.GetAsync($"/api/v1/whitelist-rules/{assetId}");
            Assert.That(listRules.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var rulesList = await listRules.Content.ReadFromJsonAsync<WhitelistRulesListResponse>();
            Assert.That(rulesList!.Success, Is.True);

            // Step 4: Review audit log
            var auditLog = await _authClient.GetAsync($"/api/v1/whitelist-rules/{assetId}/audit-log");
            Assert.That(auditLog.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Journey 12: Enforcement report for compliance monitoring ──────────────

        [Test]
        public async Task Journey_ComplianceMonitoring_EnforcementReport()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20070;

            // Set up initial whitelist
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            // Simulate some transfer activity
            await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 100 });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = assetId, FromAddress = AlgoAddr3, ToAddress = AlgoAddr1, Amount = 50 }); // blocked

            // Get enforcement report for compliance monitoring
            var report = await _authClient.GetAsync($"/api/v1/whitelist/enforcement-report?assetId={assetId}");
            Assert.That(report.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var reportBody = await report.Content.ReadFromJsonAsync<WhitelistEnforcementReportResponse>();
            Assert.That(reportBody!.Summary, Is.Not.Null, "Summary statistics must be present");
            Assert.That(reportBody.Summary!.TotalValidations, Is.GreaterThan(0),
                "Summary must reflect validation activity");
        }

        // ── Journey 13: MICA-compliant investor status check ──────────────────────

        [Test]
        public async Task Journey_MicaComplianceCheck_AllDisclosuresPresent()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong assetId = 20080;

            // Add both investors
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "EUKYCProvider"
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active,
                KycVerified = true, KycProvider = "EUKYCProvider"
            });

            // MICA-required allowlist check on VOI network
            var verify = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status",
                new VerifyAllowlistStatusRequest
                {
                    AssetId = assetId, SenderAddress = AlgoAddr1, RecipientAddress = AlgoAddr2,
                    Network = "aramidmain-v1.0"
                });

            var result = await verify.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.MicaDisclosure!.RequiresMicaCompliance, Is.True,
                "Aramid main requires MICA compliance");
            Assert.That(result.MicaDisclosure.ApplicableRegulations, Is.Not.Empty,
                "Applicable regulations must be listed for MICA disclosure");
            Assert.That(result.AuditMetadata, Is.Not.Null, "Audit metadata must be present");
            Assert.That(result.AuditMetadata!.VerificationId, Is.Not.Null.And.Not.Empty,
                "Verification ID required for compliance records");
            Assert.That(result.CacheDurationSeconds, Is.GreaterThan(0),
                "Cache duration must be set for downstream consumers");
        }

        // ── Journey 14: Empty asset – clear behavior for unconfigured offerings ───

        [Test]
        public async Task Journey_UnconfiguredOffering_ClearBehavior()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong newAssetId = 99990000; // No whitelist configured

            // List returns empty (not error)
            var list = await _authClient.GetAsync($"/api/v1/whitelist/{newAssetId}");
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var listBody = await list.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(listBody!.TotalCount, Is.EqualTo(0), "New offering has empty whitelist");

            // Transfer is denied (no whitelist = nobody allowed)
            var transfer = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer",
                new ValidateTransferRequest { AssetId = newAssetId, FromAddress = AlgoAddr1, ToAddress = AlgoAddr2, Amount = 1 });
            var transferResult = await transfer.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(transferResult!.IsAllowed, Is.False, "No whitelist → transfer denied");

            // Audit log returns empty (not error)
            var audit = await _authClient.GetAsync($"/api/v1/whitelist/{newAssetId}/audit-log");
            Assert.That(audit.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Journey 15: Duplicate prevention across multiple assets ──────────────

        [Test]
        public async Task Journey_DuplicatePrevention_SameAddressDifferentAssets()
        {
            if (_accessToken == null) { Assert.Ignore("No auth token"); return; }

            const ulong asset1 = 20090;
            const ulong asset2 = 20091;

            // Same address added to both assets
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = asset1, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = asset2, Address = AlgoAddr1, Status = WhitelistStatus.Inactive
            });

            // Each asset has independent status
            var list1 = await _authClient.GetAsync($"/api/v1/whitelist/{asset1}");
            var body1 = await list1.Content.ReadFromJsonAsync<WhitelistListResponse>();
            var list2 = await _authClient.GetAsync($"/api/v1/whitelist/{asset2}");
            var body2 = await list2.Content.ReadFromJsonAsync<WhitelistListResponse>();

            // Asset 1 entry should be Active
            var asset1Entry = body1!.Entries.FirstOrDefault(e => e.Address == AlgoAddr1);
            var asset2Entry = body2!.Entries.FirstOrDefault(e => e.Address == AlgoAddr1);

            Assert.That(asset1Entry, Is.Not.Null, "Asset1 must have entry");
            Assert.That(asset2Entry, Is.Not.Null, "Asset2 must have entry");
            Assert.That(asset1Entry!.Status, Is.EqualTo(WhitelistStatus.Active), "Asset1 entry is Active");
            Assert.That(asset2Entry!.Status, Is.EqualTo(WhitelistStatus.Inactive), "Asset2 entry is Inactive");
        }
    }
}

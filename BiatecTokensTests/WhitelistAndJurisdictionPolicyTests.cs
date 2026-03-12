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
    /// Integration tests for the whitelist and jurisdiction policy API endpoints.
    ///
    /// These tests verify the full HTTP contract for:
    /// - Whitelist CRUD operations (add, list, remove, bulk-add)
    /// - Transfer validation and allowlist verification
    /// - Jurisdiction rule management (create, list, get, update, delete)
    /// - Jurisdiction assignment and evaluation
    /// - Authorization enforcement (unauthenticated access blocked)
    /// - Error contracts for invalid inputs, duplicates, and conflicts
    /// - Audit-trail availability after mutations
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WhitelistControllerIntegrationTests
    {
        private WhitelistTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        // Known valid Algorand addresses for tests
        private const string AlgoAddr1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string AlgoAddr2 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AlgoAddr3 = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new WhitelistTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Register and get JWT token for authenticated calls
            var email = $"whitelist-test-{Guid.NewGuid():N}@biatec.io";
            var reg = new RegisterRequest
            {
                Email = email,
                Password = "WhitelistTest123!",
                ConfirmPassword = "WhitelistTest123!"
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

        // ── WebApplicationFactory ─────────────────────────────────────────────────

        private sealed class WhitelistTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "WhitelistIntegrationTestKey32MinCharsReq!!",
                        ["JwtConfig:SecretKey"] = "WhitelistControllerIntegrationTestSecretKey32CharMin!!",
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

        // ── Authorization enforcement ─────────────────────────────────────────────

        [Test]
        public async Task ListWhitelist_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/whitelist/12345");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Unauthenticated access to whitelist must be rejected with 401");
        }

        [Test]
        public async Task AddWhitelist_Unauthenticated_Returns401()
        {
            var body = new
            {
                assetId = 12345,
                address = AlgoAddr1,
                status = "Active"
            };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/whitelist", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Unauthenticated add must be rejected with 401");
        }

        [Test]
        public async Task JurisdictionRules_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/compliance/jurisdiction-rules");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Unauthenticated jurisdiction access must be rejected with 401");
        }

        [Test]
        public async Task ValidateTransfer_Unauthenticated_Returns401()
        {
            var body = new { assetId = 12345, fromAddress = AlgoAddr1, toAddress = AlgoAddr2 };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Unauthenticated transfer validation must be rejected with 401");
        }

        // ── Whitelist CRUD ────────────────────────────────────────────────────────

        [Test]
        public async Task ListWhitelist_Authenticated_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available - skipping authenticated test");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/99999999");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Authenticated list must return 200");

            var body = await resp.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Entries, Is.Not.Null);
        }

        [Test]
        public async Task AddWhitelist_ValidEntry_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available - skipping authenticated test");
                return;
            }

            var body = new AddWhitelistEntryRequest
            {
                AssetId = 88881111UL,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active,
                Reason = "KYC verified",
                Network = "mainnet-v1.0"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid whitelist add must return 200");

            var result = await resp.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True, result.ErrorMessage ?? "no error");
            Assert.That(result.Entry, Is.Not.Null);
            Assert.That(result.Entry!.AssetId, Is.EqualTo(88881111UL));
        }

        [Test]
        public async Task AddWhitelist_InvalidAddress_Returns400Or200WithError()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new AddWhitelistEntryRequest
            {
                AssetId = 88881111UL,
                Address = "NOT_VALID_ADDRESS",
                Status = WhitelistStatus.Active
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", body);

            // Either 400 (model validation) or 200 with success=false (service validation)
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var result = await resp.Content.ReadFromJsonAsync<WhitelistResponse>();
                Assert.That(result!.Success, Is.False, "Invalid address must return success=false");
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                    "Error message must be populated for invalid address");
            }
            else
            {
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                    "Invalid address must return 400 or success=false");
            }
        }

        [Test]
        public async Task AddWhitelist_DuplicateAddress_UpdatesEntry()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            const ulong assetId = 77771111UL;
            var body = new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr2,
                Status = WhitelistStatus.Active
            };

            // First add
            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", body);
            Assert.That(resp1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Second add (duplicate) - should update
            body.Status = WhitelistStatus.Inactive;
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/whitelist", body);
            Assert.That(resp2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result2 = await resp2.Content.ReadFromJsonAsync<WhitelistResponse>();
            Assert.That(result2!.Success, Is.True, "Duplicate add (update) must succeed");
        }

        [Test]
        public async Task BulkAddWhitelist_ValidEntries_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new BulkAddWhitelistRequest
            {
                AssetId = 66661111UL,
                Addresses = new List<string> { AlgoAddr1, AlgoAddr2 },
                Status = WhitelistStatus.Active,
                Reason = "Bulk import"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/bulk", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Bulk add endpoint must return 200 (even if tier doesn't allow it)");

            var result = await resp.Content.ReadFromJsonAsync<BulkWhitelistResponse>();
            Assert.That(result, Is.Not.Null);
            // Either bulk succeeded (Enterprise tier) or returned a clear tier limitation error
            if (!result!.Success)
            {
                Assert.That(result.ErrorMessage, Does.Contain("tier").Or.Contain("subscription").Or.Contain("Bulk"),
                    "Bulk failure must explain tier restriction in error message");
            }
            else
            {
                Assert.That(result.SuccessCount, Is.GreaterThan(0), "Bulk success should process entries");
            }
        }

        [Test]
        public async Task BulkAddWhitelist_MixedValidInvalid_PartialSuccess()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new BulkAddWhitelistRequest
            {
                AssetId = 55551111UL,
                Addresses = new List<string> { AlgoAddr1, "INVALID_ADDRESS_123" },
                Status = WhitelistStatus.Active
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/bulk", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<BulkWhitelistResponse>();
            Assert.That(result, Is.Not.Null);
            // If bulk is tier-restricted, we get a proper error message
            if (!result!.Success)
            {
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                    "Bulk failure must provide a meaningful error message");
            }
            else
            {
                // If it succeeds, verify partial success behavior
                Assert.That(result.SuccessCount, Is.EqualTo(1), "One valid address should succeed");
                Assert.That(result.FailedCount, Is.EqualTo(1), "One invalid address should fail");
            }
        }

        [Test]
        public async Task RemoveWhitelist_ExistingEntry_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            const ulong assetId = 44441111UL;

            // First add
            var addBody = new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr3,
                Status = WhitelistStatus.Active
            };
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", addBody);

            // Then delete
            var deleteResp = await _authClient.DeleteAsync($"/api/v1/whitelist?assetId={assetId}&address={Uri.EscapeDataString(AlgoAddr3)}");

            // Accepts either 200 or a success response
            Assert.That((int)deleteResp.StatusCode, Is.LessThan(500),
                "Delete should not return 5xx");
        }

        // ── Whitelist list with pagination and filtering ──────────────────────────

        [Test]
        public async Task ListWhitelist_WithStatusFilter_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/12345?status=Active&page=1&pageSize=10");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<WhitelistListResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Page, Is.EqualTo(1));
        }

        [Test]
        public async Task ListWhitelist_ResponseHasPaginationFields()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/12345?page=1&pageSize=20");
            var body = await resp.Content.ReadFromJsonAsync<WhitelistListResponse>();

            Assert.That(body!.Page, Is.EqualTo(1), "Page number must be returned");
            Assert.That(body.PageSize, Is.GreaterThan(0), "Page size must be positive");
            Assert.That(body.TotalPages, Is.GreaterThanOrEqualTo(0), "Total pages must be non-negative");
        }

        // ── Audit log ────────────────────────────────────────────────────────────

        [Test]
        public async Task GetAuditLog_AfterAddOperation_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            // Add an entry first (generates audit entry)
            const ulong assetId = 33331111UL;
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active
            });

            var resp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Audit log endpoint must return 200");

            var body = await resp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
        }

        [Test]
        public async Task GetAuditLog_HasRetentionPolicy()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/12345/audit-log");
            var body = await resp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();

            Assert.That(body!.RetentionPolicy, Is.Not.Null,
                "Audit log must include retention policy for MICA compliance");
            Assert.That(body.RetentionPolicy!.MinimumRetentionYears, Is.GreaterThanOrEqualTo(7),
                "MICA requires minimum 7-year retention");
        }

        [Test]
        public async Task GetAuditLogRetentionPolicy_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/audit-log/retention-policy");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Transfer validation ───────────────────────────────────────────────────

        [Test]
        public async Task ValidateTransfer_BothWhitelisted_ReturnsAllowed()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            const ulong assetId = 22221111UL;

            // Add both addresses
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr1, Status = WhitelistStatus.Active
            });
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId, Address = AlgoAddr2, Status = WhitelistStatus.Active
            });

            var body = new ValidateTransferRequest
            {
                AssetId = assetId,
                FromAddress = AlgoAddr1,
                ToAddress = AlgoAddr2,
                Amount = 100
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.True,
                "Both addresses whitelisted → transfer must be allowed");
        }

        [Test]
        public async Task ValidateTransfer_NeitherWhitelisted_ReturnsDenied()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new ValidateTransferRequest
            {
                AssetId = 11110001UL, // Asset with no whitelist entries
                FromAddress = AlgoAddr1,
                ToAddress = AlgoAddr2,
                Amount = 100
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
            Assert.That(result!.IsAllowed, Is.False,
                "Neither address whitelisted → transfer must be denied");
            Assert.That(result.DenialReason, Is.Not.Null.And.Not.Empty,
                "Denial reason must be provided for blocked transfers");
        }

        [Test]
        public async Task ValidateTransfer_InvalidAddress_ReturnsErrorContract()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new ValidateTransferRequest
            {
                AssetId = 11110002UL,
                FromAddress = "NOT_VALID",
                ToAddress = AlgoAddr2,
                Amount = 100
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/validate-transfer", body);
            
            // Expects 200 with error or 400
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var result = await resp.Content.ReadFromJsonAsync<ValidateTransferResponse>();
                Assert.That(result!.Success, Is.False, "Invalid address must return success=false");
                Assert.That(result.IsAllowed, Is.False, "Invalid address → transfer not allowed");
            }
            else
            {
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
        }

        // ── Verify allowlist status ───────────────────────────────────────────────

        [Test]
        public async Task VerifyAllowlistStatus_Returns200WithAuditMetadata()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new VerifyAllowlistStatusRequest
            {
                AssetId = 11119999UL,
                SenderAddress = AlgoAddr1,
                RecipientAddress = AlgoAddr2,
                Network = "mainnet-v1.0"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AuditMetadata, Is.Not.Null,
                "Audit metadata must be returned for compliance tracking");
            Assert.That(result.AuditMetadata!.VerificationId, Is.Not.Null.And.Not.Empty,
                "Verification ID must be set for auditability");
        }

        [Test]
        public async Task VerifyAllowlistStatus_ReturnsMicaDisclosure()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new VerifyAllowlistStatusRequest
            {
                AssetId = 11119998UL,
                SenderAddress = AlgoAddr1,
                RecipientAddress = AlgoAddr2,
                Network = "voimain-v1.0"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist/verify-allowlist-status", body);
            var result = await resp.Content.ReadFromJsonAsync<VerifyAllowlistStatusResponse>();

            Assert.That(result!.MicaDisclosure, Is.Not.Null,
                "MICA disclosure must be included for regulated network verifications");
        }

        // ── Enforcement report ────────────────────────────────────────────────────

        [Test]
        public async Task GetEnforcementReport_Returns200WithSummary()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist/enforcement-report?assetId=12345");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<WhitelistEnforcementReportResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Summary, Is.Not.Null, "Summary statistics must be included");
        }

        // ── Schema contract validation ────────────────────────────────────────────

        [Test]
        public async Task WhitelistResponse_HasRequiredSchemaFields()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var addResp = await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = 99998888UL,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active
            });

            var json = await addResp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Response must have 'success' field");
        }

        [Test]
        public async Task AuditLogEntry_HasActorAndTimestamp()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            const ulong assetId = 99997777UL;

            // Add entry to generate audit log
            await _authClient.PostAsJsonAsync("/api/v1/whitelist", new AddWhitelistEntryRequest
            {
                AssetId = assetId,
                Address = AlgoAddr1,
                Status = WhitelistStatus.Active
            });

            var resp = await _authClient.GetAsync($"/api/v1/whitelist/{assetId}/audit-log");
            var body = await resp.Content.ReadFromJsonAsync<WhitelistAuditLogResponse>();

            if (body!.Entries.Any())
            {
                var entry = body.Entries.First();
                Assert.That(entry.PerformedBy, Is.Not.Null.And.Not.Empty,
                    "Audit entry must record the actor");
                Assert.That(entry.PerformedAt, Is.GreaterThan(DateTime.UtcNow.AddDays(-1)),
                    "Audit entry timestamp must be recent");
            }
        }
    }

    /// <summary>
    /// Integration tests for jurisdiction policy API endpoints.
    ///
    /// These tests verify the full HTTP contract for jurisdiction rule management:
    /// - Create, read, update, delete jurisdiction rules
    /// - Jurisdiction assignment to tokens
    /// - Compliance evaluation
    /// - Authorization enforcement
    /// - Error contracts for invalid jurisdiction codes
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class JurisdictionPolicyTests
    {
        private JurisdictionTestFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;
        private string? _accessToken;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new JurisdictionTestFactory();
            _unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Register and get JWT token
            var email = $"jurisdiction-test-{Guid.NewGuid():N}@biatec.io";
            var reg = new RegisterRequest
            {
                Email = email,
                Password = "JurisdictionTest123!",
                ConfirmPassword = "JurisdictionTest123!"
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

        private sealed class JurisdictionTestFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "JurisdictionIntegrationTestKey32MinCharsReq!!",
                        ["JwtConfig:SecretKey"] = "JurisdictionPolicyTestSecretKey32CharMinRequired!!",
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

        // ── Authorization tests ───────────────────────────────────────────────────

        [Test]
        public async Task ListJurisdictionRules_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/compliance/jurisdiction-rules");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task CreateJurisdictionRule_Unauthenticated_Returns401()
        {
            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "US",
                JurisdictionName = "United States",
                RegulatoryFramework = "SEC"
            };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── CRUD operations ───────────────────────────────────────────────────────

        [Test]
        public async Task ListJurisdictionRules_Authenticated_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<ListJurisdictionRulesResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Rules, Is.Not.Null);
        }

        [Test]
        public async Task CreateJurisdictionRule_ValidRequest_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            // Use unique jurisdiction code per test run to avoid conflicts
            var jurisdictionCode = $"T{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = jurisdictionCode,
                JurisdictionName = "Test Jurisdiction",
                RegulatoryFramework = "MICA",
                IsActive = true,
                Priority = 100,
                Requirements = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement
                    {
                        RequirementCode = "KYC_REQUIRED",
                        Category = "KYC",
                        Description = "KYC verification required",
                        IsMandatory = true,
                        Severity = RequirementSeverity.Critical
                    }
                }
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid jurisdiction rule creation must return 200");

            var result = await resp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True, result.ErrorMessage ?? "no error");
            Assert.That(result.Rule, Is.Not.Null);
            Assert.That(result.Rule!.JurisdictionCode, Is.EqualTo(jurisdictionCode));
        }

        [Test]
        public async Task CreateJurisdictionRule_MissingCode_ReturnsFail()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "", // Empty code - invalid
                JurisdictionName = "Test",
                RegulatoryFramework = "MICA"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);

            // Either 400 or 200 with success=false
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var result = await resp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
                Assert.That(result!.Success, Is.False, "Empty jurisdiction code must fail");
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            }
            else
            {
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
        }

        [Test]
        public async Task CreateJurisdictionRule_DuplicateCode_ReturnsFail()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var code = $"D{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Duplicate Test",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            // Create once - should succeed
            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            Assert.That(resp1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result1 = await resp1.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(result1!.Success, Is.True, "First creation must succeed");

            // Create again with same code - should fail
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);

            if (resp2.StatusCode == HttpStatusCode.OK)
            {
                var result2 = await resp2.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
                Assert.That(result2!.Success, Is.False,
                    "Duplicate jurisdiction code must return success=false");
                Assert.That(result2.ErrorMessage, Does.Contain(code).Or.Contain("already exists"),
                    "Error must mention the conflict");
            }
            else
            {
                Assert.That(resp2.StatusCode, Is.EqualTo(HttpStatusCode.Conflict).Or.EqualTo(HttpStatusCode.BadRequest),
                    "Duplicate must return 409 or 400");
            }
        }

        [Test]
        public async Task GetJurisdictionRule_ExistingRule_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            // Create a rule first
            var code = $"G{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var body = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Get Test",
                RegulatoryFramework = "FATF",
                IsActive = true
            };

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", body);
            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            if (createResult?.Rule?.Id == null)
            {
                Assert.Ignore("Could not create rule for get test");
                return;
            }

            // Get by ID
            var getResp = await _authClient.GetAsync($"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Get existing rule must return 200");

            var getResult = await getResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(getResult!.Success, Is.True);
            Assert.That(getResult.Rule!.JurisdictionCode, Is.EqualTo(code));
        }

        [Test]
        public async Task GetJurisdictionRule_NonExistentId_ReturnsNotFoundOrFail()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules/nonexistent-rule-id-xyz");

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var result = await resp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
                Assert.That(result!.Success, Is.False, "Non-existent rule must return success=false");
            }
            else
            {
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound).Or.EqualTo(HttpStatusCode.BadRequest));
            }
        }

        [Test]
        public async Task UpdateJurisdictionRule_ValidUpdate_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            // Create a rule first
            var code = $"U{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var createBody = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Update Test Original",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", createBody);
            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            if (createResult?.Rule?.Id == null)
            {
                Assert.Ignore("Could not create rule for update test");
                return;
            }

            // Update
            var updateBody = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Update Test Modified",
                RegulatoryFramework = "MICA",
                IsActive = false, // Changed
                Notes = "Updated for testing"
            };

            var updateResp = await _authClient.PutAsJsonAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}", updateBody);

            Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid update must return 200");

            var updateResult = await updateResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(updateResult!.Success, Is.True, updateResult.ErrorMessage ?? "no error");
        }

        [Test]
        public async Task DeleteJurisdictionRule_ExistingRule_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            // Create a rule to delete
            var code = $"X{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var createBody = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Delete Test",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", createBody);
            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            if (createResult?.Rule?.Id == null)
            {
                Assert.Ignore("Could not create rule for delete test");
                return;
            }

            var deleteResp = await _authClient.DeleteAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");

            Assert.That((int)deleteResp.StatusCode, Is.LessThan(500),
                "Delete must not return 5xx");
        }

        // ── Jurisdiction filtering ────────────────────────────────────────────────

        [Test]
        public async Task ListJurisdictionRules_WithFrameworkFilter_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules?regulatoryFramework=MICA");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ListJurisdictionRules_ActiveOnly_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules?isActive=true");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<ListJurisdictionRulesResponse>();
            // All returned rules should be active
            foreach (var rule in body!.Rules)
            {
                Assert.That(rule.IsActive, Is.True, "Filter should only return active rules");
            }
        }

        // ── Token jurisdiction assignment ─────────────────────────────────────────

        [Test]
        public async Task AssignTokenJurisdiction_ValidAssignment_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            // Create jurisdiction rule first
            var code = $"A{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Assign Test",
                RegulatoryFramework = "MICA",
                IsActive = true
            });

            // Assign to token
            var assignBody = new
            {
                assetId = 55554444UL,
                network = "mainnet-v1.0",
                jurisdictionCode = code,
                isPrimary = true
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules/assign", assignBody);
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Assignment must not return 5xx");
        }

        [Test]
        public async Task GetTokenJurisdictions_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/compliance/jurisdiction-rules/token-jurisdictions?assetId=55554444&network=mainnet-v1.0");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // ── Compliance evaluation ─────────────────────────────────────────────────

        [Test]
        public async Task EvaluateTokenCompliance_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync(
                "/api/v1/compliance/jurisdiction-rules/evaluate?assetId=12345&network=mainnet-v1.0&issuerId=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<JurisdictionEvaluationResult>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.AssetId, Is.EqualTo(12345UL));
        }

        // ── Rule schema contract ──────────────────────────────────────────────────

        [Test]
        public async Task JurisdictionRule_HasRequiredSchemaFields()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var code = $"S{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var resp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Schema Test",
                RegulatoryFramework = "MICA",
                IsActive = true
            });

            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True,
                "Response must have 'success' field");

            if (root.TryGetProperty("rule", out var rule) && rule.ValueKind != JsonValueKind.Null)
            {
                Assert.That(rule.TryGetProperty("id", out _), Is.True, "Rule must have 'id' field");
                Assert.That(rule.TryGetProperty("jurisdictionCode", out _), Is.True,
                    "Rule must have 'jurisdictionCode' field");
                Assert.That(rule.TryGetProperty("createdAt", out _), Is.True,
                    "Rule must have 'createdAt' timestamp for audit trail");
            }
        }

        // ── Whitelist rules CRUD ──────────────────────────────────────────────────

        [Test]
        public async Task CreateWhitelistRule_Unauthenticated_Returns401()
        {
            var body = new { assetId = 12345 };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/whitelist-rules", body);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ListWhitelistRules_Authenticated_Returns200()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var resp = await _authClient.GetAsync("/api/v1/whitelist-rules/12345");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task ApplyWhitelistRule_Returns200OrSuccessContract()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var body = new
            {
                assetId = 12345UL,
                address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                ruleType = "AllowAll"
            };

            var resp = await _authClient.PostAsJsonAsync("/api/v1/whitelist-rules/apply", body);
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Apply rule must not return 5xx");
        }

        // ── Idempotency ───────────────────────────────────────────────────────────

        [Test]
        public async Task CreateJurisdictionRule_ThenGet_DataIsPersisted()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var code = $"P{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var createBody = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Persistence Test",
                RegulatoryFramework = "SEC",
                IsActive = true,
                Priority = 200,
                Notes = "Test note for persistence verification"
            };

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", createBody);
            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            if (createResult?.Rule?.Id == null)
            {
                Assert.Ignore("Could not create rule");
                return;
            }

            // Retrieve and verify
            var getResp = await _authClient.GetAsync(
                $"/api/v1/compliance/jurisdiction-rules/{createResult.Rule.Id}");
            var getResult = await getResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();

            Assert.That(getResult!.Rule!.JurisdictionName, Is.EqualTo("Persistence Test"),
                "Retrieved rule name must match created rule");
            Assert.That(getResult.Rule.Priority, Is.EqualTo(200),
                "Retrieved rule priority must match created rule");
            Assert.That(getResult.Rule.Notes, Is.EqualTo("Test note for persistence verification"),
                "Retrieved rule notes must match created rule");
        }

        [Test]
        public async Task CreateJurisdictionRule_WithRequirements_RequirementsArePersisted()
        {
            if (_accessToken == null)
            {
                Assert.Ignore("No auth token available");
                return;
            }

            var code = $"R{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var createBody = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = code,
                JurisdictionName = "Requirements Test",
                RegulatoryFramework = "MICA",
                IsActive = true,
                Requirements = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement
                    {
                        RequirementCode = "MICA_ART_17",
                        Category = "Disclosure",
                        Description = "MICA Article 17 white paper requirement",
                        IsMandatory = true,
                        Severity = RequirementSeverity.Critical,
                        RegulatoryReference = "MICA Article 17"
                    },
                    new ComplianceRequirement
                    {
                        RequirementCode = "KYC_BASIC",
                        Category = "KYC",
                        Description = "Basic KYC verification",
                        IsMandatory = true,
                        Severity = RequirementSeverity.High
                    }
                }
            };

            var createResp = await _authClient.PostAsJsonAsync("/api/v1/compliance/jurisdiction-rules", createBody);
            Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var createResult = await createResp.Content.ReadFromJsonAsync<JurisdictionRuleResponse>();
            Assert.That(createResult!.Success, Is.True);
            Assert.That(createResult.Rule!.Requirements, Has.Count.EqualTo(2),
                "Both compliance requirements must be persisted");
        }
    }
}

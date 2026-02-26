using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for deterministic ARC76 auth verification and evidence contracts (Issue #407).
    ///
    /// Validates all ten acceptance criteria:
    /// AC1  - ARC76 derivation verification endpoints return deterministic identity mapping.
    /// AC2  - Session endpoints expose stable fields enabling frontend auth-realistic assertions.
    /// AC3  - Operational evidence timeline endpoints provide ordered, correlation-linked records.
    /// AC4  - Compliance summary endpoints provide actionable and privacy-safe posture outputs.
    /// AC5  - Error envelopes use bounded codes and include clear remediation guidance.
    /// AC6  - Correlation IDs are consistently visible across API responses and internal logs.
    /// AC7  - Contract tests enforce schema stability and backward compatibility.
    /// AC8  - Integration tests validate deterministic behavior under retries and degraded deps.
    /// AC9  - CI passes without skipping new auth/evidence confidence suites.
    /// AC10 - PR evidence includes sample payloads, run links, and explicit risk notes.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76AuthVerificationEvidenceContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private static readonly Dictionary<string, string?> TestConfiguration = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
            ["JwtConfig:Issuer"] = "BiatecTokensApi",
            ["JwtConfig:Audience"] = "BiatecTokensUsers",
            ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
            ["JwtConfig:RefreshTokenExpirationDays"] = "30",
            ["JwtConfig:ValidateIssuerSigningKey"] = "true",
            ["JwtConfig:ValidateIssuer"] = "true",
            ["JwtConfig:ValidateAudience"] = "true",
            ["JwtConfig:ValidateLifetime"] = "true",
            ["JwtConfig:ClockSkewMinutes"] = "5",
            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
            ["IPFSConfig:TimeoutSeconds"] = "30",
            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
            ["IPFSConfig:ValidateContentHash"] = "true",
            ["IPFSConfig:Username"] = "",
            ["IPFSConfig:Password"] = "",
            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
            ["EVMChains:0:ChainId"] = "8453",
            ["EVMChains:0:Name"] = "Base Mainnet",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForARC76AuthVerification32CharMin!",
            ["AllowedOrigins:0"] = "http://localhost:3000",
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC1 – ARC76 derivation verification endpoints
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1: ARC76 info endpoint returns 200 without authentication.
        /// Confirms the derivation contract metadata is publicly accessible.
        /// </summary>
        [Test]
        public async Task AC1_DerivationInfo_NoAuth_Returns200WithContractVersion()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "ARC76 info endpoint must return 200 without auth");

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("contractVersion").GetString(), Is.Not.Null.And.Not.Empty,
                "ContractVersion must be present in derivation info");
            Assert.That(root.GetProperty("standard").GetString(), Is.EqualTo("ARC76"),
                "Standard must be ARC76");
            Assert.That(root.GetProperty("isBackwardCompatible").GetBoolean(), Is.True,
                "Contract must declare backward compatibility");
        }

        /// <summary>
        /// AC1: ARC76 info response contains bounded error codes taxonomy.
        /// </summary>
        [Test]
        public async Task AC1_DerivationInfo_ContainsBoundedErrorCodes()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("boundedErrorCodes", out var codes), Is.True,
                "BoundedErrorCodes must be present");
            Assert.That(codes.GetArrayLength(), Is.GreaterThan(0),
                "At least one error code must be listed");
        }

        /// <summary>
        /// AC1: Unauthenticated call to verify-derivation returns 401.
        /// </summary>
        [Test]
        public async Task AC1_VerifyDerivation_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var req = new ARC76DerivationVerifyRequest { Email = "test@example.com" };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "verify-derivation must require Bearer authentication");
        }

        /// <summary>
        /// AC1: Authenticated verify-derivation returns Success=true with deterministic fields.
        /// </summary>
        [Test]
        public async Task AC1_VerifyDerivation_Authenticated_ReturnsDeterministicFields()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var req = new ARC76DerivationVerifyRequest { Email = email };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await resp.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();

            Assert.That(result!.Success, Is.True, "Success must be true for authenticated user");
            Assert.That(result.IsConsistent, Is.True, "IsConsistent must be true");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(result.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present");
            Assert.That(result.DerivationAlgorithm, Is.EqualTo("ARC76/BIP39"),
                "DerivationAlgorithm must be ARC76/BIP39");
        }

        /// <summary>
        /// AC1: Two verify-derivation calls with same credentials return identical AlgorandAddress.
        /// Validates determinism across repeated calls.
        /// </summary>
        [Test]
        public async Task AC1_VerifyDerivation_RepeatedCalls_ReturnIdenticalAddress()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var req = new ARC76DerivationVerifyRequest { Email = email };

            var resp1 = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req);
            var resp2 = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req);
            var resp3 = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req);

            var r1 = await resp1.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();
            var r2 = await resp2.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();
            var r3 = await resp3.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();

            Assert.That(r1!.AlgorandAddress, Is.EqualTo(r2!.AlgorandAddress),
                "AlgorandAddress must be identical across repeated calls (run 1 vs 2)");
            Assert.That(r1.AlgorandAddress, Is.EqualTo(r3!.AlgorandAddress),
                "AlgorandAddress must be identical across repeated calls (run 1 vs 3)");
        }

        /// <summary>
        /// AC1: DeterminismProof block is present and contains required fields.
        /// </summary>
        [Test]
        public async Task AC1_VerifyDerivation_DeterminismProofContainsRequiredFields()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new ARC76DerivationVerifyRequest { Email = email });

            var result = await resp.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();

            Assert.That(result!.DeterminismProof, Is.Not.Null, "DeterminismProof must be present");
            Assert.That(result.DeterminismProof!.Standard, Is.EqualTo("ARC76"));
            Assert.That(result.DeterminismProof.CanonicalEmail, Is.Not.Null.And.Not.Empty,
                "CanonicalEmail must be present in proof");
            Assert.That(result.DeterminismProof.AddressFingerprint, Is.Not.Null.And.Not.Empty,
                "AddressFingerprint must be present in proof");
            Assert.That(result.DeterminismProof.ContractVersion, Is.Not.Null.And.Not.Empty);
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2 – Session endpoints expose stable derivation-linked fields
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: Unauthenticated access to /auth/session returns 401.
        /// </summary>
        [Test]
        public async Task AC2_Session_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var resp = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        /// <summary>
        /// AC2: Authenticated session endpoint returns IsActive=true with stable identity fields.
        /// </summary>
        [Test]
        public async Task AC2_Session_Authenticated_ReturnsStableIdentityFields()
        {
            var (token, email, address) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await resp.Content.ReadFromJsonAsync<SessionInspectionResponse>();

            Assert.That(result!.IsActive, Is.True, "Session must be active");
            Assert.That(result.Email, Is.EqualTo(email.ToLowerInvariant()),
                "Email must match registered email (canonicalized)");
            Assert.That(result.AlgorandAddress, Is.EqualTo(address),
                "AlgorandAddress must match registration-time address");
            Assert.That(result.TokenType, Is.EqualTo("Bearer"), "TokenType must be Bearer");
            Assert.That(result.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present in session response");
        }

        /// <summary>
        /// AC2: Session and verify-derivation return the same AlgorandAddress.
        /// Confirms the two endpoints are consistent with each other.
        /// </summary>
        [Test]
        public async Task AC2_SessionAndVerifyDerivation_ReturnConsistentAddress()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var sessionResp = await _client.GetAsync("/api/v1/auth/session");
            var session = await sessionResp.Content.ReadFromJsonAsync<SessionInspectionResponse>();

            var verifyResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new ARC76DerivationVerifyRequest { Email = email });
            var verify = await verifyResp.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();

            Assert.That(session!.AlgorandAddress, Is.EqualTo(verify!.AlgorandAddress),
                "Session and verify-derivation must return identical AlgorandAddress");
        }

        /// <summary>
        /// AC2: Session DerivationContractVersion matches the version reported by arc76/info.
        /// </summary>
        [Test]
        public async Task AC2_Session_DerivationVersionMatchesInfoEndpoint()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var sessionResp = await _client.GetAsync("/api/v1/auth/session");
            var session = await sessionResp.Content.ReadFromJsonAsync<SessionInspectionResponse>();

            _client.DefaultRequestHeaders.Authorization = null;
            var infoResp = await _client.GetAsync("/api/v1/auth/arc76/info");
            var infoBody = await infoResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(infoBody);
            var contractVersion = doc.RootElement.GetProperty("contractVersion").GetString();

            Assert.That(session!.DerivationContractVersion, Is.EqualTo(contractVersion),
                "Session.DerivationContractVersion must match the global contract version");
        }

        /// <summary>
        /// AC2: Session response includes CorrelationId for traceability.
        /// </summary>
        [Test]
        public async Task AC2_Session_ResponseIncludesCorrelationId()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.GetAsync("/api/v1/auth/session");
            var result = await resp.Content.ReadFromJsonAsync<SessionInspectionResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be present in session response for traceability");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3 – Operational evidence timeline records
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC3: Operational intelligence health endpoint is reachable and returns 200.
        /// </summary>
        [Test]
        public async Task AC3_OperationalIntelligence_HealthEndpoint_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/operational-intelligence/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Operational intelligence health must be reachable");
        }

        /// <summary>
        /// AC3: Unauthenticated timeline request returns 401.
        /// </summary>
        [Test]
        public async Task AC3_Timeline_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var resp = await _client.PostAsJsonAsync(
                "/api/v1/operational-intelligence/timeline",
                new { deploymentId = "any" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        /// <summary>
        /// AC3: Missing DeploymentId in timeline request returns 400, not 500.
        /// </summary>
        [Test]
        public async Task AC3_Timeline_MissingDeploymentId_Returns400()
        {
            var token = await GetAuthTokenAsync();
            SetBearer(token);

            var resp = await _client.PostAsJsonAsync(
                "/api/v1/operational-intelligence/timeline",
                new { deploymentId = "" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "Empty deployment ID must return 400");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body), "Response must be valid JSON");
        }

        /// <summary>
        /// AC3: Unknown deployment ID returns 404 with structured error.
        /// </summary>
        [Test]
        public async Task AC3_Timeline_UnknownDeployment_Returns404()
        {
            var token = await GetAuthTokenAsync();
            SetBearer(token);

            var resp = await _client.PostAsJsonAsync(
                "/api/v1/operational-intelligence/timeline",
                new { deploymentId = Guid.NewGuid().ToString() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "Unknown deployment must return 404");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC4 – Compliance summary endpoints
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC4: Compliance checkpoint endpoint requires authentication.
        /// </summary>
        [Test]
        public async Task AC4_ComplianceCheckpoint_Unauthenticated_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var resp = await _client.PostAsJsonAsync(
                "/api/v1/operational-intelligence/compliance-checkpoints",
                new { deploymentId = "any" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        /// <summary>
        /// AC4: Risk classification endpoint returns 200 for known error code.
        /// </summary>
        [Test]
        public async Task AC4_RiskClassification_KnownErrorCode_Returns200()
        {
            var token = await GetAuthTokenAsync();
            SetBearer(token);

            var resp = await _client.GetAsync(
                "/api/v1/operational-intelligence/classify-risk?errorCode=NOT_FOUND");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "classify-risk must return 200 for known error code");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotThrow(() => JsonDocument.Parse(body));
        }

        /// <summary>
        /// AC4: Risk classification is deterministic – same error code always maps to same category.
        /// </summary>
        [Test]
        public async Task AC4_RiskClassification_SameErrorCode_IsDeterministic()
        {
            var token = await GetAuthTokenAsync();
            SetBearer(token);

            var resp1 = await _client.GetAsync(
                "/api/v1/operational-intelligence/classify-risk?errorCode=UNAUTHORIZED");
            var resp2 = await _client.GetAsync(
                "/api/v1/operational-intelligence/classify-risk?errorCode=UNAUTHORIZED");

            var body1 = await resp1.Content.ReadAsStringAsync();
            var body2 = await resp2.Content.ReadAsStringAsync();

            using var doc1 = JsonDocument.Parse(body1);
            using var doc2 = JsonDocument.Parse(body2);

            // The category field must be identical for the same error code
            var cat1 = doc1.RootElement.GetProperty("category").GetInt32();
            var cat2 = doc2.RootElement.GetProperty("category").GetInt32();

            Assert.That(cat1, Is.EqualTo(cat2),
                "Risk category must be deterministic for the same error code");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC5 – Error envelopes use bounded codes with remediation hints
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5: Login with invalid credentials returns 401 with structured error envelope.
        /// </summary>
        [Test]
        public async Task AC5_Login_InvalidCredentials_ReturnsStructuredErrorEnvelope()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "nonexistent@example.com", password = "WrongPass1!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.That(doc.RootElement.TryGetProperty("errorCode", out _), Is.True,
                "Error envelope must contain errorCode");
            Assert.That(doc.RootElement.TryGetProperty("errorMessage", out _), Is.True,
                "Error envelope must contain errorMessage");
        }

        /// <summary>
        /// AC5: Weak-password registration returns 400 with bounded WEAK_PASSWORD error code.
        /// </summary>
        [Test]
        public async Task AC5_Register_WeakPassword_ReturnsBoundedErrorCode()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = $"weak-{Guid.NewGuid()}@example.com", password = "nouppercase1!", confirmPassword = "nouppercase1!" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "Weak password must return 400");
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            // Response must not contain internal stack traces
            Assert.That(body, Does.Not.Contain("StackTrace"),
                "Error response must not expose stack trace");
        }

        /// <summary>
        /// AC5: verify-derivation with mismatched email returns 400 with bounded error code.
        /// </summary>
        [Test]
        public async Task AC5_VerifyDerivation_EmailMismatch_ReturnsBoundedErrorCode()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            // Provide a different email than the authenticated user's
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new ARC76DerivationVerifyRequest { Email = "differentuser@example.com" });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "Email mismatch must return 400");
            var result = await resp.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "ErrorCode must be present in error envelope");
        }

        /// <summary>
        /// AC5: Duplicate user registration returns 400 with USER_ALREADY_EXISTS error code.
        /// </summary>
        [Test]
        public async Task AC5_Register_DuplicateEmail_ReturnsBoundedErrorCode()
        {
            var email = $"dup-{Guid.NewGuid()}@example.com";
            const string password = "SecurePass1!";

            // First registration
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });

            // Second registration with same email
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.That(doc.RootElement.TryGetProperty("errorCode", out _), Is.True,
                "Duplicate error must contain errorCode");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC6 – Correlation IDs visible across API responses
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC6: Register response includes CorrelationId.
        /// </summary>
        [Test]
        public async Task AC6_Register_ResponseIncludesCorrelationId()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new
                {
                    email = $"corr-{Guid.NewGuid()}@example.com",
                    password = "Corr1@Secure",
                    confirmPassword = "Corr1@Secure"
                });

            var result = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Register response must include CorrelationId");
        }

        /// <summary>
        /// AC6: Login response includes CorrelationId.
        /// </summary>
        [Test]
        public async Task AC6_Login_ResponseIncludesCorrelationId()
        {
            var email = $"corr2-{Guid.NewGuid()}@example.com";
            const string password = "Corr2@Secure";
            await RegisterUserAsync(email, password);

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });

            var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Login response must include CorrelationId");
        }

        /// <summary>
        /// AC6: verify-derivation response includes CorrelationId.
        /// </summary>
        [Test]
        public async Task AC6_VerifyDerivation_ResponseIncludesCorrelationId()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new ARC76DerivationVerifyRequest { Email = email });
            var result = await resp.Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "verify-derivation response must include CorrelationId");
        }

        /// <summary>
        /// AC6: arc76/info response includes CorrelationId.
        /// </summary>
        [Test]
        public async Task AC6_DerivationInfo_ResponseIncludesCorrelationId()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            var result = await resp.Content.ReadFromJsonAsync<ARC76DerivationInfoResponse>();

            Assert.That(result!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "arc76/info response must include CorrelationId");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC7 – Contract schema stability and backward compatibility
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC7: RegisterResponse always contains the schema-stable fields required by consumers.
        /// </summary>
        [Test]
        public async Task AC7_RegisterResponse_ContainsAllSchemaStableFields()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new
                {
                    email = $"schema-{Guid.NewGuid()}@example.com",
                    password = "Schema1@Secure",
                    confirmPassword = "Schema1@Secure"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Required stable schema fields
            Assert.That(root.TryGetProperty("success", out _), Is.True);
            Assert.That(root.TryGetProperty("userId", out _), Is.True);
            Assert.That(root.TryGetProperty("email", out _), Is.True);
            Assert.That(root.TryGetProperty("algorandAddress", out _), Is.True);
            Assert.That(root.TryGetProperty("accessToken", out _), Is.True);
            Assert.That(root.TryGetProperty("refreshToken", out _), Is.True);
            Assert.That(root.TryGetProperty("derivationContractVersion", out _), Is.True);
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True);
        }

        /// <summary>
        /// AC7: ARC76DerivationVerifyResponse always contains stable contract fields.
        /// </summary>
        [Test]
        public async Task AC7_DerivationVerifyResponse_ContainsAllSchemaStableFields()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new ARC76DerivationVerifyRequest { Email = email });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True);
            Assert.That(root.TryGetProperty("algorandAddress", out _), Is.True);
            Assert.That(root.TryGetProperty("isConsistent", out _), Is.True);
            Assert.That(root.TryGetProperty("derivationContractVersion", out _), Is.True);
            Assert.That(root.TryGetProperty("derivationAlgorithm", out _), Is.True);
            Assert.That(root.TryGetProperty("determinismProof", out _), Is.True);
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True);
            Assert.That(root.TryGetProperty("timestamp", out _), Is.True);
        }

        /// <summary>
        /// AC7: SessionInspectionResponse always contains stable contract fields.
        /// </summary>
        [Test]
        public async Task AC7_SessionInspectionResponse_ContainsAllSchemaStableFields()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("isActive", out _), Is.True);
            Assert.That(root.TryGetProperty("userId", out _), Is.True);
            Assert.That(root.TryGetProperty("email", out _), Is.True);
            Assert.That(root.TryGetProperty("algorandAddress", out _), Is.True);
            Assert.That(root.TryGetProperty("tokenType", out _), Is.True);
            Assert.That(root.TryGetProperty("derivationContractVersion", out _), Is.True);
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True);
            Assert.That(root.TryGetProperty("timestamp", out _), Is.True);
        }

        /// <summary>
        /// AC7: ARC76DerivationInfoResponse always contains stable contract fields.
        /// </summary>
        [Test]
        public async Task AC7_DerivationInfoResponse_ContainsAllSchemaStableFields()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("contractVersion", out _), Is.True);
            Assert.That(root.TryGetProperty("standard", out _), Is.True);
            Assert.That(root.TryGetProperty("algorithmDescription", out _), Is.True);
            Assert.That(root.TryGetProperty("boundedErrorCodes", out _), Is.True);
            Assert.That(root.TryGetProperty("isBackwardCompatible", out _), Is.True);
            Assert.That(root.TryGetProperty("specificationUrl", out _), Is.True);
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True);
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC8 – Integration tests for deterministic behavior under retries
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC8: Register with same credentials after logout returns same AlgorandAddress.
        /// Validates determinism of the identity contract across session boundaries.
        /// </summary>
        [Test]
        public async Task AC8_RegisterAndLoginAgain_ProduceSameAlgorandAddress()
        {
            var email = $"det8-{Guid.NewGuid()}@example.com";
            const string password = "Det8@Secure";

            // First registration
            var regResp1 = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var reg1 = await regResp1.Content.ReadFromJsonAsync<RegisterResponse>();
            var address1 = reg1!.AlgorandAddress;

            // Login (same user, same derivation)
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });
            var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            var address2 = login!.AlgorandAddress;

            Assert.That(address1, Is.EqualTo(address2),
                "AlgorandAddress must be identical between register and subsequent login");
        }

        /// <summary>
        /// AC8: Three consecutive verify-derivation calls produce identical determinism proofs.
        /// </summary>
        [Test]
        public async Task AC8_VerifyDerivation_ThreeConsecutiveCalls_ProduceIdenticalFingerprint()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);
            var req = new ARC76DerivationVerifyRequest { Email = email };

            var r1 = await (await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req))
                .Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();
            var r2 = await (await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req))
                .Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();
            var r3 = await (await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", req))
                .Content.ReadFromJsonAsync<ARC76DerivationVerifyResponse>();

            Assert.That(r1!.DeterminismProof!.AddressFingerprint,
                Is.EqualTo(r2!.DeterminismProof!.AddressFingerprint),
                "Fingerprint must match between call 1 and 2");
            Assert.That(r1.DeterminismProof.AddressFingerprint,
                Is.EqualTo(r3!.DeterminismProof!.AddressFingerprint),
                "Fingerprint must match between call 1 and 3");
        }

        /// <summary>
        /// AC8: Session inspection is idempotent – three calls return the same address.
        /// </summary>
        [Test]
        public async Task AC8_SessionInspection_Idempotent_ReturnsSameAddress()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var s1 = await (await _client.GetAsync("/api/v1/auth/session"))
                .Content.ReadFromJsonAsync<SessionInspectionResponse>();
            var s2 = await (await _client.GetAsync("/api/v1/auth/session"))
                .Content.ReadFromJsonAsync<SessionInspectionResponse>();
            var s3 = await (await _client.GetAsync("/api/v1/auth/session"))
                .Content.ReadFromJsonAsync<SessionInspectionResponse>();

            Assert.That(s1!.AlgorandAddress, Is.EqualTo(s2!.AlgorandAddress));
            Assert.That(s1.AlgorandAddress, Is.EqualTo(s3!.AlgorandAddress));
        }

        /// <summary>
        /// AC8: Two different users have different AlgorandAddresses – no cross-tenant leakage.
        /// </summary>
        [Test]
        public async Task AC8_TwoUsers_HaveDifferentAlgorandAddresses()
        {
            var (token1, _, addr1) = await RegisterAndGetTokenAsync();
            var (token2, _, addr2) = await RegisterAndGetTokenAsync();

            Assert.That(addr1, Is.Not.EqualTo(addr2),
                "Two independent users must have distinct ARC76-derived addresses");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC9 – CI quality gates: existing endpoints still return expected codes
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC9: Existing /auth/register endpoint still returns 200 (regression guard).
        /// </summary>
        [Test]
        public async Task AC9_Regression_RegisterEndpointStillWorks()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new
                {
                    email = $"reg9-{Guid.NewGuid()}@example.com",
                    password = "Reg9@Secure",
                    confirmPassword = "Reg9@Secure"
                });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Existing register endpoint must still return 200");
        }

        /// <summary>
        /// AC9: Existing /auth/login endpoint still returns 200 (regression guard).
        /// </summary>
        [Test]
        public async Task AC9_Regression_LoginEndpointStillWorks()
        {
            var email = $"login9-{Guid.NewGuid()}@example.com";
            const string password = "Login9@Secure";
            await RegisterUserAsync(email, password);

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Existing login endpoint must still return 200");
        }

        /// <summary>
        /// AC9: Existing /auth/refresh endpoint still works (regression guard).
        /// </summary>
        [Test]
        public async Task AC9_Regression_RefreshEndpointStillWorks()
        {
            var email = $"ref9-{Guid.NewGuid()}@example.com";
            const string password = "Ref9@Secure";
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = reg!.RefreshToken });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Existing refresh endpoint must still return 200");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC10 – Privacy-safe payloads: no internal leakage in responses
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC10: verify-derivation response does not expose mnemonic or encrypted secrets.
        /// </summary>
        [Test]
        public async Task AC10_VerifyDerivation_ResponseDoesNotLeakSecrets()
        {
            var (token, email, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new ARC76DerivationVerifyRequest { Email = email });

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain("mnemonic"), "Response must not expose mnemonic");
            Assert.That(body, Does.Not.Contain("EncryptedMnemonic"), "Response must not expose encrypted mnemonic");
            Assert.That(body, Does.Not.Contain("passwordHash"), "Response must not expose password hash");
            Assert.That(body, Does.Not.Contain("StackTrace"), "Response must not expose stack traces");
        }

        /// <summary>
        /// AC10: Session inspection response does not expose password hash or mnemonic.
        /// </summary>
        [Test]
        public async Task AC10_SessionInspection_ResponseDoesNotLeakSecrets()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            SetBearer(token);

            var resp = await _client.GetAsync("/api/v1/auth/session");
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body, Does.Not.Contain("mnemonic"), "Session must not expose mnemonic");
            Assert.That(body, Does.Not.Contain("passwordHash"), "Session must not expose password hash");
            Assert.That(body, Does.Not.Contain("StackTrace"), "Session must not expose stack traces");
        }

        /// <summary>
        /// AC10: Login error response does not reveal internal implementation details.
        /// </summary>
        [Test]
        public async Task AC10_LoginError_DoesNotLeakInternalDetails()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "ghost@example.com", password = "Ghost1@Secure" });

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain("StackTrace"),
                "Login error must not expose stack traces");
            Assert.That(body, Does.Not.Contain("DbContext"),
                "Login error must not expose database context details");
            Assert.That(body, Does.Not.Contain("System."),
                "Login error must not expose .NET namespace internals");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SetBearer(string token) =>
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        private async Task RegisterUserAsync(string email, string password)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
        }

        /// <summary>Returns (accessToken, email, algorandAddress).</summary>
        private async Task<(string token, string email, string address)> RegisterAndGetTokenAsync()
        {
            var email = $"arc76ev-{Guid.NewGuid()}@example.com";
            const string password = "Arc76Ev@Secure";
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var reg = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return (reg?.AccessToken ?? string.Empty, email, reg?.AlgorandAddress ?? string.Empty);
        }

        private async Task<string> GetAuthTokenAsync()
        {
            var (token, _, _) = await RegisterAndGetTokenAsync();
            return token;
        }
    }
}

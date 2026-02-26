using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Pure service-layer unit tests for Issue #409:
    /// MVP – Deliver deterministic ARC76 auth-derived backend issuance orchestration.
    ///
    /// These tests use only mocked or in-memory dependencies (no HTTP, no WebApplicationFactory)
    /// and cover the core orchestration service methods with determinism, error handling,
    /// state machine enforcement, and audit trail guarantees.
    ///
    /// Coverage (per PO requirements):
    /// - DeploymentStatusService: state machine transitions, idempotency, failure recording
    /// - AuthenticationService: derivation determinism, error propagation, no secret leakage
    /// - Authorization: ownership guards, correlation ID threading
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76IssuanceOrchestrationServiceUnitTests
    {
        // ── AuthenticationService setup ───────────────────────────────────────

        private Mock<IUserRepository> _mockUserRepo = null!;
        private Mock<ILogger<AuthenticationService>> _mockAuthLogger = null!;
        private AuthenticationService _authService = null!;

        // ── DeploymentStatusService setup ─────────────────────────────────────

        private DeploymentStatusService _deploymentService = null!;
        private Mock<IWebhookService> _mockWebhook = null!;

        private const string TestJwtSecret = "Issuance409UnitTestSecretKeyHs256Min32Chars!";
        private const string TestEncryptionKey = "Issuance409UnitTestEncryptionKeyMin32Chars!!";

        [SetUp]
        public void Setup()
        {
            // AuthenticationService dependencies
            _mockUserRepo = new Mock<IUserRepository>();
            _mockAuthLogger = new Mock<ILogger<AuthenticationService>>();

            var jwtConfig = new JwtConfig
            {
                SecretKey = TestJwtSecret,
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensUsers",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30
            };

            var keyMgmtConfig = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = TestEncryptionKey
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(cfg =>
            {
                cfg.Provider = "Hardcoded";
                cfg.HardcodedKey = TestEncryptionKey;
            });
            services.AddSingleton<HardcodedKeyProvider>();
            var sp = services.BuildServiceProvider();

            var keyProviderFactory = new KeyProviderFactory(
                sp,
                Options.Create(keyMgmtConfig),
                new Mock<ILogger<KeyProviderFactory>>().Object);

            _authService = new AuthenticationService(
                _mockUserRepo.Object,
                _mockAuthLogger.Object,
                Options.Create(jwtConfig),
                keyProviderFactory);

            // DeploymentStatusService dependencies
            var repoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var deployRepo = new DeploymentStatusRepository(repoLogger.Object);
            _mockWebhook = new Mock<IWebhookService>();
            var svcLogger = new Mock<ILogger<DeploymentStatusService>>();
            _deploymentService = new DeploymentStatusService(deployRepo, _mockWebhook.Object, svcLogger.Object);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DeploymentStatusService – State machine
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// CreateDeploymentAsync assigns a unique non-empty DeploymentId and sets
        /// the initial status to Queued.
        /// </summary>
        [Test]
        public async Task CreateDeploymentAsync_AssignsUniqueId_AndSetsQueuedStatus()
        {
            var id = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "owner@unit.test",
                "UnitToken", "UT", "corr-create");

            Assert.That(id, Is.Not.Null.And.Not.Empty,
                "CreateDeploymentAsync must return a non-empty deployment ID");

            var deployment = await _deploymentService.GetDeploymentAsync(id);
            Assert.That(deployment, Is.Not.Null, "Created deployment must be retrievable");
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "Initial deployment status must be Queued");
        }

        /// <summary>
        /// CreateDeploymentAsync produces an initial status history entry for audit compliance.
        /// The first entry must record Queued status with a timestamp and message.
        /// </summary>
        [Test]
        public async Task CreateDeploymentAsync_ProducesInitialAuditHistoryEntry()
        {
            var id = await _deploymentService.CreateDeploymentAsync(
                "ARC3", "algorand-mainnet", "audit@unit.test",
                "AuditToken", "AUD", "corr-audit");

            var history = await _deploymentService.GetStatusHistoryAsync(id);

            Assert.That(history, Is.Not.Null.And.Not.Empty,
                "Status history must not be empty after creation");
            Assert.That(history[0].Status, Is.EqualTo(DeploymentStatus.Queued),
                "First history entry must record Queued status");
            Assert.That(history[0].Timestamp, Is.Not.EqualTo(default(DateTime)),
                "First history entry must include timestamp for audit compliance");
            Assert.That(history[0].Message, Is.Not.Null.And.Not.Empty,
                "First history entry must include descriptive message");
        }

        /// <summary>
        /// Two calls to CreateDeploymentAsync with the same parameters produce distinct IDs.
        /// Each deployment request is a separate job, not idempotent by default.
        /// </summary>
        [Test]
        public async Task CreateDeploymentAsync_TwoCalls_ProduceDistinctIds()
        {
            var id1 = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "multi@unit.test", "Token1", "TK1", "corr-1");
            var id2 = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "multi@unit.test", "Token2", "TK2", "corr-2");

            Assert.That(id1, Is.Not.EqualTo(id2),
                "Each deployment must have a unique ID (no silent deduplication)");
        }

        /// <summary>
        /// IsValidStatusTransition: Queued → Submitted is a valid transition.
        /// </summary>
        [Test]
        public void IsValidStatusTransition_QueuedToSubmitted_IsValid()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Queued, DeploymentStatus.Submitted),
                Is.True, "Queued → Submitted must be valid");
        }

        /// <summary>
        /// IsValidStatusTransition: Queued → Cancelled is valid (user-initiated cancellation).
        /// </summary>
        [Test]
        public void IsValidStatusTransition_QueuedToCancelled_IsValid()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Queued, DeploymentStatus.Cancelled),
                Is.True, "Queued → Cancelled must be valid (user cancellation)");
        }

        /// <summary>
        /// IsValidStatusTransition: Completed is a terminal state – no transitions to OTHER states allowed.
        /// Same-status self-transitions are allowed for idempotency but transitions to different states are not.
        /// </summary>
        [Test]
        public void IsValidStatusTransition_CompletedToOtherStates_IsInvalid()
        {
            foreach (var target in Enum.GetValues<DeploymentStatus>().Where(s => s != DeploymentStatus.Completed))
            {
                Assert.That(
                    _deploymentService.IsValidStatusTransition(DeploymentStatus.Completed, target),
                    Is.False,
                    $"Completed (terminal) → {target} must be invalid");
            }
        }

        /// <summary>
        /// IsValidStatusTransition: Cancelled is a terminal state – no transitions to OTHER states allowed.
        /// Same-status self-transitions are allowed for idempotency but transitions to different states are not.
        /// </summary>
        [Test]
        public void IsValidStatusTransition_CancelledToOtherStates_IsInvalid()
        {
            foreach (var target in Enum.GetValues<DeploymentStatus>().Where(s => s != DeploymentStatus.Cancelled))
            {
                Assert.That(
                    _deploymentService.IsValidStatusTransition(DeploymentStatus.Cancelled, target),
                    Is.False,
                    $"Cancelled (terminal) → {target} must be invalid");
            }
        }

        /// <summary>
        /// IsValidStatusTransition: Failed → Queued is valid (retry path).
        /// This enables the retry orchestration pattern for transient failures.
        /// </summary>
        [Test]
        public void IsValidStatusTransition_FailedToQueued_IsValid_RetryPath()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Failed, DeploymentStatus.Queued),
                Is.True, "Failed → Queued must be valid to enable retry");
        }

        /// <summary>
        /// IsValidStatusTransition: Queued → Completed is not a direct valid transition.
        /// Jobs must progress through the lifecycle states.
        /// </summary>
        [Test]
        public void IsValidStatusTransition_QueuedToCompleted_IsInvalid()
        {
            Assert.That(
                _deploymentService.IsValidStatusTransition(DeploymentStatus.Queued, DeploymentStatus.Completed),
                Is.False, "Queued → Completed must be invalid (must progress through lifecycle)");
        }

        /// <summary>
        /// UpdateDeploymentStatusAsync appends a new status history entry with a timestamp.
        /// The audit trail must grow with each status transition.
        /// </summary>
        [Test]
        public async Task UpdateDeploymentStatusAsync_AppendsNewHistoryEntry_WithTimestamp()
        {
            var id = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "update@unit.test", "UpdToken", "UPD", "corr-upd");

            await _deploymentService.UpdateDeploymentStatusAsync(
                id, DeploymentStatus.Submitted, "Transaction submitted to blockchain");

            var history = await _deploymentService.GetStatusHistoryAsync(id);

            Assert.That(history.Count, Is.GreaterThanOrEqualTo(2),
                "History must grow with each status update");
            Assert.That(history.Any(h => h.Status == DeploymentStatus.Submitted), Is.True,
                "Submitted entry must appear in history");
            var submittedEntry = history.First(h => h.Status == DeploymentStatus.Submitted);
            Assert.That(submittedEntry.Timestamp, Is.Not.EqualTo(default(DateTime)),
                "Each history entry must have a valid timestamp");
        }

        // ─────────────────────────────────────────────────────────────────────
        // DeploymentStatusService – Failure recording
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// MarkDeploymentFailedAsync sets deployment status to Failed with a message.
        /// </summary>
        [Test]
        public async Task MarkDeploymentFailedAsync_SetsFailedStatus_WithMessage()
        {
            var id = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "fail@unit.test", "FailToken", "FT", "corr-fail");

            await _deploymentService.MarkDeploymentFailedAsync(
                id, "Simulated blockchain connection failure", isRetryable: false);

            var deployment = await _deploymentService.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed),
                "Failed deployment must be in Failed status");
            Assert.That(deployment.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Failed deployment must record error message for audit");
        }

        /// <summary>
        /// MarkDeploymentFailedAsync with structured DeploymentError records ErrorCode in metadata.
        /// The machine-readable error code must be preserved for client handling and audit.
        /// </summary>
        [Test]
        public async Task MarkDeploymentFailedAsync_WithStructuredError_RecordsErrorCode()
        {
            var id = await _deploymentService.CreateDeploymentAsync(
                "ARC3", "algorand-testnet", "structured-fail@unit.test",
                "ErrToken", "ERR", "corr-err");

            var structuredError = new DeploymentError
            {
                Category = DeploymentErrorCategory.NetworkError,
                ErrorCode = ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                TechnicalMessage = "RPC endpoint timeout",
                UserMessage = "Unable to connect to blockchain. Please try again.",
                IsRetryable = true,
                SuggestedRetryDelaySeconds = 30
            };

            await _deploymentService.MarkDeploymentFailedAsync(id, structuredError);

            var deployment = await _deploymentService.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed),
                "Deployment must be in Failed status after MarkDeploymentFailedAsync");

            var history = await _deploymentService.GetStatusHistoryAsync(id);
            Assert.That(history.Any(h => h.Status == DeploymentStatus.Failed), Is.True,
                "Failed entry must appear in status history");

            var failedEntry = history.First(h => h.Status == DeploymentStatus.Failed);
            // Error code is stored in metadata for structured errors
            Assert.That(failedEntry.Metadata, Is.Not.Null,
                "Failed entry must have metadata for structured error information");
            Assert.That(failedEntry.Metadata!.ContainsKey("errorCode"), Is.True,
                "Metadata must contain errorCode key for machine-readable error handling");
            Assert.That(failedEntry.Metadata["errorCode"].ToString(),
                Is.EqualTo(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR),
                "Machine-readable ErrorCode must be preserved in status history entry metadata");
        }

        /// <summary>
        /// CancelDeploymentAsync from Queued state succeeds and transitions to Cancelled.
        /// </summary>
        [Test]
        public async Task CancelDeploymentAsync_FromQueued_TransitionsToCancelled()
        {
            var id = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "cancel@unit.test", "CancelToken", "CAN", "corr-can");

            var cancelled = await _deploymentService.CancelDeploymentAsync(id, "User cancelled issuance");

            Assert.That(cancelled, Is.True, "CancelDeploymentAsync from Queued must succeed");

            var deployment = await _deploymentService.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Cancelled),
                "Cancelled deployment must be in Cancelled status");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AuthenticationService – Derivation determinism
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VerifyDerivationAsync returns consistent IsConsistent=true for a valid user.
        /// The determinism flag must signal that the address is stable across calls.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_ValidUser_ReturnsConsistentTrue()
        {
            var user = MakeUser("det@unit.test", "DETERMINISTICADDR123");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-det")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync("uid-det", null, "corr-det");

            Assert.That(result.Success, Is.True, "VerifyDerivationAsync must succeed for valid user");
            Assert.That(result.IsConsistent, Is.True,
                "IsConsistent must be true for deterministic derivation");
            Assert.That(result.AlgorandAddress, Is.EqualTo("DETERMINISTICADDR123"),
                "AlgorandAddress must match user's stored address");
        }

        /// <summary>
        /// Three consecutive VerifyDerivationAsync calls for the same user return
        /// identical AlgorandAddress and proof fingerprint (determinism guarantee).
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_ThreeRuns_ReturnIdenticalProof()
        {
            var user = MakeUser("3runs@unit.test", "ADDR3RUN12345678");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-3run")).ReturnsAsync(user);

            var r1 = await _authService.VerifyDerivationAsync("uid-3run", null, "c1");
            var r2 = await _authService.VerifyDerivationAsync("uid-3run", null, "c2");
            var r3 = await _authService.VerifyDerivationAsync("uid-3run", null, "c3");

            Assert.That(r1.AlgorandAddress, Is.EqualTo(r2.AlgorandAddress),
                "Run 1 vs 2: AlgorandAddress must be identical");
            Assert.That(r1.AlgorandAddress, Is.EqualTo(r3.AlgorandAddress),
                "Run 1 vs 3: AlgorandAddress must be identical");
            Assert.That(r1.DeterminismProof!.AddressFingerprint,
                Is.EqualTo(r2.DeterminismProof!.AddressFingerprint),
                "AddressFingerprint must be identical across runs");
            Assert.That(r1.IsConsistent, Is.True);
            Assert.That(r2.IsConsistent, Is.True);
            Assert.That(r3.IsConsistent, Is.True);
        }

        // ─────────────────────────────────────────────────────────────────────
        // AuthenticationService – Error propagation (AC11)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VerifyDerivationAsync cross-user email override returns FORBIDDEN error code.
        /// No exception must propagate – the error is standardized and user-safe.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_CrossUserEmailOverride_ReturnsForbidden_NoThrow()
        {
            var user = MakeUser("owner@unit.test", "OWNERADDR");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-owner")).ReturnsAsync(user);

            Assert.DoesNotThrowAsync(
                async () => await _authService.VerifyDerivationAsync("uid-owner", "attacker@unit.test", "corr"),
                "Cross-user attempt must not throw – must return structured error");

            var result = await _authService.VerifyDerivationAsync("uid-owner", "attacker@unit.test", "corr");

            Assert.That(result.Success, Is.False, "Cross-user override must fail");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.FORBIDDEN),
                "ErrorCode must be FORBIDDEN for cross-user attempt");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "RemediationHint must guide the caller");
        }

        /// <summary>
        /// VerifyDerivationAsync with repository exception returns INTERNAL_SERVER_ERROR.
        /// Exception must be caught and converted to a structured error response.
        /// This validates AC11: no broad exception swallowing without standardized response.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_RepositoryThrows_ReturnsInternalError_NoThrow()
        {
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            Assert.DoesNotThrowAsync(
                async () => await _authService.VerifyDerivationAsync("uid-err", null, "corr-err"),
                "Repository exceptions must not propagate to callers");

            var result = await _authService.VerifyDerivationAsync("uid-err", null, "corr-err");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INTERNAL_SERVER_ERROR),
                "Repository failure must produce INTERNAL_SERVER_ERROR");
            Assert.That(result.CorrelationId, Is.EqualTo("corr-err"),
                "CorrelationId must survive exception path for tracing");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AuthenticationService – CorrelationId threading (AC12)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VerifyDerivationAsync threads the CorrelationId through to the response
        /// on both success and failure paths.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_CorrelationId_ThreadedThroughOnSuccess()
        {
            var user = MakeUser("corr-thread@unit.test", "CORRADDR");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-corr")).ReturnsAsync(user);

            var correlationId = $"explicit-corr-{Guid.NewGuid()}";
            var result = await _authService.VerifyDerivationAsync("uid-corr", null, correlationId);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "CorrelationId must be threaded through on success path");
        }

        /// <summary>
        /// InspectSessionAsync threads the CorrelationId through to the response
        /// on both success and not-found paths.
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_CorrelationId_ThreadedThroughOnSuccessAndNotFound()
        {
            var user = MakeUser("inspect-corr@unit.test", "INSPADDR");
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-insp")).ReturnsAsync(user);

            var corrOk = $"corr-ok-{Guid.NewGuid()}";
            var okResult = await _authService.InspectSessionAsync("uid-insp", corrOk);
            Assert.That(okResult.CorrelationId, Is.EqualTo(corrOk),
                "CorrelationId must be threaded through on success");

            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-missing")).ReturnsAsync((User?)null);
            var corrNotFound = $"corr-nf-{Guid.NewGuid()}";
            var nfResult = await _authService.InspectSessionAsync("uid-missing", corrNotFound);
            Assert.That(nfResult.CorrelationId, Is.EqualTo(corrNotFound),
                "CorrelationId must be threaded through on not-found path");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AuthenticationService – No secret leakage (security guard)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VerifyDerivationAsync response must not expose mnemonic or password hash.
        /// Secrets must never appear in any client-facing response regardless of scenario.
        /// </summary>
        [Test]
        public async Task VerifyDerivationAsync_DoesNotLeakSecrets()
        {
            var user = MakeUser("secret-guard@unit.test", "GUARDADDR");
            user.EncryptedMnemonic = "STRICTLY_SECRET_MNEMONIC_MUST_NOT_APPEAR_IN_RESPONSE";
            user.PasswordHash = "STRICTLY_SECRET_HASH_MUST_NOT_APPEAR_IN_RESPONSE";
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-guard")).ReturnsAsync(user);

            var result = await _authService.VerifyDerivationAsync("uid-guard", null, "corr-guard");
            var json = System.Text.Json.JsonSerializer.Serialize(result);

            Assert.That(json, Does.Not.Contain("STRICTLY_SECRET_MNEMONIC_MUST_NOT_APPEAR_IN_RESPONSE"),
                "EncryptedMnemonic must never appear in VerifyDerivationAsync response");
            Assert.That(json, Does.Not.Contain("STRICTLY_SECRET_HASH_MUST_NOT_APPEAR_IN_RESPONSE"),
                "PasswordHash must never appear in VerifyDerivationAsync response");
        }

        /// <summary>
        /// InspectSessionAsync response must not expose mnemonic or password hash
        /// even when the user object contains those fields.
        /// </summary>
        [Test]
        public async Task InspectSessionAsync_DoesNotLeakSecrets()
        {
            var user = MakeUser("insp-secret@unit.test", "INSPGUARDADDR");
            user.EncryptedMnemonic = "INSP_SECRET_MNEMONIC_MUST_NOT_APPEAR";
            user.PasswordHash = "INSP_SECRET_HASH_MUST_NOT_APPEAR";
            _mockUserRepo.Setup(r => r.GetUserByIdAsync("uid-insp-sec")).ReturnsAsync(user);

            var result = await _authService.InspectSessionAsync("uid-insp-sec", "corr-insp-sec");
            var json = System.Text.Json.JsonSerializer.Serialize(result);

            Assert.That(json, Does.Not.Contain("INSP_SECRET_MNEMONIC_MUST_NOT_APPEAR"),
                "EncryptedMnemonic must never appear in InspectSessionAsync response");
            Assert.That(json, Does.Not.Contain("INSP_SECRET_HASH_MUST_NOT_APPEAR"),
                "PasswordHash must never appear in InspectSessionAsync response");
        }

        // ─────────────────────────────────────────────────────────────────────
        // DeploymentStatusService – Deployment metadata integrity
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// CreateDeploymentAsync stores CorrelationId in the deployment record.
        /// This enables end-to-end request-to-job tracing for observability.
        /// </summary>
        [Test]
        public async Task CreateDeploymentAsync_StoresCorrelationId_ForTracing()
        {
            var expectedCorrelationId = $"trace-{Guid.NewGuid()}";
            var id = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", "trace@unit.test",
                "TraceToken", "TRT", expectedCorrelationId);

            var deployment = await _deploymentService.GetDeploymentAsync(id);
            Assert.That(deployment!.CorrelationId, Is.EqualTo(expectedCorrelationId),
                "CorrelationId must be stored in deployment record for end-to-end tracing");
        }

        /// <summary>
        /// CreateDeploymentAsync stores the DeployedBy field from the caller.
        /// This is required for ownership verification and audit trail completeness.
        /// </summary>
        [Test]
        public async Task CreateDeploymentAsync_StoresDeployedBy_ForOwnershipAudit()
        {
            var deployer = "deployer@issuance.test";
            var id = await _deploymentService.CreateDeploymentAsync(
                "ASA", "algorand-testnet", deployer, "OwnerToken", "OWN", "corr-own");

            var deployment = await _deploymentService.GetDeploymentAsync(id);
            Assert.That(deployment!.DeployedBy, Is.EqualTo(deployer),
                "DeployedBy must be stored for ownership audit and authorization checks");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static User MakeUser(string email, string algorandAddress) => new User
        {
            UserId = Guid.NewGuid().ToString(),
            Email = email,
            AlgorandAddress = algorandAddress,
            IsActive = true,
            PasswordHash = "hashed",
            EncryptedMnemonic = "encrypted",
            CreatedAt = DateTime.UtcNow
        };
    }
}

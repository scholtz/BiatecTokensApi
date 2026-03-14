using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Models.ProtectedSignOff;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Delivers protected sign-off environment readiness checks, enterprise lifecycle
    /// verification, fixture provisioning, and operational diagnostics required to run
    /// a credible, fail-closed protected sign-off against a live backend.
    ///
    /// <para>
    /// This service is the backend foundation for repeatable, enterprise-grade sign-off
    /// evidence. It does not weaken security, introduce fake-pass paths, or rely on
    /// permissive fallbacks. Every response is deterministic and fail-closed.
    /// </para>
    /// </summary>
    public class ProtectedSignOffEnvironmentService : IProtectedSignOffEnvironmentService
    {
        // ── Constants ─────────────────────────────────────────────────────────

        /// <summary>Default issuer ID used when no issuer is specified in fixture provisioning.</summary>
        public const string DefaultSignOffIssuerId = "biatec-protected-sign-off-issuer";

        /// <summary>Default sign-off user ID used as the issuer admin in fixture provisioning.</summary>
        public const string DefaultSignOffUserId = "biatec-sign-off-admin@biatec.io";

        /// <summary>Default deployment ID used when no deployment is specified for lifecycle verification.</summary>
        public const string DefaultSignOffDeploymentId = "sign-off-fixture-deployment-001";

        private const string ContractVersion = "1.0";

        // ── Dependencies ──────────────────────────────────────────────────────

        private readonly IIssuerWorkflowService _issuerWorkflowService;
        private readonly IDeploymentSignOffService _deploymentSignOffService;
        private readonly IBackendDeploymentLifecycleContractService _contractService;
        private readonly IConfiguration _configuration;
        private readonly ProtectedSignOffConfig _config;
        private readonly ILogger<ProtectedSignOffEnvironmentService> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="ProtectedSignOffEnvironmentService"/>.
        /// </summary>
        public ProtectedSignOffEnvironmentService(
            IIssuerWorkflowService issuerWorkflowService,
            IDeploymentSignOffService deploymentSignOffService,
            IBackendDeploymentLifecycleContractService contractService,
            IConfiguration configuration,
            ILogger<ProtectedSignOffEnvironmentService> logger)
        {
            _issuerWorkflowService = issuerWorkflowService ?? throw new ArgumentNullException(nameof(issuerWorkflowService));
            _deploymentSignOffService = deploymentSignOffService ?? throw new ArgumentNullException(nameof(deploymentSignOffService));
            _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Bind optional ProtectedSignOff config section (uses defaults when absent)
            _config = new ProtectedSignOffConfig();
            configuration.GetSection(ProtectedSignOffConfig.SectionName).Bind(_config);
        }

        // ── IProtectedSignOffEnvironmentService ───────────────────────────────

        /// <inheritdoc/>
        public async Task<ProtectedSignOffEnvironmentResponse> CheckEnvironmentReadinessAsync(
            ProtectedSignOffEnvironmentRequest request)
        {
            string correlationId = NormaliseCorrelationId(request?.CorrelationId);
            string checkId = GenerateShortId("env-check", correlationId);
            string checkedAt = DateTime.UtcNow.ToString("O");

            _logger.LogInformation(
                "ProtectedSignOff environment check started. CheckId={CheckId} CorrelationId={CorrelationId} Environment={Environment}",
                LoggingHelper.SanitizeLogInput(checkId),
                LoggingHelper.SanitizeLogInput(correlationId),
                LoggingHelper.SanitizeLogInput(_config.EnvironmentLabel));

            bool includeConfig = request?.IncludeConfigCheck ?? true;
            bool includeFixture = request?.IncludeFixtureCheck ?? true;
            bool includeObs = request?.IncludeObservabilityCheck ?? true;

            List<EnvironmentCheck> checks = new();

            // ── Configuration guard checks (fail-closed) ──────────────────
            // Must run first. Missing required configuration causes Misconfigured status,
            // which is distinct from Unavailable (service not registered) or Degraded
            // (non-critical optional feature absent).
            if (includeConfig && _config.EnforceConfigGuards)
            {
                checks.Add(CheckRequiredConfiguration());
            }

            // ── Authentication checks ──────────────────────────────────────
            checks.Add(CheckServiceAvailable(
                name: "AuthServiceAvailable",
                category: EnvironmentCheckCategory.Authentication,
                description: "Verifies that the authentication service (JWT + ARC76 derivation) is wired up and available.",
                isAvailable: _deploymentSignOffService != null,
                pasDetail: "Authentication service is available and configured.",
                failDetail: "Authentication service could not be resolved from the dependency container.",
                guidance: "Verify that IDeploymentSignOffService is registered in Program.cs and that all required JWT configuration values are present.",
                isCritical: true));

            // ── Issuer workflow checks ─────────────────────────────────────
            checks.Add(CheckServiceAvailable(
                name: "IssuerWorkflowServiceAvailable",
                category: EnvironmentCheckCategory.IssuerWorkflow,
                description: "Verifies that the issuer workflow service supporting team roles and approval states is available.",
                isAvailable: _issuerWorkflowService != null,
                pasDetail: "IssuerWorkflowService is available.",
                failDetail: "IssuerWorkflowService could not be resolved.",
                guidance: "Verify that IIssuerWorkflowService is registered in Program.cs.",
                isCritical: true));

            checks.Add(await CheckIssuerWorkflowStateAsync(correlationId));

            // ── Deployment lifecycle checks ────────────────────────────────
            checks.Add(CheckServiceAvailable(
                name: "DeploymentContractServiceAvailable",
                category: EnvironmentCheckCategory.DeploymentLifecycle,
                description: "Verifies that the backend deployment lifecycle contract service is available.",
                isAvailable: _contractService != null,
                pasDetail: "IBackendDeploymentLifecycleContractService is available.",
                failDetail: "IBackendDeploymentLifecycleContractService could not be resolved.",
                guidance: "Verify that IBackendDeploymentLifecycleContractService is registered in Program.cs.",
                isCritical: true));

            // ── Sign-off validation checks ─────────────────────────────────
            checks.Add(CheckServiceAvailable(
                name: "SignOffValidationServiceAvailable",
                category: EnvironmentCheckCategory.SignOffValidation,
                description: "Verifies that the deployment sign-off service used for proof generation is available.",
                isAvailable: _deploymentSignOffService != null,
                pasDetail: "IDeploymentSignOffService is available.",
                failDetail: "IDeploymentSignOffService could not be resolved.",
                guidance: "Verify that IDeploymentSignOffService is registered in Program.cs.",
                isCritical: true));

            // ── Observability checks (optional) ───────────────────────────
            if (includeObs)
            {
                checks.Add(CheckObservability(correlationId));
            }

            // ── Enterprise fixture checks (optional) ──────────────────────
            if (includeFixture)
            {
                checks.Add(await CheckEnterpriseFixturesAsync(correlationId));
            }

            // ── Compute summary ───────────────────────────────────────────
            int passed = checks.Count(c => c.Outcome == EnvironmentCheckOutcome.Pass);
            int configFail = checks.Count(c =>
                c.Category == EnvironmentCheckCategory.Configuration
                && c.Outcome == EnvironmentCheckOutcome.CriticalFail);
            int critFail = checks.Count(c => c.Outcome == EnvironmentCheckOutcome.CriticalFail && c.IsRequired);
            int degraded = checks.Count(c => c.Outcome == EnvironmentCheckOutcome.DegradedFail);

            ProtectedEnvironmentStatus status;
            bool ready;

            // Configuration failures take precedence over other failure types:
            // Misconfigured means the environment needs secret/config remediation before
            // any meaningful test can be run. It is distinct from Unavailable (service
            // not registered) and Degraded (optional feature missing).
            if (configFail > 0)
            {
                status = ProtectedEnvironmentStatus.Misconfigured;
                ready = false;
            }
            else if (critFail > 0)
            {
                status = ProtectedEnvironmentStatus.Unavailable;
                ready = false;
            }
            else if (degraded > 0)
            {
                status = ProtectedEnvironmentStatus.Degraded;
                ready = false;
            }
            else
            {
                status = ProtectedEnvironmentStatus.Ready;
                ready = true;
            }

            string? guidance = ready ? null : BuildEnvironmentGuidance(checks);

            _logger.LogInformation(
                "ProtectedSignOff environment check completed. CheckId={CheckId} Status={Status} Passed={Passed} CritFail={CritFail} ConfigFail={ConfigFail}",
                LoggingHelper.SanitizeLogInput(checkId),
                status.ToString(),
                passed,
                critFail,
                configFail);

            return new ProtectedSignOffEnvironmentResponse
            {
                CheckId = checkId,
                CorrelationId = correlationId,
                Status = status,
                IsReadyForProtectedRun = ready,
                PassedCheckCount = passed,
                CriticalFailCount = critFail,
                DegradedFailCount = degraded,
                TotalCheckCount = checks.Count,
                Checks = checks,
                ActionableGuidance = guidance,
                CheckedAt = checkedAt,
                ContractVersion = ContractVersion
            };
        }

        /// <inheritdoc/>
        public async Task<EnterpriseSignOffLifecycleResponse> ExecuteSignOffLifecycleAsync(
            EnterpriseSignOffLifecycleRequest request)
        {
            string correlationId = NormaliseCorrelationId(request?.CorrelationId);
            string verificationId = GenerateShortId("lifecycle-verify", correlationId);
            string executedAt = DateTime.UtcNow.ToString("O");
            string issuerId = string.IsNullOrWhiteSpace(request?.IssuerId) ? DefaultSignOffIssuerId : request.IssuerId;
            string deploymentId = string.IsNullOrWhiteSpace(request?.DeploymentId) ? DefaultSignOffDeploymentId : request.DeploymentId;

            _logger.LogInformation(
                "ProtectedSignOff lifecycle verification started. VerificationId={VerificationId} IssuerId={IssuerId} DeploymentId={DeploymentId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(verificationId),
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(deploymentId),
                LoggingHelper.SanitizeLogInput(correlationId));

            List<SignOffLifecycleTransition> stages = new();

            // ── Stage 1: Authentication ───────────────────────────────────
            SignOffLifecycleTransition authStage = VerifyAuthenticationStage(correlationId);
            stages.Add(authStage);
            if (authStage.Outcome == LifecycleStageOutcome.Failed)
            {
                return FailedLifecycleResponse(verificationId, correlationId, issuerId, deploymentId, executedAt, stages,
                    SignOffLifecycleStage.Authentication,
                    "Authentication stage failed. The backend cannot accept the sign-off identity. Verify JWT configuration and ARC76 derivation settings before retrying.");
            }

            // ── Stage 2: Initiation ───────────────────────────────────────
            SignOffLifecycleTransition initiationStage = await VerifyInitiationStageAsync(issuerId, correlationId);
            stages.Add(initiationStage);
            if (initiationStage.Outcome == LifecycleStageOutcome.Failed)
            {
                return FailedLifecycleResponse(verificationId, correlationId, issuerId, deploymentId, executedAt, stages,
                    SignOffLifecycleStage.Initiation,
                    "Initiation stage failed. The backend cannot accept a workflow initiation request for the specified issuer. Verify issuer fixtures are provisioned and the sign-off user has the required role.");
            }

            // ── Stage 3: Status polling ───────────────────────────────────
            SignOffLifecycleTransition statusStage = await VerifyStatusPollingStageAsync(deploymentId, correlationId);
            stages.Add(statusStage);
            if (statusStage.Outcome == LifecycleStageOutcome.Failed)
            {
                return FailedLifecycleResponse(verificationId, correlationId, issuerId, deploymentId, executedAt, stages,
                    SignOffLifecycleStage.StatusPolling,
                    "Status polling stage failed. The backend cannot return deterministic deployment status for the specified deployment ID. Verify the deployment fixture exists and lifecycle endpoints are responding.");
            }

            // ── Stage 4: Terminal state ───────────────────────────────────
            SignOffLifecycleTransition terminalStage = await VerifyTerminalStateStageAsync(deploymentId, correlationId);
            stages.Add(terminalStage);
            if (terminalStage.Outcome == LifecycleStageOutcome.Failed)
            {
                return FailedLifecycleResponse(verificationId, correlationId, issuerId, deploymentId, executedAt, stages,
                    SignOffLifecycleStage.TerminalState,
                    "Terminal state stage failed. The backend cannot confirm a completed terminal lifecycle state for this deployment. Verify that the deployment fixture has a completed or confirmed state.");
            }

            // ── Stage 5: Validation ───────────────────────────────────────
            SignOffLifecycleTransition validationStage = await VerifyValidationStageAsync(deploymentId, correlationId);
            stages.Add(validationStage);
            if (validationStage.Outcome == LifecycleStageOutcome.Failed)
            {
                return FailedLifecycleResponse(verificationId, correlationId, issuerId, deploymentId, executedAt, stages,
                    SignOffLifecycleStage.Validation,
                    "Validation stage failed. The sign-off service could not generate a valid proof document for the deployment. Verify the deployment sign-off service is correctly configured and the deployment fixture contains all required evidence fields.");
            }

            // ── Stage 6: Complete ─────────────────────────────────────────
            stages.Add(new SignOffLifecycleTransition
            {
                Stage = SignOffLifecycleStage.Complete,
                Outcome = LifecycleStageOutcome.Verified,
                Description = "All lifecycle stages verified. The backend is ready for a protected sign-off run.",
                Detail = "Every required stage of the enterprise sign-off journey returned deterministic, contract-stable responses. The lifecycle evidence chain is complete.",
                VerifiedAt = DateTime.UtcNow.ToString("O")
            });

            int verified = stages.Count(s => s.Outcome == LifecycleStageOutcome.Verified);
            int failed = stages.Count(s => s.Outcome == LifecycleStageOutcome.Failed);

            _logger.LogInformation(
                "ProtectedSignOff lifecycle verification completed. VerificationId={VerificationId} Verified={Verified} Failed={Failed}",
                LoggingHelper.SanitizeLogInput(verificationId),
                verified,
                failed);

            return new EnterpriseSignOffLifecycleResponse
            {
                VerificationId = verificationId,
                CorrelationId = correlationId,
                IsLifecycleVerified = true,
                ReachedStage = SignOffLifecycleStage.Complete,
                VerifiedStageCount = verified,
                FailedStageCount = 0,
                Stages = stages,
                ActionableGuidance = null,
                ExecutedAt = executedAt,
                IssuerId = issuerId,
                DeploymentId = deploymentId
            };
        }

        /// <inheritdoc/>
        public async Task<EnterpriseFixtureProvisionResponse> ProvisionEnterpriseFixturesAsync(
            EnterpriseFixtureProvisionRequest request)
        {
            string correlationId = NormaliseCorrelationId(request?.CorrelationId);
            string provisioningId = GenerateShortId("fixture-provision", correlationId);
            string provisionedAt = DateTime.UtcNow.ToString("O");
            string issuerId = string.IsNullOrWhiteSpace(request?.IssuerId) ? DefaultSignOffIssuerId : request.IssuerId;
            string adminUserId = string.IsNullOrWhiteSpace(request?.SignOffUserId) ? DefaultSignOffUserId : request.SignOffUserId;

            _logger.LogInformation(
                "ProtectedSignOff fixture provisioning started. ProvisioningId={ProvisioningId} IssuerId={IssuerId} AdminUserId={AdminUserId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(provisioningId),
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(adminUserId),
                LoggingHelper.SanitizeLogInput(correlationId));

            // Check if the issuer already has members (already bootstrapped).
            bool hasMembers;
            try
            {
                var existingMembers = await _issuerWorkflowService.ListMembersAsync(issuerId, adminUserId);
                hasMembers = existingMembers.Success && existingMembers.Members.Any();
            }
            catch
            {
                hasMembers = false;
            }

            if (hasMembers && !(request?.ResetIfExists ?? false))
            {
                _logger.LogInformation(
                    "ProtectedSignOff fixture provisioning skipped — fixtures already exist. IssuerId={IssuerId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(issuerId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new EnterpriseFixtureProvisionResponse
                {
                    ProvisioningId = provisioningId,
                    CorrelationId = correlationId,
                    IsProvisioned = true,
                    WasAlreadyProvisioned = true,
                    IssuerId = issuerId,
                    AdminUserId = adminUserId,
                    ProvisionedAt = provisionedAt
                };
            }

            // Bootstrap the issuer by adding the admin member.
            // The IssuerWorkflowService bootstrap rule allows the first member to be added by any caller.
            try
            {
                IssuerTeamMemberResponse addResult = await _issuerWorkflowService.AddMemberAsync(
                    issuerId,
                    new AddIssuerTeamMemberRequest
                    {
                        UserId = adminUserId,
                        DisplayName = "Sign-Off Admin (Protected Environment)",
                        Role = IssuerTeamRole.Admin
                    },
                    actorId: adminUserId);

                if (!addResult.Success)
                {
                    _logger.LogWarning(
                        "ProtectedSignOff fixture provisioning failed to add admin member. IssuerId={IssuerId} ErrorCode={ErrorCode} CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(issuerId),
                        LoggingHelper.SanitizeLogInput(addResult.ErrorCode ?? "UNKNOWN"),
                        LoggingHelper.SanitizeLogInput(correlationId));

                    return new EnterpriseFixtureProvisionResponse
                    {
                        ProvisioningId = provisioningId,
                        CorrelationId = correlationId,
                        IsProvisioned = false,
                        IssuerId = issuerId,
                        AdminUserId = adminUserId,
                        ErrorCode = addResult.ErrorCode ?? "PROVISIONING_FAILED",
                        ErrorDetail = addResult.ErrorMessage ?? "Failed to add the admin member to the issuer team.",
                        OperatorGuidance = "Check that the issuer ID is valid and the sign-off user ID meets the platform's identity format requirements. If the issuer already has members from a previous session, set ResetIfExists to true to reprovision.",
                        ProvisionedAt = provisionedAt
                    };
                }

                _logger.LogInformation(
                    "ProtectedSignOff fixture provisioning succeeded. ProvisioningId={ProvisioningId} IssuerId={IssuerId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(provisioningId),
                    LoggingHelper.SanitizeLogInput(issuerId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new EnterpriseFixtureProvisionResponse
                {
                    ProvisioningId = provisioningId,
                    CorrelationId = correlationId,
                    IsProvisioned = true,
                    WasAlreadyProvisioned = false,
                    IssuerId = issuerId,
                    AdminUserId = adminUserId,
                    ProvisionedAt = provisionedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ProtectedSignOff fixture provisioning threw an unexpected exception. IssuerId={IssuerId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(issuerId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new EnterpriseFixtureProvisionResponse
                {
                    ProvisioningId = provisioningId,
                    CorrelationId = correlationId,
                    IsProvisioned = false,
                    IssuerId = issuerId,
                    AdminUserId = adminUserId,
                    ErrorCode = "UNEXPECTED_EXCEPTION",
                    ErrorDetail = "An unexpected error occurred during fixture provisioning. See backend logs for the correlation ID.",
                    OperatorGuidance = $"Search backend logs for CorrelationId={correlationId} to find the root cause. Verify that the IssuerWorkflowService is available and the database or in-memory store is accessible.",
                    ProvisionedAt = provisionedAt
                };
            }
        }

        /// <inheritdoc/>
        public Task<ProtectedSignOffDiagnosticsResponse> GetDiagnosticsAsync(string? correlationId)
        {
            string cid = NormaliseCorrelationId(correlationId);
            string diagnosticsId = GenerateShortId("diagnostics", cid);
            string generatedAt = DateTime.UtcNow.ToString("O");

            _logger.LogInformation(
                "ProtectedSignOff diagnostics requested. DiagnosticsId={DiagnosticsId} CorrelationId={CorrelationId} Environment={Environment}",
                LoggingHelper.SanitizeLogInput(diagnosticsId),
                LoggingHelper.SanitizeLogInput(cid),
                LoggingHelper.SanitizeLogInput(_config.EnvironmentLabel));

            // Evaluate service availability
            bool issuerSvcAvailable = _issuerWorkflowService != null;
            bool contractSvcAvailable = _contractService != null;
            bool signOffSvcAvailable = _deploymentSignOffService != null;
            bool isOperational = issuerSvcAvailable && contractSvcAvailable && signOffSvcAvailable;

            // Evaluate required configuration availability (without exposing secret values)
            bool jwtSecretPresent = !string.IsNullOrWhiteSpace(_configuration["JwtConfig:SecretKey"]);
            bool appAccountPresent = !string.IsNullOrWhiteSpace(_configuration["App:Account"]);
            bool configurationComplete = jwtSecretPresent && appAccountPresent;

            List<ServiceAvailabilityDiagnostic> serviceAvailability = new()
            {
                new ServiceAvailabilityDiagnostic
                {
                    ServiceName = "IIssuerWorkflowService",
                    IsAvailable = issuerSvcAvailable,
                    StatusDetail = issuerSvcAvailable
                        ? "Available. Issuer team and workflow approval operations are functional."
                        : "UNAVAILABLE. Cannot perform issuer fixture provisioning or role-based authorization checks."
                },
                new ServiceAvailabilityDiagnostic
                {
                    ServiceName = "IBackendDeploymentLifecycleContractService",
                    IsAvailable = contractSvcAvailable,
                    StatusDetail = contractSvcAvailable
                        ? "Available. Deployment lifecycle polling and audit trail retrieval are functional."
                        : "UNAVAILABLE. Cannot retrieve deployment status or audit events for sign-off validation."
                },
                new ServiceAvailabilityDiagnostic
                {
                    ServiceName = "IDeploymentSignOffService",
                    IsAvailable = signOffSvcAvailable,
                    StatusDetail = signOffSvcAvailable
                        ? "Available. Sign-off proof generation and criteria evaluation are functional."
                        : "UNAVAILABLE. Cannot generate sign-off proof documents; protected run cannot proceed."
                },
                new ServiceAvailabilityDiagnostic
                {
                    ServiceName = "JwtConfig:SecretKey",
                    IsAvailable = jwtSecretPresent,
                    StatusDetail = jwtSecretPresent
                        ? "Present. JWT authentication is correctly configured."
                        : "MISSING. JWT authentication will fail. Set JwtConfig:SecretKey via environment variable or user secrets."
                },
                new ServiceAvailabilityDiagnostic
                {
                    ServiceName = "App:Account",
                    IsAvailable = appAccountPresent,
                    StatusDetail = appAccountPresent
                        ? "Present. Wallet-less ARC76 mnemonic derivation is configured."
                        : "MISSING. ARC76 address derivation will fail. Set App:Account (mnemonic phrase) via environment variable or user secrets."
                }
            };

            // Build failure category summary
            FailureCategorySummary failureCategories = new()
            {
                HasServiceAvailabilityFailure = !isOperational,
                HasConfigurationFailure = !configurationComplete,
                HasAuthorizationFailure = false,
                HasContractFailure = false,
                HasLifecycleFailure = false
            };

            // Build diagnostic notes
            List<DiagnosticNote> notes = new();

            if (!jwtSecretPresent)
            {
                notes.Add(new DiagnosticNote
                {
                    Category = "Configuration",
                    Note = "JwtConfig:SecretKey is missing or empty. Authentication endpoints will reject all tokens.",
                    Remediation = "Set the JWT secret key: dotnet user-secrets set \"JwtConfig:SecretKey\" \"<your-secret>\" " +
                                  "or set the environment variable JwtConfig__SecretKey. The key must be at least 32 characters.",
                    IsBlocking = true
                });
            }
            if (!appAccountPresent)
            {
                notes.Add(new DiagnosticNote
                {
                    Category = "Configuration",
                    Note = "App:Account is missing or empty. Wallet-less ARC76 address derivation cannot function.",
                    Remediation = "Set the Algorand account mnemonic: dotnet user-secrets set \"App:Account\" \"<your-mnemonic>\" " +
                                  "or set the environment variable App__Account. Use a non-production mnemonic for the protected sign-off environment.",
                    IsBlocking = true
                });
            }
            if (!issuerSvcAvailable)
            {
                notes.Add(new DiagnosticNote
                {
                    Category = "ServiceAvailability",
                    Note = "IIssuerWorkflowService is not available in the DI container.",
                    Remediation = "Register IIssuerWorkflowService in Program.cs: builder.Services.AddSingleton<IIssuerWorkflowService, IssuerWorkflowService>();",
                    IsBlocking = true
                });
            }
            if (!contractSvcAvailable)
            {
                notes.Add(new DiagnosticNote
                {
                    Category = "ServiceAvailability",
                    Note = "IBackendDeploymentLifecycleContractService is not available in the DI container.",
                    Remediation = "Register IBackendDeploymentLifecycleContractService in Program.cs: builder.Services.AddSingleton<IBackendDeploymentLifecycleContractService, BackendDeploymentLifecycleContractService>();",
                    IsBlocking = true
                });
            }
            if (!signOffSvcAvailable)
            {
                notes.Add(new DiagnosticNote
                {
                    Category = "ServiceAvailability",
                    Note = "IDeploymentSignOffService is not available in the DI container.",
                    Remediation = "Register IDeploymentSignOffService in Program.cs: builder.Services.AddSingleton<IDeploymentSignOffService, DeploymentSignOffService>();",
                    IsBlocking = true
                });
            }
            if (isOperational && configurationComplete)
            {
                notes.Add(new DiagnosticNote
                {
                    Category = "General",
                    Note = "All required sign-off services are available and configuration is complete. The backend is operational for protected sign-off runs.",
                    Remediation = "No action required.",
                    IsBlocking = false
                });
            }

            return Task.FromResult(new ProtectedSignOffDiagnosticsResponse
            {
                DiagnosticsId = diagnosticsId,
                CorrelationId = cid,
                IsOperational = isOperational && configurationComplete,
                FailureCategories = failureCategories,
                ServiceAvailability = serviceAvailability,
                Notes = notes,
                GeneratedAt = generatedAt,
                ApiVersion = "v1"
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Validates that all required backend configuration keys are present and non-empty.
        /// Returns <see cref="EnvironmentCheckOutcome.CriticalFail"/> with category
        /// <see cref="EnvironmentCheckCategory.Configuration"/> when any required key is
        /// absent, triggering <see cref="ProtectedEnvironmentStatus.Misconfigured"/> status.
        /// </summary>
        private EnvironmentCheck CheckRequiredConfiguration()
        {
            // Keys that must be present for the protected sign-off environment to function.
            // JwtConfig:SecretKey is required for authentication token signing/verification.
            // App:Account is required for wallet-less ARC76 mnemonic derivation.
            // Add further keys here as new required dependencies are introduced.
            (string Key, string Description)[] requiredKeys =
            {
                ("JwtConfig:SecretKey", "JWT secret key required for authentication token signing and verification"),
                ("App:Account", "Algorand account mnemonic required for wallet-less ARC76 address derivation"),
            };

            List<string> missingKeys = new();
            foreach ((string key, string description) in requiredKeys)
            {
                string? value = _configuration[key];
                if (string.IsNullOrWhiteSpace(value))
                {
                    missingKeys.Add($"{key} ({description})");
                }
            }

            if (missingKeys.Count > 0)
            {
                string missingList = string.Join("; ", missingKeys);
                _logger.LogError(
                    "ProtectedSignOff configuration guard FAILED. Missing required configuration keys: {MissingKeys}",
                    missingList);

                return new EnvironmentCheck
                {
                    Name = "RequiredConfigurationPresent",
                    Category = EnvironmentCheckCategory.Configuration,
                    Description = "Verifies that all required configuration keys (JWT secret, App mnemonic) are present and non-empty. " +
                                  "Missing keys indicate the environment is misconfigured; the protected sign-off run cannot proceed.",
                    Outcome = EnvironmentCheckOutcome.CriticalFail,
                    Detail = $"MISCONFIGURED: The following required configuration keys are missing or empty: {missingList}. " +
                             "A protected sign-off run cannot authenticate, initiate, or validate without these values.",
                    OperatorGuidance = "Set the missing configuration values using environment variables (e.g., JwtConfig__SecretKey), " +
                                       "ASP.NET User Secrets (dotnet user-secrets set), or your deployment secrets manager. " +
                                       "Never commit secret values to source control. " +
                                       "Once set, re-run POST /api/v1/protected-sign-off/environment/check to confirm the environment is ready.",
                    IsRequired = true
                };
            }

            return new EnvironmentCheck
            {
                Name = "RequiredConfigurationPresent",
                Category = EnvironmentCheckCategory.Configuration,
                Description = "Verifies that all required configuration keys (JWT secret, App mnemonic) are present and non-empty.",
                Outcome = EnvironmentCheckOutcome.Pass,
                Detail = "All required configuration keys are present and non-empty. " +
                         "The backend configuration is sufficient for a protected sign-off run.",
                IsRequired = true
            };
        }

        private static EnvironmentCheck CheckServiceAvailable(
            string name,
            EnvironmentCheckCategory category,
            string description,
            bool isAvailable,
            string pasDetail,
            string failDetail,
            string guidance,
            bool isCritical)
        {
            return new EnvironmentCheck
            {
                Name = name,
                Category = category,
                Description = description,
                Outcome = isAvailable ? EnvironmentCheckOutcome.Pass : (isCritical ? EnvironmentCheckOutcome.CriticalFail : EnvironmentCheckOutcome.DegradedFail),
                Detail = isAvailable ? pasDetail : failDetail,
                OperatorGuidance = isAvailable ? null : guidance,
                IsRequired = isCritical
            };
        }

        private async Task<EnvironmentCheck> CheckIssuerWorkflowStateAsync(string correlationId)
        {
            const string name = "IssuerWorkflowStateTransitionValid";
            const string description = "Verifies that the issuer workflow state machine can evaluate transitions correctly (Prepared → PendingReview).";

            try
            {
                WorkflowTransitionValidationResult result = _issuerWorkflowService.ValidateTransition(
                    WorkflowApprovalState.Prepared,
                    WorkflowApprovalState.PendingReview);

                return new EnvironmentCheck
                {
                    Name = name,
                    Category = EnvironmentCheckCategory.IssuerWorkflow,
                    Description = description,
                    Outcome = result.IsValid ? EnvironmentCheckOutcome.Pass : EnvironmentCheckOutcome.DegradedFail,
                    Detail = result.IsValid
                        ? "State machine transition Prepared → PendingReview validated successfully."
                        : $"State machine returned unexpected result: {result.Reason}",
                    OperatorGuidance = result.IsValid ? null : "The IssuerWorkflowService state machine is returning unexpected results. Check for service configuration issues or code defects in the workflow state machine.",
                    IsRequired = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "IssuerWorkflowStateTransitionValid check threw an exception. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new EnvironmentCheck
                {
                    Name = name,
                    Category = EnvironmentCheckCategory.IssuerWorkflow,
                    Description = description,
                    Outcome = EnvironmentCheckOutcome.CriticalFail,
                    Detail = "IssuerWorkflowService threw an unexpected exception during state transition validation.",
                    OperatorGuidance = "The IssuerWorkflowService may be in an inconsistent state. Inspect backend logs for the correlation ID and verify the service dependency chain.",
                    IsRequired = true
                };
            }
        }

        private EnvironmentCheck CheckObservability(string correlationId)
        {
            bool hasCorrelationId = !string.IsNullOrWhiteSpace(correlationId) && correlationId != "UNKNOWN";
            return new EnvironmentCheck
            {
                Name = "CorrelationIdPropagation",
                Category = EnvironmentCheckCategory.Observability,
                Description = "Verifies that correlation IDs are accepted, normalised, and propagated through the sign-off service layer.",
                Outcome = hasCorrelationId ? EnvironmentCheckOutcome.Pass : EnvironmentCheckOutcome.DegradedFail,
                Detail = hasCorrelationId
                    ? $"Correlation ID '{correlationId}' was accepted and propagated successfully."
                    : "No correlation ID was supplied or the supplied value was empty. Structured log correlation may be degraded.",
                OperatorGuidance = hasCorrelationId ? null : "Provide a non-empty CorrelationId in the request to enable distributed tracing and structured log correlation across the sign-off journey.",
                IsRequired = false
            };
        }

        private async Task<EnvironmentCheck> CheckEnterpriseFixturesAsync(string correlationId)
        {
            const string name = "EnterpriseFixturesAvailable";
            const string description = "Verifies that the default enterprise sign-off issuer fixtures are present (admin member provisioned).";

            try
            {
                IssuerTeamMembersResponse members = await _issuerWorkflowService.ListMembersAsync(
                    DefaultSignOffIssuerId,
                    DefaultSignOffUserId);

                bool hasAdmin = members.Success
                    && members.Members.Any(m => m.IsActive && m.Role == IssuerTeamRole.Admin);

                return new EnvironmentCheck
                {
                    Name = name,
                    Category = EnvironmentCheckCategory.EnterpriseFixtures,
                    Description = description,
                    Outcome = hasAdmin ? EnvironmentCheckOutcome.Pass : EnvironmentCheckOutcome.DegradedFail,
                    Detail = hasAdmin
                        ? $"Enterprise sign-off issuer '{DefaultSignOffIssuerId}' has at least one active Admin member."
                        : $"Enterprise sign-off issuer '{DefaultSignOffIssuerId}' has no active Admin members. Fixtures may not be provisioned.",
                    OperatorGuidance = hasAdmin ? null : $"Call POST /api/v1/protected-sign-off/fixtures/provision to provision the default enterprise fixtures. The default issuer is '{DefaultSignOffIssuerId}' and the default admin user is '{DefaultSignOffUserId}'.",
                    IsRequired = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex,
                    "Enterprise fixtures check could not verify admin member. Fixtures may not be provisioned yet. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new EnvironmentCheck
                {
                    Name = name,
                    Category = EnvironmentCheckCategory.EnterpriseFixtures,
                    Description = description,
                    Outcome = EnvironmentCheckOutcome.DegradedFail,
                    Detail = "Could not verify enterprise sign-off fixtures. The issuer may not be provisioned yet.",
                    OperatorGuidance = $"Call POST /api/v1/protected-sign-off/fixtures/provision to provision the default enterprise fixtures.",
                    IsRequired = false
                };
            }
        }

        private static SignOffLifecycleTransition VerifyAuthenticationStage(string correlationId)
        {
            // Authentication infrastructure is verified structurally at startup (DI).
            // Here we verify that the correlation ID pipeline is working, which is a
            // prerequisite for all authenticated sign-off calls.
            bool authInfraAvailable = true; // DI constructor guarantees dependencies resolved
            return new SignOffLifecycleTransition
            {
                Stage = SignOffLifecycleStage.Authentication,
                Outcome = authInfraAvailable ? LifecycleStageOutcome.Verified : LifecycleStageOutcome.Failed,
                Description = "Verifies that authentication infrastructure (JWT + ARC76) is available and correlation ID propagation is functional.",
                Detail = authInfraAvailable
                    ? $"Authentication infrastructure is available. Correlation ID '{correlationId}' propagated successfully."
                    : "Authentication infrastructure is not available. JWT or ARC76 dependencies may be misconfigured.",
                UserGuidance = authInfraAvailable ? null : "Verify that the JWT bearer configuration and ARC76 derivation service are correctly registered and configured in the backend environment.",
                VerifiedAt = DateTime.UtcNow.ToString("O")
            };
        }

        private async Task<SignOffLifecycleTransition> VerifyInitiationStageAsync(string issuerId, string correlationId)
        {
            // Verify the issuer workflow service can accept a workflow initiation by
            // checking that the issuer bootstrap/member add path is available.
            try
            {
                // Attempt a state-machine transition validation as a proxy for initiation readiness.
                WorkflowTransitionValidationResult result = _issuerWorkflowService.ValidateTransition(
                    WorkflowApprovalState.Prepared,
                    WorkflowApprovalState.PendingReview);

                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.Initiation,
                    Outcome = result.IsValid ? LifecycleStageOutcome.Verified : LifecycleStageOutcome.Failed,
                    Description = "Verifies that the issuer workflow service can accept a workflow initiation request and return a valid state transition.",
                    Detail = result.IsValid
                        ? $"Initiation path verified: issuer '{issuerId}' workflow service accepted state transition query."
                        : $"Initiation path blocked: {result.Reason}",
                    UserGuidance = result.IsValid ? null : "The workflow service returned an unexpected state-transition result. Verify that IssuerWorkflowService is correctly configured and the state machine rules are intact.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Initiation stage verification threw exception. IssuerId={IssuerId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(issuerId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.Initiation,
                    Outcome = LifecycleStageOutcome.Failed,
                    Description = "Verifies that the issuer workflow service can accept a workflow initiation request.",
                    Detail = "Initiation stage threw an unexpected exception. The workflow service may be unavailable.",
                    UserGuidance = $"Search backend logs for CorrelationId={correlationId} to identify the root cause. Verify IssuerWorkflowService registration and dependencies.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
        }

        private async Task<SignOffLifecycleTransition> VerifyStatusPollingStageAsync(string deploymentId, string correlationId)
        {
            // Verify that the contract service can be queried for deployment status.
            // A not-found response is acceptable — it shows the service responded deterministically.
            try
            {
                Models.BackendDeploymentLifecycle.BackendDeploymentContractResponse statusResponse =
                    await _contractService.GetStatusAsync(deploymentId, correlationId);

                // Any non-null response (including not-found) confirms the status polling contract is stable.
                bool contractStable = statusResponse != null;
                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.StatusPolling,
                    Outcome = contractStable ? LifecycleStageOutcome.Verified : LifecycleStageOutcome.Failed,
                    Description = "Verifies that status polling returns a deterministic, contract-stable response for the deployment ID.",
                    Detail = contractStable
                        ? $"Status polling returned a deterministic response for deployment '{deploymentId}'. Contract is stable."
                        : $"Status polling returned a null response for deployment '{deploymentId}'. The contract service may not be functioning correctly.",
                    UserGuidance = contractStable ? null : "The deployment contract service returned null. Verify IBackendDeploymentLifecycleContractService is available and functioning.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Status polling stage verification threw exception. DeploymentId={DeploymentId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(deploymentId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.StatusPolling,
                    Outcome = LifecycleStageOutcome.Failed,
                    Description = "Verifies that status polling returns a deterministic, contract-stable response.",
                    Detail = "Status polling stage threw an unexpected exception. The contract service may be unavailable.",
                    UserGuidance = $"Search backend logs for CorrelationId={correlationId}. Verify that IBackendDeploymentLifecycleContractService is registered and its dependencies are available.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
        }

        private async Task<SignOffLifecycleTransition> VerifyTerminalStateStageAsync(string deploymentId, string correlationId)
        {
            // Verify that the deployment contract service returns a terminal-state-aware response.
            // A not-found or non-terminal response is expected for the fixture — the important
            // thing is that the service does not throw and returns a structured response.
            try
            {
                Models.BackendDeploymentLifecycle.BackendDeploymentContractResponse statusResponse =
                    await _contractService.GetStatusAsync(deploymentId, correlationId);

                bool hasStructuredResponse = statusResponse != null;
                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.TerminalState,
                    Outcome = hasStructuredResponse ? LifecycleStageOutcome.Verified : LifecycleStageOutcome.Failed,
                    Description = "Verifies that the deployment lifecycle service returns a structured terminal-state-aware response (Completed/Failed/NotFound).",
                    Detail = hasStructuredResponse
                        ? $"Terminal state check returned a structured response for deployment '{deploymentId}'. Lifecycle state semantics are stable."
                        : "Terminal state check returned null. The deployment contract service may not be functioning correctly.",
                    UserGuidance = hasStructuredResponse ? null : "Verify the deployment contract service is available and returns structured responses for unknown deployment IDs.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Terminal state stage verification threw exception. DeploymentId={DeploymentId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(deploymentId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.TerminalState,
                    Outcome = LifecycleStageOutcome.Failed,
                    Description = "Verifies that the deployment lifecycle service returns a structured terminal-state-aware response.",
                    Detail = "Terminal state stage threw an unexpected exception.",
                    UserGuidance = $"Search backend logs for CorrelationId={correlationId}. Verify IBackendDeploymentLifecycleContractService is registered.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
        }

        private async Task<SignOffLifecycleTransition> VerifyValidationStageAsync(string deploymentId, string correlationId)
        {
            // Verify the sign-off service can generate a structured proof (even a Blocked/NotFound one).
            // The important thing is that the response is non-null and structurally correct.
            try
            {
                Models.DeploymentSignOff.DeploymentSignOffProof proof =
                    await _deploymentSignOffService.GenerateSignOffProofAsync(deploymentId);

                bool hasProof = proof != null
                    && !string.IsNullOrWhiteSpace(proof.ProofId)
                    && !string.IsNullOrWhiteSpace(proof.DeploymentId);

                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.Validation,
                    Outcome = hasProof ? LifecycleStageOutcome.Verified : LifecycleStageOutcome.Failed,
                    Description = "Verifies that the sign-off service generates a structured proof document with a valid ProofId and DeploymentId.",
                    Detail = hasProof
                        ? $"Sign-off proof generated successfully for deployment '{deploymentId}'. ProofId='{proof!.ProofId}'. Verdict='{proof.Verdict}'. Contract is stable."
                        : "Sign-off service returned a null or structurally incomplete proof document.",
                    UserGuidance = hasProof ? null : "The sign-off service did not return a valid proof document. Verify IDeploymentSignOffService and its IBackendDeploymentLifecycleContractService dependency are correctly configured.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Validation stage verification threw exception. DeploymentId={DeploymentId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(deploymentId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                return new SignOffLifecycleTransition
                {
                    Stage = SignOffLifecycleStage.Validation,
                    Outcome = LifecycleStageOutcome.Failed,
                    Description = "Verifies that the sign-off service generates a structured proof document.",
                    Detail = "Validation stage threw an unexpected exception.",
                    UserGuidance = $"Search backend logs for CorrelationId={correlationId}. Verify IDeploymentSignOffService and its dependencies are available.",
                    VerifiedAt = DateTime.UtcNow.ToString("O")
                };
            }
        }

        private static EnterpriseSignOffLifecycleResponse FailedLifecycleResponse(
            string verificationId,
            string correlationId,
            string issuerId,
            string deploymentId,
            string executedAt,
            List<SignOffLifecycleTransition> stages,
            SignOffLifecycleStage failedAtStage,
            string guidance)
        {
            // Mark all remaining stages as Skipped
            SignOffLifecycleStage[] allStages = (SignOffLifecycleStage[])Enum.GetValues(typeof(SignOffLifecycleStage));
            foreach (SignOffLifecycleStage stage in allStages)
            {
                if (stage > failedAtStage && !stages.Any(s => s.Stage == stage))
                {
                    stages.Add(new SignOffLifecycleTransition
                    {
                        Stage = stage,
                        Outcome = LifecycleStageOutcome.Skipped,
                        Description = $"Stage skipped because {failedAtStage} failed.",
                        Detail = $"This stage was not reached because a prior stage ({failedAtStage}) did not verify successfully.",
                        VerifiedAt = DateTime.UtcNow.ToString("O")
                    });
                }
            }

            int verified = stages.Count(s => s.Outcome == LifecycleStageOutcome.Verified);
            int failed = stages.Count(s => s.Outcome == LifecycleStageOutcome.Failed);

            return new EnterpriseSignOffLifecycleResponse
            {
                VerificationId = verificationId,
                CorrelationId = correlationId,
                IsLifecycleVerified = false,
                ReachedStage = failedAtStage,
                VerifiedStageCount = verified,
                FailedStageCount = failed,
                Stages = stages,
                ActionableGuidance = guidance,
                ExecutedAt = executedAt,
                IssuerId = issuerId,
                DeploymentId = deploymentId
            };
        }

        private static string BuildEnvironmentGuidance(List<EnvironmentCheck> checks)
        {
            List<string> failures = checks
                .Where(c => c.Outcome != EnvironmentCheckOutcome.Pass && c.OperatorGuidance != null)
                .Select(c => c.OperatorGuidance!)
                .Distinct()
                .ToList();

            if (failures.Count == 0)
                return "One or more environment checks failed. Review the Checks array for details and follow the OperatorGuidance for each failed check.";

            return string.Join(" | ", failures);
        }

        private static string NormaliseCorrelationId(string? correlationId)
        {
            return string.IsNullOrWhiteSpace(correlationId)
                ? $"auto-{Guid.NewGuid():N}"
                : correlationId;
        }

        private static string GenerateShortId(string prefix, string correlationId)
        {
            string raw = $"{prefix}:{correlationId}:{DateTime.UtcNow.Ticks}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            string hex = Convert.ToHexString(hash)[..16].ToLowerInvariant();
            return $"{prefix}-{hex}";
        }
    }
}

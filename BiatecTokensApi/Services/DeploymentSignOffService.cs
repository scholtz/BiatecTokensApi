using System.Security.Cryptography;
using System.Text;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Models.DeploymentSignOff;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Validates deployment sign-off readiness and generates structured proof documents
    /// for enterprise sign-off journeys.
    ///
    /// <para>
    /// The service is fail-closed: if any required sign-off criterion cannot be satisfied
    /// (missing asset ID, missing transaction ID, non-terminal state, absent audit trail),
    /// the proof verdict is <see cref="SignOffVerdict.Blocked"/> and actionable,
    /// non-technical guidance is returned so a business operator can understand the
    /// problem without needing blockchain expertise.
    /// </para>
    ///
    /// <para>
    /// This service depends on <see cref="IBackendDeploymentLifecycleContractService"/>
    /// to retrieve deployment state and audit trail.
    /// </para>
    /// </summary>
    public class DeploymentSignOffService : IDeploymentSignOffService
    {
        private readonly IBackendDeploymentLifecycleContractService _contractService;
        private readonly ILogger<DeploymentSignOffService> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="DeploymentSignOffService"/>.
        /// </summary>
        public DeploymentSignOffService(
            IBackendDeploymentLifecycleContractService contractService,
            ILogger<DeploymentSignOffService> logger)
        {
            _contractService = contractService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<DeploymentSignOffProof> ValidateSignOffReadinessAsync(
            DeploymentSignOffValidationRequest request)
        {
            var correlationId = request?.CorrelationId ?? Guid.NewGuid().ToString();
            var proofId = GenerateProofId(request?.DeploymentId ?? string.Empty, correlationId);

            if (request == null || string.IsNullOrWhiteSpace(request.DeploymentId))
            {
                _logger.LogWarning(
                    "SignOff validation requested with missing DeploymentId. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(correlationId));

                return BuildBlockedProof(
                    proofId,
                    string.Empty,
                    correlationId,
                    new List<SignOffCriterion>
                    {
                        BuildCriterion(
                            "DeploymentId",
                            "Deployment identifier must be present to evaluate sign-off readiness.",
                            SignOffCriterionCategory.LifecycleIntegrity,
                            SignOffCriterionOutcome.Fail,
                            "Deployment ID is missing or empty.",
                            "Please provide the deployment ID returned when the deployment was initiated.",
                            required: true)
                    },
                    "No deployment ID was provided. Supply the deployment ID returned by the initiation endpoint.",
                    null);
            }

            var deploymentId = request.DeploymentId;

            _logger.LogInformation(
                "Evaluating sign-off readiness: DeploymentId={DeploymentId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(deploymentId),
                LoggingHelper.SanitizeLogInput(correlationId));

            // Fetch deployment status and audit trail in parallel
            var statusTask = _contractService.GetStatusAsync(deploymentId, correlationId);
            var auditTask = _contractService.GetAuditTrailAsync(deploymentId, correlationId);
            await Task.WhenAll(statusTask, auditTask);

            var status = await statusTask;
            var audit = await auditTask;

            // Check if the deployment was not found
            bool deploymentNotFound = status.ErrorCode == DeploymentErrorCode.RequiredFieldMissing &&
                                      status.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
            if (deploymentNotFound)
            {
                _logger.LogWarning(
                    "Deployment not found for sign-off: DeploymentId={DeploymentId}",
                    LoggingHelper.SanitizeLogInput(deploymentId));

                return BuildBlockedProof(
                    proofId, deploymentId, correlationId,
                    new List<SignOffCriterion>
                    {
                        BuildCriterion(
                            "DeploymentId",
                            "Deployment must exist before sign-off can be evaluated.",
                            SignOffCriterionCategory.LifecycleIntegrity,
                            SignOffCriterionOutcome.Fail,
                            $"Deployment '{deploymentId}' was not found.",
                            "Check that the deployment ID is correct. If the deployment was never initiated, start the deployment process first.",
                            required: true)
                    },
                    "The deployment record was not found. Verify the deployment ID and ensure the deployment was initiated.",
                    null);
            }

            // Evaluate all criteria
            var criteria = new List<SignOffCriterion>();

            // ── Criterion 1: Lifecycle terminal state ────────────────────────────
            criteria.Add(EvaluateTerminalStateCriterion(status));

            // ── Criterion 2: Deployer identity ───────────────────────────────────
            criteria.Add(EvaluateDeployerIdentityCriterion(status));

            // ── Criterion 3: Blockchain evidence – AssetId ───────────────────────
            if (request.RequireAssetId)
                criteria.Add(EvaluateAssetIdCriterion(status));

            // ── Criterion 4: Blockchain evidence – TransactionId ─────────────────
            if (request.RequireTransactionId)
                criteria.Add(EvaluateTransactionIdCriterion(status));

            // ── Criterion 5: Blockchain evidence – ConfirmedRound ────────────────
            if (request.RequireConfirmedRound)
                criteria.Add(EvaluateConfirmedRoundCriterion(status));

            // ── Criterion 6: Audit trail completeness ────────────────────────────
            if (request.RequireAuditTrail)
                criteria.Add(EvaluateAuditTrailCriterion(audit));

            // ── Criterion 7: No degraded-mode deployment ─────────────────────────
            criteria.Add(EvaluateDegradedModeCriterion(status));

            // ── Evaluate overall verdict ─────────────────────────────────────────
            var failedRequired = criteria.Where(c => c.IsRequired && c.Outcome == SignOffCriterionOutcome.Fail).ToList();
            var passedRequired = criteria.Where(c => c.IsRequired && c.Outcome == SignOffCriterionOutcome.Pass).ToList();

            bool isReady = failedRequired.Count == 0;
            SignOffVerdict verdict;

            if (status.State == ContractLifecycleState.Failed)
                verdict = SignOffVerdict.TerminalFailure;
            else if (status.State != ContractLifecycleState.Completed)
                verdict = SignOffVerdict.InProgress;
            else if (isReady)
                verdict = SignOffVerdict.Approved;
            else
                verdict = SignOffVerdict.Blocked;

            string? actionableGuidance = BuildActionableGuidance(verdict, failedRequired);

            bool hasAuditTrail = audit.Events.Count > 0;

            var proof = new DeploymentSignOffProof
            {
                ProofId = proofId,
                DeploymentId = deploymentId,
                CorrelationId = correlationId,
                IsReadyForSignOff = verdict == SignOffVerdict.Approved,
                Verdict = verdict,
                PassedCriteriaCount = passedRequired.Count,
                FailedCriteriaCount = failedRequired.Count,
                TotalCriteriaCount = criteria.Count,
                Criteria = criteria,
                ActionableGuidance = actionableGuidance,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                TokenStandard = audit.TokenStandard,
                Network = audit.Network,
                AssetId = status.AssetId,
                TransactionId = status.TransactionId,
                ConfirmedRound = status.ConfirmedRound,
                DeployerAddress = status.DeployerAddress ?? audit.DeployerAddress,
                IsDeterministicAddress = status.IsDeterministicAddress,
                HasAuditTrail = hasAuditTrail,
                AuditEventCount = audit.Events.Count,
                DeploymentInitiatedAt = string.IsNullOrEmpty(status.InitiatedAt) ? null : status.InitiatedAt,
                DeploymentLastUpdatedAt = string.IsNullOrEmpty(status.LastUpdatedAt) ? null : status.LastUpdatedAt
            };

            _logger.LogInformation(
                "Sign-off evaluation complete: DeploymentId={DeploymentId}, Verdict={Verdict}, " +
                "Passed={Passed}, Failed={Failed}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(deploymentId),
                verdict,
                passedRequired.Count,
                failedRequired.Count,
                LoggingHelper.SanitizeLogInput(correlationId));

            return proof;
        }

        /// <inheritdoc/>
        public Task<DeploymentSignOffProof> GenerateSignOffProofAsync(string deploymentId)
        {
            return ValidateSignOffReadinessAsync(new DeploymentSignOffValidationRequest
            {
                DeploymentId = deploymentId,
                RequireAssetId = true,
                RequireConfirmedRound = true,
                RequireTransactionId = true,
                RequireAuditTrail = true
            });
        }

        // ── Criterion evaluators ──────────────────────────────────────────────────

        private static SignOffCriterion EvaluateTerminalStateCriterion(BackendDeploymentContractResponse status)
        {
            if (status.State == ContractLifecycleState.Completed)
            {
                return BuildCriterion(
                    "LifecycleTerminalState",
                    "Deployment must have reached the Completed terminal state.",
                    SignOffCriterionCategory.LifecycleIntegrity,
                    SignOffCriterionOutcome.Pass,
                    "Deployment is in the Completed terminal state.",
                    null, required: true);
            }

            if (status.State == ContractLifecycleState.Failed)
            {
                return BuildCriterion(
                    "LifecycleTerminalState",
                    "Deployment must have reached the Completed terminal state.",
                    SignOffCriterionCategory.LifecycleIntegrity,
                    SignOffCriterionOutcome.Fail,
                    $"Deployment ended in a terminal failure: {status.Message}",
                    "The deployment failed. Review the error details and contact support if you need assistance restarting the deployment process.",
                    required: true);
            }

            if (status.State == ContractLifecycleState.Cancelled)
            {
                return BuildCriterion(
                    "LifecycleTerminalState",
                    "Deployment must have reached the Completed terminal state.",
                    SignOffCriterionCategory.LifecycleIntegrity,
                    SignOffCriterionOutcome.Fail,
                    "Deployment was cancelled before completing.",
                    "The deployment was cancelled. A new deployment must be initiated to proceed with sign-off.",
                    required: true);
            }

            // Non-terminal in-progress states
            return BuildCriterion(
                "LifecycleTerminalState",
                "Deployment must have reached the Completed terminal state.",
                SignOffCriterionCategory.LifecycleIntegrity,
                SignOffCriterionOutcome.Fail,
                $"Deployment is still in progress (current state: {status.State}). Sign-off requires a completed deployment.",
                "The deployment has not finished yet. Please wait for the deployment to complete before requesting sign-off.",
                required: true);
        }

        private static SignOffCriterion EvaluateDeployerIdentityCriterion(BackendDeploymentContractResponse status)
        {
            if (string.IsNullOrWhiteSpace(status.DeployerAddress))
            {
                return BuildCriterion(
                    "DeployerAddress",
                    "Deployer address must be recorded in the deployment to establish identity.",
                    SignOffCriterionCategory.DeployerIdentity,
                    SignOffCriterionOutcome.Fail,
                    "Deployer address is absent from the deployment record.",
                    "The deployer's blockchain address was not captured. This is required to prove who authorised the deployment. Re-initiate the deployment with valid credentials.",
                    required: true);
            }

            return BuildCriterion(
                "DeployerAddress",
                "Deployer address must be recorded in the deployment to establish identity.",
                SignOffCriterionCategory.DeployerIdentity,
                SignOffCriterionOutcome.Pass,
                $"Deployer address is present: {status.DeployerAddress[..Math.Min(8, status.DeployerAddress.Length)]}…",
                null, required: true);
        }

        private static SignOffCriterion EvaluateAssetIdCriterion(BackendDeploymentContractResponse status)
        {
            if (!status.AssetId.HasValue || status.AssetId == 0)
            {
                return BuildCriterion(
                    "AssetId",
                    "On-chain asset ID must be present to prove the token was created on the blockchain.",
                    SignOffCriterionCategory.BlockchainEvidence,
                    SignOffCriterionOutcome.Fail,
                    "On-chain asset ID is missing from the deployment record.",
                    "The token's blockchain asset ID was not recorded. This is a required proof of token creation. If the deployment appeared to succeed, contact support to retrieve the asset ID from the blockchain.",
                    required: true);
            }

            return BuildCriterion(
                "AssetId",
                "On-chain asset ID must be present to prove the token was created on the blockchain.",
                SignOffCriterionCategory.BlockchainEvidence,
                SignOffCriterionOutcome.Pass,
                $"On-chain asset ID confirmed: {status.AssetId}.",
                null, required: true);
        }

        private static SignOffCriterion EvaluateTransactionIdCriterion(BackendDeploymentContractResponse status)
        {
            if (string.IsNullOrWhiteSpace(status.TransactionId))
            {
                return BuildCriterion(
                    "TransactionId",
                    "On-chain transaction ID must be present to provide an immutable deployment reference.",
                    SignOffCriterionCategory.BlockchainEvidence,
                    SignOffCriterionOutcome.Fail,
                    "Transaction ID is absent from the deployment record.",
                    "The blockchain transaction reference is missing. This is needed for audit and compliance purposes. Contact support to verify the transaction ID from the blockchain.",
                    required: true);
            }

            return BuildCriterion(
                "TransactionId",
                "On-chain transaction ID must be present to provide an immutable deployment reference.",
                SignOffCriterionCategory.BlockchainEvidence,
                SignOffCriterionOutcome.Pass,
                $"Transaction ID confirmed: {status.TransactionId[..Math.Min(12, status.TransactionId.Length)]}…",
                null, required: true);
        }

        private static SignOffCriterion EvaluateConfirmedRoundCriterion(BackendDeploymentContractResponse status)
        {
            if (!status.ConfirmedRound.HasValue || status.ConfirmedRound == 0)
            {
                return BuildCriterion(
                    "ConfirmedRound",
                    "Confirmed on-chain round/block must be present to prove network finality.",
                    SignOffCriterionCategory.BlockchainEvidence,
                    SignOffCriterionOutcome.Fail,
                    "Confirmed round/block is absent from the deployment record.",
                    "The block number at which the deployment was confirmed is missing. This proves the transaction reached network finality. Contact support if you believe the deployment succeeded.",
                    required: true);
            }

            return BuildCriterion(
                "ConfirmedRound",
                "Confirmed on-chain round/block must be present to prove network finality.",
                SignOffCriterionCategory.BlockchainEvidence,
                SignOffCriterionOutcome.Pass,
                $"Network finality confirmed at round {status.ConfirmedRound}.",
                null, required: true);
        }

        private static SignOffCriterion EvaluateAuditTrailCriterion(BackendDeploymentAuditTrail audit)
        {
            if (audit.Events.Count == 0)
            {
                return BuildCriterion(
                    "AuditTrail",
                    "Deployment must have a non-empty audit trail to support compliance reporting.",
                    SignOffCriterionCategory.AuditTrail,
                    SignOffCriterionOutcome.Fail,
                    "No audit events were recorded for this deployment.",
                    "The deployment audit trail is empty. Compliance reporting requires evidence of lifecycle events. Contact support for assistance.",
                    required: true);
            }

            return BuildCriterion(
                "AuditTrail",
                "Deployment must have a non-empty audit trail to support compliance reporting.",
                SignOffCriterionCategory.AuditTrail,
                SignOffCriterionOutcome.Pass,
                $"Audit trail is present with {audit.Events.Count} lifecycle event(s).",
                null, required: true);
        }

        private static SignOffCriterion EvaluateDegradedModeCriterion(BackendDeploymentContractResponse status)
        {
            if (status.IsDegraded)
            {
                return BuildCriterion(
                    "NoDegradedMode",
                    "Deployment must not be in degraded mode for enterprise sign-off.",
                    SignOffCriterionCategory.BlockchainEvidence,
                    SignOffCriterionOutcome.Fail,
                    "Deployment completed in degraded mode. Degraded-mode evidence is not acceptable for sign-off.",
                    "The deployment used a fallback or degraded evidence path. For enterprise sign-off, the deployment must be re-run with a reliable blockchain connection. Contact support for guidance.",
                    required: true);
            }

            return BuildCriterion(
                "NoDegradedMode",
                "Deployment must not be in degraded mode for enterprise sign-off.",
                SignOffCriterionCategory.BlockchainEvidence,
                SignOffCriterionOutcome.Pass,
                "Deployment completed without degraded-mode fallbacks.",
                null, required: true);
        }

        // ── Builders ─────────────────────────────────────────────────────────────

        private static SignOffCriterion BuildCriterion(
            string name,
            string description,
            SignOffCriterionCategory category,
            SignOffCriterionOutcome outcome,
            string detail,
            string? userGuidance,
            bool required)
        {
            return new SignOffCriterion
            {
                Name = name,
                Description = description,
                Category = category,
                Outcome = outcome,
                Detail = detail,
                UserGuidance = userGuidance,
                IsRequired = required
            };
        }

        private static DeploymentSignOffProof BuildBlockedProof(
            string proofId,
            string deploymentId,
            string correlationId,
            List<SignOffCriterion> criteria,
            string actionableGuidance,
            BackendDeploymentContractResponse? status)
        {
            return new DeploymentSignOffProof
            {
                ProofId = proofId,
                DeploymentId = deploymentId,
                CorrelationId = correlationId,
                IsReadyForSignOff = false,
                Verdict = SignOffVerdict.Blocked,
                PassedCriteriaCount = criteria.Count(c => c.Outcome == SignOffCriterionOutcome.Pass),
                FailedCriteriaCount = criteria.Count(c => c.Outcome == SignOffCriterionOutcome.Fail),
                TotalCriteriaCount = criteria.Count,
                Criteria = criteria,
                ActionableGuidance = actionableGuidance,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                AssetId = status?.AssetId,
                TransactionId = status?.TransactionId,
                ConfirmedRound = status?.ConfirmedRound,
                DeployerAddress = status?.DeployerAddress,
                IsDeterministicAddress = status?.IsDeterministicAddress ?? false,
                HasAuditTrail = false,
                AuditEventCount = 0
            };
        }

        private static string? BuildActionableGuidance(
            SignOffVerdict verdict,
            List<SignOffCriterion> failedCriteria)
        {
            if (verdict == SignOffVerdict.Approved)
                return null;

            if (verdict == SignOffVerdict.TerminalFailure)
                return "The deployment ended in a terminal failure and cannot proceed to sign-off. " +
                       "Review the error details, correct the underlying issue, and initiate a new deployment.";

            if (verdict == SignOffVerdict.InProgress)
                return "The deployment is still in progress. Sign-off can only be evaluated once the deployment reaches the Completed state. Please wait and try again.";

            if (failedCriteria.Count == 0)
                return null;

            var guidanceItems = failedCriteria
                .Where(c => c.UserGuidance != null)
                .Select(c => $"• {c.Name}: {c.UserGuidance}")
                .ToList();

            if (guidanceItems.Count == 0)
                return "One or more sign-off criteria failed. Review the criteria list for details.";

            return "The following items must be resolved before sign-off can be approved:\n" +
                   string.Join("\n", guidanceItems);
        }

        private static string GenerateProofId(string deploymentId, string correlationId)
        {
            var input = $"{deploymentId}:{correlationId}:{DateTime.UtcNow:o}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return $"proof-{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
        }
    }
}

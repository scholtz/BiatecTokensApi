using BiatecTokensApi.Models.DeploymentSignOff;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service that validates deployment sign-off readiness and generates structured
    /// proof documents for enterprise sign-off journeys.
    ///
    /// <para>
    /// The sign-off service is the backend's authoritative check that a deployment
    /// lifecycle has completed with all required evidence fields present. It evaluates
    /// each criterion in a fail-closed manner: if any required criterion fails, sign-off
    /// is blocked and actionable guidance is returned so a non-technical operator can
    /// understand the problem and take corrective action.
    /// </para>
    /// </summary>
    public interface IDeploymentSignOffService
    {
        /// <summary>
        /// Validates whether a deployment is ready for enterprise sign-off.
        ///
        /// <para>
        /// Evaluates all configured sign-off criteria against the deployment record.
        /// Missing required fields (AssetId, TransactionId, ConfirmedRound, DeployerAddress)
        /// are treated as explicit failures, not silently ignored.
        /// </para>
        /// </summary>
        /// <param name="request">
        /// Validation request specifying the deployment ID and which criteria are required.
        /// </param>
        /// <returns>
        /// A structured proof document with per-criterion outcomes, an overall verdict,
        /// and actionable guidance for any failures.
        /// </returns>
        Task<DeploymentSignOffProof> ValidateSignOffReadinessAsync(
            DeploymentSignOffValidationRequest request);

        /// <summary>
        /// Generates a sign-off proof document for a deployment without an explicit
        /// criteria request. Uses default criteria (all required).
        /// </summary>
        /// <param name="deploymentId">Deployment contract ID to generate proof for.</param>
        /// <returns>Structured sign-off proof document.</returns>
        Task<DeploymentSignOffProof> GenerateSignOffProofAsync(string deploymentId);
    }
}

using BiatecTokensApi.Models.ASA;

namespace BiatecTokensApi.Models.AVM
{
    /// <summary>
    /// Represents the response of a token deployment operation in the AVM (Algorand Virtual Machine).
    /// </summary>
    /// <remarks>This class provides details about the outcome of a token deployment, including the
    /// transaction ID, asset ID, creator address, and deployment status. If the deployment fails, an error message is
    /// provided.</remarks>
    public class AVMTokenDeploymentResponse : BaseResponse
    {
        /// <summary>
        /// Transaction ID of the asset creation
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Asset ID of the created token
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Creator account address
        /// </summary>
        public string? CreatorAddress { get; set; }

        /// <summary>
        /// Round number when the transaction was confirmed
        /// </summary>
        public ulong? ConfirmedRound { get; set; }
    }
}

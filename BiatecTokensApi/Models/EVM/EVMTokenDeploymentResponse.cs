namespace BiatecTokensApi.Models.EVM
{

    /// <summary>
    /// Represents the response of an Ethereum Virtual Machine (EVM) token deployment operation.
    /// </summary>
    /// <remarks>This class encapsulates the details of a token deployment, including the transaction hash, 
    /// the deployed contract address, the status of the deployment, and any error messages if the  deployment failed.
    /// It also provides information about the address that received the initial  token supply.</remarks>
    public class EVMTokenDeploymentResponse : BaseResponse
    {
        /// <summary>
        /// Transaction hash of the deployment
        /// </summary>
        public required string TransactionHash { get; set; }
    }
}

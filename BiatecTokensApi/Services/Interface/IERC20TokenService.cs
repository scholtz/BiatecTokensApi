using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ERC20.Response;
using BiatecTokensApi.Models.EVM;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Defines methods for interacting with and managing ERC20 tokens, including deployment functionality.
    /// </summary>
    /// <remarks>This interface provides an abstraction for operations related to ERC20 tokens, such as
    /// deploying new tokens. Implementations of this interface are expected to handle the underlying blockchain
    /// interactions required for these operations.</remarks>
    public interface IERC20TokenService
    {
        /// <summary>
        /// Deploys an ERC20 token
        /// </summary>
        /// <param name="request">Token deployment parameters</param>
        /// <param name="tokenType">Token type</param>
        /// <returns>Response with transaction details</returns>
        Task<ERC20TokenDeploymentResponse> DeployERC20TokenAsync(ERC20TokenDeploymentRequest request, TokenType tokenType);
    }
}
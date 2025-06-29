using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services
{
    public interface IERC20TokenService
    {
        /// <summary>
        /// Deploys an ERC20 token to the Base blockchain
        /// </summary>
        /// <param name="request">Token deployment parameters</param>
        /// <returns>Response with transaction details</returns>
        Task<TokenDeploymentResponse> DeployTokenAsync(TokenDeploymentRequest request);
    }
}
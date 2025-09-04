using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC200.Response;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for deploying ARC200 tokens on the blockchain
    /// </summary>
    public interface IARC200TokenService
    {
        /// <summary>
        /// Deploys an ARC200 token
        /// </summary>
        /// <param name="request">Token deployment parameters</param>
        /// <param name="tokenType">Token type</param>
        /// <returns>Response with transaction details</returns>
        Task<ARC200TokenDeploymentResponse> CreateARC200TokenAsync(ARC200TokenDeploymentRequest request, TokenType tokenType);
    }
}
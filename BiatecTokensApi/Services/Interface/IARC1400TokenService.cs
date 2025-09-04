using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Models.ARC1400.Response;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for deploying ARC1400 tokens on the blockchain
    /// </summary>
    public interface IARC1400TokenService
    {
        /// <summary>
        /// Deploys an ARC1400 token
        /// </summary>
        /// <param name="request">Token deployment parameters</param>
        /// <param name="tokenType">Token type</param>
        /// <returns>Response with transaction details</returns>
        Task<ARC1400TokenDeploymentResponse> CreateARC1400TokenAsync(ARC1400TokenDeploymentRequest request, TokenType tokenType);
    }
}
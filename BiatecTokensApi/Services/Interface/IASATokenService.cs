using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC200.Response;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for ARC3 Fungible Token Service operations on Algorand blockchain
    /// </summary>
    public interface IASATokenService
    {
        /// <summary>
        /// Creates an ASA token on Algorand type blockchain
        /// </summary>
        /// <param name="request">Token creation parameters</param>
        /// <param name="tokenType">Token type</param>
        /// <returns>Response with transaction details and asset ID</returns>
        Task<ASATokenDeploymentResponse> CreateASATokenAsync(ASABaseTokenDeploymentRequest request, TokenType tokenType);

    }
}
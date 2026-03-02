using BiatecTokensApi.Models.TokenLaunch;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for previewing and validating token configurations before deployment
    /// </summary>
    public interface ITokenConfigPreviewService
    {
        /// <summary>
        /// Previews a token configuration with completeness scoring and guided validation
        /// </summary>
        /// <param name="request">Token configuration preview request</param>
        /// <returns>Preview result with completeness score, field issues, and improvement guidance</returns>
        Task<TokenConfigPreviewResponse> PreviewConfigAsync(TokenConfigPreviewRequest request);

        /// <summary>
        /// Computes a trust score for an existing deployed token
        /// </summary>
        /// <param name="tokenIdentifier">Token asset ID or contract address</param>
        /// <param name="network">Network the token is deployed on</param>
        /// <returns>Trust score response with breakdown and signals</returns>
        Task<TokenTrustScoreResponse> ComputeTrustScoreAsync(string tokenIdentifier, string network);
    }
}

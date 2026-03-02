using BiatecTokensApi.Models.Wallet;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for computing wallet routing options to optimize cross-network operations
    /// </summary>
    public interface IWalletRoutingService
    {
        /// <summary>
        /// Returns optimized routing options for a cross-network wallet operation
        /// </summary>
        /// <param name="request">Routing request with source network, target network, and operation type</param>
        /// <returns>Routing response with recommended and available routes</returns>
        Task<WalletRoutingResponse> GetRoutingOptionsAsync(WalletRoutingRequest request);
    }
}

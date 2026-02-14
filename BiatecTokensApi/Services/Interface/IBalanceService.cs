using BiatecTokensApi.Models.Balance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for querying token balances across different blockchain networks
    /// </summary>
    public interface IBalanceService
    {
        /// <summary>
        /// Query balance for a specific token and address
        /// </summary>
        /// <param name="request">Balance query request</param>
        /// <returns>Balance information including formatted balance and token metadata</returns>
        Task<BalanceQueryResponse> GetBalanceAsync(BalanceQueryRequest request);

        /// <summary>
        /// Query balances for multiple tokens for a single address
        /// </summary>
        /// <param name="request">Multi-balance query request</param>
        /// <returns>List of token balances for the address</returns>
        Task<MultiBalanceQueryResponse> GetMultipleBalancesAsync(MultiBalanceQueryRequest request);
    }
}

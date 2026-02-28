using BiatecTokensApi.Models.Account;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for account management operations (ARC76 address lookup, testnet funding, balance queries)
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Gets the ARC76-derived Algorand address for the authenticated user
        /// </summary>
        /// <param name="userId">The authenticated user's ID</param>
        /// <param name="correlationId">Correlation ID for tracing</param>
        /// <returns>Account address response with the Algorand address</returns>
        Task<AccountAddressResponse> GetAddressAsync(string userId, string correlationId);

        /// <summary>
        /// Requests testnet ALGO funding for the user's Algorand address
        /// </summary>
        /// <param name="userId">The authenticated user's ID</param>
        /// <param name="network">The target testnet network</param>
        /// <param name="correlationId">Correlation ID for tracing</param>
        /// <returns>Fund response indicating if the request was submitted</returns>
        Task<AccountFundResponse> RequestTestnetFundingAsync(string userId, string network, string correlationId);

        /// <summary>
        /// Gets ALGO and token balances for the user's Algorand account
        /// </summary>
        /// <param name="userId">The authenticated user's ID</param>
        /// <param name="network">The target network to query</param>
        /// <param name="correlationId">Correlation ID for tracing</param>
        /// <returns>Balance response with ALGO and token amounts</returns>
        Task<AccountBalanceResponse> GetBalanceAsync(string userId, string network, string correlationId);
    }
}

using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Account;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing user Algorand accounts derived via ARC76
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AccountService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountService"/> class.
        /// </summary>
        public AccountService(
            IUserRepository userRepository,
            ILogger<AccountService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _userRepository = userRepository;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public async Task<AccountAddressResponse> GetAddressAsync(string userId, string correlationId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("GetAddress: user not found. UserId={UserId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId), correlationId);
                    return new AccountAddressResponse
                    {
                        Success = false,
                        ErrorMessage = "User not found",
                        CorrelationId = correlationId
                    };
                }

                _logger.LogInformation("GetAddress: address retrieved. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);

                return new AccountAddressResponse
                {
                    Success = true,
                    AlgorandAddress = user.AlgorandAddress,
                    DerivationStandard = "ARC76",
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAddress: unexpected error. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);
                return new AccountAddressResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving the account address",
                    CorrelationId = correlationId
                };
            }
        }

        /// <inheritdoc />
        public async Task<AccountFundResponse> RequestTestnetFundingAsync(string userId, string network, string correlationId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("RequestTestnetFunding: user not found. UserId={UserId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId), correlationId);
                    return new AccountFundResponse
                    {
                        Success = false,
                        ErrorMessage = "User not found",
                        CorrelationId = correlationId
                    };
                }

                if (string.IsNullOrWhiteSpace(user.AlgorandAddress))
                {
                    return new AccountFundResponse
                    {
                        Success = false,
                        ErrorMessage = "Account address not available",
                        CorrelationId = correlationId
                    };
                }

                // Only support testnet funding
                if (!network.Contains("testnet", StringComparison.OrdinalIgnoreCase))
                {
                    return new AccountFundResponse
                    {
                        Success = false,
                        ErrorMessage = "Funding is only available on testnet networks",
                        Network = network,
                        AlgorandAddress = user.AlgorandAddress,
                        CorrelationId = correlationId
                    };
                }

                // Call the Algorand testnet faucet
                var funded = await CallAlgorandFaucetAsync(user.AlgorandAddress, correlationId);

                _logger.LogInformation(
                    "RequestTestnetFunding: faucet call completed. UserId={UserId}, Address={Address}, Success={Success}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(user.AlgorandAddress),
                    funded,
                    correlationId);

                return new AccountFundResponse
                {
                    Success = funded,
                    AlgorandAddress = user.AlgorandAddress,
                    Network = network,
                    Message = funded
                        ? "Testnet funding request submitted successfully"
                        : "Faucet request could not be completed at this time",
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestTestnetFunding: unexpected error. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);
                return new AccountFundResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while requesting testnet funding",
                    Network = network,
                    CorrelationId = correlationId
                };
            }
        }

        /// <inheritdoc />
        public async Task<AccountBalanceResponse> GetBalanceAsync(string userId, string network, string correlationId)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("GetBalance: user not found. UserId={UserId}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(userId), correlationId);
                    return new AccountBalanceResponse
                    {
                        Success = false,
                        ErrorMessage = "User not found",
                        Network = network,
                        CorrelationId = correlationId
                    };
                }

                _logger.LogInformation("GetBalance: balance queried. UserId={UserId}, Network={Network}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(network),
                    correlationId);

                // Return address with zero balances — live on-chain query is handled by network-specific integrations
                return new AccountBalanceResponse
                {
                    Success = true,
                    AlgorandAddress = user.AlgorandAddress,
                    AlgoBalanceMicroAlgos = 0,
                    AlgoBalance = 0,
                    TokenBalances = new List<TokenBalance>(),
                    Network = network,
                    CorrelationId = correlationId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBalance: unexpected error. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId), correlationId);
                return new AccountBalanceResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving the account balance",
                    Network = network,
                    CorrelationId = correlationId
                };
            }
        }

        /// <summary>
        /// Calls the Algorand testnet faucet for the given address
        /// </summary>
        private async Task<bool> CallAlgorandFaucetAsync(string address, string correlationId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AlgorandFaucet");
                var faucetUrl = $"https://bank.testnet.algorand.network/?account={Uri.EscapeDataString(address)}";
                var response = await client.GetAsync(faucetUrl);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Faucet call failed. Address={Address}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(address), correlationId);
                return false;
            }
        }
    }
}

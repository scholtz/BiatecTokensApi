using Algorand;
using Algorand.Algod;
using Algorand.Algod.Model;
using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Balance;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using System.Numerics;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for querying token balances across Algorand and EVM chains
    /// </summary>
    public class BalanceService : IBalanceService
    {
        private readonly AlgorandAuthenticationOptionsV2 _algorandConfig;
        private readonly EVMChains _evmConfig;
        private readonly ILogger<BalanceService> _logger;

        public BalanceService(
            IOptions<AlgorandAuthenticationOptionsV2> algorandConfig,
            IOptions<EVMChains> evmConfig,
            ILogger<BalanceService> logger)
        {
            _algorandConfig = algorandConfig.Value;
            _evmConfig = evmConfig.Value;
            _logger = logger;
        }

        /// <summary>
        /// Query balance for a specific token and address
        /// </summary>
        public async Task<BalanceQueryResponse> GetBalanceAsync(BalanceQueryRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Querying balance: Token={Token}, Address={Address}, Chain={Chain}",
                    LoggingHelper.SanitizeLogInput(request.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(request.Address),
                    LoggingHelper.SanitizeLogInput(request.Chain)
                );

                // Determine chain type
                if (request.Chain.Contains("algorand") || request.Chain.Contains("voi") || request.Chain.Contains("aramid"))
                {
                    return await GetAlgorandBalanceAsync(request);
                }
                else if (request.Chain.Contains("base") || request.Chain.Contains("ethereum") || request.Chain.Contains("evm"))
                {
                    return await GetEVMBalanceAsync(request);
                }
                else
                {
                    return new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_NETWORK,
                        ErrorMessage = $"Unsupported chain: {request.Chain}",
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying balance: {Message}", ex.Message);
                return new BalanceQueryResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "Failed to query balance",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Query balances for multiple tokens for a single address
        /// </summary>
        public async Task<MultiBalanceQueryResponse> GetMultipleBalancesAsync(MultiBalanceQueryRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Querying multiple balances: Address={Address}, Chain={Chain}, TokenCount={Count}",
                    LoggingHelper.SanitizeLogInput(request.Address),
                    LoggingHelper.SanitizeLogInput(request.Chain),
                    request.TokenIdentifiers?.Count ?? 0
                );

                var balances = new List<TokenBalance>();

                if (request.TokenIdentifiers != null && request.TokenIdentifiers.Any())
                {
                    // Query specific tokens
                    foreach (var tokenId in request.TokenIdentifiers)
                    {
                        var balanceRequest = new BalanceQueryRequest
                        {
                            TokenIdentifier = tokenId,
                            Address = request.Address,
                            Chain = request.Chain
                        };

                        var result = await GetBalanceAsync(balanceRequest);
                        if (result.Success)
                        {
                            if (request.IncludeZeroBalances || result.Balance != "0")
                            {
                                balances.Add(new TokenBalance
                                {
                                    TokenIdentifier = result.TokenIdentifier ?? tokenId,
                                    Name = result.Name,
                                    Symbol = result.Symbol,
                                    Decimals = result.Decimals ?? 0,
                                    Balance = result.Balance ?? "0",
                                    FormattedBalance = result.FormattedBalance ?? "0",
                                    IsOptedIn = result.IsOptedIn,
                                    IsFrozen = result.IsFrozen
                                });
                            }
                        }
                    }
                }

                return new MultiBalanceQueryResponse
                {
                    Success = true,
                    Address = request.Address,
                    Chain = request.Chain,
                    Balances = balances,
                    TotalTokens = balances.Count,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying multiple balances: {Message}", ex.Message);
                return new MultiBalanceQueryResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "Failed to query balances",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private async Task<BalanceQueryResponse> GetAlgorandBalanceAsync(BalanceQueryRequest request)
        {
            try
            {
                // Parse asset ID
                if (!ulong.TryParse(request.TokenIdentifier, out var assetId))
                {
                    return new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.InvalidRequest,
                        ErrorMessage = "Invalid asset ID for Algorand",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Find network configuration
                AlgodConfig? network = null;
                foreach (var net in _algorandConfig.AllowedNetworks)
                {
                    if (request.Chain.Contains(net.Key))
                    {
                        network = net.Value;
                        break;
                    }
                }

                if (network == null)
                {
                    return new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_NETWORK,
                        ErrorMessage = $"Network configuration not found for: {request.Chain}",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Create Algorand client
                using var httpClient = HttpClientConfigurator.ConfigureHttpClient(network.Server, network.Token, network.Header);
                var algodApi = new DefaultApi(httpClient);

                // Get account information
                var accountInfo = await algodApi.AccountInformationAsync(request.Address);

                // Find asset holding
                var assetHolding = accountInfo.Assets?.FirstOrDefault(a => a.AssetId == assetId);
                var isOptedIn = assetHolding != null;
                var balance = assetHolding?.Amount.ToString() ?? "0";
                var isFrozen = assetHolding?.IsFrozen ?? false;

                // Get asset information
                Asset? assetInfo = null;
                try
                {
                    assetInfo = await algodApi.GetAssetByIDAsync(assetId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch asset info for asset {AssetId}", assetId);
                }

                var decimals = (int)(assetInfo?.Params?.Decimals ?? 0);
                var name = assetInfo?.Params?.Name;
                var symbol = assetInfo?.Params?.UnitName;

                // Format balance
                var formattedBalance = FormatBalance(balance, decimals);

                return new BalanceQueryResponse
                {
                    Success = true,
                    TokenIdentifier = assetId.ToString(),
                    Address = request.Address,
                    Chain = request.Chain,
                    Balance = balance,
                    Decimals = decimals,
                    FormattedBalance = formattedBalance,
                    Symbol = symbol,
                    Name = name,
                    IsOptedIn = isOptedIn,
                    IsFrozen = isFrozen,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Algorand balance: {Message}", ex.Message);
                return new BalanceQueryResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                    ErrorMessage = $"Failed to query Algorand balance: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private async Task<BalanceQueryResponse> GetEVMBalanceAsync(BalanceQueryRequest request)
        {
            try
            {
                // Find EVM chain configuration
                var chain = _evmConfig.Chains?
                    .FirstOrDefault(c => request.Chain.Contains(c.ChainId.ToString()));

                if (chain == null)
                {
                    return new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_NETWORK,
                        ErrorMessage = $"EVM chain configuration not found for: {request.Chain}",
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Create Web3 client
                var web3 = new Web3(chain.RpcUrl);

                // Get ERC20 contract
                var erc20 = web3.Eth.ERC20.GetContractService(request.TokenIdentifier);

                // Query balance
                var balance = await erc20.BalanceOfQueryAsync(request.Address);
                var balanceString = balance.ToString();

                // Query decimals
                var decimals = 18; // Default
                try
                {
                    decimals = (int)await erc20.DecimalsQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch decimals for token {Token}, using default", 
                        LoggingHelper.SanitizeLogInput(request.TokenIdentifier));
                }

                // Query symbol
                string? symbol = null;
                try
                {
                    symbol = await erc20.SymbolQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch symbol for token {Token}", 
                        LoggingHelper.SanitizeLogInput(request.TokenIdentifier));
                }

                // Query name
                string? name = null;
                try
                {
                    name = await erc20.NameQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch name for token {Token}", 
                        LoggingHelper.SanitizeLogInput(request.TokenIdentifier));
                }

                // Format balance
                var formattedBalance = FormatBalance(balanceString, decimals);

                return new BalanceQueryResponse
                {
                    Success = true,
                    TokenIdentifier = request.TokenIdentifier,
                    Address = request.Address,
                    Chain = request.Chain,
                    Balance = balanceString,
                    Decimals = decimals,
                    FormattedBalance = formattedBalance,
                    Symbol = symbol,
                    Name = name,
                    IsOptedIn = null, // Not applicable for EVM
                    IsFrozen = null, // Would need additional checks
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying EVM balance: {Message}", ex.Message);
                return new BalanceQueryResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                    ErrorMessage = $"Failed to query EVM balance: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private string FormatBalance(string balance, int decimals)
        {
            try
            {
                if (!BigInteger.TryParse(balance, out var balanceBigInt))
                {
                    return "0";
                }

                if (decimals == 0)
                {
                    return balance;
                }

                var divisor = BigInteger.Pow(10, decimals);
                var wholePart = balanceBigInt / divisor;
                var fractionalPart = balanceBigInt % divisor;

                if (fractionalPart == 0)
                {
                    return wholePart.ToString();
                }

                var fractionalString = fractionalPart.ToString().PadLeft(decimals, '0').TrimEnd('0');
                return $"{wholePart}.{fractionalString}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error formatting balance: {Balance}, decimals: {Decimals}", balance, decimals);
                return balance;
            }
        }
    }
}

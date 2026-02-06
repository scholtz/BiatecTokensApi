using System.Numerics;
using BiatecTokensApi.Configuration;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Text.Json;
using System.Text.Json.Serialization;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Models.AVM;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ERC20.Response;
using BiatecTokensApi.Models;
using AlgorandARC76AccountDotNet;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Helpers;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Provides functionality for deploying and interacting with ERC-20 token contracts on blockchain networks.
    /// </summary>
    /// <remarks>The <see cref="ERC20TokenService"/> class is designed to facilitate the deployment of ERC-20
    /// token contracts and manage interactions with the BiatecToken smart contract. It loads the ABI and bytecode for
    /// the BiatecToken contract from a JSON file and uses this information to deploy contracts and perform related
    /// operations.  This service relies on blockchain configuration settings and application-specific settings provided
    /// via dependency injection. It also logs relevant information and errors during operations.  Ensure that the
    /// required ABI and bytecode file ("BiatecToken.json") is present in the "ABI" directory under the application's
    /// base directory.</remarks>
    public class ERC20TokenService : IERC20TokenService
    {
        private readonly IOptionsMonitor<EVMChains> _config;
        private readonly IOptionsMonitor<AppConfiguration> _appConfig;
        private readonly ILogger<ERC20TokenService> _logger;
        private readonly ITokenIssuanceRepository _tokenIssuanceRepository;
        private readonly IComplianceRepository _complianceRepository;
        private readonly IDeploymentStatusService _deploymentStatusService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IUserRepository _userRepository;

        // BiatecToken ABI loaded from the JSON file
        private readonly string _biatecTokenMintableAbi;
        private readonly string _biatecTokenMintableBytecode;
        private readonly string _biatecTokenPremintedAbi;
        private readonly string _biatecTokenPremintedBytecode;
        /// <summary>
        /// Initializes a new instance of the <see cref="ERC20TokenService"/> class,  loading the ABI and bytecode for
        /// the BiatecToken contract and configuring the service.
        /// </summary>
        /// <remarks>This constructor reads the ABI and bytecode for the BiatecToken contract from a JSON
        /// file  located in the "ABI" directory under the application's base directory. The loaded ABI and  bytecode
        /// are used to interact with the BiatecToken smart contract. Ensure that the  "BiatecToken.json" file is
        /// present and correctly formatted in the expected location.</remarks>
        /// <param name="config">The configuration monitor for blockchain-related settings.</param>
        /// <param name="appConfig">The configuration monitor for application-specific settings.</param>
        /// <param name="logger">The logger used to log information and errors for this service.</param>
        /// <param name="tokenIssuanceRepository">The token issuance audit repository</param>
        /// <param name="complianceRepository">The compliance metadata repository</param>
        /// <param name="deploymentStatusService">The deployment status tracking service</param>
        /// <param name="authenticationService">The authentication service for user account management</param>
        /// <param name="userRepository">The user repository for accessing user data</param>
        /// <exception cref="InvalidOperationException">Thrown if the BiatecToken contract bytecode is not found in the ABI JSON file.</exception>
        public ERC20TokenService(
            IOptionsMonitor<EVMChains> config,
            IOptionsMonitor<AppConfiguration> appConfig,
            ILogger<ERC20TokenService> logger,
            ITokenIssuanceRepository tokenIssuanceRepository,
            IComplianceRepository complianceRepository,
            IDeploymentStatusService deploymentStatusService,
            IAuthenticationService authenticationService,
            IUserRepository userRepository
            )
        {
            _config = config;
            _appConfig = appConfig;
            _logger = logger;
            _tokenIssuanceRepository = tokenIssuanceRepository;
            _complianceRepository = complianceRepository;
            _deploymentStatusService = deploymentStatusService;
            _authenticationService = authenticationService;
            _userRepository = userRepository;

            // Load the BiatecToken ABI and bytecode from the JSON file
            var abiFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ABI", "BiatecTokenMintable.json");
            var jsonContent = File.ReadAllText(abiFilePath);
            var contractData = JsonSerializer.Deserialize<BiatecTokenContract>(jsonContent);
            _biatecTokenMintableAbi = JsonSerializer.Serialize(contractData?.Abi);
            _biatecTokenMintableBytecode = contractData?.Bytecode ?? throw new InvalidOperationException("Bytecode not found in BiatecToken.json");

            // Load the BiatecToken ABI and bytecode from the JSON file
            abiFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ABI", "BiatecTokenPreminted.json");
            jsonContent = File.ReadAllText(abiFilePath);
            contractData = JsonSerializer.Deserialize<BiatecTokenContract>(jsonContent);
            _biatecTokenPremintedAbi = JsonSerializer.Serialize(contractData?.Abi);
            _biatecTokenPremintedBytecode = contractData?.Bytecode ?? throw new InvalidOperationException("Bytecode not found in BiatecToken.json");

            _logger.LogInformation("Loaded BiatecToken ABI and bytecode from {Path}", abiFilePath);

        }


        private EVMBlockchainConfig GetBlockchainConfig(int chainId)
        {
            // Find the configuration for the specified chain ID
            var config = _config.CurrentValue.Chains.FirstOrDefault(c => c.ChainId == chainId);
            if (config == null)
            {
                throw new InvalidOperationException($"No configuration found for chain ID {chainId}");
            }
            return config;
        }
        /// <summary>
        /// Validates the deployment request for an ERC20 token based on the specified token type.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="tokenType"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void ValidateRequest(ERC20TokenDeploymentRequest request, TokenType tokenType)
        {
            // Validate compliance metadata
            var isRwaToken = ComplianceValidator.IsRwaToken(request.ComplianceMetadata);
            if (!ComplianceValidator.ValidateComplianceMetadata(request.ComplianceMetadata, isRwaToken, out var complianceErrors))
            {
                throw new ArgumentException($"Compliance validation failed: {string.Join("; ", complianceErrors)}");
            }

            switch (tokenType)
            {
                case TokenType.ERC20_Mintable:
                    var mintableRequest = request as ERC20MintableTokenDeploymentRequest;
                    if (mintableRequest == null)
                    {
                        throw new ArgumentException("Request must be of type ERC20MintableTokenDeploymentRequest for mintable tokens.");
                    }
                    if (mintableRequest.Symbol.Length > 10)
                    {
                        throw new ArgumentException("Symbol for ERC20 Mintable token must be 10 characters or less.");
                    }
                    if (mintableRequest.Name.Length > 50)
                    {
                        throw new ArgumentException("Name for ERC20 Mintable token must be 50 characters or less.");
                    }
                    if (mintableRequest.InitialSupply <= 0)
                    {
                        throw new ArgumentException("Initial supply for ERC20 Mintable token must be a positive value.");
                    }
                    if (mintableRequest.Decimals < 0 || mintableRequest.Decimals > 18)
                    {
                        throw new ArgumentException("Decimals for ERC20 Mintable token must be between 0 and 18.");
                    }
                    if (string.IsNullOrEmpty(mintableRequest.InitialSupplyReceiver))
                    {
                        throw new ArgumentException("Initial supply receiver address must be provided for ERC20 Mintable token deployment.");
                    }
                    if (mintableRequest.Cap < mintableRequest.InitialSupply)
                    {
                        throw new ArgumentException("Cap for ERC20 Mintable token must be at least the initial supply.");
                    }

                    break;
                case TokenType.ARC200_Preminted:
                    var premintedRequest = request as ERC20PremintedTokenDeploymentRequest;
                    if (premintedRequest == null)
                    {
                        throw new ArgumentException("Request must be of type ERC20PremintedTokenDeploymentRequest for preminted tokens.");
                    }
                    if (premintedRequest.Symbol.Length > 10)
                    {
                        throw new ArgumentException("Symbol for ERC20 Mintable token must be 10 characters or less.");
                    }
                    if (premintedRequest.Name.Length > 50)
                    {
                        throw new ArgumentException("Name for ERC20 Mintable token must be 50 characters or less.");
                    }
                    if (premintedRequest.InitialSupply <= 0)
                    {
                        throw new ArgumentException("Initial supply for ERC20 Mintable token must be a non-negative value.");
                    }
                    if (premintedRequest.Decimals < 0 || premintedRequest.Decimals > 18)
                    {
                        throw new ArgumentException("Decimals for ERC20 Mintable token must be between 0 and 18.");
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tokenType), $"Unsupported token type: {tokenType}");
            }
        }

        /// <summary>
        /// Deploys an ERC-20 token contract to the specified blockchain network.
        /// </summary>
        /// <remarks>This method deploys an ERC-20 token contract using the provided deployment
        /// parameters. The initial supply is allocated to the specified receiver address, or to the deployer's address
        /// if no receiver is provided. The method handles exceptions and logs relevant information about the deployment
        /// process.</remarks>
        /// <param name="request">The deployment request containing the token details, such as name, symbol, decimals, initial supply, and the
        /// blockchain configuration (e.g., chain ID and RPC URL).</param>
        /// <param name="tokenType">Token type</param>
        /// <param name="userId">Optional user ID for JWT-authenticated requests. If provided, uses user's ARC76 account. If null, uses system account.</param>
        /// <returns>A <see cref="ERC20TokenDeploymentResponse"/> containing the deployment result, including the contract
        /// address, transaction hash, and success status. If the deployment fails, the response includes an error
        /// message.</returns>
        public async Task<ERC20TokenDeploymentResponse> DeployERC20TokenAsync(ERC20TokenDeploymentRequest request, TokenType tokenType, string? userId = null)
        {
            ERC20TokenDeploymentResponse? response = null;
            string? deploymentId = null;

            try
            {
                ValidateRequest(request, tokenType);

                // Determine which account mnemonic to use: user's ARC76 account or system account
                string accountMnemonic;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    // JWT-authenticated user: use their ARC76-derived account
                    var userMnemonic = await _authenticationService.GetUserMnemonicForSigningAsync(userId);
                    if (string.IsNullOrWhiteSpace(userMnemonic))
                    {
                        _logger.LogError("Failed to retrieve mnemonic for user: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                        return new ERC20TokenDeploymentResponse
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.USER_NOT_FOUND,
                            ErrorMessage = "Failed to retrieve user account for token deployment",
                            TransactionHash = string.Empty,
                            ContractAddress = string.Empty
                        };
                    }
                    accountMnemonic = userMnemonic;
                    _logger.LogInformation("Using user's ARC76 account for deployment: UserId={UserId}", LoggingHelper.SanitizeLogInput(userId));
                }
                else
                {
                    // ARC-0014 authenticated or system: use system account
                    accountMnemonic = _appConfig.CurrentValue.Account;
                    _logger.LogInformation("Using system account for deployment (ARC-0014 authentication)");
                }

                var acc = ARC76.GetEVMAccount(accountMnemonic, Convert.ToInt32(request.ChainId));
                var chainConfig = GetBlockchainConfig(Convert.ToInt32(request.ChainId));
                var account = new Account(acc, request.ChainId);

                // Create deployment tracking record
                deploymentId = await _deploymentStatusService.CreateDeploymentAsync(
                    tokenType.ToString(),
                    GetNetworkName(chainConfig.ChainId),
                    account.Address,
                    request.Name,
                    request.Symbol);

                _logger.LogInformation("Created deployment tracking: DeploymentId={DeploymentId}, TokenType={TokenType}",
                    deploymentId, tokenType);

                // Connect to the blockchain
                var web3 = new Web3(account, chainConfig.RpcUrl);

                // Calculate token supply with decimals (convert to BigInteger properly)
                var decimalMultiplier = BigInteger.Pow(10, request.Decimals);
                var initialSupplyBigInteger = new BigInteger(request.InitialSupply) * decimalMultiplier;

                // Determine the initial supply receiver - use provided address or default to deployer
                var initialSupplyReceiver = !string.IsNullOrEmpty(request.InitialSupplyReceiver)
                    ? request.InitialSupplyReceiver
                    : account.Address;

                _logger.LogInformation("Deploying BiatecToken {Name} ({Symbol}) with supply {Supply} and {Decimals} decimals to receiver {Receiver}",
                    request.Name, request.Symbol, request.InitialSupply, request.Decimals, initialSupplyReceiver);

                // Update status to Submitted
                await _deploymentStatusService.UpdateDeploymentStatusAsync(
                    deploymentId,
                    DeploymentStatus.Submitted,
                    "Deployment transaction submitted to blockchain");

                // Deploy the BiatecToken contract with updated constructor parameters
                // BiatecToken constructor: (string name, string symbol, uint8 decimals_, uint256 initialSupply, address initialSupplyReceiver)

                var _biatecTokenAbi = tokenType == TokenType.ERC20_Mintable ? _biatecTokenMintableAbi : _biatecTokenPremintedAbi;
                var _biatecTokenBytecode = tokenType == TokenType.ERC20_Mintable ? _biatecTokenMintableBytecode : _biatecTokenPremintedBytecode;

                var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    _biatecTokenAbi,
                    _biatecTokenBytecode,
                    account.Address,
                    new HexBigInteger(chainConfig.GasLimit),
                    null, // No ETH value being sent
                    request.Name,              // string name
                    request.Symbol,            // string symbol
                    (byte)request.Decimals,    // uint8 decimals_
                    initialSupplyBigInteger,   // uint256 initialSupply
                    initialSupplyReceiver      // address initialSupplyReceiver
                );

                // Check if deployment was successful
                if (receipt?.Status?.Value == 1 && !string.IsNullOrEmpty(receipt.ContractAddress))
                {
                    // Update status to Confirmed
                    await _deploymentStatusService.UpdateDeploymentStatusAsync(
                        deploymentId,
                        DeploymentStatus.Confirmed,
                        "Deployment transaction confirmed on blockchain",
                        transactionHash: receipt.TransactionHash);

                    // Update asset identifier
                    await _deploymentStatusService.UpdateAssetIdentifierAsync(deploymentId, receipt.ContractAddress);

                    response = new ERC20TokenDeploymentResponse()
                    {
                        ContractAddress = receipt.ContractAddress,
                        Success = true,
                        TransactionHash = receipt.TransactionHash,
                        DeploymentId = deploymentId
                    };

                    _logger.LogInformation("BiatecToken {Symbol} deployed successfully at address {Address} with transaction {TxHash}",
                        request.Symbol, receipt.ContractAddress, receipt.TransactionHash);

                    // Persist compliance metadata if provided
                    if (request.ComplianceMetadata != null)
                    {
                        await PersistComplianceMetadata(request.ComplianceMetadata, receipt.ContractAddress, 
                            GetNetworkName(chainConfig.ChainId), account.Address);
                    }

                    // Log token issuance audit entry
                    await LogTokenIssuanceAudit(request, tokenType, receipt.ContractAddress, receipt.TransactionHash, 
                        account.Address, true, null, GetNetworkName(chainConfig.ChainId));

                    // Update status to Completed
                    await _deploymentStatusService.UpdateDeploymentStatusAsync(
                        deploymentId,
                        DeploymentStatus.Completed,
                        "Deployment completed successfully with all post-deployment operations finished");
                }
                else
                {
                    response = new ERC20TokenDeploymentResponse()
                    {
                        Success = false,
                        TransactionHash = receipt?.TransactionHash ?? string.Empty,
                        ContractAddress = string.Empty,
                        ErrorMessage = "Contract deployment failed - transaction reverted or no contract address received",
                        DeploymentId = deploymentId
                    };
                    _logger.LogError("BiatecToken deployment failed: {Error}", response.ErrorMessage);

                    // Log failed token issuance audit entry
                    await LogTokenIssuanceAudit(request, tokenType, null, receipt?.TransactionHash, 
                        account.Address, false, response.ErrorMessage, GetNetworkName(chainConfig.ChainId));

                    // Mark deployment as failed
                    await _deploymentStatusService.MarkDeploymentFailedAsync(
                        deploymentId,
                        response.ErrorMessage,
                        isRetryable: false);
                }
            }
            catch (Exception ex)
            {
                response = new ERC20TokenDeploymentResponse()
                {
                    Success = false,
                    TransactionHash = string.Empty,
                    ContractAddress = string.Empty,
                    ErrorMessage = ex.Message,
                    DeploymentId = deploymentId
                };
                _logger.LogError(ex, "Error deploying BiatecToken: {Message}", ex.Message);

                // Mark deployment as failed if we have a deployment ID
                if (!string.IsNullOrEmpty(deploymentId))
                {
                    await _deploymentStatusService.MarkDeploymentFailedAsync(
                        deploymentId,
                        ex.Message,
                        isRetryable: true);
                }

                // Log failed token issuance audit entry
                try
                {
                    var acc = ARC76.GetEVMAccount(_appConfig.CurrentValue.Account, Convert.ToInt32(request.ChainId));
                    var account = new Account(acc, request.ChainId);
                    var chainConfig = GetBlockchainConfig(Convert.ToInt32(request.ChainId));
                    await LogTokenIssuanceAudit(request, tokenType, null, null, account.Address, false, ex.Message, GetNetworkName(chainConfig.ChainId));
                }
                catch
                {
                    // Ignore audit logging errors
                }
            }

            return response;
        }

        /// <summary>
        /// Gets network name from chain ID
        /// </summary>
        private string GetNetworkName(int chainId)
        {
            return chainId switch
            {
                8453 => "base-mainnet",
                84532 => "base-sepolia",
                1 => "ethereum-mainnet",
                11155111 => "ethereum-sepolia",
                _ => $"evm-chain-{chainId}"
            };
        }

        /// <summary>
        /// Logs token issuance audit entry
        /// </summary>
        private async Task LogTokenIssuanceAudit(
            ERC20TokenDeploymentRequest request,
            TokenType tokenType,
            string? contractAddress,
            string? transactionHash,
            string deployedBy,
            bool success,
            string? errorMessage,
            string network)
        {
            try
            {
                var auditEntry = new TokenIssuanceAuditLogEntry
                {
                    ContractAddress = contractAddress,
                    AssetIdentifier = contractAddress,
                    Network = network,
                    TokenType = tokenType.ToString(),
                    TokenName = request.Name,
                    TokenSymbol = request.Symbol,
                    TotalSupply = request.InitialSupply.ToString(),
                    Decimals = request.Decimals,
                    DeployedBy = deployedBy,
                    DeployedAt = DateTime.UtcNow,
                    Success = success,
                    ErrorMessage = errorMessage,
                    TransactionHash = transactionHash,
                    IsMintable = tokenType == TokenType.ERC20_Mintable,
                    IsPausable = true, // BiatecToken is pausable
                    IsBurnable = true  // BiatecToken is burnable
                };

                await _tokenIssuanceRepository.AddAuditLogEntryAsync(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging token issuance audit entry");
            }
        }

        /// <summary>
        /// Persists compliance metadata for a deployed token
        /// </summary>
        private async Task PersistComplianceMetadata(
            TokenDeploymentComplianceMetadata deploymentMetadata,
            string contractAddress,
            string network,
            string createdBy)
        {
            try
            {
                // For EVM tokens, we use a deterministic hash of the contract address as the AssetId
                // Using SHA256 to ensure consistency across application restarts and environments
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(contractAddress.ToLowerInvariant()));
                var assetIdHash = BitConverter.ToUInt64(hashBytes, 0) & 0x7FFFFFFFFFFFFFFF; // Take first 8 bytes and ensure positive

                var complianceMetadata = new ComplianceMetadata
                {
                    AssetId = assetIdHash,
                    Network = network,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    IssuerName = deploymentMetadata.IssuerName,
                    KycProvider = deploymentMetadata.KycProvider,
                    Jurisdiction = deploymentMetadata.Jurisdiction,
                    RegulatoryFramework = deploymentMetadata.RegulatoryFramework,
                    AssetType = deploymentMetadata.AssetType,
                    TransferRestrictions = deploymentMetadata.TransferRestrictions,
                    MaxHolders = deploymentMetadata.MaxHolders,
                    RequiresAccreditedInvestors = deploymentMetadata.RequiresAccreditedInvestors,
                    Notes = deploymentMetadata.Notes,
                    ComplianceStatus = ComplianceValidator.DefaultComplianceStatus,
                    VerificationStatus = ComplianceValidator.DefaultVerificationStatus
                };

                await _complianceRepository.UpsertMetadataAsync(complianceMetadata);
                _logger.LogInformation("Persisted compliance metadata for contract {Address} on network {Network}",
                    contractAddress, network);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting compliance metadata for contract {Address}", contractAddress);
            }
        }


        // Helper class to deserialize the BiatecToken.json file
        private class BiatecTokenContract
        {
            [JsonPropertyName("abi")]
            public JsonElement[]? Abi { get; set; }

            [JsonPropertyName("bytecode")]
            public string? Bytecode { get; set; }
        }
    }
}
using System.Numerics;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace BiatecTokensApi.Services
{
    public class TokenService : ITokenService
    {
        private readonly BlockchainConfig _config;
        private readonly ILogger<TokenService> _logger;

        // Standard ERC20 token contract ABI (simplified)
        private const string ERC20_ABI = @"[{""inputs"":[{""internalType"":""string"",""name"":""name_"",""type"":""string""},{""internalType"":""string"",""name"":""symbol_"",""type"":""string""},{""internalType"":""uint256"",""name"":""initialSupply"",""type"":""uint256""},{""internalType"":""uint8"",""name"":""decimals_"",""type"":""uint8""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""owner"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""spender"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""inputs"":[{""internalType"":""address"",""name"":""owner"",""type"":""address""},{""internalType"":""address"",""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""decimals"",""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""subtractedValue"",""type"":""uint256""}],""name"":""decreaseAllowance"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""addedValue"",""type"":""uint256""}],""name"":""increaseAllowance"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""name"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""symbol"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""totalSupply"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""}]";

        // Simple ERC20 token contract bytecode that works with Ganache
        private const string ERC20_BYTECODE = "0x608060405234801561001057600080fd5b5060405161084f38038061084f83398101604052810190610030919061010c565b83600390816100409190610353565b5082600490816100509190610353565b508160025560ff81166005819055508160008061006c61008d565b73ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055505050505050610095565b600033905090565b6107b5806100a46000396000f3fe608060405234801561001057600080fd5b50600436106100df5760003560e01c806370a082311161008c57806395d89b4111610066578063a457c2d714610208578063a9059cbb14610238578063dd62ed3e14610268576100df565b806370a082311461019a578063313ce567146101ca578063395093511461024c576100df565b806318160ddd116100bd57806318160ddd1461012c57806323b872dd1461014a57806306fdde03146100e4576100df565b806306fdde03146100e4578063095ea7b314610102576100df565b3480156100df575f80fd5b50600480360381019061010e9190610518565b61008a565b6040516101199190610550565b60405180910390f35b6000603380549050905090565b5f80339050610157848285610334565b610162848484610391565b60019150509392505050565b5f602052805f5260405f205f915090505481565b5f60055f9054906101000a900460ff16905090565b5f8061019e6103e7565b90506101b38185856101b08589610298565b6101b99190610594565b6103ef565b60019150509392505050565b6101dc33838560e01c6104b8565b50565b5f806101e96103e7565b905061020081858561022885886102985761022261051b565b5061022c610594565b6103ef565b60019150509392505050565b5f8061021c6103e7565b905061022c8185856102358589610298565b61023f9190610590565b6103ef565b5f8061024f6103e7565b905061025c818585610391565b60019150509392505050565b5f60015f8473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f205f8373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f2054905092915050565b5f73ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff1603610330576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161032790610625565b60405180910390fd5b5f73ffffffffffffffffffffffffffffffffffffffff168273ffffffffffffffffffffffffffffffffffffffff160361036e576040517f08c379a0000000000000000000000000000000000000000000000000000000008152600401610365906106b3565b60405180910390fd5b8060015f8573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f205f8473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f20819055508173ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925836040516104429190610550565b60405180910390a3505050565b5f61045a8484610298565b90507fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff81146104b257818110156104a4576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161049b9061071b565b60405180910390fd5b6104b184848484036102e2565b5b50505050565b5f73ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff1603610526576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161051d906107a9565b60405180910390fd5b5f73ffffffffffffffffffffffffffffffffffffffff168273ffffffffffffffffffffffffffffffffffffffff1603610594576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161058b90610837565b60405180910390fd5b61059f838383610609565b5f805f8573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f2054905081811015610622576040517f08c379a0000000000000000000000000000000000000000000000000000000008152600401610619906108c5565b60405180910390fd5b818103905550505050565b81815f8573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f20819055508160015f8573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f20600082825461069a9190610594565b925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040516106fe9190610550565b60405180910390a3610711848484610614565b50505050565b505050565b505050565b5f819050919050565b61073381610720565b82525050565b5f6020820190506107485f83018461072a565b92915050565b5f80fd5b5f73ffffffffffffffffffffffffffffffffffffffff82169050919050565b5f61077a82610751565b9050919050565b61078a81610770565b81146107945f80fd5b50565b5f813590506107a581610781565b92915050565b6107b481610720565b81146107be5f80fd5b50565b5f813590506107cf816107ab565b92915050565b5f80604083850312156107eb576107ea61074d565b5b5f6107f885828601610797565b9250506020610809858286016107c1565b9150509250929050565b5f8115159050919050565b61082781610813565b82525050565b5f6020820190506108405f83018461081e565b92915050565b5f806040838503121561085c5761085b61074d565b5b5f61086985828601610797565b925050602061087a85828601610797565b9150509250929050565b7f4e487b71000000000000000000000000000000000000000000000000000000005f52602260045260245ffd5b5f60028204905060018216806108c957607f821691505b6020821081036108dc576108dc610884565b5b5091905056fea26469706673582212208b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b64736f6c63430008190033";

        public TokenService(IOptions<BlockchainConfig> config, ILogger<TokenService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task<TokenDeploymentResponse> DeployTokenAsync(TokenDeploymentRequest request)
        {
            var response = new TokenDeploymentResponse { Success = false };
            
            try
            {
                // Create an account with the provided private key
                var account = new Account(request.DeployerPrivateKey, _config.ChainId);
                
                // Connect to the blockchain
                var web3 = new Web3(account, _config.BaseRpcUrl);
                
                // Calculate token supply with decimals (convert to BigInteger properly)
                var decimalMultiplier = BigInteger.Pow(10, request.Decimals);
                var initialSupplyBigInteger = new BigInteger(request.InitialSupply) * decimalMultiplier;
                
                _logger.LogInformation("Deploying token {Name} ({Symbol}) with supply {Supply} and {Decimals} decimals", 
                    request.Name, request.Symbol, request.InitialSupply, request.Decimals);
                
                // Deploy the contract with proper constructor parameters
                var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    ERC20_ABI,
                    ERC20_BYTECODE,
                    account.Address,
                    new HexBigInteger(_config.GasLimit),
                    null, // No ETH value being sent
                    request.Name,
                    request.Symbol,
                    initialSupplyBigInteger,
                    (byte)request.Decimals);
                
                // Check if deployment was successful
                if (receipt?.Status?.Value == 1 && !string.IsNullOrEmpty(receipt.ContractAddress))
                {
                    response.TransactionHash = receipt.TransactionHash;
                    response.ContractAddress = receipt.ContractAddress;
                    response.Success = true;
                    
                    _logger.LogInformation("Token {Symbol} deployed successfully at address {Address}", 
                        request.Symbol, receipt.ContractAddress);
                }
                else
                {
                    response.ErrorMessage = "Contract deployment failed - transaction reverted or no contract address received";
                    _logger.LogError("Token deployment failed: {Error}", response.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying ERC20 token: {Message}", ex.Message);
                response.ErrorMessage = ex.Message;
            }
            
            return response;
        }
    }
}
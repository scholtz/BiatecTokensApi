# BiatecTokensTests - ERC20 Token Tests

This project includes tests for the BiatecTokensApi, including specialized tests for ERC20 token functionality that deploy to a local blockchain network.

## ERC20 Token Tests

The `Erc20TokenTests.cs` file includes comprehensive tests for ERC20 token functionality:

1. **Token deployment** - Deploys a new ERC20 token to a local blockchain
2. **Basic token properties** - Tests name, symbol, decimals, and total supply
3. **Token transfers** - Tests direct token transfers between accounts
4. **Allowances** - Tests approve, transferFrom, increaseAllowance, and decreaseAllowance functions

## Requirements

To run the ERC20 token tests, you need:

1. **Local Blockchain**: A local Ethereum blockchain running at `http://127.0.0.1:8545`
   - We recommend using Ganache: https://trufflesuite.com/ganache/
   - Launch Ganache and select "QUICKSTART" to use the default configuration

2. **Default Test Accounts**: The tests use the first two default accounts from Ganache
   - Account #0: Owner account for token deployment and primary operations
   - Account #1: User account for testing transfers and approvals

## Running the Tests

1. Launch Ganache with the default configuration
2. Open the test project in Visual Studio
3. Run the `Erc20TokenTests` tests via the Test Explorer
4. If the tests can't connect to a local blockchain, they will fail with instructions on how to set up Ganache

## Test Categories

The ERC20 tests are marked with the category `LocalBlockchainRequired` to make it clear they require external infrastructure.

## Implementation Details

The tests interact with the deployed token contract using Nethereum and cover the following ERC20 methods:

- `totalSupply()` - Returns the total token supply
- `balanceOf(address)` - Returns the token balance of an address
- `transfer(address, uint256)` - Transfers tokens directly to another address
- `approve(address, uint256)` - Approves an address to spend tokens on behalf of the sender
- `allowance(address, address)` - Returns the amount of tokens an address can spend on behalf of another
- `transferFrom(address, address, uint256)` - Transfers tokens from one address to another if allowed
- `increaseAllowance(address, uint256)` - Increases the allowance of an address
- `decreaseAllowance(address, uint256)` - Decreases the allowance of an address

These tests ensure the ERC20 token contract deployed by the BiatecTokensApi functions correctly according to the ERC20 standard.
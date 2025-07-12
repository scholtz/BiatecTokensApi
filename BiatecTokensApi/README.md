# Biatec Tokens API

A comprehensive API for deploying and managing various types of tokens on different blockchain networks, including ERC20 tokens on EVM chains, ARC3 tokens, and ARC200 tokens on Algorand.

## Features

- **ERC20 Token Deployment**: Deploy mintable and preminted ERC20 tokens on EVM chains (Base blockchain)
- **Algorand Standard Assets (ASA)**: Create fungible tokens, NFTs, and fractional NFTs on Algorand
- **ARC3 Token Support**: Deploy ARC3-compliant tokens with rich metadata and IPFS integration
- **ARC200 Token Support**: Create ARC200 tokens with mintable and preminted variants
- **Authentication**: Secure API access using ARC-0014 Algorand authentication
- **Multi-Network Support**: Support for various Algorand networks and EVM chains

## Supported Token Types

### EVM Chains (Base Blockchain)
- **ERC20 Mintable**: Advanced ERC20 tokens with minting, burning, and pausable functionality
- **ERC20 Preminted**: Standard ERC20 tokens with fixed supply

### Algorand Network
- **ASA Fungible Tokens**: Standard Algorand assets for fungible tokens
- **ASA NFTs**: Non-fungible tokens with quantity of 1
- **ASA Fractional NFTs**: Fractional non-fungible tokens with custom supply
- **ARC3 Fungible Tokens**: ARC3-compliant tokens with rich metadata and IPFS support
- **ARC3 NFTs**: ARC3-compliant non-fungible tokens with metadata
- **ARC3 Fractional NFTs**: ARC3-compliant fractional tokens
- **ARC200 Mintable**: ARC200 tokens with minting capabilities
- **ARC200 Preminted**: ARC200 tokens with fixed supply

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- Algorand account with sufficient funds for transactions
- Access to supported blockchain networks

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd BiatecTokensApi
```

2. Restore NuGet packages:
```bash
dotnet restore
```

3. Configure the application settings in `appsettings.json`:
```json
{
  "App": {
    "Account": "your-mnemonic-phrase-here"
  },
  "EVMChains": [
    {
      "RpcUrl": "https://mainnet.base.org",
      "ChainId": 8453,
      "GasLimit": 4500000
    }
  ],
  "IPFSConfig": {
    "ApiUrl": "https://ipfs-api.biatec.io",
    "GatewayUrl": "https://ipfs.biatec.io/ipfs",
    "Username": "",
    "Password": "",
    "TimeoutSeconds": 30,
    "MaxFileSizeBytes": 10485760,
    "ValidateContentHash": true
  },
  "AlgorandAuthentication": {
    "Realm": "BiatecTokens#ARC14",
    "CheckExpiration": true,
    "Debug": false,
    "AllowedNetworks": {
      "SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=": {
        "Server": "https://testnet-api.4160.nodely.dev",
        "Token": "",
        "Header": ""
      }
    }
  }
}
```

4. Run the application:
```bash
dotnet run
```

5. Access the API documentation at `https://localhost:7000/swagger` (or your configured port)

## Authentication

The API uses ARC-0014 Algorand authentication. You need to include an `Authorization` header with your authentication transaction.

Realm: ***BiatecTokens#ARC14***

### Example Authentication Header:
```
Authorization: SigTx <your-arc14-signed-transaction>
```

## API Endpoints

### Base URL
```
https://localhost:7000/api/v1/token
```

### ERC20 Tokens (Base Blockchain)

#### Deploy Mintable ERC20 Token
```http
POST /erc20-mintable/create
```

**Request Body:**
```json
{
  "name": "My Mintable Token",
  "symbol": "MMT",
  "initialSupply": 1000000,
  "decimals": 18,
  "initialSupplyReceiver": "0x742d35Cc6634C0532925a3b8D4434d3C7f2db9bc",
  "chainId": 8453,
  "cap": 10000000
}
```

#### Deploy Preminted ERC20 Token
```http
POST /erc20-preminted/create
```

**Request Body:**
```json
{
  "name": "My Preminted Token",
  "symbol": "MPT",
  "initialSupply": 1000000,
  "decimals": 18,
  "initialSupplyReceiver": "0x742d35Cc6634C0532925a3b8D4434d3C7f2db9bc",
  "chainId": 8453
}
```

### ASA Tokens (Algorand)

#### Deploy ASA Fungible Token
```http
POST /asa-ft/create
```

**Request Body:**
```json
{
  "name": "My ASA Token",
  "unitName": "MAT",
  "totalSupply": 1000000,
  "decimals": 6,
  "network": "testnet-v1.0",
  "defaultFrozen": false,
  "managerAddress": "ALGONAUTSPIUHDCX3SLFXOFDUKOE4VY36XV4JX2JHQTWJNKVBKPEBQACRY",
  "url": "https://example.com"
}
```

#### Deploy ASA NFT
```http
POST /asa-nft/create
```

**Request Body:**
```json
{
  "name": "My NFT",
  "unitName": "NFT",
  "network": "testnet-v1.0",
  "defaultFrozen": false,
  "url": "https://example.com/nft-metadata"
}
```

#### Deploy ASA Fractional NFT
```http
POST /asa-fnft/create
```

**Request Body:**
```json
{
  "name": "My Fractional NFT",
  "unitName": "FNFT",
  "totalSupply": 100,
  "decimals": 0,
  "network": "testnet-v1.0",
  "defaultFrozen": false
}
```

### ARC3 Tokens (Algorand with Rich Metadata)

#### Deploy ARC3 Fungible Token
```http
POST /arc3-ft/create
```

**Request Body:**
```json
{
  "name": "My ARC3 Token",
  "unitName": "ARC3",
  "totalSupply": 1000000,
  "decimals": 6,
  "network": "testnet-v1.0",
  "defaultFrozen": false,
  "metadata": {
    "name": "My ARC3 Token",
    "description": "A token with rich metadata",
    "image": "https://example.com/image.png",
    "properties": {
      "category": "utility",
      "rarity": "common"
    }
  }
}
```

#### Deploy ARC3 NFT
```http
POST /arc3-nft/create
```

**Request Body:**
```json
{
  "name": "My ARC3 NFT",
  "unitName": "NFT",
  "network": "testnet-v1.0",
  "defaultFrozen": false,
  "metadata": {
    "name": "Unique NFT",
    "description": "A unique digital asset",
    "image": "https://example.com/nft.png",
    "properties": {
      "trait_type": "Color",
      "value": "Blue"
    }
  }
}
```

#### Deploy ARC3 Fractional NFT
```http
POST /arc3-fnft/create
```

**Request Body:**
```json
{
  "name": "My Fractional ARC3 NFT",
  "unitName": "FNFT",
  "totalSupply": 100,
  "decimals": 0,
  "network": "testnet-v1.0",
  "defaultFrozen": false,
  "metadata": {
    "name": "Fractional Art Piece",
    "description": "A fractional ownership of digital art",
    "image": "https://example.com/art.png"
  }
}
```

### ARC200 Tokens (Algorand Smart Contracts)

#### Deploy ARC200 Mintable Token
```http
POST /arc200-mintable/create
```

**Request Body:**
```json
{
  "name": "My ARC200 Mintable Token",
  "symbol": "ARC200M",
  "initialSupply": 1000000,
  "decimals": 18,
  "network": "testnet-v1.0",
  "cap": 10000000
}
```

#### Deploy ARC200 Preminted Token
```http
POST /arc200-preminted/create
```

**Request Body:**
```json
{
  "name": "My ARC200 Preminted Token",
  "symbol": "ARC200P",
  "initialSupply": 1000000,
  "decimals": 18,
  "network": "testnet-v1.0"
}
```

## Response Format

All endpoints return responses in the following format:

### Success Response
```json
{
  "success": true,
  "transactionId": "transaction-hash",
  "assetId": 123456,
  "creatorAddress": "creator-address",
  "confirmedRound": 12345,
  "errorMessage": null
}
```

### Error Response
```json
{
  "success": false,
  "transactionId": null,
  "assetId": null,
  "creatorAddress": null,
  "confirmedRound": null,
  "errorMessage": "Error description"
}
```

## Supported Networks

### Algorand Networks
- `mainnet-v1.0` - Algorand Mainnet
- `testnet-v1.0` - Algorand Testnet
- `betanet-v1.0` - Algorand Betanet
- `voimain-v1.0` - Voi Mainnet
- `aramidmain-v1.0` - Aramid Mainnet

### EVM Networks
- **Base Mainnet** (Chain ID: 8453)

## Error Handling

The API returns standard HTTP status codes:

- `200 OK` - Successful operation
- `400 Bad Request` - Invalid request parameters
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Insufficient permissions
- `500 Internal Server Error` - Server error

## Rate Limiting

Please be mindful of blockchain network limitations and transaction fees when making requests. Each token deployment creates a blockchain transaction that requires network fees.

## Security Considerations

1. **Keep your mnemonic phrase secure** - Never commit it to version control
2. **Use environment variables** for sensitive configuration
3. **Validate all inputs** before making API calls
4. **Monitor transaction costs** on mainnet networks

## Development and Testing

### Running Tests
```bash
dotnet test BiatecTokensTests
```

### Development Environment
The API includes Swagger/OpenAPI documentation available at `/swagger` endpoint when running in development mode.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support and questions:
- Open an issue on GitHub
- Contact the development team
- Check the API documentation at `/swagger`

## Changelog

### Version 1.0
- Initial release
- Support for ERC20, ASA, ARC3, and ARC200 tokens
- Multi-network support
- ARC-0014 authentication integration
- IPFS metadata support for ARC3 tokens
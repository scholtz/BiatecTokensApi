# Biatec Tokens API

[![Test Pull Request](https://github.com/scholtz/BiatecTokensApi/actions/workflows/test-pr.yml/badge.svg)](https://github.com/scholtz/BiatecTokensApi/actions/workflows/test-pr.yml)

A comprehensive API for deploying and managing various types of tokens on different blockchain networks, including ERC20 tokens on EVM chains, ARC3 tokens, and ARC200 tokens on Algorand.

## Features

- **ERC20 Token Deployment**: Deploy mintable and preminted ERC20 tokens on EVM chains (Base blockchain)
- **Algorand Standard Assets (ASA)**: Create fungible tokens, NFTs, and fractional NFTs on Algorand
- **ARC3 Token Support**: Deploy ARC3-compliant tokens with rich metadata and IPFS integration
- **ARC200 Token Support**: Create ARC200 tokens with mintable and preminted variants
- **RWA Compliance Management**: Comprehensive compliance metadata and whitelist management for Real World Asset tokens
- **Compliance Indicators API**: Frontend-friendly endpoint exposing MICA readiness, whitelisting status, and enterprise readiness scores
- **Network-Specific Validation**: Enforce compliance rules for VOI and Aramid networks
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

### Continuous Integration

This project uses GitHub Actions for continuous integration. On every pull request, the CI pipeline:
- Builds the solution in Release configuration
- Runs all unit and integration tests
- Reports test results

The test pipeline ensures code quality and prevents breaking changes from being merged.

### Running Tests

Run all tests:
```bash
dotnet test BiatecTokensTests
```

Run tests with detailed output:
```bash
dotnet test BiatecTokensTests --verbosity detailed
```

Run tests in Release configuration:
```bash
dotnet test BiatecTokensTests --configuration Release
```

### Test-Driven Development (TDD)

We follow TDD practices for new features:

1. **Write failing tests first** - Define expected behavior through tests
2. **Implement the minimum code** - Make the tests pass
3. **Refactor** - Clean up code while keeping tests green

### Test Structure

The test suite includes:
- **Unit Tests**: Test individual components in isolation (services, repositories)
- **Integration Tests**: Test interactions between components
- **Mock Tests**: Use Moq to isolate dependencies

Example test file structure:
```
BiatecTokensTests/
├── TokenServiceTests.cs       # Service layer tests
├── IPFSRepositoryTests.cs     # Repository unit tests
├── TokenControllerTests.cs    # Controller tests
└── Erc20TokenTests.cs         # ERC20 functionality tests
```

### Development Environment

The API includes comprehensive Swagger/OpenAPI documentation:
- Interactive API explorer available at `/swagger` endpoint
- OpenAPI JSON specification at `/swagger/v1/swagger.json`

#### Running the API Locally

Use the provided script for easy local development:

```bash
./run-local.sh
```

The script will:
1. Check for required .NET SDK installation
2. Prompt for user secrets configuration
3. Restore dependencies and build the project
4. Start the API server

The API will be available at:
- HTTPS: https://localhost:7000
- HTTP: http://localhost:5000
- Swagger UI: https://localhost:7000/swagger

#### Sample Data for Testing

The `sample-seed-data.json` file contains sample request payloads for all token types. Use these as templates when testing the API endpoints.

#### OpenAPI Contract

The OpenAPI specification is automatically generated and can be used by frontend developers for:
- Generating type-safe API clients
- Creating mock servers for testing
- Validating requests and responses
- API documentation

See [OPENAPI.md](OPENAPI.md) for detailed information about accessing and using the OpenAPI contract.

#### CI/CD Integration

On every pull request and push to main/master:
- The OpenAPI specification is generated and published as a GitHub Actions artifact
- All tests (unit and integration) are run
- Test results are reported in the PR

To download the OpenAPI specification from CI:
1. Go to the Actions tab in GitHub
2. Select the latest workflow run
3. Download the `openapi-specification` artifact

## RWA Compliance Management

The API provides comprehensive compliance metadata management for Real World Asset (RWA) tokens, including KYC/AML verification tracking, jurisdiction management, and regulatory compliance monitoring.

### Compliance Indicators Endpoint (NEW)

**GET** `/api/v1/token/{assetId}/compliance-indicators` - Get frontend-friendly compliance indicators

Returns simplified compliance status including:
- **MICA readiness flag** - Whether token meets MICA regulatory requirements
- **Whitelisting enabled** - Status and count of whitelisted addresses
- **Transfer restrictions** - Summary of any restrictions in place
- **Enterprise readiness score** - Overall score (0-100) indicating compliance maturity
- **Regulatory framework** - Applicable frameworks (MICA, SEC Reg D, etc.)
- **KYC verification status** - Current verification state

For detailed documentation, see [COMPLIANCE_INDICATORS_API.md](../COMPLIANCE_INDICATORS_API.md)

### Compliance Metadata Endpoints

- `GET /api/v1/compliance/{assetId}` - Get compliance metadata for a token
- `POST /api/v1/compliance` - Create or update compliance metadata
- `DELETE /api/v1/compliance/{assetId}` - Delete compliance metadata
- `GET /api/v1/compliance` - List compliance metadata with filtering

### Compliance Attestation Endpoints (MICA Audit Trail)

- `POST /api/v1/compliance/attestations` - Create compliance attestation (KYC, AML, accreditation)
- `GET /api/v1/compliance/attestations` - List attestations with filtering
- `GET /api/v1/compliance/attestations/{id}` - Get specific attestation by ID
- `GET /api/v1/compliance/attestations/export/json` - Export attestations as JSON
- `GET /api/v1/compliance/attestations/export/csv` - Export attestations as CSV
- `POST /api/v1/compliance/attestation` - Generate MICA attestation package for audit

### Whitelist Management

- `GET /api/v1/whitelist/{assetId}` - List whitelisted addresses
- `POST /api/v1/whitelist` - Add address to whitelist (with KYC fields)
- `DELETE /api/v1/whitelist` - Remove address from whitelist
- `POST /api/v1/whitelist/bulk` - Bulk add addresses
- `GET /api/v1/whitelist/{assetId}/audit-log` - Get compliance audit trail
- `GET /api/v1/whitelist/audit-log` - Get audit logs across all assets
- `GET /api/v1/whitelist/audit-log/export/csv` - Export audit log as CSV
- `GET /api/v1/whitelist/audit-log/export/json` - Export audit log as JSON

For frontend integration guide, see [RWA_WHITELIST_FRONTEND_INTEGRATION.md](../RWA_WHITELIST_FRONTEND_INTEGRATION.md)

### Network-Specific Compliance Rules

#### VOI Network
- Requires KYC verification for accredited investor tokens
- Jurisdiction must be specified

#### Aramid Network
- Requires regulatory framework for compliant tokens
- Requires max holders specification for security tokens

For detailed documentation, see [COMPLIANCE_API.md](COMPLIANCE_API.md)

## Contributing

We welcome contributions! Please follow these guidelines:

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/your-feature`)
3. **Follow TDD practices**:
   - Write tests first (they should fail initially)
   - Implement the feature to make tests pass
   - Refactor while keeping tests green
4. **Add tests for new functionality** - All new code should include appropriate tests
5. **Run tests locally** - Ensure all tests pass before submitting
6. **Open a Pull Request** - The CI pipeline will automatically run tests

### Pull Request Requirements
- All tests must pass
- Code should build without errors
- Include test coverage for new features
- Follow existing code style and conventions

## Client generators

```
cd BiatecTokensApi/Generated
docker run --rm -v ".:/app/out" scholtz2/dotnet-avm-generated-client:latest dotnet client-generator.dll --namespace "BiatecTokensApi.Generated" --url https://raw.githubusercontent.com/scholtz/arc-1400/refs/heads/main/projects/arc-1400/smart_contracts/artifacts/security_token/Arc1644.arc56.json
docker run --rm -v ".:/app/out" scholtz2/dotnet-avm-generated-client:latest dotnet client-generator.dll --namespace "BiatecTokensApi.Generated" --url https://raw.githubusercontent.com/scholtz/arc200/refs/heads/main/contracts/artifacts/Arc200.arc56.json
```

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
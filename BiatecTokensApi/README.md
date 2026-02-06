# Biatec Tokens API

[![Test Pull Request](https://github.com/scholtz/BiatecTokensApi/actions/workflows/test-pr.yml/badge.svg)](https://github.com/scholtz/BiatecTokensApi/actions/workflows/test-pr.yml)

A comprehensive, wallet-free API for deploying and managing various types of tokens on different blockchain networks. **No wallet installation or blockchain knowledge required** - users authenticate with email and password, and the backend handles all blockchain operations transparently using ARC76 account derivation.

**Perfect for:** Traditional businesses, non-crypto-native users, and regulated financial institutions looking to issue compliant tokens without the complexity of wallet management.

## Features

- **🔐 Wallet-Free Authentication**: Email/password authentication with automatic ARC76 account derivation - no wallet installation or blockchain knowledge required
- **🚀 Server-Side Token Deployment**: Complete backend-managed token creation across 11 token standards - users never handle private keys
- **ERC20 Token Deployment**: Deploy mintable and preminted ERC20 tokens on EVM chains (Base blockchain)
- **Algorand Standard Assets (ASA)**: Create fungible tokens, NFTs, and fractional NFTs on Algorand
- **ARC3 Token Support**: Deploy ARC3-compliant tokens with rich metadata and IPFS integration
- **ARC200 Token Support**: Create ARC200 tokens with mintable and preminted variants
- **RWA Compliance Management**: Comprehensive compliance metadata and whitelist management for Real World Asset tokens
- **Compliance Indicators API**: Frontend-friendly endpoint exposing MICA readiness, whitelisting status, and enterprise readiness scores
- **Compliance Capability Matrix**: Configuration-driven API providing jurisdiction-aware compliance rules and enforcement
- **Network-Specific Validation**: Enforce compliance rules for VOI and Aramid networks
- **Dual Authentication**: JWT Bearer (email/password) and ARC-0014 (blockchain signatures) support
- **Multi-Network Support**: Support for various Algorand networks and EVM chains
- **Subscription Management**: Stripe-powered subscription tiers (Free, Basic, Premium, Enterprise) with self-service billing
- **Health Monitoring**: Comprehensive health checks for all external dependencies (IPFS, Algorand, EVM chains)
- **Enhanced Error Handling**: Global exception handling with standardized error responses and correlation IDs
- **Request Tracing**: Request/response logging for debugging and monitoring

## Health Monitoring & Stability

The API includes comprehensive health monitoring and error handling for production reliability. See [HEALTH_MONITORING.md](../HEALTH_MONITORING.md) for detailed documentation.

**Health Endpoints:**
- `/health` - Basic health check
- `/health/ready` - Kubernetes readiness probe
- `/health/live` - Kubernetes liveness probe
- `/api/v1/status` - Detailed component health and API information

**Key Features:**
- Real-time monitoring of IPFS, Algorand networks, and EVM chains
- Graceful degradation when dependencies are unavailable
- Structured error responses with error codes and correlation IDs
- Request/response logging for debugging
- Kubernetes-compatible probes for container orchestration

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

The API supports **two authentication methods** for different use cases:

### 1. JWT Bearer Authentication (Email/Password)

**Wallet-free authentication** for non-crypto-native users:

**Register a new user:**
```bash
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!",
  "fullName": "John Doe"
}
```

**Response includes:**
- User ID
- Email
- **Algorand address** (automatically derived from ARC76)
- Access token (JWT, 60 min expiry)
- Refresh token (30 days expiry)

**Login:**
```bash
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Use JWT token in API requests:**
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Key Features:**
- ✅ Automatic ARC76 account derivation (no wallet required)
- ✅ Secure BIP39 mnemonic generation
- ✅ AES-256-GCM encryption for mnemonic storage  
- ✅ Server-side token deployment using user's derived account
- ✅ Password requirements: 8+ chars, uppercase, lowercase, number, special character
- ✅ Account lockout after 5 failed login attempts

**Additional endpoints:**
- `POST /api/v1/auth/refresh` - Refresh access token
- `POST /api/v1/auth/logout` - Logout and invalidate tokens
- `GET /api/v1/auth/profile` - Get user profile with Algorand address
- `POST /api/v1/auth/change-password` - Change password

### 2. ARC-0014 Authentication (Blockchain Signatures)

**Blockchain-native authentication** for users with Algorand wallets:

Realm: ***BiatecTokens#ARC14***

### Example Authentication Header:
```
Authorization: SigTx <your-arc14-signed-transaction>
```

**How it works:**
1. Create a transaction with note: `BiatecTokens#ARC14`
2. Sign the transaction with your Algorand wallet
3. Include the signed transaction in the Authorization header

**Supported Networks:**
- Algorand Mainnet
- Algorand Testnet  
- Algorand Betanet

---

## Quick Start for Non-Crypto Users

**No wallet required!** Here's how to get started with the Biatec Tokens API using just email and password:

### Step 1: Register Your Account
```bash
curl -X POST https://api.biatec.io/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "your-email@example.com",
    "password": "SecurePass123!",
    "confirmPassword": "SecurePass123!",
    "fullName": "Your Name"
  }'
```

**Response includes:**
- Your unique Algorand blockchain address (automatically generated)
- JWT access token for API requests
- Refresh token for session renewal

### Step 2: Deploy Your First Token
```bash
curl -X POST https://api.biatec.io/api/v1/token/erc20-mintable/create \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "name": "My Company Token",
    "symbol": "MCT",
    "decimals": 18,
    "initialSupply": "1000000",
    "maxSupply": "10000000",
    "chainId": "8453"
  }'
```

**That's it!** The backend:
- ✅ Signs the transaction using your derived blockchain account
- ✅ Submits it to the blockchain network
- ✅ Tracks deployment status
- ✅ Returns the token contract address

**You never need to:**
- ❌ Install a wallet extension
- ❌ Write down seed phrases
- ❌ Approve transaction popups
- ❌ Switch networks manually
- ❌ Understand gas fees or blockchain concepts

### Step 3: Track Deployment Status
```bash
curl -X GET https://api.biatec.io/api/v1/deployment/status \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

See all your token deployments, their status (Pending, Confirmed, Completed), and blockchain transaction details.

---

- VOI Mainnet
- Aramid Mainnet

For more details, see [ARC-0014 specification](https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md) and [JWT_AUTHENTICATION_COMPLETE_GUIDE.md](../JWT_AUTHENTICATION_COMPLETE_GUIDE.md).

## Subscription Management

The API supports tiered subscriptions powered by Stripe for monetization and feature gating. See [STRIPE_SUBSCRIPTION_IMPLEMENTATION.md](../STRIPE_SUBSCRIPTION_IMPLEMENTATION.md) for full documentation.

### Subscription Tiers

| Tier | Price | Whitelist Addresses | Audit Logs | Bulk Operations |
|------|-------|---------------------|------------|-----------------|
| **Free** | $0/month | 10 per asset | ❌ | ❌ |
| **Basic** | $9/month | 100 per asset | ✅ | ❌ |
| **Premium** | $9/month | 1,000 per asset | ✅ | ✅ |
| **Enterprise** | $99/month | Unlimited | ✅ | ✅ |

### Subscription Endpoints

#### Create Checkout Session
```http
POST /api/v1/subscription/checkout
Authorization: SigTx <your-arc14-signed-transaction>
Content-Type: application/json

{
  "tier": "Basic"
}
```

#### Get Subscription Status
```http
GET /api/v1/subscription/status
Authorization: SigTx <your-arc14-signed-transaction>
```

#### Access Billing Portal
```http
POST /api/v1/subscription/billing-portal
Authorization: SigTx <your-arc14-signed-transaction>
Content-Type: application/json

{
  "returnUrl": "https://your-domain.com/dashboard"
}
```

For complete subscription documentation, including webhook setup and configuration, see [SUBSCRIPTION_IMPLEMENTATION_VERIFICATION.md](../SUBSCRIPTION_IMPLEMENTATION_VERIFICATION.md).

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

## Deployment Status Tracking

The API provides comprehensive deployment status tracking for all token creation requests. Track your deployments in real-time through polling endpoints.

### Check Deployment Status

```http
GET /api/v1/token/deployments/{deploymentId}
Authorization: Bearer <jwt-token> or SigTx <arc14-signed-tx>
```

**Response:**
```json
{
  "deploymentId": "deploy_abc123",
  "status": "Completed",
  "tokenType": "ERC20_Mintable",
  "transactionHash": "T7YFVXO5W5Q4NBVXMTGABCD...",
  "assetId": "123456789",
  "createdAt": "2026-02-06T13:00:00Z",
  "completedAt": "2026-02-06T13:02:30Z",
  "history": [
    {
      "status": "Queued",
      "timestamp": "2026-02-06T13:00:00Z"
    },
    {
      "status": "Submitted",
      "timestamp": "2026-02-06T13:00:15Z"
    },
    {
      "status": "Confirmed",
      "timestamp": "2026-02-06T13:02:00Z"
    },
    {
      "status": "Completed",
      "timestamp": "2026-02-06T13:02:30Z"
    }
  ]
}
```

### Deployment Status States

- `Queued` - Deployment request received
- `Submitted` - Transaction submitted to blockchain
- `Pending` - Waiting for confirmation
- `Confirmed` - Transaction confirmed on blockchain
- `Indexed` - Transaction indexed by blockchain explorer
- `Completed` - Deployment fully complete
- `Failed` - Deployment failed (see error message)
- `Cancelled` - Deployment cancelled by user

### List All Deployments

```http
GET /api/v1/token/deployments?status=Completed&page=1&pageSize=20
Authorization: Bearer <jwt-token> or SigTx <arc14-signed-tx>
```

**Query Parameters:**
- `status` - Filter by status (optional)
- `tokenType` - Filter by token type (optional)
- `page` - Page number (default: 1)
- `pageSize` - Items per page (default: 20, max: 100)

### Get Deployment History

```http
GET /api/v1/token/deployments/{deploymentId}/history
Authorization: Bearer <jwt-token> or SigTx <arc14-signed-tx>
```

Returns complete audit trail of all status transitions with timestamps.

For more details, see [DEPLOYMENT_STATUS_IMPLEMENTATION_SUMMARY.md](../DEPLOYMENT_STATUS_IMPLEMENTATION_SUMMARY.md).

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

### Compliance Capability Matrix API (NEW)

**GET** `/api/v1/compliance/capabilities` - Query compliance capability matrix with optional filtering
**POST** `/api/v1/compliance/capabilities/check` - Check if a specific action is allowed
**GET** `/api/v1/compliance/capabilities/version` - Get current capability matrix version

The Compliance Capability Matrix API provides a configurable, jurisdiction-aware system that defines which token standards, compliance checks, and transaction types are allowed per jurisdiction, wallet type, and KYC tier. This serves as the single source of truth for compliance gating across the platform.

**Key Features:**
- **Jurisdiction-specific rules** - Support for US, CH, EU, SG, and more
- **Wallet type awareness** - Different rules for custodial vs. non-custodial wallets
- **KYC tier gating** - Progressive capabilities based on KYC level (0-3)
- **Token standard support** - Rules for ARC-3, ARC-19, ARC-200, ERC-20
- **Action enforcement** - Control mint, transfer, burn, freeze operations
- **Required checks** - Define mandatory compliance checks per action
- **Audit logging** - All capability queries and enforcement decisions logged
- **Caching** - In-memory caching for performance

**Example - Get capabilities for Switzerland:**
```bash
GET /api/v1/compliance/capabilities?jurisdiction=CH&walletType=custodial&kycTier=2
```

**Example - Check if mint is allowed:**
```bash
POST /api/v1/compliance/capabilities/check
{
  "jurisdiction": "CH",
  "walletType": "custodial",
  "tokenStandard": "ARC-19",
  "kycTier": "2",
  "action": "mint"
}
```

For comprehensive documentation, examples, and integration guide, see [CAPABILITY_MATRIX_API.md](../CAPABILITY_MATRIX_API.md)

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

#### CRUD Operations
- `GET /api/v1/whitelist/{assetId}` - List whitelisted addresses with pagination
- `POST /api/v1/whitelist` - Add single address to whitelist (with KYC fields)
- `DELETE /api/v1/whitelist` - Remove address from whitelist
- `POST /api/v1/whitelist/bulk` - Bulk add addresses (up to 1000)
- `POST /api/v1/whitelist/validate-transfer` - Validate if transfer is allowed

#### CSV Import/Export
- `GET /api/v1/whitelist/{assetId}/export/csv` - Export whitelist entries as CSV (up to 10,000 entries)
- `POST /api/v1/whitelist/{assetId}/import/csv` - Import whitelist entries from CSV file (max 1 MB, 1000 addresses)

#### Audit Trail & Compliance
- `GET /api/v1/whitelist/{assetId}/audit-log` - Get compliance audit trail for specific token
- `GET /api/v1/whitelist/audit-log` - Get audit logs across all assets with filtering
- `GET /api/v1/whitelist/audit-log/export/csv` - Export audit log as CSV
- `GET /api/v1/whitelist/audit-log/export/json` - Export audit log as JSON
- `GET /api/v1/whitelist/audit-log/retention-policy` - Get MICA compliance policy (7-year retention)

**Documentation:**
- [Enforcement Examples & Integration Guide](../WHITELIST_ENFORCEMENT_EXAMPLES.md) - Complete examples for applying whitelist enforcement to token operations
- [Frontend Integration Guide](../RWA_WHITELIST_FRONTEND_INTEGRATION.md) - Frontend developer integration guide
- [Feature Overview](../WHITELIST_FEATURE.md) - Business value and technical overview

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
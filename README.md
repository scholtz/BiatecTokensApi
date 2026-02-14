# Biatec Tokens API

> Comprehensive backend API for regulated token issuance and wallet-free blockchain operations

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## Overview

Biatec Tokens API is a production-ready platform for creating and managing tokens across multiple blockchain networks. Designed for traditional businesses and enterprises, it eliminates wallet complexity by handling all blockchain operations server-side with email/password authentication.

### Key Features

- **🔐 Wallet-Free Operations**: Full blockchain integration without requiring users to manage wallets or private keys
- **📝 11 Token Standards**: Support for ASA, ARC3, ARC200, ARC1400, and ERC20 tokens
- **🌐 Multi-Chain Support**: Algorand (mainnet, testnet, betanet), VOI, Aramid, and Base blockchain
- **✅ MICA Compliance**: Built-in compliance validation, KYC integration, and audit trails
- **🔄 Idempotent Deployments**: Prevent accidental duplicate token creation with 24-hour caching
- **📊 Real-Time Tracking**: Comprehensive deployment status tracking with webhooks
- **🔔 Enterprise Webhooks**: Event-driven notifications with HMAC signing and retry logic
- **📋 OpenAPI Documentation**: Interactive Swagger UI with complete API specifications

## Table of Contents

- [Quick Start](#quick-start)
- [Authentication](#authentication)
- [Token Standards](#token-standards)
- [API Endpoints](#api-endpoints)
- [Webhooks](#webhooks)
- [Examples](#examples)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later
- Docker (optional, for containerized deployment)
- Access to blockchain networks (Algorand, Base)
- IPFS access (for ARC3 tokens with metadata)

### Local Development

```bash
# Clone the repository
git clone https://github.com/scholtz/BiatecTokensApi.git
cd BiatecTokensApi

# Restore dependencies
dotnet restore

# Configure user secrets (don't commit these!)
dotnet user-secrets set "App:Account" "your-algorand-mnemonic-phrase"
dotnet user-secrets set "IPFSConfig:Username" "your-ipfs-username"
dotnet user-secrets set "IPFSConfig:Password" "your-ipfs-password"

# Run the API
dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj

# Access Swagger UI
# Navigate to: https://localhost:7000/swagger
```

### Docker Deployment

```bash
# Build Docker image
docker build -t biatec-tokens-api -f BiatecTokensApi/Dockerfile .

# Run container
docker run -p 7000:7000 \
  -e App__Account="your-mnemonic" \
  -e IPFSConfig__Username="your-ipfs-username" \
  -e IPFSConfig__Password="your-ipfs-password" \
  biatec-tokens-api
```

## Authentication

The API supports two authentication methods:

### 1. ARC-0014 (Algorand Authentication) - Recommended for Blockchain Operations

Transaction-based authentication where users sign an authentication transaction.

```bash
# Example authentication flow
curl -X POST https://api.example.com/api/v1/token/asa-ft/create \
  -H "Authorization: SigTx gqNzaWfEQE..." \
  -H "Content-Type: application/json" \
  -d '{
    "assetName": "MyToken",
    "unitName": "MTK",
    "totalSupply": 1000000,
    "decimals": 6,
    "network": "mainnet-v1.0"
  }'
```

**Realm**: `BiatecTokens#ARC14`

### 2. JWT Bearer Authentication - For Traditional Web Applications

Email/password authentication with JWT tokens.

```bash
# Login to get JWT token
curl -X POST https://api.example.com/api/v2/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "secure-password"
  }'

# Use JWT token in subsequent requests
curl -X POST https://api.example.com/api/v1/token/erc20-mintable/create \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MyToken",
    "symbol": "MTK",
    "decimals": 18,
    "initialSupply": "1000000"
  }'
```

For detailed authentication implementation, see [JWT_AUTHENTICATION_COMPLETE_GUIDE.md](JWT_AUTHENTICATION_COMPLETE_GUIDE.md) and [ARC76_AUTH_IMPLEMENTATION_SUMMARY.md](ARC76_AUTH_IMPLEMENTATION_SUMMARY.md).

## Token Standards

### Supported Standards

| Standard | Type | Blockchain | Features |
|----------|------|------------|----------|
| **ASA** | Fungible, NFT, Fractional NFT | Algorand | Basic token standard |
| **ARC3** | Fungible, NFT, Fractional NFT | Algorand | Rich metadata on IPFS |
| **ARC200** | Fungible (mintable/preminted) | Algorand | Smart contract tokens with advanced features |
| **ARC1400** | Security Token (mintable) | Algorand | Regulated securities with compliance |
| **ERC20** | Fungible (mintable/preminted) | Base | Ethereum-compatible tokens |

### Token Type Details

#### Algorand Standard Assets (ASA)
- **Fungible Tokens (ASA-FT)**: Basic fungible tokens with decimals support
- **NFTs (ASA-NFT)**: Unique tokens with total supply of 1
- **Fractional NFTs (ASA-FNFT)**: NFTs with fractional ownership support

#### ARC3 Tokens
- **Enhanced Metadata**: Store rich metadata on IPFS
- **Images and Media**: Support for token images, background images, animation URLs
- **Properties and Attributes**: Extensible metadata for NFTs
- **External Links**: Website and social media links

#### ARC200 Tokens
- **Mintable**: Owner can mint new tokens after deployment
- **Preminted**: Fixed supply created at deployment
- **Smart Contract Features**: Advanced functionality via Algorand smart contracts

#### ARC1400 Security Tokens
- **Regulatory Compliance**: Built-in compliance checks and enforcement
- **Transfer Restrictions**: Whitelist-based transfer validation
- **Compliance Metadata**: Issuer information, jurisdiction tracking

#### ERC20 Tokens (Base Blockchain)
- **Mintable with Cap**: Owner can mint up to a maximum cap
- **Preminted**: Fixed total supply created at deployment
- **Pausable**: Owner can pause/unpause token transfers
- **Burnable**: Support for burning tokens

## API Endpoints

### Token Deployment

#### ERC20 Tokens (Base Blockchain)

**POST** `/api/v1/token/erc20-mintable/create`

Deploy a mintable ERC20 token with cap.

```json
{
  "name": "MyToken",
  "symbol": "MTK",
  "decimals": 18,
  "cap": "10000000",
  "initialSupply": "1000000",
  "initialSupplyReceiver": "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb"
}
```

**POST** `/api/v1/token/erc20-preminted/create`

Deploy a fixed-supply ERC20 token.

```json
{
  "name": "FixedToken",
  "symbol": "FTK",
  "decimals": 18,
  "totalSupply": "1000000"
}
```

#### Algorand Standard Assets (ASA)

**POST** `/api/v1/token/asa-ft/create`

Create an Algorand fungible token.

```json
{
  "assetName": "AlgoToken",
  "unitName": "ALGO",
  "totalSupply": 1000000,
  "decimals": 6,
  "url": "https://example.com",
  "network": "mainnet-v1.0",
  "manager": "OPTIONAL_MANAGER_ADDRESS",
  "reserve": "OPTIONAL_RESERVE_ADDRESS",
  "freeze": "OPTIONAL_FREEZE_ADDRESS",
  "clawback": "OPTIONAL_CLAWBACK_ADDRESS"
}
```

**POST** `/api/v1/token/asa-nft/create`

Create an Algorand NFT.

```json
{
  "assetName": "MyNFT",
  "unitName": "NFT",
  "url": "ipfs://Qm...",
  "network": "mainnet-v1.0"
}
```

**POST** `/api/v1/token/asa-fnft/create`

Create an Algorand fractional NFT.

```json
{
  "assetName": "FractionalNFT",
  "unitName": "FNFT",
  "totalSupply": 1000,
  "decimals": 0,
  "url": "ipfs://Qm...",
  "network": "mainnet-v1.0"
}
```

#### ARC3 Tokens (with IPFS Metadata)

**POST** `/api/v1/token/arc3-ft/create`

Create an ARC3 fungible token with rich metadata.

```json
{
  "name": "Premium Token",
  "symbol": "PREM",
  "totalSupply": 1000000,
  "decimals": 6,
  "description": "A premium token with enhanced metadata",
  "image": "https://example.com/logo.png",
  "external_url": "https://example.com",
  "properties": {
    "category": "DeFi",
    "verified": true
  },
  "network": "mainnet-v1.0"
}
```

**POST** `/api/v1/token/arc3-nft/create`

Create an ARC3 NFT with metadata.

```json
{
  "name": "Digital Art #1",
  "description": "Unique digital artwork",
  "image": "ipfs://Qm.../image.png",
  "animation_url": "ipfs://Qm.../animation.mp4",
  "attributes": [
    {
      "trait_type": "Rarity",
      "value": "Legendary"
    },
    {
      "trait_type": "Artist",
      "value": "JohnDoe"
    }
  ],
  "network": "mainnet-v1.0"
}
```

**POST** `/api/v1/token/arc3-fnft/create`

Create an ARC3 fractional NFT.

```json
{
  "name": "Real Estate Token",
  "totalSupply": 10000,
  "decimals": 0,
  "description": "Fractional ownership of commercial property",
  "image": "https://example.com/property.jpg",
  "properties": {
    "location": "New York, NY",
    "sqft": 5000,
    "type": "Commercial"
  },
  "network": "mainnet-v1.0"
}
```

#### ARC200 Tokens (Smart Contract)

**POST** `/api/v1/token/arc200-mintable/create`

Deploy a mintable ARC200 token.

```json
{
  "name": "MintableToken",
  "symbol": "MINT",
  "decimals": 6,
  "totalSupply": "1000000",
  "network": "voimain-v1.0"
}
```

**POST** `/api/v1/token/arc200-preminted/create`

Deploy a fixed-supply ARC200 token.

```json
{
  "name": "FixedARC200",
  "symbol": "FARC",
  "decimals": 6,
  "totalSupply": "10000000",
  "network": "voimain-v1.0"
}
```

#### ARC1400 Security Tokens

**POST** `/api/v1/token/arc1400-mintable/create`

Deploy a regulated security token.

```json
{
  "name": "Security Token",
  "symbol": "SEC",
  "decimals": 6,
  "totalSupply": "1000000",
  "network": "mainnet-v1.0"
}
```

### Deployment Tracking

**GET** `/api/v1/token/deployments/{deploymentId}`

Get the status and history of a token deployment.

```bash
curl -X GET https://api.example.com/api/v1/token/deployments/abc-123-def-456 \
  -H "Authorization: Bearer <token>"
```

Response:
```json
{
  "success": true,
  "deployment": {
    "deploymentId": "abc-123-def-456",
    "currentStatus": "Completed",
    "tokenType": "ERC20_Mintable",
    "network": "base-mainnet",
    "deployedBy": "user@example.com",
    "tokenName": "MyToken",
    "tokenSymbol": "MTK",
    "transactionHash": "0x123...",
    "assetId": "0x456...",
    "createdAt": "2026-02-14T12:00:00Z",
    "completedAt": "2026-02-14T12:02:30Z",
    "statusHistory": [
      {
        "status": "Queued",
        "message": "Deployment request queued",
        "timestamp": "2026-02-14T12:00:00Z"
      },
      {
        "status": "Submitted",
        "message": "Transaction submitted to blockchain",
        "timestamp": "2026-02-14T12:00:15Z"
      },
      {
        "status": "Confirmed",
        "message": "Transaction confirmed",
        "timestamp": "2026-02-14T12:02:00Z"
      },
      {
        "status": "Completed",
        "message": "Deployment completed successfully",
        "timestamp": "2026-02-14T12:02:30Z"
      }
    ]
  }
}
```

**GET** `/api/v1/token/deployments?page=1&pageSize=50&status=Completed&tokenType=ERC20_Mintable`

List deployments with filters.

Query Parameters:
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 50, max: 100)
- `status`: Filter by status (Queued, Submitted, Pending, Confirmed, Indexed, Completed, Failed, Cancelled)
- `tokenType`: Filter by token type
- `network`: Filter by blockchain network
- `startDate`: Filter by creation date (ISO 8601)
- `endDate`: Filter by creation date (ISO 8601)

### Token Metadata

**GET** `/api/v1/metadata/token?tokenIdentifier={id}&chain={chain}&includeValidation=true`

Retrieve comprehensive token metadata.

```bash
curl -X GET "https://api.example.com/api/v1/metadata/token?tokenIdentifier=123456&chain=algorand-mainnet&includeValidation=true"
```

Response:
```json
{
  "success": true,
  "metadata": {
    "tokenIdentifier": "123456",
    "chain": "algorand-mainnet",
    "name": "MyToken",
    "symbol": "MTK",
    "decimals": 6,
    "totalSupply": "1000000",
    "description": "A sample token",
    "image": "ipfs://Qm.../logo.png",
    "externalUrl": "https://example.com",
    "properties": {},
    "completenessScore": 95,
    "validationStatus": "Valid",
    "validationIssues": [],
    "createdAt": "2026-01-15T10:00:00Z",
    "updatedAt": "2026-02-14T12:00:00Z"
  }
}
```

**POST** `/api/v1/metadata/validate`

Validate token metadata against a specific standard.

```json
{
  "standard": "ARC3",
  "metadata": {
    "name": "Test Token",
    "description": "Testing validation",
    "image": "https://example.com/image.png"
  }
}
```

### Token Registry

**GET** `/api/v1/registry/tokens?page=1&pageSize=50&standard=ARC3&complianceStatus=Compliant`

Discover and filter tokens.

Query Parameters:
- `page`, `pageSize`: Pagination
- `standard`: Filter by token standard (ASA, ARC3, ARC200, ARC1400, ERC20)
- `complianceStatus`: Filter by compliance (Compliant, NonCompliant, Unknown)
- `chain`: Filter by blockchain
- `issuer`: Filter by issuer address
- `search`: Search by name or symbol
- `tags`: Filter by tags (comma-separated)
- `contractVerified`: Filter by contract verification status (true/false)

### Idempotency

All token deployment endpoints support idempotency to prevent accidental duplicate deployments.

```bash
# Include Idempotency-Key header
curl -X POST https://api.example.com/api/v1/token/erc20-mintable/create \
  -H "Authorization: Bearer <token>" \
  -H "Idempotency-Key: unique-deployment-id-12345" \
  -H "Content-Type: application/json" \
  -d '{ ... }'
```

If the same idempotency key is used within 24 hours:
- Same parameters: Returns cached response (no duplicate deployment)
- Different parameters: Returns `400 Bad Request` with error code `IDEMPOTENCY_KEY_MISMATCH`

Response headers include:
- `X-Idempotency-Hit: true` - Cached response returned
- `X-Idempotency-Hit: false` - New deployment created

## Webhooks

Subscribe to real-time events for deployments, compliance changes, and more.

### Webhook Management

**POST** `/api/v1/webhooks/subscriptions`

Create a webhook subscription.

```json
{
  "url": "https://your-app.com/webhooks/biatec",
  "eventTypes": [
    "TokenDeploymentStarted",
    "TokenDeploymentConfirming",
    "TokenDeploymentCompleted",
    "TokenDeploymentFailed"
  ],
  "filters": {
    "assetId": "123456",
    "network": "mainnet-v1.0"
  },
  "secret": "your-webhook-secret"
}
```

**GET** `/api/v1/webhooks/subscriptions`

List your webhook subscriptions.

**DELETE** `/api/v1/webhooks/subscriptions/{id}`

Delete a webhook subscription.

### Webhook Events

#### Token Deployment Events

**TokenDeploymentStarted**
```json
{
  "eventType": "TokenDeploymentStarted",
  "deploymentId": "abc-123-def-456",
  "tokenType": "ERC20_Mintable",
  "network": "base-mainnet",
  "timestamp": "2026-02-14T12:00:00Z",
  "data": {
    "tokenName": "MyToken",
    "tokenSymbol": "MTK",
    "deployer": "user@example.com"
  }
}
```

**TokenDeploymentConfirming**
```json
{
  "eventType": "TokenDeploymentConfirming",
  "deploymentId": "abc-123-def-456",
  "transactionHash": "0x123...",
  "timestamp": "2026-02-14T12:01:00Z",
  "data": {
    "confirmations": 5,
    "requiredConfirmations": 12
  }
}
```

**TokenDeploymentCompleted**
```json
{
  "eventType": "TokenDeploymentCompleted",
  "deploymentId": "abc-123-def-456",
  "assetId": "0x456...",
  "transactionHash": "0x123...",
  "timestamp": "2026-02-14T12:02:30Z",
  "data": {
    "tokenName": "MyToken",
    "tokenSymbol": "MTK",
    "contractAddress": "0x456...",
    "totalSupply": "1000000"
  }
}
```

**TokenDeploymentFailed**
```json
{
  "eventType": "TokenDeploymentFailed",
  "deploymentId": "abc-123-def-456",
  "timestamp": "2026-02-14T12:01:30Z",
  "data": {
    "errorMessage": "Insufficient funds for gas",
    "errorCode": "INSUFFICIENT_FUNDS",
    "retryable": true
  }
}
```

### Webhook Security

All webhook payloads are signed with HMAC-SHA256.

```typescript
// Verify webhook signature
import crypto from 'crypto';

function verifyWebhookSignature(
  payload: string,
  signature: string,
  secret: string
): boolean {
  const hmac = crypto.createHmac('sha256', secret);
  hmac.update(payload);
  const expectedSignature = hmac.digest('hex');
  
  return crypto.timingSafeEqual(
    Buffer.from(signature),
    Buffer.from(expectedSignature)
  );
}

// Express middleware example
app.post('/webhooks/biatec', (req, res) => {
  const signature = req.headers['x-webhook-signature'];
  const payload = JSON.stringify(req.body);
  
  if (!verifyWebhookSignature(payload, signature, WEBHOOK_SECRET)) {
    return res.status(401).send('Invalid signature');
  }
  
  // Process webhook event
  const event = req.body;
  console.log('Received event:', event.eventType);
  
  res.status(200).send('OK');
});
```

### Webhook Retry Logic

Failed webhook deliveries are retried with exponential backoff:
- 1st retry: After 1 minute
- 2nd retry: After 5 minutes
- 3rd retry: After 15 minutes

After 3 failed attempts, the delivery is moved to dead-letter tracking.

**GET** `/api/v1/webhooks/deliveries?status=failed`

View failed webhook deliveries.

For detailed webhook documentation, see [WEBHOOKS.md](WEBHOOKS.md).

## Examples

### Complete Token Deployment Flow

```typescript
// 1. Authenticate with JWT
const loginResponse = await fetch('https://api.example.com/api/v2/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'user@example.com',
    password: 'secure-password'
  })
});
const { token } = await loginResponse.json();

// 2. Deploy token with idempotency
const deploymentResponse = await fetch('https://api.example.com/api/v1/token/erc20-mintable/create', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
    'Idempotency-Key': `deployment-${Date.now()}`
  },
  body: JSON.stringify({
    name: 'MyToken',
    symbol: 'MTK',
    decimals: 18,
    cap: '10000000',
    initialSupply: '1000000'
  })
});

const deployment = await deploymentResponse.json();
console.log('Deployment ID:', deployment.deploymentId);

// 3. Poll deployment status
async function pollDeploymentStatus(deploymentId: string): Promise<void> {
  const statusResponse = await fetch(
    `https://api.example.com/api/v1/token/deployments/${deploymentId}`,
    {
      headers: { 'Authorization': `Bearer ${token}` }
    }
  );
  
  const { deployment } = await statusResponse.json();
  console.log('Current status:', deployment.currentStatus);
  
  if (deployment.currentStatus === 'Completed') {
    console.log('Token deployed!');
    console.log('Contract address:', deployment.assetId);
    console.log('Transaction hash:', deployment.transactionHash);
    return;
  }
  
  if (deployment.currentStatus === 'Failed') {
    console.error('Deployment failed:', deployment.errorMessage);
    return;
  }
  
  // Poll again in 5 seconds
  setTimeout(() => pollDeploymentStatus(deploymentId), 5000);
}

await pollDeploymentStatus(deployment.deploymentId);
```

### Using Webhooks Instead of Polling

```typescript
// 1. Create webhook subscription
const webhookResponse = await fetch('https://api.example.com/api/v1/webhooks/subscriptions', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    url: 'https://your-app.com/webhooks/biatec',
    eventTypes: [
      'TokenDeploymentCompleted',
      'TokenDeploymentFailed'
    ],
    secret: 'your-webhook-secret'
  })
});

// 2. Deploy token
const deployment = await deployToken();

// 3. Your webhook endpoint receives events
// (No polling needed!)
```

## Development

### Project Structure

```
BiatecTokensApi/
├── BiatecTokensApi/              # Main API project
│   ├── Controllers/              # API controllers
│   ├── Services/                 # Business logic
│   │   └── Interface/            # Service interfaces
│   ├── Models/                   # Data models and DTOs
│   ├── Repositories/             # Data access layer
│   ├── Filters/                  # Action filters (auth, idempotency)
│   ├── Helpers/                  # Utility helpers
│   ├── Configuration/            # Configuration models
│   ├── ABI/                      # Smart contract ABIs
│   └── Program.cs                # Application entry point
├── BiatecTokensTests/            # Test project
└── BiatecTokensApi.sln           # Solution file
```

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build BiatecTokensApi.sln

# Build in Release mode
dotnet build BiatecTokensApi.sln --configuration Release

# Build specific project
dotnet build BiatecTokensApi/BiatecTokensApi.csproj
```

### Code Style

- Follow standard C# naming conventions
- Use explicit types when type is not obvious
- Add XML documentation for all public APIs
- Enable nullable reference types
- Sanitize all user inputs before logging (use `LoggingHelper.SanitizeLogInput()`)

For detailed contribution guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md).

## Testing

### Run Tests

```bash
# Run all tests
dotnet test BiatecTokensTests

# Run with detailed output
dotnet test BiatecTokensTests --verbosity detailed

# Run specific test
dotnet test BiatecTokensTests --filter "FullyQualifiedName~TokenServiceTests"

# Run tests excluding real endpoint tests
dotnet test --filter "FullyQualifiedName!~RealEndpoint"
```

### Test Categories

- **Unit Tests**: Service logic, validation, helpers
- **Integration Tests**: API endpoints, database operations
- **Webhook Tests**: Event delivery, retry logic, signature validation
- **Compliance Tests**: KYC enforcement, whitelist validation

Test coverage: ~99%+ (1545+ tests passing)

For testing best practices, see [TEST_PLAN.md](TEST_PLAN.md).

## Deployment

### Docker Deployment

```bash
# Build image
docker build -t biatec-tokens-api -f BiatecTokensApi/Dockerfile .

# Run container
docker run -p 7000:7000 \
  -e App__Account="<mnemonic>" \
  -e IPFSConfig__Username="<username>" \
  -e IPFSConfig__Password="<password>" \
  -e JwtConfig__SecretKey="<secret>" \
  biatec-tokens-api
```

### Kubernetes Deployment

Kubernetes manifests are available in the `k8s/` directory:

```bash
# Apply configurations
kubectl apply -f k8s/
```

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `App__Account` | Algorand account mnemonic | Yes |
| `IPFSConfig__Username` | IPFS API username | For ARC3 |
| `IPFSConfig__Password` | IPFS API password | For ARC3 |
| `JwtConfig__SecretKey` | JWT signing secret (32+ chars) | For JWT auth |
| `AlgorandAuthentication__AllowedNetworks__0__NetworkId` | Algorand network ID | Yes |
| `AlgorandAuthentication__AllowedNetworks__0__AlgodUrl` | Algorand node URL | Yes |
| `EVMChains__0__ChainId` | EVM chain ID (e.g., 8453 for Base) | For EVM |
| `EVMChains__0__RpcUrl` | EVM RPC endpoint | For EVM |
| `EVMChains__0__PrivateKey` | EVM deployer private key | For EVM |

### Health Checks

- **Liveness**: `GET /health/live` - Always returns 200 if API is running
- **Readiness**: `GET /health/ready` - Returns 200 if all dependencies are healthy
- **Detailed**: `GET /health` - Returns detailed health information

```bash
# Check if API is ready
curl https://api.example.com/health/ready
```

Response:
```json
{
  "status": "Healthy",
  "checks": {
    "ipfs": "Healthy",
    "algorand_mainnet": "Healthy",
    "base_mainnet": "Healthy"
  },
  "timestamp": "2026-02-14T12:00:00Z"
}
```

## Documentation

- **[OPENAPI.md](OPENAPI.md)**: OpenAPI specification and Swagger UI
- **[FRONTEND_INTEGRATION_GUIDE.md](FRONTEND_INTEGRATION_GUIDE.md)**: Frontend integration guide
- **[WEBHOOKS.md](WEBHOOKS.md)**: Comprehensive webhook documentation
- **[JWT_AUTHENTICATION_COMPLETE_GUIDE.md](JWT_AUTHENTICATION_COMPLETE_GUIDE.md)**: JWT authentication guide
- **[ARC76_AUTH_IMPLEMENTATION_SUMMARY.md](ARC76_AUTH_IMPLEMENTATION_SUMMARY.md)**: ARC76 wallet-free authentication
- **[IDEMPOTENCY_IMPLEMENTATION.md](IDEMPOTENCY_IMPLEMENTATION.md)**: Idempotency guide
- **[KYC_IMPLEMENTATION_COMPLETE.md](KYC_IMPLEMENTATION_COMPLETE.md)**: KYC integration guide
- **[COMPLIANCE_IMPLEMENTATION_SUMMARY.md](COMPLIANCE_IMPLEMENTATION_SUMMARY.md)**: MICA compliance guide
- **[KEY_MANAGEMENT_GUIDE.md](KEY_MANAGEMENT_GUIDE.md)**: Key management and security

## Support

- **Issues**: [GitHub Issues](https://github.com/scholtz/BiatecTokensApi/issues)
- **Documentation**: [GitHub Repository](https://github.com/scholtz/BiatecTokensApi)
- **Roadmap**: [Business Owner Roadmap](https://raw.githubusercontent.com/scholtz/biatec-tokens/refs/heads/main/business-owner-roadmap.md)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [.NET 10.0](https://dotnet.microsoft.com/)
- Algorand integration via [Algorand4](https://github.com/scholtz/AlgorandAVM4)
- EVM integration via [Nethereum](https://nethereum.com/)
- ARC-0014 authentication via [AlgorandAuthentication](https://github.com/scholtz/AlgorandAuthentication)
- ARC76 account management via [AlgorandARC76Account](https://github.com/scholtz/AlgorandARC76Account)

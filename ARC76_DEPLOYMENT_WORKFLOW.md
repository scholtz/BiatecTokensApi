# ARC76 Account Management and Token Deployment Workflow

## Overview

This document describes the complete workflow for ARC76 account management and token deployment in the BiatecTokensApi backend. This system enables walletless, email/password-based blockchain operations.

## Architecture

### Core Components

1. **Authentication Service**: Handles user registration, login, and ARC76 account derivation
2. **Key Management System**: Secures user mnemonics using configurable providers
3. **Token Services**: Deploy tokens across multiple blockchain networks
4. **Deployment Status Service**: Tracks deployment lifecycle with 8-state machine
5. **Audit Service**: Records all operations for compliance

### Technology Stack

- **.NET 10.0**: Core framework
- **AlgorandARC76AccountDotNet**: Deterministic account derivation
- **NBitcoin**: BIP39 mnemonic generation
- **AES-256-GCM**: Mnemonic encryption
- **JWT**: Session management
- **Nethereum**: EVM blockchain interaction

## ARC76 Account Lifecycle

### 1. User Registration

```
POST /api/v1/auth/register
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "fullName": "John Doe"
}
```

**Backend Process**:

```
1. Validate password strength (8+ chars, upper/lower/number/special)
2. Check if user already exists
3. Generate 24-word BIP39 mnemonic (256-bit entropy via NBitcoin)
4. Derive deterministic ARC76 account from mnemonic
5. Hash password with BCrypt (work factor: 12)
6. Encrypt mnemonic with system key (via KeyProvider)
7. Store user record in database:
   - UserId (GUID)
   - Email (lowercased)
   - PasswordHash (BCrypt)
   - AlgorandAddress (from ARC76)
   - EncryptedMnemonic (AES-256-GCM)
   - FullName
   - CreatedAt (UTC)
8. Generate JWT access token (15-minute expiry)
9. Generate refresh token (7-day expiry)
10. Return tokens and Algorand address to frontend
```

**Response**:

```json
{
  "success": true,
  "userId": "guid",
  "email": "user@example.com",
  "algorandAddress": "ALGORAND_ADDRESS_HERE",
  "accessToken": "jwt_token",
  "refreshToken": "refresh_token",
  "expiresAt": "2026-02-09T14:50:00Z"
}
```

### 2. User Login

```
POST /api/v1/auth/login
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Backend Process**:

```
1. Retrieve user by email (case-insensitive)
2. Check if account is locked (5 failed attempts = 30-minute lockout)
3. Verify password hash (BCrypt)
4. On success:
   - Reset failed login counter
   - Generate new JWT access token
   - Generate new refresh token (rotate old one)
   - Return tokens and user info
5. On failure:
   - Increment failed login counter
   - Lock account if threshold reached (5 attempts)
   - Return authentication error
```

### 3. Token Refresh

```
POST /api/v1/auth/refresh
{
  "refreshToken": "refresh_token"
}
```

**Backend Process**:

```
1. Validate refresh token format and signature
2. Check if token is expired (7-day TTL)
3. Check if token is revoked (logout)
4. Retrieve user by userId from token
5. Generate new access token
6. Rotate refresh token (invalidate old, issue new)
7. Return new tokens
```

### 4. Logout

```
POST /api/v1/auth/logout
{
  "refreshToken": "refresh_token"
}
```

**Backend Process**:

```
1. Revoke refresh token (mark as invalid)
2. Remove from active sessions
3. Client discards access token
```

## Token Deployment Workflow

### Supported Token Standards

| Standard | Network | Description |
|----------|---------|-------------|
| **ERC20** | Base (Chain ID: 8453) | Fungible tokens on EVM |
| **ASA** | Algorand | Fungible, NFT, Fractional NFT |
| **ARC3** | Algorand | Tokens with IPFS metadata |
| **ARC200** | Algorand | Smart contract tokens |
| **ARC1400** | Algorand | Security tokens with compliance |

### Deployment Flow (Example: ASA Fungible Token)

```
POST /api/v1/token/asa-ft/create
Authorization: Bearer {jwt_token}
{
  "name": "My Token",
  "unitName": "MTK",
  "totalSupply": 1000000,
  "decimals": 6,
  "network": "algorand-testnet"
}
```

**Backend Process**:

```
1. Validate JWT token (check signature, expiry, claims)
2. Extract userId from token claims
3. Validate request parameters:
   - Name (required, max 32 chars)
   - Unit name (required, max 8 chars)
   - Total supply (positive, within limits)
   - Decimals (0-19)
   - Network (supported network)
4. Check subscription tier limits (tokens per month, etc.)
5. Verify idempotency key (prevent duplicate deployments within 24 hours)
6. Retrieve user from database
7. Decrypt user mnemonic using KeyProvider
8. Derive signing account from mnemonic (ARC76)
9. Construct Algorand ASA creation transaction:
   - Sender: user's Algorand address
   - AssetName: provided name
   - UnitName: provided unit name
   - Total: total supply × 10^decimals
   - Decimals: provided decimals
   - Manager/Reserve/Freeze/Clawback: user's address (configurable)
   - Fee: calculated from network
10. Sign transaction with user's private key
11. Submit transaction to Algorand network
12. Create deployment status record (state: Submitted)
13. Return transaction ID and deployment ID
14. Background worker monitors transaction:
    - Poll blockchain every 5 seconds
    - Update status: Pending → Confirmed → Indexed → Completed
    - Trigger webhook on state changes
15. Record audit trail:
    - Timestamp
    - User ID
    - Transaction ID
    - Asset ID (once confirmed)
    - Network
    - Token parameters
```

**Response**:

```json
{
  "success": true,
  "deploymentId": "guid",
  "transactionId": "TXID_HERE",
  "assetId": 12345678,
  "creatorAddress": "ALGORAND_ADDRESS",
  "confirmedRound": 29876543,
  "status": "Submitted",
  "correlationId": "correlation-guid"
}
```

### Deployment State Machine

```
┌─────────┐
│ Queued  │ (Initial state, waiting for processing)
└────┬────┘
     │
     v
┌──────────┐
│Submitted │ (Transaction sent to blockchain)
└────┬─────┘
     │
     v
┌─────────┐
│ Pending │ (Transaction in mempool)
└────┬────┘
     │
     ├─────> [Network Error] ──> Retry or Failed
     │
     v
┌───────────┐
│ Confirmed │ (Transaction included in block)
└─────┬─────┘
      │
      v
┌─────────┐
│ Indexed │ (Transaction indexed by explorer)
└────┬────┘
     │
     v
┌───────────┐
│ Completed │ (Deployment fully successful)
└───────────┘

Alternative Paths:
┌──────────┐
│  Failed  │ (Permanent error)
└──────────┘

┌───────────┐
│ Cancelled │ (User-initiated cancellation)
└───────────┘
```

### Status Polling

```
GET /api/v1/deployment/{deploymentId}/status
Authorization: Bearer {jwt_token}
```

**Response**:

```json
{
  "deploymentId": "guid",
  "status": "Confirmed",
  "transactionId": "TXID_HERE",
  "assetId": 12345678,
  "confirmedRound": 29876543,
  "network": "algorand-testnet",
  "tokenStandard": "ASA",
  "createdAt": "2026-02-09T14:30:00Z",
  "updatedAt": "2026-02-09T14:30:45Z",
  "errorMessage": null
}
```

## Multi-Network Support

### Algorand Networks

| Network | Genesis Hash | RPC Endpoint |
|---------|--------------|--------------|
| Mainnet | wGHE2Pwdvd7... | https://mainnet-api.4160.nodely.dev |
| Testnet | SGO1GKSzyE7... | https://testnet-api.4160.nodely.dev |
| Betanet | mFgazF+2uRS5... | https://betanet-api.algonode.cloud |
| VOI Mainnet | - | Custom RPC |
| Aramid Mainnet | - | Custom RPC |

### EVM Networks

| Network | Chain ID | RPC Endpoint |
|---------|----------|--------------|
| Base Mainnet | 8453 | https://mainnet.base.org |
| Base Testnet | 84532 | https://testnet.base.org |

### Network Configuration

Configured in `appsettings.json`:

```json
{
  "EVMChains": [
    {
      "RpcUrl": "https://mainnet.base.org",
      "ChainId": 8453,
      "GasLimit": 4500000
    }
  ],
  "AlgorandAuthentication": {
    "AllowedNetworks": {
      "wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=": {
        "Server": "https://mainnet-api.4160.nodely.dev",
        "Token": "",
        "Header": ""
      }
    }
  }
}
```

## Security Architecture

### Mnemonic Protection

1. **Generation**: 24-word BIP39 mnemonic using NBitcoin (256-bit entropy)
2. **Encryption**: AES-256-GCM with system key from KeyProvider
3. **Storage**: Encrypted blob in database (never plain text)
4. **Decryption**: On-demand for transaction signing only
5. **Memory**: Zeroed after use (sensitive data cleared)

### Key Provider Security

- **Environment Variable**: OS-level secrets management
- **Azure Key Vault**: FIPS 140-2 Level 2 validated HSMs
- **AWS KMS**: FIPS 140-2 Level 2 validated HSMs
- **Hardcoded**: Development only, generates warnings

### JWT Security

- **Algorithm**: HS256 (HMAC-SHA256)
- **Secret Key**: 64-byte random key from configuration
- **Access Token**: 15-minute expiry
- **Refresh Token**: 7-day expiry with rotation
- **Claims**: userId, email, algorandAddress

### Password Security

- **Hashing**: BCrypt with work factor 12
- **Requirements**: 8+ chars, upper/lower/number/special
- **Lockout**: 5 failed attempts = 30-minute lockout
- **Reset**: Secure password reset flow (not implemented in MVP)

## Idempotency

### Token Deployment

- **Idempotency Key**: Hash of (userId + tokenParams + timestamp)
- **Cache Duration**: 24 hours
- **Behavior**: Return cached deployment ID if duplicate request within window
- **Purpose**: Prevent accidental duplicate token creation

### Request Validation

```
Request 1: POST /api/v1/token/asa-ft/create {...}
Response: { deploymentId: "guid-1", transactionId: "tx-1" }

Request 2: Same parameters within 24 hours
Response: { deploymentId: "guid-1", transactionId: "tx-1" } (cached)

Request 3: Same parameters after 24 hours
Response: { deploymentId: "guid-2", transactionId: "tx-2" } (new deployment)
```

## Audit Trail

### Captured Events

1. **Authentication Events**:
   - User registration (email, Algorand address, timestamp)
   - Login success/failure (IP address, user agent)
   - Token refresh
   - Logout

2. **Deployment Events**:
   - Token creation request (parameters, subscription tier)
   - Transaction submission (transaction ID, network)
   - Status changes (state transitions)
   - Completion or failure (asset ID, error message)

3. **Security Events**:
   - Failed login attempts
   - Account lockouts
   - Key access (from KeyProvider)
   - Invalid JWT tokens

### Audit Log Format

```json
{
  "eventType": "TokenDeployment",
  "timestamp": "2026-02-09T14:30:00Z",
  "userId": "guid",
  "email": "user@example.com",
  "deploymentId": "guid",
  "transactionId": "tx-id",
  "network": "algorand-testnet",
  "tokenStandard": "ASA",
  "assetId": 12345678,
  "status": "Completed",
  "correlationId": "correlation-guid"
}
```

### Retention Policy

- **Duration**: 7 years (configurable)
- **Export**: JSON and CSV formats
- **Access**: Authenticated API endpoints with admin role

## Error Handling

### Error Codes

- **AUTH001**: Invalid credentials
- **AUTH002**: Account locked (too many failed attempts)
- **AUTH003**: Expired token
- **AUTH004**: Invalid token
- **DEPLOY001**: Invalid parameters
- **DEPLOY002**: Insufficient balance
- **DEPLOY003**: Network error (transient, retry)
- **DEPLOY004**: Transaction rejected by network
- **KEY001**: Encryption key not found
- **KEY002**: Decryption failed

### Error Response Format

```json
{
  "success": false,
  "errorCode": "DEPLOY001",
  "errorMessage": "Token name must be between 1 and 32 characters",
  "correlationId": "correlation-guid",
  "timestamp": "2026-02-09T14:30:00Z"
}
```

### Retry Logic

- **Transient Errors**: Automatic retry with exponential backoff
- **Network Timeouts**: Retry up to 3 times
- **Gas Estimation Failures**: Retry with increased gas limit
- **Permanent Errors**: No retry, mark as Failed

## Monitoring and Observability

### Key Metrics

1. **Authentication**:
   - Registration rate
   - Login success rate
   - Account lockout frequency
   - Token refresh frequency

2. **Deployment**:
   - Deployment success rate
   - Average deployment time
   - Failed deployments by error code
   - Deployments by network/standard

3. **Performance**:
   - API response time (p50, p95, p99)
   - Database query time
   - Blockchain RPC latency
   - Key provider access time

### Health Checks

```
GET /health - Basic health check
GET /health/ready - Readiness probe (checks dependencies)
GET /health/live - Liveness probe (checks if app is running)
```

### Log Levels

- **Debug**: Detailed diagnostic information
- **Information**: General application flow
- **Warning**: Potential issues (e.g., hardcoded key provider)
- **Error**: Errors that prevented operation completion
- **Critical**: System failures requiring immediate attention

## Scalability

### Horizontal Scaling

- **Stateless Design**: No in-memory state, all data in database
- **Load Balancing**: Any instance can handle any request
- **Concurrency**: Thread-safe operations with database locks

### Database Optimization

- **Indexes**: On userId, email, deploymentId, transactionId
- **Connection Pooling**: Reuse database connections
- **Query Optimization**: Minimize N+1 queries

### Background Processing

- **Transaction Monitoring**: Separate worker process
- **Polling Interval**: 5 seconds (configurable)
- **Batch Processing**: Monitor multiple deployments in parallel

## Integration Guide

### Frontend Integration

See `FRONTEND_INTEGRATION_GUIDE.md` for:
- Authentication flow implementation
- Token deployment UI
- Status polling and webhooks
- Error handling

### API Documentation

- **Swagger UI**: Available at `/swagger` endpoint
- **OpenAPI Spec**: Generated from XML documentation
- **Examples**: Request/response samples for all endpoints

## Testing

### Test Coverage

- **Unit Tests**: 1,384+ tests (99% coverage)
- **Integration Tests**: End-to-end deployment flows
- **Security Tests**: CodeQL scanning, vulnerability checks

### Test Execution

```bash
# Run all tests
dotnet test --configuration Release

# Run specific category
dotnet test --filter "FullyQualifiedName~AuthenticationService"
dotnet test --filter "FullyQualifiedName~KeyProvider"

# Exclude real endpoint tests
dotnet test --filter "FullyQualifiedName!~RealEndpoint"
```

## Troubleshooting

### Common Issues

**Issue**: "Encryption key not found in environment variable"
**Solution**: Set `BIATEC_ENCRYPTION_KEY` environment variable (see KEY_MANAGEMENT_GUIDE.md)

**Issue**: "Account locked due to too many failed login attempts"
**Solution**: Wait 30 minutes or contact support to unlock account

**Issue**: "Transaction failed: insufficient balance"
**Solution**: Fund the user's Algorand address with ALGO or Base address with ETH

**Issue**: "Deployment stuck in Pending state"
**Solution**: Check blockchain network status, transaction may be in congestion

## Production Checklist

- [ ] Set up KeyProvider (Environment Variable, Azure Key Vault, or AWS KMS)
- [ ] Configure JWT secret key (64-byte random value)
- [ ] Set up database with proper indexes
- [ ] Configure CORS for frontend domain
- [ ] Enable HTTPS/TLS
- [ ] Set up monitoring and alerting
- [ ] Configure audit log retention
- [ ] Test backup and recovery
- [ ] Review security settings
- [ ] Load test with expected traffic
- [ ] Document incident response procedures

## Support and References

- **Repository**: https://github.com/scholtz/BiatecTokensApi
- **Documentation**: See `/docs` directory
- **Key Management**: See `KEY_MANAGEMENT_GUIDE.md`
- **Frontend Integration**: See `FRONTEND_INTEGRATION_GUIDE.md`
- **API Documentation**: Available at `/swagger` endpoint

## Version History

- **v1.0** (2026-02-09): Initial release with ARC76 authentication, key management system, and multi-standard token deployment
